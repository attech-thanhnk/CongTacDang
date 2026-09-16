"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { userService, CadreItem, RoleItem } from "@/services/userService";
import { organizationService, BranchItem } from "@/services/organizationService";

function UsersContent() {
  const searchParams = useSearchParams();
  const paramTab = searchParams.get("tab");
  const initialTab: "cadres" | "branches" | "roles" = 
    paramTab === "roles" || paramTab === "phan_quyen" ? "roles" : 
    paramTab === "branches" || paramTab === "to_chuc" ? "branches" : "cadres";
  const [activeTab, setActiveTab] = useState<"cadres" | "branches" | "roles">(initialTab);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedBranch, setSelectedBranch] = useState("all");

  const [cadres, setCadres] = useState<CadreItem[]>([]);
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [roles, setRoles] = useState<RoleItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Form thêm mới cán bộ
  const [showAddModal, setShowAddModal] = useState(false);
  const [newFullName, setNewFullName] = useState("");
  const [newPartyCard, setNewPartyCard] = useState("");
  const [newAdminTitle, setNewAdminTitle] = useState("");
  const [newBranchId, setNewBranchId] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Form chỉnh sửa cán bộ
  const [editingCadre, setEditingCadre] = useState<CadreItem | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editPartyCard, setEditPartyCard] = useState("");
  const [editAdminTitle, setEditAdminTitle] = useState("");
  const [editBranchId, setEditBranchId] = useState("");

  // Form thêm / sửa Chi bộ
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
    } catch (err: any) {
      setErrorMsg(err.message || "Không thể tải dữ liệu từ máy chủ nội bộ.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newFullName.trim()) {
      alert("Vui lòng nhập họ và tên cán bộ.");
      return;
    }

    setIsSubmitting(true);
    try {
      await userService.createUser({
        fullName: newFullName.trim(),
        partyCardNumber: newPartyCard.trim() || null,
        adminTitle: newAdminTitle.trim() || null,
        partyCellId: newBranchId || null,
      });

      alert("Đã lưu hồ sơ cán bộ thành công vào cơ sở dữ liệu!");
      setShowAddModal(false);
      setNewFullName("");
      setNewPartyCard("");
      setNewAdminTitle("");
      setNewBranchId("");
      loadData();
    } catch (err: any) {
      alert(err.message || "Lỗi khi tạo mới cán bộ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenEditUser = (c: CadreItem) => {
    setEditingCadre(c);
    setEditFullName(c.fullName);
    setEditPartyCard(c.partyCardNumber || "");
    setEditAdminTitle(c.adminTitle || "");
    const foundBranch = branches.find(b => b.name === c.partyCellName);
    setEditBranchId(foundBranch ? foundBranch.id : "");
  };

  const handleUpdateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingCadre) return;
    if (!editFullName.trim()) {
      alert("Họ và tên cán bộ không được để trống.");
      return;
    }

    setIsSubmitting(true);
    try {
      await userService.updateUser(editingCadre.id, {
        fullName: editFullName.trim(),
        partyCardNumber: editPartyCard.trim() || null,
        adminTitle: editAdminTitle.trim() || null,
        partyCellId: editBranchId || null,
      });

      alert("Cập nhật hồ sơ cán bộ thành công!");
      setEditingCadre(null);
      loadData();
    } catch (err: any) {
      alert(err.message || "Không thể cập nhật hồ sơ cán bộ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteUser = async (id: string, name: string) => {
    if (!confirm(`Xác nhận xóa hồ sơ của đồng chí '${name}' khỏi cơ sở dữ liệu?`)) return;

    try {
      await userService.deleteUser(id);
      alert("Đã xóa hồ sơ cán bộ thành công.");
      loadData();
    } catch (err: any) {
      alert(err.message || "Không thể xóa cán bộ từ máy chủ.");
    }
  };

  const handleCreateBranch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!branchName.trim()) {
      alert("Vui lòng nhập tên Chi bộ.");
      return;
    }

    setIsSubmitting(true);
    try {
      await organizationService.createBranch({
        code: branchCode.trim() || undefined,
        name: branchName.trim(),
        description: branchDescription.trim() || undefined,
      });

      alert("Đã thêm mới Chi bộ Đảng thành công!");
      setShowAddBranchModal(false);
      setBranchCode("");
      setBranchName("");
      setBranchDescription("");
      loadData();
    } catch (err: any) {
      alert(err.message || "Lỗi khi thêm mới Chi bộ.");
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
    if (!editingBranch) return;
    if (!branchName.trim()) {
      alert("Tên Chi bộ không được để trống.");
      return;
    }

    setIsSubmitting(true);
    try {
      await organizationService.updateBranch(editingBranch.id, {
        name: branchName.trim(),
        description: branchDescription.trim(),
      });

      alert("Cập nhật Chi bộ thành công!");
      setEditingBranch(null);
      setBranchName("");
      setBranchDescription("");
      loadData();
    } catch (err: any) {
      alert(err.message || "Không thể cập nhật Chi bộ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteBranch = async (id: string, name: string) => {
    if (!confirm(`Xác nhận xóa Chi bộ '${name}'?`)) return;

    try {
      await organizationService.deleteBranch(id);
      alert("Đã xóa Chi bộ thành công.");
      loadData();
    } catch (err: any) {
      alert(err.message || "Không thể xóa Chi bộ.");
    }
  };

  const filteredCadres = cadres.filter(c => {
    const matchSearch = (c.fullName || "").toLowerCase().includes(searchTerm.toLowerCase()) ||
                        (c.adminTitle || "").toLowerCase().includes(searchTerm.toLowerCase()) ||
                        (c.partyCardNumber || "").toLowerCase().includes(searchTerm.toLowerCase());
    const matchBranch = selectedBranch === "all" || c.partyCellName === selectedBranch;
    return matchSearch && matchBranch;
  });

  return (
    <div className="space-y-6 font-serif max-w-5xl mx-auto">
      {/* Tiêu đề & Điều hướng Tab */}
      <div className="bg-white border border-slate-300 rounded p-5 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-300 pb-3">
          <div>
            <h1 className="text-base font-bold text-slate-900 uppercase">
              QUẢN TRỊ NGƯỜI DÙNG, TỔ CHỨC & PHÂN QUYỀN
            </h1>
            <p className="text-xs text-slate-600 mt-0.5">
              Hệ cơ sở dữ liệu hồ sơ cán bộ lãnh đạo, quản lý 2 vai, chi bộ Đảng và ma trận phân quyền
            </p>
          </div>

          <div className="flex gap-2">
            {activeTab === "branches" ? (
              <button
                onClick={() => {
                  setBranchCode("");
                  setBranchName("");
                  setBranchDescription("");
                  setShowAddBranchModal(true);
                }}
                className="px-3.5 py-1.5 bg-slate-900 text-white rounded text-xs font-semibold hover:bg-slate-800 transition"
              >
                + Thành lập Chi bộ mới
              </button>
            ) : (
              <button
                onClick={() => setShowAddModal(true)}
                className="px-3.5 py-1.5 bg-slate-900 text-white rounded text-xs font-semibold hover:bg-slate-800 transition"
              >
                + Tiếp nhận hồ sơ cán bộ
              </button>
            )}
          </div>
        </div>

        {/* Thanh chuyển Tab thuần văn bản */}
        <div className="flex border-b border-slate-300 mt-4 text-xs font-semibold">
          <button
            onClick={() => setActiveTab("cadres")}
            className={`px-4 py-2 border-b-2 transition ${
              activeTab === "cadres"
                ? "border-slate-900 text-slate-950 font-bold bg-slate-50"
                : "border-transparent text-slate-600 hover:text-slate-900"
            }`}
          >
            1. Danh sách Cán bộ 2 vai ({cadres.length})
          </button>

          <button
            onClick={() => setActiveTab("branches")}
            className={`px-4 py-2 border-b-2 transition ${
              activeTab === "branches"
                ? "border-slate-900 text-slate-950 font-bold bg-slate-50"
                : "border-transparent text-slate-600 hover:text-slate-900"
            }`}
          >
            2. Chi bộ & Tổ chức Đảng ({branches.length})
          </button>

          <button
            onClick={() => setActiveTab("roles")}
            className={`px-4 py-2 border-b-2 transition ${
              activeTab === "roles"
                ? "border-slate-900 text-slate-950 font-bold bg-slate-50"
                : "border-transparent text-slate-600 hover:text-slate-900"
            }`}
          >
            3. Ma trận Phân quyền ({roles.length})
          </button>
        </div>

        {/* Thông báo lỗi nếu có */}
        {errorMsg && (
          <div className="my-3 p-3 bg-red-50 border border-red-300 text-red-700 text-xs rounded">
            <strong>Thông báo:</strong> {errorMsg}
          </div>
        )}

        {/* Tab 1: Danh sách Cán bộ (Full CRUD: Create, Read, Update, Delete) */}
        {activeTab === "cadres" && (
          <div className="mt-4 space-y-4">
            {/* Bộ lọc tìm kiếm */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <span className="text-xs text-slate-600">Đơn vị:</span>
                <select
                  value={selectedBranch}
                  onChange={(e) => setSelectedBranch(e.target.value)}
                  className="border border-slate-300 rounded px-2.5 py-1 text-xs text-slate-800 bg-white"
                >
                  <option value="all">-- Toàn bộ Chi bộ trực thuộc --</option>
                  {branches.map(b => (
                    <option key={b.id} value={b.name}>{b.name}</option>
                  ))}
                </select>
              </div>

              <input
                type="text"
                placeholder="Tìm họ tên, chức danh, số thẻ đảng..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="border border-slate-300 rounded px-3 py-1 text-xs w-full sm:w-72"
              />
            </div>

            {loading ? (
              <p className="text-xs text-slate-500 py-6 text-center italic">Đang tải dữ liệu từ cơ sở dữ liệu nội bộ...</p>
            ) : filteredCadres.length === 0 ? (
              <div className="border border-slate-200 rounded p-6 text-center text-xs text-slate-600 bg-slate-50">
                Không tìm thấy cán bộ nào phù hợp với điều kiện tìm kiếm.
              </div>
            ) : (
              <div className="overflow-x-auto border border-slate-300 rounded">
                <table className="w-full text-xs text-slate-800 border-collapse">
                  <thead>
                    <tr className="bg-slate-100 font-bold border-b border-slate-300 text-left">
                      <th className="border-r border-slate-300 p-2.5 text-center w-10">STT</th>
                      <th className="border-r border-slate-300 p-2.5">Họ và tên cán bộ</th>
                      <th className="border-r border-slate-300 p-2.5">Số thẻ Đảng</th>
                      <th className="border-r border-slate-300 p-2.5">Chi bộ sinh hoạt</th>
                      <th className="border-r border-slate-300 p-2.5">Chức danh công tác</th>
                      <th className="border-r border-slate-300 p-2.5 text-center w-24">Trạng thái</th>
                      <th className="p-2.5 text-center w-28">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredCadres.map((c, idx) => (
                      <tr key={c.id} className="border-b border-slate-200 hover:bg-slate-50">
                        <td className="border-r border-slate-200 p-2 text-center">{idx + 1}</td>
                        <td className="border-r border-slate-200 p-2 font-semibold text-slate-950">
                          {c.fullName}
                          {c.isPartyMember && (
                            <span className="ml-1.5 text-[10px] text-red-700 bg-red-50 border border-red-200 px-1 py-0.2 rounded font-normal">
                              Đảng viên
                            </span>
                          )}
                        </td>
                        <td className="border-r border-slate-200 p-2 text-slate-600">{c.partyCardNumber || "—"}</td>
                        <td className="border-r border-slate-200 p-2">{c.partyCellName || "—"}</td>
                        <td className="border-r border-slate-200 p-2 text-slate-700">{c.adminTitle || c.partyRole || "—"}</td>
                        <td className="border-r border-slate-200 p-2 text-center">
                          <span className="text-[11px] text-emerald-800 font-medium">Hoạt động</span>
                        </td>
                        <td className="p-2 text-center space-x-2">
                          <button
                            onClick={() => handleOpenEditUser(c)}
                            className="text-[11px] text-blue-700 hover:text-blue-900 underline font-semibold"
                          >
                            Sửa
                          </button>
                          <button
                            onClick={() => handleDeleteUser(c.id, c.fullName)}
                            className="text-[11px] text-red-600 hover:text-red-800 underline hover:font-bold"
                          >
                            Xóa
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Tab 2: Danh sách Chi bộ (Full CRUD) */}
        {activeTab === "branches" && (
          <div className="mt-4 space-y-4">
            <div className="overflow-x-auto border border-slate-300 rounded">
              <table className="w-full text-xs text-slate-800 border-collapse">
                <thead>
                  <tr className="bg-slate-100 font-bold border-b border-slate-300 text-left">
                    <th className="border-r border-slate-300 p-2.5 text-center w-10">STT</th>
                    <th className="border-r border-slate-300 p-2.5 w-28">Mã Chi bộ</th>
                    <th className="border-r border-slate-300 p-2.5">Tên Chi bộ / Đơn vị Đảng</th>
                    <th className="border-r border-slate-300 p-2.5">Nhiệm vụ trọng tâm</th>
                    <th className="border-r border-slate-300 p-2.5 text-center w-28">Sĩ số</th>
                    <th className="p-2.5 text-center w-28">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {branches.map((b, idx) => (
                    <tr key={b.id} className="border-b border-slate-200 hover:bg-slate-50">
                      <td className="border-r border-slate-200 p-2 text-center">{idx + 1}</td>
                      <td className="border-r border-slate-200 p-2 font-mono font-bold text-slate-900">{b.code}</td>
                      <td className="border-r border-slate-200 p-2 font-semibold text-slate-950">{b.name}</td>
                      <td className="border-r border-slate-200 p-2 text-slate-600">{b.description || "—"}</td>
                      <td className="border-r border-slate-200 p-2 text-center font-bold text-slate-900">{b.memberCount} đồng chí</td>
                      <td className="p-2 text-center space-x-2">
                        <button
                          onClick={() => handleOpenEditBranch(b)}
                          className="text-[11px] text-blue-700 hover:text-blue-900 underline font-semibold"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleDeleteBranch(b.id, b.name)}
                          className="text-[11px] text-red-600 hover:text-red-800 underline hover:font-bold"
                        >
                          Xóa
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 3: Ma trận Phân quyền */}
        {activeTab === "roles" && (
          <div className="mt-4 space-y-4">
            <div className="p-3 bg-slate-50 border border-slate-300 rounded text-xs text-slate-700">
              <strong>Nguyên tắc phân quyền hệ thống:</strong> Thiết lập kiểm soát theo đúng vai trò công tác, bảo đảm tính độc lập giữa khâu tự đánh giá, thẩm định chuyên môn và quyết định xếp loại của Ban Thường vụ Đảng ủy.
            </div>

            <div className="overflow-x-auto border border-slate-300 rounded">
              <table className="w-full text-xs text-slate-800 border-collapse">
                <thead>
                  <tr className="bg-slate-100 font-bold border-b border-slate-300 text-left">
                    <th className="border-r border-slate-300 p-2.5 text-center w-10">STT</th>
                    <th className="border-r border-slate-300 p-2.5 w-44">Mã định danh vai trò</th>
                    <th className="border-r border-slate-300 p-2.5 w-52">Tên nhóm quyền hạn</th>
                    <th className="p-2.5">Phạm vi trách nhiệm và thẩm quyền thực hiện</th>
                  </tr>
                </thead>
                <tbody>
                  {roles.map((r, idx) => (
                    <tr key={r.code} className="border-b border-slate-200 hover:bg-slate-50">
                      <td className="border-r border-slate-200 p-2 text-center">{idx + 1}</td>
                      <td className="border-r border-slate-200 p-2 font-mono font-bold text-slate-900">{r.code}</td>
                      <td className="border-r border-slate-200 p-2 font-semibold text-slate-950">{r.name}</td>
                      <td className="p-2 text-slate-700">{r.description}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {/* Modal 1: Tiếp nhận hồ sơ cán bộ mới */}
      {showAddModal && (
        <div className="fixed inset-0 bg-slate-900/60 flex items-center justify-center p-4 z-50">
          <div className="bg-white border border-slate-400 rounded max-w-lg w-full p-5 shadow-lg space-y-4">
            <div className="border-b border-slate-300 pb-2">
              <h2 className="text-sm font-bold text-slate-950 uppercase">Tiếp nhận hồ sơ cán bộ mới</h2>
              <p className="text-xs text-slate-600">Nhập đầy đủ thông tin để ghi nhận vào cơ sở dữ liệu</p>
            </div>

            <form onSubmit={handleCreateUser} className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-800 mb-1">
                  Họ và tên cán bộ <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="Ví dụ: Nguyễn Văn Bình"
                  value={newFullName}
                  onChange={(e) => setNewFullName(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Số thẻ Đảng viên (nếu có)</label>
                <input
                  type="text"
                  placeholder="Ví dụ: ATTECH-008"
                  value={newPartyCard}
                  onChange={(e) => setNewPartyCard(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Chức danh công tác</label>
                <input
                  type="text"
                  placeholder="Ví dụ: Phó Trưởng phòng Kỹ thuật"
                  value={newAdminTitle}
                  onChange={(e) => setNewAdminTitle(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Chi bộ Đảng trực thuộc</label>
                <select
                  value={newBranchId}
                  onChange={(e) => setNewBranchId(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2 bg-white"
                >
                  <option value="">-- Chưa phân công chi bộ --</option>
                  {branches.map(b => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-3 py-1.5 border border-slate-300 rounded text-slate-700 hover:bg-slate-100"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-4 py-1.5 bg-slate-900 text-white rounded font-semibold hover:bg-slate-800 disabled:opacity-50"
                >
                  {isSubmitting ? "Đang lưu..." : "Lưu vào cơ sở dữ liệu"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 2: Chỉnh sửa hồ sơ cán bộ */}
      {editingCadre && (
        <div className="fixed inset-0 bg-slate-900/60 flex items-center justify-center p-4 z-50">
          <div className="bg-white border border-slate-400 rounded max-w-lg w-full p-5 shadow-lg space-y-4">
            <div className="border-b border-slate-300 pb-2">
              <h2 className="text-sm font-bold text-slate-950 uppercase">Chỉnh sửa hồ sơ cán bộ</h2>
              <p className="text-xs text-slate-600">Cập nhật thông tin cán bộ trong cơ sở dữ liệu</p>
            </div>

            <form onSubmit={handleUpdateUser} className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-800 mb-1">
                  Họ và tên cán bộ <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={editFullName}
                  onChange={(e) => setEditFullName(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Số thẻ Đảng viên</label>
                <input
                  type="text"
                  value={editPartyCard}
                  onChange={(e) => setEditPartyCard(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Chức danh công tác</label>
                <input
                  type="text"
                  value={editAdminTitle}
                  onChange={(e) => setEditAdminTitle(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Chi bộ Đảng trực thuộc</label>
                <select
                  value={editBranchId}
                  onChange={(e) => setEditBranchId(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2 bg-white"
                >
                  <option value="">-- Chưa phân công chi bộ --</option>
                  {branches.map(b => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
                <button
                  type="button"
                  onClick={() => setEditingCadre(null)}
                  className="px-3 py-1.5 border border-slate-300 rounded text-slate-700 hover:bg-slate-100"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-4 py-1.5 bg-slate-900 text-white rounded font-semibold hover:bg-slate-800 disabled:opacity-50"
                >
                  {isSubmitting ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 3: Thêm mới Chi bộ */}
      {showAddBranchModal && (
        <div className="fixed inset-0 bg-slate-900/60 flex items-center justify-center p-4 z-50">
          <div className="bg-white border border-slate-400 rounded max-w-lg w-full p-5 shadow-lg space-y-4">
            <div className="border-b border-slate-300 pb-2">
              <h2 className="text-sm font-bold text-slate-950 uppercase">Thành lập Chi bộ Đảng mới</h2>
              <p className="text-xs text-slate-600">Đăng ký Chi bộ mới trực thuộc Đảng bộ công ty</p>
            </div>

            <form onSubmit={handleCreateBranch} className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-800 mb-1">Mã định danh Chi bộ</label>
                <input
                  type="text"
                  placeholder="Ví dụ: CB-KT, CB-SX..."
                  value={branchCode}
                  onChange={(e) => setBranchCode(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2 font-mono uppercase"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">
                  Tên Chi bộ <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="Ví dụ: Chi bộ Khối Nghiên cứu phát triển"
                  value={branchName}
                  onChange={(e) => setBranchName(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Nhiệm vụ trọng tâm / Mô tả</label>
                <textarea
                  rows={3}
                  placeholder="Mô tả phạm vi phụ trách của Chi bộ..."
                  value={branchDescription}
                  onChange={(e) => setBranchDescription(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
                <button
                  type="button"
                  onClick={() => setShowAddBranchModal(false)}
                  className="px-3 py-1.5 border border-slate-300 rounded text-slate-700 hover:bg-slate-100"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-4 py-1.5 bg-slate-900 text-white rounded font-semibold hover:bg-slate-800 disabled:opacity-50"
                >
                  {isSubmitting ? "Đang lưu..." : "Tạo Chi bộ"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 4: Chỉnh sửa Chi bộ */}
      {editingBranch && (
        <div className="fixed inset-0 bg-slate-900/60 flex items-center justify-center p-4 z-50">
          <div className="bg-white border border-slate-400 rounded max-w-lg w-full p-5 shadow-lg space-y-4">
            <div className="border-b border-slate-300 pb-2">
              <h2 className="text-sm font-bold text-slate-950 uppercase">Chỉnh sửa Chi bộ Đảng</h2>
              <p className="text-xs text-slate-600">Cập nhật thông tin Chi bộ {editingBranch.code}</p>
            </div>

            <form onSubmit={handleUpdateBranch} className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-800 mb-1">
                  Tên Chi bộ <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={branchName}
                  onChange={(e) => setBranchName(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-800 mb-1">Nhiệm vụ trọng tâm / Mô tả</label>
                <textarea
                  rows={3}
                  value={branchDescription}
                  onChange={(e) => setBranchDescription(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
                <button
                  type="button"
                  onClick={() => setEditingBranch(null)}
                  className="px-3 py-1.5 border border-slate-300 rounded text-slate-700 hover:bg-slate-100"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-4 py-1.5 bg-slate-900 text-white rounded font-semibold hover:bg-slate-800 disabled:opacity-50"
                >
                  {isSubmitting ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default function UsersPage() {
  return (
    <React.Suspense
      fallback={
        <div className="p-6 text-center text-xs text-slate-500 font-serif">
          Đang nạp phân hệ Quản trị người dùng & Tổ chức...
        </div>
      }
    >
      <UsersContent />
    </React.Suspense>
  );
}
