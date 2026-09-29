"use client";

import React from "react";
import Link from "next/link";
import { PeriodReadinessDto, STEP_MODE_LABELS } from "@/services/evaluationService";

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
          <h2 className="h6 mb-0">Kiểm tra kẹt luồng</h2>
          <button type="button" className="btn btn-outline-primary btn-sm" disabled={loading} onClick={onCheck}>
            {loading ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-shield-check me-1" />}
            Kiểm tra lại
          </button>
        </div>
        {!readiness ? (
          <div className="small text-secondary">Chưa kiểm tra.</div>
        ) : readiness.ready ? (
          <div className="alert alert-success small mb-0">
            <i className="bi bi-check-circle me-1" />
            Không có cảnh báo: {readiness.checkedRecords} hồ sơ chưa công bố đều có người thực hiện được mọi bước còn lại.
          </div>
        ) : (
          <>
            <div className="alert alert-warning small">
              <i className="bi bi-exclamation-triangle me-1" />
              {readiness.issues.length} cảnh báo trên {readiness.checkedRecords} hồ sơ: các hồ sơ dưới đây sẽ kẹt nếu không bổ sung người thực hiện.
              Hãy <Link href="/admin/users">gán vai trò</Link> có quyền tương ứng trong phạm vi của hồ sơ, hoặc sửa cấu hình bước của hồ sơ luồng.
            </div>
            <div className="table-responsive" style={{ maxHeight: 360, overflowY: "auto" }}>
              <table className="table table-sm align-middle small mb-0">
                <thead>
                  <tr className="text-secondary"><th>Hồ sơ</th><th>Hồ sơ luồng</th><th>Bước sẽ kẹt</th><th>Quyền cần có</th><th>Phạm vi</th></tr>
                </thead>
                <tbody>
                  {readiness.issues.map((issue) => (
                    <tr key={`${issue.recordId}-${issue.step}`} title={issue.message}>
                      <td><Link href={`/evaluations/${issue.recordId}`}>{issue.fullName}</Link></td>
                      <td>{issue.workflowProfileName}</td>
                      <td>{issue.stepName}{issue.mode === "External" && <div className="text-secondary">{STEP_MODE_LABELS.External}</div>}</td>
                      <td>{issue.permissionName}</td>
                      <td>{issue.scope}</td>
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
