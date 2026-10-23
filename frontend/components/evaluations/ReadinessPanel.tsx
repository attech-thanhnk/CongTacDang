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

/** Bước kẹt vì không có tài khoản hoạt động (ngoài chủ hồ sơ) có quyền thực hiện; máy chủ tính. */
export function ReadinessPanel({ readiness, loading, onCheck }: Props) {
  return (
    <section className="card">
      <div className="card-body">
        <div className={`d-flex flex-wrap justify-content-between align-items-center gap-2 ${readiness && readiness.checkedRecords > 0 && !readiness.ready ? "mb-3" : ""}`}>
          <div className="flex-grow-1">
            {!readiness ? (
              <span className="text-secondary">Chưa kiểm tra.</span>
            ) : readiness.checkedRecords === 0 ? (
              <span className="text-secondary">Chưa có người được đánh giá.</span>
            ) : readiness.ready ? (
              <span className="text-success"><i className="bi bi-check-circle-fill me-1" />Mọi bước đều có người thực hiện.</span>
            ) : (
              <span>
                <i className="bi bi-exclamation-triangle-fill text-warning me-1" />
                <span className="num">{readiness.issues.length}</span> bước chưa có người phụ trách.{" "}
                <Link
                  href={performersHref(
                    new Set(readiness.issues.map((i) => i.departmentId || i.partyCellId || "")).size === 1
                      ? { ...readiness.issues[0], permission: "" }
                      : undefined
                  )}
                >
                  Phân công người phụ trách
                </Link>
              </span>
            )}
          </div>
          <button type="button" className="btn btn-outline-secondary btn-sm" disabled={loading} onClick={onCheck}>
            {loading ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-arrow-clockwise me-1" />}
            Kiểm tra lại
          </button>
        </div>
        {readiness && readiness.checkedRecords > 0 && !readiness.ready && (
          <>
            <div className="table-responsive" style={{ maxHeight: 360, overflowY: "auto" }}>
              <table className="table table-sm align-middle small mb-0">
                <thead>
                  <tr className="text-secondary"><th>Hồ sơ</th><th>Cách xét duyệt</th><th>Bước chưa có người phụ trách</th><th>Vai trò</th><th>Nơi phân công</th></tr>
                </thead>
                <tbody>
                  {readiness.issues.map((issue) => (
                    <tr key={`${issue.recordId}-${issue.step}`} title={issue.message}>
                      <td><Link href={`/evaluations/${issue.recordId}`}>{issue.fullName}</Link></td>
                      <td>{issue.workflowProfileName}</td>
                      <td>{issue.stepName}{issue.mode === "External" && <div className="text-secondary">{STEP_MODE_LABELS.External}</div>}</td>
                      <td>
                        {issue.roleNames?.length ? issue.roleNames.join(" hoặc ") : issue.permissionName}
                        {!issue.roleNames?.length && issue.permission && <div className="text-secondary">Chưa vai trò nào có quyền này</div>}
                      </td>
                      <td>{issue.scopeName ? <Link href={performersHref(issue)}>{issue.scopeName}</Link> : "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="small text-secondary mt-2">
              Có thể phân công tại nơi ghi trong cột &quot;Nơi phân công&quot;, đơn vị cấp trên hoặc Toàn công ty. Không phân công cho chủ hồ sơ.
            </div>
          </>
        )}
      </div>
    </section>
  );
}

export default ReadinessPanel;
