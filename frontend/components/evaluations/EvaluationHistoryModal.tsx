"use client";

import React from "react";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { EvaluationRecordHistoryDto } from "@/services/auditService";

interface EvaluationHistoryModalProps {
  isOpen: boolean;
  record: EvaluationRecordDto | null;
  history: EvaluationRecordHistoryDto[];
  loading: boolean;
  onClose: () => void;
}

const statusLabels: Record<string, string> = {
  Draft: "Bản nháp",
  TasksSubmitted: "Đã gửi đăng ký nhiệm vụ",
  TasksApproved: "Nhiệm vụ đã được duyệt",
  SelfEvaluated: "Đã tự chấm điểm",
  Voted: "Chi bộ đã bỏ phiếu",
  Reviewed: "Đã thẩm định hồ sơ",
  Approved: "Đã phê duyệt chính thức",
  Published: "Đã công bố kết quả",
};

/** Chuyển mã trạng thái quy trình thành nhãn tiếng Việt. */
function statusLabel(value?: string | null) {
  return value ? statusLabels[value] || value : "Bắt đầu hồ sơ";
}

/** Định dạng mốc thời gian trong timeline hồ sơ. */
function formatDate(value: string) {
  return new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

/** Modal hiển thị timeline chuyển trạng thái của một hồ sơ đánh giá. */
export function EvaluationHistoryModal({
  isOpen,
  record,
  history,
  loading,
  onClose,
}: EvaluationHistoryModalProps) {
  if (!isOpen) return null;

  return (
    <div className="modal fade show d-block" tabIndex={-1} style={{ backgroundColor: "rgba(15, 23, 42, 0.55)", zIndex: 1070 }}>
      <div className="modal-dialog modal-dialog-centered modal-dialog-scrollable" style={{ maxWidth: 620 }}>
        <div className="modal-content border-0 shadow-lg" style={{ borderRadius: 14 }}>
          <div className="modal-header px-4 py-3">
            <div className="d-flex align-items-center gap-2">
              <div className="d-flex align-items-center justify-content-center rounded-3" style={{ width: 34, height: 34, background: "#eff6ff", color: "#1d4ed8" }}>
                <i className="bi bi-clock-history" />
              </div>
              <div>
                <h2 className="modal-title fs-6 fw-bold mb-0">Lịch sử hồ sơ đánh giá</h2>
                <div className="small text-secondary">{record?.fullName || "Hồ sơ chưa xác định"}</div>
              </div>
            </div>
            <button type="button" className="btn-close" aria-label="Đóng" onClick={onClose} />
          </div>

          <div className="modal-body px-4 py-3">
            {loading ? (
              <div className="text-center text-secondary py-5">
                <span className="spinner-border spinner-border-sm me-2" />Đang tải lịch sử...
              </div>
            ) : history.length === 0 ? (
              <div className="text-center text-secondary py-5">
                <i className="bi bi-clock-history fs-2 d-block mb-2" />
                Chưa có mốc chuyển trạng thái được ghi nhận.
              </div>
            ) : (
              <div className="position-relative">
                {history.map((item, index) => (
                  <div className="d-flex gap-3 position-relative" key={item.id}>
                    {index < history.length - 1 && <div style={{ position: "absolute", left: 9, top: 22, bottom: -12, width: 1, background: "#dbe4f2" }} />}
                    <div className="rounded-circle mt-1" style={{ width: 20, height: 20, flexShrink: 0, background: index === history.length - 1 ? "#1a56db" : "#dbeafe", border: "4px solid #fff", boxShadow: "0 0 0 1px #bfdbfe", zIndex: 1 }} />
                    <div className="pb-4 flex-grow-1">
                      <div className="d-flex flex-wrap justify-content-between gap-2">
                        <div className="fw-semibold text-dark">
                          {statusLabel(item.fromStatus)} <i className="bi bi-arrow-right mx-1 text-secondary" /> {statusLabel(item.toStatus)}
                        </div>
                        <time className="small text-secondary">{formatDate(item.createdAt)}</time>
                      </div>
                      <div className="small text-secondary mt-1">
                        <i className="bi bi-person me-1" />{item.actorName || "system"}
                      </div>
                      {item.comment && <div className="small mt-2 p-2 rounded-2" style={{ background: "#f8fafd", color: "#334155" }}>{item.comment}</div>}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="modal-footer px-4 py-2">
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={onClose}>Đóng</button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default EvaluationHistoryModal;
