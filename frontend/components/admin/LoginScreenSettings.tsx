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
import styles from "./LoginScreenSettings.module.css";

const POSITIONS: { value: LoginPanelPosition; label: string }[] = [
  { value: "center", label: "Giữa" },
  { value: "left", label: "Bên trái" },
  { value: "right", label: "Bên phải" },
];

const ACCEPTED_TYPES = LOGIN_BACKGROUND_ACCEPT.split(",");
const MAX_MB = LOGIN_BACKGROUND_MAX_BYTES / 1024 / 1024;
// Ảnh hẹp hơn mức này bị phóng to và mờ trên màn hình máy tính.
const MIN_GOOD_WIDTH = 1280;
// Bản xem trước coi khung là màn hình rộng 1440 px / điện thoại rộng 390 px; trang đăng nhập phóng ảnh tối đa 1,3 lần.
const DESKTOP_WIDTH = 1440;
const PHONE_WIDTH = 390;
const MAX_ZOOM = 1.3;

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
    guideDocumentNumber: settings.guideDocumentNumber ?? null,
    guideDocumentDate: settings.guideDocumentDate ?? null,
    loginPanelPosition: position,
  };
}

function MiniCard({ glass }: { glass: boolean }) {
  return (
    <div className={`${styles.card} ${glass ? styles.glass : ""}`}>
      <span className={styles.emblem} />
      <span className={styles.line} />
      <span className={styles.input} />
      <span className={styles.input} />
      <span className={styles.button} />
    </div>
  );
}

/** Ảnh nền và vị trí khung đăng nhập. */
export function LoginScreenSettings({ settings, canManage, onSaved }: Props) {
  const { toast, confirm } = useToast();
  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState<{ url: string; width: number; height: number } | null>(null);

  const imageUrl = settings.loginBackgroundUrl ? resolveApiUrl(settings.loginBackgroundUrl) : null;
  const position = settings.loginPanelPosition ?? "center";

  // Cỡ ảnh gắn với đường dẫn để ảnh mới không dùng nhầm cỡ ảnh cũ.
  const size = loaded && loaded.url === imageUrl ? loaded : null;

  const handleImageLoad = (e: React.SyntheticEvent<HTMLImageElement>) => {
    const { naturalWidth, naturalHeight } = e.currentTarget;
    if (imageUrl) setLoaded({ url: imageUrl, width: naturalWidth, height: naturalHeight });
  };

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
      toast.error(errorMessage(err, "Tải lên ảnh nền không thành công."), errorTitle(err), 8000);
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
          toast.error(errorMessage(err, "Bỏ ảnh nền không thành công."), errorTitle(err), 8000);
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
      toast.error(errorMessage(err, "Lưu vị trí khung đăng nhập không thành công."), errorTitle(err), 8000);
    } finally {
      setBusy(false);
    }
  };

  // Giới hạn cỡ ảnh như trang thật: không quá 1,3 lần cỡ gốc so với màn hình giả định.
  const desktopMax = size ? `min(100%, ${((size.width * MAX_ZOOM) / DESKTOP_WIDTH) * 100}cqw)` : "100%";
  const phoneMax = size ? `min(100%, ${((size.width * MAX_ZOOM) / PHONE_WIDTH) * 100}cqw)` : "100%";
  const sideClass = imageUrl && position === "left" ? styles.left : imageUrl && position === "right" ? styles.right : "";

  return (
    <Card className="mt-3">
      <div className="card-header">
        <h2 className="mb-0">Màn hình đăng nhập</h2>
      </div>
      <CardBody>
        <div className="row g-4">
          <div className="col-12 col-lg-7">
            <div className={styles.previews} aria-label="Xem trước màn hình đăng nhập">
              <div style={{ flex: "1 1 260px", maxWidth: 440 }}>
                <div className={styles.label}>Máy tính</div>
                <div className={`${styles.desktop} ${imageUrl ? styles.sky : ""} ${sideClass}`}>
                  {imageUrl && (
                    <div className={styles.photo}>
                      <img src={imageUrl} alt="" onLoad={handleImageLoad} style={{ maxWidth: desktopMax }} />
                    </div>
                  )}
                  <MiniCard glass={!!imageUrl} />
                </div>
              </div>
              <div>
                <div className={styles.label}>Điện thoại</div>
                <div className={`${styles.phone} ${imageUrl ? styles.sky : ""}`}>
                  {imageUrl && (
                    <div className={styles.phonePhoto}>
                      <img src={imageUrl} alt="" style={{ maxWidth: phoneMax }} />
                    </div>
                  )}
                  <MiniCard glass={!!imageUrl} />
                </div>
              </div>
            </div>
            <div className={styles.meta}>
              {imageUrl ? (
                <>
                  {settings.loginBackgroundFileName ?? "Ảnh nền"}
                  {size && <span className="text-secondary"> · {size.width} × {size.height} px</span>}
                </>
              ) : (
                "Chưa có ảnh nền — đang dùng nền mặc định."
              )}
            </div>
            {imageUrl && size && size.width < MIN_GOOD_WIDTH && (
              <div className={styles.warn}>
                <i className="bi bi-exclamation-triangle me-1" />
                Ảnh rộng dưới {MIN_GOOD_WIDTH} px nên sẽ hiện nhỏ trên màn hình máy tính. Nên dùng ảnh rộng từ 1920 px.
              </div>
            )}
          </div>

          <div className="col-12 col-lg-5">
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
                <div className="form-text">JPG, PNG hoặc WEBP, tối đa {MAX_MB} MB.</div>
              </div>
            )}

            <div className="form-label" id="org-loginPanelPosition">Vị trí khung đăng nhập</div>
            <div className="btn-group btn-group-sm" role="radiogroup" aria-labelledby="org-loginPanelPosition">
              {POSITIONS.map((p) => (
                <button
                  key={p.value}
                  type="button"
                  role="radio"
                  aria-checked={position === p.value}
                  className={`btn ${position === p.value ? "btn-primary" : "btn-outline-secondary"} ${styles.positionButton}`}
                  disabled={!canManage || busy}
                  onClick={() => handlePosition(p.value)}
                >
                  {p.label}
                </button>
              ))}
            </div>
          </div>
        </div>
      </CardBody>
    </Card>
  );
}

export default LoginScreenSettings;
