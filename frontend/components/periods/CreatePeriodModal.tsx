"use client";

import React, { useEffect, useState } from "react";
import { AdminModal } from "@/components/admin/AdminModal";
import { useToast } from "@/contexts/ToastContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { EvaluationPeriodDto, evaluationService } from "@/services/evaluationService";
import { CriteriaSetListItem, criteriaService } from "@/services/criteriaService";
import { errorMessage } from "@/components/admin/adminUtils";
import { DateField } from "@/components/evaluations/DateField";

const pad = (n: number) => String(n).padStart(2, "0");

/** Ngày đầu và cuối quý (yyyy-MM-dd). */
const quarterRange = (year: number, quarter: number) => {
  const firstMonth = (quarter - 1) * 3 + 1;
  const lastDay = new Date(year, firstMonth + 2, 0).getDate();
  return { startDate: `${year}-${pad(firstMonth)}-01`, endDate: `${year}-${pad(firstMonth + 2)}-${pad(lastDay)}` };
};

const defaultName = (year: number, quarter: number) =>
  `Đánh giá, xếp loại cán bộ Quý ${["I", "II", "III", "IV"][quarter - 1]}/${year}`;

/** Quý hiện tại, nếu đã có kỳ thì lấy quý kế tiếp chưa có kỳ. */
const firstFreeQuarter = (existing: Pick<EvaluationPeriodDto, "year" | "quarter">[]) => {
  const now = new Date();
  let year = now.getFullYear();
  let quarter = Math.floor(now.getMonth() / 3) + 1;
  for (let i = 0; i < 8 && existing.some((p) => p.year === year && p.quarter === quarter); i++) {
    quarter = quarter === 4 ? 1 : quarter + 1;
    if (quarter === 1) year += 1;
  }
  return { year, quarter };
};

interface Props {
  onClose: () => void;
  onCreated: (period: EvaluationPeriodDto) => void;
  /** Các kỳ đã có: mỗi quý chỉ một kỳ. */
  existing?: EvaluationPeriodDto[];
}

/** Hộp thoại tạo kỳ đánh giá (dự thảo) với bộ tiêu chí đã xuất bản. */
export function CreatePeriodModal({ onClose, onCreated, existing = [] }: Props) {
  const { toast } = useToast();
  const { reload: reloadPeriods } = usePeriod();
  const [saving, setSaving] = useState(false);
  const [criteriaSets, setCriteriaSets] = useState<CriteriaSetListItem[] | null>(null);
  const [form, setForm] = useState(() => {
    const { year, quarter } = firstFreeQuarter(existing);
    return { year, quarter, name: null as string | null, ...quarterRange(year, quarter), criteriaSetId: "" };
  });
  // Đổi năm/quý thì đặt lại ngày theo quý cho khớp.
  const setQuarter = (year: number, quarter: number) =>
    setForm((f) => ({ ...f, year, quarter, ...(year >= 2000 && year <= 2100 ? quarterRange(year, quarter) : {}) }));
  const periodName = form.name ?? defaultName(form.year, form.quarter);
  const duplicate = existing.find((p) => p.year === form.year && p.quarter === form.quarter);
  const dateError = form.endDate && form.startDate && form.endDate < form.startDate ? "Đến ngày phải sau Từ ngày." : null;

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

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const period = await evaluationService.createPeriod({
        ...form,
        name: periodName.trim() || defaultName(form.year, form.quarter),
      });
      toast.success("Đã tạo kỳ đánh giá (dự thảo).");
      reloadPeriods().catch(() => undefined);
      onCreated(period);
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tạo kỳ đánh giá không thành công."));
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
          <button type="submit" form="create-period-form" className="btn btn-primary btn-sm" disabled={saving || !form.criteriaSetId || !!duplicate || !!dateError}>
            {saving ? "Đang tạo..." : "Tạo kỳ"}
          </button>
        </>
      }
    >
      <form id="create-period-form" onSubmit={create} className="row g-3">
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-year">Năm</label>
          <input id="cp-year" type="number" className={`form-control form-control-sm ${duplicate ? "is-invalid" : ""}`} value={form.year} onChange={(e) => setQuarter(Number(e.target.value), form.quarter)} />
        </div>
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-quarter">Quý</label>
          <select id="cp-quarter" className={`form-select form-select-sm ${duplicate ? "is-invalid" : ""}`} value={form.quarter} onChange={(e) => setQuarter(form.year, Number(e.target.value))}>
            {[1, 2, 3, 4].map((q) => <option key={q} value={q}>Quý {q}</option>)}
          </select>
        </div>
        {duplicate && <div className="col-12 mt-1"><div className="invalid-feedback d-block mt-0">Quý này đã có kỳ “{duplicate.name}”.</div></div>}
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-start">Từ ngày</label>
          <DateField id="cp-start" value={form.startDate} onChange={(value) => setForm({ ...form, startDate: value })} />
        </div>
        <div className="col-6 col-md-3">
          <label className="form-label" htmlFor="cp-end">Đến ngày</label>
          <DateField id="cp-end" value={form.endDate} invalid={!!dateError} onChange={(value) => setForm({ ...form, endDate: value })} />
          {dateError && <div className="invalid-feedback d-block">{dateError}</div>}
        </div>
        <div className="col-12">
          <label className="form-label" htmlFor="cp-name">Tên kỳ</label>
          <input id="cp-name" className="form-control form-control-sm" maxLength={200} value={periodName} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </div>
        <div className="col-12">
          <label className="form-label" htmlFor="cp-criteria">Bộ tiêu chí</label>
          {criteriaSets !== null && criteriaSets.length === 0 ? (
            <div className="alert alert-warning py-2 mb-0">Chưa có bộ tiêu chí đã xuất bản. Hãy xuất bản một bộ ở trang Bộ tiêu chí trước.</div>
          ) : criteriaSets !== null && criteriaSets.length === 1 ? (
            <div id="cp-criteria" className="form-control-plaintext py-0">{criteriaSets[0].name}</div>
          ) : (
            <select id="cp-criteria" className="form-select form-select-sm" required value={form.criteriaSetId}
              onChange={(e) => setForm({ ...form, criteriaSetId: e.target.value })}>
              {criteriaSets === null && <option value="">Đang tải...</option>}
              {(criteriaSets ?? []).map((s) => <option key={s.id} value={s.id}>{s.name} ({s.code})</option>)}
            </select>
          )}
        </div>
      </form>
    </AdminModal>
  );
}

export default CreatePeriodModal;
