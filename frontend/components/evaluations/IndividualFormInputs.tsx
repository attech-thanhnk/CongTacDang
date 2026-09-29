"use client";

import React from "react";
import {
  AxisNote,
  SelfAssessmentSection,
  TASK_RESULT_LIMITS,
  TaskResultRow,
} from "@/services/evaluationService";

interface AxisInfo {
  code: string;
  name: string;
}

/** Số ký tự đã nhập / tối đa (đỏ khi vượt). */
function Counter({ length, max }: { length: number; max: number }) {
  return (
    <span className={`small ${length > max ? "text-danger fw-semibold" : "text-secondary"}`}>
      {length.toLocaleString("vi-VN")} / {max.toLocaleString("vi-VN")} ký tự
    </span>
  );
}

/**
 * Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân: một ô tự luận cho mỗi mục khai báo trong bộ tiêu chí của kỳ (tiêu đề, câu dẫn,
 * lưu ý in như biểu mẫu gốc). Phần "Tự đề xuất xếp loại" lấy từ điểm và mức tự đề xuất của phiếu tự chấm.
 */
export function SelfAssessmentInput({
  sections,
  value,
  onChange,
}: {
  sections: SelfAssessmentSection[];
  value: Record<string, string>;
  onChange: (value: Record<string, string>) => void;
}) {
  if (sections.length === 0) return null;
  return (
    <div className="d-flex flex-column gap-3">
      {sections.map((section) => {
        const text = value[section.code] ?? "";
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
              className={`form-control form-control-sm ${text.length > section.maxLength ? "is-invalid" : ""}`}
              rows={8}
              required={section.required}
              value={text}
              onChange={(e) => onChange({ ...value, [section.code]: e.target.value })}
            />
            <div className="d-flex justify-content-between gap-2 mt-1">
              {section.note ? <span className="small text-secondary fst-italic">{section.note}</span> : <span />}
              <Counter length={text.length} max={section.maxLength} />
            </div>
          </div>
        );
      })}
    </div>
  );
}

const EMPTY_ROW = (axisCode: string): TaskResultRow => ({ axisCode, content: "", deadline: "", status: "", product: "", progress: "", note: "" });

/**
 * Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý: các dòng nhiệm vụ nhóm theo trục của bộ tiêu chí,
 * đúng các cột (2)–(7) của biểu mẫu gốc.
 */
export function TaskResultsInput({
  axes,
  rows,
  onChange,
}: {
  axes: AxisInfo[];
  rows: TaskResultRow[];
  onChange: (rows: TaskResultRow[]) => void;
}) {
  const update = (index: number, change: Partial<TaskResultRow>) =>
    onChange(rows.map((r, i) => (i === index ? { ...r, ...change } : r)));
  const remove = (index: number) => onChange(rows.filter((_, i) => i !== index));
  const add = (axisCode: string) => {
    if (rows.length >= TASK_RESULT_LIMITS.rows) return;
    // Chèn sau dòng cuối của trục để giữ thứ tự nhập.
    let last = -1;
    rows.forEach((r, i) => {
      if (r.axisCode.toLowerCase() === axisCode.toLowerCase()) last = i;
    });
    const next = [...rows];
    next.splice(last >= 0 ? last + 1 : rows.length, 0, EMPTY_ROW(axisCode));
    onChange(next);
  };

  return (
    <div className="table-responsive">
      <table className="table table-sm align-top small mb-0">
        <thead>
          <tr className="text-secondary">
            <th style={{ width: 40 }}>TT</th>
            <th style={{ minWidth: 220 }}>Nội dung nhiệm vụ, công việc</th>
            <th style={{ width: 120 }}>Thời hạn hoàn thành</th>
            <th style={{ minWidth: 160 }}>Tình hình thực hiện</th>
            <th style={{ minWidth: 160 }}>Sản phẩm hoàn thành</th>
            <th style={{ width: 130 }}>Đánh giá tiến độ</th>
            <th style={{ width: 130 }}>Ghi chú</th>
            <th style={{ width: 32 }} />
          </tr>
        </thead>
        <tbody>
          {axes.map((axis, axisIndex) => {
            const indexes = rows
              .map((r, i) => ({ r, i }))
              .filter(({ r }) => r.axisCode.toLowerCase() === axis.code.toLowerCase());
            return (
              <React.Fragment key={axis.code}>
                <tr className="table-light">
                  <td className="fw-semibold text-nowrap">Trục {axisIndex + 1}</td>
                  <td colSpan={6} className="fw-semibold">{axis.name}</td>
                  <td>
                    <button type="button" className="btn btn-link btn-sm p-0" title="Thêm dòng" aria-label={`Thêm nhiệm vụ trục ${axisIndex + 1}`}
                      disabled={rows.length >= TASK_RESULT_LIMITS.rows} onClick={() => add(axis.code)}>
                      <i className="bi bi-plus-lg" />
                    </button>
                  </td>
                </tr>
                {indexes.map(({ r, i }, order) => (
                  <tr key={i}>
                    <td>{order + 1}</td>
                    <td>
                      <textarea className="form-control form-control-sm" rows={2} maxLength={TASK_RESULT_LIMITS.long} required value={r.content}
                        onChange={(e) => update(i, { content: e.target.value })} aria-label={`Nội dung nhiệm vụ trục ${axisIndex + 1} dòng ${order + 1}`} />
                    </td>
                    <td>
                      <input className="form-control form-control-sm" maxLength={TASK_RESULT_LIMITS.short} value={r.deadline ?? ""}
                        onChange={(e) => update(i, { deadline: e.target.value })} aria-label="Thời hạn hoàn thành" />
                    </td>
                    <td>
                      <textarea className="form-control form-control-sm" rows={2} maxLength={TASK_RESULT_LIMITS.long} value={r.status ?? ""}
                        onChange={(e) => update(i, { status: e.target.value })} aria-label="Tình hình thực hiện" />
                    </td>
                    <td>
                      <textarea className="form-control form-control-sm" rows={2} maxLength={TASK_RESULT_LIMITS.long} value={r.product ?? ""}
                        onChange={(e) => update(i, { product: e.target.value })} aria-label="Sản phẩm hoàn thành" />
                    </td>
                    <td>
                      <input className="form-control form-control-sm" maxLength={TASK_RESULT_LIMITS.short} value={r.progress ?? ""}
                        onChange={(e) => update(i, { progress: e.target.value })} aria-label="Đánh giá tiến độ" />
                    </td>
                    <td>
                      <input className="form-control form-control-sm" maxLength={TASK_RESULT_LIMITS.short} value={r.note ?? ""}
                        onChange={(e) => update(i, { note: e.target.value })} aria-label="Ghi chú" />
                    </td>
                    <td>
                      <button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa dòng" onClick={() => remove(i)}>
                        <i className="bi bi-x-lg" />
                      </button>
                    </td>
                  </tr>
                ))}
              </React.Fragment>
            );
          })}
        </tbody>
      </table>
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
      <textarea id={`an-${axisCode}-${key}`} className="form-control form-control-sm" rows={rows} maxLength={TASK_RESULT_LIMITS.axisNote}
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

/** Nội dung Mẫu 09C đã lưu (chỉ đọc). */
export function SelfAssessmentView({ sections, value }: { sections: SelfAssessmentSection[]; value?: Record<string, string> }) {
  const filled = sections.filter((s) => (value?.[s.code] ?? "").trim().length > 0);
  if (filled.length === 0) return <div className="small text-secondary">Chưa nhập.</div>;
  return (
    <div className="d-flex flex-column gap-2">
      {filled.map((s) => (
        <div key={s.code}>
          <div className="small fw-semibold">{s.title}</div>
          <div className="small" style={{ whiteSpace: "pre-wrap" }}>{value?.[s.code]}</div>
        </div>
      ))}
    </div>
  );
}

/** Các dòng Mẫu 9D đã lưu (chỉ đọc), nhóm theo trục. */
export function TaskResultsView({ axes, rows }: { axes: AxisInfo[]; rows?: TaskResultRow[] }) {
  if (!rows || rows.length === 0) return <div className="small text-secondary">Chưa nhập.</div>;
  return (
    <table className="table table-sm small mb-0">
      <thead>
        <tr><th>TT</th><th>Nội dung</th><th>Thời hạn</th><th>Tình hình thực hiện</th><th>Sản phẩm</th><th>Tiến độ</th><th>Ghi chú</th></tr>
      </thead>
      <tbody>
        {axes.map((axis, axisIndex) => {
          const own = rows.filter((r) => r.axisCode.toLowerCase() === axis.code.toLowerCase());
          if (own.length === 0) return null;
          return (
            <React.Fragment key={axis.code}>
              <tr className="table-light"><td className="fw-semibold text-nowrap">Trục {axisIndex + 1}</td><td colSpan={6} className="fw-semibold">{axis.name}</td></tr>
              {own.map((r, i) => (
                <tr key={`${axis.code}-${i}`}>
                  <td>{i + 1}</td>
                  <td style={{ whiteSpace: "pre-wrap" }}>{r.content}</td>
                  <td>{r.deadline || "—"}</td>
                  <td style={{ whiteSpace: "pre-wrap" }}>{r.status || "—"}</td>
                  <td style={{ whiteSpace: "pre-wrap" }}>{r.product || "—"}</td>
                  <td>{r.progress || "—"}</td>
                  <td>{r.note || "—"}</td>
                </tr>
              ))}
            </React.Fragment>
          );
        })}
      </tbody>
    </table>
  );
}
