using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Thông tin mô tả một mã quyền: dùng để seed bảng <c>permissions</c> và hiển thị trên giao diện quản trị.
/// </summary>
/// <param name="Code">Mã quyền (duy nhất, do code định nghĩa).</param>
/// <param name="Name">Tên hiển thị tiếng Việt (dùng trong thông báo 403).</param>
/// <param name="Module">Phân hệ (system, catalog, period, evaluation, collective, meeting, report, attachment).</param>
/// <param name="Description">Mô tả chi tiết.</param>
/// <param name="AppliesScope">
/// true: quyền có nghĩa theo phạm vi gán (đơn vị chính quyền / tổ chức Đảng — gồm cả cấp dưới — hoặc toàn công ty);
/// false: chỉ có nghĩa khi gán phạm vi Global (gán phạm vi khác sẽ bị API từ chối).
/// </param>
public sealed record PermissionDefinition(string Code, string Name, string Module, string Description, bool AppliesScope);

/// <summary>
/// Danh mục mã quyền chuẩn theo thiết kế phân quyền động (docs/thiet-ke/phan-quyen.md, mục 3).
/// Code chỉ kiểm tra theo mã quyền, không theo tên vai trò.
/// </summary>
public static class PermissionCodes
{
    #region Quản trị hệ thống

    /// <summary>Xem tài khoản, hồ sơ cán bộ (không gồm hồ sơ đánh giá).</summary>
    public const string SystemUsersRead = "system.users.read";

    /// <summary>Quản lý tài khoản: tạo, sửa, khóa/mở, xóa, đặt lại mật khẩu, mở khóa đăng nhập.</summary>
    public const string SystemUsersManage = "system.users.manage";

    /// <summary>Quản lý vai trò: tạo/sửa/xóa vai trò, chọn quyền cho vai trò.</summary>
    public const string SystemRolesManage = "system.roles.manage";

    /// <summary>Gán/thu hồi vai trò kèm phạm vi, thời hạn.</summary>
    public const string SystemAssignmentsManage = "system.assignments.manage";

    /// <summary>Xem nhật ký thao tác và nhật ký đăng nhập.</summary>
    public const string SystemAuditRead = "system.audit.read";

    /// <summary>Nhập dữ liệu (phải có thêm quyền quản lý loại dữ liệu được nhập).</summary>
    public const string SystemImport = "system.import";

    #endregion

    #region Danh mục, kỳ đánh giá

    /// <summary>Quản lý danh mục đơn vị chính quyền, tổ chức Đảng, loại đơn vị, chức vụ. Xem danh mục: mọi người đã đăng nhập.</summary>
    public const string CatalogManage = "catalog.manage";

    /// <summary>Quản lý kỳ đánh giá: tạo kỳ, cấu hình bước/thời hạn/tham số, danh sách người được đánh giá, mở/khóa kỳ.</summary>
    public const string PeriodManage = "period.manage";

    #endregion

    #region Đánh giá cá nhân

    /// <summary>Tham gia đánh giá (bản thân) — chỉ trên hồ sơ của chính mình.</summary>
    public const string EvaluationSelf = "evaluation.self";

    /// <summary>Xem hồ sơ đánh giá. Chủ hồ sơ luôn xem được hồ sơ của mình.</summary>
    public const string EvaluationRead = "evaluation.read";

    /// <summary>Duyệt danh mục sản phẩm (B1).</summary>
    public const string EvaluationTasksApprove = "evaluation.tasks.approve";

    /// <summary>Chi bộ xác nhận phiếu tự chấm (B2).</summary>
    public const string EvaluationCellConfirm = "evaluation.cell.confirm";

    /// <summary>Ghi nhận đề xuất của tập thể lãnh đạo (B3a).</summary>
    public const string EvaluationCollectiveRecord = "evaluation.collective.record";

    /// <summary>Thẩm định (B3b).</summary>
    public const string EvaluationAppraise = "evaluation.appraise";

    /// <summary>Nhận xét, đề xuất của cấp trực tiếp sử dụng (B3c).</summary>
    public const string EvaluationDirectorReview = "evaluation.director.review";

    /// <summary>
    /// Lãnh đạo đơn vị (Trưởng phòng) đề xuất mức — dùng làm quyền thực hiện B3c trong hồ sơ luồng
    /// "Bí thư/Phó bí thư Chi bộ là nhân viên" (HD03 PL III ví dụ 3).
    /// </summary>
    public const string EvaluationUnitReview = "evaluation.unit.review";

    /// <summary>Ghi nhận quyết định của Đảng ủy cơ sở (B4) — quyền mặc định của B4 khi hồ sơ luồng đặt bước này làm trong hệ thống.</summary>
    public const string EvaluationDecide = "evaluation.decide";

    /// <summary>
    /// Ghi nhận kết quả của bước do cấp trên / cơ quan ngoài hệ thống thực hiện (chế độ "Cấp trên thực hiện" trong hồ sơ luồng):
    /// cơ quan, số/ngày văn bản, nhận xét, mức đề xuất/quyết định, tệp đính kèm.
    /// </summary>
    public const string EvaluationExternalRecord = "evaluation.external.record";

    /// <summary>Công bố, khóa kết quả (B5).</summary>
    public const string EvaluationPublish = "evaluation.publish";

    /// <summary>Mở lại hồ sơ đã khóa để đính chính (bắt buộc lý do).</summary>
    public const string EvaluationReopen = "evaluation.reopen";

    #endregion

    #region Tập thể, hội nghị, báo cáo, văn bản

    /// <summary>Lập hồ sơ tự đánh giá tập thể (Mẫu 06–08).</summary>
    public const string CollectiveManage = "collective.manage";

    /// <summary>Xem biên bản hội nghị, kiểm phiếu (Mẫu 12–13).</summary>
    public const string MeetingRead = "meeting.read";

    /// <summary>Lập biên bản hội nghị, kiểm phiếu.</summary>
    public const string MeetingManage = "meeting.manage";

    /// <summary>Xuất báo cáo tổng hợp (Mẫu 14–16, danh sách cán bộ) — lọc theo phạm vi.</summary>
    public const string ReportExport = "report.export";

    /// <summary>Quản lý văn bản chung (tài liệu hướng dẫn, biểu mẫu trống; FormCode = GENERAL).</summary>
    public const string AttachmentGeneralManage = "attachment.general.manage";

    #endregion

    /// <summary>Metadata của toàn bộ mã quyền, theo thứ tự hiển thị (SortOrder = chỉ số trong danh sách).</summary>
    public static readonly IReadOnlyList<PermissionDefinition> Definitions = new PermissionDefinition[]
    {
        new(SystemUsersRead, "Xem tài khoản, hồ sơ cán bộ", "system", "Xem danh sách tài khoản và hồ sơ cán bộ; không gồm hồ sơ đánh giá.", true),
        new(SystemUsersManage, "Quản lý tài khoản", "system", "Tạo, sửa, khóa/mở, xóa tài khoản, đặt lại mật khẩu, mở khóa đăng nhập.", true),
        new(SystemRolesManage, "Quản lý vai trò", "system", "Tạo/sửa/xóa vai trò, chọn quyền cho vai trò.", false),
        new(SystemAssignmentsManage, "Gán vai trò", "system", "Gán/thu hồi vai trò cho người dùng kèm phạm vi và thời hạn.", false),
        new(SystemAuditRead, "Xem nhật ký", "system", "Xem nhật ký thao tác và nhật ký đăng nhập.", false),
        new(SystemImport, "Nhập dữ liệu", "system", "Nhập dữ liệu từ tệp; cần thêm quyền quản lý loại dữ liệu được nhập.", false),
        new(CatalogManage, "Quản lý danh mục", "catalog", "Quản lý danh mục đơn vị chính quyền, tổ chức Đảng, loại đơn vị, chức vụ. Xem danh mục: mọi người đã đăng nhập.", false),
        new(PeriodManage, "Quản lý kỳ đánh giá", "period", "Tạo kỳ, cấu hình bước/thời hạn/tham số, danh sách người được đánh giá, mở/khóa kỳ.", false),
        new(EvaluationSelf, "Tham gia đánh giá (bản thân)", "evaluation", "Đăng ký sản phẩm, tự chấm, giải trình, nộp minh chứng — chỉ trên hồ sơ của mình (phạm vi gán được bỏ qua).", true),
        new(EvaluationRead, "Xem hồ sơ đánh giá", "evaluation", "Xem hồ sơ đánh giá trong phạm vi được gán; chủ hồ sơ luôn xem được hồ sơ của mình.", true),
        new(EvaluationTasksApprove, "Duyệt danh mục sản phẩm", "evaluation", "Duyệt danh mục sản phẩm đăng ký đầu kỳ (bước 1).", true),
        new(EvaluationCellConfirm, "Chi bộ xác nhận phiếu tự chấm", "evaluation", "Chi bộ xác nhận phiếu tự chấm điểm (bước 2).", true),
        new(EvaluationCollectiveRecord, "Ghi nhận đề xuất của tập thể lãnh đạo", "evaluation", "Ghi kết quả kiểm phiếu đề xuất của tập thể lãnh đạo (bước 3a), không ghi phiếu từng người.", true),
        new(EvaluationAppraise, "Thẩm định", "evaluation", "Thẩm định hồ sơ đánh giá (bước 3b).", true),
        new(EvaluationDirectorReview, "Nhận xét của cấp trực tiếp sử dụng", "evaluation", "Nhận xét, đề xuất của cấp trực tiếp sử dụng cán bộ (bước 3c).", true),
        new(EvaluationUnitReview, "Lãnh đạo đơn vị đề xuất", "evaluation", "Trưởng phòng (lãnh đạo đơn vị) đề xuất mức xếp loại thay cấp trực tiếp sử dụng — dùng làm quyền thực hiện bước 3c trong hồ sơ luồng được cấu hình (HD03 PL III ví dụ 3).", true),
        new(EvaluationDecide, "Ghi nhận quyết định của Đảng ủy cơ sở", "evaluation", "Ghi nhận quyết định xếp loại của Đảng ủy cơ sở (bước 4) khi hồ sơ luồng đặt bước này làm trong hệ thống; bước do cấp trên quyết định dùng quyền Ghi nhận kết quả của cấp trên.", true),
        new(EvaluationExternalRecord, "Ghi nhận kết quả của cấp trên", "evaluation", "Ghi nhận kết quả của bước do cấp trên / cơ quan ngoài hệ thống thực hiện (thẩm định, nhận xét, quyết định…): cơ quan, số/ngày văn bản, nhận xét, mức, tệp đính kèm.", true),
        new(EvaluationPublish, "Công bố, khóa kết quả", "evaluation", "Công bố và khóa kết quả đánh giá (bước 5).", true),
        new(EvaluationReopen, "Mở lại hồ sơ đã khóa", "evaluation", "Mở lại hồ sơ đã khóa để đính chính; bắt buộc ghi lý do.", true),
        new(CollectiveManage, "Lập hồ sơ tự đánh giá tập thể", "collective", "Lập hồ sơ tự đánh giá của tập thể (Mẫu 06–08).", true),
        new(MeetingRead, "Xem biên bản hội nghị", "meeting", "Xem biên bản hội nghị, biên bản kiểm phiếu (Mẫu 12–13).", true),
        new(MeetingManage, "Lập biên bản hội nghị", "meeting", "Lập biên bản hội nghị, biên bản kiểm phiếu.", true),
        new(ReportExport, "Xuất báo cáo tổng hợp", "report", "Xuất báo cáo tổng hợp (Mẫu 14–16, danh sách cán bộ), lọc theo phạm vi.", true),
        new(AttachmentGeneralManage, "Quản lý văn bản chung", "attachment", "Quản lý tài liệu hướng dẫn, biểu mẫu trống dùng chung (FormCode = GENERAL).", false),
    };

    /// <summary>Tất cả mã quyền chuẩn.</summary>
    public static readonly string[] All = Definitions.Select(d => d.Code).ToArray();

    private static readonly Dictionary<string, PermissionDefinition> ByCode =
        Definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);

    /// <summary>Mã có thuộc danh mục chuẩn hay không.</summary>
    public static bool IsDefined(string code) => ByCode.ContainsKey(code);

    /// <summary>Tra metadata theo mã; null nếu mã không thuộc danh mục chuẩn.</summary>
    public static PermissionDefinition? Find(string code) => ByCode.TryGetValue(code, out var d) ? d : null;

    /// <summary>Tên hiển thị của quyền (dùng cho thông báo lỗi); trả lại chính mã nếu không tìm thấy.</summary>
    public static string DisplayName(string code) => Find(code)?.Name ?? code;
}
