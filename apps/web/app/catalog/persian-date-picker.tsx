"use client";

// Adapted from ArginAccounting shared PersianDatePicker; ISO date is the external contract.
import { useEffect, useMemo, useRef, useState } from "react";

const persianParts = new Intl.DateTimeFormat("fa-IR-u-ca-persian-nu-latn", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  timeZone: "UTC"
});

const monthNames = [
  "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
  "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
] as const;
const weekDays = ["ش", "ی", "د", "س", "چ", "پ", "ج"] as const;

interface PersianDateParts {
  year: number;
  month: number;
  day: number;
}

function normalizeDigits(value: string): string {
  const persian = "۰۱۲۳۴۵۶۷۸۹";
  const arabic = "٠١٢٣٤٥٦٧٨٩";
  return value.replace(/[۰-۹٠-٩]/g, (digit) => {
    const p = persian.indexOf(digit);
    if (p >= 0) return String(p);
    return String(arabic.indexOf(digit));
  });
}

function toPersianDigits(value: string): string {
  const digits = "۰۱۲۳۴۵۶۷۸۹";
  return value.replace(/\d/g, (digit) => digits[Number(digit)] ?? digit);
}

export function gregorianIsoToPersian(value: string): PersianDateParts | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const date = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(date.getTime())) return null;
  const parts = persianParts.formatToParts(date);
  const read = (type: "year" | "month" | "day") =>
    Number(parts.find((part) => part.type === type)?.value ?? "0");
  return { year: read("year"), month: read("month"), day: read("day") };
}

function samePersianDate(date: Date, target: PersianDateParts): boolean {
  const parts = gregorianIsoToPersian(date.toISOString().slice(0, 10));
  return Boolean(parts && parts.year === target.year && parts.month === target.month && parts.day === target.day);
}

export function persianToGregorianIso(target: PersianDateParts): string | null {
  if (target.year < 1200 || target.year > 1700 || target.month < 1 || target.month > 12 || target.day < 1 || target.day > 31) {
    return null;
  }
  const approximateYear = target.year + 621;
  const start = Date.UTC(approximateYear, 1, 20);
  for (let offset = 0; offset < 430; offset += 1) {
    const date = new Date(start + offset * 86400000);
    if (samePersianDate(date, target)) return date.toISOString().slice(0, 10);
  }
  return null;
}

function formatPersian(parts: PersianDateParts | null): string {
  if (!parts) return "";
  return toPersianDigits(`${parts.year}/${String(parts.month).padStart(2, "0")}/${String(parts.day).padStart(2, "0")}`);
}

function parsePersian(value: string): PersianDateParts | null {
  const normalized = normalizeDigits(value.trim()).replace(/-/g, "/");
  const match = /^(\d{4})\/(\d{1,2})\/(\d{1,2})$/.exec(normalized);
  if (!match) return null;
  return { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) };
}

function daysInPersianMonth(year: number, month: number): number {
  if (month <= 6) return 31;
  if (month <= 11) return 30;
  return persianToGregorianIso({ year, month, day: 30 }) ? 30 : 29;
}

interface PersianDatePickerProps {
  value: string;
  onChange(value: string): void;
  disabled?: boolean;
  id?: string;
  ariaLabel?: string;
  placeholder?: string;
}

export function PersianDatePicker({
  value,
  onChange,
  disabled = false,
  id,
  ariaLabel,
  placeholder = "مثال: ۱۴۰۵/۰۱/۰۱"
}: PersianDatePickerProps) {
  const selected = useMemo(() => gregorianIsoToPersian(value), [value]);
  const [text, setText] = useState(formatPersian(selected));
  const [open, setOpen] = useState(false);
  const [viewYear, setViewYear] = useState(selected?.year ?? 1405);
  const [viewMonth, setViewMonth] = useState(selected?.month ?? 1);
  const [invalid, setInvalid] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setText(formatPersian(selected));
    if (selected) {
      setViewYear(selected.year);
      setViewMonth(selected.month);
    }
  }, [selected]);

  useEffect(() => {
    function handlePointer(event: MouseEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handlePointer);
    return () => document.removeEventListener("mousedown", handlePointer);
  }, []);

  const firstIso = persianToGregorianIso({ year: viewYear, month: viewMonth, day: 1 });
  const firstWeekDay = firstIso ? (new Date(`${firstIso}T00:00:00.000Z`).getUTCDay() + 1) % 7 : 0;
  const dayCount = daysInPersianMonth(viewYear, viewMonth);

  function commitText() {
    const parsed = parsePersian(text);
    const iso = parsed ? persianToGregorianIso(parsed) : null;
    if (!iso) {
      setInvalid(Boolean(text.trim()));
      return;
    }
    setInvalid(false);
    onChange(iso);
    setText(formatPersian(parsed));
  }

  function selectDay(day: number) {
    const iso = persianToGregorianIso({ year: viewYear, month: viewMonth, day });
    if (!iso) return;
    onChange(iso);
    setInvalid(false);
    setOpen(false);
  }

  function moveMonth(delta: number) {
    const next = viewMonth + delta;
    if (next < 1) {
      setViewYear((year) => year - 1);
      setViewMonth(12);
    } else if (next > 12) {
      setViewYear((year) => year + 1);
      setViewMonth(1);
    } else {
      setViewMonth(next);
    }
  }

  return (
    <div className="ui-persian-date" ref={rootRef}>
      <div className="ui-persian-date__control">
        <input
          id={id}
          className="ui-input ui-persian-date__input"
          value={text}
          disabled={disabled}
          aria-label={ariaLabel}
          aria-invalid={invalid || undefined}
          placeholder={placeholder}
          inputMode="numeric"
          dir="ltr"
          onChange={(event) => setText(event.target.value)}
          onBlur={commitText}
          onKeyDown={(event) => {
            if (event.key === "Enter") commitText();
            if (event.key === "ArrowDown") setOpen(true);
          }}
        />
        <button
          type="button"
          className="ui-persian-date__trigger"
          disabled={disabled}
          aria-label="باز کردن تقویم شمسی"
          aria-expanded={open}
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => setOpen((current) => !current)}
        >
          <svg
            viewBox="0 0 24 24"
            width="17"
            height="17"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M7 2v3M17 2v3M3.5 9h17M5.5 4h13a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z" />
          </svg>
        </button>
      </div>
      {invalid ? <span className="ui-field__error">تاریخ شمسی معتبر وارد کنید.</span> : null}

      {open ? (
        <div className="ui-persian-date__popover" role="dialog" aria-label="انتخاب تاریخ شمسی">
          <div className="ui-persian-date__header">
            <button type="button" onClick={() => moveMonth(1)} aria-label="ماه بعد">‹</button>
            <strong>{monthNames[viewMonth - 1]} {toPersianDigits(String(viewYear))}</strong>
            <button type="button" onClick={() => moveMonth(-1)} aria-label="ماه قبل">›</button>
          </div>
          <div className="ui-persian-date__weekdays">
            {weekDays.map((day) => <span key={day}>{day}</span>)}
          </div>
          <div className="ui-persian-date__days">
            {Array.from({ length: firstWeekDay }, (_, index) => <span key={`blank-${index}`} />)}
            {Array.from({ length: dayCount }, (_, index) => index + 1).map((day) => {
              const active = selected?.year === viewYear && selected.month === viewMonth && selected.day === day;
              return (
                <button
                  key={day}
                  type="button"
                  className={active ? "ui-persian-date__day ui-persian-date__day--active" : "ui-persian-date__day"}
                  onClick={() => selectDay(day)}
                >
                  {toPersianDigits(String(day))}
                </button>
              );
            })}
          </div>
          <div className="ui-persian-date__footer">
            <button
              type="button"
              onClick={() => {
                const today = new Date().toISOString().slice(0, 10);
                const parts = gregorianIsoToPersian(today);
                if (parts) {
                  setViewYear(parts.year);
                  setViewMonth(parts.month);
                  onChange(today);
                  setOpen(false);
                }
              }}
            >امروز</button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
