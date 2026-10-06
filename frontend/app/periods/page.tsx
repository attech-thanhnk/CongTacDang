"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { PeriodStatusBadge } from "@/components/periods/PeriodStatusBadge";
import { CreatePeriodModal } from "@/components/periods/CreatePeriodModal";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { EvaluationPeriodDto, PeriodPresetDto, evaluationService } from "@/services/evaluationService";

/** Kỳ đánh giá: danh sách (mọi người đã đăng nhập) và tạo kỳ từ kiểu kỳ dựng sẵn (period.manage). */
export default function PeriodsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("period.manage");
  const { toast } = useToast();
  const router = useRouter();
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [presets, setPresets] = useState<PeriodPresetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const canSeeCriteria = canManage || hasPermission("criteria.manage");

  useEffect(() => {
    Promise.all([evaluationService.getPeriods(), evaluationService.getPresets()])
      .then(([list, presetList]) => {
        setPeriods(list);
        setPresets(presetList);
      })
      .catch((err: any) => toast.error(err?.message || "Không tải được danh sách kỳ."))
      .finally(() => setLoading(false));
  }, [toast]);

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
              <EmptyState title="Chưa có kỳ đánh giá" />
            ) : (
              <div className="table-responsive"><table className="table table-hover align-middle mb-0">
                <thead>
                  <tr><th>Kỳ</th><th>Trạng thái</th><th>Bộ tiêu chí</th><th>Hồ sơ luồng</th><th className="text-end">Người được đánh giá</th><th /></tr>
                </thead>
                <tbody>
                  {periods.map((p) => (
                    <tr key={p.id} style={{ cursor: "pointer" }} onClick={() => router.push(`/periods/${p.id}`)}>
                      <td className="fw-semibold">
                        {p.name}
                        {p.isActive && <span className="tag t-ok ms-2">Hiện hành</span>}
                        <div className="small text-secondary fw-normal">Quý {p.quarter}/{p.year}</div>
                      </td>
                      <td><PeriodStatusBadge status={p.status} label={p.statusDisplayName} /></td>
                      <td>{p.criteria ? <>{p.criteria.name}<div className="small text-secondary">Mẫu {p.criteria.selfScoreForm}</div></> : <span className="text-danger">Chưa chọn</span>}</td>
                      <td>{p.settings?.profiles?.map((profile) => profile.name).join(", ") || "—"}</td>
                      <td className="text-end num">{p.totalRecords}</td>
                      <td className="text-end">
                        <Link className="btn btn-outline-secondary btn-sm" href={`/periods/${p.id}`} onClick={(e) => e.stopPropagation()}>{canManage ? "Cấu hình" : "Xem"}</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table></div>
            )}
          </div>
        </section>
      </div>

      {creating && (
        <CreatePeriodModal
          presets={presets}
          onClose={() => setCreating(false)}
          onCreated={(period) => router.push(`/periods/${period.id}`)}
        />
      )}
    </div>
  );
}
