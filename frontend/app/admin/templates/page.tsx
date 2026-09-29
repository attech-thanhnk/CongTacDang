"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { AdminModal } from "@/components/admin/AdminModal";
import { NoAccess } from "@/components/admin/NoAccess";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import {
  TEMPLATE_MAX_FILE_BYTES,
  TemplateCheck,
  TemplateSummary,
  TemplateTag,
  templateService,
  TemplateVersion,
} from "@/services/templateService";

const KIND_LABELS: Record<TemplateTag["kind"], string> = {
  field: "Trường",
  repeat: "Khối lặp",
  if: "Hiện khi đúng",
  ifnot: "Hiện khi sai",
};

function baseName(fileName: string) {
  return fileName.replace(/\.docx$/i, "");
}

function formatSize(bytes: number) {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/** Kết quả kiểm tra: lỗi (chặn), cảnh báo (không chặn), tag có trong tệp. */
function CheckResult({ check }: { check: TemplateCheck }) {
  return (
    <div className="small">
      {check.errors.length > 0 ? (
        <div className="alert alert-danger py-2 mb-2">
          <div className="fw-semibold mb-1">Không dùng được tệp này ({check.errors.length} lỗi):</div>
          <ul className="mb-0 ps-3">
            {check.errors.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        </div>
      ) : (
        <div className="alert alert-success py-2 mb-2">Tệp hợp lệ: mở được, không có tag lạ, sinh thử thành công.</div>
      )}
      {check.warnings.length > 0 && (
        <div className="alert alert-warning py-2 mb-2">
          <div className="fw-semibold mb-1">Cảnh báo ({check.warnings.length}):</div>
          <ul className="mb-0 ps-3">
            {check.warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
        </div>
      )}
      {check.tags.length > 0 && (
        <div className="text-secondary">
          Tag trong tệp:{" "}
          {check.tags.map((t) => (
            <code key={t} className={`me-2 ${check.unknownTags.includes(t) ? "text-danger" : ""}`}>
              {t}
            </code>
          ))}
        </div>
      )}
    </div>
  );
}

/** Hộp thoại tải lên phiên bản mới: chọn tệp → kiểm tra → tải lên (kích hoạt ngay hoặc để sau). */
function UploadModal({
  template,
  onClose,
  onUploaded,
}: {
  template: TemplateSummary;
  onClose: () => void;
  onUploaded: () => void;
}) {
  const { toast } = useToast();
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [note, setNote] = useState("");
  const [activate, setActivate] = useState(true);
  const [check, setCheck] = useState<TemplateCheck | null>(null);
  const [busy, setBusy] = useState<"" | "check" | "upload">("");

  const handleFile = (event: React.ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0] || null;
    setCheck(null);
    if (selected && !selected.name.toLowerCase().endsWith(".docx")) {
      toast.error("Chỉ nhận tệp Word .docx.");
      event.target.value = "";
      setFile(null);
      return;
    }
    if (selected && selected.size > TEMPLATE_MAX_FILE_BYTES) {
      toast.error("Tệp vượt quá 10 MB. Hãy nén ảnh trong tài liệu rồi thử lại.");
      event.target.value = "";
      setFile(null);
      return;
    }
    setFile(selected);
  };

  const handleCheck = async () => {
    if (!file) return;
    setBusy("check");
    try {
      setCheck(await templateService.check(template.code, file));
    } catch (err) {
      toast.error(errorMessage(err, "Không kiểm tra được tệp."), errorTitle(err), 8000);
    } finally {
      setBusy("");
    }
  };

  const handleUpload = async () => {
    if (!file) return;
    setBusy("upload");
    try {
      const result = await templateService.upload(template.code, file, note.trim(), activate);
      setCheck(result);
      if (!result.isValid || !result.version) {
        toast.error("File mẫu có lỗi nên chưa được lưu. Hãy sửa rồi tải lên lại.", "Dữ liệu chưa hợp lệ", 8000);
        return;
      }
      toast.success(
        result.version.isActive
          ? `Đã tải lên và kích hoạt phiên bản ${result.version.versionNumber}.`
          : `Đã tải lên phiên bản ${result.version.versionNumber} (chưa kích hoạt).`
      );
      onUploaded();
    } catch (err) {
      toast.error(errorMessage(err, "Không tải lên được file mẫu."), errorTitle(err), 8000);
    } finally {
      setBusy("");
    }
  };

  return (
    <AdminModal
      title={`Tải lên phiên bản mới — ${template.name}`}
      onClose={onClose}
      closeDisabled={busy !== ""}
      maxWidth={720}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onClose} disabled={busy !== ""}>
            Đóng
          </button>
          <button type="button" className="btn btn-outline-primary btn-sm" onClick={handleCheck} disabled={!file || busy !== ""}>
            {busy === "check" ? "Đang kiểm tra…" : "Kiểm tra"}
          </button>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={handleUpload}
            disabled={!file || busy !== "" || (check !== null && !check.isValid)}
          >
            {busy === "upload" ? "Đang tải lên…" : "Tải lên"}
          </button>
        </>
      }
    >
      <p className="small text-secondary">
        Sửa file mẫu trong Word (bật tab Developer), giữ nguyên các Content Control có Tag. Nên tải file đang dùng về để sửa.
        Hệ thống kiểm tra tag và sinh thử tài liệu trước khi lưu; tag không nhận diện sẽ bị từ chối.
      </p>
      <div className="mb-3">
        <input ref={input} type="file" accept=".docx" className="form-control form-control-sm" onChange={handleFile} disabled={busy !== ""} />
      </div>
      <div className="mb-3">
        <label className="form-label small fw-semibold mb-1" htmlFor="template-note">
          Ghi chú
        </label>
        <input
          id="template-note"
          className="form-control form-control-sm"
          maxLength={500}
          placeholder="Lý do thay đổi, căn cứ văn bản…"
          value={note}
          onChange={(e) => setNote(e.target.value)}
          disabled={busy !== ""}
        />
      </div>
      <div className="form-check mb-3">
        <input
          id="template-activate"
          type="checkbox"
          className="form-check-input"
          checked={activate}
          onChange={(e) => setActivate(e.target.checked)}
          disabled={busy !== ""}
        />
        <label className="form-check-label small" htmlFor="template-activate">
          Kích hoạt ngay (biểu mẫu xuất từ bây giờ dùng phiên bản này)
        </label>
      </div>
      {check && <CheckResult check={check} />}
    </AdminModal>
  );
}

/** Lịch sử phiên bản: tải xuống, kích hoạt lại, quay về file gốc. */
function HistoryModal({
  template,
  onClose,
  onChanged,
}: {
  template: TemplateSummary;
  onClose: () => void;
  onChanged: () => void;
}) {
  const { toast, confirm } = useToast();
  const [versions, setVersions] = useState<TemplateVersion[] | null>(null);
  const [busy, setBusy] = useState("");

  const load = useCallback(async () => {
    try {
      setVersions(await templateService.versions(template.code));
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được lịch sử phiên bản."), errorTitle(err));
      setVersions([]);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [template.code]);

  useEffect(() => {
    load();
  }, [load]);

  const handleActivate = (version: TemplateVersion) =>
    confirm({
      title: "Kích hoạt phiên bản",
      message: `Biểu mẫu xuất từ bây giờ sẽ dùng phiên bản ${version.versionNumber} (${version.originalFileName}). Tiếp tục?`,
      confirmText: "Kích hoạt",
      onConfirm: async () => {
        setBusy(version.id);
        try {
          await templateService.activate(template.code, version.id);
          toast.success(`Đã kích hoạt phiên bản ${version.versionNumber}.`);
          await load();
          onChanged();
        } catch (err) {
          toast.error(errorMessage(err, "Không kích hoạt được phiên bản."), errorTitle(err), 8000);
        } finally {
          setBusy("");
        }
      },
    });

  const handleDownload = async (version: TemplateVersion) => {
    setBusy(`dl-${version.id}`);
    try {
      await templateService.downloadVersion(template.code, version, `${baseName(template.templateFileName)}_v${version.versionNumber}.docx`);
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được tệp."), errorTitle(err));
    } finally {
      setBusy("");
    }
  };

  return (
    <AdminModal title={`Lịch sử phiên bản — ${template.name}`} onClose={onClose} maxWidth={900}>
      {versions === null ? (
        <div className="text-secondary small">Đang tải…</div>
      ) : versions.length === 0 ? (
        <div className="text-secondary small">Chưa tải lên phiên bản nào — đang dùng file mẫu gốc đi kèm ứng dụng.</div>
      ) : (
        <div className="table-responsive">
          <table className="table table-sm align-middle small mb-0">
            <thead>
              <tr>
                <th>Phiên bản</th>
                <th>Tệp</th>
                <th>Tải lên</th>
                <th>Ghi chú / cảnh báo</th>
                <th className="text-end">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {versions.map((v) => (
                <tr key={v.id} className={v.isActive ? "table-success" : ""}>
                  <td className="text-nowrap">
                    v{v.versionNumber}
                    {v.isActive && <span className="badge bg-success ms-2">Đang dùng</span>}
                  </td>
                  <td>
                    <div className="text-break">{v.originalFileName}</div>
                    <div className="text-secondary">{formatSize(v.fileSize)}</div>
                  </td>
                  <td className="text-nowrap">
                    <div>{formatDateTime(v.uploadedAt)}</div>
                    <div className="text-secondary">{v.uploadedByName || "—"}</div>
                  </td>
                  <td>
                    {v.note && <div>{v.note}</div>}
                    {v.warnings.map((w) => (
                      <div key={w} className="text-warning-emphasis">
                        <i className="bi bi-exclamation-triangle me-1" />
                        {w}
                      </div>
                    ))}
                    {v.activatedAt && (
                      <div className="text-secondary">
                        Kích hoạt: {formatDateTime(v.activatedAt)}
                        {v.activatedByName ? ` — ${v.activatedByName}` : ""}
                      </div>
                    )}
                  </td>
                  <td className="text-end text-nowrap">
                    <button
                      type="button"
                      className="btn btn-outline-secondary btn-sm me-1"
                      onClick={() => handleDownload(v)}
                      disabled={busy !== ""}
                      title="Tải tệp"
                    >
                      <i className="bi bi-download" />
                    </button>
                    {!v.isActive && (
                      <button
                        type="button"
                        className="btn btn-outline-primary btn-sm"
                        onClick={() => handleActivate(v)}
                        disabled={busy !== ""}
                      >
                        {busy === v.id ? "Đang kích hoạt…" : "Kích hoạt"}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </AdminModal>
  );
}

/** Danh mục tag của biểu mẫu. */
function TagsModal({ template, onClose }: { template: TemplateSummary; onClose: () => void }) {
  return (
    <AdminModal title={`Danh mục tag — ${template.name}`} onClose={onClose} maxWidth={760}>
      <p className="small text-secondary">
        Tag bắt buộc thiếu trong file mẫu sẽ bị cảnh báo; tag không có trong danh mục bị từ chối. Tag thông tin đơn vị (ORG_*)
        dùng được ở mọi biểu mẫu, không bắt buộc — giá trị lấy từ trang Thông tin đơn vị. Hướng dẫn: docs/bieu-mau.md.
      </p>
      <div className="table-responsive">
        <table className="table table-sm small align-middle mb-0">
          <thead>
            <tr>
              <th>Tag</th>
              <th>Loại</th>
              <th>Trong khối lặp</th>
              <th>Bắt buộc</th>
            </tr>
          </thead>
          <tbody>
            {template.tags.map((t) => (
              <tr key={t.tag}>
                <td>
                  <code>{t.tag}</code>
                </td>
                <td>{KIND_LABELS[t.kind] ?? t.kind}</td>
                <td>{t.within ? <code>repeat:{t.within}</code> : "—"}</td>
                <td>{t.required ? "Có" : t.isCommon ? "Không (thông tin đơn vị)" : "Không"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </AdminModal>
  );
}

/** Quản lý file mẫu Word của biểu mẫu (quyền system.templates.manage). */
export default function TemplatesPage() {
  const { hasPermission } = useAuth();
  const { toast, confirm } = useToast();
  const canManage = hasPermission("system.templates.manage");

  const [templates, setTemplates] = useState<TemplateSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState("");
  const [dialog, setDialog] = useState<{ kind: "upload" | "history" | "tags"; template: TemplateSummary } | null>(null);

  const load = useCallback(async () => {
    try {
      setTemplates(await templateService.list());
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh mục biểu mẫu."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (canManage) load();
    else setLoading(false);
  }, [canManage, load]);

  if (!canManage) return <NoAccess title="Biểu mẫu Word" permissionName="Quản lý file mẫu biểu mẫu" />;

  const download = async (key: string, action: () => Promise<void>) => {
    setBusy(key);
    try {
      await action();
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được tệp."), errorTitle(err));
    } finally {
      setBusy("");
    }
  };

  const handleUseOriginal = (template: TemplateSummary) =>
    confirm({
      title: "Dùng file mẫu gốc",
      message: `${template.name} sẽ xuất theo file mẫu gốc đi kèm ứng dụng. Các phiên bản đã tải lên vẫn được giữ và kích hoạt lại được. Tiếp tục?`,
      confirmText: "Dùng file gốc",
      isDanger: true,
      onConfirm: async () => {
        setBusy(`orig-${template.code}`);
        try {
          await templateService.useOriginal(template.code);
          toast.success("Đã chuyển về file mẫu gốc.");
          await load();
        } catch (err) {
          toast.error(errorMessage(err, "Không chuyển được về file mẫu gốc."), errorTitle(err));
        } finally {
          setBusy("");
        }
      },
    });

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Biểu mẫu Word"
        subTitle="File mẫu của từng biểu mẫu: tải về để sửa trong Word, tải lên phiên bản mới, kích hoạt lại phiên bản cũ."
      />
      <div className="page-body">
        <Card>
          <div className="table-responsive">
            <table className="table table-hover align-middle small mb-0">
              <thead className="table-light">
                <tr>
                  <th>Mã</th>
                  <th>Biểu mẫu</th>
                  <th>Đang dùng</th>
                  <th>Cập nhật</th>
                  <th className="text-end">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={5} className="text-center text-secondary py-4">
                      Đang tải…
                    </td>
                  </tr>
                ) : (
                  templates.map((t) => {
                    const active = t.activeVersion;
                    return (
                      <tr key={t.code}>
                        <td className="text-nowrap">
                          <code>{t.code}</code>
                        </td>
                        <td>
                          <div className="fw-semibold">{t.name}</div>
                          <div className="text-secondary">{t.templateFileName}</div>
                        </td>
                        <td className="text-nowrap">
                          {active ? (
                            <span className="badge bg-primary-subtle text-primary border border-primary-subtle">
                              Phiên bản {active.versionNumber}
                            </span>
                          ) : (
                            <span className="badge bg-secondary-subtle text-secondary border">File gốc</span>
                          )}
                          <div className="text-secondary mt-1">{t.versionCount} phiên bản đã tải lên</div>
                        </td>
                        <td className="text-nowrap">
                          {active ? (
                            <>
                              <div>{formatDateTime(active.activatedAt ?? active.uploadedAt)}</div>
                              <div className="text-secondary">{active.activatedByName || active.uploadedByName || "—"}</div>
                            </>
                          ) : (
                            <span className="text-secondary">—</span>
                          )}
                        </td>
                        <td className="text-end">
                          <div className="d-inline-flex flex-wrap gap-1 justify-content-end">
                            <button
                              type="button"
                              className="btn btn-outline-secondary btn-sm"
                              disabled={busy !== ""}
                              onClick={() =>
                                download(`cur-${t.code}`, () =>
                                  templateService.downloadCurrent(
                                    t.code,
                                    active ? `${baseName(t.templateFileName)}_v${active.versionNumber}.docx` : t.templateFileName
                                  )
                                )
                              }
                            >
                              <i className="bi bi-download me-1" />
                              File đang dùng
                            </button>
                            <button
                              type="button"
                              className="btn btn-outline-secondary btn-sm"
                              disabled={busy !== ""}
                              onClick={() => download(`org-${t.code}`, () => templateService.downloadOriginal(t.code, t.templateFileName))}
                            >
                              File gốc
                            </button>
                            <button
                              type="button"
                              className="btn btn-primary btn-sm"
                              disabled={busy !== ""}
                              onClick={() => setDialog({ kind: "upload", template: t })}
                            >
                              <i className="bi bi-upload me-1" />
                              Tải lên
                            </button>
                            <button
                              type="button"
                              className="btn btn-outline-primary btn-sm"
                              disabled={busy !== ""}
                              onClick={() => setDialog({ kind: "history", template: t })}
                            >
                              Lịch sử
                            </button>
                            <button
                              type="button"
                              className="btn btn-outline-secondary btn-sm"
                              disabled={busy !== ""}
                              onClick={() => setDialog({ kind: "tags", template: t })}
                            >
                              Tag
                            </button>
                            {active && (
                              <button
                                type="button"
                                className="btn btn-outline-danger btn-sm"
                                disabled={busy !== ""}
                                onClick={() => handleUseOriginal(t)}
                              >
                                Dùng file gốc
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      {dialog?.kind === "upload" && (
        <UploadModal
          template={dialog.template}
          onClose={() => setDialog(null)}
          onUploaded={() => {
            setDialog(null);
            load();
          }}
        />
      )}
      {dialog?.kind === "history" && (
        <HistoryModal template={dialog.template} onClose={() => setDialog(null)} onChanged={load} />
      )}
      {dialog?.kind === "tags" && <TagsModal template={dialog.template} onClose={() => setDialog(null)} />}
    </div>
  );
}
