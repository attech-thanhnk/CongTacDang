import type { AuthUser } from "@/contexts/AuthContext";

export interface MenuItem {
  title: string;
  href: string;
  icon: string;
  /** Mã quyền cần có (ở bất kỳ phạm vi nào). */
  permission?: string;
  /** Điều kiện tùy chỉnh (ưu tiên hơn `permission`). */
  check?: () => boolean;
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
        { title: "Tổng quan", href: "/", icon: "bi-house" },
        { title: "Việc cần xử lý", href: "/work-queue", icon: "bi-inbox", check: hasAnyEvaluationAction },
        {
          title: "Hồ sơ đánh giá",
          href: "/evaluations",
          icon: "bi-file-earmark-text",
          // Người chỉ tự đánh giá mở hồ sơ của mình từ Tổng quan; mục này dành cho người xem hồ sơ người khác.
          check: () => hasPermission("evaluation.read"),
        },
        {
          title: "Đánh giá tập thể",
          href: "/collective-evaluations",
          icon: "bi-people",
          // Chỉ người lập hồ sơ tập thể, biên bản hoặc ghi nhận đề xuất tập thể.
          check: () =>
            hasPermission("collective.manage") ||
            hasPermission("meeting.read") ||
            hasPermission("meeting.manage") ||
            hasPermission("evaluation.collective.record"),
        },
        {
          title: "Đối soát chuyên ngành",
          href: "/crosschecks",
          icon: "bi-patch-check",
          check: () =>
            hasPermission("evaluation.crosscheck.request") ||
            hasPermission("evaluation.crosscheck.respond") ||
            hasPermission("evaluation.crosscheck.confirm"),
        },
        {
          title: "Hiệu chuẩn và kiểm soát",
          href: "/quality-control",
          icon: "bi-shield-check",
          check: () =>
            hasPermission("evaluation.calibration.manage") ||
            hasPermission("evaluation.calibration.conclude") ||
            hasPermission("evaluation.control.perform") ||
            hasPermission("evaluation.control.confirm"),
        },
        { title: "Kết quả đánh giá", href: "/results", icon: "bi-trophy", permission: "evaluation.results.view" },
        { title: "Báo cáo", href: "/reports", icon: "bi-bar-chart-line", permission: "report.export" },
      ],
    },
    {
      key: "manage",
      title: "Quản lý",
      items: [
        { title: "Kỳ đánh giá", href: "/periods", icon: "bi-calendar3", permission: "period.manage" },
        { title: "Bộ tiêu chí", href: "/criteria", icon: "bi-list-check", permission: "criteria.manage" },
        { title: "Danh mục tổ chức", href: "/catalog", icon: "bi-building", permission: "catalog.manage" },
      ],
    },
    {
      key: "system",
      title: "Hệ thống",
      collapsible: true,
      items: [
        { title: "Tài khoản", href: "/admin/users", icon: "bi-people", permission: "system.users.read" },
        { title: "Vai trò", href: "/admin/roles", icon: "bi-shield-lock", permission: "system.roles.manage" },
        { title: "Người thực hiện", href: "/admin/performers", icon: "bi-person-check", permission: "system.assignments.manage" },
        { title: "Nhập dữ liệu", href: "/admin/imports", icon: "bi-file-earmark-arrow-up", permission: "system.import" },
        { title: "Nhật ký hệ thống", href: "/audit", icon: "bi-clock-history", permission: "system.audit.read" },
        { title: "Thông tin đơn vị", href: "/admin/settings", icon: "bi-gear", permission: "system.settings.manage" },
        { title: "Biểu mẫu Word", href: "/admin/templates", icon: "bi-file-earmark-word", permission: "system.templates.manage" },
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
}

// Trang không có trên menu nhưng vẫn cần đường dẫn.
const EXTRA_PAGES: MenuItem[] = [{ title: "Tài khoản", href: "/account", icon: "bi-person" }];

/** Mục menu ứng với đường dẫn hiện tại — dùng cho đường dẫn trên thanh đầu trang. */
export function findMenuTrail(groups: MenuGroup[], pathname: string): MenuTrail | null {
  for (const group of groups) {
    const item = group.items.find((i) => isMenuItemActive(i, pathname));
    if (item) return { item, isDetail: pathname !== item.href };
  }
  const extra = EXTRA_PAGES.find((i) => isMenuItemActive(i, pathname));
  return extra ? { item: extra, isDetail: pathname !== extra.href } : null;
}
