"use client";

import React, { useState } from "react";
import { TaskInputDto } from "@/services/evaluationService";
import { useToast } from "@/contexts/ToastContext";
import { Button } from "@/components/common";

interface Step1RegisterTasksProps {
  tasks: TaskInputDto[];
  onChangeTasks: (tasks: TaskInputDto[]) => void;
  onSubmit: () => void;
  isSubmitting: boolean;
  isLocked?: boolean;
  onOpenUploadModal: (taskIndex: number, currentAttId?: string | null) => void;
  onOpenDocViewer: (attId: string, fileName?: string | null) => void;
  onOpenPdf?: () => void;
}

export function Step1RegisterTasks({
  tasks,
  onChangeTasks,
  onSubmit,
  isSubmitting,
  isLocked = false,
  onOpenUploadModal,
  onOpenDocViewer,
  onOpenPdf,
}: Step1RegisterTasksProps) {
  const { toast } = useToast();
  const [newTaskName, setNewTaskName] = useState("");
  const [newTargetOutput, setNewTargetOutput] = useState("");
  const [newWeight, setNewWeight] = useState<number>(10);
  const [newDeadline, setNewDeadline] = useState("");

  const totalWeight = tasks.reduce((sum, t) => sum + (Number(t.weight) || 0), 0);
  const isWeightValid = Math.abs(totalWeight - 70.0) < 0.01;
  const isCountValid = tasks.length >= 3 && tasks.length <= 7;

  const handleAddTask = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTaskName.trim() || !newTargetOutput.trim()) {
      toast.warning("Vui lòng nhập tên nhiệm vụ và chỉ tiêu sản phẩm.");
      return;
    }
    if (tasks.length >= 7) {
      toast.warning("Đã đạt tối đa 7 nhiệm vụ theo quy định Hướng dẫn 03-HD/TVĐU.");
      return;
    }

    const updated = [
      ...tasks,
      {
        taskName: newTaskName.trim(),
        targetOutput: newTargetOutput.trim(),
        weight: Number(newWeight) || 10,
        deadline: newDeadline || "",
      },
    ];
    onChangeTasks(updated);
    setNewTaskName("");
    setNewTargetOutput("");
    setNewWeight(10);
    setNewDeadline("");
  };

  const handleRemoveTask = (index: number) => {
    const updated = tasks.filter((_, idx) => idx !== index);
    onChangeTasks(updated);
  };

  const handleUpdateTaskField = (index: number, field: keyof TaskInputDto, value: any) => {
    const updated = [...tasks];
    updated[index] = { ...updated[index], [field]: value };
    onChangeTasks(updated);
  };

  return (
    <div className="border rounded bg-white" style={{ borderColor: "#e2e8f0" }}>
      {/* Header thanh lịch */}
      <div className="card-header bg-white border-bottom py-2.5 px-3 d-flex justify-content-between align-items-center" style={{ borderColor: "#e2e8f0" }}>
        <div className="d-flex align-items-center gap-2">
          <span
            className="badge"
            style={{ backgroundColor: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe" }}
          >
            Mẫu 01
          </span>
          <span className="fw-semibold text-dark small">
            Đăng ký 3 - 7 nhiệm vụ chuyên môn trọng tâm
          </span>
        </div>

        <div className="d-flex align-items-center gap-2">
          <span className="text-secondary small">Tổng trọng số:</span>
          <span
            className="badge fw-semibold"
            style={{
              fontSize: "12.5px",
              backgroundColor: isWeightValid ? "#ecfdf5" : "#fef2f2",
              color: isWeightValid ? "#047857" : "#b91c1c",
              border: isWeightValid ? "1px solid #a7f3d0" : "1px solid #fecaca",
            }}
          >
            {totalWeight.toFixed(1)} / 70.0đ
          </span>
        </div>
      </div>

      <div className="card-body p-3">
        {/* Cảnh báo ngắn khi chưa đạt chuẩn */}
        {(!isWeightValid || !isCountValid) && (
          <div
            className="p-2 px-3 mb-3 rounded d-flex align-items-center gap-2 small"
            style={{ backgroundColor: "#fffbeb", color: "#92400e", border: "1px solid #fde68a" }}
          >
            <i className="bi bi-exclamation-triangle"></i>
            <span>
              {!isCountValid && `Số lượng công việc (${tasks.length}) cần từ 3 đến 7 việc. `}
              {!isWeightValid && `Tổng điểm (${totalWeight.toFixed(1)}đ) cần đạt đúng 70.0đ.`}
            </span>
          </div>
        )}

        {/* Bảng danh sách nhiệm vụ */}
        <div className="table-responsive mb-3">
          <table className="table table-sm table-hover align-middle mb-0">
            <thead className="text-center">
              <tr>
                <th style={{ width: "40px" }}>STT</th>
                <th>Tên nhiệm vụ chuyên môn</th>
                <th style={{ width: "260px" }}>Chỉ tiêu đầu ra</th>
                <th style={{ width: "85px" }}>Trọng số (đ)</th>
                <th style={{ width: "125px" }}>Thời hạn</th>
                <th style={{ width: "120px" }}>Minh chứng</th>
                {!isLocked && <th style={{ width: "45px" }}></th>}
              </tr>
            </thead>
            <tbody>
              {tasks.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-4 text-muted">
                    <div className="d-flex flex-column align-items-center justify-content-center gap-1.5 py-2">
                      <i className="bi bi-inbox text-secondary fs-4"></i>
                      <span className="fw-medium text-secondary">Chưa có nhiệm vụ đăng ký.</span>
                      <span className="small text-muted">Thêm từ 3 đến 7 nhiệm vụ theo Mẫu 01 phía dưới.</span>
                    </div>
                  </td>
                </tr>
              ) : (
                tasks.map((task, idx) => (
                  <tr key={idx}>
                    <td className="text-center text-muted">{idx + 1}</td>
                    <td>
                      {isLocked ? (
                        <div className="fw-medium text-dark">{task.taskName}</div>
                      ) : (
                        <input
                          type="text"
                          value={task.taskName}
                          onChange={(e) => handleUpdateTaskField(idx, "taskName", e.target.value)}
                          className="form-control form-control-sm"
                          placeholder="Tên nhiệm vụ..."
                        />
                      )}
                    </td>
                    <td>
                      {isLocked ? (
                        <div className="text-secondary">{task.targetOutput}</div>
                      ) : (
                        <input
                          type="text"
                          value={task.targetOutput}
                          onChange={(e) => handleUpdateTaskField(idx, "targetOutput", e.target.value)}
                          className="form-control form-control-sm"
                          placeholder="Chỉ tiêu đầu ra..."
                        />
                      )}
                    </td>
                    <td className="text-center">
                      {isLocked ? (
                        <span className="fw-bold text-dark">{task.weight}</span>
                      ) : (
                        <input
                          type="number"
                          step="0.5"
                          min="1"
                          max="70"
                          value={task.weight}
                          onChange={(e) => handleUpdateTaskField(idx, "weight", parseFloat(e.target.value) || 0)}
                          className="form-control form-control-sm text-center fw-bold text-primary"
                        />
                      )}
                    </td>
                    <td className="text-center">
                      {isLocked ? (
                        <span className="text-secondary">{new Date(task.deadline).toLocaleDateString("vi-VN")}</span>
                      ) : (
                        <input
                          type="date"
                          value={task.deadline ? task.deadline.substring(0, 10) : ""}
                          onChange={(e) => handleUpdateTaskField(idx, "deadline", e.target.value)}
                          className="form-control form-control-sm text-center"
                        />
                      )}
                    </td>
                    <td className="text-center">
                      {task.attachmentId ? (
                        <div className="d-flex align-items-center justify-content-center gap-1">
                          <button
                            type="button"
                            onClick={() => onOpenDocViewer(task.attachmentId!, task.attachmentFileName)}
                            className="btn btn-sm btn-light border py-0 px-2 d-inline-flex align-items-center gap-1 text-truncate"
                            style={{ maxWidth: "105px", fontSize: "11px", backgroundColor: "#f8fafc" }}
                            title={task.attachmentFileName || "Xem file"}
                          >
                            <i
                              className={`bi ${
                                task.attachmentFileName?.toLowerCase().endsWith(".pdf")
                                  ? "bi-file-earmark-pdf text-danger"
                                  : task.attachmentFileName?.toLowerCase().endsWith(".xlsx") || task.attachmentFileName?.toLowerCase().endsWith(".xls")
                                  ? "bi-file-earmark-excel text-success"
                                  : task.attachmentFileName?.toLowerCase().endsWith(".docx") || task.attachmentFileName?.toLowerCase().endsWith(".doc")
                                  ? "bi-file-earmark-word text-primary"
                                  : "bi-file-earmark-text text-secondary"
                              }`}
                            ></i>
                            <span className="text-truncate">{task.attachmentFileName || "Xem"}</span>
                          </button>
                          {!isLocked && (
                            <>
                              <button
                                type="button"
                                onClick={() => onOpenUploadModal(idx, task.attachmentId)}
                                className="btn btn-sm btn-link p-0 text-muted"
                                title="Thay đổi tệp minh chứng"
                              >
                                <i className="bi bi-arrow-repeat"></i>
                              </button>
                              <button
                                type="button"
                                onClick={() => {
                                  handleUpdateTaskField(idx, "attachmentId", null);
                                  handleUpdateTaskField(idx, "attachmentFileName", null);
                                }}
                                className="btn btn-sm btn-link p-0 text-danger opacity-75"
                                title="Gỡ tệp đính kèm"
                              >
                                <i className="bi bi-x-circle"></i>
                              </button>
                            </>
                          )}
                        </div>
                      ) : (
                        !isLocked && (
                          <Button
                            type="button"
                            size="sm"
                            variant="outline-secondary"
                            icon="bi-paperclip"
                            onClick={() => onOpenUploadModal(idx, null)}
                            style={{ padding: "2px 8px", fontSize: "11.5px" }}
                          >
                            Đính kèm
                          </Button>
                        )
                      )}
                    </td>
                    {!isLocked && (
                      <td className="text-center">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline-danger"
                          icon="bi-trash"
                          onClick={() => handleRemoveTask(idx)}
                          title="Xóa nhiệm vụ"
                          style={{ padding: "2px 6px" }}
                        />
                      </td>
                    )}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Thêm nhanh nhiệm vụ */}
        {!isLocked && (
          <form onSubmit={handleAddTask} className="row g-1.5 p-2 bg-light border rounded align-items-center" style={{ borderColor: "#e2e8f0" }}>
            <div className="col-12 col-md-4">
              <input
                type="text"
                required
                placeholder="Tên nhiệm vụ / công tác trọng tâm *"
                value={newTaskName}
                onChange={(e) => setNewTaskName(e.target.value)}
                className="form-control form-control-sm"
              />
            </div>

            <div className="col-12 col-md-4">
              <input
                type="text"
                required
                placeholder="Sản phẩm / Tiêu chuẩn đầu ra *"
                value={newTargetOutput}
                onChange={(e) => setNewTargetOutput(e.target.value)}
                className="form-control form-control-sm"
              />
            </div>

            <div className="col-6 col-md-2">
              <input
                type="number"
                required
                step="0.5"
                min="0.5"
                max="100"
                placeholder="Trọng số (đ) *"
                value={newWeight}
                onChange={(e) => setNewWeight(parseFloat(e.target.value) || 0)}
                className="form-control form-control-sm text-center"
              />
            </div>

            <div className="col-6 col-md-2 d-flex gap-1">
              <input
                type="date"
                value={newDeadline}
                onChange={(e) => setNewDeadline(e.target.value)}
                className="form-control form-control-sm text-center"
              />
              <Button
                type="submit"
                size="sm"
                variant="primary"
                icon="bi-plus-lg"
                className="text-nowrap"
              >
                Thêm
              </Button>
            </div>
          </form>
        )}

        {/* Nút lưu & Xuất PDF */}
        <div className="d-flex justify-content-end align-items-center gap-2 pt-2.5 border-top" style={{ borderColor: "#e2e8f0" }}>
          {onOpenPdf && tasks.length > 0 && (
            <Button
              type="button"
              variant="outline-primary"
              onClick={onOpenPdf}
              icon="bi-file-earmark-pdf"
              className="text-nowrap"
              title="Xem và xuất Bản đăng ký công việc (Mẫu 01) ra PDF"
            >
              Xuất Mẫu 01 (PDF)
            </Button>
          )}

          <Button
            type="button"
            variant="primary"
            onClick={onSubmit}
            disabled={isSubmitting || !isWeightValid || !isCountValid}
            loading={isSubmitting}
            loadingText="Đang lưu..."
            icon="bi-check2-circle"
            className="px-4"
          >
            Lưu đăng ký (Mẫu 01)
          </Button>
        </div>
      </div>
    </div>
  );
}
