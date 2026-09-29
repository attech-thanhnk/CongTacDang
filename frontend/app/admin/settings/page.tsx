"use client";

import React, { useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, CardBody, CardHeader, PageHeader } from "@/components/common";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import {
  OrganizationSettings,
  organizationSettingsService,
  SaveOrganizationSettingsPayload,
} from "@/services/organizationSettingsService";

type FieldKey = keyof SaveOrganizationSettingsPayload;

interface FieldMeta {
  key: FieldKey;
  label: string;
  help: string;
  maxLength: number;
}

/** Các trường thông tin đơn vị và nơi dùng (khớp máy chủ). */
const FIELDS: FieldMeta[] = [
  {
    key: "superiorPartyName",
    label: "Tên tổ chức Đảng cấp trên",
    help: "Dòng trên cùng của tiêu đề trái (Excel Mẫu 14, 15; Word Mẫu 11, 13). Ghi đúng như in, thường là chữ in hoa.",
    maxLength: 300,
  },
  {
    key: "partyCommitteeName",
    label: "Tên Đảng bộ",
    help: "Dòng tiêu đề trái của biểu mẫu, báo cáo (Excel Mẫu 14, 15, danh sách cán bộ; Word Mẫu 11, 13 khi xuất toàn Đảng bộ).",
    maxLength: 300,
  },
  {
    key: "companyName",
    label: "Tên đầy đủ của công ty",
    help: "Word Mẫu 01, 02: dòng cơ quan quản lý (tự in hoa). Trang đăng nhập.",
    maxLength: 300,
  },
  {
    key: "parentCompanyName",
    label: "Tên đơn vị chủ quản cấp trên",
    help: "Word Mẫu 10: dòng cơ quan cấp trên (tự in hoa). Trang đăng nhập.",
    maxLength: 300,
  },
  {
    key: "shortName",
    label: "Tên viết tắt",
    help: "Dùng trong tên tệp tải xuống và tiêu đề danh sách cán bộ. Chỉ chữ không dấu, số, dấu - hoặc _.",
    maxLength: 30,
  },
  {
    key: "location",
    label: "Địa danh",
    help: "Dòng \"…, ngày … tháng … năm …\" trên biểu mẫu Word và Excel.",
    maxLength: 100,
  },
  {
    key: "systemName",
    label: "Tên hiển thị của hệ thống",
    help: "Hiện trên thanh bên, đầu trang, tiêu đề cửa sổ và trang đăng nhập.",
    maxLength: 300,
  },
];

const EMPTY: SaveOrganizationSettingsPayload = {
  partyCommitteeName: "",
  superiorPartyName: "",
  companyName: "",
  parentCompanyName: "",
  shortName: "",
  location: "",
  systemName: "",
};

function toPayload(settings: OrganizationSettings): SaveOrganizationSettingsPayload {
  const { updatedAt: _updatedAt, updatedByName: _updatedByName, ...payload } = settings;
  return payload;
}

/** Thông tin đơn vị: xem (mọi người đã đăng nhập), sửa (system.settings.manage). */
export default function OrganizationSettingsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission("system.settings.manage");

  const [settings, setSettings] = useState<OrganizationSettings | null>(null);
  const [form, setForm] = useState<SaveOrganizationSettingsPayload>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    organizationSettingsService
      .get()
      .then((data) => {
        setSettings(data);
        setForm(toPayload(data));
      })
      .catch((err) => toast.error(errorMessage(err, "Không tải được thông tin đơn vị."), errorTitle(err)))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const dirty = settings !== null && FIELDS.some((f) => form[f.key] !== settings[f.key]);
  const shortNameInvalid = form.shortName.trim().length > 0 && !/^[A-Za-z0-9_-]+$/.test(form.shortName.trim());
  const missing = FIELDS.filter((f) => form[f.key].trim().length === 0);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!canManage || missing.length > 0 || shortNameInvalid) return;
    setSaving(true);
    try {
      const trimmed = Object.fromEntries(
        FIELDS.map((f) => [f.key, form[f.key].trim()])
      ) as SaveOrganizationSettingsPayload;
      const saved = await organizationSettingsService.update(trimmed);
      setSettings(saved);
      setForm(toPayload(saved));
      toast.success("Đã cập nhật thông tin đơn vị. Biểu mẫu xuất từ bây giờ dùng giá trị mới.");
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được thông tin đơn vị."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Thông tin đơn vị"
        subTitle="Tên Đảng bộ, tên công ty, tên viết tắt, địa danh dùng trên biểu mẫu, báo cáo và giao diện."
      />
      <div className="page-body">
        <Card>
          <CardHeader>
            <span className="fw-semibold">Thông tin in trên biểu mẫu</span>
          </CardHeader>
          <CardBody>
            {loading ? (
              <div className="text-secondary small">Đang tải…</div>
            ) : (
              <form onSubmit={handleSubmit} noValidate>
                {!canManage && (
                  <div className="alert alert-light border small py-2">
                    Bạn chỉ xem được. Cần quyền &quot;Quản lý thông tin đơn vị&quot; để sửa.
                  </div>
                )}
                <div className="row g-3">
                  {FIELDS.map((field) => {
                    const value = form[field.key];
                    const invalid =
                      (canManage && dirty && value.trim().length === 0) || (field.key === "shortName" && shortNameInvalid);
                    return (
                      <div className="col-12 col-lg-6" key={field.key}>
                        <label className="form-label small fw-semibold mb-1" htmlFor={`org-${field.key}`}>
                          {field.label} <span className="text-danger">*</span>
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
                            ? "Tên viết tắt chỉ gồm chữ không dấu, số, dấu - hoặc _ (dùng trong tên tệp)."
                            : invalid
                              ? `${field.label} không được để trống.`
                              : field.help}
                        </div>
                      </div>
                    );
                  })}
                </div>

                <div className="d-flex flex-wrap align-items-center gap-2 mt-4">
                  {canManage && (
                    <>
                      <button
                        type="submit"
                        className="btn btn-primary btn-sm"
                        disabled={!dirty || saving || missing.length > 0 || shortNameInvalid}
                      >
                        {saving ? "Đang lưu…" : "Lưu thay đổi"}
                      </button>
                      <button
                        type="button"
                        className="btn btn-outline-secondary btn-sm"
                        disabled={!dirty || saving}
                        onClick={() => settings && setForm(toPayload(settings))}
                      >
                        Hoàn tác
                      </button>
                    </>
                  )}
                  <span className="text-secondary small ms-auto">
                    {settings?.updatedAt
                      ? `Cập nhật lần cuối: ${formatDateTime(settings.updatedAt)}${settings.updatedByName ? ` — ${settings.updatedByName}` : ""}`
                      : "Đang dùng giá trị mặc định."}
                  </span>
                </div>
              </form>
            )}
          </CardBody>
        </Card>
      </div>
    </div>
  );
}
