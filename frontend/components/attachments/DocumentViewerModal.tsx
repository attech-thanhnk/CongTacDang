"use client";

import React, { useState, useEffect } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";

interface DocumentViewerModalProps {
  isOpen: boolean;
  onClose: () => void;
  attachmentId?: string | null;
  attachmentFileName?: string | null;
  fileName?: string | null;
}

export function DocumentViewerModal({
  isOpen,
  onClose,
  attachmentId,
  attachmentFileName,
  fileName: propFileName,
}: DocumentViewerModalProps) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attachmentInfo, setAttachmentInfo] = useState<AttachmentItem | null>(null);

  useEffect(() => {
    if (!isOpen || !attachmentId) {
      if (blobUrl) {
        window.URL.revokeObjectURL(blobUrl);
        setBlobUrl(null);
      }
      setAttachmentInfo(null);
      setError(null);
      return;
    }

    let isMounted = true;
    setLoading(true);
    setError(null);

    attachmentService.getBlobUrl(attachmentId)
      .then((url) => {
        if (isMounted) {
          setBlobUrl(url);
        }
      })
      .catch((err) => {
        if (isMounted) {
          setError(err.message || "Không thể tải nội dung tệp minh chứng.");
        }
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isOpen, attachmentId]);

  if (!isOpen || !attachmentId) return null;

  const fileName = propFileName || attachmentFileName || attachmentInfo?.fileName || "Tệp minh chứng";
  const isPdf = fileName.toLowerCase().endsWith(".pdf");
  const isImage = /\.(jpg|jpeg|png|webp)$/i.test(fileName);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/75 backdrop-blur-sm p-4 animate-in fade-in duration-150">
      <div className="bg-white rounded-lg shadow-2xl flex flex-col w-full max-w-5xl h-[88vh] border border-slate-300 overflow-hidden">
        {/* Header modal */}
        <div className="flex items-center justify-between px-5 py-3.5 bg-slate-900 text-white">
          <div className="flex items-center gap-3 overflow-hidden">
            <span className="text-xs font-bold uppercase tracking-wider bg-rose-800 text-white px-2 py-0.5 rounded">
              Minh chứng
            </span>
            <span className="font-semibold text-sm truncate max-w-md" title={fileName}>
              {fileName}
            </span>
          </div>
          <div className="flex items-center gap-2">
            {blobUrl && (
              <>
                <button
                  type="button"
                  onClick={() => window.open(blobUrl, "_blank")}
                  className="px-3 py-1 text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 rounded transition border border-slate-700"
                >
                  Mở tab mới
                </button>
                <button
                  type="button"
                  onClick={() => attachmentService.downloadAttachment(attachmentId, fileName)}
                  className="px-3 py-1 text-xs font-medium bg-rose-800 hover:bg-rose-700 text-white rounded transition"
                >
                  Tải về máy
                </button>
              </>
            )}
            <button
              type="button"
              onClick={onClose}
              className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded transition ml-2"
              title="Đóng (Esc)"
            >
              Đóng
            </button>
          </div>
        </div>

        {/* Content Viewer Body */}
        <div className="flex-1 bg-slate-100 relative overflow-hidden flex items-center justify-center">
          {loading && (
            <div className="text-center p-8 space-y-3">
              <div className="w-8 h-8 border-3 border-rose-900 border-t-transparent rounded-full animate-spin mx-auto"></div>
              <p className="text-xs text-slate-600 font-medium">Đang nạp dữ liệu văn bản minh chứng...</p>
            </div>
          )}

          {error && (
            <div className="text-center p-8 max-w-md bg-white border border-rose-200 rounded-lg shadow-sm">
              <p className="text-sm font-semibold text-rose-800 mb-1">Không thể hiển thị văn bản</p>
              <p className="text-xs text-slate-500 mb-4">{error}</p>
              <button
                type="button"
                onClick={() => attachmentService.downloadAttachment(attachmentId, fileName)}
                className="px-4 py-2 bg-rose-900 text-white text-xs font-medium rounded hover:bg-rose-800 transition"
              >
                Thử tải trực tiếp về máy
              </button>
            </div>
          )}

          {!loading && !error && blobUrl && (
            <>
              {isPdf ? (
                <iframe
                  src={`${blobUrl}#toolbar=1&navpanes=0`}
                  title={fileName}
                  className="w-full h-full border-0"
                />
              ) : isImage ? (
                <div className="w-full h-full p-4 flex items-center justify-center overflow-auto">
                  <img
                    src={blobUrl}
                    alt={fileName}
                    className="max-h-full max-w-full object-contain rounded shadow"
                  />
                </div>
              ) : (
                <div className="text-center p-6 bg-white border border-slate-200 rounded-xl shadow-sm max-w-lg mx-4">
                  <div className="mb-3">
                    <i
                      className={`bi ${
                        fileName.toLowerCase().endsWith(".xlsx") || fileName.toLowerCase().endsWith(".xls")
                          ? "bi-file-earmark-excel-fill text-success"
                          : fileName.toLowerCase().endsWith(".docx") || fileName.toLowerCase().endsWith(".doc")
                          ? "bi-file-earmark-word-fill text-primary"
                          : "bi-file-earmark-text-fill text-slate-500"
                      }`}
                      style={{ fontSize: "54px" }}
                    ></i>
                  </div>
                  <h4 className="text-base font-bold text-slate-800 mb-1.5 break-words">{fileName}</h4>
                  <p className="text-xs text-slate-500 mb-4 leading-relaxed max-w-sm mx-auto">
                    Tệp tài liệu văn phòng ({fileName.substring(fileName.lastIndexOf("."))} ) được bảo mật.
                    Vui lòng bấm tải về để mở xem chi tiết bằng ứng dụng Microsoft Office trên máy tính.
                  </p>
                  <div className="flex justify-center gap-2">
                    <button
                      type="button"
                      onClick={() => attachmentService.downloadAttachment(attachmentId, fileName)}
                      className="px-4 py-2 bg-blue-700 text-white text-xs font-semibold rounded-lg hover:bg-blue-800 transition inline-flex items-center gap-1.5 shadow-sm"
                    >
                      <i className="bi bi-download"></i>
                      <span>Tải về máy để xem</span>
                    </button>
                    {blobUrl && (
                      <button
                        type="button"
                        onClick={() => window.open(blobUrl, "_blank")}
                        className="px-3 py-2 bg-slate-100 text-slate-700 text-xs font-medium rounded-lg hover:bg-slate-200 transition border border-slate-300"
                      >
                        Mở tab mới
                      </button>
                    )}
                  </div>
                </div>
              )}
            </>
          )}
        </div>

        {/* Footer info */}
        <div className="px-5 py-2.5 bg-white border-t border-slate-200 flex justify-between items-center text-[11px] text-slate-500">
          <span>Hệ thống số hóa Đảng bộ ATTECH • Lưu trữ tài liệu minh chứng</span>
          <span>Mã tệp: {attachmentId}</span>
        </div>
      </div>
    </div>
  );
}
