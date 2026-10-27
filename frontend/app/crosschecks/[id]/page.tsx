"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { HeaderMenu } from "@/components/common/HeaderMenu";
import { EmptyState } from "@/components/common/EmptyState";
import { Badge } from "@/components/common/StatusBadge";
import { DateField } from "@/components/evaluations/DateField";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { CrosscheckRequestForm } from "@/components/crosschecks/CrosscheckRequestForm";
import { OwnerAttachments } from "@/components/attachments/OwnerAttachments";
import { errorMessage, errorStatus } from "@/components/admin/adminUtils";
import { useToast } from "@/contexts/ToastContext";
import { reportService } from "@/services/reportService";
import { formatDate, formatDateTime } from "@/lib/dateFormat";
import {
  CROSSCHECK_ADJUSTMENT_LABELS,
  CROSSCHECK_RESULT_LABELS,
  CROSSCHECK_STATUS_TONES,
  CrosscheckAdjustment,
  CrosscheckDto,
  CrosscheckItemDto,
  CrosscheckResult,
  SaveCrosscheckRequest,
  crosscheckService,
} from "@/services/crosscheckService";

interface DraftItem {
  id: string;
  sourceData: string;
  result: CrosscheckResult | "";
  adjustmentType: CrosscheckAdjustment;
  points: string;
  basisText: string;
  basisDocNumber: string;
  basisDocDate: string;
  basisAgency: string;
  notApplicable: boolean;
}

interface Draft {
  issuedDate: string;
  items: DraftItem[];
}

const toDraft = (c: CrosscheckDto): Draft => ({
  issuedDate: c.issuedDate?.slice(0, 10) ?? "",
  items: c.items.map((i) => ({
    id: i.id,
    sourceData: i.sourceData,
    result: i.result ?? "",
    adjustmentType: i.adjustmentType,
    points: i.adjustmentPoints == null ? "" : String(i.adjustmentPoints),
    basisText: i.basisText,
    basisDocNumber: i.basisDocNumber,
    basisDocDate: i.basisDocDate?.slice(0, 10) ?? "",
    basisAgency: i.basisAgency,
    notApplicable: i.notApplicable,
  })),
});

type Dialog = "return" | "cancel" | null;

/** Phiếu đối soát chuyên ngành: xem, sửa yêu cầu, nhập kết quả và xử lý; nút theo cờ do máy chủ trả về. */
export default function CrosscheckDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast, confirm } = useToast();
  const [data, setData] = useState<CrosscheckDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editingRequest, setEditingRequest] = useState(false);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [draft, setDraft] = useState<Draft | null>(null);

  const apply = useCallback((c: CrosscheckDto) => {
    setData(c);
    setDraft(toDraft(c));
  }, []);

  const load = useCallback(async () => {
    try {
      apply(await crosscheckService.get(id));
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải phiếu đối soát không thành công."));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [id, apply, toast]);

  useEffect(() => {
    load();
  }, [load]);

  const dirty = useMemo(() => !!data && !!draft && JSON.stringify(draft) !== JSON.stringify(toDraft(data)), [data, draft]);

  const exportWord = async () => {
    if (!data) return;
    try {
      await reportService.exportQualityForm("03", data.id, `Mau_03_${data.id}.docx`);
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Xuất phiếu đối soát không thành công."));
    }
  };

  /** Chạy lệnh ghi; lỗi hiện qua toast. Lỗi dữ liệu nhập (400) giữ nguyên phần đang nhập, các lỗi khác tải lại phiếu. */
  const run = async (work: () => Promise<CrosscheckDto>, success: string, failure: string): Promise<boolean> => {
    setBusy(true);
    try {
      apply(await work());
      toast.success(success);
      return true;
    } catch (err: unknown) {
      toast.error(errorMessage(err, failure));
      if (errorStatus(err) !== 400) await load();
      return false;
    } finally {
      setBusy(false);
    }
  };

  const responseBody = (version: number) => ({
    version,
    issuedDate: draft?.issuedDate || null,
    items: (draft?.items ?? []).map((i) => ({
      id: i.id,
      sourceData: i.sourceData,
      result: i.notApplicable ? null : i.result || null,
      adjustmentType: i.notApplicable ? ("None" as const) : i.adjustmentType,
      adjustmentPoints: !i.notApplicable && i.adjustmentType === "Decrease" && i.points !== "" ? Number(i.points) : null,
      basisText: i.basisText,
      basisDocNumber: i.basisDocNumber,
      basisDocDate: i.basisDocDate || null,
      basisAgency: i.basisAgency,
      notApplicable: i.notApplicable,
    })),
  });

  const saveResponse = () =>
    data && run(() => crosscheckService.saveResponse(data.id, responseBody(data.version)), "Đã lưu kết quả đối soát.", "Lưu kết quả đối soát không thành công.");

  const submit = () => {
    if (!data) return;
    confirm({
      title: "Gửi xác nhận",
      message: "Phiếu sẽ chuyển cho lãnh đạo đơn vị đối soát xác nhận.",
      confirmText: "Gửi xác nhận",
      isDanger: false,
      onConfirm: async () => {
        let version = data.version;
        if (dirty) {
          try {
            const saved = await crosscheckService.saveResponse(data.id, responseBody(data.version));
            apply(saved);
            version = saved.version;
          } catch (err: unknown) {
            toast.error(errorMessage(err, "Lưu kết quả đối soát không thành công."));
            if (errorStatus(err) !== 400) await load();
            return;
          }
        }
        await run(() => crosscheckService.submit(data.id, version), "Đã gửi phiếu chờ xác nhận.", "Gửi xác nhận không thành công.");
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
        await run(() => crosscheckService.confirm(data.id, data.version), "Đã xác nhận phiếu đối soát.", "Xác nhận phiếu không thành công.");
      },
    });
  };

  const saveRequest = async (body: SaveCrosscheckRequest) => {
    if (!data) return;
    if (await run(() => crosscheckService.updateRequest(data.id, body), "Đã cập nhật yêu cầu đối soát.", "Cập nhật yêu cầu không thành công.")) {
      setEditingRequest(false);
    }
  };

  const reasonDone = async (reason: string) => {
    if (!data || !dialog) return;
    const kind = dialog;
    const ok =
      kind === "return"
        ? await run(() => crosscheckService.returnBack(data.id, data.version, reason), "Đã trả lại phiếu.", "Trả lại phiếu không thành công.")
        : await run(() => crosscheckService.cancel(data.id, data.version, reason), "Đã hủy yêu cầu đối soát.", "Hủy yêu cầu không thành công.");
    if (ok) setDialog(null);
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
        <PageHeader title="Phiếu đối soát chuyên ngành" />
        <div className="page-body">
          <EmptyState icon="bi-file-earmark-x" title="Chưa mở được phiếu" description="Phiếu không tồn tại hoặc bạn không có quyền xem." />
        </div>
      </div>
    );
  }

  const primary = data.canConfirm
    ? { label: "Xác nhận", onClick: confirmIt }
    : data.canSubmit
      ? { label: "Gửi xác nhận", onClick: submit }
      : null;

  const setItem = (itemId: string, patch: Partial<DraftItem>) =>
    setDraft((d) => (d ? { ...d, items: d.items.map((i) => (i.id === itemId ? { ...i, ...patch } : i)) } : d));

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Phiếu đối soát chuyên ngành"
        subTitle={`${data.memberFullName} · ${data.periodName}`}
        badge={<Badge tone={CROSSCHECK_STATUS_TONES[data.status] ?? "neutral"}>{data.statusName}</Badge>}
        actions={
          <div className="d-flex gap-2 align-items-center">
            {primary && (
              <button type="button" className="btn btn-primary" disabled={busy} onClick={primary.onClick}>
                {primary.label}
              </button>
            )}
            <HeaderMenu
              itemLabel="phiếu đối soát"
              disabled={busy}
              actions={[
                { key: "edit", label: "Sửa yêu cầu", icon: "bi-pencil-square", hidden: !data.canEditRequest || editingRequest, onClick: () => setEditingRequest(true) },
                { key: "word", label: "Xuất Word", icon: "bi-download", onClick: exportWord },
                { key: "return", label: "Trả lại", icon: "bi-arrow-return-left", hidden: !data.canReturn, onClick: () => setDialog("return") },
                { key: "cancel", label: "Hủy yêu cầu", icon: "bi-x-circle", danger: true, hidden: !data.canCancel, onClick: () => setDialog("cancel") },
              ]}
            />
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {data.returnReason && data.status !== "Confirmed" && data.status !== "Cancelled" && (
          <div className="alert alert-warning small mb-0">Phiếu bị trả lại: {data.returnReason}</div>
        )}
        {data.status === "Cancelled" && (
          <div className="alert alert-secondary small mb-0">
            Yêu cầu đã hủy{data.cancelledByName ? ` bởi ${data.cancelledByName}` : ""}
            {data.cancelledAt ? ` lúc ${formatDateTime(data.cancelledAt)}` : ""}
            {data.cancelReason ? `: ${data.cancelReason}` : "."}
          </div>
        )}

        <section className="card">
          <div className="card-header">
            <h2 className="mb-0">Yêu cầu đối soát</h2>
          </div>
          <div className="card-body">
            {editingRequest ? (
              <CrosscheckRequestForm crosscheck={data} busy={busy} submitLabel="Lưu yêu cầu" onCancel={() => setEditingRequest(false)} onSubmit={saveRequest} />
            ) : (
              <dl className="small mb-0 row">
                <Info label="Hồ sơ">
                  <Link href={`/evaluations/${data.recordId}`}>{data.memberFullName}</Link>
                  {[data.departmentName, data.partyCellName].filter(Boolean).length > 0 && (
                    <span className="text-secondary"> · {[data.departmentName, data.partyCellName].filter(Boolean).join(" · ")}</span>
                  )}
                </Info>
                <Info label="Đơn vị đối soát">
                  {data.expertUnitName}
                  {data.enteredOnBehalf && <span className="text-secondary"> (người yêu cầu nhập kết quả thay)</span>}
                </Info>
                <Info label="Lĩnh vực">{data.fieldNames.join(", ") || "—"}</Info>
                <Info label="Kính gửi">{data.addresseeName || "—"}</Info>
                <Info label="Nội dung yêu cầu"><span style={{ whiteSpace: "pre-wrap" }}>{data.requestReason}</span></Info>
                <Info label="Người yêu cầu">{data.requestedByName} · {formatDateTime(data.requestedAt)}</Info>
                {data.submittedByName && <Info label="Người lập phiếu">{data.submittedByName} · {formatDateTime(data.submittedAt)}</Info>}
                {data.confirmedByName && <Info label="Người xác nhận">{data.confirmedByName} · {formatDateTime(data.confirmedAt)}</Info>}
              </dl>
            )}
          </div>
        </section>

        <section className="card">
          <div className="card-header d-flex justify-content-between align-items-center gap-2 flex-wrap">
            <h2 className="mb-0">Kết quả đối soát</h2>
            {data.canRespond && (
              <div className="d-flex gap-2">
                <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy || !dirty} onClick={() => setDraft(toDraft(data))}>
                  Hủy
                </button>
                <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy || !dirty} onClick={saveResponse}>
                  {busy ? "Đang lưu..." : "Lưu kết quả"}
                </button>
              </div>
            )}
          </div>
          <div className="card-body d-flex flex-column gap-3">
            {data.canRespond ? (
              <div className="row g-2">
                <div className="col-12 col-md-4">
                  <label className="form-label small mb-1" htmlFor="cc-issued">Ngày lập phiếu</label>
                  <DateField id="cc-issued" value={draft.issuedDate} onChange={(v) => setDraft({ ...draft, issuedDate: v })} />
                </div>
              </div>
            ) : (
              data.issuedDate && <div className="small"><span className="text-secondary">Ngày lập phiếu: </span>{formatDate(data.issuedDate)}</div>
            )}
            {data.items.map((item, index) => {
              const d = draft.items.find((x) => x.id === item.id);
              return d ? (
                <ItemCard key={item.id} index={index} item={item} draft={d} editable={data.canRespond} disabled={busy} onChange={(p) => setItem(item.id, p)} />
              ) : null;
            })}
          </div>
        </section>

        <section className="card">
          <div className="card-body">
            <OwnerAttachments ownerType="ExpertCrosscheck" ownerId={data.id} editable={data.canAttach} formCode="M03" subject="phiếu đối soát" description="Căn cứ, minh chứng đối soát" defaultOpen />
          </div>
        </section>

        {data.history.length > 0 && (
          <section className="card">
            <div className="card-header">
              <h2 className="mb-0">Lịch sử</h2>
            </div>
            <ul className="list-group list-group-flush small">
              {data.history.map((h, i) => (
                <li key={i} className="list-group-item">
                  <span className="fw-semibold">{h.actionName}</span>
                  <span className="text-secondary"> · {h.byUserName} · {formatDateTime(h.at)}</span>
                  {h.reason && <div style={{ whiteSpace: "pre-wrap" }}>{h.reason}</div>}
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>

      <ReasonDialog
        isOpen={dialog !== null}
        title={dialog === "return" ? "Trả lại phiếu" : "Hủy yêu cầu đối soát"}
        description={dialog === "return" ? "Phiếu quay về người lập để lập lại." : "Yêu cầu sẽ không còn chặn việc hoàn thành thẩm định."}
        confirmText={dialog === "return" ? "Trả lại" : "Hủy yêu cầu"}
        busy={busy}
        onCancel={() => setDialog(null)}
        onConfirm={reasonDone}
      />
    </div>
  );
}

function Info({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="col-12 col-md-3 fw-normal text-secondary">{label}</dt>
      <dd className="col-12 col-md-9 mb-2">{children}</dd>
    </>
  );
}

/** Một dòng chỉ tiêu: dữ liệu tự khai (chỉ xem) và kết quả đối soát (nhập khi được phép). */
function ItemCard({
  index,
  item,
  draft,
  editable,
  disabled,
  onChange,
}: {
  index: number;
  item: CrosscheckItemDto;
  draft: DraftItem;
  editable: boolean;
  disabled: boolean;
  onChange: (patch: Partial<DraftItem>) => void;
}) {
  const key = `cc-i-${item.id}`;
  const off = draft.notApplicable;
  return (
    <div className="border rounded p-3 d-flex flex-column gap-2">
      <div>
        <div className="fw-semibold small">{index + 1}. {item.itemName}</div>
        <div className="small" style={{ whiteSpace: "pre-wrap" }}>
          <span className="text-secondary">Dữ liệu cá nhân: </span>{item.selfData || "—"}
        </div>
      </div>

      {editable ? (
        <div className="row g-2">
          <div className="col-12">
            <label className="form-label small mb-1" htmlFor={`${key}-src`}>Kết quả đối soát dữ liệu gốc</label>
            <textarea id={`${key}-src`} className="form-control form-control-sm" rows={2} maxLength={4000} value={draft.sourceData} disabled={disabled}
              onChange={(e) => onChange({ sourceData: e.target.value })} />
          </div>
          <div className="col-12">
            <div className="form-check">
              <input id={`${key}-na`} type="checkbox" className="form-check-input" checked={off} disabled={disabled}
                onChange={(e) => onChange(e.target.checked ? { notApplicable: true, result: "", adjustmentType: "None", points: "" } : { notApplicable: false })} />
              <label className="form-check-label small" htmlFor={`${key}-na`}>Không áp dụng</label>
            </div>
          </div>
          <div className="col-12 col-md-4">
            <label className="form-label small mb-1" htmlFor={`${key}-res`}>Kết quả</label>
            <select id={`${key}-res`} className="form-select form-select-sm" value={draft.result} disabled={disabled || off}
              onChange={(e) => onChange({ result: e.target.value as CrosscheckResult | "" })}>
              <option value="">Chưa chọn</option>
              {(Object.keys(CROSSCHECK_RESULT_LABELS) as CrosscheckResult[]).map((r) => <option key={r} value={r}>{CROSSCHECK_RESULT_LABELS[r]}</option>)}
            </select>
          </div>
          <div className="col-12 col-md-5">
            <label className="form-label small mb-1" htmlFor={`${key}-adj`}>Đề xuất tăng/giảm điểm</label>
            <select id={`${key}-adj`} className="form-select form-select-sm" value={draft.adjustmentType} disabled={disabled || off}
              onChange={(e) => onChange({ adjustmentType: e.target.value as CrosscheckAdjustment, points: e.target.value === "Decrease" ? draft.points : "" })}>
              {(Object.keys(CROSSCHECK_ADJUSTMENT_LABELS) as CrosscheckAdjustment[]).map((a) => <option key={a} value={a}>{CROSSCHECK_ADJUSTMENT_LABELS[a]}</option>)}
            </select>
          </div>
          {draft.adjustmentType === "Decrease" && !off && (
            <div className="col-6 col-md-3">
              <label className="form-label small mb-1" htmlFor={`${key}-pts`}>Số điểm giảm</label>
              <input id={`${key}-pts`} type="number" min={0} step={0.1} className="form-control form-control-sm text-end" value={draft.points} disabled={disabled}
                onChange={(e) => onChange({ points: e.target.value })} />
            </div>
          )}
          <div className="col-12 col-md-6">
            <label className="form-label small mb-1" htmlFor={`${key}-basis`}>Căn cứ / xác nhận chuyên môn</label>
            <textarea id={`${key}-basis`} className="form-control form-control-sm" rows={2} maxLength={2000} value={draft.basisText} disabled={disabled}
              onChange={(e) => onChange({ basisText: e.target.value })} />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small mb-1" htmlFor={`${key}-no`}>Văn bản số</label>
            <input id={`${key}-no`} className="form-control form-control-sm" maxLength={100} value={draft.basisDocNumber} disabled={disabled}
              onChange={(e) => onChange({ basisDocNumber: e.target.value })} />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small mb-1" htmlFor={`${key}-date`}>Ngày văn bản</label>
            <DateField id={`${key}-date`} value={draft.basisDocDate} disabled={disabled} onChange={(v) => onChange({ basisDocDate: v })} />
          </div>
          <div className="col-12 col-md-2">
            <label className="form-label small mb-1" htmlFor={`${key}-ag`}>Cơ quan ban hành</label>
            <input id={`${key}-ag`} className="form-control form-control-sm" maxLength={300} value={draft.basisAgency} disabled={disabled}
              onChange={(e) => onChange({ basisAgency: e.target.value })} />
          </div>
        </div>
      ) : (
        <dl className="small mb-0 row">
          <Info label="Kết quả đối soát dữ liệu gốc"><span style={{ whiteSpace: "pre-wrap" }}>{item.sourceData || "—"}</span></Info>
          {item.notApplicable ? (
            <Info label="Kết quả">Không áp dụng</Info>
          ) : (
            <>
              <Info label="Kết quả">{item.result ? CROSSCHECK_RESULT_LABELS[item.result] : "—"}</Info>
              <Info label="Đề xuất tăng/giảm điểm">{item.adjustmentText || "0"}</Info>
            </>
          )}
          {(item.basisText || item.basisDocNumber || item.basisAgency) && (
            <Info label="Căn cứ">
              {[item.basisText, item.basisDocNumber && `số ${item.basisDocNumber}`, item.basisDocDate && `ngày ${formatDate(item.basisDocDate)}`, item.basisAgency]
                .filter(Boolean)
                .join(", ")}
            </Info>
          )}
        </dl>
      )}
    </div>
  );
}
