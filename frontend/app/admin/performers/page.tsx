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
import { endOfDayExclusiveIso, errorMessage, errorTitle, formatDate } from "@/components/admin/adminUtils";
import { treeLabel } from "@/services/catalogService";
import { Performer, PerformerJob, roleService, ScopeType, UnitPerformers } from "@/services/roleService";

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

  useEffect(() => {
    if (canAssign) load();
  }, [canAssign, load]);

  useEffect(() => {
    if (!data || !focusPermission) return;
    document.getElementById(`job-${focusPermission}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
  }, [data, focusPermission]);

  const unitOptions = (kind === "Department" ? departments : kind === "PartyCell" ? partyCells : []).filter(
    (item) => item.isActive || item.id === departmentId || item.id === partyCellId
  );
  const selectedUnitId = kind === "Department" ? departmentId : kind === "PartyCell" ? partyCellId : "";
  const waitingForUnit = kind !== "Global" && !selectedUnitId;
  const missing = useMemo(() => (data ? data.jobs.filter((job) => job.people.length === 0).length : 0), [data]);

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
          await load();
        } catch (err) {
          toast.error(errorMessage(err, "Không gỡ được người thực hiện."), errorTitle(err), 8000);
        }
      },
    });

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
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Không thêm được người thực hiện."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  if (!canAssign) return <NoAccess title="Người thực hiện" permissionName="Gán vai trò" />;

  return (
    <div className="page-wrapper">
      <PageHeader title="Người thực hiện" subTitle="Ai làm từng việc của quy trình đánh giá tại mỗi đơn vị" />
      <div className="page-body">
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
            <div className="small text-secondary mt-2">
              Người được giao ở đơn vị cấp trên hoặc toàn công ty cũng làm được việc ở đơn vị cấp dưới.
            </div>
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
          canManageRoles={canManageRoles}
          saving={saving}
          onClose={() => setAdding(null)}
          onSubmit={handleAdd}
        />
      )}
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
  return (
    <div id={`job-${job.permission}`} className={`card shadow-sm ${empty ? "border-danger" : "border-0"} ${focused ? "border-primary border-2" : ""}`}>
      <div className="card-body py-2">
        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
          <div className="fw-semibold">
            {job.permissionName}
            {empty && <span className="badge bg-danger ms-2">Chưa có người</span>}
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
                <span className="text-secondary">
                  — vai trò {person.roleName}, giao tại {person.scopeText}
                  {person.validTo && `, đến ${formatDate(person.validTo)}`}
                </span>
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
  canManageRoles,
  saving,
  onClose,
  onSubmit,
}: {
  job: PerformerJob;
  unitName: string;
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
