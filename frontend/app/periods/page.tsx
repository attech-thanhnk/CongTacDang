"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { PeriodStatusBadge } from "@/components/periods/PeriodStatusBadge";
import { CreatePeriodModal } from "@/components/periods/CreatePeriodModal";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { EvaluationPeriodDto, evaluationService } from "@/services/evaluationService";
import { errorMessage } from "@/components/admin/adminUtils";
import { formatDate } from "@/lib/dateFormat";
import styles from "@/components/periods/periods.module.css";

/** Kỳ đánh giá: danh sách (mọi người đã đăng nhập) và tạo kỳ với bộ tiêu chí đã chọn (period.manage). */
export default function PeriodsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("period.manage");
  const { toast } = useToast();
  const router = useRouter();
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const canSeeCriteria = canManage || hasPermission("criteria.manage");

  useEffect(() => {
    evaluationService.getPeriods()
      .then(setPeriods)
      .catch((err: unknown) => toast.error(errorMessage(err, "Tải danh sách kỳ không thành công.")))
      .finally(() => setLoading(false));
  }, [toast]);
  const paging = usePagination(periods, { storageKey: "periods" });

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Kỳ đánh giá"
        actions={
          <div className="d-flex flex-wrap gap-2">
            {canSeeCriteria && <Link href="/criteria" className="btn btn-outline-secondary"><i className="bi bi-list-check me-1" />Bộ tiêu chí</Link>}
            {canManage && (
              <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
                <i className="bi bi-plus-lg me-1" />Tạo kỳ
              </button>
            )}
          </div>
        }
      />
      <div className="page-body">
        <section className="card">
          <div className="card-body p-0">
            {loading ? (
              <div className="text-secondary p-3 loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
            ) : periods.length === 0 ? (
              <EmptyState
                icon="bi-calendar-x"
                title="Chưa có kỳ đánh giá"
                description={canManage ? "Tạo kỳ đánh giá, sau đó chọn bộ tiêu chí và cách xét duyệt." : "Kỳ đánh giá hiện ở đây sau khi được tạo."}
                action={canManage ? (
                  <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
                    <i className="bi bi-plus-lg me-1" />Tạo kỳ đánh giá
                  </button>
                ) : undefined}
              />
            ) : (
              <>
              <div className="table-responsive"><table className={`table table-hover align-middle mb-0 periods-table ${styles.compactTable}`}>
                <thead>
                  <tr>
                    <th>Kỳ</th>
                    <th>Trạng thái</th>
                    <th className="d-none d-md-table-cell">Bộ tiêu chí</th>
                    <th className="d-none d-lg-table-cell">Cách xét duyệt</th>
                    <th className="text-end d-none d-sm-table-cell">Người được đánh giá</th>
                    <th className="text-end d-none d-sm-table-cell">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {paging.pageItems.map((p) => (
                    <tr key={p.id} style={{ cursor: "pointer" }} onClick={() => router.push(`/periods/${p.id}`)}>
                      <td className="fw-semibold">
                        {p.name}
                        {p.isActive && <span className="tag t-ok ms-2">Hiện hành</span>}
                        <div className="small text-secondary fw-normal num">{formatDate(p.startDate)} – {formatDate(p.endDate)}</div>
                      </td>
                      <td><PeriodStatusBadge status={p.status} label={p.statusDisplayName} /></td>
                      <td className="d-none d-md-table-cell">
                        {p.criteria ? p.criteria.name : <span className="text-danger">Chưa chọn</span>}
                      </td>
                      <td className="d-none d-lg-table-cell"><ProfilesSummary names={p.settings?.profiles?.map((profile) => profile.name) ?? []} /></td>
                      <td className="text-end num d-none d-sm-table-cell">{p.totalRecords}</td>
                      <td className="text-end text-nowrap d-none d-sm-table-cell">
                        <Link className="btn btn-outline-secondary btn-sm" href={`/periods/${p.id}`} onClick={(e) => e.stopPropagation()}>{canManage ? "Cấu hình" : "Xem"}</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table></div>
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
        </section>
      </div>

      {creating && (
        <CreatePeriodModal
          existing={periods}
          onClose={() => setCreating(false)}
          onCreated={(period) => router.push(`/periods/${period.id}`)}
        />
      )}
    </div>
  );
}

/** Cách xét duyệt của kỳ: tên đầu tiên và số còn lại (đủ tên khi rê chuột) để cột không quá dài. */
function ProfilesSummary({ names }: { names: string[] }) {
  if (names.length === 0) return <>—</>;
  return (
    <span className={styles.profiles} title={names.join(", ")}>
      {names[0]}
      {names.length > 1 && <span className="text-secondary"> và <span className="num">{names.length - 1}</span> cách khác</span>}
    </span>
  );
}
