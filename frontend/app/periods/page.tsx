"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { EvaluationPeriodDto, PeriodPresetDto, evaluationService } from "@/services/evaluationService";

const today = () => new Date().toISOString().substring(0, 10);

/** Kỳ đánh giá: danh sách (mọi người đã đăng nhập) và tạo kỳ từ kiểu kỳ dựng sẵn (period.manage). */
export default function PeriodsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("period.manage");
  const { toast } = useToast();
  const router = useRouter();
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [presets, setPresets] = useState<PeriodPresetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    year: new Date().getFullYear(),
    quarter: Math.floor(new Date().getMonth() / 3) + 1,
    name: "",
    startDate: today(),
    endDate: today(),
    preset: "full",
  });

  useEffect(() => {
    Promise.all([evaluationService.getPeriods(), evaluationService.getPresets()])
      .then(([list, presetList]) => {
        setPeriods(list);
        setPresets(presetList);
      })
      .catch((err: any) => toast.error(err?.message || "Không tải được danh sách kỳ."))
      .finally(() => setLoading(false));
  }, [toast]);

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const period = await evaluationService.createPeriod({
        ...form,
        name: form.name.trim() || `Đánh giá, xếp loại cán bộ Quý ${["I", "II", "III", "IV"][form.quarter - 1]}/${form.year}`,
      });
      toast.success("Đã tạo kỳ đánh giá (dự thảo). Hãy cấu hình và thêm người được đánh giá trước khi mở kỳ.");
      router.push(`/periods/${period.id}`);
    } catch (err: any) {
      toast.error(err?.message || "Không tạo được kỳ đánh giá.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page-wrapper">
      <PageHeader title="Kỳ đánh giá" subTitle="Cấu hình hồ sơ luồng theo nhóm đối tượng, thời hạn, tham số và danh sách người được đánh giá theo từng kỳ." />
      <div className="page-body">
        <div className="row g-3">
          <div className={canManage ? "col-12 col-xl-8" : "col-12"}>
            <section className="card border-0 shadow-sm">
              <div className="card-body p-0">
                {loading ? (
                  <div className="text-secondary p-3"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
                ) : periods.length === 0 ? (
                  <EmptyState title="Chưa có kỳ đánh giá" />
                ) : (
                  <table className="table table-hover table-sm align-middle mb-0">
                    <thead>
                      <tr className="small text-secondary"><th className="ps-3">Kỳ</th><th>Trạng thái</th><th>Mẫu tự chấm</th><th>Hồ sơ luồng</th><th>Người được đánh giá</th><th /></tr>
                    </thead>
                    <tbody>
                      {periods.map((p) => (
                        <tr key={p.id}>
                          <td className="ps-3 small fw-semibold">
                            {p.name}
                            {p.isActive && <span className="badge text-bg-success ms-2">Hiện hành</span>}
                            <div className="text-secondary fw-normal">Quý {p.quarter}/{p.year}</div>
                          </td>
                          <td className="small">{p.statusDisplayName}</td>
                          <td className="small">{p.settings?.selfScoreForm}</td>
                          <td className="small">{p.settings?.profiles?.map((profile) => profile.name).join(", ") || "—"}</td>
                          <td className="small">{p.totalRecords}</td>
                          <td className="text-end pe-3"><Link className="btn btn-outline-primary btn-sm" href={`/periods/${p.id}`}>{canManage ? "Cấu hình" : "Xem"}</Link></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </section>
          </div>

          {canManage && (
            <div className="col-12 col-xl-4">
              <section className="card border-0 shadow-sm">
                <div className="card-body">
                  <h2 className="h6">Tạo kỳ mới</h2>
                  <form onSubmit={create} className="row g-2">
                    <div className="col-6"><label className="form-label small">Năm</label><input type="number" className="form-control form-control-sm" value={form.year} onChange={(e) => setForm({ ...form, year: Number(e.target.value) })} /></div>
                    <div className="col-6"><label className="form-label small">Quý</label>
                      <select className="form-select form-select-sm" value={form.quarter} onChange={(e) => setForm({ ...form, quarter: Number(e.target.value) })}>
                        {[1, 2, 3, 4].map((q) => <option key={q} value={q}>Quý {q}</option>)}
                      </select>
                    </div>
                    <div className="col-12"><label className="form-label small">Tên kỳ</label><input className="form-control form-control-sm" maxLength={200} placeholder="Để trống: tự đặt theo quý/năm" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></div>
                    <div className="col-6"><label className="form-label small">Từ ngày</label><input type="date" className="form-control form-control-sm" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></div>
                    <div className="col-6"><label className="form-label small">Đến ngày</label><input type="date" className="form-control form-control-sm" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} /></div>
                    <div className="col-12">
                      <label className="form-label small">Kiểu kỳ (sinh sẵn các hồ sơ luồng — sửa được sau khi tạo)</label>
                      {presets.map((preset) => (
                        <div className="form-check" key={preset.code}>
                          <input className="form-check-input" type="radio" name="preset" id={`preset-${preset.code}`} checked={form.preset === preset.code} onChange={() => setForm({ ...form, preset: preset.code })} />
                          <label className="form-check-label small" htmlFor={`preset-${preset.code}`}>
                            <strong>{preset.name}</strong>
                            <div className="text-secondary">{preset.description}</div>
                          </label>
                        </div>
                      ))}
                    </div>
                    <div className="col-12"><button type="submit" className="btn btn-primary btn-sm" disabled={saving}>{saving ? "Đang tạo..." : "Tạo kỳ (dự thảo)"}</button></div>
                  </form>
                </div>
              </section>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
