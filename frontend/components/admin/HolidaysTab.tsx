"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card, Pagination, usePagination } from "@/components/common";
import { Holiday, holidayService } from "@/services/holidayService";
import { AdminModal } from "./AdminModal";
import { errorMessage, errorTitle } from "./adminUtils";

interface FormState {
  id?: string;
  date: string;
  endDate: string;
  name: string;
}

const WEEKDAYS = ["Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"];

const formatDate = (iso: string) => iso.split("-").reverse().join("/");
const weekday = (iso: string) => WEEKDAYS[new Date(`${iso}T00:00:00Z`).getUTCDay()];

/** Danh mục ngày nghỉ theo năm — dùng khi tính thời hạn các bước theo "ngày làm việc". */
export function HolidaysTab({ canManage }: { canManage: boolean }) {
  const { toast, confirm } = useToast();
  const [year, setYear] = useState(new Date().getFullYear());
  const [items, setItems] = useState<Holiday[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<FormState | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await holidayService.listByYear(year));
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh mục ngày nghỉ."), errorTitle(err));
    } finally {
      setLoading(false);
    }
  }, [year, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    setSaving(true);
    try {
      const payload = { date: form.date, endDate: form.id ? null : form.endDate || null, name: form.name.trim() };
      if (form.id) {
        await holidayService.update(form.id, payload);
        toast.success("Đã cập nhật ngày nghỉ.");
      } else {
        const created = await holidayService.create(payload);
        toast.success(`Đã thêm ${created.length} ngày nghỉ.`);
      }
      setForm(null);
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được ngày nghỉ."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (item: Holiday) =>
    confirm({
      title: "Xóa ngày nghỉ",
      message: `Xóa ngày nghỉ ${formatDate(item.date)} (${item.name})? Thời hạn các bước đã điền không đổi; chỉ ảnh hưởng lần "Áp dụng quy tắc" sau.`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await holidayService.remove(item.id);
          toast.success("Đã xóa ngày nghỉ.");
          await load();
        } catch (err) {
          toast.error(errorMessage(err, "Không xóa được ngày nghỉ."), errorTitle(err), 8000);
        }
      },
    });

  const paging = usePagination(items, { storageKey: "catalog.holidays", resetKeys: [year] });

  return (
    <>
      <Card>
        <div className="card-header d-flex align-items-center gap-2">
          <label className="form-label mb-0" htmlFor="holiday-year">Năm</label>
          <input id="holiday-year" type="number" min={2000} max={2100} className="form-control form-control-sm" style={{ width: 100 }}
            value={year} onChange={(e) => setYear(Number(e.target.value) || year)} />
          <span className="text-secondary small">Chỉ khai báo lễ, Tết, nghỉ bù.</span>
          {canManage && (
            <button type="button" className="btn btn-sm btn-primary ms-auto text-nowrap"
              onClick={() => setForm({ date: `${year}-01-01`, endDate: "", name: "" })}>
              <i className="bi bi-plus-lg me-1" />Thêm ngày nghỉ
            </button>
          )}
        </div>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th className="ps-3" style={{ width: 140 }}>Ngày</th>
                <th style={{ width: 120 }}>Thứ</th>
                <th>Tên ngày nghỉ</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 100 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={4} className="text-center py-4 text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải…</td></tr>
              ) : items.length === 0 ? (
                <tr><td colSpan={4} className="text-center py-4 text-secondary">Chưa có ngày nghỉ nào trong năm {year}.</td></tr>
              ) : (
                paging.pageItems.map((h) => (
                  <tr key={h.id}>
                    <td className="ps-3 fw-semibold">{formatDate(h.date)}</td>
                    <td>{weekday(h.date)}</td>
                    <td>{h.name}</td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <button type="button" className="btn btn-sm btn-link p-1" title="Sửa" onClick={() => setForm({ id: h.id, date: h.date, endDate: "", name: h.name })}>
                          <i className="bi bi-pencil-square" />
                        </button>
                        <button type="button" className="btn btn-sm btn-link p-1 text-danger" title="Xóa" onClick={() => handleDelete(h)}>
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
        <Pagination
          page={paging.page}
          pageSize={paging.pageSize}
          total={paging.total}
          onPageChange={paging.setPage}
          onPageSizeChange={paging.setPageSize}
        />
      </Card>

      {form && (
        <AdminModal title={form.id ? "Sửa ngày nghỉ" : "Thêm ngày nghỉ"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={460}>
          <form onSubmit={handleSave}>
            <div className="mb-2">
              <label className="form-label">Tên ngày nghỉ <span className="text-danger">*</span></label>
              <input className="form-control form-control-sm" value={form.name} maxLength={200} required placeholder="VD: Quốc khánh, Tết Nguyên đán"
                onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div className="row g-2 mb-2">
              <div className="col-6">
                <label className="form-label">{form.id ? "Ngày" : "Từ ngày"} <span className="text-danger">*</span></label>
                <input type="date" className="form-control form-control-sm" value={form.date} required onChange={(e) => setForm({ ...form, date: e.target.value })} />
              </div>
              {!form.id && (
                <div className="col-6">
                  <label className="form-label">Đến ngày (nghỉ nhiều ngày)</label>
                  <input type="date" className="form-control form-control-sm" value={form.endDate} min={form.date} onChange={(e) => setForm({ ...form, endDate: e.target.value })} />
                </div>
              )}
            </div>
            <div className="d-flex justify-content-end gap-2 mt-3">
              <button type="button" className="btn btn-sm btn-outline-secondary" disabled={saving} onClick={() => setForm(null)}>Hủy</button>
              <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>{saving ? "Đang lưu…" : "Lưu"}</button>
            </div>
          </form>
        </AdminModal>
      )}
    </>
  );
}

export default HolidaysTab;
