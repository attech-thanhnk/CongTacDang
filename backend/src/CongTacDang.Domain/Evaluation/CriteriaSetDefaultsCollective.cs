using System.Collections.Generic;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Khung tiêu chí đánh giá tập thể mặc định (Mẫu 06, 07) chép nguyên văn HD03 Phụ lục I "KHUNG TIÊU CHÍ ĐÁNH GIÁ ĐỐI VỚI TẬP THỂ"
/// theo bản trích xuất <c>docs/nghiep-vu/hd03-trich-xuat.md</c> mục 5.1: nhóm I tiêu chí chung 30 điểm (6 × 5), nhóm II kết quả
/// thực hiện nhiệm vụ 70 điểm (50 + 20). Chờ nghiệp vụ xác nhận; sửa được trong bộ tiêu chí bản nháp.
/// </summary>
public static partial class CriteriaSetDefaults
{
    /// <summary>Khung tiêu chí tập thể mặc định — bản mới mỗi lần gọi (an toàn để sửa).</summary>
    public static List<CollectiveCriteriaGroup> CollectiveGroups() => new()
    {
        new CollectiveCriteriaGroup
        {
            Code = "I",
            Name = "Nhóm tiêu chí chung",
            Kind = CollectiveScoreKind.General,
            Items = new List<CriteriaItem>
            {
                Item("I.1", "Kết quả công tác xây dựng, chỉnh đốn Đảng; củng cố, xây dựng tổ chức đảng và hệ thống chính trị; nâng cao chất lượng đội ngũ cán bộ, đảng viên theo nghị quyết, kế hoạch, chỉ tiêu được giao.", 5),
                Item("I.2", "Năng lực lãnh đạo của cấp uỷ; hiệu quả lãnh đạo, chỉ đạo của cấp uỷ đối với chính quyền và các tổ chức chính trị - xã hội; hiệu quả trong phối hợp công tác; xây dựng đoàn kết nội bộ.", 5),
                Item("I.3", "Hiệu quả trong công tác thanh tra, kiểm tra, giám sát và kỷ luật đảng; thực hiện các quy định về kiểm soát quyền lực; không để xảy ra tham nhũng, lãng phí, tiêu cực, suy thoái về tư tưởng chính trị, đạo đức, lối sống, \"tự diễn biến\", \"tự chuyển hoá\" trong nội bộ.", 5),
                Item("I.4", "Kết quả thực hiện các nguyên tắc tổ chức và hoạt động của Đảng, nhất là nguyên tắc tập trung dân chủ, tự phê bình và phê bình; việc chấp hành quy chế làm việc, nội quy, chế độ công tác; phân công nhiệm vụ, tổ chức thực hiện hiệu quả.", 5),
                Item("I.5", "Kết quả lãnh đạo, chỉ đạo, kiểm tra, giám sát việc thực hiện nhiệm vụ chính trị; xây dựng bộ máy tinh gọn, tinh giản biên chế, hoạt động hiệu năng, hiệu lực, hiệu quả; thực hiện cải cách hành chính, chuyển đổi số, đẩy mạnh ứng dụng khoa học, công nghệ, phương pháp quản lý gắn với xây dựng văn hoá công vụ trong hoạt động quản lý, điều hành tổ chức, cơ quan, đơn vị.", 5),
                Item("I.6", "Tinh thần tự phê bình và phê bình, tự soi, tự sửa của tập thể; kết quả khắc phục những hạn chế, yếu kém, khuyết điểm đã được chỉ ra trong các kỳ kiểm điểm trước hoặc qua thanh tra, kiểm tra, giám sát.", 5)
            }
        },
        new CollectiveCriteriaGroup
        {
            Code = "II",
            Name = "Nhóm tiêu chí về kết quả thực hiện nhiệm vụ",
            Kind = CollectiveScoreKind.Task,
            Items = new List<CriteriaItem>
            {
                // Mục 1 (50 điểm) có hai nhánh không có điểm riêng: 1.1 khối cơ quan tham mưu, giúp việc của Đảng và 1.2 khối cơ quan,
                // đơn vị trực thuộc. Đơn vị dùng hệ thống thuộc khối 1.2 nên ghép nguyên văn nội dung 1.2 vào tiêu chí II.1; bỏ 1.1.
                Item("II.1", "Nhóm tiêu chí về việc cụ thể hoá nghị quyết, chương trình, kế hoạch công tác của cấp uỷ hoặc cấp có thẩm quyền và tổ chức thực hiện hiệu quả các mục tiêu, chỉ tiêu, nhiệm vụ được giao trên các lĩnh vực (kết quả được lượng hoá bằng sản phẩm, tiến độ, chất lượng hoặc chỉ số đo lường hiệu quả công việc), cụ thể: Khối cơ quan, đơn vị trực thuộc: Tập trung vào thực hiện nhiệm vụ được giao (nếu có); kết quả sản xuất kinh doanh; về quản lý tài chính, tài sản; về quản trị doanh nghiệp, tổ chức bộ máy, nhân sự; xây dựng văn hoá doanh nghiệp; việc chăm lo đời sống người lao động; phòng, chống tham nhũng, lãng phí, tiêu cực… theo chức năng, nhiệm vụ được giao.", 50),
                Item("II.2", "Nhóm tiêu chí về kết quả đánh giá, xếp loại đối với các lĩnh vực, cơ quan, đơn vị do tập thể phụ trách (nếu có); kết quả đánh giá chất lượng đối với các tổ chức trực thuộc và cá nhân liên quan trực tiếp.", 20)
            }
        }
    };
}
