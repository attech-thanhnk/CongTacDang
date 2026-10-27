using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Common.Security;

/// <summary>Dùng để seed bảng <c>permissions</c> và hiển thị trên giao diện quản trị.</summary>
/// <param name="Name">Tên hiển thị, dùng cả trong thông báo 403.</param>
/// <param name="AppliesScope">false: chỉ gán được ở phạm vi Global (API từ chối phạm vi khác).</param>
public sealed record PermissionDefinition(string Code, string Name, string Module, string Description, bool AppliesScope);

/// <summary>Danh mục mã quyền chuẩn; code chỉ kiểm tra theo mã quyền, không theo tên vai trò.</summary>
public static class PermissionCodes
{
    #region Quản trị hệ thống

    public const string SystemPrefix = "system.";

    /// <summary>Không gồm hồ sơ đánh giá.</summary>
    public const string SystemUsersRead = "system.users.read";

    public const string SystemUsersManage = "system.users.manage";

    public const string SystemRolesManage = "system.roles.manage";

    public const string SystemAssignmentsManage = "system.assignments.manage";

    public const string SystemAuditRead = "system.audit.read";

    /// <summary>Xem thông tin đơn vị: mọi người đã đăng nhập.</summary>
    public const string SystemSettingsManage = "system.settings.manage";

    public const string SystemTemplatesManage = "system.templates.manage";

    /// <summary>Chỉ mở chức năng; mỗi loại nhập còn đòi quyền quản lý dữ liệu tương ứng.</summary>
    public const string SystemImport = "system.import";

    #endregion

    #region Danh mục, kỳ đánh giá

    /// <summary>Xem danh mục: mọi người đã đăng nhập.</summary>
    public const string CatalogManage = "catalog.manage";

    public const string PeriodManage = "period.manage";

    public const string CriteriaManage = "criteria.manage";

    #endregion

    #region Đánh giá cá nhân

    /// <summary>Chỉ trên hồ sơ của chính mình.</summary>
    public const string EvaluationSelf = "evaluation.self";

    /// <summary>Chủ hồ sơ luôn xem được hồ sơ của mình.</summary>
    public const string EvaluationRead = "evaluation.read";

    /// <summary>B1.</summary>
    public const string EvaluationTasksApprove = "evaluation.tasks.approve";

    /// <summary>B2.</summary>
    public const string EvaluationCellConfirm = "evaluation.cell.confirm";

    /// <summary>B3a.</summary>
    public const string EvaluationCollectiveRecord = "evaluation.collective.record";

    /// <summary>B3b.</summary>
    public const string EvaluationAppraise = "evaluation.appraise";

    /// <summary>B3c.</summary>
    public const string EvaluationDirectorReview = "evaluation.director.review";

    /// <summary>Trưởng phòng đề xuất mức — quyền thực hiện B3c khi Bí thư/Phó bí thư Chi bộ là nhân viên.</summary>
    public const string EvaluationUnitReview = "evaluation.unit.review";

    /// <summary>B4 khi diện quyết định đặt bước này làm trong hệ thống.</summary>
    public const string EvaluationDecide = "evaluation.decide";

    /// <summary>Ghi nhận kết quả bước ở chế độ "Cấp trên thực hiện".</summary>
    public const string EvaluationExternalRecord = "evaluation.external.record";

    /// <summary>B5.</summary>
    public const string EvaluationPublish = "evaluation.publish";

    /// <summary>Bắt buộc lý do.</summary>
    public const string EvaluationReopen = "evaluation.reopen";

    #endregion

    #region Sau công bố

    /// <summary>Chỉ danh sách mức (điểm khi bộ tiêu chí cho phép), không chi tiết hồ sơ; phạm vi công khai = phạm vi bản gán.</summary>
    public const string EvaluationResultsView = "evaluation.results.view";

    /// <summary>Chỉ trên hồ sơ của chính mình.</summary>
    public const string EvaluationAppealSubmit = "evaluation.appeal.submit";

    /// <summary>Không xử lý kiến nghị của mình hay kiến nghị liên quan tới mình.</summary>
    public const string EvaluationAppealResolve = "evaluation.appeal.resolve";

    /// <summary>Kế hoạch khắc phục 30-60-90 ngày (Mẫu 17).</summary>
    public const string EvaluationImprovementManage = "evaluation.improvement.manage";

    #endregion

    #region Đối soát, hiệu chuẩn, kiểm soát

    /// <summary>Mẫu 03: yêu cầu đối soát trên hồ sơ; không áp dụng khi là chủ hồ sơ.</summary>
    public const string EvaluationCrosscheckRequest = "evaluation.crosscheck.request";

    /// <summary>Mẫu 03: phạm vi kiểm tra là đơn vị đối soát của phiếu.</summary>
    public const string EvaluationCrosscheckRespond = "evaluation.crosscheck.respond";

    /// <summary>Mẫu 03: phạm vi kiểm tra là đơn vị đối soát của phiếu; khác người lập.</summary>
    public const string EvaluationCrosscheckConfirm = "evaluation.crosscheck.confirm";

    /// <summary>Mẫu 20: kiểm tra trên từng hồ sơ trong biên bản.</summary>
    public const string EvaluationCalibrationManage = "evaluation.calibration.manage";

    /// <summary>Mẫu 20: khác người lập biên bản.</summary>
    public const string EvaluationCalibrationConclude = "evaluation.calibration.conclude";

    /// <summary>Mẫu 19: kiểm tra trên từng hồ sơ trong phiếu.</summary>
    public const string EvaluationControlPerform = "evaluation.control.perform";

    /// <summary>Mẫu 19: khác người lập phiếu.</summary>
    public const string EvaluationControlConfirm = "evaluation.control.confirm";

    /// <summary>Không dùng làm quyền thực hiện bước của diện quyết định.</summary>
    public static readonly IReadOnlySet<string> FormPermissions = new HashSet<string>(StringComparer.Ordinal)
    {
        EvaluationCrosscheckRequest, EvaluationCrosscheckRespond, EvaluationCrosscheckConfirm,
        EvaluationCalibrationManage, EvaluationCalibrationConclude, EvaluationControlPerform, EvaluationControlConfirm
    };

    #endregion

    #region Tập thể, hội nghị, báo cáo

    /// <summary>Mẫu 06–08.</summary>
    public const string CollectiveManage = "collective.manage";

    /// <summary>Mẫu 12–13.</summary>
    public const string MeetingRead = "meeting.read";

    public const string MeetingManage = "meeting.manage";

    /// <summary>Mẫu 14–16, danh sách cán bộ; lọc theo phạm vi.</summary>
    public const string ReportExport = "report.export";

    #endregion

    /// <summary>Theo thứ tự hiển thị (SortOrder = chỉ số trong danh sách).</summary>
    public static readonly IReadOnlyList<PermissionDefinition> Definitions = new PermissionDefinition[]
    {
        new(SystemUsersRead, "Xem tài khoản, hồ sơ cán bộ", "system", "Xem danh sách tài khoản và hồ sơ cán bộ; không gồm hồ sơ đánh giá.", true),
        new(SystemUsersManage, "Quản lý tài khoản", "system", "Tạo, sửa, khóa/mở, xóa tài khoản, đặt lại mật khẩu, mở khóa đăng nhập.", true),
        new(SystemRolesManage, "Quản lý vai trò", "system", "Tạo/sửa/xóa vai trò, chọn quyền cho vai trò.", false),
        new(SystemAssignmentsManage, "Gán vai trò", "system", "Gán/thu hồi vai trò cho người dùng kèm phạm vi và thời hạn.", false),
        new(SystemAuditRead, "Xem nhật ký", "system", "Xem nhật ký thao tác và nhật ký đăng nhập.", false),
        new(SystemSettingsManage, "Quản lý thông tin đơn vị", "system", "Sửa tên Đảng bộ, tên công ty, tên viết tắt, địa danh… dùng trên biểu mẫu, báo cáo và giao diện.", false),
        new(SystemTemplatesManage, "Quản lý file mẫu biểu mẫu", "system", "Tải lên phiên bản file mẫu Word mới (kiểm tra tag), kích hoạt lại phiên bản cũ, xem lịch sử.", false),
        new(SystemImport, "Nhập dữ liệu từ Excel", "system", "Nhập đơn vị, tổ chức Đảng, cán bộ và gán vai trò từ file Excel; mỗi loại còn cần quyền quản lý dữ liệu tương ứng.", false),
        new(CatalogManage, "Quản lý danh mục", "catalog", "Quản lý danh mục đơn vị chính quyền, tổ chức Đảng, loại đơn vị, chức vụ. Xem danh mục: mọi người đã đăng nhập.", false),
        new(PeriodManage, "Quản lý kỳ đánh giá", "period", "Tạo kỳ, cấu hình bước/thời hạn, chọn bộ tiêu chí, danh sách người được đánh giá, mở/khóa kỳ.", false),
        new(CriteriaManage, "Quản lý bộ tiêu chí", "criteria", "Tạo, nhân bản, sửa bản nháp, xuất bản, lưu trữ bộ tiêu chí và thang điểm (tiêu chí chung, trục kết quả, khung tỷ trọng, mức xếp loại, tham số).", false),
        new(EvaluationSelf, "Tham gia đánh giá (bản thân)", "evaluation", "Đăng ký sản phẩm, tự chấm, giải trình, nộp minh chứng — chỉ trên hồ sơ của mình (phạm vi gán được bỏ qua).", true),
        new(EvaluationRead, "Xem hồ sơ đánh giá", "evaluation", "Xem hồ sơ đánh giá trong phạm vi được gán; chủ hồ sơ luôn xem được hồ sơ của mình.", true),
        new(EvaluationTasksApprove, "Duyệt danh mục sản phẩm", "evaluation", "Duyệt danh mục sản phẩm đăng ký đầu kỳ (bước 1).", true),
        new(EvaluationCellConfirm, "Chi bộ xác nhận phiếu tự chấm", "evaluation", "Chi bộ xác nhận phiếu tự chấm điểm (bước 2).", true),
        new(EvaluationCollectiveRecord, "Ghi nhận đề xuất của tập thể lãnh đạo", "evaluation", "Ghi kết quả kiểm phiếu đề xuất của tập thể lãnh đạo (bước 3a), không ghi phiếu từng người.", true),
        new(EvaluationAppraise, "Thẩm định", "evaluation", "Thẩm định hồ sơ đánh giá (bước 3b).", true),
        new(EvaluationDirectorReview, "Nhận xét của cấp trực tiếp sử dụng", "evaluation", "Nhận xét, đề xuất của cấp trực tiếp sử dụng cán bộ (bước 3c).", true),
        new(EvaluationUnitReview, "Lãnh đạo đơn vị đề xuất", "evaluation", "Trưởng phòng (lãnh đạo đơn vị) đề xuất mức xếp loại thay cấp trực tiếp sử dụng — dùng làm quyền thực hiện bước 3c trong diện quyết định được cấu hình.", true),
        new(EvaluationDecide, "Ghi nhận quyết định của Đảng ủy cơ sở", "evaluation", "Ghi nhận quyết định xếp loại của Đảng ủy cơ sở (bước 4) khi diện quyết định đặt bước này làm trong hệ thống; bước do cấp trên quyết định dùng quyền Ghi nhận kết quả của cấp trên.", true),
        new(EvaluationExternalRecord, "Ghi nhận kết quả của cấp trên", "evaluation", "Ghi nhận kết quả của bước do cấp trên / cơ quan ngoài hệ thống thực hiện (thẩm định, nhận xét, quyết định…): cơ quan, số/ngày văn bản, nhận xét, mức, tệp đính kèm.", true),
        new(EvaluationPublish, "Công bố và lưu trữ kết quả", "evaluation", "Công bố và khóa kết quả đánh giá (bước 5).", true),
        new(EvaluationReopen, "Mở lại hồ sơ đã khóa", "evaluation", "Mở lại hồ sơ đã khóa để đính chính; bắt buộc ghi lý do.", true),
        new(EvaluationResultsView, "Xem kết quả đánh giá đã công bố", "evaluation", "Xem danh sách kết quả đã công bố (họ tên, chức danh, đơn vị, mức xếp loại chính thức; điểm nếu bộ tiêu chí cho phép) trong phạm vi được gán — không xem chi tiết hồ sơ, minh chứng, ý kiến.", true),
        new(EvaluationAppealSubmit, "Gửi kiến nghị về kết quả", "evaluation", "Gửi kiến nghị xem xét lại kết quả đã công bố, kèm tệp minh chứng — chỉ trên hồ sơ của mình (phạm vi gán được bỏ qua).", true),
        new(EvaluationAppealResolve, "Xử lý kiến nghị về kết quả", "evaluation", "Xem xét, trả lời (bắt buộc nêu căn cứ) kiến nghị sau công bố trong phạm vi được gán; không xử lý kiến nghị của mình hoặc kiến nghị liên quan tới bước mình đã thực hiện.", true),
        new(EvaluationImprovementManage, "Lập kế hoạch khắc phục 30-60-90 ngày", "evaluation", "Lập, duyệt và ghi kết quả từng mốc của kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (Mẫu 17) cho cán bộ trong phạm vi được gán; không áp dụng trên hồ sơ của mình.", true),
        new(EvaluationCrosscheckRequest, "Yêu cầu đối soát chuyên ngành (Mẫu 03)", "evaluation", "Yêu cầu cơ quan chuyên môn đối soát dữ liệu tự khai của cán bộ trong phạm vi được gán, nhập các dòng cần đối soát, hủy yêu cầu (nêu lý do); không áp dụng trên hồ sơ của mình.", true),
        new(EvaluationCrosscheckRespond, "Lập phiếu đối soát chuyên ngành", "evaluation", "Cơ quan chuyên môn điền kết quả đối soát với dữ liệu gốc, đề xuất tăng/giảm điểm kèm căn cứ; phạm vi gán là đơn vị đối soát. Không cần quyền xem hồ sơ đánh giá.", true),
        new(EvaluationCrosscheckConfirm, "Xác nhận phiếu đối soát chuyên ngành", "evaluation", "Lãnh đạo cơ quan chuyên môn xác nhận hoặc trả lại phiếu đối soát; phạm vi gán là đơn vị đối soát; khác người lập phiếu.", true),
        new(EvaluationCalibrationManage, "Lập biên bản họp thống nhất (Mẫu 20)", "evaluation", "Tạo biên bản họp thống nhất cho nhóm hồ sơ tương đồng, nhập thành phần, trường hợp thảo luận; kiểm tra trên từng hồ sơ trong biên bản, không áp dụng trên hồ sơ của mình.", true),
        new(EvaluationCalibrationConclude, "Chốt biên bản họp thống nhất", "evaluation", "Chốt hoặc mở lại biên bản họp thống nhất (bắt buộc ghi lý do khi mở lại); khác người lập biên bản.", true),
        new(EvaluationControlPerform, "Lập phiếu kiểm tra (Mẫu 19)", "evaluation", "Lập phiếu kiểm tra tính hợp lệ, công bằng và nhất quán trước khi trình phê duyệt; kiểm tra trên từng hồ sơ trong phiếu, không áp dụng trên hồ sơ của mình.", true),
        new(EvaluationControlConfirm, "Xác nhận phiếu kiểm tra", "evaluation", "Xác nhận hoặc trả lại phiếu kiểm tra; khác người lập phiếu.", true),
        new(CollectiveManage, "Lập hồ sơ tự đánh giá tập thể", "collective", "Lập hồ sơ tự đánh giá của tập thể (Mẫu 06–08).", true),
        new(MeetingRead, "Xem biên bản hội nghị", "meeting", "Xem biên bản hội nghị, biên bản kiểm phiếu (Mẫu 12–13).", true),
        new(MeetingManage, "Lập biên bản hội nghị", "meeting", "Lập biên bản hội nghị, biên bản kiểm phiếu.", true),
        new(ReportExport, "Xuất báo cáo tổng hợp", "report", "Xuất báo cáo tổng hợp (Mẫu 14–16, danh sách cán bộ), lọc theo phạm vi.", true),
    };

    public static readonly string[] All = Definitions.Select(d => d.Code).ToArray();

    private static readonly Dictionary<string, PermissionDefinition> ByCode =
        Definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);

    public static bool IsDefined(string code) => ByCode.ContainsKey(code);

    public static PermissionDefinition? Find(string code) => ByCode.TryGetValue(code, out var d) ? d : null;

    /// <summary>Trả lại chính mã nếu không tìm thấy.</summary>
    public static string DisplayName(string code) => Find(code)?.Name ?? code;
}
