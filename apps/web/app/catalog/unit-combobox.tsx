"use client";

import { useEffect, useRef, useState } from "react";

export type CatalogUnitOption = {
  id: string; code: string; title: string; isActive: boolean;
};

export default function CatalogUnitCombobox({
  value, onChange, search, disabled = false,
}: {
  value: string;
  onChange: (value: string) => void;
  search: (text: string) => Promise<CatalogUnitOption[]>;
  disabled?: boolean;
}) {
  const [query, setQuery] = useState("");
  const [options, setOptions] = useState<CatalogUnitOption[]>([]);
  const [expanded, setExpanded] = useState(false);
  const [active, setActive] = useState(0);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const seq = useRef(0);
  const selected = options.find(u => u.id === value);
  useEffect(() => {
    if (disabled) return;
    let alive = true;
    const request = ++seq.current;
    const timer = setTimeout(() => {
      setBusy(true);
      void search(query).then(items => {
        if (!alive || request !== seq.current) return;
        setOptions(items);
        setActive(0);
        setFailure(false);
      }).catch(() => {
        if (alive && request === seq.current) { setOptions([]); setFailure(true); }
      }).finally(() => {
        if (alive && request === seq.current) setBusy(false);
      });
    }, 250);
    return () => { alive = false; clearTimeout(timer); };
  }, [query, disabled, search]);

  useEffect(() => {
    function outside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setExpanded(false);
    }
    document.addEventListener("mousedown", outside);
    return () => document.removeEventListener("mousedown", outside);
  }, []);

  function choose(item: CatalogUnitOption) {
    onChange(item.id);
    setQuery(item.title + " (" + item.code + ")");
    setExpanded(false);
  }
  return <div ref={ref} className="catalog-unit-combo">
    <input role="combobox" type="text" autoComplete="off" aria-label="جست‌وجوی واحد اندازه‌گیری"
      aria-expanded={expanded} aria-autocomplete="list" aria-controls="catalog-unit-options"
      value={expanded ? query : selected ? selected.title + " (" + selected.code + ")" : query}
      disabled={disabled} placeholder="نام یا کد واحد را بنویسید…"
      onFocus={() => setExpanded(true)}
      onChange={e => { setQuery(e.target.value); setExpanded(true); onChange(""); }}
      onKeyDown={e => {
        if (e.key === "Escape") { e.stopPropagation(); setExpanded(false); }
        if (e.key === "ArrowDown") { e.preventDefault(); setExpanded(true); setActive(a => Math.min(a + 1, options.length - 1)); }
        if (e.key === "ArrowUp") { e.preventDefault(); setActive(a => Math.max(0, a - 1)); }
        if (e.key === "Enter" && expanded && options[active]) {
          e.preventDefault(); choose(options[active]);
        }
      }} />
    {expanded && <div id="catalog-unit-options" role="listbox" aria-label="نتایج جست‌وجوی واحد" className="catalog-unit-options">
      {busy && <div role="status">در حال جست‌وجو…</div>}
      {failure && <div role="alert">دریافت فهرست واحدها ناموفق بود.</div>}
      {!busy && !failure && options.length === 0 && <div>واحدی پیدا نشد.</div>}
      {!busy && options.map((unit, i) =>
        <button key={unit.id} type="button" role="option" aria-selected={unit.id === value}
          className={i === active ? "is-active" : ""}
          onMouseDown={e => e.preventDefault()} onClick={() => choose(unit)}>
          <span>{unit.title} <bdi dir="ltr">{unit.code}</bdi></span>
          {!unit.isActive && <small>مرجع پیش‌نویس</small>}
        </button>)}
    </div>}
    {value && !selected && <small className="catalog-selected-unit">شناسه واحد انتخاب‌شده: <bdi dir="ltr">{value}</bdi></small>}
    <input type="hidden" value={value} required aria-hidden="true" />
  </div>;
}
