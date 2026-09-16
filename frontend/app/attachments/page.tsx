"use client";

import React, { useState, useEffect, useRef } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";

export default function AttachmentsPage() {
  const [files, setFiles] = useState<AttachmentItem[]>([]);
  const [searchTerm, setSearchTerm] = useState("");
  const [filterCategory, setFilterCategory] = useState("all");
  const [loading, setLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadSuccess, setUploadSuccess] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [category, setCategory] = useState("GENERAL");
  const [description, setDescription] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Edit attachment state
  const [editingFile, setEditingFile] = useState<AttachmentItem | null>(null);
  const [editCategory, setEditCategory] = useState("GENERAL");
  const [editDescription, setEditDescription] = useState("");
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [editError, setEditError] = useState<string | null>(null);

  const loadFiles = async () => {
    setLoading(true);
    setServerError(null);
    try {
      const data = await attachmentService.getAttachments();
      setFiles(data);
    } catch (err: any) {
      setServerError(err.message || "Không thể kết nối đến máy chủ lưu trữ tệp tin.");
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
        setUploadError("Dung lượng tệp vượt quá giới hạn cho phép (25MB).");
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
      setUploadError("Yêu cầu chọn một tệp tin trước khi tải lên.");
      return;
    }

    setIsUploading(true);
    setUploadError(null);
    setUploadSuccess(null);

    try {
      await attachmentService.uploadAttachment(selectedFile, category, description);
      setUploadSuccess(`Đã lưu tệp tin '${selectedFile.name}' vào hệ thống thành công!`);
      setSelectedFile(null);
      setDescription("");
      if (fileInputRef.current) fileInputRef.current.value = "";
      loadFiles();
    } catch (err: any) {
      setUploadError(err.message || "Tải lên thất bại. Vui lòng kiểm tra lại kết nối.");
    } finally {
      setIsUploading(false);
    }
  };

  const handleDelete = async (id: string, name: string) => {
    if (!confirm(`Xác nhận xóa tệp tin '${name}' khỏi máy chủ?`)) return;

    try {
      await attachmentService.deleteAttachment(id);
      alert("Đã xóa tệp tin thành công.");
      loadFiles();
    } catch (err: any) {
      alert(err.message || "Không thể xóa tệp tin.");
    }
  };

  const handleStartEdit = (file: AttachmentItem) => {
    setEditingFile(file);
    setEditCategory(file.category || "GENERAL");
    setEditDescription(file.description || "");
    setEditError(null);
  };

  const handleCancelEdit = () => {
    setEditingFile(null);
    setEditError(null);
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingFile) return;

    setIsSavingEdit(true);
    setEditError(null);

    try {
      await attachmentService.updateAttachment(editingFile.id, editCategory, editDescription);
      setEditingFile(null);
      await loadFiles();
    } catch (err: any) {
      setEditError(err.message || "Cập nhật thông tin tệp tin thất bại.");
    } finally {
      setIsSavingEdit(false);
    }
  };

  const handleDownload = (id: string) => {
    attachmentService.downloadAttachment(id);
  };

  const filteredFiles = files.filter(f => {
    const matchesSearch = f.fileName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                          (f.description || "").toLowerCase().includes(searchTerm.toLowerCase()) ||
                          f.uploadedBy.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = filterCategory === "all" || f.category === filterCategory;
    return matchesSearch && matchesCategory;
  });

  return (
    <div className="space-y-6 font-serif max-w-5xl mx-auto">
      {/* Tiêu đề trang */}
      <div className="bg-white border border-slate-300 rounded p-5 shadow-sm">
        <div className="border-b border-slate-300 pb-3">
          <h1 className="text-base font-bold text-slate-900 uppercase">
            QUẢN LÝ TỆP TIN & VĂN BẢN ĐÍNH KÈM
          </h1>
          <p className="text-xs text-slate-600 mt-0.5">
            Lưu trữ tập trung tệp minh chứng, biểu mẫu đã ký và văn bản chỉ đạo trong mạng nội bộ
          </p>
        </div>

        {/* Khung tải lên tệp tin mới */}
        <div className="mt-4 p-4 border border-slate-300 rounded bg-slate-50">
          <h2 className="text-xs font-bold text-slate-900 uppercase mb-3">
            Tải lên tệp văn bản / minh chứng mới
          </h2>

          <form onSubmit={handleUploadSubmit} className="space-y-3">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-800 mb-1">
                  Chọn tệp tin từ máy tính <span className="text-red-600">*</span>
                </label>
                <input
                  ref={fileInputRef}
                  type="file"
                  onChange={handleFileSelect}
                  className="w-full text-xs border border-slate-300 rounded p-1.5 bg-white text-slate-700"
                  accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.zip,.rar"
                />
                <p className="text-[11px] text-slate-500 mt-1">
                  Hỗ trợ định dạng PDF, Word, Excel, Hình ảnh, Tệp nén. Tối đa 25MB.
                </p>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-800 mb-1">
                  Phân loại văn bản
                </label>
                <select
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  className="w-full text-xs border border-slate-300 rounded p-2 bg-white text-slate-800"
                >
                  <option value="GENERAL">Tài liệu chung / Công văn</option>
                  <option value="EVIDENCE">Hồ sơ minh chứng đánh giá</option>
                  <option value="FORM">Biểu mẫu đã ký duyệt</option>
                  <option value="DECISION">Quyết định / Nghị quyết</option>
                </select>
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-800 mb-1">
                Trích yếu nội dung hoặc ghi chú
              </label>
              <input
                type="text"
                placeholder="Ví dụ: Biên bản kiểm tra an toàn kỹ thuật Quý III/2026..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full text-xs border border-slate-300 rounded p-2 bg-white text-slate-800"
              />
            </div>

            {uploadError && (
              <div className="p-2.5 bg-red-50 border border-red-300 text-red-700 text-xs rounded">
                {uploadError}
              </div>
            )}

            {uploadSuccess && (
              <div className="p-2.5 bg-emerald-50 border border-emerald-300 text-emerald-800 text-xs rounded">
                {uploadSuccess}
              </div>
            )}

            <div className="flex justify-end pt-1">
              <button
                type="submit"
                disabled={isUploading || !selectedFile}
                className="px-4 py-1.5 bg-slate-900 text-white rounded text-xs font-semibold hover:bg-slate-800 disabled:opacity-50 transition"
              >
                {isUploading ? "Đang truyền dữ liệu..." : "Lưu tệp lên máy chủ"}
              </button>
            </div>
          </form>
        </div>

        {/* Bộ lọc và Danh sách tệp tin đã lưu */}
        <div className="mt-6 space-y-3">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-600">Phân loại:</span>
              <select
                value={filterCategory}
                onChange={(e) => setFilterCategory(e.target.value)}
                className="border border-slate-300 rounded px-2.5 py-1 text-xs text-slate-800 bg-white"
              >
                <option value="all">-- Tất cả danh mục --</option>
                <option value="GENERAL">Tài liệu chung / Công văn</option>
                <option value="EVIDENCE">Hồ sơ minh chứng đánh giá</option>
                <option value="FORM">Biểu mẫu đã ký duyệt</option>
                <option value="DECISION">Quyết định / Nghị quyết</option>
              </select>
            </div>

            <input
              type="text"
              placeholder="Tìm kiếm theo tên tệp, trích yếu..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="border border-slate-300 rounded px-3 py-1 text-xs w-full sm:w-72"
            />
          </div>

          {serverError && (
            <div className="p-3 bg-red-50 border border-red-300 text-red-700 text-xs rounded">
              <strong>Lỗi máy chủ:</strong> {serverError}
            </div>
          )}

          {loading ? (
            <p className="text-xs text-slate-500 py-6 text-center italic">Đang tải danh mục tệp tin từ máy chủ...</p>
          ) : filteredFiles.length === 0 ? (
            <div className="border border-slate-200 rounded p-6 text-center text-xs text-slate-600 bg-slate-50">
              Chưa có tệp tin nào được lưu trữ trong danh mục này.
            </div>
          ) : (
            <div className="overflow-x-auto border border-slate-300 rounded">
              <table className="w-full text-xs text-slate-800 border-collapse">
                <thead>
                  <tr className="bg-slate-100 font-bold border-b border-slate-300 text-left">
                    <th className="border-r border-slate-300 p-2.5 text-center w-10">STT</th>
                    <th className="border-r border-slate-300 p-2.5">Tên tệp tin</th>
                    <th className="border-r border-slate-300 p-2.5 w-36">Phân loại</th>
                    <th className="border-r border-slate-300 p-2.5 w-24 text-right">Dung lượng</th>
                    <th className="border-r border-slate-300 p-2.5 w-28 text-center">Ngày tải lên</th>
                    <th className="border-r border-slate-300 p-2.5 w-32">Người tải lên</th>
                    <th className="p-2.5 text-center w-28">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredFiles.map((f, idx) => (
                    <tr key={f.id} className="border-b border-slate-200 hover:bg-slate-50">
                      <td className="border-r border-slate-200 p-2 text-center">{idx + 1}</td>
                      <td className="border-r border-slate-200 p-2">
                        <div className="font-semibold text-slate-950">{f.fileName}</div>
                        {f.description && (
                          <div className="text-[11px] text-slate-600 italic mt-0.5">{f.description}</div>
                        )}
                        {f.checksum && (
                          <div className="text-[10px] font-mono text-slate-500 mt-0.5">
                            SHA-256: {f.checksum.substring(0, 16)}...
                          </div>
                        )}
                      </td>
                      <td className="border-r border-slate-200 p-2 text-slate-700">
                        <div>{attachmentService.getCategoryName(f.category)}</div>
                        {f.provider && (
                          <span className="inline-block mt-0.5 px-1.5 py-0.5 text-[9px] font-mono bg-slate-100 text-slate-600 border border-slate-200 rounded">
                            {f.provider.toUpperCase()}
                          </span>
                        )}
                      </td>
                      <td className="border-r border-slate-200 p-2 text-right font-mono text-slate-600">
                        {attachmentService.formatFileSize(f.fileSize)}
                      </td>
                      <td className="border-r border-slate-200 p-2 text-center text-slate-600">
                        {new Date(f.uploadedAt).toLocaleDateString("vi-VN")}
                      </td>
                      <td className="border-r border-slate-200 p-2 text-slate-700">
                        {f.uploadedBy}
                      </td>
                      <td className="p-2 text-center space-x-2">
                        <button
                          onClick={() => handleDownload(f.id)}
                          className="text-[11px] text-blue-700 hover:text-blue-900 underline font-semibold"
                        >
                          Tải về
                        </button>
                        <button
                          onClick={() => handleStartEdit(f)}
                          className="text-[11px] text-amber-700 hover:text-amber-900 underline font-semibold"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleDelete(f.id, f.fileName)}
                          className="text-[11px] text-red-600 hover:text-red-800 underline"
                        >
                          Xóa
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Modal Sửa thông tin tệp tin */}
      {editingFile && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white border border-slate-400 rounded-lg shadow-xl max-w-lg w-full p-5 space-y-4">
            <div className="border-b border-slate-300 pb-2 flex justify-between items-center">
              <h2 className="text-sm font-bold text-slate-900 uppercase">
                SỬA THÔNG TIN TỆP TIN
              </h2>
              <button
                onClick={handleCancelEdit}
                className="text-slate-400 hover:text-slate-700 text-lg leading-none"
              >
                &times;
              </button>
            </div>

            {editError && (
              <div className="p-2.5 bg-red-50 border border-red-300 text-red-700 text-xs rounded">
                {editError}
              </div>
            )}

            <form onSubmit={handleSaveEdit} className="space-y-3 text-xs">
              <div>
                <label className="block text-slate-600 font-semibold mb-1">Tên tệp tin (cố định):</label>
                <div className="p-2 bg-slate-100 border border-slate-200 rounded font-semibold text-slate-800">
                  {editingFile.fileName}
                </div>
              </div>

              <div>
                <label className="block text-slate-700 font-semibold mb-1">Danh mục hồ sơ:</label>
                <select
                  value={editCategory}
                  onChange={(e) => setEditCategory(e.target.value)}
                  className="w-full border border-slate-300 rounded p-2 text-xs focus:ring-1 focus:ring-blue-500 focus:outline-none"
                >
                  <option value="GENERAL">Tài liệu chung / Công văn</option>
                  <option value="EVIDENCE">Hồ sơ minh chứng đánh giá</option>
                  <option value="FORM">Biểu mẫu đã ký duyệt</option>
                  <option value="DECISION">Quyết định / Nghị quyết</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-700 font-semibold mb-1">Trích yếu / Nội dung tệp tin:</label>
                <textarea
                  rows={3}
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  placeholder="Nhập trích yếu hoặc tóm tắt nội dung văn bản..."
                  className="w-full border border-slate-300 rounded p-2 text-xs focus:ring-1 focus:ring-blue-500 focus:outline-none"
                />
              </div>

              <div className="pt-2 flex justify-end space-x-2 border-t border-slate-200">
                <button
                  type="button"
                  onClick={handleCancelEdit}
                  disabled={isSavingEdit}
                  className="px-3 py-1.5 border border-slate-300 text-slate-700 rounded hover:bg-slate-50"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSavingEdit}
                  className="px-4 py-1.5 bg-blue-800 text-white font-semibold rounded hover:bg-blue-900 disabled:opacity-50"
                >
                  {isSavingEdit ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
