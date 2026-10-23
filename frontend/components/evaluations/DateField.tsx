"use client";

import React, { useEffect, useRef, useState } from "react";
import styles from "./record.module.css";

interface Props {
  id?: string;
  /** yyyy-MM-dd; "" = chưa nhập. */
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  disabled?: boolean;
  "aria-label"?: string;
}

const pad = (n: number) => String(n).padStart(2, "0");

/** yyyy-MM-dd → dd/MM/yyyy. */
function toDisplay(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso || "");
  return m ? `${m[3]}/${m[2]}/${m[1]}` : "";
}

/** dd/MM/yyyy → yyyy-MM-dd; "" khi chưa đủ hoặc không phải ngày có thật. */
function toIso(text: string): string {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text);
  if (!m) return "";
  const [d, mo, y] = [Number(m[1]), Number(m[2]), Number(m[3])];
  const date = new Date(y, mo - 1, d);
  if (date.getFullYear() !== y || date.getMonth() !== mo - 1 || date.getDate() !== d) return "";
  return `${y}-${pad(mo)}-${pad(d)}`;
}

/** Tự chèn dấu "/" khi gõ số liền. */
function mask(raw: string): string {
  const digits = raw.replace(/\D/g, "").slice(0, 8);
  if (digits.length <= 2) return digits;
  if (digits.length <= 4) return `${digits.slice(0, 2)}/${digits.slice(2)}`;
  return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
}

/** Ô ngày luôn hiển thị dd/MM/yyyy (ô date gốc theo ngôn ngữ trình duyệt có thể ra mm/dd/yyyy); nút lịch mở bộ chọn ngày của trình duyệt. */
export function DateField({ id, value, onChange, required, disabled, "aria-label": ariaLabel }: Props) {
  const [text, setText] = useState(toDisplay(value));
  const [touched, setTouched] = useState(false);
  const picker = useRef<HTMLInputElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const invalid = text !== "" && toIso(text) === "";

  // Giá trị đổi từ ngoài (chọn trên lịch, đặt lại form) thì hiện lại; đang gõ dở thì giữ nguyên.
  useEffect(() => {
    setText((current) => (toIso(current) === value ? current : toDisplay(value)));
  }, [value]);

  useEffect(() => {
    input.current?.setCustomValidity(invalid ? "Ngày theo dạng dd/mm/yyyy." : "");
  }, [invalid]);

  const openPicker = () => {
    const el = picker.current;
    if (!el) return;
    try {
      el.showPicker();
    } catch {
      el.focus();
    }
  };

  return (
    <div className="position-relative">
      <div className={`input-group input-group-sm has-validation ${styles.dateGroup}`}>
        <input
          ref={input}
          id={id}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          placeholder="dd/mm/yyyy"
          maxLength={10}
          className={`form-control form-control-sm ${touched && invalid ? "is-invalid" : ""}`}
          value={text}
          required={required}
          disabled={disabled}
          aria-label={ariaLabel}
          aria-invalid={touched && invalid ? true : undefined}
          onChange={(e) => {
            const next = e.target.value.includes("/") && e.target.value.length < text.length ? e.target.value : mask(e.target.value);
            setText(next);
            onChange(toIso(next));
          }}
          onBlur={() => setTouched(true)}
        />
        <button type="button" className="btn btn-outline-secondary" disabled={disabled} onClick={openPicker} aria-label="Chọn ngày" title="Chọn ngày">
          <i className="bi bi-calendar3" aria-hidden="true" />
        </button>
      {touched && invalid && <div className="invalid-feedback">Ngày theo dạng dd/mm/yyyy.</div>}
      </div>
      {/* Ô date ẩn chỉ để mở bộ chọn ngày gốc. */}
      <input
        ref={picker}
        type="date"
        tabIndex={-1}
        aria-hidden="true"
        className="position-absolute bottom-0 end-0 opacity-0 border-0 p-0"
        style={{ width: 1, height: 1, pointerEvents: "none" }}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  );
}

export default DateField;
