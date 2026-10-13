"use client";

import { useCallback, useEffect, useState } from "react";
import CustomerOwnershipConsole from "../account/customer-ownership-console";

type Account = {
  customerUserId: string; rootLevel1UserId: string | null; partnerLevel2UserId: string | null;
  acquisitionChannel: number; revision: number; mobile: string | null; email: string | null;
  createdAtUtc: string; isActive: boolean; taxpayerCount: number;
};
type Taxpayer = {
  id: string; kind: number; normalizedIdentifier: string; displayName: string;
  status: number; revision: number;
};
function fail(status: number) {
  return status === 401 ? "نشست معتبر نیست؛ دوباره وارد شوید." :
    status === 403 ? "دسترسی شما به این اطلاعات مجاز نیست." :
    status === 404 ? "حساب انتخاب‌شده یافت نشد یا دیگر در محدوده شما نیست." :
    status === 503 ? "ارتباط با سرویس برقرار نشد." :
    "دریافت اطلاعات ناموفق بود؛ وضعیت سرویس را بررسی کنید.";
}
async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { cache: "no-store", credentials: "same-origin" });
  if (!response.ok) throw new Error(fail(response.status));
  return await response.json() as T;
}
const source: Record<number,string> = {
  0: "قدیمی؛ نیازمند تعیین مالکیت", 1: "ثبت‌نام عمومی", 2: "ثبت توسط ادمین سطح ۱",
  3: "ثبت توسط ادمین سطح ۲", 4: "معرفی تأییدشده"
};

export default function UsersWorkspace({ level1 }: { level1: boolean }) {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(true);
  const [detailsBusy, setDetailsBusy] = useState(false);
  const [error, setError] = useState("");
  const [detailsError, setDetailsError] = useState("");
  const [showOwnership, setShowOwnership] = useState(false);

  const reload = useCallback(async () => {
    setBusy(true); setError("");
    try {
      const records = await getJson<Account[]>("/api/customer-owners/accounts");
      setAccounts(records);
      setSelected(current => records.some(a => a.customerUserId === current) ? current : null);
    } catch (e) { setAccounts([]); setSelected(null); setError((e as Error).message); }
    finally { setBusy(false); }
  }, []);
  useEffect(() => {
    void reload();
    const onFocus = () => void reload();
    window.addEventListener("focus", onFocus);
    return () => window.removeEventListener("focus", onFocus);
  }, [reload]);

  const openAccount = useCallback(async (id: string) => {
    setSelected(id); setTaxpayers([]); setDetailsError(""); setDetailsBusy(true);
    try {
      setTaxpayers(await getJson<Taxpayer[]>("/api/customer-owners/" + id + "/taxpayers"));
    } catch (e) { setDetailsError((e as Error).message); setSelected(null); }
    finally { setDetailsBusy(false); }
  }, []);

  useEffect(() => {
    if (selected && !busy) void openAccount(selected);
  }, [accounts, busy, selected, openAccount]);

  const chosen = accounts.find(a => a.customerUserId === selected);
  const visible = accounts.filter(a =>
    [a.mobile || "", a.email || "", a.customerUserId].join(" ")
      .toLocaleLowerCase("fa").includes(search.trim().toLocaleLowerCase("fa")));

  return <div className="taxpayer-workspace">
    <aside className="taxpayer-panel">
      <div className="taxpayer-section-heading">
        <h2>{level1 ? "فهرست کاربران نرم‌افزار" : "مشتریان زیرمجموعه من"}</h2>
        <button type="button" className="identity-secondary" disabled={busy}
          onClick={() => void reload()}>بازخوانی</button>
      </div>
      <label htmlFor="user-list-search">جست‌وجوی کاربر</label>
      <input id="user-list-search" type="search" value={search}
        onChange={e => setSearch(e.target.value)} placeholder="شماره همراه، ایمیل یا شناسه حساب"/>
      {error && <p role="alert" className="form-error">{error}</p>}
      {busy ? <p role="status">در حال دریافت کاربران…</p> :
        visible.length === 0 ? <p className="identity-muted">کاربری در محدوده مجاز شما یافت نشد.</p> :
        <ul className="taxpayer-list">{visible.map(a => <li key={a.customerUserId}>
          <button type="button" aria-pressed={selected === a.customerUserId}
            onClick={() => void openAccount(a.customerUserId)}>
            <strong dir={a.mobile ? "ltr" : "auto"}>{a.mobile || a.email || "کاربر بدون شماره همراه"}</strong>
            {a.mobile && a.email && <span dir="ltr">{a.email}</span>}
            <small dir="ltr">{a.customerUserId}</small>
            <span>{a.taxpayerCount} مؤدی ثبت‌شده · {a.isActive ? "حساب فعال" : "حساب غیرفعال"}{!a.rootLevel1UserId ? " · مالکیت نامشخص" : ""}</span>
          </button>
        </li>)}</ul>}
      <p className="field-hint">این فهرست شامل حساب‌های دارای مالکیت تجاری ثبت‌شده است. تطبیق حساب‌های قدیمیِ فاقد مالکیت باید جداگانه انجام شود.</p>
    </aside>
    <section className="taxpayer-main" aria-label="پرونده کاربر">
      {!chosen && <div className="taxpayer-panel">
        <h2>پرونده کاربر</h2><p className="identity-muted">یک کاربر را از فهرست انتخاب کنید.</p>
      </div>}
      {chosen && <article className="taxpayer-panel">
        <div className="taxpayer-section-heading">
          <h2>مشخصات حساب کاربر</h2>
          <button type="button" className="identity-secondary"
            onClick={() => void openAccount(chosen.customerUserId)}>بازخوانی پرونده</button>
        </div>
        <dl className="taxpayer-facts">
          <div><dt>شماره همراه</dt><dd dir="ltr">{chosen.mobile || "ثبت نشده"}</dd></div>
          <div><dt>ایمیل</dt><dd dir="ltr">{chosen.email || "ثبت نشده"}</dd></div>
          <div><dt>شناسه حساب</dt><dd dir="ltr">{chosen.customerUserId}</dd></div>
          <div><dt>وضعیت</dt><dd>{chosen.isActive ? "فعال" : "غیرفعال"}</dd></div>
          <div><dt>شیوه جذب</dt><dd>{source[chosen.acquisitionChannel] || "نامشخص"}</dd></div>
          <div><dt>مالک تجاری</dt><dd>{!chosen.rootLevel1UserId ? "نیازمند تعیین مالکیت" : chosen.partnerLevel2UserId ? "شریک سطح ۲" : "مستقیم سطح ۱"}</dd></div>
          <div><dt>تعداد مؤدیان ثبت‌شده</dt><dd>{chosen.taxpayerCount}</dd></div>
          <div><dt>تاریخ ثبت‌نام</dt><dd>{new Date(chosen.createdAtUtc).toLocaleDateString("fa-IR")}</dd></div>
        </dl>
        <h3>مؤدیان متعلق به این کاربر</h3>
        {detailsError && <p role="alert" className="form-error">{detailsError}</p>}
        {detailsBusy ? <p role="status">در حال دریافت مؤدیان کاربر…</p> :
          taxpayers.length === 0 ? <p className="identity-muted">برای این حساب هنوز مؤدی متصل ثبت نشده است.</p> :
          <ul className="taxpayer-memory-list">{taxpayers.map(t => <li key={t.id}>
            <div>
              <strong>{t.displayName}</strong>
              <span>{t.kind === 1 ? "شخص حقیقی" : "شخص حقوقی"}</span>
              <small dir="ltr">{t.normalizedIdentifier}</small>
            </div>
            {level1 && <span>جزئیات حافظه مالیاتی در ماژول اختصاصی مؤدی</span>}
          </li>)}</ul>}
        <p className="taxpayer-warning">محدودیت یک یا چند مؤدی باید از پلن فعال و قرارداد تخصیص در Backend تعیین شود. تا تکمیل کنترل پلن، این صفحه مجوز اضافه‌کردن مؤدی فراتر از سهمیه صادر نمی‌کند.</p>
      </article>}
      {level1 && <div className="taxpayer-panel">
        <div className="taxpayer-section-heading"><h2>مالکیت و انتساب کاربران</h2>
          <button type="button" className="identity-secondary"
            onClick={() => setShowOwnership(v => !v)}>{showOwnership ? "بستن پنل" : "مدیریت انتساب"}</button>
        </div>
        <p className="identity-muted">انتساب تجاری روی حساب کاربر انجام می‌شود، نه روی تک‌تک مؤدیان.</p>
        {showOwnership && <CustomerOwnershipConsole level1={true}/>}
      </div>}
    </section>
  </div>;
}
