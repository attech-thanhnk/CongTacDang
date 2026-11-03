"use client";

import React from "react";
import { CriteriaSetContent, GeneralItemScore, fmt } from "@/services/criteriaService";
import styles from "./record.module.css";

interface Props {
  content: CriteriaSetContent;
  selfScores: Record<string, GeneralItemScore>;
  value: Record<string, GeneralItemScore>;
  onChange: (value: Record<string, GeneralItemScore>) => void;
}

/** Điểm khởi tạo: điểm thẩm định đã lưu, chưa có thì lấy điểm tự chấm để người thẩm định chỉ sửa chỗ lệch. */
export function initialAppraisalItems(content: CriteriaSetContent, saved: Record<string, GeneralItemScore> | null | undefined,
  self: Record<string, GeneralItemScore> | null | undefined): Record<string, GeneralItemScore> {
  const result: Record<string, GeneralItemScore> = {};
  for (const group of content.generalGroups)
    for (const item of group.items) {
      const source = saved?.[item.code] ?? self?.[item.code];
      result[item.code] = source
        ? { score: source.score, notApplicable: !!source.notApplicable, reason: source.reason ?? "" }
        : { score: item.maxScore, notApplicable: false, reason: "" };
    }
  return result;
}

/** Nội dung gửi máy chủ: K/AD gửi điểm 0, lý do trống gửi null. */
export function appraisalItemsPayload(value: Record<string, GeneralItemScore>): Record<string, GeneralItemScore> {
  return Object.fromEntries(Object.entries(value).map(([code, s]) => [
    code,
    { score: s.notApplicable ? 0 : Number(s.score) || 0, notApplicable: !!s.notApplicable, reason: s.reason?.trim() || null },
  ]));
}

/** Bảng chấm thẩm định từng tiêu chí con, cùng quy tắc phiếu tự chấm; điểm tự chấm hiện bên cạnh. */
export function AppraisalItemsTable({ content, selfScores, value, onChange }: Props) {
  const p = content.parameters;
  const setItem = (code: string, change: Partial<GeneralItemScore>) => onChange({ ...value, [code]: { ...value[code], ...change } });
  const needsReason = (maxScore: number, s: GeneralItemScore) =>
    !!s.notApplicable || (p.deductionReasonMinPoints != null && maxScore - (Number(s.score) || 0) + 1e-6 >= p.deductionReasonMinPoints);
  const selfText = (s: GeneralItemScore | undefined) => (s == null ? "—" : s.notApplicable ? "K/AD" : fmt(s.score));

  return (
    <div className={`table-responsive ${styles.critWrap}`}>
      <table className={`table table-sm align-middle mb-0 ${styles.critTable}`}>
        <thead>
          <tr className="small text-secondary">
            <th style={{ width: 48 }}>Mã</th><th style={{ minWidth: 220 }}>Tiêu chí</th><th style={{ width: 52 }}>Tối đa</th>
            <th style={{ width: 60 }}>Tự chấm</th><th style={{ width: 190 }}>Thẩm định</th><th style={{ width: 160 }}>Lý do</th>
          </tr>
        </thead>
        <tbody>
          {content.generalGroups.map((group) => (
            <React.Fragment key={group.code}>
              <tr className="table-light small fw-semibold">
                <td>{group.code}</td>
                <td colSpan={5}>{group.name} — {fmt(group.items.reduce((s, i) => s + i.maxScore, 0))} điểm</td>
              </tr>
              {group.items.map((item) => {
                const s = value[item.code] ?? { score: item.maxScore, notApplicable: false, reason: "" };
                const na = !!s.notApplicable;
                const reasonRequired = needsReason(item.maxScore, s);
                return (
                  <tr key={item.code} className="small">
                    <td>{item.code}</td>
                    <td>{item.text}</td>
                    <td className={styles.critMax}>{fmt(item.maxScore)}</td>
                    <td className="num">{selfText(selfScores[item.code])}</td>
                    <td>
                      {group.scoringMode === "Binary" ? (
                        <select className="form-select form-select-sm" aria-label={`Thẩm định tiêu chí ${item.code}`}
                          value={na ? "na" : Number(s.score) >= item.maxScore ? "ok" : "no"}
                          onChange={(e) => setItem(item.code, e.target.value === "na"
                            ? { notApplicable: true, score: 0 }
                            : { notApplicable: false, score: e.target.value === "ok" ? item.maxScore : 0 })}>
                          <option value="ok">Đảm bảo</option>
                          <option value="no">Không đảm bảo</option>
                          {p.allowNotApplicable && <option value="na">Không áp dụng</option>}
                        </select>
                      ) : (
                        <div className="d-flex gap-2 align-items-center">
                          <input type="number" min={0} max={item.maxScore} step={0.25} disabled={na} required={!na}
                            className="form-control form-control-sm num" style={{ width: 80 }} aria-label={`Thẩm định tiêu chí ${item.code}`}
                            value={na ? "" : s.score} onChange={(e) => setItem(item.code, { score: Number(e.target.value) })} />
                          {p.allowNotApplicable && (
                            <label className="form-check mb-0">
                              <input type="checkbox" className="form-check-input" checked={na} onChange={(e) => setItem(item.code, { notApplicable: e.target.checked })} />
                              <span className="form-check-label">K/AD</span>
                            </label>
                          )}
                        </div>
                      )}
                    </td>
                    <td>
                      {reasonRequired || s.reason ? (
                        <input className="form-control form-control-sm" maxLength={4000} required={reasonRequired}
                          placeholder={na ? "Lý do không áp dụng" : "Lý do giảm điểm"} aria-label={`Lý do tiêu chí ${item.code}`}
                          value={s.reason || ""} onChange={(e) => setItem(item.code, { reason: e.target.value })} />
                      ) : (
                        <span className={`text-secondary ${styles.critEmpty}`}>—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </React.Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
}
