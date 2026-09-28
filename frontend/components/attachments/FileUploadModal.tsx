"use client";

import React, { useState, useEffect, useRef } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";

interface FileUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: (attachment: AttachmentItem) => void;
  onUploadSuccess?: (attachment: AttachmentItem) => void;
  defaultFormCode?: string;
  formCode?: string;
  defaultDescription?: string;
  taskTitle?: string;
  targetTitle?: string;
  currentAttachmentId?: string | null;
}

export function FileUploadModal({
  isOpen,
  onClose,
  onSuccess,
  onUploadSuccess,
  defaultFormCode = "MAU01",
  formCode: propFormCode,
  defaultDescription = "",
  taskTitle: propTaskTitle,
  targetTitle,
  currentAttachmentId,
}: FileUploadModalProps) {
  const finalOnSuccess = onUploadSuccess || onSuccess || (() => {});
  const effectiveFormCode = propFormCode || defaultFormCode;
  const effectiveTaskTitle = targetTitle || propTaskTitle;
  const [activeTab, setActiveTab] = useState<"upload" | "existing">("upload");
  const [dragActive, setDragActive] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [formCode, setFormCode] = useState(effectiveFormCode);
  const [description, setDescription] = useState(defaultDescription);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Existing files in repository
  const [existingFiles, setExistingFiles] = useState<AttachmentItem[]>([]);
  const [loadingExisting, setLoadingExisting] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");

  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setFormCode(effectiveFormCode);
      setDescription(defaultDescription || (effectiveTaskTitle ? `Minh chứng cho nhiệm vụ: ${effectiveTaskTitle}` : ""));
      setSelectedFile(null);
      setErrorMsg(null);
      setActiveTab("upload");

      // Load existing files
      setLoadingExisting(true);
      attachmentService.getAttachments()
        .then((list) => setExistingFiles(list))
        .catch(() => {})
        .finally(() => setLoadingExisting(false));
    }
  }, [isOpen, effectiveFormCode, defaultDescription, effectiveTaskTitle]);

  if (!isOpen) return null;

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
      validateAndSetFile(e.dataTransfer.files[0]);
    }
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      validateAndSetFile(e.target.files[0]);
    }
  };

  const validateAndSetFile = (file: File) => {
    setErrorMsg(null);
    const validExts = [".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png"];
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!validExts.includes(ext)) {
      setErrorMsg(`Định dạng tệp '${ext}' không được chấp nhận. Chỉ cho phép PDF, DOCX, XLSX, JPG, PNG.`);
      return;
    }
    if (file.size > 25 * 1024 * 1024) {
      setErrorMsg("Dung lượng tệp vượt quá 25MB.");
      return;
    }
    setSelectedFile(file);
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      setErrorMsg("Vui lòng chọn hoặc kéo thả một tệp tin minh chứng.");
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);

    try {
      const res: any = await attachmentService.uploadAttachment(
        selectedFile,
        formCode.trim(),
        description.trim()
      );

      const created: AttachmentItem = res.data || res;
      finalOnSuccess(created);
      onClose();
    } catch (err: any) {
      setErrorMsg(err.message || "Tải lên thất bại. Vui lòng kiểm tra lại kết nối.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSelectExisting = (item: AttachmentItem) => {
    finalOnSuccess(item);
    onClose();
  };

  const filteredExisting = existingFiles.filter(f =>
    f.fileName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    (f.description || "").toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 animate-in fade-in duration-150 font-sans">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-3.5 bg-slate-900 text-white">
          <div>
            <h3 className="font-bold text-sm">Đính kèm tài liệu</h3>
            {effectiveTaskTitle && (
              <p className="text-[11px] text-slate-300 truncate max-w-md mt-0.5" title={effectiveTaskTitle}>
                Nhiệm vụ: {effectiveTaskTitle}
              </p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-white text-xs px-2 py-1 rounded transition"
          >
            Đóng
          </button>
        </div>

        {/* Tab switcher */}
        <div className="flex border-b border-slate-200 bg-slate-50 text-xs font-semibold">
          <button
            type="button"
            onClick={() => setActiveTab("upload")}
            className={`flex-1 py-2.5 px-4 text-center border-b-2 transition ${
              activeTab === "upload"
                ? "border-rose-900 text-rose-900 bg-white"
                : "border-transparent text-slate-600 hover:text-slate-900"
            }`}
          >
            Tải tệp mới từ máy tính
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("existing")}
            className={`flex-1 py-2.5 px-4 text-center border-b-2 transition ${
              activeTab === "existing"
                ? "border-rose-900 text-rose-900 bg-white"
                : "border-transparent text-slate-600 hover:text-slate-900"
            }`}
          >
            Chọn từ kho minh chứng ({existingFiles.length})
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-5 flex-1 overflow-y-auto">
          {errorMsg && (
            <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded text-xs text-rose-900 font-medium">
              {errorMsg}
            </div>
          )}

          {activeTab === "upload" ? (
            <form onSubmit={handleUpload} className="space-y-4">
              {/* Drag and drop zone */}
              <div
                onDragEnter={handleDrag}
                onDragLeave={handleDrag}
                onDragOver={handleDrag}
                onDrop={handleDrop}
                onClick={() => fileInputRef.current?.click()}
                className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition ${
                  dragActive
                    ? "border-rose-800 bg-rose-50/50"
                    : selectedFile
                    ? "border-emerald-600 bg-emerald-50/30"
                    : "border-slate-300 hover:border-slate-400 bg-slate-50/50"
                }`}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  onChange={handleFileInputChange}
                  accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png"
                  className="hidden"
                />

                {selectedFile ? (
                  <div className="space-y-1">
                    <span className="inline-block px-2.5 py-0.5 bg-emerald-700 text-white font-bold text-[11px] rounded uppercase">
                      Đã chọn tệp
                    </span>
                    <p className="text-sm font-bold text-slate-800 truncate max-w-sm mx-auto">
                      {selectedFile.name}
                    </p>
                    <p className="text-xs text-slate-500">
                      {attachmentService.formatFileSize(selectedFile.size)} • Bấm để chọn tệp khác
                    </p>
                  </div>
                ) : (
                  <div className="space-y-1.5">
                    <p className="text-xs font-bold text-slate-700">
                      Kéo và thả tệp minh chứng vào đây, hoặc <span className="text-rose-900 underline">chọn từ máy tính</span>
                    </p>
                    <p className="text-[11px] text-slate-500">
                      Hỗ trợ định dạng PDF, Word (.docx), Excel (.xlsx), Ảnh (.jpg, .png). Tối đa 25MB.
                    </p>
                  </div>
                )}
              </div>

              {/* Form fields */}
              <div className="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Phân loại minh chứng:</label>
                  <select
                    value={formCode}
                    onChange={(e) => setFormCode(e.target.value)}
                    className="w-full border border-slate-300 rounded p-2 bg-white focus:outline-none focus:border-rose-900"
                  >
                    <option value="MAU01">Đăng ký nhiệm vụ chuyên môn</option>
                    <option value="MAU02">Minh chứng sản phẩm chuyên môn</option>
                    <option value="MAU09">Minh chứng tiêu chí chung</option>
                    <option value="MAU10">Biên bản / Nhận xét Chi bộ</option>
                    <option value="MAU15">Hồ sơ thẩm định Đảng ủy</option>
                    <option value="GENERAL">Tài liệu minh chứng khác</option>
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Trích yếu minh chứng:</label>
                  <input
                    type="text"
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="VD: Quyết định, Báo cáo nghiệm thu, Kế hoạch..."
                    className="w-full border border-slate-300 rounded p-2 focus:outline-none focus:border-rose-900"
                  />
                </div>
              </div>

              {/* Actions */}
              <div className="pt-3 border-t border-slate-200 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-3.5 py-1.5 border border-slate-300 rounded text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting || !selectedFile}
                  className="px-4 py-1.5 bg-rose-900 text-white rounded text-xs font-semibold hover:bg-rose-950 disabled:opacity-50 transition"
                >
                  {isSubmitting ? "Đang lưu tệp..." : "Tải lên & Đính kèm"}
                </button>
              </div>
            </form>
          ) : (
            <div className="space-y-3">
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Tìm kiếm tệp trong kho lưu trữ..."
                className="w-full border border-slate-300 rounded p-2 text-xs focus:outline-none focus:border-rose-900"
              />

              <div className="max-h-60 overflow-y-auto divide-y divide-slate-100 border border-slate-200 rounded">
                {loadingExisting ? (
                  <p className="p-4 text-center text-xs text-slate-500">Đang tải danh sách...</p>
                ) : filteredExisting.length === 0 ? (
                  <p className="p-4 text-center text-xs text-slate-500">Chưa có tệp minh chứng nào phù hợp.</p>
                ) : (
                  filteredExisting.map((file) => (
                    <div
                      key={file.id}
                      className="p-2.5 flex items-center justify-between hover:bg-rose-50/40 transition text-xs"
                    >
                      <div className="min-w-0 pr-3">
                        <p className="font-bold text-slate-900 truncate">{file.fileName}</p>
                        <p className="text-[11px] text-slate-500 truncate">
                          {file.category} • {attachmentService.formatFileSize(file.fileSize)} • Người tải: {file.uploadedBy}
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => handleSelectExisting(file)}
                        className="px-2.5 py-1 bg-rose-900 text-white font-semibold rounded text-[11px] hover:bg-rose-950 whitespace-nowrap"
                      >
                        Chọn tệp này
                      </button>
                    </div>
                  ))
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
