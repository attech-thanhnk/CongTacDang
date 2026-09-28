namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Định nghĩa danh mục Quyền hạn nguyên tử (Atomic Permissions) chuẩn hóa toàn hệ thống theo 03-HD/TVĐU
/// </summary>
[System.Obsolete("Mã quyền cũ — dùng PermissionCodes. Chỉ còn cho file ngoài phạm vi task 09 (UserController, OrganizationController, AuditController); sẽ xóa khi tích hợp.")]
public static class AppPermissions
{
    #region Phân hệ Hồ sơ Cán bộ (Users)

    /// <summary>Quyền xem danh sách và chi tiết hồ sơ cán bộ</summary>
    public const string UsersRead = "users.read";

    /// <summary>Quyền thêm mới hồ sơ cán bộ</summary>
    public const string UsersCreate = "users.create";

    /// <summary>Quyền chỉnh sửa thông tin hồ sơ cán bộ</summary>
    public const string UsersUpdate = "users.update";

    /// <summary>Quyền xóa hồ sơ cán bộ</summary>
    public const string UsersDelete = "users.delete";

    #endregion

    #region Phân hệ Tổ chức Chi bộ (Branches)

    /// <summary>Quyền xem danh sách và chi tiết Chi bộ</summary>
    public const string BranchesRead = "branches.read";

    /// <summary>Quyền thành lập Chi bộ mới</summary>
    public const string BranchesCreate = "branches.create";

    /// <summary>Quyền cập nhật thông tin Chi bộ</summary>
    public const string BranchesUpdate = "branches.update";

    /// <summary>Quyền giải thể / xóa Chi bộ</summary>
    public const string BranchesDelete = "branches.delete";

    #endregion

    #region Phân hệ Tệp đính kèm & Minh chứng (Attachments)

    /// <summary>Quyền xem danh mục và tải về tệp tin đính kèm</summary>
    public const string AttachmentsRead = "attachments.read";

    /// <summary>Quyền tải lên tệp tin và hồ sơ minh chứng mới</summary>
    public const string AttachmentsUpload = "attachments.upload";

    /// <summary>Quyền xóa tệp tin khỏi hệ thống</summary>
    public const string AttachmentsDelete = "attachments.delete";

    #endregion

    #region Phân hệ Đánh giá & Xếp loại Cán bộ 5 Bước (Evaluations - 03-HD/TVĐU)

    /// <summary>Quyền xem hồ sơ đánh giá và tiến trình 5 bước</summary>
    public const string EvaluationsRead = "evaluations.read";

    /// <summary>Quyền đăng ký danh mục 3-7 công việc chuyên môn đầu quý (Mẫu 01)</summary>
    public const string EvaluationsRegister = "evaluations.register";

    /// <summary>Quyền tự chấm điểm 30đ chung và 70đ chuyên môn cuối quý (Mẫu 02 & 09)</summary>
    public const string EvaluationsSelfScore = "evaluations.self_score";

    /// <summary>Quyền Chi bộ nhận xét và ghi nhận kết quả bỏ phiếu kín (Mẫu 10 & 11/13)</summary>
    public const string EvaluationsBranchVote = "evaluations.branch_vote";

    /// <summary>Quyền Tổ Thẩm định đối soát điểm và kiểm soát tỷ lệ trần 20% (Mẫu 03 & 15)</summary>
    public const string EvaluationsAppraise = "evaluations.appraise";

    /// <summary>Quyền Ban Thường vụ phê duyệt chính thức mức xếp loại cán bộ (Mẫu 14 & 16)</summary>
    public const string EvaluationsApprove = "evaluations.approve";

    #endregion

    #region Phân hệ Báo cáo & Xuất dữ liệu (Reports)

    /// <summary>Quyền kết xuất dữ liệu và tải báo cáo bảng tính Excel</summary>
    public const string ReportsExport = "reports.export";

    #endregion

    #region Phân hệ Quản trị Phân quyền động (Roles)

    /// <summary>Quyền quản trị ma trận phân quyền, vai trò và gán quyền (Dynamic RBAC)</summary>
    public const string RolesManage = "roles.manage";

    #endregion

    #region Composite Policies (Phân quyền bảo vệ đa cấp)

    /// <summary>Chính sách thẩm định hoặc chuẩn y (Tổ thẩm định hoặc Ban Thường vụ)</summary>
    public const string PolicyEvaluationsAppraiseOrApprove = "Policy_Evaluations_AppraiseOrApprove";

    /// <summary>Chính sách xem/đánh giá hồ sơ Chi bộ (Chi bộ, Tổ thẩm định, hoặc Ban Thường vụ)</summary>
    public const string PolicyEvaluationsBranchView = "Policy_Evaluations_BranchView";

    /// <summary>Chính sách quản lý cấu hình kỳ đánh giá (Quản trị hệ thống hoặc Ban Thường vụ)</summary>
    public const string PolicyManagePeriods = "Policy_Manage_Periods";

    #endregion

    /// <summary>Tất cả mã quyền hạn nguyên tử trong hệ thống</summary>
    public static readonly string[] All =
    {
        UsersRead,
        UsersCreate,
        UsersUpdate,
        UsersDelete,
        BranchesRead,
        BranchesCreate,
        BranchesUpdate,
        BranchesDelete,
        AttachmentsRead,
        AttachmentsUpload,
        AttachmentsDelete,
        EvaluationsRead,
        EvaluationsRegister,
        EvaluationsSelfScore,
        EvaluationsBranchVote,
        EvaluationsAppraise,
        EvaluationsApprove,
        ReportsExport,
        RolesManage
    };
}
