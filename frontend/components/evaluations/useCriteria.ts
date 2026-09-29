"use client";

import { useEffect, useState } from "react";
import { evaluationService } from "@/services/evaluationService";
import type { CriteriaSnapshot } from "@/services/criteriaService";

const cache = new Map<string, Promise<CriteriaSnapshot | null>>();

/** Ảnh chụp bộ tiêu chí của kỳ (đọc từ thông tin kỳ; ghi nhớ theo kỳ trong phiên trang). */
export function useCriteria(periodId: string | undefined): { criteria: CriteriaSnapshot | null; loading: boolean } {
  const [criteria, setCriteria] = useState<CriteriaSnapshot | null>(null);
  const [loading, setLoading] = useState(!!periodId);

  useEffect(() => {
    if (!periodId) return;
    let alive = true;
    let pending = cache.get(periodId);
    if (!pending) {
      pending = evaluationService.getPeriod(periodId).then((p) => p.criteria ?? null).catch(() => null);
      cache.set(periodId, pending);
    }
    setLoading(true);
    pending.then((value) => {
      if (!alive) return;
      if (!value) cache.delete(periodId);
      setCriteria(value);
      setLoading(false);
    });
    return () => {
      alive = false;
    };
  }, [periodId]);

  return { criteria, loading };
}
