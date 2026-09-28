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
/// Trạng thái kỳ đánh giá (docs/thiet-ke/luong-danh-gia.md mục 3.1): <c>Draft → Open → Locked → Closed</c>,
/// chỉ tiến, trừ <c>Locked → Open</c> (có lý do).
/// </summary>
public enum PeriodStatus
{
    /// <summary>Dự thảo: cấu hình bước, tham số, danh sách người được đánh giá; chưa ai thao tác hồ sơ.</summary>
    Draft = 0,

    /// <summary>Đang mở: các bước chạy theo trạng thái từng hồ sơ.</summary>
    Open = 1,

    /// <summary>Khóa dữ liệu (HD03 II.2): chỉ các bước từ thẩm định trở đi được thao tác; chủ hồ sơ không sửa được.</summary>
    Locked = 2,

    /// <summary>Đã đóng: toàn bộ hồ sơ đã công bố; chỉ còn mở lại hồ sơ.</summary>
    Closed = 3
}

/// <summary>Trạng thái hồ sơ tự đánh giá của tập thể (Mẫu 06–08) — không thuộc luồng 9 bước của hồ sơ cá nhân.</summary>
public enum CollectiveRecordStatus
{
    /// <summary>Bản nháp.</summary>
    Draft = 0,

    /// <summary>Đã lập.</summary>
    Submitted = 1
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
/// Trạng thái hồ sơ cá nhân = <b>bước đang chờ</b> (docs/thiet-ke/luong-danh-gia.md mục 2).
/// </summary>
public enum RecordStatus
{
    /// <summary>Chờ cá nhân đăng ký sản phẩm (B1_REGISTER).</summary>
    AwaitingRegistration = 1,

    /// <summary>Chờ duyệt danh mục (B1_APPROVE).</summary>
    AwaitingTaskApproval = 2,

    /// <summary>Chờ tự chấm (B2_SELF_SCORE).</summary>
    AwaitingSelfScore = 3,

    /// <summary>Chờ Chi bộ xác nhận (B2_CELL_CONFIRM).</summary>
    AwaitingCellConfirm = 4,

    /// <summary>Chờ ghi nhận đề xuất tập thể (B3A_COLLECTIVE).</summary>
    AwaitingCollective = 5,

    /// <summary>Chờ thẩm định (B3B_APPRAISAL).</summary>
    AwaitingAppraisal = 6,

    /// <summary>Chờ cấp trực tiếp sử dụng (B3C_DIRECTOR).</summary>
    AwaitingDirectorReview = 7,

    /// <summary>Chờ quyết định (B4_DECISION).</summary>
    AwaitingDecision = 8,

    /// <summary>Chờ công bố (B5_PUBLISH).</summary>
    AwaitingPublish = 9,

    /// <summary>Đã công bố — khóa, mọi sửa đổi phải qua mở lại.</summary>
    Published = 10
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
