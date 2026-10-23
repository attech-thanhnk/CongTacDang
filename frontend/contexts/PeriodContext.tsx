"use client";

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { EvaluationPeriodDto, evaluationService } from "@/services/evaluationService";

/** Khóa localStorage lưu kỳ đang chọn. */
const STORAGE_KEY = "ctd.periodId";

interface PeriodContextType {
  /** Không gồm kỳ Nháp; thứ tự như API trả về. */
  periods: EvaluationPeriodDto[];
  /** Chuỗi rỗng khi chưa có kỳ nào. */
  periodId: string;
  setPeriodId: (id: string) => void;
  /** null khi chưa nạp xong hoặc không có kỳ. */
  period: EvaluationPeriodDto | null;
  loading: boolean;
  /** Gọi sau khi tạo/mở/khóa kỳ. */
  reload: () => Promise<void>;
}

const PeriodContext = createContext<PeriodContextType | undefined>(undefined);

function readStored(): string {
  try {
    return window.localStorage.getItem(STORAGE_KEY) ?? "";
  } catch {
    return "";
  }
}

function writeStored(id: string) {
  try {
    if (id) window.localStorage.setItem(STORAGE_KEY, id);
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Trình duyệt chặn lưu trữ: chỉ giữ lựa chọn trong phiên hiện tại.
  }
}

/** Chọn kỳ: kỳ đã lưu nếu còn trong danh sách → kỳ đang hoạt động → kỳ đầu tiên. */
function pickPeriod(list: EvaluationPeriodDto[], preferred: string): string {
  if (preferred && list.some((p) => p.id === preferred)) return preferred;
  return (list.find((p) => p.isActive) ?? list[0])?.id ?? "";
}

/** `enabled = false` (chưa đăng nhập, phải đổi mật khẩu) thì không gọi API. */
export function PeriodProvider({ enabled, children }: { enabled: boolean; children: React.ReactNode }) {
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodIdState] = useState("");
  const [loading, setLoading] = useState(enabled);

  const reload = useCallback(async () => {
    try {
      const list = (await evaluationService.getPeriods()).filter((p) => p.status !== "Draft");
      setPeriods(list);
      setPeriodIdState((current) => {
        const next = pickPeriod(list, current || readStored());
        writeStored(next);
        return next;
      });
    } catch {
      setPeriods([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!enabled) {
      setLoading(false);
      return;
    }
    setLoading(true);
    reload();
  }, [enabled, reload]);

  const setPeriodId = useCallback((id: string) => {
    setPeriodIdState(id);
    writeStored(id);
  }, []);

  const value = useMemo<PeriodContextType>(
    () => ({
      periods,
      periodId,
      setPeriodId,
      period: periods.find((p) => p.id === periodId) ?? null,
      loading,
      reload,
    }),
    [periods, periodId, setPeriodId, loading, reload],
  );

  return <PeriodContext.Provider value={value}>{children}</PeriodContext.Provider>;
}

export function usePeriod() {
  const context = useContext(PeriodContext);
  if (!context) {
    throw new Error("usePeriod must be used within a PeriodProvider");
  }
  return context;
}
