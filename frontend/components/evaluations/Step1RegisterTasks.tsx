"use client";

import React, { useState } from "react";
import { TaskInputDto } from "@/services/evaluationService";
import { useToast } from "@/contexts/ToastContext";
import { Button } from "@/components/common";

interface Step1RegisterTasksProps {
  tasks: TaskInputDto[];
  onChangeTasks: (tasks: TaskInputDto[]) => void;
  onSubmit: (tasks?: TaskInputDto[]) => void;
  isSubmitting: boolean;
  isLocked?: boolean;
  onOpenUploadModal: (taskIndex: number, currentAttId?: string | null) => void;
  onOpenDocViewer: (attId: string, fileName?: string | null) => void;
  onOpenPdf?: () => void;
  onExportDocx?: () => void;
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
  onExportDocx,
}: Step1RegisterTasksProps) {
  const { toast } = useToast();
  const [newTaskName, setNewTaskName] = useState("");
  const [newTargetOutput, setNewTargetOutput] = useState("");
  const [newWeight, setNewWeight] = useState<number>(10);
  const [newDeadline, setNewDeadline] = useState("");
  const [isAddFormOpen, setIsAddFormOpen] = useState(false);

  const totalWeight = tasks.reduce((sum, t) => sum + (Number(t.weight) || 0), 0);
  const displayedTaskCount = tasks.length + (isAddFormOpen ? 1 : 0);
  const displayedTotalWeight = totalWeight + (isAddFormOpen ? (Number(newWeight) || 0) : 0);
  const isDisplayedWeightValid = Math.abs(displayedTotalWeight - 70) < 0.01;
  const isWeightValid = Math.abs(totalWeight - 70.0) < 0.01;
  const isCountValid = tasks.length >= 3 && tasks.length <= 7;

  const handleAddTask = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTaskName.trim() || !newTargetOutput.trim()) {
      toast.warning("Vui lòng nhập tên sản phẩm, công việc và chỉ tiêu đầu ra.");
      return;
    }
    if (tasks.length >= 7) {
      toast.warning("Yêu cầu tối đa không quá 7 sản phẩm, công việc.");
      return;
    }

    const updated = [
      ...tasks,
      {
        taskName: newTaskName.trim(),
        targetOutput: newTargetOutput.trim(),
        weight: Number(newWeight) || 10,
        deadline: newDeadline || new Date().toISOString().split("T")[0],
      },
    ];
    onChangeTasks(updated);
    setNewTaskName("");
    setNewTargetOutput("");
    setNewWeight(10);
    setNewDeadline("");
    setIsAddFormOpen(false);
  };

  const handleRemoveTask = (index: number) => {
    const updated = tasks.filter((_, idx) => idx !== index);
    onChangeTasks(updated);
  };

  const handleSaveRegistration = () => {
    if (isAddFormOpen) {
      if (!newTaskName.trim() || !newTargetOutput.trim()) {
        toast.warning("Vui lòng nhập đủ tên công việc và sản phẩm đầu ra.");
        return;
      }
      const newTask = {
        taskName: newTaskName.trim(),
        targetOutput: newTargetOutput.trim(),
        weight: Number(newWeight) || 10,
        deadline: newDeadline || new Date().toISOString().split("T")[0],
      };
      const updatedTasks = [
        ...tasks,
        newTask,
      ];
      const nextTotalWeight = totalWeight + newTask.weight;
      if (updatedTasks.length > 7) {
        toast.warning("Số lượng công việc tối đa là 7.");
        return;
      }
      if (Math.abs(nextTotalWeight - 70) > 0.01) {
        toast.warning(`Tổng trọng số hiện tại: ${nextTotalWeight.toFixed(1)}/70 điểm.`);
        return;
      }
      onSubmit(updatedTasks);
      return;
    }
    if (tasks.length > 0 && (tasks.length < 3 || tasks.length > 7)) {
      toast.warning(`Số lượng công việc phải từ 3 đến 7. Hiện tại: ${tasks.length}.`);
      return;
    }
    if (tasks.length > 0 && Math.abs(totalWeight - 70) > 0.01) {
      toast.warning(`Tổng trọng số hiện tại: ${totalWeight.toFixed(1)}/70 điểm.`);
      return;
    }
    onSubmit(tasks);
  };

  const handleUpdateTaskField = (index: number, field: keyof TaskInputDto, value: any) => {
    const updated = [...tasks];
    updated[index] = { ...updated[index], [field]: value };
    onChangeTasks(updated);
  };

  return (
    <div className="border rounded bg-white shadow-sm" style={{ borderColor: "#e2e8f0" }}>
      {/* Header đồng nhất */}
      <div
        className="card-header bg-white border-bottom py-2.5 px-3 d-flex justify-content-between align-items-center"
        style={{ borderColor: "#e2e8f0" }}
      >
        <div>
          <h2 className="mb-0 text-dark" style={{ fontSize: "14px", fontWeight: 600, lineHeight: 1.4 }}>
            Đăng ký sản phẩm, công việc chuyên môn (Từ 3 đến 7 sản phẩm, công việc)
          </h2>
        </div>

        <div className="d-flex align-items-center gap-3">
          <span className="text-secondary small">Công việc: <strong className="text-dark">{displayedTaskCount}</strong></span>
          <span className="text-secondary small">Tổng trọng số:</span>
          <span
            className="badge fw-semibold"
            style={{
              fontSize: "12px",
              backgroundColor: isDisplayedWeightValid ? "#ecfdf5" : "#fef2f2",
              color: isDisplayedWeightValid ? "#047857" : "#b91c1c",
              border: isDisplayedWeightValid ? "1px solid #a7f3d0" : "1px solid #fecaca",
            }}
          >
              {displayedTotalWeight.toFixed(1)} / 70.0đ
          </span>
            </div>
      </div>

      <div className="card-body p-3">
        {/* Bảng danh sách nhiệm vụ */}
        <div className="table-responsive mb-3">
          <table className="table table-sm table-hover align-middle mb-0 task-table" style={{ fontSize: "13px", tableLayout: "fixed" }}>
            <thead className="text-center" style={{ backgroundColor: "#f8fafc" }}>
              <tr style={{ fontSize: "12.5px" }} className="text-secondary">
                <th style={{ width: "40px" }}>STT</th>
                <th style={{ width: "340px" }}>Tên sản phẩm, công việc chuyên môn</th>
                <th style={{ width: "320px" }}>Chỉ tiêu đầu ra</th>
                <th style={{ width: "85px" }}>Trọng số (đ)</th>
                <th style={{ width: "125px" }}>Thời hạn</th>
                <th style={{ width: "120px" }}>Tài liệu đính kèm</th>
                {!isLocked && <th style={{ width: "45px" }}></th>}
              </tr>
            </thead>
            <tbody>
              {tasks.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-4 text-muted">
                    <div className="d-flex flex-column align-items-center justify-content-center gap-1.5 py-2">
                      <span className="fw-medium text-secondary">Chưa có sản phẩm, công việc đăng ký.</span>
                      <span className="small text-muted">Đăng ký từ 3 đến 7 sản phẩm, công việc chuyên môn theo danh mục phía dưới.</span>
                    </div>
                  </td>
                </tr>
              ) : (
                tasks.map((task, idx) => (
                  <tr key={idx}>
                    <td className="text-center text-muted">{idx + 1}</td>
                    <td>
                      {isLocked ? (
                        <div className="fw-medium text-dark task-table-long-text">{task.taskName}</div>
                      ) : (
                        <textarea
                          value={task.taskName}
                          onChange={(e) => handleUpdateTaskField(idx, "taskName", e.target.value)}
                          className="form-control form-control-sm"
                          placeholder="Tên nhiệm vụ..."
                          rows={2}
                        />
                      )}
                    </td>
                    <td>
                      {isLocked ? (
                        <div className="text-secondary task-table-long-text">{task.targetOutput}</div>
                      ) : (
                        <textarea
                          value={task.targetOutput}
                          onChange={(e) => handleUpdateTaskField(idx, "targetOutput", e.target.value)}
                          className="form-control form-control-sm"
                          placeholder="Chỉ tiêu đầu ra..."
                          rows={3}
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
                          value={task.weight || ""}
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
                          className="task-delete-button"
                        />
                      </td>
                    )}
                  </tr>
                ))
              )}
              {!isLocked && isAddFormOpen && (
                <tr className="task-draft-row">
                  <td className="text-center text-primary fw-semibold">+</td>
                  <td><textarea autoFocus rows={2} value={newTaskName} onChange={(e) => setNewTaskName(e.target.value)} className="form-control form-control-sm" placeholder="Tên công việc..." /></td>
                  <td><textarea rows={3} value={newTargetOutput} onChange={(e) => setNewTargetOutput(e.target.value)} className="form-control form-control-sm" placeholder="Sản phẩm đầu ra..." /></td>
                  <td><input type="number" min="1" max="70" step="0.5" value={newWeight || ""} onChange={(e) => setNewWeight(parseFloat(e.target.value) || 0)} className="form-control form-control-sm text-center" /></td>
                  <td><input type="date" value={newDeadline} onChange={(e) => setNewDeadline(e.target.value)} className="form-control form-control-sm" /></td>
                  <td className="text-center text-muted task-draft-empty-cell">—</td>
                  <td className="text-center">
                    <div className="d-flex flex-column gap-1 align-items-center">
                      <button type="button" className="task-draft-cancel" onClick={() => setIsAddFormOpen(false)} aria-label="Hủy thêm công việc" title="Hủy">
                        <i className="bi bi-x-lg"></i>
                      </button>
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Thêm nhanh nhiệm vụ */}
        {!isLocked && !isAddFormOpen && (
          <div className="task-add-trigger">
            <Button type="button" size="md" variant="primary" icon="bi-plus-lg" onClick={() => setIsAddFormOpen(true)}>
              Thêm công việc
            </Button>
          </div>
        )}

        {!isLocked && false && isAddFormOpen && (
          <form onSubmit={handleAddTask} className="task-add-form">
            <div className="task-add-heading">
              <div>
                <strong>Thêm công việc</strong>
                <span>Nhập thông tin công việc và sản phẩm đầu ra</span>
              </div>
            </div>
            <div className="task-add-field task-add-name">
              <label htmlFor="new-task-name">Tên công việc <span>*</span></label>
              <input
                id="new-task-name"
                type="text"
                required
                placeholder="Công tác trọng tâm"
                value={newTaskName}
                onChange={(e) => setNewTaskName(e.target.value)}
                className="form-control form-control-sm"
              />
            </div>

            <div className="task-add-field task-add-output">
              <label htmlFor="new-task-output">Sản phẩm đầu ra <span>*</span></label>
              <input
                id="new-task-output"
                type="text"
                required
                placeholder="Kết quả cần đạt"
                value={newTargetOutput}
                onChange={(e) => setNewTargetOutput(e.target.value)}
                className="form-control form-control-sm"
              />
            </div>

            <div className="task-add-field">
              <label htmlFor="new-task-weight">Trọng số <span>*</span></label>
              <input
                id="new-task-weight"
                type="number"
                required
                step="0.5"
                min="0.5"
                max="100"
                placeholder="Điểm"
                value={newWeight}
                onChange={(e) => setNewWeight(parseFloat(e.target.value) || 0)}
                className="form-control form-control-sm text-center"
              />
            </div>

            <div className="task-add-field">
              <label htmlFor="new-task-deadline">Hạn hoàn thành</label>
              <input
                id="new-task-deadline"
                type="date"
                value={newDeadline}
                onChange={(e) => setNewDeadline(e.target.value)}
                className="form-control form-control-sm text-center"
              />
            </div>
            <div className="task-add-submit">
              <Button
                type="submit"
                size="sm"
                variant="primary"
                icon="bi-plus-lg"
                className="text-nowrap"
              >
                Thêm công việc
              </Button>
              <Button type="button" size="sm" variant="outline-secondary" onClick={() => setIsAddFormOpen(false)}>
                Hủy
              </Button>
            </div>
          </form>
        )}

      </div>

      {/* Chân trang thao tác đồng nhất */}
      <div
        className="card-footer evaluation-action-bar bg-white border-top py-2.5 px-3 d-flex flex-wrap justify-content-between align-items-center gap-2"
        style={{ borderColor: "#e2e8f0" }}
      >
        <div className="text-secondary small">
          {null}
        </div>

        <div className="d-flex align-items-center gap-2 ms-auto">
          {onExportDocx && tasks.length > 0 && (
            <Button
              type="button"
              size="sm"
              variant="outline-primary"
              onClick={onExportDocx}
              icon="bi-file-earmark-word"
              className="text-nowrap"
              title="Tải Phiếu giao/đăng ký nhiệm vụ định dạng Word"
            >
              Tải bản Word (.docx)
            </Button>
          )}

          {onOpenPdf && tasks.length > 0 && (
            <Button
              type="button"
              size="sm"
              variant="outline-primary"
              onClick={onOpenPdf}
              icon="bi-file-earmark-pdf"
              className="text-nowrap"
              title="Xem và in Bản đăng ký công việc"
            >
              Xem / In (PDF)
            </Button>
          )}

          <Button
            type="button"
            size="sm"
            variant="primary"
            onClick={handleSaveRegistration}
            disabled={isSubmitting}
            loading={isSubmitting}
            loadingText="Đang lưu..."
            icon="bi-check2-circle"
            className="px-3 text-nowrap"
          >
            Lưu đăng ký công việc
          </Button>
            </div>
        </div>
      </div>
  );
}
