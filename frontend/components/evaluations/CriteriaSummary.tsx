"use client";

import React from "react";
import { CriteriaSetContent, GRADE_LABELS, ROUNDING_LABELS, fmt } from "@/services/criteriaService";

/** Xem nội dung bộ tiêu chí (chỉ đọc): tiêu chí chung, trục, khung tỷ trọng, thang quy đổi, mức xếp loại, tham số. */
export function CriteriaSummary({ content, form }: { content: CriteriaSetContent; form: string }) {
  const p = content.parameters;
  return (
    <div className="d-flex flex-column gap-3 small">
      <div>
        <h3 className="h6 small fw-bold text-secondary">Nhóm tiêu chí chung (tối đa {fmt(p.generalMaxScore)} điểm)</h3>
        <table className="table table-sm mb-0">
          <thead><tr className="text-secondary"><th style={{ width: 60 }}>Mã</th><th>Tiêu chí</th><th style={{ width: 80 }}>Tối đa</th></tr></thead>
          <tbody>
            {content.generalGroups.map((g) => (
              <React.Fragment key={g.code}>
                <tr className="table-light fw-semibold">
                  <td>{g.code}</td>
                  <td>{g.name} <span className="fw-normal text-secondary">— {g.scoringMode === "Binary" ? "Đảm bảo / Không đảm bảo" : "chấm điểm trong khoảng"}</span></td>
                  <td>{fmt(g.items.reduce((s, i) => s + i.maxScore, 0))}</td>
                </tr>
                {g.items.map((i) => <tr key={i.code}><td>{i.code}</td><td>{i.text}</td><td>{fmt(i.maxScore)}</td></tr>)}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>

      <div>
        <h3 className="h6 small fw-bold text-secondary">Trục kết quả{form === "09B" ? ` (chấm trực tiếp, tổng ${fmt(content.axes.reduce((s, a) => s + a.maxScore, 0))} điểm)` : ""}</h3>
        <table className="table table-sm mb-0">
          <thead><tr className="text-secondary"><th style={{ width: 60 }}>Mã</th><th>Trục</th>{form === "09B" && <th style={{ width: 80 }}>Tối đa</th>}</tr></thead>
          <tbody>
            {content.axes.map((a) => (
              <tr key={a.code}><td>{a.code}</td><td>{a.name}{a.description && <div className="text-secondary">{a.description}</div>}</td>{form === "09B" && <td>{fmt(a.maxScore)}</td>}</tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="row g-3">
        <div className="col-lg-6">
          <h3 className="h6 small fw-bold text-secondary">Khung tỷ trọng A-B-C-D</h3>
          <table className="table table-sm mb-0">
            <thead><tr className="text-secondary"><th>Khung</th><th>A</th><th>B</th><th>C</th><th>D</th></tr></thead>
            <tbody>
              {content.weightFrames.map((f) => (
                <tr key={f.code}><td>{f.code} — {f.name}{p.defaultWeightFrameCode === f.code && <span className="badge text-bg-light ms-1">mặc định</span>}</td><td>{fmt(f.a * 100)}%</td><td>{fmt(f.b * 100)}%</td><td>{fmt(f.c * 100)}%</td><td>{fmt(f.d * 100)}%</td></tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="col-lg-6">
          <h3 className="h6 small fw-bold text-secondary">Thang quy đổi % A-B-C-D</h3>
          <ul className="mb-0 ps-3">
            {content.conversionScale.map((b) => <li key={b.minPercent}><strong>{b.label}</strong>{b.description ? `: ${b.description}` : ""}</li>)}
          </ul>
        </div>
      </div>

      <div>
        <h3 className="h6 small fw-bold text-secondary">Mức xếp loại</h3>
        <table className="table table-sm mb-0">
          <thead><tr className="text-secondary"><th>Mức</th><th style={{ width: 90 }}>Từ điểm</th><th style={{ width: 120 }}>% vượt chuẩn tối thiểu</th><th>Điều kiện kèm theo</th></tr></thead>
          <tbody>
            {content.grades.map((g) => (
              <tr key={g.grade}><td>{GRADE_LABELS[g.grade] ?? g.grade}</td><td>{fmt(g.minScore)}</td><td>{g.minExceedStandardRatio != null ? `${fmt(g.minExceedStandardRatio * 100)}%` : "—"}</td><td>{g.conditions || "—"}</td></tr>
            ))}
          </tbody>
        </table>
      </div>

      <div>
        <h3 className="h6 small fw-bold text-secondary">Tham số</h3>
        <ul className="mb-0 ps-3">
          <li>Số sản phẩm {p.minTasks}–{p.maxTasks}; tổng trọng số / điểm tối đa nhóm nhiệm vụ {fmt(p.totalTaskWeight)} (sai số {fmt(p.taskWeightTolerance)}).</li>
          <li>K/AD: {p.allowNotApplicable ? (p.notApplicableRule === "GrantFull" ? "cho phép, tính như đạt tối đa" : "cho phép, bỏ khỏi mẫu số rồi quy đổi") : "không cho phép"}; khoản giảm {p.deductionReasonMinPoints != null ? `từ ${fmt(p.deductionReasonMinPoints)} điểm phải nêu căn cứ` : "không bắt buộc căn cứ"}.</li>
          <li>Giải trình khi chênh lệch tự chấm – thẩm định từ {fmt(p.explanationThreshold)} điểm{p.explanationOnGradeChange ? " hoặc làm đổi mức" : ""}.</li>
          <li>Trần xuất sắc {fmt(p.excellentQuota.ratio * 100)}% số {p.excellentQuota.denominator === "GoodOnly" ? "\"Hoàn thành tốt\"" : "\"Hoàn thành tốt\" trở lên"}, làm tròn: {ROUNDING_LABELS[p.excellentQuota.rounding]}.</li>
          <li>
            Làm tròn — điểm sản phẩm: {p.rounding.taskScore.decimals} chữ số, {ROUNDING_LABELS[p.rounding.taskScore.mode]}; tổng nhiệm vụ: {p.rounding.tasksTotal.decimals}, {ROUNDING_LABELS[p.rounding.tasksTotal.mode]};
            tiêu chí chung: {p.rounding.generalTotal.decimals}, {ROUNDING_LABELS[p.rounding.generalTotal.mode]}; tổng điểm: {p.rounding.total.decimals}, {ROUNDING_LABELS[p.rounding.total.mode]}.
          </li>
          <li>Hồ sơ tập thể: tối đa {fmt(p.collectiveGeneralMaxScore)} / {fmt(p.collectiveTaskMaxScore)} điểm.</li>
        </ul>
      </div>
    </div>
  );
}

export default CriteriaSummary;
