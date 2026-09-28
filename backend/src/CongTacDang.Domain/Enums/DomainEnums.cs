namespace CongTacDang.Domain.Enums;

/// <summary>
/// Chức vụ công tác Đảng trong hệ thống tổ chức Đảng bộ
/// </summary>
public enum PartyRole
{
    /// <summary>Đảng viên</summary>
    DangVien = 1,

    /// <summary>Chi ủy viên</summary>
    ChiUyVien = 2,

    /// <summary>Phó Bí thư Chi bộ</summary>
    PhoBiThuChiBo = 3,

    /// <summary>Bí thư Chi bộ</summary>
    BiThuChiBo = 4,

    /// <summary>Đảng ủy viên</summary>
    DangUyVien = 5,

    /// <summary>Ủy viên Ban Thường vụ Đảng ủy</summary>
    UyVienBanThuongVu = 6,

    /// <summary>Phó Bí thư Đảng ủy</summary>
    PhoBiThuDangUy = 7,

    /// <summary>Bí thư Đảng ủy</summary>
    BiThuDangUy = 8
}

/// <summary>
/// Chức danh quản lý, chuyên môn trong chính quyền Công ty ATTECH
/// </summary>
public enum AdministrativePosition
{
    /// <summary>Chuyên viên / Kỹ sư</summary>
    ChuyenVien = 1,

    /// <summary>Phó Trưởng phòng</summary>
    PhoTruongPhong = 2,

    /// <summary>Trưởng phòng</summary>
    TruongPhong = 3,

    /// <summary>Phó Quản đốc phân xưởng</summary>
    PhoQuanDoc = 4,

    /// <summary>Quản đốc phân xưởng</summary>
    QuanDoc = 5,

    /// <summary>Phó Giám đốc công ty</summary>
    PhoGiamDoc = 6,

    /// <summary>Giám đốc công ty</summary>
    GiamDoc = 7,

    /// <summary>Chủ tịch công ty</summary>
    ChuTichCongTy = 8,

    /// <summary>Kiểm soát viên</summary>
    KiemSoatVien = 9
}

/// <summary>
/// Khung chức danh và cơ cấu trọng số tiêu chí (A-B-C-D) theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public enum JobGroup
{
    /// <summary>Khung 1: Quản lý, tham mưu, công tác Đảng, đoàn thể (Trọng số 25% - 35% - 20% - 20%)</summary>
    Khung1_QuanLyDangDoanThe = 1,

    /// <summary>Khung 2: An toàn, tuân thủ, khai thác, kỹ thuật (Trọng số 15% - 50% - 15% - 20%)</summary>
    Khung2_AnToanKyThuat = 2,

    /// <summary>Khung 3: Dự án, đầu tư, tài chính (Trọng số 20% - 30% - 35% - 15%)</summary>
    Khung3_DuAnDauTu = 3,

    /// <summary>Khung 4: KHCN, đổi mới sáng tạo, chuyển đổi số (Trọng số 15% - 30% - 20% - 35%)</summary>
    Khung4_KhcnChuyenDoiSo = 4
}

/// <summary>
/// Trạng thái của kỳ đánh giá theo các mốc thời gian quy trình 5 bước
/// </summary>
public enum PeriodStatus
{
    /// <summary>Dự thảo khởi tạo kỳ đánh giá</summary>
    Draft = 0,

    /// <summary>Đang mở cho cán bộ đăng ký nhiệm vụ đầu quý (Mẫu 01)</summary>
    TaskRegistration = 1,

    /// <summary>Đang mở cho cán bộ tự chấm điểm cuối quý (Mẫu 02 & 09)</summary>
    SelfEvaluation = 2,

    /// <summary>Chi bộ đang tổ chức hội nghị đánh giá và bỏ phiếu kín (Mẫu 11)</summary>
    BranchReview = 3,

    /// <summary>Tổ Thẩm định đang đối soát và kiểm tra trần 20% (Mẫu 03 & 15)</summary>
    Appraisal = 4,

    /// <summary>Ban Thường vụ đã phê duyệt chính thức (Mẫu 14 & 16)</summary>
    Completed = 5
}

/// <summary>
/// Kỳ đánh giá theo các quý trong năm
/// </summary>
public enum EvaluationQuarter
{
    /// <summary>Quý I</summary>
    Quy1 = 1,

    /// <summary>Quý II</summary>
    Quy2 = 2,

    /// <summary>Quý III</summary>
    Quy3 = 3,

    /// <summary>Quý IV</summary>
    Quy4 = 4
}

/// <summary>Nhóm biểu mẫu đánh giá tập thể theo Hướng dẫn 03-HD/TVĐU.</summary>
public enum CollectiveEvaluationForm
{
    /// <summary>Báo cáo kết quả tập thể/lĩnh vực phụ trách liên kết trách nhiệm cán bộ.</summary>
    M06 = 6,

    /// <summary>Báo cáo tự đánh giá, xếp loại của tập thể Đảng ủy, Chi ủy, Chi bộ.</summary>
    M07 = 7,

    /// <summary>Báo cáo tổng hợp kết quả thực hiện nhiệm vụ của cơ quan, đơn vị.</summary>
    M08 = 8
}

/// <summary>
/// Mức xếp loại chất lượng cán bộ lãnh đạo, quản lý
/// </summary>
public enum EvaluationGrade
{
    /// <summary>Chưa xếp loại</summary>
    ChuaXepLoai = 0,

    /// <summary>Hoàn thành xuất sắc nhiệm vụ (Tối đa 20% số hoàn thành tốt)</summary>
    HoanThanhXuatSac = 1,

    /// <summary>Hoàn thành tốt nhiệm vụ</summary>
    HoanThanhTot = 2,

    /// <summary>Hoàn thành nhiệm vụ</summary>
    HoanThanh = 3,

    /// <summary>Không hoàn thành nhiệm vụ</summary>
    KhongHoanThanh = 4
}

/// <summary>
/// Trạng thái hồ sơ trong quy trình đánh giá 5 bước theo 03-HD/TVĐU
/// </summary>
public enum RecordStatus
{
    /// <summary>Bản nháp đăng ký Mẫu 01</summary>
    Draft = 0,

    /// <summary>Đã gửi đăng ký nhiệm vụ đầu quý</summary>
    TasksSubmitted = 1,

    /// <summary>Cấp ủy / Lãnh đạo đã duyệt nhiệm vụ</summary>
    TasksApproved = 2,

    /// <summary>Cá nhân cán bộ đã tự chấm điểm Mẫu 02/09</summary>
    SelfEvaluated = 3,

    /// <summary>Tập thể đã họp nhận xét và bỏ phiếu tín nhiệm</summary>
    Voted = 4,

    /// <summary>Tổ Thẩm định Đảng ủy đã thẩm định hồ sơ và kiểm tra trần 20%</summary>
    Reviewed = 5,

    /// <summary>Ban Thường vụ Đảng ủy đã phê duyệt kết quả chính thức</summary>
    Approved = 6,

    /// <summary>Đã công bố kết quả đánh giá, xếp loại</summary>
    Published = 7
}

/// <summary>
/// 6 Trục nhiệm vụ chiến lược (T1 - T6) theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public enum TaskResultAxis
{
    /// <summary>T1: Nhiệm vụ chính trị, SXKD và dịch vụ bảo đảm hoạt động bay</summary>
    T1 = 1,

    /// <summary>T2: Thể chế, phân cấp, kiểm tra, kiểm soát và giám sát</summary>
    T2 = 2,

    /// <summary>T3: KHCN, đổi mới sáng tạo và chuyển đổi số</summary>
    T3 = 3,

    /// <summary>T4: Xây dựng Đảng và hệ thống chính trị</summary>
    T4 = 4,

    /// <summary>T5: Văn hóa doanh nghiệp, con người và an sinh người lao động</summary>
    T5 = 5,

    /// <summary>T6: Quốc phòng, an ninh, đối ngoại và hợp tác quốc tế</summary>
    T6 = 6
}
