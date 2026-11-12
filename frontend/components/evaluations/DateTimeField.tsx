"use client";

import React, { useEffect, useRef, useState } from "react";
import { DateField } from "./DateField";

interface Props {
  id?: string;
  /** yyyy-MM-ddTHH:mm (như giá trị ô datetime-local); "" = chưa nhập. */
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  /** Viền đỏ do lỗi của form cha. */
  invalid?: boolean;
  "aria-label"?: string;
}

/** "HH:mm" hợp lệ (00:00–23:59) hoặc "". */
function toTime(text: string): string {
  const m = /^(\d{2}):(\d{2})$/.exec(text);
  return m && Number(m[1]) < 24 && Number(m[2]) < 60 ? text : "";
}

/** Tự chèn dấu ":" khi gõ số liền. */
function mask(raw: string): string {
  const digits = raw.replace(/\D/g, "").slice(0, 4);
  return digits.length <= 2 ? digits : `${digits.slice(0, 2)}:${digits.slice(2)}`;
}

function split(value: string): [string, string] {
  const m = /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})/.exec(value || "");
  return m ? [m[1], m[2]] : ["", ""];
}

/** Ô ngày giờ luôn hiển thị dd/mm/yyyy và giờ 24h (HH:mm), không theo ngôn ngữ trình duyệt; đủ ngày và giờ mới có giá trị. */
export function DateTimeField({ id, value, onChange, disabled, invalid, "aria-label": ariaLabel }: Props) {
  const [date, setDate] = useState(split(value)[0]);
  const [timeText, setTimeText] = useState(split(value)[1]);
  const [touched, setTouched] = useState(false);
  const emitted = useRef(value);
  const timeInput = useRef<HTMLInputElement>(null);
  const time = toTime(timeText);
  const timeError = timeText !== "" && time === "" ? "Giờ theo dạng HH:mm (24 giờ)." : date && !timeText ? "Hãy nhập giờ." : "";

  // Giá trị đổi từ ngoài (đặt lại form) thì hiện lại; giá trị do chính ô phát ra thì giữ phần đang gõ dở.
  useEffect(() => {
    if (value === emitted.current) return;
    emitted.current = value;
    const [d, t] = split(value);
    setDate(d);
    setTimeText(t);
  }, [value]);

  useEffect(() => {
    timeInput.current?.setCustomValidity(timeError);
  }, [timeError]);

  const emit = (nextDate: string, nextTime: string) => {
    const next = nextDate && nextTime ? `${nextDate}T${nextTime}` : "";
    emitted.current = next;
    if (next !== value) onChange(next);
  };

  return (
    <div className="d-flex gap-2 align-items-start" role="group" aria-label={ariaLabel}>
      <div className="flex-grow-1">
        <DateField
          id={id}
          value={date}
          disabled={disabled}
          invalid={invalid}
          aria-label={ariaLabel ? `${ariaLabel} – ngày` : "Ngày"}
          onChange={(d) => {
            setDate(d);
            emit(d, time);
          }}
        />
      </div>
      <div style={{ width: 84 }}>
        <input
          ref={timeInput}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          placeholder="HH:mm"
          maxLength={5}
          className={`form-control form-control-sm num ${(touched && timeError) || invalid ? "is-invalid" : ""}`}
          value={timeText}
          disabled={disabled}
          aria-label={ariaLabel ? `${ariaLabel} – giờ` : "Giờ"}
          title={touched && timeError ? timeError : undefined}
          onChange={(e) => {
            const raw = e.target.value;
            const next = raw.includes(":") && raw.length < timeText.length ? raw : mask(raw);
            setTimeText(next);
            emit(date, toTime(next));
          }}
          onBlur={() => setTouched(true)}
        />
      </div>
    </div>
  );
}

export default DateTimeField;
