"use client";

import React, { useEffect, useState } from "react";
import { AccountListItem, userService } from "@/services/userService";
import { errorMessage } from "./adminUtils";

export interface PickedUser {
  id: string;
  username: string;
  fullName: string;
}

/** Ô tìm tài khoản (theo tên đăng nhập, họ tên, email, số thẻ) để chọn người được gán vai trò. */
export function UserPicker({
  value,
  onChange,
  activeOnly = true,
}: {
  value: PickedUser | null;
  onChange: (user: PickedUser | null) => void;
  /** Chỉ tìm tài khoản đang hoạt động (mặc định). */
  activeOnly?: boolean;
}) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<AccountListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (value) return;
    const term = query.trim();
    if (term.length < 2) {
      setResults([]);
      return;
    }
    let cancelled = false;
    const timer = setTimeout(() => {
      setLoading(true);
      userService
        .search({ q: term, pageSize: 10, isActive: activeOnly ? true : undefined })
        .then((page) => {
          if (!cancelled) {
            setResults(page.items);
            setError(null);
          }
        })
        .catch((err: unknown) => {
          if (!cancelled) setError(errorMessage(err, "Không tìm được tài khoản."));
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query, value, activeOnly]);

  if (value) {
    return (
      <div className="d-flex align-items-center justify-content-between border rounded px-2 py-1 small">
        <span>
          <strong>{value.fullName}</strong> <span className="text-secondary">({value.username})</span>
        </span>
        <button type="button" className="btn btn-sm btn-link p-0" onClick={() => onChange(null)}>
          Đổi người
        </button>
      </div>
    );
  }

  return (
    <div>
      <input
        className="form-control form-control-sm"
        placeholder="Nhập ít nhất 2 ký tự: tên đăng nhập, họ tên, email…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        autoComplete="off"
      />
      {loading && <div className="small text-secondary mt-1">Đang tìm…</div>}
      {error && <div className="small text-danger mt-1">{error}</div>}
      {results.length > 0 && (
        <div className="list-group mt-1" style={{ maxHeight: 220, overflowY: "auto" }}>
          {results.map((account) => (
            <button
              type="button"
              key={account.id}
              className="list-group-item list-group-item-action py-1 small"
              onClick={() => onChange({ id: account.id, username: account.username, fullName: account.fullName })}
            >
              <strong>{account.fullName}</strong> <span className="text-secondary">({account.username})</span>
              <div className="text-secondary" style={{ fontSize: 12 }}>
                {[account.positionTitle, account.departmentName, account.partyCellName].filter(Boolean).join(" · ") || "—"}
              </div>
            </button>
          ))}
        </div>
      )}
      {!loading && !error && query.trim().length >= 2 && results.length === 0 && (
        <div className="small text-secondary mt-1">{activeOnly ? "Không có tài khoản đang hoạt động phù hợp." : "Không có tài khoản phù hợp."}</div>
      )}
    </div>
  );
}

export default UserPicker;
