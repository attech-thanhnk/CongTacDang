"use client";

import React, { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { Badge } from "@/components/common/StatusBadge";
import { NoAccess } from "@/components/admin/NoAccess";
import { errorMessage } from "@/components/admin/adminUtils";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { useToast } from "@/contexts/ToastContext";
import { formatDate } from "@/lib/dateFormat";
import { CROSSCHECK_STATUS_LABELS, CROSSCHECK_STATUS_TONES, CrosscheckStatus, CrosscheckSummaryDto, crosscheckService } from "@/services/crosscheckService";

const STATUSES = Object.keys(CROSSCHECK_STATUS_LABELS) as CrosscheckStatus[];

/** Phiếu đối soát chuyên ngành trong phạm vi được xem; mặc định theo kỳ đang chọn. */
export default function CrosschecksPage() {
  const { hasPermission, loading: authLoading } = useAuth();
  const { periods, periodId } = usePeriod();
  const { toast } = useToast();
  const router = useRouter();
  const canUse =
    hasPermission("evaluation.read") ||
    hasPermission("evaluation.crosscheck.request") ||
    hasPermission("evaluation.crosscheck.respond") ||
    hasPermission("evaluation.crosscheck.confirm");
  const [period, setPeriod] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [items, setItems] = useState<CrosscheckSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const seq = useRef(0);

  // Kỳ lọc theo kỳ đang chọn ở thanh bên cho tới khi người dùng tự chọn.
  const periodFilter = period ?? periodId ?? "";

  useEffect(() => {
    if (!canUse) {
      setLoading(false);
      return;
    }
    const mine = ++seq.current;
    setLoading(true);
    crosscheckService
      .list({ periodId: periodFilter || undefined, status: status || undefined })
      .then((rows) => {
        if (seq.current === mine) setItems(rows);
      })
      .catch((err: unknown) => {
        if (seq.current === mine) toast.error(errorMessage(err, "Tải danh sách phiếu đối soát không thành công."));
      })
      .finally(() => {
        if (seq.current === mine) setLoading(false);
      });
  }, [canUse, periodFilter, status, toast]);

  const paging = usePagination(items, { storageKey: "crosschecks", resetKeys: [periodFilter, status] });

  if (authLoading) return null;
  if (!canUse) return <NoAccess title="Đối soát chuyên ngành" permissionName="Yêu cầu, lập hoặc xác nhận phiếu đối soát" />;

  const unit = (c: CrosscheckSummaryDto) => [c.departmentName, c.partyCellName].filter(Boolean).join(" · ");

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Đối soát chuyên ngành"
        actions={
          <div className="d-flex gap-2 flex-wrap">
            <select className="form-select form-select-sm" aria-label="Lọc theo kỳ" value={periodFilter} onChange={(e) => setPeriod(e.target.value)}>
              <option value="">Tất cả kỳ</option>
              {periods.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <select className="form-select form-select-sm" aria-label="Lọc theo trạng thái" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">Mọi trạng thái</option>
              {STATUSES.map((s) => <option key={s} value={s}>{CROSSCHECK_STATUS_LABELS[s]}</option>)}
            </select>
          </div>
        }
      />
      <div className="page-body">
        {loading ? (
          <div className="text-secondary loading-delay">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải...
          </div>
        ) : items.length === 0 ? (
          <EmptyState icon="bi-patch-check" title="Chưa có phiếu đối soát" description="Phiếu gửi tới bạn hoặc do bạn yêu cầu sẽ hiện ở đây." />
        ) : (
          <>
            <div className="table-responsive d-none d-md-block">
              <table className="table table-hover align-middle mb-0">
                <thead>
                  <tr>
                    <th>Cán bộ</th>
                    <th>Đơn vị đối soát</th>
                    <th>Lĩnh vực</th>
                    <th className="text-end">Chỉ tiêu</th>
                    <th>Ngày yêu cầu</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {paging.pageItems.map((c) => (
                    <tr key={c.id} role="link" style={{ cursor: "pointer" }} onClick={() => router.push(`/crosschecks/${c.id}`)}>
                      <td>
                        <Link href={`/crosschecks/${c.id}`} onClick={(e) => e.stopPropagation()} className="fw-semibold text-decoration-none">{c.memberFullName}</Link>
                        <div className="small text-secondary">{unit(c) || "—"}</div>
                      </td>
                      <td>{c.expertUnitName}</td>
                      <td className="small">{c.fieldNames.join(", ")}</td>
                      <td className="text-end num">{c.itemCount}</td>
                      <td className="num">{formatDate(c.requestedAt)}</td>
                      <td><Badge tone={CROSSCHECK_STATUS_TONES[c.status] ?? "neutral"}>{c.statusName}</Badge></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="d-md-none d-flex flex-column gap-2">
              {paging.pageItems.map((c) => (
                <Link key={c.id} href={`/crosschecks/${c.id}`} className="card text-decoration-none text-reset">
                  <div className="card-body py-2 px-3 d-flex flex-column gap-1">
                    <div className="d-flex justify-content-between align-items-start gap-2">
                      <span className="fw-semibold">{c.memberFullName}</span>
                      <Badge tone={CROSSCHECK_STATUS_TONES[c.status] ?? "neutral"}>{c.statusName}</Badge>
                    </div>
                    <div className="small text-secondary">{unit(c) || "—"}</div>
                    <div className="small">{c.expertUnitName}</div>
                    <div className="small text-secondary">{c.fieldNames.join(", ")}</div>
                    <div className="small text-secondary num">{c.itemCount} chỉ tiêu · {formatDate(c.requestedAt)}</div>
                  </div>
                </Link>
              ))}
            </div>
            <Pagination
              page={paging.page}
              pageSize={paging.pageSize}
              total={paging.total}
              onPageChange={paging.setPage}
              onPageSizeChange={paging.setPageSize}
            />
          </>
        )}
      </div>
    </div>
  );
}
