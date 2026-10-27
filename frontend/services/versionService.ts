import { useEffect, useState } from "react";
import { request } from "./apiClient";

/** Phiên bản backend đang chạy. */
export interface AppVersion {
  commit: string;
  buildTime: string;
  environment: string;
}

const UNKNOWN = "chưa rõ";
let cache: Promise<AppVersion> | null = null;

/** Mã commit đã ghi vào bản build giao diện. */
export const FRONTEND_COMMIT = process.env.NEXT_PUBLIC_APP_VERSION || UNKNOWN;

export const versionService = {
  get(): Promise<AppVersion> {
    if (!cache) {
      cache = request<AppVersion>("/version").catch((err) => {
        cache = null;
        throw err;
      });
    }
    return cache;
  },
};

/** Dòng "Phiên bản <commit>"; thêm mã của giao diện khi hai phía lệch nhau (một trong hai chưa được dựng lại). */
export function versionLabel(v: AppVersion | null): string {
  if (!v) return "";
  const mismatch = FRONTEND_COMMIT !== UNKNOWN && v.commit !== UNKNOWN && FRONTEND_COMMIT !== v.commit;
  return mismatch ? `Phiên bản ${v.commit} (giao diện ${FRONTEND_COMMIT})` : `Phiên bản ${v.commit}`;
}

export function useAppVersion(): AppVersion | null {
  const [version, setVersion] = useState<AppVersion | null>(null);
  useEffect(() => {
    let alive = true;
    versionService.get().then((v) => alive && setVersion(v)).catch(() => {});
    return () => {
      alive = false;
    };
  }, []);
  return version;
}
