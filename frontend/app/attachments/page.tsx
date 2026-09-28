"use client";

import React, { useState, useEffect, useRef } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { DocumentViewerModal } from "@/components/attachments/DocumentViewerModal";
import { PageHeader, Button, EmptyState } from "@/components/common";

export default function AttachmentsPage() {
  const { hasPermission } = useAuth();
  const { toast, confirm } = useToast();
  const [files, setFiles] = useState<AttachmentItem[]>([]);
  const [searchTerm, setSearchTerm] = useState("");
  const [loading, setLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadSuccess, setUploadSuccess] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [formCode, setFormCode] = useState("");
  const [description, setDescription] = useState("");
  const [dragActive, setDragActive] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // State xem tài liệu trực tiếp
  const [viewerOpen, setViewerOpen] = useState(false);
  const [viewerAttachmentId, setViewerAttachmentId] = useState<string | null>(null);
  const [viewerFileName, setViewerFileName] = useState<string | null>(null);

  const loadFiles = async () => {
    setLoading(true);
    setServerError(null);
    try {
      const data = await attachmentService.getAttachments();
      setFiles(data);
    } catch (err: any) {
      setServerError(err.message || "Không thể tải danh sách tệp tin.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadFiles();
  }, []);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0];
      if (file.size > 25 * 1024 * 1024) {
        setUploadError("Dung lượng tệp vượt quá 25MB.");
        setSelectedFile(null);
        return;
      }
      setSelectedFile(file);
      setUploadError(null);
    }
  };

  const handleDrag = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true);
    } else if (e.type === "dragleave") {
      setDragActive(false);
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      const file = e.dataTransfer.files[0];
      if (file.size > 25 * 1024 * 1024) {
        setUploadError("Dung lượng tệp vượt quá 25MB.");
        setSelectedFile(null);
        return;
      }
      setSelectedFile(file);
      setUploadError(null);
    }
  };

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      setUploadError("Vui lòng chọn một tệp tin.");
      return;
    }

    setIsUploading(true);
    setUploadError(null);
    setUploadSuccess(null);

    try {
      await attachmentService.uploadAttachment(selectedFile, formCode.trim(), description.trim());
      toast.success(`Đã tải lên tệp "${selectedFile.name}".`);
      setUploadSuccess(`Đã tải lên tệp "${selectedFile.name}".`);
      setSelectedFile(null);
      setFormCode("");
      setDescription("");
      if (fileInputRef.current) fileInputRef.current.value = "";
      loadFiles();
    } catch (err: any) {
      setUploadError(err.message || "Tải lên thất bại.");
      toast.error(err.message || "Tải lên thất bại.");
    } finally {
      setIsUploading(false);
    }
  };

  const handleDelete = (id: string, fileName: string) => {
    confirm({
      title: "Xác nhận xóa tệp tin",
      message: `Đồng chí có chắc chắn muốn xóa tệp tin "${fileName}" khỏi hệ thống?`,
      confirmText: "Xóa tệp",
      isDanger: true,
      onConfirm: async () => {
        try {
          await attachmentService.deleteAttachment(id);
          toast.success(`Đã xóa tệp "${fileName}".`);
          loadFiles();
        } catch (err: any) {
          toast.error(err.message || "Không thể xóa tệp tin.");
        }
      },
    });
  };

  const handleOpenViewer = (id: string, fileName: string) => {
    setViewerAttachmentId(id);
    setViewerFileName(fileName);
    setViewerOpen(true);
  };

  const filteredFiles = files.filter((f) => {
    const term = searchTerm.toLowerCase();
    return (
      f.fileName.toLowerCase().includes(term) ||
      (f.category || "").toLowerCase().includes(term) ||
      (f.description || "").toLowerCase().includes(term) ||
      f.uploadedBy.toLowerCase().includes(term)
    );
  });

  return (
    <div className="page-wrapper">
      {/* Header dùng chung */}
      <PageHeader
        title="Tài liệu đính kèm"
        actions={
          <Button
            size="sm"
            variant="outline-secondary"
            icon="bi-arrow-clockwise"
            loading={loading}
            loadingText="Đang tải..."
            onClick={loadFiles}
            title="Tải lại dữ liệu"
          >
            Tải lại
          </Button>
        }
      />

      {/* Grid: Tải lên (nếu có quyền) & Danh sách tệp */}
      <div className="page-body">
        {serverError && (
          <div className="alert alert-danger small mb-0">
            {serverError}
          </div>
        )}

        <div className="row g-3">
        {/* Cột trái: Tải lên (Chỉ hiển thị khi có quyền upload) */}
        {hasPermission("attachments.upload") && (
          <div className="col-12 col-lg-4">
          <div className="border rounded bg-white" style={{ borderColor: "#e2e8f0" }}>
              <div className="card-header bg-white border-bottom py-2 px-3" style={{ borderColor: "#e2e8f0" }}>
                <span className="fw-semibold text-dark small">Tải lên tài liệu</span>
              </div>

              <div className="card-body p-3">
                <form onSubmit={handleUploadSubmit} className="space-y-2.5">
                  <div
                    onDragEnter={handleDrag}
                    onDragLeave={handleDrag}
                    onDragOver={handleDrag}
                    onDrop={handleDrop}
                    onClick={() => fileInputRef.current?.click()}
                    className={`border border-2 border-dashed rounded p-3 text-center transition ${
                      dragActive
                        ? "border-primary bg-primary-subtle text-primary"
                        : selectedFile
                        ? "border-success bg-success-subtle text-success"
                        : "border-secondary-subtle bg-light text-secondary"
                    }`}
                    style={{ cursor: "pointer" }}
                  >
                    <input
                      ref={fileInputRef}
                      type="file"
                      onChange={handleFileSelect}
                      className="d-none"
                      accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.zip,.rar"
                    />

                    {selectedFile ? (
                      <div>
                        <i className="bi bi-check-circle-fill fs-4 text-success d-block mb-1"></i>
                        <div className="fw-semibold small text-dark text-truncate">{selectedFile.name}</div>
                        <div className="text-muted small" style={{ fontSize: "11px" }}>
                          {(selectedFile.size / 1024).toFixed(1)} KB — Nhấp để đổi tệp
                        </div>
                      </div>
                    ) : (
                      <div>
                        <i className="bi bi-folder-symlink fs-4 text-secondary d-block mb-1"></i>
                        <div className="fw-medium small text-dark">Kéo thả tệp vào đây</div>
                        <div className="text-muted small" style={{ fontSize: "11px" }}>hoặc nhấp chuột để chọn tệp</div>
                      </div>
                    )}
                  </div>

                  <div>
                    <label className="form-label small fw-semibold text-secondary mb-1">Mã biểu mẫu</label>
                    <select
                      value={formCode}
                      onChange={(e) => setFormCode(e.target.value)}
                      className="form-select form-select-sm"
                    >
                      <option value="">Tùy chọn (Mẫu 01, 02...)</option>
                      <option value="MAU_01">Mẫu 01 - Đăng ký nhiệm vụ</option>
                      <option value="MAU_02">Mẫu 02 - Tự đánh giá sản phẩm</option>
                      <option value="MAU_09">Mẫu 09 - Tự chấm tiêu chí chung</option>
                      <option value="MAU_10">Mẫu 10 - Chi bộ nhận xét</option>
                      <option value="MAU_13">Mẫu 13 - Biên bản kiểm phiếu</option>
                      <option value="MINH_CHUNG">Minh chứng thực tế</option>
                    </select>
                  </div>

                  <div>
                    <label className="form-label small fw-semibold text-secondary mb-1">Mô tả tệp</label>
                    <textarea
                      rows={2}
                      placeholder="Mô tả nội dung tệp..."
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      className="form-control form-control-sm"
                    />
                  </div>

                  {uploadError && (
                    <div className="alert alert-danger py-1.5 px-2.5 small mb-0">
                      {uploadError}
                    </div>
                  )}

                  {uploadSuccess && (
                    <div className="alert alert-success py-1.5 px-2.5 small mb-0">
                      {uploadSuccess}
                    </div>
                  )}

                  <Button
                    type="submit"
                    disabled={!selectedFile}
                    loading={isUploading}
                    loadingText="Đang tải lên..."
                    icon="bi-upload"
                    className="w-100"
                  >
                    Tải lên
                  </Button>
                </form>
              </div>
            </div>
          </div>
        )}

        {/* Cột phải: Danh sách tệp (tự động giãn full width nếu không có quyền upload) */}
        <div className={hasPermission("attachments.upload") ? "col-12 col-lg-8" : "col-12"}>
          <div className="border rounded bg-white" style={{ borderColor: "#e2e8f0" }}>
            <div className="card-header bg-white border-bottom py-2 px-3" style={{ borderColor: "#e2e8f0" }}>
              <div className="d-flex justify-content-between align-items-center gap-2">
                <span className="small text-secondary">
                  Tổng cộng: <strong>{filteredFiles.length}</strong> tệp tin
                </span>

                <input
                  type="text"
                  placeholder="Tìm kiếm tệp tin..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="form-control form-control-sm"
                  style={{ maxWidth: "240px" }}
                />
              </div>
            </div>

            <div className="card-body p-0">
              {loading ? (
                <div className="text-center py-5 text-muted small">
                  <div className="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                  <span>Đang nạp danh sách tài liệu...</span>
                </div>
              ) : filteredFiles.length === 0 ? (
                <EmptyState
                  icon="bi-folder-x"
                  title="Chưa có tệp tin nào"
                  description="Không tìm thấy tài liệu nào trong kho lưu trữ hoặc theo từ khóa tìm kiếm."
                />
              ) : (
                <div className="table-responsive">
                  <table className="table table-sm table-hover align-middle mb-0">
                    <thead className="text-center">
                      <tr>
                        <th style={{ width: "40px" }}>STT</th>
                        <th>Tên tệp</th>
                        <th style={{ width: "95px" }}>Biểu mẫu</th>
                        <th style={{ width: "80px" }}>Dung lượng</th>
                        <th style={{ width: "120px" }}>Người tải</th>
                        <th style={{ width: "140px" }}>Thao tác</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredFiles.map((f, idx) => (
                        <tr key={f.id}>
                          <td className="text-center text-muted">{idx + 1}</td>
                          <td>
                            <div className="fw-semibold text-dark text-truncate" style={{ maxWidth: "260px" }}>
                              {f.fileName}
                            </div>
                            {f.description && (
                              <div className="text-muted text-truncate" style={{ fontSize: "11.5px", maxWidth: "260px" }}>
                                {f.description}
                              </div>
                            )}
                          </td>
                          <td className="text-center">
                            {f.formCode ? (
                              <span className="badge bg-light text-secondary border font-monospace" style={{ fontSize: "10px" }}>
                                {f.formCode}
                              </span>
                            ) : "—"}
                          </td>
                          <td className="text-center text-secondary">{f.fileSize}</td>
                          <td className="text-secondary">{f.uploadedBy}</td>
                          <td className="text-center">
                            <div className="d-flex justify-content-center gap-1.5">
                              <Button
                                size="sm"
                                variant="outline-primary"
                                onClick={() => handleOpenViewer(f.id, f.fileName)}
                                style={{ fontSize: "11.5px" }}
                              >
                                Xem
                              </Button>
                              <Button
                                size="sm"
                                variant="outline-secondary"
                                onClick={() => attachmentService.downloadAttachment(f.id, f.fileName)}
                                style={{ fontSize: "11.5px" }}
                              >
                                Tải về
                              </Button>
                              {hasPermission("attachments.delete") && (
                                <Button
                                  size="sm"
                                  variant="outline-danger"
                                  onClick={() => handleDelete(f.id, f.fileName)}
                                  style={{ fontSize: "11.5px" }}
                                >
                                  Xóa
                                </Button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
      </div>

      {/* Modal xem trước */}
      {viewerOpen && viewerAttachmentId && (
        <DocumentViewerModal
          isOpen={viewerOpen}
          onClose={() => setViewerOpen(false)}
          attachmentId={viewerAttachmentId}
          fileName={viewerFileName || "Tài liệu"}
        />
      )}
    </div>
  );
}
