"use client";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import InvoiceParticipantForm from "./invoice-participant-form";
import TaxpayerWorkspace from "../taxpayers/taxpayer-workspace";
import ConsoleLoader from "../components/console-loader";

type FiscalRow = { id: string; buyerId: string; type: "buyer" | "taxpayer";
  kind: number | null; name: string; identifier: string; status: string };
type BuyerDetails = { id: string; revision: number; firstName?: string; lastName?: string;
  registeredName?: string; nationalCode?: string; nationalIdentifier?: string;
  registrationNumber?: string | null };
type Taxpayer = { id: string; kind: number; displayName: string; normalizedIdentifier: string;
  status: number; revision: number };
type Mode = "view" | "edit" | "archive";
const errorText = (status: number) => status === 403 ? "دسترسی ویرایش این پرونده را ندارید." :
  status === 404 ? "این پرونده در محدوده دسترسی فعلی یافت نشد." :
  status === 409 ? "این پرونده در حال استفاده، تغییرکرده یا وابسته به سوابق دیگر است. مجدداً بارگذاری کنید." :
  "ثبت تغییرات ناموفق بود.";

export default function FiscalRecordModal({ record, isLevel1, onClose, onChanged }:
  { record: FiscalRow; isLevel1: boolean; onClose: () => void; onChanged: () => Promise<void> }) {
  const [person, setPerson] = useState<BuyerDetails | Taxpayer | null>(null);
  const [mode, setMode] = useState<Mode>("view");
  const [name, setName] = useState(""); const [family, setFamily] = useState("");
  const [identifier, setIdentifier] = useState(""); const [registration, setRegistration] = useState("");
  const [buyerKind, setBuyerKind] = useState(record.kind ?? 1);
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true);
  const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const closeRef = useRef<HTMLButtonElement>(null); const dialog = useRef<HTMLDivElement>(null);
  const title = record.type === "buyer" ? "خریدار" : "مؤدی";
  const buyerPath = "/api/party-workspace/customers/" + record.buyerId + "/parties/" +
    record.id + (record.kind === 1 ? "/natural" : "/legal");
  const source = record.type === "buyer" ? buyerPath : "/api/taxpayers/" + record.id;
  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const res = await fetch(source, { credentials: "same-origin", cache: "no-store" });
      if (!res.ok) throw new Error(errorText(res.status));
      const raw = await res.text();
      if (!raw.trim()) throw new Error("سرویس پرونده پاسخ خالی برگرداند؛ API را بررسی کنید.");
      let details: BuyerDetails & Taxpayer;
      try { details = JSON.parse(raw) as BuyerDetails & Taxpayer; }
      catch { throw new Error("پاسخ پرونده JSON معتبر نیست؛ API را بررسی کنید."); }
      if (!details || details.id !== record.id)
        throw new Error("اطلاعات برگشتی با شناسه پرونده انتخاب‌شده مطابقت ندارد.");
      setPerson(details);
      setBuyerKind(record.kind ?? 1);
      setName(record.type === "buyer"
        ? (record.kind === 1 ? details.firstName || "" : details.registeredName || "")
        : details.displayName);
      setFamily(details.lastName || "");
      setIdentifier(details.nationalCode || details.nationalIdentifier || details.normalizedIdentifier || "");
      setRegistration(details.registrationNumber || "");
    } catch (ex) { setError((ex as Error).message); }
    finally { setLoading(false); }
  }, [source, record.type, record.kind]);
  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const before = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeRef.current?.focus();
    const handle = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) onClose();
      if (event.key === "Tab" && dialog.current) {
        const elements = Array.from(dialog.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled])'));
        if (!elements.length) return;
        if (event.shiftKey && document.activeElement === elements[0]) {
          event.preventDefault(); elements[elements.length - 1].focus();
        } else if (!event.shiftKey && document.activeElement === elements[elements.length - 1]) {
          event.preventDefault(); elements[0].focus();
        }
      }
    };
    window.addEventListener("keydown", handle);
    return () => { window.removeEventListener("keydown", handle); before?.focus(); };
  }, [busy, onClose]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!person || busy) return;
    setBusy(true); setError(""); setNotice("");
    try {
      let res: Response;
      if (record.type === "buyer") {
        const body = {
          kind: buyerKind,
          identifier,
          name,
          lastName: buyerKind === 1 ? family : null,
          registrationNumber: buyerKind === 2 ? registration || null : null,
          expectedRevision: person.revision
        };
        res = await fetch(mode === "archive" ? buyerPath :
          "/api/party-workspace/customers/" + record.buyerId +
          "/parties/" + record.id + "/identity", {
          method: "PUT", credentials: "same-origin",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(mode === "archive" ?
            (record.kind === 1 ? {
              nationalCode: identifier, firstName: name, lastName: family,
              expectedRevision: person.revision, archive: true
            } : {
              nationalIdentifier: identifier, registeredName: name,
              registrationNumber: registration || null,
              expectedRevision: person.revision, archive: true
            }) : body)
        });
      } else {
        if (mode === "archive") {
          res = await fetch("/api/taxpayers/" + record.id + "/archive", {
            method: "POST", credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ expectedRevision: person.revision }) });
        } else {
          res = await fetch("/api/taxpayers/" + record.id + "/display-name", {
            method: "PATCH", credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ displayName: name, expectedRevision: person.revision }) });
        }
      }
      if (!res.ok) throw new Error(errorText(res.status));
      setMode("view"); setNotice(mode === "archive"
        ? "پرونده بایگانی شد؛ سوابق آن به‌صورت فیزیکی حذف نشده‌اند."
        : "اطلاعات پرونده ویرایش شد.");
      await onChanged();
      onClose();
    } catch (ex) { setError((ex as Error).message); }
    finally { setBusy(false); }
  }
  return <div className="argin-modal-backdrop" onMouseDown={e => {
    if (e.target === e.currentTarget && !busy) onClose();
  }}>
    <div className="argin-modal" role="dialog" aria-modal="true"
      aria-label={"پرونده " + title + " — " + record.name} ref={dialog}>
      <header className="argin-modal-header">
        <div><h2>{mode === "archive" ? "بایگانی " : mode === "edit" ? "ویرایش " : "مشاهده پرونده "}
          {title} — {record.name}</h2>
          <p>این پرونده مستقل از حساب ورود کاربر نرم‌افزار است.</p></div>
        <button type="button" ref={closeRef} className="argin-modal-close"
          onClick={onClose} aria-label="بستن">×</button>
      </header>
      <div className="argin-modal-body">
        {loading ? <ConsoleLoader label={"در حال دریافت پرونده " + title + "…"} /> : person && <>
          {mode === "view" && <div className="console-directory-actions">
            {record.status === "فعال" && <button type="button" className="identity-primary" onClick={() => setMode("edit")}>ویرایش {title}</button>}
            {record.status === "فعال" && <button type="button" className="identity-secondary" onClick={() => setMode("archive")}>بایگانی {title}</button>}
            <button type="button" className="identity-secondary" onClick={() => void load()}>بازخوانی پرونده</button>
          </div>}
          {mode === "edit" && record.type === "buyer" && <p className="field-hint">نام، شناسه قانونی و نوع شخصیت قابل ویرایش‌اند. پیش از هر تغییر، نسخه قبلی هویت در تاریخچه مستقل ثبت می‌شود. صورتحساب صادرشده باید Snapshot اختصاصی زمان صدور را نگهداری کند.</p>}
          {mode === "archive" && <p className="invoice-missing">
            این عملیات حذف فیزیکی نیست. شناسه و سوابق پرونده باقی می‌مانند. برای خریدار یا مؤدی دارای صورتحساب،
            تغییر اطلاعات فعلی نباید اطلاعات ثبت‌شده در صورتحساب قبلی را بازنویسی کند.
          </p>}
          {mode !== "view" && <form className="invoice-fields" onSubmit={save}>
            {record.type === "buyer" && mode !== "archive" && <label>نوع شخصیت خریدار
              <select value={buyerKind} onChange={e => { setBuyerKind(Number(e.target.value));
                setIdentifier(""); setName(""); setFamily(""); setRegistration(""); }}>
                <option value={1}>حقیقی</option><option value={2}>حقوقی</option>
              </select>
              <small>با تغییر نوع شخصیت، نام و شناسه معتبر متناسب با نوع جدید وارد کنید.</small>
            </label>}
            <label>{record.type === "buyer" && buyerKind === 1 ? "نام" : "نام / عنوان"}
              <input value={name} onChange={e => setName(e.target.value)} required
                disabled={mode === "archive"} maxLength={256}/></label>
            {record.type === "buyer" && buyerKind === 1 && <label>نام خانوادگی
              <input value={family} onChange={e => setFamily(e.target.value)}
                required disabled={mode === "archive"} maxLength={100}/></label>}
            {record.type === "buyer" && <label>{buyerKind === 1 ? "کد ملی" : "شناسه ملی"}
              <input dir="ltr" value={identifier} onChange={e => setIdentifier(e.target.value)}
                required disabled={mode === "archive"}/></label>}
            {record.type === "buyer" && buyerKind === 2 && <label>شماره ثبت
              <input dir="ltr" value={registration} onChange={e => setRegistration(e.target.value)}
                disabled={mode === "archive"}/></label>}
            <button className="identity-primary" type="submit" disabled={busy}>
              {busy ? "در حال ثبت…" : mode === "archive" ? "تأیید بایگانی " + title : "ذخیره ویرایش " + title}
            </button>
            <button type="button" className="identity-secondary" onClick={() => setMode("view")}>انصراف</button>
          </form>}
          {mode === "view" && <>
            <dl className="taxpayer-facts">
              <div><dt>نام / عنوان</dt><dd>{record.name}</dd></div>
              <div><dt>نوع شخصیت</dt><dd>{record.kind === 1 ? "حقیقی" : "حقوقی"}</dd></div>
              <div><dt>شناسه قانونی</dt><dd dir="ltr">{identifier}</dd></div>
              <div><dt>شناسه پرونده</dt><dd dir="ltr">{record.id}</dd></div>
            </dl>
            <InvoiceParticipantForm key={record.id} customerId={record.buyerId} subjectId={record.id}
              role={record.type === "buyer" ? "buyer" : "seller"} personKind={record.kind ?? undefined}/>
            {record.type === "taxpayer" && <section className="argin-memory-step">
              <h3>حافظه و مشخصات اتصال مؤدی</h3>
              <TaxpayerWorkspace initialTaxpayerId={record.id} customerId={record.buyerId}
                showCreateForm={false} isLevel1={isLevel1}/>
            </section>}
          </>}
        </>}
        {error && <p role="alert" className="party-error">{error}</p>}
        {notice && <p role="status" className="party-success">{notice}</p>}
      </div>
    </div>
  </div>;
}
