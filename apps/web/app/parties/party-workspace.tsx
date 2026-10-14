"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";

type Party = { id: string; kind: number; displayName: string; status: number; revision: number };
type Page = { items: Party[]; page: number; pageSize: number; total: number };
type Profile = { customerUserId: string; displayName: string; status: number; loginActive: boolean;
  preferredPartyId: string | null; revision: number };
type Taxpayer = { id: string; kind: number; displayName: string; status: number; revision: number };
type Mine = { customerUserId: string; displayName: string | null; profileStatus: number | null;
  preferredPartyId: string | null; taxpayers: Taxpayer[]; activeMembershipCount: number;
  writesEnabled: boolean };
type Contact = { id: string; kind: number; value: string; contactPerson: string | null;
  isPrimary: boolean; revision: number };
type Address = { id: string; label: string; city: string; province: string;
  streetAddress: string; postalCode: string | null; isPrimary: boolean; revision: number };
type Details = { contacts: Contact[]; addresses: Address[] };
type Person = { id: string; revision: number; firstName?: string; lastName?: string;
  nationalCode?: string; registeredName?: string; nationalIdentifier?: string; registrationNumber?: string | null };
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const errors: Record<number, string> = {
  400: "اطلاعات ورودی معتبر نیست.", 401: "نشست ورود معتبر نیست؛ دوباره وارد شوید.",
  403: "دسترسی به این مشتری یا عملیات مجاز نیست.",
  404: "اطلاعات یافت نشد یا مجوز آن لغو شده است.",
  409: "رکورد تکراری است یا نسخه اطلاعات تغییر کرده؛ بازخوانی کنید.",
  503: "اتصال به سرویس برقرار نشد."
};
async function api<T>(path: string, method: "GET" | "POST" | "PUT" | "DELETE" = "GET", data?: unknown): Promise<T> {
  const result = await fetch("/api/party-workspace/" + path, {
    method, credentials: "same-origin", cache: "no-store",
    headers: data !== undefined ? { "Content-Type": "application/json" } : undefined,
    body: data !== undefined ? JSON.stringify(data) : undefined
  });
  if (!result.ok) throw new Error(errors[result.status] ?? "درخواست انجام نشد.");
  return result.json() as Promise<T>;
}

export default function PartyWorkspace({ isAdmin, customerId }: { isAdmin: boolean; customerId: string }) {
  const [customer, setCustomer] = useState(customerId);
  const [page, setPage] = useState(1);
  const [kind, setKind] = useState("all");
  const [query, setQuery] = useState("");
  const [items, setItems] = useState<Page>({ items: [], page: 1, pageSize: 20, total: 0 });
  const [profile, setProfile] = useState<Profile | null>(null);
  const [mine, setMine] = useState<Mine | null>(null);
  const [selected, setSelected] = useState<Party | null>(null);
  const [person, setPerson] = useState<Person | null>(null);
  const [details, setDetails] = useState<Details>({ contacts: [], addresses: [] });
  const [newKind, setNewKind] = useState("1");
  const [national, setNational] = useState("");
  const [first, setFirst] = useState("");
  const [last, setLast] = useState("");
  const [registration, setRegistration] = useState("");
  const [contactKind, setContactKind] = useState("1");
  const [contactValue, setContactValue] = useState("");
  const [addressCity, setAddressCity] = useState("");
  const [addressProvince, setAddressProvince] = useState("");
  const [addressStreet, setAddressStreet] = useState("");
  const [addressPostal, setAddressPostal] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      if (!isAdmin) {
        const data = await api<Mine>("mine");
        setMine(data);
        return;
      }
      if (!uuid.test(customer)) { setItems({ items: [], page: 1, pageSize: 20, total: 0 }); return; }
      const base = "customers/" + customer;
      const [parties, account] = await Promise.all([
        api<Page>(base + "/parties?page=" + page + "&pageSize=20" +
          (kind === "all" ? "" : "&kind=" + kind) + "&query=" + encodeURIComponent(query.trim())),
        api<Profile>(base + "/profile").catch(() => null)
      ]);
      setItems(parties); setProfile(account);
      if (selected && !parties.items.some(p => p.id === selected.id)) {
        setSelected(null); setPerson(null); setDetails({ contacts: [], addresses: [] });
      }
    } catch (ex) { setError((ex as Error).message); }
    finally { setLoading(false); }
  }, [customer, isAdmin, kind, page, query, selected]);

  useEffect(() => { void load(); }, [load]);

  async function view(p: Party) {
    setSelected(p); setPerson(null); setError(""); setNotice("");
    try {
      const path = "customers/" + customer + "/parties/" + p.id;
      const [record, extras] = await Promise.all([
        api<Person>(path + (p.kind === 1 ? "/natural" : "/legal")),
        api<Details>(path + "/details")
      ]);
      setPerson(record); setDetails(extras);
      setNational(record.nationalCode ?? record.nationalIdentifier ?? "");
      setFirst(record.firstName ?? record.registeredName ?? "");
      setLast(record.lastName ?? ""); setRegistration(record.registrationNumber ?? "");
    } catch (ex) { setError((ex as Error).message); }
  }

  async function apply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy || !uuid.test(customer)) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const type = selected?.kind ?? Number(newKind);
      const body = type === 1
        ? { nationalCode: national, firstName: first, lastName: last,
            expectedRevision: person?.revision ?? 0, archive: false }
        : { nationalIdentifier: national, registeredName: first, registrationNumber: registration || null,
            expectedRevision: person?.revision ?? 0, archive: false };
      const path = "customers/" + customer + "/parties" +
        (selected ? "/" + selected.id : "") + (type === 1 ? "/natural" : "/legal");
      await api(path, selected ? "PUT" : "POST", body);
      setNotice("اطلاعات ذخیره شد."); setSelected(null); setPerson(null);
      setNational(""); setFirst(""); setLast(""); setRegistration("");
      await load();
    } catch (ex) { setError((ex as Error).message); }
    finally { setBusy(false); }
  }

  async function addContact(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selected || busy) return;
    setBusy(true); setError("");
    try {
      await api("customers/" + customer + "/parties/" + selected.id + "/contacts", "POST",
        { kind: Number(contactKind), value: contactValue, contactPerson: null, isPrimary: false });
      setContactValue(""); setNotice("اطلاعات تماس ثبت شد."); await view(selected);
    } catch (ex) { setError((ex as Error).message); } finally { setBusy(false); }
  }

  async function addAddress(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selected || busy) return;
    setBusy(true); setError("");
    try {
      await api("customers/" + customer + "/parties/" + selected.id + "/addresses", "POST",
        { label: "اصلی", countryCode: "IR", province: addressProvince, city: addressCity,
          streetAddress: addressStreet, postalCode: addressPostal || null, isPrimary: false });
      setAddressCity(""); setAddressProvince(""); setAddressStreet(""); setAddressPostal("");
      setNotice("آدرس ثبت شد."); await view(selected);
    } catch (ex) { setError((ex as Error).message); } finally { setBusy(false); }
  }

  if (!isAdmin) return <section className="party-card" aria-busy={loading}>
    <h2>داشبورد مشتری</h2>
    <p className="identity-muted">مشاهده اطلاعات حساب و مؤدیان مجاز — عملیات مدیریتی غیرفعال است.</p>
    {error && <p role="alert" className="party-error">{error}</p>}
    {loading && <p role="status">در حال دریافت اطلاعات…</p>}
    {mine && <><p>نام حساب: {mine.displayName ?? "ثبت نشده"}</p>
      <p>تعداد ارتباط فعال مؤدیان: {mine.activeMembershipCount}</p>
      <p>وضعیت ویرایش: {mine.writesEnabled ? "فعال" : "فقط مشاهده"}</p>
      <h3>مؤدیان شما</h3>
      {mine.taxpayers.length === 0 ? <p>مؤدی‌ای برای این حساب ثبت نشده است.</p> :
        <ul>{mine.taxpayers.map(t => <li key={t.id}>{t.displayName} — {t.kind === 1 ? "حقیقی" : "حقوقی"} <bdi dir="ltr">{t.id}</bdi></li>)}</ul>}
    </>}
  </section>;

  return <div className="party-grid">
    <section className="party-card">
      <h2>انتخاب مشتری و جست‌وجوی اشخاص</h2>
      <label htmlFor="party-customer">شناسه حساب مشتری</label>
      <input id="party-customer" dir="ltr" value={customer} onChange={e => {
        setCustomer(e.target.value); setPage(1); setSelected(null); setPerson(null);
      }} aria-invalid={!uuid.test(customer)}/>
      <form onSubmit={e => { e.preventDefault(); setPage(1); void load(); }}>
        <label htmlFor="party-query">جست‌وجو بر اساس نام یا شناسه</label>
        <input id="party-query" value={query} onChange={e => setQuery(e.target.value)}/>
        <label htmlFor="party-kind">نوع شخص</label>
        <select id="party-kind" value={kind} onChange={e => { setKind(e.target.value); setPage(1); }}>
          <option value="all">همه</option><option value="1">حقیقی</option><option value="2">حقوقی</option>
        </select>
        <button className="identity-secondary" type="submit" disabled={loading}>بازخوانی</button>
      </form>
      {error && <p className="party-error" role="alert">{error}</p>}
      {notice && <p className="party-success" role="status">{notice}</p>}
      {loading && <p role="status">در حال بارگذاری…</p>}
      {profile && <p>پروفایل حساب: {profile.displayName} — {profile.status === 1 ? "فعال" : "تعلیق"}</p>}
      <p>تعداد اشخاص: {items.total}</p>
      <ul className="party-list">{items.items.map(p => <li key={p.id}>
        <button type="button" onClick={() => void view(p)} aria-pressed={selected?.id === p.id}>
          {p.displayName} <small>{p.kind === 1 ? "حقیقی" : "حقوقی"} · نسخه {p.revision}</small>
        </button></li>)}</ul>
      {!loading && items.items.length === 0 && <p>موردی یافت نشد.</p>}
      <div className="party-actions">
        <button disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>قبلی</button>
        <span>صفحه {page}</span>
        <button disabled={loading || page * 20 >= items.total} onClick={() => setPage(p => p + 1)}>بعدی</button>
      </div>
    </section>
    <section className="party-card">
      <div className="party-actions">
        <h2>{selected ? "ویرایش شخص" : "ثبت شخص جدید"}</h2>
        <button type="button" onClick={() => { setSelected(null); setPerson(null);
          setNational(""); setFirst(""); setLast(""); setRegistration(""); }}>جدید</button>
      </div>
      <form onSubmit={apply} className="party-form">
        {!selected && <><label htmlFor="party-new-kind">نوع شخص</label>
          <select id="party-new-kind" value={newKind} onChange={e => setNewKind(e.target.value)}>
            <option value="1">حقیقی</option><option value="2">حقوقی</option>
          </select></>}
        <label htmlFor="party-national">{(selected?.kind ?? Number(newKind)) === 1 ? "کد ملی" : "شناسه ملی"}</label>
        <input id="party-national" dir="ltr" required value={national} onChange={e => setNational(e.target.value)}/>
        <label htmlFor="party-first">{(selected?.kind ?? Number(newKind)) === 1 ? "نام" : "نام ثبتی"}</label>
        <input id="party-first" required maxLength={256} value={first} onChange={e => setFirst(e.target.value)}/>
        {(selected?.kind ?? Number(newKind)) === 1 ? <>
          <label htmlFor="party-last">نام خانوادگی</label>
          <input id="party-last" required maxLength={100} value={last} onChange={e => setLast(e.target.value)}/>
        </> : <>
          <label htmlFor="party-reg">شماره ثبت (اختیاری)</label>
          <input id="party-reg" dir="ltr" value={registration} onChange={e => setRegistration(e.target.value)}/>
        </>}
        <button className="identity-primary" disabled={busy || !uuid.test(customer) || (!!selected && !person)}>
          {busy ? "در حال ذخیره…" : selected ? "ثبت ویرایش" : "ایجاد شخص"}
        </button>
      </form>
      {selected && person && <div className="party-details">
        <h3>اطلاعات تماس</h3>
        <ul>{details.contacts.map(c => <li key={c.id}><bdi dir="ltr">{c.value}</bdi>{c.isPrimary ? " · اصلی" : ""}</li>)}</ul>
        <form className="party-form" onSubmit={addContact}>
          <label htmlFor="party-contact-kind">نوع تماس</label>
          <select id="party-contact-kind" value={contactKind} onChange={e => setContactKind(e.target.value)}>
            <option value="1">موبایل</option><option value="2">تلفن</option><option value="3">ایمیل</option>
          </select>
          <label htmlFor="party-contact-value">شماره یا ایمیل</label>
          <input id="party-contact-value" required dir="ltr" value={contactValue}
            onChange={e => setContactValue(e.target.value)}/>
          <button type="submit" disabled={busy}>افزودن تماس</button>
        </form>
        <h3>آدرس‌ها</h3>
        <ul>{details.addresses.map(a => <li key={a.id}>{a.label} — {a.province}، {a.city}، {a.streetAddress}</li>)}</ul>
        <form className="party-form" onSubmit={addAddress}>
          <label htmlFor="party-province">استان</label><input id="party-province" required value={addressProvince} onChange={e => setAddressProvince(e.target.value)}/>
          <label htmlFor="party-city">شهر</label><input id="party-city" required value={addressCity} onChange={e => setAddressCity(e.target.value)}/>
          <label htmlFor="party-street">نشانی</label><textarea id="party-street" required minLength={3} value={addressStreet} onChange={e => setAddressStreet(e.target.value)}/>
          <label htmlFor="party-postal">کد پستی (اختیاری)</label><input id="party-postal" dir="ltr" value={addressPostal} onChange={e => setAddressPostal(e.target.value)}/>
          <button type="submit" disabled={busy}>افزودن آدرس</button>
        </form>
      </div>}
    </section>
  </div>;
}
