"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import ParticipantRegistrationModal from "./participant-registration-modal";
import FiscalRecordModal from "./fiscal-record-modal";
import ConsoleLoader from "../components/console-loader";

type Buyer = { customerUserId: string; mobile: string | null; email: string | null;
  isActive: boolean; taxpayerCount: number; rootLevel1UserId: string | null;
  partnerLevel2UserId: string | null; personName?: string | null;
  companyName?: string | null; profileComplete?: boolean };
type Taxpayer = { id: string; kind: number; displayName: string; status: number;
  normalizedIdentifier: string; revision: number };
type Party = { id: string; kind: number; displayName: string; status: number };
type FiscalEntry = { id: string; customerUserId: string; type: "buyer" | "taxpayer";
  kind: number; displayName: string; identifier: string; status: number };
type FiscalPage = { items: FiscalEntry[]; page: number; pageSize: number; total: number };
type Row = { id: string; buyerId: string; type: "buyer" | "taxpayer"; kind: number | null;
  name: string; identifier: string; status: string; ownerName: string };

const kindLabel = (kind: number | null) => kind === 1 ? "شخص حقیقی" : kind === 2 ? "شخص حقوقی" : "تعیین‌نشده";
const formatError = (status: number) => status === 401 ? "نشست معتبر نیست." :
  status === 403 ? "مجوز دسترسی ندارید." : status === 409 ? "اطلاعات تکراری است." :
  "دریافت یا ذخیره اطلاعات با خطا مواجه شد.";
async function get<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: "no-store", credentials: "same-origin" });
  if (!res.ok) throw new Error(formatError(res.status));
  return res.json() as Promise<T>;
}

export default function BuyerTaxpayerDirectory({ admin, level1, actorUserId }: { admin: boolean; level1: boolean; actorUserId: string }) {
  const [rows, setRows] = useState<Row[]>([]);
  const requestVersion = useRef(0);
  const [buyers, setBuyers] = useState<Buyer[]>([]);
  const [registrationBuyers, setRegistrationBuyers] = useState<Buyer[]>([]);
  const [selectedRecord, setSelectedRecord] = useState<Row | null>(null);
  const [registerMode, setRegisterMode] = useState<"buyer" | "seller" | null>(null);
  const [filter, setFilter] = useState<"all" | "buyer" | "taxpayer">("all");
  const [kind, setKind] = useState("all");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [refreshedAt, setRefreshedAt] = useState<string | null>(null);
  const [error, setError] = useState("");

  const reload = useCallback(async () => {
    const version = ++requestVersion.current;
    setLoading(true); setError("");
    try {
      const parameters = new URLSearchParams({ page: String(page), pageSize: String(pageSize),
        type: filter, ...(kind === "all" ? {} : { kind }) });
      if (query.trim()) parameters.set("query", query.trim());
      // This is a fiscal row query, not a query of software login accounts.
      const fiscal = await get<FiscalPage>("/api/fiscal-participants?" + parameters);
      if (version !== requestVersion.current) return;
      setRows(fiscal.items.map(item => ({
        id: item.id, buyerId: item.customerUserId, type: item.type,
        kind: item.kind, name: item.displayName, identifier: item.identifier,
        status: item.status === 1 ? "فعال" : "غیرفعال",
        ownerName: item.customerUserId
      })));
      setTotal(fiscal.total);
      if (!admin) {
        const mine = await get<{ customerUserId: string; displayName: string | null }>(
          "/api/party-workspace/mine");
        if (version !== requestVersion.current) return;
        setBuyers([{ customerUserId: mine.customerUserId, mobile: null,
          email: mine.displayName, isActive: true, taxpayerCount: 0, profileComplete: true,
          personName: mine.displayName,
          rootLevel1UserId: null, partnerLevel2UserId: null }]);
      }
    } catch (e) {
      if (version === requestVersion.current) {
        setError((e as Error).message); setRows([]); setTotal(0);
      }
    } finally {
      if (version === requestVersion.current) {
        setLoading(false); setLoadedOnce(true);
        setRefreshedAt(new Date().toLocaleTimeString("fa-IR"));
      }
    }
  }, [admin, page, pageSize, query, filter, kind]);
  useEffect(() => {
    void reload();
    return () => { requestVersion.current++; };
  }, [reload]);

  const maxPage = Math.max(1, Math.ceil(total / pageSize));
  const activePage = Math.min(page, maxPage);
  const shown = rows;

  async function startRegistration(mode: "buyer" | "seller") {
    setRegisterMode(mode);
    if (!admin) return;
    try {
      const all = await get<Buyer[]>("/api/customer-owners/accounts");
      setRegistrationBuyers(all.filter(x => x.isActive && x.profileComplete));
    } catch (e) {
      setError((e as Error).message);
      setRegisterMode(null);
    }
  }

  return <section className="console-directory">
    {selectedRecord && <FiscalRecordModal key={selectedRecord.type + selectedRecord.id}
      record={selectedRecord} isLevel1={level1}
      onClose={() => setSelectedRecord(null)} onChanged={reload} />}
    {registerMode && <ParticipantRegistrationModal mode={registerMode} buyers={admin ? registrationBuyers : buyers} actorUserId={actorUserId}
      level1={level1} onClose={() => setRegisterMode(null)} onCompleted={reload}/> }
    {<div className="console-directory-actions">
      <button className="identity-primary" type="button" onClick={() => void startRegistration("buyer")}>+ افزودن خریدار</button>
      <button className="identity-secondary" type="button" onClick={() => void startRegistration("seller")}>+ افزودن مؤدی</button>
      <button type="button" className="identity-secondary" onClick={() => { setError(""); void reload(); }} disabled={loading}>بازخوانی فهرست</button>
    </div>}
      <div className="console-directory-filters">
        <label>نوع رکورد<select value={filter} onChange={e => { setFilter(e.target.value as typeof filter); setPage(1); }}>
          <option value="all">همه</option><option value="buyer">خریداران</option><option value="taxpayer">مؤدیان</option>
        </select></label>
        <label>نوع شخصیت<select value={kind} onChange={e => { setKind(e.target.value); setPage(1); }}>
          <option value="all">همه</option><option value="1">شخص حقیقی</option><option value="2">شخص حقوقی</option>
        </select></label>
        <label className="console-filter-search">جست‌وجو<input value={query} onChange={e => { setQuery(e.target.value); setPage(1); }}
          placeholder="نام خریدار / مؤدی یا شناسه قانونی"/></label>
      </div>
      {error && <p role="alert" className="party-error">{error}</p>}
      <div className="console-table-stage" aria-busy={loading}>
        {loading && <ConsoleLoader full label="در حال دریافت خریداران و مؤدیان ثبت‌شده…" />}
        <div className="console-table-scroll"><table className="console-table">
          <thead><tr><th>ردیف</th><th>نوع</th><th>شخصیت</th><th>نام / عنوان</th>
            <th>شناسه مالیاتی</th><th>حساب مالک در نرم‌افزار</th><th>وضعیت</th><th>عملیات</th></tr></thead>
          <tbody>{shown.map((r,i) => <tr key={r.type + r.id}>
            <td>{(activePage-1)*20+i+1}</td>
            <td>{r.type === "buyer" ? "خریدار" : "مؤدی"}</td><td>{kindLabel(r.kind)}</td>
            <td>{r.name}</td><td><bdi dir="ltr">{r.identifier}</bdi></td>
            <td>{r.ownerName}</td><td><span className="console-status">{r.status}</span></td>
            <td><button className="console-row-action" type="button"
              onClick={() => setSelectedRecord(r)}>
              مشاهده پرونده</button></td>
          </tr>)}</tbody>
        </table></div>
      </div>
      {!loading && loadedOnce && !error && shown.length === 0 && <p className="console-empty">رکوردی مطابق فیلتر پیدا نشد.</p>}
      <footer className="console-table-footer"><span>{total} رکورد{refreshedAt ? " — آخرین بازخوانی: " + refreshedAt : ""}</span>
        <div><button disabled={activePage <= 1} onClick={() => setPage(p => p-1)}>قبلی</button>
          <span>صفحه {activePage} از {maxPage}</span>
          <button disabled={activePage >= maxPage} onClick={() => setPage(p => p+1)}>بعدی</button></div>
      </footer>
      <p className="field-hint">این فهرست مستقلاً از سوابق خریدار و مؤدی ساخته می‌شود، نه از حساب‌های ثبت‌نام‌شده نرم‌افزار.</p>
  </section>;
}
