"use client";

import React, { useId, useMemo } from "react";
import { DocWithToc } from "@/components/common/DocWithToc";
import { COLLECTIVE_KIND_LABELS, CriteriaSetContent, GRADE_LABELS, RECORD_FORMS, ROUNDING_LABELS, fmt } from "@/services/criteriaService";
import styles from "./CriteriaSummary.module.css";

interface NavEntry {
  id: string;
  label: string;
  code?: string;
  child?: boolean;
}

interface CriteriaSummaryProps {
  content: CriteriaSetContent;
  /** Hiện mục lục (thanh bên dính trên màn rộng, thanh nhảy nhanh trên màn hẹp) — dùng khi bộ tiêu chí là nội dung chính của trang. */
  navigation?: boolean;
}

type ItemRow = { code: string; text: string; maxScore: number };

/** Xem nội dung bộ tiêu chí (chỉ đọc): tiêu chí chung, trục, khung tỷ trọng, thang quy đổi, mức xếp loại, tham số. */
export function CriteriaSummary({ content, navigation = false }: CriteriaSummaryProps) {
  const p = content.parameters;
  const collectiveGroups = content.collectiveGroups ?? [];
  const prefix = `cs${useId().replace(/[^a-zA-Z0-9]/g, "")}`;
  const sid = (key: string) => `${prefix}-${key}`;

  const nav = useMemo<NavEntry[]>(() => {
    const entries: NavEntry[] = [{ id: sid("general"), label: "Tiêu chí chung" }];
    content.generalGroups.forEach((g, i) => entries.push({ id: sid(`g${i}`), label: g.name, code: g.code, child: true }));
    if (collectiveGroups.length > 0) entries.push({ id: sid("collective"), label: "Tiêu chí tập thể" });
    entries.push(
      { id: sid("axes"), label: "Trục kết quả" },
      { id: sid("forms"), label: "Biểu mẫu" },
      { id: sid("frames"), label: "Tỷ trọng và quy đổi" },
      { id: sid("grades"), label: "Mức xếp loại" },
      { id: sid("params"), label: "Tham số" }
    );
    return entries;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [content, prefix]);

  const body = (
    <div className={styles.blocks}>
      <section id={sid("general")} className={styles.block}>
        <header className={styles.blockHead}>
          <h3 className={styles.blockTitle}>Nhóm tiêu chí chung</h3>
          <span className={styles.blockMeta}>
            Tối đa <strong className="num">{fmt(p.generalMaxScore)}</strong> điểm
          </span>
        </header>
        <CriteriaGroups
          groups={content.generalGroups.map((g, i) => ({
            id: sid(`g${i}`),
            code: g.code,
            name: g.name,
            note: g.scoringMode === "Binary" ? "Đảm bảo / Không đảm bảo" : "Chấm điểm trong khoảng",
            items: g.items,
          }))}
        />
      </section>

      {collectiveGroups.length > 0 && (
        <section id={sid("collective")} className={styles.block}>
          <header className={styles.blockHead}>
            <h3 className={styles.blockTitle}>Khung tiêu chí tập thể (Mẫu 06, 07)</h3>
          </header>
          <CriteriaGroups
            groups={collectiveGroups.map((g, i) => ({
              id: sid(`c${i}`),
              code: g.code,
              name: g.name,
              note: COLLECTIVE_KIND_LABELS[g.kind] ?? g.kind,
              items: g.items,
            }))}
          />
        </section>
      )}

      <section id={sid("axes")} className={styles.block}>
        <header className={styles.blockHead}>
          <h3 className={styles.blockTitle}>Trục kết quả</h3>
        </header>
        <div className={styles.axes}>
          {content.axes.map((a) => (
            <div key={a.code} className={styles.axis}>
              <div className={styles.axisHead}>
                <span className={styles.code}>{a.code}</span>
                <span className={styles.axisName}>{a.name}</span>
              </div>
              {a.description && <div className={styles.axisDesc}>{a.description}</div>}
            </div>
          ))}
        </div>
      </section>

      <section id={sid("forms")} className={styles.block}>
        <header className={styles.blockHead}>
          <h3 className={styles.blockTitle}>Biểu mẫu cá nhân</h3>
        </header>
        <div className={styles.blockBody}>
          <ul className={styles.formList}>
            {RECORD_FORMS.filter((f) => f.code === "09A" || (content.requiredForms ?? []).includes(f.code)).map((f) => (
              <li key={f.code}>
                <i className="bi bi-file-earmark-text" aria-hidden="true" />
                {f.name}
              </li>
            ))}
          </ul>
          {(content.requiredForms ?? []).includes("09C") && (content.selfAssessmentSections ?? []).length > 0 && (
            <>
              <h4 className={styles.subTitle}>Mục tự đánh giá của Mẫu 09C</h4>
              <ul className={styles.plainList}>
                {(content.selfAssessmentSections ?? []).map((s) => (
                  <li key={s.code}>
                    <strong>{s.code}</strong>: {s.title} — tối đa {fmt(s.maxLength, 0)} ký tự{s.required ? ", bắt buộc khi nộp" : ""}
                  </li>
                ))}
              </ul>
            </>
          )}
          {(content.taskLevels ?? []).length > 0 && (
            <>
              <h4 className={styles.subTitle}>Mức tự đánh giá nhiệm vụ</h4>
              <ul className={styles.plainList}>
                {(content.taskLevels ?? []).map((l) => (
                  <li key={l.code}>
                    <strong>{l.code}</strong>: {l.label}
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      </section>

      <section id={sid("frames")} className={styles.block}>
        <header className={styles.blockHead}>
          <h3 className={styles.blockTitle}>Khung tỷ trọng và thang quy đổi A-B-C-D</h3>
        </header>
        <div className={`${styles.blockBody} ${styles.twoCols}`}>
          <div>
            <h4 className={styles.subTitle}>Khung tỷ trọng</h4>
            <div className="table-responsive">
              <table className={`table table-sm mb-0 ${styles.table}`}>
                <thead>
                  <tr>
                    <th>Khung</th>
                    <th className="text-end">A</th>
                    <th className="text-end">B</th>
                    <th className="text-end">C</th>
                    <th className="text-end">D</th>
                  </tr>
                </thead>
                <tbody>
                  {content.weightFrames.map((f) => (
                    <tr key={f.code}>
                      <td>
                        <span className="fw-semibold">{f.code}</span> — {f.name}
                        {p.defaultWeightFrameCode === f.code && <span className={`tag t-info ${styles.inlineTag}`}>Mặc định</span>}
                      </td>
                      {[f.a, f.b, f.c, f.d].map((v, i) => (
                        <td key={i} className="text-end num">
                          {fmt(v * 100)}%
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {content.weightFrames.length > 0 && (
              <div className={styles.note}>
                {p.frameAdjustLimit != null && p.frameAdjustLimit > 0
                  ? `Được điều chỉnh mỗi thành phần ±${fmt(p.frameAdjustLimit)} điểm % khi duyệt danh mục.`
                  : "Không điều chỉnh tỷ lệ."}
              </div>
            )}
          </div>
          <div>
            <h4 className={styles.subTitle}>Thang quy đổi %</h4>
            <ul className={styles.scaleList}>
              {content.conversionScale.map((b) => (
                <li key={b.minPercent}>
                  <strong>{b.label}</strong>
                  {b.description && <span className={styles.scaleDesc}>{b.description}</span>}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </section>

      <section id={sid("grades")} className={styles.block}>
        <header className={styles.blockHead}>
          <h3 className={styles.blockTitle}>Mức xếp loại</h3>
        </header>
        <div className={`table-responsive ${styles.flush}`}>
          <table className={`table mb-0 ${styles.table} ${styles.gradeTable}`}>
            <thead>
              <tr>
                <th>Mức</th>
                <th className="text-end">Từ điểm</th>
                <th className="text-end">% vượt chuẩn tối thiểu</th>
                <th>Điều kiện kèm theo</th>
              </tr>
            </thead>
            <tbody>
              {content.grades.map((g) => (
                <tr key={g.grade}>
                  <td className="fw-semibold">{GRADE_LABELS[g.grade] ?? g.grade}</td>
                  <td className="text-end num" data-label="Từ điểm">{fmt(g.minScore)}</td>
                  <td className="text-end num" data-label="% vượt chuẩn tối thiểu">{g.minExceedStandardRatio != null ? `${fmt(g.minExceedStandardRatio * 100)}%` : "—"}</td>
                  <td data-label="Điều kiện kèm theo">{g.conditions || "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section id={sid("params")} className={styles.block}>
        <header className={styles.blockHead}>
          <h3 className={styles.blockTitle}>Tham số</h3>
        </header>
        <dl className={styles.params}>
          <dt>Sản phẩm, nhiệm vụ</dt>
          <dd>
            Số sản phẩm {p.minTasks}–{p.maxTasks}; tổng trọng số / điểm tối đa nhóm nhiệm vụ {fmt(p.totalTaskWeight)} (sai số {fmt(p.taskWeightTolerance)}).
          </dd>
          <dt>Không áp dụng (K/AD)</dt>
          <dd>
            {p.allowNotApplicable ? (p.notApplicableRule === "GrantFull" ? "Được phép, tính như đạt tối đa" : "Được phép, bỏ khỏi mẫu số rồi quy đổi") : "Không được phép"}; giảm từ{" "}
            {p.deductionReasonMinPoints != null ? `${fmt(p.deductionReasonMinPoints)} điểm phải nêu lý do` : "không bắt buộc nêu lý do"}.
          </dd>
          <dt>Giải trình</dt>
          <dd>
            Khi chênh lệch tự chấm – thẩm định từ {fmt(p.explanationThreshold)} điểm{p.explanationOnGradeChange ? " hoặc làm đổi mức" : ""}.
          </dd>
          <dt>Trần xuất sắc</dt>
          <dd>{fmt(p.excellentQuota.ratio * 100)}% số &quot;Hoàn thành tốt&quot; của từng nhóm chức danh, làm tròn từ 0,5 trở lên thành 1, không tính người đứng đầu.</dd>
          <dt>Làm tròn</dt>
          <dd>
            Điểm sản phẩm: {p.rounding.taskScore.decimals} chữ số, {ROUNDING_LABELS[p.rounding.taskScore.mode]}; tổng nhiệm vụ: {p.rounding.tasksTotal.decimals},{" "}
            {ROUNDING_LABELS[p.rounding.tasksTotal.mode]}; tiêu chí chung: {p.rounding.generalTotal.decimals}, {ROUNDING_LABELS[p.rounding.generalTotal.mode]}; tổng điểm:{" "}
            {p.rounding.total.decimals}, {ROUNDING_LABELS[p.rounding.total.mode]}.
          </dd>
          <dt>Hồ sơ tập thể</dt>
          <dd>
            Tối đa {fmt(p.collectiveGeneralMaxScore)} / {fmt(p.collectiveTaskMaxScore)} điểm
            {collectiveGroups.length > 0 ? ", chấm theo khung tiêu chí tập thể" : ", nhập thẳng điểm hai nhóm"}.
          </dd>
          <dt>Sau công bố</dt>
          <dd>
            Công khai {p.publishScores ? "mức xếp loại kèm điểm chính thức" : "chỉ mức xếp loại"}; bắt buộc lập kế hoạch 30-60-90 ngày (Mẫu 17) với mức{" "}
            {(p.improvementPlanRequiredGrades ?? ["HoanThanh", "KhongHoanThanh"]).map((g) => GRADE_LABELS[g as keyof typeof GRADE_LABELS] ?? g).join(", ") || "— (không bắt buộc)"};
            cảnh báo kế hoạch cần lập tới {p.improvementPlanAlertDays ?? 90} ngày sau khi đóng kỳ.
          </dd>
        </dl>
      </section>
    </div>
  );

  if (!navigation) return <div className={styles.root}>{body}</div>;

  return (
    <DocWithToc
      ariaLabel="Mục lục bộ tiêu chí"
      entries={nav.map((n) => ({ id: n.id, label: n.label, code: n.code, level: n.child ? 2 : 1 }))}
    >
      <div className={styles.root}>{body}</div>
    </DocWithToc>
  );
}

function CriteriaGroups({ groups }: { groups: { id: string; code: string; name: string; note: string; items: ItemRow[] }[] }) {
  return (
    <div className={styles.groups}>
      <div className={styles.colHead} aria-hidden="true">
        <span>Mã</span>
        <span>Tiêu chí</span>
        <span className="text-end">Tối đa</span>
      </div>
      {groups.map((g) => (
        <div key={g.id} id={g.id} className={styles.group}>
          <div className={styles.groupHead}>
            <span className={styles.groupCode}>{g.code}</span>
            <span className={styles.groupName}>
              {g.name}
              <span className={styles.groupNote}>{g.note}</span>
            </span>
            <span className={styles.max}>
              <span className="num">{fmt(g.items.reduce((s, i) => s + i.maxScore, 0))}</span>
              <span className={styles.maxUnit}>điểm</span>
            </span>
          </div>
          <ul className={styles.items}>
            {g.items.map((i) => (
              <li key={i.code} className={styles.itemRow}>
                <span className={styles.itemCode}>{i.code}</span>
                <span className={styles.itemText}>{i.text}</span>
                <span className={`${styles.itemMax} num`}>{fmt(i.maxScore)}</span>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

export default CriteriaSummary;
