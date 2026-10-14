"use client";
export default function ConsoleLoader({ label = "در حال دریافت اطلاعات…", full = false }:
  { label?: string; full?: boolean }) {
  return <div className={full ? "argin-loader argin-loader-overlay" : "argin-loader"}
    role="status" aria-live="polite" aria-busy="true">
    <span className="argin-loader-spinner" aria-hidden="true" />
    <strong>{label}</strong>
    <span className="argin-loader-subtitle">لطفاً منتظر بمانید</span>
  </div>;
}
