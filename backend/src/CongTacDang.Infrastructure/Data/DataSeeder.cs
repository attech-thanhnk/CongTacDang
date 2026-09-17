using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;

namespace CongTacDang.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(CongTacDangDbContext context)
    {
        // 0. Chuẩn hóa tên bảng CSDL sang snake_case đồng nhất (Đổi tên an toàn nếu tồn tại bảng cũ PascalCase)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'PartyMemberProfiles') THEN
                        ALTER TABLE ""PartyMemberProfiles"" RENAME TO party_member_profiles;
                    END IF;
                    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'PartyCells') THEN
                        ALTER TABLE ""PartyCells"" RENAME TO party_cells;
                    END IF;
                    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'AdministrativeDepartments') THEN
                        ALTER TABLE ""AdministrativeDepartments"" RENAME TO administrative_departments;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS refresh_tokens (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""UserId"" uuid NOT NULL REFERENCES party_member_profiles(""Id"") ON DELETE CASCADE,
                    ""Token"" character varying(256) NOT NULL,
                    ""ExpiresAt"" timestamp with time zone NOT NULL,
                    ""IsRevoked"" boolean NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""ReplacedByToken"" character varying(256),
                    ""CreatedByIp"" character varying(100)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_refresh_tokens_Token"" ON refresh_tokens (""Token"");
                CREATE INDEX IF NOT EXISTS ""IX_refresh_tokens_UserId"" ON refresh_tokens (""UserId"");
            ");
        }
        catch
        {
            // Bỏ qua nếu môi trường test in-memory hoặc lỗi cú pháp DB khác
        }

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

        var existingPermCodes = await context.Permissions.Select(p => p.Code).ToListAsync();
        var missingPerms = definedPermissions.Where(p => !existingPermCodes.Contains(p.Code)).ToList();
        if (missingPerms.Any())
        {
            await context.Permissions.AddRangeAsync(missingPerms);
            await context.SaveChangesAsync();
        }

        // 2. Seed Roles & Role-Permission Mapping
        if (!await context.Roles.AnyAsync())
        {
            var allPerms = await context.Permissions.ToListAsync();
            var permMap = allPerms.ToDictionary(p => p.Code);

            // Role 1: CAN_BO
            var roleCanBo = new AppRole
            {
                Code = AppRoles.CAN_BO,
                Name = "Cán bộ, Đảng viên",
                Description = "Quyền cơ bản của mọi Đảng viên, cán bộ trong hệ thống",
                IsSystem = true,
                Permissions = new List<Permission>
                {
                    permMap[AppPermissions.UsersRead],
                    permMap[AppPermissions.BranchesRead],
                    permMap[AppPermissions.AttachmentsRead],
                    permMap[AppPermissions.AttachmentsUpload],
                    permMap[AppPermissions.ReportsExport],
                    permMap[AppPermissions.EvaluationsRead],
                    permMap[AppPermissions.EvaluationsRegister],
                    permMap[AppPermissions.EvaluationsSelfScore]
                }
            };

            // Role 2: BI_THU_CHI_BO
            var roleBiThuChiBo = new AppRole
            {
                Code = AppRoles.BI_THU_CHI_BO,
                Name = "Bí thư Chi bộ",
                Description = "Bí thư / Phó Bí thư Chi bộ cơ sở trực thuộc Đảng bộ",
                IsSystem = true,
                Permissions = new List<Permission>(roleCanBo.Permissions)
                {
                    permMap[AppPermissions.BranchesUpdate],
                    permMap[AppPermissions.EvaluationsBranchVote]
                }
            };

            // Role 3: TO_THAM_DINH
            var roleToThamDinh = new AppRole
            {
                Code = AppRoles.TO_THAM_DINH,
                Name = "Tổ Thẩm định",
                Description = "Tổ thẩm định đánh giá cán bộ và rà soát minh chứng",
                IsSystem = true,
                Permissions = new List<Permission>(roleCanBo.Permissions)
                {
                    permMap[AppPermissions.AttachmentsDelete],
                    permMap[AppPermissions.EvaluationsAppraise]
                }
            };

            // Role 4: BAN_THUONG_VU
            var roleBanThuongVu = new AppRole
            {
                Code = AppRoles.BAN_THUONG_VU,
                Name = "Ban Thường vụ Đảng ủy",
                Description = "Ủy viên Ban Thường vụ, Phó Bí thư, Bí thư Đảng ủy",
                IsSystem = true,
                Permissions = new List<Permission>(roleCanBo.Permissions)
                {
                    permMap[AppPermissions.BranchesCreate],
                    permMap[AppPermissions.BranchesUpdate],
                    permMap[AppPermissions.BranchesDelete],
                    permMap[AppPermissions.AttachmentsDelete],
                    permMap[AppPermissions.EvaluationsBranchVote],
                    permMap[AppPermissions.EvaluationsAppraise],
                    permMap[AppPermissions.EvaluationsApprove]
                }
            };

            // Role 5: QUAN_TRI_HE_THONG (Quản trị viên kỹ thuật & phân quyền - Không tham gia đánh giá cá nhân)
            var roleAdmin = new AppRole
            {
                Code = AppRoles.QUAN_TRI_HE_THONG,
                Name = "Quản trị hệ thống",
                Description = "Toàn quyền quản trị kỹ thuật hệ thống, danh mục Chi bộ/Phòng ban và phân quyền",
                IsSystem = true,
                Permissions = new List<Permission>
                {
                    permMap[AppPermissions.UsersRead],
                    permMap[AppPermissions.UsersCreate],
                    permMap[AppPermissions.UsersUpdate],
                    permMap[AppPermissions.UsersDelete],
                    permMap[AppPermissions.BranchesRead],
                    permMap[AppPermissions.BranchesCreate],
                    permMap[AppPermissions.BranchesUpdate],
                    permMap[AppPermissions.BranchesDelete],
                    permMap[AppPermissions.AttachmentsRead],
                    permMap[AppPermissions.AttachmentsUpload],
                    permMap[AppPermissions.AttachmentsDelete],
                    permMap[AppPermissions.ReportsExport],
                    permMap[AppPermissions.RolesManage],
                    permMap[AppPermissions.EvaluationsRead]
                }
            };

            await context.Roles.AddRangeAsync(roleCanBo, roleBiThuChiBo, roleToThamDinh, roleBanThuongVu, roleAdmin);
            await context.SaveChangesAsync();
        }

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
            var roleAdmin = await context.Roles.FirstAsync(r => r.Code == AppRoles.QUAN_TRI_HE_THONG);
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            var adminUser = new PartyMemberProfile
            {
                Username = "admin",
                PasswordHash = defaultPasswordHash,
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
                IsApprovedByAttech = true,
                Roles = new List<AppRole> { roleAdmin }
            };

            var biThuAttech = new PartyMemberProfile
            {
                Username = "bithu_attech",
                PasswordHash = defaultPasswordHash,
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
                IsApprovedByAttech = false,
                Roles = new List<AppRole> { roleBanThuongVu, roleCanBo }
            };

            var biThuCbkt = new PartyMemberProfile
            {
                Username = "bithu_cbkt",
                PasswordHash = defaultPasswordHash,
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
                IsApprovedByAttech = true,
                Roles = new List<AppRole> { roleBiThuCb, roleCanBo }
            };

            var roleToThamDinh = await context.Roles.FirstAsync(r => r.Code == AppRoles.TO_THAM_DINH);
            var depTccb = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-TCCB");

            var canBoKt = new PartyMemberProfile
            {
                Username = "canbo_kt",
                PasswordHash = defaultPasswordHash,
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
                IsApprovedByAttech = true,
                Roles = new List<AppRole> { roleCanBo }
            };

            var thamDinhDu = new PartyMemberProfile
            {
                Username = "thamdinh_du",
                PasswordHash = defaultPasswordHash,
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
                IsApprovedByAttech = true,
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
                    IsApprovedByAttech = true,
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

            // Đảm bảo tài khoản Tổ thẩm định luôn tồn tại
            if (!await context.PartyMemberProfiles.AnyAsync(u => u.Username == "thamdinh_du"))
            {

                var thamDinh = new PartyMemberProfile
                {
                    Username = "thamdinh_du",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
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
                    IsApprovedByAttech = true,
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

        // Tự động nâng cấp mật khẩu cũ dạng plain-text sang BCrypt hash trong cơ sở dữ liệu hiện có
        var allUsers = await context.PartyMemberProfiles.ToListAsync();
        var plainTextUsers = allUsers
            .Where(u => !u.PasswordHash.StartsWith("$2a$") && !u.PasswordHash.StartsWith("$2b$") && !u.PasswordHash.StartsWith("$2y$"))
            .ToList();
        if (plainTextUsers.Any())
        {
            foreach (var u in plainTextUsers)
            {
                var rawPass = string.IsNullOrWhiteSpace(u.PasswordHash) ? "123456" : u.PasswordHash;
                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPass);
            }
            await context.SaveChangesAsync();
        }

        // 6. Seed Tệp tin văn bản chỉ đạo & tài liệu mẫu
        if (!await context.TaskAttachments.AnyAsync())
        {
            var doc1 = new TaskAttachment
            {
                FileName = "Huong_dan_03_HD_TVDU_Danh_gia_can_bo.pdf",
                OriginalFileName = "03-HD-TVDU.pdf",
                ContentType = "application/pdf",
                FileSize = 1048576,
                ObjectKey = "documents/202609/Huong_dan_03_HD_TVDU_Danh_gia_can_bo.pdf",
                Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                FormCode = "03-HD/TVĐU",
                Description = "Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty",
                UploadedBy = "Lê Tiến Thịnh",
                UploadedAt = DateTime.UtcNow
            };

            var doc2 = new TaskAttachment
            {
                FileName = "Quyet_dinh_thanh_lap_To_tham_dinh.pdf",
                OriginalFileName = "QD-To-Tham-Dinh.pdf",
                ContentType = "application/pdf",
                FileSize = 524288,
                ObjectKey = "documents/202609/Quyet_dinh_thanh_lap_To_tham_dinh.pdf",
                Checksum = "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb",
                FormCode = "QD-TD",
                Description = "Quyết định thành lập Tổ Thẩm định hồ sơ đánh giá cán bộ",
                UploadedBy = "Lê Tiến Thịnh",
                UploadedAt = DateTime.UtcNow
            };

            await context.TaskAttachments.AddRangeAsync(doc1, doc2);
            await context.SaveChangesAsync();
        }

        // 7. Đồng bộ quyền hạn đánh giá mới vào các Vai trò đã tồn tại
        var rolesWithPerms = await context.Roles.Include(r => r.Permissions).ToListAsync();
        var allDbPerms = await context.Permissions.ToListAsync();
        var permLookup = allDbPerms.ToDictionary(p => p.Code);

        foreach (var role in rolesWithPerms)
        {
            var existingCodes = role.Permissions.Select(p => p.Code).ToHashSet();
            void AddIfMissing(string code)
            {
                if (!existingCodes.Contains(code) && permLookup.TryGetValue(code, out var p))
                {
                    role.Permissions.Add(p);
                }
            }

            if (role.Code == AppRoles.QUAN_TRI_HE_THONG)
            {
                // Tách biệt vai trò Admin: gỡ bỏ các quyền tham gia đánh giá cá nhân và biểu quyết
                var evalPermsToRemove = role.Permissions
                    .Where(p => p.Code == AppPermissions.EvaluationsRegister ||
                                p.Code == AppPermissions.EvaluationsSelfScore ||
                                p.Code == AppPermissions.EvaluationsBranchVote ||
                                p.Code == AppPermissions.EvaluationsAppraise ||
                                p.Code == AppPermissions.EvaluationsApprove)
                    .ToList();
                foreach (var p in evalPermsToRemove)
                {
                    role.Permissions.Remove(p);
                }

                AddIfMissing(AppPermissions.EvaluationsRead);
                AddIfMissing(AppPermissions.RolesManage);
                continue;
            }

            AddIfMissing(AppPermissions.EvaluationsRead);
            AddIfMissing(AppPermissions.EvaluationsRegister);
            AddIfMissing(AppPermissions.EvaluationsSelfScore);

            if (role.Code == AppRoles.BI_THU_CHI_BO || role.Code == AppRoles.BAN_THUONG_VU)
            {
                AddIfMissing(AppPermissions.EvaluationsBranchVote);
            }
            if (role.Code == AppRoles.TO_THAM_DINH || role.Code == AppRoles.BAN_THUONG_VU)
            {
                AddIfMissing(AppPermissions.EvaluationsAppraise);
            }
            if (role.Code == AppRoles.BAN_THUONG_VU)
            {
                AddIfMissing(AppPermissions.EvaluationsApprove);
            }
        }
        await context.SaveChangesAsync();

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
            var docAttachment = await context.TaskAttachments.FirstOrDefaultAsync();

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
                        IsExceedStandard = isKeyLeader,
                        AttachmentId = docAttachment?.Id
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
}
