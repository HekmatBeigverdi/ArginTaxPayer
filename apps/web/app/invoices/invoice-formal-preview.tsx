"use client";

import { useEffect, useState } from "react";

type Line = {
  description: string;
  officialIdentifier: string | null;
  quantity: number;
  unitPriceRial: number;
  discountRial: number;
  netRial: number;
  vatRatePercent: number;
  vatRial: number;
  totalRial: number;
};

type Invoice = {
  issueDateUtc: string;
  revision: number;
  uniqueInvoiceId: string | null;
  seller: string | null;
  buyer: string | null;
  description: string | null;
  contractNumber: string | null;
  settlementMethod: string | null;
  lines: Line[];
  grossRial: number;
  discountRial: number;
  netRial: number;
  vatRial: number;
  totalRial: number;
};

type InvoiceFormalPreviewProps = {
  taxpayerId: string;
  invoiceId: string;
  reference: string;
  authorityStatus?: string;
  readOnly?: boolean;
  onCancellationCompleted?:()=>void;
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
}: {
  invoice: Invoice;
  reference: string;
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
        <span>
          نسخه: <strong>{invoice.revision}</strong>
        </span>
        <span>
          شناسه یکتای فاکتور (شناسه یکتای مالیاتی):{" "}
          <strong dir="ltr">
            {invoice.uniqueInvoiceId ?? "در انتظار تخصیص"}
          </strong>
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

function AuthorityActions({authorityStatus,onCancel}:{
 authorityStatus?:string;onCancel:()=>void
}){
 if(authorityStatus!=="SUCCESS")return null;
 return <div style={actionsStyle}>
  <button type="button" className="identity-secondary" disabled title="مسیر اصلاحی در گام بعدی تکمیل می‌شود">اصلاح</button>
  <button type="button" className="identity-secondary" onClick={onCancel}>ابطال</button>
  <button type="button" className="identity-secondary" disabled title="مسیر برگشت از فروش در گام بعدی تکمیل می‌شود">برگشت از فروش</button>
 </div>;
}

export default function InvoiceFormalPreview({
  taxpayerId,
  invoiceId,
  reference,
  authorityStatus,
  readOnly = false,
  onCancellationCompleted,
}: InvoiceFormalPreviewProps) {
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [error, setError] = useState("");
  const [cancelOpen,setCancelOpen]=useState(false);
  const [cancelEligibility,setCancelEligibility]=useState<string>("");
  const [cancelChecking,setCancelChecking]=useState(false);
  const [cancelSubmitting,setCancelSubmitting]=useState(false);
  const [cancelEligible,setCancelEligible]=useState(false);
  const [cancelOutcome,setCancelOutcome]=useState("");

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

        if (!controller.signal.aborted) setInvoice(data);
      } catch (caughtError) {
        if (!controller.signal.aborted) {
          setError((caughtError as Error).message);
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
      <h3 style={{ textAlign: "center", margin: "2px 0 8px" }}>
        صورتحساب فروش کالا و خدمات
      </h3>

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
       {cancelEligibility&&<p role="status">{cancelEligibility}</p>}
       {cancelOutcome&&<p role="status">{cancelOutcome}</p>}
       <div style={{display:"flex",gap:8}}><button type="button" className="identity-secondary" onClick={()=>setCancelOpen(false)}>بازگشت</button><button type="button" className="identity-primary" disabled={!cancelEligible||cancelChecking||cancelSubmitting}
        onClick={()=>{if(cancelSubmitting||!cancelEligible)return;setCancelSubmitting(true);setCancelOutcome("در حال صدور و ارسال سند ابطالی…");
         void fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/cancel-and-inquire",
          {method:"POST",credentials:"same-origin",cache:"no-store"})
          .then(async response=>{const data=await response.json() as Record<string,unknown>;
           if(!response.ok)throw Error(String(data.error??response.status));
           setCancelEligible(false);
           setCancelOutcome("سند ابطالی با شناسه داخلی "+String(data.invoiceId??"—")+
             " ثبت شد. وضعیت ارسال: "+String(data.status??"نامشخص")+
             "؛ رهگیری: "+String(data.referenceNumber??"در انتظار بررسی")+
             ". برای تأیید قطعی سازمان، نتیجه استعلام سند ابطالی را بررسی کنید.");
           onCancellationCompleted?.();
          })
          .catch(e=>{setCancelEligible(false);setCancelOutcome("وضعیت ابطال نیازمند بررسی است: "+(e as Error).message+"؛ درخواست را تکرار نکنید.");})
          .finally(()=>setCancelSubmitting(false));}}> {cancelSubmitting?"در حال ارسال…":"تأیید و ارسال ابطال"}</button></div>
      </div>}
      {invoice && (
        <>
          <InvoiceHeader invoice={invoice} reference={reference} />
          <InvoiceLines lines={invoice.lines} />
          <InvoiceTotals invoice={invoice} />
          {!readOnly && <AuthorityActions authorityStatus={authorityStatus} onCancel={()=>{
            setCancelOpen(true);setCancelChecking(true);setCancelEligibility("");setCancelEligible(false);setCancelOutcome("");
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
