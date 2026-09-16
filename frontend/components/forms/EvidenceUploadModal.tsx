"use client";

import React, { useState } from "react";
import { Upload, FileText, CheckCircle2, Trash2, Download, AlertCircle, X } from "lucide-react";

export interface AttachmentItem {
  id: string;
  fileName: string;
  originalFileName: string;
  fileSize: number;
  uploadedAt: string;
  uploadedBy: string;
  formCode: string;
}

interface EvidenceUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  taskTitle: string;
  taskId?: string;
  formCode: string; // M01, M02, M04, M10
}

export function EvidenceUploadModal({
  isOpen,
  onClose,
  taskTitle,
  taskId,
  formCode
}: EvidenceUploadModalProps) {
  const [files, setFiles] = useState<AttachmentItem[]>([
    {
      id: "att-1",
      fileName: "BB_NghiemThu_CNS_T8.pdf",
      originalFileName: "Biên bản nghiệm thu kỹ thuật Trạm Radar Sơn Trà T8_2026.pdf",
      fileSize: 2450000,
      uploadedAt: "12/09/2026 14:20",
      uploadedBy: "Nguyễn Văn A",
      formCode: "M01"
    },
    {
      id: "att-2",
      fileName: "QD_ThanhLap_ToCongTac.docx",
      originalFileName: "Quyết định thành lập Tổ triển khai bảo dưỡng định kỳ 2026.docx",
      fileSize: 520000,
      uploadedAt: "13/09/2026 09:15",
      uploadedBy: "Nguyễn Văn A",
      formCode: "M01"
    }
  ]);

  const [isDragging, setIsDragging] = useState(false);
  const [uploadSuccess, setUploadSuccess] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFiles(e.dataTransfer.files);
    }
  };

  const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      handleFiles(e.target.files);
    }
  };

  const handleFiles = (uploadedFileList: FileList) => {
    const newItems: AttachmentItem[] = [];
    for (let i = 0; i < uploadedFileList.length; i++) {
      const f = uploadedFileList[i];
      newItems.push({
        id: "att-" + Date.now() + "-" + i,
        fileName: f.name,
        originalFileName: f.name,
        fileSize: f.size,
        uploadedAt: new Date().toLocaleString("vi-VN"),
        uploadedBy: "Nguyễn Văn A",
        formCode: formCode
      });
    }
    setFiles(prev => [...prev, ...newItems]);
    setUploadSuccess(`Đã tải lên thành công ${newItems.length} tệp minh chứng.`);
    setTimeout(() => setUploadSuccess(null), 3500);
  };

  const handleDelete = (id: string) => {
    setFiles(prev => prev.filter(f => f.id !== id));
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
    return (bytes / (1024 * 1024)).toFixed(2) + " MB";
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
      <div className="w-full max-w-2xl bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-900 text-white">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-dang-crimson/80 flex items-center justify-center text-dang-gold">
              <FileText className="w-5 h-5" />
            </div>
            <div>
              <h3 className="font-bold text-base leading-tight">Đính kèm Tệp Minh chứng</h3>
              <p className="text-xs text-slate-300">Biểu mẫu: <span className="font-semibold text-dang-gold">{formCode}</span> • Hỗ trợ đối soát kết quả</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Task reference info */}
        <div className="px-6 py-3 bg-amber-50/70 border-b border-amber-200/60 text-xs text-slate-700 flex items-start gap-2">
          <AlertCircle className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
          <div>
            <span className="font-semibold text-amber-900">Nhiệm vụ / Sản phẩm đính kèm:</span>{" "}
            <span className="italic">{taskTitle}</span>
          </div>
        </div>

        <div className="p-6 overflow-y-auto space-y-5 flex-1">
          {/* Drag & Drop Area */}
          <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={`border-2 border-dashed rounded-xl p-6 text-center transition-all ${
              isDragging
                ? "border-attech-blue bg-blue-50/60 scale-[1.01]"
                : "border-slate-300 hover:border-slate-400 bg-slate-50/50"
            }`}
          >
            <input
              type="file"
              id="fileUploadInput"
              multiple
              className="hidden"
              onChange={handleFileInput}
              accept=".pdf,.docx,.doc,.xlsx,.xls,.png,.jpg,.jpeg"
            />
            <label htmlFor="fileUploadInput" className="cursor-pointer flex flex-col items-center">
              <div className="w-12 h-12 rounded-full bg-blue-100 text-attech-blue flex items-center justify-center mb-2">
                <Upload className="w-6 h-6" />
              </div>
              <p className="text-sm font-semibold text-slate-800">
                Kéo và thả tệp minh chứng vào đây, hoặc <span className="text-attech-blue hover:underline">chọn tệp từ máy tính</span>
              </p>
              <p className="text-xs text-slate-400 mt-1">
                Hỗ trợ PDF, Word (.docx), Excel (.xlsx), Hình ảnh minh chứng • Tối đa 25MB/tệp
              </p>
            </label>
          </div>

          {uploadSuccess && (
            <div className="flex items-center gap-2 p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg text-xs animate-in fade-in">
              <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
              <span>{uploadSuccess}</span>
            </div>
          )}

          {/* List of uploaded files */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500">
                Danh sách tệp minh chứng ({files.length})
              </h4>
              <span className="text-[11px] text-slate-400">Lưu trữ bảo mật trong mạng LAN ATTECH</span>
            </div>

            {files.length === 0 ? (
              <div className="p-8 text-center border rounded-xl border-dashed border-slate-200 text-slate-400 text-xs">
                Chưa có tệp minh chứng nào được đính kèm.
              </div>
            ) : (
              <div className="divide-y divide-slate-100 border rounded-xl border-slate-200 overflow-hidden">
                {files.map((item) => (
                  <div key={item.id} className="p-3.5 flex items-center justify-between hover:bg-slate-50/80 transition text-xs">
                    <div className="flex items-center gap-3 min-w-0">
                      <div className="w-8 h-8 rounded bg-slate-100 text-slate-600 flex items-center justify-center shrink-0">
                        <FileText className="w-4 h-4 text-attech-blue" />
                      </div>
                      <div className="min-w-0">
                        <p className="font-semibold text-slate-800 truncate max-w-sm">{item.originalFileName}</p>
                        <p className="text-[11px] text-slate-400">
                          {formatFileSize(item.fileSize)} • Người tải: {item.uploadedBy} • {item.uploadedAt}
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0 ml-3">
                      <button
                        title="Tải xuống tệp"
                        onClick={() => alert(`Tải tệp: ${item.originalFileName}`)}
                        className="p-1.5 text-slate-500 hover:text-attech-blue hover:bg-blue-50 rounded transition"
                      >
                        <Download className="w-4 h-4" />
                      </button>
                      <button
                        title="Xóa tệp minh chứng"
                        onClick={() => handleDelete(item.id)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded transition"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-3.5 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
          <span className="text-xs text-slate-500">Đính kèm minh chứng là cơ sở chấm điểm tiêu chí vượt chuẩn</span>
          <button
            onClick={onClose}
            className="px-4 py-2 bg-slate-800 text-white rounded-lg text-xs font-semibold hover:bg-slate-700 transition"
          >
            Đóng & Lưu liên kết
          </button>
        </div>
      </div>
    </div>
  );
}
