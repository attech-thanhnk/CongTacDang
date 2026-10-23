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

namespace CongTacDang.Application.Services;

/// <summary>
/// Danh mục kiểu trình bày biểu mẫu tự đánh giá (quyền quản lý danh mục kiểm tra ở controller) và kiểu áp dụng cho cán bộ
/// (theo chức vụ, đặt riêng được).
/// </summary>
public interface IFormLayoutService
{
    /// <summary>Danh mục kiểu (mọi người đã đăng nhập).</summary>
    Task<List<FormLayoutDto>> GetAllAsync(CancellationToken ct = default);

    Task<FormLayoutDto> CreateAsync(SaveFormLayoutDto input, CancellationToken ct = default);

    Task<FormLayoutDto> UpdateAsync(Guid id, SaveFormLayoutDto input, CancellationToken ct = default);

    /// <summary>Xóa kiểu (409 khi còn chức vụ hoặc cán bộ dùng).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kiểu của cán bộ: đang áp dụng, suy ra, đặt riêng (<c>system.users.read</c> trong phạm vi của cán bộ).</summary>
    Task<MemberFormLayoutDto> GetMemberLayoutAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đặt riêng / bỏ đặt riêng kiểu cho cán bộ (<c>system.users.manage</c> trong phạm vi của cán bộ).</summary>
    Task<MemberFormLayoutDto> SetMemberLayoutAsync(Guid userId, SetMemberFormLayoutDto input, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class FormLayoutService : IFormLayoutService
{
    private readonly IFormLayoutRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly TimeProvider _time;
    private readonly ICurrentUserService? _currentUser;

    public FormLayoutService(IFormLayoutRepository repo, IUnitOfWork unitOfWork, IAuthorizationGuard guard, TimeProvider? time = null,
        ICurrentUserService? currentUser = null)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _time = time ?? TimeProvider.System;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<List<FormLayoutDto>> GetAllAsync(CancellationToken ct = default)
    {
        var layouts = await _repo.ListAsync(ct);
        var positions = await _repo.CountPositionsAsync(ct);
        var members = await _repo.CountMembersAsync(ct);
        return layouts.Select(l => ToDto(l, positions.GetValueOrDefault(l.Id), members.GetValueOrDefault(l.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<FormLayoutDto> CreateAsync(SaveFormLayoutDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var layout = new FormLayout();
        await ApplyAsync(layout, input, null, ct);
        _repo.Add(layout);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(layout, 0, 0);
    }

    /// <inheritdoc />
    public async Task<FormLayoutDto> UpdateAsync(Guid id, SaveFormLayoutDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var layout = await _repo.FindAsync(id, ct)
            ?? throw new NotFoundException("Không tìm thấy kiểu trình bày cần sửa. Hãy tải lại danh sách.");
        await ApplyAsync(layout, input, id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(layout, (await _repo.CountPositionsAsync(ct)).GetValueOrDefault(id), (await _repo.CountMembersAsync(ct)).GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var layout = await _repo.FindAsync(id, ct)
            ?? throw new NotFoundException("Không tìm thấy kiểu trình bày cần xóa. Hãy tải lại danh sách.");
        var positions = (await _repo.CountPositionsAsync(ct)).GetValueOrDefault(id);
        var members = (await _repo.CountMembersAsync(ct)).GetValueOrDefault(id);
        if (positions > 0 || members > 0)
            throw new ConflictException(
                $"Không thể xóa kiểu \"{layout.Name}\" vì đang được dùng ({positions} chức vụ, {members} cán bộ đặt riêng). "
                + "Hãy chuyển các chức vụ, cán bộ đó sang kiểu khác trước.");
        _repo.Remove(layout);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<MemberFormLayoutDto> GetMemberLayoutAsync(Guid userId, CancellationToken ct = default)
    {
        var member = await LoadMemberAsync(userId, ct);
        _guard.Ensure(PermissionCodes.SystemUsersRead, TargetOf(member));
        return await BuildMemberDtoAsync(member, ct);
    }

    /// <inheritdoc />
    public async Task<MemberFormLayoutDto> SetMemberLayoutAsync(Guid userId, SetMemberFormLayoutDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var member = await LoadMemberAsync(userId, ct);
        // Kiểu trình bày quyết định tiêu đề, cấp ký trên biểu mẫu của chính mình → không tự đặt (xung đột lợi ích).
        if (_currentUser?.UserId == member.Id)
            throw new ForbiddenException(
                "Bạn không thể tự đặt kiểu trình bày biểu mẫu cho chính mình (xung đột lợi ích). Hãy nhờ người quản lý tài khoản khác thực hiện.");
        _guard.Ensure(PermissionCodes.SystemUsersManage, TargetOf(member).ForWrite());
        if (input.LayoutId.HasValue)
            member.FormLayoutOverride = await _repo.FindAsync(input.LayoutId.Value, ct)
                ?? throw new ValidationException("Kiểu trình bày đã chọn không còn trong danh mục. Hãy tải lại trang.");
        else
            member.FormLayoutOverride = null;
        member.FormLayoutOverrideId = input.LayoutId;
        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildMemberDtoAsync(member, ct);
    }

    private async Task<MemberFormLayoutDto> BuildMemberDtoAsync(PartyMemberProfile member, CancellationToken ct)
    {
        var held = await _repo.ListHeldPositionsAsync(member.Id, _time.GetUtcNow().UtcDateTime, ct);
        var defaults = await _repo.ListAsync(ct);
        var fallback = defaults.FirstOrDefault(l => l.IsDefault);
        var derived = FormLayout.Resolve(null, held.Select(mp => mp.Position?.FormLayout), fallback);
        var effective = FormLayout.Resolve(member.FormLayoutOverride, held.Select(mp => mp.Position?.FormLayout), fallback);
        return new MemberFormLayoutDto
        {
            EffectiveId = effective?.Id,
            EffectiveName = effective?.Name,
            DerivedId = derived?.Id,
            DerivedName = derived?.Name,
            OverrideId = member.FormLayoutOverrideId,
            PositionLayouts = held.Where(mp => mp.Position?.FormLayout != null)
                .Select(mp => $"{PositionRules.DisplayName(mp)} → {mp.Position!.FormLayout!.Name}")
                .Distinct()
                .ToList()
        };
    }

    private async Task ApplyAsync(FormLayout layout, SaveFormLayoutDto input, Guid? id, CancellationToken ct)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > FormLayout.MaxNameLength)
            throw new ValidationException($"Tên kiểu trình bày phải có từ 1 đến {FormLayout.MaxNameLength} ký tự.");
        if (!string.Equals(name, layout.Name, StringComparison.OrdinalIgnoreCase) && await _repo.NameExistsAsync(name, id, ct))
            throw new ConflictException($"Kiểu trình bày \"{name}\" đã có trong danh mục. Hãy dùng tên khác.");
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > FormLayout.MaxDescriptionLength)
            throw new ValidationException($"Mô tả dài quá {FormLayout.MaxDescriptionLength} ký tự. Hãy rút ngắn.");
        if (!Enum.TryParse<FormHeaderStyle>(input.HeaderStyle?.Trim(), true, out var header) || !Enum.IsDefined(header))
            throw new ValidationException("Hãy chọn tiêu đề Mẫu 09B: theo Chi bộ sinh hoạt hoặc theo Đảng ủy cơ sở.");
        var empty = string.IsNullOrWhiteSpace(input.EmptyPositionText) ? null : input.EmptyPositionText.Trim();
        if (empty?.Length > FormLayout.MaxEmptyPositionTextLength)
            throw new ValidationException($"Chữ in thay chức vụ trống dài quá {FormLayout.MaxEmptyPositionTextLength} ký tự.");

        layout.Name = name;
        layout.Description = description;
        layout.HeaderStyle = header;
        layout.ShowCellConfirmation = input.ShowCellConfirmation;
        layout.SingleLinePositions = input.SingleLinePositions;
        layout.ShowDateLineOn9D = input.ShowDateLineOn9D;
        layout.EmptyPositionText = empty;
        layout.SortOrder = input.SortOrder;
        if (input.IsDefault && !layout.IsDefault)
        {
            // Chỉ một kiểu mặc định.
            foreach (var other in await _repo.ListDefaultsAsync(ct))
                other.IsDefault = false;
        }
        layout.IsDefault = input.IsDefault;
    }

    private async Task<PartyMemberProfile> LoadMemberAsync(Guid userId, CancellationToken ct)
        => await _repo.FindMemberAsync(userId, ct)
            ?? throw new NotFoundException("Không tìm thấy tài khoản cán bộ (có thể đã bị xóa). Hãy tải lại danh sách.");

    private static AccessTarget TargetOf(PartyMemberProfile member) => new(DepartmentId: member.DepartmentId, PartyCellId: member.PartyCellId);

    private static FormLayoutDto ToDto(FormLayout l, int positions, int members) => new()
    {
        Id = l.Id,
        Name = l.Name,
        Description = l.Description,
        HeaderStyle = l.HeaderStyle.ToString(),
        ShowCellConfirmation = l.ShowCellConfirmation,
        SingleLinePositions = l.SingleLinePositions,
        ShowDateLineOn9D = l.ShowDateLineOn9D,
        EmptyPositionText = l.EmptyPositionText,
        IsDefault = l.IsDefault,
        SortOrder = l.SortOrder,
        PositionCount = positions,
        MemberCount = members
    };
}
