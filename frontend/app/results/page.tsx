"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { NoAccess } from "@/components/admin/NoAccess";
import { useAuth } from "@/contexts/AuthContext";
import { GRADE_OPTIONS } from "@/services/evaluationService";
import { PublishedResultsDto, postPublishService } from "@/services/postPublishService";

const formatDate = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" }).format(new Date(value)) : "—";

/**
 * Kết quả đánh giá đã công bố (HD03 Bước 5). Chỉ họ tên, chức danh, đơn vị, mức xếp loại chính thức (điểm khi bộ tiêu chí cho phép);
 * không có chi tiết hồ sơ, minh chứng, ý kiến. Phạm vi xem do bản gán vai trò quyết định (máy chủ lọc).
 */
export default function ResultsPage() {
  const { hasPermission, loading: authLoading } = useAuth();
  const [periodId, setPeriodId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [grade, setGrade] = useState("");
  const [data, setData] = useState<PublishedResultsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const allowed = hasPermission("evaluation.results.view");
  const defaultPeriodChosen = useRef(false);

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
      // Mặc định chọn kỳ mới nhất có kết quả.
      if (!defaultPeriodChosen.current) {
        defaultPeriodChosen.current = true;
        if (!periodId && result.periods.length > 0) setPeriodId(result.periods[0].id);
      }
    } catch (err: any) {
      setError(err?.message || "Không tải được danh sách kết quả.");
    } finally {
      setLoading(false);
    }
  }, [periodId, departmentId, grade]);

  useEffect(() => {
    if (allowed) load();
  }, [allowed, load]);

  if (authLoading) return null;
  if (!allowed) return <NoAccess title="Kết quả đánh giá" permissionName="Xem kết quả đánh giá đã công bố" />;

  const items = data?.items ?? [];

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Kết quả đánh giá"
        subTitle="Kết quả xếp loại đã công bố trong phạm vi được giao. Chi tiết hồ sơ, minh chứng, ý kiến không được công khai."
        badge={data ? <span className="badge text-bg-primary">{items.length}</span> : undefined}
        actions={
          <div className="d-flex flex-wrap gap-2">
            <select className="form-select form-select-sm" aria-label="Kỳ đánh giá" value={periodId}
              onChange={(e) => { setPeriodId(e.target.value); setDepartmentId(""); }}>
              <option value="">Tất cả kỳ</option>
              {(data?.periods ?? []).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <select className="form-select form-select-sm" aria-label="Đơn vị" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">Tất cả đơn vị</option>
              {(data?.departments ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
            <select className="form-select form-select-sm" aria-label="Mức xếp loại" value={grade} onChange={(e) => setGrade(e.target.value)}>
              <option value="">Tất cả mức</option>
              {GRADE_OPTIONS.filter((g) => g.value !== "ChuaXepLoai").map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
            </select>
            <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => load()} aria-label="Tải lại">
              <i className="bi bi-arrow-clockwise" />
            </button>
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {data && data.gradeCounts.length > 0 && (
          <div className="d-flex flex-wrap gap-2">
            {data.gradeCounts.map((g) => (
              <span key={g.grade} className="badge text-bg-light border fw-normal">{g.gradeName}: <strong>{g.count}</strong></span>
            ))}
          </div>
        )}
        {loading ? (
          <div className="text-secondary"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        ) : items.length === 0 ? (
          <EmptyState icon="bi-award" title="Chưa có kết quả công bố" description="Khi kết quả đánh giá được công bố trong phạm vi của bạn, danh sách sẽ hiện ở đây." />
        ) : (
          <section className="card border-0 shadow-sm">
            <div className="card-body p-0">
              <div className="table-responsive">
                <table className="table table-hover table-sm align-middle mb-0">
                  <thead>
                    <tr className="small text-secondary">
                      <th className="ps-3">#</th>
                      <th>Họ và tên</th>
                      <th>Chức danh</th>
                      <th>Đơn vị</th>
                      <th>Mức xếp loại</th>
                      {data?.showsScores && <th className="text-end">Điểm</th>}
                      <th>Công bố</th>
                      <th>Ghi chú</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item, index) => (
                      <tr key={`${item.periodId}-${item.fullName}-${index}`}>
                        <td className="ps-3 small text-secondary">{index + 1}</td>
                        <td className="small fw-semibold">
                          {item.recordId ? <Link href={`/evaluations/${item.recordId}`}>{item.fullName}</Link> : item.fullName}
                          {item.isOwn && <span className="badge text-bg-info ms-2">Của tôi</span>}
                          {!periodId && <div className="small text-secondary fw-normal">{item.periodName}</div>}
                        </td>
                        <td className="small">{item.positionTitle || "—"}</td>
                        <td className="small">{item.departmentName || "—"}</td>
                        <td className="small">{item.finalGradeName}</td>
                        {data?.showsScores && <td className="small text-end">{item.finalScore ?? "—"}</td>}
                        <td className="small">{formatDate(item.publishedAt)}</td>
                        <td className="small">
                          {item.underReview && <span className="badge text-bg-warning">Đang xem xét kiến nghị</span>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
