"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { HeaderMenu } from "@/components/common/HeaderMenu";
import { EmptyState } from "@/components/common/EmptyState";
import { Badge, GradeBadge } from "@/components/common/StatusBadge";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { RecordPickerDialog, PickerRow } from "@/components/quality/RecordPickerDialog";
import { OwnerAttachments } from "@/components/attachments/OwnerAttachments";
import { errorMessage } from "@/components/admin/adminUtils";
import { useToast } from "@/contexts/ToastContext";
import { formatDateTime } from "@/lib/dateFormat";
import { gradeLabel } from "@/services/evaluationService";
import { reportService } from "@/services/reportService";
import {
  CONTROL_ACTION_LABELS,
  CONTROL_CONCLUSION_LABELS,
  CONTROL_RESULT_LABELS,
  CONTROL_STATUS_TONES,
  ControlCheckConclusion,
  ControlCheckDto,
  ControlCheckResult,
  controlCheckService,
} from "@/services/controlCheckService";

interface RecRow {
  recordId: string;
  fullName: string;
  unit: string;
  grade: string;
  score: number;
  isCurrent: boolean;
  isStale: boolean;
}

interface Draft {
  groupName: string;
  records: RecRow[];
  conclusion: ControlCheckConclusion | "";
  needsWorkContent: string;
  items: { itemCode: string; result: ControlCheckResult | ""; evidence: string }[];
}

const toDraft = (c: ControlCheckDto): Draft => ({
  groupName: c.groupName ?? "",
  records: c.records.map((r) => ({
    recordId: r.recordId,
    fullName: r.fullName,
    unit: [r.departmentName, r.partyCellName].filter(Boolean).join(" · "),
    grade: r.gradeAtCheck,
    score: r.scoreAtCheck,
    isCurrent: r.isCurrent,
    isStale: r.isStale,
  })),
  conclusion: c.conclusion ?? "",
  needsWorkContent: c.needsWorkContent ?? "",
  items: c.items.map((i) => ({ itemCode: i.itemCode, result: i.result ?? "", evidence: i.evidence ?? "" })),
});

type Dialog = "return" | "reopen" | "cancel" | null;

const DIALOGS: Record<Exclude<Dialog, null>, { title: string; description: string; confirmText: string }> = {
  return: { title: "Trả lại phiếu", description: "Phiếu quay về người lập để sửa.", confirmText: "Trả lại" },
  reopen: { title: "Mở lại phiếu", description: "Phiếu đã xác nhận quay về bản nháp; hồ sơ cần được xác nhận lại trước khi trình.", confirmText: "Mở lại" },
  cancel: { title: "Hủy phiếu", description: "Các hồ sơ trong phiếu được giải phóng để lập phiếu khác.", confirmText: "Hủy phiếu" },
};

/** Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán: soạn, gửi xác nhận, xác nhận; nút theo cờ do máy chủ trả về. */
export default function ControlCheckDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast, confirm } = useToast();
  const [data, setData] = useState<ControlCheckDto | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [adding, setAdding] = useState(false);
  const [pickerRows, setPickerRows] = useState<PickerRow[]>([]);
  const [pickerLoading, setPickerLoading] = useState(false);
  const [candidateInfo, setCandidateInfo] = useState<Record<string, RecRow>>({});

  const apply = useCallback((c: ControlCheckDto) => {
    setData(c);
    setDraft(toDraft(c));
  }, []);

  const load = useCallback(async () => {
    try {
      apply(await controlCheckService.get(id));
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải phiếu kiểm soát không thành công."));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [id, apply, toast]);

  useEffect(() => {
    load();
  }, [load]);

  const dirty = useMemo(() => !!data && !!draft && JSON.stringify(draft) !== JSON.stringify(toDraft(data)), [data, draft]);

  const body = (version: number) => ({
    version,
    groupName: draft?.groupName.trim() ?? "",
    recordIds: (draft?.records ?? []).map((r) => r.recordId),
    items: (draft?.items ?? []).map((i) => ({ itemCode: i.itemCode, result: i.result || null, evidence: i.evidence.trim() || null })),
    conclusion: draft?.conclusion ?? "",
    needsWorkContent: draft?.needsWorkContent.trim() ?? "",
  });

  /** Chạy lệnh ghi; lỗi (kể cả lệch phiên bản) hiện qua toast và tải lại phiếu. */
  const run = async (work: () => Promise<ControlCheckDto>, success: string, failure: string): Promise<boolean> => {
    setBusy(true);
    try {
      apply(await work());
      toast.success(success);
      return true;
    } catch (err: unknown) {
      toast.error(errorMessage(err, failure));
      await load();
      return false;
    } finally {
      setBusy(false);
    }
  };

  const save = () => data && run(() => controlCheckService.update(data.id, body(data.version)), "Đã lưu phiếu kiểm soát.", "Lưu phiếu kiểm soát không thành công.");

  const submit = () => {
    if (!data) return;
    confirm({
      title: "Gửi xác nhận",
      message: "Phiếu sẽ chuyển cho người xác nhận; người lập không sửa được nữa.",
      confirmText: "Gửi xác nhận",
      isDanger: false,
      onConfirm: async () => {
        let version = data.version;
        if (dirty) {
          try {
            version = (await controlCheckService.update(data.id, body(data.version))).version;
          } catch (err: unknown) {
            toast.error(errorMessage(err, "Lưu phiếu kiểm soát không thành công."));
            await load();
            return;
          }
        }
        await run(() => controlCheckService.submit(data.id, version), "Đã gửi phiếu chờ xác nhận.", "Gửi xác nhận không thành công.");
      },
    });
  };

  const confirmIt = () => {
    if (!data) return;
    confirm({
      title: "Xác nhận phiếu",
      message: "Phiếu được xác nhận sẽ không sửa được nữa.",
      confirmText: "Xác nhận",
      isDanger: false,
      onConfirm: async () => {
        await run(() => controlCheckService.confirm(data.id, data.version), "Đã xác nhận phiếu kiểm soát.", "Xác nhận phiếu không thành công.");
      },
    });
  };

  const reasonDone = async (reason: string) => {
    if (!data || !dialog) return;
    const ok =
      dialog === "return"
        ? await run(() => controlCheckService.returnBack(data.id, data.version, reason), "Đã trả lại phiếu.", "Trả lại phiếu không thành công.")
        : dialog === "reopen"
          ? await run(() => controlCheckService.reopen(data.id, data.version, reason), "Đã mở lại phiếu.", "Mở lại phiếu không thành công.")
          : await run(() => controlCheckService.cancel(data.id, data.version, reason), "Đã hủy phiếu.", "Hủy phiếu không thành công.");
    if (ok) setDialog(null);
  };

  const exportWord = async () => {
    if (!data) return;
    try {
      await reportService.exportQualityForm("19", data.id, `Mau_19_${data.id}.docx`);
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Xuất phiếu kiểm soát không thành công."));
    }
  };

  const openPicker = async () => {
    if (!data || !draft) return;
    setAdding(true);
    setPickerLoading(true);
    setPickerRows([]);
    try {
      const rows = await controlCheckService.candidates(data.periodId);
      const taken = new Set(draft.records.map((r) => r.recordId));
      const free = rows.filter((r) => !taken.has(r.recordId));
      setCandidateInfo(
        Object.fromEntries(
          free.map((r) => [
            r.recordId,
            {
              recordId: r.recordId,
              fullName: r.fullName,
              unit: [r.departmentName, r.partyCellName].filter(Boolean).join(" · "),
              grade: r.grade,
              score: r.score,
              isCurrent: true,
              isStale: false,
            } as RecRow,
          ])
        )
      );
      setPickerRows(
        free.map((r) => ({
          id: r.recordId,
          name: r.fullName,
          detail: [r.departmentName, r.partyCellName, `Mức hiện tại: ${gradeLabel(r.grade)} (${r.score})`].filter(Boolean).join(" · "),
          group: r.workflowProfileName,
          unit: r.departmentName || r.partyCellName || undefined,
        }))
      );
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải danh sách hồ sơ không thành công."));
      setAdding(false);
    } finally {
      setPickerLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="page-wrapper">
        <div className="page-body text-secondary loading-delay">
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải...
        </div>
      </div>
    );
  }
  if (!data || !draft) {
    return (
      <div className="page-wrapper">
        <PageHeader title="Phiếu kiểm soát" />
        <div className="page-body">
          <EmptyState icon="bi-file-earmark-x" title="Chưa mở được phiếu" description="Phiếu không tồn tại hoặc bạn không có quyền xem." />
        </div>
      </div>
    );
  }

  const ro = !data.canEdit;
  const primary = data.canConfirm ? { label: "Xác nhận", onClick: confirmIt } : data.canSubmit ? { label: "Gửi xác nhận", onClick: submit } : null;
  const addRecords = (ids: string[]) => {
    const rows = ids.map((i) => candidateInfo[i]).filter((r): r is RecRow => !!r);
    setDraft({ ...draft, records: [...draft.records, ...rows] });
    setAdding(false);
  };
  const removeRecord = (rid: string) => setDraft({ ...draft, records: draft.records.filter((r) => r.recordId !== rid) });
  const setItem = (code: string, patch: Partial<Draft["items"][number]>) =>
    setDraft((d) => (d ? { ...d, items: d.items.map((i) => (i.itemCode === code ? { ...i, ...patch } : i)) } : d));

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Phiếu kiểm soát"
        subTitle={`${data.groupName || "—"} · ${data.periodName}`}
        badge={
          <span className="d-inline-flex gap-1">
            <Badge tone={CONTROL_STATUS_TONES[data.status] ?? "neutral"}>{data.statusName}</Badge>
            {data.isStale && <Badge tone="danger">Hết hiệu lực</Badge>}
          </span>
        }
        actions={
          <div className="d-flex gap-2 align-items-center">
            {primary && (
              <button type="button" className="btn btn-primary" disabled={busy} onClick={primary.onClick}>
                {primary.label}
              </button>
            )}
            <HeaderMenu
              itemLabel="phiếu kiểm soát"
              disabled={busy}
              actions={[
                { key: "word", label: "Xuất Word", icon: "bi-download", onClick: exportWord },
                { key: "return", label: "Trả lại", icon: "bi-arrow-return-left", hidden: !data.canReturn, onClick: () => setDialog("return") },
                { key: "reopen", label: "Mở lại", icon: "bi-arrow-counterclockwise", hidden: !data.canReopen, onClick: () => setDialog("reopen") },
                { key: "cancel", label: "Hủy phiếu", icon: "bi-x-circle", danger: true, hidden: !data.canCancel, onClick: () => setDialog("cancel") },
              ]}
            />
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        <div>
          <Link href="/quality-control?tab=control" className="small text-decoration-none">
            <i className="bi bi-chevron-left" aria-hidden="true" /> Danh sách phiếu
          </Link>
        </div>

        {data.isStale && (
          <div className="alert alert-danger small mb-0" role="alert">
            Phiếu hết hiệu lực với các hồ sơ có nhãn "Hết hiệu lực" bên dưới: hồ sơ đó đã đổi kết quả hoặc bước sau khi xác nhận. Hãy mở lại phiếu và xác nhận lại; hồ sơ đã ghi nhận quyết định không bị ảnh hưởng.
          </div>
        )}
        {data.returnReason && data.status === "Draft" && <div className="alert alert-warning small mb-0">Phiếu bị trả lại: {data.returnReason}</div>}

        {!ro && (
          <div className="d-flex justify-content-end align-items-center gap-2">
            {dirty && <span className="text-secondary small me-auto">Có thay đổi chưa lưu</span>}
            <button type="button" className="btn btn-outline-secondary" disabled={busy || !dirty} onClick={() => setDraft(toDraft(data))}>Hủy</button>
            <button type="button" className="btn btn-outline-primary" disabled={busy || !dirty} onClick={save}>{busy ? "Đang lưu..." : "Lưu"}</button>
          </div>
        )}

        <section className="card">
          <div className="card-header"><h2 className="mb-0">Phạm vi</h2></div>
          <div className="card-body d-flex flex-column gap-3">
            <div>
              <label className="form-label small mb-1" htmlFor="cc19-group">Nhóm chức danh / phạm vi rà soát</label>
              <input id="cc19-group" className="form-control form-control-sm" maxLength={300} value={draft.groupName} disabled={ro}
                onChange={(e) => setDraft({ ...draft, groupName: e.target.value })} />
            </div>
            <div className="small text-secondary">
              Người lập: {data.performedByName}
              {data.performedAt ? ` · ${formatDateTime(data.performedAt)}` : ""}
              {data.confirmedByName ? ` · Xác nhận: ${data.confirmedByName}${data.confirmedAt ? ` · ${formatDateTime(data.confirmedAt)}` : ""}` : ""}
            </div>
            <div>
              <div className="d-flex justify-content-between align-items-center gap-2 mb-1">
                <div className="fw-semibold small">Hồ sơ trong phiếu ({draft.records.length})</div>
                {!ro && <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={openPicker}>Thêm hồ sơ</button>}
              </div>
              <div className="d-flex flex-column gap-1">
                {draft.records.map((r) => (
                  <div key={r.recordId} className="d-flex flex-wrap justify-content-between gap-2 small border rounded px-2 py-1" style={r.isCurrent ? undefined : { opacity: 0.6 }}>
                    <span>
                      <Link href={`/evaluations/${r.recordId}`} className="fw-semibold text-decoration-none">{r.fullName}</Link>
                      <span className="text-secondary"> · {r.unit || "—"}</span>
                      {!r.isCurrent && <span className="text-secondary"> · đã chuyển sang phiếu khác</span>}
                      {r.isCurrent && r.isStale && <span className="ms-2"><Badge tone="danger">Hết hiệu lực</Badge></span>}
                    </span>
                    <span className="d-inline-flex gap-2 align-items-center">
                      <GradeBadge grade={r.grade} />
                      <span className="num">{r.score}</span>
                      {!ro && draft.records.length > 1 && (
                        <button type="button" className="btn btn-link btn-sm text-danger p-0" disabled={busy} onClick={() => removeRecord(r.recordId)}>Gỡ</button>
                      )}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section className="card">
          <div className="card-header"><h2 className="mb-0">Nội dung kiểm soát</h2></div>
          <div className="card-body d-flex flex-column gap-3">
            {data.items.map((item, index) => {
              const d = draft.items.find((x) => x.itemCode === item.itemCode);
              if (!d) return null;
              const key = `cc19-${item.itemCode}`;
              return (
                <div key={item.itemCode} className="border rounded p-3 d-flex flex-column gap-2">
                  <div className="small fw-semibold">{index + 1}. {item.itemText}</div>
                  <div className="d-flex flex-wrap gap-3" role="radiogroup" aria-label={`Kết quả — ${item.itemCode}`}>
                    {(Object.keys(CONTROL_RESULT_LABELS) as ControlCheckResult[]).map((r) => (
                      <div className="form-check" key={r}>
                        <input id={`${key}-${r}`} type="radio" name={key} className="form-check-input" checked={d.result === r} disabled={ro || busy}
                          onChange={() => setItem(item.itemCode, { result: r })} />
                        <label className="form-check-label small" htmlFor={`${key}-${r}`}>{CONTROL_RESULT_LABELS[r]}</label>
                      </div>
                    ))}
                  </div>
                  <div>
                    <label className="form-label small mb-1" htmlFor={`${key}-ev`}>Minh chứng / biện pháp</label>
                    <textarea id={`${key}-ev`} className="form-control form-control-sm" rows={2} maxLength={4000} value={d.evidence} disabled={ro || busy}
                      onChange={(e) => setItem(item.itemCode, { evidence: e.target.value })} />
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        <section className="card">
          <div className="card-header"><h2 className="mb-0">Kết luận</h2></div>
          <div className="card-body d-flex flex-column gap-2">
            <div className="d-flex flex-wrap gap-3" role="radiogroup" aria-label="Kết luận">
              {(Object.keys(CONTROL_CONCLUSION_LABELS) as ControlCheckConclusion[]).map((c) => (
                <div className="form-check" key={c}>
                  <input id={`cc19-concl-${c}`} type="radio" name="cc19-concl" className="form-check-input" checked={draft.conclusion === c} disabled={ro || busy}
                    onChange={() => setDraft({ ...draft, conclusion: c })} />
                  <label className="form-check-label small" htmlFor={`cc19-concl-${c}`}>{CONTROL_CONCLUSION_LABELS[c]}</label>
                </div>
              ))}
            </div>
            {(draft.conclusion === "NeedsWork" || draft.needsWorkContent) && (
              <div>
                <label className="form-label small mb-1" htmlFor="cc19-needs">Nội dung cần hoàn thiện</label>
                <textarea id="cc19-needs" className="form-control form-control-sm" rows={3} maxLength={4000} value={draft.needsWorkContent} disabled={ro || busy}
                  onChange={(e) => setDraft({ ...draft, needsWorkContent: e.target.value })} />
              </div>
            )}
          </div>
        </section>
        <section className="card">
          <div className="card-body">
            <OwnerAttachments ownerType="ControlCheck" ownerId={data.id} editable={data.canAttach} formCode="M19" subject="phiếu kiểm soát" description="Căn cứ, minh chứng kiểm soát" defaultOpen />
          </div>
        </section>


        {data.history.length > 0 && (
          <section className="card">
            <div className="card-header"><h2 className="mb-0">Lịch sử</h2></div>
            <ul className="list-group list-group-flush small">
              {data.history.map((h, i) => (
                <li key={i} className="list-group-item">
                  <span className="fw-semibold">{CONTROL_ACTION_LABELS[h.action] ?? h.action}</span>
                  <span className="text-secondary"> · {h.byUserName} · {formatDateTime(h.at)}</span>
                  {h.reason && <div style={{ whiteSpace: "pre-wrap" }}>{h.reason}</div>}
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>

      <RecordPickerDialog
        isOpen={adding}
        title="Thêm hồ sơ vào phiếu"
        confirmText="Thêm hồ sơ"
        rows={pickerRows}
        loading={pickerLoading}
        emptyText="Không còn hồ sơ nào có thể thêm."
        onCancel={() => setAdding(false)}
        onConfirm={(ids) => addRecords(ids)}
      />

      <ReasonDialog
        isOpen={dialog !== null}
        title={dialog ? DIALOGS[dialog].title : ""}
        description={dialog ? DIALOGS[dialog].description : undefined}
        confirmText={dialog ? DIALOGS[dialog].confirmText : ""}
        busy={busy}
        onCancel={() => setDialog(null)}
        onConfirm={reasonDone}
      />
    </div>
  );
}
