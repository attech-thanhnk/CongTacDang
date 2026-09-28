"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { AssignmentFormModal } from "@/components/admin/AssignmentFormModal";
import { AssignmentTable } from "@/components/admin/AssignmentTable";
import { NoAccess } from "@/components/admin/NoAccess";
import { PermissionMatrix } from "@/components/admin/PermissionMatrix";
import { RoleFormModal } from "@/components/admin/RoleFormModal";
import { errorMessage, errorTitle } from "@/components/admin/adminUtils";
import { useCatalogOptions } from "@/components/admin/useCatalogOptions";
import {
  AdminRole,
  CreateAssignmentPayload,
  PermissionModule,
  RoleAssignment,
  roleService,
  SaveRolePayload,
  UpdateAssignmentPayload,
} from "@/services/roleService";

/** Hai quyền quản trị mà vai trò bảo vệ bắt buộc giữ. */
const PROTECTED_CODES = new Set(["system.roles.manage", "system.assignments.manage"]);

type Tab = "permissions" | "assignments";

/** Chi tiết vai trò: ma trận quyền + người đang được gán. */
export default function AdminRoleDetailPage({ params }: { params: { id: string } }) {
  const roleId = params.id;
  const router = useRouter();
  const { user: currentUser, hasPermissionIn } = useAuth();
  const { toast, confirm } = useToast();
  const { departments, partyCells } = useCatalogOptions();
  const canManage = hasPermissionIn("system.roles.manage", "Global");
  const canAssign = hasPermissionIn("system.assignments.manage", "Global");

  const [role, setRole] = useState<AdminRole | null>(null);
  const [modules, setModules] = useState<PermissionModule[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [tab, setTab] = useState<Tab>("permissions");
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState(false);

  const [assignments, setAssignments] = useState<RoleAssignment[]>([]);
  const [showHistory, setShowHistory] = useState(false);
  const [assignmentForm, setAssignmentForm] = useState<{ assignment?: RoleAssignment } | null>(null);

  const load = useCallback(async () => {
    try {
      const [roleData, moduleData] = await Promise.all([roleService.getRole(roleId), roleService.listPermissions()]);
      setRole(roleData);
      setModules(moduleData);
      setSelected(new Set(roleData.permissionCodes));
      setLoadError(null);
    } catch (err) {
      setLoadError(errorMessage(err, "Không tải được vai trò."));
    }
  }, [roleId]);

  const loadAssignments = useCallback(async () => {
    if (!canAssign) return;
    try {
      setAssignments(await roleService.listAssignments({ roleId }));
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh sách người được gán."), errorTitle(err));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roleId, canAssign]);

  useEffect(() => {
    if (canManage) load();
  }, [canManage, load]);

  useEffect(() => {
    loadAssignments();
  }, [loadAssignments]);

  const globalOnlyCodes = useMemo(
    () => new Set(modules.flatMap((m) => m.permissions.filter((p) => !p.appliesScope).map((p) => p.code))),
    [modules]
  );

  const dirty = useMemo(() => {
    if (!role) return false;
    if (role.permissionCodes.length !== selected.size) return true;
    return role.permissionCodes.some((code) => !selected.has(code));
  }, [role, selected]);

  const heldBySelf = assignments.some((a) => a.userId === currentUser?.id && a.status === "Active");
  const lockedCodes = role?.isProtected ? PROTECTED_CODES : undefined;
  const scopedAssignments = assignments.filter((a) => a.status !== "Expired" && a.scopeType !== "Global").length;
  const addedGlobalOnly = role ? Array.from(selected).filter((code) => globalOnlyCodes.has(code) && !role.permissionCodes.includes(code)) : [];

  const savePermissions = async () => {
    if (!role) return;
    setSaving(true);
    try {
      const updated = await roleService.setRolePermissions(role.id, Array.from(selected));
      setRole(updated);
      setSelected(new Set(updated.permissionCodes));
      toast.success(`Đã lưu quyền của vai trò "${updated.name}".`);
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được quyền."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  const handleRename = async (payload: SaveRolePayload) => {
    if (!role) return;
    setSaving(true);
    try {
      setRole(await roleService.updateRole(role.id, payload));
      setEditing(false);
      toast.success("Đã cập nhật vai trò.");
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được vai trò."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = () => {
    if (!role) return;
    confirm({
      title: "Xóa vai trò",
      message:
        role.assignmentCount > 0
          ? `Vai trò "${role.name}" đang có ${role.assignmentCount} bản gán đang hoặc sắp hiệu lực. Hãy kết thúc các bản gán trước khi xóa. Vẫn thử xóa?`
          : `Xóa vai trò "${role.name}"?`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await roleService.deleteRole(role.id);
          toast.success(`Đã xóa vai trò "${role.name}".`);
          router.push("/admin/roles");
        } catch (err) {
          toast.error(errorMessage(err, "Không xóa được vai trò."), errorTitle(err), 10000);
        }
      },
    });
  };

  const afterAssignmentChange = async () => {
    await loadAssignments();
    await load();
  };

  const runAssignment = async (action: () => Promise<unknown>, success: string, fallback: string) => {
    try {
      await action();
      toast.success(success);
      await afterAssignmentChange();
    } catch (err) {
      toast.error(errorMessage(err, fallback), errorTitle(err), 10000);
    }
  };

  const handleCreateAssignment = async (payload: CreateAssignmentPayload) => {
    setSaving(true);
    try {
      await roleService.createAssignment(payload);
      setAssignmentForm(null);
      toast.success("Đã gán vai trò.");
      await afterAssignmentChange();
    } catch (err) {
      toast.error(errorMessage(err, "Không gán được vai trò."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateAssignment = async (id: string, payload: UpdateAssignmentPayload) => {
    setSaving(true);
    try {
      await roleService.updateAssignment(id, payload);
      setAssignmentForm(null);
      toast.success("Đã cập nhật bản gán.");
      await afterAssignmentChange();
    } catch (err) {
      toast.error(errorMessage(err, "Không cập nhật được bản gán."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  if (!canManage) return <NoAccess title="Vai trò" permissionName="Quản lý vai trò" />;

  if (loadError) {
    return (
      <div className="page-wrapper">
        <PageHeader title="Vai trò" />
        <div className="page-body">
          <div className="alert alert-danger">{loadError}</div>
          <Link href="/admin/roles" className="btn btn-sm btn-outline-secondary">
            <i className="bi bi-arrow-left me-1" />
            Về danh sách
          </Link>
        </div>
      </div>
    );
  }

  if (!role) {
    return (
      <div className="page-wrapper">
        <div className="page-body text-center py-5 text-secondary">
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải…
        </div>
      </div>
    );
  }

  const visibleAssignments = showHistory ? assignments : assignments.filter((a) => a.status !== "Expired");
  const isOwnAssignment = (a: RoleAssignment) => a.userId === currentUser?.id;

  return (
    <div className="page-wrapper">
      <PageHeader
        title={role.name}
        subTitle={role.description || "Chưa có mô tả."}
        badge={
          role.isProtected ? (
            <span className="badge bg-secondary-subtle text-secondary border">
              <i className="bi bi-lock-fill me-1" />
              Vai trò bảo vệ
            </span>
          ) : undefined
        }
        actions={
          <div className="d-flex gap-2">
            <Link href="/admin/roles" className="btn btn-sm btn-outline-secondary">
              <i className="bi bi-arrow-left me-1" />
              Danh sách
            </Link>
            <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setEditing(true)}>
              <i className="bi bi-pencil-square me-1" />
              Đổi tên, mô tả
            </button>
            {!role.isProtected && (
              <button type="button" className="btn btn-sm btn-outline-danger" onClick={handleDelete}>
                <i className="bi bi-trash me-1" />
                Xóa
              </button>
            )}
          </div>
        }
      />

      <div className="page-body">
        <ul className="nav nav-tabs mb-3">
          <li className="nav-item">
            <button type="button" className={`nav-link ${tab === "permissions" ? "active fw-semibold" : ""}`} onClick={() => setTab("permissions")}>
              <i className="bi bi-shield-check me-1" />
              Ma trận quyền ({role.permissionCodes.length})
            </button>
          </li>
          {canAssign && (
            <li className="nav-item">
              <button type="button" className={`nav-link ${tab === "assignments" ? "active fw-semibold" : ""}`} onClick={() => setTab("assignments")}>
                <i className="bi bi-people me-1" />
                Người được gán ({role.assignmentCount})
              </button>
            </li>
          )}
        </ul>

        {tab === "permissions" && (
          <>
            {heldBySelf && (
              <div className="alert alert-info small py-2">
                Bạn đang được gán vai trò này nên không thể tự sửa quyền của nó (chống tự nâng quyền). Nhờ quản trị viên khác thực hiện.
              </div>
            )}
            {role.isProtected && (
              <div className="alert alert-secondary small py-2">
                Vai trò bảo vệ luôn giữ hai quyền &quot;Quản lý vai trò&quot; và &quot;Gán vai trò&quot; để hệ thống luôn còn người quản trị.
              </div>
            )}
            {addedGlobalOnly.length > 0 && scopedAssignments > 0 && (
              <div className="alert alert-warning small py-2">
                Vai trò đang được gán theo Phòng/Chi bộ ({scopedAssignments} bản gán) nên không thêm được quyền &quot;Chỉ Toàn công ty&quot;. Máy chủ sẽ từ chối
                khi lưu.
              </div>
            )}
            <div className="d-flex justify-content-end gap-2 mb-2 position-sticky" style={{ top: 0, zIndex: 2 }}>
              <button type="button" className="btn btn-sm btn-light border" disabled={!dirty || saving} onClick={() => setSelected(new Set(role.permissionCodes))}>
                Hoàn tác
              </button>
              <button type="button" className="btn btn-sm btn-primary" disabled={!dirty || saving || heldBySelf} onClick={savePermissions}>
                {saving && <span className="spinner-border spinner-border-sm me-1" />}
                Lưu quyền {dirty && `(${selected.size})`}
              </button>
            </div>
            <PermissionMatrix modules={modules} selected={selected} onChange={setSelected} readOnly={heldBySelf} lockedCodes={lockedCodes} />
          </>
        )}

        {tab === "assignments" && canAssign && (
          <>
            <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
              <div className="form-check mb-0">
                <input id="show-history" type="checkbox" className="form-check-input" checked={showHistory} onChange={(e) => setShowHistory(e.target.checked)} />
                <label htmlFor="show-history" className="form-check-label small">
                  Hiện cả bản gán đã hết hạn (lịch sử)
                </label>
              </div>
              <button type="button" className="btn btn-sm btn-primary" onClick={() => setAssignmentForm({})}>
                <i className="bi bi-person-plus me-1" />
                Gán cho người
              </button>
            </div>
            <Card>
              <AssignmentTable
                assignments={visibleAssignments}
                showUser
                canManage
                emptyText="Chưa có ai được gán vai trò này."
                onEdit={(a) => (isOwnAssignment(a) ? toast.warning("Không thể sửa bản gán của chính mình.") : setAssignmentForm({ assignment: a }))}
                onEnd={(a) =>
                  confirm({
                    title: "Kết thúc bản gán",
                    message: `Kết thúc ngay vai trò "${a.roleName}" (${a.scopeName}) của ${a.fullName}?`,
                    confirmText: "Kết thúc",
                    isDanger: true,
                    onConfirm: () => runAssignment(() => roleService.endAssignment(a.id), "Đã kết thúc bản gán.", "Không kết thúc được bản gán."),
                  })
                }
                onDelete={(a) =>
                  confirm({
                    title: "Xóa bản gán",
                    message: `Xóa bản gán vai trò "${a.roleName}" (${a.scopeName}) của ${a.fullName}? Nên dùng "Kết thúc" để giữ lịch sử; chỉ xóa khi gán nhầm.`,
                    confirmText: "Xóa",
                    isDanger: true,
                    onConfirm: () => runAssignment(() => roleService.deleteAssignment(a.id), "Đã xóa bản gán.", "Không xóa được bản gán."),
                  })
                }
              />
            </Card>
          </>
        )}
      </div>

      {editing && <RoleFormModal role={role} saving={saving} onClose={() => setEditing(false)} onSubmit={handleRename} />}
      {assignmentForm && (
        <AssignmentFormModal
          assignment={assignmentForm.assignment}
          fixedRole={role}
          roles={[role]}
          globalOnlyCodes={globalOnlyCodes}
          departments={departments}
          partyCells={partyCells}
          saving={saving}
          onClose={() => setAssignmentForm(null)}
          onCreate={handleCreateAssignment}
          onUpdate={(payload) => assignmentForm.assignment && handleUpdateAssignment(assignmentForm.assignment.id, payload)}
        />
      )}
    </div>
  );
}
