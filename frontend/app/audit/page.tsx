"use client";

import React, { useEffect, useMemo, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { auditService, AuditLogDto } from "@/services/auditService";
import { PageHeader } from "@/components/common";
import styles from "./audit.module.css";
import { LoginEventsTab } from "@/components/admin/LoginEventsTab";
import { NoAccess } from "@/components/admin/NoAccess";
import { AdminModal } from "@/components/admin/AdminModal";
import { errorMessage, formatDateTime } from "@/components/admin/adminUtils";

type AuditTab = "actions" | "logins";

const entityOptions = [
  { value: "", label: "Tất cả đối tượng" },
  { value: "EvaluationRecord", label: "Hồ sơ đánh giá" },
  { value: "EvaluationTask", label: "Nhiệm vụ đánh giá" },
  { value: "EvaluationPeriod", label: "Kỳ đánh giá" },
  { value: "PartyMemberProfile", label: "Hồ sơ cán bộ" },
  { value: "PartyCell", label: "Tổ chức Đảng" },
  { value: "TaskAttachment", label: "Tệp minh chứng" },
  { value: "AppRole", label: "Vai trò / phân quyền" },
  { value: "UserRoleAssignment", label: "Bản gán vai trò" },
  { value: "AdministrativeDepartment", label: "Đơn vị chính quyền" },
];

/** Định dạng thời điểm audit theo locale tiếng Việt. */
function formatDate(value: string) {
  return formatDateTime(value);
}

/** Trả về nhãn và màu hiển thị tương ứng với loại thao tác. */
function actionMeta(action: string) {
  switch (action) {
    case "Create":
      return { label: "Tạo mới", icon: "bi-plus-lg", tone: "t-ok" };
    case "Delete":
      return { label: "Xóa mềm", icon: "bi-archive", tone: "t-bad" };
    case "Approve":
      return { label: "Phê duyệt", icon: "bi-check2-circle", tone: "t-ok" };
    case "AssignRoles":
      return { label: "Gán vai trò", icon: "bi-person-badge", tone: "t-wait" };
    case "UpdatePermissions":
      return { label: "Cập nhật quyền", icon: "bi-shield-check", tone: "t-wait" };
    default:
      return { label: "Cập nhật", icon: "bi-pencil-square", tone: "t-info" };
  }
}

/** Chuyển tên CLR entity thành nhãn dễ đọc trên giao diện. */
function entityLabel(entityType: string) {
  return entityOptions.find((option) => option.value === entityType)?.label || entityType;
}

/** Parse snapshot JSON để hiển thị dễ đọc và an toàn khi dữ liệu lỗi. */
function parseSnapshot(value: string) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value || "{}";
  }
}

/** Nội dung trang nhật ký: tab Nhật ký thao tác + tab Nhật ký đăng nhập. */
function AuditPageContent() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const [tab, setTab] = useState<AuditTab>("actions");
  const [logs, setLogs] = useState<AuditLogDto[]>([]);
  const [entityType, setEntityType] = useState("");
  const [entityId, setEntityId] = useState("");
  const [appliedEntityId, setAppliedEntityId] = useState("");
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);
  const [loading, setLoading] = useState(true);

  /** Tải lại audit log theo bộ lọc hiện tại hoặc bộ lọc truyền vào. */
  const loadLogs = async (filters?: { entityType?: string; entityId?: string }) => {
    setLoading(true);
    try {
      const nextEntityType = filters?.entityType ?? entityType;
      const nextEntityId = filters?.entityId ?? appliedEntityId;
      const result = await auditService.getLogs({
        entityType: nextEntityType || undefined,
        entityId: nextEntityId || undefined,
        limit: 200,
      });
      setLogs(result);
      setSelectedLog((current) => (current && result.some((log) => log.id === current.id) ? current : null));
    } catch (error) {
      toast.error(errorMessage(error, "Không thể tải nhật ký thao tác."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (hasPermission("system.audit.read")) {
      loadLogs();
    }
  }, [hasPermission]);

  const stats = useMemo(() => {
    const actorCount = new Set(logs.map((log) => log.actorId || log.actorName)).size;
    const updateCount = logs.filter((log) => log.action === "Update").length;
    const deleteCount = logs.filter((log) => log.action === "Delete").length;
    return { actorCount, updateCount, deleteCount };
  }, [logs]);

  /** Áp dụng bộ lọc tra cứu và đồng bộ Id đang hiển thị. */
  const handleApplyFilters = async (event: React.FormEvent) => {
    event.preventDefault();
    setAppliedEntityId(entityId.trim());
    await loadLogs({ entityType, entityId: entityId.trim() });
  };

  /** Xóa toàn bộ bộ lọc và tải lại nhật ký mới nhất. */
  const clearFilters = async () => {
    setEntityType("");
    setEntityId("");
    setAppliedEntityId("");
    await loadLogs({ entityType: "", entityId: "" });
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
              { label: "Tổng thao tác", value: logs.length, tone: "var(--info)" },
              { label: "Lượt cập nhật", value: stats.updateCount, tone: "var(--wait)" },
              { label: "Xóa mềm", value: stats.deleteCount, tone: "var(--bad)" },
              { label: "Người thao tác", value: stats.actorCount, tone: "var(--ok)" },
            ].map((stat) => (
              <div className={styles.stat} key={stat.label} style={{ "--c": stat.tone } as React.CSSProperties}>
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
                    <select id="audit-entity-type" className="form-select form-select-sm" value={entityType} onChange={(event) => setEntityType(event.target.value)}>
                      {entityOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                    </select>
                  </div>
                  <div className="col-12 col-md-5 col-xl-5">
                    <label className="form-label" htmlFor="audit-entity-id">Id đối tượng</label>
                    <input
                      id="audit-entity-id"
                      className="form-control form-control-sm"
                      value={entityId}
                      onChange={(event) => setEntityId(event.target.value)}
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
                      <tr><td colSpan={5} className="text-center py-5 text-secondary"><span className="spinner-border spinner-border-sm me-2" />Đang tải nhật ký...</td></tr>
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

/** Route trang nhật ký hệ thống. */
export default function AuditPage() {
  return <AuditPageContent />;
}
