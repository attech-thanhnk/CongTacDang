"use client";

import { formatDate, formatDateTime } from "@/lib/dateFormat";
import { EmptyState } from "@/components/common/EmptyState";
import { CollectiveTracking, CollectiveTrackingForm } from "@/services/collectiveService";

const FORM_LABELS: Record<string, string> = { M06: "Mẫu 06", M07: "Mẫu 07", M08: "Mẫu 08" };

function FormCell({ form }: { form: CollectiveTrackingForm }) {
  if (!form.status) return <span className="tag t-bad">Chưa lập</span>;
  return form.status === "Submitted"
    ? <span className="tag t-ok">Đã nộp</span>
    : <span className="tag t-wait">Nháp</span>;
}

/** Bảng theo dõi tổ chức Đảng đã/chưa lập Mẫu 06, 07, 08 trong kỳ; chỉ hiển thị. */
export function CollectiveTrackingTable({ tracking }: { tracking: CollectiveTracking | null }) {
  if (!tracking || tracking.rows.length === 0) {
    return <EmptyState compact icon="bi-people" title="Chưa có tổ chức Đảng nào có cán bộ trong kỳ" />;
  }
  const done = tracking.rows.filter((row) => row.complete).length;
  return (
    <>
      <div className="px-4 py-3 text-secondary small border-bottom">
        Đã lập đủ <span className="num fw-semibold">{done}/{tracking.rows.length}</span> tổ chức · Hạn <span className="num">{formatDate(tracking.deadline)}</span>
      </div>
      <div className="table-responsive">
        <table className="table table-hover align-middle mb-0">
          <thead>
            <tr>
              <th className="ps-4">Tổ chức Đảng</th>
              <th className="text-end">Cán bộ</th>
              {Object.values(FORM_LABELS).map((label) => <th key={label}>{label}</th>)}
              <th>Cập nhật</th>
              <th className="pe-4">Hạn</th>
            </tr>
          </thead>
          <tbody>
            {tracking.rows.map((row) => (
              <tr key={row.partyCellId}>
                <td className="ps-4 fw-semibold">{row.partyCellName}</td>
                <td className="text-end num">{row.memberCount}</td>
                {row.forms.map((form) => <td key={form.form}><FormCell form={form} /></td>)}
                <td className="num">{formatDateTime(row.lastUpdatedAt)}</td>
                <td className="pe-4">{row.overdue ? <span className="tag t-bad">Quá hạn</span> : row.complete ? <span className="tag t-ok">Đủ</span> : "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
