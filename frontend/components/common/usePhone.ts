import { useSyncExternalStore } from "react";

const PHONE = "(max-width: 575.98px)";

/** Điện thoại (< 576px): nút thao tác chính ở lại thân trang. */
export function usePhone(): boolean {
  return useSyncExternalStore(
    (notify) => {
      const mq = window.matchMedia(PHONE);
      mq.addEventListener("change", notify);
      return () => mq.removeEventListener("change", notify);
    },
    () => window.matchMedia(PHONE).matches,
    () => false,
  );
}
