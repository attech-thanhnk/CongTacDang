using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;

namespace CongTacDang.Infrastructure.Data;

/// <summary>
/// Khởi tạo dữ liệu nền: danh mục quyền (từ <see cref="PermissionCodes"/>), vai trò mặc định
/// (docs/thiet-ke/phan-quyen.md mục 6 — ĐỀ XUẤT, chờ nghiệp vụ xác nhận), tài khoản quản trị ban đầu
/// và dữ liệu mẫu khi bật <c>Database:SeedSampleData</c>.
/// <para>
/// Đây là nơi <b>duy nhất</b> biết mã vai trò (<see cref="AppRole.Code"/>): mã chỉ dùng để seeder nhận ra vai trò mặc định
/// (đặt lại quyền, gán mẫu, chọn vai trò quản trị ban đầu); logic phân quyền chỉ dùng mã quyền.
/// </para>
/// </summary>
public static class DataSeeder
{
    /// <summary>Cấu hình tài khoản quản trị ban đầu (<c>Seed:InitialAdmin:*</c>).</summary>
    /// <param name="Username">Tên đăng nhập.</param>
    /// <param name="FullName">Họ tên hiển thị (trống → "Quản trị hệ thống").</param>
    /// <param name="Password">Mật khẩu ban đầu (phải đổi ở lần đăng nhập đầu).</param>
    /// <param name="PasswordMinLength">Độ dài tối thiểu mật khẩu (<c>Security:Password:MinLength</c>).</param>
    public sealed record InitialAdminOptions(string? Username, string? FullName, string? Password,
        int PasswordMinLength = PasswordPolicy.DefaultMinLength)
    {
        /// <summary>Đã đặt đủ tên đăng nhập và mật khẩu.</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

        /// <summary>Không in mật khẩu khi ghi log/đối tượng.</summary>
        public override string ToString() => $"InitialAdminOptions {{ Username = {Username}, FullName = {FullName} }}";
    }

    /// <summary>Cấu hình dữ liệu mẫu (<c>Database:SeedSampleData</c>, <c>Seed:SamplePassword</c>).</summary>
    /// <param name="Enabled">Tạo dữ liệu mẫu (chỉ môi trường thử nghiệm).</param>
    /// <param name="Password">
    /// Mật khẩu tạm chung của tài khoản mẫu. Trống → sinh ngẫu nhiên và ghi log mức Warning đúng một lần khi tạo.
    /// </param>
    /// <param name="PasswordMinLength">Độ dài tối thiểu mật khẩu (<c>Security:Password:MinLength</c>).</param>
    public sealed record SampleDataOptions(bool Enabled, string? Password = null,
        int PasswordMinLength = PasswordPolicy.DefaultMinLength)
    {
        /// <summary>Không in mật khẩu khi ghi log/đối tượng.</summary>
        public override string ToString() => $"SampleDataOptions {{ Enabled = {Enabled} }}";
    }

    /// <summary>Định nghĩa một vai trò mặc định và bộ quyền của nó.</summary>
    private sealed record RoleDefinition(string Code, string Name, string Description, string[] Permissions, bool IsProtected = false);

    /// <summary>Mã nội bộ của vai trò mặc định (chỉ seeder dùng, không dùng để phân quyền).</summary>
    private static class RoleCodes
    {
        public const string Evaluatee = "NGUOI_DUOC_DANH_GIA";
        public const string DepartmentLeader = "LANH_DAO_PHONG";
        public const string CollectiveSecretary = "THU_KY_TAP_THE";
        public const string CellCommittee = "CHI_UY_CHI_BO";
        public const string Appraisal = "CO_QUAN_THAM_DINH";
        public const string DirectSupervisor = "CAP_TRUC_TIEP_SU_DUNG";
        public const string PartyCommitteeMember = "CAP_UY_VIEN";
        public const string PartyOffice = "VAN_PHONG_DANG_UY";
        public const string Administrator = "QUAN_TRI_HE_THONG";
    }

    /// <summary>Quyền của vai trò quản trị hệ thống: <c>system.*</c>, <c>catalog.manage</c>, <c>attachment.general.manage</c>.</summary>
    private static readonly string[] AdministratorPermissionCodes = PermissionCodes.All
        .Where(code => code.StartsWith("system.", StringComparison.Ordinal)
            || code == PermissionCodes.CatalogManage
            || code == PermissionCodes.AttachmentGeneralManage)
        .ToArray();

    /// <summary>Cấu hình mặc định (docs/thiet-ke/phan-quyen.md mục 6).</summary>
    private static readonly RoleDefinition[] DefaultRoles =
    {
        new(RoleCodes.Evaluatee, "Người được đánh giá", "Tham gia đánh giá bản thân (HD03 IV.1, IV.2). Phạm vi gán điển hình: Toàn công ty.",
            new[] { PermissionCodes.EvaluationSelf }),
        new(RoleCodes.DepartmentLeader, "Lãnh đạo Phòng", "Xem hồ sơ, duyệt danh mục sản phẩm của Phòng (HD03 IV.1; PL II mục II); đề xuất mức thay cấp trực tiếp sử dụng "
            + "ở hồ sơ luồng được cấu hình (PL III ví dụ 3). Phạm vi gán điển hình: Phòng.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationTasksApprove, PermissionCodes.EvaluationUnitReview }),
        new(RoleCodes.CollectiveSecretary, "Thư ký tập thể lãnh đạo", "Ghi nhận đề xuất của tập thể lãnh đạo, lập biên bản (HD03 IV.3a; Mẫu 11–13). Phạm vi gán điển hình: Phòng hoặc Toàn công ty.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCollectiveRecord, PermissionCodes.MeetingRead, PermissionCodes.MeetingManage }),
        new(RoleCodes.CellCommittee, "Chi ủy / Bí thư Chi bộ", "Chi bộ xác nhận phiếu tự chấm, lập hồ sơ tập thể (Mẫu 09A–9D, Mẫu 07). Phạm vi gán điển hình: Chi bộ.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCellConfirm, PermissionCodes.CollectiveManage, PermissionCodes.MeetingRead }),
        new(RoleCodes.Appraisal, "Cơ quan thẩm định (Phòng TCCB-LĐ)", "Rà soát, thẩm định; quản lý kỳ đánh giá (HD03 IV.1, IV.3b). Phạm vi gán: Toàn công ty.",
            new[]
            {
                PermissionCodes.EvaluationRead, PermissionCodes.EvaluationAppraise, PermissionCodes.PeriodManage,
                PermissionCodes.ReportExport, PermissionCodes.SystemImport, PermissionCodes.SystemUsersRead
            }),
        new(RoleCodes.DirectSupervisor, "Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)", "Nhận xét, đề xuất của cấp trực tiếp sử dụng cán bộ (HD03 IV.3c). Phạm vi gán: Toàn công ty.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationDirectorReview, PermissionCodes.ReportExport }),
        new(RoleCodes.PartyCommitteeMember, "Cấp ủy viên Đảng ủy", "Xem hồ sơ, biên bản, báo cáo (HD03 IV.4; Mẫu 18). Phạm vi gán: Toàn công ty.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.MeetingRead, PermissionCodes.ReportExport }),
        new(RoleCodes.PartyOffice, "Văn phòng Đảng ủy (ghi nhận quyết định)", "Ghi nhận quyết định của Đảng ủy cơ sở, ghi nhận kết quả của cấp trên "
            + "(thẩm định, nhận xét, quyết định do cấp trên thực hiện), công bố, mở lại hồ sơ (HD03 IV.4, IV.5). Phạm vi gán: Toàn công ty.",
            new[]
            {
                PermissionCodes.EvaluationRead, PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationExternalRecord,
                PermissionCodes.EvaluationPublish, PermissionCodes.EvaluationReopen, PermissionCodes.MeetingRead,
                PermissionCodes.MeetingManage, PermissionCodes.ReportExport
            }),
        new(RoleCodes.Administrator, "Quản trị hệ thống", "Quản trị kỹ thuật: tài khoản, vai trò, gán vai trò, nhật ký, danh mục, văn bản chung. Không xem nội dung đánh giá (Mẫu 18).",
            AdministratorPermissionCodes, IsProtected: true)
    };

    /// <summary>
    /// Khởi tạo dữ liệu nền. Vai trò mặc định chỉ được tạo khi CSDL chưa có vai trò nào; quyền của vai trò đã tồn tại
    /// không bị ghi đè trừ khi <paramref name="resetRolePermissions"/> = true (<c>Database:ResetRolePermissions</c>).
    /// Dữ liệu mẫu chỉ được tạo trên CSDL chưa có tài khoản, Phòng, Chi bộ, kỳ đánh giá nào (không bao giờ gán lại vai trò).
    /// </summary>
    public static async Task SeedAsync(
        CongTacDangDbContext context,
        SampleDataOptions? sampleData = null,
        bool resetRolePermissions = false,
        ILogger? logger = null,
        InitialAdminOptions? initialAdmin = null)
    {
        // 1. Danh mục quyền: đồng bộ từ PermissionCodes (tạo mới, cập nhật tên/mô tả/phân hệ/thứ tự).
        await SyncPermissionCatalogAsync(context, logger);

        // 2. Vai trò mặc định (chỉ khi chưa có vai trò nào).
        await SeedDefaultRolesAsync(context, logger);
        if (resetRolePermissions)
            await ResetRolePermissionsAsync(context, logger);

        // 2b. Danh mục loại đơn vị và danh mục chức vụ mặc định (chỉ khi danh mục trống — task 14, chờ nghiệp vụ xác nhận).
        await SeedOrganizationCatalogsAsync(context, logger);

        // 3. Dữ liệu mẫu (chỉ môi trường thử nghiệm).
        if (sampleData?.Enabled == true)
            await SeedSampleDataAsync(context, sampleData, logger);

        // 4. Tài khoản quản trị ban đầu (độc lập với dữ liệu mẫu) — chỉ khi hệ thống chưa có quản trị nào.
        await SeedInitialAdministratorAsync(context, initialAdmin, logger);
    }

    #region Quản trị ban đầu

    /// <summary>
    /// Tạo tài khoản quản trị ban đầu (cấu hình <c>Seed:InitialAdmin:*</c>) khi chưa có tài khoản đang hoạt động nào giữ
    /// <c>system.roles.manage</c> và <c>system.assignments.manage</c> phạm vi Toàn công ty: bắt buộc đổi mật khẩu ở lần
    /// đăng nhập đầu, gán vai trò quản trị hệ thống (được bảo vệ) phạm vi Toàn công ty. Đã có quản trị → bỏ qua (không đổi
    /// mật khẩu). Thiếu cấu hình / cấu hình sai → ghi log, không dừng ứng dụng. Mật khẩu không bao giờ được ghi log.
    /// </summary>
    private static async Task SeedInitialAdministratorAsync(CongTacDangDbContext context, InitialAdminOptions? options, ILogger? logger)
    {
        var now = DateTime.UtcNow;
        var grants = await new RoleAssignmentRepository(context).GetAdministratorGrantsAsync(now);
        if (AdministratorInvariant.Holds(grants))
        {
            if (options?.IsConfigured == true)
                logger?.LogInformation(
                    "Đã có tài khoản quản trị đang hoạt động; bỏ qua cấu hình Seed:InitialAdmin (không tạo mới, không đổi mật khẩu).");
            return;
        }

        if (options?.IsConfigured != true)
        {
            logger?.LogWarning(
                "Hệ thống chưa có tài khoản quản trị nào đang hoạt động nên không ai quản trị được. Hãy đặt Seed:InitialAdmin:Username "
                + "và Seed:InitialAdmin:Password (biến môi trường Seed__InitialAdmin__Username, Seed__InitialAdmin__Password; tùy chọn "
                + "Seed__InitialAdmin__FullName) rồi khởi động lại để tạo tài khoản quản trị ban đầu.");
            return;
        }

        string username;
        try
        {
            username = AccountRules.NormalizeAndValidateUsername(options.Username);
            new PasswordPolicy(options.PasswordMinLength).Validate(options.Password);
        }
        catch (ValidationException ex)
        {
            logger?.LogError("Không tạo được tài khoản quản trị ban đầu từ cấu hình Seed:InitialAdmin: {Reason}", ex.Message);
            return;
        }

        if (await context.PartyMemberProfiles.IgnoreQueryFilters().AnyAsync(m => m.Username.ToLower() == username))
        {
            logger?.LogError(
                "Không tạo được tài khoản quản trị ban đầu: tên đăng nhập {Username} đã được dùng (kể cả tài khoản đã xóa/khóa). "
                + "Hãy đặt Seed:InitialAdmin:Username khác.", username);
            return;
        }

        var candidates = await context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.IsProtected)
            .OrderBy(r => r.Code == RoleCodes.Administrator ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();
        var adminRole = candidates.FirstOrDefault(r =>
            AdministratorInvariant.Codes.All(code => r.Permissions.Any(p => p.Code == code && !p.IsDeleted)));
        if (adminRole == null)
        {
            logger?.LogError(
                "Không tạo được tài khoản quản trị ban đầu: không có vai trò được bảo vệ nào chứa đủ quyền quản trị "
                + "(system.roles.manage, system.assignments.manage).");
            return;
        }

        var fullName = string.IsNullOrWhiteSpace(options.FullName) ? "Quản trị hệ thống" : options.FullName.Trim();
        var admin = new PartyMemberProfile
        {
            Username = username,
            FullName = fullName,
            PositionTitle = "Quản trị hệ thống",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.Password),
            MustChangePassword = true,
            IsActive = true,
            IsPartyMember = false,
            SecurityStamp = UserAccountService.NewSecurityStamp(),
            ApprovalAuthority = ApprovalAuthority.CoSo
        };
        context.PartyMemberProfiles.Add(admin);
        context.Set<UserRoleAssignment>().Add(new UserRoleAssignment
        {
            UserId = admin.Id,
            RoleId = adminRole.Id,
            ScopeType = RoleScopeType.Global,
            ValidFrom = now,
            Note = "Tài khoản quản trị ban đầu (cấu hình Seed:InitialAdmin)."
        });
        await context.SaveChangesAsync();
        logger?.LogInformation(
            "Đã tạo tài khoản quản trị ban đầu {Username} với vai trò {Role} (Toàn công ty); phải đổi mật khẩu ở lần đăng nhập đầu.",
            username, adminRole.Name);
    }

    #endregion

    #region Danh mục quyền, vai trò

    /// <summary>
    /// Đồng bộ bảng <c>permissions</c> từ <see cref="PermissionCodes.Definitions"/>: tạo mã còn thiếu, cập nhật tên/mô tả/phân hệ/thứ tự,
    /// khôi phục mã bị xóa mềm. Mã trong CSDL không còn trong code được giữ nguyên (resolver bỏ qua và ghi cảnh báo).
    /// </summary>
    private static async Task SyncPermissionCatalogAsync(CongTacDangDbContext context, ILogger? logger)
    {
        var existing = await context.Permissions.IgnoreQueryFilters().ToListAsync();
        var created = new List<string>();
        for (var index = 0; index < PermissionCodes.Definitions.Count; index++)
        {
            var definition = PermissionCodes.Definitions[index];
            var permission = existing.FirstOrDefault(p => p.Code == definition.Code);
            if (permission == null)
            {
                context.Permissions.Add(new Permission
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Description = definition.Description,
                    Module = definition.Module,
                    SortOrder = index
                });
                created.Add(definition.Code);
                continue;
            }

            if (permission.Name != definition.Name) permission.Name = definition.Name;
            if (permission.Description != definition.Description) permission.Description = definition.Description;
            if (permission.Module != definition.Module) permission.Module = definition.Module;
            if (permission.SortOrder != index) permission.SortOrder = index;
            if (permission.IsDeleted)
            {
                permission.IsDeleted = false;
                permission.DeletedAt = null;
                permission.DeletedBy = null;
            }
        }

        if (context.ChangeTracker.HasChanges())
            await context.SaveChangesAsync();
        if (created.Count > 0)
            logger?.LogInformation("Đã tạo mã quyền: {Permissions}", string.Join(", ", created));
    }

    /// <summary>
    /// Tạo vai trò mặc định (mục 6 thiết kế) khi CSDL chưa có vai trò nào (kể cả đã xóa). Vai trò quản trị được bảo vệ.
    /// Không bao giờ ghi đè vai trò đã tồn tại.
    /// </summary>
    private static async Task SeedDefaultRolesAsync(CongTacDangDbContext context, ILogger? logger)
    {
        if (await context.Roles.IgnoreQueryFilters().AnyAsync())
            return;

        var permissions = await context.Permissions.ToDictionaryAsync(p => p.Code);
        foreach (var definition in DefaultRoles)
        {
            context.Roles.Add(new AppRole
            {
                Code = definition.Code,
                Name = definition.Name,
                Description = definition.Description,
                IsSystem = true,
                IsProtected = definition.IsProtected,
                Permissions = definition.Permissions.Where(permissions.ContainsKey).Select(code => permissions[code]).ToList()
            });
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Đã tạo vai trò mặc định: {Roles}", string.Join(", ", DefaultRoles.Select(r => r.Name)));
    }

    /// <summary>Đặt lại quyền của các vai trò mặc định về cấu hình mặc định (chỉ khi bật Database:ResetRolePermissions).</summary>
    private static async Task ResetRolePermissionsAsync(CongTacDangDbContext context, ILogger? logger)
    {
        var permissions = await context.Permissions.ToDictionaryAsync(p => p.Code);
        var codes = DefaultRoles.Select(r => r.Code).ToList();
        var roles = await context.Roles
            .Include(r => r.Permissions)
            .Where(r => codes.Contains(r.Code))
            .ToListAsync();

        foreach (var role in roles)
        {
            var definition = DefaultRoles.First(r => r.Code == role.Code);
            role.Permissions.Clear();
            foreach (var code in definition.Permissions.Where(permissions.ContainsKey))
                role.Permissions.Add(permissions[code]);
            role.IsProtected = definition.IsProtected;
        }

        await context.SaveChangesAsync();
        logger?.LogWarning(
            "Database:ResetRolePermissions đang bật: đã đặt lại quyền của các vai trò {Roles} về mặc định. Hãy tắt cờ này sau khi dùng.",
            string.Join(", ", roles.Select(r => r.Name)));
    }

    #endregion

    #region Danh mục tổ chức mặc định (task 14)

    /// <summary>Loại đơn vị mặc định (tên, bên) — ĐỀ XUẤT, chờ nghiệp vụ xác nhận; quản trị sửa được.</summary>
    private static readonly (string Name, OrgSide Side)[] DefaultUnitTypes =
    {
        ("Đảng ủy", OrgSide.Party),
        ("Đảng bộ bộ phận", OrgSide.Party),
        ("Chi bộ", OrgSide.Party),
        ("Công ty", OrgSide.Administrative),
        ("Đơn vị", OrgSide.Administrative),
        ("Phòng", OrgSide.Administrative),
        ("Trung tâm", OrgSide.Administrative),
        ("Xưởng", OrgSide.Administrative),
        ("Đội", OrgSide.Administrative)
    };

    /// <summary>Một chức vụ mặc định.</summary>
    private sealed record DefaultPosition(string Name, PositionSide Side, string? StatCode, ApprovalAuthority? Authority, bool IsLeadership);

    /// <summary>
    /// Danh mục chức vụ mặc định — ĐỀ XUẤT theo bản trích xuất HD03 (mục 1.4 bảng thẩm quyền, mục 7 mã M1–M26), CHỜ XÁC NHẬN.
    /// Mã M1–M16 (Mẫu 15A) → cấp trên quyết định; M17–M26 (Mẫu 15B) → Đảng ủy cơ sở quyết định.
    /// </summary>
    private static readonly DefaultPosition[] DefaultPositions =
    {
        // Mẫu 15A — cấp ủy cấp trên quyết định (M1–M16).
        new("Bí thư Đảng ủy Tổng công ty", PositionSide.Party, "M1", ApprovalAuthority.CapTren, true),
        new("Phó Bí thư Đảng ủy Tổng công ty", PositionSide.Party, "M2", ApprovalAuthority.CapTren, true),
        new("Thành viên Hội đồng thành viên Tổng công ty", PositionSide.Administrative, "M3", ApprovalAuthority.CapTren, true),
        new("Phó Tổng giám đốc Tổng công ty", PositionSide.Administrative, "M4", ApprovalAuthority.CapTren, true),
        new("Ủy viên Ban Thường vụ Đảng ủy Tổng công ty", PositionSide.Party, "M5", ApprovalAuthority.CapTren, true),
        new("Ủy viên Ban Chấp hành Đảng bộ Tổng công ty", PositionSide.Party, "M6", ApprovalAuthority.CapTren, true),
        new("Ủy viên Ủy ban Kiểm tra Đảng ủy Tổng công ty", PositionSide.Party, "M7", ApprovalAuthority.CapTren, true),
        new("Bí thư Đảng ủy", PositionSide.Party, "M8", ApprovalAuthority.CapTren, true),
        new("Phó Bí thư Đảng ủy", PositionSide.Party, "M9", ApprovalAuthority.CapTren, true),
        new("Bí thư Chi bộ trực thuộc Đảng ủy bộ phận Văn phòng Tổng công ty", PositionSide.Party, "M10", ApprovalAuthority.CapTren, true),
        new("Phó Bí thư Chi bộ trực thuộc Đảng ủy bộ phận Văn phòng Tổng công ty", PositionSide.Party, "M11", ApprovalAuthority.CapTren, true),
        new("Trưởng, phó cơ quan tham mưu, giúp việc Đảng ủy Tổng công ty", PositionSide.Party, "M12", ApprovalAuthority.CapTren, true),
        new("Kế toán trưởng Tổng công ty", PositionSide.Administrative, "M13", ApprovalAuthority.CapTren, true),
        new("Chủ tịch Công ty", PositionSide.Administrative, "M14", ApprovalAuthority.CapTren, true),
        new("Giám đốc", PositionSide.Administrative, "M14", ApprovalAuthority.CapTren, true),
        new("Phó Giám đốc", PositionSide.Administrative, "M14", ApprovalAuthority.CapTren, true),
        new("Kiểm soát viên", PositionSide.Administrative, "M15", ApprovalAuthority.CapTren, true),
        new("Bí thư Đoàn Thanh niên Tổng công ty", PositionSide.MassOrganization, "M16", ApprovalAuthority.CapTren, true),
        // Mẫu 15B — Đảng ủy cơ sở quyết định (M17–M26).
        new("Ủy viên Ban Thường vụ Đảng ủy", PositionSide.Party, "M17", ApprovalAuthority.CoSo, true),
        new("Đảng ủy viên", PositionSide.Party, "M18", ApprovalAuthority.CoSo, true),
        new("Ủy viên Ủy ban Kiểm tra Đảng ủy", PositionSide.Party, "M19", ApprovalAuthority.CoSo, true),
        new("Bí thư Đảng bộ bộ phận", PositionSide.Party, "M20", ApprovalAuthority.CoSo, true),
        new("Phó Bí thư Đảng bộ bộ phận", PositionSide.Party, "M21", ApprovalAuthority.CoSo, true),
        new("Bí thư Chi bộ", PositionSide.Party, "M22", ApprovalAuthority.CoSo, true),
        new("Phó Bí thư Chi bộ", PositionSide.Party, "M23", ApprovalAuthority.CoSo, true),
        new("Bí thư Chi bộ trực thuộc Đảng bộ bộ phận", PositionSide.Party, "M24", ApprovalAuthority.CoSo, true),
        new("Phó Bí thư Chi bộ trực thuộc Đảng bộ bộ phận", PositionSide.Party, "M25", ApprovalAuthority.CoSo, true),
        new("Trưởng phòng", PositionSide.Administrative, "M26", ApprovalAuthority.CoSo, true),
        new("Phó Trưởng phòng", PositionSide.Administrative, "M26", ApprovalAuthority.CoSo, true),
        new("Trưởng trung tâm", PositionSide.Administrative, "M26", ApprovalAuthority.CoSo, true),
        new("Phó Trưởng trung tâm", PositionSide.Administrative, "M26", ApprovalAuthority.CoSo, true),
        new("Quản đốc", PositionSide.Administrative, "M26", ApprovalAuthority.CoSo, true),
        new("Phó Quản đốc", PositionSide.Administrative, "M26", ApprovalAuthority.CoSo, true),
        // Chức vụ thường gặp chưa xác định mã thống kê/thẩm quyền (chờ xác nhận).
        new("Kế toán trưởng", PositionSide.Administrative, null, null, true),
        new("Chi ủy viên", PositionSide.Party, null, null, true),
        new("Đảng viên", PositionSide.Party, null, null, false),
        new("Chuyên viên", PositionSide.Administrative, null, null, false),
        new("Nhân viên", PositionSide.Administrative, null, null, false)
    };

    /// <summary>
    /// Tạo loại đơn vị và danh mục chức vụ mặc định, mỗi danh mục chỉ khi đang trống (kể cả bản ghi đã xóa) —
    /// không bao giờ ghi đè cấu hình quản trị đã sửa.
    /// </summary>
    private static async Task SeedOrganizationCatalogsAsync(CongTacDangDbContext context, ILogger? logger)
    {
        if (!await context.OrgUnitTypes.IgnoreQueryFilters().AnyAsync())
        {
            context.OrgUnitTypes.AddRange(DefaultUnitTypes.Select((t, i) => new OrgUnitType { Name = t.Name, Side = t.Side, SortOrder = i }));
            await context.SaveChangesAsync();
            logger?.LogInformation("Đã tạo {Count} loại đơn vị mặc định (chờ nghiệp vụ xác nhận).", DefaultUnitTypes.Length);
        }

        if (!await context.Positions.IgnoreQueryFilters().AnyAsync())
        {
            context.Positions.AddRange(DefaultPositions.Select((p, i) => new Position
            {
                Name = p.Name,
                Side = p.Side,
                StatCode = p.StatCode,
                DefaultApprovalAuthority = p.Authority,
                IsLeadership = p.IsLeadership,
                SortOrder = (i + 1) * 10
            }));
            await context.SaveChangesAsync();
            logger?.LogInformation("Đã tạo {Count} chức vụ mặc định theo HD03 (chờ nghiệp vụ xác nhận).", DefaultPositions.Length);
        }
    }

    #endregion

    #region Dữ liệu mẫu

    /// <summary>Một chức vụ của tài khoản mẫu: tên chức vụ (danh mục mặc định), mã đơn vị, chức vụ chính.</summary>
    private sealed record SamplePosition(string Position, string UnitCode, bool IsPrimary);

    /// <summary>Một tài khoản mẫu kèm chức vụ, bản gán vai trò và trạng thái hồ sơ trong kỳ mẫu (null = không được đánh giá).</summary>
    private sealed record SampleAccount(
        string Username, string FullName, string Department, string? PartyCell, SamplePosition[] Positions,
        string Title, JobGroup JobGroup, (string Role, RoleScopeType Scope)[] Roles, RecordStatus? RecordStatus);

    /// <summary>Cây đơn vị chính quyền mẫu (mã, tên, mô tả, mã cha, loại).</summary>
    private static readonly (string Code, string Name, string Description, string? Parent, string Type)[] SampleDepartments =
    {
        ("ATTECH", "Công ty TNHH Kỹ thuật Quản lý bay", "Đơn vị gốc", null, "Công ty"),
        ("BGD", "Ban Giám đốc", "Lãnh đạo Công ty", "ATTECH", "Đơn vị"),
        ("PH-KT", "Phòng Kỹ thuật", "Quản lý kỹ thuật CNS/ATM", "ATTECH", "Phòng"),
        ("PH-KH", "Phòng Kế hoạch - Kinh doanh", "Kế hoạch, đầu tư, kinh doanh", "ATTECH", "Phòng"),
        ("PH-TCCB", "Phòng Tổ chức cán bộ - Lao động", "Tham mưu tổ chức, nhân sự, lao động tiền lương", "ATTECH", "Phòng")
    };

    /// <summary>Cây tổ chức Đảng mẫu (mã, tên, mô tả, mã cha, loại).</summary>
    private static readonly (string Code, string Name, string Description, string? Parent, string Type)[] SampleCells =
    {
        ("DU-ATTECH", "Đảng ủy Công ty", "Đảng bộ cơ sở trực thuộc Đảng ủy Tổng công ty", null, "Đảng ủy"),
        ("CB-KT", "Chi bộ Khối Kỹ thuật", "Chi bộ các phòng kỹ thuật", "DU-ATTECH", "Chi bộ"),
        ("CB-VP", "Chi bộ Khối Văn phòng", "Chi bộ Ban Giám đốc và các phòng tham mưu", "DU-ATTECH", "Chi bộ")
    };

    /// <summary>
    /// Tài khoản mẫu. Phạm vi <see cref="RoleScopeType.Department"/>/<see cref="RoleScopeType.PartyCell"/> lấy theo đơn vị/tổ chức Đảng
    /// của chính tài khoản. Thẩm quyền phê duyệt suy ra từ chức vụ. Trạng thái hồ sơ theo mẫu kỳ "Quý III/2026 — chuyển tiếp".
    /// </summary>
    private static readonly SampleAccount[] SampleAccounts =
    {
        new("admin", "Quản trị hệ thống (mẫu)", "PH-KH", null, new[] { new SamplePosition("Chuyên viên", "PH-KH", true) },
            "Chuyên viên CNTT", JobGroup.Khung4_KhcnChuyenDoiSo,
            new[] { (RoleCodes.Administrator, RoleScopeType.Global) }, null),
        new("giamdoc", "Lê Tiến Thịnh", "BGD", "CB-VP",
            new[] { new SamplePosition("Giám đốc", "ATTECH", true), new SamplePosition("Bí thư Đảng ủy", "DU-ATTECH", false) },
            "Bí thư Đảng ủy, Giám đốc Công ty", JobGroup.Khung1_QuanLyDangDoanThe,
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.DirectSupervisor, RoleScopeType.Global) },
            RecordStatus.Published),
        new("vanphong", "Phạm Thu Hà", "PH-KH", "CB-VP", new[] { new SamplePosition("Chuyên viên", "PH-KH", true) },
            "Chuyên viên Văn phòng Đảng ủy", JobGroup.Khung1_QuanLyDangDoanThe,
            new[] { (RoleCodes.PartyOffice, RoleScopeType.Global), (RoleCodes.PartyCommitteeMember, RoleScopeType.Global) }, null),
        new("thamdinh", "Vũ Đình Hùng", "PH-TCCB", "CB-VP",
            new[] { new SamplePosition("Trưởng phòng", "PH-TCCB", true), new SamplePosition("Đảng ủy viên", "DU-ATTECH", false) },
            "Trưởng phòng Tổ chức cán bộ - Lao động", JobGroup.Khung1_QuanLyDangDoanThe,
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.Appraisal, RoleScopeType.Global) },
            RecordStatus.AwaitingDirectorReview),
        new("truongphong.kt", "Nguyễn Văn Hùng", "PH-KT", "CB-KT",
            new[] { new SamplePosition("Trưởng phòng", "PH-KT", true), new SamplePosition("Chi ủy viên", "CB-KT", false) },
            "Trưởng phòng Kỹ thuật", JobGroup.Khung2_AnToanKyThuat,
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.DepartmentLeader, RoleScopeType.Department) },
            RecordStatus.AwaitingAppraisal),
        new("bithu.kt", "Trần Minh Đức", "PH-KT", "CB-KT",
            new[] { new SamplePosition("Phó Trưởng phòng", "PH-KT", true), new SamplePosition("Bí thư Chi bộ", "CB-KT", false) },
            "Bí thư Chi bộ, Phó Trưởng phòng Kỹ thuật", JobGroup.Khung2_AnToanKyThuat,
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.CellCommittee, RoleScopeType.PartyCell) },
            RecordStatus.AwaitingCollective),
        new("thuky.kt", "Đỗ Thị Lan", "PH-KT", "CB-KT", new[] { new SamplePosition("Chuyên viên", "PH-KT", true) },
            "Chuyên viên, Thư ký tập thể lãnh đạo Phòng Kỹ thuật", JobGroup.Khung2_AnToanKyThuat,
            new[] { (RoleCodes.CollectiveSecretary, RoleScopeType.Department) }, null),
        new("canbo.kt1", "Trần Quốc Tuấn", "PH-KT", "CB-KT", new[] { new SamplePosition("Phó Trưởng phòng", "PH-KT", true) },
            "Phó Trưởng phòng Kỹ thuật", JobGroup.Khung2_AnToanKyThuat,
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global) }, RecordStatus.AwaitingSelfScore),
        new("canbo.kt2", "Hoàng Văn Nam", "PH-KT", "CB-KT", new[] { new SamplePosition("Phó Trưởng phòng", "PH-KT", true) },
            "Phó Trưởng phòng Kỹ thuật", JobGroup.Khung2_AnToanKyThuat,
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global) }, RecordStatus.AwaitingCellConfirm)
    };

    /// <summary>
    /// Dữ liệu mẫu dùng thử ngay theo mô hình hiện hành: cây đơn vị chính quyền (Công ty → Ban Giám đốc, các Phòng) và cây
    /// tổ chức Đảng (Đảng ủy → 2 Chi bộ) có loại đơn vị, tài khoản (bắt buộc đổi mật khẩu) kèm chức vụ (có kiêm nhiệm; thẩm quyền
    /// phê duyệt suy ra từ chức vụ) và bản gán vai trò có phạm vi, kỳ "Quý III/2026" (kiểu kỳ chuyển tiếp, đủ 3 hồ sơ luồng,
    /// đang mở, kiểm tra kẹt luồng sạch) và hồ sơ ở nhiều bước — hồ sơ Giám đốc theo hồ sơ luồng cấp trên. Chỉ chạy trên CSDL
    /// chưa có tài khoản, đơn vị, kỳ nào — không bao giờ gán lại vai trò cho người đã có.
    /// </summary>
    private static async Task SeedSampleDataAsync(CongTacDangDbContext context, SampleDataOptions options, ILogger? logger)
    {
        var hasData = await context.PartyMemberProfiles.IgnoreQueryFilters().AnyAsync()
            || await context.PartyCells.IgnoreQueryFilters().AnyAsync()
            || await context.AdministrativeDepartments.IgnoreQueryFilters().AnyAsync()
            || await context.EvaluationPeriods.IgnoreQueryFilters().AnyAsync();
        if (hasData)
        {
            logger?.LogInformation("Database:SeedSampleData: CSDL đã có dữ liệu nên không tạo dữ liệu mẫu.");
            return;
        }

        var generated = string.IsNullOrEmpty(options.Password);
        var password = generated ? UserAccountService.GenerateTemporaryPassword() : options.Password!;
        try
        {
            new PasswordPolicy(options.PasswordMinLength).Validate(password);
        }
        catch (ValidationException ex)
        {
            logger?.LogError("Không tạo dữ liệu mẫu: Seed:SamplePassword không đạt chính sách mật khẩu ({Reason}).", ex.Message);
            return;
        }

        var now = DateTime.UtcNow;
        var unitTypes = await context.OrgUnitTypes.ToListAsync();
        Guid? TypeId(OrgSide side, string name) => unitTypes.FirstOrDefault(t => t.Side == side && t.Name == name)?.Id;

        var departments = SampleDepartments
            .Select((d, i) => new AdministrativeDepartment
            {
                Code = d.Code, Name = d.Name, Description = d.Description, SortOrder = i, UnitTypeId = TypeId(OrgSide.Administrative, d.Type)
            })
            .ToDictionary(d => d.Code);
        foreach (var d in SampleDepartments.Where(d => d.Parent != null))
            departments[d.Code].ParentId = departments[d.Parent!].Id;
        OrganizationService.RecomputePaths(departments.Values.ToList(), "đơn vị");

        var cells = SampleCells
            .Select((c, i) => new PartyCell
            {
                Code = c.Code, Name = c.Name, Description = c.Description, SortOrder = i, UnitTypeId = TypeId(OrgSide.Party, c.Type)
            })
            .ToDictionary(c => c.Code);
        foreach (var c in SampleCells.Where(c => c.Parent != null))
            cells[c.Code].ParentId = cells[c.Parent!].Id;
        OrganizationService.RecomputePaths(cells.Values.ToList(), "tổ chức Đảng");

        context.AdministrativeDepartments.AddRange(departments.Values);
        context.PartyCells.AddRange(cells.Values);

        var positions = await context.Positions.ToDictionaryAsync(p => p.Name);
        var roles = await context.Roles.ToDictionaryAsync(r => r.Code, r => r.Id);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var members = new Dictionary<string, PartyMemberProfile>(StringComparer.Ordinal);
        foreach (var sample in SampleAccounts)
        {
            var department = departments[sample.Department];
            var cell = sample.PartyCell == null ? null : cells[sample.PartyCell];
            var member = new PartyMemberProfile
            {
                Username = sample.Username,
                FullName = sample.FullName,
                Email = sample.Username.Replace('.', '_') + "@example.invalid",
                PasswordHash = passwordHash,
                MustChangePassword = true,
                SecurityStamp = UserAccountService.NewSecurityStamp(),
                IsPartyMember = cell != null,
                PartyCardNumber = cell != null ? $"MAU-{members.Count + 1:000}" : null,
                PartyCellId = cell?.Id,
                DepartmentId = department.Id,
                PositionTitle = sample.Title,
                JobGroup = sample.JobGroup
            };
            members[sample.Username] = member;
            context.PartyMemberProfiles.Add(member);

            // Chức vụ (kể cả kiêm nhiệm) theo danh mục mặc định; thẩm quyền phê duyệt suy ra từ chức vụ.
            var held = new List<HeldPosition>();
            foreach (var sp in sample.Positions)
            {
                if (!positions.TryGetValue(sp.Position, out var position))
                    continue; // danh mục chức vụ đã được quản trị sửa — bỏ qua chức vụ mẫu không còn
                var isCell = cells.TryGetValue(sp.UnitCode, out var unitCell);
                context.MemberPositions.Add(new MemberPosition
                {
                    UserId = member.Id,
                    PositionId = position.Id,
                    PartyCellId = isCell ? unitCell!.Id : null,
                    DepartmentId = isCell ? null : departments[sp.UnitCode].Id,
                    IsPrimary = sp.IsPrimary,
                    ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Note = "Chức vụ mẫu (Database:SeedSampleData)."
                });
                held.Add(new HeldPosition(position.Name, position.StatCode, position.DefaultApprovalAuthority));
            }
            member.ApprovalAuthority = PositionRules.DeriveApprovalAuthority(held);

            foreach (var (roleCode, scope) in sample.Roles)
            {
                if (!roles.TryGetValue(roleCode, out var roleId))
                    continue;
                var scopeId = scope switch
                {
                    RoleScopeType.Department => department.Id,
                    RoleScopeType.PartyCell => cell?.Id,
                    _ => null
                };
                if (scope != RoleScopeType.Global && scopeId == null)
                    continue;

                context.Set<UserRoleAssignment>().Add(new UserRoleAssignment
                {
                    UserId = member.Id,
                    RoleId = roleId,
                    ScopeType = scope,
                    ScopeId = scopeId,
                    ValidFrom = now,
                    Note = "Gán mẫu (Database:SeedSampleData)."
                });
            }
        }

        var settings = PeriodSettings.TransitionQ3Preset();
        var period = new EvaluationPeriod
        {
            Year = 2026,
            Quarter = EvaluationQuarter.Quy3,
            Name = "Đánh giá, xếp loại cán bộ Quý III/2026",
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = PeriodStatus.Open,
            Settings = settings.ToJson(),
            StatusChangedAt = now
        };
        context.EvaluationPeriods.Add(period);

        foreach (var sample in SampleAccounts.Where(s => s.RecordStatus.HasValue))
            AddSampleRecord(context, period, settings, members[sample.Username], sample.RecordStatus!.Value, now);

        await context.SaveChangesAsync();

        var usernames = string.Join(", ", SampleAccounts.Select(s => s.Username));
        if (generated)
        {
            // Ghi đúng một lần, khi tạo: không lưu ở đâu khác; tài khoản bắt buộc đổi mật khẩu ở lần đăng nhập đầu.
            logger?.LogWarning(
                "Đã tạo dữ liệu mẫu (Database:SeedSampleData). Tài khoản mẫu: {Usernames}. Mật khẩu tạm chung (chỉ hiển thị lần này, "
                + "phải đổi ở lần đăng nhập đầu): {SamplePassword}. Không bật dữ liệu mẫu trên CSDL thật.",
                usernames, password);
        }
        else
        {
            logger?.LogWarning(
                "Đã tạo dữ liệu mẫu (Database:SeedSampleData). Tài khoản mẫu: {Usernames}; mật khẩu tạm theo cấu hình Seed:SamplePassword, "
                + "phải đổi ở lần đăng nhập đầu. Không bật dữ liệu mẫu trên CSDL thật.", usernames);
        }
    }

    /// <summary>
    /// Thêm hồ sơ mẫu đang chờ <paramref name="status"/>: ảnh chụp đơn vị/tổ chức Đảng/khung/cấp quyết định như khi thêm người vào kỳ,
    /// điền dữ liệu của các bước đã qua và ghi lịch sử từng bước.
    /// </summary>
    private static void AddSampleRecord(
        CongTacDangDbContext context, EvaluationPeriod period, PeriodSettings settings, PartyMemberProfile member,
        RecordStatus status, DateTime now)
    {
        var record = new EvaluationRecord
        {
            PeriodId = period.Id,
            MemberId = member.Id,
            DepartmentId = member.DepartmentId,
            PartyCellId = member.PartyCellId,
            JobGroup = member.JobGroup,
            ApprovalAuthority = member.ApprovalAuthority,
            UpdatedAt = now
        };

        // Hồ sơ luồng mặc định theo cấp quyết định (như khi thêm người vào kỳ): cấp trên → B3b/B3c/B4 do cấp trên thực hiện.
        var profile = settings.ResolveProfile(null, member.ApprovalAuthority);
        record.WorkflowProfileCode = profile.Code;
        var enabled = profile.ActiveSteps();
        var current = RecordStateMachine.Initial(enabled);
        var at = new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc);
        context.EvaluationRecordHistories.Add(new EvaluationRecordHistory
        {
            RecordId = record.Id, ToStatus = current, Action = WorkflowAction.Create,
            ActorName = "Dữ liệu mẫu", Comment = "Thêm vào danh sách được đánh giá (dữ liệu mẫu).", CreatedAt = at
        });

        while (current != status)
        {
            var step = WorkflowSteps.StepOf(current)
                ?? throw new InvalidOperationException($"Trạng thái mẫu {status} không đạt được theo cấu hình kỳ.");
            at = at.AddDays(1);
            FillStep(record, step, at);
            var external = profile.Mode(step) == StepMode.External;
            if (external)
                AddSampleExternalResult(context, record, step, at);
            var next = RecordStateMachine.NextAfter(step, enabled);
            context.EvaluationRecordHistories.Add(new EvaluationRecordHistory
            {
                RecordId = record.Id, FromStatus = current, ToStatus = next, Step = step,
                Action = external ? WorkflowAction.RecordExternal : WorkflowAction.Complete,
                ScoreAfter = record.EffectiveScore(), GradeAfter = record.EffectiveGrade(),
                ActorName = "Dữ liệu mẫu", Comment = WorkflowSteps.DisplayName(step), CreatedAt = at
            });
            current = next;
        }

        record.Status = current;
        context.EvaluationRecords.Add(record);
    }

    /// <summary>Kết quả mẫu của bước do cấp trên thực hiện (khớp dữ liệu đã điền vào hồ sơ ở <see cref="FillStep"/>).</summary>
    private static void AddSampleExternalResult(CongTacDangDbContext context, EvaluationRecord record, WorkflowStep step, DateTime at)
    {
        var (authority, grade, score, comment) = step switch
        {
            WorkflowStep.B3B_APPRAISAL => ("Ban Tổ chức Đảng ủy Tổng công ty (dữ liệu mẫu)", record.AppraisalProposedGrade, record.AppraisalScore, record.AppraisalComment),
            WorkflowStep.B3C_DIRECTOR => ("Hội đồng thành viên Tổng công ty (dữ liệu mẫu)", record.DirectorProposedGrade, (double?)null, record.DirectorComment),
            WorkflowStep.B4_DECISION => ("Ban Thường vụ Đảng ủy Tổng công ty (dữ liệu mẫu)", record.FinalGrade, (double?)record.FinalScore, "Quyết định mức xếp loại (dữ liệu mẫu)."),
            _ => ("Cấp trên (dữ liệu mẫu)", EvaluationGrade.ChuaXepLoai, (double?)null, (string?)null)
        };
        switch (step)
        {
            case WorkflowStep.B3B_APPRAISAL:
                record.AppraisedByName = authority;
                break;
            case WorkflowStep.B3C_DIRECTOR:
                record.DirectorReviewedByName = authority;
                break;
            case WorkflowStep.B4_DECISION:
                record.DecisionAuthorityName = authority;
                break;
        }
        context.Set<EvaluationExternalResult>().Add(new EvaluationExternalResult
        {
            RecordId = record.Id,
            Step = step,
            AuthorityName = authority,
            DocumentNumber = step == WorkflowStep.B4_DECISION ? record.DecisionDocumentNumber : null,
            DocumentDate = step == WorkflowStep.B4_DECISION ? record.DecisionDocumentDate : null,
            Comment = comment,
            Grade = grade,
            Score = score,
            RecordedByName = "Văn phòng Đảng ủy (dữ liệu mẫu)",
            RecordedAt = at,
            CreatedAt = at
        });
    }

    /// <summary>Điền dữ liệu hợp lệ của một bước đã hoàn thành (mẫu tự chấm 09B: 6 trục).</summary>
    private static void FillStep(EvaluationRecord record, WorkflowStep step, DateTime at)
    {
        switch (step)
        {
            case WorkflowStep.B2_SELF_SCORE:
                (record.GeneralScoreT1, record.GeneralScoreT2, record.GeneralScoreT3) = (4.5, 4.5, 4.5);
                (record.GeneralScoreT4, record.GeneralScoreT5, record.GeneralScoreT6) = (4.5, 4.5, 4.5);
                record.GeneralCriteriaScore = 27.0;
                (record.AxisScoreT1, record.AxisScoreT2, record.AxisScoreT3) = (13.0, 9.0, 9.0);
                (record.AxisScoreT4, record.AxisScoreT5, record.AxisScoreT6) = (13.0, 9.0, 9.0);
                record.TasksScore = 62.0;
                record.TotalSelfScore = 89.0;
                record.SelfProposedGrade = EvaluationGrade.HoanThanhTot;
                record.SelfScoreForm = PeriodSettings.Form09B;
                record.SelfScoredAt = at;
                break;
            case WorkflowStep.B2_CELL_CONFIRM:
                record.PartyCellComment = "Chi bộ xác nhận phiếu tự chấm đúng thực tế (dữ liệu mẫu).";
                record.CellConfirmedByName = "Chi ủy Chi bộ (dữ liệu mẫu)";
                record.CellConfirmedAt = at;
                break;
            case WorkflowStep.B3A_COLLECTIVE:
                record.CollectiveProposedGrade = EvaluationGrade.HoanThanhTot;
                record.CollectiveComment = "Tập thể lãnh đạo thống nhất đề xuất (dữ liệu mẫu).";
                record.CollectiveRecordedByName = "Thư ký tập thể (dữ liệu mẫu)";
                record.CollectiveRecordedAt = at;
                break;
            case WorkflowStep.B3B_APPRAISAL:
                record.AppraisalScore = 88.5;
                record.AppraisalComment = "Hồ sơ đầy đủ minh chứng theo Hướng dẫn 03-HD/TVĐU (dữ liệu mẫu).";
                record.AppraisalProposedGrade = EvaluationGrade.HoanThanhTot;
                record.AppraisedByName = "Cơ quan thẩm định (dữ liệu mẫu)";
                record.AppraisedAt = at;
                break;
            case WorkflowStep.B3C_DIRECTOR:
                record.DirectorComment = "Nhất trí với kết quả thẩm định (dữ liệu mẫu).";
                record.DirectorProposedGrade = EvaluationGrade.HoanThanhTot;
                record.DirectorReviewedByName = "Cấp trực tiếp sử dụng (dữ liệu mẫu)";
                record.DirectorReviewedAt = at;
                break;
            case WorkflowStep.B4_DECISION:
                record.FinalGrade = EvaluationGrade.HoanThanhTot;
                record.FinalScore = record.AppraisalScore ?? record.TotalSelfScore;
                record.DecisionDocumentNumber = "01-QĐ/MẪU";
                record.DecisionDocumentDate = at;
                record.DecisionAuthorityName = record.ApprovalAuthority == ApprovalAuthority.CapTren
                    ? "Ban Thường vụ Đảng ủy Tổng công ty (dữ liệu mẫu)"
                    : "Đảng ủy cơ sở (dữ liệu mẫu)";
                record.DecisionRecordedByName = "Văn phòng Đảng ủy (dữ liệu mẫu)";
                record.DecisionRecordedAt = at;
                break;
            case WorkflowStep.B5_PUBLISH:
                record.PublishedByName = "Văn phòng Đảng ủy (dữ liệu mẫu)";
                record.PublishedAt = at;
                break;
        }
    }

    #endregion
}
