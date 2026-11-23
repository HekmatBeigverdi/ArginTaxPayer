"use client";

import {useEffect,useState} from "react";
import SearchableSelect,{type SearchableSelectOption} from "../components/searchable-select";

export type CatalogUnitOption={id:string;code:string;title:string;isActive:boolean};

export default function CatalogUnitCombobox({value,onChange,search,disabled=false}:{
  value:string;onChange(value:string):void;search:(text:string)=>Promise<CatalogUnitOption[]>;disabled?:boolean;
}){
  const [query,setQuery]=useState("");
  const [units,setUnits]=useState<CatalogUnitOption[]>([]);
  const [loading,setLoading]=useState(false);
  const [error,setError]=useState("");
  useEffect(()=>{
    if(disabled)return;
    let current=true;
    setLoading(true);setError("");
    void search(query).then(result=>{if(current)setUnits(result);})
      .catch(()=>{if(current){setUnits([]);setError("دریافت فهرست واحدها ناموفق بود.");}})
      .finally(()=>{if(current)setLoading(false);});
    return()=>{current=false;};
  },[query,search,disabled]);
  const options:SearchableSelectOption[]=units.map(unit=>({
    value:unit.id,label:unit.title,description:unit.code,
    searchText:unit.code,disabled:!unit.isActive
  }));
  const selected=units.find(unit=>unit.id===value);
  return <SearchableSelect id="catalog-base-unit" value={value} options={options}
    onChange={onChange} onSearchChange={setQuery} disabled={disabled} required
    selectedLabel={selected?`${selected.title} (${selected.code})`:value?`واحد انتخاب‌شده · ${value}`:""}
    placeholder="جست‌وجوی نام یا کد واحد…" loading={loading} error={error}
    emptyMessage="واحدی با این نام یا کد پیدا نشد." />;
}
