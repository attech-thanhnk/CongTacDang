"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { NoAccess } from "@/components/admin/NoAccess";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import {
  IMPORT_MAX_FILE_BYTES,
  ImportCommitResult,
  ImportKind,
  ImportPreview,
  ImportRowAction,
  importService,
} from "@/services/importService";

const ACTION_LABELS: Record<ImportRowAction, { label: string; tone: string }> = {
  create: { label: "Tạo mới", tone: "tone-success" },
  update: { label: "Cập nhật", tone: "tone-info" },
  error: { label: "Lỗi", tone: "tone-danger" },
};

/** Nhập dữ liệu từ Excel: chọn loại → tải file mẫu → tải file lên → xem trước → nhập. */
export default function ImportsPage() {
  const { hasPermission } = useAuth();
  const { toast, confirm } = useToast();
  const canImport = hasPermission("system.import");

  const [kinds, setKinds] = useState<ImportKind[]>([]);
  const [kind, setKind] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [onlyErrors, setOnlyErrors] = useState(false);
  const [result, setResult] = useState<ImportCommitResult | null>(null);
  const [resultDownloaded, setResultDownloaded] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!canImport) {
      setLoading(false);
      return;
    }
    importService
      .kinds()
      .then((list) => {
        setKinds(list);
        setKind((current) => current || list[0]?.kind || "");
      })
      .catch((err) => toast.error(errorMessage(err, "Không tải được danh sách loại dữ liệu."), errorTitle(err)))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canImport]);

  const selected = kinds.find((k) => k.kind === kind);

  const visibleRows = useMemo(
    () => (preview ? (onlyErrors ? preview.rows.filter((r) => r.action === "error") : preview.rows) : []),
    [preview, onlyErrors]
  );

  if (!canImport) return <NoAccess title="Nhập dữ liệu" permissionName="Nhập dữ liệu từ Excel" />;

  const reset = () => {
    setFile(null);
    setPreview(null);
    setResult(null);
    setResultDownloaded(false);
    setOnlyErrors(false);
    if (fileInput.current) fileInput.current.value = "";
  };

  const chooseKind = (value: string) => {
    setKind(value);
    reset();
  };

  const downloadTemplate = async () => {
    if (!selected) return;
    setBusy("template");
    try {
      await importService.downloadTemplate(selected.kind);
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được file mẫu."), errorTitle(err));
    } finally {
      setBusy("");
    }
  };

  const chooseFile = (picked: File | null) => {
    setPreview(null);
    setResult(null);
    setResultDownloaded(false);
    if (picked && !picked.name.toLowerCase().endsWith(".xlsx")) {
      toast.error("Chỉ nhận file Excel .xlsx.");
      setFile(null);
      if (fileInput.current) fileInput.current.value = "";
      return;
    }
    if (picked && picked.size > IMPORT_MAX_FILE_BYTES) {
      toast.error("File lớn quá 5 MB. Hãy chia thành nhiều file nhỏ hơn.");
      setFile(null);
      if (fileInput.current) fileInput.current.value = "";
      return;
    }
    setFile(picked);
  };

  const runPreview = async () => {
    if (!selected || !file) return;
    setBusy("preview");
    try {
      const data = await importService.preview(selected.kind, file);
      setPreview(data);
      setOnlyErrors(data.summary.error > 0);
      setResult(null);
    } catch (err) {
      setPreview(null);
      toast.error(errorMessage(err, "Không đọc được file."), errorTitle(err));
    } finally {
      setBusy("");
    }
  };

  const runCommit = () => {
    if (!preview || !selected) return;
    const parts = [
      preview.summary.create > 0 ? `tạo mới ${preview.summary.create}` : "",
      preview.summary.update > 0 ? `cập nhật ${preview.summary.update}` : "",
    ].filter(Boolean);
    confirm({
      title: `Nhập ${selected.name.toLowerCase()}`,
      message: `Nhập ${preview.summary.total} dòng (${parts.join(", ")}). Tiếp tục?`,
      confirmText: "Nhập dữ liệu",
      onConfirm: async () => {
        setBusy("commit");
        try {
          const data = await importService.commit(preview.sessionId);
          setResult(data);
          setResultDownloaded(false);
          setPreview(null);
          setFile(null);
          if (fileInput.current) fileInput.current.value = "";
          toast.success(`Đã nhập ${data.total} dòng.`);
        } catch (err) {
          toast.error(errorMessage(err, "Không nhập được dữ liệu."), errorTitle(err));
        } finally {
          setBusy("");
        }
      },
    });
  };

  const downloadResult = async () => {
    if (!result?.resultFileToken) return;
    setBusy("result");
    try {
      await importService.downloadResult(result.resultFileToken, `mat-khau-tam-${result.kind}.xlsx`);
      setResultDownloaded(true);
    } catch (err) {
      setResultDownloaded(true);
      toast.error(errorMessage(err, "Không tải được file mật khẩu tạm."), errorTitle(err));
    } finally {
      setBusy("");
    }
  };

  const columns = preview?.columns ?? [];

  return (
    <div className="page-wrapper">
      <PageHeader title="Nhập dữ liệu" />
      <div className="page-body d-flex flex-column gap-3">
        <Card>
          <div className="card-body">
            {loading ? (
              <div className="text-secondary loading-delay">Đang tải…</div>
            ) : kinds.length === 0 ? (
              <div className="text-secondary">Bạn chưa được giao quyền nhập loại dữ liệu nào.</div>
            ) : (
              <>
                <div className="row g-3 align-items-end">
                  <div className="col-12 col-md-4">
                    <label className="form-label fw-semibold" htmlFor="import-kind">
                      Loại dữ liệu
                    </label>
                    <select
                      id="import-kind"
                      className="form-select"
                      value={kind}
                      disabled={busy !== ""}
                      onChange={(e) => chooseKind(e.target.value)}
                    >
                      {kinds.map((k) => (
                        <option key={k.kind} value={k.kind}>
                          {k.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-12 col-md-auto">
                    <button type="button" className="btn btn-outline-secondary" disabled={!selected || busy !== ""} onClick={downloadTemplate}>
                      <i className="bi bi-download me-1" />
                      {busy === "template" ? "Đang tải…" : "Tải file mẫu"}
                    </button>
                  </div>
                  <div className="col-12 col-md">
                    <label className="form-label fw-semibold" htmlFor="import-file">
                      File dữ liệu (.xlsx)
                    </label>
                    <input
                      id="import-file"
                      ref={fileInput}
                      type="file"
                      accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                      className="form-control"
                      disabled={busy !== ""}
                      onChange={(e) => chooseFile(e.target.files?.[0] ?? null)}
                    />
                  </div>
                  <div className="col-12 col-md-auto">
                    <button type="button" className="btn btn-primary" disabled={!file || busy !== ""} onClick={runPreview}>
                      <i className="bi bi-search me-1" />
                      {busy === "preview" ? "Đang kiểm tra…" : "Kiểm tra file"}
                    </button>
                  </div>
                </div>

                {selected && (
                  <div className="mt-3 small">
                    <div className="text-secondary mb-2">{selected.description}</div>
                    <div className="d-flex flex-wrap gap-1">
                      {selected.columns.map((c) => (
                        <span key={c.key} className={`tag ${c.required ? "t-wait" : "t-info"}`} title={c.description}>
                          {c.header}
                          {c.required ? " *" : ""}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </Card>

        {result && (
          <Card>
            <div className="card-body">
              <div className="fw-semibold mb-1">
                <i className="bi bi-check-circle text-success me-1" />
                Đã nhập {result.total} dòng: {result.created} tạo mới, {result.updated} cập nhật.
              </div>
              {result.resultFileToken && (
                <div className="alert alert-warning py-2 mt-2 mb-0 d-flex flex-wrap align-items-center gap-2">
                  <div className="flex-grow-1">
                    {resultDownloaded
                      ? "Đã tải file mật khẩu tạm. File không tải lại được."
                      : `File mật khẩu tạm của tài khoản mới chỉ tải được một lần${
                          result.resultFileExpiresAt ? `, trước ${formatDateTime(result.resultFileExpiresAt)}` : ""
                        }.`}
                  </div>
                  {!resultDownloaded && (
                    <button type="button" className="btn btn-warning btn-sm" disabled={busy !== ""} onClick={downloadResult}>
                      <i className="bi bi-key me-1" />
                      {busy === "result" ? "Đang tải…" : "Tải file mật khẩu tạm"}
                    </button>
                  )}
                </div>
              )}
              <button type="button" className="btn btn-outline-secondary btn-sm mt-3" onClick={reset}>
                Nhập file khác
              </button>
            </div>
          </Card>
        )}

        {preview && (
          <Card>
            <div className="card-body pb-2">
              <div className="d-flex flex-wrap align-items-center gap-2">
                <span className="fw-semibold me-1">{preview.fileName}</span>
                <span className="status-badge tone-neutral">{preview.summary.total} dòng</span>
                {preview.summary.create > 0 && <span className="status-badge tone-success">{preview.summary.create} tạo mới</span>}
                {preview.summary.update > 0 && <span className="status-badge tone-info">{preview.summary.update} cập nhật</span>}
                {preview.summary.error > 0 && <span className="status-badge tone-danger">{preview.summary.error} lỗi</span>}
                <div className="form-check form-switch ms-md-auto mb-0">
                  <input
                    id="only-errors"
                    className="form-check-input"
                    type="checkbox"
                    checked={onlyErrors}
                    disabled={preview.summary.error === 0}
                    onChange={(e) => setOnlyErrors(e.target.checked)}
                  />
                  <label className="form-check-label" htmlFor="only-errors">
                    Chỉ dòng lỗi
                  </label>
                </div>
              </div>
            </div>
            <div className="table-responsive" style={{ maxHeight: "60vh" }}>
              <table className="table table-sm table-hover align-middle mb-0">
                <thead className="sticky-top bg-white">
                  <tr>
                    <th className="text-nowrap">Dòng</th>
                    <th className="text-nowrap">Kết quả</th>
                    {columns.map((c) => (
                      <th key={c.key} className="text-nowrap">
                        {c.header}
                      </th>
                    ))}
                    <th style={{ minWidth: 260 }}>Lỗi / ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleRows.map((row) => {
                    const action = ACTION_LABELS[row.action];
                    return (
                      <tr key={row.rowNumber} className={row.action === "error" ? "table-danger" : undefined}>
                        <td>{row.rowNumber}</td>
                        <td className="text-nowrap">
                          <span className={`status-badge ${action.tone}`}>{action.label}</span>
                        </td>
                        {columns.map((c) => (
                          <td key={c.key} className="text-nowrap">
                            {row.data[c.key] || <span className="text-secondary">—</span>}
                          </td>
                        ))}
                        <td className="small">
                          {row.errors.map((e, i) => (
                            <div key={`e${i}`} className="text-danger">
                              {e}
                            </div>
                          ))}
                          {row.notes.map((n, i) => (
                            <div key={`n${i}`} className="text-secondary">
                              {n}
                            </div>
                          ))}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="card-body pt-3 d-flex flex-wrap align-items-center gap-2">
              {preview.summary.error > 0 ? (
                <span className="text-danger">
                  Còn {preview.summary.error} dòng lỗi. Sửa file rồi bấm Kiểm tra file lại.
                </span>
              ) : (
                <span className="text-secondary">Phiên xem trước hết hạn lúc {formatDateTime(preview.expiresAt)}.</span>
              )}
              <div className="ms-auto d-flex gap-2">
                <button type="button" className="btn btn-outline-secondary" disabled={busy !== ""} onClick={reset}>
                  Hủy
                </button>
                <button
                  type="button"
                  className="btn btn-primary"
                  disabled={preview.summary.error > 0 || busy !== ""}
                  onClick={runCommit}
                >
                  <i className="bi bi-upload me-1" />
                  {busy === "commit" ? "Đang nhập…" : "Nhập dữ liệu"}
                </button>
              </div>
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}
