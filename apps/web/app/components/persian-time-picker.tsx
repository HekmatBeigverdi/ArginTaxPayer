"use client";

import { useMemo } from "react";

/**
 * Shared RTL Persian 24-hour time control.
 * Value contract: ASCII HH:mm; display: Persian digits. No AM/PM native picker.
 * Time-zone interpretation belongs to the consuming form.
 */
const digits = "۰۱۲۳۴۵۶۷۸۹";
const fa = (value: string) => value.replace(/\d/g, d => digits[Number(d)]);
export function PersianTimePicker({ id, value, onChange, disabled = false,
  minuteStep = 1, timeZoneLabel = "UTC" }: {
  id: string; value: string; onChange(value: string): void;
  disabled?: boolean; minuteStep?: number; timeZoneLabel?: string;
}) {
  const valid = /^([01]\d|2[0-3]):[0-5]\d$/.test(value);
  const [hour, minute] = valid ? value.split(":").map(Number) : [12, 0];
  const minutes = useMemo(() => {
    const step = Number.isInteger(minuteStep) && minuteStep >= 1 && minuteStep <= 30
      ? minuteStep : 1;
    const available = Array.from({ length: Math.ceil(60 / step) },
      (_, i) => Math.min(59, i * step));
    if (!available.includes(minute)) available.push(minute);
    return available.sort((a,b) => a-b);
  }, [minuteStep, minute]);
  const pad = (n: number) => String(n).padStart(2, "0");
  return <div className="persian-time-picker" role="group"
      aria-label={"انتخاب ساعت به قالب ۲۴ ساعته — " + timeZoneLabel}>
    <select id={id} aria-label="ساعت" disabled={disabled} value={hour}
      onChange={e => onChange(pad(Number(e.target.value)) + ":" + pad(minute))}>
      {Array.from({length:24}, (_,i) => <option value={i} key={i}>{fa(pad(i))}</option>)}
    </select>
    <span aria-hidden="true">:</span>
    <select aria-label="دقیقه" disabled={disabled} value={minute}
      onChange={e => onChange(pad(hour) + ":" + pad(Number(e.target.value)))}>
      {minutes.map(i => <option value={i} key={i}>{fa(pad(i))}</option>)}
    </select>
    <small>{timeZoneLabel}</small>
  </div>;
}
