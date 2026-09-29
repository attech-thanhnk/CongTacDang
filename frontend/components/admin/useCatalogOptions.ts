"use client";

import { useEffect, useState } from "react";
import { catalogService, CatalogItem } from "@/services/catalogService";

/** Danh mục đơn vị chính quyền và tổ chức Đảng (mọi người đã đăng nhập đều xem được) cho các ô chọn. */
export function useCatalogOptions() {
  const [departments, setDepartments] = useState<CatalogItem[]>([]);
  const [partyCells, setPartyCells] = useState<CatalogItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.all([catalogService.list("departments"), catalogService.list("branches")])
      .then(([deps, cells]) => {
        if (cancelled) return;
        setDepartments(deps);
        setPartyCells(cells);
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : "Không tải được danh mục tổ chức.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return { departments, partyCells, error };
}
