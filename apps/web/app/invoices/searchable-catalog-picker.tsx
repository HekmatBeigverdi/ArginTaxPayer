"use client";

import SearchableSelect,{type SearchableSelectOption} from "../components/searchable-select";

export type InvoiceCatalogChoice={id:string;title:string;internalCode?:string;vatRatePercent?:number};

export default function SearchableCatalogPicker({items,onQueryChange,onChoose,loading}:{
  items:InvoiceCatalogChoice[];onQueryChange(value:string):void;
  onChoose(item:InvoiceCatalogChoice):void;loading:boolean;
}){
  const options:SearchableSelectOption[]=items.map(item=>({
    value:item.id,label:item.title,
    description:`${item.internalCode||"بدون کد داخلی"} · ارزش افزوده ${item.vatRatePercent??0}٪`,
    searchText:item.internalCode
  }));
  return <SearchableSelect id="invoice-catalog-search" value="" options={options}
    onChange={id=>{const chosen=items.find(item=>item.id===id);if(chosen){onChoose(chosen);onQueryChange("");}}}
    onSearchChange={onQueryChange} placeholder="جست‌وجوی کالا یا خدمت با نام یا کد…"
    loading={loading} emptyMessage="کالا یا خدمتی یافت نشد؛ عبارت جست‌وجو را تغییر دهید." />;
}
