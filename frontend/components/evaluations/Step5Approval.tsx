"use client";

import React from "react";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { Button } from "@/components/common";

interface Step5ApprovalProps {
  records: EvaluationRecordDto[];
  selectedRecord: EvaluationRecordDto | null;
  onSelectRecord: (record: EvaluationRecordDto) => void;
  finalScore: number;
  onChangeFinalScore: (v: number) => void;
  finalGrade: string;
  onChangeFinalGrade: (v: string) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  onOpenPdf: (record: EvaluationRecordDto) => void;
}

export function Step5Approval({
  records,
  selectedRecord,
  onSelectRecord,
  finalScore,
  onChangeFinalScore,
  finalGrade,
  onChangeFinalGrade,
  onSubmit,
  isSubmitting,
  onOpenPdf,
}: Step5ApprovalProps) {
  const [searchTerm, setSearchTerm] = React.useState("");
  const [filterStatus, setFilterStatus] = React.useState<"all" | "pending" | "approved">("all");

  const filteredRecords = records.filter((rec) => {
    const term = searchTerm.toLowerCase().trim();
    const matchTerm =
      !term ||
      rec.fullName?.toLowerCase().includes(term) ||
      rec.partyCellName?.toLowerCase().includes(term) ||
      rec.departmentName?.toLowerCase().includes(term);

    const isApproved = rec.status === "Approved" || rec.finalScore > 0;
    if (filterStatus === "pending") return matchTerm && !isApproved;
    if (filterStatus === "approved") return matchTerm && isApproved;
    return matchTerm;
  });

  const handleScoreChange = (val: number) => {
    const score = isNaN(val) ? 0 : Math.min(100, Math.max(0, val));
    onChangeFinalScore(score);
    if (score >= 90) {
      if (selectedRecord?.appraisalProposedGrade === "HoanThanhXuatSac" || selectedRecord?.partyCellProposedGrade === "HoanThanhXuatSac") {
        onChangeFinalGrade("HoanThanhXuatSac");
      } else {
        onChangeFinalGrade("HoanThanhTot");
      }
    } else if (score >= 70) {
      onChangeFinalGrade("HoanThanhTot");
    } else if (score >= 50) {
      onChangeFinalGrade("HoanThanh");
    } else if (score > 0) {
      onChangeFinalGrade("KhongHoanThanh");
    }
  };

  return (
    <div className="border rounded bg-white" style={{ borderColor: "#e2e8f0" }}>
      <div className="card-header bg-white border-bottom py-2.5 px-3 d-flex justify-content-between align-items-center" style={{ borderColor: "#e2e8f0" }}>
        <div className="d-flex align-items-center gap-2">
          <span
            className="badge"
            style={{ backgroundColor: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe" }}
          >
            Mẫu 07
          </span>
          <span className="fw-semibold text-dark small">
            BTV Đảng ủy chuẩn y xếp loại
          </span>
        </div>
      </div>

      <div className="card-body p-3">
        <div className="row g-3">
          {/* Bảng tổng hợp hồ sơ */}
          <div className="col-12 col-lg-8 border-end" style={{ borderColor: "#e2e8f0" }}>
            <div className="d-flex flex-column flex-sm-row justify-content-between align-items-sm-center gap-2 mb-2">
              <span className="small fw-semibold text-secondary">
                Bảng tổng hợp xếp loại ({records.length} cán bộ)
              </span>

              {/* Ô tìm kiếm & Lọc nhanh */}
              <div className="d-flex gap-2">
                <div className="input-group input-group-sm" style={{ width: "200px" }}>
                  <span className="input-group-text bg-light text-secondary border-end-0">
                    <i className="bi bi-search"></i>
                  </span>
                  <input
                    type="text"
                    className="form-control border-start-0"
                    placeholder="Tìm cán bộ..."
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

                <div className="btn-group btn-group-sm" role="group">
                  <button
                    type="button"
                    onClick={() => setFilterStatus("all")}
                    className={`btn btn-sm py-0.5 px-2 ${filterStatus === "all" ? "btn-primary" : "btn-light border"}`}
                    style={{ fontSize: "11px" }}
                  >
                    Tất cả
                  </button>
                  <button
                    type="button"
                    onClick={() => setFilterStatus("pending")}
                    className={`btn btn-sm py-0.5 px-2 ${filterStatus === "pending" ? "btn-primary" : "btn-light border"}`}
                    style={{ fontSize: "11px" }}
                  >
                    Chờ duyệt
                  </button>
                  <button
                    type="button"
                    onClick={() => setFilterStatus("approved")}
                    className={`btn btn-sm py-0.5 px-2 ${filterStatus === "approved" ? "btn-primary" : "btn-light border"}`}
                    style={{ fontSize: "11px" }}
                  >
                    Đã chuẩn y
                  </button>
                </div>
              </div>
            </div>

            {records.length === 0 ? (
              <div className="text-muted small text-center py-4">Chưa có hồ sơ nào.</div>
            ) : filteredRecords.length === 0 ? (
              <div className="text-muted small text-center py-4">Không tìm thấy cán bộ phù hợp.</div>
            ) : (
              <div className="table-responsive border rounded overflow-y-auto" style={{ borderColor: "#e2e8f0", maxHeight: "480px" }}>
                <table className="table table-sm table-hover align-middle mb-0">
                  <thead className="text-center sticky-top bg-white">
                    <tr>
                      <th style={{ width: "35px" }}>STT</th>
                      <th>Cán bộ / Đơn vị</th>
                      <th style={{ width: "65px" }}>Tự chấm</th>
                      <th style={{ width: "100px" }}>Chi bộ</th>
                      <th style={{ width: "75px" }}>T.Định</th>
                      <th style={{ width: "115px" }}>BTV Chuẩn y</th>
                      <th style={{ width: "65px" }}>Biểu mẫu</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRecords.map((rec, idx) => {
                      const isSelected = selectedRecord?.id === rec.id;
                      const isApproved = rec.status === "Approved" || rec.finalScore > 0;

                      return (
                        <tr
                          key={rec.id}
                          onClick={() => onSelectRecord(rec)}
                          style={{
                            cursor: "pointer",
                            backgroundColor: isSelected ? "#eff6ff" : "transparent",
                          }}
                        >
                          <td className="text-center text-muted">{idx + 1}</td>
                          <td>
                            <div className="fw-medium text-dark">{rec.fullName}</div>
                            <div className="text-secondary small" style={{ fontSize: "11px" }}>
                              {rec.partyCellName}
                            </div>
                          </td>
                          <td className="text-center fw-medium text-dark">
                            {rec.totalSelfScore > 0 ? `${rec.totalSelfScore}đ` : "-"}
                          </td>
                          <td className="text-center">
                            {rec.partyCellProposedGrade ? (
                              <span className="small fw-semibold text-primary" style={{ fontSize: "11px" }}>
                                {rec.partyCellProposedGrade}
                              </span>
                            ) : (
                              <span className="text-muted" style={{ fontSize: "11px" }}>-</span>
                            )}
                          </td>
                          <td className="text-center">
                            {rec.appraisalScore !== null && rec.appraisalScore !== undefined ? (
                              <span className="small fw-semibold text-info">
                                {rec.appraisalScore}đ
                              </span>
                            ) : (
                              <span className="text-muted" style={{ fontSize: "11px" }}>-</span>
                            )}
                          </td>
                          <td className="text-center">
                            {isApproved ? (
                              <span className="small fw-semibold text-success">
                                {rec.finalGrade} ({rec.finalScore}đ)
                              </span>
                            ) : (
                              <span className="text-muted" style={{ fontSize: "11px" }}>Chờ chuẩn y</span>
                            )}
                          </td>
                          <td className="text-center">
                            <Button
                              size="sm"
                              variant="outline-secondary"
                              icon="bi-file-earmark-pdf"
                              onClick={(e) => {
                                e.stopPropagation();
                                onOpenPdf(rec);
                              }}
                              title="Xem và xuất PDF biểu mẫu"
                              style={{ padding: "2px 6px" }}
                            />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Khung chuẩn y */}
          <div className="col-12 col-lg-4">
            {!selectedRecord ? (
              <div className="h-100 d-flex align-items-center justify-content-center p-4 text-muted small fst-italic border rounded bg-light">
                Chọn cán bộ để chuẩn y điểm và xếp loại.
              </div>
            ) : (
              <div className="border rounded p-3 bg-light space-y-2.5" style={{ borderColor: "#e2e8f0" }}>
                <div>
                  <div className="fw-bold text-dark">{selectedRecord.fullName}</div>
                  <div className="text-secondary small" style={{ fontSize: "11px" }}>
                    {selectedRecord.partyCellName} • {selectedRecord.positionTitle}
                  </div>
                </div>

                <div className="small border-top border-bottom py-1.5 text-secondary d-flex justify-content-between" style={{ borderColor: "#cbd5e1" }}>
                  <span>Tự chấm: <strong className="text-dark">{selectedRecord.totalSelfScore}đ</strong></span>
                  <span>TĐ: <strong className="text-primary">{selectedRecord.appraisalScore || "—"}đ</strong></span>
                </div>

                <div>
                  <label className="form-label small fw-semibold text-secondary mb-1">
                    Điểm chính thức (0 - 100đ):
                  </label>
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="100"
                    value={finalScore}
                    onChange={(e) => handleScoreChange(parseFloat(e.target.value))}
                    className="form-control form-control-sm fw-bold text-primary"
                  />
                  <div className="text-muted mt-1" style={{ fontSize: "10.5px" }}>
                    {finalScore >= 90
                      ? "Gợi ý: Hoàn thành tốt / Xuất sắc"
                      : finalScore >= 70
                      ? "Gợi ý: Hoàn thành tốt"
                      : finalScore >= 50
                      ? "Gợi ý: Hoàn thành nhiệm vụ"
                      : finalScore > 0
                      ? "Gợi ý: Không hoàn thành"
                      : "Khung điểm: 0 - 100đ"}
                  </div>
                </div>

                <div>
                  <label className="form-label small fw-semibold text-secondary mb-1">
                    Mức xếp loại chính thức:
                  </label>
                  <select
                    value={finalGrade}
                    onChange={(e) => onChangeFinalGrade(e.target.value)}
                    className="form-select form-select-sm fw-medium"
                  >
                    <option value="HoanThanhXuatSac">Hoàn thành xuất sắc</option>
                    <option value="HoanThanhTot">Hoàn thành tốt</option>
                    <option value="HoanThanh">Hoàn thành</option>
                    <option value="KhongHoanThanh">Không hoàn thành</option>
                  </select>
                </div>

                <div className="pt-2 d-flex flex-column gap-2">
                  <Button
                    variant="outline-secondary"
                    onClick={() => onOpenPdf(selectedRecord)}
                    icon="bi-file-earmark-pdf"
                    className="w-100"
                  >
                    Xuất biểu mẫu hồ sơ (PDF)
                  </Button>
                  <Button
                    variant="primary"
                    onClick={onSubmit}
                    loading={isSubmitting}
                    loadingText="Đang lưu..."
                    icon="bi-check2-circle"
                    className="w-100"
                  >
                    Chuẩn y (Mẫu 07)
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
