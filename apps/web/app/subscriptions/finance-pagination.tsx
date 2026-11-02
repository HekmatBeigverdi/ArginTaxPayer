"use client";

const number=(n:number)=>new Intl.NumberFormat("fa-IR").format(n);
export default function FinancePagination({page,totalCount,loading,onPageChange}:{
  page:number;totalCount:number;loading:boolean;onPageChange:(page:number)=>void;
}) {
  const pages=Math.max(1,Math.ceil(totalCount/10));
  return <nav className="phase12-sales-pagination" aria-label="صفحه‌بندی جدول">
    <span>{number(totalCount)} ردیف | صفحهٔ {number(page)} از {number(pages)}</span>
    <button type="button" disabled={loading||page<=1} onClick={()=>onPageChange(page-1)}>صفحه قبل</button>
    <button type="button" disabled={loading||page>=pages} onClick={()=>onPageChange(page+1)}>صفحه بعد</button>
  </nav>;
}
