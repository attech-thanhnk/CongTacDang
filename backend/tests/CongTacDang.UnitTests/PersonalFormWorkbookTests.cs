using ClosedXML.Excel;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Bản Excel Mẫu 01, 02, 09A, 9D, 10 dựng từ cùng lớp dữ liệu với bản Word.</summary>
public class PersonalFormWorkbookTests
{
    private static readonly OrganizationTemplateFields Org =
        OrganizationTemplateFields.From("ĐẢNG BỘ A", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội", "Ban Tổ chức");

    [Fact]
    public void Mau01_HasTitle_Columns_TaskRows_AndWeightTotal()
    {
        var record = DocumentTemplateTests.SampleData().Records[0];
        record.TasksApprovedAt = new DateTime(2026, 7, 3, 2, 0, 0, DateTimeKind.Utc);
        using var wb = Open(PersonalFormWorkbooks.Mau01(Mau01Data.From(record), Org));
        var ws = wb.Worksheet(1);

        Assert.Equal("Mẫu số 01", ws.Cell(1, 11).GetString());
        Assert.Equal("CÔNG TY A", ws.Cell(2, 1).GetString());
        Assert.Contains("Hà Nội, ngày 03 tháng 07 năm 2026", Texts(ws));
        Assert.Contains("PHIẾU GIAO / ĐĂNG KÝ SẢN PHẨM, CÔNG VIỆC CHUYÊN MÔN HẰNG QUÝ", Texts(ws));
        Assert.Contains("Họ và tên cán bộ: Nguyễn Văn A", Texts(ws));

        var head = Find(ws, "Tên sản phẩm/nhiệm vụ");
        Assert.Equal("Thẩm quyền xác nhận", ws.Cell(head.Address.RowNumber, 11).GetString());
        var first = head.Address.RowNumber + 1;
        Assert.Equal(1, ws.Cell(first, 1).GetDouble());
        Assert.Equal("Bảo dưỡng hệ thống radar", ws.Cell(first, 2).GetString());
        Assert.Equal(30, ws.Cell(first, 6).GetDouble());
        Assert.Equal("30/09/2026", ws.Cell(first, 7).GetString());

        var total = Find(ws, "CỘNG").Address.RowNumber;
        Assert.Equal(first + 3, total);
        Assert.Equal(70, ws.Cell(total, 6).GetDouble());
        Assert.Contains("CÁ NHÂN ĐĂNG KÝ", Texts(ws));

        // Họ tên dưới bốn ô ký; bước gửi chưa có người thực hiện thì để dòng chấm; đầu phiếu để ghi tay.
        record.TasksApprovedById = Guid.NewGuid();
        record.TasksApprovedByName = "Nguyễn Văn Duyệt";
        record.TasksReviewedById = Guid.NewGuid();
        record.TasksReviewedByName = "Lê Thị Rà Soát";
        using var approved = Open(PersonalFormWorkbooks.Mau01(Mau01Data.From(record), Org));
        var sheet = approved.Worksheet(1);
        var approval = Find(sheet, "CẤP CÓ THẨM QUYỀN PHÊ DUYỆT").Address;
        Assert.Equal("Nguyễn Văn Duyệt", sheet.Cell(approval.RowNumber + 5, approval.ColumnNumber).GetString());
        Assert.Equal("Lê Thị Rà Soát", sheet.Cell(approval.RowNumber + 5, Find(sheet, "CƠ QUAN TỔ CHỨC CÁN BỘ").Address.ColumnNumber).GetString());
        Assert.Equal("……", sheet.Cell(approval.RowNumber + 5, Find(sheet, "NGƯỜI TRỰC TIẾP SỬ DỤNG").Address.ColumnNumber).GetString());
        Assert.Equal(record.Member!.FullName, sheet.Cell(approval.RowNumber + 5, Find(sheet, "CÁ NHÂN ĐĂNG KÝ").Address.ColumnNumber).GetString());
        Assert.Contains("Người quản lý trực tiếp: ……", Texts(sheet));
    }

    [Fact]
    public void Mau02_HasRatios_Scores_ExceedMark_AndTotals()
    {
        var record = DocumentTemplateTests.SampleData().Records[0];
        using var wb = Open(PersonalFormWorkbooks.Mau02(Mau02Data.From(record), Org));
        var ws = wb.Worksheet(1);

        var head = Find(ws, "A. Khối lượng (%)").Address.RowNumber;
        Assert.Equal("Minh chứng/đường dẫn", ws.Cell(head, 11).GetString());
        var first = head + 1;
        Assert.Equal(0.9, ws.Cell(first, 5).GetDouble(), 3);
        Assert.Equal(27.3, ws.Cell(first, 9).GetDouble(), 3);
        Assert.Equal("Đạt vượt chuẩn", ws.Cell(first, 10).GetString());
        Assert.Equal("-", ws.Cell(first + 1, 10).GetString());

        var total = Find(ws, "CỘNG").Address.RowNumber;
        Assert.Equal(70, ws.Cell(total, 3).GetDouble());
        Assert.Equal(65.4, ws.Cell(total, 9).GetDouble(), 3);
        Assert.Equal("1 SP", ws.Cell(total, 10).GetString());
        Assert.Contains("CÁ NHÂN TỰ ĐÁNH GIÁ", Texts(ws));
    }

    [Fact]
    public void Mau09A_ListsCriteriaGroups_Items_AndSummary()
    {
        var source = Source();
        var data = Mau09AData.From(source);
        using var wb = Open(PersonalFormWorkbooks.Mau09A(data, Org));
        var ws = wb.Worksheet(1);

        Assert.Equal("ĐẢNG BỘ A", ws.Cell(2, 1).GetString());
        Assert.Equal("CHI BỘ KỸ THUẬT", ws.Cell(3, 1).GetString());
        Assert.Contains("PHIẾU TỰ CHẤM ĐIỂM ĐÁNH GIÁ, XẾP LOẠI CHẤT LƯỢNG HẰNG QUÝ", Texts(ws));
        Assert.Contains("Ngày sinh: 21/10/1973", Texts(ws));

        var firstGroup = data.Groups[0];
        var groupRow = Find(ws, firstGroup.Name!).Address.RowNumber;
        Assert.Equal(firstGroup.Number, ws.Cell(groupRow, 1).GetString());
        var item = firstGroup.Items[0];
        Assert.Equal(item.Code, ws.Cell(groupRow + 1, 1).GetString());
        Assert.Equal("X", ws.Cell(groupRow + 1, 3).GetString());

        var reduced = Find(ws, "2.1").Address.RowNumber;
        Assert.Equal(0, ws.Cell(reduced, 6).GetDouble());
        Assert.Equal("X", ws.Cell(reduced, 4).GetString());
        Assert.Equal("Căn cứ giảm điểm", ws.Cell(reduced, 7).GetString());

        var total = Find(ws, "Tổng").Address.RowNumber;
        Assert.Equal(source.Criteria.GeneralGroups.Sum(g => g.MaxScore), ws.Cell(total, 5).GetDouble(), 3);
        Assert.Equal(29, ws.Cell(total, 6).GetDouble());

        var summary = Find(ws, "TỔNG ĐIỂM (100)").Address.RowNumber + 1;
        Assert.Equal(93, ws.Cell(summary, 4).GetDouble());
        Assert.Equal(0.5, ws.Cell(summary, 5).GetDouble(), 3);
        Assert.Contains("☒ Hoàn thành tốt", ws.Cell(summary, 7).GetString());
    }

    [Fact]
    public void Mau9D_HasOneRowPerAxis_WithReportedSummary()
    {
        var source = Source();
        using var wb = Open(PersonalFormWorkbooks.Mau9D(Mau9DData.From(source), Org));
        var ws = wb.Worksheet(1);

        Assert.Contains("KẾT QUẢ THỰC HIỆN NHIỆM VỤ, CÔNG VIỆC ĐƯỢC GIAO TRONG QUÝ III NĂM 2026", Texts(ws));
        Assert.Contains("Đơn vị công tác: Phòng Kỹ thuật, Công ty A", Texts(ws));
        var head = Find(ws, "Nội dung nhiệm vụ, công việc").Address.RowNumber;
        Assert.Equal("(7)", ws.Cell(head + 1, 7).GetString());
        var axis1 = Find(ws, "Trục 1").Address.RowNumber;
        Assert.Equal(head + 2, axis1);
        Assert.Equal("30/9/2026", ws.Cell(axis1, 3).GetString());
        Assert.Equal("Đã xong", ws.Cell(axis1, 4).GetString());
        Assert.Equal("Đúng hạn", ws.Cell(axis1, 6).GetString());
        Assert.Equal(source.Criteria.Axes.Count, Enumerable.Range(axis1, source.Criteria.Axes.Count)
            .Count(r => ws.Cell(r, 1).GetString().StartsWith("Trục ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Mau10_HasSelfAndAppraisalScores_Diff_AndProposedGrade()
    {
        var record = DocumentTemplateTests.SampleData().Records[0];
        record.AppraisalGeneralScore = 28;
        record.AppraisalTasksScore = 64;
        using var wb = Open(PersonalFormWorkbooks.Mau10(Mau10Data.From(record), Org));
        var ws = wb.Worksheet(1);

        Assert.Equal("CÔNG TY A", ws.Cell(2, 1).GetString());
        Assert.Equal("BAN TỔ CHỨC", ws.Cell(3, 1).GetString());
        Assert.Equal("Căn cứ thẩm định/đối soát", ws.Cell(Find(ws, "Nội dung thẩm định").Address.RowNumber, 5).GetString());
        var general = Find(ws, "1. Nhóm tiêu chí chung (TC / 30đ)").Address.RowNumber;
        Assert.Equal(28.5, ws.Cell(general, 2).GetDouble(), 3);
        Assert.Equal(28, ws.Cell(general, 3).GetDouble());
        Assert.Equal(-0.5, ws.Cell(general, 4).GetDouble(), 3);
        var total = Find(ws, "TỔNG ĐIỂM (100đ)").Address.RowNumber;
        Assert.Equal(93.9, ws.Cell(total, 2).GetDouble(), 3);
        Assert.Equal(92, ws.Cell(total, 3).GetDouble());
        Assert.Contains("Kết luận của Cơ quan thẩm định: Nhất trí", Texts(ws));
        Assert.Contains(Texts(ws), t => t.Contains("☒ Hoàn thành tốt", StringComparison.Ordinal));
        var levels = Find(ws, "3. Đánh giá 3 Mức chất lượng nhiệm vụ").Address.RowNumber;
        Assert.Equal("", ws.Cell(levels, 2).GetString());
        Assert.Equal(Mau10Data.From(record).TaskLevels ?? "Mức 1: … SP\nMức 2: … SP\nMức 3: … SP", ws.Cell(levels, 3).GetString());
    }

    [Fact]
    public void SetNumber_ReadsFormTextNumbers_AndKeepsOtherTextAsIs()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("x");
        ExcelFormLayout.SetNumber(ws.Cell(1, 1), "26,5");
        ExcelFormLayout.SetNumber(ws.Cell(1, 2), "90%");
        ExcelFormLayout.SetNumber(ws.Cell(1, 3), "K/AD");
        ExcelFormLayout.SetNumber(ws.Cell(1, 4), "1.1");
        ExcelFormLayout.SetNumber(ws.Cell(1, 5), null);

        Assert.Equal(26.5, ws.Cell(1, 1).GetDouble());
        Assert.Equal("0.0", ws.Cell(1, 1).Style.NumberFormat.Format);
        Assert.Equal(0.9, ws.Cell(1, 2).GetDouble(), 3);
        Assert.Equal("K/AD", ws.Cell(1, 3).GetString());
        Assert.Equal("1.1", ws.Cell(1, 4).GetString());
        Assert.True(ws.Cell(1, 5).IsEmpty());
    }

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    private static List<string> Texts(IXLWorksheet ws) =>
        ws.CellsUsed().Select(c => c.GetString()).ToList();

    private static IXLCell Find(IXLWorksheet ws, string text) =>
        ws.CellsUsed().First(c => c.GetString() == text);

    private static IndividualFormSource Source()
    {
        var criteria = CriteriaSetDefaults.Build();
        var period = new EvaluationPeriod { Year = 2026, Quarter = EvaluationQuarter.Quy3, Name = "Quý III/2026" };
        var cell = new PartyCell { Name = "Chi bộ Kỹ thuật" };
        var dept = new AdministrativeDepartment { Name = "Phòng Kỹ thuật", ParentId = Guid.NewGuid() };
        var member = new PartyMemberProfile
        {
            FullName = "Nguyễn Văn A", DateOfBirth = new DateOnly(1973, 10, 21), PositionTitle = "Trưởng phòng", PartyCell = cell, Department = dept
        };
        var general = criteria.AllItems.ToDictionary(
            x => x.Item.Code,
            x => x.Item.Code == "2.1" ? new GeneralItemScore { Score = 0, Reason = "Căn cứ giảm điểm" } : new GeneralItemScore { Score = x.Item.MaxScore });
        var record = new EvaluationRecord
        {
            Period = period, Member = member, PartyCell = cell, Department = dept,
            GeneralScores = EvaluationScoring.GeneralScoresToJson(criteria, general),
            GeneralCriteriaScore = 29, TasksScore = 64, TotalSelfScore = 93,
            SelfProposedGrade = EvaluationGrade.HoanThanhTot,
            SelfScoredAt = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            AxisReports = RecordFormContent.AxisReportsToJson(criteria, new Dictionary<string, AxisReport?>
            {
                ["T1"] = new() { Tasks = { new() { Content = "Việc T1" } }, Deadline = "30/9/2026", Status = "Đã xong", Product = "Báo cáo", Progress = "Đúng hạn" }
            })
        };
        record.Tasks.Add(new EvaluationTask { TaskOrder = 1, TaskName = "Sản phẩm 1", Weight = 40, SelfScore = 38, IsExceedStandard = true });
        record.Tasks.Add(new EvaluationTask { TaskOrder = 2, TaskName = "Sản phẩm 2", Weight = 30, SelfScore = 26 });
        var positions = MemberPositionNames.From(new[]
        {
            (PositionSide.Party, "Bí thư Chi bộ"),
            (PositionSide.Administrative, "Trưởng phòng")
        });
        return new IndividualFormSource(record, criteria, positions);
    }
}
