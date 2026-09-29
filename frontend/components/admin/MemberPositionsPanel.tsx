"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card } from "@/components/common";
import {
  APPROVAL_AUTHORITY_CODE_LABELS,
  ApprovalAuthorityCode,
  CatalogItem,
  catalogService,
  Position,
  POSITION_SIDE_LABELS,
  PositionSide,
  treeLabel,
} from "@/services/catalogService";
import { ApprovalAuthorityInfo, MemberPosition, SaveMemberPositionPayload, userService } from "@/services/userService";
import { AdminModal } from "./AdminModal";
import {
  endOfDayExclusiveIso,
  errorMessage,
  errorTitle,
  exclusiveIsoToLastDayInput,
  formatDate,
  startOfDayIso,
  toDateInput,
} from "./adminUtils";

interface PositionForm {
  id?: string;
  positionId: string;
  unitId: string;
  isPrimary: boolean;
  validFrom: string;
  validTo: string;
  note: string;
}

export interface MemberPositionsPanelProps {
  userId: string;
  canManage: boolean;
  departments: CatalogItem[];
  partyCells: CatalogItem[];
  /** Gọi sau khi chức vụ/thẩm quyền thay đổi (chức danh, thẩm quyền trên thông tin tài khoản có thể đổi). */
  onChanged?: () => void;
}

/** Nơi giữ chức vụ theo bên: chức vụ Đảng → tổ chức Đảng; chính quyền → đơn vị; đoàn thể/khác → cả hai. */
function unitKinds(side?: PositionSide): ("department" | "cell")[] {
  if (side === "Party") return ["cell"];
  if (side === "Administrative") return ["department"];
  return ["department", "cell"];
}

/** Chức vụ (kể cả kiêm nhiệm) của cán bộ và thẩm quyền phê duyệt (suy ra / đặt tay). */
export function MemberPositionsPanel({ userId, canManage, departments, partyCells, onChanged }: MemberPositionsPanelProps) {
  const { toast, confirm } = useToast();
  const [items, setItems] = useState<MemberPosition[]>([]);
  const [authority, setAuthority] = useState<ApprovalAuthorityInfo | null>(null);
  const [positions, setPositions] = useState<Position[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<PositionForm | null>(null);
  const [overrideForm, setOverrideForm] = useState<{ value: "" | ApprovalAuthorityCode; reason: string } | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, info] = await Promise.all([userService.listPositions(userId), userService.getApprovalAuthority(userId)]);
      setItems(list);
      setAuthority(info);
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được chức vụ của cán bộ."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (!canManage) return;
    catalogService
      .listPositions()
      .then(setPositions)
      .catch((err: unknown) => toast.error(errorMessage(err, "Không tải được danh mục chức vụ."), errorTitle(err)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canManage]);

  const afterChange = async () => {
    await load();
    onChanged?.();
  };

  const current = items.filter((i) => i.isEffective || (i.validTo == null && new Date(i.validFrom) > new Date()));
  const history = items.filter((i) => !current.includes(i));

  const selectedPosition = useMemo(() => positions.find((p) => p.id === form?.positionId), [positions, form?.positionId]);
  const editing = form?.id ? items.find((i) => i.id === form.id) : undefined;
  const side = (selectedPosition?.side ?? (editing?.side as PositionSide | undefined)) as PositionSide | undefined;
  const kinds = unitKinds(side);
  const unitOptions = [
    ...(kinds.includes("department") ? departments.map((d) => ({ value: `d:${d.id}`, label: treeLabel(d), active: d.isActive })) : []),
    ...(kinds.includes("cell") ? partyCells.map((c) => ({ value: `c:${c.id}`, label: treeLabel(c), active: c.isActive })) : []),
  ].filter((o) => o.active || o.value === form?.unitId);

  const openCreate = () =>
    setForm({ positionId: "", unitId: "", isPrimary: current.every((c) => !c.isPrimary), validFrom: "", validTo: "", note: "" });

  const openEdit = (item: MemberPosition) =>
    setForm({
      id: item.id,
      positionId: item.positionId,
      unitId: item.departmentId ? `d:${item.departmentId}` : item.partyCellId ? `c:${item.partyCellId}` : "",
      isPrimary: item.isPrimary,
      validFrom: toDateInput(item.validFrom),
      validTo: exclusiveIsoToLastDayInput(item.validTo),
      note: item.note ?? "",
    });

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    if (form.validFrom && form.validTo && form.validTo < form.validFrom) {
      toast.error("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
      return;
    }
    const [kind, unitId] = form.unitId ? form.unitId.split(":") : ["", ""];
    const payload: SaveMemberPositionPayload = {
      positionId: form.id ? undefined : form.positionId,
      departmentId: kind === "d" ? unitId : form.id ? "00000000-0000-0000-0000-000000000000" : undefined,
      partyCellId: kind === "c" ? unitId : form.id ? "00000000-0000-0000-0000-000000000000" : undefined,
      isPrimary: form.isPrimary,
      validFrom: startOfDayIso(form.validFrom),
      validTo: endOfDayExclusiveIso(form.validTo),
      clearValidTo: form.id ? !form.validTo : undefined,
      note: form.note.trim(),
    };
    setSaving(true);
    try {
      if (form.id) await userService.updatePosition(userId, form.id, payload);
      else await userService.addPosition(userId, payload);
      toast.success(form.id ? "Đã cập nhật chức vụ." : "Đã thêm chức vụ.");
      setForm(null);
      await afterChange();
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được chức vụ."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  const handleEnd = (item: MemberPosition) =>
    confirm({
      title: "Kết thúc chức vụ",
      message: `Kết thúc ngay chức vụ "${item.positionName}"? Chức vụ chuyển vào lịch sử; thẩm quyền phê duyệt được suy ra lại.`,
      confirmText: "Kết thúc",
      isDanger: true,
      onConfirm: async () => {
        try {
          await userService.endPosition(userId, item.id);
          toast.success("Đã kết thúc chức vụ.");
          await afterChange();
        } catch (err) {
          toast.error(errorMessage(err, "Không kết thúc được chức vụ."), errorTitle(err), 10000);
        }
      },
    });

  const handleDelete = (item: MemberPosition) =>
    confirm({
      title: "Xóa chức vụ",
      message: `Xóa bản ghi chức vụ "${item.positionName}"? Chỉ dùng khi nhập nhầm; bình thường hãy dùng "Kết thúc" để giữ lịch sử.`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await userService.removePosition(userId, item.id);
          toast.success("Đã xóa chức vụ.");
          await afterChange();
        } catch (err) {
          toast.error(errorMessage(err, "Không xóa được chức vụ."), errorTitle(err), 10000);
        }
      },
    });

  const handleOverride = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!overrideForm) return;
    setSaving(true);
    try {
      setAuthority(await userService.setApprovalAuthority(userId, overrideForm.value, overrideForm.reason.trim() || undefined));
      toast.success(overrideForm.value ? "Đã đặt tay thẩm quyền phê duyệt." : "Đã bỏ đặt tay — thẩm quyền suy ra từ chức vụ.");
      setOverrideForm(null);
      onChanged?.();
    } catch (err) {
      toast.error(errorMessage(err, "Không cập nhật được thẩm quyền."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  const unitName = (i: MemberPosition) => i.departmentName || i.partyCellName || "—";

  const renderTable = (rows: MemberPosition[], active: boolean) => (
    <div className="table-responsive">
      <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
        <thead style={{ background: "var(--bg-base)" }}>
          <tr>
            <th className="ps-3">Chức vụ</th>
            <th>Nơi giữ chức vụ</th>
            <th style={{ width: 80 }}>Mã</th>
            <th style={{ width: 110 }}>Từ ngày</th>
            <th style={{ width: 110 }}>Đến hết ngày</th>
            {canManage && <th className="pe-3 text-end" style={{ width: 120 }}>Thao tác</th>}
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={6} className="text-center py-3 text-secondary small">
                {active ? "Chưa có chức vụ nào đang giữ." : "Chưa có chức vụ nào đã kết thúc."}
              </td>
            </tr>
          ) : (
            rows.map((i) => (
              <tr key={i.id}>
                <td className="ps-3">
                  <span className="fw-semibold text-dark">{i.positionName}</span>
                  {i.isPrimary && active && <span className="badge bg-primary-subtle text-primary border border-primary-subtle ms-2">Chính</span>}
                  {!i.isPrimary && active && <span className="badge bg-light text-secondary border ms-2">Kiêm nhiệm</span>}
                  {i.defaultApprovalAuthority === "CapTren" && <span className="badge bg-warning-subtle text-warning-emphasis border ms-1">Cấp trên QĐ</span>}
                  <div className="small text-secondary">{POSITION_SIDE_LABELS[i.side as PositionSide] ?? i.side}{i.note ? ` · ${i.note}` : ""}</div>
                </td>
                <td>{unitName(i)}</td>
                <td>{i.statCode ? <code>{i.statCode}</code> : "—"}</td>
                <td>{formatDate(i.validFrom)}</td>
                <td>{i.validTo ? formatDate(exclusiveIsoToLastDayInput(i.validTo)) : "Không thời hạn"}</td>
                {canManage && (
                  <td className="pe-3 text-end text-nowrap">
                    {active && (
                      <>
                        <button type="button" className="btn btn-sm btn-link p-1" title="Sửa" onClick={() => openEdit(i)}>
                          <i className="bi bi-pencil-square" />
                        </button>
                        <button type="button" className="btn btn-sm btn-link p-1 text-warning" title="Kết thúc" onClick={() => handleEnd(i)}>
                          <i className="bi bi-stop-circle" />
                        </button>
                      </>
                    )}
                    <button type="button" className="btn btn-sm btn-link p-1 text-danger" title="Xóa (nhập nhầm)" onClick={() => handleDelete(i)}>
                      <i className="bi bi-trash" />
                    </button>
                  </td>
                )}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );

  return (
    <>
      <Card className="mb-3">
        <div className="px-3 py-2 d-flex flex-wrap align-items-center gap-3" style={{ fontSize: 13 }}>
          <div>
            <div className="small text-secondary">Cấp quyết định xếp loại đang áp dụng</div>
            <div className="fw-semibold">
              {authority ? APPROVAL_AUTHORITY_CODE_LABELS[authority.effective] : "—"}
              {authority?.override ? (
                <span className="badge bg-warning-subtle text-warning-emphasis border ms-2">Đặt tay</span>
              ) : (
                <span className="badge bg-light text-secondary border ms-2">Suy ra từ chức vụ</span>
              )}
            </div>
            {authority && (
              <div className="small text-secondary">
                Suy ra: {APPROVAL_AUTHORITY_CODE_LABELS[authority.derived]}
                {authority.capTrenPositions.length > 0 && ` (do giữ: ${authority.capTrenPositions.join(", ")})`}
                {authority.override && authority.overrideReason && ` · Lý do đặt tay: ${authority.overrideReason}`}
              </div>
            )}
          </div>
          <div>
            <div className="small text-secondary">Mã chức danh thống kê (15A/15B)</div>
            <div className="fw-semibold">{authority?.statCode ?? "—"}</div>
          </div>
          {canManage && authority && (
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary ms-auto"
              onClick={() => setOverrideForm({ value: authority.override ?? "", reason: authority.overrideReason ?? "" })}
            >
              <i className="bi bi-sliders me-1" />
              Đặt tay / bỏ đặt tay
            </button>
          )}
        </div>
      </Card>

      <div className="d-flex justify-content-between align-items-center mb-2">
        <h2 className="h6 fw-bold mb-0">Chức vụ đang giữ (kể cả kiêm nhiệm)</h2>
        {canManage && (
          <button type="button" className="btn btn-sm btn-primary" onClick={openCreate}>
            <i className="bi bi-plus-lg me-1" />
            Thêm chức vụ
          </button>
        )}
      </div>
      <Card className="mb-3">
        {loading && items.length === 0 ? (
          <div className="text-center py-4 text-secondary small">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải…
          </div>
        ) : (
          renderTable(current, true)
        )}
      </Card>
      <h2 className="h6 fw-bold mb-2">Đã kết thúc</h2>
      <Card>{renderTable(history, false)}</Card>

      {form && (
        <AdminModal title={form.id ? "Sửa chức vụ" : "Thêm chức vụ"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={560}>
          <form onSubmit={handleSave}>
            <div className="mb-2">
              <label className="form-label small fw-semibold mb-1">
                Chức vụ <span className="text-danger">*</span>
              </label>
              {form.id ? (
                <div className="form-control form-control-sm bg-light">{editing?.positionName}</div>
              ) : (
                <select
                  className="form-select form-select-sm"
                  value={form.positionId}
                  required
                  onChange={(e) => setForm({ ...form, positionId: e.target.value, unitId: "" })}
                >
                  <option value="">— Chọn chức vụ —</option>
                  {positions
                    .filter((p) => p.isActive)
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name} ({POSITION_SIDE_LABELS[p.side]}
                        {p.statCode ? `, ${p.statCode}` : ""})
                      </option>
                    ))}
                </select>
              )}
            </div>
            <div className="mb-2">
              <label className="form-label small fw-semibold mb-1">Nơi giữ chức vụ</label>
              <select className="form-select form-select-sm" value={form.unitId} onChange={(e) => setForm({ ...form, unitId: e.target.value })}>
                <option value="">— Không ghi đơn vị —</option>
                {unitOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                    {o.active ? "" : " (ngừng hoạt động)"}
                  </option>
                ))}
              </select>
              <div className="form-text">
                {side === "Party"
                  ? "Chức vụ Đảng: chọn tổ chức Đảng."
                  : side === "Administrative"
                    ? "Chức vụ chính quyền: chọn đơn vị chính quyền."
                    : "Chọn đơn vị chính quyền hoặc tổ chức Đảng."}
              </div>
            </div>
            <div className="row g-2 mb-2">
              <div className="col-6">
                <label className="form-label small fw-semibold mb-1">Từ ngày</label>
                <input type="date" className="form-control form-control-sm" value={form.validFrom} onChange={(e) => setForm({ ...form, validFrom: e.target.value })} />
                <div className="form-text">{form.id ? "Để trống = giữ nguyên." : "Để trống = hôm nay."}</div>
              </div>
              <div className="col-6">
                <label className="form-label small fw-semibold mb-1">Đến hết ngày</label>
                <input type="date" className="form-control form-control-sm" value={form.validTo} onChange={(e) => setForm({ ...form, validTo: e.target.value })} />
                <div className="form-text">Để trống = không thời hạn.</div>
              </div>
            </div>
            <div className="form-check mb-2">
              <input
                id="member-position-primary"
                type="checkbox"
                className="form-check-input"
                checked={form.isPrimary}
                onChange={(e) => setForm({ ...form, isPrimary: e.target.checked })}
              />
              <label className="form-check-label small" htmlFor="member-position-primary">
                Chức vụ chính (chức danh hiển thị mặc định; bỏ đánh dấu = kiêm nhiệm)
              </label>
            </div>
            <div className="mb-2">
              <label className="form-label small fw-semibold mb-1">Ghi chú</label>
              <input
                className="form-control form-control-sm"
                maxLength={1000}
                value={form.note}
                placeholder="VD: Quyết định bổ nhiệm số …"
                onChange={(e) => setForm({ ...form, note: e.target.value })}
              />
            </div>
            <div className="d-flex justify-content-end gap-2 mt-3">
              <button type="button" className="btn btn-sm btn-light" onClick={() => setForm(null)} disabled={saving}>
                Hủy
              </button>
              <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
                {saving && <span className="spinner-border spinner-border-sm me-1" />}
                Lưu
              </button>
            </div>
          </form>
        </AdminModal>
      )}

      {overrideForm && (
        <AdminModal title="Thẩm quyền phê duyệt" onClose={() => setOverrideForm(null)} closeDisabled={saving} maxWidth={480}>
          <form onSubmit={handleOverride}>
            <div className="mb-2">
              <label className="form-label small fw-semibold mb-1">Cấp quyết định xếp loại</label>
              <select
                className="form-select form-select-sm"
                value={overrideForm.value}
                onChange={(e) => setOverrideForm({ ...overrideForm, value: e.target.value as "" | ApprovalAuthorityCode })}
              >
                <option value="">Suy ra từ chức vụ (khuyến nghị)</option>
                {(Object.keys(APPROVAL_AUTHORITY_CODE_LABELS) as ApprovalAuthorityCode[]).map((a) => (
                  <option key={a} value={a}>
                    Đặt tay: {APPROVAL_AUTHORITY_CODE_LABELS[a]}
                  </option>
                ))}
              </select>
            </div>
            {overrideForm.value && (
              <div className="mb-2">
                <label className="form-label small fw-semibold mb-1">
                  Lý do <span className="text-danger">*</span>
                </label>
                <textarea
                  className="form-control form-control-sm"
                  rows={2}
                  required
                  maxLength={1000}
                  value={overrideForm.reason}
                  placeholder="Căn cứ văn bản phân cấp…"
                  onChange={(e) => setOverrideForm({ ...overrideForm, reason: e.target.value })}
                />
              </div>
            )}
            <div className="small text-secondary">Hồ sơ đánh giá đã tạo giữ nguyên ảnh chụp thẩm quyền; thay đổi áp dụng cho hồ sơ tạo sau.</div>
            <div className="d-flex justify-content-end gap-2 mt-3">
              <button type="button" className="btn btn-sm btn-light" onClick={() => setOverrideForm(null)} disabled={saving}>
                Hủy
              </button>
              <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
                {saving && <span className="spinner-border spinner-border-sm me-1" />}
                Lưu
              </button>
            </div>
          </form>
        </AdminModal>
      )}
    </>
  );
}

export default MemberPositionsPanel;
