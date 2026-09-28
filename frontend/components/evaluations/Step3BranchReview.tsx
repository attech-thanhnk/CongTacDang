"use client";

import React, { useState } from "react";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { Button } from "@/components/common";

export interface BranchMeetingVoteState {
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
  proposedGrade: string;
  comment: string;
}

interface Step3BranchReviewProps {
  records: EvaluationRecordDto[];
  totalVoters: number;
  onChangeTotalVoters: (v: number) => void;
  meetingVotes: { [recordId: string]: BranchMeetingVoteState };
  onChangeMemberVote: (recordId: string, field: keyof BranchMeetingVoteState, value: any) => void;
  onSubmitMeeting: () => void;
  isSubmitting: boolean;
  onOpenDocViewer: (attId: string, fileName?: string | null) => void;
  onExportMau11Docx?: () => void;
  onExportMau13Docx?: () => void;
}

export function Step3BranchReview({
  records,
  totalVoters,
  onChangeTotalVoters,
  meetingVotes,
  onChangeMemberVote,
  onSubmitMeeting,
  isSubmitting,
  onOpenDocViewer,
  onExportMau11Docx,
  onExportMau13Docx,
}: Step3BranchReviewProps) {
  const [searchTerm, setSearchTerm] = useState("");
  const [detailRecord, setDetailRecord] = useState<EvaluationRecordDto | null>(null);

  const getGradeLabel = (g?: string | null) => {
    switch (g) {
      case "HoanThanhXuatSac":
        return "HT Xuất sắc";
      case "HoanThanhTot":
        return "HT Tốt";
      case "HoanThanh":
        return "Hoàn thành";
      case "KhongHoanThanh":
        return "Không HT";
      default:
        return g || "Chưa chọn";
    }
  };

  const filteredRecords = records.filter((rec) => {
    const term = searchTerm.toLowerCase().trim();
    return (
      !term ||
      rec.fullName?.toLowerCase().includes(term) ||
      rec.positionTitle?.toLowerCase().includes(term) ||
      rec.partyRole?.toLowerCase().includes(term)
    );
  });

  // Đếm số cán bộ có tổng phiếu = totalVoters
  const validVotesCount = records.filter((rec) => {
    const v = meetingVotes[rec.id];
    if (!v) return false;
    const sum =
      (v.votesExcellent || 0) +
      (v.votesGood || 0) +
      (v.votesSatisfactory || 0) +
      (v.votesUnsatisfactory || 0);
    return sum === totalVoters && totalVoters > 0;
  }).length;

  const branchName = records[0]?.partyCellName || "Chi bộ trực thuộc";

  return (
    <div className="border rounded bg-white shadow-sm" style={{ borderColor: "#e2e8f0" }}>
      {/* Header đồng nhất */}
      <div
        className="card-header bg-white border-bottom py-2.5 px-3 d-flex flex-wrap justify-content-between align-items-center gap-2"
        style={{ borderColor: "#e2e8f0" }}
      >
        <div>
          <h2 className="mb-0 text-dark" style={{ fontSize: "14px", fontWeight: 600, lineHeight: 1.4 }}>
            Đánh giá, bỏ phiếu tín nhiệm Chi bộ — {branchName}
          </h2>
        </div>

        <div className="d-flex align-items-center gap-2">
          {onExportMau11Docx && (
            <Button
              size="sm"
              variant="outline-primary"
              onClick={onExportMau11Docx}
              icon="bi-file-earmark-word"
              title="Tải phiếu bầu bỏ phiếu kín định dạng Word để in cho cuộc họp Chi bộ"
            >
              Tải phiếu bầu (.docx)
            </Button>
          )}
          {onExportMau13Docx && (
            <Button
              size="sm"
              variant="outline-primary"
              onClick={onExportMau13Docx}
              icon="bi-file-earmark-word"
              title="Tải biên bản kiểm phiếu định dạng Word đã điền kết quả"
            >
              Tải biên bản kiểm phiếu (.docx)
            </Button>
          )}
        </div>
      </div>

      {/* Meeting Parameters Bar */}
      <div
        className="p-3 border-bottom d-flex flex-wrap justify-content-between align-items-center gap-3"
        style={{ backgroundColor: "#f8fafc", borderColor: "#e2e8f0" }}
      >
        <div className="d-flex flex-wrap align-items-center gap-3">
          <div className="d-flex align-items-center gap-2 bg-white px-3 py-1.5 rounded border" style={{ borderColor: "#cbd5e1" }}>
            <label className="fw-semibold text-secondary small mb-0 text-nowrap">
              <i className="bi bi-people-fill text-primary me-1"></i>
              Tổng số đảng viên dự họp (Cử tri):
            </label>
            <input
              type="number"
              min="1"
              value={totalVoters || ""}
              onChange={(e) => onChangeTotalVoters(parseInt(e.target.value) || 0)}
              className="form-control form-control-sm text-center fw-bold text-primary"
              style={{ width: "70px", height: "30px", fontSize: "14px" }}
              placeholder="0"
            />
          </div>

          <div className="small text-muted d-flex align-items-center gap-2">
            <span>
              Tổng số cán bộ: <strong className="text-dark">{records.length}</strong>
            </span>
            <span>•</span>
            <span>
              Hợp lệ phiếu:{" "}
              <strong className={validVotesCount === records.length && records.length > 0 ? "text-success" : "text-warning"}>
                {validVotesCount}/{records.length}
              </strong>
            </span>
          </div>
        </div>

        <div className="d-flex align-items-center gap-2">
          {/* Ô tìm kiếm nhanh */}
          <div className="input-group input-group-sm" style={{ width: "220px" }}>
            <span className="input-group-text bg-white border-end-0">
              <i className="bi bi-search text-muted"></i>
            </span>
            <input
              type="text"
              className="form-control border-start-0"
              placeholder="Tìm cán bộ..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>
      </div>

      {/* Table Grid (Biên bản kiểm phiếu Mẫu 13) */}
      <div className="table-responsive">
        <table className="table table-bordered table-hover align-middle mb-0 small">
          <thead className="table-light text-center" style={{ borderColor: "#cbd5e1", verticalAlign: "middle" }}>
            <tr>
              <th style={{ width: "40px" }} rowSpan={2}>STT</th>
              <th style={{ minWidth: "170px" }} rowSpan={2} className="text-start">Họ và tên cán bộ, đảng viên</th>
              <th style={{ width: "90px" }} rowSpan={2}>Điểm tự chấm</th>
              <th colSpan={4} className="bg-light-subtle">
                Kết quả kiểm phiếu (Số phiếu)
              </th>
              <th style={{ width: "80px" }} rowSpan={2}>Tổng phiếu</th>
              <th style={{ minWidth: "185px" }} rowSpan={2}>Chi bộ đề xuất xếp loại</th>
              <th style={{ minWidth: "220px" }} rowSpan={2}>Ý kiến nhận xét của Chi ủy</th>
            </tr>
            <tr>
              <th style={{ width: "70px" }} className="text-success" title="Hoàn thành xuất sắc nhiệm vụ">Xuất sắc</th>
              <th style={{ width: "70px" }} className="text-primary" title="Hoàn thành tốt nhiệm vụ">Tốt</th>
              <th style={{ width: "70px" }} className="text-info" title="Hoàn thành nhiệm vụ">Hoàn thành</th>
              <th style={{ width: "70px" }} className="text-danger" title="Không hoàn thành nhiệm vụ">Không HT</th>
            </tr>
          </thead>
          <tbody>
            {filteredRecords.length === 0 ? (
              <tr>
                <td colSpan={10} className="text-center py-4 text-muted fst-italic">
                  Không tìm thấy hồ sơ cán bộ nào trong Chi bộ.
                </td>
              </tr>
            ) : (
              filteredRecords.map((rec, idx) => {
                const vote = meetingVotes[rec.id] || {
                  votesExcellent: rec.votesExcellent || 0,
                  votesGood: rec.votesGood || 0,
                  votesSatisfactory: rec.votesSatisfactory || 0,
                  votesUnsatisfactory: rec.votesUnsatisfactory || 0,
                  proposedGrade: rec.partyCellProposedGrade || "HoanThanhTot",
                  comment: rec.partyCellComment || "",
                };

                const sumVotes =
                  (vote.votesExcellent || 0) +
                  (vote.votesGood || 0) +
                  (vote.votesSatisfactory || 0) +
                  (vote.votesUnsatisfactory || 0);

                const isValid = totalVoters > 0 && sumVotes === totalVoters;

                return (
                  <tr key={rec.id} style={{ backgroundColor: isValid ? "transparent" : "#fffbeb" }}>
                    <td className="text-center text-muted fw-semibold">{idx + 1}</td>
                    <td>
                      <div className="d-flex justify-content-between align-items-center">
                        <div>
                          <span
                            className="fw-bold text-primary text-decoration-none"
                            style={{ cursor: "pointer" }}
                            onClick={() => setDetailRecord(rec)}
                            title="Bấm để xem chi tiết tiêu chuẩn và minh chứng"
                          >
                            {rec.fullName}
                          </span>
                          <div className="text-muted" style={{ fontSize: "11px" }}>
                            {rec.positionTitle || "Đảng viên"} {rec.partyRole ? `• ${rec.partyRole}` : ""}
                          </div>
                        </div>
                        <button
                          type="button"
                          className="btn btn-link btn-sm p-0 text-secondary"
                          onClick={() => setDetailRecord(rec)}
                          title="Xem chi tiết nhiệm vụ và tệp minh chứng"
                        >
                          <i className="bi bi-info-circle"></i>
                        </button>
                      </div>
                    </td>
                    <td className="text-center">
                      <span className="fw-bold text-dark">{rec.totalSelfScore}đ</span>
                      <div className="text-muted" style={{ fontSize: "10px" }}>
                        {getGradeLabel(rec.selfProposedGrade)}
                      </div>
                    </td>
                    <td className="p-1">
                      <input
                        type="number"
                        min="0"
                        className="form-control form-control-sm text-center p-1 fw-bold text-success"
                        value={vote.votesExcellent}
                        onChange={(e) =>
                          onChangeMemberVote(rec.id, "votesExcellent", parseInt(e.target.value) || 0)
                        }
                      />
                    </td>
                    <td className="p-1">
                      <input
                        type="number"
                        min="0"
                        className="form-control form-control-sm text-center p-1 fw-bold text-primary"
                        value={vote.votesGood}
                        onChange={(e) =>
                          onChangeMemberVote(rec.id, "votesGood", parseInt(e.target.value) || 0)
                        }
                      />
                    </td>
                    <td className="p-1">
                      <input
                        type="number"
                        min="0"
                        className="form-control form-control-sm text-center p-1 fw-bold text-info"
                        value={vote.votesSatisfactory}
                        onChange={(e) =>
                          onChangeMemberVote(rec.id, "votesSatisfactory", parseInt(e.target.value) || 0)
                        }
                      />
                    </td>
                    <td className="p-1">
                      <input
                        type="number"
                        min="0"
                        className="form-control form-control-sm text-center p-1 fw-bold text-danger"
                        value={vote.votesUnsatisfactory}
                        onChange={(e) =>
                          onChangeMemberVote(rec.id, "votesUnsatisfactory", parseInt(e.target.value) || 0)
                        }
                      />
                    </td>
                    <td className="text-center">
                      <span
                        className={`badge ${
                          isValid
                            ? "bg-success-subtle text-success border border-success-subtle"
                            : "bg-danger-subtle text-danger border border-danger-subtle"
                        }`}
                        style={{ fontSize: "11px" }}
                        title={isValid ? "Hợp lệ (bằng tổng cử tri)" : `Chưa khớp (${sumVotes}/${totalVoters})`}
                      >
                        {sumVotes}/{totalVoters}
                      </span>
                    </td>
                    <td className="p-1">
                      <select
                        className="form-select form-select-sm"
                        style={{ minWidth: "175px" }}
                        value={vote.proposedGrade}
                        onChange={(e) =>
                          onChangeMemberVote(rec.id, "proposedGrade", e.target.value)
                        }
                      >
                        <option value="HoanThanhXuatSac">Hoàn thành xuất sắc</option>
                        <option value="HoanThanhTot">Hoàn thành tốt</option>
                        <option value="HoanThanh">Hoàn thành</option>
                        <option value="KhongHoanThanh">Không hoàn thành</option>
                      </select>
                    </td>
                    <td className="p-1">
                      <input
                        type="text"
                        className="form-control form-control-sm"
                        placeholder="Nhận xét ưu điểm, hạn chế..."
                        value={vote.comment}
                        onChange={(e) =>
                          onChangeMemberVote(rec.id, "comment", e.target.value)
                        }
                      />
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Chân trang thao tác đồng nhất */}
      <div
        className="card-footer bg-white border-top py-2.5 px-3 d-flex flex-wrap justify-content-between align-items-center gap-2"
        style={{ borderColor: "#e2e8f0" }}
      >
        <div className="text-secondary small">
          Đã kiểm phiếu:{" "}
          <strong className={validVotesCount === records.length && records.length > 0 ? "text-success" : "text-dark"}>
            {validVotesCount}/{records.length} cán bộ
          </strong>{" "}
          (Tổng cử tri dự họp: <strong>{totalVoters}</strong>)
        </div>

        <div className="d-flex align-items-center gap-2 ms-auto">
          <Button
            variant="primary"
            size="sm"
            onClick={onSubmitMeeting}
            disabled={isSubmitting || records.length === 0}
            icon={isSubmitting ? "bi-arrow-repeat spin" : "bi-check2-circle"}
            loading={isSubmitting}
            loadingText="Đang lưu..."
            className="px-3 text-nowrap"
          >
            Lưu Biên bản kiểm phiếu Chi bộ
          </Button>
        </div>
      </div>

      {/* Modal Chi tiết Cán bộ (Xem nhiệm vụ & minh chứng) */}
      {detailRecord && (
        <div
          className="modal fade show d-block"
          tabIndex={-1}
          style={{ backgroundColor: "rgba(0,0,0,0.5)", zIndex: 1050 }}
        >
          <div className="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div className="modal-content border-0 shadow">
              <div className="modal-header py-2.5 px-3 bg-light border-bottom">
                <h6 className="modal-title fw-bold text-dark mb-0">
                  <i className="bi bi-person-badge text-primary me-2"></i>
                  Chi tiết hồ sơ tự chấm: {detailRecord.fullName}
                </h6>
                <button
                  type="button"
                  className="btn-close"
                  onClick={() => setDetailRecord(null)}
                ></button>
              </div>
              <div className="modal-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-3 p-2 bg-light rounded border">
                  <div>
                    <span className="text-secondary small">Chức vụ: </span>
                    <strong className="text-dark">{detailRecord.positionTitle}</strong>
                    <span className="text-muted ms-2">({detailRecord.partyCellName})</span>
                  </div>
                  <div>
                    <span className="text-secondary small">Tổng điểm tự chấm: </span>
                    <strong className="text-success fs-6">{detailRecord.totalSelfScore}đ</strong>
                    <span className="badge bg-primary-subtle text-primary ms-2">
                      {getGradeLabel(detailRecord.selfProposedGrade)}
                    </span>
                  </div>
                </div>

                <h6 className="fw-semibold text-secondary small mb-2">
                  Danh sách nhiệm vụ chuyên môn và tệp minh chứng:
                </h6>
                <div className="table-responsive border rounded">
                  <table className="table table-sm table-hover align-middle mb-0 small">
                    <thead className="table-light">
                      <tr>
                        <th style={{ width: "35px" }} className="text-center">STT</th>
                        <th>Nhiệm vụ & Tiêu chuẩn giao</th>
                        <th style={{ width: "75px" }} className="text-center">Trọng số</th>
                        <th style={{ width: "75px" }} className="text-center">Tự chấm</th>
                        <th style={{ width: "120px" }} className="text-center">Tài liệu đính kèm</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(detailRecord.tasks || []).map((t, i) => {
                        const attId = t.attachmentId;
                        const attName = t.attachmentFileName || t.attachmentOriginalName;
                        return (
                          <tr key={t.id}>
                            <td className="text-center text-muted">{i + 1}</td>
                            <td>
                              <div className="fw-medium text-dark">{t.taskName}</div>
                              <div className="text-muted" style={{ fontSize: "11px" }}>{t.targetOutput}</div>
                            </td>
                            <td className="text-center">{t.weight}đ</td>
                            <td className="text-center fw-bold text-success">{t.selfScore}đ</td>
                            <td className="text-center">
                              {attId ? (
                                <Button
                                  size="sm"
                                  variant="outline-primary"
                                  icon="bi-file-earmark-pdf"
                                  onClick={() => onOpenDocViewer(attId, attName)}
                                  title={attName || "Xem PDF"}
                                  style={{ padding: "2px 6px", fontSize: "11px", maxWidth: "110px" }}
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
              </div>
              <div className="modal-footer py-2 px-3 bg-light border-top">
                <Button variant="secondary" size="sm" onClick={() => setDetailRecord(null)}>
                  Đóng
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
