"use client";

import { formatDate } from "@/lib/dateFormat";
import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { Badge, GradeBadge } from "@/components/common/StatusBadge";
import { NoAccess } from "@/components/admin/NoAccess";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { GRADE_OPTIONS } from "@/services/evaluationService";
import { PublishedResultsDto, postPublishService } from "@/services/postPublishService";
import { errorMessage } from "@/components/admin/adminUtils";
import { fmt } from "@/services/criteriaService";
import styles from "@/components/reports/Reports.module.css";

/** Kết quả đã công bố: chỉ mức xếp loại (điểm khi bộ tiêu chí cho phép); máy chủ lọc theo phạm vi. */
export default function ResultsPage() {
  const { hasPermission, loading: authLoading } = useAuth();
  const { periodId, loading: periodLoading } = usePeriod();
  const [departmentId, setDepartmentId] = useState("");
  const [grade, setGrade] = useState("");
  const [data, setData] = useState<PublishedResultsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const allowed = hasPermission("evaluation.results.view");

  // Đổi kỳ thì bỏ lọc đơn vị (danh sách đơn vị theo kỳ).
  useEffect(() => {
    setDepartmentId("");
  }, [periodId]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await postPublishService.getResults({
        periodId: periodId || undefined,
        departmentId: departmentId || undefined,
        grade: grade || undefined,
      });
      setData(result);
    } catch (err: unknown) {
      setError(errorMessage(err, "Tải danh sách kết quả không thành công."));
    } finally {
      setLoading(false);
    }
  }, [periodId, departmentId, grade]);

  useEffect(() => {
    if (allowed && !periodLoading) load();
  }, [allowed, periodLoading, load]);

  const items = data?.items ?? [];
  const paging = usePagination(items, { storageKey: "results", resetKeys: [periodId, departmentId, grade] });

  // Chưa có kết quả nào thì bộ lọc không có tác dụng.
  const showFilters = items.length > 0 || !!departmentId || !!grade;

  if (authLoading) return null;
  if (!allowed) return <NoAccess title="Kết quả đánh giá" permissionName="Xem kết quả đánh giá đã công bố" />;

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Kết quả đánh giá"
        toolbar={
          showFilters ? (
              <>
                <select className="form-select w-auto" aria-label="Đơn vị" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
                  <option value="">Tất cả đơn vị</option>
                  {(data?.departments ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
                <select className="form-select w-auto" aria-label="Mức xếp loại" value={grade} onChange={(e) => setGrade(e.target.value)}>
                  <option value="">Tất cả mức</option>
                  {GRADE_OPTIONS.filter((g) => g.value !== "ChuaXepLoai").map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
                </select>
              </>
          ) : undefined
        }
        actions={
          <button type="button" className="btn btn-outline-secondary" onClick={() => load()} aria-label="Làm mới" title="Làm mới">
            <i className="bi bi-arrow-clockwise" />
          </button>
        }
      />

      <div className="page-body">
        {loading ? (
          <div className="text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        ) : error ? (
          <section className="card">
            <EmptyState
              icon="bi-exclamation-triangle"
              title="Tải danh sách kết quả không thành công"
              description={error}
              action={<button type="button" className="btn btn-outline-secondary" onClick={() => load()}><i className="bi bi-arrow-clockwise me-1" />Làm mới</button>}
            />
          </section>
        ) : items.length === 0 ? (
          <section className="card">
            {departmentId || grade ? (
              <EmptyState
                icon="bi-funnel"
                title="Không có kết quả phù hợp"
                description="Thử chọn đơn vị hoặc mức xếp loại khác."
                action={
                  <button type="button" className="btn btn-outline-secondary" onClick={() => { setDepartmentId(""); setGrade(""); }}>
                    Bỏ lọc
                  </button>
                }
              />
            ) : (
              <EmptyState icon="bi-award" title="Chưa có kết quả công bố" description="Kết quả hiện ở đây sau khi kỳ đánh giá được công bố." />
            )}
          </section>
        ) : (
          <section className="card">
            <div className="card-header d-flex flex-wrap align-items-center gap-2">
              <h2 className="mb-0 me-2">
                Danh sách <span className="text-secondary fw-medium num">{items.length}</span>
              </h2>
              {(data?.gradeCounts ?? []).filter((g) => g.count > 0).map((g) => (
                <span key={g.grade} className="tag t-info">
                  {g.gradeName} <strong className="num">{g.count}</strong>
                </span>
              ))}
            </div>
            <div className="table-responsive">
              <table className={`table table-hover align-middle mb-0 ${styles.resultsTable}`}>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Họ và tên</th>
                    <th className={styles.hideSm}>Chức danh</th>
                    <th>Đơn vị</th>
                    <th>Mức xếp loại</th>
                    {data?.showsScores && <th className="text-end">Điểm</th>}
                    <th className={styles.hideSm}>Công bố</th>
                  </tr>
                </thead>
                <tbody>
                  {paging.pageItems.map((item, i) => {
                    const index = paging.offset + i;
                    return (
                    <tr key={`${item.periodId}-${item.fullName}-${index}`}>
                      <td className="text-secondary num">{index + 1}</td>
                      <td className={`fw-semibold ${styles.name}`}>
                        {item.recordId ? <Link href={`/evaluations/${item.recordId}`}>{item.fullName}</Link> : item.fullName}
                        {item.isOwn && <span className="tag t-info ms-2">Của tôi</span>}
                        {!periodId && <div className="small text-secondary fw-normal">{item.periodName}</div>}
                      </td>
                      <td className={styles.hideSm}>{item.positionTitle || "—"}</td>
                      <td>{item.departmentName || "—"}</td>
                      <td>
                        <div className="d-flex flex-wrap gap-1">
                          <GradeBadge grade={item.finalGrade} />
                          {item.underReview && <Badge tone="warning">Đang xem xét kiến nghị</Badge>}
                        </div>
                      </td>
                      {data?.showsScores && <td className="text-end num">{item.finalScore == null ? "—" : fmt(item.finalScore)}</td>}
                      <td className={`text-nowrap num ${styles.hideSm}`}>{formatDate(item.publishedAt)}</td>
                    </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <Pagination
              page={paging.page}
              pageSize={paging.pageSize}
              total={paging.total}
              onPageChange={paging.setPage}
              onPageSizeChange={paging.setPageSize}
            />
          </section>
        )}
      </div>
    </div>
  );
}
