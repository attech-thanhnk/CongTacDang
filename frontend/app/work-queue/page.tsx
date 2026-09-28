"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { EvaluationPeriodDto, WorkQueueDto, evaluationService } from "@/services/evaluationService";

const formatDate = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" }).format(new Date(value)) : null;

/**
 * Việc cần xử lý: hồ sơ đang chờ chính người dùng ở bước họ có quyền, trong phạm vi được gán (máy chủ lọc).
 * Mặc định gồm mọi kỳ đang mở/khóa dữ liệu; chọn kỳ để lọc.
 */
export default function WorkQueuePage() {
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    evaluationService.getPeriods().then(setPeriods).catch(() => setPeriods([]));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setQueue(await evaluationService.getWorkQueue(periodId || undefined));
    } catch (err: any) {
      setError(err?.message || "Không tải được danh sách việc cần xử lý.");
    } finally {
      setLoading(false);
    }
  }, [periodId]);

  useEffect(() => {
    load();
  }, [load]);

  const selectablePeriods = periods.filter((p) => p.status !== "Draft");

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Việc cần xử lý"
        subTitle="Hồ sơ đánh giá đang chờ bạn thực hiện, nhóm theo bước."
        badge={queue ? <span className="badge text-bg-primary">{queue.total}</span> : undefined}
        actions={
          <div className="d-flex gap-2">
            <select className="form-select form-select-sm" aria-label="Kỳ đánh giá" value={periodId} onChange={(e) => setPeriodId(e.target.value)}>
              <option value="">Tất cả kỳ đang mở</option>
              {selectablePeriods.map((p) => (
                <option key={p.id} value={p.id}>{p.name} ({p.statusDisplayName})</option>
              ))}
            </select>
            <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => load()} aria-label="Tải lại">
              <i className="bi bi-arrow-clockwise" />
            </button>
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {loading ? (
          <div className="text-secondary"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        ) : !queue || queue.total === 0 ? (
          <EmptyState icon="bi-check2-circle" title="Không có việc nào đang chờ bạn" description="Khi có hồ sơ cần bạn xử lý, hồ sơ sẽ xuất hiện ở đây." />
        ) : (
          queue.groups.map((group) => (
            <section key={group.step} className="card border-0 shadow-sm">
              <div className="card-header bg-white d-flex justify-content-between align-items-center">
                <h2 className="h6 mb-0">{group.stepName}</h2>
                <span className="badge text-bg-light">{group.count} hồ sơ</span>
              </div>
              <div className="card-body p-0">
                <div className="table-responsive">
                  <table className="table table-hover table-sm align-middle mb-0">
                    <thead>
                      <tr className="small text-secondary">
                        <th className="ps-3">Cán bộ</th>
                        <th>Phòng / Chi bộ</th>
                        <th>Kỳ</th>
                        <th>Thời hạn</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {group.items.map((item) => (
                        <tr key={item.recordId}>
                          <td className="ps-3">
                            <div className="fw-semibold small">
                              {item.fullName}
                              {item.isOwnRecord && <span className="badge text-bg-info ms-2">Hồ sơ của tôi</span>}
                            </div>
                            {item.returnReason && <div className="small text-danger">Bị trả lại: {item.returnReason}</div>}
                          </td>
                          <td className="small">{item.departmentName || "—"} · {item.partyCellName || "—"}</td>
                          <td className="small">{item.periodName}</td>
                          <td className={`small ${item.overdue ? "text-danger fw-semibold" : ""}`}>
                            {formatDate(item.deadline) || "—"}{item.overdue ? " (quá hạn)" : ""}
                          </td>
                          <td className="text-end pe-3">
                            <Link href={`/evaluations/${item.recordId}`} className="btn btn-primary btn-sm">Xử lý</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </section>
          ))
        )}
      </div>
    </div>
  );
}
