import { useRef } from "react";

/** Có thay đổi so với lúc mở form không (so ảnh chụp đầu tiên khi form chuyển từ null sang có giá trị). */
export function useFormDirty<T>(form: T | null | undefined): boolean {
  const baseline = useRef<string | null>(null);
  if (form == null) {
    baseline.current = null;
    return false;
  }
  const current = JSON.stringify(form);
  if (baseline.current === null) baseline.current = current;
  return current !== baseline.current;
}

export default useFormDirty;
