namespace CongTacDang.Domain.Enums;

/// <summary>Bên của loại đơn vị tổ chức (<c>OrgUnitType</c>).</summary>
public enum OrgSide
{
    /// <summary>Bảng <c>party_cells</c>.</summary>
    Party = 1,

    /// <summary>Bảng <c>administrative_departments</c>.</summary>
    Administrative = 2
}

public enum PositionSide
{
    Party = 1,

    Administrative = 2,

    /// <summary>Công đoàn, Đoàn Thanh niên, Hội…</summary>
    MassOrganization = 3,

    Other = 4
}

/// <summary>Chỉ tiến <c>Draft → Open → Locked → Closed</c>, trừ <c>Locked → Open</c> (có lý do).</summary>
public enum PeriodStatus
{
    /// <summary>Dự thảo: cấu hình kỳ, chưa ai thao tác hồ sơ.</summary>
    Draft = 0,

    Open = 1,

    /// <summary>Khóa dữ liệu: chỉ các bước từ thẩm định trở đi được thao tác.</summary>
    Locked = 2,

    /// <summary>Mọi hồ sơ đã công bố; chỉ còn mở lại hồ sơ.</summary>
    Closed = 3
}

/// <summary>Hồ sơ tập thể (Mẫu 06–08), không theo luồng 9 bước cá nhân.</summary>
public enum CollectiveRecordStatus
{
    Draft = 0,

    Submitted = 1
}

public enum EvaluationQuarter
{
    Quy1 = 1,

    Quy2 = 2,

    Quy3 = 3,

    Quy4 = 4
}

public enum CollectiveEvaluationForm
{
    /// <summary>Kết quả tập thể/lĩnh vực phụ trách liên kết trách nhiệm cán bộ.</summary>
    M06 = 6,

    /// <summary>Tự đánh giá, xếp loại của tập thể Đảng ủy, Chi ủy, Chi bộ.</summary>
    M07 = 7,

    /// <summary>Tổng hợp kết quả thực hiện nhiệm vụ của cơ quan, đơn vị.</summary>
    M08 = 8
}

public enum TaskKind
{
    /// <summary>Trục chính, trọng số lớn.</summary>
    TrongTam = 1,

    /// <summary>Thường xuyên / định kỳ.</summary>
    ThuongXuyen = 2,

    /// <summary>Phát sinh trong kỳ (Mẫu 05); không chọn khi đăng ký đầu kỳ.</summary>
    DotXuat = 3
}

public enum TaskRole
{
    ChuTri = 1,

    PhoiHop = 2
}

public enum EvaluationGrade
{
    ChuaXepLoai = 0,

    /// <summary>Bị giới hạn bởi trần tỷ lệ Xuất sắc của bộ tiêu chí.</summary>
    HoanThanhXuatSac = 1,

    HoanThanhTot = 2,

    HoanThanh = 3,

    KhongHoanThanh = 4
}

/// <summary>Trạng thái hồ sơ = bước đang chờ.</summary>
public enum RecordStatus
{
    AwaitingRegistration = 1,

    AwaitingTaskApproval = 2,

    AwaitingSelfScore = 3,

    AwaitingCellConfirm = 4,

    AwaitingCollective = 5,

    AwaitingAppraisal = 6,

    AwaitingDirectorReview = 7,

    AwaitingDecision = 8,

    AwaitingPublish = 9,

    /// <summary>Đã khóa; mọi sửa đổi phải qua mở lại.</summary>
    Published = 10
}

/// <summary>Nháp → Đã xuất bản (bất biến) → Lưu trữ.</summary>
public enum CriteriaSetStatus
{
    /// <summary>Sửa, xóa được; kỳ chưa chọn được.</summary>
    Draft = 0,

    /// <summary>Không sửa được, chỉ nhân bản thành nháp mới; kỳ dự thảo chọn được.</summary>
    Published = 1,

    /// <summary>Kỳ đã dùng vẫn giữ ảnh chụp; không chọn được cho kỳ mới.</summary>
    Archived = 2
}
