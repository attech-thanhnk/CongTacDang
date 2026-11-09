"use client";

import React, { useEffect, useMemo, useState } from "react";
import { Holiday, holidayService } from "@/services/holidayService";
import { lunarLabel, toLunar } from "@/lib/lunarCalendar";
import styles from "./LunarMonthPicker.module.css";

const WEEK_HEADERS = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

const pad = (value: number) => String(value).padStart(2, "0");
const isoOf = (year: number, month: number, day: number) => `${year}-${pad(month)}-${pad(day)}`;

function todayIso(): string {
  const now = new Date();
  return isoOf(now.getFullYear(), now.getMonth() + 1, now.getDate());
}

interface Props {
  /** Ngày đầu đang chọn, "yyyy-MM-dd" hoặc rỗng. */
  from: string;
  /** Ngày cuối khi nghỉ nhiều ngày. */
  to?: string;
  /** Tháng mở sẵn khi chưa chọn ngày. */
  initialYear: number;
  onPick: (iso: string) => void;
}

/** Tờ lịch tháng có cả ngày dương và âm để chọn ngày nghỉ. */
export function LunarMonthPicker({ from, to, initialYear, onPick }: Props) {
  const today = todayIso();
  const [view, setView] = useState(() => {
    if (from) return { year: Number(from.slice(0, 4)), month: Number(from.slice(5, 7)) };
    return initialYear === Number(today.slice(0, 4))
      ? { year: initialYear, month: Number(today.slice(5, 7)) }
      : { year: initialYear, month: 1 };
  });
  const [holidays, setHolidays] = useState<Holiday[]>([]);

  useEffect(() => {
    let active = true;
    holidayService.listByYear(view.year).then((items) => active && setHolidays(items)).catch(() => active && setHolidays([]));
    return () => {
      active = false;
    };
  }, [view.year]);

  const holidayNames = useMemo(() => new Map(holidays.map((h) => [h.date.slice(0, 10), h.name])), [holidays]);

  const cells = useMemo(() => {
    const blanks = (new Date(Date.UTC(view.year, view.month - 1, 1)).getUTCDay() + 6) % 7;
    const days = new Date(Date.UTC(view.year, view.month, 0)).getUTCDate();
    // Luôn đủ 6 tuần để khung lịch không đổi kích thước khi chuyển tháng.
    return [
      ...Array.from({ length: blanks }, () => null),
      ...Array.from({ length: days }, (_, i) => {
        const iso = isoOf(view.year, view.month, i + 1);
        return { iso, day: i + 1, lunar: toLunar(iso), column: (blanks + i) % 7 };
      }),
      ...Array.from({ length: 42 - blanks - days }, () => null),
    ];
  }, [view]);

  const shift = (delta: number) =>
    setView((current) => {
      const index = current.year * 12 + current.month - 1 + delta;
      return { year: Math.floor(index / 12), month: (index % 12) + 1 };
    });

  const years = Array.from({ length: 9 }, (_, i) => view.year - 3 + i);
  const end = to || from;

  return (
    <div className={styles.picker}>
      <div className={styles.head}>
        <button type="button" className="btn btn-sm btn-outline-secondary" aria-label="Tháng trước" onClick={() => shift(-1)}>
          <i className="fa-solid fa-chevron-left" aria-hidden="true" />
        </button>
        <select className="form-select form-select-sm" aria-label="Tháng" value={view.month}
          onChange={(e) => setView({ ...view, month: Number(e.target.value) })}>
          {Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>Tháng {i + 1}</option>)}
        </select>
        <select className="form-select form-select-sm" aria-label="Năm" value={view.year}
          onChange={(e) => setView({ ...view, year: Number(e.target.value) })}>
          {years.map((y) => <option key={y} value={y}>{y}</option>)}
        </select>
        <button type="button" className="btn btn-sm btn-outline-secondary" aria-label="Tháng sau" onClick={() => shift(1)}>
          <i className="fa-solid fa-chevron-right" aria-hidden="true" />
        </button>
      </div>

      <div className={styles.grid}>
        {WEEK_HEADERS.map((h, i) => <div key={h} className={`${styles.week} ${i > 4 ? styles.weekend : ""}`}>{h}</div>)}
        {cells.map((cell, index) => {
          if (!cell) return <div key={`blank-${index}`} />;
          const holiday = holidayNames.get(cell.iso);
          const isEdge = cell.iso === from || cell.iso === to;
          const inRange = !!from && cell.iso > from && cell.iso < end;
          const lunarText = lunarLabel(cell.lunar);
          const classes = [
            styles.day,
            cell.column > 4 ? styles.weekend : "",
            holiday ? styles.holiday : "",
            inRange ? styles.range : "",
            isEdge ? styles.selected : "",
            cell.iso === today ? styles.today : "",
          ].filter(Boolean).join(" ");
          return (
            <button key={cell.iso} type="button" className={classes} aria-pressed={isEdge || inRange} title={holiday}
              aria-label={`Ngày ${pad(cell.day)}/${pad(view.month)}/${view.year}, âm lịch ${lunarText}${holiday ? `, ${holiday}` : ""}`}
              onClick={() => onPick(cell.iso)}>
              <span className={`${styles.solar} num`}>{cell.day}</span>
              <span className={`${styles.lunar} ${cell.lunar.day === 1 ? styles.lunarFirst : ""} num`}>{lunarText}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

export default LunarMonthPicker;
