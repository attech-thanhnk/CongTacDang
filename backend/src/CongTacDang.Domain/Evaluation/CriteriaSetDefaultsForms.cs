using System.Collections.Generic;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Nội dung biểu mẫu cá nhân của hai bộ mặc định (task 18 — T-82), chép nguyên văn từ file biểu mẫu gốc
/// <c>docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx</c>: tiêu đề và nội dung gợi ý từng trục ở Mẫu 09B
/// (phần II), mục tự đánh giá của Mẫu 09C; danh sách biểu mẫu áp dụng theo trích xuất HD03 mục 7–8
/// (Quý III/2026 chưa áp dụng Mẫu 01–06, 09A, 11).
/// </summary>
public static partial class CriteriaSetDefaults
{
    /// <summary>Biểu mẫu cá nhân áp dụng — bộ 09B (Quý III/2026): 09B, 09C, 9D, 10.</summary>
    public static IReadOnlyList<string> RequiredForms09B { get; } = new[]
    {
        RecordFormCodes.Form09B, RecordFormCodes.Form09C, RecordFormCodes.Form9D, RecordFormCodes.Form10
    };

    /// <summary>Biểu mẫu cá nhân áp dụng — bộ 09A (từ 2027, áp dụng đầy đủ): 01, 02, 09A, 09C, 9D, 10.</summary>
    public static IReadOnlyList<string> RequiredForms09A { get; } = new[]
    {
        RecordFormCodes.Form01, RecordFormCodes.Form02, RecordFormCodes.Form09A, RecordFormCodes.Form09C, RecordFormCodes.Form9D, RecordFormCodes.Form10
    };

    /// <summary>Mục tự đánh giá của Mẫu 09C (mẫu gốc có một mục; phần II lấy từ điểm tự chấm đã lưu).</summary>
    public static List<SelfAssessmentSection> SelfAssessmentSections() => new()
    {
        new SelfAssessmentSection
        {
            Code = "I",
            Title = "I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao",
            Guidance = "Trên cơ sở nhiệm vụ được giao, cá nhân tự đánh giá về kết quả thực hiện nhiệm vụ theo quý như sau:",
            Note = "(Lưu ý: Viết tóm tắt theo kết quả 6 trục. Đối với nhiệm vụ trọng tâm, then chốt, nhất là nhiệm vụ an toàn, an ninh, chất lượng, khai thác, tài chính, đầu tư, dự án và pháp chế, phải kết luận rõ chất lượng sản phẩm, hiệu quả thực hiện nhiệm vụ theo 01 trong 03 mức: Mức 1-Chủ động tiếp cận, giải quyết hiệu quả vấn đề, có kết quả cụ thể; Mức 2-Cơ bản đáp ứng yêu cầu nhưng còn hạn chế, cần tiếp tục hoàn thiện; Mức 3-Không đáp ứng yêu cầu nhiệm vụ)",
            // Lưu ý cuối Mẫu 09C: "Nội dung trình bày trong phạm vi không quá 02 trang A4" — khoảng 6.000 ký tự.
            MaxLength = 6000,
            // Văn bản không nói hệ thống phải chặn nộp khi để trống; bật được trong bộ tiêu chí khi nghiệp vụ yêu cầu.
            Required = false
        }
    };

    /// <summary>Tiêu đề và nội dung gợi ý của 6 trục trên Mẫu 09B (theo thứ tự T1…T6).</summary>
    private static readonly (string Title, string Guidance)[] AxisFormTexts =
    {
        ("TRỤC (1) – NHIỆM VỤ CHÍNH TRỊ, SẢN XUẤT KINH DOANH, CUNG CẤP DỊCH VỤ",
            string.Join("\n",
                "- Đảm bảo cung cấp dịch vụ BĐHĐB “An toàn-Điều hòa-Hiệu quả”;",
                "- Hoàn thành các chỉ tiêu tài chính (doanh thu, chi phí, lợi nhuận, tiết giảm chi phí, giải ngân) theo kế hoạch được giao;",
                "- Hoàn thành kế hoạch mua sắm, sửa chữa theo quy định;",
                "- Hoàn thành tiến độ, chất lượng các dự án đầu tư theo quy định;",
                "- Quản lý, tổ chức thực hiện các hợp đồng chặt chẽ, đúng quy định.",
                "- Việc sắp xếp, tinh gọn tổ chức bộ máy đảm bảo hiệu năng, hiệu lực, hiệu quả;",
                "- Công tác cán bộ (đánh giá, quy hoạch, đào tạo bồi dưỡng, bổ nhiệm, bố trí sử dụng, khen thưởng, kỷ luật, chính sách cán bộ,…);",
                "- Xây dựng/thực hiện, rà soát, cập nhật mô tả công việc của từng vị trí thuộc quyền quản lý; phân công công việc rõ ràng, bố trí, sử dụng nhân sự hợp lý, phù hợp năng lực; chủ động đề xuất đào tạo, bồi dưỡng, huấn luyện theo nhu cầu của cơ quan/đơn vị; xây dựng đội ngũ nhân viên chuyên nghiệp, đáp ứng tốt yêu cầu nhiệm vụ, nhận được phản hồi tích cực từ cấp trên, trong nội bộ và đơn vị phối hợp; đánh giá kết quả làm việc của nhân viên đảm bảo chặt chẽ, công khai, minh bạch, thống nhất và tuân thủ các quy chế, quy trình, chuẩn mực có liên quan;",
                "- Chủ động rà soát và đề xuất điều chỉnh chức năng, nhiệm vụ, cơ cấu tổ chức, sắp xếp nhân sự, phân cấp/phân quyền phù hợp; ứng dụng công nghệ nhằm nâng cao hiệu quả hoạt động của công tác tổ chức, cán bộ, đảm bảo quá trình tái cấu trúc, sắp xếp tinh gọn bộ máy không gây xáo trộn lớn và đúng định hướng của Tổng công ty;",
                "- Hoàn thành các nhiệm vụ chính trị được giao khác theo lĩnh vực chuyên môn đảm nhiệm (bao gồm các nhiệm vụ theo kế hoạch và các nhiệm vụ phát sinh khác theo yêu cầu, chỉ đạo của cấp trên), đảm bảo tiến độ và chất lượng theo quy định.",
                "- Các yêu cầu trong quá trình lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ:",
                "+ Lãnh đạo, chỉ đạo, phân công, phối hợp thực hiện công việc; thiết lập rõ ràng mục tiêu, sản phẩm đầu ra, thời hạn hoàn thành và trách nhiệm của từng cá nhân/bộ phận trong cơ quan/đơn vị; chủ động đôn đốc, kiểm tra tiến độ thực hiện; kịp thời tháo gỡ khó khăn, xử lý tồn tại;",
                "+ Xây dựng/thực hiện kế hoạch công tác chi tiết, phân công rõ trách nhiệm, quyền hạn, thời hạn xử lý công việc; xây dựng/thực hiện hệ thống theo dõi, kiểm soát tiến độ thực hiện công việc;",
                "+ Dự báo tình hình sản xuất kinh doanh mang tính thực tiễn, được áp dụng hiệu quả trong kế hoạch hoạt động; nhận diện sớm và có đề xuất giải pháp phòng ngừa, giảm thiểu rủi ro, tối ưu hiệu quả thực hiện nhiệm vụ của cơ quan/ đơn vị;",
                "+ Quan hệ phối hợp trong nội bộ và với các cơ quan, đơn vị liên quan được thực hiện đồng bộ, chặt chẽ, minh bạch, đúng quy định, nhận được phản hồi tích cực, không có văn bản phê bình, nhắc nhở, kiến nghị từ lãnh đạo Tổng công ty hoặc cơ quan liên quan; xử lý tranh chấp, kiến nghị, vấn đề phát sinh kịp thời, đúng quy định pháp luật, nhận được sự đồng thuận từ các bên liên quan, được cấp trên đánh giá cao;",
                "+ Đánh giá kết quả thực hiện công việc; khen thưởng, động viên hoặc phê bình, nhắc nhở các tập thể, cá nhân có liên quan;",
                "+ Báo cáo đúng thời hạn, đúng yêu cầu; thông tin, số liệu chính xác, trích dẫn đúng nguồn gốc; phản ánh toàn diện tình hình cơ quan/đơn vị; có đánh giá, nêu rõ nguyên nhân, tác động, trách nhiệm; đề xuất giải pháp có tính khả thi, hiệu quả.")),
        ("TRỤC (2) - HOÀN THIỆN THỂ CHẾ, ĐẨY MẠNH PHÂN CẤP, PHÂN QUYỀN GẮN VỚI KIỂM TRA, GIÁM SÁT",
            string.Join("\n",
                "- Ban hành kế hoạch, chương trình hành động, quy chế, quy định thực hiện các nghị quyết, chỉ thị, kết luận về phân cấp, phân quyền;",
                "- Triển khai, quán triệt, tổ chức thực hiện chính sách, kế hoạch, chương trình hành động, quy chế, quy định về phân cấp, phân quyền;",
                "- Xây dựng/thực hiện quy chế, quy trình quản lý nội bộ: Chủ động ban hành và thường xuyên cập nhật quy trình, quy chế quản lý nội bộ của cơ quan/đơn vị;",
                "- Xây dựng/thực hiện cơ chế kiểm soát quyền lực; kiểm tra, giám sát việc thực hiện phân cấp, phân quyền và thực thi các quy định của pháp luật, các quy chế, quy định quản lý nội bộ khác; kiểm toán, kiểm soát nội bộ;",
                "- Giải quyết 100% đơn, thư thuộc trách nhiệm, không để vượt cấp, phát sinh kéo dài;",
                "- Các yêu cầu trong quá trình lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ: Xem chi tiết tại Trục 1;",
                "- Thực hiện các nội dung khác về hoàn thiện thể chế, phân cấp, phân quyền (nếu có).")),
        ("TRỤC (3) - THÚC ĐẨY PHÁT TRIỂN KHOA HỌC, CÔNG NGHỆ, ĐỔI MỚI SÁNG TẠO VÀ CHUYỂN ĐỔI SỐ",
            string.Join("\n",
                "- Triển khai chương trình, đề án chuyển đổi số;",
                "- Ứng dụng khoa học công nghệ, đổi mới sáng tạo trong quản lý hành chính và các lĩnh vực, các hoạt động của cơ quan, đơn vị, tổ chức;",
                "- Nghiên cứu, thử nghiệm, tự động hóa, dữ liệu, an ninh mạng, hệ thống ATM/CNS/AIM/MET, ứng dụng AI/phân tích dữ liệu...;",
                "- Xây dựng cơ sở dữ liệu, các chương trình/phần mềm phục vụ công tác chuyên môn;",
                "- Trích lập, quản lý và sử dụng có hiệu quả Quỹ Khoa học-Công nghệ;",
                "- Cơ quan/ đơn vị do cá nhân phụ trách có sáng kiến, giải pháp hữu ích áp dụng hiệu quả; chủ động tiếp nhận, triển khai thực hiện hiệu quả các nhiệm vụ Khoa học-Công nghệ; thực hiện tốt công tác chuyển đổi số, ứng dụng công nghệ thông tin trong quản lý, điều hành, lưu trữ hồ sơ;",
                "- Các yêu cầu trong quá trình lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ: Xem chi tiết tại Trục 1;",
                "- Thực hiện các nội dung khác về thúc đẩy phát triển khoa học, công nghệ, đổi mới sảng tạo và chuyển đổi số (nếu có).")),
        ("TRỤC (4) - XÂY DỰNG ĐẢNG VÀ HỆ THỐNG CHÍNH TRỊ TRONG SẠCH, VỮNG MẠNH; GIỮ GÌN ĐOÀN KẾT, THỐNG NHẤT NỘI BỘ; PHÒNG, CHỐNG THAM NHŨNG, LÃNG PHÍ, TIÊU CỰC",
            string.Join("\n",
                "- Xây dựng, củng cố tổ chức cơ sở Đảng (thành lập mới, sắp xếp, kiện toàn các tổ chức Đảng);",
                "- Thực hiện kế hoạch phát triển đảng viên mới;",
                "- Triển khai sinh hoạt chi bộ, sinh hoạt cấp ủy định kỳ; tổ chức phổ biến, quán triệt các nghị quyết, văn bản của cấp ủy cấp trên theo định kỳ hoặc đột xuất;",
                "- Chấp hành nghiêm các nguyên tắc tổ chức và hoạt động của Đảng;",
                "- Công tác cấp ủy, UBKT (đánh giá, quy hoạch, đào tạo bồi dưỡng, chỉ định/chuẩn y, khen thưởng, kỷ luật, chính sách cán bộ,…); công tác bảo vệ chính trị nội bộ (kết luận tiêu chuẩn chính trị; quản lý cán bộ, đảng viên, người lao động ra nước ngoài hoặc tiếp xúc, làm việc với các cá nhân, tổ chức nước ngoài);",
                "- Công tác đấu tranh, bảo vệ nền tảng tư tưởng của Đảng, phản bác các quan điểm sai trái, thù địch;",
                "- Công tác phòng chống tham nhũng, lãng phí, tiêu cực;",
                "- Công tác kiểm tra, giám sát, thi hành kỷ luật Đảng;",
                "- Ứng dụng khoa học công nghệ, chuyển đổi số trong công tác xây dựng Đảng, hệ thống chính trị;",
                "- Xây dựng, củng cố tổ chức công đoàn, đoàn thanh niên; phát huy vai trò của tổ chức công đoàn, đoàn thanh niên trong công tác xây dựng Đảng và các nhiệm vụ trọng tâm, then chốt của Tổng công ty;",
                "- Các yêu cầu trong quá trình lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ: Xem chi tiết tại Trục 1;",
                "- Thực hiện các nội dung khác về công tác xây dựng Đảng, hệ thống chính trị… (nếu có).")),
        ("TRỤC (5) - PHÁT TRIỂN VĂN HÓA, NÂNG CAO ĐỜI SỐNG VẬT CHẤT VÀ TINH THẦN NGƯỜI LAO ĐỘNG; THỰC HIỆN TRÁCH NHIỆM XÃ HỘI",
            string.Join("\n",
                "- Thực hiện Quy chế Dân chủ cơ sở và các quy chế, quy định có liên quan đến quyền lợi hợp pháp, chính đáng của người lao động;",
                "- Định kỳ gặp gỡ, tiếp xúc, đối thoại với cán bộ, đảng viên, người lao động trong cơ quan, đơn vị theo quy định;",
                "- Lấy ý kiến người lao động và tổ chức công đoàn khi xây dựng mới hoặc sửa đổi, bổ sung các quy chế, quy định hoặc triển khai các công việc của cơ quan, đơn vị có liên quan đến quyền lợi hợp pháp, chính đáng của người lao động;",
                "- Xây dựng môi trường văn hóa tự học tập theo tinh thần Lênin: “Học, học nữa, học mãi”; tham gia giảng dạy, thuyết trình, báo cáo chuyên đề, chia sẻ kinh nghiệm tại đơn vị hoặc tại các hội nghị, hội thảo, khóa đào tạo - huấn luyện do cơ quan, đơn vị tổ chức;",
                "- Đảm bảo môi trường làm việc an toàn, thân thiện, chia sẻ, gắn kết (cơ sở vật chất, cảnh quan, truyền thông, văn hóa công sở…);",
                "- Tổ chức các phong trào, hoạt động thể thao, văn hóa, văn nghệ;",
                "- Xây dựng/thực hiện chuẩn mực văn hóa chính trực, liêm chính Người lao động Quản lý bay;",
                "- Thực hiện đường lối, chủ trương, chính sách của Đảng, pháp luật Nhà nước về chính sách Người có công với Cách mạng và trách nhiệm xã hội;",
                "- Hưởng ứng các hoạt động phong trào do chính quyền địa phương nơi cơ quan, đơn vị đứng chân tổ chức;",
                "- Các yêu cầu trong quá trình lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ: Xem chi tiết tại Trục 1;",
                "- Thực hiện các nội dung khác có liên quan (nếu có).")),
        ("TRỤC (6) – THỰC HIỆN NHIỆM VỤ QUỐC PHÒNG, AN NINH, GIỮ VỮNG ỔN ĐỊNH CHÍNH TRỊ, TRẬT TỰ, AN TOÀN CƠ QUAN, ĐƠN VỊ; NÂNG CAO HIỆU QUẢ ĐỐI NGOẠI VÀ HỘI NHẬP QUỐC TẾ",
            string.Join("\n",
                "- Xây dựng nền quốc phòng toàn dân, thế trận an ninh nhân dân vững chắc;",
                "- Giữ vững an ninh chính trị, trật tự an toàn xã hội; không để hình thành \"điểm nóng\", bị động, bất ngờ;",
                "- Hoàn thành nhiệm vụ huấn luyện dân quân tự vệ, diễn tập khu vực phòng thủ;",
                "- Không để xảy ra tệ nạn xã hội; đấu tranh phòng, chống tội phạm, ma túy;",
                "- Triển khai hiệu quả công tác đối ngoại, hợp tác quốc tế hợp tác quốc tế (ICAO/CANSO/khu vực) theo phân cấp;",
                "- Bảo đảm an ninh hàng không, an ninh kinh tế, an ninh mạng, an ninh tôn giáo, dân tộc;",
                "- Phối hợp quân sự - dân sự, phòng chống thiên tai, tìm kiếm cứu nạn;",
                "- Các yêu cầu trong quá trình lãnh đạo, chỉ đạo, tổ chức thực hiện nhiệm vụ: Xem chi tiết tại Trục 1;",
                "- Thực hiện các nội dung khác có liên quan (nếu có).")),
    };

    /// <summary>Gắn tiêu đề/nội dung gợi ý Mẫu 09B cho các trục theo thứ tự.</summary>
    private static void ApplyAxisFormTexts(IList<ResultAxis> axes)
    {
        for (var i = 0; i < axes.Count && i < AxisFormTexts.Length; i++)
        {
            axes[i].FormTitle = AxisFormTexts[i].Title;
            axes[i].FormGuidance = AxisFormTexts[i].Guidance;
        }
    }
}
