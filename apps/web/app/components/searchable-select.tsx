"use client";

import {useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState} from "react";
import {createPortal} from "react-dom";

export type SearchableSelectOption = {
  value: string;
  label: string;
  description?: string;
  searchText?: string;
  disabled?: boolean;
};

type SearchableSelectProps = {
  id: string;
  value: string;
  options: SearchableSelectOption[];
  onChange(value: string): void;
  placeholder?: string;
  selectedLabel?: string;
  disabled?: boolean;
  required?: boolean;
  clearable?: boolean;
  loading?: boolean;
  error?: string;
  emptyMessage?: string;
  onSearchChange?(query: string): void;
};

const normalize = (value: string) => value.trim().toLocaleLowerCase()
  .replace(/[يى]/g,"ی").replace(/ك/g,"ک")
  .replace(/[۰-۹]/g,digit=>String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit)))
  .replace(/[٠-٩]/g,digit=>String("٠١٢٣٤٥٦٧٨٩".indexOf(digit)));

export default function SearchableSelect({
  id,value,options,onChange,placeholder="جست‌وجو یا انتخاب کنید…",selectedLabel,
  disabled=false,required=false,clearable=true,loading=false,error="",
  emptyMessage="موردی با این عبارت پیدا نشد.",onSearchChange
}:SearchableSelectProps){
  const [open,setOpen]=useState(false);
  const [query,setQuery]=useState("");
  const [active,setActive]=useState(0);
  const [placement,setPlacement]=useState<{top:number;left:number;width:number;height:number;above:boolean}|null>(null);
  const root=useRef<HTMLDivElement>(null);
  const control=useRef<HTMLDivElement>(null);
  const input=useRef<HTMLInputElement>(null);
  const popup=useRef<HTMLDivElement>(null);
  const items=useMemo(()=>{
    const needle=normalize(query);
    if(!needle)return options;
    return options.filter(option=>normalize(`${option.label} ${option.description??""} ${option.searchText??""}`).includes(needle));
  },[options,query]);
  const chosen=options.find(option=>option.value===value);
  const shownLabel=selectedLabel||chosen?.label||"";
  const optionId=(option:SearchableSelectOption)=>`${id}-option-${encodeURIComponent(option.value)}`;

  const positionPopup=useCallback(()=>{
    const rect=control.current?.getBoundingClientRect();
    if(!rect)return;
    const gap=7,margin=12,maxHeight=320;
    const below=window.innerHeight-rect.bottom-margin;
    const above=rect.top-margin;
    const useAbove=below<190&&above>below;
    const height=Math.max(100,Math.min(maxHeight,(useAbove?above:below)-gap));
    setPlacement({top:useAbove?Math.max(margin,rect.top-gap-height):rect.bottom+gap,
      left:Math.max(margin,Math.min(rect.left,window.innerWidth-rect.width-margin)),
      width:Math.min(rect.width,window.innerWidth-2*margin),height,above:useAbove});
  },[]);

  const openMenu=()=>{
    if(disabled)return;
    setQuery("");setActive(0);setOpen(true);
  };
  const closeMenu=()=>{setOpen(false);setQuery("");setPlacement(null);};
  const choose=(option:SearchableSelectOption)=>{
    if(option.disabled)return;
    closeMenu();onChange(option.value);
  };

  useEffect(()=>{
    if(input.current)input.current.setCustomValidity(required&&!value?"لطفاً یک گزینه را از فهرست انتخاب کنید.":"");
  },[required,value]);

  useEffect(()=>{
    if(!open)return;
    positionPopup();
    const outside=(event:PointerEvent)=>{
      const target=event.target;
      if(target instanceof Node&&!root.current?.contains(target)&&!popup.current?.contains(target))closeMenu();
    };
    const escape=(event:KeyboardEvent)=>{
      if(event.key!=="Escape")return;
      event.preventDefault();event.stopPropagation();event.stopImmediatePropagation();closeMenu();
    };
    document.addEventListener("pointerdown",outside);
    document.addEventListener("keydown",escape,true);
    window.addEventListener("resize",positionPopup);
    window.addEventListener("scroll",positionPopup,true);
    const observer=typeof ResizeObserver!=="undefined"&&control.current
      ?new ResizeObserver(positionPopup):null;
    if(observer&&control.current)observer.observe(control.current);
    return()=>{
      document.removeEventListener("pointerdown",outside);
      document.removeEventListener("keydown",escape,true);
      window.removeEventListener("resize",positionPopup);
      window.removeEventListener("scroll",positionPopup,true);
      observer?.disconnect();
    };
  },[open,positionPopup]);

  useEffect(()=>{
    if(!open||!onSearchChange)return;
    const timer=window.setTimeout(()=>onSearchChange(query),170);
    return()=>window.clearTimeout(timer);
  },[open,query,onSearchChange]);

  useEffect(()=>{
    if(active>=items.length)setActive(Math.max(0,items.length-1));
  },[active,items.length]);

  useLayoutEffect(()=>{
    if(!open||!placement||!popup.current||!control.current)return;
    const rect=control.current.getBoundingClientRect();
    const gap=7,margin=12;
    const actualTop=placement.above
      ?Math.max(margin,rect.top-gap-popup.current.getBoundingClientRect().height)
      :rect.bottom+gap;
    if(Math.abs(actualTop-placement.top)>1)setPlacement(current=>current?{...current,top:actualTop}:current);
  },[open,placement,items.length,loading,error]);

  const popupContent=open&&placement&&typeof document!=="undefined"?createPortal(
    <div ref={popup} className="searchable-select__popup" style={{top:placement.top,left:placement.left,width:placement.width,maxHeight:placement.height}}
      dir="rtl" aria-label="فهرست انتخاب">
      <div className="searchable-select__popup-head">
        <span>{loading?"در حال جست‌وجو…":`${items.length.toLocaleString("fa-IR")} مورد`}</span>
        <button type="button" className="searchable-select__close" onClick={closeMenu} aria-label="بستن فهرست انتخاب" title="بستن فهرست">
          <svg viewBox="0 0 20 20" aria-hidden="true"><path d="m5 5 10 10M15 5 5 15"/></svg>
          <span>بستن فهرست</span>
        </button>
      </div>
      {error&&<p className="searchable-select__message" role="alert">{error}</p>}
      {loading?<p className="searchable-select__message" role="status">در حال دریافت گزینه‌ها…</p>:
        items.length===0?<p className="searchable-select__message">{emptyMessage}</p>:
        <div className="searchable-select__options" id={`${id}-options`} role="listbox" aria-label="نتایج جست‌وجو">
          {items.map((option,index)=><button type="button" role="option" key={option.value}
            id={optionId(option)} aria-selected={option.value===value} disabled={option.disabled}
            className={`${index===active?"is-active":""} ${option.value===value?"is-selected":""}`}
            onMouseEnter={()=>setActive(index)} onMouseDown={event=>event.preventDefault()}
            onClick={()=>choose(option)}>
            <span className="searchable-select__option-copy"><strong>{option.label}</strong>
              {option.description&&<small>{option.description}</small>}</span>
            {option.value===value&&<svg className="searchable-select__check" viewBox="0 0 20 20" aria-label="انتخاب‌شده"><path d="m4 10 4 4 8-9"/></svg>}
          </button>)}
        </div>}
    </div>,document.body):null;

  return <div className="searchable-select" ref={root}>
    <div ref={control} className={`searchable-select__control ${open?"is-open":""}`}>
      <svg className="searchable-select__search-icon" viewBox="0 0 20 20" aria-hidden="true"><circle cx="8.5" cy="8.5" r="5.5"/><path d="m13 13 4 4"/></svg>
      <input ref={input} id={id} type="text" role="combobox" autoComplete="off" aria-autocomplete="list"
        aria-expanded={open} aria-controls={`${id}-options`} aria-activedescendant={open&&items[active]?optionId(items[active]):undefined}
        aria-invalid={!!error} value={open?query:shownLabel} placeholder={placeholder} disabled={disabled}
        onFocus={openMenu} onClick={()=>{if(!open)openMenu();}}
        onChange={event=>{setQuery(event.target.value);setActive(0);setOpen(true);onChange("");}}
        onKeyDown={event=>{
          if(event.key==="Escape"&&open){event.preventDefault();event.stopPropagation();event.nativeEvent.stopImmediatePropagation();closeMenu();return;}
          if(event.key==="ArrowDown"){event.preventDefault();if(!open)openMenu();else setActive(index=>Math.min(items.length-1,index+1));}
          if(event.key==="ArrowUp"){event.preventDefault();setActive(index=>Math.max(0,index-1));}
          if(event.key==="Enter"&&open&&items[active]){event.preventDefault();choose(items[active]);}
        }}/>
      {clearable&&value&&!disabled&&<button type="button" className="searchable-select__clear" aria-label="پاک‌کردن انتخاب" title="پاک‌کردن انتخاب"
        onMouseDown={event=>event.preventDefault()} onClick={()=>{onChange("");onSearchChange?.("");setQuery("");setOpen(false);input.current?.focus();}}>
        <svg viewBox="0 0 20 20" aria-hidden="true"><path d="m6 6 8 8m0-8-8 8"/></svg>
      </button>}
      <button type="button" className="searchable-select__toggle" disabled={disabled} aria-label={open?"بستن فهرست انتخاب":"نمایش فهرست انتخاب"}
        onMouseDown={event=>event.preventDefault()} onClick={()=>open?closeMenu():openMenu()}>
        <svg viewBox="0 0 20 20" aria-hidden="true"><path d="m5 7 5 5 5-5"/></svg>
      </button>
    </div>
    {popupContent}
  </div>;
}
