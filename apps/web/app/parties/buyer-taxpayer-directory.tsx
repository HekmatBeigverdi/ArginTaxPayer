"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import ParticipantRegistrationModal from "./participant-registration-modal";
import PartyWorkspace from "./party-workspace";
import TaxpayerWorkspace from "../taxpayers/taxpayer-workspace";
import ConsoleLoader from "../components/console-loader";

type Buyer = { customerUserId: string; mobile: string | null; email: string | null;
  isActive: boolean; taxpayerCount: number };
type Taxpayer = { id: string; kind: number; displayName: string; status: number;
  normalizedIdentifier: string; revision: number };
type Party = { id: string; kind: number; displayName: string; status: number };
type PartyPage = { items: Party[]; total: number };
type Row = { id: string; buyerId: string; type: "buyer" | "taxpayer"; kind: number | null;
  name: string; identifier: string; status: string; ownerName: string };
type View = "list" | "buyer-details" | "taxpayer-connection";
const kindLabel = (kind: number | null) => kind === 1 ? "شخص حقیقی" : kind === 2 ? "شخص حقوقی" : "تعیین‌نشده";
const formatError = (status: number) => status === 401 ? "نشست معتبر نیست." :
  status === 403 ? "مجوز دسترسی ندارید." : status === 409 ? "اطلاعات تکراری است." :
  "دریافت یا ذخیره اطلاعات با خطا مواجه شد.";
async function get<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: "no-store", credentials: "same-origin" });
  if (!res.ok) throw new Error(formatError(res.status));
  return res.json() as Promise<T>;
}

export default function BuyerTaxpayerDirectory({ admin, level1 }: { admin: boolean; level1: boolean }) {
  const [rows, setRows] = useState<Row[]>([]);
  const [buyers, setBuyers] = useState<Buyer[]>([]);
  const [view, setView] = useState<View>("list");
  const [registerMode, setRegisterMode] = useState<"buyer" | "seller" | null>(null);
  const [filter, setFilter] = useState<"all" | "buyer" | "taxpayer">("all");
  const [kind, setKind] = useState("all");
  const [query, setQuery] = useState("");
  const [selectedBuyer, setSelectedBuyer] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [progress, setProgress] = useState({ done: 0, total: 0 });
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [createdTaxpayerId, setCreatedTaxpayerId] = useState("");

  const reload = useCallback(async () => {
    setLoading(true); setError(""); setProgress({ done: 0, total: 0 });
    try {
      if (!admin) {
        const mine = await get<{ customerUserId: string; taxpayers: Taxpayer[]; displayName: string | null }>(
          "/api/party-workspace/mine");
        const mineRows: Row[] = mine.taxpayers.map(t => ({
          id: t.id, buyerId: mine.customerUserId, type: "taxpayer", kind: t.kind,
          name: t.displayName, identifier: "", status: t.status === 1 ? "فعال" : "غیرفعال",
          ownerName: mine.displayName || "حساب شما"
        }));
        setRows(mineRows); setBuyers([]);
        return;
      }
      const accounts = await get<Buyer[]>("/api/customer-owners/accounts");
      setBuyers(accounts);
      setProgress({ done: 0, total: accounts.length });
      const combined: Row[] = accounts.map(b => ({ id: b.customerUserId, buyerId: b.customerUserId,
        type: "buyer", kind: null, name: b.mobile || b.email || b.customerUserId,
        identifier: b.mobile || b.email || "", status: b.isActive ? "فعال" : "غیرفعال",
        ownerName: b.mobile || b.email || b.customerUserId }));
      setRows([...combined]);
      // Bounded batches avoid unbounded HTTP concurrency; the API still enforces authorization.
      for (let offset = 0; offset < accounts.length; offset += 6) {
        const batch = accounts.slice(offset, offset + 6);
        const records = await Promise.all(batch.map(async buyer => {
          const account = buyer.mobile || buyer.email || buyer.customerUserId;
          const [parties, taxpayers] = await Promise.all([
            get<PartyPage>(`/api/party-workspace/customers/${buyer.customerUserId}/parties?page=1&pageSize=100`)
              .catch(() => null),
            get<Taxpayer[]>(`/api/customer-owners/${buyer.customerUserId}/taxpayers`).catch(() => null)
          ]);
          // Each linked legal/natural Party is its own buyer-facing record.
          // Do not misclassify an account that is linked to both types.
          const buyerRows: Row[] = parties?.items?.length
            ? parties.items.map(p => ({ id: p.id, buyerId: buyer.customerUserId,
                type: "buyer" as const, kind: p.kind, name: p.displayName,
                identifier: account, status: buyer.isActive ? "فعال" : "غیرفعال",
                ownerName: account }))
            : [{ id: buyer.customerUserId, buyerId: buyer.customerUserId,
                type: "buyer", kind: null, name: account, identifier: account,
                status: buyer.isActive ? "فعال" : "غیرفعال", ownerName: account }];
          const taxpayerRows: Row[] = (taxpayers || []).map(t => ({
            id: t.id, buyerId: buyer.customerUserId, type: "taxpayer", kind: t.kind,
            name: t.displayName, identifier: t.normalizedIdentifier,
            status: t.status === 1 ? "فعال" : "غیرفعال", ownerName: account
          }));
          return [...buyerRows, ...taxpayerRows];
        }));
        const resolved = new Set(batch.map(a => a.customerUserId));
        const next = [...combined.filter(r => !resolved.has(r.buyerId)), ...records.flat()];
        combined.splice(0, combined.length, ...next);
        setRows([...combined]);
        setProgress({ done: Math.min(offset + batch.length, accounts.length), total: accounts.length });
      }
      setRows(combined);
    } catch (e) { setError((e as Error).message); setRows([]); }
    finally { setLoading(false); setLoadedOnce(true); }
  }, [admin]);
  useEffect(() => { void reload(); }, [reload]);

  const filtered = useMemo(() => rows.filter(r => {
    if (filter !== "all" && r.type !== filter) return false;
    if (kind !== "all" && String(r.kind) !== kind) return false;
    return (r.name + " " + r.identifier + " " + r.ownerName)
      .toLocaleLowerCase("fa").includes(query.trim().toLocaleLowerCase("fa"));
  }), [rows, filter, kind, query]);
  const maxPage = Math.max(1, Math.ceil(filtered.length / 20));
  const activePage = Math.min(page, maxPage);
  const shown = filtered.slice((activePage - 1) * 20, activePage * 20);

  function open(mode: View, customer = "") {
    setView(mode); setSelectedBuyer(customer); setError(""); setNotice("");
  }

  return <section className="console-directory">
    {registerMode && <ParticipantRegistrationModal mode={registerMode} buyers={buyers}
      level1={level1} onClose={() => setRegisterMode(null)} onCompleted={reload}/> }
    {admin && <div className="console-directory-actions">
      <button className="identity-primary" type="button" onClick={() => setRegisterMode("buyer")}>+ افزودن خریدار</button>
      <button className="identity-secondary" type="button" onClick={() => setRegisterMode("seller")}>+ افزودن مؤدی</button>
      <button type="button" className="identity-secondary" onClick={() => void reload()} disabled={loading}>بازخوانی فهرست</button>
    </div>}
    {view !== "list" && <div className="console-directory-panel">
      <div className="console-directory-panel-header"><h2>{{
        "buyer-details": "اشخاص خریدار و اطلاعات تماس",
        "taxpayer-connection": "پرونده و حافظه مالیاتی مؤدی",
        list: ""
      }[view]}</h2><button type="button" onClick={() => { setView("list"); void reload(); }}>بازگشت به فهرست</button></div>
      {view === "buyer-details" && <PartyWorkspace key={selectedBuyer}
        isAdmin={true} canCreateTaxpayer={level1} customerId={selectedBuyer} />}
      {view === "taxpayer-connection" && <>
        <p>ثبت کد حافظه مالیاتی و مشاهده وضعیت گواهی در این بخش انجام می‌شود.
          اطلاعات گواهی و اتصال در سامانه صرفاً فراداده هستند؛ ذخیره کلید خصوصی و اتصال عملیاتی مالیاتی در این مرحله فعال نیست.</p>
        {createdTaxpayerId && <p>مؤدی انتخاب‌شده: <bdi dir="ltr">{createdTaxpayerId}</bdi></p>}
        <TaxpayerWorkspace key={createdTaxpayerId || selectedBuyer} isLevel1={level1} initialTaxpayerId={createdTaxpayerId || undefined} customerId={selectedBuyer} showCreateForm={false}/>
      </>}
    </div>}
    {view === "list" && <>
      <div className="console-directory-filters">
        <label>نوع رکورد<select value={filter} onChange={e => { setFilter(e.target.value as typeof filter); setPage(1); }}>
          <option value="all">همه</option><option value="buyer">خریداران</option><option value="taxpayer">مؤدیان</option>
        </select></label>
        <label>نوع شخصیت<select value={kind} onChange={e => { setKind(e.target.value); setPage(1); }}>
          <option value="all">همه</option><option value="1">شخص حقیقی</option><option value="2">شخص حقوقی</option>
        </select></label>
        <label className="console-filter-search">جست‌وجو<input value={query} onChange={e => { setQuery(e.target.value); setPage(1); }}
          placeholder="نام، موبایل یا شناسه"/></label>
      </div>
      {error && <p role="alert" className="party-error">{error}</p>}
      {notice && <p role="status" className="party-success">{notice}</p>}
      <div className="console-table-stage" aria-busy={loading}>
        {loading && <ConsoleLoader full label={progress.total ?
          `در حال تکمیل فهرست خریداران و مؤدیان (${progress.done} از ${progress.total})` :
          "در حال دریافت فهرست خریداران و مؤدیان…"} />}
        <div className="console-table-scroll"><table className="console-table">
          <thead><tr><th>ردیف</th><th>نوع</th><th>شخصیت</th><th>نام / عنوان</th>
            <th>شناسه / موبایل</th><th>خریدار مرتبط</th><th>وضعیت</th><th>عملیات</th></tr></thead>
          <tbody>{shown.map((r,i) => <tr key={r.type + r.id}>
            <td>{(activePage-1)*20+i+1}</td>
            <td>{r.type === "buyer" ? "خریدار" : "مؤدی"}</td><td>{kindLabel(r.kind)}</td>
            <td>{r.name}</td><td><bdi dir="ltr">{r.identifier}</bdi></td>
            <td>{r.ownerName}</td><td><span className="console-status">{r.status}</span></td>
            <td><button className="console-row-action" type="button"
              onClick={() => { setCreatedTaxpayerId(r.type === "taxpayer" ? r.id : "");
                open(r.type === "buyer" ? "buyer-details" : "taxpayer-connection", r.buyerId); }}>
              مشاهده پرونده</button></td>
          </tr>)}</tbody>
        </table></div>
      </div>
      {!loading && loadedOnce && !error && shown.length === 0 && <p className="console-empty">رکوردی مطابق فیلتر پیدا نشد.</p>}
      <footer className="console-table-footer"><span>{filtered.length} رکورد</span>
        <div><button disabled={activePage <= 1} onClick={() => setPage(p => p-1)}>قبلی</button>
          <span>صفحه {activePage} از {maxPage}</span>
          <button disabled={activePage >= maxPage} onClick={() => setPage(p => p+1)}>بعدی</button></div>
      </footer>
      {admin && <p className="field-hint">نوع شخصیت برای هر شخص مرتبط به‌صورت مستقل ثبت می‌شود؛ حساب فاقد مشخصات شخص به‌عنوان «تعیین‌نشده» نمایش داده می‌شود.</p>}
    </>}
  </section>;
}
