using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly CongTacDangDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly IOrganizationRepository _orgRepo;

    public ReportService(CongTacDangDbContext db, IUserRepository userRepo, IOrganizationRepository orgRepo)
    {
        _db = db;
        _userRepo = userRepo;
        _orgRepo = orgRepo;
    }

    public async Task<ReportFileResult> ExportCadresReportAsync()
    {
        var members = await _userRepo.GetAllWithDetailsAsync();

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Danh sach can bo");

            ws.Cell("A1").Value = "DANG BO CONG TY TNHH KY THUAT QUAN LY BAY";
            ws.Cell("A1").Style.Font.Bold = true;

            ws.Cell("A3").Value = "DANH SACH CAN BO LANH DAO, QUAN LY ATTECH";
            ws.Range("A3:H3").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "STT", "Ho va ten", "So the Dang", "Chi bo", "Chuc vu Dang", "Don vi chuyen mon", "Chuc danh chinh quyen", "Trang thai" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(5, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 6;
            int stt = 1;
            foreach (var m in members)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = m.FullName;
                ws.Cell(row, 3).Value = m.PartyCardNumber ?? "";
                ws.Cell(row, 4).Value = m.PartyCell?.Name ?? "";
                ws.Cell(row, 5).Value = m.PartyRole.ToString();
                ws.Cell(row, 6).Value = m.Department?.Name ?? "";
                ws.Cell(row, 7).Value = m.PositionTitle ?? "";
                ws.Cell(row, 8).Value = m.IsActive ? "Dang hoat dong" : "Da khoa";

                for (int c = 1; c <= 8; c++)
                {
                    ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return new ReportFileResult
                {
                    FileBytes = stream.ToArray(),
                    FileName = "DanhSach_CanBo_ATTECH.xlsx"
                };
            }
        }
    }

    public async Task<ReportFileResult> ExportForm14ReportAsync()
    {
        var members = await _userRepo.GetAllWithDetailsAsync();

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Mau 14 - Danh sach can bo");

            ws.Cell("A1").Value = "DANG BO TONG CONG TY QUAN LY BAY VIET NAM";
            ws.Cell("A2").Value = "DANG BO CONG TY TNHH KY THUAT QUAN LY BAY";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.Bold = true;

            ws.Cell("D1").Value = "DANG CONG SAN VIET NAM";
            ws.Cell("D1").Style.Font.Bold = true;
            ws.Cell("D2").Value = $"Ha Noi, ngay {DateTime.Now:dd} thang {DateTime.Now:MM} nam {DateTime.Now:yyyy}";

            ws.Cell("A4").Value = "DANH SACH TONG HOP HO SO CAN BO LANH DAO, QUAN LY 2 VAI QUY III/2026";
            ws.Range("A4:H4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell("A5").Value = "(Kem theo Huong dan so 03-HD/TVDU ngay 06/7/2026 cua Ban Thuong vu Dang uy Tong cong ty)";
            ws.Range("A5:H5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "STT", "Ho va ten can bo", "So the Dang", "Chuc vu Dang", "Chuc danh chinh quyen", "Chi bo sinh hoat", "Don vi chuyen mon", "Trang thai ho so" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(7, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 8;
            int stt = 1;
            foreach (var m in members)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = m.FullName;
                ws.Cell(row, 3).Value = m.PartyCardNumber ?? "";
                ws.Cell(row, 4).Value = m.PartyRole.ToString();
                ws.Cell(row, 5).Value = m.PositionTitle ?? "";
                ws.Cell(row, 6).Value = m.PartyCell?.Name ?? "";
                ws.Cell(row, 7).Value = m.Department?.Name ?? "";
                ws.Cell(row, 8).Value = m.IsActive ? "Hop le" : "Chua xac thuc";

                for (int c = 1; c <= 8; c++)
                {
                    ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return new ReportFileResult
                {
                    FileBytes = stream.ToArray(),
                    FileName = "Mau_14_DanhSachHoSoCanBo_Q3_2026.xlsx"
                };
            }
        }
    }

    public async Task<ReportFileResult> ExportForm15ReportAsync()
    {
        var cells = await _orgRepo.GetPartyCellsWithMembersAsync();

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Mau 15 - Thong ke Chi bo");

            ws.Cell("A1").Value = "DANG BO TONG CONG TY QUAN LY BAY VIET NAM";
            ws.Cell("A2").Value = "DANG BO CONG TY TNHH KY THUAT QUAN LY BAY";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.Bold = true;

            ws.Cell("D1").Value = "DANG CONG SAN VIET NAM";
            ws.Cell("D1").Style.Font.Bold = true;
            ws.Cell("D2").Value = $"Ha Noi, ngay {DateTime.Now:dd} thang {DateTime.Now:MM} nam {DateTime.Now:yyyy}";

            ws.Cell("A4").Value = "BANG TONG HOP THONG KE CO CAU CHI BO VA CAN BO LANH DAO QUY III/2026";
            ws.Range("A4:F4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell("A5").Value = "(Thuc hien theo quy dinh ve quan ly to chuc co so Dang truc thuoc)";
            ws.Range("A5:F5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "STT", "To chuc Dang / Chi bo", "Ma Chi bo", "So luong can bo", "Mo ta nhiem vu", "Trang thai hoat dong" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(7, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 8;
            int stt = 1;
            foreach (var c in cells)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = c.Name;
                ws.Cell(row, 3).Value = c.Code;
                ws.Cell(row, 4).Value = c.Members.Count;
                ws.Cell(row, 5).Value = c.Description ?? "";
                ws.Cell(row, 6).Value = c.IsActive ? "Hoat dong" : "Tam ngung";

                for (int col = 1; col <= 6; col++)
                {
                    ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return new ReportFileResult
                {
                    FileBytes = stream.ToArray(),
                    FileName = "Mau_15_ThongKeChiBo_Q3_2026.xlsx"
                };
            }
        }
    }
}
