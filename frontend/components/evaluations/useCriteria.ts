"use client";

import { useEffect, useState } from "react";
import { evaluationService } from "@/services/evaluationService";
import type { CriteriaSnapshot } from "@/services/criteriaService";

const cache = new Map<string, Promise<CriteriaSnapshot | null>>();
/** Bộ tiêu chí đã nạp xong theo kỳ — đọc đồng bộ để trang không hiện một lần thiếu bộ tiêu chí rồi dựng lại bố cục. */
const resolved = new Map<string, CriteriaSnapshot>();

/** Nạp (hoặc lấy từ bộ nhớ) bộ tiêu chí của kỳ; trang gọi trước khi hiện nội dung để bố cục dựng một lần. */
export function loadCriteria(periodId: string): Promise<CriteriaSnapshot | null> {
  let pending = cache.get(periodId);
  if (!pending) {
    pending = evaluationService
      .getPeriod(periodId)
      .then((p) => p.criteria ?? null)
      .catch(() => null)
      .then((value) => {
        if (value) resolved.set(periodId, value);
        else cache.delete(periodId);
        return value;
      });
    cache.set(periodId, pending);
  }
  return pending;
}

/** Ảnh chụp bộ tiêu chí của kỳ (đọc từ thông tin kỳ; ghi nhớ theo kỳ trong phiên trang). */
export function useCriteria(periodId: string | undefined): { criteria: CriteriaSnapshot | null; loading: boolean } {
  const [criteria, setCriteria] = useState<CriteriaSnapshot | null>(() => (periodId ? resolved.get(periodId) ?? null : null));
  const [loading, setLoading] = useState(!!periodId && !resolved.has(periodId));

  useEffect(() => {
    if (!periodId) return;
    let alive = true;
    const ready = resolved.get(periodId);
    if (ready) {
      setCriteria(ready);
      setLoading(false);
      return;
    }
    setLoading(true);
    loadCriteria(periodId).then((value) => {
      if (!alive) return;
      setCriteria(value);
      setLoading(false);
    });
    return () => {
      alive = false;
    };
  }, [periodId]);

  return { criteria, loading };
}
