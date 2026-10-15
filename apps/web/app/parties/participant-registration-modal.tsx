"use client";
import { useEffect, useRef, useState, type FormEvent } from "react";
import InvoiceParticipantForm from "./invoice-participant-form";
import TaxpayerWorkspace from "../taxpayers/taxpayer-workspace";

type Buyer = { customerUserId: string; mobile: string | null; email: string | null;
  isActive: boolean; rootLevel1UserId: string | null; partnerLevel2UserId: string | null };
type Mode = "buyer" | "seller";
type Created = { id: string };
const normalize = (s: string) => s.trim();
const errorMessage = (code: number) => code === 400 ? "اطلاعات هویتی نامعتبر است؛ شناسه و نام را بررسی کنید." :
  code === 401 ? "نشست منقضی شده است." : code === 403 ? "مجوز دسترسی ندارید." :
  code === 409 ? "این شناسه قبلاً ثبت شده است؛ پرونده موجود را انتخاب کنید." :
  "عملیات انجام نشد؛ وضعیت سرور را بررسی کنید.";

export default function ParticipantRegistrationModal({ mode, buyers, level1, actorUserId, onClose, onCompleted,
  initialCustomerId = "", initialSubjectId = "" }: {
  mode: Mode; buyers: Buyer[]; level1: boolean; actorUserId: string; onClose: () => void;
  onCompleted: () => Promise<void>; initialCustomerId?: string; initialSubjectId?: string;
}) {
  const [customerId, setCustomerId] = useState(initialCustomerId);
  const [kind, setKind] = useState("1");
  const [identifier, setIdentifier] = useState("");
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [registrationNumber, setRegistrationNumber] = useState("");
  const [subjectId, setSubjectId] = useState(initialSubjectId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const dialog = useRef<HTMLDivElement>(null);
  const busyRef = useRef(false);
  busyRef.current = busy;
  const closeRef = useRef<HTMLButtonElement>(null);
  const complete = Boolean(subjectId);
  const chosenCustomer = buyers.find(b => b.customerUserId === customerId);
  const authorizedCustomer = Boolean(chosenCustomer?.isActive &&
    (level1 ? chosenCustomer.rootLevel1UserId === actorUserId :
      chosenCustomer.partnerLevel2UserId === actorUserId));

  useEffect(() => {
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeRef.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busyRef.current) onClose();
      if (event.key === "Tab" && dialog.current) {
        const targets = Array.from(dialog.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),a[href]'));
        if (!targets.length) return;
        const first = targets[0], last = targets[targets.length - 1];
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault(); last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault(); first.focus();
        }
      }
    };
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("keydown", onKey); previous?.focus(); };
  }, []);

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || !authorizedCustomer || (mode === "seller" && !level1)) {
      setError("مالکیت تجاری حساب انتخاب‌شده به ادمین فعلی اختصاص ندارد. ابتدا انتساب مشتری را در بخش کاربران بررسی کنید.");
      return;
    }
    setBusy(true); setError("");
    try {
      const isNatural = kind === "1";
      const path = mode === "seller" ? "/api/taxpayers/new" :
        "/api/party-workspace/customers/" + customerId + "/parties/" + (isNatural ? "natural" : "legal");
      const payload = mode === "seller" ? {
        customerUserId: customerId, kind: Number(kind), identifier: normalize(identifier),
        displayName: normalize(firstName + (isNatural ? " " + lastName : ""))
      } : isNatural ? {
        nationalCode: normalize(identifier), firstName: normalize(firstName),
        lastName: normalize(lastName), expectedRevision: 0, archive: false
      } : {
        nationalIdentifier: normalize(identifier), registeredName: normalize(firstName),
        registrationNumber: normalize(registrationNumber) || null, expectedRevision: 0, archive: false
      };
      const response = await fetch(path, { method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
      if (!response.ok) {
        if (response.status === 403) {
          const diagnostic = await response.json().catch(() => null) as { error?: string } | null;
          throw new Error(diagnostic?.error === "origin_not_allowed"
            ? "درخواست در کنترل امنیتی آدرس مرورگر رد شد. پنل را با آدرس محلی صحیح باز کنید."
            : "بک‌اند اجازه ثبت را نداد. وضعیت MFA و انتساب مالکیت تجاری مشتری به ادمین فعلی را بررسی کنید.");
        }
        throw new Error(errorMessage(response.status));
      }
      const created = await response.json() as Created;
      if (!created.id) throw new Error("شناسه ثبت‌شده دریافت نشد؛ قبل از تلاش مجدد، فهرست را بازخوانی کنید.");
      setSubjectId(created.id);
      await onCompleted();
    } catch (ex) { setError((ex as Error).message); }
    finally { setBusy(false); }
  }

  return <div className="argin-modal-backdrop" onMouseDown={event => {
      if (event.target === event.currentTarget && !busy) onClose();
    }}>
    <div className="argin-modal" role="dialog" aria-modal="true"
      aria-label={mode === "buyer" ? "افزودن خریدار صورتحساب" : "افزودن فروشنده / مؤدی"}
      ref={dialog}>
      <header className="argin-modal-header">
        <div><h2>{mode === "buyer" ? "افزودن خریدار صورتحساب" : "افزودن فروشنده / مؤدی"}</h2>
          <p>{complete ? "مرحله دوم: اطلاعات سربرگ و حافظه مالیاتی" :
            "مرحله اول: اطلاعات هویتی طرف صورتحساب (بدون ایجاد حساب ورود)"}</p></div>
        <button ref={closeRef} type="button" className="argin-modal-close"
          disabled={busy} onClick={onClose} aria-label="بستن پنجره">×</button>
      </header>
      <div className="argin-modal-body">
        {!complete ? <form className="invoice-fields" onSubmit={create}>
          <label>حساب مشتری / مالک پرونده
            <select value={customerId} onChange={e => setCustomerId(e.target.value)} required>
              <option value="">حساب مرتبط را انتخاب کنید</option>
              {buyers.map(b => {
                const permitted = b.isActive && (level1 ?
                  b.rootLevel1UserId === actorUserId : b.partnerLevel2UserId === actorUserId);
                return <option value={b.customerUserId} key={b.customerUserId} disabled={!permitted}>
                  {b.mobile || b.email || b.customerUserId}
                  {!permitted ? " — بدون انتساب مجاز / غیرفعال" : ""}
                </option>;
              })}
            </select>
          </label>
          {customerId && !authorizedCustomer && <p role="alert" className="party-error">
            این حساب در محدوده مالکیت تجاری ادمین فعلی نیست. از بخش کاربران نرم‌افزار، انتساب تجاری مشتری را بررسی کنید.
          </p>}
          {buyers.every(b => !b.isActive || (level1 ? b.rootLevel1UserId !== actorUserId : b.partnerLevel2UserId !== actorUserId)) &&
            <p role="alert" className="party-error">در فهرست فعلی مشتری مجاز برای ثبت وجود ندارد. ابتدا انتساب تجاری مشتری را بررسی کنید.</p>}
          <label>نوع شخصیت
            <select value={kind} onChange={e => setKind(e.target.value)}>
              <option value="1">شخص حقیقی</option><option value="2">شخص حقوقی (شرکت)</option>
            </select>
          </label>
          <label>{kind === "1" ? "کد ملی" : "شناسه ملی شرکت"}
            <input required inputMode="numeric" dir="ltr" value={identifier}
              onChange={e => setIdentifier(e.target.value)} maxLength={32}/>
          </label>
          <label>{kind === "1" ? "نام" : "نام ثبتی شرکت"}
            <input required value={firstName} maxLength={256}
              onChange={e => setFirstName(e.target.value)}/>
          </label>
          {kind === "1" ? <label>نام خانوادگی
            <input required value={lastName} maxLength={100}
              onChange={e => setLastName(e.target.value)}/></label> :
            <label>شماره ثبت شرکت (در صورت وجود)
              <input value={registrationNumber} maxLength={32} dir="ltr"
                onChange={e => setRegistrationNumber(e.target.value)}/></label>}
          <p className="field-hint">اطلاعات تکمیلی مانند شماره اقتصادی، کد شعبه، کد پستی، نشانی و تلفن در مرحله بعد ذخیره می‌شوند. الزامات نهایی به نوع و الگوی صورتحساب وابسته است.</p>
          {mode === "seller" && !level1 && <p role="alert" className="party-error">
            طبق مجوز فعلی، ایجاد مؤدی مالیاتی فقط برای ادمین سطح یک مجاز است.</p>}
          {error && <p role="alert" className="party-error">{error}</p>}
          <button className="identity-primary" type="submit"
            disabled={busy || !authorizedCustomer || (mode === "seller" && !level1)}>
            {busy ? "در حال ثبت…" : "ثبت هویت و ادامه به مشخصات مالیاتی"}</button>
        </form> : <>
          <p className="party-success" role="status">پرونده هویتی ثبت شد. اکنون اطلاعات مالیاتی را تکمیل و ذخیره کنید.</p>
          <InvoiceParticipantForm key={subjectId} customerId={customerId}
            subjectId={subjectId} role={mode === "buyer" ? "buyer" : "seller"}/>
          {mode === "seller" && <div className="argin-memory-step">
            <h3>حافظه مالیاتی و اطلاعات گواهی</h3>
            <p>ثبت شناسه حافظه و فراداده گواهی مستقل از ذخیره مشخصات فروشنده است؛ تأیید یا اتصال واقعی به سازمان در این مرحله انجام نمی‌شود.</p>
            <TaxpayerWorkspace key={subjectId} customerId={customerId} isLevel1={level1}
              initialTaxpayerId={subjectId} showCreateForm={false}/>
          </div>}
          <button className="identity-secondary" type="button" onClick={() => {
            void onCompleted(); onClose();
          }}>پایان و بازگشت به فهرست</button>
        </>}
      </div>
    </div>
  </div>;
}
