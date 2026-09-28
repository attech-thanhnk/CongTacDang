using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Thông tin hồ sơ và vai trò của cán bộ đang đăng nhập</summary>
public class UserProfileDto
{
    /// <summary>Mã định danh duy nhất của cán bộ</summary>
    public Guid Id { get; set; }

    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Tên tài khoản đăng nhập</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Chức vụ công tác Đảng</summary>
    public string PartyRole { get; set; } = string.Empty;

    /// <summary>Chức danh quản lý chuyên môn chính quyền</summary>
    public string AdminTitle { get; set; } = string.Empty;

    /// <summary>Tên Chi bộ Đảng sinh hoạt</summary>
    public string PartyBranchName { get; set; } = string.Empty;

    /// <summary>Tên đơn vị / Phòng ban chuyên môn</summary>
    public string AdminDeptName { get; set; } = string.Empty;

    /// <summary>Nhóm chức danh công tác</summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>Danh sách mã vai trò hệ thống</summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>Danh sách mã quyền hạn nguyên tử được cấp</summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>Người dùng phải đổi mật khẩu tạm trước khi tiếp tục</summary>
    public bool MustChangePassword { get; set; }
}

/// <summary>Thông tin tóm tắt hồ sơ cán bộ lãnh đạo, quản lý 2 vai</summary>
public class CadreDto
{
    /// <summary>Mã định danh cán bộ</summary>
    public Guid Id { get; set; }

    /// <summary>Họ và tên đầy đủ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số thẻ Đảng viên</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức vụ công tác Đảng</summary>
    public string? PartyRole { get; set; }

    /// <summary>Chức danh quản lý chuyên môn</summary>
    public string? AdminTitle { get; set; }

    /// <summary>Tên Chi bộ sinh hoạt Đảng</summary>
    public string? PartyCellName { get; set; }

    /// <summary>Tên Phòng ban chuyên môn</summary>
    public string? DepartmentName { get; set; }

    /// <summary>Đã kết nạp Đảng viên hay chưa</summary>
    public bool IsPartyMember { get; set; }

    /// <summary>Trạng thái hoạt động</summary>
    public bool IsActive { get; set; }
}

/// <summary>Dữ liệu yêu cầu tiếp nhận hồ sơ cán bộ mới</summary>
public class CreateUserDto
{
    /// <summary>Họ và tên cán bộ (bắt buộc)</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số thẻ Đảng viên (nếu có)</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức danh quản lý chính quyền</summary>
    public string? AdminTitle { get; set; }

    /// <summary>Mã Chi bộ phân công sinh hoạt</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Mã Phòng ban công tác chuyên môn</summary>
    public Guid? DepartmentId { get; set; }
}

/// <summary>Dữ liệu yêu cầu cập nhật thông tin hồ sơ cán bộ</summary>
public class UpdateUserDto
{
    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số thẻ Đảng viên</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức danh quản lý chính quyền</summary>
    public string? AdminTitle { get; set; }

    /// <summary>Mã Chi bộ phân công sinh hoạt</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Mã Phòng ban công tác chuyên môn</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Trạng thái kích hoạt hồ sơ</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Thông tin tổ chức Chi bộ cơ sở</summary>
public class BranchDto
{
    /// <summary>Mã định danh Chi bộ</summary>
    public Guid Id { get; set; }

    /// <summary>Ký hiệu / Mã quản lý Chi bộ</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên Chi bộ Đảng</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Nhiệm vụ trọng tâm hoặc mô tả Chi bộ</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang hoạt động (false = đã ngừng hoạt động)</summary>
    public bool IsActive { get; set; }

    /// <summary>Số lượng cán bộ, Đảng viên sinh hoạt tại Chi bộ</summary>
    public int MemberCount { get; set; }
}

/// <summary>Thông tin Phòng ban / Đơn vị chuyên môn</summary>
public class DepartmentDto
{
    /// <summary>Mã định danh phòng ban</summary>
    public Guid Id { get; set; }

    /// <summary>Mã ký hiệu phòng ban</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên phòng ban chuyên môn</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả chức năng nhiệm vụ</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang hoạt động (false = đã ngừng hoạt động)</summary>
    public bool IsActive { get; set; }

    /// <summary>Số lượng cán bộ đang công tác</summary>
    public int MemberCount { get; set; }
}

/// <summary>Thông tin tệp tin đính kèm và văn bản minh chứng</summary>
public class AttachmentDto
{
    /// <summary>Mã định danh tệp tin</summary>
    public Guid Id { get; set; }

    /// <summary>Tên tệp tin gốc</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Định dạng MIME của tệp</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Dung lượng tệp tính bằng byte</summary>
    public long FileSize { get; set; }

    /// <summary>Mã biểu mẫu hoặc ký hiệu hồ sơ (M01, M02, M10...)</summary>
    public string FormCode { get; set; } = string.Empty;

    /// <summary>Bí danh tương thích ngược cho FormCode</summary>
    public string Category 
    { 
        get => FormCode; 
        set => FormCode = value; 
    }

    /// <summary>Trích yếu hoặc nội dung tóm tắt văn bản</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Mã băm SHA-256 bảo đảm toàn vẹn dữ liệu</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>Đường dẫn tải về trực tiếp (nếu có)</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Thời điểm tải lên hệ thống</summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>Họ tên hoặc tài khoản người tải lên</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>Mã người tải lên phiên bản này (null với dữ liệu cũ)</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>Loại đối tượng sở hữu tệp (General, EvaluationRecord, EvaluationTask)</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>Mã đối tượng sở hữu tệp</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Mã nhóm phiên bản (dùng để xem lịch sử, tải phiên bản cụ thể)</summary>
    public Guid FileGroupId { get; set; }

    /// <summary>Số thứ tự phiên bản</summary>
    public int VersionNumber { get; set; }

    /// <summary>Là phiên bản hiện hành</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Thời điểm phiên bản bị thay (null nếu đang hiện hành)</summary>
    public DateTime? SupersededAt { get; set; }

    /// <summary>Người đã thay phiên bản này</summary>
    public Guid? SupersededById { get; set; }
}

/// <summary>Thông tin một bản ghi audit dành cho khu vực quản trị.</summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OldValues { get; set; } = "{}";
    public string NewValues { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

#region DTOs Quy trình Đánh giá Cán bộ 5 Bước (03-HD/TVĐU)

/// <summary>Lịch sử chuyển trạng thái hồ sơ đánh giá.</summary>
public class EvaluationRecordHistoryDto
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Thông tin công việc chuyên môn đăng ký và đánh giá (Mẫu 01 & Mẫu 02)</summary>
public class EvaluationTaskDto
{
    /// <summary>Mã định danh công việc</summary>
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin dùng khi cập nhật công việc.</summary>
    public uint Version { get; set; }

    /// <summary>Mã hồ sơ đánh giá sở hữu</summary>
    public Guid RecordId { get; set; }

    /// <summary>Thứ tự công việc (1 đến 7)</summary>
    public int TaskOrder { get; set; }

    /// <summary>Tên sản phẩm, công việc chuyên môn</summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>Chỉ tiêu đầu ra, tiêu chuẩn kỹ thuật</summary>
    public string TargetOutput { get; set; } = string.Empty;

    /// <summary>Trọng số điểm giao việc</summary>
    public double Weight { get; set; }

    /// <summary>Thời hạn hoàn thành</summary>
    public DateTime Deadline { get; set; }

    /// <summary>Tỷ lệ hoàn thành Tiêu chí A - Khối lượng (0.0 đến 1.0)</summary>
    public double CriteriaA_Ratio { get; set; }

    /// <summary>Tỷ lệ hoàn thành Tiêu chí B - Chất lượng (0.0 đến 1.0)</summary>
    public double CriteriaB_Ratio { get; set; }

    /// <summary>Tỷ lệ hoàn thành Tiêu chí C - Tiến độ (0.0 đến 1.0)</summary>
    public double CriteriaC_Ratio { get; set; }

    /// <summary>Tỷ lệ hoàn thành Tiêu chí D - Hiệu quả, sáng kiến (0.0 đến 1.0)</summary>
    public double CriteriaD_Ratio { get; set; }

    /// <summary>Điểm tự chấm của công việc</summary>
    public double SelfScore { get; set; }

    /// <summary>Điểm do cấp trên/thẩm định chấm</summary>
    public double? SupervisorScore { get; set; }

    /// <summary>Đạt tiêu chuẩn vượt chuẩn xét Xuất sắc</summary>
    public bool IsExceedStandard { get; set; }

    /// <summary>Mã tệp tin minh chứng dẫn chiếu</summary>
    public Guid? AttachmentId { get; set; }

    /// <summary>Tên tệp tin minh chứng đính kèm</summary>
    public string? AttachmentFileName { get; set; }

    /// <summary>Tên gốc của tệp tin minh chứng đính kèm</summary>
    public string? AttachmentOriginalName { get; set; }
}

/// <summary>Dữ liệu tạo/sửa một đầu việc chuyên môn (Mẫu 01)</summary>
public class TaskInputDto
{
    /// <summary>Mã công việc (nếu sửa việc có sẵn, để trống nếu thêm mới)</summary>
    public Guid? Id { get; set; }

    /// <summary>Thứ tự công việc (1 đến 7)</summary>
    public int TaskOrder { get; set; }

    /// <summary>Tên sản phẩm, công việc chuyên môn</summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>Chỉ tiêu đầu ra, tiêu chuẩn chất lượng</summary>
    public string TargetOutput { get; set; } = string.Empty;

    /// <summary>Trọng số điểm (tổng 3-7 việc phải đúng 70.0 điểm)</summary>
    public double Weight { get; set; }

    /// <summary>Thời hạn hoàn thành</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Mã tệp tin minh chứng đính kèm</summary>
    public Guid? AttachmentId { get; set; }

    /// <summary>Tên tệp tin minh chứng đính kèm</summary>
    public string? AttachmentFileName { get; set; }
}

/// <summary>Dữ liệu tự chấm điểm một đầu việc (Mẫu 02)</summary>
public class TaskScoreInputDto
{
    /// <summary>Mã công việc</summary>
    public Guid TaskId { get; set; }
    /// <summary>Phiên bản xmin đọc được của công việc.</summary>
    public uint? Version { get; set; }

    /// <summary>Tỷ lệ hoàn thành Tiêu chí A (0.0 - 1.0)</summary>
    public double CriteriaA_Ratio { get; set; } = 1.0;

    /// <summary>Tỷ lệ hoàn thành Tiêu chí B (0.0 - 1.0)</summary>
    public double CriteriaB_Ratio { get; set; } = 1.0;

    /// <summary>Tỷ lệ hoàn thành Tiêu chí C (0.0 - 1.0)</summary>
    public double CriteriaC_Ratio { get; set; } = 1.0;

    /// <summary>Tỷ lệ hoàn thành Tiêu chí D (0.0 - 1.0)</summary>
    public double CriteriaD_Ratio { get; set; } = 1.0;

    /// <summary>Đạt tiêu chuẩn vượt chuẩn</summary>
    public bool IsExceedStandard { get; set; } = false;

    /// <summary>Mã tệp tin minh chứng đã tải lên</summary>
    public Guid? AttachmentId { get; set; }
}

/// <summary>Thông tin Kỳ đánh giá định kỳ hằng quý</summary>
public class EvaluationPeriodDto
{
    /// <summary>Mã định danh kỳ đánh giá</summary>
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin dùng cho thao tác cập nhật kỳ.</summary>
    public uint Version { get; set; }

    /// <summary>Năm đánh giá</summary>
    public int Year { get; set; }

    /// <summary>Quý đánh giá (1, 2, 3, 4)</summary>
    public int Quarter { get; set; }

    /// <summary>Tên hiển thị kỳ đánh giá</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ngày bắt đầu</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Ngày kết thúc</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Mã trạng thái kỳ đánh giá</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Tên hiển thị trạng thái tiếng Việt</summary>
    public string StatusDisplayName { get; set; } = string.Empty;

    /// <summary>Tổng số hồ sơ cán bộ trong kỳ</summary>
    public int TotalRecords { get; set; }

    /// <summary>Kỳ đánh giá có đang hoạt động hay không</summary>
    public bool IsActive { get; set; }
}

/// <summary>Dữ liệu yêu cầu tạo Kỳ đánh giá mới</summary>
public class CreatePeriodDto
{
    /// <summary>Năm đánh giá (VD: 2026)</summary>
    public int Year { get; set; }

    /// <summary>Quý đánh giá (1, 2, 3, 4)</summary>
    public int Quarter { get; set; }

    /// <summary>Tên kỳ đánh giá</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ngày bắt đầu</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Ngày kết thúc</summary>
    public DateTime EndDate { get; set; }
}

/// <summary>Thông tin Hồ sơ Đánh giá Cán bộ</summary>
public class EvaluationRecordDto
{
    /// <summary>Mã định danh hồ sơ đánh giá</summary>
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin dùng cho thao tác cập nhật hồ sơ.</summary>
    public uint Version { get; set; }

    /// <summary>Mã kỳ đánh giá</summary>
    public Guid PeriodId { get; set; }

    /// <summary>Tên kỳ đánh giá</summary>
    public string PeriodName { get; set; } = string.Empty;

    /// <summary>Mã cán bộ được đánh giá</summary>
    public Guid MemberId { get; set; }

    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số thẻ Đảng viên</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức vụ công tác Đảng</summary>
    public string PartyRole { get; set; } = string.Empty;

    /// <summary>Chức danh quản lý chính quyền</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Tên Chi bộ sinh hoạt Đảng</summary>
    public string? PartyCellName { get; set; }

    /// <summary>Mã Chi bộ sinh hoạt</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Tên Phòng ban chuyên môn</summary>
    public string? DepartmentName { get; set; }

    /// <summary>Khung chức danh (1 đến 4)</summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>Điểm 6 tiêu chí chung (T1 - T6)</summary>
    public double[] GeneralScores { get; set; } = Array.Empty<double>();

    /// <summary>Tổng điểm Tiêu chí chung (tối đa 30.0đ)</summary>
    public double GeneralCriteriaScore { get; set; }

    /// <summary>Tổng điểm Sản phẩm chuyên môn (tối đa 70.0đ)</summary>
    public double TasksScore { get; set; }

    /// <summary>Tổng điểm tự chấm toàn diện (tối đa 100.0đ)</summary>
    public double TotalSelfScore { get; set; }

    /// <summary>Mức xếp loại cá nhân tự đề xuất</summary>
    public string SelfProposedGrade { get; set; } = string.Empty;

    /// <summary>Ý kiến nhận xét của Chi bộ (Mẫu 10)</summary>
    public string PartyCellComment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại do Chi bộ đề xuất</summary>
    public string PartyCellProposedGrade { get; set; } = string.Empty;

    /// <summary>Kết quả bỏ phiếu kín tại Chi bộ (Mẫu 13)</summary>
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
    public int TotalVoters { get; set; }

    /// <summary>Điểm Tổ Thẩm định chấm lại (nếu có)</summary>
    public double? AppraisalScore { get; set; }

    /// <summary>Ý kiến nhận xét đối soát của Tổ Thẩm định</summary>
    public string AppraisalComment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại Tổ Thẩm định đề xuất</summary>
    public string AppraisalProposedGrade { get; set; } = string.Empty;

    /// <summary>Điểm số chính thức do Ban Thường vụ chuẩn y</summary>
    public double FinalScore { get; set; }

    /// <summary>Mức xếp loại chính thức</summary>
    public string FinalGrade { get; set; } = string.Empty;

    /// <summary>Mã trạng thái quy trình 5 bước</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Tên hiển thị trạng thái tiếng Việt</summary>
    public string StatusDisplayName { get; set; } = string.Empty;

    /// <summary>Danh sách các công việc chuyên môn (Mẫu 01 & Mẫu 02)</summary>
    public List<EvaluationTaskDto> Tasks { get; set; } = new();
}

/// <summary>Bước 1: Cán bộ đăng ký 3-7 công việc chuyên môn (Mẫu 01)</summary>
public class RegisterTasksRequestDto
{
    /// <summary>Mã kỳ đánh giá</summary>
    public Guid PeriodId { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ hiện có; bằng 0 khi tạo mới.</summary>
    public uint? Version { get; set; }

    /// <summary>Danh sách 3 đến 7 công việc chuyên môn (Tổng trọng số đúng 70.0 điểm)</summary>
    public List<TaskInputDto> Tasks { get; set; } = new();
}

/// <summary>Bước 2: Cán bộ tự chấm điểm 100đ (Mẫu 02 & Mẫu 09)</summary>
public class SubmitSelfScoreRequestDto
{
    /// <summary>Mã hồ sơ đánh giá</summary>
    public Guid RecordId { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ cần cập nhật.</summary>
    public uint? Version { get; set; }

    /// <summary>Điểm 6 tiêu chí chung T1 đến T6 (mỗi tiêu chí tối đa 5.0đ, tổng tối đa 30.0đ)</summary>
    public double[] GeneralScores { get; set; } = new double[6];

    /// <summary>Kết quả tự chấm 4 tiêu chí A-B-C-D cho từng công việc</summary>
    public List<TaskScoreInputDto> TaskScores { get; set; } = new();

    /// <summary>Mức xếp loại cá nhân tự đề xuất (HoanThanhXuatSac, HoanThanhTot, HoanThanh, KhongHoanThanh)</summary>
    public string SelfProposedGrade { get; set; } = "HoanThanhTot";
}

/// <summary>Bước 3: Chi bộ nhận xét và nhập kết quả bỏ phiếu kín (Mẫu 10, 11, 13)</summary>
public class SubmitBranchReviewRequestDto
{
    /// <summary>Mã hồ sơ đánh giá</summary>
    public Guid RecordId { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ cần cập nhật.</summary>
    public uint? Version { get; set; }

    /// <summary>Ý kiến nhận xét của Cấp ủy / Chi bộ (Mẫu 10)</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại do Chi bộ đề xuất</summary>
    public string ProposedGrade { get; set; } = "HoanThanhTot";

    /// <summary>Số phiếu Hoàn thành xuất sắc nhiệm vụ</summary>
    public int VotesExcellent { get; set; }

    /// <summary>Số phiếu Hoàn thành tốt nhiệm vụ</summary>
    public int VotesGood { get; set; }

    /// <summary>Số phiếu Hoàn thành nhiệm vụ</summary>
    public int VotesSatisfactory { get; set; }

    /// <summary>Số phiếu Không hoàn thành nhiệm vụ</summary>
    public int VotesUnsatisfactory { get; set; }

    /// <summary>Tổng số đảng viên chính thức tham gia bỏ phiếu</summary>
    public int TotalVoters { get; set; }
}

/// <summary>Chi tiết phiếu bầu cho từng đảng viên trong cuộc họp Chi bộ</summary>
public class BranchMemberVoteInputDto
{
    public Guid RecordId { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ cần cập nhật.</summary>
    public uint? Version { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ProposedGrade { get; set; } = "HoanThanhTot";
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
}

/// <summary>Yêu cầu lưu toàn bộ Biên bản kiểm phiếu của Chi bộ trong cuộc họp</summary>
public class SubmitBranchMeetingRequestDto
{
    public Guid PeriodId { get; set; }
    public Guid PartyCellId { get; set; }
    public int TotalVoters { get; set; }
    public List<BranchMemberVoteInputDto> MemberVotes { get; set; } = new();
}

/// <summary>Bước 4: Tổ Thẩm định đối soát điểm và đề xuất xếp loại (Mẫu 03 & Mẫu 15)</summary>
public class SubmitAppraisalRequestDto
{
    /// <summary>Mã hồ sơ đánh giá</summary>
    public Guid RecordId { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ cần cập nhật.</summary>
    public uint? Version { get; set; }

    /// <summary>Điểm do Tổ Thẩm định chấm lại (nếu có)</summary>
    public double? AppraisalScore { get; set; }

    /// <summary>Ý kiến đối soát của Tổ Thẩm định</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại Tổ Thẩm định đề xuất</summary>
    public string ProposedGrade { get; set; } = "HoanThanhTot";
}

/// <summary>Bước 5: Ban Thường vụ chuẩn y mức xếp loại chính thức (Mẫu 14 & Mẫu 16)</summary>
public class ApproveFinalGradeRequestDto
{
    /// <summary>Mã hồ sơ đánh giá</summary>
    public Guid RecordId { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ cần cập nhật.</summary>
    public uint? Version { get; set; }

    /// <summary>Điểm số chính thức sau cùng</summary>
    public double FinalScore { get; set; }

    /// <summary>Mức xếp loại chính thức do Ban Thường vụ chuẩn y</summary>
    public string FinalGrade { get; set; } = "HoanThanhTot";
}

/// <summary>Báo cáo kiểm soát tỷ lệ trần 20% theo từng Chi bộ (Mẫu 15)</summary>
public class BranchQuotaCheckDto
{
    /// <summary>Mã Chi bộ</summary>
    public Guid BranchId { get; set; }

    /// <summary>Tên Chi bộ</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>Tổng số cán bộ được đánh giá</summary>
    public int TotalCadres { get; set; }

    /// <summary>Số cán bộ xếp loại Hoàn thành tốt trở lên</summary>
    public int GoodOrBetterCount { get; set; }

    /// <summary>Số lượng Hoàn thành xuất sắc tối đa được phép (Trần 20%)</summary>
    public int MaxExcellentAllowed { get; set; }

    /// <summary>Số lượng Hoàn thành xuất sắc đang được đề xuất</summary>
    public int ProposedExcellentCount { get; set; }

    /// <summary>Tỷ lệ phần trăm Xuất sắc thực tế</summary>
    public double ActualExcellentPercentage { get; set; }

    /// <summary>Có vi phạm vượt quá trần 20% hay không</summary>
    public bool IsExceedingQuota { get; set; }
}

/// <summary>Dòng nội dung chi tiết của hồ sơ tập thể Mẫu 06 hoặc Mẫu 08.</summary>
public class CollectiveEvaluationItemDto
{
    public Guid Id { get; set; }
    public int ItemOrder { get; set; }
    public string Category { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string PlanOrDirection { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Hồ sơ đánh giá tập thể Mẫu 06, 07, 08.</summary>
public class CollectiveEvaluationRecordDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin của hồ sơ tập thể.</summary>
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string Form { get; set; } = string.Empty;
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? HeadId { get; set; }
    public string? HeadName { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Causes { get; set; } = string.Empty;
    public string PreviousRemediation { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public string RemediationPlan { get; set; } = string.Empty;
    public double GeneralCriteriaScore { get; set; }
    public double TaskCriteriaScore { get; set; }
    public double TotalScore { get; set; }
    public string SelfProposedGrade { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<CollectiveEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>Dữ liệu tạo/cập nhật hồ sơ tập thể.</summary>
public class SaveCollectiveEvaluationRequestDto
{
    /// <summary>Phiên bản xmin khi cập nhật hồ sơ tập thể.</summary>
    public uint? Version { get; set; }
    public Guid PeriodId { get; set; }
    public string Form { get; set; } = "M07";
    public Guid? PartyCellId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? HeadId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Causes { get; set; } = string.Empty;
    public string PreviousRemediation { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public string RemediationPlan { get; set; } = string.Empty;
    public double GeneralCriteriaScore { get; set; }
    public double TaskCriteriaScore { get; set; }
    public string SelfProposedGrade { get; set; } = "HoanThanhTot";
    public List<CollectiveEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>Thông tin hội nghị và biên bản Mẫu 12, 13.</summary>
public class EvaluationMeetingDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin của biên bản hội nghị.</summary>
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string MeetingType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int InvitedCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string AbsentReasons { get; set; } = string.Empty;
    public Guid? ChairId { get; set; }
    public string ChairName { get; set; } = string.Empty;
    public Guid? SecretaryId { get; set; }
    public string SecretaryName { get; set; } = string.Empty;
    public string MinutesContent { get; set; } = string.Empty;
    public string OutcomeContent { get; set; } = string.Empty;
    public string VoteCountingContent { get; set; } = string.Empty;
    public List<EvaluationMeetingVoteSummaryDto> VoteSummaries { get; set; } = new();
}

/// <summary>Tổng hợp phiếu theo từng hồ sơ, không lưu danh tính người bỏ phiếu.</summary>
public class EvaluationMeetingVoteSummaryDto
{
    public Guid? Id { get; set; }
    public Guid RecordId { get; set; }
    public string? FullName { get; set; }
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
    public int InvalidVotes { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Dữ liệu tạo/cập nhật biên bản hội nghị và kiểm phiếu.</summary>
public class SaveEvaluationMeetingRequestDto
{
    /// <summary>Phiên bản xmin khi cập nhật biên bản hội nghị.</summary>
    public uint? Version { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? PartyCellId { get; set; }
    public string FormCode { get; set; } = "M12";
    public string MeetingType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int InvitedCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string AbsentReasons { get; set; } = string.Empty;
    public Guid? ChairId { get; set; }
    public string ChairName { get; set; } = string.Empty;
    public Guid? SecretaryId { get; set; }
    public string SecretaryName { get; set; } = string.Empty;
    public string MinutesContent { get; set; } = string.Empty;
    public string OutcomeContent { get; set; } = string.Empty;
    public string VoteCountingContent { get; set; } = string.Empty;
    public List<EvaluationMeetingVoteSummaryDto> VoteSummaries { get; set; } = new();
}

#endregion

/// <summary>Payload đăng nhập từ client</summary>
public class LoginRequestDto
{
    /// <summary>Tên tài khoản đăng nhập</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu xác thực</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Kết quả đăng nhập — trả về thông tin cơ bản, JWT set qua HttpOnly Cookie</summary>
public class LoginResponseDto
{
    /// <summary>Mã định danh cán bộ</summary>
    public Guid Id { get; set; }

    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Tên tài khoản</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Tên các vai trò từ bản gán đang hiệu lực (chỉ để hiển thị, không dùng để phân quyền)</summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>Các mã quyền có ở ít nhất một phạm vi</summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>Quyền kèm phạm vi: <c>{ code, scopeType, scopeId, scopeName }</c></summary>
    public List<AccessGrantDto> Grants { get; set; } = new();

    /// <summary>Người dùng phải đổi mật khẩu tạm trước khi tiếp tục</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Thời điểm hết hạn của phiên làm việc</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Kết quả xác thực nội bộ chuyển từ Application Service về Controller</summary>
public class AuthResultDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public LoginResponseDto UserResponse { get; set; } = null!;
}

/// <summary>Payload đổi mật khẩu của người dùng đang đăng nhập.</summary>
public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Kết quả đặt lại mật khẩu tạm cho cán bộ.</summary>
public class ResetPasswordResponseDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}

/// <summary>Thông tin quyền hạn chi tiết trong hệ thống (Atomic Permission)</summary>
public class PermissionDto
{
    /// <summary>Mã định danh quyền hạn</summary>
    public Guid Id { get; set; }

    /// <summary>Mã quyền hạn chuẩn (ví dụ: users.read, reports.export)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị tiếng Việt của quyền</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tài nguyên áp dụng quyền (users, branches, attachments, reports, roles)</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>Hành động cho phép (read, create, update, delete, export, manage)</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết thẩm quyền</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>Thông tin vai trò kèm danh sách quyền hạn nguyên tử (Dynamic RBAC)</summary>
public class RoleDto
{
    /// <summary>Mã định danh vai trò</summary>
    public Guid Id { get; set; }

    /// <summary>Mã kỹ thuật của vai trò (không dùng để phân quyền; vai trò tạo qua API có mã sinh tự động)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị vai trò</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả thẩm quyền của vai trò</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Vai trò hệ thống mặc định không thể xóa</summary>
    public bool IsSystem { get; set; }

    /// <summary>Danh sách quyền hạn nguyên tử được cấp cho vai trò</summary>
    public List<PermissionDto> Permissions { get; set; } = new();
}

/// <summary>Yêu cầu cập nhật quyền hạn cho vai trò</summary>
public class UpdateRolePermissionsDto
{
    /// <summary>Danh sách mã quyền hạn nguyên tử cần cấp cho vai trò</summary>
    public List<string> PermissionCodes { get; set; } = new();
}

