using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Nội dung biểu mẫu cá nhân của bộ mặc định, chép nguyên văn từ file biểu mẫu gốc trong <c>docs/</c>.</summary>
public static partial class CriteriaSetDefaults
{
    public static IReadOnlyList<string> RequiredForms { get; } = new[]
    {
        RecordFormCodes.Form01, RecordFormCodes.Form02, RecordFormCodes.Form09A, RecordFormCodes.Form09C, RecordFormCodes.Form9D, RecordFormCodes.Form10
    };

    /// <summary>Ba mức kết luận chất lượng nhiệm vụ trọng tâm.</summary>
    public static List<TaskLevel> TaskLevels() => new()
    {
        new TaskLevel { Code = 1, Label = "Chủ động tiếp cận, giải quyết hiệu quả vấn đề, có kết quả cụ thể" },
        new TaskLevel { Code = 2, Label = "Cơ bản đáp ứng yêu cầu nhưng còn hạn chế, cần tiếp tục hoàn thiện" },
        new TaskLevel { Code = 3, Label = "Không đáp ứng yêu cầu nhiệm vụ" }
    };

    public const string TaskLineLabel = "- Nhiệm vụ {n}:";

    public const string TaskLevelPhrase = "Tự đánh giá mức {level}.";

    /// <summary>Mẫu gốc có một mục; phần II lấy từ điểm tự chấm đã lưu.</summary>
    public static List<SelfAssessmentSection> SelfAssessmentSections() => new()
    {
        new SelfAssessmentSection
        {
            Code = "I",
            Title = "I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao",
            Guidance = "Trên cơ sở nhiệm vụ được giao, cá nhân tự đánh giá về kết quả thực hiện nhiệm vụ theo quý như sau:",
            Note = "(Lưu ý: Viết tóm tắt theo kết quả 6 trục. Đối với nhiệm vụ trọng tâm, then chốt, nhất là nhiệm vụ an toàn, an ninh, chất lượng, khai thác, tài chính, đầu tư, dự án và pháp chế, phải kết luận rõ chất lượng sản phẩm, hiệu quả thực hiện nhiệm vụ theo 01 trong 03 mức: Mức 1-Chủ động tiếp cận, giải quyết hiệu quả vấn đề, có kết quả cụ thể; Mức 2-Cơ bản đáp ứng yêu cầu nhưng còn hạn chế, cần tiếp tục hoàn thiện; Mức 3-Không đáp ứng yêu cầu nhiệm vụ)",
            // "Không quá 02 trang A4" ≈ 6.000 ký tự.
            MaxLength = 6000,
            // Văn bản không yêu cầu chặn khi để trống; bật trong bộ tiêu chí khi cần.
            Required = false
        }
    };

    /// <summary>Nguyên văn biểu mẫu gốc, theo mã tiêu chí con.</summary>
    private static readonly IReadOnlyDictionary<string, string> ItemFormTexts = new Dictionary<string, string>
    {
        ["1.1"] = "Tuyệt đối trung thành với Đảng, với Tổ quốc và Nhân dân; kiên định chủ nghĩa Mác - Lênin, tư tưởng Hồ Chí Minh, mục tiêu độc lập dân tộc, chủ nghĩa xã hội. Có lập trường, quan điểm, bản lĩnh chính trị vững vàng; kiên quyết bảo vệ nền tảng tư tưởng, Cương lĩnh, đường lối của Đảng, Hiến pháp và pháp luật của Nhà nước; đấu tranh phản bác các quan điểm sai trái, thù địch, các biểu hiện suy thoái, “tự diễn biến”, “tự chuyển hóa”.",
        ["1.2"] = "Có tinh thần yêu nước sâu sắc, tận tuỵ phục vụ Nhân dân, sâu sát cơ sở, luôn hành động vì lợi ích của Nhân dân. Đặt lợi ích của Đảng, quốc gia - dân tộc, Nhân dân, tập thể lên trên lợi ích cá nhân, sẵn sàng hy sinh vì sự nghiệp cách mạng của Đảng, vì độc lập, tự do của Tổ quốc, vì hạnh phúc của Nhân dân.",
        ["1.3"] = "Chấp hành nghiêm chủ trương, đường lối, nghị quyết, chỉ thị, quy định, nguyên tắc tổ chức, kỷ luật của Đảng, nhất là nguyên tắc tập trung dân chủ, tự phê bình và phê bình; chấp hành nghiêm pháp luật của Nhà nước và quy định của cơ quan, đơn vị. Tuyệt đối chấp hành sự phân công của tổ chức, yên tâm công tác và hoàn thành tốt mọi nhiệm vụ được giao.",
        ["1.4"] = "Có tinh thần tự giác, trách nhiệm cao trong nghiên cứu, học tập chủ nghĩa Mác - Lênin, tư tưởng Hồ Chí Minh, các nghị quyết, chỉ thị của Đảng và các chương trình bồi dưỡng, cập nhật kiến thức mới nhằm nâng cao trình độ về mọi mặt, đáp ứng yêu cầu, nhiệm vụ.",
        ["1.5"] = "Có phẩm chất đạo đức, lối sống trong sáng, trung thực, khiêm tốn, chân thành, giản dị; cần, kiệm, liêm, chính, chí công vô tư; chấp hành nghiêm quy định về chuẩn mực đạo đức cách mạng của cán bộ, đảng viên trong giai đoạn mới, trách nhiệm nêu gương; không vi phạm Quy định về những điều đảng viên không được làm. Không né tránh công việc, chạy theo thành tích; không vi phạm đạo đức, lối sống đến mức bị xử lý kỷ luật.",
        ["1.6"] = "Không tham vọng quyền lực; không chạy chức, chạy quyền; không tham nhũng, lãng phí, cơ hội, vụ lợi, cục bộ, lợi ích nhóm; không để gia đình, người thân và người khác lợi dụng chức vụ, vị trí công tác để trục lợi. Không có biểu hiện suy thoái về tư tưởng chính trị, đạo đức, lối sống, những biểu hiện \"tự diễn biến\", \"tự chuyển hoá\" trong nội bộ. Kiên quyết đấu tranh chống quan liêu, cửa quyền, tham nhũng, xa hoa, lãng phí, tiêu cực, chủ nghĩa cá nhân, lối sống cơ hội, thực dụng, bè phái, lợi ích nhóm, nói không đi đôi với làm.",
        ["1.7"] = "Có uy tín cao, tiêu biểu về phẩm chất đạo đức và phong cách công tác; là trung tâm đoàn kết, thương yêu đồng chí, đồng nghiệp.",
        ["1.8"] = "Có tinh thần chủ động, đổi mới sáng tạo; phấn đấu vì mục tiêu phát triển của cơ quan, đơn vị, đóng góp vào mục tiêu chung của đất nước.",
        ["1.9"] = "Thực hiện việc kê khai và công khai tài sản, thu nhập theo quy định. Báo cáo đầy đủ, trung thực, cung cấp thông tin chính xác, khách quan về những nội dung liên quan đến việc thực hiện chức trách, nhiệm vụ được giao và hoạt động của cơ quan, tổ chức, đơn vị với cấp trên khi được yêu cầu.",
        ["2.1"] = "Có tư duy đổi mới, tầm nhìn chiến lược, khả năng lãnh đạo, chỉ đạo thích ứng với sự phát triển của thời đại và xu thế toàn cầu hoá; phương pháp làm việc khoa học, nhạy bén chính trị; có năng lực cụ thể hoá trong lãnh đạo, chỉ đạo cơ quan, đơn vị thực hiện và hoàn thành tốt chức năng, nhiệm vụ được giao.",
        ["2.2"] = "Luôn bám sát thực tiễn, có nhiều cách làm hay, sáng tạo, đạt hiệu quả cao trong lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ; xây dựng cấp uỷ, tổ chức đảng trong sạch, vững mạnh, cơ quan, đơn vị vững mạnh toàn diện.",
        ["2.3"] = "Nói đi đôi với làm, dám nghĩ, dám làm, dám chịu trách nhiệm, dám đột phá vì lợi ích chung. Có khả năng phân tích, dự báo tình hình, phát hiện những khó khăn, bất cập, thời cơ, thuận lợi trong thực tiễn; đề xuất hoặc quyết định những giải pháp phù hợp, kịp thời, hiệu quả.",
        ["2.4"] = "Có khát vọng phấn đấu, cống hiến; có khả năng quy tụ và phát huy được sức mạnh của tập thể, cá nhân trong cơ quan, đơn vị và các cơ quan, đơn vị có liên quan.",
        ["3.1"] = "Chủ động, nghiêm túc thực hiện tự phê bình và phê bình, có tinh thần cầu thị và tiếp thu phản biện, góp ý.",
        ["3.2"] = "Có kế hoạch rõ ràng và quyết liệt trong khắc phục hạn chế, khuyết điểm đã được chỉ ra.",
        ["3.3"] = "Kết quả khắc phục hoàn thành từ ≥ 80% nội dung, có tiến bộ rõ, được tổ chức đánh giá tốt; không để tái diễn tồn tại.",
        ["3.4"] = "Tự soi, tự sửa trên tinh thần trách nhiệm chính trị cao, không né tránh, không đổ lỗi.",
    };

    private static CriteriaSetContent WithItemFormTexts(CriteriaSetContent content)
    {
        foreach (var item in content.GeneralGroups.SelectMany(g => g.Items))
        {
            if (ItemFormTexts.TryGetValue(item.Code, out var text))
                item.FormText = text;
        }
        return content;
    }
}
