using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Triển khai <see cref="IUserAccountService"/> (task 08): tên đăng nhập chuẩn hóa chữ thường, duy nhất kể cả tài khoản
/// đã xóa; kiểm tra Phòng/Chi bộ; mật khẩu tạm ngẫu nhiên trả về một lần; khóa/xóa/đặt lại mật khẩu vô hiệu phiên ngay
/// (đổi dấu bảo mật, thu hồi refresh token, xóa cache); chốt chặn tự khóa và quản trị viên cuối cùng.
/// Quyền trên từng tài khoản kiểm tra qua <see cref="IAuthorizationGuard"/> theo Phòng/Chi bộ của tài khoản.
/// </summary>
public sealed class UserAccountService : IUserAccountService
{
    /// <summary>Độ dài tối đa tên đăng nhập.</summary>
    public const int MaxUsernameLength = AccountRules.UsernameMaxLength;

    private const int TemporaryPasswordLength = 12;
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";

    private readonly IUserRepository _users;
    private readonly IUserAccountRepository? _accounts;
    private readonly IRefreshTokenRepository? _refreshTokens;
    private readonly ICurrentUserService? _currentUser;
    private readonly IAccessCacheInvalidator? _accessCache;
    private readonly IAccountStateProvider? _accountState;
    private readonly IAdministratorGuard? _adminGuard;
    private readonly IAuthorizationGuard? _authz;

    /// <summary>
    /// Bản tối giản chỉ hỗ trợ <see cref="CreateAsync"/> (không kiểm tra danh mục/quyền) — dùng cho test đơn vị cũ.
    /// </summary>
    public UserAccountService(IUserRepository users) => _users = users;

    /// <summary>Khởi tạo dịch vụ đầy đủ (DI).</summary>
    public UserAccountService(
        IUserRepository users,
        IUserAccountRepository accounts,
        IRefreshTokenRepository refreshTokens,
        ICurrentUserService currentUser,
        IAccessCacheInvalidator accessCache,
        IAccountStateProvider accountState,
        IAdministratorGuard adminGuard,
        IAuthorizationGuard authz)
    {
        _users = users;
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _accessCache = accessCache;
        _accountState = accountState;
        _adminGuard = adminGuard;
        _authz = authz;
    }

    private IUserAccountRepository Accounts => _accounts ?? throw NotConfigured();

    /// <inheritdoc />
    public async Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default)
    {
        var (member, temporaryPassword) = await BuildNewAccountAsync(cmd, ct);
        if (_accounts != null)
            await _accounts.AddAsync(member, ct);
        else
            await _users.AddAsync(member);

        return new CreatedAccount(member.Id, member.Username, temporaryPassword);
    }

    /// <inheritdoc />
    public async Task<CreatedAccount> StageCreateAsync(CreateAccountCommand cmd, CancellationToken ct = default)
    {
        var (member, temporaryPassword) = await BuildNewAccountAsync(cmd, ct);
        Accounts.Stage(member);
        return new CreatedAccount(member.Id, member.Username, temporaryPassword);
    }

    /// <summary>Kiểm tra dữ liệu, quyền, danh mục, trùng tên và dựng tài khoản mới (chưa đưa vào CSDL).</summary>
    private async Task<(PartyMemberProfile Member, string TemporaryPassword)> BuildNewAccountAsync(
        CreateAccountCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ct.ThrowIfCancellationRequested();

        var username = AccountRules.NormalizeAndValidateUsername(cmd.Username);
        var fullName = cmd.FullName?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
            throw new ValidationException("Họ và tên không được để trống. Hãy nhập họ và tên cán bộ.");
        var email = AccountRules.NormalizeOptionalEmail(cmd.Email);
        var departmentId = cmd.DepartmentId == Guid.Empty ? null : cmd.DepartmentId;
        var partyCellId = cmd.PartyCellId == Guid.Empty ? null : cmd.PartyCellId;

        if (_accounts != null)
        {
            EnsureCanManage(departmentId, partyCellId);
            await EnsureCatalogAsync(departmentId, partyCellId, ct);
        }

        if (await UsernameTakenAsync(username, ct))
            throw new ConflictException(
                $"Tên đăng nhập \"{username}\" đã được dùng cho tài khoản khác (kể cả tài khoản đã xóa). Hãy chọn tên đăng nhập khác.");

        var temporaryPassword = GenerateTemporaryPassword();
        var partyCardNumber = string.IsNullOrWhiteSpace(cmd.PartyCardNumber) ? null : cmd.PartyCardNumber.Trim();
        var member = new PartyMemberProfile
        {
            Username = username,
            FullName = fullName,
            Email = email,
            PhoneNumber = cmd.PhoneNumber?.Trim() ?? string.Empty,
            PartyCardNumber = partyCardNumber,
            IsPartyMember = partyCardNumber != null,
            PositionTitle = string.IsNullOrWhiteSpace(cmd.PositionTitle) ? "Cán bộ" : cmd.PositionTitle.Trim(),
            DepartmentId = departmentId,
            PartyCellId = partyCellId,
            ApprovalAuthority = cmd.ApprovalAuthority,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            MustChangePassword = true,
            SecurityStamp = NewSecurityStamp()
        };

        return (member, temporaryPassword);
    }

    /// <inheritdoc />
    public async Task<PagedResult<AccountListItemDto>> SearchAsync(AccountSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var scope = Authz.GetScope(PermissionCodes.SystemUsersRead);
        if (scope.IsEmpty)
            return new PagedResult<AccountListItemDto> { Page = page, PageSize = pageSize };

        var criteria = new AccountSearchCriteria(
            page, pageSize, query.Query, query.DepartmentId, query.PartyCellId, query.IsActive,
            scope.IsGlobal ? null : new AccountScope(scope.DepartmentIds, scope.PartyCellIds));
        var result = await Accounts.SearchAsync(criteria, ct);
        return new PagedResult<AccountListItemDto>
        {
            Items = result.Items.Select(ToDto).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };
    }

    /// <inheritdoc />
    public async Task<AccountListItemDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var member = await LoadAsync(id, ct);
        Authz.Ensure(PermissionCodes.SystemUsersRead, TargetOf(member.DepartmentId, member.PartyCellId));
        return ToDto(member);
    }

    /// <inheritdoc />
    public async Task<AccountListItemDto> UpdateAsync(Guid id, UpdateAccountCommand cmd, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var member = await LoadAsync(id, ct);
        EnsureCanManage(member.DepartmentId, member.PartyCellId);

        if (cmd.FullName != null)
        {
            var fullName = cmd.FullName.Trim();
            if (fullName.Length == 0)
                throw new ValidationException("Họ và tên không được để trống. Hãy nhập họ và tên cán bộ.");
            member.FullName = fullName;
        }

        if (cmd.Email != null)
            member.Email = AccountRules.NormalizeOptionalEmail(cmd.Email);
        if (cmd.PhoneNumber != null)
            member.PhoneNumber = cmd.PhoneNumber.Trim();
        if (cmd.PartyCardNumber != null)
        {
            member.PartyCardNumber = string.IsNullOrWhiteSpace(cmd.PartyCardNumber) ? null : cmd.PartyCardNumber.Trim();
            member.IsPartyMember = member.PartyCardNumber != null;
        }
        if (cmd.PositionTitle != null && !string.IsNullOrWhiteSpace(cmd.PositionTitle))
            member.PositionTitle = cmd.PositionTitle.Trim();
        if (cmd.ApprovalAuthority.HasValue)
        {
            if (!Enum.IsDefined(cmd.ApprovalAuthority.Value))
                throw new ValidationException("Cấp có thẩm quyền quyết định xếp loại không hợp lệ. Hãy chọn Đảng ủy cơ sở hoặc cấp trên.");
            member.ApprovalAuthority = cmd.ApprovalAuthority.Value;
        }

        var newDepartment = cmd.DepartmentId.HasValue
            ? (cmd.DepartmentId.Value == Guid.Empty ? null : cmd.DepartmentId)
            : member.DepartmentId;
        var newCell = cmd.PartyCellId.HasValue
            ? (cmd.PartyCellId.Value == Guid.Empty ? null : cmd.PartyCellId)
            : member.PartyCellId;
        if (newDepartment != member.DepartmentId || newCell != member.PartyCellId)
        {
            // Chuyển tài khoản sang Phòng/Chi bộ khác: người thao tác phải quản lý được cả nơi đến.
            EnsureCanManage(newDepartment, newCell);
            await EnsureCatalogAsync(
                newDepartment != member.DepartmentId ? newDepartment : null,
                newCell != member.PartyCellId ? newCell : null,
                ct);
            member.DepartmentId = newDepartment;
            member.PartyCellId = newCell;
        }

        await Accounts.SaveChangesAsync(ct);
        AccessCache.InvalidateUser(member.Id);

        var reloaded = await LoadAsync(id, ct);
        return ToDto(reloaded);
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var member = await LoadAsync(id, ct);
        EnsureCanManage(member.DepartmentId, member.PartyCellId);
        if (member.IsActive == isActive)
            return;

        if (!isActive)
        {
            await AdminGuard.EnsureCanDisableAsync(_currentUser?.UserId, member.Id, "khóa", ct);
            member.IsActive = false;
            await RevokeSessionsAsync(member, ct);
            return;
        }

        member.IsActive = true;
        await Accounts.SaveChangesAsync(ct);
        InvalidateCaches(member.Id);
    }

    /// <inheritdoc />
    public async Task UnlockAsync(Guid id, CancellationToken ct = default)
    {
        var member = await LoadAsync(id, ct);
        EnsureCanManage(member.DepartmentId, member.PartyCellId);

        member.FailedLoginCount = 0;
        member.LockoutEnd = null;
        await Accounts.SaveChangesAsync(ct);
        InvalidateCaches(member.Id);
    }

    /// <inheritdoc />
    public async Task<CreatedAccount> ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var member = await LoadAsync(id, ct);
        EnsureCanManage(member.DepartmentId, member.PartyCellId);

        var temporaryPassword = GenerateTemporaryPassword();
        member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        member.MustChangePassword = true;
        member.FailedLoginCount = 0;
        member.LockoutEnd = null;
        await RevokeSessionsAsync(member, ct);

        return new CreatedAccount(member.Id, member.Username, temporaryPassword);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var member = await LoadAsync(id, ct);
        EnsureCanManage(member.DepartmentId, member.PartyCellId);
        await AdminGuard.EnsureCanDisableAsync(_currentUser?.UserId, member.Id, "xóa", ct);

        member.SecurityStamp = NewSecurityStamp();
        await Accounts.SoftDeleteAsync(member, ct);
        await RefreshTokens.RevokeAllUserTokensAsync(member.Id);
        InvalidateCaches(member.Id);
    }

    /// <summary>Sinh mật khẩu tạm ngẫu nhiên an toàn, luôn có cả chữ và số, không dùng ký tự dễ nhầm (0/O, 1/l/I).</summary>
    public static string GenerateTemporaryPassword()
    {
        const string alphabet = Letters + Digits;
        var password = new char[TemporaryPasswordLength];
        for (var i = 0; i < password.Length; i++)
            password[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        // Bảo đảm có ít nhất một chữ và một số ở vị trí ngẫu nhiên khác nhau.
        var letterIndex = RandomNumberGenerator.GetInt32(password.Length);
        var digitIndex = (letterIndex + 1 + RandomNumberGenerator.GetInt32(password.Length - 1)) % password.Length;
        password[letterIndex] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        password[digitIndex] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        return new string(password);
    }

    /// <summary>Dấu bảo mật mới (32 ký tự hex).</summary>
    public static string NewSecurityStamp() => Guid.NewGuid().ToString("N");

    /// <summary>Ánh xạ tài khoản sang DTO danh sách.</summary>
    public static AccountListItemDto ToDto(PartyMemberProfile m) => new()
    {
        Id = m.Id,
        Username = m.Username,
        FullName = m.FullName,
        Email = m.Email,
        PhoneNumber = m.PhoneNumber,
        PartyCardNumber = m.PartyCardNumber,
        IsPartyMember = m.IsPartyMember,
        PositionTitle = m.PositionTitle,
        DepartmentId = m.DepartmentId,
        DepartmentName = m.Department?.Name,
        PartyCellId = m.PartyCellId,
        PartyCellName = m.PartyCell?.Name,
        ApprovalAuthority = m.ApprovalAuthority,
        IsActive = m.IsActive,
        IsLockedOut = m.LockoutEnd.HasValue && m.LockoutEnd.Value > DateTime.UtcNow,
        LockoutEnd = m.LockoutEnd,
        FailedLoginCount = m.FailedLoginCount,
        MustChangePassword = m.MustChangePassword,
        LastLoginAt = m.LastLoginAt,
        CreatedAt = m.CreatedAt
    };

    private async Task RevokeSessionsAsync(PartyMemberProfile member, CancellationToken ct)
    {
        member.SecurityStamp = NewSecurityStamp();
        await Accounts.SaveChangesAsync(ct);
        await RefreshTokens.RevokeAllUserTokensAsync(member.Id);
        InvalidateCaches(member.Id);
    }

    private void InvalidateCaches(Guid userId)
    {
        AccessCache.InvalidateUser(userId);
        _accountState?.Invalidate(userId);
    }

    private async Task<PartyMemberProfile> LoadAsync(Guid id, CancellationToken ct) =>
        await Accounts.FindByIdAsync(id, ct)
        ?? throw new NotFoundException("Không tìm thấy tài khoản cần thao tác (có thể đã bị xóa). Hãy tải lại danh sách tài khoản.");

    private async Task<bool> UsernameTakenAsync(string username, CancellationToken ct)
    {
        if (_accounts != null)
            return await _accounts.UsernameExistsAsync(username, ct);

        var duplicates = await _users.FindAsync(m => m.Username.ToLower() == username);
        return duplicates.Count > 0;
    }

    private async Task EnsureCatalogAsync(Guid? departmentId, Guid? partyCellId, CancellationToken ct)
    {
        if (departmentId.HasValue && !await Accounts.DepartmentIsActiveAsync(departmentId.Value, ct))
            throw new ValidationException(
                "Phòng/đơn vị đã chọn không tồn tại hoặc đã ngừng hoạt động. Hãy chọn Phòng khác trong danh mục.");
        if (partyCellId.HasValue && !await Accounts.PartyCellIsActiveAsync(partyCellId.Value, ct))
            throw new ValidationException(
                "Chi bộ đã chọn không tồn tại hoặc đã ngừng hoạt động. Hãy chọn Chi bộ khác trong danh mục.");
    }

    /// <summary>
    /// Người thao tác phải có <c>system.users.manage</c> bao trùm Phòng/Chi bộ của tài khoản.
    /// Bỏ qua khi không có người dùng đăng nhập (tác vụ hệ thống).
    /// </summary>
    private void EnsureCanManage(Guid? departmentId, Guid? partyCellId)
    {
        if (_currentUser?.UserId == null)
            return;
        Authz.Ensure(PermissionCodes.SystemUsersManage, TargetOf(departmentId, partyCellId));
    }

    private static AccessTarget TargetOf(Guid? departmentId, Guid? partyCellId) =>
        new(DepartmentId: departmentId, PartyCellId: partyCellId);

    private IAuthorizationGuard Authz => _authz ?? throw NotConfigured();
    private IRefreshTokenRepository RefreshTokens => _refreshTokens ?? throw NotConfigured();
    private IAccessCacheInvalidator AccessCache => _accessCache ?? throw NotConfigured();
    private IAdministratorGuard AdminGuard => _adminGuard ?? throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("UserAccountService được khởi tạo ở chế độ tối giản; thao tác này cần đầy đủ phụ thuộc (DI).");
}
