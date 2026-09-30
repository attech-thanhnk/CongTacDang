"use client";

import React, { useEffect, useState } from "react";
import { AdminModal } from "@/components/admin/AdminModal";
import { useToast } from "@/contexts/ToastContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { EvaluationPeriodDto, PeriodPresetDto, evaluationService } from "@/services/evaluationService";
import { CriteriaSetListItem, criteriaService } from "@/services/criteriaService";

const today = () => new Date().toISOString().substring(0, 10);

interface Props {
  presets: PeriodPresetDto[];
  onClose: () => void;
  onCreated: (period: EvaluationPeriodDto) => void;
}

/** Hộp thoại tạo kỳ đánh giá (dự thảo) từ kiểu kỳ dựng sẵn. */
export function CreatePeriodModal({ presets, onClose, onCreated }: Props) {
  const { toast } = useToast();
  const { reload: reloadPeriods } = usePeriod();
  const [saving, setSaving] = useState(false);
  const [criteriaSets, setCriteriaSets] = useState<CriteriaSetListItem[]>([]);
  const [form, setForm] = useState({
    year: new Date().getFullYear(),
    quarter: Math.floor(new Date().getMonth() / 3) + 1,
    name: "",
    startDate: today(),
    endDate: today(),
    preset: "full",
    criteriaSetId: "",
  });

  useEffect(() => {
    criteriaService.list().then((list) => setCriteriaSets(list.filter((s) => s.status === "Published"))).catch(() => setCriteriaSets([]));
  }, []);

  const suggestedForm = presets.find((p) => p.code === form.preset)?.suggestedForm;

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const period = await evaluationService.createPeriod({
        ...form,
        criteriaSetId: form.criteriaSetId || null,
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
          <button type="submit" form="create-period-form" className="btn btn-primary btn-sm" disabled={saving}>
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
          <div className="form-label">Kiểu kỳ</div>
          {presets.map((preset) => (
            <div className="form-check" key={preset.code}>
              <input className="form-check-input" type="radio" name="preset" id={`preset-${preset.code}`} checked={form.preset === preset.code} onChange={() => setForm({ ...form, preset: preset.code })} />
              <label className="form-check-label" htmlFor={`preset-${preset.code}`}>
                <strong>{preset.name}</strong>
                <div className="small text-secondary">{preset.description}</div>
              </label>
            </div>
          ))}
        </div>
        <div className="col-12">
          <label className="form-label" htmlFor="period-criteria">Bộ tiêu chí (đã xuất bản)</label>
          <select id="period-criteria" className="form-select form-select-sm" value={form.criteriaSetId} onChange={(e) => setForm({ ...form, criteriaSetId: e.target.value })}>
            <option value="">Tự chọn bộ mới nhất theo kiểu kỳ{suggestedForm ? ` (Mẫu ${suggestedForm})` : ""}</option>
            {criteriaSets.map((s) => <option key={s.id} value={s.id}>{s.name} — Mẫu {s.selfScoreForm}</option>)}
          </select>
        </div>
      </form>
    </AdminModal>
  );
}

export default CreatePeriodModal;
