"use client";

import { formatDate } from "@/lib/dateFormat";
import React, { useCallback, useEffect, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { EvaluationRecordDto } from "@/services/evaluationService";
import {
  ImprovementMilestoneDto,
  ImprovementPlanDto,
  MilestoneCode,
  MilestoneInput,
  MilestoneResult,
  RecordImprovementPlanDto,
  milestoneResultLabel,
  postPublishService,
} from "@/services/postPublishService";
import { errorMessage } from "@/components/admin/adminUtils";
import { DateField } from "./DateField";
import { RowActions } from "@/components/common/RowActions";
import styles from "./record.module.css";

const MILESTONES: { code: MilestoneCode; name: string }[] = [
  { code: "M30", name: "Mốc 30 ngày (Khắc phục cấp bách)" },
  { code: "M60", name: "Mốc 60 ngày (Cải thiện hiệu suất)" },
  { code: "M90", name: "Mốc 90 ngày (Đánh giá chuyển biến)" },
];

/** Cột của bảng Mẫu 17. */
const COLUMNS: { key: keyof Omit<MilestoneInput, "code">; label: string }[] = [
  { key: "limitation", label: "Hạn chế cần khắc phục" },
  { key: "target", label: "Mục tiêu/sản phẩm" },
  { key: "measures", label: "Biện pháp & đào tạo hỗ trợ" },
  { key: "coordination", label: "Phối hợp/giám sát" },
];

interface Draft {
  supporterName: string;
  supporterTitle: string;
  startDate: string;
  milestones: MilestoneInput[];
}

const toDraft = (plan?: ImprovementPlanDto | null): Draft => ({
  supporterName: plan?.supporterName ?? "",
  supporterTitle: plan?.supporterTitle ?? "",
  startDate: plan?.startDate ?? "",
  milestones: MILESTONES.map(({ code }) => {
    const m = plan?.milestones.find((x) => x.code === code);
    return { code, limitation: m?.limitation ?? "", target: m?.target ?? "", measures: m?.measures ?? "", coordination: m?.coordination ?? "" };
  }),
});

/** Kế hoạch 30-60-90 ngày (Mẫu 17) của hồ sơ đã công bố; mức bắt buộc theo bộ tiêu chí của kỳ. */
export function ImprovementPlanPanel({ record, onPendingChange }: { record: EvaluationRecordDto; onPendingChange?: (pending: boolean) => void }) {
  const { toast } = useToast();
  const [data, setData] = useState<RecordImprovementPlanDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Draft>(toDraft());
  const [busy, setBusy] = useState(false);
  const [ackComment, setAckComment] = useState("");

  const load = useCallback(async () => {
    try {
      setData(await postPublishService.getRecordPlan(record.id));
      setError(null);
    } catch (err: unknown) {
      setError(errorMessage(err, "Không tải được kế hoạch 30-60-90 ngày."));
    }
  }, [record.id]);

  useEffect(() => {
    load();
  }, [load, record.status, record.version]);

  // Người xem đang phải lập/duyệt hoặc xác nhận kế hoạch → trang đưa khối lên gần khu thao tác.
  const plan = data?.plan ?? null;
  const pending = !!data && (data.canAcknowledge || data.canApprove || (data.canManage && !plan && data.required));
  useEffect(() => {
    onPendingChange?.(pending);
  }, [pending, onPendingChange]);

  if (!data && !error) return null;
  if (data && !data.isPublished && !data.plan) return null;
  if (data && !data.required && !data.plan && !data.canManage) return null;

  const run = async (action: () => Promise<unknown>, message: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(message);
      await load();
      return true;
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Không thực hiện được thao tác."));
      await load();
      return false;
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    const input = {
      version: plan?.version,
      supporterName: draft.supporterName,
      supporterTitle: draft.supporterTitle,
      startDate: draft.startDate || null,
      milestones: draft.milestones,
    };
    const ok = await run(
      () => (plan ? postPublishService.updatePlan(plan.id, input) : postPublishService.createPlan(record.id, input)),
      "Đã lưu kế hoạch."
    );
    if (ok) setEditing(false);
  };

  const download = (format: "original" | "pdf") => {
    if (!plan) return;
    postPublishService.downloadMau17(plan.id, record.fullName, format)
      .catch((err: unknown) => toast.error(errorMessage(err, "Không xuất được Mẫu 17.")));
  };

  const updateMilestone = (code: MilestoneCode, key: keyof Omit<MilestoneInput, "code">, value: string) =>
    setDraft({ ...draft, milestones: draft.milestones.map((m) => (m.code === code ? { ...m, [key]: value } : m)) });

  return (
    <section className={`card ${pending ? styles.pendingCard : ""}`}>
      <div className="card-header d-flex justify-content-between align-items-center flex-wrap gap-2">
        <h2 className="mb-0 d-flex align-items-center gap-2">
          Kế hoạch 30-60-90 ngày (Mẫu 17)
          {plan && <span className="tag t-info">{plan.statusName}</span>}
        </h2>
        {plan && (
          <RowActions
            itemLabel="Kế hoạch Mẫu 17"
            primary={[{ key: "word", label: "Xuất Word", icon: "bi-download", onClick: () => download("original") }]}
            menu={[{ key: "pdf", label: "Xuất PDF", icon: "bi-file-earmark-pdf", onClick: () => download("pdf") }]}
          />
        )}
      </div>
      <div className="card-body d-flex flex-column gap-3">
        {error && <div className="alert alert-warning mb-0 small">{error}</div>}
        {data?.required && (
          <div className={`alert ${plan ? "alert-info" : "alert-warning"} mb-0 small`}>
            Bắt buộc lập kế hoạch với mức &quot;{data.finalGradeName}&quot;.
            {!plan && " Hồ sơ chưa có kế hoạch."}
          </div>
        )}

        {data?.canManage && !plan && !editing && (
          <div>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => { setDraft(toDraft()); setEditing(true); }}>
              <i className="bi bi-plus-lg me-1" />Lập kế hoạch
            </button>
          </div>
        )}

        {/* Việc của người xem (duyệt, xác nhận) đặt trước bảng dài để không phải cuộn tìm. */}
        {plan && !editing && ((data?.canManage && plan.status === "Draft") || data?.canAcknowledge) && (
          <div className="d-flex gap-2 flex-wrap align-items-center">
            {data?.canManage && plan.status === "Draft" && (
              <>
                <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy}
                  onClick={() => { setDraft(toDraft(plan)); setEditing(true); }}>Sửa kế hoạch</button>
                {data.canApprove ? (
                  <button type="button" className="btn btn-primary btn-sm" disabled={busy}
                    onClick={() => run(() => postPublishService.approvePlan(plan.id, plan.version), "Đã duyệt kế hoạch; chờ cá nhân xác nhận.")}>
                    Duyệt kế hoạch
                  </button>
                ) : (
                  <span className="small text-secondary">Kế hoạch do bạn lập, chờ người khác duyệt.</span>
                )}
              </>
            )}
            {data?.canAcknowledge && (
              <>
                <input className="form-control form-control-sm w-auto flex-grow-1" placeholder="Ý kiến của cá nhân (tùy chọn)" maxLength={2000}
                  value={ackComment} onChange={(e) => setAckComment(e.target.value)} aria-label="Ý kiến khi xác nhận" />
                <button type="button" className="btn btn-primary btn-sm" disabled={busy}
                  onClick={() => run(() => postPublishService.acknowledgePlan(plan.id, plan.version, ackComment || undefined), "Đã xác nhận cam kết khắc phục.")}>
                  Xác nhận cam kết khắc phục
                </button>
              </>
            )}
          </div>
        )}

        {editing ? (
          <div className="d-flex flex-column gap-2">
            <div className="row g-2">
              <div className="col-12 col-md-5">
                <label className="form-label small mb-1" htmlFor="plan-supporter">Người trực tiếp hỗ trợ, giám sát</label>
                <input id="plan-supporter" className="form-control form-control-sm" maxLength={200} value={draft.supporterName}
                  onChange={(e) => setDraft({ ...draft, supporterName: e.target.value })} />
              </div>
              <div className="col-12 col-md-4">
                <label className="form-label small mb-1" htmlFor="plan-supporter-title">Chức vụ</label>
                <input id="plan-supporter-title" className="form-control form-control-sm" maxLength={200} value={draft.supporterTitle}
                  onChange={(e) => setDraft({ ...draft, supporterTitle: e.target.value })} />
              </div>
              <div className="col-12 col-md-3">
                <label className="form-label small mb-1" htmlFor="plan-start">Ngày bắt đầu</label>
                <DateField id="plan-start" value={draft.startDate} onChange={(v) => setDraft({ ...draft, startDate: v })} />
              </div>
            </div>
            {MILESTONES.map(({ code, name }) => {
              const m = draft.milestones.find((x) => x.code === code)!;
              return (
                <fieldset key={code} className="border rounded p-2">
                  <legend className="small fw-semibold float-none w-auto px-1 mb-0">{name}</legend>
                  <div className="row g-2">
                    {COLUMNS.map((c) => (
                      <div key={c.key} className="col-12 col-md-6">
                        <label className="form-label small mb-1" htmlFor={`${code}-${c.key}`}>{c.label}</label>
                        <textarea id={`${code}-${c.key}`} className="form-control form-control-sm" rows={2} maxLength={2000}
                          value={m[c.key] ?? ""} onChange={(e) => updateMilestone(code, c.key, e.target.value)} />
                      </div>
                    ))}
                  </div>
                </fieldset>
              );
            })}
            <div className="d-flex gap-2 justify-content-end">
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={() => setEditing(false)}>Hủy</button>
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={save}>Lưu kế hoạch</button>
            </div>
          </div>
        ) : plan && (
          <PlanView plan={plan} canManage={!!data?.canManage} busy={busy}
            onRecordResult={(code, result, note) =>
              run(() => postPublishService.recordMilestoneResult(plan.id, plan.version, code, result, note), "Đã ghi kết quả mốc.")} />
        )}

      </div>
    </section>
  );
}

function PlanView({
  plan,
  canManage,
  busy,
  onRecordResult,
}: {
  plan: ImprovementPlanDto;
  canManage: boolean;
  busy: boolean;
  onRecordResult: (code: MilestoneCode, result: MilestoneResult, note?: string) => Promise<boolean>;
}) {
  const canRecord = canManage && (plan.status === "Approved" || plan.status === "Acknowledged");
  return (
    <div className="d-flex flex-column gap-2">
      <div className="row g-2 small">
        <div className="col-6 col-md-4"><div className="text-secondary">Người hỗ trợ, giám sát</div><div className="fw-semibold">{plan.supporterName || "—"}{plan.supporterTitle ? ` (${plan.supporterTitle})` : ""}</div></div>
        <div className="col-6 col-md-4"><div className="text-secondary">Ngày bắt đầu</div><div className="fw-semibold">{formatDate(plan.startDate)}</div></div>
        <div className="col-6 col-md-4"><div className="text-secondary">Duyệt</div><div className="fw-semibold">{plan.approvedByName ? `${plan.approvedByName} · ${formatDate(plan.approvedAt)}` : "—"}</div></div>
        <div className="col-6 col-md-4"><div className="text-secondary">Cá nhân xác nhận</div><div className="fw-semibold">{plan.acknowledgedAt ? formatDate(plan.acknowledgedAt) : "—"}</div></div>
        {plan.acknowledgementComment && <div className="col-12 col-md-8"><div className="text-secondary">Ý kiến của cá nhân</div><div>{plan.acknowledgementComment}</div></div>}
      </div>
      <div className="table-responsive">
        <table className="table table-sm small align-top mb-0">
          <thead>
            <tr><th>Giai đoạn</th>{COLUMNS.map((c) => <th key={c.key}>{c.label}</th>)}<th>Kết quả sau mốc</th></tr>
          </thead>
          <tbody>
            {plan.milestones.map((m) => (
              <tr key={m.code}>
                <td className="fw-semibold">{m.name}<div className="text-secondary fw-normal">Hạn: {formatDate(m.dueDate)}</div></td>
                {COLUMNS.map((c) => <td key={c.key} style={{ whiteSpace: "pre-wrap" }}>{m[c.key] || "—"}</td>)}
                <td>
                  <MilestoneResultCell milestone={m} canRecord={canRecord} busy={busy} onRecord={onRecordResult} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function MilestoneResultCell({
  milestone,
  canRecord,
  busy,
  onRecord,
}: {
  milestone: ImprovementMilestoneDto;
  canRecord: boolean;
  busy: boolean;
  onRecord: (code: MilestoneCode, result: MilestoneResult, note?: string) => Promise<boolean>;
}) {
  const [result, setResult] = useState<MilestoneResult | "">(milestone.result ?? "");
  const [note, setNote] = useState(milestone.resultNote ?? "");
  return (
    <div className="d-flex flex-column gap-1" style={{ minWidth: 170 }}>
      <div className={milestone.result ? "fw-semibold" : "text-secondary"}>{milestone.resultName}</div>
      {milestone.resultNote && <div className="text-secondary">{milestone.resultNote}</div>}
      {milestone.resultRecordedByName && <div className="text-secondary">{milestone.resultRecordedByName} · {formatDate(milestone.resultRecordedAt)}</div>}
      {canRecord && (
        <>
          <select className="form-select form-select-sm" value={result} aria-label={`Kết quả ${milestone.name}`}
            onChange={(e) => setResult(e.target.value as MilestoneResult | "")}>
            <option value="">— Chọn kết quả —</option>
            {(["Achieved", "NotAchieved"] as MilestoneResult[]).map((r) => (
              <option key={r} value={r}>{milestoneResultLabel(milestone.code, r)}</option>
            ))}
          </select>
          <input className="form-control form-control-sm" placeholder="Ghi chú, minh chứng" maxLength={2000} value={note}
            onChange={(e) => setNote(e.target.value)} aria-label={`Ghi chú ${milestone.name}`} />
          <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy || !result}
            onClick={() => result && onRecord(milestone.code, result, note || undefined)}>
            {milestone.code === "M90" ? "Ghi kết quả & đóng kế hoạch" : "Ghi kết quả"}
          </button>
        </>
      )}
    </div>
  );
}

export default ImprovementPlanPanel;
