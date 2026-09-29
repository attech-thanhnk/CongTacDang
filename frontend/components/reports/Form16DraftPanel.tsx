"use client";

import React, { useEffect, useState } from "react";
import { Button } from "@/components/common";
import { useToast } from "@/contexts/ToastContext";
import {
  Form16Draft,
  Form16DraftContent,
  Form16SummaryRow,
  ReportFileFormat,
  hd03FileName,
  reportService,
  withQuery,
} from "@/services/reportService";

interface Props {
  periodId: string;
  /** Tổ chức Đảng được chọn; rỗng = phạm vi mặc định theo quyền (toàn Đảng bộ hoặc tổ chức duy nhất được giao). */
  branchId?: string;
  /** Nhãn kỳ cho tên tệp ("Quy III-2026"). */
  periodLabel: string;
  /** Tên viết tắt của đơn vị (tên tệp khi xuất toàn Đảng bộ). */
  shortName: string;
}

const FIELDS: { key: keyof Form16DraftContent; label: string; placeholder: string; multiline?: boolean }[] = [
  { key: "documentNumber", label: "Số văn bản", placeholder: "Số …..-BC/……" },
  { key: "recipient", label: "Kính gửi / Kính trình", placeholder: "Ban Thường vụ Đảng ủy TCT (hoặc Đảng ủy cơ sở…)" },
  { key: "workingRules", label: "Căn cứ Quy chế làm việc của", placeholder: "… nhiệm kỳ…" },
  { key: "meetingDate", label: "Ngày tổ chức hội nghị", placeholder: "Để trống: lấy ngày hội nghị quyết định mới nhất" },
  { key: "organizer", label: "Tổ chức Đảng tổ chức hội nghị", placeholder: "Để trống: tên tổ chức Đảng của báo cáo" },
  { key: "proposer", label: "Mục III — Đề xuất của", placeholder: "Để trống: tên tổ chức Đảng của báo cáo" },
  { key: "proposal1", label: "Mục III.1 — Đề nghị xem xét, quyết định đánh giá, xếp loại", placeholder: "Để trống: giữ đoạn mẫu của biểu mẫu", multiline: true },
  { key: "proposal2", label: "Mục III.2 — Điều chuyển, bố trí công tác khác (nếu có)", placeholder: "Để trống: giữ đoạn mẫu của biểu mẫu", multiline: true },
  { key: "proposal3", label: "Mục III.3 — Ý kiến đề xuất khác (nếu có)", placeholder: "Để trống: giữ đoạn mẫu của biểu mẫu", multiline: true },
  { key: "signerName", label: "Bí thư ký báo cáo", placeholder: "Họ và tên" },
];

const percent = (row: Form16SummaryRow) =>
  row.excellentPercent === null || row.excellentPercent === undefined ? "" : `${row.excellentPercent.toFixed(1).replace(".", ",")}%`;

function SummaryTable({ title, rows }: { title: string; rows: Form16SummaryRow[] }) {
  return (
    <div className="mb-3">
      <div className="fw-semibold small mb-1">{title}</div>
      <div className="table-responsive">
        <table className="table table-sm table-bordered align-middle mb-0" style={{ fontSize: "12.5px" }}>
          <thead className="table-light">
            <tr>
              <th>Đối tượng đánh giá, xếp loại</th>
              <th className="text-center">Tổng số</th>
              <th className="text-center">HT xuất sắc NV</th>
              <th className="text-center">HT tốt NV</th>
              <th className="text-center">HTNV</th>
              <th className="text-center">Không HTNV</th>
              <th className="text-center">Chưa xếp loại</th>
              <th className="text-center">Tỷ lệ % xuất sắc trong số tốt trở lên</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.statCode ?? "none"}-${index}`} className={index === rows.length - 1 ? "fw-semibold" : undefined}>
                <td>{row.statCode ? `${row.statCode} — ` : ""}{row.subject}</td>
                <td className="text-center">{row.total}</td>
                <td className="text-center">{row.excellent}</td>
                <td className="text-center">{row.good}</td>
                <td className="text-center">{row.satisfactory}</td>
                <td className="text-center">{row.unsatisfactory}</td>
                <td className="text-center">{row.notRated}</td>
                <td className="text-center">{percent(row)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/** Soạn Mẫu 16: xem số liệu tổng hợp tự động, nhập phần nhập tay (lưu nháp theo kỳ + tổ chức Đảng), xuất Word/PDF. */
export function Form16DraftPanel({ periodId, branchId, periodLabel, shortName }: Props) {
  const { toast } = useToast();
  const [draft, setDraft] = useState<Form16Draft | null>(null);
  const [content, setContent] = useState<Form16DraftContent>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [exporting, setExporting] = useState<ReportFileFormat | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    reportService
      .getForm16Draft(periodId, branchId || undefined)
      .then((loaded) => {
        if (!active) return;
        setDraft(loaded);
        setContent(loaded.content ?? {});
      })
      .catch((err: any) => active && setError(err?.message || "Không tải được bản nháp Mẫu 16."))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [periodId, branchId]);

  const save = async () => {
    if (!draft) return;
    setSaving(true);
    try {
      const saved = await reportService.saveForm16Draft(periodId, branchId || undefined, draft.version, content);
      setDraft(saved);
      setContent(saved.content ?? {});
      toast.success("Đã lưu bản nháp Mẫu 16.");
    } catch (err: any) {
      toast.error(err?.message || "Không lưu được bản nháp Mẫu 16.");
    } finally {
      setSaving(false);
    }
  };

  const exportFile = async (format: ReportFileFormat) => {
    if (!draft) return;
    setExporting(format);
    try {
      const unit = draft.partyCellId ? draft.partyOrganizationName : shortName;
      await reportService.downloadReport(
        withQuery("/reports/docx/mau-16", { periodId, branchId: branchId || undefined }),
        hd03FileName("16", unit, periodLabel, ".docx"),
        format
      );
    } catch (err: any) {
      toast.error(err?.message || "Không xuất được Mẫu 16.");
    } finally {
      setExporting(null);
    }
  };

  if (loading) return <div className="text-secondary small p-3">Đang tải số liệu Mẫu 16…</div>;
  if (error || !draft) return <div className="alert alert-warning small m-3">{error || "Không có dữ liệu."}</div>;

  return (
    <div className="p-3">
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <div className="small text-secondary">
          {draft.partyOrganizationName} · {draft.periodName}
          {draft.updatedAt ? ` · Bản nháp lưu lúc ${new Date(draft.updatedAt).toLocaleString("vi-VN")}` : " · Chưa lưu bản nháp"}
        </div>
        <div className="d-flex gap-2">
          <Button size="sm" variant="primary" icon="bi-save" loading={saving} loadingText="Đang lưu..." onClick={save}>
            Lưu nháp
          </Button>
          <Button size="sm" variant="outline-primary" icon="bi-file-earmark-word" loading={exporting === "original"} loadingText="Đang xuất..." onClick={() => exportFile("original")}>
            Xuất Word
          </Button>
          <Button size="sm" variant="outline-secondary" icon="bi-file-earmark-pdf" loading={exporting === "pdf"} loadingText="Đang xuất..." onClick={() => exportFile("pdf")}>
            PDF
          </Button>
        </div>
      </div>

      <SummaryTable title="I. Các trường hợp thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của đảng ủy, chi ủy cơ sở" rows={draft.baseRows} />
      <SummaryTable title="II. Các trường hợp thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của Ban Thường vụ Đảng ủy Tổng công ty" rows={draft.superiorRows} />
      <div className="small text-secondary mb-3">
        Số liệu tính tự động khi xuất: mức quyết định; hồ sơ chưa có quyết định tính theo mức đề xuất gần nhất (cấp trực tiếp sử dụng → thẩm định → tập thể lãnh đạo).
        Hãy lưu nháp trước khi xuất để phần nhập tay có trong báo cáo.
      </div>

      <div className="row g-2">
        {FIELDS.map((field) => (
          <div key={field.key} className={field.multiline ? "col-12" : "col-md-6"}>
            <label className="form-label small mb-1">{field.label}</label>
            {field.multiline ? (
              <textarea
                className="form-control form-control-sm"
                rows={2}
                placeholder={field.placeholder}
                value={content[field.key] ?? ""}
                onChange={(event) => setContent({ ...content, [field.key]: event.target.value })}
              />
            ) : (
              <input
                className="form-control form-control-sm"
                placeholder={field.key === "meetingDate" && draft.suggestedMeetingDate ? `${field.placeholder} (${draft.suggestedMeetingDate})` : field.placeholder}
                value={content[field.key] ?? ""}
                onChange={(event) => setContent({ ...content, [field.key]: event.target.value })}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

export default Form16DraftPanel;
