"use client";

import React, { useEffect, useRef, useState } from "react";
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
  { value: "right", label: "Bên phải ảnh" },
  { value: "left", label: "Bên trái ảnh" },
  { value: "center", label: "Dưới ảnh" },
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
  const [imageNatural, setImageNatural] = useState<{ w: number; h: number } | null>(null);

  useEffect(() => {
    setImageNatural(null);
    if (!imageUrl) return;
    let active = true;
    const img = new window.Image();
    img.onload = () => {
      if (active) setImageNatural({ w: img.naturalWidth, h: img.naturalHeight });
    };
    img.src = imageUrl;
    return () => {
      active = false;
    };
  }, [imageUrl]);

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

  const vertical = position === "center";
  const mockImage = (
    <div
      className="rounded"
      style={{
        flex: vertical ? "0 0 auto" : "1 1 0",
        width: vertical ? "60%" : undefined,
        aspectRatio: imageNatural ? `${imageNatural.w} / ${imageNatural.h}` : "16 / 9",
        background: imageUrl ? `center / contain no-repeat url("${imageUrl}")` : "var(--rule)",
      }}
    />
  );
  const mockCard = (
    <div
      className="rounded border"
      style={{ width: vertical ? "26%" : "28%", aspectRatio: "3 / 4", background: "var(--paper)", flex: "0 0 auto" }}
    />
  );

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
                padding: "5%",
                gap: "5%",
                flexDirection: vertical ? "column" : position === "left" ? "row-reverse" : "row",
                alignItems: "center",
                justifyContent: "center",
                background: "#2a3140",
              }}
              aria-label="Xem trước màn hình đăng nhập"
            >
              {mockImage}
              {mockCard}
            </div>
            <div className="form-text">
              {settings.loginBackgroundFileName
                ? `${settings.loginBackgroundFileName}${imageNatural ? ` — ${imageNatural.w}×${imageNatural.h} px` : ""}`
                : "Chưa có ảnh nền — đang dùng nền mặc định."}
            </div>
            {imageNatural && imageNatural.w < 1280 && (
              <div className="form-text text-warning-emphasis">
                Ảnh nhỏ, có thể hơi mờ trên màn hình lớn. Nên dùng ảnh rộng từ 1600 px.
              </div>
            )}
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
                <div className="form-text">JPG, PNG hoặc WEBP, tối đa {MAX_MB} MB. Nên dùng ảnh ngang, rộng từ 1600 px.</div>
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
            <div className="form-text">Trên màn hình hẹp, khung đăng nhập luôn nằm dưới ảnh.</div>
          </div>
        </div>
      </CardBody>
    </Card>
  );
}

export default LoginScreenSettings;
