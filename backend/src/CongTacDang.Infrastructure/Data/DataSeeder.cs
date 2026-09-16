using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(CongTacDangDbContext context)
    {
        // 1. Seed Chi bộ Đảng tại ATTECH
        if (!await context.PartyCells.AnyAsync())
        {
            var cellKt = new PartyCell { Code = "CB-KT", Name = "Chi bộ Khối Kỹ thuật", Description = "Chi bộ phụ trách an toàn, điều hành kỹ thuật CNS, ATM" };
            var cellSx = new PartyCell { Code = "CB-SX", Name = "Chi bộ Sản xuất công nghiệp", Description = "Chi bộ phụ trách Xưởng sản xuất thiết bị hàng không" };
            var cellDv = new PartyCell { Code = "CB-DV", Name = "Chi bộ Dịch vụ kỹ thuật", Description = "Chi bộ phụ trách dịch vụ lắp đặt, bảo dưỡng" };
            var cellVp = new PartyCell { Code = "CB-VP", Name = "Chi bộ Khối Văn phòng", Description = "Chi bộ phụ trách Kế hoạch, Tài chính, TCCB-LĐ" };

            await context.PartyCells.AddRangeAsync(cellKt, cellSx, cellDv, cellVp);
            await context.SaveChangesAsync();
        }

        // 2. Seed Phòng ban Chính quyền tại ATTECH
        if (!await context.AdministrativeDepartments.AnyAsync())
        {
            var depKh = new AdministrativeDepartment { Code = "PH-KH", Name = "Phòng Kế hoạch", Description = "Phòng Kế hoạch đầu tư, dự án" };
            var depTc = new AdministrativeDepartment { Code = "PH-TC", Name = "Phòng Tài chính - Kế toán", Description = "Phòng Tài chính kế toán công ty" };
            var depTccb = new AdministrativeDepartment { Code = "PH-TCCB", Name = "Phòng Tổ chức cán bộ - Lao động", Description = "Phòng tham mưu tổ chức nhân sự, lao động tiền lương" };
            var depXs = new AdministrativeDepartment { Code = "XUONG-SX", Name = "Xưởng Sản xuất thiết bị", Description = "Xưởng sản xuất công nghiệp cơ khí, điện tử" };

            await context.AdministrativeDepartments.AddRangeAsync(depKh, depTc, depTccb, depXs);
            await context.SaveChangesAsync();
        }

        // 3. Seed Cán bộ / Đảng viên mẫu
        if (!await context.PartyMemberProfiles.AnyAsync())
        {
            var cellKt = await context.PartyCells.FirstAsync(x => x.Code == "CB-KT");
            var cellVp = await context.PartyCells.FirstAsync(x => x.Code == "CB-VP");
            var depTccb = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-TCCB");
            var depKh = await context.AdministrativeDepartments.FirstAsync(x => x.Code == "PH-KH");

            var biThuAttech = new PartyMemberProfile
            {
                Username = "bithu_attech",
                PasswordHash = "123456",
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
                IsApprovedByAttech = false
            };

            var biThuCbkt = new PartyMemberProfile
            {
                Username = "bithu_cbkt",
                PasswordHash = "123456",
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
                IsApprovedByAttech = true
            };

            var canBoKt = new PartyMemberProfile
            {
                Username = "canbo_kt",
                PasswordHash = "123456",
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
                IsApprovedByAttech = true
            };

            await context.PartyMemberProfiles.AddRangeAsync(biThuAttech, biThuCbkt, canBoKt);
            await context.SaveChangesAsync();
        }

        // 4. Seed Kỳ đánh giá Quý III/2026 và Cấu hình tham số động theo 03-HD/TVĐU
        if (!await context.EvaluationPeriods.AnyAsync())
        {
            var periodQ3 = new EvaluationPeriod
            {
                Year = 2026,
                Quarter = EvaluationQuarter.Quy3,
                Name = "Đánh giá định kỳ Quý III/2026",
                RegistrationDeadline = new DateTime(2026, 7, 5, 23, 59, 59, DateTimeKind.Utc),
                SelfEvaluationDeadline = new DateTime(2026, 9, 12, 23, 59, 59, DateTimeKind.Utc),
                VotingDeadline = new DateTime(2026, 9, 15, 23, 59, 59, DateTimeKind.Utc),
                ApprovalDeadline = new DateTime(2026, 9, 20, 23, 59, 59, DateTimeKind.Utc),
                IsActive = true,
                IsLocked = false
            };

            var setting = new EvaluationSetting
            {
                Period = periodQ3,
                MaxTotalScore = 100m,
                GeneralCriteriaMaxScore = 30m,
                TaskCriteriaMaxScore = 70m,
                Tc1Score = 18m,
                Tc2Score = 4m,
                Tc3Score = 8m,
                MaxExcellentPercentage = 20m,
                MaxExcellentSpecialPercentage = 25m,
                MinTasks = 3,
                MaxTasks = 7,
                ExcellentMinScore = 90m,
                GoodMinScore = 70m,
                PassMinScore = 50m
            };

            periodQ3.Setting = setting;
            await context.EvaluationPeriods.AddAsync(periodQ3);
            await context.SaveChangesAsync();
        }
    }
}
