namespace CongTacDang.Domain.Enums;

public enum PartyRole
{
    DangVien = 1,
    ChiUyVien = 2,
    PhoBiThuChiBo = 3,
    BiThuChiBo = 4,
    DangUyVien = 5,
    UyVienBanThuongVu = 6,
    PhoBiThuDangUy = 7,
    BiThuDangUy = 8
}

public enum AdministrativePosition
{
    ChuyenVien = 1,
    PhoTruongPhong = 2,
    TruongPhong = 3,
    PhoQuanDoc = 4,
    QuanDoc = 5,
    PhoGiamDoc = 6,
    GiamDoc = 7,
    ChuTichCongTy = 8,
    KiemSoatVien = 9
}

public enum JobGroup
{
    // Khung 1: Quản lý, tham mưu, công tác Đảng, đoàn thể (25% - 35% - 20% - 20%)
    Khung1_QuanLyDangDoanThe = 1,
    // Khung 2: An toàn, tuân thủ, khai thác, kỹ thuật (15% - 50% - 15% - 20%)
    Khung2_AnToanKyThuat = 2,
    // Khung 3: Dự án, đầu tư, tài chính (20% - 30% - 35% - 15%)
    Khung3_DuAnDauTu = 3,
    // Khung 4: KHCN, đổi mới sáng tạo, chuyển đổi số (15% - 30% - 20% - 35%)
    Khung4_KhcnChuyenDoiSo = 4
}

public enum EvaluationQuarter
{
    Quy1 = 1,
    Quy2 = 2,
    Quy3 = 3,
    Quy4 = 4
}

public enum EvaluationGrade
{
    ChuaXepLoai = 0,
    HoanThanhXuatSac = 1,
    HoanThanhTot = 2,
    HoanThanh = 3,
    KhongHoanThanh = 4
}

public enum RecordStatus
{
    Draft = 0,             // Bản nháp đăng ký Mẫu 01
    TasksSubmitted = 1,    // Đã gửi đăng ký nhiệm vụ
    TasksApproved = 2,     // Cấp ủy / Lãnh đạo đã duyệt nhiệm vụ Mẫu 01
    SelfEvaluated = 3,     // Cá nhân đã tự chấm điểm Mẫu 02
    Voted = 4,             // Tập thể đã họp và bỏ phiếu kín Mẫu 11
    Reviewed = 5,          // Ban TC Đảng ủy đã thẩm định & kiểm tra trần 20%
    Approved = 6,          // BTV Đảng ủy ATTECH đã phê duyệt chính thức
    Published = 7          // Đã công bố kết quả
}

public enum TaskResultAxis
{
    T1 = 1, // Nhiệm vụ chính trị, SXKD và dịch vụ bảo đảm bay
    T2 = 2, // Thể chế, phân cấp, kiểm tra, kiểm soát và giám sát
    T3 = 3, // KHCN, đổi mới sáng tạo và chuyển đổi số
    T4 = 4, // Xây dựng Đảng và hệ thống chính trị
    T5 = 5, // Văn hóa, con người và an sinh người lao động
    T6 = 6  // Quốc phòng, an ninh, đối ngoại và hợp tác quốc tế
}
