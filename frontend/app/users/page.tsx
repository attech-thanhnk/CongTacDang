"use client";

import React, { useState, useEffect, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { userService, CadreItem, RoleItem } from "@/services/userService";
import { organizationService, BranchItem } from "@/services/organizationService";
import { roleService, RoleDetailItem, PermissionItem } from "@/services/roleService";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader, Button, StatusBadge } from "@/components/common";

function UsersContent() {
  const { hasPermission } = useAuth();
  const { toast, confirm } = useToast();
  const canManageRoles = hasPermission("system.roles.manage");
  const searchParams = useSearchParams();
  const paramTab = searchParams.get("tab");
  const initialTab: "cadres" | "branches" | "roles" =
    (paramTab === "roles" || paramTab === "phan_quyen") && canManageRoles ? "roles" :
    paramTab === "branches" || paramTab === "to_chuc" ? "branches" : "cadres";
  const [activeTab, setActiveTab] = useState<"cadres" | "branches" | "roles">(initialTab);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedBranch, setSelectedBranch] = useState("all");

  const [cadres, setCadres] = useState<CadreItem[]>([]);
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [roles, setRoles] = useState<RoleItem[]>([]);
  const [adminRoles, setAdminRoles] = useState<RoleDetailItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Bảo vệ activeTab: Nếu không có quyền quản trị vai trò thì không thể vào tab roles
  useEffect(() => {
    if (activeTab === "roles" && !canManageRoles) {
      setActiveTab("cadres");
    }
  }, [activeTab, canManageRoles]);

  // Modal Gán vai trò cho cán bộ
  const [showAssignRoleModal, setShowAssignRoleModal] = useState(false);
  const [selectedCadreForRoles, setSelectedCadreForRoles] = useState<CadreItem | null>(null);
  const [selectedRoleCodes, setSelectedRoleCodes] = useState<string[]>([]);
  const [isAssigningRoles, setIsAssigningRoles] = useState(false);

  // Modal Chỉnh sửa quyền hạn của vai trò
  const [showEditRolePermsModal, setShowEditRolePermsModal] = useState(false);
  const [selectedRoleForPerms, setSelectedRoleForPerms] = useState<RoleDetailItem | null>(null);
  const [allPermissions, setAllPermissions] = useState<PermissionItem[]>([]);
  const [selectedPermCodes, setSelectedPermCodes] = useState<string[]>([]);
  const [isUpdatingPerms, setIsUpdatingPerms] = useState(false);

  // Modal cán bộ
  const [showAddModal, setShowAddModal] = useState(false);
  const [newFullName, setNewFullName] = useState("");
  const [newPartyCard, setNewPartyCard] = useState("");
  const [newAdminTitle, setNewAdminTitle] = useState("");
  const [newBranchId, setNewBranchId] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Sửa cán bộ
  const [editingCadre, setEditingCadre] = useState<CadreItem | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editPartyCard, setEditPartyCard] = useState("");
  const [editAdminTitle, setEditAdminTitle] = useState("");
  const [editBranchId, setEditBranchId] = useState("");

  // Modal Chi bộ
  const [showAddBranchModal, setShowAddBranchModal] = useState(false);
  const [editingBranch, setEditingBranch] = useState<BranchItem | null>(null);
  const [branchCode, setBranchCode] = useState("");
  const [branchName, setBranchName] = useState("");
  const [branchDescription, setBranchDescription] = useState("");

  const loadData = async () => {
    setLoading(true);
    setErrorMsg(null);
    try {
      const [cadresData, branchesData, rolesData] = await Promise.all([
        userService.getUsers(),
        organizationService.getBranches(),
        userService.getRoles(),
      ]);
      setCadres(cadresData);
      setBranches(branchesData);
      setRoles(rolesData);

      if (canManageRoles) {
        try {
          const adminData = await roleService.getAdminRoles();
          setAdminRoles(adminData);
        } catch {
          // Bỏ qua nếu lỗi
        }
      }
    } catch (err: any) {
      setErrorMsg(err.message || "Không thể tải dữ liệu.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newFullName.trim()) return;

    setIsSubmitting(true);
    try {
      await userService.createUser({
        fullName: newFullName.trim(),
        partyCardNumber: newPartyCard.trim() || null,
        adminTitle: newAdminTitle.trim() || null,
        partyCellId: newBranchId || null,
      });
      toast.success("Đã thêm mới hồ sơ cán bộ thành công.");
      setShowAddModal(false);
      setNewFullName("");
      setNewPartyCard("");
      setNewAdminTitle("");
      setNewBranchId("");
      loadData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi lưu hồ sơ cán bộ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenEditUser = (c: CadreItem) => {
    setEditingCadre(c);
    setEditFullName(c.fullName);
    setEditPartyCard(c.partyCardNumber || "");
    setEditAdminTitle(c.adminTitle || "");
    setEditBranchId(c.partyCellId || "");
  };

  const handleUpdateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingCadre || !editFullName.trim()) return;

    setIsSubmitting(true);
    try {
      await userService.updateUser(editingCadre.id, {
        fullName: editFullName.trim(),
        partyCardNumber: editPartyCard.trim() || null,
        adminTitle: editAdminTitle.trim() || null,
        partyCellId: editBranchId || null,
      });
      toast.success("Đã cập nhật hồ sơ cán bộ thành công.");
      setEditingCadre(null);
      loadData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi cập nhật hồ sơ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteUser = (id: string, name: string) => {
    confirm({
      title: "Xác nhận xóa cán bộ",
      message: `Đồng chí có chắc chắn muốn xóa hồ sơ cán bộ "${name}" khỏi hệ thống?`,
      confirmText: "Xóa cán bộ",
      isDanger: true,
      onConfirm: async () => {
        try {
          await userService.deleteUser(id);
          toast.success(`Đã xóa cán bộ "${name}".`);
          loadData();
        } catch (err: any) {
          toast.error(err.message || "Lỗi khi xóa cán bộ.");
        }
      },
    });
  };

  const handleResetPassword = async (id: string, name: string) => {
    try {
      const result = await userService.resetPassword(id);
      toast.success(`Mật khẩu tạm của ${name} là: ${result.temporaryPassword}. Hãy cung cấp qua kênh an toàn.`);
    } catch (err: any) {
      toast.error(err.message || "Không thể đặt lại mật khẩu.");
    }
  };

  const handleCreateBranch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!branchName.trim()) return;

    setIsSubmitting(true);
    try {
      await organizationService.createBranch({
        code: branchCode.trim() || undefined,
        name: branchName.trim(),
        description: branchDescription.trim() || undefined,
      });
      toast.success("Đã thành lập Chi bộ mới thành công.");
      setShowAddBranchModal(false);
      setBranchCode("");
      setBranchName("");
      setBranchDescription("");
      loadData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi tạo Chi bộ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenEditBranch = (b: BranchItem) => {
    setEditingBranch(b);
    setBranchName(b.name);
    setBranchDescription(b.description || "");
  };

  const handleUpdateBranch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingBranch || !branchName.trim()) return;

    setIsSubmitting(true);
    try {
      await organizationService.updateBranch(editingBranch.id, {
        name: branchName.trim(),
        description: branchDescription.trim(),
      });
      toast.success("Đã cập nhật thông tin Chi bộ thành công.");
      setEditingBranch(null);
      setBranchName("");
      setBranchDescription("");
      loadData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi cập nhật Chi bộ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteBranch = (id: string, name: string) => {
    confirm({
      title: "Xác nhận xóa Chi bộ",
      message: `Đồng chí có chắc chắn muốn xóa Chi bộ "${name}"?`,
      confirmText: "Xóa Chi bộ",
      isDanger: true,
      onConfirm: async () => {
        try {
          await organizationService.deleteBranch(id);
          toast.success(`Đã xóa Chi bộ "${name}".`);
          loadData();
        } catch (err: any) {
          toast.error(err.message || "Lỗi khi xóa Chi bộ.");
        }
      },
    });
  };

  // Xử lý Phân vai trò cho Cán bộ
  const handleOpenAssignRoles = (cadre: CadreItem) => {
    setSelectedCadreForRoles(cadre);
    setSelectedRoleCodes([]);
    setShowAssignRoleModal(true);
  };

  const handleSaveUserRoles = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCadreForRoles) return;
    if (selectedRoleCodes.length === 0) {
      toast.warning("Vui lòng chọn ít nhất 1 vai trò hệ thống.");
      return;
    }

    setIsAssigningRoles(true);
    try {
      await roleService.assignUserRoles(selectedCadreForRoles.id, selectedRoleCodes);
      toast.success(`Đã gán vai trò cho cán bộ "${selectedCadreForRoles.fullName}" thành công.`);
      setShowAssignRoleModal(false);
      setSelectedCadreForRoles(null);
      loadData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi gán vai trò.");
    } finally {
      setIsAssigningRoles(false);
    }
  };

  // Xử lý Chỉnh sửa quyền hạn cho Vai trò
  const handleOpenEditRolePerms = async (roleItem: RoleDetailItem) => {
    setSelectedRoleForPerms(roleItem);
    setSelectedPermCodes(roleItem.permissions?.map((p: any) => p.code || p) || []);
    setShowEditRolePermsModal(true);

    if (allPermissions.length === 0) {
      try {
        const perms = await roleService.getAdminPermissions();
        setAllPermissions(perms);
      } catch (err: any) {
        console.error("Không thể tải danh mục quyền hệ thống:", err);
      }
    }
  };

  const handleSaveRolePerms = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRoleForPerms) return;

    setIsUpdatingPerms(true);
    try {
      await roleService.updateRolePermissions(selectedRoleForPerms.id, selectedPermCodes);
      toast.success(`Đã cập nhật danh sách quyền cho vai trò "${selectedRoleForPerms.name}" thành công.`);
      setShowEditRolePermsModal(false);
      setSelectedRoleForPerms(null);
      const adminData = await roleService.getAdminRoles();
      setAdminRoles(adminData);
    } catch (err: any) {
      toast.error(err.message || "Lỗi cập nhật quyền.");
    } finally {
      setIsUpdatingPerms(false);
    }
  };

  const filteredCadres = cadres.filter((c) => {
    const term = searchTerm.toLowerCase();
    const matchSearch =
      c.fullName.toLowerCase().includes(term) ||
      (c.adminTitle && c.adminTitle.toLowerCase().includes(term)) ||
      (c.partyCardNumber && c.partyCardNumber.toLowerCase().includes(term)) ||
      (c.partyCellName && c.partyCellName.toLowerCase().includes(term));

    const matchBranch =
      selectedBranch === "all" ||
      c.partyCellName === selectedBranch ||
      c.partyCellId === selectedBranch;

    return matchSearch && matchBranch;
  });

  return (
    <div className="page-wrapper">
      {/* Header trang dùng chung */}
      <PageHeader
        title="Cán bộ & Chi bộ"
        actions={
          <>
            {activeTab === "cadres" && hasPermission("system.users.manage") && (
              <Button
                size="sm"
                variant="primary"
                icon="bi-plus-lg"
                onClick={() => setShowAddModal(true)}
              >
                Thêm cán bộ
              </Button>
            )}
            {activeTab === "branches" && hasPermission("catalog.manage") && (
              <Button
                size="sm"
                variant="primary"
                icon="bi-plus-lg"
                onClick={() => {
                  setBranchCode("");
                  setBranchName("");
                  setBranchDescription("");
                  setShowAddBranchModal(true);
                }}
              >
                Thêm chi bộ
              </Button>
            )}
          </>
        }
      />

      <div className="page-body">
        {/* Tabs Điều hướng */}
        <div
          style={{
            display: "flex",
            gap: "8px",
            marginBottom: "16px",
            borderBottom: "1px solid var(--border-base)",
            paddingBottom: "12px",
          }}
        >
          <button
            type="button"
            onClick={() => setActiveTab("cadres")}
            style={{
              padding: "6px 14px",
              borderRadius: "var(--radius-sm)",
              fontSize: "13px",
              fontWeight: 600,
              border: activeTab === "cadres" ? "1px solid var(--color-cobalt)" : "1px solid var(--border-base)",
              background: activeTab === "cadres" ? "var(--color-primary-light)" : "var(--bg-card)",
              color: activeTab === "cadres" ? "var(--color-cobalt)" : "var(--text-secondary)",
              cursor: "pointer",
              transition: "all var(--transition-fast)",
            }}
          >
            Cán bộ ({cadres.length})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("branches")}
            style={{
              padding: "6px 14px",
              borderRadius: "var(--radius-sm)",
              fontSize: "13px",
              fontWeight: 600,
              border: activeTab === "branches" ? "1px solid var(--color-cobalt)" : "1px solid var(--border-base)",
              background: activeTab === "branches" ? "var(--color-primary-light)" : "var(--bg-card)",
              color: activeTab === "branches" ? "var(--color-cobalt)" : "var(--text-secondary)",
              cursor: "pointer",
              transition: "all var(--transition-fast)",
            }}
          >
            Chi bộ ({branches.length})
          </button>
          {canManageRoles && (
            <button
              type="button"
              onClick={() => setActiveTab("roles")}
              style={{
                padding: "6px 14px",
                borderRadius: "var(--radius-sm)",
                fontSize: "13px",
                fontWeight: 600,
                border: activeTab === "roles" ? "1px solid var(--color-cobalt)" : "1px solid var(--border-base)",
                background: activeTab === "roles" ? "var(--color-primary-light)" : "var(--bg-card)",
                color: activeTab === "roles" ? "var(--color-cobalt)" : "var(--text-secondary)",
                cursor: "pointer",
                transition: "all var(--transition-fast)",
              }}
            >
              Phân quyền ({roles.length})
            </button>
          )}
        </div>

        {errorMsg && (
          <div className="alert alert-danger py-2 px-3 small mb-3">
            {errorMsg}
          </div>
        )}

        {/* Tab 1: Cán bộ */}
        {activeTab === "cadres" && (
          <div
            style={{
              background: "var(--bg-card)",
              border: "1px solid var(--border-base)",
              borderRadius: "var(--radius-lg)",
              padding: "16px 20px",
              boxShadow: "var(--shadow-sm)",
            }}
          >
            {/* Bộ lọc */}
            <div className="d-flex flex-column flex-sm-row justify-content-between align-items-center gap-2 mb-3">
              <div className="d-flex align-items-center gap-2 w-100 w-sm-auto">
                <span className="small text-secondary text-nowrap">Chi bộ:</span>
                <select
                  value={selectedBranch}
                  onChange={(e) => setSelectedBranch(e.target.value)}
                  className="form-select form-select-sm"
                  style={{ width: "220px" }}
                >
                  <option value="all">Tất cả Chi bộ</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.name}>{b.name}</option>
                  ))}
                </select>
              </div>

              <input
                type="text"
                placeholder="Tìm kiếm cán bộ..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="form-control form-control-sm"
                style={{ maxWidth: "260px" }}
              />
            </div>

            {loading ? (
              <div className="text-center py-4 text-muted small">Đang tải...</div>
            ) : filteredCadres.length === 0 ? (
              <div className="text-center py-4 text-muted small">Không tìm thấy cán bộ nào.</div>
            ) : (
              <div className="table-responsive">
                <table className="table table-sm table-hover align-middle mb-0">
                  <thead className="text-center">
                    <tr>
                      <th style={{ width: "40px" }}>STT</th>
                      <th>Họ và tên</th>
                      <th style={{ width: "130px" }}>Số thẻ Đảng</th>
                      <th style={{ width: "180px" }}>Chi bộ</th>
                      <th>Chức danh</th>
                      <th style={{ width: "95px" }}>Trạng thái</th>
                      <th style={{ width: "100px" }}>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredCadres.map((c, idx) => (
                      <tr key={c.id}>
                        <td className="text-center text-muted">{idx + 1}</td>
                        <td className="fw-semibold text-dark">
                          {c.fullName}
                          {c.isPartyMember && (
                            <StatusBadge type="partyMember" className="ms-1.5" />
                          )}
                        </td>
                        <td className="text-secondary">{c.partyCardNumber || "—"}</td>
                        <td>{c.partyCellName || "—"}</td>
                        <td className="text-secondary">{c.adminTitle || c.partyRole || "—"}</td>
                        <td className="text-center">
                          <StatusBadge type="active" value="active" />
                        </td>
                        <td className="text-center">
                          <div className="d-flex justify-content-center gap-1.5">
                            {hasPermission("system.users.manage") && (
                              <Button
                                size="sm"
                                variant="outline-secondary"
                                onClick={() => handleOpenEditUser(c)}
                                style={{ fontSize: "11.5px" }}
                              >
                                Sửa
                              </Button>
                            )}
                            {hasPermission("system.users.manage") && (
                              <Button
                                size="sm"
                                variant="outline-secondary"
                                onClick={() => handleResetPassword(c.id, c.fullName)}
                                style={{ fontSize: "11.5px" }}
                              >
                                Đặt lại MK
                              </Button>
                            )}
                            {canManageRoles && hasPermission("system.assignments.manage") && (
                              <Button
                                size="sm"
                                variant="outline-primary"
                                onClick={() => handleOpenAssignRoles(c)}
                                style={{ fontSize: "11.5px" }}
                                title="Phân vai trò hệ thống"
                              >
                                Vai trò
                              </Button>
                            )}
                            {hasPermission("system.users.manage") && (
                              <Button
                                size="sm"
                                variant="outline-danger"
                                onClick={() => handleDeleteUser(c.id, c.fullName)}
                                style={{ fontSize: "11.5px" }}
                              >
                                Xóa
                              </Button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Tab 2: Chi bộ */}
        {activeTab === "branches" && (
          <div
            style={{
              background: "var(--bg-card)",
              border: "1px solid var(--border-base)",
              borderRadius: "var(--radius-lg)",
              padding: "16px 20px",
              boxShadow: "var(--shadow-sm)",
            }}
          >
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0" style={{ fontSize: "13px" }}>
                <thead style={{ background: "var(--bg-base)" }}>
                  <tr>
                    <th style={{ width: "40px", textAlign: "center" }}>STT</th>
                    <th style={{ width: "110px" }}>Mã Chi bộ</th>
                    <th>Tên Chi bộ trực thuộc</th>
                    <th>Mô tả chức năng</th>
                    <th style={{ width: "100px", textAlign: "center" }}>Số cán bộ</th>
                    <th style={{ width: "110px", textAlign: "center" }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {branches.map((b, idx) => (
                    <tr key={b.id}>
                      <td style={{ textAlign: "center", color: "var(--text-muted)" }}>{idx + 1}</td>
                      <td style={{ fontFamily: "var(--font-mono)", fontWeight: 600, color: "var(--text-primary)" }}>{b.code}</td>
                      <td style={{ fontWeight: 600, color: "var(--text-primary)" }}>{b.name}</td>
                      <td style={{ color: "var(--text-secondary)", fontSize: "12.5px" }}>{b.description || "—"}</td>
                      <td style={{ textAlign: "center", fontWeight: 600 }}>{b.memberCount}</td>
                      <td style={{ textAlign: "center" }}>
                        <div style={{ display: "flex", justifyContent: "center", gap: "6px" }}>
                          {hasPermission("catalog.manage") && (
                            <Button
                              size="sm"
                              variant="outline-secondary"
                              onClick={() => handleOpenEditBranch(b)}
                              style={{ padding: "2px 8px", fontSize: "11.5px" }}
                            >
                              Sửa
                            </Button>
                          )}
                          {hasPermission("catalog.manage") && (
                            <Button
                              size="sm"
                              variant="outline-danger"
                              onClick={() => handleDeleteBranch(b.id, b.name)}
                              style={{ padding: "2px 8px", fontSize: "11.5px" }}
                            >
                              Xóa
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 3: Phân quyền */}
        {activeTab === "roles" && (
          <div
            style={{
              background: "var(--bg-card)",
              border: "1px solid var(--border-base)",
              borderRadius: "var(--radius-lg)",
              padding: "16px 20px",
              boxShadow: "var(--shadow-sm)",
            }}
          >
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0" style={{ fontSize: "13px" }}>
                <thead style={{ background: "var(--bg-base)" }}>
                  <tr>
                    <th style={{ width: "160px" }}>Mã vai trò</th>
                    <th style={{ width: "190px" }}>Tên vai trò</th>
                    <th style={{ width: "220px" }}>Mô tả nhiệm vụ</th>
                    <th>Danh mục quyền được cấp</th>
                    <th style={{ width: "110px", textAlign: "center" }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {(adminRoles.length > 0 ? adminRoles : roles).map((r: any) => (
                    <tr key={r.id || r.code}>
                      <td style={{ fontFamily: "var(--font-mono)", fontWeight: 600, color: "var(--text-primary)" }}>{r.code}</td>
                      <td style={{ fontWeight: 600, color: "var(--text-primary)" }}>{r.name}</td>
                      <td style={{ color: "var(--text-secondary)", fontSize: "12.5px" }}>{r.description}</td>
                      <td>
                        {r.permissions && r.permissions.length > 0 ? (
                          <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
                            {r.permissions.map((p: any) => (
                              <span
                                key={p.code || p}
                                style={{
                                  fontSize: "10.5px",
                                  fontFamily: "var(--font-mono)",
                                  padding: "1px 6px",
                                  borderRadius: "4px",
                                  background: "var(--bg-base)",
                                  border: "1px solid var(--border-base)",
                                  color: "var(--text-secondary)",
                                }}
                              >
                                {p.code || p}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span style={{ color: "var(--text-muted)", fontSize: "11px" }}>Kế thừa</span>
                        )}
                      </td>
                      <td style={{ textAlign: "center" }}>
                        {hasPermission("system.roles.manage") && r.id ? (
                          <Button
                            size="sm"
                            variant="outline-primary"
                            icon="bi-pencil-square"
                            onClick={() => handleOpenEditRolePerms(r)}
                            style={{ padding: "2px 8px", fontSize: "11.5px" }}
                          >
                            Sửa quyền
                          </Button>
                        ) : (
                          <span style={{ color: "var(--text-muted)", fontSize: "11px" }}>—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>{/* end page-body */}

      {/* Modal Thêm cán bộ */}
      {showAddModal && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: "440px" }}>
            <div className="modal-content shadow border-0">
              <div className="modal-header py-2 px-3">
                <h6 className="modal-title fw-bold mb-0">Thêm cán bộ</h6>
                <button type="button" onClick={() => setShowAddModal(false)} className="btn-close"></button>
              </div>
              <form onSubmit={handleCreateUser}>
                <div className="modal-body p-3 space-y-2">
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Họ và tên *</label>
                    <input
                      type="text"
                      required
                      value={newFullName}
                      onChange={(e) => setNewFullName(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Số thẻ Đảng</label>
                    <input
                      type="text"
                      value={newPartyCard}
                      onChange={(e) => setNewPartyCard(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Chức danh</label>
                    <input
                      type="text"
                      value={newAdminTitle}
                      onChange={(e) => setNewAdminTitle(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Chi bộ</label>
                    <select
                      value={newBranchId}
                      onChange={(e) => setNewBranchId(e.target.value)}
                      className="form-select form-select-sm"
                    >
                      <option value="">Chưa phân công</option>
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="modal-footer py-2 px-3">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setShowAddModal(false)}
                  >
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    size="sm"
                    variant="primary"
                    loading={isSubmitting}
                    loadingText="Đang lưu..."
                  >
                    Lưu
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal Sửa cán bộ */}
      {editingCadre && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: "440px" }}>
            <div className="modal-content shadow border-0">
              <div className="modal-header py-2 px-3">
                <h6 className="modal-title fw-bold mb-0">Sửa thông tin cán bộ</h6>
                <button type="button" onClick={() => setEditingCadre(null)} className="btn-close"></button>
              </div>
              <form onSubmit={handleUpdateUser}>
                <div className="modal-body p-3 space-y-2">
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Họ và tên *</label>
                    <input
                      type="text"
                      required
                      value={editFullName}
                      onChange={(e) => setEditFullName(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Số thẻ Đảng</label>
                    <input
                      type="text"
                      value={editPartyCard}
                      onChange={(e) => setEditPartyCard(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Chức danh</label>
                    <input
                      type="text"
                      value={editAdminTitle}
                      onChange={(e) => setEditAdminTitle(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Chi bộ</label>
                    <select
                      value={editBranchId}
                      onChange={(e) => setEditBranchId(e.target.value)}
                      className="form-select form-select-sm"
                    >
                      <option value="">Chưa phân công</option>
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="modal-footer py-2 px-3">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setEditingCadre(null)}
                  >
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    size="sm"
                    variant="primary"
                    loading={isSubmitting}
                    loadingText="Đang cập nhật..."
                  >
                    Cập nhật
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal Thêm Chi bộ */}
      {showAddBranchModal && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: "440px" }}>
            <div className="modal-content shadow border-0">
              <div className="modal-header py-2 px-3">
                <h6 className="modal-title fw-bold mb-0">Thêm Chi bộ</h6>
                <button type="button" onClick={() => setShowAddBranchModal(false)} className="btn-close"></button>
              </div>
              <form onSubmit={handleCreateBranch}>
                <div className="modal-body p-3 space-y-2">
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Mã Chi bộ</label>
                    <input
                      type="text"
                      value={branchCode}
                      onChange={(e) => setBranchCode(e.target.value)}
                      className="form-control form-control-sm"
                      placeholder="CB-..."
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Tên Chi bộ *</label>
                    <input
                      type="text"
                      required
                      value={branchName}
                      onChange={(e) => setBranchName(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Mô tả</label>
                    <textarea
                      rows={2}
                      value={branchDescription}
                      onChange={(e) => setBranchDescription(e.target.value)}
                      className="form-control form-control-sm"
                    ></textarea>
                  </div>
                </div>
                <div className="modal-footer py-2 px-3">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setShowAddBranchModal(false)}
                  >
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    size="sm"
                    variant="primary"
                    loading={isSubmitting}
                    loadingText="Đang lưu..."
                  >
                    Lưu
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal Sửa Chi bộ */}
      {editingBranch && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: "440px" }}>
            <div className="modal-content shadow border-0">
              <div className="modal-header py-2 px-3">
                <h6 className="modal-title fw-bold mb-0">Sửa Chi bộ</h6>
                <button type="button" onClick={() => setEditingBranch(null)} className="btn-close"></button>
              </div>
              <form onSubmit={handleUpdateBranch}>
                <div className="modal-body p-3 space-y-2">
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Tên Chi bộ *</label>
                    <input
                      type="text"
                      required
                      value={branchName}
                      onChange={(e) => setBranchName(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold text-secondary mb-1">Mô tả</label>
                    <textarea
                      rows={2}
                      value={branchDescription}
                      onChange={(e) => setBranchDescription(e.target.value)}
                      className="form-control form-control-sm"
                    ></textarea>
                  </div>
                </div>
                <div className="modal-footer py-2 px-3">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setEditingBranch(null)}
                  >
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    size="sm"
                    variant="primary"
                    loading={isSubmitting}
                    loadingText="Đang cập nhật..."
                  >
                    Cập nhật
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
      {/* Modal Phân vai trò cho cán bộ */}
      {showAssignRoleModal && selectedCadreForRoles && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: "480px" }}>
            <div className="modal-content shadow border-0" style={{ borderRadius: "12px" }}>
              <div className="modal-header py-2.5 px-3 border-bottom" style={{ borderColor: "#e2e8f0" }}>
                <div>
                  <h6 className="modal-title fw-bold text-dark mb-0" style={{ fontSize: "14px" }}>
                    Phân vai trò hệ thống
                  </h6>
                  <div className="text-secondary" style={{ fontSize: "11.5px" }}>
                    Cán bộ: <strong className="text-dark">{selectedCadreForRoles.fullName}</strong>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setShowAssignRoleModal(false)}
                  className="btn-close"
                  aria-label="Đóng"
                ></button>
              </div>

              <form onSubmit={handleSaveUserRoles}>
                <div className="modal-body p-3">
                  <div className="text-secondary mb-2" style={{ fontSize: "12px" }}>
                    Tích chọn một hoặc nhiều vai trò áp dụng cho tài khoản này:
                  </div>

                  <div className="d-flex flex-column gap-2">
                    {/* Task 09: danh sách vai trò lấy từ máy chủ; chọn theo Id vai trò, gán phạm vi Toàn công ty
                        (gán theo Phòng/Chi bộ, thời hạn: giao diện quản trị mới — task 11). */}
                    {adminRoles.map((r) => ({ code: r.id, name: r.name, desc: r.description })).map((role) => {
                      const isChecked = selectedRoleCodes.includes(role.code);
                      return (
                        <label
                          key={role.code}
                          className={`p-2.5 border rounded-2 d-flex align-items-start gap-2.5 cursor-pointer transition ${
                            isChecked ? "bg-primary-subtle border-primary" : "bg-light border-light-subtle"
                          }`}
                          style={{ cursor: "pointer" }}
                        >
                          <input
                            type="checkbox"
                            checked={isChecked}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setSelectedRoleCodes([...selectedRoleCodes, role.code]);
                              } else {
                                setSelectedRoleCodes(selectedRoleCodes.filter((c) => c !== role.code));
                              }
                            }}
                            className="form-check-input mt-0.5 shrink-0"
                          />
                          <div className="flex-grow-1">
                            <div className="d-flex align-items-center justify-content-between">
                              <span className="fw-semibold text-dark" style={{ fontSize: "12.5px" }}>
                                {role.name}
                              </span>
                            </div>
                            <div className="text-secondary mt-0.5" style={{ fontSize: "11px" }}>
                              {role.desc}
                            </div>
                          </div>
                        </label>
                      );
                    })}
                  </div>
                </div>

                <div className="modal-footer py-2 px-3 border-top" style={{ borderColor: "#e2e8f0" }}>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setShowAssignRoleModal(false)}
                  >
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    size="sm"
                    variant="primary"
                    loading={isAssigningRoles}
                    loadingText="Đang lưu..."
                  >
                    Lưu vai trò
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal Chỉnh sửa quyền hạn của vai trò */}
      {showEditRolePermsModal && selectedRoleForPerms && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered modal-lg">
            <div className="modal-content shadow border-0" style={{ borderRadius: "12px" }}>
              <div className="modal-header py-2.5 px-3 border-bottom" style={{ borderColor: "#e2e8f0" }}>
                <div>
                  <h6 className="modal-title fw-bold text-dark mb-0" style={{ fontSize: "14px" }}>
                    Cập nhật quyền hạn vai trò: {selectedRoleForPerms.name}
                  </h6>
                  <div className="text-secondary" style={{ fontSize: "11.5px" }}>
                    Mã: <code className="text-primary">{selectedRoleForPerms.code}</code> — {selectedRoleForPerms.description}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setShowEditRolePermsModal(false)}
                  className="btn-close"
                  aria-label="Đóng"
                ></button>
              </div>

              <form onSubmit={handleSaveRolePerms}>
                <div className="modal-body p-3" style={{ maxHeight: "68vh", overflowY: "auto" }}>
                  <div className="d-flex justify-content-between align-items-center mb-2.5 pb-2 border-bottom">
                    <div className="text-secondary" style={{ fontSize: "12px" }}>
                      Đã chọn: <strong className="text-primary">{selectedPermCodes.length}</strong> quyền
                    </div>
                    <div className="d-flex gap-2">
                      <button
                        type="button"
                        onClick={() => {
                          const allCodes = allPermissions.map((p) => p.code);
                          setSelectedPermCodes(allCodes);
                        }}
                        className="btn btn-sm btn-link p-0 text-decoration-none"
                        style={{ fontSize: "11.5px" }}
                      >
                        Chọn tất cả
                      </button>
                      <span className="text-muted">|</span>
                      <button
                        type="button"
                        onClick={() => setSelectedPermCodes([])}
                        className="btn btn-sm btn-link p-0 text-danger text-decoration-none"
                        style={{ fontSize: "11.5px" }}
                      >
                        Bỏ chọn tất cả
                      </button>
                    </div>
                  </div>

                  {/* Nhóm quyền theo Phân hệ — task 09: danh mục lấy từ máy chủ (GET /api/admin/permissions) */}
                  {Object.values(
                    allPermissions.reduce<Record<string, { groupName: string; resource: string; items: { code: string; name: string; desc: string }[] }>>(
                      (acc, p) => {
                        const group = acc[p.resource] ?? { groupName: p.resource, resource: p.resource, items: [] };
                        group.items.push({ code: p.code, name: p.name, desc: p.description });
                        acc[p.resource] = group;
                        return acc;
                      },
                      {}
                    )
                  ).map((grp) => (
                    <div key={grp.groupName} className="mb-3">
                      <div className="fw-bold text-dark mb-1.5 pb-1 border-bottom" style={{ fontSize: "12px", borderColor: "#f1f5f9" }}>
                        {grp.groupName}
                      </div>
                      <div className="row g-2">
                        {grp.items.map((item) => {
                          const isChecked = selectedPermCodes.includes(item.code);
                          return (
                            <div key={item.code} className="col-12 col-md-6">
                              <label
                                className={`p-2 border rounded-2 d-flex align-items-start gap-2 h-100 transition ${
                                  isChecked ? "bg-primary-subtle border-primary" : "bg-white border-light-subtle"
                                }`}
                                style={{ cursor: "pointer" }}
                              >
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  onChange={(e) => {
                                    if (e.target.checked) {
                                      setSelectedPermCodes([...selectedPermCodes, item.code]);
                                    } else {
                                      setSelectedPermCodes(selectedPermCodes.filter((c) => c !== item.code));
                                    }
                                  }}
                                  className="form-check-input mt-0.5 shrink-0"
                                />
                                <div>
                                  <div className="d-flex align-items-center gap-1.5">
                                    <span className="fw-semibold text-dark" style={{ fontSize: "12px" }}>
                                      {item.name}
                                    </span>
                                  </div>
                                  <code className="text-secondary d-block" style={{ fontSize: "10px" }}>
                                    {item.code}
                                  </code>
                                  <div className="text-muted" style={{ fontSize: "10.5px", marginTop: "2px" }}>
                                    {item.desc}
                                  </div>
                                </div>
                              </label>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  ))}
                </div>

                <div className="modal-footer py-2 px-3 border-top" style={{ borderColor: "#e2e8f0" }}>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setShowEditRolePermsModal(false)}
                  >
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    size="sm"
                    variant="primary"
                    loading={isUpdatingPerms}
                    loadingText="Đang lưu..."
                  >
                    Lưu quyền hạn
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default function UsersPage() {
  return (
    <Suspense fallback={<div className="text-center py-4 text-muted small">Đang tải...</div>}>
      <UsersContent />
    </Suspense>
  );
}
