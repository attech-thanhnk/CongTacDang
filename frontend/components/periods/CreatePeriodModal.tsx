"use client";

import React, { useEffect, useState } from "react";
import { AdminModal } from "@/components/admin/AdminModal";
import { useToast } from "@/contexts/ToastContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { EvaluationPeriodDto, evaluationService } from "@/services/evaluationService";
import { CriteriaSetListItem, criteriaService } from "@/services/criteriaService";

const today = () => new Date().toISOString().substring(0, 10);

/** Bước đăng ký, duyệt sản phẩm theo mẫu tự chấm của bộ tiêu chí (máy chủ đặt sẵn khi tạo kỳ). */
const FORM_FLOW: Record<string, string> = {
  "09A": "Có bước đăng ký, duyệt sản phẩm (Mẫu 01); tự chấm theo sản phẩm.",
  "09B": "Không có bước đăng ký sản phẩm; tự chấm theo trục.",
};

interface Props {
  onClose: () => void;
  onCreated: (period: EvaluationPeriodDto) => void;
}

/** Hộp thoại tạo kỳ đánh giá (dự thảo) với bộ tiêu chí đã xuất bản. */
export function CreatePeriodModal({ onClose, onCreated }: Props) {
  const { toast } = useToast();
  const { reload: reloadPeriods } = usePeriod();
  const [saving, setSaving] = useState(false);
  const [criteriaSets, setCriteriaSets] = useState<CriteriaSetListItem[] | null>(null);
  const [form, setForm] = useState({
    year: new Date().getFullYear(),
    quarter: Math.floor(new Date().getMonth() / 3) + 1,
    name: "",
    startDate: today(),
    endDate: today(),
    criteriaSetId: "",
  });

  useEffect(() => {
    criteriaService.list()
      .then((list) => {
        const published = list
          .filter((s) => s.status === "Published")
          .sort((a, b) => (b.publishedAt ?? "").localeCompare(a.publishedAt ?? ""));
        setCriteriaSets(published);
        setForm((f) => (f.criteriaSetId ? f : { ...f, criteriaSetId: published[0]?.id ?? "" }));
      })
      .catch(() => setCriteriaSets([]));
  }, []);

  const selectedForm = criteriaSets?.find((s) => s.id === form.criteriaSetId)?.selfScoreForm;

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const period = await evaluationService.createPeriod({
        ...form,
        name: form.name.trim() || `Đánh giá, xếp loại cán bộ Quý ${["I", "II", "III", "IV"][form.quarter - 1]}/${form.year}`,
      });
      toast.success("Đã tạo kỳ đánh giá (dự thảo).");
      reloadPeriods().catch(() => undefined);
      onCreated(period);
    } catch (err: any) {
      toast.error(err?.message || "Không tạo được kỳ đánh giá.");
      setSaving(false);
    }
  };

  return (
    <AdminModal
      title="Tạo kỳ đánh giá"
      onClose={onClose}
      closeDisabled={saving}
      maxWidth={640}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onClose} disabled={saving}>Hủy</button>
          <button type="submit" form="create-period-form" className="btn btn-primary btn-sm" disabled={saving || !form.criteriaSetId}>
            {saving ? "Đang tạo..." : "Tạo kỳ (dự thảo)"}
          </button>
        </>
      }
    >
      <form id="create-period-form" onSubmit={create} className="row g-3">
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-year">Năm</label>
          <input id="cp-year" type="number" className="form-control form-control-sm" value={form.year} onChange={(e) => setForm({ ...form, year: Number(e.target.value) })} />
        </div>
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-quarter">Quý</label>
          <select id="cp-quarter" className="form-select form-select-sm" value={form.quarter} onChange={(e) => setForm({ ...form, quarter: Number(e.target.value) })}>
            {[1, 2, 3, 4].map((q) => <option key={q} value={q}>Quý {q}</option>)}
          </select>
        </div>
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-start">Từ ngày</label>
          <input id="cp-start" type="date" className="form-control form-control-sm" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
        </div>
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-end">Đến ngày</label>
          <input id="cp-end" type="date" className="form-control form-control-sm" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} />
        </div>
        <div className="col-12">
          <label className="form-label" htmlFor="cp-name">Tên kỳ</label>
          <input id="cp-name" className="form-control form-control-sm" maxLength={200} placeholder="Để trống: tự đặt theo quý/năm" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </div>
        <div className="col-12">
          <label className="form-label" htmlFor="cp-criteria">Bộ tiêu chí</label>
          {criteriaSets !== null && criteriaSets.length === 0 ? (
            <div className="alert alert-warning py-2 mb-0">Chưa có bộ tiêu chí đã xuất bản. Hãy xuất bản một bộ ở trang Bộ tiêu chí trước.</div>
          ) : (
            <>
              <select id="cp-criteria" className="form-select form-select-sm" required value={form.criteriaSetId}
                onChange={(e) => setForm({ ...form, criteriaSetId: e.target.value })}>
                {criteriaSets === null && <option value="">Đang tải...</option>}
                {(criteriaSets ?? []).map((s) => <option key={s.id} value={s.id}>{s.name} — Mẫu {s.selfScoreForm}</option>)}
              </select>
              {selectedForm && FORM_FLOW[selectedForm] && <div className="form-text">{FORM_FLOW[selectedForm]}</div>}
            </>
          )}
        </div>
      </form>
    </AdminModal>
  );
}

export default CreatePeriodModal;
