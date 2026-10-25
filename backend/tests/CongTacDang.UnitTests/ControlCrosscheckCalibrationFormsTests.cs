using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Mẫu 03, 19, 20: tag khớp lớp dữ liệu, tài liệu hợp lệ, dữ liệu điền đúng chỗ.</summary>
public class ControlCrosscheckCalibrationFormsTests
{
    private static readonly FileWordTemplateStore Bundled = new();

    private static ExpertCrosscheckDto Crosscheck() => new()
    {
        MemberFullName = "Nguyễn Văn A",
        ExpertUnitName = "Ban An toàn Chất lượng",
        AddresseeName = "Ban Tổ chức cán bộ - Lao động",
        Fields = new List<string> { CrosscheckFields.SafetyQuality, CrosscheckFields.Finance },
        RequestedAt = new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc),
        IssuedDate = new DateOnly(2026, 9, 12),
        SubmittedByName = "Trần Thị B",
        ConfirmedByName = "Lê Văn C",
        Items =
        {
            new ExpertCrosscheckItemDto
            {
                Order = 1, ItemName = "Tỷ lệ sự cố kỹ thuật", SelfData = "0 sự cố", SourceData = "1 sự cố nhỏ",
                Result = nameof(CrosscheckResult.Defect), AdjustmentType = nameof(CrosscheckAdjustment.Decrease),
                AdjustmentPoints = 1.5, AdjustmentText = "−1,5đ",
                BasisText = "Sự cố ghi nhận ngày 3/8", BasisDocNumber = "12/BC-ATCL", BasisDocDate = new DateOnly(2026, 8, 5), BasisAgency = "Ban An toàn Chất lượng"
            },
            new ExpertCrosscheckItemDto
            {
                Order = 2, ItemName = "Tiến độ dự án", SelfData = "Đúng hạn", SourceData = "Đúng hạn",
                Result = nameof(CrosscheckResult.Exceed), AdjustmentType = nameof(CrosscheckAdjustment.RecognizeExceed),
                AdjustmentText = "+ ghi nhận vượt chuẩn"
            },
            new ExpertCrosscheckItemDto { Order = 3, ItemName = "Chỉ tiêu tài chính", SelfData = "K/AD", NotApplicable = true }
        }
    };

    private static ControlCheckDto ControlCheck(string? conclusion = nameof(ControlConclusion.NeedsWork))
    {
        var texts = new[]
        {
            "Nhiệm vụ, chỉ tiêu, trọng số được giao/xác nhận đúng thẩm quyền; thay đổi trong kỳ có căn cứ và không hồi tố bất lợi.",
            "Tiêu chí áp dụng phù hợp chức danh.", "Mỗi điểm số có minh chứng.", "Đã loại trừ việc tính trùng kết quả.",
            "Đã xem xét điều kiện nguồn lực.", "Không có xung đột lợi ích.", "Vị trí an toàn trọng yếu.",
            "Chênh lệch bất thường đã được giải trình.", "Cá nhân đã được phản hồi.", "Kết quả cuối cùng đã được phê duyệt."
        };
        var check = new ControlCheckDto
        {
            GroupName = "Nhóm kỹ thuật CNS",
            PerformedByName = "Phạm Văn D",
            PerformedAt = new DateTime(2026, 9, 20, 2, 0, 0, DateTimeKind.Utc),
            ConfirmedByName = "Hoàng Thị E",
            ConfirmedAt = new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc),
            Conclusion = conclusion,
            NeedsWorkContent = "Bổ sung minh chứng mục 3"
        };
        for (var i = 0; i < texts.Length; i++)
        {
            check.Items.Add(new ControlCheckItemDto
            {
                ItemCode = $"K{i + 1:00}",
                ItemText = texts[i],
                Result = i switch { 2 => nameof(ControlItemResult.Fail), 3 => nameof(ControlItemResult.NotApplicable), 9 => null, _ => nameof(ControlItemResult.Pass) },
                Evidence = i == 2 ? "Thiếu biên bản nghiệm thu" : null
            });
        }
        return check;
    }

    private static CalibrationSessionDto Calibration() => new()
    {
        GroupName = "Trưởng phòng kỹ thuật",
        HeldAt = new DateTime(2026, 9, 25, 1, 30, 0, DateTimeKind.Utc),
        Location = "Phòng họp số 2",
        ChairName = "Vũ Văn F",
        SecretaryName = "Đỗ Thị G",
        ScopeSuggestion = "02 hồ sơ của nhóm Trưởng phòng kỹ thuật",
        ExtraConclusion = "Giao thư ký lưu biên bản.",
        Attendees =
        {
            new CalibrationAttendeeDto { Order = 1, FullName = "Vũ Văn F", Capacity = "Chủ trì" },
            new CalibrationAttendeeDto { Order = 2, FullName = "Ngô Văn H", Capacity = "Thành viên", DissentNote = "Đề nghị giữ mức Hoàn thành tốt" }
        },
        Summary =
        {
            new CalibrationSummaryRowDto { GradeCode = "HoanThanhXuatSac", GradeName = "Hoàn thành xuất sắc nhiệm vụ", CountBefore = 1, PercentBefore = 50, CountAfter = 0, PercentAfter = 0, NotableDifference = "Chênh 3 điểm" },
            new CalibrationSummaryRowDto { GradeCode = "HoanThanhTot", GradeName = "Hoàn thành tốt nhiệm vụ", CountBefore = 1, PercentBefore = 50, CountAfter = 2, PercentAfter = 100, PreliminaryConclusion = "Giữ" },
            new CalibrationSummaryRowDto { GradeCode = "TOTAL", GradeName = "Tổng", CountBefore = 2, PercentBefore = 100, CountAfter = 2, PercentAfter = 100 }
        },
        Cases =
        {
            new CalibrationCaseDto
            {
                Order = 1, FullName = "Nguyễn Văn A", GradeBefore = "HoanThanhXuatSac", ScoreBefore = 92.5, Clarification = "Có sự cố nhỏ chưa tính",
                GradeAfter = "HoanThanhTot", ScoreAfter = 89, Basis = "Báo cáo 12/BC-ATCL", ResponsibleName = "Vũ Văn F", Changed = true
            },
            new CalibrationCaseDto { Order = 2, FullName = "Trần Văn I", GradeBefore = "HoanThanhTot", ScoreBefore = 85, Clarification = "Đủ minh chứng", Changed = false }
        }
    };

    private static byte[] Render(string templateFileName, object formData)
    {
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ A", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội");
        var data = TemplateDataBinder.Bind(formData).WithShared(TemplateDataBinder.Bind(org));
        var result = DocxTemplateEngine.Render(Bundled.Load(templateFileName), data);
        Assert.Empty(result.MissingTags);
        return result.Content;
    }

    private static string BodyText(byte[] content)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        return doc.MainDocumentPart!.Document!.Body!.InnerText;
    }

    public static IEnumerable<object[]> Forms()
    {
        yield return new object[] { Mau03Data.TemplateFileName, Mau03Data.From(Crosscheck(), "Trưởng phòng Kỹ thuật — Phòng Kỹ thuật") };
        yield return new object[] { Mau19Data.TemplateFileName, Mau19Data.From(ControlCheck(), "Phòng Kỹ thuật", new DateTime(2026, 7, 1), new DateTime(2026, 9, 30)) };
        yield return new object[] { Mau20Data.TemplateFileName, Mau20Data.From(Calibration()) };
        // Phiếu trống: danh sách rỗng, chưa có người ký.
        yield return new object[] { Mau03Data.TemplateFileName, Mau03Data.From(new ExpertCrosscheckDto(), null) };
        yield return new object[] { Mau19Data.TemplateFileName, Mau19Data.From(new ControlCheckDto(), null, null, null) };
        yield return new object[] { Mau20Data.TemplateFileName, Mau20Data.From(new CalibrationSessionDto()) };
    }

    [Theory]
    [InlineData("MAU_03")]
    [InlineData("MAU_19")]
    [InlineData("MAU_20")]
    public void Template_TagsMatchDataClass(string code)
    {
        var form = WordFormCatalog.Find(code)!;
        var templateTags = DocxTemplateEngine.GetTags(Bundled.Load(form.TemplateFileName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalog = form.FormTags;
        var common = OrganizationTemplateFields.Tags.Select(t => t.Tag);

        Assert.Empty(templateTags.Except(catalog.Select(t => t.Tag).Concat(common), StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
        Assert.Empty(catalog.Where(t => t.Kind is TemplateTagCatalog.FieldKind or TemplateTagCatalog.RepeatKind)
            .Select(t => t.Tag).Where(t => !templateTags.Contains(t)).OrderBy(t => t));
        foreach (var condition in catalog.Where(t => t.Kind == TemplateTagCatalog.IfKind).Select(t => t.ConditionName!))
            Assert.True(templateTags.Contains("if:" + condition) || templateTags.Contains("ifnot:" + condition), condition);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Template_RendersValidDocument_WithoutPlaceholders(string templateFileName, object formData)
    {
        var content = Render(templateFileName, formData);

        Assert.Empty(DocumentTemplateTests.Validate(content));
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain("{{", body.InnerText);
        Assert.DoesNotContain("«", body.InnerText);
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
    }

    [Fact]
    public void Templates_DoNotHardCodeOrganizationNames()
    {
        foreach (var file in new[] { Mau03Data.TemplateFileName, Mau19Data.TemplateFileName, Mau20Data.TemplateFileName })
        {
            using var doc = WordprocessingDocument.Open(new MemoryStream(Bundled.Load(file)), false);
            var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.DoesNotContain("BỘ XÂY DỰNG", text);
            Assert.DoesNotContain("QUẢN LÝ BAY", text);
            Assert.DoesNotContain("Hà Nội", text);
        }
    }

    [Fact]
    public void Mau03_FillsHeaderPersonFieldsAndRows()
    {
        var text = BodyText(Render(Mau03Data.TemplateFileName, Mau03Data.From(Crosscheck(), "Trưởng phòng Kỹ thuật — Phòng Kỹ thuật")));

        Assert.Contains("TỔNG CÔNG TY B", text);
        Assert.Contains("BAN AN TOÀN CHẤT LƯỢNG", text);
        Assert.Contains("Hà Nội, ngày 12 tháng 09 năm 2026", text);
        Assert.Contains("Kính gửi: Ban Tổ chức cán bộ - Lao động", text);
        Assert.Contains("Cơ quan/Đơn vị đối soát: Ban An toàn Chất lượng", text);
        Assert.Contains("Họ tên cán bộ được đối soát: Nguyễn Văn A", text);
        Assert.Contains("Chức vụ/Đơn vị: Trưởng phòng Kỹ thuật — Phòng Kỹ thuật", text);
        Assert.Contains("☒ An toàn - Chất lượng", text);
        Assert.Contains("☐ Kỹ thuật CNS", text);
        Assert.Contains("☒ Tài chính", text);
        Assert.Contains("☐ Pháp chế / Thanh tra / Kiểm toán", text);
        Assert.Contains("Tỷ lệ sự cố kỹ thuật", text);
        Assert.Contains("1 sự cố nhỏ", text);
        Assert.Contains("☐ Đạt chuẩn", text);
        Assert.Contains("☒ Có khiếm khuyết", text);
        Assert.Contains("☒ Vượt chuẩn", text);
        Assert.Contains("−1,5đ", text);
        Assert.Contains("+ ghi nhận vượt chuẩn", text);
        Assert.Contains("Sự cố ghi nhận ngày 3/8", text);
        Assert.Contains("số 12/BC-ATCL ngày 05/08/2026 của Ban An toàn Chất lượng", text);
        Assert.Contains("K/AD", text);
        Assert.Contains("Lê Văn C", text);
        Assert.Contains("Trần Thị B", text);
        // Chữ in sẵn giữ nguyên, dòng mẫu thừa và chữ giữ chỗ không còn.
        Assert.Contains("PHIẾU Ý KIẾN CHUYÊN MÔN ĐỘC LẬP / ĐỐI SOÁT DỮ LIỆU CHUYÊN NGÀNH", text);
        Assert.Contains("Lưu ý: Mọi điểm số/mức nhận xét phải có minh chứng", text);
        Assert.DoesNotContain("[Chỉ tiêu kỹ thuật", text);
        Assert.DoesNotContain("[Dữ liệu", text);
        Assert.DoesNotContain("[0 / -", text);
    }

    [Fact]
    public void Mau03_NotApplicableRow_ShowsOnlyKAD()
    {
        var crosscheck = new ExpertCrosscheckDto
        {
            Items = { new ExpertCrosscheckItemDto { Order = 1, ItemName = "Chỉ tiêu A", NotApplicable = true, AdjustmentText = "0" } }
        };

        var text = BodyText(Render(Mau03Data.TemplateFileName, Mau03Data.From(crosscheck, null)));

        Assert.Contains("K/AD", text);
        Assert.DoesNotContain("Đạt chuẩn☐", text.Replace(" ", string.Empty).Replace("Vượt", string.Empty));
        Assert.DoesNotContain("Vượt chuẩn", text.Replace("tiêu chí vượt chuẩn", string.Empty));
    }

    [Fact]
    public void Mau19_PrintsTenItemsWithMarks_AndConclusion()
    {
        var text = BodyText(Render(Mau19Data.TemplateFileName,
            Mau19Data.From(ControlCheck(), "Phòng Kỹ thuật", new DateTime(2026, 7, 1), new DateTime(2026, 9, 30))));

        Assert.Contains("ĐẢNG BỘ CẤP TRÊN", text);
        Assert.Contains("ĐẢNG BỘ A", text);
        Assert.Contains("Hà Nội, ngày 21 tháng 09 năm 2026", text);
        Assert.Contains("Cơ quan/đơn vị:Phòng Kỹ thuật", text);
        Assert.Contains("Từ ngày 01/07/2026 đến ngày 30/09/2026", text);
        Assert.Contains("Nhóm kỹ thuật CNS", text);
        Assert.Contains("Phạm Văn D", text);
        Assert.Contains("Nhiệm vụ, chỉ tiêu, trọng số được giao/xác nhận đúng thẩm quyền; thay đổi trong kỳ có căn cứ và không hồi tố bất lợi.", text);
        Assert.Contains("Kết quả cuối cùng đã được phê duyệt.", text);
        Assert.Contains("Thiếu biên bản nghiệm thu", text);
        Assert.Contains("☒ K/AD", text);
        // 7 mục đạt, 1 mục chưa đạt, 1 mục K/AD và 1 ô kết luận; mục 10 chưa chọn nên không có dấu.
        Assert.Equal(10, CountOf(text, "☒"));
        Assert.Contains("☐ Đủ điều kiện trình phê duyệt", text);
        Assert.Contains("☒ Cần hoàn thiện trước khi trình", text);
        Assert.Contains("Bổ sung minh chứng mục 3", text);
        Assert.Contains("Ngày 20/09/2026", text);
        Assert.Contains("Ngày 21/09/2026", text);
        Assert.Contains("Hoàng Thị E", text);
    }

    [Fact]
    public void Mau19_EligibleConclusion_TicksFirstBox()
    {
        var text = BodyText(Render(Mau19Data.TemplateFileName,
            Mau19Data.From(ControlCheck(nameof(ControlConclusion.Eligible)), null, null, null)));

        Assert.Contains("☒ Đủ điều kiện trình phê duyệt", text);
        Assert.Contains("☐ Cần hoàn thiện trước khi trình", text);
    }

    [Fact]
    public void Mau20_FillsSummaryCasesAndMembers()
    {
        var text = BodyText(Render(Mau20Data.TemplateFileName, Mau20Data.From(Calibration())));

        Assert.Contains("ĐẢNG BỘ CẤP TRÊN", text);
        Assert.Contains("Hà Nội, ngày 25 tháng 09 năm 2026", text);
        Assert.Contains("08h30, ngày 25/09/2026, tại Phòng họp số 2", text);
        Assert.Contains("Chủ trì:Vũ Văn F", text);
        Assert.Contains("Vũ Văn F (Chủ trì); Ngô Văn H (Thành viên)", text);
        Assert.Contains("02 hồ sơ của nhóm Trưởng phòng kỹ thuật", text);
        Assert.Contains("Hoàn thành xuất sắc nhiệm vụ", text);
        Assert.Contains("50,0", text);
        Assert.Contains("Chênh 3 điểm", text);
        Assert.Contains("Giữ", text);
        Assert.Contains("Tổng", text);
        Assert.Contains("100,0", text);
        Assert.Contains("Nguyễn Văn A", text);
        Assert.Contains("Hoàn thành xuất sắc nhiệm vụ (92,5 điểm)", text);
        Assert.Contains("Hoàn thành tốt nhiệm vụ (89 điểm)", text);
        Assert.Contains("Báo cáo 12/BC-ATCL", text);
        Assert.Contains("Người chịu trách nhiệm: Vũ Văn F", text);
        Assert.Contains("Giữ nguyên", text);
        Assert.Contains("Giao thư ký lưu biên bản.", text);
        Assert.Contains("Ngô Văn H (Thành viên), ý kiến bảo lưu: Đề nghị giữ mức Hoàn thành tốt", text);
        Assert.Contains("Vũ Văn F (Chủ trì), thống nhất", text);
        Assert.Contains("Đỗ Thị G", text);
        // Chữ cố định của biên bản giữ nguyên.
        Assert.Contains("Việc hiệu chuẩn chỉ nhằm bảo đảm cùng tiêu chuẩn", text);
    }

    [Fact]
    public void Mau20_EmptySession_KeepsDefaultDotsAndNoExtraConclusion()
    {
        var text = BodyText(Render(Mau20Data.TemplateFileName, Mau20Data.From(new CalibrationSessionDto())));

        Assert.Contains("....", text);
        Assert.DoesNotContain("thống nhất, ", text);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
