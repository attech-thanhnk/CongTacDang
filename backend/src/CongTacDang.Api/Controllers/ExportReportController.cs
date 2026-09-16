using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExportReportController : ControllerBase
    {
        private readonly CongTacDangDbContext _db;

        public ExportReportController(CongTacDangDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Ket xuat file Excel Mau 14: Danh sach danh gia va de xuat xep loai quy
        /// </summary>
        [HttpGet("mau-14")]
        public async Task<IActionResult> ExportMau14([FromQuery] int periodId = 3)
        {
            var records = await _db.EvaluationRecords
                .Include(r => r.Member)
                .Include(r => r.Period)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Mau 14 - De xuat xep loai");

                ws.Cell("A1").Value = "DANG BO TONG CONG TY QUAN LY BAY VIET NAM";
                ws.Cell("A2").Value = "DANG BO CONG TY TNHH KY THUAT QUAN LY BAY";
                ws.Cell("A1").Style.Font.Bold = true;
                ws.Cell("A2").Style.Font.Bold = true;

                ws.Cell("F1").Value = "DANG CONG SAN VIET NAM";
                ws.Cell("F1").Style.Font.Bold = true;
                ws.Cell("F2").Value = $"Ha Noi, ngay {DateTime.Now:dd} thang {DateTime.Now:MM} nam {DateTime.Now:yyyy}";

                ws.Cell("A4").Value = "DANH SACH DANH GIA VA DE XUAT XEP LOAI QUY III NAM 2026";
                ws.Range("A4:J4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell("A5").Value = "(Ban hanh kem theo Huong dan so 03-HD/TVDU ngay 10/9/2026 cua BTV Dang uy VATM)";
                ws.Range("A5:J5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                var headers = new[] { "STT", "Ho va ten", "Chuc danh 2 vai", "Chi bo", "Diem TC Chung (30d)", "Diem Chuyen mon (70d)", "Tong diem (100d)", "Tap the de xuat", "Tham dinh de xuat", "Xep loai chinh thuc" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(7, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                int row = 8;
                int stt = 1;
                var sampleData = new[]
                {
                    new { Name = "Nguyen Van A", Title = "Truong phong Ke hoach KD", Branch = "Chi bo Khoi Ky thuat", Common = 30.0, Tasks = 67.6, Total = 97.6, Prop1 = "HXS", Prop2 = "HXS", Final = "Hoan thanh xuat sac nhiem vu" },
                    new { Name = "Tran Thi B", Title = "Truong phong TCCB - Lao dong", Branch = "Chi bo Co quan Van phong", Common = 28.0, Tasks = 58.0, Total = 86.0, Prop1 = "HTT", Prop2 = "HTT", Final = "Hoan thanh tot nhiem vu" },
                    new { Name = "Le Quang C", Title = "Quan doc Xuong San xuat", Branch = "Chi bo Khoi San xuat", Common = 29.0, Tasks = 63.0, Total = 92.0, Prop1 = "HTT", Prop2 = "HTT", Final = "Hoan thanh tot nhiem vu" },
                    new { Name = "Pham Van D", Title = "Pho Truong phong Ke toan", Branch = "Chi bo Co quan Van phong", Common = 28.5, Tasks = 60.0, Total = 88.5, Prop1 = "HTNV", Prop2 = "HTNV", Final = "Hoan thanh tot nhiem vu" },
                };

                foreach (var item in sampleData)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = item.Name;
                    ws.Cell(row, 3).Value = item.Title;
                    ws.Cell(row, 4).Value = item.Branch;
                    ws.Cell(row, 5).Value = item.Common;
                    ws.Cell(row, 6).Value = item.Tasks;
                    ws.Cell(row, 7).Value = item.Total;
                    ws.Cell(row, 8).Value = item.Prop1;
                    ws.Cell(row, 9).Value = item.Prop2;
                    ws.Cell(row, 10).Value = item.Final;

                    for (int c = 1; c <= 10; c++)
                    {
                        ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    row++;
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_14_DanhSachXepLoai_Q3_2026.xlsx");
                }
            }
        }

        /// <summary>
        /// Ket xuat file Excel Mau 15: Tong hop ty le % Xuat sac (Ap tran 20%)
        /// </summary>
        [HttpGet("mau-15")]
        public IActionResult ExportMau15([FromQuery] int periodId = 3)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Mau 15 - Tong hop tran 20%");

                ws.Cell("A1").Value = "DANG BO TONG CONG TY QUAN LY BAY VIET NAM";
                ws.Cell("A2").Value = "DANG BO CONG TY TNHH KY THUAT QUAN LY BAY";
                ws.Cell("A1").Style.Font.Bold = true;
                ws.Cell("A2").Style.Font.Bold = true;

                ws.Cell("D1").Value = "DANG CONG SAN VIET NAM";
                ws.Cell("D1").Style.Font.Bold = true;
                ws.Cell("D2").Value = $"Ha Noi, ngay {DateTime.Now:dd} thang {DateTime.Now:MM} nam {DateTime.Now:yyyy}";

                ws.Cell("A4").Value = "BANG TONG HOP TY LE CAN BO HOAN THANH XUAT SAC NHIEM VU QUY III/2026";
                ws.Range("A4:G4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell("A5").Value = "(Kiem soat khong qua 20% so can bo duoc xep loai Hoan thanh tot nhiem vu tro len)";
                ws.Range("A5:G5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                var headers = new[] { "STT", "To chuc Dang / Don vi", "Tong so can bo", "So dat Hoan thanh tot tro len", "So luong Xuat sac (Toi da 20%)", "Ty le thuc te (%)", "Trang thai kiem soat" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(7, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                var branches = new[]
                {
                    new { Name = "Chi bo Khoi Ky thuat", Total = 18, GoodPlus = 18, MaxAllowed = 3, Actual = 3, Pct = 16.7, Status = "Hop le (<= 20%)" },
                    new { Name = "Chi bo Khoi San xuat", Total = 16, GoodPlus = 15, MaxAllowed = 3, Actual = 2, Pct = 13.3, Status = "Hop le (<= 20%)" },
                    new { Name = "Chi bo Khoi Dich vu", Total = 14, GoodPlus = 14, MaxAllowed = 2, Actual = 2, Pct = 14.3, Status = "Hop le (<= 20%)" },
                    new { Name = "Chi bo Co quan Van phong", Total = 20, GoodPlus = 20, MaxAllowed = 4, Actual = 4, Pct = 20.0, Status = "Kich tran (20.0%)" },
                };

                int row = 8;
                int stt = 1;
                foreach (var b in branches)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = b.Name;
                    ws.Cell(row, 3).Value = b.Total;
                    ws.Cell(row, 4).Value = b.GoodPlus;
                    ws.Cell(row, 5).Value = b.MaxAllowed;
                    ws.Cell(row, 6).Value = $"{b.Pct}%";
                    ws.Cell(row, 7).Value = b.Status;

                    for (int c = 1; c <= 7; c++)
                    {
                        ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    row++;
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_15_TongHopTran20_Q3_2026.xlsx");
                }
            }
        }
    }
}
