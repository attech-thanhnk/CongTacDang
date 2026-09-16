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

        // 3. Seed Cán bộ / Đảng viên mẫu (Quản trị người dùng & Phân quyền)
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

        // 4. Seed Tệp tin văn bản chỉ đạo & tài liệu mẫu (Upload & Quản lý tài liệu)
        if (!await context.TaskAttachments.AnyAsync())
        {
            var doc1 = new TaskAttachment
            {
                FileName = "Huong_dan_03_HD_TVDU_Danh_gia_can_bo.pdf",
                OriginalFileName = "03-HD-TVDU.pdf",
                ContentType = "application/pdf",
                FileSize = 1048576,
                ObjectKey = "general/202609/Huong_dan_03_HD_TVDU_Danh_gia_can_bo.pdf",
                Provider = "local",
                Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                FormCode = "GENERAL",
                Description = "Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy",
                UploadedBy = "Lê Tiến Thịnh",
                UploadedAt = DateTime.UtcNow
            };

            var doc2 = new TaskAttachment
            {
                FileName = "Quyet_dinh_thanh_lap_To_tham_dinh.pdf",
                OriginalFileName = "QD-To-Tham-Dinh.pdf",
                ContentType = "application/pdf",
                FileSize = 524288,
                ObjectKey = "decision/202609/Quyet_dinh_thanh_lap_To_tham_dinh.pdf",
                Provider = "local",
                Checksum = "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb",
                FormCode = "DECISION",
                Description = "Quyết định thành lập Tổ Thẩm định hồ sơ đánh giá cán bộ",
                UploadedBy = "Lê Tiến Thịnh",
                UploadedAt = DateTime.UtcNow
            };

            await context.TaskAttachments.AddRangeAsync(doc1, doc2);
            await context.SaveChangesAsync();
        }
    }
}
