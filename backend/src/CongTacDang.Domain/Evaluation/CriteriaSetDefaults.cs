using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Hai bộ tiêu chí mặc định lấy theo <b>bản trích xuất HD 03-HD/TVĐU (docs/nghiep-vu/hd03-trich-xuat.md, CHƯA XÁC NHẬN)</b> —
/// mục 3.2 (17 tiêu chí con, 3 nhóm 18/4/8), 3.3 (6 trục, điểm tối đa 09B), 3.6 (khung tỷ trọng), 3.7 (thang quy đổi), 3.10
/// (làm tròn), 4.1 (mức xếp loại), 4.3 (trần xuất sắc). Chờ Phòng TCCB-LĐ / Ban Tổ chức Đảng ủy xác nhận; sửa được qua giao
/// diện bằng cách nhân bản thành bản nháp. Lựa chọn khi văn bản mâu thuẫn ghi trong báo cáo task 16.
/// </summary>
public static class CriteriaSetDefaults
{
    /// <summary>Mã bộ mặc định Mẫu 09B (Quý III/2026).</summary>
    public const string Code09B = "HD03-09B-Q3-2026";

    /// <summary>Mã bộ mặc định Mẫu 09A (từ 2027).</summary>
    public const string Code09A = "HD03-09A-2027";

    /// <summary>Tên bộ mặc định Mẫu 09B.</summary>
    public const string Name09B = "Mẫu 09B — Quý III/2026";

    /// <summary>Tên bộ mặc định Mẫu 09A.</summary>
    public const string Name09A = "Mẫu 09A — từ 2027";

    /// <summary>Ghi chú chung của hai bộ mặc định.</summary>
    public const string Notes =
        "Khởi tạo theo bản trích xuất HD 03-HD/TVĐU (docs/nghiep-vu/hd03-trich-xuat.md) — CHỜ NGHIỆP VỤ XÁC NHẬN. "
        + "Chỗ văn bản mâu thuẫn: làm tròn 1 chữ số, nửa lên (theo ví dụ PL II, không dùng bước 0,5); tiêu chí chung chấm "
        + "\"Đảm bảo/Không đảm bảo\" (theo Mẫu 09A/09B); K/AD bỏ khỏi mẫu số rồi quy đổi (HD03 tr.9 \"xử lý trọng số\", "
        + "\"không mặc nhiên chấm điểm tối đa\"); trần xuất sắc 20% số \"Hoàn thành tốt\" (câu chữ III.6), làm tròn 0,5 lên 1.";

    /// <summary>Nội dung bộ Mẫu 09B (Quý III/2026): 6 trục có điểm tối đa 15/10/10/15/10/10.</summary>
    public static CriteriaSetContent Build09B() => Build(axisMaxScores: new[] { 15.0, 10, 10, 15, 10, 10 });

    /// <summary>Nội dung bộ Mẫu 09A (từ 2027): chấm theo nhiệm vụ Mẫu 01/02, công thức A-B-C-D; trục không có điểm tối đa riêng.</summary>
    public static CriteriaSetContent Build09A() => Build(axisMaxScores: new[] { 0.0, 0, 0, 0, 0, 0 });

    private static CriteriaSetContent Build(double[] axisMaxScores)
    {
        var axes = new List<ResultAxis>
        {
            new() { Code = "T1", Name = "Nhiệm vụ chính trị, sản xuất kinh doanh và cung cấp dịch vụ",
                Description = "Bảo đảm an toàn, điều hòa, hiệu quả hoạt động bay; chất lượng ATS, ATFM, CNS, AIM, MET, SAR; sản xuất kinh doanh, tài chính, đầu tư, dự án và nhiệm vụ chính trị khác." },
            new() { Code = "T2", Name = "Thể chế, phân cấp, kiểm tra, kiểm soát và giám sát",
                Description = "Quy chế, quy định, quy trình; phân cấp, phân quyền; kiểm soát quyền lực; pháp chế, kiểm tra, giám sát, kiểm toán, chất lượng và tuân thủ." },
            new() { Code = "T3", Name = "Khoa học công nghệ, đổi mới sáng tạo và chuyển đổi số",
                Description = "Hiện đại hóa ATM/CNS/AIM/MET; tự động hóa, số hóa dữ liệu, nghiên cứu, sáng kiến, an toàn thông tin, an ninh mạng và cải tiến quy trình." },
            new() { Code = "T4", Name = "Xây dựng Đảng và hệ thống chính trị",
                Description = "Công tác xây dựng Đảng, kiểm tra, giám sát, tổ chức cán bộ, đoàn thể, phòng chống tham nhũng, lãng phí, tiêu cực." },
            new() { Code = "T5", Name = "Văn hóa, con người và an sinh người lao động",
                Description = "Văn hóa an toàn, văn hóa doanh nghiệp, nguồn nhân lực, đào tạo, dân chủ cơ sở, chế độ chính sách, sức khỏe và đời sống người lao động." },
            new() { Code = "T6", Name = "Quốc phòng, an ninh, đối ngoại và hợp tác quốc tế",
                Description = "Hiệp đồng quân sự - dân dụng, an ninh vùng trời/hàng không, phòng chống thiên tai, SAR; hợp tác ICAO, CANSO và đối tác quốc tế." }
        };
        for (var i = 0; i < axes.Count; i++)
            axes[i].MaxScore = axisMaxScores[i];

        return new CriteriaSetContent
        {
            GeneralGroups = new List<CriteriaGroup>
            {
                new()
                {
                    Code = "1", Name = "Về phẩm chất chính trị, đạo đức, lối sống, thực hiện trách nhiệm nêu gương",
                    Items = new List<CriteriaItem>
                    {
                        Item("1.1", "Trung thành với Đảng, Tổ quốc, Nhân dân; kiên định chủ nghĩa Mác-Lênin, tư tưởng Hồ Chí Minh; bản lĩnh chính trị; đấu tranh phản bác quan điểm sai trái, \"tự diễn biến\", \"tự chuyển hóa\"", 2),
                        Item("1.2", "Yêu nước, tận tụy phục vụ Nhân dân; đặt lợi ích Đảng, quốc gia, tập thể lên trên lợi ích cá nhân", 2),
                        Item("1.3", "Chấp hành nghiêm chủ trương, nghị quyết, nguyên tắc tổ chức, kỷ luật Đảng (tập trung dân chủ, tự phê bình và phê bình); pháp luật; chấp hành phân công", 2),
                        Item("1.4", "Tự giác học tập lý luận, nghị quyết, bồi dưỡng cập nhật kiến thức", 2),
                        Item("1.5", "Đạo đức, lối sống trong sáng; cần, kiệm, liêm, chính; nêu gương; không vi phạm Quy định những điều đảng viên không được làm; không vi phạm đạo đức, lối sống đến mức bị xử lý kỷ luật", 2),
                        Item("1.6", "Không tham vọng quyền lực, chạy chức chạy quyền, tham nhũng, lãng phí, lợi ích nhóm; không để người thân trục lợi; đấu tranh chống quan liêu, tiêu cực", 2),
                        Item("1.7", "Có uy tín cao, tiêu biểu về phẩm chất đạo đức và phong cách công tác; là trung tâm đoàn kết, thương yêu đồng chí, đồng nghiệp.", 2),
                        Item("1.8", "Có tinh thần chủ động, đổi mới sáng tạo; phấn đấu vì mục tiêu phát triển của cơ quan, đơn vị, đóng góp vào mục tiêu chung của đất nước.", 2),
                        Item("1.9", "Thực hiện việc kê khai và công khai tài sản, thu nhập theo quy định. Báo cáo đầy đủ, trung thực, cung cấp thông tin chính xác...", 2)
                    }
                },
                new()
                {
                    Code = "2", Name = "Tư duy đổi mới, chiến lược, khát vọng cống hiến, dám nghĩ, dám làm",
                    Items = new List<CriteriaItem>
                    {
                        Item("2.1", "Tư duy đổi mới, tầm nhìn chiến lược, năng lực cụ thể hóa lãnh đạo, chỉ đạo", 1),
                        Item("2.2", "Bám sát thực tiễn, cách làm hay, sáng tạo; xây dựng cấp ủy, tổ chức đảng trong sạch, đơn vị vững mạnh", 1),
                        Item("2.3", "Nói đi đôi với làm, dám nghĩ, dám làm, dám chịu trách nhiệm, dám đột phá vì lợi ích chung...", 1),
                        Item("2.4", "Có khát vọng phấn đấu, cống hiến; có khả năng quy tụ và phát huy được sức mạnh của tập thể...", 1)
                    }
                },
                new()
                {
                    Code = "3", Name = "Về tự phê bình và phê bình, tự soi, tự sửa, khắc phục hạn chế, khuyết điểm",
                    Items = new List<CriteriaItem>
                    {
                        Item("3.1", "Chủ động, nghiêm túc thực hiện tự phê bình và phê bình, có tinh thần cầu thị và tiếp thu phản biện, góp ý.", 2),
                        Item("3.2", "Có kế hoạch rõ ràng và quyết liệt trong khắc phục hạn chế, khuyết điểm đã được chỉ ra.", 2),
                        Item("3.3", "Kết quả khắc phục hoàn thành từ ≥ 80% nội dung, có tiến bộ rõ, được tổ chức đánh giá tốt; không để tái diễn tồn tại.", 2),
                        Item("3.4", "Tự soi, tự sửa trên tinh thần trách nhiệm chính trị cao, không né tránh, không đổ lỗi.", 2)
                    }
                }
            },
            Axes = axes,
            WeightFrames = new List<WeightFrame>
            {
                new() { Code = "K1", Name = "Khung 1 - Quản lý, tham mưu, công tác Đảng, Đoàn thể", A = 0.25, B = 0.35, C = 0.20, D = 0.20 },
                new() { Code = "K2", Name = "Khung 2 - An toàn/tuân thủ/khai thác/kỹ thuật", A = 0.15, B = 0.50, C = 0.15, D = 0.20 },
                new() { Code = "K3", Name = "Khung 3 - Dự án/đầu tư/xây dựng/tài chính dự án", A = 0.20, B = 0.30, C = 0.35, D = 0.15 },
                new() { Code = "K4", Name = "Khung 4 - KHCN/đổi mới/chuyển đổi số/cải tiến", A = 0.15, B = 0.30, C = 0.20, D = 0.35 }
            },
            ConversionScale = new List<ConversionBand>
            {
                new() { MinPercent = 95, Label = "95 - 100%", Description = "Hoàn thành đầy đủ chuẩn giao; đúng hạn hoặc điều chỉnh hợp lệ; chất lượng và hiệu quả đáp ứng đầy đủ; tổ chức thực hiện chủ động, minh chứng rõ ràng." },
                new() { MinPercent = 90, Label = "90% - dưới 95%", Description = "Hoàn thành cơ bản trọn vẹn; chỉ còn khiếm khuyết nhỏ không ảnh hưởng đến mục tiêu chung, an toàn, khả năng sử dụng; được khắc phục kịp thời." },
                new() { MinPercent = 80, Label = "80% - dưới 90%", Description = "Đạt phần lớn yêu cầu; còn nội dung cần hoàn thiện hoặc chậm nhẹ do chủ quan nhưng không làm hỏng kết quả chung." },
                new() { MinPercent = 65, Label = "65% - dưới 80%", Description = "Đáp ứng mức tối thiểu/cơ bản; còn thiếu hụt đáng kể, cần bổ sung hoặc phải được đôn đốc; chưa gây hậu quả nghiêm trọng." },
                new() { MinPercent = 50, Label = "50% - dưới 65%", Description = "Chỉ hoàn thành một phần; chất lượng, tiến độ hoặc tổ chức thực hiện còn yếu; phải chỉnh sửa lớn hoặc tiếp tục xử lý." },
                new() { MinPercent = 0, Label = "0% - dưới 50%", Description = "Không có đầu ra hữu ích, không hoàn thành nhiệm vụ, sai sót nghiêm trọng do lỗi chủ quan hoặc vi phạm kỷ luật lao động/văn hóa an toàn làm nhiệm vụ." }
            },
            Grades = new List<GradeRule>
            {
                new()
                {
                    Grade = EvaluationGrade.HoanThanhXuatSac, MinScore = 90, MinExceedStandardRatio = 0.30,
                    Conditions = "Đồng thời đáp ứng: (i) hoàn thành 100% nhiệm vụ được giao, đúng hạn, bảo đảm chất lượng, hiệu quả; (ii) đối với cán bộ lãnh đạo, quản lý, tập thể/lĩnh vực/bộ phận do cá nhân trực tiếp phụ trách hoàn thành 100% nhiệm vụ; (iii) có ít nhất 30% nhiệm vụ hoàn thành vượt mức yêu cầu; (iv) đã khắc phục 100% hạn chế, khuyết điểm đến hạn được chỉ ra ở kỳ trước (nếu có); (v) không thuộc trường hợp bị khống chế mức xếp loại. Không có nhiệm vụ trọng tâm bị xếp ở Mức 2 hoặc Mức 3."
                },
                new()
                {
                    Grade = EvaluationGrade.HoanThanhTot, MinScore = 70,
                    Conditions = "Đồng thời hoàn thành 100% nhiệm vụ được giao, đúng hạn, bảo đảm chất lượng, hiệu quả; đối với cán bộ lãnh đạo, quản lý, tập thể/lĩnh vực/bộ phận trực tiếp phụ trách hoàn thành 100% nhiệm vụ, đúng hạn, bảo đảm chất lượng, hiệu quả. Không có nhiệm vụ trọng tâm bị xếp ở Mức 3; tỷ lệ nhiệm vụ trọng tâm xếp Mức 2 dưới 20% (văn bản ghi \"từ 20% trở lên\" — trích xuất HD03 mục 10 #2, chờ xác nhận)."
                },
                new()
                {
                    Grade = EvaluationGrade.HoanThanh, MinScore = 50,
                    Conditions = "Đồng thời hoàn thành 100% nhiệm vụ được giao; số nhiệm vụ chưa bảo đảm tiến độ không vượt quá 20%; đối với cán bộ lãnh đạo, quản lý, tập thể/lĩnh vực/bộ phận trực tiếp phụ trách hoàn thành 100% nhiệm vụ, số nhiệm vụ chưa bảo đảm tiến độ không vượt quá 20%. Không có nhiệm vụ trọng tâm bị xếp ở Mức 3 và tỷ lệ phần trăm nhiệm vụ trọng tâm xếp Mức 2 dưới 20%."
                },
                new()
                {
                    Grade = EvaluationGrade.KhongHoanThanh, MinScore = 0,
                    Conditions = "Dưới 50 điểm hoặc thuộc trường hợp bắt buộc: hoàn thành dưới 100% nhiệm vụ trong quý do nguyên nhân chủ quan; thuộc trường hợp không hoàn thành theo Quy định 366 và quy định liên quan; có nhiệm vụ trọng tâm xếp Mức 3. Yếu tố khách quan, bất khả kháng, nhiệm vụ được điều chỉnh/loại trừ hợp lệ không tính là nhiệm vụ không hoàn thành."
                }
            },
            Parameters = new CriteriaParameters
            {
                MinTasks = 3,
                MaxTasks = 7,
                TotalTaskWeight = 70,
                TaskWeightTolerance = 0.05,
                GeneralMaxScore = 30,
                // Văn bản không quy định khung mặc định; giữ mặc định của code trước task 16 (Khung 2) — chờ xác nhận.
                DefaultWeightFrameCode = "K2",
                AllowNotApplicable = true,
                NotApplicableRule = NotApplicableRule.ExcludeAndRescale,
                DeductionReasonMinPoints = 1,
                ExplanationThreshold = 5,
                ExplanationOnGradeChange = true,
                ExcellentQuota = new ExcellentQuotaRule { Ratio = 0.20, Denominator = QuotaDenominator.GoodOnly, Rounding = ScoreRoundingMode.HalfUp },
                Rounding = new RoundingSettings
                {
                    TaskScore = new RoundingRule(1, ScoreRoundingMode.HalfUp),
                    TasksTotal = new RoundingRule(1, ScoreRoundingMode.HalfUp),
                    GeneralTotal = new RoundingRule(1, ScoreRoundingMode.HalfUp),
                    Total = new RoundingRule(1, ScoreRoundingMode.HalfUp)
                },
                CollectiveGeneralMaxScore = 30,
                CollectiveTaskMaxScore = 70
            }
        }.Normalize();
    }

    private static CriteriaItem Item(string code, string text, double maxScore) => new() { Code = code, Text = text, MaxScore = maxScore };
}
