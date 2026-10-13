"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import CustomerOwnershipConsole from "../account/customer-ownership-console";

type Account = {
  customerUserId: string; rootLevel1UserId: string | null; partnerLevel2UserId: string | null;
  acquisitionChannel: number; revision: number; mobile: string | null; email: string | null;
  createdAtUtc: string; isActive: boolean; taxpayerCount: number;
};
type Taxpayer = { id: string; kind: number; normalizedIdentifier: string; displayName: string;
  status: number; revision: number };
const acquisition: Record<number,string> = {
  0: "نیازمند تعیین مالکیت", 1: "ثبت‌نام مستقیم", 2: "ثبت توسط سطح ۱",
  3: "ثبت توسط سطح ۲", 4: "معرفی تأییدشده"
};
const PAGE_SIZES = [10, 20, 50];
const errorLabel = (status: number) => status === 401 ? "نشست ورود معتبر نیست." :
  status === 403 ? "دسترسی به این اطلاعات مجاز نیست." :
  status === 404 ? "کاربر یا دسترسی به او یافت نشد." : "دریافت اطلاعات ناموفق بود.";
async function load<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: "no-store", credentials: "same-origin" });
  if (!res.ok) throw new Error(errorLabel(res.status));
  return await res.json() as T;
}

export default function UsersWorkspace({ level1 }: { level1: boolean }) {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selected, setSelected] = useState<Account | null>(null);
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [search, setSearch] = useState("");
  const [ownershipFilter, setOwnershipFilter] = useState("all");
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [error, setError] = useState("");
  const [modalError, setModalError] = useState("");
  const [ownershipOpen, setOwnershipOpen] = useState(false);
  const dialogRef = useRef<HTMLDialogElement>(null);
  const requestRef = useRef(0);

  const refresh = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const rows = await load<Account[]>("/api/customer-owners/accounts");
      setAccounts(rows);
      // A revoked partner must not keep a customer's information in a previously opened dialog.
      setSelected(previous => {
        if (previous && !rows.some(x => x.customerUserId === previous.customerUserId)) {
          dialogRef.current?.close();
          return null;
        }
        return previous ? rows.find(x => x.customerUserId === previous.customerUserId) || null : null;
      });
    } catch (e) {
      setAccounts([]); setSelected(null); dialogRef.current?.close();
      setError((e as Error).message);
    } finally { setLoading(false); }
  }, []);

  useEffect(() => {
    void refresh();
    const focus = () => void refresh();
    const visibility = () => { if (document.visibilityState === "visible") void refresh(); };
    window.addEventListener("focus", focus);
    document.addEventListener("visibilitychange", visibility);
    return () => {
      window.removeEventListener("focus", focus);
      document.removeEventListener("visibilitychange", visibility);
    };
  }, [refresh]);

  const filtered = useMemo(() => accounts.filter(a => {
    const key = (a.mobile || "") + " " + (a.email || "") + " " + a.customerUserId;
    if (!key.toLocaleLowerCase("fa").includes(search.trim().toLocaleLowerCase("fa"))) return false;
    return ownershipFilter === "all" ||
      ownershipFilter === "root" && !!a.rootLevel1UserId && !a.partnerLevel2UserId ||
      ownershipFilter === "partner" && !!a.partnerLevel2UserId ||
      ownershipFilter === "unresolved" && !a.rootLevel1UserId;
  }), [accounts, search, ownershipFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const activePage = Math.min(page, totalPages);
  const pageRows = filtered.slice((activePage - 1) * pageSize, activePage * pageSize);
  const firstIndex = filtered.length ? (activePage - 1) * pageSize + 1 : 0;
  const lastIndex = Math.min(activePage * pageSize, filtered.length);

  function close() {
    requestRef.current++;
    dialogRef.current?.close();
    setSelected(null); setTaxpayers([]); setModalError("");
  }

  async function open(account: Account) {
    setSelected(account); setTaxpayers([]); setModalError(""); setDetailsLoading(true);
    dialogRef.current?.showModal();
    const requestId = ++requestRef.current;
    try {
      const rows = await load<Taxpayer[]>("/api/customer-owners/" + account.customerUserId + "/taxpayers");
      if (requestId === requestRef.current) setTaxpayers(rows);
    } catch (e) {
      if (requestId === requestRef.current) {
        setModalError((e as Error).message);
        if (!level1) { close(); void refresh(); }
      }
    } finally { if (requestId === requestRef.current) setDetailsLoading(false); }
  }

  const ownerLabel = (a: Account) => !a.rootLevel1UserId ? "نیازمند بررسی" :
    a.partnerLevel2UserId ? "شریک سطح ۲" : "مستقیم سطح ۱";

  return <section className="users-panel" aria-label="فهرست کاربران نرم‌افزار">
    <div className="users-toolbar">
      <div><h2>{level1 ? "کاربران ثبت‌نام‌شده" : "مشتریان من"}</h2>
        <p>هر ردیف یک حساب کاربری است؛ مؤدیان داخل پرونده همان کاربر قرار دارند.</p></div>
      <button type="button" className="users-button" disabled={loading} onClick={() => void refresh()}>
        بازخوانی کاربران
      </button>
    </div>

    <div className="users-filters">
      <label>جست‌وجو
        <input type="search" placeholder="نام کاربری، موبایل، ایمیل یا شناسه"
          value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/>
      </label>
      {level1 && <label>مالکیت تجاری
        <select value={ownershipFilter} onChange={e => { setOwnershipFilter(e.target.value); setPage(1); }}>
          <option value="all">همه کاربران</option>
          <option value="root">مستقیم سطح ۱</option>
          <option value="partner">متعلق به شرکا</option>
          <option value="unresolved">نیازمند تعیین مالکیت</option>
        </select>
      </label>}
      <label>تعداد در صفحه
        <select value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}>
          {PAGE_SIZES.map(n => <option key={n} value={n}>{n}</option>)}
        </select>
      </label>
    </div>

    {error && <p role="alert" className="form-error">{error}</p>}
    <div className="users-table-scroll">
      <table className="users-table">
        <thead><tr>
          <th scope="col">ردیف</th><th scope="col">کاربر</th><th scope="col">وضعیت</th>
          <th scope="col">مالکیت تجاری</th><th scope="col">مؤدیان</th>
          <th scope="col">ثبت‌نام</th><th scope="col">عملیات</th>
        </tr></thead>
        <tbody>
          {loading ? <tr><td colSpan={7} role="status">در حال دریافت کاربران…</td></tr> :
            pageRows.length === 0 ? <tr><td colSpan={7}>کاربری با این معیارها پیدا نشد.</td></tr> :
            pageRows.map((a, i) => <tr key={a.customerUserId}>
              <td>{firstIndex + i}</td>
              <td><strong dir="auto">{a.mobile || a.email || "کاربر بدون اطلاعات تماس"}</strong>
                {a.mobile && a.email && <small dir="ltr">{a.email}</small>}
                <small dir="ltr" title={a.customerUserId}>{a.customerUserId.slice(0, 8)}…</small></td>
              <td><span className={a.isActive ? "users-state" : "users-state users-state-muted"}>
                {a.isActive ? "فعال" : "غیرفعال"}</span></td>
              <td><span className={!a.rootLevel1UserId ? "users-state users-state-warning" : ""}>
                {ownerLabel(a)}</span></td>
              <td>{a.taxpayerCount.toLocaleString("fa-IR")}</td>
              <td>{new Date(a.createdAtUtc).toLocaleDateString("fa-IR")}</td>
              <td><button className="users-action" type="button" onClick={() => void open(a)}>
                مشاهده پرونده
              </button></td>
            </tr>)}
        </tbody>
      </table>
    </div>

    <div className="users-pagination" aria-label="صفحه‌بندی کاربران">
      <span>نمایش {firstIndex.toLocaleString("fa-IR")} تا {lastIndex.toLocaleString("fa-IR")} از {filtered.length.toLocaleString("fa-IR")} کاربر</span>
      <div>
        <button type="button" disabled={activePage <= 1 || loading} onClick={() => setPage(p => p - 1)}>قبلی</button>
        <span>صفحه {activePage.toLocaleString("fa-IR")} از {totalPages.toLocaleString("fa-IR")}</span>
        <button type="button" disabled={activePage >= totalPages || loading} onClick={() => setPage(p => p + 1)}>بعدی</button>
      </div>
    </div>

    {level1 && <div className="users-ownership-toggle">
      <button className="users-button" type="button" onClick={() => setOwnershipOpen(v => !v)}>
        {ownershipOpen ? "بستن بخش انتساب تجاری" : "مدیریت مالکیت تجاری و انتساب کاربران به شرکا"}
      </button>
      {ownershipOpen && <CustomerOwnershipConsole level1={true}/>}
    </div>}

    <dialog className="users-dialog" ref={dialogRef} onClose={close}
      aria-labelledby="user-detail-heading" dir="rtl">
      <div className="users-dialog-head">
        <div><p>پرونده حساب کاربری</p><h2 id="user-detail-heading">{selected?.mobile || selected?.email || "مشخصات کاربر"}</h2></div>
        <button type="button" className="users-dialog-close" onClick={close} aria-label="بستن پرونده">×</button>
      </div>
      {selected && <div className="users-dialog-content">
        <dl className="users-details">
          <div><dt>شناسه حساب</dt><dd dir="ltr">{selected.customerUserId}</dd></div>
          <div><dt>تلفن همراه</dt><dd dir="ltr">{selected.mobile || "—"}</dd></div>
          <div><dt>ایمیل</dt><dd dir="ltr">{selected.email || "—"}</dd></div>
          <div><dt>وضعیت</dt><dd>{selected.isActive ? "فعال" : "غیرفعال"}</dd></div>
          <div><dt>مالکیت</dt><dd>{ownerLabel(selected)}</dd></div>
          <div><dt>شیوه ثبت‌نام</dt><dd>{acquisition[selected.acquisitionChannel] || "نامشخص"}</dd></div>
          <div><dt>تاریخ ثبت‌نام</dt><dd>{new Date(selected.createdAtUtc).toLocaleString("fa-IR")}</dd></div>
          <div><dt>تعداد مؤدیان</dt><dd>{selected.taxpayerCount.toLocaleString("fa-IR")}</dd></div>
        </dl>
        <h3>مؤدیان وابسته به این کاربر</h3>
        {modalError && <p role="alert" className="form-error">{modalError}</p>}
        {detailsLoading ? <p role="status">در حال بارگذاری پرونده مالیاتی…</p> :
          taxpayers.length === 0 ? <p className="identity-muted">مؤدی ثبت‌شده‌ای در این حساب وجود ندارد.</p> :
          <div className="users-taxpayers">{taxpayers.map(t => <div key={t.id}>
            <strong>{t.displayName}</strong>
            <small>{t.kind === 1 ? "حقیقی" : "حقوقی"}</small>
            <small dir="ltr">{t.normalizedIdentifier}</small>
          </div>)}</div>}
        <p className="field-hint">تعداد مؤدیان مجاز باید براساس پلن فعال در Backend کنترل شود؛ تا آن زمان این پنجره فقط نمایش می‌دهد.</p>
      </div>}
      <div className="users-dialog-footer"><button type="button" className="users-button"
        onClick={close}>بستن</button></div>
    </dialog>
  </section>;
}
