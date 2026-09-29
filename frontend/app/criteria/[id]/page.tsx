"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { CriteriaSummary } from "@/components/evaluations/CriteriaSummary";
import {
  CriteriaGroup,
  CriteriaSetContent,
  CriteriaSetDto,
  GRADE_LABELS,
  ROUNDING_LABELS,
  RoundingRule,
  ScoreRoundingMode,
  criteriaService,
  fmt,
  quickChecks,
} from "@/services/criteriaService";

type Rounding = CriteriaSetContent["parameters"]["rounding"];

/** Xem / sửa bản nháp bộ tiêu chí (bảng sửa trực tiếp, kiểm tra tổng điểm tức thì), xuất bản, nhân bản, lưu trữ. */
export default function CriteriaSetPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const router = useRouter();
  const { hasPermission } = useAuth();
  const canManage = hasPermission("criteria.manage");
  const { toast, confirm } = useToast();
  const [set, setSet] = useState<CriteriaSetDto | null>(null);
  const [content, setContent] = useState<CriteriaSetContent | null>(null);
  const [info, setInfo] = useState({ code: "", name: "", selfScoreForm: "09A", notes: "" });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    try {
      const loaded = await criteriaService.get(id);
      setSet(loaded);
      setContent(JSON.parse(JSON.stringify(loaded.content)));
      setInfo({ code: loaded.code, name: loaded.name, selfScoreForm: loaded.selfScoreForm, notes: loaded.notes || "" });
    } catch (err: any) {
      setError(err?.message || "Không tải được bộ tiêu chí.");
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const checks = useMemo(() => (content ? quickChecks(content, info.selfScoreForm) : []), [content, info.selfScoreForm]);

  if (error) return <div className="page-wrapper"><div className="alert alert-danger m-4">{error}</div></div>;
  if (!set || !content) return <div className="page-wrapper"><div className="text-secondary p-4"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div></div>;

  const editable = canManage && set.status === "Draft";
  const p = content.parameters;

  const run = async (work: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(success);
      await load();
    } catch (err: any) {
      toast.error(err?.message || "Không thực hiện được thao tác.");
      if (err?.status === 409) await load();
    } finally {
      setBusy(false);
    }
  };

  const save = () =>
    run(() => criteriaService.update(set.id, { version: set.version, ...info, content }), "Đã lưu bản nháp.");

  const publish = () =>
    confirm({
      title: "Xuất bản bộ tiêu chí",
      message: "Sau khi xuất bản, bộ không sửa được nữa (chỉ nhân bản thành bản nháp mới); kỳ dự thảo chọn được bộ này. Hãy lưu thay đổi trước khi xuất bản.",
      confirmText: "Xuất bản",
      onConfirm: () => run(() => criteriaService.publish(set.id, set.version), "Đã xuất bản bộ tiêu chí."),
    });

  const clone = () =>
    run(async () => {
      const created = await criteriaService.clone(set.id);
      router.push(`/criteria/${created.id}`);
    }, "Đã nhân bản thành bản nháp mới.");

  const update = (change: Partial<CriteriaSetContent>) => setContent({ ...content, ...change });
  const setParam = <K extends keyof CriteriaSetContent["parameters"]>(key: K, value: CriteriaSetContent["parameters"][K]) =>
    update({ parameters: { ...p, [key]: value } });
  const setGroup = (index: number, change: Partial<CriteriaGroup>) =>
    update({ generalGroups: content.generalGroups.map((g, i) => (i === index ? { ...g, ...change } : g)) });
  const num = (value: string) => (value === "" ? 0 : Number(value));

  const generalTotal = content.generalGroups.reduce((s, g) => s + g.items.reduce((t, i) => t + (Number(i.maxScore) || 0), 0), 0);
  const axisTotal = content.axes.reduce((s, a) => s + (Number(a.maxScore) || 0), 0);

  return (
    <div className="page-wrapper">
      <PageHeader
        title={set.name}
        subTitle={`${set.code} · Mẫu ${set.selfScoreForm} · ${set.statusDisplayName}${set.usedByPeriods.length ? ` · Kỳ đang dùng: ${set.usedByPeriods.join(", ")}` : ""}`}
        actions={
          <div className="d-flex gap-2 flex-wrap">
            <Link href="/criteria" className="btn btn-outline-secondary btn-sm"><i className="bi bi-arrow-left me-1" />Danh sách</Link>
            {editable && <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={save}>Lưu bản nháp</button>}
            {editable && <button type="button" className="btn btn-success btn-sm" disabled={busy} onClick={publish}>Xuất bản</button>}
            {canManage && <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy} onClick={clone}>Nhân bản</button>}
            {canManage && set.status === "Published" && (
              <button type="button" className="btn btn-outline-warning btn-sm" disabled={busy}
                onClick={() => confirm({ title: "Lưu trữ", message: "Kỳ đã dùng bộ này giữ nguyên ảnh chụp; bộ không chọn được cho kỳ mới.", confirmText: "Lưu trữ", onConfirm: () => run(() => criteriaService.archive(set.id, set.version), "Đã lưu trữ.") })}>
                Lưu trữ
              </button>
            )}
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {set.notes && !editable && <div className="alert alert-light border small mb-0">{set.notes}</div>}
        {set.validationErrors.length > 0 && (
          <div className="alert alert-warning small mb-0">
            <div className="fw-semibold mb-1">Chưa xuất bản được — lỗi kiểm tra (theo lần lưu gần nhất):</div>
            <ul className="mb-0">{set.validationErrors.map((e) => <li key={e}>{e}</li>)}</ul>
          </div>
        )}

        {!editable ? (
          <section className="card border-0 shadow-sm"><div className="card-body"><CriteriaSummary content={content} form={set.selfScoreForm} /></div></section>
        ) : (
          <>
            {checks.length > 0 && (
              <div className="alert alert-danger small mb-0"><ul className="mb-0">{checks.map((c) => <li key={c}>{c}</li>)}</ul></div>
            )}

            <section className="card border-0 shadow-sm">
              <div className="card-body row g-2">
                <div className="col-md-3"><label className="form-label small" htmlFor="cs-code">Mã bộ</label><input id="cs-code" className="form-control form-control-sm" maxLength={50} value={info.code} onChange={(e) => setInfo({ ...info, code: e.target.value })} /></div>
                <div className="col-md-5"><label className="form-label small" htmlFor="cs-name">Tên bộ</label><input id="cs-name" className="form-control form-control-sm" maxLength={200} value={info.name} onChange={(e) => setInfo({ ...info, name: e.target.value })} /></div>
                <div className="col-md-4"><label className="form-label small" htmlFor="cs-form">Mẫu tự chấm</label>
                  <select id="cs-form" className="form-select form-select-sm" value={info.selfScoreForm} onChange={(e) => setInfo({ ...info, selfScoreForm: e.target.value })}>
                    <option value="09A">09A — chấm theo nhiệm vụ (Mẫu 01/02, A-B-C-D)</option>
                    <option value="09B">09B — chấm trực tiếp theo trục</option>
                  </select>
                </div>
                <div className="col-12"><label className="form-label small" htmlFor="cs-notes">Ghi chú (căn cứ văn bản)</label><textarea id="cs-notes" className="form-control form-control-sm" rows={2} maxLength={4000} value={info.notes} onChange={(e) => setInfo({ ...info, notes: e.target.value })} /></div>
              </div>
            </section>

            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <h2 className="h6 mb-0">Nhóm tiêu chí chung — tổng <span className={Math.abs(generalTotal - p.generalMaxScore) > 1e-6 ? "text-danger" : "text-success"}>{fmt(generalTotal)}</span> / {fmt(p.generalMaxScore)}</h2>
                  <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ generalGroups: [...content.generalGroups, { code: String(content.generalGroups.length + 1), name: "", scoringMode: "Binary", items: [] }] })}>Thêm nhóm</button>
                </div>
                {content.generalGroups.map((g, gi) => (
                  <div className="border rounded-3 p-2 mb-2" key={gi}>
                    <div className="row g-2 align-items-end mb-2">
                      <div className="col-md-1"><label className="form-label small mb-0">Mã</label><input className="form-control form-control-sm" value={g.code} onChange={(e) => setGroup(gi, { code: e.target.value })} /></div>
                      <div className="col-md-6"><label className="form-label small mb-0">Tên nhóm</label><input className="form-control form-control-sm" value={g.name} onChange={(e) => setGroup(gi, { name: e.target.value })} /></div>
                      <div className="col-md-3"><label className="form-label small mb-0">Cách chấm</label>
                        <select className="form-select form-select-sm" value={g.scoringMode} onChange={(e) => setGroup(gi, { scoringMode: e.target.value as CriteriaGroup["scoringMode"] })}>
                          <option value="Binary">Đảm bảo / Không đảm bảo</option>
                          <option value="Range">Nhập điểm 0..tối đa</option>
                        </select>
                      </div>
                      <div className="col-md-2 small text-secondary">Cộng nhóm: <strong>{fmt(g.items.reduce((s, i) => s + (Number(i.maxScore) || 0), 0))}</strong>
                        <button type="button" className="btn btn-link btn-sm text-danger p-0 ms-2" onClick={() => update({ generalGroups: content.generalGroups.filter((_, i) => i !== gi) })}>Xóa nhóm</button>
                      </div>
                    </div>
                    <table className="table table-sm mb-1">
                      <thead><tr className="small text-secondary"><th style={{ width: 80 }}>Mã</th><th>Nội dung tiêu chí con</th><th style={{ width: 90 }}>Tối đa</th><th style={{ width: 30 }} /></tr></thead>
                      <tbody>
                        {g.items.map((item, ii) => {
                          const setItem = (change: Partial<typeof item>) => setGroup(gi, { items: g.items.map((x, i) => (i === ii ? { ...x, ...change } : x)) });
                          return (
                            <tr key={ii}>
                              <td><input className="form-control form-control-sm" value={item.code} onChange={(e) => setItem({ code: e.target.value })} /></td>
                              <td><textarea className="form-control form-control-sm" rows={1} value={item.text} onChange={(e) => setItem({ text: e.target.value })} /></td>
                              <td><input type="number" min={0} step={0.5} className="form-control form-control-sm" value={item.maxScore} onChange={(e) => setItem({ maxScore: num(e.target.value) })} /></td>
                              <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa tiêu chí" onClick={() => setGroup(gi, { items: g.items.filter((_, i) => i !== ii) })}><i className="bi bi-x-lg" /></button></td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                    <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setGroup(gi, { items: [...g.items, { code: `${g.code}.${g.items.length + 1}`, text: "", maxScore: 1 }] })}>Thêm tiêu chí con</button>
                  </div>
                ))}
              </div>
            </section>

            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <h2 className="h6 mb-0">Trục kết quả{info.selfScoreForm === "09B" && <> — tổng <span className={Math.abs(axisTotal - p.totalTaskWeight) > 1e-6 ? "text-danger" : "text-success"}>{fmt(axisTotal)}</span> / {fmt(p.totalTaskWeight)}</>}</h2>
                  <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ axes: [...content.axes, { code: `T${content.axes.length + 1}`, name: "", description: "", maxScore: 0 }] })}>Thêm trục</button>
                </div>
                <table className="table table-sm mb-0">
                  <thead><tr className="small text-secondary"><th style={{ width: 80 }}>Mã</th><th>Tên trục</th><th>Nội dung áp dụng</th><th style={{ width: 100 }}>Tối đa (09B)</th><th style={{ width: 30 }} /></tr></thead>
                  <tbody>
                    {content.axes.map((a, ai) => {
                      const setAxis = (change: Partial<typeof a>) => update({ axes: content.axes.map((x, i) => (i === ai ? { ...x, ...change } : x)) });
                      return (
                        <tr key={ai}>
                          <td><input className="form-control form-control-sm" value={a.code} onChange={(e) => setAxis({ code: e.target.value })} /></td>
                          <td><input className="form-control form-control-sm" value={a.name} onChange={(e) => setAxis({ name: e.target.value })} /></td>
                          <td><textarea className="form-control form-control-sm" rows={1} value={a.description || ""} onChange={(e) => setAxis({ description: e.target.value })} /></td>
                          <td><input type="number" min={0} step={0.5} className="form-control form-control-sm" value={a.maxScore} onChange={(e) => setAxis({ maxScore: num(e.target.value) })} /></td>
                          <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa trục" onClick={() => update({ axes: content.axes.filter((_, i) => i !== ai) })}><i className="bi bi-x-lg" /></button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <h2 className="h6 mb-0">Khung tỷ trọng A-B-C-D (%, tổng 100)</h2>
                  <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ weightFrames: [...content.weightFrames, { code: `K${content.weightFrames.length + 1}`, name: "", a: 0.25, b: 0.25, c: 0.25, d: 0.25 }] })}>Thêm khung</button>
                </div>
                <table className="table table-sm mb-2">
                  <thead><tr className="small text-secondary"><th style={{ width: 80 }}>Mã</th><th>Tên khung</th><th style={{ width: 80 }}>A</th><th style={{ width: 80 }}>B</th><th style={{ width: 80 }}>C</th><th style={{ width: 80 }}>D</th><th style={{ width: 70 }}>Tổng</th><th style={{ width: 30 }} /></tr></thead>
                  <tbody>
                    {content.weightFrames.map((f, fi) => {
                      const setFrame = (change: Partial<typeof f>) => update({ weightFrames: content.weightFrames.map((x, i) => (i === fi ? { ...x, ...change } : x)) });
                      const sum = (f.a + f.b + f.c + f.d) * 100;
                      return (
                        <tr key={fi}>
                          <td><input className="form-control form-control-sm" value={f.code} onChange={(e) => setFrame({ code: e.target.value })} /></td>
                          <td><input className="form-control form-control-sm" value={f.name} onChange={(e) => setFrame({ name: e.target.value })} /></td>
                          {(["a", "b", "c", "d"] as const).map((k) => (
                            <td key={k}><input type="number" min={0} max={100} step={1} className="form-control form-control-sm" value={Math.round(f[k] * 10000) / 100}
                              onChange={(e) => setFrame({ [k]: num(e.target.value) / 100 } as Partial<typeof f>)} aria-label={`Tỷ trọng ${k.toUpperCase()} khung ${f.code}`} /></td>
                          ))}
                          <td className={`small ${Math.abs(sum - 100) > 1e-4 ? "text-danger" : "text-success"}`}>{fmt(sum)}%</td>
                          <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa khung" onClick={() => update({ weightFrames: content.weightFrames.filter((_, i) => i !== fi) })}><i className="bi bi-x-lg" /></button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                <div className="row g-2">
                  <div className="col-md-4"><label className="form-label small mb-0">Khung mặc định (cán bộ chưa có khung)</label>
                    <select className="form-select form-select-sm" value={p.defaultWeightFrameCode || ""} onChange={(e) => setParam("defaultWeightFrameCode", e.target.value || null)}>
                      <option value="">— Không (kẹt luồng báo lỗi) —</option>
                      {content.weightFrames.map((f) => <option key={f.code} value={f.code}>{f.code} — {f.name}</option>)}
                    </select>
                  </div>
                </div>
              </div>
            </section>

            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <h2 className="h6 mb-0">Thang quy đổi % A-B-C-D (mức áp dụng cho giá trị ≥ ngưỡng dưới)</h2>
                  <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ conversionScale: [...content.conversionScale, { minPercent: 0, label: "", description: "" }] })}>Thêm mức</button>
                </div>
                <table className="table table-sm mb-0">
                  <thead><tr className="small text-secondary"><th style={{ width: 110 }}>Từ (%)</th><th style={{ width: 180 }}>Nhãn</th><th>Cách hiểu</th><th style={{ width: 30 }} /></tr></thead>
                  <tbody>
                    {content.conversionScale.map((b, bi) => {
                      const setBand = (change: Partial<typeof b>) => update({ conversionScale: content.conversionScale.map((x, i) => (i === bi ? { ...x, ...change } : x)) });
                      return (
                        <tr key={bi}>
                          <td><input type="number" min={0} max={100} className="form-control form-control-sm" value={b.minPercent} onChange={(e) => setBand({ minPercent: num(e.target.value) })} /></td>
                          <td><input className="form-control form-control-sm" value={b.label} onChange={(e) => setBand({ label: e.target.value })} /></td>
                          <td><textarea className="form-control form-control-sm" rows={1} value={b.description || ""} onChange={(e) => setBand({ description: e.target.value })} /></td>
                          <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa mức" onClick={() => update({ conversionScale: content.conversionScale.filter((_, i) => i !== bi) })}><i className="bi bi-x-lg" /></button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <h2 className="h6">Mức xếp loại (4 mức cố định — cấu hình ngưỡng và điều kiện)</h2>
                <table className="table table-sm mb-0">
                  <thead><tr className="small text-secondary"><th style={{ width: 230 }}>Mức</th><th style={{ width: 100 }}>Từ điểm</th><th style={{ width: 130 }}>% vượt chuẩn tối thiểu</th><th>Điều kiện kèm theo (hiển thị)</th></tr></thead>
                  <tbody>
                    {content.grades.map((g, gi) => {
                      const setGrade = (change: Partial<typeof g>) => update({ grades: content.grades.map((x, i) => (i === gi ? { ...x, ...change } : x)) });
                      return (
                        <tr key={g.grade}>
                          <td className="small">{GRADE_LABELS[g.grade] ?? g.grade}</td>
                          <td><input type="number" min={0} max={100} step={0.5} className="form-control form-control-sm" value={g.minScore} onChange={(e) => setGrade({ minScore: num(e.target.value) })} /></td>
                          <td><input type="number" min={0} max={100} step={1} className="form-control form-control-sm" placeholder="Không đòi"
                            value={g.minExceedStandardRatio != null ? Math.round(g.minExceedStandardRatio * 100) : ""}
                            onChange={(e) => setGrade({ minExceedStandardRatio: e.target.value === "" ? null : num(e.target.value) / 100 })} /></td>
                          <td><textarea className="form-control form-control-sm" rows={2} value={g.conditions || ""} onChange={(e) => setGrade({ conditions: e.target.value })} /></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <h2 className="h6">Tham số</h2>
                <div className="row g-2">
                  {([
                    ["minTasks", "Số sản phẩm tối thiểu", 1],
                    ["maxTasks", "Số sản phẩm tối đa", 1],
                    ["totalTaskWeight", "Tổng trọng số = điểm tối đa nhóm nhiệm vụ", 0.5],
                    ["taskWeightTolerance", "Sai số tổng trọng số", 0.01],
                    ["generalMaxScore", "Điểm tối đa nhóm tiêu chí chung", 0.5],
                    ["explanationThreshold", "Ngưỡng chênh lệch cần giải trình", 0.5],
                    ["collectiveGeneralMaxScore", "Tập thể: tối đa tiêu chí chung", 0.5],
                    ["collectiveTaskMaxScore", "Tập thể: tối đa nhiệm vụ", 0.5],
                  ] as [keyof CriteriaSetContent["parameters"], string, number][]).map(([key, label, step]) => (
                    <div className="col-6 col-md-3" key={key}>
                      <label className="form-label small mb-0">{label}</label>
                      <input type="number" step={step} className="form-control form-control-sm" value={p[key] as number} onChange={(e) => setParam(key, num(e.target.value) as never)} />
                    </div>
                  ))}
                  <div className="col-6 col-md-3">
                    <label className="form-label small mb-0">Giảm từ … điểm phải nêu căn cứ</label>
                    <input type="number" step={0.5} min={0} className="form-control form-control-sm" placeholder="Không bắt buộc" value={p.deductionReasonMinPoints ?? ""}
                      onChange={(e) => setParam("deductionReasonMinPoints", e.target.value === "" ? null : num(e.target.value))} />
                  </div>
                  <div className="col-6 col-md-3 d-flex flex-column justify-content-end">
                    <label className="form-check small mb-0"><input type="checkbox" className="form-check-input" checked={p.allowNotApplicable} onChange={(e) => setParam("allowNotApplicable", e.target.checked)} /> <span className="form-check-label">Cho phép K/AD (có lý do)</span></label>
                    <label className="form-check small mb-0"><input type="checkbox" className="form-check-input" checked={p.explanationOnGradeChange} onChange={(e) => setParam("explanationOnGradeChange", e.target.checked)} /> <span className="form-check-label">Giải trình cả khi đổi mức</span></label>
                  </div>
                  <div className="col-6 col-md-3">
                    <label className="form-label small mb-0">Xử lý điểm khi K/AD</label>
                    <select className="form-select form-select-sm" value={p.notApplicableRule} onChange={(e) => setParam("notApplicableRule", e.target.value as typeof p.notApplicableRule)}>
                      <option value="ExcludeAndRescale">Bỏ khỏi mẫu số rồi quy đổi</option>
                      <option value="GrantFull">Tính như đạt tối đa</option>
                    </select>
                  </div>
                  <div className="col-6 col-md-3">
                    <label className="form-label small mb-0">Trần xuất sắc (%)</label>
                    <input type="number" min={0} max={100} step={1} className="form-control form-control-sm" value={Math.round(p.excellentQuota.ratio * 10000) / 100}
                      onChange={(e) => setParam("excellentQuota", { ...p.excellentQuota, ratio: num(e.target.value) / 100 })} />
                  </div>
                  <div className="col-6 col-md-3">
                    <label className="form-label small mb-0">Mẫu số trần xuất sắc</label>
                    <select className="form-select form-select-sm" value={p.excellentQuota.denominator} onChange={(e) => setParam("excellentQuota", { ...p.excellentQuota, denominator: e.target.value as typeof p.excellentQuota.denominator })}>
                      <option value="GoodOnly">Số "Hoàn thành tốt"</option>
                      <option value="GoodOrBetter">Số "Hoàn thành tốt" trở lên</option>
                    </select>
                  </div>
                  <div className="col-6 col-md-3">
                    <label className="form-label small mb-0">Làm tròn số người tối đa</label>
                    <RoundingModeSelect value={p.excellentQuota.rounding} onChange={(mode) => setParam("excellentQuota", { ...p.excellentQuota, rounding: mode })} />
                  </div>
                </div>
                <h3 className="h6 small fw-bold text-secondary mt-3">Làm tròn theo loại điểm</h3>
                <div className="row g-2">
                  {([
                    ["taskScore", "Điểm từng sản phẩm"],
                    ["tasksTotal", "Tổng điểm nhiệm vụ"],
                    ["generalTotal", "Tổng tiêu chí chung"],
                    ["total", "Tổng điểm"],
                  ] as [keyof Rounding, string][]).map(([key, label]) => {
                    const rule: RoundingRule = p.rounding[key];
                    const setRule = (change: Partial<RoundingRule>) => setParam("rounding", { ...p.rounding, [key]: { ...rule, ...change } });
                    return (
                      <div className="col-6 col-md-3" key={key}>
                        <label className="form-label small mb-0">{label}</label>
                        <div className="input-group input-group-sm">
                          <input type="number" min={0} max={4} className="form-control" value={rule.decimals} onChange={(e) => setRule({ decimals: num(e.target.value) })} aria-label={`Số chữ số thập phân — ${label}`} />
                          <RoundingModeSelect value={rule.mode} onChange={(mode) => setRule({ mode })} />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            </section>
          </>
        )}
      </div>
    </div>
  );
}

function RoundingModeSelect({ value, onChange }: { value: ScoreRoundingMode; onChange: (mode: ScoreRoundingMode) => void }) {
  return (
    <select className="form-select form-select-sm" value={value} onChange={(e) => onChange(e.target.value as ScoreRoundingMode)}>
      {(Object.keys(ROUNDING_LABELS) as ScoreRoundingMode[]).map((mode) => <option key={mode} value={mode}>{ROUNDING_LABELS[mode]}</option>)}
    </select>
  );
}
