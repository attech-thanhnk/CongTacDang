"use client";

import React, { useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, CardBody, PageHeader } from "@/components/common";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import { LoginScreenSettings } from "@/components/admin/LoginScreenSettings";
import {
  OrganizationSettings,
  organizationSettingsService,
  SaveOrganizationSettingsPayload,
} from "@/services/organizationSettingsService";
import { DateField } from "@/components/evaluations/DateField";
import styles from "./settings.module.css";

type TextPayload = Omit<SaveOrganizationSettingsPayload, "loginPanelPosition" | "guideDocumentNumber" | "guideDocumentDate">;
type FieldKey = keyof TextPayload;

interface FieldMeta {
  key: FieldKey;
  label: string;
  help: string;
  maxLength: number;
  optional?: boolean;
}

/** Các trường thông tin đơn vị và nơi dùng (khớp máy chủ). */
const FIELDS: FieldMeta[] = [
  {
    key: "superiorPartyName",
    label: "Tên tổ chức Đảng cấp trên",
    help: "Ghi đúng như in, thường là chữ in hoa.",
    maxLength: 300,
  },
  {
    key: "partyCommitteeName",
    label: "Tên Đảng bộ",
    help: "",
    maxLength: 300,
  },
  {
    key: "companyName",
    label: "Tên đầy đủ của công ty",
    help: "",
    maxLength: 300,
  },
  {
    key: "parentCompanyName",
    label: "Tên đơn vị chủ quản cấp trên",
    help: "",
    maxLength: 300,
  },
  {
    key: "shortName",
    label: "Tên viết tắt",
    help: "Chỉ chữ không dấu, số, dấu - hoặc _.",
    maxLength: 30,
  },
  {
    key: "location",
    label: "Địa danh",
    help: "",
    maxLength: 100,
  },
  {
    key: "appraisalUnitName",
    label: "Cơ quan thẩm định",
    help: "In dưới tên công ty trên Phiếu thẩm định (Mẫu 10).",
    maxLength: 300,
    optional: true,
  },
  {
    key: "systemName",
    label: "Tên hiển thị của hệ thống",
    help: "",
    maxLength: 300,
  },
];

const EMPTY: TextPayload = {
  partyCommitteeName: "",
  superiorPartyName: "",
  companyName: "",
  parentCompanyName: "",
  shortName: "",
  location: "",
  appraisalUnitName: "",
  systemName: "",
};

function toPayload(settings: OrganizationSettings): TextPayload {
  return Object.fromEntries(FIELDS.map((f) => [f.key, settings[f.key] ?? ""])) as TextPayload;
}

/** Thông tin đơn vị: xem (mọi người đã đăng nhập), sửa (system.settings.manage). */
export default function OrganizationSettingsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission("system.settings.manage");

  const [settings, setSettings] = useState<OrganizationSettings | null>(null);
  const [form, setForm] = useState<TextPayload>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [guideNumber, setGuideNumber] = useState("");
  const [guideDate, setGuideDate] = useState("");

  useEffect(() => {
    // Bỏ kết quả của lần tải cũ (chạy hai lần ở chế độ dev) để không ghi đè phần người dùng đang nhập.
    let alive = true;
    organizationSettingsService
      .get()
      .then((data) => {
        if (!alive) return;
        setSettings(data);
        setForm(toPayload(data));
        setGuideNumber(data.guideDocumentNumber ?? "");
        setGuideDate(data.guideDocumentDate ?? "");
      })
      .catch((err) => alive && toast.error(errorMessage(err, "Tải thông tin đơn vị không thành công."), errorTitle(err)))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const dirty =
    settings !== null &&
    (FIELDS.some((f) => form[f.key] !== settings[f.key]) ||
      guideNumber !== (settings.guideDocumentNumber ?? "") ||
      guideDate !== (settings.guideDocumentDate ?? ""));
  const shortNameInvalid = form.shortName.trim().length > 0 && !/^[A-Za-z0-9_-]+$/.test(form.shortName.trim());
  const missing = FIELDS.filter((f) => !f.optional && form[f.key].trim().length === 0);

  // Cảnh báo khi đóng/tải lại trang lúc còn thay đổi chưa lưu.
  useEffect(() => {
    if (!dirty || !canManage) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty, canManage]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!canManage || missing.length > 0 || shortNameInvalid) return;
    setSaving(true);
    try {
      const trimmed = Object.fromEntries(
        FIELDS.map((f) => [f.key, form[f.key].trim()])
      ) as TextPayload;
      const saved = await organizationSettingsService.update({
        ...trimmed,
        guideDocumentNumber: guideNumber.trim() || null,
        guideDocumentDate: guideDate || null,
        loginPanelPosition: settings?.loginPanelPosition ?? "center",
      });
      setSettings(saved);
      setForm(toPayload(saved));
      setGuideNumber(saved.guideDocumentNumber ?? "");
      setGuideDate(saved.guideDocumentDate ?? "");
      toast.success("Đã cập nhật thông tin đơn vị.");
    } catch (err) {
      toast.error(errorMessage(err, "Lưu thông tin đơn vị không thành công."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Thông tin đơn vị"
      />
      <div className="page-body">
        <Card>
          <div className="card-header">
            <h2 className="mb-0">Thông tin in trên biểu mẫu</h2>
          </div>
          <CardBody>
            {loading ? (
              <div className="text-secondary small loading-delay">Đang tải…</div>
            ) : (
              <form onSubmit={handleSubmit} noValidate>
                {!canManage && (
                  <div className="alert alert-light border py-2">Chỉ xem.</div>
                )}
                <div className="row g-3">
                  {FIELDS.map((field) => {
                    const value = form[field.key];
                    const invalid =
                      (canManage && dirty && !field.optional && value.trim().length === 0) || (field.key === "shortName" && shortNameInvalid);
                    return (
                      <div className="col-12 col-lg-6" key={field.key}>
                        <label className="form-label" htmlFor={`org-${field.key}`}>
                          {field.label} {!field.optional && <span className="text-danger">*</span>}
                        </label>
                        <input
                          id={`org-${field.key}`}
                          className={`form-control form-control-sm ${invalid ? "is-invalid" : ""}`}
                          value={value}
                          maxLength={field.maxLength}
                          disabled={!canManage || saving}
                          onChange={(e) => setForm((prev) => ({ ...prev, [field.key]: e.target.value }))}
                        />
                        <div className={invalid ? "invalid-feedback" : "form-text"}>
                          {field.key === "shortName" && shortNameInvalid
                            ? "Tên viết tắt chỉ gồm chữ không dấu, số, dấu - hoặc _."
                            : invalid
                              ? `${field.label} không được để trống.`
                              : field.help}
                        </div>
                      </div>
                    );
                  })}
                  <div className="col-12 col-lg-6">
                    <label className="form-label" htmlFor="org-guideDocumentNumber">Số văn bản hướng dẫn</label>
                    <input
                      id="org-guideDocumentNumber"
                      className="form-control form-control-sm"
                      value={guideNumber}
                      maxLength={50}
                      disabled={!canManage || saving}
                      onChange={(e) => setGuideNumber(e.target.value)}
                    />
                  </div>
                  <div className="col-12 col-lg-6">
                    <label className="form-label" htmlFor="org-guideDocumentDate">Ngày văn bản hướng dẫn</label>
                    <DateField id="org-guideDocumentDate" value={guideDate} disabled={!canManage || saving} onChange={setGuideDate} />
                  </div>
                </div>

                <div className={styles.footer}>
                  <span className={styles.updated}>
                    {settings?.updatedAt
                      ? `Cập nhật lần cuối: ${formatDateTime(settings.updatedAt)}${settings.updatedByName ? ` — ${settings.updatedByName}` : ""}`
                      : "Đang dùng giá trị mặc định."}
                  </span>
                  {canManage && (
                    <>
                      {dirty && (
                        <span className={`${styles.state} ${styles.stateDirty}`} role="status">
                          <span className={styles.dot} aria-hidden="true" />
                          Có thay đổi chưa lưu
                        </span>
                      )}
                      <button
                        type="button"
                        className={`btn btn-outline-secondary ${styles.action}`}
                        disabled={!dirty || saving}
                        onClick={() => {
                          if (!settings) return;
                          setForm(toPayload(settings));
                          setGuideNumber(settings.guideDocumentNumber ?? "");
                          setGuideDate(settings.guideDocumentDate ?? "");
                        }}
                      >
                        Hoàn tác
                      </button>
                      <button
                        type="submit"
                        className={`btn btn-primary ${styles.action}`}
                        disabled={!dirty || saving || missing.length > 0 || shortNameInvalid}
                      >
                        {saving ? "Đang lưu…" : "Lưu"}
                      </button>
                    </>
                  )}
                </div>
              </form>
            )}
          </CardBody>
        </Card>
        {settings && !loading && (
          <LoginScreenSettings settings={settings} canManage={canManage} onSaved={setSettings} />
        )}
      </div>
    </div>
  );
}
