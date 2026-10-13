"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { DEFAULT_PAGE_SIZE, PAGE_SIZE_OPTIONS } from "./Pagination";

const STORAGE_PREFIX = "ctd.pageSize.";

function isAllowedSize(size: number): boolean {
  return (PAGE_SIZE_OPTIONS as readonly number[]).includes(size);
}

function readPageSize(storageKey?: string): number {
  if (!storageKey) return DEFAULT_PAGE_SIZE;
  try {
    const stored = Number(window.localStorage.getItem(STORAGE_PREFIX + storageKey));
    return isAllowedSize(stored) ? stored : DEFAULT_PAGE_SIZE;
  } catch {
    return DEFAULT_PAGE_SIZE;
  }
}

function writePageSize(storageKey: string | undefined, size: number) {
  if (!storageKey) return;
  try {
    window.localStorage.setItem(STORAGE_PREFIX + storageKey, String(size));
  } catch {
    /* trình duyệt chặn lưu trữ: vẫn dùng giá trị trong bộ nhớ */
  }
}

/** Số dòng/trang ghi nhớ theo khóa — dùng trực tiếp cho danh sách phân trang phía máy chủ. */
export function usePageSize(storageKey?: string): [number, (size: number) => void] {
  // Đọc ngay khi khởi tạo để không tải trang hai lần; phía máy chủ (không có window) dùng mặc định.
  const [pageSize, setPageSizeState] = useState<number>(() => readPageSize(storageKey));
  const setPageSize = useCallback(
    (size: number) => {
      const next = isAllowedSize(size) ? size : DEFAULT_PAGE_SIZE;
      setPageSizeState(next);
      writePageSize(storageKey, next);
    },
    [storageKey]
  );
  return [pageSize, setPageSize];
}

export interface UsePaginationOptions {
  /** Khóa ghi nhớ số dòng/trang trong localStorage. */
  storageKey?: string;
  /** Khi một giá trị thay đổi (bộ lọc, ô tìm kiếm…) thì quay về trang 1. */
  resetKeys?: readonly unknown[];
}

export interface UsePaginationResult<T> {
  pageItems: T[];
  page: number;
  pageSize: number;
  total: number;
  setPage: (page: number) => void;
  setPageSize: (size: number) => void;
  /** Vị trí (từ 0) của dòng đầu trang hiện tại trong toàn danh sách — dùng cho cột STT. */
  offset: number;
}

/** Phân trang phía trình duyệt cho một mảng đã có trong bộ nhớ; dùng cùng `<Pagination>`. */
export function usePagination<T>(items: readonly T[], options: UsePaginationOptions = {}): UsePaginationResult<T> {
  const { storageKey, resetKeys = [] } = options;
  const [pageSize, setPageSizeRaw] = usePageSize(storageKey);
  const [page, setPageState] = useState(1);
  const total = items.length;
  const lastPage = Math.max(1, Math.ceil(total / pageSize));

  const resetSignature = JSON.stringify(resetKeys);
  useEffect(() => {
    setPageState(1);
  }, [resetSignature]);

  useEffect(() => {
    if (page > lastPage) setPageState(lastPage);
  }, [page, lastPage]);

  const current = Math.min(page, lastPage);
  const offset = (current - 1) * pageSize;
  const pageItems = useMemo(() => items.slice(offset, offset + pageSize), [items, offset, pageSize]);

  const setPage = useCallback((p: number) => setPageState(Math.max(1, p)), []);
  const setPageSize = useCallback(
    (size: number) => {
      setPageSizeRaw(size);
      setPageState(1);
    },
    [setPageSizeRaw]
  );

  return { pageItems, page: current, pageSize, total, setPage, setPageSize, offset };
}
