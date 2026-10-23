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
import { FormLayout, formLayoutService, MemberFormLayout } from "@/services/formLayoutService";
import { AdminModal } from "./AdminModal";
import { useFormDirty } from "./useFormDirty";
import { RowActions } from "@/components/common/RowActions";
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
  isActing: boolean;
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
  const [memberLayout, setMemberLayout] = useState<MemberFormLayout | null>(null);
  const [layouts, setLayouts] = useState<FormLayout[]>([]);
  const [layoutForm, setLayoutForm] = useState<{ value: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const formDirty = useFormDirty(form);
  const layoutDirty = useFormDirty(layoutForm);
  const overrideDirty = useFormDirty(overrideForm);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, info, layout] = await Promise.all([
        userService.listPositions(userId),
        userService.getApprovalAuthority(userId),
        formLayoutService.getForMember(userId),
      ]);
      setItems(list);
      setAuthority(info);
      setMemberLayout(layout);
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
    formLayoutService
      .list()
      .then(setLayouts)
      .catch((err: unknown) => toast.error(errorMessage(err, "Không tải được danh mục kiểu trình bày."), errorTitle(err)));
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
    setForm({ positionId: "", unitId: "", isPrimary: current.every((c) => !c.isPrimary), isActing: false, validFrom: "", validTo: "", note: "" });

  const openEdit = (item: MemberPosition) =>
    setForm({
      id: item.id,
      positionId: item.positionId,
      unitId: item.departmentId ? `d:${item.departmentId}` : item.partyCellId ? `c:${item.partyCellId}` : "",
      isPrimary: item.isPrimary,
      isActing: item.isActing,
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
      isActing: form.isActing,
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
      message: `Kết thúc ngay chức vụ "${item.displayName}"?`,
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
      message: `Xóa bản ghi chức vụ "${item.displayName}"? Chỉ dùng khi nhập nhầm; bình thường hãy dùng "Kết thúc" để giữ lịch sử.`,
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
      toast.success(overrideForm.value ? "Đã đổi cấp quyết định xếp loại." : "Cấp quyết định xếp loại theo chức vụ.");
      setOverrideForm(null);
      onChanged?.();
    } catch (err) {
      toast.error(errorMessage(err, "Không cập nhật được thẩm quyền."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  const handleLayout = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!layoutForm) return;
    setSaving(true);
    try {
      setMemberLayout(await formLayoutService.setForMember(userId, layoutForm.value || null));
      toast.success(layoutForm.value ? "Đã đặt riêng kiểu trình bày." : "Đã bỏ đặt riêng — kiểu trình bày theo chức vụ.");
      setLayoutForm(null);
    } catch (err) {
      toast.error(errorMessage(err, "Không cập nhật được kiểu trình bày."), errorTitle(err), 10000);
    } finally {
      setSaving(false);
    }
  };

  const unitName = (i: MemberPosition) => i.departmentName || i.partyCellName || "—";

  const renderTable = (rows: MemberPosition[], active: boolean) => (
    <div className="table-responsive">
      <table className="table table-hover align-middle mb-0">
        <thead>
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
                Chưa có chức vụ nào.
              </td>
            </tr>
          ) : (
            rows.map((i) => (
              <tr key={i.id}>
                <td className="ps-3">
                  <span className="fw-semibold text-dark">{i.displayName}</span>
                  {i.isPrimary && active && <span className="tag t-info ms-2">Chính</span>}
                  {!i.isPrimary && active && <span className="status-badge tone-neutral ms-2">Kiêm nhiệm</span>}
                  {i.defaultApprovalAuthority === "CapTren" && <span className="tag t-wait ms-1">Cấp trên QĐ</span>}
                  <div className="small text-secondary">{POSITION_SIDE_LABELS[i.side as PositionSide] ?? i.side}{i.note ? ` · ${i.note}` : ""}</div>
                </td>
                <td>{unitName(i)}</td>
                <td>{i.statCode ? <code>{i.statCode}</code> : "—"}</td>
                <td>{formatDate(i.validFrom)}</td>
                <td>{i.validTo ? formatDate(exclusiveIsoToLastDayInput(i.validTo)) : "Không thời hạn"}</td>
                {canManage && (
                  <td className="pe-3 text-end text-nowrap">
                    <RowActions
                      itemLabel={i.displayName}
                      iconOnly
                      primary={[{ key: "edit", label: "Sửa", icon: "bi-pencil", hidden: !active, onClick: () => openEdit(i) }]}
                      menu={[
                        { key: "end", label: "Kết thúc", icon: "bi-stop-circle", danger: true, hidden: !active, onClick: () => handleEnd(i) },
                        { key: "delete", label: "Xóa (nhập nhầm)", icon: "bi-trash", danger: true, onClick: () => handleDelete(i) },
                      ]}
                    />
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
    <div className="d-flex flex-column gap-4">
      <Card>
        <div className="card-body d-flex flex-wrap align-items-center gap-4">
          <div>
            <div className="small text-secondary">Cấp quyết định xếp loại</div>
            <div className="fw-semibold">{authority ? APPROVAL_AUTHORITY_CODE_LABELS[authority.effective] : "—"}</div>
            {authority && (
              <div className="small text-secondary">
                {authority.override
                  ? [
                      "Đặt riêng",
                      authority.overrideReason && `lý do: ${authority.overrideReason}`,
                      authority.derived !== authority.effective && `theo chức vụ sẽ là: ${APPROVAL_AUTHORITY_CODE_LABELS[authority.derived]}`,
                    ]
                      .filter(Boolean)
                      .join(" · ")
                  : authority.capTrenPositions.length > 0
                    ? `Theo chức vụ ${authority.capTrenPositions.join(", ")}`
                    : "Theo chức vụ đang giữ"}
              </div>
            )}
          </div>
          {authority?.statCode && (
            <div>
              <div className="small text-secondary">Mã chức danh</div>
              <div className="fw-semibold">{authority.statCode}</div>
            </div>
          )}
          {canManage && authority && (
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary ms-auto"
              onClick={() => setOverrideForm({ value: authority.override ?? "", reason: authority.overrideReason ?? "" })}
            >
              <i className="bi bi-pencil me-1" />
              Thay đổi
            </button>
          )}
        </div>
        <div className="card-body border-top d-flex flex-wrap align-items-center gap-4">
          <div>
            <div className="small text-secondary">Kiểu trình bày phiếu tự đánh giá</div>
            <div className="fw-semibold">{memberLayout?.effectiveName ?? "Như biểu mẫu gốc"}</div>
            {memberLayout && (
              <div className="small text-secondary">
                {memberLayout.overrideId
                  ? memberLayout.derivedId !== memberLayout.effectiveId
                    ? `Đặt riêng · theo chức vụ sẽ là: ${memberLayout.derivedName ?? "Như biểu mẫu gốc"}`
                    : "Đặt riêng"
                  : "Theo chức vụ đang giữ"}
              </div>
            )}
          </div>
          {canManage && memberLayout && (
            <button type="button" className="btn btn-sm btn-outline-secondary ms-auto" onClick={() => setLayoutForm({ value: memberLayout.overrideId ?? "" })}>
              <i className="bi bi-pencil me-1" />
              Thay đổi
            </button>
          )}
        </div>
      </Card>

      <Card>
        <div className="card-header d-flex justify-content-between align-items-center gap-2">
          <h2 className="mb-0">Chức vụ đang giữ</h2>
          {canManage && (
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={openCreate}>
              <i className="bi bi-plus-lg me-1" />
              Thêm chức vụ
            </button>
          )}
        </div>
        {loading && items.length === 0 ? (
          <div className="text-center py-4 text-secondary small loading-delay">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải…
          </div>
        ) : (
          renderTable(current, true)
        )}
      </Card>
      {history.length > 0 && (
        <Card>
          <div className="card-header">
            <h2 className="mb-0">Đã kết thúc</h2>
          </div>
          {renderTable(history, false)}
        </Card>
      )}

      {form && (
        <AdminModal title={form.id ? "Sửa chức vụ" : "Thêm chức vụ"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={560}
          footer={
            <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => setForm(null)} disabled={saving}>
              Hủy
            </button>
            <button type="submit" form="member-position-form" className="btn btn-primary" disabled={saving || !formDirty}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Lưu
            </button>
            </>
          }
        >
          <form id="member-position-form" onSubmit={handleSave}>
            <div className="mb-2">
              <label className="form-label">
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
              <label className="form-label">Nơi giữ chức vụ</label>
              <select className="form-select form-select-sm" value={form.unitId} onChange={(e) => setForm({ ...form, unitId: e.target.value })}>
                <option value="">— Không ghi đơn vị —</option>
                {unitOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                    {o.active ? "" : " (ngừng hoạt động)"}
                  </option>
                ))}
              </select>
            </div>
            <div className="row g-2 mb-2">
              <div className="col-6">
                <label className="form-label">Từ ngày</label>
                <input type="date" className="form-control form-control-sm" value={form.validFrom} onChange={(e) => setForm({ ...form, validFrom: e.target.value })} />
                <div className="form-text">{form.id ? "Để trống = giữ nguyên." : "Để trống = hôm nay."}</div>
              </div>
              <div className="col-6">
                <label className="form-label">Đến hết ngày</label>
                <input type="date" className="form-control form-control-sm" value={form.validTo} onChange={(e) => setForm({ ...form, validTo: e.target.value })} />
                <div className="form-text">Để trống = không thời hạn.</div>
              </div>
            </div>
            <div className="mb-2">
              <div className="form-check form-check-inline">
                <input
                  id="member-position-primary"
                  type="checkbox"
                  className="form-check-input"
                  checked={form.isPrimary}
                  onChange={(e) => setForm({ ...form, isPrimary: e.target.checked })}
                />
                <label className="form-check-label small" htmlFor="member-position-primary">
                  Chức vụ chính (bỏ đánh dấu = kiêm nhiệm)
                </label>
              </div>
              <div className="form-check form-check-inline">
                <input
                  id="member-position-acting"
                  type="checkbox"
                  className="form-check-input"
                  checked={form.isActing}
                  onChange={(e) => setForm({ ...form, isActing: e.target.checked })}
                />
                <label className="form-check-label small" htmlFor="member-position-acting">
                  Quyền
                </label>
              </div>
            </div>
            <div className="mb-2">
              <label className="form-label">Ghi chú</label>
              <input
                className="form-control form-control-sm"
                maxLength={1000}
                value={form.note}
                placeholder="VD: Quyết định bổ nhiệm số …"
                onChange={(e) => setForm({ ...form, note: e.target.value })}
              />
            </div>
          </form>
        </AdminModal>
      )}

      {layoutForm && (
        <AdminModal title="Kiểu trình bày biểu mẫu" onClose={() => setLayoutForm(null)} closeDisabled={saving} maxWidth={480}
          footer={
            <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => setLayoutForm(null)} disabled={saving}>
              Hủy
            </button>
            <button type="submit" form="member-layout-form" className="btn btn-primary" disabled={saving || !layoutDirty}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Lưu
            </button>
            </>
          }
        >
          <form id="member-layout-form" onSubmit={handleLayout}>
            <label className="form-label">Kiểu trình bày Mẫu 09C, 9D</label>
            <select className="form-select form-select-sm" value={layoutForm.value} onChange={(e) => setLayoutForm({ value: e.target.value })}>
              <option value="">Theo chức vụ ({memberLayout?.derivedName ?? "Như biểu mẫu gốc"})</option>
              {layouts.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
            </select>
          </form>
        </AdminModal>
      )}

      {overrideForm && (
        <AdminModal title="Cấp quyết định xếp loại" onClose={() => setOverrideForm(null)} closeDisabled={saving} maxWidth={480}
          footer={
            <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => setOverrideForm(null)} disabled={saving}>
              Hủy
            </button>
            <button type="submit" form="member-authority-form" className="btn btn-primary" disabled={saving || !overrideDirty}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Lưu
            </button>
            </>
          }
        >
          <form id="member-authority-form" onSubmit={handleOverride}>
            <div className="mb-2">
              <label className="form-label">Cấp quyết định xếp loại</label>
              <select
                className="form-select form-select-sm"
                value={overrideForm.value}
                onChange={(e) => setOverrideForm({ ...overrideForm, value: e.target.value as "" | ApprovalAuthorityCode })}
              >
                <option value="">Theo chức vụ{authority ? ` (${APPROVAL_AUTHORITY_CODE_LABELS[authority.derived]})` : ""}</option>
                {(Object.keys(APPROVAL_AUTHORITY_CODE_LABELS) as ApprovalAuthorityCode[]).map((a) => (
                  <option key={a} value={a}>
                    {APPROVAL_AUTHORITY_CODE_LABELS[a]}
                  </option>
                ))}
              </select>
            </div>
            {overrideForm.value && (
              <div className="mb-2">
                <label className="form-label">
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
            <div className="small text-secondary">Chỉ áp dụng cho hồ sơ đánh giá tạo sau.</div>
          </form>
        </AdminModal>
      )}
    </div>
  );
}

export default MemberPositionsPanel;
