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

function formatDate(value: string) {
  return formatDateTime(value);
}

function actionMeta(action: string) {
  return ACTION_META[action] ?? { label: action, icon: "bi-dot", tone: "t-info" };
}

function entityLabel(entityType: string) {
  return ENTITY_LABELS[entityType] || entityType;
}

/** JSON lỗi thì hiện nguyên chuỗi. */
function parseSnapshot(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value || "{}";
  }
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
      toast.error(errorMessage(error, "Không thể tải nhật ký thao tác."));
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

  if (!hasPermission("system.audit.read")) {
    return <NoAccess title="Nhật ký hệ thống" permissionName="Xem nhật ký" />;
  }

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Nhật ký hệ thống"
        actions={
          tab === "actions" && (
          <button
            type="button"
            className="btn btn-outline-secondary"
            onClick={() => loadLogs()}
            disabled={loading}
          >
            <i className={`bi ${loading ? "bi-arrow-repeat" : "bi-arrow-clockwise"} me-1`} />
            Làm mới
          </button>
          )
        }
      />

      <div className="page-body">
        <div className="d-flex flex-column gap-3">
        <ul className="nav nav-tabs">
          {(
            [
              { key: "actions" as const, label: "Nhật ký thao tác", icon: "bi-activity" },
              { key: "logins" as const, label: "Nhật ký đăng nhập", icon: "bi-box-arrow-in-right" },
            ]
          ).map((t) => (
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
        <div className="d-flex flex-column gap-4">
        <section className="card">
          <div className={styles.stats}>
            {[
              { label: "Tổng thao tác", value: data?.totalCount ?? 0, tone: "var(--info)" },
              { label: "Lượt cập nhật", value: counts.Update ?? 0, tone: "var(--wait)", action: "Update" },
              { label: "Xóa mềm", value: counts.Delete ?? 0, tone: "var(--bad)", action: "Delete" },
              { label: "Người thao tác", value: data?.actorCount ?? 0, tone: "var(--ok)" },
            ].map((stat) => (
              <div
                className={styles.stat}
                key={stat.label}
                style={{ "--c": stat.tone, cursor: stat.action ? "pointer" : undefined } as React.CSSProperties}
                onClick={stat.action ? () => filterByAction(stat.action) : undefined}
                title={stat.action ? (filters.action === stat.action ? "Bỏ lọc" : "Chỉ xem loại thao tác này") : undefined}
              >
                <div className={styles.value}>{stat.value}</div>
                <div className={styles.label}>{stat.label}</div>
              </div>
            ))}
          </div>
        </section>

            <section className="card">
              <div className="card-header">
                <form className="row g-2 align-items-end" onSubmit={handleApplyFilters}>
                  <div className="col-12 col-md-4 col-xl-3">
                    <label className="form-label" htmlFor="audit-entity-type">Đối tượng</label>
                    <select id="audit-entity-type" className="form-select form-select-sm" value={draft.entityType} onChange={(event) => setDraft({ ...draft, entityType: event.target.value })}>
                      <option value="">Tất cả đối tượng</option>
                      {entityOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                    </select>
                  </div>
                  <div className="col-12 col-md-3 col-xl-2">
                    <label className="form-label" htmlFor="audit-action">Thao tác</label>
                    <select id="audit-action" className="form-select form-select-sm" value={draft.action} onChange={(event) => setDraft({ ...draft, action: event.target.value })}>
                      <option value="">Tất cả</option>
                      {(draft.action && !actionOptions.includes(draft.action) ? [...actionOptions, draft.action] : actionOptions).map((action) => (
                        <option key={action} value={action}>{actionMeta(action).label}</option>
                      ))}
                    </select>
                  </div>
                  <div className="col-12 col-md-5 col-xl-4">
                    <label className="form-label" htmlFor="audit-entity-id">Id đối tượng</label>
                    <input
                      id="audit-entity-id"
                      className="form-control form-control-sm"
                      value={draft.entityId}
                      onChange={(event) => setDraft({ ...draft, entityId: event.target.value })}
                    />
                  </div>
                  <div className="col-12 col-md-auto d-flex gap-2">
                    <button type="submit" className="btn btn-sm btn-primary" disabled={loading}>
                      <i className="bi bi-search me-1" />Tra cứu
                    </button>
                    <button type="button" className="btn btn-sm btn-outline-secondary" onClick={clearFilters}>
                      Xóa bộ lọc
                    </button>
                  </div>
                </form>
              </div>
              <div className="table-responsive">
                <table className="table table-hover align-middle mb-0">
                  <thead>
                    <tr>
                      <th>Thời điểm</th>
                      <th>Thao tác</th>
                      <th>Người thực hiện</th>
                      <th>Đối tượng</th>
                      <th>Đường dẫn API</th>
                    </tr>
                  </thead>
                  <tbody>
                    {loading ? (
                      <tr><td colSpan={5} className="text-center py-5 text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải nhật ký...</td></tr>
                    ) : logs.length === 0 ? (
                      <tr><td colSpan={5} className="text-center py-5 text-secondary"><i className="bi bi-inbox fs-3 d-block mb-2" />Chưa có bản ghi phù hợp.</td></tr>
                    ) : logs.map((log) => {
                      const meta = actionMeta(log.action);
                      const isSelected = selectedLog?.id === log.id;
                      return (
                        <tr key={log.id} onClick={() => setSelectedLog(log)} style={{ cursor: "pointer", background: isSelected ? "var(--bg-row-selected)" : undefined }}>
                          <td className="text-nowrap">
                            <div className="fw-semibold text-dark">{formatDate(log.createdAt).split(",")[0]}</div>
                            <div className="small text-secondary">{formatDate(log.createdAt).split(",").slice(1).join(",").trim()}</div>
                          </td>
                          <td className="text-nowrap">
                            <span className={`tag ${meta.tone}`}>
                              <i className={`bi ${meta.icon} me-1`} />{meta.label}
                            </span>
                          </td>
                          <td>
                            <div className="fw-semibold text-dark">{log.actorName || "system"}</div>
                            {log.ipAddress && <div className="small text-secondary">{log.ipAddress}</div>}
                          </td>
                          <td>
                            <div className="fw-semibold text-dark">{entityLabel(log.entityType)}</div>
                            <code className="small text-secondary">{log.entityId.slice(0, 14)}…</code>
                          </td>
                          <td className="text-secondary text-truncate" style={{ maxWidth: 320 }} title={log.requestPath || undefined}>{log.requestPath || "—"}</td>
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

        </div>
        )}
        </div>

        {tab === "actions" && selectedLog && (
          <AdminModal title="Chi tiết thay đổi" onClose={() => setSelectedLog(null)} maxWidth={900}>
            <div className="d-flex justify-content-between align-items-start gap-2 mb-3">
              <div>
                <div className="fw-semibold">{entityLabel(selectedLog.entityType)}</div>
                <code className="small text-dark text-break">{selectedLog.entityId}</code>
              </div>
              <div className="text-end">
                <div className="text-nowrap">{formatDate(selectedLog.createdAt)}</div>
                <div className="small text-secondary">{selectedLog.actorName || "system"}</div>
              </div>
            </div>
            <div className="row g-3">
              <div className="col-12 col-lg-6">
                <div className="audit-detail-block h-100">
                  <div className="small fw-semibold text-secondary mb-1">Dữ liệu trước</div>
                  <pre className="mb-0">{parseSnapshot(selectedLog.oldValues)}</pre>
                </div>
              </div>
              <div className="col-12 col-lg-6">
                <div className="audit-detail-block h-100">
                  <div className="small fw-semibold text-secondary mb-1">Dữ liệu sau</div>
                  <pre className="mb-0">{parseSnapshot(selectedLog.newValues)}</pre>
                </div>
              </div>
            </div>
          </AdminModal>
        )}
      </div>
    </div>
  );
}

export default function AuditPage() {
  return <AuditPageContent />;
}
