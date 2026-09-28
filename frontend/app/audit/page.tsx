"use client";

import React, { useEffect, useMemo, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { auditService, AuditLogDto } from "@/services/auditService";
import { Card, CardBody, PageHeader } from "@/components/common";
import { LoginEventsTab } from "@/components/admin/LoginEventsTab";
import { NoAccess } from "@/components/admin/NoAccess";
import { errorMessage } from "@/components/admin/adminUtils";

type AuditTab = "actions" | "logins";

const entityOptions = [
  { value: "", label: "Tất cả đối tượng" },
  { value: "EvaluationRecord", label: "Hồ sơ đánh giá" },
  { value: "EvaluationTask", label: "Nhiệm vụ đánh giá" },
  { value: "EvaluationPeriod", label: "Kỳ đánh giá" },
  { value: "PartyMemberProfile", label: "Hồ sơ cán bộ" },
  { value: "PartyCell", label: "Chi bộ" },
  { value: "TaskAttachment", label: "Tệp minh chứng" },
  { value: "AppRole", label: "Vai trò / phân quyền" },
  { value: "UserRoleAssignment", label: "Bản gán vai trò" },
  { value: "AdministrativeDepartment", label: "Phòng / đơn vị" },
];

/** Định dạng thời điểm audit theo locale tiếng Việt. */
function formatDate(value: string) {
  return new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

/** Trả về nhãn và màu hiển thị tương ứng với loại thao tác. */
function actionMeta(action: string) {
  switch (action) {
    case "Create":
      return { label: "Tạo mới", icon: "bi-plus-lg", color: "#047857", background: "#ecfdf5" };
    case "Delete":
      return { label: "Xóa mềm", icon: "bi-archive", color: "#b91c1c", background: "#fef2f2" };
    case "Approve":
      return { label: "Phê duyệt", icon: "bi-check2-circle", color: "#6d28d9", background: "#f5f3ff" };
    case "AssignRoles":
      return { label: "Gán vai trò", icon: "bi-person-badge", color: "#0369a1", background: "#f0f9ff" };
    case "UpdatePermissions":
      return { label: "Cập nhật quyền", icon: "bi-shield-check", color: "#0369a1", background: "#f0f9ff" };
    default:
      return { label: "Cập nhật", icon: "bi-pencil-square", color: "#1d4ed8", background: "#eff6ff" };
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
      setSelectedLog((current) => (current && result.some((log) => log.id === current.id) ? current : result[0] || null));
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
            className="btn btn-sm btn-outline-primary"
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
        <ul className="nav nav-tabs mb-3">
          {(
            [
              { key: "actions" as const, label: "Nhật ký thao tác", icon: "bi-activity" },
              { key: "logins" as const, label: "Nhật ký đăng nhập", icon: "bi-box-arrow-in-right" },
            ]
          ).map((t) => (
            <li className="nav-item" key={t.key}>
              <button type="button" className={`nav-link ${tab === t.key ? "active fw-semibold" : ""}`} onClick={() => setTab(t.key)}>
                <i className={`bi ${t.icon} me-1`} />
                {t.label}
              </button>
            </li>
          ))}
        </ul>

        {tab === "logins" && <LoginEventsTab />}

        {tab === "actions" && (
        <>
        <div className="row g-3 mb-3">
          {[
            { label: "Tổng thao tác", value: logs.length, icon: "bi-activity", color: "#1d4ed8", background: "#eff6ff" },
            { label: "Lượt cập nhật", value: stats.updateCount, icon: "bi-pencil-square", color: "#0369a1", background: "#f0f9ff" },
            { label: "Xóa mềm", value: stats.deleteCount, icon: "bi-archive", color: "#b91c1c", background: "#fef2f2" },
            { label: "Người thao tác", value: stats.actorCount, icon: "bi-people", color: "#047857", background: "#ecfdf5" },
          ].map((stat) => (
            <div className="col-6 col-xl-3" key={stat.label}>
              <Card>
                <CardBody className="d-flex align-items-center gap-3 py-3">
                  <div
                    className="d-flex align-items-center justify-content-center rounded-3"
                    style={{ width: 40, height: 40, color: stat.color, background: stat.background, flexShrink: 0 }}
                  >
                    <i className={`bi ${stat.icon} fs-5`} />
                  </div>
                  <div>
                    <div className="small text-secondary">{stat.label}</div>
                    <div className="fs-5 fw-bold text-dark">{stat.value}</div>
                  </div>
                </CardBody>
              </Card>
            </div>
          ))}
        </div>

        <Card className="mb-3">
          <CardBody>
            <form className="row g-2 align-items-end" onSubmit={handleApplyFilters}>
              <div className="col-12 col-md-4 col-xl-3">
                <label className="form-label small fw-semibold text-secondary mb-1">Đối tượng</label>
                <select className="form-select form-select-sm" value={entityType} onChange={(event) => setEntityType(event.target.value)}>
                  {entityOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </select>
              </div>
              <div className="col-12 col-md-5 col-xl-5">
                <label className="form-label small fw-semibold text-secondary mb-1">Id đối tượng</label>
                <input
                  className="form-control form-control-sm"
                  value={entityId}
                  onChange={(event) => setEntityId(event.target.value)}
                  placeholder="Dán Id hồ sơ, kỳ đánh giá hoặc bản ghi cần tra cứu"
                />
              </div>
              <div className="col-12 col-md-auto">
                <button type="submit" className="btn btn-sm btn-primary" disabled={loading}>
                  <i className="bi bi-search me-1" />Tra cứu
                </button>
              </div>
              <div className="col-12 col-md-auto">
                <button type="button" className="btn btn-sm btn-link text-secondary text-decoration-none" onClick={clearFilters}>
                  Xóa bộ lọc
                </button>
              </div>
            </form>
          </CardBody>
        </Card>

        <div className="row g-3 align-items-start">
          <div className="col-12 col-xl-8">
            <Card>
              <div className="table-responsive">
                <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
                  <thead style={{ background: "var(--bg-base)" }}>
                    <tr>
                      <th className="ps-3">Thời điểm</th>
                      <th>Thao tác</th>
                      <th>Người thực hiện</th>
                      <th>Đối tượng</th>
                      <th className="pe-3">Endpoint</th>
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
                          <td className="ps-3 text-nowrap">
                            <div className="fw-semibold text-dark">{formatDate(log.createdAt).split(",")[0]}</div>
                            <div className="small text-secondary">{formatDate(log.createdAt).split(",").slice(1).join(",").trim()}</div>
                          </td>
                          <td>
                            <span className="badge border" style={{ color: meta.color, background: meta.background, borderColor: `${meta.color}33` }}>
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
                          <td className="pe-3 text-secondary small text-truncate" style={{ maxWidth: 180 }}>{log.requestPath || "—"}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </Card>
          </div>

          <div className="col-12 col-xl-4">
            <Card>
              <div className="card-header bg-white border-bottom py-3 px-3">
                <div className="d-flex align-items-center gap-2">
                  <i className="bi bi-file-diff text-primary" />
                  <span className="fw-bold text-dark">Chi tiết thay đổi</span>
                </div>
              </div>
              <CardBody>
                {selectedLog ? (
                  <div>
                    <div className="d-flex justify-content-between align-items-start gap-2 mb-3">
                      <div>
                        <div className="small text-secondary">{entityLabel(selectedLog.entityType)}</div>
                        <code className="small text-dark text-break">{selectedLog.entityId}</code>
                      </div>
                      <span className="small text-secondary text-nowrap">{formatDate(selectedLog.createdAt)}</span>
                    </div>
                    <div className="audit-detail-block mb-3">
                      <div className="small fw-semibold text-secondary mb-1">Dữ liệu trước</div>
                      <pre className="mb-0">{parseSnapshot(selectedLog.oldValues)}</pre>
                    </div>
                    <div className="audit-detail-block">
                      <div className="small fw-semibold text-secondary mb-1">Dữ liệu sau</div>
                      <pre className="mb-0">{parseSnapshot(selectedLog.newValues)}</pre>
                    </div>
                  </div>
                ) : (
                  <div className="text-center text-secondary py-4 small">
                    <i className="bi bi-cursor-text fs-4 d-block mb-2" />Chọn một dòng để xem snapshot thay đổi.
                  </div>
                )}
              </CardBody>
            </Card>
          </div>
        </div>
        </>
        )}
      </div>
    </div>
  );
}

/** Route trang nhật ký hệ thống. */
export default function AuditPage() {
  return <AuditPageContent />;
}
