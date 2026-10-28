"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { HeaderMenu } from "@/components/common/HeaderMenu";
import { EmptyState } from "@/components/common/EmptyState";
import { Badge, GradeBadge } from "@/components/common/StatusBadge";
import { DateField } from "@/components/evaluations/DateField";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { RecordPickerDialog, PickerRow } from "@/components/quality/RecordPickerDialog";
import { OwnerAttachments } from "@/components/attachments/OwnerAttachments";
import { errorMessage } from "@/components/admin/adminUtils";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, formatDateTime } from "@/lib/dateFormat";
import { GRADE_OPTIONS, gradeLabel } from "@/services/evaluationService";
import { reportService } from "@/services/reportService";
import {
  CALIBRATION_STATUS_TONES,
  CalibrationHistoryDto,
  CalibrationSessionDto,
  UpdateCalibrationRequest,
  calibrationService,
} from "@/services/calibrationService";

interface RecRow {
  recordId: string;
  fullName: string;
  unit: string;
  gradeBefore: string;
  scoreBefore: number | null;
  outdated: boolean;
  /** Hồ sơ vừa chọn thêm, chưa lưu vào biên bản. */
  added?: boolean;
}

interface AttRow {
  fullName: string;
  memberId: string | null;
  capacity: string;
  dissentNote: string;
}

interface SumRow {
  gradeCode: string;
  notableDifference: string;
  preliminaryConclusion: string;
}

interface CaseRow {
  recordId: string;
  clarification: string;
  gradeAfter: string;
  scoreAfter: string;
  basis: string;
  responsibleName: string;
  responsibleMemberId: string | null;
}

interface Draft {
  groupName: string;
  heldAt: string;
  location: string;
  chairName: string;
  secretaryName: string;
  scopeNote: string;
  extraConclusion: string;
  records: RecRow[];
  attendees: AttRow[];
  summary: SumRow[];
  cases: CaseRow[];
}

const toDraft = (s: CalibrationSessionDto): Draft => ({
  groupName: s.groupName ?? "",
  heldAt: s.heldAt?.slice(0, 10) ?? "",
  location: s.location ?? "",
  chairName: s.chairName ?? "",
  secretaryName: s.secretaryName ?? "",
  scopeNote: s.scopeNote ?? "",
  extraConclusion: s.extraConclusion ?? "",
  records: s.records.map((r) => ({
    recordId: r.recordId,
    fullName: r.fullName,
    unit: [r.departmentName, r.partyCellName].filter(Boolean).join(" · "),
    gradeBefore: r.gradeBefore,
    scoreBefore: r.scoreBefore ?? null,
    outdated: r.snapshotOutdated,
  })),
  attendees: s.attendees.map((a) => ({ fullName: a.fullName, memberId: a.memberId ?? null, capacity: a.capacity ?? "", dissentNote: a.dissentNote ?? "" })),
  summary: s.summary
    .filter((r) => r.gradeCode !== "TOTAL")
    .map((r) => ({ gradeCode: r.gradeCode, notableDifference: r.notableDifference ?? "", preliminaryConclusion: r.preliminaryConclusion ?? "" })),
  cases: s.cases.map((c) => ({
    recordId: c.recordId,
    clarification: c.clarification ?? "",
    gradeAfter: c.gradeAfter ?? "",
    scoreAfter: c.scoreAfter == null ? "" : String(c.scoreAfter),
    basis: c.basis ?? "",
    responsibleName: c.responsibleName ?? "",
    responsibleMemberId: c.responsibleMemberId ?? null,
  })),
});

const blank = (v: string) => (v.trim() === "" ? null : v.trim());

const toRequest = (d: Draft, version: number): UpdateCalibrationRequest => ({
  version,
  groupName: blank(d.groupName),
  heldAt: d.heldAt || null,
  location: blank(d.location),
  chairName: blank(d.chairName),
  secretaryName: blank(d.secretaryName),
  scopeNote: blank(d.scopeNote),
  extraConclusion: blank(d.extraConclusion),
  recordIds: d.records.map((r) => r.recordId),
  attendees: d.attendees
    .filter((a) => a.fullName.trim() !== "")
    .map((a) => ({ fullName: a.fullName.trim(), memberId: a.memberId, capacity: blank(a.capacity), dissentNote: blank(a.dissentNote) })),
  summary: d.summary.map((r) => ({ gradeCode: r.gradeCode, notableDifference: blank(r.notableDifference), preliminaryConclusion: blank(r.preliminaryConclusion) })),
  cases: d.cases.map((c) => ({
    recordId: c.recordId,
    clarification: blank(c.clarification),
    gradeAfter: c.gradeAfter || null,
    scoreAfter: c.scoreAfter.trim() === "" ? null : Number(c.scoreAfter),
    basis: blank(c.basis),
    responsibleName: blank(c.responsibleName),
    responsibleMemberId: c.responsibleMemberId,
  })),
});

const pct = (v?: number | null) => (v == null ? "—" : `${Math.round(v * 1000) / 10}%`);
const score = (v?: number | null) => (v == null ? "—" : String(v));
const GRADES = GRADE_OPTIONS.filter((g) => g.value !== "ChuaXepLoai");

type Dialog = "reopen" | "void" | null;

/** Biên bản họp thống nhất: soạn, chốt, mở lại, hủy; nút theo cờ do máy chủ trả về. */
export default function CalibrationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast, confirm } = useToast();
  const [data, setData] = useState<CalibrationSessionDto | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [history, setHistory] = useState<CalibrationHistoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [adding, setAdding] = useState(false);
  const [pickerRows, setPickerRows] = useState<PickerRow[]>([]);
  const [pickerLoading, setPickerLoading] = useState(false);
  const [candidateInfo, setCandidateInfo] = useState<Record<string, RecRow>>({});

  const apply = useCallback((s: CalibrationSessionDto) => {
    setData(s);
    setDraft(toDraft(s));
    calibrationService.history(s.id).then(setHistory).catch(() => setHistory([]));
  }, []);

  const load = useCallback(async () => {
    try {
      apply(await calibrationService.get(id));
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải biên bản họp thống nhất không thành công."));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [id, apply, toast]);

  useEffect(() => {
    load();
  }, [load]);

  const dirty = useMemo(() => !!data && !!draft && JSON.stringify(draft) !== JSON.stringify(toDraft(data)), [data, draft]);

  /** Chạy lệnh ghi; lỗi (kể cả lệch phiên bản) hiện qua toast và tải lại biên bản. */
  const run = async (work: () => Promise<CalibrationSessionDto>, success: string, failure: string): Promise<boolean> => {
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

  const save = () =>
    data && draft && run(() => calibrationService.update(data.id, toRequest(draft, data.version)), "Đã lưu biên bản họp thống nhất.", "Lưu biên bản họp thống nhất không thành công.");

  const conclude = () => {
    if (!data || !draft) return;
    confirm({
      title: "Chốt biên bản",
      message: "Kết quả sau họp thống nhất sẽ được áp vào các hồ sơ trong biên bản.",
      confirmText: "Chốt biên bản",
      isDanger: false,
      onConfirm: async () => {
        let version = data.version;
        if (dirty) {
          try {
            version = (await calibrationService.update(data.id, toRequest(draft, data.version))).version;
          } catch (err: unknown) {
            toast.error(errorMessage(err, "Lưu biên bản họp thống nhất không thành công."));
            await load();
            return;
          }
        }
        await run(() => calibrationService.conclude(data.id, version), "Đã chốt biên bản họp thống nhất.", "Chốt biên bản không thành công.");
      },
    });
  };

  const reasonDone = async (reason: string) => {
    if (!data || !dialog) return;
    const ok =
      dialog === "reopen"
        ? await run(() => calibrationService.reopen(data.id, data.version, reason), "Đã mở lại biên bản.", "Mở lại biên bản không thành công.")
        : await run(() => calibrationService.void(data.id, data.version, reason), "Đã hủy biên bản.", "Hủy biên bản không thành công.");
    if (ok) setDialog(null);
  };

  const exportForm = async (format: "original" | "pdf") => {
    if (!data) return;
    try {
      await reportService.exportQualityForm("20", data.id, `Mau_20_${data.id}.docx`, format);
    } catch (err: unknown) {
      toast.error(errorMessage(err, format === "pdf" ? "Tải PDF không thành công. Hãy tải bản Word hoặc liên hệ quản trị hệ thống." : "Tải biên bản họp thống nhất không thành công."));
    }
  };

  const openPicker = async () => {
    if (!data || !draft) return;
    setAdding(true);
    setPickerLoading(true);
    setPickerRows([]);
    try {
      const rows = await calibrationService.candidates(data.periodId, data.departmentId ?? undefined);
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
              gradeBefore: r.gradeBefore,
              scoreBefore: r.scoreBefore ?? null,
              outdated: false,
              added: true,
            } as RecRow,
          ])
        )
      );
      setPickerRows(
        free.map((r) => ({
          id: r.recordId,
          name: r.fullName,
          detail: [r.departmentName, r.partyCellName, `Trước họp thống nhất: ${gradeLabel(r.gradeBefore)}`].filter(Boolean).join(" · "),
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
        <PageHeader title="Biên bản họp thống nhất" />
        <div className="page-body">
          <EmptyState card icon="bi-file-earmark-x" title="Chưa mở được biên bản" description="Biên bản không tồn tại hoặc bạn không có quyền xem." />
        </div>
      </div>
    );
  }

  const ro = !data.canEdit;
  const set = (patch: Partial<Draft>) => setDraft((d) => (d ? { ...d, ...patch } : d));
  const blockedNow = !dirty && !!data.concludeBlockedReason;
  const recordById = (rid: string) => draft.records.find((r) => r.recordId === rid);
  const caseIds = new Set(draft.cases.map((c) => c.recordId));
  const freeForCase = draft.records.filter((r) => !caseIds.has(r.recordId));

  const removeRecord = (rid: string) =>
    set({ records: draft.records.filter((r) => r.recordId !== rid), cases: draft.cases.filter((c) => c.recordId !== rid) });
  const addRecords = (ids: string[]) => {
    const rows = ids.map((i) => candidateInfo[i]).filter((r): r is RecRow => !!r);
    set({ records: [...draft.records, ...rows] });
    setAdding(false);
  };

  const setAtt = (index: number, patch: Partial<AttRow>) => set({ attendees: draft.attendees.map((a, i) => (i === index ? { ...a, ...patch } : a)) });
  const setSum = (code: string, patch: Partial<SumRow>) => set({ summary: draft.summary.map((r) => (r.gradeCode === code ? { ...r, ...patch } : r)) });
  const setCase = (rid: string, patch: Partial<CaseRow>) => set({ cases: draft.cases.map((c) => (c.recordId === rid ? { ...c, ...patch } : c)) });
  const caseDto = (rid: string) => data.cases.find((c) => c.recordId === rid);

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Biên bản họp thống nhất"
        subTitle={`${data.groupName || "—"} · ${data.periodName}`}
        badge={<Badge tone={CALIBRATION_STATUS_TONES[data.status] ?? "neutral"}>{data.statusName}</Badge>}
        actions={
          <div className="d-flex gap-2 align-items-center">
            {!ro && (
              <>
                {dirty && <span className="text-secondary small">Có thay đổi chưa lưu</span>}
                <button type="button" className="btn btn-outline-primary" disabled={busy || !dirty} onClick={save}>{busy ? "Đang lưu..." : "Lưu"}</button>
              </>
            )}
            {data.canConclude && (
              <button type="button" className="btn btn-primary" disabled={busy || blockedNow} onClick={conclude}>
                Chốt biên bản
              </button>
            )}
            <HeaderMenu
              itemLabel="biên bản họp thống nhất"
              disabled={busy}
              actions={[
                { key: "discard", label: "Hủy thay đổi", icon: "bi-x-lg", hidden: ro || !dirty, onClick: () => setDraft(toDraft(data)) },
                { key: "word", label: "Tải Word", icon: "bi-download", onClick: () => exportForm("original") },
                { key: "pdf", label: "Tải PDF", icon: "bi-filetype-pdf", onClick: () => exportForm("pdf") },
                { key: "reopen", label: "Mở lại", icon: "bi-arrow-counterclockwise", hidden: !data.canReopen, onClick: () => setDialog("reopen") },
                { key: "void", label: "Hủy biên bản", icon: "bi-x-circle", danger: true, hidden: !data.canVoid, onClick: () => setDialog("void") },
              ]}
            />
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {data.status === "Draft" && data.concludeBlockedReason && (
          <div className="alert alert-warning small mb-0" role="status">{data.concludeBlockedReason}</div>
        )}
        {data.status === "Void" && (
          <div className="alert alert-secondary small mb-0">Biên bản đã hủy{data.voidReason ? `: ${data.voidReason}` : "."}</div>
        )}
        {data.status === "Draft" && data.reopenReason && (
          <div className="alert alert-info small mb-0">Biên bản được mở lại: {data.reopenReason}</div>
        )}
        {data.status === "Concluded" && (
          <div className="alert alert-success small mb-0">
            Đã chốt{data.concludedByName ? ` bởi ${data.concludedByName}` : ""}
            {data.concludedAt ? ` lúc ${formatDateTime(data.concludedAt)}` : ""}.
          </div>
        )}

        <section className="card">
          <div className="card-header"><h2 className="mb-0">Cuộc họp</h2></div>
          <div className="card-body row g-2">
            <div className="col-12 col-md-6">
              <label className="form-label small mb-1" htmlFor="cal-group">Nhóm chức danh / công việc tương đồng</label>
              <input id="cal-group" className="form-control form-control-sm" maxLength={300} value={draft.groupName} disabled={ro} onChange={(e) => set({ groupName: e.target.value })} />
            </div>
            <div className="col-6 col-md-3">
              <label className="form-label small mb-1" htmlFor="cal-held">Ngày họp</label>
              <DateField id="cal-held" value={draft.heldAt} disabled={ro} onChange={(v) => set({ heldAt: v })} />
            </div>
            <div className="col-6 col-md-3">
              <label className="form-label small mb-1" htmlFor="cal-loc">Địa điểm</label>
              <input id="cal-loc" className="form-control form-control-sm" maxLength={300} value={draft.location} disabled={ro} onChange={(e) => set({ location: e.target.value })} />
            </div>
            <div className="col-12 col-md-6">
              <label className="form-label small mb-1" htmlFor="cal-chair">Chủ trì</label>
              <input id="cal-chair" className="form-control form-control-sm" maxLength={200} value={draft.chairName} disabled={ro} onChange={(e) => set({ chairName: e.target.value })} />
            </div>
            <div className="col-12 col-md-6">
              <label className="form-label small mb-1" htmlFor="cal-sec">Thư ký</label>
              <input id="cal-sec" className="form-control form-control-sm" maxLength={200} value={draft.secretaryName} disabled={ro} onChange={(e) => set({ secretaryName: e.target.value })} />
            </div>
            <div className="col-12">
              <label className="form-label small mb-1" htmlFor="cal-scope">Phạm vi, số lượng hồ sơ</label>
              <textarea id="cal-scope" className="form-control form-control-sm" rows={2} maxLength={2000} value={draft.scopeNote} disabled={ro}
                placeholder={data.scopeSuggestion} onChange={(e) => set({ scopeNote: e.target.value })} />
            </div>
          </div>
        </section>

        <section className="card">
          <div className="card-header d-flex justify-content-between align-items-center gap-2">
            <h2 className="mb-0">Hồ sơ trong biên bản</h2>
            {!ro && <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={openPicker}>Thêm hồ sơ</button>}
          </div>
          <div className="card-body">
            {draft.records.length === 0 ? (
              <div className="text-secondary small">Chưa có hồ sơ.</div>
            ) : (
              <>
                <div className="table-responsive d-none d-md-block">
                  <table className="table table-sm align-middle mb-0">
                    <thead>
                      <tr className="text-secondary small">
                        <th>Cán bộ</th>
                        <th>Mức trước</th>
                        <th className="text-end">Điểm trước</th>
                        <th>Sau họp thống nhất</th>
                        {!ro && <th />}
                      </tr>
                    </thead>
                    <tbody>
                      {draft.records.map((r) => {
                        const full = data.records.find((x) => x.recordId === r.recordId);
                        return (
                          <tr key={r.recordId}>
                            <td>
                              <Link href={`/evaluations/${r.recordId}`} className="fw-semibold text-decoration-none">{r.fullName}</Link>
                              <div className="small text-secondary">{r.unit || "—"}</div>
                              {r.outdated && <div className="small text-danger">Điểm hoặc mức của hồ sơ đã đổi: lưu biên bản để cập nhật.</div>}
                            </td>
                            <td><GradeBadge grade={r.gradeBefore} /></td>
                            <td className="text-end num">{score(r.scoreBefore)}</td>
                            <td>{full?.calibratedGrade ? <><GradeBadge grade={full.calibratedGrade} /> <span className="num small">{score(full.calibratedScore)}</span></> : "—"}</td>
                            {!ro && (
                              <td className="text-end">
                                <button type="button" className="btn btn-link btn-sm text-danger p-0" disabled={busy} onClick={() => removeRecord(r.recordId)}>Gỡ</button>
                              </td>
                            )}
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
                <div className="d-md-none d-flex flex-column gap-2">
                  {draft.records.map((r) => (
                    <div key={r.recordId} className="border rounded p-2 small">
                      <div className="d-flex justify-content-between align-items-start gap-2">
                        <Link href={`/evaluations/${r.recordId}`} className="fw-semibold text-decoration-none">{r.fullName}</Link>
                        {!ro && <button type="button" className="btn btn-link btn-sm text-danger p-0" disabled={busy} onClick={() => removeRecord(r.recordId)}>Gỡ</button>}
                      </div>
                      <div className="text-secondary">{r.unit || "—"}</div>
                      <div className="d-flex gap-2 align-items-center mt-1"><GradeBadge grade={r.gradeBefore} /><span className="num">{score(r.scoreBefore)}</span></div>
                      {r.outdated && <div className="text-danger">Điểm hoặc mức của hồ sơ đã đổi: lưu biên bản để cập nhật.</div>}
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </section>

        <section className="card">
          <div className="card-header d-flex justify-content-between align-items-center gap-2">
            <h2 className="mb-0">Thành phần tham dự</h2>
            {!ro && (
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy}
                onClick={() => set({ attendees: [...draft.attendees, { fullName: "", memberId: null, capacity: "", dissentNote: "" }] })}>
                Thêm người
              </button>
            )}
          </div>
          <div className="card-body d-flex flex-column gap-2">
            {draft.attendees.length === 0 && <div className="text-secondary small">Chưa có người tham dự.</div>}
            {draft.attendees.map((a, i) => (
              <div key={i} className="row g-2 align-items-end">
                <div className="col-12 col-md-4">
                  <label className="form-label small mb-1" htmlFor={`cal-att-${i}`}>Họ tên</label>
                  <input id={`cal-att-${i}`} className="form-control form-control-sm" maxLength={200} value={a.fullName} disabled={ro}
                    onChange={(e) => setAtt(i, { fullName: e.target.value, memberId: null })} />
                </div>
                <div className="col-12 col-md-3">
                  <label className="form-label small mb-1" htmlFor={`cal-cap-${i}`}>Tư cách</label>
                  <input id={`cal-cap-${i}`} className="form-control form-control-sm" maxLength={200} value={a.capacity} disabled={ro} onChange={(e) => setAtt(i, { capacity: e.target.value })} />
                </div>
                <div className="col-12 col-md">
                  <label className="form-label small mb-1" htmlFor={`cal-dis-${i}`}>Ý kiến bảo lưu</label>
                  <input id={`cal-dis-${i}`} className="form-control form-control-sm" maxLength={2000} value={a.dissentNote} disabled={ro}
                    placeholder="Để trống nếu thống nhất" onChange={(e) => setAtt(i, { dissentNote: e.target.value })} />
                </div>
                {!ro && (
                  <div className="col-auto">
                    <button type="button" className="btn btn-outline-danger btn-sm" aria-label="Xóa người tham dự" disabled={busy}
                      onClick={() => set({ attendees: draft.attendees.filter((_, x) => x !== i) })}>
                      <i className="bi bi-trash" aria-hidden="true" />
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        </section>

        <section className="card">
          <div className="card-header"><h2 className="mb-0">Kết quả chung</h2></div>
          <div className="card-body">
            <div className="table-responsive d-none d-md-block">
              <table className="table table-sm align-middle mb-0">
                <thead>
                  <tr className="text-secondary small">
                    <th>Mức</th>
                    <th className="text-end">Trước (SL)</th>
                    <th className="text-end">Trước (%)</th>
                    <th className="text-end">Sau (SL)</th>
                    <th className="text-end">Sau (%)</th>
                    <th style={{ width: "22%" }}>Chênh lệch đáng chú ý</th>
                    <th style={{ width: "22%" }}>Kết luận sơ bộ</th>
                  </tr>
                </thead>
                <tbody>
                  {data.summary.map((row) => {
                    const total = row.gradeCode === "TOTAL";
                    const edit = draft.summary.find((s) => s.gradeCode === row.gradeCode);
                    return (
                      <tr key={row.gradeCode} className={total ? "fw-semibold" : undefined}>
                        <td>{row.gradeName}</td>
                        <td className="text-end num">{row.countBefore}</td>
                        <td className="text-end num">{pct(row.percentBefore)}</td>
                        <td className="text-end num">{row.countAfter}</td>
                        <td className="text-end num">{pct(row.percentAfter)}</td>
                        <td>
                          {edit && (
                            <input className="form-control form-control-sm" aria-label={`Chênh lệch đáng chú ý — ${row.gradeName}`} maxLength={1000} value={edit.notableDifference} disabled={ro}
                              onChange={(e) => setSum(row.gradeCode, { notableDifference: e.target.value })} />
                          )}
                        </td>
                        <td>
                          {edit && (
                            <input className="form-control form-control-sm" aria-label={`Kết luận sơ bộ — ${row.gradeName}`} maxLength={1000} value={edit.preliminaryConclusion} disabled={ro}
                              onChange={(e) => setSum(row.gradeCode, { preliminaryConclusion: e.target.value })} />
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="d-md-none d-flex flex-column gap-2">
              {data.summary.map((row) => {
                const edit = draft.summary.find((s) => s.gradeCode === row.gradeCode);
                return (
                  <div key={row.gradeCode} className="border rounded p-2 small">
                    <div className="fw-semibold">{row.gradeName}</div>
                    <div className="text-secondary num">Trước {row.countBefore} ({pct(row.percentBefore)}) · Sau {row.countAfter} ({pct(row.percentAfter)})</div>
                    {edit && (
                      <div className="d-flex flex-column gap-1 mt-1">
                        <input className="form-control form-control-sm" aria-label={`Chênh lệch đáng chú ý — ${row.gradeName}`} placeholder="Chênh lệch đáng chú ý" maxLength={1000}
                          value={edit.notableDifference} disabled={ro} onChange={(e) => setSum(row.gradeCode, { notableDifference: e.target.value })} />
                        <input className="form-control form-control-sm" aria-label={`Kết luận sơ bộ — ${row.gradeName}`} placeholder="Kết luận sơ bộ" maxLength={1000}
                          value={edit.preliminaryConclusion} disabled={ro} onChange={(e) => setSum(row.gradeCode, { preliminaryConclusion: e.target.value })} />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
            <div className="text-secondary small mt-2">Số lượng và tỷ lệ tính lại sau mỗi lần lưu.</div>
          </div>
        </section>

        <section className="card">
          <div className="card-header d-flex justify-content-between align-items-center gap-2">
            <h2 className="mb-0">Trường hợp thảo luận</h2>
            {!ro && (
              <select className="form-select form-select-sm w-auto" aria-label="Thêm trường hợp thảo luận" value="" disabled={busy || freeForCase.length === 0}
                onChange={(e) => {
                  if (!e.target.value) return;
                  set({
                    cases: [...draft.cases, { recordId: e.target.value, clarification: "", gradeAfter: "", scoreAfter: "", basis: "", responsibleName: "", responsibleMemberId: null }],
                  });
                }}>
                <option value="">Thêm trường hợp...</option>
                {freeForCase.map((r) => <option key={r.recordId} value={r.recordId}>{r.fullName}</option>)}
              </select>
            )}
          </div>
          <div className="card-body d-flex flex-column gap-3">
            {draft.cases.length === 0 && <div className="text-secondary small">Chưa có trường hợp thảo luận.</div>}
            {draft.cases.map((c, i) => {
              const rec = recordById(c.recordId);
              const dto = caseDto(c.recordId);
              const key = `cal-case-${i}`;
              const beforeGrade = rec?.gradeBefore ?? dto?.gradeBefore;
              const beforeScore = rec?.scoreBefore ?? dto?.scoreBefore;
              // Cùng cách tính với máy chủ: chỉ coi là điều chỉnh khi mức hoặc điểm sau khác trước.
              const changed =
                (c.gradeAfter !== "" && c.gradeAfter !== beforeGrade) ||
                (c.scoreAfter.trim() !== "" && (beforeScore == null || Math.abs(Number(c.scoreAfter) - beforeScore) >= 0.0001));
              return (
                <div key={c.recordId} className="border rounded p-3 d-flex flex-column gap-2">
                  <div className="d-flex justify-content-between align-items-start gap-2 flex-wrap">
                    <div>
                      <span className="fw-semibold">{i + 1}. {rec?.fullName ?? dto?.fullName ?? ""}</span>
                      <span className="ms-2 small text-secondary">Trước: {gradeLabel(rec?.gradeBefore ?? dto?.gradeBefore)} · <span className="num">{score(rec?.scoreBefore ?? dto?.scoreBefore)}</span></span>
                    </div>
                    <div className="d-flex gap-2 align-items-center">
                      {changed ? <Badge tone="warning">Có điều chỉnh</Badge> : <Badge tone="neutral">Giữ nguyên</Badge>}
                      {!ro && (
                        <button type="button" className="btn btn-outline-danger btn-sm" aria-label="Xóa trường hợp" disabled={busy}
                          onClick={() => set({ cases: draft.cases.filter((x) => x.recordId !== c.recordId) })}>
                          <i className="bi bi-trash" aria-hidden="true" />
                        </button>
                      )}
                    </div>
                  </div>
                  <div className="row g-2">
                    <div className="col-12">
                      <label className="form-label small mb-1" htmlFor={`${key}-cl`}>Nội dung làm rõ</label>
                      <textarea id={`${key}-cl`} className="form-control form-control-sm" rows={2} maxLength={4000} value={c.clarification} disabled={ro}
                        onChange={(e) => setCase(c.recordId, { clarification: e.target.value })} />
                    </div>
                    <div className="col-12 col-md-5">
                      <label className="form-label small mb-1" htmlFor={`${key}-gr`}>Mức sau họp thống nhất</label>
                      <select id={`${key}-gr`} className="form-select form-select-sm" value={c.gradeAfter} disabled={ro} onChange={(e) => setCase(c.recordId, { gradeAfter: e.target.value })}>
                        <option value="">Giữ mức trước</option>
                        {GRADES.map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
                      </select>
                    </div>
                    <div className="col-6 col-md-3">
                      <label className="form-label small mb-1" htmlFor={`${key}-sc`}>Điểm sau</label>
                      <input id={`${key}-sc`} type="number" step={0.1} min={0} max={100} className="form-control form-control-sm text-end" value={c.scoreAfter} disabled={ro}
                        onChange={(e) => setCase(c.recordId, { scoreAfter: e.target.value })} />
                    </div>
                    <div className="col-6 col-md-4">
                      <label className="form-label small mb-1" htmlFor={`${key}-rp`}>Người chịu trách nhiệm</label>
                      <input id={`${key}-rp`} className="form-control form-control-sm" maxLength={200} value={c.responsibleName} disabled={ro}
                        onChange={(e) => setCase(c.recordId, { responsibleName: e.target.value, responsibleMemberId: null })} />
                    </div>
                    <div className="col-12">
                      <label className="form-label small mb-1" htmlFor={`${key}-ba`}>Căn cứ</label>
                      <textarea id={`${key}-ba`} className="form-control form-control-sm" rows={2} maxLength={4000} value={c.basis} disabled={ro}
                        onChange={(e) => setCase(c.recordId, { basis: e.target.value })} />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        <section className="card">
          <div className="card-header"><h2 className="mb-0">Kết luận bổ sung</h2></div>
          <div className="card-body">
            <label className="visually-hidden" htmlFor="cal-extra">Kết luận bổ sung</label>
            <textarea id="cal-extra" className="form-control form-control-sm" rows={3} maxLength={4000} value={draft.extraConclusion} disabled={ro}
              onChange={(e) => set({ extraConclusion: e.target.value })} />
          </div>
        </section>

        <section className="card">
          <div className="card-body">
            <OwnerAttachments ownerType="CalibrationSession" ownerId={data.id} editable={data.canAttach} formCode="M20" subject="biên bản" description="Căn cứ, tài liệu kèm biên bản" defaultOpen />
          </div>
        </section>

        {history.length > 0 && (
          <section className="card">
            <div className="card-header"><h2 className="mb-0">Lịch sử</h2></div>
            <ul className="list-group list-group-flush small">
              {history.map((h, i) => (
                <li key={i} className="list-group-item">
                  <span className="fw-semibold">{h.actionName}</span>
                  <span className="text-secondary"> · {h.byUserName} · {formatDateTime(h.at)}</span>
                  {h.reason && <div style={{ whiteSpace: "pre-wrap" }}>{h.reason}</div>}
                </li>
              ))}
            </ul>
          </section>
        )}
        <div className="small text-secondary">Lập bởi {data.preparedByName} · {formatDate(data.preparedAt)} · lần {data.revision}</div>
      </div>

      <ReasonDialog
        isOpen={dialog !== null}
        title={dialog === "reopen" ? "Mở lại biên bản" : "Hủy biên bản"}
        description={dialog === "reopen" ? "Kết quả đã áp lên hồ sơ sẽ bị hủy cho tới khi chốt lại." : "Biên bản bị hủy sẽ không còn hiệu lực với các hồ sơ."}
        confirmText={dialog === "reopen" ? "Mở lại" : "Hủy biên bản"}
        busy={busy}
        onCancel={() => setDialog(null)}
        onConfirm={reasonDone}
      />

      <RecordPickerDialog
        isOpen={adding}
        title="Thêm hồ sơ vào biên bản"
        confirmText="Thêm hồ sơ"
        rows={pickerRows}
        loading={pickerLoading}
        emptyText="Không còn hồ sơ nào có thể thêm."
        onCancel={() => setAdding(false)}
        onConfirm={(ids) => addRecords(ids)}
      />
    </div>
  );
}
