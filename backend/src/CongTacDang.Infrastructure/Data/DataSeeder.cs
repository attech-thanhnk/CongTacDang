using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;

namespace CongTacDang.Infrastructure.Data;

public static class DataSeeder
{
    /// <summary>Định nghĩa một vai trò hệ thống và bộ quyền mặc định.</summary>
    private sealed record RoleDefinition(string Code, string Name, string Description, string[] Permissions);

    /// <summary>
    /// Mã quyền mới (<see cref="PermissionCodes"/>) cấp cho vai trò quản trị để endpoint dùng mã mới chạy được ngay:
    /// <c>system.*</c>, <c>catalog.manage</c>, <c>attachment.general.manage</c>.
    /// Khai báo trước <see cref="DefaultRoles"/> vì trường tĩnh được khởi tạo theo thứ tự khai báo.
    /// </summary>
    private static readonly string[] AdministratorNewPermissionCodes = PermissionCodes.All
        .Where(code => code.StartsWith("system.", StringComparison.Ordinal)
            || code == PermissionCodes.CatalogManage
            || code == PermissionCodes.AttachmentGeneralManage)
        .ToArray();

    private static readonly string[] CanBoPermissions =
    {
        AppPermissions.UsersRead,
        AppPermissions.BranchesRead,
        AppPermissions.AttachmentsRead,
        AppPermissions.AttachmentsUpload,
        AppPermissions.ReportsExport,
        AppPermissions.EvaluationsRead,
        AppPermissions.EvaluationsRegister,
        AppPermissions.EvaluationsSelfScore
    };

    /// <summary>Ma trận vai trò - quyền mặc định, dùng khi tạo role lần đầu và khi đặt lại có chủ đích.</summary>
    private static readonly RoleDefinition[] DefaultRoles =
    {
        new(AppRoles.CAN_BO, "Cán bộ, Đảng viên", "Quyền cơ bản của mọi Đảng viên, cán bộ trong hệ thống", CanBoPermissions),
        new(AppRoles.BI_THU_CHI_BO, "Bí thư Chi bộ", "Bí thư / Phó Bí thư Chi bộ cơ sở trực thuộc Đảng bộ",
            CanBoPermissions.Concat(new[] { AppPermissions.BranchesUpdate, AppPermissions.EvaluationsBranchVote }).ToArray()),
        new(AppRoles.TO_THAM_DINH, "Tổ Thẩm định", "Tổ thẩm định đánh giá cán bộ và rà soát minh chứng",
            CanBoPermissions.Concat(new[] { AppPermissions.AttachmentsDelete, AppPermissions.EvaluationsAppraise }).ToArray()),
        new(AppRoles.BAN_THUONG_VU, "Ban Thường vụ Đảng ủy Tổng công ty", "Cấp có thẩm quyền phê duyệt các hồ sơ thuộc diện Ban Thường vụ Đảng ủy Tổng công ty",
            CanBoPermissions.Concat(new[]
            {
                AppPermissions.BranchesCreate,
                AppPermissions.BranchesUpdate,
                AppPermissions.BranchesDelete,
                AppPermissions.AttachmentsDelete,
                AppPermissions.EvaluationsBranchVote,
                AppPermissions.EvaluationsAppraise,
                AppPermissions.EvaluationsApprove
            }).ToArray()),
        new(AppRoles.DANG_UY_CO_SO, "Đảng ủy cơ sở", "Quyết định, phê duyệt hồ sơ thuộc thẩm quyền Đảng ủy cơ sở",
            new[] { AppPermissions.EvaluationsRead, AppPermissions.EvaluationsApprove, AppPermissions.ReportsExport }),
        // Quản trị viên kỹ thuật & phân quyền - Không tham gia đánh giá cá nhân
        new(AppRoles.QUAN_TRI_HE_THONG, "Quản trị hệ thống", "Toàn quyền quản trị kỹ thuật hệ thống, danh mục Chi bộ/Phòng ban và phân quyền",
            new[]
            {
                AppPermissions.UsersRead,
                AppPermissions.UsersCreate,
                AppPermissions.UsersUpdate,
                AppPermissions.UsersDelete,
                AppPermissions.BranchesRead,
                AppPermissions.BranchesCreate,
                AppPermissions.BranchesUpdate,
                AppPermissions.BranchesDelete,
                AppPermissions.AttachmentsRead,
                AppPermissions.AttachmentsUpload,
                AppPermissions.AttachmentsDelete,
                AppPermissions.ReportsExport,
                AppPermissions.RolesManage,
                AppPermissions.EvaluationsRead
            }.Concat(AdministratorNewPermissionCodes).ToArray())
    };

    /// <summary>
    /// Khởi tạo dữ liệu nền. Role/permission chỉ được tạo khi chưa có; quyền của role đã tồn tại
    /// không bị ghi đè trừ khi <paramref name="resetRolePermissions"/> = true (Database:ResetRolePermissions).
    /// </summary>
    public static async Task SeedAsync(
        CongTacDangDbContext context,
        bool seedSampleData = true,
        bool resetRolePermissions = false,
        ILogger? logger = null)
    {
        // 1. Seed Permissions (Danh mục quyền hạn chuẩn hóa toàn hệ thống theo 03-HD/TVĐU)
        var definedPermissions = new List<Permission>
        {
            // Users
            new() { Code = AppPermissions.UsersRead, Name = "Xem hồ sơ cán bộ", Resource = "users", Action = "read", Description = "Xem danh sách và chi tiết hồ sơ cán bộ, đảng viên" },
            new() { Code = AppPermissions.UsersCreate, Name = "Tạo mới cán bộ", Resource = "users", Action = "create", Description = "Thêm mới hồ sơ cán bộ, đảng viên vào hệ thống" },
            new() { Code = AppPermissions.UsersUpdate, Name = "Cập nhật cán bộ", Resource = "users", Action = "update", Description = "Chỉnh sửa thông tin hồ sơ cán bộ, đảng viên" },
            new() { Code = AppPermissions.UsersDelete, Name = "Xóa cán bộ", Resource = "users", Action = "delete", Description = "Xóa hồ sơ cán bộ, đảng viên" },

            // Branches
            new() { Code = AppPermissions.BranchesRead, Name = "Xem tổ chức Chi bộ", Resource = "branches", Action = "read", Description = "Xem danh sách Chi bộ và cơ cấu tổ chức" },
            new() { Code = AppPermissions.BranchesCreate, Name = "Tạo mới Chi bộ", Resource = "branches", Action = "create", Description = "Thành lập Chi bộ mới" },
            new() { Code = AppPermissions.BranchesUpdate, Name = "Cập nhật Chi bộ", Resource = "branches", Action = "update", Description = "Chỉnh sửa thông tin Chi bộ" },
            new() { Code = AppPermissions.BranchesDelete, Name = "Xóa Chi bộ", Resource = "branches", Action = "delete", Description = "Xóa Chi bộ" },

            // Attachments
            new() { Code = AppPermissions.AttachmentsRead, Name = "Xem & tải tài liệu", Resource = "attachments", Action = "read", Description = "Xem danh sách và tải tệp minh chứng, tài liệu" },
            new() { Code = AppPermissions.AttachmentsUpload, Name = "Tải lên tài liệu", Resource = "attachments", Action = "upload", Description = "Tải lên văn bản, minh chứng đánh giá" },
            new() { Code = AppPermissions.AttachmentsDelete, Name = "Xóa tài liệu", Resource = "attachments", Action = "delete", Description = "Xóa tệp minh chứng khỏi hệ thống" },

            // Evaluations (03-HD/TVĐU Quy trình 5 bước)
            new() { Code = AppPermissions.EvaluationsRead, Name = "Xem hồ sơ đánh giá", Resource = "evaluations", Action = "read", Description = "Xem hồ sơ đánh giá và tiến trình 5 bước" },
            new() { Code = AppPermissions.EvaluationsRegister, Name = "Đăng ký nhiệm vụ", Resource = "evaluations", Action = "register", Description = "Đăng ký 3-7 nhiệm vụ trọng tâm đầu quý (Mẫu 01 - 70 điểm)" },
            new() { Code = AppPermissions.EvaluationsSelfScore, Name = "Tự chấm điểm", Resource = "evaluations", Action = "self_score", Description = "Tự chấm 30đ tiêu chí chung và 70đ chuyên môn (Mẫu 02 & 09)" },
            new() { Code = AppPermissions.EvaluationsBranchVote, Name = "Chi bộ đánh giá & bỏ phiếu", Resource = "evaluations", Action = "branch_vote", Description = "Chi bộ nhận xét và bỏ phiếu kín (Mẫu 10 & 13)" },
            new() { Code = AppPermissions.EvaluationsAppraise, Name = "Thẩm định hồ sơ", Resource = "evaluations", Action = "appraise", Description = "Tổ Thẩm định đối soát điểm và kiểm tra trần 20% (Mẫu 03 & 15)" },
            new() { Code = AppPermissions.EvaluationsApprove, Name = "Chuẩn y xếp loại", Resource = "evaluations", Action = "approve", Description = "Ban Thường vụ chuẩn y mức xếp loại chính thức (Mẫu 14 & 16)" },

            // Reports
            new() { Code = AppPermissions.ReportsExport, Name = "Xuất báo cáo", Resource = "reports", Action = "export", Description = "Xuất báo cáo tổng hợp đánh giá theo chuẩn 03-HD/TVĐU" },

            // Roles Management
            new() { Code = AppPermissions.RolesManage, Name = "Quản trị vai trò & quyền", Resource = "roles", Action = "manage", Description = "Quản trị động vai trò, gán quyền và gán vai trò người dùng" },
        };

        // Mã quyền chuẩn mới (PermissionCodes) — cùng tồn tại với mã cũ trong giai đoạn chuyển tiếp.
        definedPermissions.AddRange(PermissionCodes.Definitions.Select(d => new Permission
        {
            Code = d.Code,
            Name = d.Name,
            Resource = d.Module,
            Action = d.Code.StartsWith(d.Module + ".", StringComparison.Ordinal) ? d.Code[(d.Module.Length + 1)..] : d.Code,
            Description = d.Description
        }));

        // Chỉ tạo bản ghi permission còn thiếu (kể cả bản ghi đã xóa mềm được coi là đã có);
        // không tự gán permission mới vào role đã tồn tại, trừ mã mới của vai trò quản trị (xem bên dưới).
        var existingPermCodes = await context.Permissions.IgnoreQueryFilters().Select(p => p.Code).ToListAsync();
        var missingPerms = definedPermissions.Where(p => !existingPermCodes.Contains(p.Code)).ToList();
        var rolesExisted = await context.Roles.AnyAsync();
        if (missingPerms.Any())
        {
            await context.Permissions.AddRangeAsync(missingPerms);
            await context.SaveChangesAsync();

            var unassigned = missingPerms.Select(p => p.Code).Except(AdministratorNewPermissionCodes).ToList();
            if (rolesExisted && !resetRolePermissions && unassigned.Count > 0)
            {
                logger?.LogWarning(
                    "Đã tạo quyền mới {Permissions} nhưng không tự gán vào vai trò nào. Hãy gán qua màn hình phân quyền hoặc bật Database:ResetRolePermissions để đặt lại về mặc định.",
                    string.Join(", ", unassigned));
            }
        }

        // 2. Seed Roles & Role-Permission Mapping: chỉ tạo role còn thiếu kèm quyền mặc định.
        await SeedMissingRolesAsync(context, logger);

        // Mã quyền mới của vai trò quản trị: chỉ gán khi bản ghi permission vừa được tạo ở lần chạy này,
        // nên cấu hình quản trị đã chỉnh tay (gỡ quyền) không bị ghi đè ở lần khởi động sau (tinh thần T-33).
        await GrantNewAdministratorPermissionsAsync(
            context,
            missingPerms.Select(p => p.Code).Intersect(AdministratorNewPermissionCodes).ToList(),
            logger);

        // Đặt lại quyền của các role hệ thống về mặc định chỉ khi được yêu cầu tường minh.
        if (resetRolePermissions)
            await ResetRolePermissionsAsync(context, logger);

        if (!seedSampleData)
            return;

        // 3. Seed Chi bộ Đảng tại ATTECH
        if (!await context.PartyCells.AnyAsync())
        {
            var cellKt = new PartyCell { Code = "CB-KT", Name = "Chi bộ Khối Kỹ thuật", Description = "Chi bộ phụ trách an toàn, điều hành kỹ thuật CNS, ATM" };
            var cellSx = new PartyCell { Code = "CB-SX", Name = "Chi bộ Sản xuất công nghiệp", Description = "Chi bộ phụ trách Xưởng sản xuất thiết bị hàng không" };
            var cellDv = new PartyCell { Code = "CB-DV", Name = "Chi bộ Dịch vụ kỹ thuật", Description = "Chi bộ phụ trách dịch vụ lắp đặt, bảo dưỡng" };
            var cellVp = new PartyCell { Code = "CB-VP", Name = "Chi bộ Khối Văn phòng", Description = "Chi bộ phụ trách Kế hoạch, Tài chính, TCCB-LĐ" };

            await context.PartyCells.AddRangeAsync(cellKt, cellSx, cellDv, cellVp);
            await context.SaveChangesAsync();
        }

        // 4. Seed Phòng ban Chính quyền tại ATTECH
        if (!await context.AdministrativeDepartments.AnyAsync())
        {
            var depKh = new AdministrativeDepartment { Code = "PH-KH", Name = "Phòng Kế hoạch", Description = "Phòng Kế hoạch đầu tư, dự án" };
            var depTc = new AdministrativeDepartment { Code = "PH-TC", Name = "Phòng Tài chính - Kế toán", Description = "Phòng Tài chính kế toán công ty" };
            var depTccb = new AdministrativeDepartment { Code = "PH-TCCB", Name = "Phòng Tổ chức cán bộ - Lao động", Description = "Phòng tham mưu tổ chức nhân sự, lao động tiền lương" };
            var depXs = new AdministrativeDepartment { Code = "XUONG-SX", Name = "Xưởng Sản xuất thiết bị", Description = "Xưởng sản xuất công nghiệp cơ khí, điện tử" };

            await context.AdministrativeDepartments.AddRangeAsync(depKh, depTc, depTccb, depXs);
            await context.SaveChangesAsync();
        }

        // 5. Seed Cán bộ / Đảng viên mẫu kèm gán vai trò ban đầu
        if (!await context.PartyMemberProfiles.AnyAsync())
        {
            var cellKt = await context.PartyCells.FirstAsync(x => x.Code == "CB-KT");
            var cellVp = await context.PartyCells.FirstAsync(x => x.Code == "CB-VP");
            var depKh = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-KH");

            var roleCanBo = await context.Roles.FirstAsync(r => r.Code == AppRoles.CAN_BO);
            var roleBiThuCb = await context.Roles.FirstAsync(r => r.Code == AppRoles.BI_THU_CHI_BO);
            var roleBanThuongVu = await context.Roles.FirstAsync(r => r.Code == AppRoles.BAN_THUONG_VU);
            var roleDangUyCoSo = await context.Roles.FirstAsync(r => r.Code == AppRoles.DANG_UY_CO_SO);
            var roleAdmin = await context.Roles.FirstAsync(r => r.Code == AppRoles.QUAN_TRI_HE_THONG);
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            var adminUser = new PartyMemberProfile
            {
                Username = "admin",
                PasswordHash = defaultPasswordHash,
                MustChangePassword = true,
                FullName = "Quản trị viên Hệ thống",
                Email = "admin@attech.com.vn",
                PhoneNumber = "0900000000",
                IsPartyMember = true,
                PartyCardNumber = "ADMIN-001",
                PartyCellId = cellVp.Id,
                PartyRole = PartyRole.DangVien,
                DepartmentId = depKh.Id,
                AdminPosition = AdministrativePosition.ChuyenVien,
                PositionTitle = "Quản trị viên Hệ thống CNTT",
                JobGroup = JobGroup.Khung4_KhcnChuyenDoiSo,
                ApprovalAuthority = ApprovalAuthority.CoSo,
                Roles = new List<AppRole> { roleAdmin }
            };

            var biThuAttech = new PartyMemberProfile
            {
                Username = "bithu_attech",
                PasswordHash = defaultPasswordHash,
                MustChangePassword = true,
                FullName = "Lê Tiến Thịnh",
                Email = "thinhlt@attech.com.vn",
                PhoneNumber = "0912345678",
                IsPartyMember = true,
                PartyCardNumber = "ATTECH-001",
                PartyCellId = cellVp.Id,
                PartyRole = PartyRole.BiThuDangUy,
                DepartmentId = depKh.Id,
                AdminPosition = AdministrativePosition.GiamDoc,
                PositionTitle = "Bí thư Đảng ủy, Giám đốc Công ty",
                JobGroup = JobGroup.Khung1_QuanLyDangDoanThe,
                ApprovalAuthority = ApprovalAuthority.CapTren,
                Roles = new List<AppRole> { roleBanThuongVu, roleDangUyCoSo, roleCanBo }
            };

            var biThuCbkt = new PartyMemberProfile
            {
                Username = "bithu_cbkt",
                PasswordHash = defaultPasswordHash,
                MustChangePassword = true,
                FullName = "Nguyễn Văn Hùng",
                Email = "hungnv@attech.com.vn",
                PhoneNumber = "0987654321",
                IsPartyMember = true,
                PartyCardNumber = "ATTECH-002",
                PartyCellId = cellKt.Id,
                PartyRole = PartyRole.BiThuChiBo,
                DepartmentId = depKh.Id,
                AdminPosition = AdministrativePosition.TruongPhong,
                PositionTitle = "Bí thư Chi bộ, Trưởng phòng Kỹ thuật",
                JobGroup = JobGroup.Khung2_AnToanKyThuat,
                ApprovalAuthority = ApprovalAuthority.CoSo,
                Roles = new List<AppRole> { roleBiThuCb, roleCanBo }
            };

            var roleToThamDinh = await context.Roles.FirstAsync(r => r.Code == AppRoles.TO_THAM_DINH);
            var depTccb = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-TCCB");

            var canBoKt = new PartyMemberProfile
            {
                Username = "canbo_kt",
                PasswordHash = defaultPasswordHash,
                MustChangePassword = true,
                FullName = "Trần Quốc Tuấn",
                Email = "tuantq@attech.com.vn",
                PhoneNumber = "0901234567",
                IsPartyMember = true,
                PartyCardNumber = "ATTECH-003",
                PartyCellId = cellKt.Id,
                PartyRole = PartyRole.DangVien,
                DepartmentId = depKh.Id,
                AdminPosition = AdministrativePosition.PhoTruongPhong,
                PositionTitle = "Phó Trưởng phòng Kỹ thuật",
                JobGroup = JobGroup.Khung2_AnToanKyThuat,
                ApprovalAuthority = ApprovalAuthority.CoSo,
                Roles = new List<AppRole> { roleCanBo }
            };

            var thamDinhDu = new PartyMemberProfile
            {
                Username = "thamdinh_du",
                PasswordHash = defaultPasswordHash,
                MustChangePassword = true,
                FullName = "Vũ Đình Hùng",
                Email = "hungvd@attech.com.vn",
                PhoneNumber = "0934567890",
                IsPartyMember = true,
                PartyCardNumber = "ATTECH-004",
                PartyCellId = cellVp.Id,
                PartyRole = PartyRole.DangUyVien,
                DepartmentId = depTccb.Id,
                AdminPosition = AdministrativePosition.TruongPhong,
                PositionTitle = "Trưởng Ban TCCB, Tổ trưởng Tổ Thẩm định",
                JobGroup = JobGroup.Khung1_QuanLyDangDoanThe,
                ApprovalAuthority = ApprovalAuthority.CoSo,
                Roles = new List<AppRole> { roleToThamDinh, roleCanBo }
            };

            await context.PartyMemberProfiles.AddRangeAsync(adminUser, biThuAttech, biThuCbkt, canBoKt, thamDinhDu);
            await context.SaveChangesAsync();
        }
        else
        {
            var cellVp = await context.PartyCells.FirstAsync(x => x.Code == "CB-VP");
            var depKh = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-KH");
            var depTccb = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-TCCB");
            var roleAdmin = await context.Roles.FirstOrDefaultAsync(r => r.Code == AppRoles.QUAN_TRI_HE_THONG);
            var roleToThamDinh = await context.Roles.FirstOrDefaultAsync(r => r.Code == AppRoles.TO_THAM_DINH);
            var roleCanBo = await context.Roles.FirstOrDefaultAsync(r => r.Code == AppRoles.CAN_BO);
            var roleDangUyCoSo = await context.Roles.FirstOrDefaultAsync(r => r.Code == AppRoles.DANG_UY_CO_SO);

            if (roleDangUyCoSo == null)
            {
                roleDangUyCoSo = new AppRole
                {
                    Code = AppRoles.DANG_UY_CO_SO,
                    Name = "Đảng ủy cơ sở",
                    Description = "Quyết định, phê duyệt hồ sơ thuộc thẩm quyền Đảng ủy cơ sở",
                    IsSystem = true,
                    Permissions = new List<Permission>()
                };
                var approvalPermission = await context.Permissions.FirstOrDefaultAsync(p => p.Code == AppPermissions.EvaluationsApprove);
                var readPermission = await context.Permissions.FirstOrDefaultAsync(p => p.Code == AppPermissions.EvaluationsRead);
                var exportPermission = await context.Permissions.FirstOrDefaultAsync(p => p.Code == AppPermissions.ReportsExport);
                if (approvalPermission != null) roleDangUyCoSo.Permissions.Add(approvalPermission);
                if (readPermission != null) roleDangUyCoSo.Permissions.Add(readPermission);
                if (exportPermission != null) roleDangUyCoSo.Permissions.Add(exportPermission);
                await context.Roles.AddAsync(roleDangUyCoSo);
                await context.SaveChangesAsync();
            }

            // Đảm bảo tài khoản Quản trị viên hệ thống (admin / 123456) luôn tồn tại
            var adminUser = await context.PartyMemberProfiles
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Username == "admin");

            if (adminUser == null)
            {
                adminUser = new PartyMemberProfile
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    MustChangePassword = true,
                    FullName = "Quản trị viên Hệ thống",
                    Email = "admin@attech.com.vn",
                    PhoneNumber = "0900000000",
                    IsPartyMember = true,
                    PartyCardNumber = "ADMIN-001",
                    PartyCellId = cellVp.Id,
                    PartyRole = PartyRole.DangVien,
                    DepartmentId = depKh.Id,
                    AdminPosition = AdministrativePosition.ChuyenVien,
                    PositionTitle = "Quản trị viên Hệ thống CNTT",
                    JobGroup = JobGroup.Khung4_KhcnChuyenDoiSo,
                    ApprovalAuthority = ApprovalAuthority.CoSo,
                    Roles = new List<AppRole>()
                };
                if (roleAdmin != null) adminUser.Roles.Add(roleAdmin);
                await context.PartyMemberProfiles.AddAsync(adminUser);
                await context.SaveChangesAsync();
            }
            else if (roleAdmin != null && !adminUser.Roles.Any(r => r.Code == AppRoles.QUAN_TRI_HE_THONG))
            {
                adminUser.Roles.Add(roleAdmin);
                await context.SaveChangesAsync();
            }

            // Tách vai trò admin ra khỏi bithu_attech (Bí thư Đảng ủy là Ban Thường vụ, không phải Admin IT)
            var biThuUser = await context.PartyMemberProfiles
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Username == "bithu_attech");
            if (biThuUser != null && roleAdmin != null)
            {
                var adminRoleInBiThu = biThuUser.Roles.FirstOrDefault(r => r.Code == AppRoles.QUAN_TRI_HE_THONG);
                if (adminRoleInBiThu != null)
                {
                    biThuUser.Roles.Remove(adminRoleInBiThu);
                    await context.SaveChangesAsync();
                }
            }
            if (biThuUser != null && roleDangUyCoSo != null && !biThuUser.Roles.Any(r => r.Code == AppRoles.DANG_UY_CO_SO))
            {
                biThuUser.Roles.Add(roleDangUyCoSo);
                await context.SaveChangesAsync();
            }

            // Đảm bảo tài khoản Tổ thẩm định luôn tồn tại
            if (!await context.PartyMemberProfiles.AnyAsync(u => u.Username == "thamdinh_du"))
            {

                var thamDinh = new PartyMemberProfile
                {
                    Username = "thamdinh_du",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    MustChangePassword = true,
                    FullName = "Vũ Đình Hùng",
                    Email = "hungvd@attech.com.vn",
                    PhoneNumber = "0934567890",
                    IsPartyMember = true,
                    PartyCardNumber = "ATTECH-004",
                    PartyCellId = cellVp.Id,
                    PartyRole = PartyRole.DangUyVien,
                    DepartmentId = depTccb.Id,
                    AdminPosition = AdministrativePosition.TruongPhong,
                    PositionTitle = "Trưởng Ban TCCB, Tổ trưởng Tổ Thẩm định",
                    JobGroup = JobGroup.Khung1_QuanLyDangDoanThe,
                    ApprovalAuthority = ApprovalAuthority.CoSo,
                    Roles = new List<AppRole>()
                };
                if (roleToThamDinh != null) thamDinh.Roles.Add(roleToThamDinh);
                if (roleCanBo != null) thamDinh.Roles.Add(roleCanBo);

                await context.PartyMemberProfiles.AddAsync(thamDinh);
                await context.SaveChangesAsync();
            }

            // Nếu users đã tồn tại nhưng chưa có roles, đồng bộ roles theo PartyRole
            var usersWithoutRoles = await context.PartyMemberProfiles
                .Include(u => u.Roles)
                .Where(u => !u.Roles.Any())
                .ToListAsync();

            if (usersWithoutRoles.Any())
            {
                var roleBiThuCb = await context.Roles.FirstOrDefaultAsync(r => r.Code == AppRoles.BI_THU_CHI_BO);
                var roleBanThuongVu = await context.Roles.FirstOrDefaultAsync(r => r.Code == AppRoles.BAN_THUONG_VU);

                foreach (var user in usersWithoutRoles)
                {
                    if (roleCanBo != null) user.Roles.Add(roleCanBo);

                    if (user.PartyRole == PartyRole.BiThuChiBo || user.PartyRole == PartyRole.PhoBiThuChiBo)
                    {
                        if (roleBiThuCb != null) user.Roles.Add(roleBiThuCb);
                    }
                    else if (user.PartyRole == PartyRole.BiThuDangUy || user.PartyRole == PartyRole.PhoBiThuDangUy || user.PartyRole == PartyRole.UyVienBanThuongVu)
                    {
                        if (roleBanThuongVu != null) user.Roles.Add(roleBanThuongVu);
                    }
                }
                await context.SaveChangesAsync();
            }
        }

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

    /// <summary>Tạo các role hệ thống còn thiếu kèm quyền mặc định; không đụng tới role đã có (kể cả đã xóa mềm).</summary>
    private static async Task SeedMissingRolesAsync(CongTacDangDbContext context, ILogger? logger)
    {
        var existingRoleCodes = await context.Roles
            .IgnoreQueryFilters()
            .Select(r => r.Code)
            .ToListAsync();
        var missingRoles = DefaultRoles.Where(r => !existingRoleCodes.Contains(r.Code)).ToList();
        if (missingRoles.Count == 0)
            return;

        var permMap = await context.Permissions.ToDictionaryAsync(p => p.Code);
        foreach (var definition in missingRoles)
        {
            await context.Roles.AddAsync(new AppRole
            {
                Code = definition.Code,
                Name = definition.Name,
                Description = definition.Description,
                IsSystem = true,
                Permissions = definition.Permissions
                    .Where(permMap.ContainsKey)
                    .Select(code => permMap[code])
                    .ToList()
            });
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Đã tạo vai trò mặc định: {Roles}", string.Join(", ", missingRoles.Select(r => r.Code)));
    }

    /// <summary>Gán các mã quyền quản trị vừa được tạo cho vai trò Quản trị hệ thống (nếu vai trò còn tồn tại).</summary>
    private static async Task GrantNewAdministratorPermissionsAsync(
        CongTacDangDbContext context,
        IReadOnlyCollection<string> newlyCreatedCodes,
        ILogger? logger)
    {
        if (newlyCreatedCodes.Count == 0)
            return;

        var adminRole = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Code == AppRoles.QUAN_TRI_HE_THONG);
        if (adminRole == null)
            return;

        var toGrant = await context.Permissions
            .Where(p => newlyCreatedCodes.Contains(p.Code))
            .ToListAsync();
        var added = new List<string>();
        foreach (var permission in toGrant)
        {
            if (adminRole.Permissions.Any(p => p.Code == permission.Code))
                continue;
            adminRole.Permissions.Add(permission);
            added.Add(permission.Code);
        }

        if (added.Count == 0)
            return;

        await context.SaveChangesAsync();
        logger?.LogInformation(
            "Đã gán quyền mới {Permissions} cho vai trò {Role}.",
            string.Join(", ", added),
            AppRoles.QUAN_TRI_HE_THONG);
    }

    /// <summary>Đặt lại quyền của các role hệ thống về ma trận mặc định (chỉ khi bật Database:ResetRolePermissions).</summary>
    private static async Task ResetRolePermissionsAsync(CongTacDangDbContext context, ILogger? logger)
    {
        var permMap = await context.Permissions.ToDictionaryAsync(p => p.Code);
        var defaultCodes = DefaultRoles.Select(r => r.Code).ToList();
        var roles = await context.Roles
            .Include(r => r.Permissions)
            .Where(r => defaultCodes.Contains(r.Code))
            .ToListAsync();

        foreach (var role in roles)
        {
            var definition = DefaultRoles.First(r => r.Code == role.Code);
            role.Permissions.Clear();
            foreach (var code in definition.Permissions.Where(permMap.ContainsKey))
                role.Permissions.Add(permMap[code]);
        }

        await context.SaveChangesAsync();
        logger?.LogWarning(
            "Database:ResetRolePermissions đang bật: đã đặt lại quyền của các vai trò {Roles} về mặc định. Hãy tắt cờ này sau khi dùng.",
            string.Join(", ", roles.Select(r => r.Code)));
    }
}
