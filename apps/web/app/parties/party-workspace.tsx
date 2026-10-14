"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import InvoiceParticipantForm from "./invoice-participant-form";

const PAGE_SIZE = 20;
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;

const errors: Record<number, string> = {
  400: "اطلاعات ورودی معتبر نیست.",
  401: "نشست ورود معتبر نیست؛ دوباره وارد شوید.",
  403: "دسترسی به این مشتری یا عملیات مجاز نیست.",
  404: "اطلاعات یافت نشد یا مجوز آن لغو شده است.",
  409: "رکورد تکراری است یا نسخه اطلاعات تغییر کرده؛ بازخوانی کنید.",
  503: "اتصال به سرویس برقرار نشد.",
};

type CustomerOption = { customerUserId: string; mobile: string | null; email: string | null; taxpayerCount: number; isActive: boolean };

type Party = {
  id: string;
  kind: number;
  displayName: string;
  status: number;
  revision: number;
};

type PartyPage = {
  items: Party[];
  page: number;
  pageSize: number;
  total: number;
};

type Profile = {
  customerUserId: string;
  displayName: string;
  status: number;
  loginActive: boolean;
  preferredPartyId: string | null;
  revision: number;
};

type Taxpayer = {
  id: string;
  kind: number;
  displayName: string;
  status: number;
  revision: number;
};

type Mine = {
  customerUserId: string;
  displayName: string | null;
  profileStatus: number | null;
  preferredPartyId: string | null;
  taxpayers: Taxpayer[];
  activeMembershipCount: number;
  writesEnabled: boolean;
};

type Contact = {
  id: string;
  kind: number;
  value: string;
  contactPerson: string | null;
  isPrimary: boolean;
  revision: number;
};

type Address = {
  id: string;
  label: string;
  city: string;
  province: string;
  streetAddress: string;
  postalCode: string | null;
  isPrimary: boolean;
  revision: number;
};

type PartyDetails = {
  contacts: Contact[];
  addresses: Address[];
};

type Person = {
  id: string;
  revision: number;
  firstName?: string;
  lastName?: string;
  nationalCode?: string;
  registeredName?: string;
  nationalIdentifier?: string;
  registrationNumber?: string | null;
};

const emptyPage = (): PartyPage => ({
  items: [],
  page: 1,
  pageSize: PAGE_SIZE,
  total: 0,
});

const emptyDetails = (): PartyDetails => ({ contacts: [], addresses: [] });

async function api<T>(
  path: string,
  method: "GET" | "POST" | "PUT" | "DELETE" = "GET",
  data?: unknown,
): Promise<T> {
  const response = await fetch(`/api/party-workspace/${path}`, {
    method,
    credentials: "same-origin",
    cache: "no-store",
    headers: data === undefined ? undefined : { "Content-Type": "application/json" },
    body: data === undefined ? undefined : JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error(errors[response.status] ?? "درخواست انجام نشد.");
  }

  return response.json() as Promise<T>;
}

export default function PartyWorkspace({
  isAdmin,
  canCreateTaxpayer,
  customerId,
}: {
  isAdmin: boolean;
  canCreateTaxpayer: boolean;
  customerId: string;
}) {
  const [customer, setCustomer] = useState(customerId);
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [customerLoadError, setCustomerLoadError] = useState("");
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [taxpayerName, setTaxpayerName] = useState("");
  const [taxpayerIdentifier, setTaxpayerIdentifier] = useState("");
  const [taxpayerKind, setTaxpayerKind] = useState("1");
  const [page, setPage] = useState(1);
  const [kind, setKind] = useState("all");
  const [query, setQuery] = useState("");
  const [items, setItems] = useState<PartyPage>(emptyPage);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [mine, setMine] = useState<Mine | null>(null);
  const [selected, setSelected] = useState<Party | null>(null);
  const [person, setPerson] = useState<Person | null>(null);
  const [details, setDetails] = useState<PartyDetails>(emptyDetails);

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

  const resetSelected = useCallback(() => {
    setSelected(null);
    setPerson(null);
    setDetails(emptyDetails());
  }, []);

  const resetPersonForm = useCallback(() => {
    setNational("");
    setFirst("");
    setLast("");
    setRegistration("");
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      if (!isAdmin) {
        setMine(await api<Mine>("mine"));
        return;
      }

      if (!uuid.test(customer)) {
        setItems(emptyPage());
        return;
      }

      const basePath = `customers/${customer}`;
      const partyQuery = new URLSearchParams({
        page: String(page),
        pageSize: String(PAGE_SIZE),
        query: query.trim(),
      });

      if (kind !== "all") {
        partyQuery.set("kind", kind);
      }

      const [parties, account, customerTaxpayers] = await Promise.all([
        api<PartyPage>(`${basePath}/parties?${partyQuery}`),
        api<Profile>(`${basePath}/profile`).catch(() => null),
        fetch(`/api/customer-owners/${customer}/taxpayers`, { cache: "no-store", credentials: "same-origin" })
          .then(async r => r.ok ? await r.json() as Taxpayer[] : []),
      ]);

      setItems(parties);
      setProfile(account);
      setTaxpayers(customerTaxpayers);

      if (selected && !parties.items.some((party) => party.id === selected.id)) {
        resetSelected();
      }
    } catch (exception) {
      setError((exception as Error).message);
    } finally {
      setLoading(false);
    }
  }, [customer, isAdmin, kind, page, query, resetSelected, selected]);

  useEffect(() => {
    if (!isAdmin) return;
    let active = true;
    fetch("/api/customer-owners/accounts", { credentials: "same-origin", cache: "no-store" })
      .then(async result => { if (!result.ok) throw new Error("فهرست مشتریان دریافت نشد."); return result.json() as Promise<CustomerOption[]>; })
      .then(rows => { if (active) setCustomers(rows); })
      .catch(() => { if (active) setCustomerLoadError("دریافت مشتریان ممکن نشد؛ از صفحه کاربران نرم‌افزار اقدام کنید."); });
    return () => { active = false; };
  }, [isAdmin]);

  useEffect(() => {
    void load();
  }, [load]);

  async function view(party: Party) {
    setSelected(party);
    setPerson(null);
    setError("");
    setNotice("");

    try {
      const partyPath = `customers/${customer}/parties/${party.id}`;
      const [record, partyDetails] = await Promise.all([
        api<Person>(`${partyPath}/${party.kind === 1 ? "natural" : "legal"}`),
        api<PartyDetails>(`${partyPath}/details`),
      ]);

      setPerson(record);
      setDetails(partyDetails);
      setNational(record.nationalCode ?? record.nationalIdentifier ?? "");
      setFirst(record.firstName ?? record.registeredName ?? "");
      setLast(record.lastName ?? "");
      setRegistration(record.registrationNumber ?? "");
    } catch (exception) {
      setError((exception as Error).message);
    }
  }

  async function apply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (busy || !uuid.test(customer)) {
      return;
    }

    setBusy(true);
    setError("");
    setNotice("");

    try {
      const type = selected?.kind ?? Number(newKind);
      const body =
        type === 1
          ? {
              nationalCode: national,
              firstName: first,
              lastName: last,
              expectedRevision: person?.revision ?? 0,
              archive: false,
            }
          : {
              nationalIdentifier: national,
              registeredName: first,
              registrationNumber: registration || null,
              expectedRevision: person?.revision ?? 0,
              archive: false,
            };
      const partyPath = `customers/${customer}/parties`;
      const path = `${partyPath}${selected ? `/${selected.id}` : ""}/${type === 1 ? "natural" : "legal"}`;

      await api(path, selected ? "PUT" : "POST", body);
      setNotice("اطلاعات ذخیره شد.");
      resetSelected();
      resetPersonForm();
      await load();
    } catch (exception) {
      setError((exception as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function createTaxpayer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canCreateTaxpayer || busy || !uuid.test(customer)) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const response = await fetch("/api/taxpayers/new", {
        method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ customerUserId: customer, kind: Number(taxpayerKind),
          identifier: taxpayerIdentifier.trim(), displayName: taxpayerName.trim() })
      });
      if (!response.ok) throw new Error(errors[response.status] ?? "ثبت مؤدی انجام نشد.");
      setTaxpayerName(""); setTaxpayerIdentifier("");
      setNotice("مؤدی برای حساب انتخاب‌شده ایجاد شد. این ثبت به معنی تأیید رسمی شناسه نیست.");
      await load();
    } catch (ex) { setError((ex as Error).message); }
    finally { setBusy(false); }
  }

  async function addContact(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selected || busy) {
      return;
    }

    setBusy(true);
    setError("");

    try {
      await api(
        `customers/${customer}/parties/${selected.id}/contacts`,
        "POST",
        {
          kind: Number(contactKind),
          value: contactValue,
          contactPerson: null,
          isPrimary: false,
        },
      );
      setContactValue("");
      setNotice("اطلاعات تماس ثبت شد.");
      await view(selected);
    } catch (exception) {
      setError((exception as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function addAddress(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selected || busy) {
      return;
    }

    setBusy(true);
    setError("");

    try {
      await api(
        `customers/${customer}/parties/${selected.id}/addresses`,
        "POST",
        {
          label: "اصلی",
          countryCode: "IR",
          province: addressProvince,
          city: addressCity,
          streetAddress: addressStreet,
          postalCode: addressPostal || null,
          isPrimary: false,
        },
      );
      setAddressCity("");
      setAddressProvince("");
      setAddressStreet("");
      setAddressPostal("");
      setNotice("آدرس ثبت شد.");
      await view(selected);
    } catch (exception) {
      setError((exception as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (!isAdmin) return (
      <section className="party-card" aria-busy={loading}>
        <h2>داشبورد مشتری</h2>
        <p className="identity-muted">
          مشاهده اطلاعات حساب و مؤدیان مجاز — عملیات مدیریتی غیرفعال است.
        </p>

        {error && <p className="party-error" role="alert">{error}</p>}
        {loading && <p role="status">در حال دریافت اطلاعات…</p>}

        {mine && (
          <>
            <p>نام حساب: {mine.displayName ?? "ثبت نشده"}</p>
            <p>تعداد ارتباط فعال مؤدیان: {mine.activeMembershipCount}</p>
            <p>وضعیت ویرایش: {mine.writesEnabled ? "فعال" : "فقط مشاهده"}</p>

            <h3>مؤدیان شما</h3>
            {mine.taxpayers.length === 0 ? (
              <p>مؤدی‌ای برای این حساب ثبت نشده است.</p>
            ) : (
              <ul>
                {mine.taxpayers.map((taxpayer) => (
                  <li key={taxpayer.id}>
                    {taxpayer.displayName} — {taxpayer.kind === 1 ? "حقیقی" : "حقوقی"}{" "}
                    <bdi dir="ltr">{taxpayer.id}</bdi>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </section>
  );

  const personKind = selected?.kind ?? Number(newKind);
  const isNaturalPerson = personKind === 1;

  return (
    <div className="party-grid">
      <section className="party-card">
        <h2>انتخاب مشتری و جست‌وجوی اشخاص</h2>

        <div className="party-onboarding"><strong>افزودن مشتری و مؤدی</strong>
          <p>ابتدا در بخش کاربران نرم‌افزار، حساب خریدار را ایجاد کنید. سپس همین‌جا شخص حقیقی/حقوقی را ثبت کنید. مؤدی مالیاتی مشتری را از بخش کاربران نرم‌افزار مدیریت کنید.</p>
          <Link href="/users" className="app-module-link">مدیریت حساب‌ها و مؤدیان ←</Link>
        </div>
        <label htmlFor="party-customer-choice">انتخاب مشتری از فهرست مجاز</label>
        <select id="party-customer-choice" value={customers.some(c => c.customerUserId === customer) ? customer : ""}
          onChange={event => { setCustomer(event.target.value); setPage(1); resetSelected(); }}>
          <option value="">انتخاب مشتری…</option>
          {customers.map(c => <option key={c.customerUserId} value={c.customerUserId}>
            {c.mobile || c.email || c.customerUserId} — {c.taxpayerCount} مؤدی
          </option>)}
        </select>
        {customerLoadError && <p role="alert" className="party-error">{customerLoadError}</p>}
        <label htmlFor="party-customer">شناسه حساب مشتری</label>
        <input
          id="party-customer"
          dir="ltr"
          value={customer}
          onChange={(event) => {
            setCustomer(event.target.value);
            setPage(1);
            resetSelected();
          }}
          aria-invalid={!uuid.test(customer)}
        />

        <form
          onSubmit={(event) => {
            event.preventDefault();
            setPage(1);
            void load();
          }}
        >
          <label htmlFor="party-query">جست‌وجو بر اساس نام یا شناسه</label>
          <input id="party-query" value={query} onChange={(event) => setQuery(event.target.value)} />

          <label htmlFor="party-kind">نوع شخص</label>
          <select
            id="party-kind"
            value={kind}
            onChange={(event) => {
              setKind(event.target.value);
              setPage(1);
            }}
          >
            <option value="all">همه</option>
            <option value="1">حقیقی</option>
            <option value="2">حقوقی</option>
          </select>

          <button className="identity-secondary" type="submit" disabled={loading}>
            بازخوانی
          </button>
        </form>

        {error && <p className="party-error" role="alert">{error}</p>}
        {notice && <p className="party-success" role="status">{notice}</p>}
        {loading && <p role="status">در حال بارگذاری…</p>}
        {profile && (
          <p>
            پروفایل حساب: {profile.displayName} — {profile.status === 1 ? "فعال" : "تعلیق"}
          </p>
        )}
        <p>تعداد اشخاص: {items.total}</p>
        {uuid.test(customer) && <div className="party-taxpayer-preview"><h3>مؤدیان این مشتری ({taxpayers.length})</h3>
          {taxpayers.length ? <ul>{taxpayers.map(t => <li key={t.id}>{t.displayName} — {t.kind === 1 ? "حقیقی" : "حقوقی"}</li>)}</ul> : <p>مؤدی مرتبط ثبت نشده است.</p>}
          {canCreateTaxpayer ? <form className="party-form" onSubmit={createTaxpayer}>
            <label htmlFor="taxpayer-new-kind">نوع مؤدی</label>
            <select id="taxpayer-new-kind" value={taxpayerKind} onChange={e => setTaxpayerKind(e.target.value)}>
              <option value="1">حقیقی</option><option value="2">حقوقی</option>
            </select>
            <label htmlFor="taxpayer-new-identifier">کد/شناسه ملی</label>
            <input id="taxpayer-new-identifier" required dir="ltr" value={taxpayerIdentifier} onChange={e => setTaxpayerIdentifier(e.target.value)}/>
            <label htmlFor="taxpayer-new-name">نام نمایشی مؤدی</label>
            <input id="taxpayer-new-name" required value={taxpayerName} onChange={e => setTaxpayerName(e.target.value)}/>
            <button type="submit" disabled={busy}>ثبت مؤدی برای این مشتری</button>
          </form> : <p className="identity-muted">ثبت مؤدی طبق مجوز فعلی فاز ۴ مختص ادمین سطح ۱ است؛ سطح ۲ می‌تواند مؤدیان مشتریان خودش را مشاهده کند.</p>}
          <Link href="/users" className="app-module-link">بازگشت به مدیریت حساب‌های نرم‌افزار ←</Link>
        </div>}

        <ul className="party-list">
          {items.items.map((party) => (
            <li key={party.id}>
              <button
                type="button"
                onClick={() => void view(party)}
                aria-pressed={selected?.id === party.id}
              >
                {party.displayName}{" "}
                <small>
                  {party.kind === 1 ? "حقیقی" : "حقوقی"} · نسخه {party.revision}
                </small>
              </button>
            </li>
          ))}
        </ul>

        {!loading && items.items.length === 0 && <p>موردی یافت نشد.</p>}

        <div className="party-actions">
          <button disabled={page === 1 || loading} onClick={() => setPage((current) => current - 1)}>
            قبلی
          </button>
          <span>صفحه {page}</span>
          <button
            disabled={loading || page * PAGE_SIZE >= items.total}
            onClick={() => setPage((current) => current + 1)}
          >
            بعدی
          </button>
        </div>
      </section>

      <section className="party-card">
        <div className="party-actions">
          <h2>{selected ? "ویرایش شخص" : "ثبت شخص جدید"}</h2>
          <button
            type="button"
            onClick={() => {
              resetSelected();
              resetPersonForm();
            }}
          >
            جدید
          </button>
        </div>

        <form onSubmit={apply} className="party-form">
          {!selected && (
            <>
              <label htmlFor="party-new-kind">نوع شخص</label>
              <select
                id="party-new-kind"
                value={newKind}
                onChange={(event) => setNewKind(event.target.value)}
              >
                <option value="1">حقیقی</option>
                <option value="2">حقوقی</option>
              </select>
            </>
          )}

          <label htmlFor="party-national">{isNaturalPerson ? "کد ملی" : "شناسه ملی"}</label>
          <input
            id="party-national"
            dir="ltr"
            required
            value={national}
            onChange={(event) => setNational(event.target.value)}
          />

          <label htmlFor="party-first">{isNaturalPerson ? "نام" : "نام ثبتی"}</label>
          <input
            id="party-first"
            required
            maxLength={256}
            value={first}
            onChange={(event) => setFirst(event.target.value)}
          />

          {isNaturalPerson ? (
            <>
              <label htmlFor="party-last">نام خانوادگی</label>
              <input
                id="party-last"
                required
                maxLength={100}
                value={last}
                onChange={(event) => setLast(event.target.value)}
              />
            </>
          ) : (
            <>
              <label htmlFor="party-reg">شماره ثبت (اختیاری)</label>
              <input
                id="party-reg"
                dir="ltr"
                value={registration}
                onChange={(event) => setRegistration(event.target.value)}
              />
            </>
          )}

          <button
            className="identity-primary"
            disabled={busy || !uuid.test(customer) || (!!selected && !person)}
          >
            {busy ? "در حال ذخیره…" : selected ? "ثبت ویرایش" : "ایجاد شخص"}
          </button>
        </form>

        {selected && person && <InvoiceParticipantForm key={selected.id} customerId={customer} subjectId={selected.id} role="buyer" />}
        {selected && person && (
          <div className="party-details">
            <h3>اطلاعات تماس</h3>
            <ul>
              {details.contacts.map((contact) => (
                <li key={contact.id}>
                  <bdi dir="ltr">{contact.value}</bdi>
                  {contact.isPrimary ? " · اصلی" : ""}
                </li>
              ))}
            </ul>

            <form className="party-form" onSubmit={addContact}>
              <label htmlFor="party-contact-kind">نوع تماس</label>
              <select
                id="party-contact-kind"
                value={contactKind}
                onChange={(event) => setContactKind(event.target.value)}
              >
                <option value="1">موبایل</option>
                <option value="2">تلفن</option>
                <option value="3">ایمیل</option>
              </select>

              <label htmlFor="party-contact-value">شماره یا ایمیل</label>
              <input
                id="party-contact-value"
                required
                dir="ltr"
                value={contactValue}
                onChange={(event) => setContactValue(event.target.value)}
              />
              <button type="submit" disabled={busy}>افزودن تماس</button>
            </form>

            <h3>آدرس‌ها</h3>
            <ul>
              {details.addresses.map((address) => (
                <li key={address.id}>
                  {address.label} — {address.province}، {address.city}، {address.streetAddress}
                </li>
              ))}
            </ul>

            <form className="party-form" onSubmit={addAddress}>
              <label htmlFor="party-province">استان</label>
              <input
                id="party-province"
                required
                value={addressProvince}
                onChange={(event) => setAddressProvince(event.target.value)}
              />

              <label htmlFor="party-city">شهر</label>
              <input
                id="party-city"
                required
                value={addressCity}
                onChange={(event) => setAddressCity(event.target.value)}
              />

              <label htmlFor="party-street">نشانی</label>
              <textarea
                id="party-street"
                required
                minLength={3}
                value={addressStreet}
                onChange={(event) => setAddressStreet(event.target.value)}
              />

              <label htmlFor="party-postal">کد پستی (اختیاری)</label>
              <input
                id="party-postal"
                dir="ltr"
                value={addressPostal}
                onChange={(event) => setAddressPostal(event.target.value)}
              />
              <button type="submit" disabled={busy}>افزودن آدرس</button>
            </form>
          </div>
        )}
      </section>
    </div>
  );
}
