"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { useAuth } from "@/contexts/AuthContext";
import {
  BranchQuotaCheckDto,
  EvaluationPeriodDto,
  EvaluationRecordDto,
  evaluationService,
  gradeLabel,
} from "@/services/evaluationService";

/**
 * Danh sách hồ sơ đánh giá: hồ sơ của tôi (nếu có trong danh sách được đánh giá của kỳ) và các hồ sơ trong phạm vi
 * được xem (máy chủ lọc theo phạm vi). Thao tác trên hồ sơ thực hiện ở trang hồ sơ theo `actions`.
 */
export default function EvaluationsPage() {
  const { hasPermission } = useAuth();
  const canRead = hasPermission("evaluation.read");
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [quotas, setQuotas] = useState<BranchQuotaCheckDto[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    evaluationService
      .getPeriods()
      .then((list) => {
        const visible = list.filter((p) => p.status !== "Draft");
        setPeriods(visible);
        const active = visible.find((p) => p.isActive) || visible[0];
        if (active) setPeriodId(active.id);
        else setLoading(false);
      })
      .catch((err: any) => {
        setError(err?.message || "Không tải được danh sách kỳ đánh giá.");
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    if (!periodId) return;
    setLoading(true);
    setError(null);
    Promise.all([
      evaluationService.getMyRecord(periodId).catch(() => null),
      canRead ? evaluationService.getRecordsByPeriod(periodId) : Promise.resolve([] as EvaluationRecordDto[]),
      canRead ? evaluationService.checkBranchQuotas(periodId).catch(() => []) : Promise.resolve([] as BranchQuotaCheckDto[]),
    ])
      .then(([mine, list, quota]) => {
        setMyRecord(mine);
        setRecords(list);
        setQuotas(quota.filter((q) => q.totalCadres > 0));
      })
      .catch((err: any) => setError(err?.message || "Không tải được hồ sơ đánh giá."))
      .finally(() => setLoading(false));
  }, [periodId, canRead]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return term
      ? records.filter((r) =>
          [r.fullName, r.departmentName, r.partyCellName, r.statusDisplayName].some((v) => v?.toLowerCase().includes(term))
        )
      : records;
  }, [records, search]);

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Hồ sơ đánh giá"
        subTitle="Hồ sơ của tôi và các hồ sơ trong phạm vi được xem."
        actions={
          <div className="d-flex gap-2">
            <select className="form-select form-select-sm" aria-label="Kỳ đánh giá" value={periodId} onChange={(e) => setPeriodId(e.target.value)}>
              {periods.map((p) => (
                <option key={p.id} value={p.id}>{p.name} ({p.statusDisplayName})</option>
              ))}
            </select>
            <Link href="/work-queue" className="btn btn-primary btn-sm text-nowrap"><i className="bi bi-list-check me-1" />Việc cần xử lý</Link>
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {!loading && periods.length === 0 && <EmptyState title="Chưa có kỳ đánh giá đang diễn ra" />}

        {myRecord && (
          <section className="card border-0 shadow-sm">
            <div className="card-body d-flex flex-wrap justify-content-between align-items-center gap-2">
              <div>
                <div className="small text-secondary">Hồ sơ của tôi</div>
                <div className="fw-semibold">{myRecord.statusDisplayName}</div>
                {myRecord.returnReason && <div className="small text-danger">Bị trả lại: {myRecord.returnReason}</div>}
              </div>
              <Link href={`/evaluations/${myRecord.id}`} className="btn btn-outline-primary btn-sm">Mở hồ sơ của tôi</Link>
            </div>
          </section>
        )}

        {canRead && (
          <section className="card border-0 shadow-sm">
            <div className="card-header bg-white d-flex justify-content-between align-items-center gap-2">
              <h2 className="h6 mb-0">Hồ sơ trong phạm vi ({filtered.length})</h2>
              <input className="form-control form-control-sm" style={{ maxWidth: 260 }} placeholder="Tìm theo tên, Phòng, Chi bộ, trạng thái" value={search} onChange={(e) => setSearch(e.target.value)} />
            </div>
            <div className="card-body p-0">
              {loading ? (
                <div className="text-secondary p-3"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
              ) : filtered.length === 0 ? (
                <EmptyState title="Không có hồ sơ" description="Không có hồ sơ nào trong phạm vi bạn được xem ở kỳ này." />
              ) : (
                <div className="table-responsive">
                  <table className="table table-hover table-sm align-middle mb-0">
                    <thead>
                      <tr className="small text-secondary">
                        <th className="ps-3">Cán bộ</th>
                        <th>Phòng</th>
                        <th>Chi bộ</th>
                        <th>Trạng thái</th>
                        <th>Tự chấm</th>
                        <th>Mức</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {filtered.map((r) => (
                        <tr key={r.id}>
                          <td className="ps-3 small fw-semibold">{r.fullName}</td>
                          <td className="small">{r.departmentName || "—"}</td>
                          <td className="small">{r.partyCellName || "—"}</td>
                          <td className="small">{r.statusDisplayName}</td>
                          <td className="small">{r.totalSelfScore || "—"}</td>
                          <td className="small">{gradeLabel(r.finalGrade !== "ChuaXepLoai" ? r.finalGrade : r.appraisalProposedGrade)}</td>
                          <td className="text-end pe-3"><Link href={`/evaluations/${r.id}`} className="btn btn-outline-primary btn-sm">Xem</Link></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </section>
        )}

        {canRead && quotas.length > 0 && (
          <section className="card border-0 shadow-sm">
            <div className="card-header bg-white"><h2 className="h6 mb-0">Kiểm soát trần Hoàn thành xuất sắc theo Chi bộ (Mẫu 15)</h2></div>
            <div className="card-body p-0">
              <table className="table table-sm mb-0">
                <thead><tr className="small text-secondary"><th className="ps-3">Chi bộ</th><th>Số hồ sơ</th><th>HT tốt trở lên</th><th>Tối đa XS</th><th>Đề xuất XS</th><th /></tr></thead>
                <tbody>
                  {quotas.map((q) => (
                    <tr key={q.branchId} className="small">
                      <td className="ps-3">{q.branchName}</td>
                      <td>{q.totalCadres}</td>
                      <td>{q.goodOrBetterCount}</td>
                      <td>{q.maxExcellentAllowed}</td>
                      <td>{q.proposedExcellentCount}</td>
                      <td>{q.isExceedingQuota ? <span className="badge text-bg-danger">Vượt trần</span> : <span className="badge text-bg-success">Đạt</span>}</td>
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
