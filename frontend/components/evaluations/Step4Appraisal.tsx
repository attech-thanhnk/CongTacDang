"use client";

import React from "react";
import { EvaluationRecordDto, BranchQuotaCheckDto } from "@/services/evaluationService";
import { Button } from "@/components/common";

interface Step4AppraisalProps {
  records: EvaluationRecordDto[];
  quotas: BranchQuotaCheckDto[];
  selectedRecord: EvaluationRecordDto | null;
  onSelectRecord: (record: EvaluationRecordDto) => void;
  appraisalScore: number;
  onChangeAppraisalScore: (v: number) => void;
  appraisalComment: string;
  onChangeAppraisalComment: (v: string) => void;
  appraisalGrade: string;
  onChangeAppraisalGrade: (v: string) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  onOpenDocViewer: (attId: string, fileName?: string | null) => void;
  onOpenPdf?: (record: EvaluationRecordDto, templateType?: any) => void;
  onExportMau10Docx?: (record: EvaluationRecordDto) => void;
}

export function Step4Appraisal({
  records,
  quotas,
  selectedRecord,
  onSelectRecord,
  appraisalScore,
  onChangeAppraisalScore,
  appraisalComment,
  onChangeAppraisalComment,
  appraisalGrade,
  onChangeAppraisalGrade,
  onSubmit,
  isSubmitting,
  onOpenDocViewer,
  onOpenPdf,
  onExportMau10Docx,
}: Step4AppraisalProps) {
  const [searchTerm, setSearchTerm] = React.useState("");
  const [filterStatus, setFilterStatus] = React.useState<"all" | "pending" | "appraised">("all");

  const filteredRecords = records.filter((rec) => {
    const term = searchTerm.toLowerCase().trim();
    const matchTerm =
      !term ||
      rec.fullName?.toLowerCase().includes(term) ||
      rec.partyCellName?.toLowerCase().includes(term) ||
      rec.departmentName?.toLowerCase().includes(term) ||
      rec.positionTitle?.toLowerCase().includes(term);

    const isAppraised = rec.appraisalScore !== null && rec.appraisalScore !== undefined;
    if (filterStatus === "pending") return matchTerm && !isAppraised;
    if (filterStatus === "appraised") return matchTerm && isAppraised;
    return matchTerm;
  });

  const handleScoreChange = (val: number) => {
    const score = isNaN(val) ? 0 : Math.min(100, Math.max(0, val));
    onChangeAppraisalScore(score);
    if (score >= 90) {
      if (selectedRecord?.partyCellProposedGrade === "HoanThanhXuatSac") {
        onChangeAppraisalGrade("HoanThanhXuatSac");
      } else {
        onChangeAppraisalGrade("HoanThanhTot");
      }
    } else if (score >= 70) {
      onChangeAppraisalGrade("HoanThanhTot");
    } else if (score >= 50) {
      onChangeAppraisalGrade("HoanThanh");
    } else if (score > 0) {
      onChangeAppraisalGrade("KhongHoanThanh");
    }
  };

  return (
    <div className="space-y-3">
      {/* Bảng Quota trần 20% với thanh tiến độ trực quan */}
      {quotas.length > 0 && (
        <div className="border rounded bg-white shadow-sm mb-3" style={{ borderColor: "#e2e8f0" }}>
          <div
            className="card-header bg-white border-bottom py-2.5 px-3 d-flex justify-content-between align-items-center"
            style={{ borderColor: "#e2e8f0" }}
          >
            <div>
              <h2 className="mb-0 text-dark" style={{ fontSize: "14px", fontWeight: 600, lineHeight: 1.4 }}>
                Kiểm soát tỷ lệ trần 20% Hoàn thành xuất sắc theo Chi bộ
              </h2>
            </div>
          </div>

          <div className="card-body p-0">
            <div className="table-responsive">
              <table className="table table-sm table-hover align-middle mb-0">
                <thead className="text-center">
                  <tr>
                    <th className="text-start">Chi bộ trực thuộc</th>
                    <th style={{ width: "120px" }}>Tổng cán bộ</th>
                    <th style={{ width: "130px" }}>Trần 20% (Tối đa)</th>
                    <th style={{ width: "130px" }}>Chi bộ đề xuất</th>
                    <th style={{ width: "130px" }}>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {quotas.map((q) => {
                    const isExceeded = q.isExceedingQuota || q.proposedExcellentCount > q.maxExcellentAllowed;

                    return (
                      <tr key={q.branchId} style={{ backgroundColor: isExceeded ? "#fef2f2" : "transparent" }}>
                        <td className="fw-medium text-dark">{q.branchName}</td>
                        <td className="text-center text-secondary">{q.totalCadres}</td>
                        <td className="text-center fw-bold text-primary">{q.maxExcellentAllowed}</td>
                        <td className="text-center fw-bold" style={{ color: isExceeded ? "#b91c1c" : "#0f172a" }}>
                          {q.proposedExcellentCount}
                        </td>
                        <td className="text-center">
                          {isExceeded ? (
                            <span className="badge bg-danger" style={{ fontSize: "11px" }}>
                              Vượt trần (+{q.proposedExcellentCount - q.maxExcellentAllowed})
                            </span>
                          ) : (
                            <span
                              className="badge"
                              style={{ backgroundColor: "#ecfdf5", color: "#047857", border: "1px solid #a7f3d0", fontSize: "11px" }}
                            >
                              Hợp lệ
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* Thẩm định hồ sơ */}
      <div className="border rounded bg-white shadow-sm" style={{ borderColor: "#e2e8f0" }}>
        <div
          className="card-header bg-white border-bottom py-2.5 px-3 d-flex justify-content-between align-items-center"
          style={{ borderColor: "#e2e8f0" }}
        >
          <div>
            <h2 className="mb-0 text-dark" style={{ fontSize: "14px", fontWeight: 600, lineHeight: 1.4 }}>
              Thẩm định điểm số, đối soát chuyên môn & ghi nhận giải trình
            </h2>
          </div>
          <span className="text-secondary small" style={{ fontSize: "12px" }}>
            Hồ sơ toàn Đảng bộ: <strong>{records.length}</strong>
          </span>
        </div>

        <div className="card-body p-3">
          <div className="row g-3">
            {/* Danh sách cán bộ */}
            <div className="col-12 col-lg-4 border-end" style={{ borderColor: "#e2e8f0" }}>
              <div className="d-flex justify-content-between align-items-center mb-2">
                <span className="small fw-semibold text-secondary">Danh sách cán bộ</span>
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
                    placeholder="Tìm theo họ tên, Chi bộ..."
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
                    Chờ TĐ
                  </button>
                  <button
                    type="button"
                    onClick={() => setFilterStatus("appraised")}
                    className={`btn btn-sm py-0.5 px-1.5 ${filterStatus === "appraised" ? "btn-primary" : "btn-light border"}`}
                    style={{ fontSize: "11px" }}
                  >
                    Đã TĐ
                  </button>
                </div>
              </div>

              {records.length === 0 ? (
                <div className="text-muted small text-center py-4">Chưa có hồ sơ nào.</div>
              ) : filteredRecords.length === 0 ? (
                <div className="text-muted small text-center py-4">Không tìm thấy cán bộ phù hợp.</div>
              ) : (
                <div
                  className="list-group list-group-flush border rounded overflow-y-auto"
                  style={{ borderColor: "#e2e8f0", maxHeight: "420px" }}
                >
                  {filteredRecords.map((rec) => {
                    const isSelected = selectedRecord?.id === rec.id;
                    const isAppraised = rec.appraisalScore !== null && rec.appraisalScore !== undefined;

                    return (
                      <button
                        key={rec.id}
                        type="button"
                        onClick={() => onSelectRecord(rec)}
                        className="list-group-item list-group-item-action p-2.5 text-start small transition"
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
                              backgroundColor: isAppraised ? "#ecfdf5" : "#f1f5f9",
                              color: isAppraised ? "#047857" : "#475569",
                              border: isAppraised ? "1px solid #a7f3d0" : "1px solid #cbd5e1",
                            }}
                          >
                            {isAppraised ? `${rec.appraisalScore}đ` : "Chờ TĐ"}
                          </span>
                        </div>
                        <div className="d-flex justify-content-between text-muted mt-1" style={{ fontSize: "11px" }}>
                          <span>{rec.partyCellName}</span>
                          <span>Tự chấm: {rec.totalSelfScore}đ</span>
                        </div>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Chi tiết thẩm định */}
            <div className="col-12 col-lg-8">
              {!selectedRecord ? (
                <div className="h-100 d-flex flex-column align-items-center justify-content-center p-4 text-muted small fst-italic border rounded bg-light" style={{ minHeight: "280px" }}>
                  <i className="bi bi-person-lines-fill fs-3 mb-2 text-secondary"></i>
                  Chọn cán bộ từ danh sách bên trái để đối soát minh chứng và nhập điểm thẩm định.
                </div>
              ) : (
                <div className="space-y-3">
                  {/* Tóm tắt */}
                  <div className="d-flex justify-content-between align-items-center p-2.5 rounded bg-light border small" style={{ borderColor: "#e2e8f0" }}>
                    <div>
                      <strong className="text-dark" style={{ fontSize: "13.5px" }}>{selectedRecord.fullName}</strong>
                      <span className="text-secondary ms-2" style={{ fontSize: "12px" }}>
                        ({selectedRecord.departmentName} — {selectedRecord.partyCellName})
                      </span>
                    </div>
                    <div>
                      Tự chấm: <strong className="text-primary">{selectedRecord.totalSelfScore}đ</strong> | Chi bộ: <strong className="text-secondary">{selectedRecord.partyCellProposedGrade || "—"}</strong>
                    </div>
                  </div>

                  {/* Minh chứng */}
                  <div className="table-responsive border rounded" style={{ borderColor: "#e2e8f0" }}>
                    <table className="table table-sm table-hover align-middle mb-0 small">
                      <thead>
                        <tr>
                          <th style={{ width: "35px" }} className="text-center">STT</th>
                          <th>Nhiệm vụ</th>
                          <th style={{ width: "70px" }} className="text-center">Trọng số</th>
                          <th style={{ width: "70px" }} className="text-center">Tự chấm</th>
                          <th style={{ width: "120px" }} className="text-center">Tài liệu đính kèm</th>
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
                                    title={attName || "Xem tài liệu"}
                                    style={{ padding: "2px 8px", fontSize: "11px", maxWidth: "120px" }}
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

                  {/* Nhập điểm thẩm định */}
                  <div className="row g-2 pt-1">
                    <div className="col-12 col-md-4">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Điểm thẩm định (0 - 100đ):
                      </label>
                      <input
                        type="number"
                        step="0.1"
                        min="0"
                        max="100"
                        value={appraisalScore}
                        onChange={(e) => handleScoreChange(parseFloat(e.target.value))}
                        className="form-control form-control-sm fw-bold text-primary"
                      />
                      <div className="text-muted mt-1" style={{ fontSize: "10.5px" }}>
                        {appraisalScore >= 90
                          ? "Gợi ý: Hoàn thành tốt / Xuất sắc"
                          : appraisalScore >= 70
                          ? "Gợi ý: Hoàn thành tốt"
                          : appraisalScore >= 50
                          ? "Gợi ý: Hoàn thành nhiệm vụ"
                          : appraisalScore > 0
                          ? "Gợi ý: Không hoàn thành"
                          : "Khung điểm: 0 - 100đ"}
                      </div>
                    </div>

                    <div className="col-12 col-md-4">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Xếp loại đề xuất:
                      </label>
                      <select
                        value={appraisalGrade}
                        onChange={(e) => onChangeAppraisalGrade(e.target.value)}
                        className="form-select form-select-sm"
                        style={{ minWidth: "175px" }}
                      >
                        <option value="HoanThanhXuatSac">Hoàn thành xuất sắc</option>
                        <option value="HoanThanhTot">Hoàn thành tốt</option>
                        <option value="HoanThanh">Hoàn thành</option>
                        <option value="KhongHoanThanh">Không hoàn thành</option>
                      </select>
                    </div>

                    <div className="col-12 col-md-4">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Căn cứ / Ghi chú:
                      </label>
                      <input
                        type="text"
                        value={appraisalComment}
                        onChange={(e) => onChangeAppraisalComment(e.target.value)}
                        placeholder="Căn cứ đối soát..."
                        className="form-control form-control-sm"
                      />
                    </div>
                  </div>

                  {/* Nút thao tác đồng nhất size sm */}
                  <div className="d-flex justify-content-end gap-2 pt-2.5 border-top" style={{ borderColor: "#e2e8f0" }}>
                    {onExportMau10Docx && (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline-primary"
                        onClick={() => onExportMau10Docx(selectedRecord)}
                        icon="bi-file-earmark-word"
                        className="text-nowrap"
                        title="Tải Phiếu thẩm định, nhận xét, giải trình định dạng Word"
                      >
                        Tải phiếu thẩm định (.docx)
                      </Button>
                    )}
                    {onOpenPdf && (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline-primary"
                        onClick={() => onOpenPdf(selectedRecord, "individual")}
                        icon="bi-file-earmark-pdf"
                        title="Xem và in hồ sơ cán bộ"
                      >
                        Xem / In hồ sơ (PDF)
                      </Button>
                    )}
                    <Button
                      type="button"
                      size="sm"
                      variant="primary"
                      onClick={onSubmit}
                      loading={isSubmitting}
                      loadingText="Đang lưu..."
                      icon="bi-check2-circle"
                      className="px-3"
                    >
                      Lưu thẩm định
                    </Button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default Step4Appraisal;
