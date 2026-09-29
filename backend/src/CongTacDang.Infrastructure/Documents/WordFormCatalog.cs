using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Infrastructure.Documents.Forms;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>Một biểu mẫu Word: mã, tên, file mẫu gốc và lớp dữ liệu (nguồn danh mục tag).</summary>
/// <param name="Code">Mã biểu mẫu (ví dụ <c>MAU_02</c>) — khóa của phiên bản file mẫu trong CSDL.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="TemplateFileName">Tên file mẫu gốc đi kèm ứng dụng (<c>Templates/Word</c>).</param>
/// <param name="DataType">Lớp dữ liệu mẫu (<c>Documents/Forms/MauXXData</c>).</param>
public sealed record WordFormDefinition(string Code, string Name, string TemplateFileName, Type DataType)
{
    /// <summary>Tag riêng của biểu mẫu (khai báo trên lớp dữ liệu) — bắt buộc có trong file mẫu.</summary>
    public IReadOnlyList<TemplateTagInfo> FormTags { get; } = TemplateTagCatalog.Describe(DataType);
}

/// <summary>
/// Danh mục biểu mẫu Word quản lý được trên giao diện (task 17 — T-81). Thêm biểu mẫu mới: thêm một dòng ở đây
/// (xem <c>docs/bieu-mau.md</c>).
/// </summary>
public static class WordFormCatalog
{
    /// <summary>Mọi biểu mẫu Word.</summary>
    public static IReadOnlyList<WordFormDefinition> All { get; } = new[]
    {
        new WordFormDefinition("MAU_01", "Mẫu 01 — Phiếu giao / đăng ký sản phẩm, công việc chuyên môn hằng quý", Mau01Data.TemplateFileName, typeof(Mau01Data)),
        new WordFormDefinition("MAU_02", "Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc", Mau02Data.TemplateFileName, typeof(Mau02Data)),
        new WordFormDefinition("MAU_09A", "Mẫu 09A — Phiếu tự chấm điểm đánh giá, xếp loại (theo sản phẩm Mẫu 01/02)", Mau09AData.TemplateFileName, typeof(Mau09AData)),
        new WordFormDefinition("MAU_09B", "Mẫu 09B — Phiếu tự chấm điểm đánh giá, xếp loại (theo trục, Quý III/2026)", Mau09BData.TemplateFileName, typeof(Mau09BData)),
        new WordFormDefinition("MAU_09C", "Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân", Mau09CData.TemplateFileName, typeof(Mau09CData)),
        new WordFormDefinition("MAU_9D", "Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý", Mau9DData.TemplateFileName, typeof(Mau9DData)),
        new WordFormDefinition("MAU_10", "Mẫu 10 — Phiếu thẩm định, nhận xét, đề xuất xếp loại", Mau10Data.TemplateFileName, typeof(Mau10Data)),
        new WordFormDefinition("MAU_11", "Mẫu 11 — Phiếu đánh giá, xếp loại cán bộ (bỏ phiếu)", Mau11Data.TemplateFileName, typeof(Mau11Data)),
        new WordFormDefinition("MAU_13", "Mẫu 13 — Biên bản kiểm phiếu", Mau13Data.TemplateFileName, typeof(Mau13Data)),
        new WordFormDefinition("MAU_07", "Mẫu 07 — Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ)", Mau07Data.TemplateFileName, typeof(Mau07Data)),
        new WordFormDefinition("MAU_08", "Mẫu 08 — Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị", Mau08Data.TemplateFileName, typeof(Mau08Data)),
        new WordFormDefinition("MAU_12", "Mẫu 12 — Biên bản hội nghị đánh giá, xếp loại chất lượng cán bộ quý", Mau12Data.TemplateFileName, typeof(Mau12Data)),
        new WordFormDefinition("MAU_16", "Mẫu 16 — Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý", Mau16Data.TemplateFileName, typeof(Mau16Data)),
        new WordFormDefinition("MAU_17", "Mẫu 17 — Kế hoạch hỗ trợ, khắc phục và phát triển 30-60-90 ngày", Mau17Data.TemplateFileName, typeof(Mau17Data))
    };

    /// <summary>Tìm theo mã (không phân biệt hoa thường); null nếu không có.</summary>
    public static WordFormDefinition? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(f => string.Equals(f.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Tìm theo tên file mẫu gốc; null nếu không có.</summary>
    public static WordFormDefinition? FindByFileName(string templateFileName) =>
        All.FirstOrDefault(f => string.Equals(f.TemplateFileName, templateFileName, StringComparison.OrdinalIgnoreCase));
}
