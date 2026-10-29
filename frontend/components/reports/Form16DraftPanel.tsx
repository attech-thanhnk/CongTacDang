"use client";

import React, { useEffect, useState } from "react";
import { Button } from "@/components/common";
import { useToast } from "@/contexts/ToastContext";
import { formatDateTime } from "@/lib/dateFormat";
import {
  Form16Draft,
  Form16DraftContent,
  Form16SummaryRow,
  ReportFileFormat,
  hd03FileName,
  reportService,
  withQuery,
} from "@/services/reportService";
import { errorMessage } from "@/components/admin/adminUtils";
import styles from "./Reports.module.css";

interface Props {
  periodId: string;
  /** Tổ chức Đảng được chọn; rỗng = phạm vi mặc định theo quyền (toàn Đảng bộ hoặc tổ chức duy nhất được giao). */
  branchId?: string;
  /** Nhãn kỳ cho tên tệp ("Quy III-2026"). */
  periodLabel: string;
  /** Tên viết tắt của đơn vị (tên tệp khi xuất toàn Đảng bộ). */
  shortName: string;
}

const FIELDS: { key: keyof Form16DraftContent; label: string; placeholder?: string; multiline?: boolean }[] = [
  { key: "documentNumber", label: "Số văn bản", placeholder: "Số …..-BC/……" },
  { key: "recipient", label: "Kính gửi / Kính trình", placeholder: "Ban Thường vụ Đảng ủy TCT (hoặc Đảng ủy cơ sở…)" },
  { key: "workingRules", label: "Căn cứ Quy chế làm việc của", placeholder: "… nhiệm kỳ…" },
  { key: "meetingDate", label: "Ngày tổ chức hội nghị" },
  { key: "organizer", label: "Tổ chức Đảng tổ chức hội nghị" },
  { key: "proposer", label: "Mục III — Đề xuất của" },
  { key: "proposal1", label: "Mục III.1 — Đề nghị xem xét, quyết định đánh giá, xếp loại", multiline: true },
  { key: "proposal2", label: "Mục III.2 — Điều chuyển, bố trí công tác khác (nếu có)", multiline: true },
  { key: "proposal3", label: "Mục III.3 — Ý kiến đề xuất khác (nếu có)", multiline: true },
  { key: "signerName", label: "Bí thư ký báo cáo", placeholder: "Họ và tên" },
];

/** Bỏ ô trống để so sánh nội dung đã lưu. */
const normalize = (content: Form16DraftContent) =>
  Object.fromEntries(Object.entries(content).filter(([, value]) => typeof value === "string" && value.trim() !== "").sort(([a], [b]) => a.localeCompare(b)));

const percent = (row: Form16SummaryRow) =>
  row.excellentPercent === null || row.excellentPercent === undefined ? "" : `${row.excellentPercent.toFixed(1).replace(".", ",")}%`;

function SummaryTable({ title, rows }: { title: string; rows: Form16SummaryRow[] }) {
  return (
    <div className="mb-3">
      <h3 className={styles.blockTitle}>{title}</h3>
      <div className="table-responsive">
        <table className="table table-sm table-bordered align-middle mb-0">
          <thead className="table-light">
            <tr>
              <th>Đối tượng đánh giá, xếp loại</th>
              <th className="text-center">Tổng số</th>
              <th className="text-center">HT xuất sắc NV</th>
              <th className="text-center">HT tốt NV</th>
              <th className="text-center">HTNV</th>
              <th className="text-center">Không HTNV</th>
              <th className="text-center">Chưa xếp loại</th>
              <th className="text-center" style={{ whiteSpace: "normal", minWidth: 120 }}>Tỷ lệ % xuất sắc trong số tốt trở lên</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.statCode ?? "none"}-${index}`} className={index === rows.length - 1 ? "fw-semibold" : undefined}>
                <td>
                  {row.statCode ? `${row.statCode} — ` : ""}{row.subject}
                  {row.isExceedingQuota && (
                    <span className="tag t-bad ms-2">Vượt trần XS (tối đa {row.maxExcellentAllowed ?? 0})</span>
                  )}
                </td>
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
      .catch((err: unknown) => active && setError(errorMessage(err, "Tải bản nháp Mẫu 16 không thành công.")))
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
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Lưu bản nháp Mẫu 16 không thành công."));
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
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải Mẫu 16 không thành công."));
    } finally {
      setExporting(null);
    }
  };

  // Phần nhập tay chỉ vào báo cáo sau khi lưu nháp.
  const dirty = !!draft && JSON.stringify(normalize(content)) !== JSON.stringify(normalize(draft.content ?? {}));

  if (loading) return <div className={`${styles.panel} text-secondary small loading-delay`}>Đang tải số liệu Mẫu 16…</div>;
  if (error || !draft) return <div className={styles.panel}><div className="alert alert-warning small mb-0">{error || "Không có dữ liệu."}</div></div>;

  return (
    <div className={styles.panel}>
      <div className={styles.panelHead}>
        <div className={styles.panelMeta}>
          {draft.partyOrganizationName}
          {draft.updatedAt ? ` · Lưu nháp lúc ${formatDateTime(draft.updatedAt)}` : " · Chưa lưu nháp"}
        </div>
        <div className={styles.panelActions}>
          <Button size="sm" variant="outline-secondary" icon="bi-download" disabled={dirty || exporting !== null} title={dirty ? "Lưu nháp trước khi tải" : undefined}
            loading={exporting === "original"} loadingText="Đang tải..." onClick={() => exportFile("original")}>
            Tải Word
          </Button>
          <Button size="sm" variant="outline-secondary" icon="bi-filetype-pdf" disabled={dirty || exporting !== null} title={dirty ? "Lưu nháp trước khi tải" : undefined}
            loading={exporting === "pdf"} loadingText="Đang tải..." onClick={() => exportFile("pdf")}>
            Tải PDF
          </Button>
          <Button size="sm" variant="primary" icon="bi-check-lg" disabled={!dirty} title={!dirty ? "Chưa có thay đổi" : undefined}
            loading={saving} loadingText="Đang lưu..." onClick={save}>
            Lưu nháp
          </Button>
        </div>
      </div>

      <SummaryTable title="I. Các trường hợp thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của đảng ủy, chi ủy cơ sở" rows={draft.baseRows} />
      <SummaryTable title="II. Các trường hợp thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của cấp trên" rows={draft.superiorRows} />

      <h3 className={styles.blockTitle}>Phần nhập tay</h3>
      <div className="row g-2">
        {FIELDS.map((field) => (
          <div key={field.key} className={field.multiline ? "col-12" : "col-md-6"}>
            <label className="form-label">{field.label}</label>
            {field.multiline ? (
              <textarea
                className="form-control form-control-sm"
                rows={2}
                value={content[field.key] ?? ""}
                onChange={(event) => setContent({ ...content, [field.key]: event.target.value })}
              />
            ) : (
              <input
                className="form-control form-control-sm"
                placeholder={field.key === "meetingDate" ? draft.suggestedMeetingDate ?? undefined : field.placeholder}
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
