"use client";

import {redirectForInvoiceSubscription} from "../../lib/invoice-subscription";
import { useEffect, useRef, useState } from "react";
import InvoiceCorrectionModal from "./invoice-correction-modal";

type Line = {
  description: string;
  officialIdentifier: string | null;
  quantity: number;
  unitPriceRial: number;
  discountRial: number;
  grossRial?:number;
  netRial: number;
  vatRatePercent: number;
  vatRial: number;
  totalRial: number;
};
type Participant={name:string|null;identity:string|null;economicCode:string|null;
 registrationNumber:string|null;branchCode:string|null;postalCode:string|null;
 province:string|null;city:string|null;address:string|null;phone:string|null;kind:string|null};

type Invoice = {
  issueDateUtc: string;
  revision: number;
  uniqueInvoiceId: string | null;
  referenceFiscalTaxId?:string|null;
  invoiceLogoDataUrl:string|null;
  invoiceType:string|null;
  pattern:string|null;
  seller: string | null;
  buyer: string | null;
  sellerDetails:Participant|null;
  buyerDetails:Participant|null;
  description: string | null;
  contractNumber: string | null;
  settlementMethod: string | null;
  lines: Line[];
  grossRial: number;
  discountRial: number;
  netRial: number;
  vatRial: number;
  totalRial: number;
  otherTaxAndLegalFeesRial:number;
  cashAmountRial:number|null;
  creditAmountRial:number|null;
  paymentDateUtc:string|null;
};

type InvoiceFormalPreviewProps = {
  taxpayerId: string;
  invoiceId: string;
  reference: string;
  authorityStatus?: string;
  subject?: string;
  readOnly?: boolean;
  authorityReference?: string|null;
  referenceFiscalTaxId?:string|null;
  printLayout?:boolean;
  invoiceSubject?:string;
  onPreviewReady?:()=>void;
  onPreviewError?:(message:string)=>void;
  onCancellationCompleted?:()=>void;
  onCorrectionCompleted?:()=>void;
  onSalesReturn?:()=>void;
};

const money = (value: number) =>
  value.toLocaleString("fa-IR", { maximumFractionDigits: 2 });

const formatIssueDate = (value: string) =>
  new Date(value).toLocaleDateString("fa-IR-u-ca-persian");

const previewStyle = {
  border: "1px solid var(--border,#d2d9df)",
  borderRadius: 10,
  padding: 10,
  marginBlock: 8,
  fontSize: 12,
  lineHeight: 1.5,
};

const detailsGridStyle = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit,minmax(150px,1fr))",
  gap: 5,
  marginBottom: 8,
};

const totalsGridStyle = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit,minmax(180px,1fr))",
  gap: 8,
};

const loadingStyle = {
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  gap: 10,
  padding: 20,
};

const actionsStyle = {
  display: "flex",
  gap: 8,
  flexWrap: "wrap" as const,
  marginTop: 10,
};

async function fetchInvoicePreview(
  taxpayerId: string,
  invoiceId: string,
  signal: AbortSignal,
): Promise<Invoice> {
  const path =
    "/api/invoice-dispatch/" +
    encodeURIComponent(taxpayerId) +
    "/" +
    encodeURIComponent(invoiceId) +
    "/print-preview";

  const response = await fetch(path, {
    cache: "no-store",
    credentials: "same-origin",
    signal,
  });

  if (!response.ok) {
    throw Error(
      response.status === 401
        ? "نشست ورود منقضی شده است."
        : "نمای صورتحساب در دسترس نیست.",
    );
  }

  return (await response.json()) as Invoice;
}

function InvoiceHeader({
  invoice,
  reference,
  authorityReference,
}: {
  invoice: Invoice;
  reference: string;
  authorityReference?:string|null;
}) {
  return (
    <>

      <div style={detailsGridStyle}>
        <span>
          فروشنده: <strong>{invoice.seller ?? "—"}</strong>
        </span>
        <span>
          خریدار: <strong>{invoice.buyer ?? "—"}</strong>
        </span>
        <span>
          تاریخ: <strong>{formatIssueDate(invoice.issueDateUtc)}</strong>
        </span>
        <span className="invoice-tax-identifiers">
          <span>شناسه منحصربه‌فرد مالیاتی: <bdi dir="ltr">
            {invoice.uniqueInvoiceId ?? "در انتظار تخصیص"}
          </bdi></span>
          {authorityReference!==undefined&&<span>شماره مرجع: <bdi dir="ltr">{authorityReference??"—"}</bdi></span>}
        </span>
        {reference && (
          <span>
            شماره صورتحساب: <strong dir="ltr">{reference}</strong>
          </span>
        )}
        {invoice.contractNumber && (
          <span>
            شماره قرارداد: <strong dir="ltr">{invoice.contractNumber}</strong>
          </span>
        )}
      </div>
    </>
  );
}

const paperDate=(value:string|null|undefined)=>value
 ?new Date(value).toLocaleString("fa-IR-u-ca-persian",{year:"numeric",month:"2-digit",day:"2-digit",hour:"2-digit",minute:"2-digit"})
 :"—";
const faDigits=(value:string|number|null|undefined)=>value==null?"—":String(value).replace(/[0-9]/g,d=>"۰۱۲۳۴۵۶۷۸۹"[Number(d)]);
const underThousandWords=(value:number):string=>{
 const ones=["","یک","دو","سه","چهار","پنج","شش","هفت","هشت","نه","ده","یازده","دوازده","سیزده","چهارده","پانزده","شانزده","هفده","هجده","نوزده"];
 const tens=["","","بیست","سی","چهل","پنجاه","شصت","هفتاد","هشتاد","نود"];
 const hundreds=["","یکصد","دویست","سیصد","چهارصد","پانصد","ششصد","هفتصد","هشتصد","نهصد"];
 const parts:string[]=[];const h=Math.floor(value/100),rest=value%100;
 if(h)parts.push(hundreds[h]);
 if(rest<20){if(rest)parts.push(ones[rest]);}
 else{parts.push(tens[Math.floor(rest/10)]);if(rest%10)parts.push(ones[rest%10]);}
 return parts.join(" و ");
};
const rialInWords=(value:number):string=>{
 if(!Number.isFinite(value))return "—";
 let amount=Math.round(Math.abs(value));
 if(amount===0)return "صفر ریال";
 const scales=["","هزار","میلیون","میلیارد","تریلیون","کوادریلیون"];
 const parts:string[]=[];let group=0;
 while(amount>0&&group<scales.length){
  const chunk=amount%1000;
  if(chunk)parts.unshift([underThousandWords(chunk),scales[group]].filter(Boolean).join(" "));
  amount=Math.floor(amount/1000);group++;
 }
 return (value<0?"منفی ":"")+parts.join(" و ")+" ریال";
};
const paperSubject:Record<string,string>={Original:"اصلی",Correction:"اصلاحی",Cancellation:"ابطالی",SalesReturn:"برگشت از فروش"};
const invoiceTypeLabel:Record<string,string>={"1":"نوع اول","2":"نوع دوم","3":"نوع سوم"};

function PaperParticipant({title,participant,fallbackName,fallbackIdentity}: {
 title:string;participant:Participant|null;fallbackName:string|null;fallbackIdentity:string|null
}){
 const p=participant;
 const fields:[string,string|null|undefined][]=[
  ["کد اقتصادی",p?.economicCode],["شماره ثبت",p?.registrationNumber],
  ["شناسه ملی",p?.identity??fallbackIdentity],["شماره شعبه",p?.branchCode],
  ["استان",p?.province],["شهرستان",p?.city],["کدپستی",p?.postalCode],
  ["تلفن",p?.phone]
 ];
 return <section className="invoice-paper__party" aria-label={title}>
  <h4 className="invoice-paper__section-title">{title}</h4>
  <div className="invoice-paper__party-grid">
   <div className="invoice-paper__cell invoice-paper__cell--wide"><strong>نام شخص {p?.kind==="NaturalPerson"?"حقیقی":"حقیقی / حقوقی"}:</strong><span>{p?.name??fallbackName??"—"}</span></div>
   {fields.map(([label,value])=><div className="invoice-paper__cell" key={label}><strong>{label}:</strong><span>{faDigits(value)}</span></div>)}
   <div className="invoice-paper__cell invoice-paper__cell--wide"><strong>نشانی:</strong><span>{p?.address??"—"}</span></div>
  </div>
 </section>;
}

function InvoicePaper({invoice,reference,referenceFiscalTaxId,invoiceSubject}: {
 invoice:Invoice;reference:string;referenceFiscalTaxId?:string|null;invoiceSubject?:string
}){
 const lineCount=Math.max(5,invoice.lines.length);
 return <div className="invoice-paper" dir="rtl">
  <header className="invoice-paper__header">
   <div className="invoice-paper__tax-number"><strong>شماره مالیاتی</strong>
    <span>شماره منحصربه‌فرد مالیاتی: <bdi>{faDigits(invoice.uniqueInvoiceId)}</bdi></span>
    <span>شماره مرجع: <bdi dir="ltr">{invoiceSubject==="Original"?"":faDigits(referenceFiscalTaxId??invoice.referenceFiscalTaxId)}</bdi></span>
    <span>تاریخ ارسال: {paperDate(invoice.paymentDateUtc)}</span>
   </div>
   <h3>صورتحساب فروش کالا و خدمات</h3>
   <div className="invoice-paper__number"><strong>شماره: <bdi>{faDigits(reference)}</bdi></strong>
    <span>تاریخ: {paperDate(invoice.issueDateUtc)}</span>
   </div>
  </header>
  <PaperParticipant title="مشخصات فروشنده" participant={invoice.sellerDetails} fallbackName={invoice.seller} fallbackIdentity={null}/>
  <PaperParticipant title="مشخصات خریدار" participant={invoice.buyerDetails} fallbackName={invoice.buyer} fallbackIdentity={null}/>
  <section className="invoice-paper__items">
   <h4 className="invoice-paper__section-title">مشخصات کالا / خدمات</h4>
   <table><thead><tr><th>ردیف</th><th>شرح کالا یا خدمات</th><th>تعداد</th>
    <th>مبلغ واحد (ریال)</th><th>مبلغ کل (ریال)</th><th>مبلغ تخفیف (ریال)</th>
    <th>مبلغ کل پس از تخفیف (ریال)</th><th>جمع مالیات و عوارض (ریال)</th>
    <th>جمع مبلغ کل با مالیات و عوارض (ریال)</th></tr></thead>
    <tbody>{Array.from({length:lineCount},(_,index)=>{
     const line=invoice.lines[index];
     return <tr key={index}><td>{faDigits(index+1)}</td><td>{line?.description??""}{line?.officialIdentifier&&<small>شناسه کالا/خدمت: <bdi>{faDigits(line.officialIdentifier)}</bdi></small>}</td>
      <td>{line?money(line.quantity):""}</td><td>{line?money(line.unitPriceRial):""}</td>
      <td>{line?money(line.grossRial??line.quantity*line.unitPriceRial):""}</td>
      <td>{line?money(line.discountRial):""}</td><td>{line?money(line.netRial):""}</td>
      <td>{line?money(line.vatRial):""}</td><td>{line?money(line.totalRial):""}</td></tr>;
    })}</tbody>
    <tfoot><tr><th colSpan={9}>مبلغ قابل پرداخت: {money(invoice.totalRial)} ریال
     — به حروف: {rialInWords(invoice.totalRial)}</th></tr></tfoot>
   </table>
  </section>
  <footer className="invoice-paper__footer">
   <div className="invoice-paper__footer-payment">
    <strong>روش تسویه: {invoice.settlementMethod??"نامشخص"}</strong>
    <span>تاریخ پرداخت: {paperDate(invoice.paymentDateUtc)}</span>
    {invoice.cashAmountRial!==null&&<span>مبلغ نقدی: {money(invoice.cashAmountRial)} ریال</span>}
    {invoice.creditAmountRial!==null&&<span>مبلغ نسیه: {money(invoice.creditAmountRial)} ریال</span>}
   </div>
   <div className="invoice-paper__footer-subject">
    نوع صورتحساب: {invoiceTypeLabel[invoice.invoiceType??""]??invoice.invoiceType??"نوع اول"}
    <br/>موضوع صورتحساب: {paperSubject[invoiceSubject??""]??"اصلی"}
   </div>
   <div className="invoice-paper__footer-mark">
    {invoice.invoiceLogoDataUrl&&<img src={invoice.invoiceLogoDataUrl} alt="لوگوی شرکت"/>}
   </div>
   <div className="invoice-paper__footer-seller"><strong>مهر و امضاء فروشنده:</strong></div>
   <div className="invoice-paper__footer-buyer"><strong>مهر و امضاء خریدار:</strong></div>
  </footer>
 </div>;
}

function InvoiceLines({ lines }: { lines: Line[] }) {
  return (
    <>

      <div
        className="table-scroll"
        style={{ maxHeight: 300, overflow: "auto", marginBottom: 14 }}
      >
        <table className="console-table" style={{ minWidth: 900 }}>
          <thead>
            <tr>
              <th>ردیف</th>
              <th>شرح کالا/خدمت</th>
              <th>شناسه رسمی</th>
              <th>تعداد</th>
              <th>فی (ریال)</th>
              <th>تخفیف</th>
              <th>مالیات</th>
              <th>مبلغ نهایی (ریال)</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line, index) => (
              <tr key={index}>
                <td>{index + 1}</td>
                <td>{line.description}</td>
                <td>
                  <bdi dir="ltr">{line.officialIdentifier ?? "—"}</bdi>
                </td>
                <td>{money(line.quantity)}</td>
                <td>{money(line.unitPriceRial)}</td>
                <td>{money(line.discountRial)}</td>
                <td>{money(line.vatRial)}</td>
                <td>{money(line.totalRial)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function InvoiceTotals({ invoice }: { invoice: Invoice }) {
  return (
    <>

      <div style={totalsGridStyle}>
        <span>
          جمع ناخالص: <strong>{money(invoice.grossRial)} ریال</strong>
        </span>
        <span>
          جمع تخفیف: <strong>{money(invoice.discountRial)} ریال</strong>
        </span>
        <span>
          جمع پس از تخفیف: <strong>{money(invoice.netRial)} ریال</strong>
        </span>
        <span>
          مالیات ارزش افزوده: <strong>{money(invoice.vatRial)} ریال</strong>
        </span>
        <span>
          مبلغ قابل پرداخت: <strong>{money(invoice.totalRial)} ریال</strong>
        </span>
        {invoice.settlementMethod && (
          <span>
            روش تسویه: <strong>{invoice.settlementMethod}</strong>
          </span>
        )}
      </div>
      {invoice.description && <p>توضیحات: {invoice.description}</p>}
    </>
  );
}

function AuthorityActions({authorityStatus,subject,onCancel,onCorrect,onSalesReturn}:{
 authorityStatus?:string;subject?:string;onCancel:()=>void;onCorrect:()=>void;onSalesReturn?:()=>void
}){
 const canEdit=authorityStatus==="SUCCESS"&&subject?.toLowerCase()!=="cancellation";
 const canPrepareSalesReturn=!!onSalesReturn&&
  ((authorityStatus==="SUCCESS"&&["Original","Correction","SalesReturn"].includes(subject??""))||
   (authorityStatus==="FAILED"&&subject==="SalesReturn"));
 if(!canEdit&&!canPrepareSalesReturn)return null;
 return <div style={actionsStyle}>
  {canEdit&&<>
   <button type="button" className="identity-secondary" onClick={onCorrect}>اصلاح</button>
   <button type="button" className="identity-secondary" onClick={onCancel}>ابطال</button>
  </>}
  {canPrepareSalesReturn&&<button type="button" className="identity-secondary" onClick={onSalesReturn}>
   {authorityStatus==="FAILED"?"ویرایش و تلاش مجدد برگشت":"برگشت از فروش"}
  </button>}
 </div>;
}

export default function InvoiceFormalPreview({
  taxpayerId,
  invoiceId,
  reference,
  authorityStatus,
  subject,
  readOnly = false,
  authorityReference,
  referenceFiscalTaxId,
  printLayout=false,
  invoiceSubject,
  onPreviewReady,
  onPreviewError,
  onCancellationCompleted,
  onCorrectionCompleted,
  onSalesReturn,
}: InvoiceFormalPreviewProps) {
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [error, setError] = useState("");
  const [cancelOpen,setCancelOpen]=useState(false);
  const [correctionOpen,setCorrectionOpen]=useState<string|null>(null);
  const [cancelEligibility,setCancelEligibility]=useState<string>("");
  const [cancelChecking,setCancelChecking]=useState(false);
  const [cancelSubmitting,setCancelSubmitting]=useState(false);
  const [cancelEligible,setCancelEligible]=useState(false);
  const [cancelOutcome,setCancelOutcome]=useState("");
  const [cancelSucceeded,setCancelSucceeded]=useState(false);
  const [cancelInquiry,setCancelInquiry]=useState<{status:string;reference:string;code:string}>({status:"",reference:"",code:""});
  const onPreviewReadyRef=useRef(onPreviewReady);
  onPreviewReadyRef.current=onPreviewReady;
  const onPreviewErrorRef=useRef(onPreviewError);
  onPreviewErrorRef.current=onPreviewError;

  useEffect(() => {
    const controller = new AbortController();

    setInvoice(null);
    setError("");

    void (async () => {
      try {
        const data = await fetchInvoicePreview(
          taxpayerId,
          invoiceId,
          controller.signal,
        );

        if (!controller.signal.aborted) {
          setInvoice(data);
          onPreviewReadyRef.current?.();
        }
      } catch (caughtError) {
        if (!controller.signal.aborted) {
          setError((caughtError as Error).message);
          onPreviewErrorRef.current?.((caughtError as Error).message);
        }
      }
    })();

    return () => controller.abort();
  }, [taxpayerId, invoiceId]);

  return (
    <section
      dir="rtl"
      aria-label="نمای رسمی صورتحساب"
      style={previewStyle}
    >
      {!printLayout&&<h3 style={{ textAlign: "center", margin: "2px 0 8px" }}>
        صورتحساب فروش کالا و خدمات
      </h3>}

      {error && <p role="alert" className="form-error">{error}</p>}

      {!invoice && !error && (
        <div role="status" aria-busy="true" style={loadingStyle}>
          <span className="argin-loader-spinner" aria-hidden="true" />
          <span>در حال بارگذاری اطلاعات و اقلام صورتحساب…</span>
        </div>
      )}

      {cancelOpen&&<div role="dialog" aria-modal="true" aria-label="تأیید ابطال صورتحساب" style={{border:"1px solid #cbd5e1",background:"#fff",padding:14,borderRadius:10,marginBlock:12}}>
       <strong>درخواست ابطال صورتحساب</strong>
       <p>ابطال پس از پذیرش سازمان غیرقابل بازگشت است. سند اصلی نباید حذف یا ویرایش شود؛ سند ابطالی با شناسه مالیاتی جدید و ارجاع قانونی صادر می‌شود.</p>
       <p>شناسه مرجع: <bdi dir="ltr">{invoice?.uniqueInvoiceId??"نامشخص"}</bdi></p>
       {cancelChecking&&<p role="status">در حال بررسی سند مرجع…</p>}
       {!cancelSucceeded&&cancelEligibility&&<p role="status">{cancelEligibility}</p>}
       {cancelSucceeded ? <>
         <p role="status">ابطال صورتحساب با موفقیت انجام شد.</p>
         <p role="status">نتیجه استعلام سند ابطالی: <strong>{cancelInquiry.status||"در انتظار بررسی"}</strong>؛ رهگیری: <bdi dir="ltr">{cancelInquiry.reference||"در انتظار بررسی"}</bdi>{cancelInquiry.code&&<>؛ کد استعلام: <bdi dir="ltr">{cancelInquiry.code}</bdi></>}</p>
       </> : cancelOutcome&&<p role="status">{cancelOutcome}</p>}
       {!cancelSucceeded&&<div style={{display:"flex",gap:8}}><button type="button" className="identity-secondary" onClick={()=>setCancelOpen(false)}>بازگشت</button><button type="button" className="identity-primary" disabled={!cancelEligible||cancelChecking||cancelSubmitting}
        onClick={()=>{if(cancelSubmitting||!cancelEligible)return;setCancelSubmitting(true);setCancelOutcome("در حال صدور و ارسال سند ابطالی…");
         void fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/cancel-and-inquire",
          {method:"POST",credentials:"same-origin",cache:"no-store"})
          .then(async response=>{const data=await response.json() as Record<string,unknown>;
           if(!response.ok&&redirectForInvoiceSubscription(data))return;
           if(!response.ok)throw Error(String(data.error??response.status));
           setCancelEligible(false);
           setCancelInquiry({status:String(data.status??"نامشخص"),reference:String(data.referenceNumber??"در انتظار بررسی"),code:String(data.inquiryCode??"")});
           setCancelSucceeded(true);
           onCancellationCompleted?.();
          })
          .catch(e=>{setCancelEligible(false);const code=(e as Error).message;
           setCancelOutcome(code==="production_legal_successors_disabled"
            ?"ابطال ارسال نشد؛ مسیر عملیات پروداکشن فعال نیست. پس از فعال‌سازی، دوباره صلاحیت را بررسی کنید."
            :"وضعیت ابطال نیازمند بررسی است: "+code+"؛ درخواست را تکرار نکنید.");})
          .finally(()=>setCancelSubmitting(false));}}> {cancelSubmitting?"در حال ارسال…":"تأیید و ارسال ابطال"}</button></div>}
      </div>}
      {correctionOpen&&<InvoiceCorrectionModal taxpayerId={taxpayerId} invoiceId={correctionOpen}
       onClose={()=>setCorrectionOpen(null)} onChanged={()=>onCorrectionCompleted?.()}/>}
      {invoice && (
        <>
          {printLayout?<InvoicePaper invoice={invoice} reference={reference}
            referenceFiscalTaxId={referenceFiscalTaxId} invoiceSubject={invoiceSubject}/>:<>
           <InvoiceHeader invoice={invoice} reference={reference}
            authorityReference={authorityReference}/>
           <InvoiceLines lines={invoice.lines} />
           <InvoiceTotals invoice={invoice} />
          </>}
          {!readOnly && <AuthorityActions authorityStatus={authorityStatus} subject={subject} onSalesReturn={onSalesReturn}
           onCorrect={()=>setCorrectionOpen(invoiceId)} onCancel={()=>{
            setCancelOpen(true);setCancelChecking(true);setCancelEligibility("");setCancelEligible(false);setCancelOutcome("");setCancelSucceeded(false);setCancelInquiry({status:"",reference:"",code:""});
            void fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/cancellation-eligibility",{cache:"no-store",credentials:"same-origin"})
              .then(async response=>{const value=await response.json();setCancelEligible(response.ok&&value.eligible===true);setCancelEligibility(response.ok&&value.eligible===true?"سند مرجع برای درخواست ابطال معتبر است.":"امکان ابطال تأیید نشد: "+String(value.code??value.error??response.status));})
              .catch(()=>setCancelEligibility("بررسی امکان ابطال در دسترس نیست."))
              .finally(()=>setCancelChecking(false));
           }} />}
        </>
      )}
    </section>
  );
}
