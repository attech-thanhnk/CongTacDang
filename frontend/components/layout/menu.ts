import type { AuthUser } from "@/contexts/AuthContext";

export interface MenuItem {
  title: string;
  href: string;
  icon: string;
  /** Mã quyền cần có (ở bất kỳ phạm vi nào). */
  permission?: string;
  /** Điều kiện tùy chỉnh (ưu tiên hơn `permission`). */
  check?: () => boolean;
  /** Trang ngoài menu: nút quay lại về mục menu đầu tiên còn hiện trong danh sách này. */
  backTo?: string[];
}

export interface MenuGroup {
  key: string;
  title?: string;
  /** Nhóm gập/mở được trên thanh bên. */
  collapsible?: boolean;
  items: MenuItem[];
}

/** Menu chỉ hiện theo mã quyền — máy chủ luôn kiểm tra lại, kể cả phạm vi. */
export function buildMenu(user: AuthUser | null, hasPermission: (code: string) => boolean): MenuGroup[] {
  const hasAnyEvaluationAction = () =>
    (user?.permissions ?? []).some((code) => code.startsWith("evaluation.") && code !== "evaluation.read");

  const groups: MenuGroup[] = [
    {
      key: "main",
      items: [
        { title: "Tổng quan", href: "/", icon: "fa-house" },
        { title: "Việc cần xử lý", href: "/work-queue", icon: "fa-inbox", check: hasAnyEvaluationAction },
      ],
    },
    {
      key: "evaluate",
      title: "Đánh giá",
      items: [
        {
          title: "Hồ sơ đánh giá",
          href: "/evaluations",
          icon: "fa-file-lines",
          // Người chỉ tự đánh giá mở hồ sơ của mình từ Tổng quan; mục này dành cho người xem hồ sơ người khác.
          check: () => hasPermission("evaluation.read"),
        },
        {
          title: "Đánh giá tập thể",
          href: "/collective-evaluations",
          icon: "fa-users",
          // Chỉ người lập hồ sơ tập thể, biên bản hoặc ghi nhận đề xuất tập thể.
          check: () =>
            hasPermission("collective.manage") ||
            hasPermission("meeting.read") ||
            hasPermission("meeting.manage") ||
            hasPermission("evaluation.collective.record"),
        },
        {
          title: "Xác nhận số liệu",
          href: "/crosschecks",
          icon: "fa-file-circle-check",
          // Người xem hồ sơ đánh giá cũng xem được danh sách phiếu (API cho phép).
          check: () =>
            hasPermission("evaluation.read") ||
            hasPermission("evaluation.crosscheck.request") ||
            hasPermission("evaluation.crosscheck.respond") ||
            hasPermission("evaluation.crosscheck.confirm"),
        },
        {
          title: "Duyệt kết quả",
          href: "/quality-control",
          icon: "fa-clipboard-check",
          check: () =>
            hasPermission("evaluation.calibration.manage") ||
            hasPermission("evaluation.calibration.conclude") ||
            hasPermission("evaluation.control.perform") ||
            hasPermission("evaluation.control.confirm"),
        },
      ],
    },
    {
      key: "results",
      title: "Kết quả",
      items: [
        { title: "Kết quả đánh giá", href: "/results", icon: "fa-award", permission: "evaluation.results.view" },
        {
          title: "Báo cáo",
          href: "/reports",
          icon: "fa-chart-column",
          // Người chỉ có quyền xem hội nghị dùng trang này để xuất bảng phiếu kín (Mẫu 11).
          check: () => hasPermission("report.export") || hasPermission("meeting.read"),
        },
      ],
    },
    {
      key: "setup",
      title: "Thiết lập",
      items: [
        { title: "Kỳ đánh giá", href: "/periods", icon: "fa-calendar-days", permission: "period.manage" },
        { title: "Bộ tiêu chí", href: "/criteria", icon: "fa-list-check", permission: "criteria.manage" },
        { title: "Danh mục tổ chức", href: "/catalog", icon: "fa-building", permission: "catalog.manage" },
        // Người quản lý danh mục đã có thẻ Ngày nghỉ trong Danh mục tổ chức.
        { title: "Ngày nghỉ", href: "/holidays", icon: "fa-calendar-xmark", check: () => hasPermission("period.manage") && !hasPermission("catalog.manage") },
      ],
    },
    {
      key: "system",
      title: "Hệ thống",
      collapsible: true,
      items: [
        { title: "Tài khoản", href: "/admin/users", icon: "fa-users", permission: "system.users.read" },
        { title: "Vai trò", href: "/admin/roles", icon: "fa-user-shield", permission: "system.roles.manage" },
        { title: "Người thực hiện", href: "/admin/performers", icon: "fa-user-check", permission: "system.assignments.manage" },
        { title: "Nhập dữ liệu", href: "/admin/imports", icon: "fa-file-arrow-up", permission: "system.import" },
        { title: "Nhật ký hệ thống", href: "/audit", icon: "fa-clock-rotate-left", permission: "system.audit.read" },
        { title: "Thông tin đơn vị", href: "/admin/settings", icon: "fa-gear", permission: "system.settings.manage" },
        { title: "Biểu mẫu Word", href: "/admin/templates", icon: "fa-file-word", permission: "system.templates.manage" },
      ],
    },
  ];

  const isVisible = (item: MenuItem) => (item.check ? item.check() : item.permission ? hasPermission(item.permission) : true);

  return groups
    .map((group) => ({ ...group, items: group.items.filter(isVisible) }))
    .filter((group) => group.items.length > 0);
}

export function isMenuItemActive(item: MenuItem, pathname: string): boolean {
  return pathname === item.href || (item.href !== "/" && pathname.startsWith(`${item.href}/`));
}

export interface MenuTrail {
  item: MenuItem;
  /** Đang ở trang con (chi tiết) của mục menu. */
  isDetail: boolean;
  /** Đích của nút quay lại khi khác `item`. */
  back?: MenuItem;
}

// Trang không có trên menu nhưng vẫn cần đường dẫn.
const EXTRA_PAGES: MenuItem[] = [
  { title: "Tài khoản", href: "/account", icon: "fa-user" },
  { title: "Hướng dẫn", href: "/help", icon: "fa-circle-question" },
  // Không có quyền xem hồ sơ người khác thì hồ sơ mở được là hồ sơ của mình.
  { title: "Hồ sơ của tôi", href: "/evaluations", icon: "fa-file-lines", backTo: ["/work-queue", "/"] },
];

/** Mục menu ứng với đường dẫn hiện tại — dùng cho đường dẫn trên thanh đầu trang. */
export function findMenuTrail(groups: MenuGroup[], pathname: string): MenuTrail | null {
  for (const group of groups) {
    const item = group.items.find((i) => isMenuItemActive(i, pathname));
    if (item) return { item, isDetail: pathname !== item.href };
  }
  const extra = EXTRA_PAGES.find((i) => isMenuItemActive(i, pathname));
  if (!extra) return null;
  const items = groups.flatMap((g) => g.items);
  const back = extra.backTo?.map((href) => items.find((i) => i.href === href)).find((i): i is MenuItem => !!i);
  return { item: extra, isDetail: pathname !== extra.href, back };
}
