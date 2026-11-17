"use client";

import { ReactNode, useState } from "react";
import { PersianDatePicker } from "./catalog/persian-date-picker";

import { backupFilePage } from "./backup-file-page";

export default function BackupFileList<T>({ items, dateOf, render }: { items: T[]; dateOf: (item: T) => string; render: (item: T) => ReactNode }) {
 const [from, setFrom] = useState("");
 const [to, setTo] = useState("");
 const [page, setPage] = useState(1);
 const result = backupFilePage(items, dateOf, from, to, page);
 return <>
  <div className="backup-file-filters">
   <label>از تاریخ<PersianDatePicker value={from} onChange={value => { setFrom(value); setPage(1); }} ariaLabel="از تاریخ بکاپ" /></label>
   <label>تا تاریخ<PersianDatePicker value={to} onChange={value => { setTo(value); setPage(1); }} ariaLabel="تا تاریخ بکاپ" /></label>
   <button type="button" disabled={!from && !to} onClick={() => { setFrom(""); setTo(""); setPage(1); }}>حذف فیلتر تاریخ</button>
  </div>
  {from && to && from > to ? <p role="alert">تاریخ پایان باید برابر یا بعد از تاریخ شروع باشد.</p> : <>
   <ul className="backup-artifact-list">{result.items.map(render)}</ul>
   {result.total === 0 && <p role="status">فایلی در این بازه وجود ندارد.</p>}
   <nav className="users-pagination" aria-label="صفحه‌بندی فایل‌های بکاپ">
    <span>نمایش {(result.total ? (result.current - 1) * 15 + 1 : 0).toLocaleString("fa-IR")} تا {Math.min(result.current * 15, result.total).toLocaleString("fa-IR")} از {result.total.toLocaleString("fa-IR")} فایل</span>
    <div>
     <button type="button" disabled={result.current === 1} onClick={() => setPage(result.current - 1)}>قبلی</button>
     <span>صفحه {result.current.toLocaleString("fa-IR")} از {result.pages.toLocaleString("fa-IR")}</span>
     <button type="button" disabled={result.current === result.pages} onClick={() => setPage(result.current + 1)}>بعدی</button>
    </div>
   </nav>
  </>}
 </>;
}
