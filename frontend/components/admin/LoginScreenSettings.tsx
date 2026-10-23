"use client";

import React, { useRef, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card, CardBody } from "@/components/common";
import { errorMessage, errorTitle } from "@/components/admin/adminUtils";
import {
  LOGIN_BACKGROUND_ACCEPT,
  LOGIN_BACKGROUND_MAX_BYTES,
  LoginPanelPosition,
  OrganizationSettings,
  organizationSettingsService,
  resolveApiUrl,
  SaveOrganizationSettingsPayload,
} from "@/services/organizationSettingsService";

const POSITIONS: { value: LoginPanelPosition; label: string }[] = [
  { value: "center", label: "Giữa" },
  { value: "left", label: "Bên trái" },
  { value: "right", label: "Bên phải" },
];

const ACCEPTED_TYPES = LOGIN_BACKGROUND_ACCEPT.split(",");
const MAX_MB = LOGIN_BACKGROUND_MAX_BYTES / 1024 / 1024;

interface Props {
  settings: OrganizationSettings;
  canManage: boolean;
  onSaved: (settings: OrganizationSettings) => void;
}

function toPayload(settings: OrganizationSettings, position: LoginPanelPosition): SaveOrganizationSettingsPayload {
  return {
    partyCommitteeName: settings.partyCommitteeName,
    superiorPartyName: settings.superiorPartyName,
    companyName: settings.companyName,
    parentCompanyName: settings.parentCompanyName,
    shortName: settings.shortName,
    location: settings.location,
    appraisalUnitName: settings.appraisalUnitName,
    systemName: settings.systemName,
    loginPanelPosition: position,
  };
}

/** Ảnh nền và vị trí khung đăng nhập. */
export function LoginScreenSettings({ settings, canManage, onSaved }: Props) {
  const { toast, confirm } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  const imageUrl = settings.loginBackgroundUrl ? resolveApiUrl(settings.loginBackgroundUrl) : null;
  const position = settings.loginPanelPosition ?? "center";

  const handleFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!ACCEPTED_TYPES.includes(file.type)) {
      toast.error("Chỉ nhận ảnh JPG, PNG hoặc WEBP.");
      return;
    }
    if (file.size > LOGIN_BACKGROUND_MAX_BYTES) {
      toast.error(`Ảnh vượt quá ${MAX_MB} MB. Hãy chọn ảnh nhỏ hơn.`);
      return;
    }
    setBusy(true);
    try {
      onSaved(await organizationSettingsService.uploadLoginBackground(file));
      toast.success("Đã cập nhật ảnh nền màn hình đăng nhập.");
    } catch (err) {
      toast.error(errorMessage(err, "Không tải lên được ảnh nền."), errorTitle(err), 8000);
    } finally {
      setBusy(false);
    }
  };

  const handleRemove = () =>
    confirm({
      title: "Bỏ ảnh nền",
      message: "Màn hình đăng nhập sẽ trở về nền mặc định.",
      confirmText: "Bỏ ảnh",
      isDanger: true,
      onConfirm: async () => {
        try {
          onSaved(await organizationSettingsService.removeLoginBackground());
          toast.success("Đã bỏ ảnh nền màn hình đăng nhập.");
        } catch (err) {
          toast.error(errorMessage(err, "Không bỏ được ảnh nền."), errorTitle(err), 8000);
          throw err;
        }
      },
    });

  const handlePosition = async (value: LoginPanelPosition) => {
    if (value === position) return;
    setBusy(true);
    try {
      onSaved(await organizationSettingsService.update(toPayload(settings, value)));
      toast.success("Đã lưu vị trí khung đăng nhập.");
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được vị trí khung đăng nhập."), errorTitle(err), 8000);
    } finally {
      setBusy(false);
    }
  };

  const cardJustify = position === "left" ? "flex-start" : position === "right" ? "flex-end" : "center";

  return (
    <Card className="mt-3">
      <div className="card-header">
        <h2 className="mb-0">Màn hình đăng nhập</h2>
      </div>
      <CardBody>
        <div className="row g-4">
          <div className="col-12 col-lg-6">
            <div
              className="border rounded overflow-hidden d-flex"
              style={{
                aspectRatio: "16 / 9",
                maxWidth: 420,
                padding: "6% 5%",
                alignItems: position === "center" ? "center" : "flex-start",
                justifyContent: cardJustify,
                background: imageUrl ? `center / cover no-repeat url("${imageUrl}")` : "var(--bg)",
              }}
              aria-label="Xem trước màn hình đăng nhập"
            >
              <div
                className="rounded border"
                style={{ width: "26%", aspectRatio: "3 / 4", background: "var(--paper)", boxShadow: "0 2px 8px rgba(0,0,0,.25)" }}
              />
            </div>
            <div className="form-text">
              {settings.loginBackgroundFileName ?? "Chưa có ảnh nền — đang dùng nền mặc định."}
            </div>
          </div>

          <div className="col-12 col-lg-6">
            {canManage && (
              <div className="mb-3">
                <div className="d-flex flex-wrap gap-2">
                  <button
                    type="button"
                    className="btn btn-outline-primary btn-sm"
                    disabled={busy}
                    onClick={() => fileInput.current?.click()}
                  >
                    <i className="bi bi-upload me-1" />
                    {imageUrl ? "Thay ảnh" : "Tải ảnh lên"}
                  </button>
                  {imageUrl && (
                    <button type="button" className="btn btn-outline-danger btn-sm" disabled={busy} onClick={handleRemove}>
                      <i className="bi bi-trash me-1" />
                      Bỏ ảnh
                    </button>
                  )}
                </div>
                <input
                  ref={fileInput}
                  type="file"
                  accept={LOGIN_BACKGROUND_ACCEPT}
                  className="d-none"
                  onChange={handleFile}
                />
                <div className="form-text">JPG, PNG hoặc WEBP, tối đa {MAX_MB} MB. Nên dùng ảnh ngang, rộng từ 1920 px.</div>
              </div>
            )}

            <label className="form-label" htmlFor="org-loginPanelPosition">Vị trí khung đăng nhập</label>
            <select
              id="org-loginPanelPosition"
              className="form-select form-select-sm"
              style={{ maxWidth: 220 }}
              value={position}
              disabled={!canManage || busy}
              onChange={(e) => handlePosition(e.target.value as LoginPanelPosition)}
            >
              {POSITIONS.map((p) => (
                <option key={p.value} value={p.value}>{p.label}</option>
              ))}
            </select>
            <div className="form-text">Trên màn hình hẹp, khung đăng nhập luôn ở giữa.</div>
          </div>
        </div>
      </CardBody>
    </Card>
  );
}

export default LoginScreenSettings;
