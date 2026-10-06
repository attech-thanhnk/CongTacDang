"use client";

import { formatDate } from "@/lib/dateFormat";
import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Badge, GradeBadge } from "@/components/common/StatusBadge";
import { NoAccess } from "@/components/admin/NoAccess";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { GRADE_OPTIONS } from "@/services/evaluationService";
import { PublishedResultsDto, postPublishService } from "@/services/postPublishService";

/**
 * Kết quả đánh giá đã công bố (HD03 Bước 5) của kỳ đang chọn ở thanh bên. Chỉ họ tên, chức danh, đơn vị,
 * mức xếp loại chính thức (điểm khi bộ tiêu chí cho phép). Phạm vi xem do bản gán vai trò quyết định (máy chủ lọc).
 */
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
    } catch (err: any) {
      setError(err?.message || "Không tải được danh sách kết quả.");
    } finally {
      setLoading(false);
    }
  }, [periodId, departmentId, grade]);

  useEffect(() => {
    if (allowed && !periodLoading) load();
  }, [allowed, periodLoading, load]);

  if (authLoading) return null;
  if (!allowed) return <NoAccess title="Kết quả đánh giá" permissionName="Xem kết quả đánh giá đã công bố" />;

  const items = data?.items ?? [];

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Kết quả đánh giá"
        actions={
          <>
            <select className="form-select w-auto" aria-label="Đơn vị" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">Tất cả đơn vị</option>
              {(data?.departments ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
            <select className="form-select w-auto" aria-label="Mức xếp loại" value={grade} onChange={(e) => setGrade(e.target.value)}>
              <option value="">Tất cả mức</option>
              {GRADE_OPTIONS.filter((g) => g.value !== "ChuaXepLoai").map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
            </select>
            <button type="button" className="btn btn-outline-secondary" onClick={() => load()} aria-label="Tải lại" title="Tải lại">
              <i className="bi bi-arrow-clockwise" />
            </button>
          </>
        }
      />

      <div className="page-body">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {loading ? (
          <div className="text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        ) : items.length === 0 ? (
          <section className="card">
            <EmptyState icon="bi-award" title="Chưa có kết quả công bố" />
          </section>
        ) : (
          <section className="card">
            <div className="card-header d-flex flex-wrap align-items-center gap-2">
              <h2 className="mb-0 me-2">
                Danh sách <span className="text-secondary fw-medium num">{items.length}</span>
              </h2>
              {(data?.gradeCounts ?? []).map((g) => (
                <span key={g.grade} className="tag t-info">
                  {g.gradeName} <strong className="num">{g.count}</strong>
                </span>
              ))}
            </div>
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Họ và tên</th>
                    <th>Chức danh</th>
                    <th>Đơn vị</th>
                    <th>Mức xếp loại</th>
                    {data?.showsScores && <th className="text-end">Điểm</th>}
                    <th>Công bố</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item, index) => (
                    <tr key={`${item.periodId}-${item.fullName}-${index}`}>
                      <td className="text-secondary num">{index + 1}</td>
                      <td className="fw-semibold">
                        {item.recordId ? <Link href={`/evaluations/${item.recordId}`}>{item.fullName}</Link> : item.fullName}
                        {item.isOwn && <span className="tag t-info ms-2">Của tôi</span>}
                        {!periodId && <div className="small text-secondary fw-normal">{item.periodName}</div>}
                      </td>
                      <td>{item.positionTitle || "—"}</td>
                      <td>{item.departmentName || "—"}</td>
                      <td><GradeBadge grade={item.finalGrade} /></td>
                      {data?.showsScores && <td className="text-end num">{item.finalScore ?? "—"}</td>}
                      <td className="text-nowrap num">{formatDate(item.publishedAt)}</td>
                      <td>{item.underReview && <Badge tone="warning">Đang xem xét kiến nghị</Badge>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
