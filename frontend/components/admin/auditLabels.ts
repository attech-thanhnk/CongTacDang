import type { AuditLogDto } from "@/services/auditService";

/** Nhãn của loại đối tượng ghi trong nhật ký; loại chưa có nhãn hiện nguyên tên. */
const ENTITY_LABELS: Record<string, string> = {
  EvaluationPeriod: "Kỳ đánh giá",
  EvaluationRecord: "Hồ sơ đánh giá",
  EvaluationTask: "Nhiệm vụ đánh giá",
  TaskAttachment: "Tệp minh chứng",
  TaskChangeRequest: "Đề nghị điều chỉnh nhiệm vụ",
  EvaluationMeeting: "Biên bản hội nghị",
  EvaluationMeetingVoteSummary: "Kết quả biểu quyết",
  EvaluationExternalResult: "Kết quả cấp trên",
  CollectiveEvaluationRecord: "Hồ sơ tập thể",
  CollectiveEvaluationItem: "Nội dung hồ sơ tập thể",
  EvaluationAppeal: "Kiến nghị",
  ImprovementPlan: "Kế hoạch khắc phục (Mẫu 17)",
  ReportDraft: "Báo cáo",
  CalibrationSession: "Biên bản họp thống nhất",
  CalibrationCase: "Trường hợp thảo luận",
  CalibrationRecord: "Hồ sơ thuộc biên bản họp thống nhất",
  CalibrationAttendee: "Người dự họp thống nhất",
  CalibrationSummaryRow: "Số liệu biên bản họp thống nhất",
  ControlCheck: "Phiếu kiểm tra",
  ControlCheckItem: "Mục của phiếu kiểm tra",
  ControlCheckRecord: "Hồ sơ thuộc phiếu kiểm tra",
  ExpertCrosscheck: "Phiếu xác nhận số liệu",
  ExpertCrosscheckItem: "Mục của phiếu xác nhận số liệu",
  CriteriaSet: "Bộ tiêu chí",
  FormLayout: "Bố cục biểu mẫu",
  WordTemplateVersion: "File mẫu Word",
  OrganizationSettings: "Thông tin đơn vị",
  PartyMemberProfile: "Hồ sơ cán bộ",
  MemberPosition: "Chức vụ của cán bộ",
  Position: "Danh mục chức vụ",
  PartyCell: "Tổ chức Đảng",
  AdministrativeDepartment: "Đơn vị chính quyền",
  OrgUnitType: "Loại đơn vị",
  Holiday: "Ngày nghỉ",
  AppRole: "Vai trò",
  Permission: "Quyền",
  role_permissions: "Quyền của vai trò",
  "Dictionary`2": "Quyền của vai trò",
  UserRoleAssignment: "Phân công vai trò",
  RefreshToken: "Phiên đăng nhập",
  Import: "Nhập dữ liệu từ Excel",
};

/** Nhãn và màu của loại thao tác. */
const ACTION_META: Record<string, { label: string; icon: string; tone: string }> = {
  Create: { label: "Tạo mới", icon: "bi-plus-lg", tone: "t-ok" },
  Update: { label: "Cập nhật", icon: "bi-pencil", tone: "t-info" },
  Delete: { label: "Xóa mềm", icon: "bi-archive", tone: "t-bad" },
  Import: { label: "Nhập dữ liệu", icon: "bi-file-earmark-arrow-up", tone: "t-wait" },
  Approve: { label: "Phê duyệt", icon: "bi-check2-circle", tone: "t-ok" },
  AssignRoles: { label: "Phân công vai trò", icon: "bi-person-badge", tone: "t-wait" },
  UpdatePermissions: { label: "Cập nhật quyền", icon: "bi-shield-check", tone: "t-wait" },
};

/** Thao tác phiên đăng nhập chỉ nhận ra được qua đường dẫn gọi. */
const SESSION_HINTS: [string, string][] = [
  ["/auth/login", "Đăng nhập"],
  ["/auth/refresh-token", "Gia hạn phiên"],
  ["/auth/logout", "Đăng xuất"],
  ["/auth/change-password", "Đổi mật khẩu"],
];

/** Trường thường chứa tên dễ đọc của đối tượng, theo thứ tự ưu tiên. */
const NAME_KEYS = ["FullName", "Name", "Title", "DisplayName", "Username", "UserName", "OriginalFileName", "FileName", "Code"];

export function actionMeta(action: string) {
  return ACTION_META[action] ?? { label: action, icon: "bi-dot", tone: "t-info" };
}

export function entityLabel(entityType: string) {
  return ENTITY_LABELS[entityType] || entityType;
}

export function parseObject(value: string): Record<string, unknown> | null {
  try {
    const parsed: unknown = JSON.parse(value);
    return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** JSON lỗi thì hiện nguyên chuỗi. */
export function prettySnapshot(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value || "{}";
  }
}

/** Tên đối tượng lấy từ dữ liệu ghi kèm (nếu có), để không phải hiện Id. */
export function entityName(log: AuditLogDto): string | null {
  for (const raw of [log.newValues, log.oldValues]) {
    const obj = parseObject(raw);
    if (!obj) continue;
    for (const key of NAME_KEYS) {
      const v = obj[key];
      if (typeof v === "string" && v.trim()) return v.trim();
    }
  }
  const hint = SESSION_HINTS.find(([path]) => log.requestPath?.includes(path));
  return hint ? hint[1] : null;
}

/** Bản ghi do máy chủ tự sinh mang tên "system". */
export function actorLabel(name?: string | null) {
  return !name || name.toLowerCase() === "system" ? "Hệ thống" : name;
}
