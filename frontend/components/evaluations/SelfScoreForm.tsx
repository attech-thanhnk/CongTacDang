"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  AxisNote,
  AxisReport,
  EvaluationRecordDto,
  GRADE_OPTIONS,
  axisTaskTextLength,
  buildAxisTaskBlocks,
  criteriaAppliesForm,
  evaluationService,
  isConcurrencyConflict,
  prefillAxisReportsFromTasks,
  selfAssessmentSections,
  taskLevels,
} from "@/services/evaluationService";
import {
  CriteriaSnapshot,
  GRADE_LABELS,
  GeneralItemScore,
  describeFrame,
  fmt,
  generalTotal,
  gradeByScore,
  roundScore,
} from "@/services/criteriaService";
import { FileUploadModal } from "@/components/attachments/FileUploadModal";
import { AxisNoteFields, AxisReportsInput, SelfAssessmentInput, SelfAssessmentPreview } from "@/components/evaluations/IndividualFormInputs";
import { useToast } from "@/contexts/ToastContext";
import { TaskChangeMark } from "@/components/evaluations/TaskChangeRequests";
import { EvidenceFiles, evidenceSlot, useRecordEvidence } from "@/components/evaluations/EvidenceFiles";
import { errorMessage } from "@/components/admin/adminUtils";

/** Ô điểm để trống ("") cho tới khi cán bộ tự nhập — không điền sẵn mức tối đa. */
type ScoreInput = number | "";

interface TaskRatio {
  a: ScoreInput;
  b: ScoreInput;
  c: ScoreInput;
  d: ScoreInput;
  exceed: boolean;
  attachmentId?: string | null;
  attachmentName?: string | null;
}

interface Props {
  record: EvaluationRecordDto;
  criteria: CriteriaSnapshot | null;
  busy: boolean;
  onSubmit: (payload: Record<string, unknown>) => void;
}

const LEAVE_MESSAGE = "Phiếu tự chấm có thay đổi chưa lưu. Rời trang và bỏ các thay đổi này?";

const timeOf = (date: Date) => date.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });

/**
 * Cảnh báo khi rời trang lúc còn thay đổi chưa lưu: đóng/tải lại tab (`beforeunload`) và bấm liên kết nội bộ (chặn click trên thẻ
 * `<a>` ở pha capture, trước khi Next.js điều hướng). Không bắt được điều hướng bằng mã (`router.push`) hay nút Back của trình duyệt.
 */
function useLeaveGuard(dirty: boolean) {
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;

  useEffect(() => {
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (!dirtyRef.current) return;
      e.preventDefault();
      e.returnValue = "";
    };
    const onClick = (e: MouseEvent) => {
      if (!dirtyRef.current || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const anchor = (e.target as Element | null)?.closest?.("a[href]") as HTMLAnchorElement | null;
      if (!anchor || (anchor.target && anchor.target !== "_self") || anchor.hasAttribute("download")) return;
      const url = new URL(anchor.href, window.location.href);
      // Liên kết ra ngoài ứng dụng tải lại toàn trang → đã có beforeunload; liên kết cùng trang (#mục) không rời phiếu.
      if (url.origin !== window.location.origin) return;
      if (url.pathname === window.location.pathname && url.search === window.location.search) return;
      if (!window.confirm(LEAVE_MESSAGE)) {
        e.preventDefault();
        e.stopPropagation();
      }
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    document.addEventListener("click", onClick, true);
    return () => {
      window.removeEventListener("beforeunload", onBeforeUnload);
      document.removeEventListener("click", onClick, true);
    };
  }, []);
}

/**
 * Phiếu tự chấm dựng động từ bộ tiêu chí của kỳ: tiêu chí con "Đảm bảo / Không đảm bảo / K/AD (lý do)" hoặc ô điểm (theo cách chấm
 * của nhóm), trục theo mã (Mẫu 09B) hoặc A-B-C-D theo khung tỷ trọng của hồ sơ (Mẫu 09A); tổng tạm tính — máy chủ tính lại.
 * Nhập kèm (khi kỳ áp dụng) Mẫu 09C — nhận xét chung và nhiệm vụ theo trục kèm mức — và Mẫu 9D — dòng tóm tắt mỗi trục — trên
 * cùng một khối theo trục, cùng phần tự luận theo trục của Mẫu 09B; nội dung đã lưu được nạp lại để sửa tiếp khi hồ sơ bị trả lại.
 */
export function SelfScoreForm({ record, criteria, busy, onSubmit }: Props) {
  if (!criteria) {
    return (
      <div className="alert alert-warning small mb-0">
        Kỳ đánh giá chưa có bộ tiêu chí nên chưa tự chấm được. Hãy liên hệ người quản lý kỳ.
      </div>
    );
  }
  return <SelfScoreFormInner record={record} criteria={criteria} busy={busy} onSubmit={onSubmit} />;
}

function SelfScoreFormInner({ record, criteria, busy, onSubmit }: Props & { criteria: CriteriaSnapshot }) {
  const content = criteria.content;
  const p = content.parameters;
  const uses09B = criteria.selfScoreForm === "09B";
  const uses09C = criteriaAppliesForm(criteria, "09C");
  const uses9D = criteriaAppliesForm(criteria, "9D");
  const sections = selfAssessmentSections(criteria);
  // Khung hiệu lực do máy chủ tính (tỷ lệ đã điều chỉnh khi duyệt danh mục, nếu có); máy chủ cũ chưa trả thì tra trong bộ.
  const frame = record.weightFrame ?? content.weightFrames.find((f) => f.code.toLowerCase() === (record.weightFrameCode || "").toLowerCase());

  const [general, setGeneral] = useState<Record<string, GeneralItemScore>>(() => {
    const initial: Record<string, GeneralItemScore> = {};
    for (const group of content.generalGroups)
      for (const item of group.items)
        initial[item.code] = record.generalScores?.[item.code] ?? { score: item.maxScore, notApplicable: false, reason: "" };
    return initial;
  });
  const { confirm, toast } = useToast();
  const [axis, setAxis] = useState<Record<string, ScoreInput>>(() =>
    Object.fromEntries(content.axes.map((a) => [a.code, record.axisScores?.[a.code] ?? ""]))
  );
  const [grade, setGrade] = useState(record.selfProposedGrade && record.selfProposedGrade !== "ChuaXepLoai" ? record.selfProposedGrade : "");
  const [ratios, setRatios] = useState<Record<string, TaskRatio>>(() =>
    Object.fromEntries(
      record.tasks.map((t) => [
        t.id,
        {
          a: t.criteriaA_Ratio == null ? "" : Math.round(t.criteriaA_Ratio * 100),
          b: t.criteriaB_Ratio == null ? "" : Math.round(t.criteriaB_Ratio * 100),
          c: t.criteriaC_Ratio == null ? "" : Math.round(t.criteriaC_Ratio * 100),
          d: t.criteriaD_Ratio == null ? "" : Math.round(t.criteriaD_Ratio * 100),
          exceed: t.isExceedStandard,
          attachmentId: t.attachmentId,
          attachmentName: t.attachmentOriginalName || t.attachmentFileName,
        },
      ])
    )
  );
  const [uploadFor, setUploadFor] = useState<string | null>(null);

  // Biểu mẫu cá nhân nhập cùng phiếu tự chấm.
  const [selfAssessment, setSelfAssessment] = useState<Record<string, string>>(() => ({ ...(record.selfAssessment ?? {}) }));
  const initialAxisReports = useMemo(
    () => prefillAxisReportsFromTasks(content.axes, record.tasks, record.axisReports),
    [content.axes, record.tasks, record.axisReports]
  );
  const [axisReports, setAxisReports] = useState<Record<string, AxisReport>>(() => JSON.parse(JSON.stringify(initialAxisReports.reports)));
  const [showPreview, setShowPreview] = useState(false);
  const blocks = useMemo(() => (uses09C ? buildAxisTaskBlocks(content.axes, axisReports, criteria) : []), [uses09C, content.axes, axisReports, criteria]);
  const [groupNotes, setGroupNotes] = useState<Record<string, string>>(() => ({ ...(record.generalGroupNotes ?? {}) }));
  const evidence = useRecordEvidence(record.id);
  const [axisNotes, setAxisNotes] = useState<Record<string, AxisNote>>(() =>
    Object.fromEntries(content.axes.map((a) => [a.code, { ...(record.axisNotes?.[a.code] ?? {}) }]))
  );

  // Lưu nháp: theo dõi thay đổi chưa lưu bằng ảnh chụp nội dung phiếu; phiên bản hồ sơ sau lần lưu nháp gần nhất được dùng cho
  // lần lưu/nộp tiếp theo (trang chưa tải lại hồ sơ) — trừ khi trang đã nạp hồ sơ mới.
  const snapshot = JSON.stringify([general, axis, grade, ratios, selfAssessment, axisReports, groupNotes, axisNotes]);
  const [savedSnapshot, setSavedSnapshot] = useState(snapshot);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const [saving, setSaving] = useState(false);
  const [draftVersion, setDraftVersion] = useState<{ base?: number; next?: number } | null>(null);
  const currentVersion = draftVersion && draftVersion.base === record.version ? draftVersion.next : record.version;
  const initialSubmittedAt = useRef(record.selfScoredAt ?? null);
  const submitted = !!record.selfScoredAt && record.selfScoredAt !== initialSubmittedAt.current;
  const dirty = !submitted && snapshot !== savedSnapshot;
  useLeaveGuard(dirty);

  const setItem = (code: string, change: Partial<GeneralItemScore>) => setGeneral({ ...general, [code]: { ...general[code], ...change } });
  const toInput = (raw: string): ScoreInput => (raw === "" ? "" : Number(raw));
  const setRatio = (taskId: string, key: keyof TaskRatio, value: ScoreInput | boolean | string | null) =>
    setRatios({ ...ratios, [taskId]: { ...ratios[taskId], [key]: value } });

  const generalScore = useMemo(() => generalTotal(content, general), [content, general]);
  const taskScore = (weight: number, r: TaskRatio) => {
    if (!frame) return 0;
    const clamp = (v: ScoreInput) => Math.min(1, Math.max(0, (Number(v) || 0) / 100));
    const ratio = clamp(r.a) * frame.a + clamp(r.b) * frame.b + clamp(r.c) * frame.c + clamp(r.d) * frame.d;
    return roundScore(weight * ratio, p.rounding.taskScore);
  };
  const tasksScore = useMemo(() => {
    if (uses09B) return roundScore(Object.values(axis).reduce<number>((s, v) => s + (Number(v) || 0), 0), p.rounding.tasksTotal);
    return roundScore(record.tasks.reduce((s, t) => s + (ratios[t.id] ? taskScore(t.weight, ratios[t.id]) : t.selfScore), 0), p.rounding.tasksTotal);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [uses09B, axis, ratios, record.tasks, frame, p]);
  const total = roundScore(generalScore + tasksScore, p.rounding.total);

  const needsReason = (item: { maxScore: number }, s: GeneralItemScore) =>
    !!s.notApplicable || (p.deductionReasonMinPoints != null && item.maxScore - (Number(s.score) || 0) + 1e-6 >= p.deductionReasonMinPoints);

  /** Nội dung gửi máy chủ. Lưu nháp: bỏ trục / nhiệm vụ còn ô điểm trống (giữ trống khi mở lại, không ghi thành 0). */
  const buildPayload = (draft: boolean): Record<string, unknown> => {
    const generalScores = Object.fromEntries(
      Object.entries(general).map(([code, s]) => [code, { score: s.notApplicable ? 0 : Number(s.score) || 0, notApplicable: !!s.notApplicable, reason: s.reason?.trim() || null }])
    );
    const payload: Record<string, unknown> = { generalScores, generalGroupNotes: groupNotes, selfProposedGrade: grade || null };
    if (uses09C) payload.selfAssessment = selfAssessment;
    if (uses09C || uses9D) payload.axisReports = axisReports;
    if (uses09B) {
      payload.axisScores = Object.fromEntries(
        Object.entries(axis).filter(([, v]) => !draft || v !== "").map(([k, v]) => [k, Number(v) || 0])
      );
      payload.axisNotes = axisNotes;
    } else {
      payload.taskScores = record.tasks
        .filter((t) => !t.changeStatus)
        .filter((t) => !draft || (["a", "b", "c", "d"] as const).every((k) => ratios[t.id][k] !== ""))
        .map((t) => ({
          taskId: t.id,
          criteriaA_Ratio: (Number(ratios[t.id].a) || 0) / 100,
          criteriaB_Ratio: (Number(ratios[t.id].b) || 0) / 100,
          criteriaC_Ratio: (Number(ratios[t.id].c) || 0) / 100,
          criteriaD_Ratio: (Number(ratios[t.id].d) || 0) / 100,
          isExceedStandard: ratios[t.id].exceed,
          attachmentId: ratios[t.id].attachmentId || null,
        }));
    }
    return payload;
  };

  const saveDraft = async () => {
    const savingSnapshot = snapshot;
    setSaving(true);
    try {
      const saved = await evaluationService.saveSelfScoreDraft(record.id, currentVersion ?? 0, buildPayload(true));
      setDraftVersion({ base: record.version, next: saved.version });
      setSavedSnapshot(savingSnapshot);
      const now = new Date();
      setSavedAt(now);
      toast.success(`Đã lưu nháp lúc ${timeOf(now)}.`);
    } catch (error: unknown) {
      if (!isConcurrencyConflict(error)) toast.error(errorMessage(error, "Không lưu được nháp."));
    } finally {
      setSaving(false);
    }
  };

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const payload = buildPayload(false);
    if (currentVersion !== record.version && currentVersion != null) payload.version = currentVersion;
    const gradeLabel = grade ? GRADE_LABELS[grade as keyof typeof GRADE_LABELS] : `${GRADE_LABELS[gradeByScore(content, total)]} (hệ thống gợi ý)`;
    const missing = [
      uses09C && !Object.values(selfAssessment).some((v) => v?.trim()) && blocks.length === 0 ? "bản tự đánh giá Mẫu 09C" : null,
      uses9D && !Object.values(axisReports).some((r) => r.status?.trim() || r.product?.trim() || r.progress?.trim()) ? "kết quả theo trục Mẫu 9D" : null,
    ].filter(Boolean);
    confirm({
      title: "Nộp phiếu tự chấm",
      message: `Tổng điểm tự chấm ${fmt(total)}, mức tự đề xuất: ${gradeLabel}.`
        + (missing.length ? ` Chưa nhập ${missing.join(" và ")}.` : "")
        + " Sau khi nộp, bạn không sửa được trừ khi bị trả lại.",
      confirmText: "Nộp phiếu",
      isDanger: false,
      onConfirm: () => onSubmit(payload),
    });
  };

  const band = (percent: number) =>
    [...content.conversionScale].sort((x, y) => y.minPercent - x.minPercent).find((b) => percent + 1e-6 >= b.minPercent);

  return (
    <form onSubmit={submit}>
      <div className="small text-secondary mb-2">
        Giảm điểm{p.deductionReasonMinPoints != null ? ` từ ${fmt(p.deductionReasonMinPoints)} điểm trở lên` : ""} hoặc chọn &quot;Không áp dụng (K/AD)&quot; phải nêu lý do.
      </div>

      <h4 className="h6 small fw-bold text-secondary">I. Nhóm tiêu chí chung (tối đa {fmt(p.generalMaxScore)} điểm)</h4>
      <div className="table-responsive mb-2">
        <table className="table table-sm align-middle">
          <thead>
            <tr className="small text-secondary">
              <th style={{ width: 48 }}>Mã</th><th style={{ minWidth: 260 }}>Tiêu chí</th><th style={{ width: 52 }}>Tối đa</th><th style={{ width: 170 }}>Chấm</th><th style={{ width: 150 }}>Lý do</th>
            </tr>
          </thead>
          <tbody>
            {content.generalGroups.map((group) => (
              <React.Fragment key={group.code}>
                <tr className="table-light small fw-semibold">
                  <td>{group.code}</td>
                  <td colSpan={4}>{group.name} — {fmt(group.items.reduce((s, i) => s + i.maxScore, 0))} điểm</td>
                </tr>
                <tr className="small">
                  <td />
                  <td colSpan={4}>
                    <textarea className="form-control form-control-sm" rows={1} maxLength={2000} value={groupNotes[group.code] ?? ""}
                      placeholder="Tóm tắt kết quả, minh chứng chung của nhóm (không bắt buộc)" aria-label={`Nhận xét nhóm ${group.code}`}
                      onChange={(e) => setGroupNotes({ ...groupNotes, [group.code]: e.target.value })} />
                    <EvidenceFiles recordId={record.id} slot={evidenceSlot("group", group.code)} label={`nhóm ${group.code} — ${group.name}`}
                      files={evidence.files} rights={evidence.rights} onChanged={evidence.reload} />
                  </td>
                </tr>
                {group.items.map((item) => {
                  const s = general[item.code];
                  const na = !!s.notApplicable;
                  const reasonRequired = needsReason(item, s);
                  return (
                    <tr key={item.code} className="small">
                      <td>{item.code}</td>
                      <td>{item.text}</td>
                      <td>{fmt(item.maxScore)}</td>
                      <td>
                        {group.scoringMode === "Binary" ? (
                          <div className="d-flex flex-wrap gap-2">
                            {[
                              { key: "ok", label: "Đảm bảo", checked: !na && Number(s.score) >= item.maxScore, apply: { notApplicable: false, score: item.maxScore } },
                              { key: "no", label: "Không đảm bảo", checked: !na && Number(s.score) < item.maxScore, apply: { notApplicable: false, score: 0 } },
                              ...(p.allowNotApplicable ? [{ key: "na", label: "Không áp dụng", checked: na, apply: { notApplicable: true, score: 0 } }] : []),
                            ].map((option) => (
                              <label className="form-check form-check-inline mb-0" key={option.key}>
                                <input type="radio" className="form-check-input" name={`gi-${item.code}`} checked={option.checked} onChange={() => setItem(item.code, option.apply)} />
                                <span className="form-check-label">{option.label}</span>
                              </label>
                            ))}
                          </div>
                        ) : (
                          <div className="d-flex gap-2 align-items-center">
                            <input type="number" min={0} max={item.maxScore} step={0.25} disabled={na} className="form-control form-control-sm" style={{ width: 90 }}
                              value={na ? "" : s.score} onChange={(e) => setItem(item.code, { score: Number(e.target.value) })} aria-label={`Điểm tiêu chí ${item.code}`} />
                            {p.allowNotApplicable && (
                              <label className="form-check mb-0">
                                <input type="checkbox" className="form-check-input" checked={na} onChange={(e) => setItem(item.code, { notApplicable: e.target.checked })} />
                                <span className="form-check-label">Không áp dụng</span>
                              </label>
                            )}
                          </div>
                        )}
                      </td>
                      <td>
                        {reasonRequired || s.reason ? (
                          <input className="form-control form-control-sm" maxLength={4000} required={reasonRequired} placeholder={na ? "Lý do không áp dụng" : "Lý do giảm điểm"}
                            value={s.reason || ""} onChange={(e) => setItem(item.code, { reason: e.target.value })} aria-label={`Lý do tiêu chí ${item.code}`} />
                        ) : (
                          <span className="text-secondary">—</span>
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
      <div className="small text-secondary mb-3">
        Cộng tiêu chí chung (tạm tính): <strong>{fmt(generalScore)}</strong> / {fmt(p.generalMaxScore)}
      </div>

      {uses09B ? (
        <>
          <h4 className="h6 small fw-bold text-secondary">II. Kết quả thực hiện nhiệm vụ — chấm trực tiếp theo trục (Mẫu 09B, tối đa {fmt(p.totalTaskWeight)} điểm)</h4>
          <div className="d-flex flex-column gap-2 mb-3">
            {content.axes.map((a) => (
              <div className="border rounded p-2" key={a.code}>
                <div className="row g-2 align-items-end mb-1">
                  <div className="col-12 col-md-9">
                    <label className="form-label small fw-semibold mb-0" htmlFor={`axis-${a.code}`} title={a.description || undefined}>{a.code} — {a.name}</label>
                  </div>
                  <div className="col-12 col-md-3">
                    <div className="input-group input-group-sm">
                      <input id={`axis-${a.code}`} type="number" min={0} max={a.maxScore} step={0.5} className="form-control form-control-sm" value={axis[a.code]} required
                        placeholder="Nhập điểm" onChange={(e) => setAxis({ ...axis, [a.code]: toInput(e.target.value) })} />
                      <span className="input-group-text">/ {fmt(a.maxScore)}</span>
                    </div>
                  </div>
                </div>
                <AxisNoteFields axisCode={a.code} value={axisNotes[a.code] ?? {}} onChange={(note) => setAxisNotes({ ...axisNotes, [a.code]: note })} />
                <EvidenceFiles recordId={record.id} slot={evidenceSlot("axis", a.code)} label={`${a.code} — ${a.name}`}
                  files={evidence.files} rights={evidence.rights} onChanged={evidence.reload} />
              </div>
            ))}
            <div className="small text-secondary">Cộng các trục (tạm tính): <strong>{fmt(tasksScore)}</strong> / {fmt(p.totalTaskWeight)}</div>
          </div>
        </>
      ) : (
        <>
          <h4 className="h6 small fw-bold text-secondary">II. Kết quả thực hiện sản phẩm (Mẫu 02) — % đạt từng thành phần A-B-C-D</h4>
          <div className="small text-secondary mb-1">
            {frame
              ? <>Khung tỷ trọng của hồ sơ: <strong>{frame.code} — {frame.name}</strong>. Tỷ lệ 4 mặt: {describeFrame(frame)}{record.weightFrameAdjusted && <strong> (đã điều chỉnh)</strong>}</>
              : <span className="text-danger">Khung tỷ trọng "{record.weightFrameCode || "—"}" của hồ sơ không có trong bộ tiêu chí — liên hệ người quản lý kỳ.</span>}
          </div>
          <div className="table-responsive mb-2">
            <table className="table table-sm align-middle">
              <thead>
                <tr className="small text-secondary">
                  <th>Sản phẩm</th><th style={{ width: 55 }}>Trục</th><th style={{ width: 65 }}>Trọng số</th><th style={{ width: 75 }}>A %</th><th style={{ width: 75 }}>B %</th><th style={{ width: 75 }}>C %</th><th style={{ width: 75 }}>D %</th><th style={{ width: 70 }}>Điểm</th><th style={{ width: 55 }}>Vượt</th><th style={{ width: 160 }}>Minh chứng</th>
                </tr>
              </thead>
              <tbody>
                {record.tasks.map((t) => {
                  const r = ratios[t.id];
                  if (t.changeStatus) {
                    return (
                      <tr key={t.id} className="text-secondary">
                        <td><div className="small">{t.taskName}<TaskChangeMark task={t} /></div></td>
                        <td className="small">{t.axisCode || "—"}</td>
                        <td className="small">0</td>
                        <td colSpan={7} />
                      </tr>
                    );
                  }
                  return (
                    <tr key={t.id}>
                      <td><div className="small fw-semibold">{t.taskName}</div><div className="small text-secondary">{t.targetOutput}</div></td>
                      <td className="small">{t.axisCode || "—"}</td>
                      <td className="small">{t.weight}</td>
                      {(["a", "b", "c", "d"] as const).map((k) => (
                        <td key={k}>
                          <input type="number" min={0} max={100} step={1} className="form-control form-control-sm" value={r[k]} required title={band(Number(r[k]) || 0)?.label}
                            onChange={(e) => setRatio(t.id, k, toInput(e.target.value))} aria-label={`Thành phần ${k.toUpperCase()} — ${t.taskName}`} />
                        </td>
                      ))}
                      <td className="small fw-semibold">{fmt(taskScore(t.weight, r))}</td>
                      <td className="text-center"><input type="checkbox" className="form-check-input" checked={r.exceed} onChange={(e) => setRatio(t.id, "exceed", e.target.checked)} aria-label="Vượt chuẩn" /></td>
                      <td className="small">
                        {r.attachmentName ? <span className="d-block text-truncate" style={{ maxWidth: 150 }} title={r.attachmentName}><i className="bi bi-paperclip me-1" />{r.attachmentName}</span> : <span className="text-secondary">Chưa có</span>}
                        <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setUploadFor(t.id)}>Đính kèm</button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <details className="small text-secondary mb-3">
            <summary>Thang quy đổi % A-B-C-D</summary>
            <ul className="mb-0">
              {content.conversionScale.map((b) => <li key={b.minPercent}><strong>{b.label}</strong>{b.description ? `: ${b.description}` : ""}</li>)}
            </ul>
          </details>
          <div className="small text-secondary mb-3">Cộng điểm sản phẩm (tạm tính): <strong>{fmt(tasksScore)}</strong> / {fmt(p.totalTaskWeight)}</div>
        </>
      )}

      {uses09C && sections.length > 0 && (
        <>
          <h4 className="h6 small fw-bold text-secondary">Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân</h4>
          <div className="mb-3">
            <SelfAssessmentInput sections={sections} value={selfAssessment} onChange={setSelfAssessment} firstSectionExtra={axisTaskTextLength(blocks)} />
          </div>
        </>
      )}

      {(uses09C || uses9D) && (
        <>
          <h4 className="h6 small fw-bold text-secondary">
            {uses09C && uses9D ? "Nhiệm vụ và kết quả theo trục (Mẫu 09C, 9D)" : uses09C ? "Nhiệm vụ theo trục (Mẫu 09C)" : "Kết quả theo trục (Mẫu 9D)"}
          </h4>
          {(initialAxisReports.taskCount > 0 || initialAxisReports.summaryFieldCount > 0) && (
            <div className="alert alert-info small py-2">
              Đã lấy thông tin từ Mẫu 01. Bổ sung kết quả thực tế.
            </div>
          )}
          <div className="mb-2">
            <AxisReportsInput axes={content.axes} value={axisReports} onChange={setAxisReports} levels={taskLevels(criteria)}
              showTasks={uses09C} showSummary={uses9D} taskLabel={content.taskLineLabel || "Nhiệm vụ {n}:"} />
          </div>
          {uses09C && sections.length > 0 && (
            <div className="mb-3">
              <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setShowPreview((v) => !v)}>
                <i className={`bi ${showPreview ? "bi-eye-slash" : "bi-eye"} me-1`} />{showPreview ? "Ẩn xem trước" : "Xem trước bản in"}
              </button>
              {showPreview && (
                <div className="mt-2">
                  <SelfAssessmentPreview section={sections[0]} text={selfAssessment[sections[0].code]} blocks={blocks} />
                </div>
              )}
            </div>
          )}
        </>
      )}

      <div className="alert alert-light border small py-2">
        Tổng điểm tạm tính: <strong>{fmt(total)}</strong> — mức theo điểm: <strong>{GRADE_LABELS[gradeByScore(content, total)]}</strong>
      </div>

      <div className="row g-2 align-items-end bg-white border-top py-2 mx-0" style={{ position: "sticky", bottom: 0, zIndex: 5 }}>
        <div className="col-md-5">
          <label className="form-label small" htmlFor="self-grade">Mức tự đề xuất</label>
          <select id="self-grade" className="form-select form-select-sm" value={grade} onChange={(e) => setGrade(e.target.value)}>
            <option value="">— Hệ thống gợi ý —</option>
            {GRADE_OPTIONS.filter((g) => g.value !== "ChuaXepLoai").map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
          </select>
        </div>
        <div className="col-md-7 d-flex flex-wrap align-items-center justify-content-end gap-2">
          {(savedAt || dirty) && (
            <span className="small text-secondary" aria-live="polite">
              {savedAt ? `Đã lưu lúc ${timeOf(savedAt)}` : null}
              {savedAt && dirty ? " · " : null}
              {dirty ? "Có thay đổi chưa lưu" : null}
            </span>
          )}
          <button type="button" className="btn btn-outline-secondary" disabled={busy || saving} onClick={saveDraft}>
            {saving ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}Lưu nháp
          </button>
          <button type="submit" className="btn btn-primary" disabled={busy || saving}>Nộp phiếu tự chấm</button>
        </div>
      </div>

      {uploadFor && (
        <FileUploadModal
          isOpen
          onClose={() => setUploadFor(null)}
          formCode="MAU02"
          taskTitle={record.tasks.find((t) => t.id === uploadFor)?.taskName}
          currentAttachmentId={ratios[uploadFor]?.attachmentId}
          onUploadSuccess={(attachment) => {
            setRatios({ ...ratios, [uploadFor]: { ...ratios[uploadFor], attachmentId: attachment.id, attachmentName: attachment.fileName } });
            setUploadFor(null);
          }}
        />
      )}
    </form>
  );
}

export default SelfScoreForm;
