"use client";

import React, { useEffect, useRef } from "react";

/** Chữ số hiển thị theo kiểu Việt Nam (dấu phẩy thập phân); null → rỗng. */
export function decimalText(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value) ? "" : String(value).replace(".", ",");
}

/** Đọc số nhập bằng dấu phẩy hoặc dấu chấm; rỗng hoặc sai → null. */
export function parseDecimal(text: string): number | null {
  const s = text.trim().replace(",", ".");
  if (s === "" || !/^\d+(\.\d+)?$/.test(s)) return null;
  return Number(s);
}

/** Lỗi của ô số (sai dạng, ngoài khoảng); null khi hợp lệ hoặc để trống. */
export function decimalError(text: string, min?: number, max?: number): string | null {
  if (text.trim() === "") return null;
  const value = parseDecimal(text);
  if (value == null) return "Nhập số, dùng dấu phẩy cho phần thập phân.";
  if ((min != null && value < min) || (max != null && value > max)) return `Nhập từ ${decimalText(min ?? 0)} đến ${decimalText(max ?? value)}.`;
  return null;
}

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type" | "value" | "onChange" | "min" | "max"> & {
  value: string;
  onChange: (text: string) => void;
  min?: number;
  max?: number;
};

/** Ô nhập điểm có dấu phẩy thập phân; báo lỗi qua kiểm tra của form. */
export function DecimalInput({ value, onChange, min, max, className, ...rest }: Props) {
  const ref = useRef<HTMLInputElement>(null);
  const error = decimalError(value, min, max);
  useEffect(() => {
    ref.current?.setCustomValidity(error ?? "");
  }, [error]);
  return (
    <input
      {...rest}
      ref={ref}
      type="text"
      inputMode="decimal"
      autoComplete="off"
      className={`${className ?? ""} ${error ? "is-invalid" : ""}`.trim()}
      title={error ?? rest.title}
      value={value}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}
