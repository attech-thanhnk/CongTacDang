"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { auditService, AuditLogDto, AuditLogPage } from "@/services/auditService";
import { PageHeader, Pagination, usePageSize } from "@/components/common";
import styles from "./audit.module.css";
import { LoginEventsTab } from "@/components/admin/LoginEventsTab";
import { NoAccess } from "@/components/admin/NoAccess";
import { AdminModal } from "@/components/admin/AdminModal";
import { errorMessage, formatDateTime } from "@/components/admin/adminUtils";

type AuditTab = "actions" | "logins";

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
  Update: { label: "Cập nhật", icon: "bi-pencil-square", tone: "t-info" },
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

function actionMeta(action: string) {
  return ACTION_META[action] ?? { label: action, icon: "bi-dot", tone: "t-info" };
}

function entityLabel(entityType: string) {
  return ENTITY_LABELS[entityType] || entityType;
}

function parseObject(value: string): Record<string, unknown> | null {
  try {
    const parsed: unknown = JSON.parse(value);
    return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** JSON lỗi thì hiện nguyên chuỗi. */
function prettySnapshot(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value || "{}";
  }
}

/** Tên đối tượng lấy từ dữ liệu ghi kèm (nếu có), để không phải hiện Id. */
function entityName(log: AuditLogDto): string | null {
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

function displayValue(value: unknown): string {
  if (value === undefined) return "";
  if (value === null) return "—";
  const text = typeof value === "string" ? (value === "" ? "(trống)" : value) : JSON.stringify(value);
  // Giá trị dài (chụp cấu hình) xem đầy đủ trong phần JSON bên dưới.
  return text.length > 240 ? `${text.slice(0, 240)}…` : text;
}

/** Các trường thay đổi giữa dữ liệu trước và sau. */
function diffRows(log: AuditLogDto) {
  const before = parseObject(log.oldValues) ?? {};
  const after = parseObject(log.newValues) ?? {};
  const keys = Array.from(new Set([...Object.keys(before), ...Object.keys(after)]));
  return keys
    .filter((key) => JSON.stringify(before[key]) !== JSON.stringify(after[key]))
    .map((key) => ({ key, before: displayValue(before[key]), after: displayValue(after[key]) }));
}

/** Bản ghi do máy chủ tự sinh mang tên "system". */
function actorLabel(name?: string | null) {
  return !name || name.toLowerCase() === "system" ? "Hệ thống" : name;
}

function splitDateTime(value: string) {
  const text = formatDateTime(value);
  const i = text.indexOf(" ");
  return i < 0 ? [text, ""] : [text.slice(0, i), text.slice(i + 1)];
}

interface Filters {
  entityType: string;
  entityId: string;
  action: string;
}

const EMPTY_FILTERS: Filters = { entityType: "", entityId: "", action: "" };

/** Nội dung trang nhật ký: tab Nhật ký thao tác + tab Nhật ký đăng nhập. */
function AuditPageContent() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const [tab, setTab] = useState<AuditTab>("actions");
  const [data, setData] = useState<AuditLogPage | null>(null);
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS);
  const [advanced, setAdvanced] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = usePageSize("audit.actions");
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);
  const [loading, setLoading] = useState(true);
  const canRead = hasPermission("system.audit.read");

  const loadLogs = useCallback(async () => {
    setLoading(true);
    try {
      setData(
        await auditService.getLogs({
          entityType: filters.entityType || undefined,
          entityId: filters.entityId || undefined,
          action: filters.action || undefined,
          page,
          pageSize,
        })
      );
    } catch (error) {
      toast.error(errorMessage(error, "Tải nhật ký thao tác không thành công."));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters, page, pageSize]);

  useEffect(() => {
    if (canRead) loadLogs();
  }, [canRead, loadLogs]);

  const logs = data?.items ?? [];
  const counts = data?.actionCounts ?? {};
  const entityOptions = useMemo(
    () =>
      (data?.entityTypes ?? [])
        .map((value) => ({ value, label: entityLabel(value) }))
        .filter((option, index, all) => all.findIndex((o) => o.label === option.label) === index)
        .sort((a, b) => a.label.localeCompare(b.label, "vi")),
    [data?.entityTypes]
  );
  const actionOptions = useMemo(
    () => Object.keys(counts).sort((a, b) => actionMeta(a).label.localeCompare(actionMeta(b).label, "vi")),
    [counts]
  );
  const hasFilters = !!(filters.entityType || filters.entityId || filters.action);

  /** Áp dụng bộ lọc tra cứu, quay về trang đầu. */
  const handleApplyFilters = (event: React.FormEvent) => {
    event.preventDefault();
    setPage(1);
    setFilters({ ...draft, entityId: draft.entityId.trim() });
  };

  const clearFilters = () => {
    setDraft(EMPTY_FILTERS);
    setPage(1);
    setFilters(EMPTY_FILTERS);
  };

  /** Lọc nhanh theo một loại thao tác khi bấm ô số liệu. */
  const filterByAction = (action: string) => {
    const next = { ...filters, action: filters.action === action ? "" : action };
    setDraft({ ...draft, action: next.action });
    setPage(1);
    setFilters(next);
  };

  /** Xem toàn bộ lịch sử của đối tượng đang mở chi tiết. */
  const showHistoryOf = (log: AuditLogDto) => {
    const next = { entityType: log.entityType, entityId: log.entityId, action: "" };
    setDraft(next);
    setAdvanced(true);
    setPage(1);
    setFilters(next);
    setSelectedLog(null);
  };

  if (!canRead) {
    return <NoAccess title="Nhật ký hệ thống" permissionName="Xem nhật ký" />;
  }

  const selectedChanges = selectedLog ? diffRows(selectedLog) : [];
  const selectedName = selectedLog ? entityName(selectedLog) : null;
  const selectedIsCreate = selectedChanges.every((row) => row.before === "");

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Nhật ký hệ thống"
        actions={
          tab === "actions" && (
            <button type="button" className="btn btn-outline-secondary" onClick={() => loadLogs()} disabled={loading}>
              <i className={`bi ${loading ? "bi-arrow-repeat" : "bi-arrow-clockwise"} me-1`} />
              Làm mới
            </button>
          )
        }
      />

      <div className="page-body">
        <div className="d-flex flex-column gap-3">
          <ul className="nav nav-tabs">
            {[
              { key: "actions" as const, label: "Nhật ký thao tác", icon: "bi-activity" },
              { key: "logins" as const, label: "Nhật ký đăng nhập", icon: "bi-box-arrow-in-right" },
            ].map((t) => (
              <li className="nav-item" key={t.key}>
                <button type="button" className={`nav-link ${tab === t.key ? "active" : ""}`} onClick={() => setTab(t.key)}>
                  <i className={`bi ${t.icon} me-1`} />
                  {t.label}
                </button>
              </li>
            ))}
          </ul>

          {tab === "logins" && <LoginEventsTab />}

          {tab === "actions" && (
            <>
              <section className="card">
                <div className={styles.stats}>
                  {[
                    { label: "Tổng thao tác", value: data?.totalCount ?? 0, tone: "var(--info)" },
                    { label: "Lượt cập nhật", value: counts.Update ?? 0, tone: "var(--wait)", action: "Update" },
                    { label: "Xóa mềm", value: counts.Delete ?? 0, tone: "var(--bad)", action: "Delete" },
                    { label: "Người thao tác", value: data?.actorCount ?? 0, tone: "var(--ok)" },
                  ].map((stat) =>
                    stat.action ? (
                      <button
                        type="button"
                        className={`${styles.stat} ${styles.statBtn} ${filters.action === stat.action ? styles.statActive : ""}`}
                        key={stat.label}
                        style={{ "--c": stat.tone } as React.CSSProperties}
                        onClick={() => filterByAction(stat.action)}
                        aria-pressed={filters.action === stat.action}
                        title={filters.action === stat.action ? "Bỏ lọc" : "Chỉ xem loại thao tác này"}
                      >
                        <div className={styles.value}>{stat.value}</div>
                        <div className={styles.label}>{stat.label}</div>
                      </button>
                    ) : (
                      <div className={styles.stat} key={stat.label} style={{ "--c": stat.tone } as React.CSSProperties}>
                        <div className={styles.value}>{stat.value}</div>
                        <div className={styles.label}>{stat.label}</div>
                      </div>
                    )
                  )}
                </div>
              </section>

              <section className="card">
                <div className="card-header">
                  <form className="row g-2 align-items-end" onSubmit={handleApplyFilters}>
                    <div className="col-12 col-sm-6 col-md-4 col-xl-3">
                      <label className="form-label" htmlFor="audit-entity-type">Đối tượng</label>
                      <select id="audit-entity-type" className="form-select form-select-sm" value={draft.entityType} onChange={(event) => setDraft({ ...draft, entityType: event.target.value })}>
                        <option value="">Tất cả đối tượng</option>
                        {entityOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                      </select>
                    </div>
                    <div className="col-12 col-sm-6 col-md-3 col-xl-2">
                      <label className="form-label" htmlFor="audit-action">Thao tác</label>
                      <select id="audit-action" className="form-select form-select-sm" value={draft.action} onChange={(event) => setDraft({ ...draft, action: event.target.value })}>
                        <option value="">Tất cả</option>
                        {(draft.action && !actionOptions.includes(draft.action) ? [...actionOptions, draft.action] : actionOptions).map((action) => (
                          <option key={action} value={action}>{actionMeta(action).label}</option>
                        ))}
                      </select>
                    </div>
                    {advanced && (
                      <div className="col-12 col-md-5 col-xl-4">
                        <label className="form-label" htmlFor="audit-entity-id">Mã đối tượng (Id)</label>
                        <input
                          id="audit-entity-id"
                          className="form-control form-control-sm font-monospace"
                          value={draft.entityId}
                          onChange={(event) => setDraft({ ...draft, entityId: event.target.value })}
                        />
                      </div>
                    )}
                    <div className="col-12 col-md-auto d-flex flex-wrap gap-2">
                      <button type="submit" className="btn btn-sm btn-primary" disabled={loading}>
                        <i className="bi bi-search me-1" />Tra cứu
                      </button>
                      {hasFilters && (
                        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={clearFilters}>
                          Xóa bộ lọc
                        </button>
                      )}
                      <button
                        type="button"
                        className="btn btn-sm btn-link text-decoration-none px-1"
                        onClick={() => setAdvanced((v) => !v)}
                        aria-expanded={advanced}
                      >
                        <i className={`bi ${advanced ? "bi-chevron-up" : "bi-sliders"} me-1`} />
                        {advanced ? "Thu gọn" : "Lọc nâng cao"}
                      </button>
                    </div>
                  </form>
                </div>
                <div className="table-responsive">
                  <table className={`table table-hover align-middle mb-0 ${styles.table}`}>
                    <thead>
                      <tr>
                        <th className="ps-3">Thời điểm</th>
                        <th>Người thực hiện</th>
                        <th>Thao tác</th>
                        <th>Đối tượng</th>
                        <th className="pe-3" aria-label="Chi tiết" />
                      </tr>
                    </thead>
                    <tbody>
                      {loading && !data ? (
                        <tr><td colSpan={5} className="text-center py-5 text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải nhật ký...</td></tr>
                      ) : logs.length === 0 ? (
                        <tr>
                          <td colSpan={5} className="text-center py-5 text-secondary">
                            <i className="bi bi-inbox fs-3 d-block mb-2" />
                            {hasFilters ? "Không có thao tác phù hợp với bộ lọc." : "Chưa có thao tác nào được ghi lại."}
                          </td>
                        </tr>
                      ) : logs.map((log) => {
                        const meta = actionMeta(log.action);
                        const [date, time] = splitDateTime(log.createdAt);
                        const name = entityName(log);
                        return (
                          <tr
                            key={log.id}
                            className={styles.row}
                            tabIndex={0}
                            onClick={() => setSelectedLog(log)}
                            onKeyDown={(e) => {
                              if (e.key === "Enter" || e.key === " ") {
                                e.preventDefault();
                                setSelectedLog(log);
                              }
                            }}
                            style={{ opacity: loading ? 0.6 : 1 }}
                          >
                            <td className={`ps-3 text-nowrap ${styles.cTime}`}>
                              <div className="num">{date}</div>
                              <div className="small text-secondary num">{time}</div>
                            </td>
                            <td className={styles.actor}>{actorLabel(log.actorName)}</td>
                            <td className={`text-nowrap ${styles.cAction}`}>
                              <span className={`tag ${meta.tone}`}>
                                <i className={`bi ${meta.icon} me-1`} />{meta.label}
                              </span>
                            </td>
                            <td className={styles.cEntity}>
                              <div className="fw-semibold">{entityLabel(log.entityType)}</div>
                              {name && <div className={`small text-secondary ${styles.clip}`} title={name}>{name}</div>}
                            </td>
                            <td className={`pe-3 text-end text-secondary ${styles.cGo}`}>
                              <i className="bi bi-chevron-right" aria-hidden="true" />
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
                <Pagination
                  page={page}
                  pageSize={pageSize}
                  total={data?.totalCount ?? 0}
                  onPageChange={setPage}
                  onPageSizeChange={(size) => {
                    setPageSize(size);
                    setPage(1);
                  }}
                  disabled={loading}
                />
              </section>
            </>
          )}
        </div>

        {tab === "actions" && selectedLog && (
          <AdminModal
            title="Chi tiết thao tác"
            onClose={() => setSelectedLog(null)}
            maxWidth={900}
            footer={
              <>
                <button type="button" className="btn btn-outline-secondary" onClick={() => showHistoryOf(selectedLog)}>
                  <i className="bi bi-clock-history me-1" />Lịch sử đối tượng này
                </button>
                <button type="button" className="btn btn-primary" onClick={() => setSelectedLog(null)}>Đóng</button>
              </>
            }
          >
            <dl className={styles.summary}>
              <dt>Thời điểm</dt>
              <dd className="num">{formatDateTime(selectedLog.createdAt)}</dd>
              <dt>Người thực hiện</dt>
              <dd>{actorLabel(selectedLog.actorName)}</dd>
              <dt>Thao tác</dt>
              <dd>
                <span className={`tag ${actionMeta(selectedLog.action).tone}`}>
                  <i className={`bi ${actionMeta(selectedLog.action).icon} me-1`} />{actionMeta(selectedLog.action).label}
                </span>
              </dd>
              <dt>Đối tượng</dt>
              <dd>
                {entityLabel(selectedLog.entityType)}
                {selectedName && <span className="text-secondary"> · {selectedName}</span>}
              </dd>
            </dl>

            <h3 className={styles.sectionTitle}>Nội dung thay đổi</h3>
            {selectedChanges.length === 0 ? (
              <div className="small text-secondary mb-3">Không có trường nào thay đổi.</div>
            ) : (
              <div className={`table-responsive mb-3 ${styles.diffWrap}`}>
                <table className={`table table-sm align-top mb-0 ${styles.diff}`}>
                  <thead>
                    <tr><th>Trường</th>{!selectedIsCreate && <th>Trước</th>}<th>{selectedIsCreate ? "Giá trị" : "Sau"}</th></tr>
                  </thead>
                  <tbody>
                    {selectedChanges.map((row) => (
                      <tr key={row.key}>
                        <td className="font-monospace small">{row.key}</td>
                        {!selectedIsCreate && <td className={styles.before}>{row.before}</td>}
                        <td className={styles.after}>{row.after}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <details className={styles.tech}>
              <summary>Thông tin kỹ thuật</summary>
              <dl className={`${styles.summary} mt-2`}>
                <dt>Mã đối tượng</dt>
                <dd className="font-monospace small text-break">{selectedLog.entityId}</dd>
                <dt>Đường dẫn API</dt>
                <dd className="font-monospace small text-break">{selectedLog.requestPath || "—"}</dd>
                <dt>Địa chỉ IP</dt>
                <dd className="font-monospace small">{selectedLog.ipAddress || "—"}</dd>
                {selectedLog.actorId && (
                  <>
                    <dt>Mã người thực hiện</dt>
                    <dd className="font-monospace small text-break">{selectedLog.actorId}</dd>
                  </>
                )}
              </dl>
              <div className="row g-3 mt-1">
                <div className="col-12 col-lg-6">
                  <div className="audit-detail-block h-100">
                    <div className="small fw-semibold text-secondary mb-1">Dữ liệu trước (JSON)</div>
                    <pre className="mb-0">{prettySnapshot(selectedLog.oldValues)}</pre>
                  </div>
                </div>
                <div className="col-12 col-lg-6">
                  <div className="audit-detail-block h-100">
                    <div className="small fw-semibold text-secondary mb-1">Dữ liệu sau (JSON)</div>
                    <pre className="mb-0">{prettySnapshot(selectedLog.newValues)}</pre>
                  </div>
                </div>
              </div>
            </details>
          </AdminModal>
        )}
      </div>
    </div>
  );
}

export default function AuditPage() {
  return <AuditPageContent />;
}
