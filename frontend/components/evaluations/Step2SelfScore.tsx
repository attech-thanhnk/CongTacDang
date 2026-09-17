"use client";

import React from "react";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { Button } from "@/components/common";

interface Step2SelfScoreProps {
  myRecord: EvaluationRecordDto;
  generalScores: number[];
  onChangeGeneralScores: (scores: number[]) => void;
  taskScoreRatios: {
    [taskId: string]: {
      a: number;
      b: number;
      c: number;
      d: number;
      exceed: boolean;
      attachmentId?: string | null;
    };
  };
  onChangeTaskRatio: (taskId: string, field: "a" | "b" | "c" | "d" | "exceed", value: any) => void;
  selfProposedGrade: string;
  onChangeProposedGrade: (grade: string) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  onOpenUploadModal: (taskId: string, currentAttId?: string | null) => void;
  onOpenDocViewer: (attId: string, fileName?: string | null) => void;
  onOpenPdf?: (record: EvaluationRecordDto) => void;
}

const GENERAL_CRITERIA = [
  "Tư tưởng chính trị, đạo đức, lối sống",
  "Ý thức tổ chức kỷ luật, thực hiện nguyên tắc tập trung dân chủ",
  "Tác phong, lề lối làm việc khoa học, văn hóa công vụ",
  "Trách nhiệm nêu gương, phòng chống tham nhũng, lãng phí",
  "Đổi mới, sáng tạo, dám nghĩ, dám làm, dám chịu trách nhiệm",
  "Mức độ hoàn thành nhiệm vụ chính trị, chức trách được giao",
];

export function getJobGroupWeights(jobGroup?: string) {
  switch (jobGroup) {
    case "Khung1_QuanLyDangDoanThe":
      return { wa: 0.25, wb: 0.35, wc: 0.20, wd: 0.20, name: "Khung 1: Quản lý Đảng, Đoàn thể" };
    case "Khung2_AnToanKyThuat":
      return { wa: 0.15, wb: 0.50, wc: 0.15, wd: 0.20, name: "Khung 2: An toàn Kỹ thuật" };
    case "Khung3_DuAnDauTu":
      return { wa: 0.20, wb: 0.30, wc: 0.35, wd: 0.15, name: "Khung 3: Dự án Đầu tư" };
    case "Khung4_KhcnChuyenDoiSo":
      return { wa: 0.15, wb: 0.30, wc: 0.20, wd: 0.35, name: "Khung 4: KHCN & Chuyển đổi số" };
    default:
      return { wa: 0.25, wb: 0.25, wc: 0.25, wd: 0.25, name: "Khung tiêu chuẩn (25% đều)" };
  }
}

export function Step2SelfScore({
  myRecord,
  generalScores,
  onChangeGeneralScores,
  taskScoreRatios,
  onChangeTaskRatio,
  selfProposedGrade,
  onChangeProposedGrade,
  onSubmit,
  isSubmitting,
  onOpenUploadModal,
  onOpenDocViewer,
  onOpenPdf,
}: Step2SelfScoreProps) {
  const sumGeneral = generalScores.reduce((sum, v) => sum + (Number(v) || 0), 0);
  const weights = getJobGroupWeights(myRecord.jobGroup);

  const sumTasks = (myRecord.tasks || []).reduce((sum, task) => {
    const ratio = taskScoreRatios[task.id] || { a: 1, b: 1, c: 1, d: 1 };
    const weightedRatio =
      (ratio.a * weights.wa) +
      (ratio.b * weights.wb) +
      (ratio.c * weights.wc) +
      (ratio.d * weights.wd);
    return sum + Math.round(task.weight * weightedRatio * 100) / 100;
  }, 0);

  const totalSelfScore = Math.round((sumGeneral + sumTasks) * 10) / 10;

  const handleGeneralScoreChange = (index: number, val: number) => {
    const next = [...generalScores];
    next[index] = Math.min(5.0, Math.max(0, val));
    onChangeGeneralScores(next);
  };

  return (
    <div className="space-y-2.5">
      {/* Tóm tắt điểm tự chấm & Xếp loại đề xuất (Thanh phẳng) */}
      <div className="border rounded bg-white p-2.5 d-flex flex-wrap justify-content-between align-items-center gap-2" style={{ borderColor: "#e2e8f0" }}>
        <div className="d-flex align-items-center gap-2.5 flex-wrap">
          <span
            className="badge"
            style={{ backgroundColor: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe" }}
          >
            Mẫu 02
          </span>
          <span
            className="badge"
            style={{ backgroundColor: "#f0fdf4", color: "#15803d", border: "1px solid #bbf7d0", fontSize: "11px" }}
            title="Khung chức danh áp dụng tính điểm 4 tiêu chí chuyên môn"
          >
            <i className="bi bi-briefcase me-1"></i>
            {weights.name}
          </span>
          <span className="small text-secondary">
            Chung: <strong className="text-primary">{sumGeneral.toFixed(1)}/30.0đ</strong>
          </span>
          <span className="text-muted opacity-40">|</span>
          <span className="small text-secondary">
            Chuyên môn: <strong className="text-success">{sumTasks.toFixed(1)}/70.0đ</strong>
          </span>
          <span className="text-muted opacity-40">|</span>
          <span className="small text-secondary">
            Tổng tự chấm: <strong className="fs-6 text-dark">{totalSelfScore.toFixed(1)}/100.0đ</strong>
          </span>
        </div>

        <div className="d-flex align-items-center gap-2 ms-auto flex-wrap">
          <label className="text-secondary small mb-0 text-nowrap">Đề xuất:</label>
          <select
            value={selfProposedGrade}
            onChange={(e) => onChangeProposedGrade(e.target.value)}
            className="form-select form-select-sm"
            style={{ width: "190px" }}
          >
            <option value="HoanThanhXuatSac">Hoàn thành xuất sắc</option>
            <option value="HoanThanhTot">Hoàn thành tốt</option>
            <option value="HoanThanh">Hoàn thành</option>
            <option value="KhongHoanThanh">Không hoàn thành</option>
          </select>

          {onOpenPdf && (
            <Button
              size="sm"
              variant="outline-primary"
              icon="bi-file-earmark-pdf"
              onClick={() => onOpenPdf(myRecord)}
              className="text-nowrap"
              title="Xem và xuất Bản tự đánh giá (Mẫu 02) ra PDF"
            >
              Xuất Mẫu 02 (PDF)
            </Button>
          )}

          <Button
            size="sm"
            variant="primary"
            icon="bi-check2-circle"
            loading={isSubmitting}
            loadingText="Đang lưu..."
            onClick={onSubmit}
            className="text-nowrap"
          >
            Lưu đánh giá (Mẫu 02)
          </Button>
        </div>
      </div>

      {/* Khối làm việc phẳng gồm 2 phần chấm điểm liền mạch */}
      <div className="border rounded bg-white overflow-hidden" style={{ borderColor: "#e2e8f0" }}>
        {/* Phần I: Tiêu chí chung (30 điểm) */}
        <div
          className="bg-light-subtle border-bottom py-2 px-3 d-flex justify-content-between align-items-center"
          style={{ borderColor: "#e2e8f0" }}
        >
          <span className="fw-semibold text-dark small">
            I. Tiêu chí chung về chính trị tư tưởng, đạo đức, tác phong (Tối đa 30.0đ)
          </span>
          <span className="text-secondary small">
            {sumGeneral.toFixed(1)} / 30.0đ
          </span>
        </div>

        <div className="table-responsive">
          <table className="table table-sm table-hover align-middle mb-0 small">
            <thead className="text-center table-light">
              <tr>
                <th style={{ width: "45px" }}>STT</th>
                <th className="text-start">Nội dung tiêu chí</th>
                <th style={{ width: "90px" }}>Tối đa</th>
                <th style={{ width: "130px" }}>Điểm tự chấm</th>
              </tr>
            </thead>
            <tbody>
              {GENERAL_CRITERIA.map((name, idx) => (
                <tr key={idx}>
                  <td className="text-center text-muted">{idx + 1}</td>
                  <td className="text-dark">{name}</td>
                  <td className="text-center text-muted">5.0đ</td>
                  <td className="text-center">
                    <input
                      type="number"
                      min="0"
                      max="5"
                      step="0.1"
                      value={generalScores[idx] || 0}
                      onChange={(e) => handleGeneralScoreChange(idx, parseFloat(e.target.value) || 0)}
                      className="form-control form-control-sm text-center fw-semibold text-primary d-inline-block"
                      style={{ width: "75px" }}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Phần II: Chấm nhiệm vụ chuyên môn (70 điểm) */}
        <div
          className="bg-light-subtle border-top border-bottom py-2 px-3 d-flex justify-content-between align-items-center"
          style={{ borderColor: "#e2e8f0" }}
        >
          <span className="fw-semibold text-dark small">
            II. Kết quả thực hiện nhiệm vụ chuyên môn (Tối đa 70.0đ)
          </span>
          <span className="text-secondary small">
            {sumTasks.toFixed(1)} / 70.0đ
          </span>
        </div>

        <div>
          {!myRecord.tasks || myRecord.tasks.length === 0 ? (
            <div className="p-4 text-muted small text-center">
              Chưa có nhiệm vụ nào được đăng ký ở Bước 1.
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-sm table-hover align-middle mb-0 small">
                <thead className="text-center">
                  <tr>
                    <th style={{ width: "40px" }}>STT</th>
                    <th className="text-start">Nhiệm vụ đăng ký</th>
                    <th style={{ width: "75px" }}>Trọng số</th>
                    <th style={{ width: "85px" }}>A: C.Lượng ({Math.round(weights.wa * 100)}%)</th>
                    <th style={{ width: "85px" }}>B: T.Độ ({Math.round(weights.wb * 100)}%)</th>
                    <th style={{ width: "85px" }}>C: C.Động ({Math.round(weights.wc * 100)}%)</th>
                    <th style={{ width: "85px" }}>D: Kỷ luật ({Math.round(weights.wd * 100)}%)</th>
                    <th style={{ width: "85px" }}>Vượt mức</th>
                    <th style={{ width: "80px" }}>Điểm đạt</th>
                    <th style={{ width: "120px" }}>Minh chứng</th>
                  </tr>
                </thead>
                <tbody>
                  {myRecord.tasks.map((task, idx) => {
                    const ratio = taskScoreRatios[task.id] || { a: 1, b: 1, c: 1, d: 1, exceed: false };
                    const weightedRatio =
                      (ratio.a * weights.wa) +
                      (ratio.b * weights.wb) +
                      (ratio.c * weights.wc) +
                      (ratio.d * weights.wd);
                    const taskScore = Math.round(task.weight * weightedRatio * 100) / 100;
                    const currentAttId = ratio.attachmentId || task.attachmentId;
                    const currentAttName = task.attachmentFileName || task.attachmentOriginalName;

                    return (
                      <tr key={task.id}>
                        <td className="text-center text-muted">{idx + 1}</td>
                        <td>
                          <div className="fw-medium text-dark">{task.taskName}</div>
                          <div className="text-muted small">{task.targetOutput}</div>
                        </td>
                        <td className="text-center fw-medium text-dark">{task.weight}đ</td>

                        <td className="text-center p-1">
                          <select
                            value={ratio.a}
                            onChange={(e) => onChangeTaskRatio(task.id, "a", parseFloat(e.target.value))}
                            className="form-select form-select-sm text-center p-1"
                          >
                            <option value="1.0">1.0</option>
                            <option value="0.9">0.9</option>
                            <option value="0.8">0.8</option>
                            <option value="0.7">0.7</option>
                            <option value="0.5">0.5</option>
                          </select>
                        </td>

                        <td className="text-center p-1">
                          <select
                            value={ratio.b}
                            onChange={(e) => onChangeTaskRatio(task.id, "b", parseFloat(e.target.value))}
                            className="form-select form-select-sm text-center p-1"
                          >
                            <option value="1.0">1.0</option>
                            <option value="0.9">0.9</option>
                            <option value="0.7">0.7</option>
                          </select>
                        </td>

                        <td className="text-center p-1">
                          <select
                            value={ratio.c}
                            onChange={(e) => onChangeTaskRatio(task.id, "c", parseFloat(e.target.value))}
                            className="form-select form-select-sm text-center p-1"
                          >
                            <option value="1.0">1.0</option>
                            <option value="0.9">0.9</option>
                            <option value="0.8">0.8</option>
                          </select>
                        </td>

                        <td className="text-center p-1">
                          <select
                            value={ratio.d}
                            onChange={(e) => onChangeTaskRatio(task.id, "d", parseFloat(e.target.value))}
                            className="form-select form-select-sm text-center p-1"
                          >
                            <option value="1.0">1.0</option>
                            <option value="0.9">0.9</option>
                            <option value="0.8">0.8</option>
                          </select>
                        </td>

                        <td className="text-center">
                          <input
                            type="checkbox"
                            checked={ratio.exceed}
                            onChange={(e) => onChangeTaskRatio(task.id, "exceed", e.target.checked)}
                            className="form-check-input"
                            title="Đạt thành tích vượt trội"
                          />
                        </td>

                        <td className="text-center fw-bold text-success">
                          {taskScore.toFixed(1)}đ
                        </td>

                        <td className="text-center">
                          {currentAttId ? (
                            <div className="d-flex align-items-center justify-content-center gap-1">
                              <button
                                type="button"
                                onClick={() => onOpenDocViewer(currentAttId, currentAttName)}
                                className="btn btn-sm btn-light border py-0 px-2 d-inline-flex align-items-center gap-1 text-truncate"
                                style={{ maxWidth: "105px", fontSize: "11px", backgroundColor: "#f8fafc" }}
                                title={currentAttName || "Xem tài liệu minh chứng"}
                              >
                                <i
                                  className={`bi ${
                                    currentAttName?.toLowerCase().endsWith(".pdf")
                                      ? "bi-file-earmark-pdf text-danger"
                                      : currentAttName?.toLowerCase().endsWith(".xlsx") || currentAttName?.toLowerCase().endsWith(".xls")
                                      ? "bi-file-earmark-excel text-success"
                                      : currentAttName?.toLowerCase().endsWith(".docx") || currentAttName?.toLowerCase().endsWith(".doc")
                                      ? "bi-file-earmark-word text-primary"
                                      : "bi-file-earmark-text text-secondary"
                                  }`}
                                ></i>
                                <span className="text-truncate">{currentAttName || "Xem"}</span>
                              </button>
                              <button
                                type="button"
                                onClick={() => onOpenUploadModal(task.id, currentAttId)}
                                className="btn btn-sm btn-link p-0 text-muted"
                                title="Thay đổi tệp minh chứng"
                              >
                                <i className="bi bi-arrow-repeat"></i>
                              </button>
                              <button
                                type="button"
                                onClick={() => onChangeTaskRatio(task.id, "attachmentId" as any, null)}
                                className="btn btn-sm btn-link p-0 text-danger opacity-75"
                                title="Gỡ tệp đính kèm"
                              >
                                <i className="bi bi-x-circle"></i>
                              </button>
                            </div>
                          ) : (
                            <Button
                              size="sm"
                              variant="outline-secondary"
                              icon="bi-paperclip"
                              onClick={() => onOpenUploadModal(task.id, null)}
                              style={{ fontSize: "11px", padding: "2px 8px" }}
                            >
                              Đính kèm
                            </Button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default Step2SelfScore;
