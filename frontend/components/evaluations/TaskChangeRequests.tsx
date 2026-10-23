"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, formatDateTime } from "@/lib/dateFormat";
import { fmt } from "@/services/criteriaService";
import { EvaluationRecordDto, EvaluationTaskDto, TASK_ROLE_LABELS, TaskRole } from "@/services/evaluationService";
import {
  RecordTaskChangesDto,
  TASK_CHANGE_TYPE_LABELS,
  TaskChangeProposal,
  TaskChangeRequestDto,
  TaskChangeType,
  taskChangeService,
} from "@/services/taskChangeService";
import { useCriteria } from "./useCriteria";
import { errorMessage } from "@/components/admin/adminUtils";

const STATUS_CLASS: Record<string, string> = { Pending: "t-wait", Approved: "t-ok", Rejected: "t-bad" };

interface Props {
  record: EvaluationRecordDto;
  /** Gọi khi danh mục việc của hồ sơ thay đổi (đã gửi / duyệt đề nghị) để trang tải lại hồ sơ. */
  onRecordChanged?: () => void;
}

type FormMode = "change" | "urgent";

/** Điều chỉnh việc trong quý: nút theo cờ máy chủ; luật (tổng trọng số, số việc, quyền) do máy chủ kiểm tra. */
export function TaskChangeRequests({ record, onRecordChanged }: Props) {
  const { toast } = useToast();
  const [data, setData] = useState<RecordTaskChangesDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState<FormMode | null>(null);

  const load = useCallback(async () => {
    try {
      setData(await taskChangeService.get(record.id));
    } catch {
      setData(null);
    }
  }, [record.id]);

  useEffect(() => {
    load();
  }, [load, record.version]);

  if (!data || !data.applicable) return null;
  if (data.requests.length === 0 && !data.canRequestChange && !data.canRecordUrgent) return null;

  const run = async (action: () => Promise<unknown>, message: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(message);
      setForm(null);
      await load();
      onRecordChanged?.();
      return true;
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Không thực hiện được thao tác."));
      await load();
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card">
      <div className="card-header d-flex justify-content-between align-items-center gap-2 flex-wrap">
        <h2 className="mb-0">Điều chỉnh việc trong quý</h2>
        {!form && (
          <div className="d-flex gap-2">
            {data.canRequestChange && (
              <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setForm("change")}>
                Đề nghị điều chỉnh
              </button>
            )}
            {data.canRecordUrgent && (
              <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setForm("urgent")}>
                Ghi nhận việc đột xuất
              </button>
            )}
          </div>
        )}
      </div>
      <div className="card-body d-flex flex-column gap-3">
        {data.blockedReason && !form && <div className="small text-secondary">{data.blockedReason}</div>}
        {form && (
          <TaskChangeForm
            record={record}
            mode={form}
            busy={busy}
            onCancel={() => setForm(null)}
            onSubmit={(body) =>
              run(() => taskChangeService.create(record.id, { ...body, version: record.version ?? 0 }), "Đã gửi đề nghị.")
            }
          />
        )}
        {data.requests.map((item) => (
          <RequestItem
            key={item.id}
            item={item}
            busy={busy}
            onApprove={(comment) =>
              run(() => taskChangeService.approve(record.id, item.id, item.version, comment), "Đã duyệt đề nghị, danh mục việc đã cập nhật.")
            }
            onReject={(comment) => run(() => taskChangeService.reject(record.id, item.id, item.version, comment), "Đã không duyệt đề nghị.")}
          />
        ))}
      </div>
    </section>
  );
}

/** Một đề nghị điều chỉnh; nút duyệt khi được phép. */
function RequestItem({
  item,
  busy,
  onApprove,
  onReject,
}: {
  item: TaskChangeRequestDto;
  busy: boolean;
  onApprove: (comment?: string) => Promise<boolean>;
  onReject: (comment: string) => Promise<boolean>;
}) {
  const { toast } = useToast();
  const [deciding, setDeciding] = useState(false);
  const [comment, setComment] = useState("");
  const p = item.proposal ?? {};
  const fields: [string, React.ReactNode][] = [
    ["Tên việc", p.taskName],
    ["Chuẩn đạt", p.targetOutput],
    ["Thời hạn", p.deadline ? formatDate(p.deadline) : null],
    ["Trục", p.axisCode],
    ["Vai trò", p.role ? TASK_ROLE_LABELS[p.role as TaskRole] ?? p.role : null],
    ["Mã SP", p.productCode],
    ["Vượt chuẩn", p.exceedCriteria],
    ["Minh chứng", p.evidenceSource],
    ["Xác nhận", p.confirmer],
  ];
  const shown = fields.filter(([, v]) => v !== null && v !== undefined && v !== "");

  const reject = async () => {
    if (!comment.trim()) {
      toast.error("Hãy nhập lý do không duyệt.");
      return;
    }
    if (await onReject(comment.trim())) setDeciding(false);
  };

  return (
    <div className="border rounded p-3 d-flex flex-column gap-2">
      <div className="d-flex justify-content-between flex-wrap gap-2">
        <div className="small">
          <span className="fw-semibold">{item.typeName}</span>
          {item.taskName && <span> — {item.taskName}</span>}
          <div className="text-secondary">{item.requestedByName} · {formatDateTime(item.requestedAt)}</div>
        </div>
        <span className={`tag ${STATUS_CLASS[item.status] ?? "t-info"}`}>{item.statusName}</span>
      </div>
      {shown.length > 0 && (
        <dl className="small mb-0 row">
          {shown.map(([label, value]) => (
            <React.Fragment key={label}>
              <dt className="col-4 col-md-3 fw-normal text-secondary">{label}</dt>
              <dd className="col-8 col-md-9 mb-1">{value}</dd>
            </React.Fragment>
          ))}
        </dl>
      )}
      {item.weights.length > 0 && (
        <table className="table table-sm small mb-0">
          <thead><tr><th>Việc</th><th className="text-end">Trọng số trước</th><th className="text-end">Sau</th></tr></thead>
          <tbody>
            {item.weights.map((row, i) => (
              <tr key={row.taskId ?? `new-${i}`}>
                <td>{row.taskName}</td>
                <td className="text-end num">{row.before == null ? "—" : fmt(row.before)}</td>
                <td className={`text-end num ${row.before != null && row.before !== row.after ? "fw-semibold" : ""}`}>{fmt(row.after)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="small" style={{ whiteSpace: "pre-wrap" }}><span className="text-secondary">Lý do: </span>{item.reason}</div>
      {item.decidedByName && (
        <div className="small border-start border-3 ps-2">
          <span className="fw-semibold">{item.decidedByName}</span>
          <span className="text-secondary"> · {formatDateTime(item.decidedAt)}</span>
          {item.decisionComment && <div style={{ whiteSpace: "pre-wrap" }}>{item.decisionComment}</div>}
        </div>
      )}
      {item.canDecide && !deciding && (
        <div className="d-flex gap-2 justify-content-end">
          <button type="button" className="btn btn-outline-danger btn-sm" disabled={busy} onClick={() => setDeciding(true)}>Không duyệt</button>
          <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onApprove()}>Duyệt</button>
        </div>
      )}
      {item.canDecide && deciding && (
        <div className="d-flex flex-column gap-2">
          <textarea className="form-control form-control-sm" rows={2} maxLength={4000} value={comment} placeholder="Lý do không duyệt"
            onChange={(e) => setComment(e.target.value)} aria-label="Lý do không duyệt" />
          <div className="d-flex gap-2 justify-content-end">
            <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={() => setDeciding(false)}>Hủy</button>
            <button type="button" className="btn btn-danger btn-sm" disabled={busy} onClick={reject}>Không duyệt</button>
          </div>
        </div>
      )}
    </div>
  );
}

const activeTasks = (tasks: EvaluationTaskDto[]) => tasks.filter((t) => !t.changeStatus);

const MARK_LABELS: Record<string, string> = { Excluded: "Đã loại trừ", CarriedOver: "Chuyển tiếp" };

/** Dấu của việc trong bảng việc: đã loại trừ / chuyển tiếp / đột xuất. */
export function TaskChangeMark({ task }: { task: EvaluationTaskDto }) {
  const label = task.changeStatus ? MARK_LABELS[task.changeStatus] : task.kind === "DotXuat" ? "Đột xuất" : null;
  if (!label) return null;
  return <span className={`tag ${task.changeStatus ? "t-wait" : "t-info"} ms-2`}>{label}</span>;
}

/** Biểu mẫu gửi đề nghị: chọn việc + loại + nội dung mới (hoặc việc đột xuất mới) + bảng trọng số sau điều chỉnh. */
function TaskChangeForm({
  record,
  mode,
  busy,
  onCancel,
  onSubmit,
}: {
  record: EvaluationRecordDto;
  mode: FormMode;
  busy: boolean;
  onCancel: () => void;
  onSubmit: (body: { type: TaskChangeType; taskId?: string | null; proposal?: TaskChangeProposal; weights: Record<string, number>; reason: string }) => void;
}) {
  const { toast } = useToast();
  const { criteria } = useCriteria(record.periodId);
  const axes = criteria?.content.axes ?? [];
  const total = criteria?.content.parameters.totalTaskWeight ?? 70;
  const tolerance = criteria?.content.parameters.taskWeightTolerance ?? 0.05;
  const tasks = useMemo(() => activeTasks(record.tasks), [record.tasks]);

  const [type, setType] = useState<TaskChangeType>(mode === "urgent" ? "Urgent" : "Adjust");
  const [taskId, setTaskId] = useState<string>(tasks[0]?.id ?? "");
  const [proposal, setProposal] = useState<TaskChangeProposal>(mode === "urgent" ? { role: "ChuTri", axisCode: axes[0]?.code } : {});
  const [weights, setWeights] = useState<Record<string, string>>(() => Object.fromEntries(tasks.map((t) => [t.id, String(t.weight)])));
  const [reason, setReason] = useState("");

  const target = tasks.find((t) => t.id === taskId);
  const removes = type === "Exclude" || type === "Carryover";

  // Điều chỉnh: điền sẵn nội dung hiện tại của việc được chọn để sửa.
  useEffect(() => {
    if (type !== "Adjust" || !target) return;
    setProposal({
      taskName: target.taskName,
      targetOutput: target.targetOutput,
      deadline: target.deadline?.slice(0, 10),
      axisCode: target.axisCode ?? undefined,
      role: target.role ?? undefined,
      exceedCriteria: target.exceedCriteria ?? undefined,
      evidenceSource: target.evidenceSource ?? undefined,
      confirmer: target.confirmer ?? undefined,
    });
  }, [type, target]);

  const sum =
    tasks.reduce((s, t) => s + (removes && t.id === taskId ? 0 : Number(weights[t.id]) || 0), 0) +
    (type === "Urgent" ? Number(proposal.weight) || 0 : 0);
  const off = Math.abs(sum - total) > tolerance;

  const set = (key: keyof TaskChangeProposal, value: string) => setProposal((p) => ({ ...p, [key]: value }));

  const submit = () => {
    if (!reason.trim()) {
      toast.error("Hãy nhập lý do / nguyên nhân.");
      return;
    }
    const map: Record<string, number> = {};
    for (const t of tasks) if (!(removes && t.id === taskId)) map[t.id] = Number(weights[t.id]) || 0;
    const body: TaskChangeProposal | undefined =
      type === "Urgent"
        ? { ...proposal, weight: Number(proposal.weight) || 0, deadline: proposal.deadline || null }
        : type === "Adjust"
          ? { ...proposal, deadline: proposal.deadline || null }
          : undefined;
    onSubmit({ type, taskId: type === "Urgent" ? null : taskId, proposal: body, weights: map, reason: reason.trim() });
  };

  const input = (key: keyof TaskChangeProposal, label: string, multiline = false) => (
    <div className={multiline ? "col-12" : "col-12 col-md-6"}>
      <label className="form-label small mb-1">{label}</label>
      {multiline ? (
        <textarea className="form-control form-control-sm" rows={2} value={(proposal[key] as string) ?? ""} onChange={(e) => set(key, e.target.value)} />
      ) : (
        <input className="form-control form-control-sm" value={(proposal[key] as string) ?? ""} onChange={(e) => set(key, e.target.value)} />
      )}
    </div>
  );

  return (
    <div className="border rounded p-3 d-flex flex-column gap-3">
      {mode === "change" && (
        <div className="row g-2">
          <div className="col-12 col-md-6">
            <label className="form-label small mb-1" htmlFor="tc-task">Việc</label>
            <select id="tc-task" className="form-select form-select-sm" value={taskId} onChange={(e) => setTaskId(e.target.value)}>
              {tasks.map((t) => <option key={t.id} value={t.id}>{t.taskOrder}. {t.taskName}</option>)}
            </select>
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small mb-1" htmlFor="tc-type">Loại đề nghị</label>
            <select id="tc-type" className="form-select form-select-sm" value={type} onChange={(e) => setType(e.target.value as TaskChangeType)}>
              {(["Adjust", "Carryover", "Exclude"] as TaskChangeType[]).map((t) => <option key={t} value={t}>{TASK_CHANGE_TYPE_LABELS[t]}</option>)}
            </select>
          </div>
        </div>
      )}

      {(type === "Adjust" || type === "Urgent") && (
        <div className="row g-2">
          {input("taskName", "Tên việc", true)}
          {input("targetOutput", "Chuẩn đạt", true)}
          {axes.length > 0 && (
            <div className="col-6 col-md-3">
              <label className="form-label small mb-1">Trục</label>
              <select className="form-select form-select-sm" value={proposal.axisCode ?? ""} onChange={(e) => set("axisCode", e.target.value)}>
                {axes.map((a) => <option key={a.code} value={a.code}>{a.code}</option>)}
              </select>
            </div>
          )}
          <div className="col-6 col-md-3">
            <label className="form-label small mb-1">Vai trò</label>
            <select className="form-select form-select-sm" value={proposal.role ?? ""} onChange={(e) => set("role", e.target.value)}>
              {(Object.keys(TASK_ROLE_LABELS) as TaskRole[]).map((r) => <option key={r} value={r}>{TASK_ROLE_LABELS[r]}</option>)}
            </select>
          </div>
          <div className="col-6 col-md-3">
            <label className="form-label small mb-1">Thời hạn</label>
            <input type="date" className="form-control form-control-sm" value={proposal.deadline?.slice(0, 10) ?? ""} onChange={(e) => set("deadline", e.target.value)} />
          </div>
          {type === "Urgent" && (
            <div className="col-6 col-md-3">
              <label className="form-label small mb-1">Trọng số</label>
              <input type="number" min={0} step={0.5} className="form-control form-control-sm" value={proposal.weight ?? ""}
                onChange={(e) => setProposal((p) => ({ ...p, weight: e.target.value === "" ? null : Number(e.target.value) }))} />
            </div>
          )}
          {input("exceedCriteria", "Tiêu chí vượt chuẩn")}
          {input("evidenceSource", "Nguồn minh chứng")}
          {input("confirmer", "Thẩm quyền xác nhận")}
        </div>
      )}

      <div>
        <div className="small fw-semibold mb-1">Trọng số sau điều chỉnh</div>
        <table className="table table-sm small mb-0">
          <thead><tr><th>Việc</th><th className="text-end">Hiện tại</th><th className="text-end" style={{ width: 110 }}>Sau</th></tr></thead>
          <tbody>
            {tasks.map((t) => (
              <tr key={t.id}>
                <td>{t.taskName}</td>
                <td className="text-end num">{fmt(t.weight)}</td>
                <td className="text-end">
                  {removes && t.id === taskId ? (
                    <span className="num">0</span>
                  ) : (
                    <input type="number" min={0} step={0.5} className="form-control form-control-sm text-end" value={weights[t.id] ?? ""}
                      aria-label={`Trọng số sau điều chỉnh — ${t.taskName}`}
                      onChange={(e) => setWeights((w) => ({ ...w, [t.id]: e.target.value }))} />
                  )}
                </td>
              </tr>
            ))}
            {type === "Urgent" && (
              <tr>
                <td>{proposal.taskName || "Việc đột xuất"}</td>
                <td className="text-end">—</td>
                <td className="text-end num">{fmt(Number(proposal.weight) || 0)}</td>
              </tr>
            )}
            <tr className="fw-semibold">
              <td>Tổng</td>
              <td />
              <td className={`text-end num ${off ? "text-danger" : ""}`}>{fmt(sum)} / {fmt(total)}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div>
        <label className="form-label small mb-1" htmlFor="tc-reason">Lý do / nguyên nhân</label>
        <textarea id="tc-reason" className="form-control form-control-sm" rows={3} maxLength={4000} value={reason} onChange={(e) => setReason(e.target.value)} />
      </div>

      <div className="d-flex gap-2 justify-content-end">
        <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={onCancel}>Hủy</button>
        <button type="button" className="btn btn-primary btn-sm" disabled={busy || off} onClick={submit}>Gửi đề nghị</button>
      </div>
    </div>
  );
}
