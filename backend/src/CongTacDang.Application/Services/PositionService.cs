using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Danh mục chức vụ, chức vụ (kể cả kiêm nhiệm) của cán bộ và thẩm quyền phê duyệt suy ra (task 14).
/// </summary>
public interface IPositionService
{
    /// <summary>Danh mục chức vụ (mọi người đã đăng nhập).</summary>
    Task<List<PositionDto>> GetPositionsAsync(CancellationToken ct = default);

    /// <summary>Thêm chức vụ (<c>catalog.manage</c> ở controller).</summary>
    Task<PositionDto> CreatePositionAsync(SavePositionDto input, CancellationToken ct = default);

    /// <summary>Sửa chức vụ; đổi thẩm quyền mặc định → tính lại thẩm quyền của người đang giữ.</summary>
    Task<PositionDto> UpdatePositionAsync(Guid id, SavePositionDto input, CancellationToken ct = default);

    /// <summary>Xóa chức vụ (409 khi đã có người được gán — hãy ngừng dùng).</summary>
    Task DeletePositionAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chức vụ của cán bộ (<c>system.users.read</c> trong phạm vi của cán bộ).</summary>
    Task<List<MemberPositionDto>> GetMemberPositionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Thêm chức vụ cho cán bộ (<c>system.users.manage</c> trong phạm vi của cán bộ).</summary>
    Task<MemberPositionDto> AddMemberPositionAsync(Guid userId, SaveMemberPositionDto input, CancellationToken ct = default);

    /// <summary>Sửa chức vụ của cán bộ (đơn vị, chính/kiêm nhiệm, thời hạn, ghi chú).</summary>
    Task<MemberPositionDto> UpdateMemberPositionAsync(Guid userId, Guid id, SaveMemberPositionDto input, CancellationToken ct = default);

    /// <summary>Kết thúc chức vụ ngay bây giờ (ValidTo = hiện tại).</summary>
    Task<MemberPositionDto> EndMemberPositionAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>Xóa (mềm) bản ghi chức vụ nhập nhầm.</summary>
    Task DeleteMemberPositionAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>Thẩm quyền phê duyệt của cán bộ: đang áp dụng, suy ra, đặt tay, mã thống kê.</summary>
    Task<ApprovalAuthorityInfoDto> GetApprovalAuthorityAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đặt tay (có lý do) hoặc bỏ đặt tay thẩm quyền phê duyệt.</summary>
    Task<ApprovalAuthorityInfoDto> SetApprovalAuthorityOverrideAsync(Guid userId, SetApprovalAuthorityOverrideDto input, CancellationToken ct = default);

    /// <summary>
    /// Tính lại thẩm quyền đang áp dụng của mọi cán bộ có chức vụ và không đặt tay (chức vụ có thời hạn bắt đầu/kết thúc
    /// theo thời gian). Trả số hồ sơ thay đổi. Dùng cho tác vụ nền, không kiểm tra quyền.
    /// </summary>
    Task<int> RecomputeAllApprovalAuthoritiesAsync(CancellationToken ct = default);
}

/// <summary>Triển khai <see cref="IPositionService"/>.</summary>
public sealed class PositionService : IPositionService
{
    private const int MaxNoteLength = 1000;

    private readonly IPositionRepository _repo;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo dịch vụ.</summary>
    public PositionService(IPositionRepository repo, IAuthorizationGuard guard, ICurrentUserService currentUser, TimeProvider? time = null)
    {
        _repo = repo;
        _guard = guard;
        _currentUser = currentUser;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ===================== Danh mục chức vụ =====================

    /// <inheritdoc />
    public async Task<List<PositionDto>> GetPositionsAsync(CancellationToken ct = default)
    {
        var positions = await _repo.ListPositionsAsync(ct);
        var holders = await _repo.CountHoldersAsync(Now, ct);
        return positions.Select(p => ToDto(p, holders.GetValueOrDefault(p.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<PositionDto> CreatePositionAsync(SavePositionDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var name = ValidatePositionName(input.Name);
        if (!TryParseSide(input.Side, out var side))
            throw new ValidationException("Hãy chọn bên của chức vụ: Đảng, Chính quyền, Đoàn thể hoặc Khác.");
        if (await _repo.PositionNameExistsAsync(name, null, ct))
            throw new ConflictException($"Chức vụ \"{name}\" đã có trong danh mục. Hãy dùng tên khác.");

        var position = new Position
        {
            Name = name,
            Side = side,
            StatCode = ParseStatCode(input.StatCode),
            DefaultApprovalAuthority = ParseAuthority(input.DefaultApprovalAuthority),
            IsLeadership = input.IsLeadership ?? false,
            SortOrder = input.SortOrder ?? 0,
            IsActive = input.IsActive ?? true
        };
        _repo.AddPosition(position);
        await _repo.SaveChangesAsync(ct);
        return ToDto(position, 0);
    }

    /// <inheritdoc />
    public async Task<PositionDto> UpdatePositionAsync(Guid id, SavePositionDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var position = await _repo.FindPositionAsync(id, ct)
            ?? throw new NotFoundException("Không tìm thấy chức vụ cần cập nhật. Hãy tải lại danh sách.");

        if (input.Name != null)
        {
            var name = ValidatePositionName(input.Name);
            if (!string.Equals(name, position.Name, StringComparison.OrdinalIgnoreCase)
                && await _repo.PositionNameExistsAsync(name, id, ct))
                throw new ConflictException($"Chức vụ \"{name}\" đã có trong danh mục. Hãy dùng tên khác.");
            position.Name = name;
        }

        if (input.Side != null)
        {
            if (!TryParseSide(input.Side, out var side))
                throw new ValidationException("Bên của chức vụ không hợp lệ. Hãy chọn Đảng, Chính quyền, Đoàn thể hoặc Khác.");
            position.Side = side;
        }

        if (input.StatCode != null) position.StatCode = ParseStatCode(input.StatCode);
        var authorityChanged = false;
        if (input.DefaultApprovalAuthority != null)
        {
            var authority = ParseAuthority(input.DefaultApprovalAuthority);
            authorityChanged = authority != position.DefaultApprovalAuthority;
            position.DefaultApprovalAuthority = authority;
        }
        if (input.IsLeadership.HasValue) position.IsLeadership = input.IsLeadership.Value;
        if (input.SortOrder.HasValue) position.SortOrder = input.SortOrder.Value;
        if (input.IsActive.HasValue) position.IsActive = input.IsActive.Value;
        await _repo.SaveChangesAsync(ct);

        if (authorityChanged)
            await RecomputeAllApprovalAuthoritiesAsync(ct);

        var holders = await _repo.CountHoldersAsync(Now, ct);
        return ToDto(position, holders.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public async Task DeletePositionAsync(Guid id, CancellationToken ct = default)
    {
        var position = await _repo.FindPositionAsync(id, ct)
            ?? throw new NotFoundException("Không tìm thấy chức vụ cần xóa. Hãy tải lại danh sách.");
        var used = await _repo.CountMemberPositionsAsync(id, ct);
        if (used > 0)
            throw new ConflictException(
                $"Không thể xóa chức vụ \"{position.Name}\" vì đã được gán {used} lần cho cán bộ (kể cả đã kết thúc) — cần giữ để tra cứu lịch sử. "
                + "Hãy chuyển chức vụ sang \"Ngừng dùng\".");
        _repo.RemovePosition(position);
        await _repo.SaveChangesAsync(ct);
    }

    // ===================== Chức vụ của cán bộ =====================

    /// <inheritdoc />
    public async Task<List<MemberPositionDto>> GetMemberPositionsAsync(Guid userId, CancellationToken ct = default)
    {
        var member = await LoadMemberAsync(userId, ct);
        _guard.Ensure(PermissionCodes.SystemUsersRead, TargetOf(member));
        var now = Now;
        return (await _repo.ListMemberPositionsAsync(userId, ct)).Select(mp => ToDto(mp, now)).ToList();
    }

    /// <inheritdoc />
    public async Task<MemberPositionDto> AddMemberPositionAsync(Guid userId, SaveMemberPositionDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var member = await LoadMemberAsync(userId, ct);
        EnsureCanManage(member);

        if (!input.PositionId.HasValue || input.PositionId.Value == Guid.Empty)
            throw new ValidationException("Hãy chọn chức vụ trong danh mục.");
        var position = await _repo.FindPositionAsync(input.PositionId.Value, ct)
            ?? throw new ValidationException("Chức vụ đã chọn không có trong danh mục (có thể đã bị xóa). Hãy chọn chức vụ khác.");
        if (!position.IsActive)
            throw new ValidationException($"Chức vụ \"{position.Name}\" đã ngừng dùng. Hãy chọn chức vụ khác.");

        var partyCellId = EmptyToNull(input.PartyCellId);
        var departmentId = EmptyToNull(input.DepartmentId);
        await ValidateUnitAsync(position, partyCellId, departmentId, ct);

        var validFrom = ToUtc(input.ValidFrom) ?? Now;
        var validTo = ToUtc(input.ValidTo);
        ValidatePeriod(validFrom, validTo);

        var all = await _repo.ListMemberPositionsForUpdateAsync(userId, ct);
        var entity = new MemberPosition
        {
            UserId = userId,
            PositionId = position.Id,
            Position = position,
            PartyCellId = partyCellId,
            DepartmentId = departmentId,
            IsPrimary = input.IsPrimary ?? !all.Any(mp => mp.IsPrimary && mp.IsEffectiveAt(Now)),
            ValidFrom = validFrom,
            ValidTo = validTo,
            Note = NormalizeNote(input.Note)
        };
        _repo.AddMemberPosition(entity);
        all.Add(entity);

        ApplyPrimary(member, entity, all);
        Recompute(member, all);
        await _repo.SaveChangesAsync(ct);
        return ToDto(entity, Now);
    }

    /// <inheritdoc />
    public async Task<MemberPositionDto> UpdateMemberPositionAsync(Guid userId, Guid id, SaveMemberPositionDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var member = await LoadMemberAsync(userId, ct);
        EnsureCanManage(member);
        var all = await _repo.ListMemberPositionsForUpdateAsync(userId, ct);
        var entity = all.FirstOrDefault(mp => mp.Id == id)
            ?? throw new NotFoundException("Không tìm thấy chức vụ cần sửa của cán bộ này. Hãy tải lại trang.");

        if (input.PositionId.HasValue && input.PositionId.Value != entity.PositionId)
            throw new ValidationException("Không đổi được chức vụ của một bản ghi. Hãy kết thúc bản ghi này và thêm chức vụ mới.");

        var partyCellId = input.PartyCellId.HasValue ? EmptyToNull(input.PartyCellId) : entity.PartyCellId;
        var departmentId = input.DepartmentId.HasValue ? EmptyToNull(input.DepartmentId) : entity.DepartmentId;
        if (partyCellId != entity.PartyCellId || departmentId != entity.DepartmentId)
            await ValidateUnitAsync(entity.Position!, partyCellId, departmentId, ct);

        var validFrom = ToUtc(input.ValidFrom) ?? entity.ValidFrom;
        var validTo = input.ClearValidTo == true ? null : ToUtc(input.ValidTo) ?? entity.ValidTo;
        ValidatePeriod(validFrom, validTo);

        entity.PartyCellId = partyCellId;
        entity.DepartmentId = departmentId;
        entity.ValidFrom = validFrom;
        entity.ValidTo = validTo;
        if (input.Note != null) entity.Note = NormalizeNote(input.Note);
        if (input.IsPrimary.HasValue) entity.IsPrimary = input.IsPrimary.Value;

        ApplyPrimary(member, entity, all);
        Recompute(member, all);
        await _repo.SaveChangesAsync(ct);
        return ToDto(entity, Now);
    }

    /// <inheritdoc />
    public async Task<MemberPositionDto> EndMemberPositionAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var member = await LoadMemberAsync(userId, ct);
        EnsureCanManage(member);
        var all = await _repo.ListMemberPositionsForUpdateAsync(userId, ct);
        var entity = all.FirstOrDefault(mp => mp.Id == id)
            ?? throw new NotFoundException("Không tìm thấy chức vụ cần kết thúc của cán bộ này. Hãy tải lại trang.");

        var now = Now;
        if (entity.ValidTo.HasValue && entity.ValidTo.Value <= now)
            throw new ConflictException("Chức vụ này đã kết thúc trước đó.");
        entity.ValidTo = now < entity.ValidFrom ? entity.ValidFrom : now;
        if (entity.IsPrimary)
        {
            entity.IsPrimary = false;
            // Chức vụ chính tiếp theo: chức vụ đang hiệu lực có thứ tự đứng trước.
            var next = all.Where(mp => mp.Id != entity.Id && mp.IsEffectiveAt(now))
                .OrderBy(mp => mp.Position?.SortOrder ?? int.MaxValue).FirstOrDefault();
            if (next != null)
            {
                next.IsPrimary = true;
                ApplyPrimary(member, next, all);
            }
        }

        Recompute(member, all);
        await _repo.SaveChangesAsync(ct);
        return ToDto(entity, now);
    }

    /// <inheritdoc />
    public async Task DeleteMemberPositionAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var member = await LoadMemberAsync(userId, ct);
        EnsureCanManage(member);
        var all = await _repo.ListMemberPositionsForUpdateAsync(userId, ct);
        var entity = all.FirstOrDefault(mp => mp.Id == id)
            ?? throw new NotFoundException("Không tìm thấy chức vụ cần xóa của cán bộ này. Hãy tải lại trang.");

        _repo.RemoveMemberPosition(entity);
        all.Remove(entity);
        Recompute(member, all);
        await _repo.SaveChangesAsync(ct);
    }

    // ===================== Thẩm quyền phê duyệt =====================

    /// <inheritdoc />
    public async Task<ApprovalAuthorityInfoDto> GetApprovalAuthorityAsync(Guid userId, CancellationToken ct = default)
    {
        var member = await LoadMemberAsync(userId, ct);
        _guard.Ensure(PermissionCodes.SystemUsersRead, TargetOf(member));
        return await BuildInfoAsync(member, ct);
    }

    /// <inheritdoc />
    public async Task<ApprovalAuthorityInfoDto> SetApprovalAuthorityOverrideAsync(
        Guid userId, SetApprovalAuthorityOverrideDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var member = await LoadMemberAsync(userId, ct);
        EnsureCanManage(member);

        var manual = ParseAuthority(input.Override);
        if (manual.HasValue)
        {
            var reason = input.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
                throw new ValidationException("Hãy nhập lý do đặt tay thẩm quyền phê duyệt (ví dụ: căn cứ văn bản phân cấp).");
            if (reason.Length > MaxNoteLength)
                throw new ValidationException($"Lý do dài quá {MaxNoteLength} ký tự. Hãy rút ngắn.");
            member.ApprovalAuthorityOverride = manual;
            member.ApprovalAuthorityOverrideReason = reason;
        }
        else
        {
            member.ApprovalAuthorityOverride = null;
            member.ApprovalAuthorityOverrideReason = null;
        }

        var all = await _repo.ListMemberPositionsForUpdateAsync(userId, ct);
        Recompute(member, all);
        await _repo.SaveChangesAsync(ct);
        return await BuildInfoAsync(member, ct);
    }

    /// <inheritdoc />
    public async Task<int> RecomputeAllApprovalAuthoritiesAsync(CancellationToken ct = default)
    {
        var members = await _repo.ListMembersWithDerivedAuthorityAsync(ct);
        if (members.Count == 0)
            return 0;
        var held = await _repo.GetHeldPositionsAsync(members.Select(m => m.Id).ToList(), Now, ct);
        var changed = 0;
        foreach (var member in members)
        {
            var derived = PositionRules.DeriveApprovalAuthority(held.GetValueOrDefault(member.Id) ?? new List<HeldPosition>());
            if (member.ApprovalAuthority != derived)
            {
                member.ApprovalAuthority = derived;
                changed++;
            }
        }

        if (changed > 0)
            await _repo.SaveChangesAsync(ct);
        return changed;
    }

    // ===================== Hỗ trợ =====================

    /// <summary>Tính lại thẩm quyền đang áp dụng từ các bản ghi chức vụ (đã theo dõi) của cán bộ.</summary>
    private void Recompute(PartyMemberProfile member, IEnumerable<MemberPosition> all)
    {
        var now = Now;
        var held = all.Where(mp => mp.IsEffectiveAt(now) && mp.Position != null)
            .Select(mp => new HeldPosition(mp.Position!.Name, mp.Position.StatCode, mp.Position.DefaultApprovalAuthority));
        member.ApprovalAuthority = PositionRules.EffectiveApprovalAuthority(member.ApprovalAuthorityOverride, held);
    }

    /// <summary>
    /// Chức vụ chính duy nhất: đặt <paramref name="entity"/> làm chính → bỏ cờ chính ở các bản ghi khác; chức danh hiển thị
    /// (<see cref="PartyMemberProfile.PositionTitle"/>) theo chức vụ chính nếu đang để mặc định.
    /// </summary>
    private static void ApplyPrimary(PartyMemberProfile member, MemberPosition entity, IEnumerable<MemberPosition> all)
    {
        if (!entity.IsPrimary)
            return;
        var previousNames = new List<string>();
        foreach (var other in all.Where(mp => mp.Id != entity.Id && mp.IsPrimary))
        {
            other.IsPrimary = false;
            if (other.Position != null)
                previousNames.Add(other.Position.Name);
        }

        var title = member.PositionTitle?.Trim() ?? string.Empty;
        var isDefaultTitle = title.Length == 0 || title == "Cán bộ" || previousNames.Contains(title, StringComparer.OrdinalIgnoreCase);
        if (isDefaultTitle && entity.Position != null)
            member.PositionTitle = entity.Position.Name;
    }

    private async Task<ApprovalAuthorityInfoDto> BuildInfoAsync(PartyMemberProfile member, CancellationToken ct)
    {
        var held = (await _repo.GetHeldPositionsAsync(new[] { member.Id }, Now, ct)).GetValueOrDefault(member.Id)
            ?? new List<HeldPosition>();
        return new ApprovalAuthorityInfoDto
        {
            Effective = member.ApprovalAuthority.ToString(),
            Derived = PositionRules.DeriveApprovalAuthority(held).ToString(),
            Override = member.ApprovalAuthorityOverride?.ToString(),
            OverrideReason = member.ApprovalAuthorityOverrideReason,
            CapTrenPositions = held.Where(h => h.DefaultApprovalAuthority == ApprovalAuthority.CapTren).Select(h => h.PositionName).ToList(),
            StatCode = PositionRules.PersonStatCode(held)
        };
    }

    private async Task ValidateUnitAsync(Position position, Guid? partyCellId, Guid? departmentId, CancellationToken ct)
    {
        if (partyCellId.HasValue && departmentId.HasValue)
            throw new ValidationException("Mỗi chức vụ chỉ giữ tại một đơn vị: chọn tổ chức Đảng hoặc đơn vị chính quyền, không chọn cả hai.");
        if (position.Side == PositionSide.Party && departmentId.HasValue)
            throw new ValidationException($"\"{position.Name}\" là chức vụ Đảng — hãy chọn tổ chức Đảng nơi giữ chức vụ, không chọn đơn vị chính quyền.");
        if (position.Side == PositionSide.Administrative && partyCellId.HasValue)
            throw new ValidationException($"\"{position.Name}\" là chức vụ chính quyền — hãy chọn đơn vị chính quyền nơi giữ chức vụ, không chọn tổ chức Đảng.");
        if (partyCellId.HasValue && !await _repo.PartyCellIsActiveAsync(partyCellId.Value, ct))
            throw new ValidationException("Tổ chức Đảng đã chọn không tồn tại hoặc đã ngừng hoạt động. Hãy chọn tổ chức khác.");
        if (departmentId.HasValue && !await _repo.DepartmentIsActiveAsync(departmentId.Value, ct))
            throw new ValidationException("Đơn vị đã chọn không tồn tại hoặc đã ngừng hoạt động. Hãy chọn đơn vị khác.");
    }

    private static void ValidatePeriod(DateTime validFrom, DateTime? validTo)
    {
        if (validTo.HasValue && validTo.Value <= validFrom)
            throw new ValidationException("Ngày hết hiệu lực phải sau ngày bắt đầu. Hãy kiểm tra lại thời hạn giữ chức vụ.");
    }

    private void EnsureCanManage(PartyMemberProfile member)
    {
        if (_currentUser.UserId == null)
            return; // tác vụ hệ thống
        _guard.Ensure(PermissionCodes.SystemUsersManage, TargetOf(member));
    }

    private async Task<PartyMemberProfile> LoadMemberAsync(Guid userId, CancellationToken ct)
        => await _repo.FindMemberAsync(userId, ct)
            ?? throw new NotFoundException("Không tìm thấy tài khoản cán bộ (có thể đã bị xóa). Hãy tải lại danh sách.");

    private static AccessTarget TargetOf(PartyMemberProfile member) => new(DepartmentId: member.DepartmentId, PartyCellId: member.PartyCellId);

    private static string ValidatePositionName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new ValidationException("Tên chức vụ không được để trống. Hãy nhập tên chức vụ.");
        if (name.Length > PositionRules.MaxNameLength)
            throw new ValidationException($"Tên chức vụ dài quá {PositionRules.MaxNameLength} ký tự. Hãy rút ngắn tên.");
        return name;
    }

    private static string? ParseStatCode(string? value)
    {
        try
        {
            return PositionRules.NormalizeStatCode(value);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }
    }

    /// <summary>Đọc thẩm quyền: rỗng → null; <c>CoSo</c>/<c>CapTren</c> (không phân biệt hoa thường); khác → 400.</summary>
    public static ApprovalAuthority? ParseAuthority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Enum.TryParse<ApprovalAuthority>(value.Trim(), ignoreCase: true, out var authority) && Enum.IsDefined(authority))
            return authority;
        throw new ValidationException($"Thẩm quyền \"{value.Trim()}\" không hợp lệ. Chỉ nhận CoSo (Đảng ủy cơ sở) hoặc CapTren (cấp trên).");
    }

    /// <summary>Đọc bên chức vụ (<c>Party</c>/<c>Administrative</c>/<c>MassOrganization</c>/<c>Other</c>).</summary>
    public static bool TryParseSide(string? value, out PositionSide side)
        => Enum.TryParse(value?.Trim(), ignoreCase: true, out side) && Enum.IsDefined(side);

    private static string? NormalizeNote(string? note)
    {
        var value = note?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;
        if (value.Length > MaxNoteLength)
            throw new ValidationException($"Ghi chú dài quá {MaxNoteLength} ký tự. Hãy rút ngắn.");
        return value;
    }

    private static Guid? EmptyToNull(Guid? id) => id.HasValue && id.Value != Guid.Empty ? id : null;

    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;
        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static PositionDto ToDto(Position p, int holders) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Side = p.Side.ToString(),
        StatCode = p.StatCode,
        DefaultApprovalAuthority = p.DefaultApprovalAuthority?.ToString(),
        IsLeadership = p.IsLeadership,
        SortOrder = p.SortOrder,
        IsActive = p.IsActive,
        HolderCount = holders
    };

    private static MemberPositionDto ToDto(MemberPosition mp, DateTime now) => new()
    {
        Id = mp.Id,
        UserId = mp.UserId,
        PositionId = mp.PositionId,
        PositionName = mp.Position?.Name ?? string.Empty,
        Side = mp.Position?.Side.ToString() ?? string.Empty,
        StatCode = mp.Position?.StatCode,
        DefaultApprovalAuthority = mp.Position?.DefaultApprovalAuthority?.ToString(),
        PartyCellId = mp.PartyCellId,
        PartyCellName = mp.PartyCell?.Name,
        DepartmentId = mp.DepartmentId,
        DepartmentName = mp.Department?.Name,
        IsPrimary = mp.IsPrimary,
        ValidFrom = mp.ValidFrom,
        ValidTo = mp.ValidTo,
        IsEffective = mp.IsEffectiveAt(now),
        Note = mp.Note
    };
}
