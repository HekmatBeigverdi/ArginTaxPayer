"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import ConsoleLoader from "../components/console-loader";


type Account = {
  customerUserId: string; rootLevel1UserId: string | null; partnerLevel2UserId: string | null;
  acquisitionChannel: number; revision: number; mobile: string | null; email: string | null;
  createdAtUtc: string; isActive: boolean; taxpayerCount: number;
};
type Partner = { userId: string; mobile: string | null; email: string | null };
type AccountPage = { items: Account[]; page: number; pageSize: number; total: number };
const acquisition: Record<number,string> = {
  0: "نیازمند تعیین مالکیت", 1: "ثبت‌نام مستقیم", 2: "ثبت توسط سطح ۱",
  3: "ثبت توسط سطح ۲", 4: "معرفی تأییدشده", 5: "بازیابی مالکیت کاربر قدیمی"
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
  const [total, setTotal] = useState(0);
  const [selected, setSelected] = useState<Account | null>(null);
  const [search, setSearch] = useState("");
  const [ownershipFilter, setOwnershipFilter] = useState("all");
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [modalError, setModalError] = useState("");
  const [partnerId, setPartnerId] = useState("");
  const [partners, setPartners] = useState<Partner[]>([]);
  const [partnerLoadError, setPartnerLoadError] = useState("");
  const [transferReason, setTransferReason] = useState("");
  const [transferKind, setTransferKind] = useState(1);
  const [savingOwner, setSavingOwner] = useState(false);
  const [transferNotice, setTransferNotice] = useState("");
  const [transferHistory, setTransferHistory] = useState<{ id: string; previousPartnerLevel2UserId: string | null; newPartnerLevel2UserId: string | null; explanation: string; changedAtUtc: string }[] | null>(null);
  const dialogRef = useRef<HTMLDialogElement>(null);
  const requestRef = useRef(0);

  const refresh = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (search.trim()) params.set("query", search.trim());
      if (ownershipFilter !== "all") params.set("ownership", ownershipFilter);
      const result = await load<AccountPage>("/api/customer-owners/accounts/page?" + params);
      setAccounts(result.items); setTotal(result.total);
      // A revoked partner must not keep a customer's information in a previously opened dialog.
      setSelected(previous => {
        if (previous && !result.items.some(x => x.customerUserId === previous.customerUserId)) {
          dialogRef.current?.close();
          return null;
        }
        return previous ? result.items.find(x => x.customerUserId === previous.customerUserId) || null : null;
      });
    } catch (e) {
      setAccounts([]); setTotal(0); setSelected(null); dialogRef.current?.close();
      setError((e as Error).message);
    } finally { setLoading(false); }
  }, [page, pageSize, search, ownershipFilter]);

  useEffect(() => {
    if (level1) void load<Partner[]>("/api/customer-owners/partners")
      .then(setPartners).catch(() => setPartnerLoadError("فهرست شرکای مجاز دریافت نشد."));
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

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const activePage = Math.min(page, totalPages);
  const pageRows = accounts;
  const firstIndex = total ? (activePage - 1) * pageSize + 1 : 0;
  const lastIndex = Math.min(activePage * pageSize, total);

  function close() {
    requestRef.current++;
    if (dialogRef.current?.open) dialogRef.current.close();
    setSelected(null); setModalError("");
  }

  function open(account: Account) {
    setSelected(account); setModalError("");
    setPartnerId(account.partnerLevel2UserId || "");
    setTransferReason(""); setTransferNotice(""); setTransferHistory(null);
    dialogRef.current?.showModal();
    requestRef.current++;
  }

  async function assignOwner(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!level1 || !selected || savingOwner || !transferReason.trim()) return;
    const target = partnerId.trim();
    if (target && !/^[0-9a-f]{8}-[0-9a-f-]{27,}$/i.test(target)) {
      setModalError("شناسه ادمین سطح ۲ معتبر نیست."); return;
    }
    if (!window.confirm("انتساب تجاری این کاربر تغییر کند؟ این عملیات سوابق فروش قبلی را تغییر نمی‌دهد.")) return;
    setSavingOwner(true); setModalError(""); setTransferNotice("");
    try {
      const response = await fetch("/api/customer-owners/" + selected.customerUserId + "/transfer", {
        method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          partnerLevel2UserId: target || null,
          expectedRevision: selected.revision,
          reason: transferKind,
          explanation: transferReason.trim()
        })
      });
      if (!response.ok) throw new Error(errorLabel(response.status));
      setTransferNotice("مالکیت تجاری با تأیید سطح ۱ ثبت شد؛ تغییر در تاریخچه محفوظ است.");
      setTransferReason("");
      await refresh();
    } catch (e) { setModalError((e as Error).message); }
    finally { setSavingOwner(false); }
  }

  async function readHistory() {
    if (!selected || !level1) return;
    setModalError("");
    try {
      setTransferHistory(await load("/api/customer-owners/" + selected.customerUserId + "/history"));
    } catch (e) { setModalError((e as Error).message); }
  }

  const ownerLabel = (a: Account) => !a.rootLevel1UserId ? "نیازمند بررسی" :
    a.partnerLevel2UserId ? "شریک سطح ۲" : "مستقیم سطح ۱";

  return <section className="users-panel" aria-label="فهرست کاربران نرم‌افزار">
    <div className="users-toolbar">
      <div><h2>{level1 ? "کاربران ثبت‌نام‌شده" : "مشتریان من"}</h2>
        <p>هر ردیف صرفاً یک حساب ورود نرم‌افزار است؛ خریداران و مؤدیان در فهرست مالیاتی مستقل مدیریت می‌شوند.</p></div>
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
          <th scope="col">مالکیت تجاری</th>
          <th scope="col">ثبت‌نام</th><th scope="col">عملیات</th>
        </tr></thead>
        <tbody>
          {loading ? <tr><td colSpan={6}><ConsoleLoader label="در حال دریافت کاربران…" /></td></tr> :
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
              <td>{new Date(a.createdAtUtc).toLocaleDateString("fa-IR")}</td>
              <td><button className="users-action" type="button" onClick={() => void open(a)}>
                مشاهده پرونده
              </button></td>
            </tr>)}
        </tbody>
      </table>
    </div>

    <div className="users-pagination" aria-label="صفحه‌بندی کاربران">
      <span>نمایش {firstIndex.toLocaleString("fa-IR")} تا {lastIndex.toLocaleString("fa-IR")} از {total.toLocaleString("fa-IR")} کاربر</span>
      <div>
        <button type="button" disabled={activePage <= 1 || loading} onClick={() => setPage(p => p - 1)}>قبلی</button>
        <span>صفحه {activePage.toLocaleString("fa-IR")} از {totalPages.toLocaleString("fa-IR")}</span>
        <button type="button" disabled={activePage >= totalPages || loading} onClick={() => setPage(p => p + 1)}>بعدی</button>
      </div>
    </div>

    <dialog className="users-dialog" ref={dialogRef} onClose={close}
      onClick={event => {
        if (event.target !== event.currentTarget) return;
        const r = event.currentTarget.getBoundingClientRect();
        if (event.clientX < r.left || event.clientX > r.right ||
            event.clientY < r.top || event.clientY > r.bottom) close();
      }}
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
        </dl>
        {level1 && <section className="users-owner-edit">
          <h3>انتساب تجاری این کاربر</h3>
          <p>ثبت‌نام عمومی زیرمجموعه سطح ۱ است. واگذاری به شریک سطح ۲ فقط با تأیید شما انجام می‌شود.</p>
          <form onSubmit={assignOwner}>
            <label htmlFor="owner-partner-id">مالک تجاری مشتری
              <select id="owner-partner-id" value={partnerId} onChange={e => setPartnerId(e.target.value)}>
                <option value="">ادمین سطح ۱ — مشتری مستقیم</option>
                {partnerId && !partners.some(p => p.userId === partnerId) &&
                  <option value={partnerId}>شریک فعلی — {partnerId.slice(0, 8)}…</option>}
                {partners.map(p => <option key={p.userId} value={p.userId}>
                  {p.mobile || p.email || p.userId} — ادمین سطح ۲
                </option>)}
              </select>
            </label>
            {partnerLoadError && <p role="alert" className="form-error">{partnerLoadError}</p>}
            <label htmlFor="owner-transfer-kind">علت انتساب
              <select id="owner-transfer-kind" value={transferKind} onChange={e => setTransferKind(Number(e.target.value))}>
                <option value={1}>معرفی مشتری توسط شریک و تأیید سطح ۱</option>
                <option value={2}>اصلاح مالکیت اشتباه</option>
                <option value={3}>غیرفعال شدن شریک قبلی</option>
                <option value={4}>سایر دلایل</option>
              </select>
            </label>
            <label htmlFor="owner-transfer-reason">شرح و مستندات
              <textarea id="owner-transfer-reason" rows={2} maxLength={1000} required
                value={transferReason} onChange={e => setTransferReason(e.target.value)}/>
            </label>
            <button className="users-button" type="submit"
              disabled={savingOwner || !!partnerLoadError || !transferReason.trim() ||
                (!!selected.rootLevel1UserId && partnerId.trim() === (selected.partnerLevel2UserId || ""))}>
              {savingOwner ? "در حال ثبت…" : selected.rootLevel1UserId ? "تأیید تغییر انتساب" : "ثبت اولیه مالکیت کاربر قدیمی"}
            </button>
          </form>
          <button className="users-action" type="button" onClick={() => void readHistory()}>مشاهده تاریخچه انتساب</button>
          {transferNotice && <p role="status" className="auth-info">{transferNotice}</p>}
          {transferHistory && <ul className="users-owner-history">{transferHistory.length === 0 ?
            <li>سابقه انتقال قبلی موجود نیست.</li> : transferHistory.map(item =>
            <li key={item.id}>
              {new Date(item.changedAtUtc).toLocaleString("fa-IR")} —
              از {item.previousPartnerLevel2UserId || "سطح ۱"} به {item.newPartnerLevel2UserId || "سطح ۱"} —
              {item.explanation}
            </li>)}</ul>}
        </section>}
        <p className="field-hint">این پنجره فقط اطلاعات حساب ورود و انتساب تجاری را مدیریت می‌کند؛ سوابق خریدار و مؤدی در بخش مالیاتی مستقل هستند.</p>
      </div>}
      <div className="users-dialog-footer"><button type="button" className="users-button"
        onClick={close}>بستن</button></div>
    </dialog>
  </section>;
}
