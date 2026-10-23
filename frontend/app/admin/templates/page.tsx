"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader, Pagination, usePagination } from "@/components/common";
import { RowActions } from "@/components/common/RowActions";
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
        <div className="text-success fw-semibold mb-2">
          <i className="bi bi-check-circle me-1" />
          Tệp hợp lệ.
        </div>
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
          <button type="button" className="btn btn-outline-secondary" onClick={onClose} disabled={busy !== ""}>
            Hủy
          </button>
          <button type="button" className="btn btn-outline-secondary" onClick={handleCheck} disabled={!file || busy !== ""}>
            {busy === "check" ? "Đang kiểm tra…" : "Kiểm tra"}
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleUpload}
            disabled={!file || busy !== "" || (check !== null && !check.isValid)}
          >
            {busy === "upload" ? "Đang tải lên…" : "Tải lên"}
          </button>
        </>
      }
    >
      <div className="mb-3">
        <input ref={input} type="file" accept=".docx" className="form-control form-control-sm" onChange={handleFile} disabled={busy !== ""} />
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="template-note">
          Ghi chú
        </label>
        <input
          id="template-note"
          className="form-control form-control-sm"
          maxLength={500}
          placeholder="Lý do thay đổi…"
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
          Kích hoạt ngay
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
  const paging = usePagination(versions ?? [], { storageKey: "templates.versions" });

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
      isDanger: false,
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
        <div className="text-secondary small loading-delay">Đang tải…</div>
      ) : versions.length === 0 ? (
        <div className="text-secondary small">Chưa có phiên bản tải lên — đang dùng file mẫu gốc.</div>
      ) : (
        <>
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
              {paging.pageItems.map((v) => (
                <tr key={v.id} className={v.isActive ? "table-success" : ""}>
                  <td className="text-nowrap">
                    v{v.versionNumber}
                    {v.isActive && <span className="tag t-ok ms-2">Đang dùng</span>}
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
                    <RowActions
                      itemLabel={`v${v.versionNumber}`}
                      disabled={busy !== ""}
                      primary={[
                        { key: "activate", label: "Kích hoạt", icon: "bi-check2-circle", hidden: v.isActive, onClick: () => handleActivate(v) },
                        { key: "download", label: "Tải xuống", icon: "bi-download", onClick: () => handleDownload(v) },
                      ]}
                      iconOnly
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination
          page={paging.page}
          pageSize={paging.pageSize}
          total={paging.total}
          onPageChange={paging.setPage}
          onPageSizeChange={paging.setPageSize}
        />
        </>
      )}
    </AdminModal>
  );
}

/** Danh mục tag của biểu mẫu. */
function TagsModal({ template, onClose }: { template: TemplateSummary; onClose: () => void }) {
  return (
    <AdminModal title={`Danh mục tag — ${template.name}`} onClose={onClose} maxWidth={760}>
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
      />
      <div className="page-body">
        <Card>
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0 table-fit">
              <thead>
                <tr>
                  <th className="ps-3">Biểu mẫu</th>
                  <th className="d-none d-md-table-cell">Đang dùng</th>
                  <th className="d-none d-md-table-cell">Cập nhật</th>
                  <th className="text-end pe-3">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={4} className="text-center text-secondary py-4 loading-delay">
                      Đang tải…
                    </td>
                  </tr>
                ) : (
                  templates.map((t) => {
                    const active = t.activeVersion;
                    return (
                      <tr key={t.code}>
                        <td className="ps-3">
                          <div className="fw-semibold">{t.name}</div>
                          <div className="text-secondary small text-break">{t.templateFileName}</div>
                          {/* Màn hẹp ẩn cột phụ nên hiện bản đang dùng ngay dưới tên. */}
                          <div className="d-md-none mt-1">
                            {active ? <span className="tag t-info">Phiên bản {active.versionNumber}</span> : <span className="status-badge tone-neutral">File gốc</span>}
                          </div>
                        </td>
                        <td className="text-nowrap d-none d-md-table-cell">
                          {active ? (
                            <span className="tag t-info">
                              Phiên bản {active.versionNumber}
                            </span>
                          ) : (
                            <span className="status-badge tone-neutral">File gốc</span>
                          )}
                          {t.versionCount > 0 && (
                            <span className="text-secondary small ms-2" title="Số phiên bản đã tải lên">
                              {t.versionCount} bản tải lên
                            </span>
                          )}
                          {active && t.activeMissingTags.length > 0 && (
                            <div className="text-warning-emphasis mt-1" title={t.activeMissingTags.join("\n")}>
                              <i className="bi bi-exclamation-triangle me-1" />
                              Thiếu {t.activeMissingTags.length} tag so với file gốc
                            </div>
                          )}
                        </td>
                        <td className="text-nowrap d-none d-md-table-cell">
                          {active ? (
                            <>
                              <div>{formatDateTime(active.activatedAt ?? active.uploadedAt)}</div>
                              <div className="text-secondary">{active.activatedByName || active.uploadedByName || "—"}</div>
                            </>
                          ) : (
                            <span className="text-secondary">—</span>
                          )}
                        </td>
                        <td className="text-end pe-3">
                          <RowActions
                            itemLabel={t.name}
                            disabled={busy !== ""}
                            primary={[
                              {
                                key: "upload",
                                label: "Tải lên",
                                title: "Tải lên phiên bản mới",
                                icon: "bi-upload",
                                onClick: () => setDialog({ kind: "upload", template: t }),
                              },
                              {
                                key: "current",
                                label: "Tải xuống",
                                title: "Tải file đang dùng",
                                icon: "bi-download",
                                onClick: () =>
                                  download(`cur-${t.code}`, () =>
                                    templateService.downloadCurrent(
                                      t.code,
                                      active ? `${baseName(t.templateFileName)}_v${active.versionNumber}.docx` : t.templateFileName
                                    )
                                  ),
                              },
                            ]}
                            menu={[
                              {
                                key: "original",
                                label: "Tải file gốc",
                                icon: "bi-file-earmark-word",
                                onClick: () => download(`org-${t.code}`, () => templateService.downloadOriginal(t.code, t.templateFileName)),
                              },
                              {
                                key: "history",
                                label: "Lịch sử phiên bản",
                                icon: "bi-clock-history",
                                onClick: () => setDialog({ kind: "history", template: t }),
                              },
                              {
                                key: "tags",
                                label: "Danh mục tag",
                                icon: "bi-tags",
                                onClick: () => setDialog({ kind: "tags", template: t }),
                              },
                              {
                                key: "use-original",
                                label: "Dùng file gốc",
                                icon: "bi-arrow-counterclockwise",
                                danger: true,
                                hidden: !active,
                                onClick: () => handleUseOriginal(t),
                              },
                            ]}
                          />
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
