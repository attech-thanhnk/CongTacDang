"use client";

import React from "react";
import Link from "next/link";
import { PeriodReadinessDto, ReadinessIssueDto, STEP_MODE_LABELS } from "@/services/evaluationService";

/** Trang "Người thực hiện" mở sẵn đơn vị của hồ sơ (ưu tiên đơn vị chính quyền) và việc cần bổ sung người. */
function performersHref(issue?: Pick<ReadinessIssueDto, "departmentId" | "partyCellId" | "permission">): string {
  const params = new URLSearchParams();
  if (issue?.departmentId) params.set("departmentId", issue.departmentId);
  else if (issue?.partyCellId) params.set("partyCellId", issue.partyCellId);
  if (issue?.permission) params.set("permission", issue.permission);
  const query = params.toString();
  return query ? `/admin/performers?${query}` : "/admin/performers";
}

interface Props {
  readiness: PeriodReadinessDto | null;
  loading: boolean;
  onCheck: () => void;
}

/**
 * Bảng kiểm tra kẹt luồng: hồ sơ nào sẽ kẹt ở bước nào vì không có tài khoản đang hoạt động (ngoài chủ hồ sơ) có quyền
 * thực hiện bước trong phạm vi của hồ sơ. Kết quả do máy chủ tính (resolver + guard).
 */
export function ReadinessPanel({ readiness, loading, onCheck }: Props) {
  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <h2 className="h6 mb-0">Người thực hiện các bước</h2>
          <button type="button" className="btn btn-outline-primary btn-sm" disabled={loading} onClick={onCheck}>
            {loading ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-shield-check me-1" />}
            Kiểm tra lại
          </button>
        </div>
        {!readiness ? (
          <div className="small text-secondary">Chưa kiểm tra.</div>
        ) : readiness.checkedRecords === 0 ? (
          <div className="small text-secondary">Chưa có người được đánh giá để kiểm tra.</div>
        ) : readiness.ready ? (
          <div className="alert alert-success small mb-0">
            <i className="bi bi-check-circle me-1" />
            Mọi bước đều có người thực hiện ({readiness.checkedRecords} hồ sơ).
          </div>
        ) : (
          <>
            <div className="alert alert-warning small">
              <i className="bi bi-exclamation-triangle me-1" />
              {readiness.issues.length} bước chưa có người thực hiện, trên {readiness.checkedRecords} hồ sơ.
              Hãy <Link
                href={performersHref(
                  new Set(readiness.issues.map((i) => i.departmentId || i.partyCellId || "")).size === 1
                    ? { ...readiness.issues[0], permission: "" }
                    : undefined
                )}
              >
                gán vai trò
              </Link> có quyền tương ứng hoặc sửa cấu hình bước.
            </div>
            <div className="table-responsive" style={{ maxHeight: 360, overflowY: "auto" }}>
              <table className="table table-sm align-middle small mb-0">
                <thead>
                  <tr className="text-secondary"><th>Hồ sơ</th><th>Cách xét duyệt</th><th>Bước thiếu người làm</th><th>Quyền cần có</th><th>Phạm vi</th></tr>
                </thead>
                <tbody>
                  {readiness.issues.map((issue) => (
                    <tr key={`${issue.recordId}-${issue.step}`} title={issue.message}>
                      <td><Link href={`/evaluations/${issue.recordId}`}>{issue.fullName}</Link></td>
                      <td>{issue.workflowProfileName}</td>
                      <td>{issue.stepName}{issue.mode === "External" && <div className="text-secondary">{STEP_MODE_LABELS.External}</div>}</td>
                      <td>{issue.permissionName}</td>
                      <td><Link href={performersHref(issue)}>{issue.scope}</Link></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </section>
  );
}

export default ReadinessPanel;
