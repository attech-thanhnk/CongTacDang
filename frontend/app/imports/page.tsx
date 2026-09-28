"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card, CardBody, PageHeader } from "@/components/common";
import {
  IMPORT_MAX_FILE_BYTES,
  importService,
  ImportCommitResult,
  ImportKind,
  ImportPreview,
  ImportPreviewRow,
} from "@/services/importService";

const ACTION_META: Record<ImportPreviewRow["action"], { label: string; badge: string; row?: string }> = {
  create: { label: "Tạo mới", badge: "bg-success-subtle text-success border border-success-subtle" },
  update: { label: "Cập nhật", badge: "bg-primary-subtle text-primary border border-primary-subtle" },
  error: { label: "Lỗi", badge: "bg-danger-subtle text-danger border border-danger-subtle", row: "table-danger" },
};

/**
 * Trang nhập dữ liệu từ Excel. Danh sách loại lấy từ máy chủ (GET /api/imports/kinds) và bảng xem trước dựng cột
 * động từ dữ liệu trả về — thêm loại import mới ở máy chủ không cần sửa trang này.
 */
export default function ImportsPage() {
  const { toast } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);

  const [kinds, setKinds] = useState<ImportKind[]>([]);
  const [loadingKinds, setLoadingKinds] = useState(true);
  const [kindCode, setKindCode] = useState<string>("");
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [onlyErrors, setOnlyErrors] = useState(false);
  const [busy, setBusy] = useState<"" | "template" | "preview" | "commit" | "download">("");
  const [result, setResult] = useState<ImportCommitResult | null>(null);
  const [resultDownloaded, setResultDownloaded] = useState(false);

  const kind = kinds.find((k) => k.kind === kindCode) || null;

  useEffect(() => {
    importService
      .getKinds()
      .then((list) => {
        setKinds(list);
        if (list.length > 0) setKindCode(list[0].kind);
      })
      .catch((err: any) => toast.error(err?.message || "Không tải được danh sách loại dữ liệu."))
      .finally(() => setLoadingKinds(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const resetFlow = () => {
    setFile(null);
    setPreview(null);
    setResult(null);
    setResultDownloaded(false);
    setOnlyErrors(false);
    if (fileInput.current) fileInput.current.value = "";
  };

  const visibleRows = useMemo(() => {
    if (!preview) return [];
    return onlyErrors ? preview.rows.filter((r) => r.action === "error") : preview.rows;
  }, [preview, onlyErrors]);

  const handleTemplate = async () => {
    if (!kind) return;
    setBusy("template");
    try {
      await importService.downloadTemplate(kind.kind);
    } catch (err: any) {
      toast.error(err?.message || "Không tải được file mẫu.");
    } finally {
      setBusy("");
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0] || null;
    setPreview(null);
    setResult(null);
    if (selected && !selected.name.toLowerCase().endsWith(".xlsx")) {
      toast.error("Chỉ nhận tệp Excel .xlsx. Hãy dùng file mẫu tải từ hệ thống.");
      event.target.value = "";
      setFile(null);
      return;
    }
    if (selected && selected.size > IMPORT_MAX_FILE_BYTES) {
      toast.error("Tệp vượt quá 5 MB. Hãy tách thành nhiều tệp nhỏ hơn.");
      event.target.value = "";
      setFile(null);
      return;
    }
    setFile(selected);
  };

  const handlePreview = async () => {
    if (!kind || !file) return;
    setBusy("preview");
    setResult(null);
    try {
      const data = await importService.preview(kind.kind, file);
      setPreview(data);
      setOnlyErrors(data.summary.error > 0);
      if (data.summary.total === 0) toast.warning("Tệp không có dòng dữ liệu nào.");
      else if (!data.canCommit) toast.warning(`Có ${data.summary.error} dòng lỗi. Hãy sửa tệp và tải lên lại.`);
    } catch (err: any) {
      toast.error(err?.message || "Không đọc được tệp.", undefined, 8000);
    } finally {
      setBusy("");
    }
  };

  const handleCommit = async () => {
    if (!preview?.canCommit) return;
    setBusy("commit");
    try {
      const data = await importService.commit(preview.sessionId);
      setResult(data);
      setPreview(null);
      toast.success(`Đã nhập dữ liệu: ${data.created} tạo mới, ${data.updated} cập nhật.`);
    } catch (err: any) {
      toast.error(err?.message || "Không ghi được dữ liệu. Không dòng nào được ghi.", undefined, 10000);
    } finally {
      setBusy("");
    }
  };

  const handleDownloadResult = async () => {
    if (!result?.resultFileToken) return;
    setBusy("download");
    try {
      await importService.downloadResult(result.resultFileToken, "tai-khoan-moi.xlsx");
      setResultDownloaded(true);
    } catch (err: any) {
      toast.error(err?.message || "Không tải được tệp kết quả.", undefined, 10000);
      if (err?.status === 404) setResultDownloaded(true);
    } finally {
      setBusy("");
    }
  };

  if (!loadingKinds && kinds.length === 0) {
    return (
      <div className="page-wrapper">
        <PageHeader title="Nhập dữ liệu" subTitle="Nhập danh mục và cán bộ từ tệp Excel." />
        <div className="page-body">
          <Card>
            <CardBody className="text-center py-5">
              <i className="bi bi-shield-lock text-secondary" style={{ fontSize: 34 }} />
              <h2 className="h6 fw-bold mt-3">Không có loại dữ liệu nào được phép nhập</h2>
              <p className="text-secondary small mb-0">
                Cần quyền &quot;Nhập dữ liệu&quot; và quyền quản lý loại dữ liệu tương ứng. Hãy liên hệ quản trị hệ thống.
              </p>
            </CardBody>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="page-wrapper">
      <PageHeader title="Nhập dữ liệu" subTitle="Tải file mẫu → điền dữ liệu → tải lên kiểm tra → xác nhận ghi toàn bộ." />

      <div className="page-body">
        <Card className="mb-3">
          <CardBody>
            <div className="row g-3 align-items-end">
              <div className="col-12 col-md-4">
                <label className="form-label small fw-semibold text-secondary mb-1">1. Loại dữ liệu</label>
                <select
                  className="form-select form-select-sm"
                  value={kindCode}
                  disabled={loadingKinds || busy !== ""}
                  onChange={(e) => {
                    setKindCode(e.target.value);
                    resetFlow();
                  }}
                >
                  {kinds.map((k) => (
                    <option key={k.kind} value={k.kind}>
                      {k.displayName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-12 col-md-auto">
                <button type="button" className="btn btn-sm btn-outline-primary" onClick={handleTemplate} disabled={!kind || busy !== ""}>
                  {busy === "template" ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-download me-1" />}
                  2. Tải file mẫu
                </button>
              </div>
              <div className="col-12 col-md">
                <label className="form-label small fw-semibold text-secondary mb-1">3. Chọn tệp đã điền (.xlsx, ≤ 5 MB, ≤ 2.000 dòng)</label>
                <input
                  ref={fileInput}
                  type="file"
                  accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                  className="form-control form-control-sm"
                  onChange={handleFileChange}
                  disabled={busy !== ""}
                />
              </div>
              <div className="col-12 col-md-auto">
                <button type="button" className="btn btn-sm btn-primary" onClick={handlePreview} disabled={!file || busy !== ""}>
                  {busy === "preview" ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-search me-1" />}
                  4. Kiểm tra tệp
                </button>
              </div>
            </div>
            {kind && <div className="small text-secondary mt-2">{kind.description}</div>}
          </CardBody>
        </Card>

        {preview && (
          <Card className="mb-3">
            <div className="card-header bg-white border-bottom py-2 px-3 d-flex flex-wrap align-items-center gap-2">
              <span className="fw-bold text-dark me-2">Kết quả kiểm tra{preview.fileName ? `: ${preview.fileName}` : ""}</span>
              <span className="badge bg-light text-dark border">Tổng {preview.summary.total}</span>
              <span className={`badge ${ACTION_META.create.badge}`}>Tạo mới {preview.summary.create}</span>
              <span className={`badge ${ACTION_META.update.badge}`}>Cập nhật {preview.summary.update}</span>
              <span className={`badge ${ACTION_META.error.badge}`}>Lỗi {preview.summary.error}</span>
              <div className="form-check form-switch ms-md-3 mb-0">
                <input
                  id="only-errors"
                  className="form-check-input"
                  type="checkbox"
                  checked={onlyErrors}
                  onChange={(e) => setOnlyErrors(e.target.checked)}
                />
                <label className="form-check-label small" htmlFor="only-errors">
                  Chỉ hiện dòng lỗi
                </label>
              </div>
              <div className="ms-auto d-flex gap-2">
                <button type="button" className="btn btn-sm btn-light" onClick={resetFlow} disabled={busy !== ""}>
                  Hủy
                </button>
                <button
                  type="button"
                  className="btn btn-sm btn-success"
                  onClick={handleCommit}
                  disabled={!preview.canCommit || busy !== ""}
                  title={preview.canCommit ? "" : "Còn dòng lỗi — không được nhập một phần"}
                >
                  {busy === "commit" ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-check2-circle me-1" />}
                  5. Xác nhận nhập {preview.summary.total} dòng
                </button>
              </div>
            </div>
            {!preview.canCommit && preview.summary.total > 0 && (
              <div className="alert alert-danger rounded-0 border-0 border-bottom small mb-0 py-2 px-3">
                Có {preview.summary.error} dòng lỗi nên không thể xác nhận (hệ thống không nhập một phần). Hãy sửa các dòng
                được tô đỏ trong tệp rồi tải lên lại.
              </div>
            )}
            <div className="table-responsive" style={{ maxHeight: "60vh" }}>
              <table className="table table-sm table-hover align-middle mb-0" style={{ fontSize: 12.5 }}>
                <thead className="sticky-top" style={{ background: "var(--bg-base)" }}>
                  <tr>
                    <th className="ps-3">Dòng</th>
                    <th>Kết quả</th>
                    {preview.columns.map((c) => (
                      <th key={c.key} className="text-nowrap" title={c.description}>
                        {c.header}
                        {c.required && <span className="text-danger">*</span>}
                      </th>
                    ))}
                    <th className="pe-3">Lý do lỗi</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleRows.length === 0 ? (
                    <tr>
                      <td colSpan={preview.columns.length + 3} className="text-center py-4 text-secondary">
                        Không có dòng nào.
                      </td>
                    </tr>
                  ) : (
                    visibleRows.map((row) => {
                      const meta = ACTION_META[row.action];
                      return (
                        <tr key={row.rowNumber} className={meta.row}>
                          <td className="ps-3 text-secondary">{row.rowNumber}</td>
                          <td>
                            <span className={`badge ${meta.badge}`}>{meta.label}</span>
                          </td>
                          {preview.columns.map((c) => (
                            <td key={c.key} className="text-nowrap">
                              {row.data[c.key] || <span className="text-secondary">—</span>}
                            </td>
                          ))}
                          <td className="pe-3 text-danger small" style={{ minWidth: 260 }}>
                            {row.errors.length > 0 && (
                              <ul className="mb-0 ps-3">
                                {row.errors.map((e, i) => (
                                  <li key={i}>{e}</li>
                                ))}
                              </ul>
                            )}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </Card>
        )}

        {result && (
          <Card className="mb-3">
            <CardBody>
              <div className="d-flex align-items-center gap-2 mb-2">
                <i className="bi bi-check-circle-fill text-success fs-5" />
                <span className="fw-bold">
                  Đã nhập xong: {result.created} tạo mới, {result.updated} cập nhật.
                </span>
              </div>
              {result.resultFileToken && (
                <div className="alert alert-warning small mb-0">
                  <div className="fw-semibold mb-1">
                    <i className="bi bi-exclamation-triangle me-1" />
                    Tệp danh sách tài khoản và mật khẩu tạm chỉ tải được MỘT LẦN và hết hạn sau 30 phút.
                  </div>
                  <div className="mb-2">
                    Hãy lưu tệp ở nơi an toàn, giao riêng mật khẩu cho từng người và hủy tệp sau khi giao. Người dùng phải đổi
                    mật khẩu ở lần đăng nhập đầu tiên.
                  </div>
                  <button
                    type="button"
                    className="btn btn-sm btn-warning"
                    onClick={handleDownloadResult}
                    disabled={resultDownloaded || busy !== ""}
                  >
                    {busy === "download" ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-download me-1" />}
                    {resultDownloaded ? "Đã tải (không tải lại được)" : "Tải tệp tài khoản"}
                  </button>
                </div>
              )}
              <button type="button" className="btn btn-sm btn-outline-secondary mt-3" onClick={resetFlow}>
                Nhập tệp khác
              </button>
            </CardBody>
          </Card>
        )}
      </div>
    </div>
  );
}
