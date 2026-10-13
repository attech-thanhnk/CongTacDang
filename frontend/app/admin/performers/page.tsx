"use client";

import React, { Suspense, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, CardBody, PageHeader } from "@/components/common";
import { NoAccess } from "@/components/admin/NoAccess";
import { AdminModal } from "@/components/admin/AdminModal";
import { UserPicker, PickedUser } from "@/components/admin/UserPicker";
import { useCatalogOptions } from "@/components/admin/useCatalogOptions";
import {
  endOfDayExclusiveIso,
  errorMessage,
  errorTitle,
  exclusiveIsoToLastDayInput,
  formatDateOnly,
} from "@/components/admin/adminUtils";
import { treeLabel } from "@/services/catalogService";
import {
  Performer,
  PerformerJob,
  PerformerReviewItem,
  PositionHolder,
  roleService,
  ScopeType,
  UnitPerformers,
} from "@/services/roleService";

type UnitKind = ScopeType;

const UNIT_KIND_LABELS: Record<UnitKind, string> = {
  Global: "Toàn công ty",
  Department: "Đơn vị chính quyền",
  PartyCell: "Tổ chức Đảng",
};

/** "Người thực hiện": chọn đơn vị → mỗi việc của quy trình đánh giá có ai làm, thêm/gỡ người ngay tại đơn vị. */
export default function PerformersPage() {
  return (
    <Suspense fallback={<div className="page-wrapper"><div className="page-body text-secondary small">Đang tải…</div></div>}>
      <PerformersContent />
    </Suspense>
  );
}

function PerformersContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { hasPermission, hasPermissionIn } = useAuth();
  const { toast, confirm } = useToast();
  const canAssign = hasPermissionIn("system.assignments.manage", "Global");
  const canViewUsers = hasPermission("system.users.read");
  const canManageRoles = hasPermissionIn("system.roles.manage", "Global");

  const departmentId = searchParams.get("departmentId") ?? "";
  const partyCellId = searchParams.get("partyCellId") ?? "";
  const focusPermission = searchParams.get("permission") ?? "";
  const urlKind: UnitKind = departmentId ? "Department" : partyCellId ? "PartyCell" : "Global";

  const { departments, partyCells, error: catalogError } = useCatalogOptions();
  const [kind, setKind] = useState<UnitKind>(urlKind);
  const [data, setData] = useState<UnitPerformers | null>(null);
  const [loading, setLoading] = useState(false);
  const [adding, setAdding] = useState<PerformerJob | null>(null);
  const [saving, setSaving] = useState(false);
  const [review, setReview] = useState<PerformerReviewItem[] | null>(null);
  const [reviewOpen, setReviewOpen] = useState(true);

  useEffect(() => setKind(urlKind), [urlKind]);

  const selectUnit = (next: { departmentId?: string; partyCellId?: string }) => {
    const params = new URLSearchParams();
    if (next.departmentId) params.set("departmentId", next.departmentId);
    if (next.partyCellId) params.set("partyCellId", next.partyCellId);
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname);
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(
        await roleService.getPerformers({ departmentId: departmentId || undefined, partyCellId: partyCellId || undefined })
      );
    } catch (err) {
      setData(null);
      toast.error(errorMessage(err, "Không tải được danh sách người thực hiện."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [departmentId, partyCellId]);

  const loadReview = useCallback(async () => {
    try {
      setReview(await roleService.getPerformerReview());
    } catch (err) {
      setReview(null);
      toast.error(errorMessage(err, "Không tải được danh sách bản gán cần xem lại."), errorTitle(err));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (canAssign) load();
  }, [canAssign, load]);

  useEffect(() => {
    if (canAssign) loadReview();
  }, [canAssign, loadReview]);

  useEffect(() => {
    if (!data || !focusPermission) return;
    document.getElementById(`job-${focusPermission}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
  }, [data, focusPermission]);

  const unitOptions = (kind === "Department" ? departments : kind === "PartyCell" ? partyCells : []).filter(
    (item) => item.isActive || item.id === departmentId || item.id === partyCellId
  );
  const selectedUnitId = kind === "Department" ? departmentId : kind === "PartyCell" ? partyCellId : "";
  const waitingForUnit = kind !== "Global" && !selectedUnitId;
  const missing = useMemo(() => (data ? data.jobs.filter((job) => job.inUse && job.people.length === 0).length : 0), [data]);

  const handleRemove = (job: PerformerJob, person: Performer) =>
    confirm({
      title: "Gỡ người thực hiện",
      message:
        `Gỡ ${person.fullName} khỏi việc "${job.permissionName}" tại ${person.scopeText}? ` +
        `Bản gán vai trò "${person.roleName}" sẽ kết thúc ngay, người này mất mọi việc khác của vai trò đó tại đơn vị này.`,
      confirmText: "Gỡ",
      isDanger: true,
      onConfirm: async () => {
        try {
          await roleService.endAssignment(person.assignmentId);
          toast.success(`Đã gỡ ${person.fullName}.`);
          await Promise.all([load(), loadReview()]);
        } catch (err) {
          toast.error(errorMessage(err, "Không gỡ được người thực hiện."), errorTitle(err), 8000);
        }
      },
    });

  const handleRemoveReviewed = (item: PerformerReviewItem) =>
    confirm({
      title: "Gỡ bản gán",
      message:
        `Gỡ vai trò "${item.roleName}" của ${item.fullName} tại ${item.scopeText}? ` +
        "Bản gán kết thúc ngay, người này mất mọi việc của vai trò đó tại nơi giao.",
      confirmText: "Gỡ",
      isDanger: true,
      onConfirm: async () => {
        try {
          await roleService.endAssignment(item.assignmentId);
          toast.success(`Đã gỡ ${item.fullName}.`);
          await Promise.all([load(), loadReview()]);
        } catch (err) {
          toast.error(errorMessage(err, "Không gỡ được bản gán."), errorTitle(err), 8000);
        }
      },
    });

  const viewUnit = (item: PerformerReviewItem) => {
    if (item.scopeType === "Department" && item.scopeId) selectUnit({ departmentId: item.scopeId });
    else if (item.scopeType === "PartyCell" && item.scopeId) selectUnit({ partyCellId: item.scopeId });
    else selectUnit({});
  };

  const handleAdd = async (payload: { user: PickedUser; roleId: string; validTo: string; note: string }) => {
    if (!data) return;
    setSaving(true);
    try {
      await roleService.createAssignment({
        userId: payload.user.id,
        roleId: payload.roleId,
        scopeType: data.scopeType,
        scopeId: data.scopeType === "Global" ? undefined : data.scopeId ?? undefined,
        validTo: endOfDayExclusiveIso(payload.validTo),
        note: payload.note.trim() || undefined,
      });
      toast.success(`Đã thêm ${payload.user.fullName}.`);
      setAdding(null);
      await Promise.all([load(), loadReview()]);
    } catch (err) {
      toast.error(errorMessage(err, "Không thêm được người thực hiện."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  if (!canAssign) return <NoAccess title="Người thực hiện" permissionName="Gán vai trò" />;

  return (
    <div className="page-wrapper">
      <PageHeader title="Người thực hiện" />
      <div className="page-body">
        <ReviewCard
          items={review}
          open={reviewOpen}
          onToggle={() => setReviewOpen((v) => !v)}
          onViewUnit={viewUnit}
          onRemove={handleRemoveReviewed}
        />
        <Card className="mb-3">
          <CardBody>
            <div className="row g-2 align-items-end">
              <div className="col-12 col-md-4">
                <label className="form-label small mb-1">Xem theo</label>
                <select
                  className="form-select form-select-sm"
                  value={kind}
                  onChange={(e) => {
                    const next = e.target.value as UnitKind;
                    setKind(next);
                    if (next === "Global") selectUnit({});
                  }}
                >
                  {(Object.keys(UNIT_KIND_LABELS) as UnitKind[]).map((k) => (
                    <option key={k} value={k}>
                      {UNIT_KIND_LABELS[k]}
                    </option>
                  ))}
                </select>
              </div>
              {kind !== "Global" && (
                <div className="col-12 col-md-8">
                  <label className="form-label small mb-1">{kind === "Department" ? "Đơn vị chính quyền" : "Tổ chức Đảng"}</label>
                  <select
                    className="form-select form-select-sm"
                    value={selectedUnitId}
                    onChange={(e) =>
                      selectUnit(kind === "Department" ? { departmentId: e.target.value } : { partyCellId: e.target.value })
                    }
                  >
                    <option value="">— Chọn {kind === "Department" ? "đơn vị chính quyền" : "tổ chức Đảng"} —</option>
                    {unitOptions.map((item) => (
                      <option key={item.id} value={item.id}>
                        {treeLabel(item)}
                      </option>
                    ))}
                  </select>
                </div>
              )}
            </div>
            {catalogError && <div className="small text-danger mt-2">{catalogError}</div>}
          </CardBody>
        </Card>

        {waitingForUnit ? (
          <div className="text-secondary small">Hãy chọn đơn vị để xem.</div>
        ) : loading && !data ? (
          <div className="text-secondary small">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải…
          </div>
        ) : data ? (
          <>
            <div className="d-flex flex-wrap align-items-center gap-2 mb-2">
              <h2 className="h6 mb-0">{data.scopeName}</h2>
              {missing > 0 ? (
                <span className="badge bg-danger-subtle text-danger-emphasis">{missing} việc chưa có người</span>
              ) : (
                <span className="badge bg-success-subtle text-success-emphasis">Mọi việc đều có người</span>
              )}
              {!data.scopeActive && <span className="badge bg-secondary">Đơn vị đã ngừng hoạt động</span>}
              {loading && <span className="spinner-border spinner-border-sm text-secondary" />}
            </div>
            {data.scopeType !== "Global" && <HoldersLine holders={data.holders} />}
            <div className="d-flex flex-column gap-2">
              {data.jobs.map((job) => (
                <JobCard
                  key={job.permission}
                  job={job}
                  focused={job.permission === focusPermission}
                  canAdd={data.scopeActive}
                  canViewUsers={canViewUsers}
                  onAdd={() => setAdding(job)}
                  onRemove={(person) => handleRemove(job, person)}
                />
              ))}
            </div>
          </>
        ) : null}
      </div>

      {adding && data && (
        <AddPerformerModal
          job={adding}
          unitName={data.scopeName}
          holders={data.holders}
          canManageRoles={canManageRoles}
          saving={saving}
          onClose={() => setAdding(null)}
          onSubmit={handleAdd}
        />
      )}
    </div>
  );
}

const EXPIRING_SOON_MS = 7 * 24 * 60 * 60 * 1000;

/** Ngày cuối còn hiệu lực của bản gán (validTo không bao gồm) dạng dd/MM/yyyy. */
function lastDay(validTo: string): string {
  return formatDateOnly(exclusiveIsoToLastDayInput(validTo));
}

function isExpiringSoon(validTo: string): boolean {
  return new Date(validTo).getTime() - Date.now() <= EXPIRING_SOON_MS;
}

function TemporaryBadge({ validTo }: { validTo: string }) {
  const soon = isExpiringSoon(validTo);
  return (
    <span className={`badge ${soon ? "bg-warning-subtle text-warning-emphasis" : "bg-info-subtle text-info-emphasis"}`}>
      <i className="bi bi-clock me-1" />
      Giao tạm đến {lastDay(validTo)}
    </span>
  );
}

/** Người giữ chức vụ tại đơn vị đang xem. */
function HoldersLine({ holders }: { holders: PositionHolder[] }) {
  if (holders.length === 0) return null;
  return (
    <div className="small mb-2 px-2 py-1 rounded border bg-light">
      <span className="text-secondary">Chức vụ tại đơn vị: </span>
      {holders.map((h, i) => (
        <React.Fragment key={`${h.userId}-${h.positionName}`}>
          {i > 0 && "; "}
          {h.positionName} — <span className="fw-semibold">{h.fullName}</span>
        </React.Fragment>
      ))}
    </div>
  );
}

/** Bản gán cần xem lại trên toàn công ty. */
function ReviewCard({
  items,
  open,
  onToggle,
  onViewUnit,
  onRemove,
}: {
  items: PerformerReviewItem[] | null;
  open: boolean;
  onToggle: () => void;
  onViewUnit: (item: PerformerReviewItem) => void;
  onRemove: (item: PerformerReviewItem) => void;
}) {
  if (!items || items.length === 0) return null;
  return (
    <div className="card shadow-sm border-warning mb-3">
      <div className="card-body py-2">
        <button
          type="button"
          className="btn btn-link p-0 text-decoration-none fw-semibold text-warning-emphasis"
          onClick={onToggle}
          aria-expanded={open}
        >
          <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"} me-1`} />
          Cần xem lại ({items.length})
        </button>
        {open && (
          <ul className="list-unstyled small mb-0 mt-2">
            {items.map((item) => (
              <li key={item.assignmentId} className="d-flex flex-wrap align-items-center gap-2 py-1 border-top">
                <span className="fw-semibold">{item.fullName}</span>
                <span className="text-secondary">({item.username})</span>
                <span className="text-secondary">
                  — vai trò {item.roleName}, giao tại {item.scopeText}
                </span>
                <span
                  className={`badge ${
                    item.reasonCode === "ExpiringSoon" ? "bg-info-subtle text-info-emphasis" : "bg-warning-subtle text-warning-emphasis"
                  }`}
                >
                  {item.reason}
                </span>
                <span className="ms-auto d-flex gap-3">
                  <button type="button" className="btn btn-sm btn-link p-0" onClick={() => onViewUnit(item)}>
                    Xem đơn vị
                  </button>
                  <button type="button" className="btn btn-sm btn-link text-danger p-0" onClick={() => onRemove(item)}>
                    Gỡ
                  </button>
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function JobCard({
  job,
  focused,
  canAdd,
  canViewUsers,
  onAdd,
  onRemove,
}: {
  job: PerformerJob;
  focused: boolean;
  canAdd: boolean;
  canViewUsers: boolean;
  onAdd: () => void;
  onRemove: (person: Performer) => void;
}) {
  const empty = job.people.length === 0;
  const missing = empty && job.inUse;
  return (
    <div id={`job-${job.permission}`} className={`card shadow-sm ${missing ? "border-danger" : "border-0"} ${job.inUse ? "" : "opacity-50"} ${focused ? "border-primary border-2" : ""}`}>
      <div className="card-body py-2">
        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
          <div className="fw-semibold">
            {job.permissionName}
            {missing && <span className="badge bg-danger ms-2">Chưa có người</span>}
            {!job.inUse && <span className="badge bg-secondary-subtle text-secondary-emphasis ms-2">Kỳ hiện tại không dùng</span>}
          </div>
          <button type="button" className="btn btn-sm btn-outline-primary" disabled={!canAdd} onClick={onAdd}>
            <i className="bi bi-person-plus me-1" />
            Thêm người
          </button>
        </div>
        {!empty && (
          <ul className="list-unstyled small mb-0 mt-2">
            {job.people.map((person) => (
              <li key={`${person.userId}-${person.assignmentId}`} className="d-flex flex-wrap align-items-center gap-2 py-1 border-top">
                <span className="fw-semibold">{person.fullName}</span>
                <span className="text-secondary">({person.username})</span>
                {person.positionAtScope && <span className="text-body">· {person.positionAtScope}</span>}
                <span className="text-secondary">
                  — vai trò {person.roleName}, giao tại {person.scopeText}
                </span>
                {person.validTo ? (
                  <TemporaryBadge validTo={person.validTo} />
                ) : (
                  person.scopeType !== "Global" &&
                  !person.positionAtScope && (
                    <span
                      className="badge bg-warning-subtle text-warning-emphasis"
                      title="Nếu đã luân chuyển thì gỡ; nếu là phụ trách tạm thì đặt thời hạn."
                    >
                      <i className="bi bi-exclamation-triangle me-1" />
                      Không giữ chức vụ ở nơi giao
                    </span>
                  )
                )}
                <span className="ms-auto">
                  {person.assignedHere ? (
                    <button type="button" className="btn btn-sm btn-link text-danger p-0" onClick={() => onRemove(person)}>
                      Gỡ
                    </button>
                  ) : canViewUsers ? (
                    <Link href={`/admin/users/${person.userId}`} className="btn btn-sm btn-link p-0">
                      Xem tài khoản
                    </Link>
                  ) : null}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function AddPerformerModal({
  job,
  unitName,
  holders,
  canManageRoles,
  saving,
  onClose,
  onSubmit,
}: {
  job: PerformerJob;
  unitName: string;
  holders: PositionHolder[];
  canManageRoles: boolean;
  saving: boolean;
  onClose: () => void;
  onSubmit: (payload: { user: PickedUser; roleId: string; validTo: string; note: string }) => void;
}) {
  const [user, setUser] = useState<PickedUser | null>(null);
  const [roleId, setRoleId] = useState(job.roles.length === 1 ? job.roles[0].id : "");
  const [validTo, setValidTo] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const role = job.roles.find((r) => r.id === roleId);

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (!user) {
      setError("Hãy chọn người.");
      return;
    }
    if (!roleId) {
      setError("Hãy chọn vai trò.");
      return;
    }
    onSubmit({ user, roleId, validTo, note });
  };

  return (
    <AdminModal title="Thêm người thực hiện" onClose={onClose} closeDisabled={saving} maxWidth={560}>
      <div className="small mb-3 p-2 rounded border bg-light">
        Việc <strong>{job.permissionName}</strong> tại <strong>{unitName}</strong>
      </div>
      {job.roles.length === 0 ? (
        <div className="alert alert-warning small mb-0">
          Chưa có vai trò nào làm được việc này tại đơn vị này.{" "}
          {canManageRoles ? (
            <>
              Hãy <Link href="/admin/roles">tạo hoặc sửa vai trò</Link> trước.
            </>
          ) : (
            "Hãy nhờ người quản lý vai trò bổ sung."
          )}
        </div>
      ) : (
        <form onSubmit={handleSubmit}>
          <div className="mb-2">
            <label className="form-label">
              Người thực hiện <span className="text-danger">*</span>
            </label>
            {holders.length > 0 && (
              <div className="d-flex flex-wrap align-items-center gap-1 mb-2 small">
                <span className="text-secondary">Chọn nhanh:</span>
                {holders.map((h) => (
                  <button
                    key={`${h.userId}-${h.positionName}`}
                    type="button"
                    className={`btn btn-sm py-0 ${user?.id === h.userId ? "btn-primary" : "btn-outline-secondary"}`}
                    onClick={() => setUser({ id: h.userId, username: h.username, fullName: h.fullName })}
                  >
                    {h.fullName} ({h.positionName})
                  </button>
                ))}
              </div>
            )}
            <UserPicker value={user} onChange={setUser} />
          </div>
          <div className="mb-2">
            <label className="form-label">
              Vai trò <span className="text-danger">*</span>
            </label>
            <select className="form-select form-select-sm" value={roleId} onChange={(e) => setRoleId(e.target.value)}>
              <option value="">— Chọn vai trò —</option>
              {job.roles.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
            {role?.description && <div className="form-text">{role.description}</div>}
          </div>
          <div className="row g-2 mb-2">
            <div className="col-12 col-md-6">
              <label className="form-label">Đến hết ngày</label>
              <input type="date" className="form-control form-control-sm" value={validTo} onChange={(e) => setValidTo(e.target.value)} />
              <div className="form-text">Để trống = không thời hạn.</div>
            </div>
          </div>
          <div className="mb-2">
            <label className="form-label">Ghi chú</label>
            <textarea
              className="form-control form-control-sm"
              rows={2}
              maxLength={1000}
              value={note}
              placeholder="VD: theo Quyết định số …"
              onChange={(e) => setNote(e.target.value)}
            />
          </div>
          {error && <div className="alert alert-danger small py-2 mb-2">{error}</div>}
          <div className="d-flex justify-content-end gap-2 mt-3">
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={onClose} disabled={saving}>
              Hủy
            </button>
            <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Thêm
            </button>
          </div>
        </form>
      )}
    </AdminModal>
  );
}
