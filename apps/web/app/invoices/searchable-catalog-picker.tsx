"use client";

import { useState } from "react";

export type InvoiceCatalogChoice = { id: string; title: string; internalCode?: string };

export default function SearchableCatalogPicker({
  items, value, onQueryChange, onChoose, loading
}: {
  items: InvoiceCatalogChoice[]; value: string; onQueryChange(value: string): void;
  onChoose(item: InvoiceCatalogChoice): void; loading: boolean;
}) {
  const [open,setOpen] = useState(false);
  const [active,setActive] = useState(0);
  const selected = items[active];
  return <div className="invoice-register__catalog-combo">
    <input type="search" role="combobox" id="invoice-catalog-search"
      autoComplete="off" aria-autocomplete="list"
      aria-expanded={open} aria-controls="invoice-catalog-options"
      aria-activedescendant={open && selected ? "catalog-choice-"+selected.id : undefined}
      placeholder="جست‌وجوی کالا یا خدمت با نام یا کد…"
      value={value}
      onFocus={()=>setOpen(true)}
      onChange={e=>{onQueryChange(e.target.value);setOpen(true);setActive(0);}}
      onKeyDown={event=>{
        if(event.key==="Escape") setOpen(false);
        if(event.key==="ArrowDown"){event.preventDefault();setOpen(true);
          setActive(i=>Math.min(items.length-1,i+1));}
        if(event.key==="ArrowUp"){event.preventDefault();
          setActive(i=>Math.max(0,i-1));}
        if(event.key==="Enter" && open && selected){
          event.preventDefault();onChoose(selected);onQueryChange("");setOpen(false);
        }
      }}
    />
    {open && <div className="invoice-register__catalog-options" role="listbox"
      id="invoice-catalog-options" aria-label="نتایج کالا و خدمات">
      {loading && <p>در حال جست‌وجو…</p>}
      {!loading && items.length===0 && <p>موردی یافت نشد؛ عبارت جست‌وجو را تغییر دهید.</p>}
      {items.slice(0,20).map((item,i)=><button type="button" role="option"
        aria-selected={i===active} id={"catalog-choice-"+item.id}
        className={i===active?"active":""} key={item.id}
        onMouseDown={e=>e.preventDefault()}
        onClick={()=>{onChoose(item);onQueryChange("");setOpen(false);}}>
        <span>{item.title}</span><small dir="ltr">{item.internalCode || ""}</small>
      </button>)}
    </div>}
  </div>;
}
