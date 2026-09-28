using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Application.Common.Security;

namespace CongTacDang.Infrastructure.Data;

/// <summary>
/// Khởi tạo dữ liệu nền: danh mục quyền (từ <see cref="PermissionCodes"/>), vai trò mặc định
/// (docs/thiet-ke/phan-quyen.md mục 6 — ĐỀ XUẤT, chờ nghiệp vụ xác nhận)
/// và dữ liệu mẫu khi bật <c>Database:SeedSampleData</c>.
/// <para>
/// Đây là nơi <b>duy nhất</b> biết mã vai trò (<see cref="AppRole.Code"/>) — chỉ để tìm vai trò mặc định; logic phân quyền
/// chỉ dùng mã quyền.
/// </para>
/// </summary>
public static class DataSeeder
{
    /// <summary>Định nghĩa một vai trò mặc định và bộ quyền của nó.</summary>
    private sealed record RoleDefinition(string Code, string Name, string Description, string[] Permissions, bool IsProtected = false);

    /// <summary>Mã vai trò mặc định (chỉ seeder dùng).</summary>
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

        // Vai trò cũ (trước task 09) — chỉ dùng để nhận biết CSDL chưa có vai trò cấu hình mới. Gán vai trò cũ
        // (bảng user_roles) được migration Wave4 chuyển thành bản gán Global của chính vai trò đó.
        public const string LegacyCadre = "CAN_BO";
        public const string LegacyCellSecretary = "BI_THU_CHI_BO";
        public const string LegacyAppraisal = "TO_THAM_DINH";
        public const string LegacyStandingCommittee = "BAN_THUONG_VU";
        public const string LegacyBaseCommittee = "DANG_UY_CO_SO";

        public static readonly string[] Legacy =
        {
            LegacyCadre, LegacyCellSecretary, LegacyAppraisal, LegacyStandingCommittee, LegacyBaseCommittee, Administrator
        };
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
        new(RoleCodes.DepartmentLeader, "Lãnh đạo Phòng", "Xem hồ sơ và duyệt danh mục sản phẩm của Phòng (HD03 IV.1; PL II mục II). Phạm vi gán điển hình: Phòng.",
            new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationTasksApprove }),
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
        new(RoleCodes.PartyOffice, "Văn phòng Đảng ủy (ghi nhận quyết định)", "Ghi nhận quyết định, công bố, mở lại hồ sơ (HD03 IV.4, IV.5). Phạm vi gán: Toàn công ty.",
            new[]
            {
                PermissionCodes.EvaluationRead, PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationDecideExternal,
                PermissionCodes.EvaluationPublish, PermissionCodes.EvaluationReopen, PermissionCodes.MeetingRead,
                PermissionCodes.MeetingManage, PermissionCodes.ReportExport
            }),
        new(RoleCodes.Administrator, "Quản trị hệ thống", "Quản trị kỹ thuật: tài khoản, vai trò, gán vai trò, nhật ký, danh mục, văn bản chung. Không xem nội dung đánh giá (Mẫu 18).",
            AdministratorPermissionCodes, IsProtected: true)
    };

    /// <summary>
    /// Khởi tạo dữ liệu nền. Vai trò mặc định chỉ được tạo khi CSDL chưa có vai trò nào ngoài các vai trò cũ;
    /// quyền của vai trò đã tồn tại không bị ghi đè trừ khi <paramref name="resetRolePermissions"/> = true (Database:ResetRolePermissions).
    /// Không bao giờ gán lại vai trò cho người đã có bản gán (T-46).
    /// </summary>
    public static async Task SeedAsync(
        CongTacDangDbContext context,
        bool seedSampleData = true,
        bool resetRolePermissions = false,
        ILogger? logger = null)
    {
        // 1. Danh mục quyền: đồng bộ từ PermissionCodes (tạo mới, cập nhật tên/mô tả/phân hệ/thứ tự).
        await SyncPermissionCatalogAsync(context, logger);

        // 2. Vai trò mặc định (chỉ khi chưa có vai trò nào ngoài vai trò cũ), vai trò quản trị được bảo vệ.
        await SeedDefaultRolesAsync(context, logger);
        if (resetRolePermissions)
            await ResetRolePermissionsAsync(context, logger);

        if (!seedSampleData)
            return;

        // 4. Seed Chi bộ Đảng tại ATTECH
        if (!await context.PartyCells.AnyAsync())
        {
            var cellKt = new PartyCell { Code = "CB-KT", Name = "Chi bộ Khối Kỹ thuật", Description = "Chi bộ phụ trách an toàn, điều hành kỹ thuật CNS, ATM" };
            var cellSx = new PartyCell { Code = "CB-SX", Name = "Chi bộ Sản xuất công nghiệp", Description = "Chi bộ phụ trách Xưởng sản xuất thiết bị hàng không" };
            var cellDv = new PartyCell { Code = "CB-DV", Name = "Chi bộ Dịch vụ kỹ thuật", Description = "Chi bộ phụ trách dịch vụ lắp đặt, bảo dưỡng" };
            var cellVp = new PartyCell { Code = "CB-VP", Name = "Chi bộ Khối Văn phòng", Description = "Chi bộ phụ trách Kế hoạch, Tài chính, TCCB-LĐ" };

            await context.PartyCells.AddRangeAsync(cellKt, cellSx, cellDv, cellVp);
            await context.SaveChangesAsync();
        }

        // 5. Seed Phòng ban Chính quyền tại ATTECH
        if (!await context.AdministrativeDepartments.AnyAsync())
        {
            var depKh = new AdministrativeDepartment { Code = "PH-KH", Name = "Phòng Kế hoạch", Description = "Phòng Kế hoạch đầu tư, dự án" };
            var depTc = new AdministrativeDepartment { Code = "PH-TC", Name = "Phòng Tài chính - Kế toán", Description = "Phòng Tài chính kế toán công ty" };
            var depTccb = new AdministrativeDepartment { Code = "PH-TCCB", Name = "Phòng Tổ chức cán bộ - Lao động", Description = "Phòng tham mưu tổ chức nhân sự, lao động tiền lương" };
            var depXs = new AdministrativeDepartment { Code = "XUONG-SX", Name = "Xưởng Sản xuất thiết bị", Description = "Xưởng sản xuất công nghiệp cơ khí, điện tử" };

            await context.AdministrativeDepartments.AddRangeAsync(depKh, depTc, depTccb, depXs);
            await context.SaveChangesAsync();
        }

        // 6. Seed Cán bộ / Đảng viên mẫu (vai trò gán ở bước 7 qua bản gán có phạm vi)
        await SeedSampleUsersAsync(context);

        // 7. Gán vai trò mẫu kèm phạm vi — chỉ cho tài khoản mẫu chưa có bản gán nào (T-46)
        await SeedSampleAssignmentsAsync(context, logger);

        // 8. Seed Kỳ đánh giá hiện hành (Quý III/2026) theo Hướng dẫn 03-HD/TVĐU
        if (!await context.EvaluationPeriods.AnyAsync())
        {
            var periodQ3 = new EvaluationPeriod
            {
                Id = Guid.NewGuid(),
                Year = 2026,
                Quarter = EvaluationQuarter.Quy3,
                Name = "Đánh giá, xếp loại cán bộ Quý III/2026",
                StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
                Status = PeriodStatus.Appraisal,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.EvaluationPeriods.AddAsync(periodQ3);
            await context.SaveChangesAsync();

            // 9. Seed Bảng đánh giá mẫu cho các cán bộ trong quý III/2026 (Loại trừ tài khoản admin kỹ thuật)
            var members = await context.PartyMemberProfiles
                .Where(m => m.Username != "admin")
                .ToListAsync();

            var records = new List<EvaluationRecord>();
            int memberIndex = 0;
            foreach (var member in members)
            {
                memberIndex++;
                bool isKeyLeader = memberIndex <= 3; // Lãnh đạo chủ chốt
                var record = new EvaluationRecord
                {
                    Id = Guid.NewGuid(),
                    PeriodId = periodQ3.Id,
                    MemberId = member.Id,
                    PartyCellId = member.PartyCellId,
                    DepartmentId = member.DepartmentId,
                    JobGroup = member.JobGroup,
                    ApprovalAuthority = member.ApprovalAuthority,
                    GeneralScoreT1 = 4.8,
                    GeneralScoreT2 = 4.9,
                    GeneralScoreT3 = 4.8,
                    GeneralScoreT4 = 4.8,
                    GeneralScoreT5 = 4.7,
                    GeneralScoreT6 = 4.9,
                    GeneralCriteriaScore = 28.9,
                    TasksScore = isKeyLeader ? 68.5 : 64.0,
                    TotalSelfScore = isKeyLeader ? 97.4 : 92.9,
                    SelfProposedGrade = EvaluationGrade.HoanThanhXuatSac,
                    PartyCellComment = "Đồng chí luôn gương mẫu trong công tác lãnh đạo, hoàn thành tốt nhiệm vụ chính trị và chuyên môn được giao.",
                    PartyCellProposedGrade = isKeyLeader ? EvaluationGrade.HoanThanhXuatSac : EvaluationGrade.HoanThanhTot,
                    VotesExcellent = isKeyLeader ? 9 : 3,
                    VotesGood = isKeyLeader ? 1 : 7,
                    VotesSatisfactory = 0,
                    VotesUnsatisfactory = 0,
                    TotalVoters = 10,
                    AppraisalScore = isKeyLeader ? 97.0 : 92.5,
                    AppraisalComment = "Hồ sơ đầy đủ minh chứng hợp lệ theo Hướng dẫn 03-HD/TVĐU.",
                    AppraisalProposedGrade = isKeyLeader ? EvaluationGrade.HoanThanhXuatSac : EvaluationGrade.HoanThanhTot,
                    FinalScore = isKeyLeader ? 97.0 : 92.5,
                    FinalGrade = isKeyLeader ? EvaluationGrade.HoanThanhXuatSac : EvaluationGrade.HoanThanhTot,
                    Status = RecordStatus.Reviewed,
                    UpdatedAt = DateTime.UtcNow
                };

                // 4 nhiệm vụ với tổng trọng số đúng bằng 70.0
                record.Tasks = new List<EvaluationTask>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        RecordId = record.Id,
                        TaskOrder = 1,
                        TaskName = "Chỉ đạo, điều hành thực hiện nhiệm vụ chuyên môn và kế hoạch sản xuất kinh doanh quý III/2026",
                        TargetOutput = "Hoàn thành 100% chỉ tiêu kế hoạch quý, không để xảy ra sai sót kỹ thuật",
                        Weight = 20.0,
                        Deadline = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                        CriteriaA_Ratio = 1.0,
                        CriteriaB_Ratio = 1.0,
                        CriteriaC_Ratio = 1.0,
                        CriteriaD_Ratio = 1.0,
                        SelfScore = 20.0,
                        SupervisorScore = 20.0,
                        IsExceedStandard = isKeyLeader
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        RecordId = record.Id,
                        TaskOrder = 2,
                        TaskName = "Tổ chức kiểm tra, giám sát chất lượng và an toàn kỹ thuật CNS/ATM",
                        TargetOutput = "100% trang thiết bị hoạt động ổn định, đạt tiêu chuẩn ICAO và VATM",
                        Weight = 20.0,
                        Deadline = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
                        CriteriaA_Ratio = 1.0,
                        CriteriaB_Ratio = 1.0,
                        CriteriaC_Ratio = 1.0,
                        CriteriaD_Ratio = 0.95,
                        SelfScore = 19.8,
                        SupervisorScore = 19.8,
                        IsExceedStandard = false
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        RecordId = record.Id,
                        TaskOrder = 3,
                        TaskName = "Đẩy mạnh ứng dụng chuyển đổi số và chuẩn hóa quy trình công tác Đảng",
                        TargetOutput = "Vận hành hệ thống số hóa đánh giá cán bộ theo Hướng dẫn 03-HD/TVĐU",
                        Weight = 15.0,
                        Deadline = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
                        CriteriaA_Ratio = 1.0,
                        CriteriaB_Ratio = 1.0,
                        CriteriaC_Ratio = 1.0,
                        CriteriaD_Ratio = 1.0,
                        SelfScore = 15.0,
                        SupervisorScore = 15.0,
                        IsExceedStandard = isKeyLeader
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        RecordId = record.Id,
                        TaskOrder = 4,
                        TaskName = "Công tác xây dựng Đảng, duy trì nền nếp sinh hoạt Chi bộ và nêu gương cán bộ",
                        TargetOutput = "Sinh hoạt Chi bộ định kỳ đầy đủ, 100% đảng viên hoàn thành chức trách",
                        Weight = 15.0,
                        Deadline = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                        CriteriaA_Ratio = 1.0,
                        CriteriaB_Ratio = 0.95,
                        CriteriaC_Ratio = 1.0,
                        CriteriaD_Ratio = 0.95,
                        SelfScore = 14.5,
                        SupervisorScore = 14.5,
                        IsExceedStandard = false
                    }
                };

                records.Add(record);
            }

            await context.EvaluationRecords.AddRangeAsync(records);
            await context.SaveChangesAsync();
        }

        // 10. Đảm bảo dọn sạch hồ sơ đánh giá của admin nếu đã từng tồn tại trong CSDL trước đây
        var adminProfile = await context.PartyMemberProfiles.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminProfile != null)
        {
            var adminEvaluationRecords = await context.EvaluationRecords
                .Where(r => r.MemberId == adminProfile.Id)
                .ToListAsync();
            if (adminEvaluationRecords.Any())
            {
                context.EvaluationRecords.RemoveRange(adminEvaluationRecords);
                await context.SaveChangesAsync();
            }
        }
    }

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
            var action = definition.Code.StartsWith(definition.Module + ".", StringComparison.Ordinal)
                ? definition.Code[(definition.Module.Length + 1)..]
                : definition.Code;
            var permission = existing.FirstOrDefault(p => p.Code == definition.Code);
            if (permission == null)
            {
                context.Permissions.Add(new Permission
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Resource = definition.Module,
                    Action = action,
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
            if (permission.Resource != definition.Module) permission.Resource = definition.Module;
            if (permission.Action != action) permission.Action = action;
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
    /// Tạo vai trò mặc định (mục 6 thiết kế) chỉ khi CSDL chưa có vai trò nào ngoài các vai trò cũ (trước task 09).
    /// Vai trò quản trị (<c>QUAN_TRI_HE_THONG</c>) luôn được đánh dấu bảo vệ; lần đầu đánh dấu thì bổ sung 2 quyền quản trị bắt buộc.
    /// Không bao giờ ghi đè quyền của vai trò đã tồn tại.
    /// </summary>
    private static async Task SeedDefaultRolesAsync(CongTacDangDbContext context, ILogger? logger)
    {
        var roles = await context.Roles.IgnoreQueryFilters().Include(r => r.Permissions).ToListAsync();
        var permissions = await context.Permissions.ToDictionaryAsync(p => p.Code);

        var hasConfiguredRoles = roles.Any(r => !RoleCodes.Legacy.Contains(r.Code));
        if (!hasConfiguredRoles)
        {
            var existingCodes = roles.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
            var createdNames = new List<string>();
            foreach (var definition in DefaultRoles.Where(d => !existingCodes.Contains(d.Code)))
            {
                var role = new AppRole
                {
                    Code = definition.Code,
                    Name = UniqueName(definition.Name, roles),
                    Description = definition.Description,
                    IsSystem = true,
                    IsProtected = definition.IsProtected,
                    Permissions = definition.Permissions.Where(permissions.ContainsKey).Select(code => permissions[code]).ToList()
                };
                context.Roles.Add(role);
                roles.Add(role);
                createdNames.Add(role.Name);
            }

            if (createdNames.Count > 0)
            {
                await context.SaveChangesAsync();
                logger?.LogInformation("Đã tạo vai trò mặc định: {Roles}", string.Join(", ", createdNames));
            }
        }

        // Vai trò quản trị luôn được bảo vệ (chốt "vai trò bảo vệ").
        var administrator = roles.FirstOrDefault(r => r.Code == RoleCodes.Administrator && !r.IsDeleted);
        if (administrator != null && !administrator.IsProtected)
        {
            administrator.IsProtected = true;
            foreach (var code in AdministratorInvariant.Codes.Concat(AdministratorPermissionCodes).Distinct())
            {
                if (permissions.TryGetValue(code, out var permission) && administrator.Permissions.All(p => p.Code != code))
                    administrator.Permissions.Add(permission);
            }

            await context.SaveChangesAsync();
            logger?.LogInformation("Đã đánh dấu vai trò {Role} là vai trò được bảo vệ.", administrator.Name);
        }
    }

    /// <summary>Tên vai trò chưa trùng với vai trò chưa xóa (tên phải duy nhất).</summary>
    private static string UniqueName(string name, IEnumerable<AppRole> roles)
    {
        var used = roles.Where(r => !r.IsDeleted).Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name))
            return name;
        for (var i = 2; ; i++)
        {
            var candidate = $"{name} ({i})";
            if (!used.Contains(candidate))
                return candidate;
        }
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

    #region Bản gán vai trò

    /// <summary>Tạo tài khoản mẫu (không gán vai trò — xem <see cref="SeedSampleAssignmentsAsync"/>).</summary>
    private static async Task SeedSampleUsersAsync(CongTacDangDbContext context)
    {
        var cellKt = await context.PartyCells.FirstAsync(x => x.Code == "CB-KT");
        var cellVp = await context.PartyCells.FirstAsync(x => x.Code == "CB-VP");
        var depKh = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-KH");
        var depTccb = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-TCCB");
        var existing = await context.PartyMemberProfiles.IgnoreQueryFilters().Select(u => u.Username).ToListAsync();
        var isEmpty = existing.Count == 0;
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("123456");

        var samples = new List<PartyMemberProfile>
        {
            new()
            {
                Username = "admin", PasswordHash = passwordHash, MustChangePassword = true,
                FullName = "Quản trị viên Hệ thống", Email = "admin@attech.com.vn", PhoneNumber = "0900000000",
                IsPartyMember = true, PartyCardNumber = "ADMIN-001", PartyCellId = cellVp.Id, PartyRole = PartyRole.DangVien,
                DepartmentId = depKh.Id, AdminPosition = AdministrativePosition.ChuyenVien, PositionTitle = "Quản trị viên Hệ thống CNTT",
                JobGroup = JobGroup.Khung4_KhcnChuyenDoiSo, ApprovalAuthority = ApprovalAuthority.CoSo
            },
            new()
            {
                Username = "bithu_attech", PasswordHash = passwordHash, MustChangePassword = true,
                FullName = "Lê Tiến Thịnh", Email = "thinhlt@attech.com.vn", PhoneNumber = "0912345678",
                IsPartyMember = true, PartyCardNumber = "ATTECH-001", PartyCellId = cellVp.Id, PartyRole = PartyRole.BiThuDangUy,
                DepartmentId = depKh.Id, AdminPosition = AdministrativePosition.GiamDoc, PositionTitle = "Bí thư Đảng ủy, Giám đốc Công ty",
                JobGroup = JobGroup.Khung1_QuanLyDangDoanThe, ApprovalAuthority = ApprovalAuthority.CapTren
            },
            new()
            {
                Username = "bithu_cbkt", PasswordHash = passwordHash, MustChangePassword = true,
                FullName = "Nguyễn Văn Hùng", Email = "hungnv@attech.com.vn", PhoneNumber = "0987654321",
                IsPartyMember = true, PartyCardNumber = "ATTECH-002", PartyCellId = cellKt.Id, PartyRole = PartyRole.BiThuChiBo,
                DepartmentId = depKh.Id, AdminPosition = AdministrativePosition.TruongPhong, PositionTitle = "Bí thư Chi bộ, Trưởng phòng Kỹ thuật",
                JobGroup = JobGroup.Khung2_AnToanKyThuat, ApprovalAuthority = ApprovalAuthority.CoSo
            },
            new()
            {
                Username = "canbo_kt", PasswordHash = passwordHash, MustChangePassword = true,
                FullName = "Trần Quốc Tuấn", Email = "tuantq@attech.com.vn", PhoneNumber = "0901234567",
                IsPartyMember = true, PartyCardNumber = "ATTECH-003", PartyCellId = cellKt.Id, PartyRole = PartyRole.DangVien,
                DepartmentId = depKh.Id, AdminPosition = AdministrativePosition.PhoTruongPhong, PositionTitle = "Phó Trưởng phòng Kỹ thuật",
                JobGroup = JobGroup.Khung2_AnToanKyThuat, ApprovalAuthority = ApprovalAuthority.CoSo
            },
            new()
            {
                Username = "thamdinh_du", PasswordHash = passwordHash, MustChangePassword = true,
                FullName = "Vũ Đình Hùng", Email = "hungvd@attech.com.vn", PhoneNumber = "0934567890",
                IsPartyMember = true, PartyCardNumber = "ATTECH-004", PartyCellId = cellVp.Id, PartyRole = PartyRole.DangUyVien,
                DepartmentId = depTccb.Id, AdminPosition = AdministrativePosition.TruongPhong, PositionTitle = "Trưởng Ban TCCB, Tổ trưởng Tổ Thẩm định",
                JobGroup = JobGroup.Khung1_QuanLyDangDoanThe, ApprovalAuthority = ApprovalAuthority.CoSo
            }
        };

        // CSDL trống: tạo đủ tài khoản mẫu. CSDL đã có dữ liệu: chỉ bảo đảm tài khoản quản trị và thẩm định mẫu tồn tại
        // (kể cả đã xóa mềm thì không tạo lại — tên đăng nhập không tái sử dụng).
        var toCreate = isEmpty
            ? samples
            : samples.Where(u => (u.Username == "admin" || u.Username == "thamdinh_du") && !existing.Contains(u.Username)).ToList();
        if (toCreate.Count == 0)
            return;

        await context.PartyMemberProfiles.AddRangeAsync(toCreate);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Gán vai trò mẫu kèm phạm vi hợp lý cho tài khoản mẫu — <b>chỉ</b> tài khoản chưa từng có bản gán nào
    /// (kể cả đã xóa/hết hạn), để không gán lại vai trò quản trị đã thu hồi (T-46).
    /// </summary>
    private static async Task SeedSampleAssignmentsAsync(CongTacDangDbContext context, ILogger? logger)
    {
        var cellKt = await context.PartyCells.FirstOrDefaultAsync(x => x.Code == "CB-KT");
        var depKh = await context.AdministrativeDepartments.FirstOrDefaultAsync(x => x.Code == "PH-KH");
        var roles = await context.Roles.ToDictionaryAsync(r => r.Code, r => r.Id);

        var plan = new Dictionary<string, (string Role, RoleScopeType Scope, Guid? ScopeId)[]>(StringComparer.Ordinal)
        {
            ["admin"] = new[] { (RoleCodes.Administrator, RoleScopeType.Global, (Guid?)null) },
            ["bithu_attech"] = new[]
            {
                (RoleCodes.Evaluatee, RoleScopeType.Global, (Guid?)null),
                (RoleCodes.DirectSupervisor, RoleScopeType.Global, null),
                (RoleCodes.PartyCommitteeMember, RoleScopeType.Global, null),
                (RoleCodes.PartyOffice, RoleScopeType.Global, null)
            },
            ["bithu_cbkt"] = new[]
            {
                (RoleCodes.Evaluatee, RoleScopeType.Global, (Guid?)null),
                (RoleCodes.CellCommittee, RoleScopeType.PartyCell, cellKt?.Id),
                (RoleCodes.DepartmentLeader, RoleScopeType.Department, depKh?.Id)
            },
            ["canbo_kt"] = new[] { (RoleCodes.Evaluatee, RoleScopeType.Global, (Guid?)null) },
            ["thamdinh_du"] = new[]
            {
                (RoleCodes.Evaluatee, RoleScopeType.Global, (Guid?)null),
                (RoleCodes.Appraisal, RoleScopeType.Global, null)
            }
        };

        var usernames = plan.Keys.ToList();
        var users = await context.PartyMemberProfiles
            .Where(u => usernames.Contains(u.Username))
            .Select(u => new { u.Id, u.Username })
            .ToListAsync();
        var userIds = users.Select(u => u.Id).ToList();
        var alreadyAssigned = await context.Set<UserRoleAssignment>()
            .IgnoreQueryFilters()
            .Where(a => userIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync();

        var count = 0;
        foreach (var user in users.Where(u => !alreadyAssigned.Contains(u.Id)))
        {
            foreach (var (roleCode, scope, scopeId) in plan[user.Username])
            {
                if (!roles.TryGetValue(roleCode, out var roleId))
                    continue;
                if (scope != RoleScopeType.Global && scopeId == null)
                    continue;

                context.Set<UserRoleAssignment>().Add(new UserRoleAssignment
                {
                    UserId = user.Id,
                    RoleId = roleId,
                    ScopeType = scope,
                    ScopeId = scope == RoleScopeType.Global ? null : scopeId,
                    ValidFrom = DateTime.UtcNow,
                    Note = "Gán mẫu (Database:SeedSampleData)."
                });
                count++;
            }
        }

        if (count == 0)
            return;

        await context.SaveChangesAsync();
        logger?.LogInformation("Đã gán {Count} vai trò mẫu cho tài khoản mẫu chưa có bản gán.", count);
    }

    #endregion
}
