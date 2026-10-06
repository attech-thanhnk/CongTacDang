"use client";

import React from "react";
import {
  AXIS_REPORT_LIMITS,
  AxisNote,
  AxisReport,
  AxisTask,
  AxisTaskBlock,
  SelfAssessmentSection,
  TaskLevel,
} from "@/services/evaluationService";

interface AxisInfo {
  code: string;
  name: string;
}

/** Số ký tự đã nhập / tối đa (đỏ khi vượt). */
function Counter({ length, max, suffix }: { length: number; max: number; suffix?: string }) {
  return (
    <span className={`small ${length > max ? "text-danger fw-semibold" : "text-secondary"}`}>
      {length.toLocaleString("vi-VN")} / {max.toLocaleString("vi-VN")} ký tự{suffix ? ` ${suffix}` : ""}
    </span>
  );
}

/**
 * Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân: một ô tự luận cho mỗi mục khai báo trong bộ tiêu chí của kỳ. Mục đầu tiên là
 * phần nhận xét chung, in trước các nhiệm vụ theo trục; số ký tự của mục tính cả phần nhiệm vụ (`firstSectionExtra`).
 */
export function SelfAssessmentInput({
  sections,
  value,
  onChange,
  firstSectionExtra = 0,
}: {
  sections: SelfAssessmentSection[];
  value: Record<string, string>;
  onChange: (value: Record<string, string>) => void;
  firstSectionExtra?: number;
}) {
  if (sections.length === 0) return null;
  return (
    <div className="d-flex flex-column gap-3">
      {sections.map((section, index) => {
        const text = value[section.code] ?? "";
        const extra = index === 0 ? firstSectionExtra : 0;
        const length = text.trim().length + extra;
        const id = `sa-${section.code}`;
        return (
          <div key={section.code}>
            <label className="form-label small fw-semibold mb-1" htmlFor={id}>
              {section.title}
              {section.required && <span className="text-danger ms-1">*</span>}
            </label>
            {section.guidance && <div className="small text-secondary mb-1">{section.guidance}</div>}
            <textarea
              id={id}
              className={`form-control form-control-sm ${length > section.maxLength ? "is-invalid" : ""}`}
              rows={index === 0 ? 4 : 8}
              placeholder={index === 0 ? "Nhận xét chung" : undefined}
              required={section.required && extra === 0}
              value={text}
              onChange={(e) => onChange({ ...value, [section.code]: e.target.value })}
            />
            <div className="d-flex justify-content-between gap-2 mt-1">
              {section.note ? <span className="small text-secondary fst-italic">{section.note}</span> : <span />}
              <Counter length={length} max={section.maxLength} suffix={extra > 0 ? "(gồm nhiệm vụ theo trục)" : undefined} />
            </div>
          </div>
        );
      })}
    </div>
  );
}

const SUMMARY_FIELDS: { key: keyof Omit<AxisReport, "tasks">; label: string; long: boolean }[] = [
  { key: "status", label: "Tình hình thực hiện", long: true },
  { key: "product", label: "Sản phẩm hoàn thành", long: true },
  { key: "progress", label: "Đánh giá tiến độ", long: false },
  { key: "deadline", label: "Thời hạn hoàn thành", long: false },
  { key: "note", label: "Ghi chú", long: false },
];

function LevelSelect({ levels, value, onChange, label }: { levels: TaskLevel[]; value?: number | null; onChange: (v: number | null) => void; label: string }) {
  if (levels.length === 0) return null;
  return (
    <select className="form-select form-select-sm" style={{ width: 96 }} value={value ?? ""} aria-label={label}
      title={levels.map((l) => `Mức ${l.code}: ${l.label}`).join("\n")}
      onChange={(e) => onChange(e.target.value ? Number(e.target.value) : null)}>
      <option value="">Mức —</option>
      {levels.map((l) => <option key={l.code} value={l.code} title={l.label}>Mức {l.code}</option>)}
    </select>
  );
}

/**
 * Nhập theo trục: các nhiệm vụ kèm mức tự đánh giá và ý con "+" (in ở mục I Mẫu 09C — `showTasks`) và dòng tóm
 * tắt "Trục n" của Mẫu 9D (`showSummary`).
 */
export function AxisReportsInput({
  axes,
  value,
  onChange,
  levels = [],
  showTasks,
  showSummary,
  taskLabel = "Nhiệm vụ {n}:",
}: {
  axes: AxisInfo[];
  value: Record<string, AxisReport>;
  onChange: (value: Record<string, AxisReport>) => void;
  levels?: TaskLevel[];
  showTasks: boolean;
  showSummary: boolean;
  taskLabel?: string;
}) {
  const report = (code: string): AxisReport => value[code] ?? {};
  const setReport = (code: string, change: Partial<AxisReport>) => onChange({ ...value, [code]: { ...report(code), ...change } });
  const setTasks = (code: string, tasks: AxisTask[]) => setReport(code, { tasks });
  const updateTask = (code: string, k: number, change: Partial<AxisTask>) =>
    setTasks(code, (report(code).tasks ?? []).map((t, i) => (i === k ? { ...t, ...change } : t)));
  const moveTask = (code: string, k: number, delta: number) => {
    const tasks = [...(report(code).tasks ?? [])];
    const j = k + delta;
    if (j < 0 || j >= tasks.length) return;
    [tasks[k], tasks[j]] = [tasks[j], tasks[k]];
    setTasks(code, tasks);
  };
  /** Điền "Tình hình thực hiện" từ các nhiệm vụ: mỗi nhiệm vụ một dòng "- …". */
  const fillStatus = (code: string) =>
    setReport(code, {
      status: (report(code).tasks ?? []).filter((t) => t.content.trim()).map((t) => `- ${t.content.trim()}`).join("\n").slice(0, AXIS_REPORT_LIMITS.long),
    });

  return (
    <div className="d-flex flex-column gap-2">
      {axes.map((axis, axisIndex) => {
        const r = report(axis.code);
        const tasks = r.tasks ?? [];
        return (
          <div key={axis.code} className="border rounded">
            <div className="bg-light px-2 py-1 small fw-semibold border-bottom">
              Trục {axisIndex + 1} — {axis.name}
            </div>
            <div className="p-2">
              {showTasks && (
                <div className="d-flex flex-column gap-2 mb-2">
                  {tasks.map((task, k) => (
                    <div key={k}>
                      <div className="d-flex gap-2 align-items-start">
                        <span className="small fw-semibold text-nowrap pt-1" style={{ minWidth: 84 }}>{taskLabel.split("{n}").join(String(k + 1))}</span>
                        <textarea className="form-control form-control-sm" rows={2} maxLength={AXIS_REPORT_LIMITS.long} required value={task.content}
                          placeholder="Nội dung nhiệm vụ, tình hình và kết quả thực hiện"
                          aria-label={`Trục ${axisIndex + 1}, nhiệm vụ ${k + 1}`}
                          onChange={(e) => updateTask(axis.code, k, { content: e.target.value })} />
                        <LevelSelect levels={levels} value={task.level} label={`Mức tự đánh giá nhiệm vụ ${k + 1}`}
                          onChange={(level) => updateTask(axis.code, k, { level })} />
                        <div className="btn-group btn-group-sm flex-shrink-0">
                          <button type="button" className="btn btn-outline-secondary" title="Lên" aria-label="Chuyển lên" disabled={k === 0}
                            onClick={() => moveTask(axis.code, k, -1)}><i className="bi bi-arrow-up" /></button>
                          <button type="button" className="btn btn-outline-secondary" title="Xuống" aria-label="Chuyển xuống" disabled={k === tasks.length - 1}
                            onClick={() => moveTask(axis.code, k, 1)}><i className="bi bi-arrow-down" /></button>
                          <button type="button" className="btn btn-outline-danger" title="Xóa nhiệm vụ" aria-label="Xóa nhiệm vụ"
                            onClick={() => setTasks(axis.code, tasks.filter((_, i) => i !== k))}><i className="bi bi-x-lg" /></button>
                        </div>
                      </div>
                      {(task.items ?? []).map((item, j) => (
                        <div key={j} className="d-flex gap-2 align-items-start mt-1" style={{ paddingLeft: 92 }}>
                          <span className="small fw-semibold pt-1">+</span>
                          <textarea className="form-control form-control-sm" rows={1} maxLength={AXIS_REPORT_LIMITS.long} required value={item.content}
                            aria-label={`Ý ${j + 1} của nhiệm vụ ${k + 1}`}
                            onChange={(e) => updateTask(axis.code, k, { items: (task.items ?? []).map((x, i) => (i === j ? { ...x, content: e.target.value } : x)) })} />
                          <LevelSelect levels={levels} value={item.level} label={`Mức tự đánh giá ý ${j + 1}`}
                            onChange={(level) => updateTask(axis.code, k, { items: (task.items ?? []).map((x, i) => (i === j ? { ...x, level } : x)) })} />
                          <button type="button" className="btn btn-link btn-sm text-danger p-0 pt-1" title="Xóa ý" aria-label="Xóa ý"
                            onClick={() => updateTask(axis.code, k, { items: (task.items ?? []).filter((_, i) => i !== j) })}>
                            <i className="bi bi-x-lg" />
                          </button>
                        </div>
                      ))}
                      {(task.items ?? []).length < AXIS_REPORT_LIMITS.items && (
                        <button type="button" className="btn btn-link btn-sm p-0" style={{ marginLeft: 92 }}
                          onClick={() => updateTask(axis.code, k, { items: [...(task.items ?? []), { content: "", level: null }] })}>
                          <i className="bi bi-plus-lg me-1" />Thêm ý
                        </button>
                      )}
                    </div>
                  ))}
                  <div>
                    <button type="button" className="btn btn-outline-primary btn-sm" disabled={tasks.length >= AXIS_REPORT_LIMITS.tasks}
                      onClick={() => setTasks(axis.code, [...tasks, { content: "", level: null, items: [] }])}>
                      <i className="bi bi-plus-lg me-1" />Thêm nhiệm vụ
                    </button>
                  </div>
                </div>
              )}
              {showSummary && (
                <div className={showTasks ? "border-top pt-2" : ""}>
                  <div className="d-flex justify-content-between align-items-center mb-1">
                    <span className="small text-secondary">Dòng Trục {axisIndex + 1} — Mẫu 9D</span>
                    {showTasks && tasks.some((t) => t.content.trim()) && (
                      <button type="button" className="btn btn-link btn-sm p-0" onClick={() => fillStatus(axis.code)}>
                        Lấy tình hình từ nhiệm vụ
                      </button>
                    )}
                  </div>
                  <div className="row g-2">
                    {SUMMARY_FIELDS.map((f) => (
                      <div key={f.key} className={f.long ? "col-12 col-lg-6" : "col-12 col-md-4"}>
                        <label className="form-label small text-secondary mb-0" htmlFor={`ar-${axis.code}-${f.key}`}>{f.label}</label>
                        {f.long ? (
                          <textarea id={`ar-${axis.code}-${f.key}`} className="form-control form-control-sm" rows={3} maxLength={AXIS_REPORT_LIMITS.long}
                            value={r[f.key] ?? ""} onChange={(e) => setReport(axis.code, { [f.key]: e.target.value })} />
                        ) : (
                          <input id={`ar-${axis.code}-${f.key}`} className="form-control form-control-sm" maxLength={AXIS_REPORT_LIMITS.short}
                            value={r[f.key] ?? ""} onChange={(e) => setReport(axis.code, { [f.key]: e.target.value })} />
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

/** Mục I Mẫu 09C như bản in: câu dẫn, nhận xét chung, rồi các trục (tiêu đề đậm, mỗi nhiệm vụ một đoạn). */
export function SelfAssessmentPreview({ section, text, blocks }: { section?: SelfAssessmentSection; text?: string; blocks: AxisTaskBlock[] }) {
  if (!section) return null;
  return (
    <div className="border rounded p-3 small" style={{ fontFamily: "'Times New Roman', serif", fontSize: "0.95rem" }}>
      <div className="fw-bold">{section.title}</div>
      {section.guidance && <div style={{ textIndent: 32 }}>{section.guidance}</div>}
      {text?.trim() && <div style={{ textIndent: 32, whiteSpace: "pre-wrap" }}>{text.trim()}</div>}
      {blocks.map((b) => (
        <React.Fragment key={b.title}>
          <div className="fw-bold mt-1" style={{ textIndent: 32 }}>{b.title}</div>
          {b.lines.map((l, i) => <div key={i} style={{ textIndent: 32, whiteSpace: "pre-wrap" }}>{l}</div>)}
        </React.Fragment>
      ))}
      {!text?.trim() && blocks.length === 0 && <div className="text-secondary">Chưa nhập.</div>}
    </div>
  );
}

/** Mẫu 09B — ba cột tự luận của một trục: mục tiêu đề ra, tóm tắt kết quả/minh chứng, ghi chú. */
export function AxisNoteFields({
  axisCode,
  value,
  onChange,
}: {
  axisCode: string;
  value: AxisNote;
  onChange: (value: AxisNote) => void;
}) {
  const field = (key: keyof AxisNote, label: string, rows: number) => (
    <div className="col-12 col-lg-4">
      <label className="form-label small text-secondary mb-0" htmlFor={`an-${axisCode}-${key}`}>{label}</label>
      <textarea id={`an-${axisCode}-${key}`} className="form-control form-control-sm" rows={rows} maxLength={AXIS_REPORT_LIMITS.axisNote}
        value={value[key] ?? ""} onChange={(e) => onChange({ ...value, [key]: e.target.value })} />
    </div>
  );
  return (
    <div className="row g-2">
      {field("target", "Mục tiêu, nhiệm vụ đề ra", 2)}
      {field("result", "Tóm tắt kết quả sản phẩm thực tế; tài liệu minh chứng", 2)}
      {field("note", "Ghi chú", 2)}
    </div>
  );
}

/** Nội dung Mẫu 09C đã lưu (chỉ đọc): mục đầu tiên gồm nhận xét chung và nhiệm vụ theo trục. */
export function SelfAssessmentView({ sections, value, blocks = [] }: { sections: SelfAssessmentSection[]; value?: Record<string, string>; blocks?: AxisTaskBlock[] }) {
  const filled = sections.filter((s, i) => (value?.[s.code] ?? "").trim().length > 0 || (i === 0 && blocks.length > 0));
  if (filled.length === 0) return <div className="small text-secondary">Chưa nhập.</div>;
  return (
    <div className="d-flex flex-column gap-2">
      {filled.map((s) => (
        <div key={s.code}>
          <div className="small fw-semibold">{s.title}</div>
          {value?.[s.code] && <div className="small" style={{ whiteSpace: "pre-wrap" }}>{value[s.code]}</div>}
          {s.code === sections[0].code && blocks.map((b) => (
            <div key={b.title} className="small mt-1">
              <div className="fw-semibold">{b.title}</div>
              {b.lines.map((l, i) => <div key={i} style={{ whiteSpace: "pre-wrap" }}>{l}</div>)}
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}

/** Mẫu 9D đã lưu (chỉ đọc): một dòng mỗi trục như biểu mẫu gốc. Khung hẹp thì bảng cuộn ngang. */
export function AxisSummaryView({ axes, reports }: { axes: AxisInfo[]; reports?: Record<string, AxisReport> }) {
  const find = (code: string) => Object.entries(reports ?? {}).find(([k]) => k.toLowerCase() === code.toLowerCase())?.[1];
  const has = axes.some((a) => {
    const r = find(a.code);
    return !!(r?.status || r?.product || r?.progress || r?.deadline || r?.note);
  });
  if (!has) return <div className="small text-secondary">Chưa nhập.</div>;
  return (
    <div className="table-responsive">
      <table className="table table-sm align-top small mb-0" style={{ minWidth: 820 }}>
        <thead>
          <tr>
            <th style={{ width: 64 }}>TT</th>
            <th style={{ minWidth: 180 }}>Nội dung</th>
            <th style={{ minWidth: 100 }}>Thời hạn</th>
            <th style={{ minWidth: 200 }}>Tình hình thực hiện</th>
            <th style={{ minWidth: 150 }}>Sản phẩm</th>
            <th style={{ minWidth: 120 }}>Tiến độ</th>
            <th style={{ minWidth: 100 }}>Ghi chú</th>
          </tr>
        </thead>
        <tbody>
          {axes.map((axis, i) => {
            const r = find(axis.code);
            return (
              <tr key={axis.code}>
                <td className="fw-semibold text-nowrap">Trục {i + 1}</td>
                <td>{axis.name}</td>
                <td>{r?.deadline || "—"}</td>
                <td style={{ whiteSpace: "pre-wrap" }}>{r?.status || "—"}</td>
                <td style={{ whiteSpace: "pre-wrap" }}>{r?.product || "—"}</td>
                <td>{r?.progress || "—"}</td>
                <td style={{ whiteSpace: "pre-wrap" }}>{r?.note || "—"}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
