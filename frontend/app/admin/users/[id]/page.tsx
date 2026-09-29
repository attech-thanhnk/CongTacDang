"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { AccountFormModal } from "@/components/admin/AccountFormModal";
import { AccountStatusBadge } from "@/components/admin/AccountStatusBadge";
import { AssignmentFormModal } from "@/components/admin/AssignmentFormModal";
import { AssignmentTable } from "@/components/admin/AssignmentTable";
import { EffectivePermissionsPanel } from "@/components/admin/EffectivePermissionsPanel";
import { MemberPositionsPanel } from "@/components/admin/MemberPositionsPanel";
import { NoAccess } from "@/components/admin/NoAccess";
import { TemporaryCredential, TemporaryPasswordModal } from "@/components/admin/TemporaryPasswordModal";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import { useCatalogOptions } from "@/components/admin/useCatalogOptions";
import { AccountListItem, APPROVAL_AUTHORITY_LABELS, UpdateAccountPayload, userService } from "@/services/userService";
import {
  AdminRole,
  CreateAssignmentPayload,
  PermissionModule,
  RoleAssignment,
  roleService,
  UpdateAssignmentPayload,
  UserEffectivePermissions,
} from "@/services/roleService";

type Tab = "info" | "positions" | "assignments" | "effective";

/** Chi tiết tài khoản: thông tin + thao tác quản trị, bản gán vai trò, quyền hiệu lực. */
export default function AdminUserDetailPage({ params }: { params: { id: string } }) {
  const userId = params.id;
  const router = useRouter();
  const { user: currentUser, hasPermission, hasPermissionIn } = useAuth();
  const { toast, confirm } = useToast();
  const { departments, partyCells } = useCatalogOptions();

  const canRead = hasPermission("system.users.read") || hasPermission("system.assignments.manage");
  const canAssign = hasPermissionIn("system.assignments.manage", "Global");
  const isSelf = currentUser?.id === userId;

  const [account, setAccount] = useState<AccountListItem | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>("info");
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [credential, setCredential] = useState<TemporaryCredential | null>(null);

  const [assignments, setAssignments] = useState<RoleAssignment[]>([]);
  const [assignmentsLoading, setAssignmentsLoading] = useState(false);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [modules, setModules] = useState<PermissionModule[]>([]);
  const [rolesError, setRolesError] = useState<string | null>(null);
  const [assignmentForm, setAssignmentForm] = useState<{ assignment?: RoleAssignment } | null>(null);

  const [effective, setEffective] = useState<UserEffectivePermissions | null>(null);
  const [effectiveLoading, setEffectiveLoading] = useState(false);
  const [effectiveError, setEffectiveError] = useState<string | null>(null);

  // Quyền quản lý tài khoản xét theo phạm vi đơn vị / tổ chức Đảng của chính tài khoản này. Phạm vi gán bao trùm cả
  // cây con nên máy chủ tính sẵn (`canManage`); chỉ dự phòng tự xét khi máy chủ chưa trả trường này.
  const canManage = useMemo(() => {
    if (!account) return false;
    if (typeof account.canManage === "boolean") return account.canManage;
    const code = "system.users.manage";
    return (
      hasPermissionIn(code, "Global") ||
      (!!account.departmentId && hasPermissionIn(code, "Department", account.departmentId)) ||
      (!!account.partyCellId && hasPermissionIn(code, "PartyCell", account.partyCellId))
    );
  }, [account, hasPermissionIn]);

  const loadAccount = useCallback(async () => {
    try {
      setAccount(await userService.get(userId));
      setLoadError(null);
    } catch (err) {
      setLoadError(errorMessage(err, "Không tải được tài khoản."));
    }
  }, [userId]);

  const loadAssignments = useCallback(async () => {
    if (!canAssign) return;
    setAssignmentsLoading(true);
    try {
      setAssignments(await roleService.listAssignments({ userId }));
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được bản gán vai trò."), errorTitle(err));
    } finally {
      setAssignmentsLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, canAssign]);

  const loadEffective = useCallback(async () => {
    setEffectiveLoading(true);
    try {
      setEffective(await roleService.getEffectivePermissions(userId));
      setEffectiveError(null);
    } catch (err) {
      setEffectiveError(errorMessage(err, "Không tải được quyền hiệu lực."));
    } finally {
      setEffectiveLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    if (canRead) loadAccount();
  }, [canRead, loadAccount]);

  useEffect(() => {
    if (tab === "assignments") loadAssignments();
    if (tab === "effective") loadEffective();
  }, [tab, loadAssignments, loadEffective]);

  // Danh sách vai trò + danh mục quyền (cần quyền quản lý vai trò). Thiếu quyền → vẫn xem/kết thúc bản gán được.
  useEffect(() => {
    if (!canAssign) return;
    Promise.all([roleService.listRoles(), roleService.listPermissions()])
      .then(([roleList, moduleList]) => {
        setRoles(roleList);
        setModules(moduleList);
      })
      .catch((err: unknown) => setRolesError(errorMessage(err, "Không tải được danh sách vai trò.")));
  }, [canAssign]);

  const globalOnlyCodes = useMemo(
    () => new Set(modules.flatMap((m) => m.permissions.filter((p) => !p.appliesScope).map((p) => p.code))),
    [modules]
  );
  const moduleNames = useMemo(() => Object.fromEntries(modules.map((m) => [m.module, m.moduleName])), [modules]);

  const current = assignments.filter((a) => a.status !== "Expired");
  const history = assignments.filter((a) => a.status === "Expired");

  /** Chạy một thao tác, báo lỗi đúng thông báo máy chủ (409 chốt chặn, 403, 400). */
  const run = async (action: () => Promise<unknown>, success: string, fallback: string) => {
    try {
      await action();
      toast.success(success);
      return true;
    } catch (err) {
      toast.error(errorMessage(err, fallback), errorTitle(err), 10000);
      return false;
    }
  };

  const handleUpdate = async (payload: UpdateAccountPayload) => {
    setSaving(true);
    try {
      setAccount(await userService.update(userId, payload));
      setEditing(false);
      toast.success("Đã cập nhật thông tin tài khoản.");
    } catch (err) {
      toast.error(errorMessage(err, "Không cập nhật được tài khoản."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = () => {
    if (!account) return;
    if (account.isActive) {
      confirm({
        title: "Vô hiệu hóa tài khoản",
        message: `Vô hiệu hóa tài khoản "${account.username}"? Cán bộ không đăng nhập được nữa và mọi phiên đang dùng bị đăng xuất ngay.`,
        confirmText: "Vô hiệu hóa",
        isDanger: true,
        onConfirm: async () => {
          if (await run(() => userService.deactivate(userId), "Đã vô hiệu hóa tài khoản.", "Không vô hiệu hóa được tài khoản.")) await loadAccount();
        },
      });
    } else {
      confirm({
        title: "Mở lại tài khoản",
        message: `Cho phép tài khoản "${account.username}" đăng nhập trở lại?`,
        confirmText: "Mở lại",
        isDanger: false,
        onConfirm: async () => {
          if (await run(() => userService.activate(userId), "Đã mở lại tài khoản.", "Không mở lại được tài khoản.")) await loadAccount();
        },
      });
    }
  };

  const handleUnlock = () => {
    if (!account) return;
    confirm({
      title: "Mở khóa đăng nhập",
      message: `Xóa trạng thái khóa tạm và số lần nhập sai mật khẩu của "${account.username}"? Mật khẩu không thay đổi.`,
      confirmText: "Mở khóa",
      isDanger: false,
      onConfirm: async () => {
        if (await run(() => userService.unlock(userId), "Đã mở khóa đăng nhập.", "Không mở khóa được.")) await loadAccount();
      },
    });
  };

  const handleResetPassword = () => {
    if (!account) return;
    confirm({
      title: "Đặt lại mật khẩu",
      message: `Đặt lại mật khẩu tạm cho "${account.username}"? Mật khẩu hiện tại hết hiệu lực, mọi phiên bị đăng xuất; cán bộ phải đổi mật khẩu ở lần đăng nhập kế tiếp.`,
      confirmText: "Đặt lại",
      isDanger: true,
      onConfirm: async () => {
        try {
          const result = await userService.resetPassword(userId);
          setCredential({
            title: "Đã đặt lại mật khẩu",
            fullName: account.fullName,
            username: result.userName,
            temporaryPassword: result.temporaryPassword,
          });
          await loadAccount();
        } catch (err) {
          toast.error(errorMessage(err, "Không đặt lại được mật khẩu."), errorTitle(err), 10000);
        }
      },
    });
  };

  const handleDelete = () => {
    if (!account) return;
    confirm({
      title: "Xóa tài khoản",
      message: `Xóa tài khoản "${account.username}" (${account.fullName})? Mọi phiên bị đăng xuất; tên đăng nhập này không dùng lại được.`,
      confirmText: "Xóa tài khoản",
      isDanger: true,
      onConfirm: async () => {
        if (await run(() => userService.remove(userId), "Đã xóa tài khoản.", "Không xóa được tài khoản.")) router.push("/admin/users");
      },
    });
  };

  const afterAssignmentChange = async () => {
    await loadAssignments();
    if (effective) await loadEffective();
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

  const handleEndAssignment = (a: RoleAssignment) =>
    confirm({
      title: "Kết thúc bản gán",
      message: `Kết thúc ngay vai trò "${a.roleName}" (${a.scopeName}) của ${a.fullName}? Bản gán chuyển vào lịch sử.`,
      confirmText: "Kết thúc",
      isDanger: true,
      onConfirm: async () => {
        if (await run(() => roleService.endAssignment(a.id), "Đã kết thúc bản gán.", "Không kết thúc được bản gán.")) await afterAssignmentChange();
      },
    });

  const handleDeleteAssignment = (a: RoleAssignment) =>
    confirm({
      title: "Xóa bản gán",
      message: `Xóa bản gán vai trò "${a.roleName}" (${a.scopeName}) của ${a.fullName}? Nên dùng "Kết thúc" để giữ lịch sử; chỉ xóa khi gán nhầm.`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        if (await run(() => roleService.deleteAssignment(a.id), "Đã xóa bản gán.", "Không xóa được bản gán.")) await afterAssignmentChange();
      },
    });

  if (!canRead) return <NoAccess title="Tài khoản" permissionName="Xem tài khoản, hồ sơ cán bộ" />;

  if (loadError) {
    return (
      <div className="page-wrapper">
        <PageHeader title="Tài khoản" />
        <div className="page-body">
          <div className="alert alert-danger">{loadError}</div>
          <Link href="/admin/users" className="btn btn-sm btn-outline-secondary">
            <i className="bi bi-arrow-left me-1" />
            Về danh sách
          </Link>
        </div>
      </div>
    );
  }

  if (!account) {
    return (
      <div className="page-wrapper">
        <div className="page-body text-center py-5 text-secondary">
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải…
        </div>
      </div>
    );
  }

  const infoRows: [string, React.ReactNode][] = [
    ["Tên đăng nhập", <code key="u">{account.username}</code>],
    ["Họ và tên", account.fullName],
    ["Chức danh", account.positionTitle || "—"],
    ["Khung tỷ trọng mặc định", account.weightFrameCode || "Chưa chọn (dùng khung mặc định của bộ tiêu chí)"],
    ["Email", account.email || "—"],
    ["Số điện thoại", account.phoneNumber || "—"],
    ["Đơn vị công tác chính", account.departmentName || "—"],
    ["Nơi sinh hoạt Đảng", account.partyCellName || "—"],
    ["Đảng viên", account.isPartyMember ? `Có (số thẻ ${account.partyCardNumber || "—"})` : "Không"],
    [
      "Cấp quyết định xếp loại",
      `${APPROVAL_AUTHORITY_LABELS[account.approvalAuthority] ?? "—"} (${account.approvalAuthorityOverride ? "đặt tay" : "suy ra từ chức vụ"})`,
    ],
    ["Trạng thái", <AccountStatusBadge key="s" account={account} />],
    ["Khóa tạm đến", account.isLockedOut ? formatDateTime(account.lockoutEnd) : "—"],
    ["Số lần nhập sai liên tiếp", account.failedLoginCount],
    ["Đăng nhập cuối", account.lastLoginAt ? formatDateTime(account.lastLoginAt) : "Chưa đăng nhập"],
    ["Ngày tạo", formatDateTime(account.createdAt)],
  ];

  return (
    <div className="page-wrapper">
      <PageHeader
        title={account.fullName}
        subTitle={`Tài khoản ${account.username}`}
        badge={<AccountStatusBadge account={account} />}
        actions={
          <Link href="/admin/users" className="btn btn-sm btn-outline-secondary">
            <i className="bi bi-arrow-left me-1" />
            Danh sách
          </Link>
        }
      />

      <div className="page-body">
        {canManage && (
          <div className="d-flex flex-wrap gap-2 mb-3">
            <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setEditing(true)}>
              <i className="bi bi-pencil-square me-1" />
              Sửa thông tin
            </button>
            <button
              type="button"
              className={`btn btn-sm ${account.isActive ? "btn-outline-warning" : "btn-outline-success"}`}
              onClick={toggleActive}
              disabled={isSelf && account.isActive}
              title={isSelf && account.isActive ? "Không thể tự vô hiệu hóa tài khoản của mình." : undefined}
            >
              <i className={`bi ${account.isActive ? "bi-person-slash" : "bi-person-check"} me-1`} />
              {account.isActive ? "Vô hiệu hóa" : "Mở lại tài khoản"}
            </button>
            {(account.isLockedOut || account.failedLoginCount > 0) && (
              <button type="button" className="btn btn-sm btn-outline-secondary" onClick={handleUnlock}>
                <i className="bi bi-unlock me-1" />
                Mở khóa đăng nhập
              </button>
            )}
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={handleResetPassword}>
              <i className="bi bi-key me-1" />
              Đặt lại mật khẩu
            </button>
            <button
              type="button"
              className="btn btn-sm btn-outline-danger"
              onClick={handleDelete}
              disabled={isSelf}
              title={isSelf ? "Không thể tự xóa tài khoản của mình." : undefined}
            >
              <i className="bi bi-trash me-1" />
              Xóa
            </button>
          </div>
        )}

        <ul className="nav nav-tabs mb-3">
          {(
            [
              { key: "info" as const, label: "Thông tin", icon: "bi-person-vcard", show: true },
              { key: "positions" as const, label: "Chức vụ", icon: "bi-diagram-3", show: true },
              { key: "assignments" as const, label: "Vai trò & phạm vi", icon: "bi-person-badge", show: canAssign },
              { key: "effective" as const, label: "Người này làm được gì", icon: "bi-shield-check", show: true },
            ]
          )
            .filter((t) => t.show)
            .map((t) => (
              <li className="nav-item" key={t.key}>
                <button type="button" className={`nav-link ${tab === t.key ? "active fw-semibold" : ""}`} onClick={() => setTab(t.key)}>
                  <i className={`bi ${t.icon} me-1`} />
                  {t.label}
                </button>
              </li>
            ))}
        </ul>

        {tab === "info" && (
          <Card>
            <table className="table mb-0" style={{ fontSize: 13 }}>
              <tbody>
                {infoRows.map(([label, value]) => (
                  <tr key={label}>
                    <th className="ps-3 text-secondary fw-semibold" style={{ width: 240 }}>
                      {label}
                    </th>
                    <td>{value}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}

        {tab === "positions" && (
          <MemberPositionsPanel
            userId={account.id}
            canManage={canManage}
            departments={departments}
            partyCells={partyCells}
            onChanged={loadAccount}
          />
        )}

        {tab === "assignments" && canAssign && (
          <>
            <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
              <h2 className="h6 fw-bold mb-0">Bản gán đang và sắp hiệu lực</h2>
              <button
                type="button"
                className="btn btn-sm btn-primary"
                onClick={() => setAssignmentForm({})}
                disabled={isSelf || roles.length === 0}
                title={isSelf ? "Không thể tự gán vai trò cho chính mình." : undefined}
              >
                <i className="bi bi-plus-lg me-1" />
                Gán vai trò
              </button>
            </div>
            {isSelf && <div className="small text-secondary mb-2">Đây là tài khoản của bạn: không thể tự gán, sửa hay thu hồi vai trò của chính mình.</div>}
            {rolesError && <div className="alert alert-warning small py-2">{rolesError}</div>}
            <Card className="mb-3">
              {assignmentsLoading && assignments.length === 0 ? (
                <div className="text-center py-4 text-secondary small">
                  <span className="spinner-border spinner-border-sm me-2" />
                  Đang tải…
                </div>
              ) : (
                <AssignmentTable
                  assignments={current}
                  showRole
                  canManage={!isSelf}
                  emptyText="Chưa có bản gán vai trò nào đang hiệu lực."
                  onEdit={(a) => setAssignmentForm({ assignment: a })}
                  onEnd={handleEndAssignment}
                  onDelete={handleDeleteAssignment}
                />
              )}
            </Card>
            <h2 className="h6 fw-bold mb-2">Lịch sử (đã hết hạn)</h2>
            <Card>
              <AssignmentTable
                assignments={history}
                showRole
                canManage={!isSelf}
                emptyText="Chưa có bản gán nào đã hết hạn."
                onDelete={handleDeleteAssignment}
              />
            </Card>
          </>
        )}

        {tab === "effective" && (
          <Card>
            <div className="d-flex justify-content-between align-items-center px-3 py-2 border-bottom">
              <span className="small text-secondary">
                Quyền tính từ các bản gán đang hiệu lực. Chủ hồ sơ luôn xem được hồ sơ đánh giá của mình; không duyệt được hồ sơ của chính mình.
              </span>
              <button type="button" className="btn btn-sm btn-outline-secondary" onClick={loadEffective} disabled={effectiveLoading}>
                <i className="bi bi-arrow-clockwise" />
              </button>
            </div>
            <EffectivePermissionsPanel data={effective} loading={effectiveLoading} error={effectiveError} moduleNames={moduleNames} />
          </Card>
        )}
      </div>

      {editing && (
        <AccountFormModal
          account={account}
          departments={departments}
          partyCells={partyCells}
          saving={saving}
          onClose={() => setEditing(false)}
          onUpdate={handleUpdate}
        />
      )}
      {assignmentForm && (
        <AssignmentFormModal
          assignment={assignmentForm.assignment}
          fixedUser={{ id: account.id, username: account.username, fullName: account.fullName }}
          roles={roles}
          globalOnlyCodes={globalOnlyCodes}
          departments={departments}
          partyCells={partyCells}
          saving={saving}
          onClose={() => setAssignmentForm(null)}
          onCreate={handleCreateAssignment}
          onUpdate={(payload) => assignmentForm.assignment && handleUpdateAssignment(assignmentForm.assignment.id, payload)}
        />
      )}
      {credential && <TemporaryPasswordModal credential={credential} onClose={() => setCredential(null)} />}
    </div>
  );
}
