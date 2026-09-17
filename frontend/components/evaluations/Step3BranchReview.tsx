"use client";

import React from "react";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { Button } from "@/components/common";

interface Step3BranchReviewProps {
  records: EvaluationRecordDto[];
  selectedRecord: EvaluationRecordDto | null;
  onSelectRecord: (record: EvaluationRecordDto) => void;
  comment: string;
  onChangeComment: (val: string) => void;
  proposedGrade: string;
  onChangeProposedGrade: (val: string) => void;
  votesExcellent: number;
  onChangeVotesExcellent: (v: number) => void;
  votesGood: number;
  onChangeVotesGood: (v: number) => void;
  votesSatisfactory: number;
  onChangeVotesSatisfactory: (v: number) => void;
  votesUnsatisfactory: number;
  onChangeVotesUnsatisfactory: (v: number) => void;
  totalVoters: number;
  onChangeTotalVoters: (v: number) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  onOpenDocViewer: (attId: string, fileName?: string | null) => void;
  onOpenPdf?: (record: EvaluationRecordDto, templateType?: any) => void;
}

export function Step3BranchReview({
  records,
  selectedRecord,
  onSelectRecord,
  comment,
  onChangeComment,
  proposedGrade,
  onChangeProposedGrade,
  votesExcellent,
  onChangeVotesExcellent,
  votesGood,
  onChangeVotesGood,
  votesSatisfactory,
  onChangeVotesSatisfactory,
  votesUnsatisfactory,
  onChangeVotesUnsatisfactory,
  totalVoters,
  onChangeTotalVoters,
  onSubmit,
  isSubmitting,
  onOpenDocViewer,
  onOpenPdf,
}: Step3BranchReviewProps) {
  const [searchTerm, setSearchTerm] = React.useState("");
  const [filterStatus, setFilterStatus] = React.useState<"all" | "pending" | "reviewed">("all");

  const sumVotes =
    (votesExcellent || 0) +
    (votesGood || 0) +
    (votesSatisfactory || 0) +
    (votesUnsatisfactory || 0);

  const isVotesValid = sumVotes === totalVoters && totalVoters > 0;

  const getGradeLabel = (g?: string | null) => {
    switch (g) {
      case "HoanThanhXuatSac": return "HT Xuất sắc";
      case "HoanThanhTot": return "HT Tốt";
      case "HoanThanh": return "Hoàn thành";
      case "KhongHoanThanh": return "Không HT";
      default: return g || "Chưa chọn";
    }
  };

  const filteredRecords = records.filter((rec) => {
    const term = searchTerm.toLowerCase().trim();
    const matchTerm =
      !term ||
      rec.fullName?.toLowerCase().includes(term) ||
      rec.positionTitle?.toLowerCase().includes(term) ||
      rec.partyRole?.toLowerCase().includes(term);

    const isReviewed = rec.status === "BranchReviewed" || (rec.votesExcellent ?? 0) > 0;
    if (filterStatus === "pending") return matchTerm && !isReviewed;
    if (filterStatus === "reviewed") return matchTerm && isReviewed;
    return matchTerm;
  });

  return (
    <div className="border rounded bg-white" style={{ borderColor: "#e2e8f0" }}>
      <div className="card-header bg-white border-bottom py-2.5 px-3 d-flex justify-content-between align-items-center" style={{ borderColor: "#e2e8f0" }}>
        <div className="d-flex align-items-center gap-2">
          <span
            className="badge"
            style={{ backgroundColor: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe" }}
          >
            Mẫu 10, 11, 13
          </span>
          <span className="fw-semibold text-dark small">
            Chi bộ đánh giá & Bỏ phiếu kín
          </span>
        </div>
      </div>

      <div className="card-body p-3">
        <div className="row g-3">
          {/* Cột trái: Danh sách cán bộ */}
          <div className="col-12 col-lg-4 border-end" style={{ borderColor: "#e2e8f0" }}>
            <div className="d-flex justify-content-between align-items-center mb-2">
              <span className="small fw-semibold text-secondary">Cán bộ trong Chi bộ</span>
              <span className="badge bg-secondary-subtle text-secondary">{records.length} hồ sơ</span>
            </div>

            {/* Ô tìm kiếm & Lọc */}
            <div className="mb-2 space-y-1.5">
              <div className="input-group input-group-sm">
                <span className="input-group-text bg-light text-secondary border-end-0">
                  <i className="bi bi-search"></i>
                </span>
                <input
                  type="text"
                  className="form-control border-start-0"
                  placeholder="Tìm theo họ tên..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
                {searchTerm && (
                  <button
                    type="button"
                    className="btn btn-outline-secondary"
                    onClick={() => setSearchTerm("")}
                    title="Xóa tìm kiếm"
                  >
                    <i className="bi bi-x"></i>
                  </button>
                )}
              </div>

              <div className="d-flex gap-1 btn-group btn-group-sm w-100" role="group">
                <button
                  type="button"
                  onClick={() => setFilterStatus("all")}
                  className={`btn btn-sm py-0.5 px-1.5 ${filterStatus === "all" ? "btn-primary" : "btn-light border"}`}
                  style={{ fontSize: "11px" }}
                >
                  Tất cả ({records.length})
                </button>
                <button
                  type="button"
                  onClick={() => setFilterStatus("pending")}
                  className={`btn btn-sm py-0.5 px-1.5 ${filterStatus === "pending" ? "btn-primary" : "btn-light border"}`}
                  style={{ fontSize: "11px" }}
                >
                  Chưa chấm
                </button>
                <button
                  type="button"
                  onClick={() => setFilterStatus("reviewed")}
                  className={`btn btn-sm py-0.5 px-1.5 ${filterStatus === "reviewed" ? "btn-primary" : "btn-light border"}`}
                  style={{ fontSize: "11px" }}
                >
                  Đã xong
                </button>
              </div>
            </div>

            {records.length === 0 ? (
              <div className="text-muted small text-center py-4">
                Không có hồ sơ nào cần thẩm tra.
              </div>
            ) : filteredRecords.length === 0 ? (
              <div className="text-muted small text-center py-4">
                Không tìm thấy cán bộ phù hợp.
              </div>
            ) : (
              <div className="list-group list-group-flush border rounded overflow-y-auto" style={{ borderColor: "#e2e8f0", maxHeight: "420px" }}>
                {filteredRecords.map((rec) => {
                  const isSelected = selectedRecord?.id === rec.id;
                  const isReviewed = rec.status === "BranchReviewed" || (rec.votesExcellent ?? 0) > 0;

                  return (
                    <button
                      key={rec.id}
                      type="button"
                      onClick={() => onSelectRecord(rec)}
                      className="list-group-item list-group-item-action p-2 text-start small transition"
                      style={{
                        backgroundColor: isSelected ? "#eff6ff" : "transparent",
                        borderColor: "#e2e8f0",
                        borderLeft: isSelected ? "3px solid #1d4ed8" : "3px solid transparent",
                      }}
                    >
                      <div className="d-flex justify-content-between align-items-center">
                        <span className={`fw-semibold ${isSelected ? "text-primary" : "text-dark"}`}>
                          {rec.fullName}
                        </span>
                        <span
                          className="badge"
                          style={{
                            fontSize: "10px",
                            backgroundColor: isReviewed ? "#ecfdf5" : "#f1f5f9",
                            color: isReviewed ? "#047857" : "#475569",
                            border: isReviewed ? "1px solid #a7f3d0" : "1px solid #cbd5e1",
                          }}
                        >
                          {isReviewed ? "Đã xong" : "Chưa chấm"}
                        </span>
                      </div>
                      <div className="d-flex justify-content-between text-muted mt-1" style={{ fontSize: "11px" }}>
                        <span>{rec.positionTitle || "Cán bộ"}</span>
                        <span>Tự chấm: <strong className="text-dark">{rec.totalSelfScore}đ</strong> ({getGradeLabel(rec.selfProposedGrade)})</span>
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </div>

          {/* Cột phải: Thẩm tra chi bộ */}
          <div className="col-12 col-lg-8">
            {!selectedRecord ? (
              <div className="h-100 d-flex align-items-center justify-content-center p-4 text-muted small fst-italic border rounded bg-light">
                Chọn cán bộ trong danh sách để thẩm tra và nhập phiếu bầu.
              </div>
            ) : (
              <div className="space-y-3">
                {/* Thông tin nhanh cán bộ */}
                <div className="d-flex justify-content-between align-items-center p-2 rounded bg-light border" style={{ borderColor: "#e2e8f0" }}>
                  <div>
                    <strong className="text-dark">{selectedRecord.fullName}</strong>
                    <span className="text-secondary small ms-2">({selectedRecord.partyCellName} • {selectedRecord.positionTitle})</span>
                  </div>
                  <div className="small">
                    Tự chấm: <strong className="text-primary">{selectedRecord.totalSelfScore}đ</strong>
                  </div>
                </div>

                {/* Danh sách nhiệm vụ & tệp minh chứng */}
                <div className="table-responsive border rounded" style={{ borderColor: "#e2e8f0" }}>
                  <table className="table table-sm table-hover align-middle mb-0 small">
                    <thead>
                      <tr>
                        <th style={{ width: "35px" }} className="text-center">STT</th>
                        <th>Nhiệm vụ & Tiêu chuẩn</th>
                        <th style={{ width: "70px" }} className="text-center">Trọng số</th>
                        <th style={{ width: "70px" }} className="text-center">Điểm</th>
                        <th style={{ width: "110px" }} className="text-center">Minh chứng</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(selectedRecord.tasks || []).map((task, idx) => {
                        const attId = task.attachmentId;
                        const attName = task.attachmentFileName || task.attachmentOriginalName;

                        return (
                          <tr key={task.id}>
                            <td className="text-center text-muted">{idx + 1}</td>
                            <td>
                              <div className="fw-medium text-dark">{task.taskName}</div>
                              <div className="text-muted" style={{ fontSize: "11px" }}>{task.targetOutput}</div>
                            </td>
                            <td className="text-center">{task.weight}đ</td>
                            <td className="text-center fw-semibold text-success">{task.selfScore}đ</td>
                            <td className="text-center">
                              {attId ? (
                                <Button
                                  size="sm"
                                  variant="outline-primary"
                                  icon="bi-file-earmark-pdf"
                                  onClick={() => onOpenDocViewer(attId, attName)}
                                  title={attName || "Xem PDF"}
                                  style={{ padding: "2px 6px", fontSize: "11px", maxWidth: "105px" }}
                                  className="text-truncate"
                                >
                                  {attName || "Xem PDF"}
                                </Button>
                              ) : (
                                <span className="text-muted" style={{ fontSize: "11px" }}>—</span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Ý kiến nhận xét & đề xuất */}
                <div className="row g-2">
                  <div className="col-12 col-md-8">
                    <label className="form-label small fw-semibold text-secondary mb-1">
                      Ý kiến nhận xét của Chi ủy:
                    </label>
                    <input
                      type="text"
                      value={comment}
                      onChange={(e) => onChangeComment(e.target.value)}
                      placeholder="Nhận xét ưu điểm, hạn chế..."
                      className="form-control form-control-sm"
                    />
                  </div>

                  <div className="col-12 col-md-4">
                    <label className="form-label small fw-semibold text-secondary mb-1">
                      Chi bộ đề xuất:
                    </label>
                    <select
                      value={proposedGrade}
                      onChange={(e) => onChangeProposedGrade(e.target.value)}
                      className="form-select form-select-sm"
                    >
                      <option value="HoanThanhXuatSac">Hoàn thành xuất sắc</option>
                      <option value="HoanThanhTot">Hoàn thành tốt</option>
                      <option value="HoanThanh">Hoàn thành</option>
                      <option value="KhongHoanThanh">Không hoàn thành</option>
                    </select>
                  </div>
                </div>

                {/* Khối nhập phiếu bầu kín */}
                <div className="border rounded p-2.5 bg-light" style={{ borderColor: "#e2e8f0" }}>
                  <div className="d-flex justify-content-between align-items-center mb-1.5">
                    <span className="small fw-semibold text-dark">Kết quả bỏ phiếu kín:</span>
                    <div className="d-flex align-items-center gap-1 small">
                      <span className="text-secondary">Tổng cử tri:</span>
                      <input
                        type="number"
                        min="1"
                        value={totalVoters}
                        onChange={(e) => onChangeTotalVoters(parseInt(e.target.value) || 0)}
                        className="form-control form-control-sm text-center p-0 fw-bold"
                        style={{ width: "50px", height: "26px" }}
                      />
                    </div>
                  </div>

                  <div className="row g-1 text-center" style={{ fontSize: "11px" }}>
                    <div className="col-3">
                      <span className="text-secondary d-block">Xuất sắc</span>
                      <input
                        type="number"
                        min="0"
                        value={votesExcellent}
                        onChange={(e) => onChangeVotesExcellent(parseInt(e.target.value) || 0)}
                        className="form-control form-control-sm text-center p-1 fw-bold text-success"
                      />
                    </div>
                    <div className="col-3">
                      <span className="text-secondary d-block">Tốt</span>
                      <input
                        type="number"
                        min="0"
                        value={votesGood}
                        onChange={(e) => onChangeVotesGood(parseInt(e.target.value) || 0)}
                        className="form-control form-control-sm text-center p-1 fw-bold text-primary"
                      />
                    </div>
                    <div className="col-3">
                      <span className="text-secondary d-block">Hoàn thành</span>
                      <input
                        type="number"
                        min="0"
                        value={votesSatisfactory}
                        onChange={(e) => onChangeVotesSatisfactory(parseInt(e.target.value) || 0)}
                        className="form-control form-control-sm text-center p-1 fw-bold text-secondary"
                      />
                    </div>
                    <div className="col-3">
                      <span className="text-secondary d-block">Không HT</span>
                      <input
                        type="number"
                        min="0"
                        value={votesUnsatisfactory}
                        onChange={(e) => onChangeVotesUnsatisfactory(parseInt(e.target.value) || 0)}
                        className="form-control form-control-sm text-center p-1 fw-bold text-danger"
                      />
                    </div>
                  </div>

                  {!isVotesValid && (
                    <div className="text-danger small mt-1.5" style={{ fontSize: "11px" }}>
                      * Tổng phiếu ({sumVotes}) chưa khớp với cử tri ({totalVoters}).
                    </div>
                  )}
                </div>

                {/* Nút thao tác */}
                <div className="d-flex justify-content-end gap-2 pt-1">
                  {onOpenPdf && (
                    <Button
                      variant="outline-secondary"
                      onClick={() => onOpenPdf(selectedRecord, "mau10")}
                      icon="bi-file-earmark-pdf"
                    >
                      Xuất Mẫu 10 (PDF)
                    </Button>
                  )}
                  <Button
                    variant="primary"
                    onClick={onSubmit}
                    disabled={!isVotesValid}
                    loading={isSubmitting}
                    loadingText="Đang lưu..."
                    icon="bi-check2-circle"
                    className="px-4"
                  >
                    Lưu đánh giá (Mẫu 10)
                  </Button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
