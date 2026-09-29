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
using CongTacDang.Application.Reports;
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

    /// <summary>Quyền của vai trò quản trị hệ thống: <c>system.*</c>, <c>catalog.manage</c>.</summary>
    private static readonly string[] AdministratorPermissionCodes = PermissionCodes.All
        .Where(code => code.StartsWith("system.", StringComparison.Ordinal)
            || code == PermissionCodes.CatalogManage)
        .ToArray();

    /// <summary>Cấu hình mặc định (docs/thiet-ke/phan-quyen.md mục 6).</summary>
    private static readonly RoleDefinition[] DefaultRoles =
    {
        // Task 20 (ĐỀ XUẤT, chờ nghiệp vụ xác nhận): xem kết quả công khai theo phạm vi bản gán (mẫu gán Toàn công ty), gửi kiến nghị.
        new(RoleCodes.Evaluatee, "Người được đánh giá", "Tham gia đánh giá bản thân (HD03 IV.1, IV.2); xem kết quả đã công bố trong phạm vi được gán, "
            + "gửi kiến nghị về kết quả của mình (Bước 5; PL II III.2). Phạm vi gán điển hình: Toàn công ty.",
            new[] { PermissionCodes.EvaluationSelf, PermissionCodes.EvaluationResultsView, PermissionCodes.EvaluationAppealSubmit }),
        new(RoleCodes.DepartmentLeader, "Lãnh đạo Phòng", "Xem hồ sơ, duyệt danh mục sản phẩm của Phòng (HD03 IV.1; PL II mục II); đề xuất mức thay cấp trực tiếp sử dụng "
            + "ở hồ sơ luồng được cấu hình (PL III ví dụ 3); lập kế hoạch khắc phục 30-60-90 ngày (Mẫu 17). Phạm vi gán điển hình: Phòng.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationTasksApprove, PermissionCodes.EvaluationUnitReview, PermissionCodes.EvaluationImprovementManage }),
        new(RoleCodes.CollectiveSecretary, "Thư ký tập thể lãnh đạo", "Ghi nhận đề xuất của tập thể lãnh đạo, lập biên bản (HD03 IV.3a; Mẫu 11–13). Phạm vi gán điển hình: Phòng hoặc Toàn công ty.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCollectiveRecord, PermissionCodes.MeetingRead, PermissionCodes.MeetingManage }),
        new(RoleCodes.CellCommittee, "Chi ủy / Bí thư Chi bộ", "Chi bộ xác nhận phiếu tự chấm, lập hồ sơ tập thể (Mẫu 09A–9D, Mẫu 07). Phạm vi gán điển hình: Chi bộ.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCellConfirm, PermissionCodes.CollectiveManage, PermissionCodes.MeetingRead }),
        new(RoleCodes.Appraisal, "Cơ quan thẩm định (Phòng TCCB-LĐ)", "Rà soát, thẩm định; quản lý kỳ đánh giá (HD03 IV.1, IV.3b). Phạm vi gán: Toàn công ty.",
            new[]
            {
                PermissionCodes.EvaluationRead, PermissionCodes.EvaluationAppraise, PermissionCodes.PeriodManage,
                PermissionCodes.CriteriaManage, PermissionCodes.ReportExport, PermissionCodes.SystemUsersRead
            }),
        new(RoleCodes.DirectSupervisor, "Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)", "Nhận xét, đề xuất của cấp trực tiếp sử dụng cán bộ (HD03 IV.3c); lập kế hoạch khắc phục 30-60-90 ngày (Mẫu 17). Phạm vi gán: Toàn công ty.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationDirectorReview, PermissionCodes.ReportExport, PermissionCodes.EvaluationImprovementManage }),
        new(RoleCodes.PartyCommitteeMember, "Cấp ủy viên Đảng ủy", "Xem hồ sơ, biên bản, báo cáo (HD03 IV.4; Mẫu 18). Phạm vi gán: Toàn công ty.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.MeetingRead, PermissionCodes.ReportExport }),
        new(RoleCodes.PartyOffice, "Văn phòng Đảng ủy (ghi nhận quyết định)", "Ghi nhận quyết định của Đảng ủy cơ sở, ghi nhận kết quả của cấp trên "
            + "(thẩm định, nhận xét, quyết định do cấp trên thực hiện), công bố, mở lại hồ sơ, xử lý kiến nghị sau công bố (HD03 IV.4, IV.5; PL II III.2). Phạm vi gán: Toàn công ty.",
            new[]
            {
                PermissionCodes.EvaluationRead, PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationExternalRecord,
                PermissionCodes.EvaluationPublish, PermissionCodes.EvaluationReopen, PermissionCodes.MeetingRead,
                PermissionCodes.MeetingManage, PermissionCodes.ReportExport, PermissionCodes.EvaluationAppealResolve
            }),
        new(RoleCodes.Administrator, "Quản trị hệ thống", "Quản trị kỹ thuật: tài khoản, vai trò, gán vai trò, nhật ký, danh mục, thông tin đơn vị, file mẫu biểu mẫu Word. "
            + "Không xem nội dung đánh giá (Mẫu 18).",
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

        // 2c. Hai bộ tiêu chí mặc định theo bản trích xuất HD03 (chỉ khi chưa có bộ nào — task 16, chờ nghiệp vụ xác nhận).
        await SeedCriteriaSetsAsync(context, logger);

        // 2d. Thông tin đơn vị mặc định (chỉ khi chưa có — task 17).
        await SeedOrganizationSettingsAsync(context, logger);

        // 3. Dữ liệu mẫu (chỉ môi trường thử nghiệm).
        if (sampleData?.Enabled == true)
            await SeedSampleDataAsync(context, sampleData, logger);

        // 4. Tài khoản quản trị ban đầu (độc lập với dữ liệu mẫu) — chỉ khi hệ thống chưa có quản trị nào.
        await SeedInitialAdministratorAsync(context, initialAdmin, logger);
    }

    #region Bộ tiêu chí mặc định

    /// <summary>
    /// Tạo hai bộ tiêu chí đã xuất bản theo bản trích xuất HD03 (<see cref="CriteriaSetDefaults"/>): "Mẫu 09B — Quý III/2026" và
    /// "Mẫu 09A — từ 2027" — chỉ khi CSDL chưa có bộ tiêu chí nào (không ghi đè bộ đã sửa).
    /// </summary>
    private static async Task SeedCriteriaSetsAsync(CongTacDangDbContext context, ILogger? logger)
    {
        if (await context.Set<CriteriaSet>().AnyAsync())
            return;

        var now = DateTime.UtcNow;
        context.Set<CriteriaSet>().AddRange(
            DefaultCriteriaSet(CriteriaSetDefaults.Code09B, CriteriaSetDefaults.Name09B, CriteriaSetContent.Form09B, CriteriaSetDefaults.Build09B(), now),
            DefaultCriteriaSet(CriteriaSetDefaults.Code09A, CriteriaSetDefaults.Name09A, CriteriaSetContent.Form09A, CriteriaSetDefaults.Build09A(), now.AddSeconds(-1)));
        await context.SaveChangesAsync();
        logger?.LogInformation("Đã tạo 2 bộ tiêu chí mặc định theo bản trích xuất HD03 (chờ nghiệp vụ xác nhận).");
    }

    private static CriteriaSet DefaultCriteriaSet(string code, string name, string form, CriteriaSetContent content, DateTime publishedAt) => new()
    {
        Code = code,
        Name = name,
        SelfScoreForm = form,
        Notes = CriteriaSetDefaults.Notes,
        Status = CriteriaSetStatus.Published,
        Content = content.ToJson(),
        PublishedAt = publishedAt,
        CreatedAt = publishedAt,
        UpdatedAt = publishedAt
    };

    #endregion

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

    #region Thông tin đơn vị mặc định (task 17)

    /// <summary>
    /// Thông tin đơn vị mặc định — đúng các chuỗi trước đây ghi cứng trong code xuất biểu mẫu/báo cáo và giao diện, để biểu mẫu
    /// không đổi khi chưa sửa. Đây là nơi duy nhất trong code chứa tên đơn vị; quản trị sửa trên giao diện (Quản trị → Thông tin đơn vị).
    /// </summary>
    public static OrganizationSettings DefaultOrganizationSettings() => new()
    {
        Id = OrganizationSettings.SingletonId,
        PartyCommitteeName = "ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY",
        SuperiorPartyName = "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM",
        CompanyName = "Công ty TNHH Kỹ thuật Quản lý bay",
        ParentCompanyName = "Tổng công ty Quản lý bay Việt Nam",
        ShortName = "ATTECH",
        Location = "Hà Nội",
        SystemName = "Đảng bộ ATTECH"
    };

    /// <summary>Tạo bản ghi thông tin đơn vị mặc định khi chưa có — không bao giờ ghi đè giá trị quản trị đã sửa.</summary>
    private static async Task SeedOrganizationSettingsAsync(CongTacDangDbContext context, ILogger? logger)
    {
        if (await context.Set<OrganizationSettings>().AnyAsync())
            return;

        context.Set<OrganizationSettings>().Add(DefaultOrganizationSettings());
        await context.SaveChangesAsync();
        logger?.LogInformation("Đã tạo thông tin đơn vị mặc định.");
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

    /// <summary>
    /// Một tài khoản mẫu kèm chức vụ, bản gán vai trò và trạng thái hồ sơ trong kỳ mẫu (null = không được đánh giá);
    /// <paramref name="Grade"/>: mức đề xuất/quyết định của các bước sau tự chấm (mặc định Hoàn thành tốt).
    /// </summary>
    private sealed record SampleAccount(
        string Username, string FullName, string Department, string? PartyCell, SamplePosition[] Positions,
        string Title, string WeightFrameCode, (string Role, RoleScopeType Scope)[] Roles, RecordStatus? RecordStatus,
        EvaluationGrade Grade = EvaluationGrade.HoanThanhTot);

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
            "Chuyên viên CNTT", "K4",
            new[] { (RoleCodes.Administrator, RoleScopeType.Global) }, null),
        new("giamdoc", "Lê Tiến Thịnh", "BGD", "CB-VP",
            new[] { new SamplePosition("Giám đốc", "ATTECH", true), new SamplePosition("Bí thư Đảng ủy", "DU-ATTECH", false) },
            "Bí thư Đảng ủy, Giám đốc Công ty", "K1",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.DirectSupervisor, RoleScopeType.Global) },
            RecordStatus.Published),
        new("vanphong", "Phạm Thu Hà", "PH-KH", "CB-VP", new[] { new SamplePosition("Chuyên viên", "PH-KH", true) },
            "Chuyên viên Văn phòng Đảng ủy", "K1",
            new[] { (RoleCodes.PartyOffice, RoleScopeType.Global), (RoleCodes.PartyCommitteeMember, RoleScopeType.Global) }, null),
        new("thamdinh", "Vũ Đình Hùng", "PH-TCCB", "CB-VP",
            new[] { new SamplePosition("Trưởng phòng", "PH-TCCB", true), new SamplePosition("Đảng ủy viên", "DU-ATTECH", false) },
            "Trưởng phòng Tổ chức cán bộ - Lao động", "K1",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.Appraisal, RoleScopeType.Global) },
            RecordStatus.AwaitingDirectorReview),
        new("truongphong.kt", "Nguyễn Văn Hùng", "PH-KT", "CB-KT",
            new[] { new SamplePosition("Trưởng phòng", "PH-KT", true), new SamplePosition("Chi ủy viên", "CB-KT", false) },
            "Trưởng phòng Kỹ thuật", "K2",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.DepartmentLeader, RoleScopeType.Department) },
            RecordStatus.AwaitingAppraisal),
        new("bithu.kt", "Trần Minh Đức", "PH-KT", "CB-KT",
            new[] { new SamplePosition("Phó Trưởng phòng", "PH-KT", true), new SamplePosition("Bí thư Chi bộ", "CB-KT", false) },
            "Bí thư Chi bộ, Phó Trưởng phòng Kỹ thuật", "K2",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global), (RoleCodes.CellCommittee, RoleScopeType.PartyCell) },
            RecordStatus.AwaitingCollective),
        new("thuky.kt", "Đỗ Thị Lan", "PH-KT", "CB-KT", new[] { new SamplePosition("Chuyên viên", "PH-KT", true) },
            "Chuyên viên, Thư ký tập thể lãnh đạo Phòng Kỹ thuật", "K2",
            new[] { (RoleCodes.CollectiveSecretary, RoleScopeType.Department) }, null),
        new("canbo.kt1", "Trần Quốc Tuấn", "PH-KT", "CB-KT", new[] { new SamplePosition("Phó Trưởng phòng", "PH-KT", true) },
            "Phó Trưởng phòng Kỹ thuật", "K2",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global) }, RecordStatus.AwaitingSelfScore),
        new("canbo.kt2", "Hoàng Văn Nam", "PH-KT", "CB-KT", new[] { new SamplePosition("Phó Trưởng phòng", "PH-KT", true) },
            "Phó Trưởng phòng Kỹ thuật", "K2",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global) }, RecordStatus.AwaitingCellConfirm),
        // Đã công bố mức C: có kế hoạch 30-60-90 ngày (Mẫu 17) đang lập và một kiến nghị chờ xử lý (thử chức năng sau công bố).
        new("canbo.kh", "Nguyễn Thị Mai", "PH-KH", "CB-VP", new[] { new SamplePosition("Phó Trưởng phòng", "PH-KH", true) },
            "Phó Trưởng phòng Kế hoạch - Kinh doanh", "K2",
            new[] { (RoleCodes.Evaluatee, RoleScopeType.Global) }, RecordStatus.Published, EvaluationGrade.HoanThanh)
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
                WeightFrameCode = sample.WeightFrameCode
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

        // Bộ tiêu chí Mẫu 09B đã xuất bản mới nhất (bộ mặc định vừa seed) — chụp vào kỳ mẫu như khi mở kỳ.
        var sampleSet = await context.Set<CriteriaSet>()
            .Where(s => s.Status == CriteriaSetStatus.Published && s.SelfScoreForm == CriteriaSetContent.Form09B)
            .OrderByDescending(s => s.PublishedAt)
            .FirstOrDefaultAsync();
        if (sampleSet != null)
        {
            period.CriteriaSetId = sampleSet.Id;
            period.CriteriaSnapshot = sampleSet.TakeSnapshot(now).ToJson();
        }
        var criteria = sampleSet?.GetContent() ?? CriteriaSetDefaults.Build09B();
        context.EvaluationPeriods.Add(period);

        var records = new Dictionary<string, EvaluationRecord>(StringComparer.Ordinal);
        foreach (var sample in SampleAccounts.Where(s => s.RecordStatus.HasValue))
            records[sample.Username] = AddSampleRecord(context, period, settings, criteria, members[sample.Username], sample.RecordStatus!.Value, sample.Grade, now);

        AddSampleCollectiveAndMeetings(context, period, departments, cells, members, records);
        AddSamplePostPublish(context, period, members, records);

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
    private static EvaluationRecord AddSampleRecord(
        CongTacDangDbContext context, EvaluationPeriod period, PeriodSettings settings, CriteriaSetContent criteria, PartyMemberProfile member,
        RecordStatus status, EvaluationGrade grade, DateTime now)
    {
        var record = new EvaluationRecord
        {
            PeriodId = period.Id,
            MemberId = member.Id,
            DepartmentId = member.DepartmentId,
            PartyCellId = member.PartyCellId,
            WeightFrameCode = member.WeightFrameCode ?? string.Empty,
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
            FillStep(record, criteria, step, grade, at);
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
        return record;
    }

    /// <summary>
    /// Hồ sơ tập thể và biên bản mẫu (đợt 8): Mẫu 07 (đủ mục I.1–I.4, II–VI) và Mẫu 08 (một số nhóm nội dung) của Chi bộ Khối
    /// Kỹ thuật; biên bản Mẫu 12 hội nghị tập thể lãnh đạo Phòng Kỹ thuật (B3a, đủ mục 3.2, chức vụ chủ trì/thư ký) có kết quả
    /// kiểm phiếu của hồ sơ đã qua B3a; biên bản kiểm phiếu hội nghị Đảng ủy (B4, Mẫu 13: Tổ kiểm phiếu, số phiếu, mục I/II).
    /// </summary>
    private static void AddSampleCollectiveAndMeetings(
        CongTacDangDbContext context, EvaluationPeriod period, IReadOnlyDictionary<string, AdministrativeDepartment> departments,
        IReadOnlyDictionary<string, PartyCell> cells, IReadOnlyDictionary<string, PartyMemberProfile> members,
        IReadOnlyDictionary<string, EvaluationRecord> records)
    {
        var cell = cells["CB-KT"];
        var sections = Hd03FormCatalog.Form07Strengths.ToDictionary(
            s => s.Code, s => $"Chi ủy thực hiện tốt nội dung \"{s.Title.TrimEnd('.')}\" trong quý (dữ liệu mẫu).");
        context.CollectiveEvaluationRecords.Add(new CollectiveEvaluationRecord
        {
            PeriodId = period.Id,
            Form = CollectiveEvaluationForm.M07,
            PartyCellId = cell.Id,
            HeadId = members["bithu.kt"].Id,
            SubjectName = "Chi ủy " + cell.Name,
            Sections = System.Text.Json.JsonSerializer.Serialize(sections),
            Limitations = "Một số báo cáo định kỳ còn chậm so với yêu cầu (dữ liệu mẫu).",
            Causes = "Khối lượng công việc chuyên môn lớn; phân công theo dõi chưa sát (dữ liệu mẫu).",
            PreviousRemediation = "Đã khắc phục hạn chế về sinh hoạt chuyên đề nêu ở kỳ trước (dữ liệu mẫu).",
            Explanation = "Không có nội dung cần giải trình thêm (dữ liệu mẫu).",
            Responsibilities = "Bí thư Chi bộ chịu trách nhiệm chính về hạn chế nêu trên (dữ liệu mẫu).",
            RemediationPlan = "Quý IV: phân công đầu mối theo dõi báo cáo, kiểm tra hằng tháng (dữ liệu mẫu).",
            GeneralCriteriaScore = 27,
            TaskCriteriaScore = 63,
            TotalScore = 90,
            SelfProposedGrade = EvaluationGrade.HoanThanhTot,
            Status = CollectiveRecordStatus.Submitted
        });
        var report08 = new CollectiveEvaluationRecord
        {
            PeriodId = period.Id,
            Form = CollectiveEvaluationForm.M08,
            PartyCellId = cell.Id,
            HeadId = members["bithu.kt"].Id,
            SubjectName = "Phòng Kỹ thuật",
            Status = CollectiveRecordStatus.Submitted
        };
        foreach (var (order, category, task, plan, result, issues) in new[]
                 {
                     (1, "1", "Bảo dưỡng định kỳ hệ thống CNS/ATM", "Kế hoạch bảo dưỡng năm 2026", "Hoàn thành 100% hạng mục quý III", "Không có"),
                     (2, "2", "Sinh hoạt chuyên đề về chuyển đổi số", "Chương trình công tác năm của Chi bộ", "Tổ chức 01 chuyên đề, 100% đảng viên tham dự", "Không có"),
                     (3, "8", "Đảm bảo an toàn kỹ thuật cho điều hành bay", "Chỉ tiêu an toàn của Tổng công ty", "Không để xảy ra sự cố do chủ quan", "Không có"),
                     (4, "13", "Xử lý sự cố thiết bị đột xuất tại trạm radar", "Chỉ đạo của lãnh đạo Công ty", "Khắc phục trong 4 giờ", "Được Công ty biểu dương")
                 })
        {
            report08.Items.Add(new CollectiveEvaluationItem
            {
                ItemOrder = order, Category = category, TaskName = task + " (dữ liệu mẫu)", PlanOrDirection = plan, Result = result,
                Limitations = issues, Notes = string.Empty
            });
        }
        context.CollectiveEvaluationRecords.Add(report08);

        static string Details(object value) => System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        // B3a — hội nghị tập thể lãnh đạo Phòng Kỹ thuật (Mẫu 12), kiểm phiếu hồ sơ đã qua bước đề xuất.
        var department = departments["PH-KT"];
        var collective = new EvaluationMeeting
        {
            PeriodId = period.Id,
            DepartmentId = department.Id,
            Stage = WorkflowStep.B3A_COLLECTIVE,
            FormCode = "M12",
            MeetingType = "Hội nghị tập thể lãnh đạo, quản lý",
            Location = "Phòng họp Phòng Kỹ thuật",
            StartedAt = new DateTime(2026, 9, 9, 1, 30, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 9, 9, 3, 30, 0, DateTimeKind.Utc),
            InvitedCount = 4,
            PresentCount = 4,
            ChairName = members["truongphong.kt"].FullName,
            SecretaryName = members["thuky.kt"].FullName,
            MinutesContent = "Hội nghị nghe báo cáo kết quả thực hiện nhiệm vụ trọng tâm quý III/2026, thảo luận, nhận xét từng cán bộ (dữ liệu mẫu).",
            OutcomeContent = "Hội nghị thống nhất đề xuất mức xếp loại theo kết quả kiểm phiếu (dữ liệu mẫu).",
            Details = Details(new
            {
                workingRules = "tập thể lãnh đạo Phòng Kỹ thuật nhiệm kỳ 2025-2030",
                reportingUnit = "Phòng Kỹ thuật",
                chairTitle = "Chi ủy viên, Trưởng phòng Kỹ thuật",
                secretaryTitle = "Chuyên viên, Thư ký tập thể lãnh đạo",
                attendees = new[] { new { name = members["thuky.kt"].FullName, title = "Chuyên viên, ghi chép" } },
                countingCommittee = new[] { new { name = members["canbo.kt1"].FullName, title = "Phó Trưởng phòng Kỹ thuật" } },
                ballotsIssued = 4, ballotsCollected = 4, ballotsValid = 4, ballotsInvalid = 0
            })
        };
        var proposed = records["truongphong.kt"];
        collective.VoteSummaries.Add(new EvaluationMeetingVoteSummary { RecordId = proposed.Id, VotesGood = 4 });
        proposed.CollectiveMeetingId = collective.Id;
        context.EvaluationMeetings.Add(collective);

        // B4 — hội nghị Đảng ủy Công ty bỏ phiếu quyết định / đề nghị (Mẫu 13): mục I (cấp trên quyết định), mục II (cơ sở).
        var decision = new EvaluationMeeting
        {
            PeriodId = period.Id,
            Stage = WorkflowStep.B4_DECISION,
            FormCode = "M13",
            MeetingType = "Hội nghị đánh giá, xếp loại chất lượng cán bộ quý",
            Location = "Hội trường Công ty",
            StartedAt = new DateTime(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 9, 15, 4, 0, 0, DateTimeKind.Utc),
            InvitedCount = 7,
            PresentCount = 7,
            ChairName = members["giamdoc"].FullName,
            SecretaryName = members["vanphong"].FullName,
            Details = Details(new
            {
                workingRules = "Đảng ủy Công ty nhiệm kỳ 2025-2030",
                chairTitle = "Bí thư Đảng ủy, Giám đốc Công ty",
                secretaryTitle = "Chuyên viên Văn phòng Đảng ủy",
                attendees = Array.Empty<object>(),
                countingCommittee = new[]
                {
                    new { name = members["thamdinh"].FullName, title = "Đảng ủy viên, Trưởng phòng Tổ chức cán bộ - Lao động" },
                    new { name = members["vanphong"].FullName, title = "Chuyên viên Văn phòng Đảng ủy" }
                },
                ballotsIssued = 7, ballotsCollected = 7, ballotsValid = 7, ballotsInvalid = 0
            })
        };
        decision.VoteSummaries.Add(new EvaluationMeetingVoteSummary { RecordId = records["giamdoc"].Id, VotesExcellent = 2, VotesGood = 5, Notes = "Đề nghị Ban Thường vụ Đảng ủy Tổng công ty quyết định" });
        decision.VoteSummaries.Add(new EvaluationMeetingVoteSummary { RecordId = records["canbo.kh"].Id, VotesGood = 1, VotesSatisfactory = 5, VotesNotRated = 1 });
        records["canbo.kh"].DecisionMeetingId = decision.Id;
        context.EvaluationMeetings.Add(decision);
    }

    /// <summary>
    /// Sau công bố (đợt 8): hồ sơ mức C có kế hoạch 30-60-90 ngày (Mẫu 17) đang lập và một kiến nghị chờ xử lý; bản nháp Mẫu 16
    /// toàn Đảng bộ.
    /// </summary>
    private static void AddSamplePostPublish(
        CongTacDangDbContext context, EvaluationPeriod period, IReadOnlyDictionary<string, PartyMemberProfile> members,
        IReadOnlyDictionary<string, EvaluationRecord> records)
    {
        var record = records["canbo.kh"];
        var owner = members["canbo.kh"];
        var director = members["giamdoc"];
        var at = new DateTime(2026, 9, 20, 2, 0, 0, DateTimeKind.Utc);

        var plan = new ImprovementPlanContent
        {
            SupporterName = members["vanphong"].FullName,
            SupporterTitle = "Chuyên viên Văn phòng Đảng ủy"
        };
        var m30 = plan.Stage(ImprovementPlanContent.M30);
        m30.Limitation = "Tiến độ các báo cáo kế hoạch quý còn chậm (dữ liệu mẫu).";
        m30.Target = "Nộp đúng hạn 100% báo cáo trong tháng đầu (dữ liệu mẫu).";
        m30.Measures = "Lập lịch theo dõi hằng tuần; kèm cặp bởi Trưởng phòng (dữ liệu mẫu).";
        m30.Coordination = "Phòng Kế hoạch - Kinh doanh, Văn phòng Đảng ủy (dữ liệu mẫu).";
        context.Set<ImprovementPlan>().Add(new ImprovementPlan
        {
            RecordId = record.Id,
            Content = plan.ToJson(),
            StartDate = new DateOnly(2026, 10, 1),
            Status = ImprovementPlanStatus.Draft,
            PreparedById = director.Id,
            PreparedByName = director.FullName,
            CreatedAt = at,
            CreatedBy = director.Id
        });

        context.Set<EvaluationAppeal>().Add(new EvaluationAppeal
        {
            RecordId = record.Id,
            SubmittedById = owner.Id,
            SubmittedByName = owner.FullName,
            SubmittedAt = at.AddDays(1),
            Content = "Đề nghị xem xét lại mức xếp loại: kết quả thẩm định chưa tính đầy đủ sản phẩm đã hoàn thành của trục 1 (dữ liệu mẫu).",
            ConcernedSteps = new List<string> { WorkflowSteps.Code(WorkflowStep.B3B_APPRAISAL) },
            Status = AppealStatus.Submitted,
            CreatedAt = at.AddDays(1),
            CreatedBy = owner.Id
        });

        context.Set<ReportDraft>().Add(new ReportDraft
        {
            PeriodId = period.Id,
            PartyCellId = null,
            FormCode = "M16",
            Content = System.Text.Json.JsonSerializer.Serialize(new
            {
                documentNumber = "Số 15-BC/ĐU",
                recipient = "Ban Thường vụ Đảng ủy Tổng công ty",
                workingRules = "Đảng ủy Công ty nhiệm kỳ 2025-2030",
                proposal1 = "Đề nghị Ban Thường vụ Đảng ủy Tổng công ty xem xét, quyết định mức xếp loại đối với cán bộ thuộc diện quản lý (dữ liệu mẫu).",
                signerName = director.FullName
            }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            CreatedAt = at,
            CreatedBy = members["vanphong"].Id
        });
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

    /// <summary>
    /// Điền dữ liệu hợp lệ của một bước đã hoàn thành (tự chấm theo bộ Mẫu 09B của kỳ: tiêu chí con "Đảm bảo" trừ hai tiêu chí
    /// cuối "Không đảm bảo" có căn cứ; mỗi trục thấp hơn điểm tối đa 1–2 điểm).
    /// </summary>
    private static void FillStep(EvaluationRecord record, CriteriaSetContent criteria, WorkflowStep step, EvaluationGrade grade, DateTime at)
    {
        switch (step)
        {
            case WorkflowStep.B2_SELF_SCORE:
                var items = criteria.AllItems.Select(x => x.Item).ToList();
                var general = items.ToDictionary(
                    i => i.Code,
                    i => items.IndexOf(i) >= items.Count - 2
                        ? new GeneralItemScore { Score = 0, Reason = "Còn hạn chế cần khắc phục trong quý (dữ liệu mẫu)." }
                        : new GeneralItemScore { Score = i.MaxScore });
                var axes = criteria.Axes.ToDictionary(a => a.Code, a => Math.Max(0, a.MaxScore - (a.MaxScore >= 15 ? 2 : 1)));
                var rounding = criteria.Parameters.Rounding;
                record.GeneralScores = EvaluationScoring.GeneralScoresToJson(criteria, general);
                record.GeneralCriteriaScore = EvaluationScoring.GeneralCriteriaScore(criteria, general);
                record.AxisScores = EvaluationScoring.AxisScoresToJson(criteria, axes);
                record.TasksScore = EvaluationScoring.AxisTasksScore(criteria, axes);
                record.TotalSelfScore = EvaluationScoring.TotalScore(record.GeneralCriteriaScore, record.TasksScore, rounding.Total);
                record.SelfProposedGrade = EvaluationScoring.SuggestGrade(criteria, record.TotalSelfScore, null);
                record.SelfScoreForm = CriteriaSetContent.Form09B;
                record.SelfScoredAt = at;
                FillIndividualForms(record, criteria);
                break;
            case WorkflowStep.B2_CELL_CONFIRM:
                record.PartyCellComment = "Chi bộ xác nhận phiếu tự chấm đúng thực tế (dữ liệu mẫu).";
                record.CellConfirmedByName = "Chi ủy Chi bộ (dữ liệu mẫu)";
                record.CellConfirmedAt = at;
                break;
            case WorkflowStep.B3A_COLLECTIVE:
                record.CollectiveProposedGrade = grade;
                record.CollectiveComment = "Tập thể lãnh đạo thống nhất đề xuất (dữ liệu mẫu).";
                record.CollectiveRecordedByName = "Thư ký tập thể (dữ liệu mẫu)";
                record.CollectiveRecordedAt = at;
                break;
            case WorkflowStep.B3B_APPRAISAL:
                record.AppraisalScore = grade == EvaluationGrade.HoanThanh ? 65 : 88.5;
                record.AppraisalExplanation = EvaluationScoring.RequiresExplanation(criteria, record.TotalSelfScore, record.AppraisalScore)
                    ? "Điều chỉnh theo minh chứng bổ sung (dữ liệu mẫu)."
                    : null;
                record.AppraisalComment = "Hồ sơ đầy đủ minh chứng theo Hướng dẫn 03-HD/TVĐU (dữ liệu mẫu).";
                record.AppraisalProposedGrade = grade;
                record.AppraisedByName = "Cơ quan thẩm định (dữ liệu mẫu)";
                record.AppraisedAt = at;
                break;
            case WorkflowStep.B3C_DIRECTOR:
                record.DirectorComment = "Nhất trí với kết quả thẩm định (dữ liệu mẫu).";
                record.DirectorProposedGrade = grade;
                record.DirectorReviewedByName = "Cấp trực tiếp sử dụng (dữ liệu mẫu)";
                record.DirectorReviewedAt = at;
                break;
            case WorkflowStep.B4_DECISION:
                record.FinalGrade = grade;
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

    /// <summary>Nội dung Mẫu 09C (mỗi mục của bộ), Mẫu 9D (một dòng mỗi trục) và phần tự luận theo trục của Mẫu 09B.</summary>
    private static void FillIndividualForms(EvaluationRecord record, CriteriaSetContent criteria)
    {
        if (criteria.RequiredForms.Contains(RecordFormCodes.Form09C))
            record.SelfAssessment = RecordFormContent.SelfAssessmentToJson(criteria, criteria.SelfAssessmentSections.ToDictionary<SelfAssessmentSection, string, string?>(
                s => s.Code,
                s => "Trong quý, tôi hoàn thành các nhiệm vụ trọng tâm theo 6 trục; nhiệm vụ an toàn, chất lượng đạt Mức 1 - Hoàn thành "
                    + "đúng hạn, đạt yêu cầu. Còn hạn chế về tiến độ báo cáo, sẽ khắc phục trong quý IV (dữ liệu mẫu)."));
        if (criteria.RequiredForms.Contains(RecordFormCodes.Form9D))
            record.TaskResults = RecordFormContent.TaskResultsToJson(criteria, criteria.Axes.Select(a => (TaskResultRow?)new TaskResultRow
            {
                AxisCode = a.Code,
                Content = $"Nhiệm vụ trọng tâm thuộc {a.Name.ToLowerInvariant()} (dữ liệu mẫu)",
                Deadline = "30/9/2026",
                Status = "Đã thực hiện theo kế hoạch",
                Product = "Báo cáo kết quả, hồ sơ minh chứng",
                Progress = "Đúng tiến độ"
            }).ToList());
        if (criteria.Axes.All(a => a.MaxScore > 0))
            record.AxisNotes = RecordFormContent.AxisNotesToJson(criteria, criteria.Axes.ToDictionary(
                a => a.Code,
                a => (AxisNote?)new AxisNote { Target = "Hoàn thành chỉ tiêu được giao trong quý (dữ liệu mẫu)", Result = "Đạt; minh chứng: báo cáo quý III (dữ liệu mẫu)" }));
    }

    #endregion
}
