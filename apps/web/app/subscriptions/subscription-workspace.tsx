"use client";

import "./subscription-workspace.css";
import FinancePagination from "./finance-pagination";
import CollectionHistoryTable from "./collection-history-table";
import PlanCatalog from "./plan-catalog";
import PartnerSettlementWorkspace from "./partner-settlement-workspace";
import PlanSalesReport from "./plan-sales-report";
import { amount, planName } from "./finance-format";
import InvoiceActionToast from "../invoices/invoice-action-toast";

import { Fragment, useCallback, useEffect, useRef, useState, type FormEvent } from "react";

type Tab = "plans" | "collections" | "revenue" | "settlements" | "sms";
const names: Record<Tab,string> = {
  plans:"تعریف پلن", collections:"ثبت دریافت",
  revenue:"گزارش درآمد", settlements:"تسویه شرکا", sms:"کیف پول پیامک"
};
type ResultState = { kind:"success"|"error"; text:string } | null;
type PendingOrder = {
  orderId:string; customerUserId:string; planCode:string; planKind:number;
  invoiceUnitLimit:number|null; issuedAtUtc:string; grossIrr:number;
  collectedIrr:number; customerDisplayName:string|null; customerContact:string|null;
  isActivated:boolean; partnerDisplayName:string|null; collections?:CollectionHistory[];
};
type CollectionHistory = {
  collectionId:string; amountIrr:number; channel:number;
  receiptReference:string; receivedAtUtc:string;
};
const uuid = () => globalThis.crypto.randomUUID();

export default function SubscriptionWorkspace({level1}:{level1:boolean}) {
  const [tab,setTab]=useState<Tab>("collections");
  const [busy,setBusy]=useState(false);
  const [notice,setNotice]=useState<ResultState>(null);
  const [planCode,setPlanCode]=useState("");
  const [pricePlanId,setPricePlanId]=useState("");
  const [priceIrr,setPriceIrr]=useState("");
  const [activationId,setActivationId]=useState("");
  const [smsCustomer,setSmsCustomer]=useState("");
  const [smsUnits,setSmsUnits]=useState("");
  const [smsEvidence,setSmsEvidence]=useState("");
  const [smsOperationKey,setSmsOperationKey]=useState(uuid);
  const [smsBalance,setSmsBalance]=useState<{granted:number;spent:number;available:number}|null>(null);
  const [planKind,setPlanKind]=useState(1);
  const [validity,setValidity]=useState(365);
  const [units,setUnits]=useState(100);
  const [pendingOrders,setPendingOrders]=useState<PendingOrder[]>([]);
  const [rejectConfirmation,setRejectConfirmation]=useState<PendingOrder|null>(null);
  const [pendingLoading,setPendingLoading]=useState(true);
  const [receiptOrderId,setReceiptOrderId]=useState<string|null>(null);
  const [receiptDrafts,setReceiptDrafts]=useState<Record<string,{amount:string;reference:string;channel:number}>>({});

  const [pendingPage,setPendingPage]=useState(1);
  const [pendingCount,setPendingCount]=useState(0);
  const [receiptRevision,setReceiptRevision]=useState(0);

  const pendingRequest=useRef(0);

  const request = useCallback(async (path:string, method:"GET"|"POST"|"PUT"="GET", data?:object) => {
    const response=await fetch("/api/phase12/subscriptions/"+path,{
      method,credentials:"same-origin",cache:"no-store",
      headers:data?{"Content-Type":"application/json"}:undefined,
      body:data?JSON.stringify(data):undefined
    });
    if(!response.ok) {
      const details=await response.json().catch(()=>null) as {message?:string}|null;
      throw new Error(details?.message||"عملیات ناموفق بود (HTTP "+response.status+").");
    }
    return response.json();
  },[]);
  const loadPending = useCallback(async()=>{
    const version=++pendingRequest.current;
    setPendingLoading(true);setNotice(null);
    try{
      const result=await request(`cash-pending?page=${pendingPage}&pageSize=10`) as {items:PendingOrder[];totalCount:number};
      if(version!==pendingRequest.current)return;
      setPendingOrders(result.items);setPendingCount(result.totalCount);
      if(result.items.length===0&&pendingPage>1)setPendingPage(v=>v-1);
    }
    catch(e){if(version===pendingRequest.current){setPendingOrders([]);setNotice({kind:"error",text:e instanceof Error?e.message:"خطای سفارش‌ها"});}}
    finally{if(version===pendingRequest.current)setPendingLoading(false);}
  },[request,pendingPage]);
  useEffect(()=>{
    if(tab==="collections")void loadPending();
    return ()=>{pendingRequest.current++;};
  },[tab,loadPending]);

  async function runAction(action:()=>Promise<unknown>, success:string) {
    if(busy)return;
    setBusy(true);setNotice(null);
    try {await action();setNotice({kind:"success",text:success});}
    catch(error){setNotice({kind:"error",text:error instanceof Error?error.message:"خطای ارتباط"});}
    finally {setBusy(false);}
  }
  async function submit(event:FormEvent,action:()=>Promise<unknown>, success:string) {
    event.preventDefault();await runAction(action,success);
  }
  return <section className="console-settings-card phase12-finance" dir="rtl">
    <div className="console-settings-card-title"><h2>عملیات مالی</h2>
      <p>این محیط مخصوص توسعه است؛ هیچ پرداخت بانکی از این صفحه انجام نمی‌شود.</p></div>
    <div className="phase12-finance-tabs" role="tablist" aria-label="بخش‌های مالی">
      {(Object.keys(names) as Tab[]).map(key=>
        <button key={key} type="button" role="tab" aria-selected={tab===key}
          onClick={()=>{
            if(key!==tab&&key==="collections")setPendingLoading(true);
            setTab(key);setNotice(null);
          }} disabled={busy}>{names[key]}</button>)}
    </div>
    {notice && <p role="status" className={`phase12-finance-notice phase12-finance-notice-${notice.kind}`}>{notice.text}</p>}
    {tab==="plans" && <PlanCatalog level1={level1}/>}
    {tab==="collections" && <section>
      <div className="phase12-collection-heading">
        <div><h3>درخواست پلن و مدیریت دریافت</h3>
          <p>فعال‌سازی با تأیید مدیر انجام می‌شود؛ ماندهٔ پرداخت پس از فعال‌سازی نیز قابل پیگیری است.</p></div>
        <button type="button" onClick={()=>void loadPending()} disabled={pendingLoading}>
          {pendingLoading?"در حال دریافت...":"بازخوانی"}
        </button>
      </div>
      <div className="console-table-scroll phase12-finance-report">
      <table className="console-table phase12-collection-table" aria-busy={pendingLoading}>
        <thead><tr><th>کاربر</th><th>ادمین سطح ۲</th><th>نام پلن</th><th>تاریخ ثبت</th><th>مبلغ کل</th><th>دریافتی</th><th>مانده</th><th>وضعیت</th><th>عملیات</th></tr></thead>
        <tbody>
        {pendingLoading&&<tr><td colSpan={9}><p className="phase12-finance-loading" role="status" aria-live="polite">
          <span aria-hidden="true"/>در حال دریافت درخواست‌ها…
        </p></td></tr>}
        {!pendingLoading&&pendingOrders.map(o=>{
          const outstanding=Math.max(0,o.grossIrr-o.collectedIrr);
          const draft=receiptDrafts[o.orderId]??{amount:String(outstanding),reference:"",channel:1};
          return <Fragment key={o.orderId}>
            <tr style={{backgroundColor:outstanding===0?"rgba(22,163,74,.10)":undefined}}>
              <td className="phase12-customer-cell" dir="auto" title={o.customerContact??o.customerUserId}>
                <strong>{o.customerDisplayName||"پروفایل تکمیل‌نشده"}</strong>
                {o.customerContact&&<small dir="ltr">{o.customerContact}</small>}
              </td>
              <td dir="auto">{o.partnerDisplayName||"فروش مستقیم"}</td>
              <td>{planName(o.planKind,o.invoiceUnitLimit)}</td>
              <td>{new Intl.DateTimeFormat("fa-IR",{dateStyle:"short",timeZone:"Asia/Tehran"}).format(new Date(o.issuedAtUtc))}</td>
              <td>{amount(o.grossIrr)}</td><td>{amount(o.collectedIrr)}</td><td>{amount(outstanding)}</td>
              <td><span className={o.isActivated?"phase12-order-status phase12-order-status-active":"phase12-order-status"}>
                {o.isActivated?"فعال":"در انتظار تأیید"}</span></td>
              <td className="phase12-collection-actions">
                {!o.isActivated && <button type="button" disabled={busy} onClick={()=>void runAction(
                  async()=>{await request("paid-orders/activate","POST",{orderId:o.orderId});
                    await loadPending();setReceiptRevision(v=>v+1);},
                  "پلن تأیید و فعال شد؛ ماندهٔ پرداخت در همین فهرست قابل پیگیری است."
                )}>تأیید و فعال‌سازی</button>}
                {!o.isActivated && o.collectedIrr===0 && <button type="button" disabled={busy}
                  title="رد، سفارش را لغو می‌کند و از فهرست دریافت خارج می‌شود"
                  onClick={()=>setRejectConfirmation(o)}>رد درخواست</button>}
                {(outstanding>0||o.collectedIrr>0) && <button type="button" disabled={busy}
                  aria-expanded={receiptOrderId===o.orderId}
                  onClick={()=>{
                    if(receiptOrderId===o.orderId){setReceiptOrderId(null);return;}
                    setReceiptOrderId(o.orderId);
                  }}>
                  {receiptOrderId===o.orderId?"بستن جزئیات":"مدیریت پرداخت"}
                </button>}
              </td>
            </tr>
            {receiptOrderId===o.orderId && <tr className="phase12-collection-detail">
              <td colSpan={9}><form onSubmit={e=>void submit(e,async()=>{
                if(outstanding<=0) return;
                await request("collections/manual","POST",{
                  orderId:o.orderId,customerUserId:o.customerUserId,channel:draft.channel,
                  amountIrr:Number(draft.amount),receiptReference:draft.reference
                });
                await loadPending();setReceiptRevision(v=>v+1);
                setReceiptDrafts(v=>{const next={...v};delete next[o.orderId];return next;});
              },"دریافت ثبت شد و مانده به‌روزرسانی شد.")}>
                <CollectionHistoryTable key={`${o.orderId}:${receiptRevision}`} orderId={o.orderId} request={request}/>
                {outstanding>0 && <>
                  <strong>ثبت مرحلهٔ جدید دریافت برای {planName(o.planKind,o.invoiceUnitLimit)}</strong>
                  <span>مانده فعلی: {amount(outstanding)}</span>
                  <label>روش دریافت<select value={draft.channel} onChange={e=>setReceiptDrafts(v=>({...v,[o.orderId]:{...draft,channel:Number(e.target.value)}}))}>
                    <option value={1}>نقدی</option><option value={2}>واریز بانکی</option></select></label>
                  <label>مبلغ (ریال)<input required type="number" min={1} max={outstanding}
                    value={draft.amount} onChange={e=>setReceiptDrafts(v=>({...v,[o.orderId]:{...draft,amount:e.target.value}}))}/></label>
                  <label>مرجع رسید<input required value={draft.reference}
                    onChange={e=>setReceiptDrafts(v=>({...v,[o.orderId]:{...draft,reference:e.target.value}}))}/></label>
                  <button type="submit" disabled={busy}>ثبت دریافت</button>
                </>}
              </form></td>
            </tr>}
          </Fragment>;
        })}</tbody>
      </table></div>
      <FinancePagination page={pendingPage} totalCount={pendingCount} loading={pendingLoading}
        onPageChange={next=>{setPendingLoading(true);setReceiptOrderId(null);setPendingPage(next);}}/>
      {!pendingLoading&&pendingOrders.length===0&&notice?.kind!=="error"&&<p className="phase12-finance-empty">درخواستی برای تأیید یا مانده‌ای برای دریافت وجود ندارد.</p>}
    </section>}
    {tab==="sms" && <section>
      <h3>کیف پول پیامک</h3>
      <p>اعتبار پیامک مستقل از سهمیه صورتحساب و درآمد است. شارژ اعتباری فقط با مجوز Level 1 انجام می‌شود.</p>
      <label>شناسه مشتری<input required value={smsCustomer} onChange={e=>setSmsCustomer(e.target.value)}/></label>
      <button type="button" disabled={busy||!smsCustomer} onClick={()=>void (async()=>{
        setBusy(true);setNotice(null);
        try {
          const result=await request("sms-wallet/"+encodeURIComponent(smsCustomer)) as {granted:number;spent:number;available:number};
          setSmsBalance(result);
        } catch(error) {setNotice({kind:"error",text:error instanceof Error?error.message:"خطای موجودی"});}
        finally {setBusy(false);}
      })()}>مشاهده موجودی</button>
      {smsBalance && <p role="status">اعتبار کل: {smsBalance.granted} | مصرف‌شده: {smsBalance.spent} | قابل‌استفاده: {smsBalance.available}</p>}
      {level1 && <form onSubmit={e=>void submit(e,async()=>{
        const result=await request("sms-wallet/grant","POST",{
          customerUserId:smsCustomer,units:Number(smsUnits),
          operationKey:smsOperationKey,evidence:smsEvidence
        }) as {granted:number;spent:number;available:number};
        setSmsBalance(result);
      },"شارژ اعتباری ثبت شد.")}>
        <h3>شارژ کیف پول با تأیید مدیر اصلی</h3>
        <label>تعداد پیامک<input required type="number" min={1} step={1} value={smsUnits} onChange={e=>setSmsUnits(e.target.value)}/></label>
        <label>مستند تأیید شارژ<input required value={smsEvidence} onChange={e=>setSmsEvidence(e.target.value)}/></label>
        <label>شناسه یکتای عملیات<input required value={smsOperationKey} onChange={e=>setSmsOperationKey(e.target.value)}/></label>
        <button type="submit" disabled={busy}>ثبت شارژ</button>
        <button type="button" onClick={()=>setSmsOperationKey(uuid())}>عملیات جدید</button>
      </form>}
      <p>کسر اعتبار پس از ارسال تأییدشده پیامک به هماهنگی با سرویس ارسال نیاز دارد و از این صفحه انجام نمی‌شود.</p>
    </section>}
    {tab==="revenue" && <section>
      <PlanSalesReport request={request}/>
    </section>}
    {tab==="settlements" && <PartnerSettlementWorkspace level1={level1} request={request}/>}
    {rejectConfirmation && <InvoiceActionToast
      title="رد درخواست پلن"
      description={`درخواست «${planName(rejectConfirmation.planKind,rejectConfirmation.invoiceUnitLimit)}» رد و سفارش لغو شود؟`}
      confirmLabel="رد و لغو درخواست"
      icon="discard"
      onCancel={()=>setRejectConfirmation(null)}
      onConfirm={()=>{
        const order=rejectConfirmation;
        setRejectConfirmation(null);
        void runAction(async()=>{
          await request("orders/reject","POST",{orderId:order.orderId});
          await loadPending();setReceiptRevision(v=>v+1);
        },"درخواست رد و لغو شد.");
      }}
    />}
  </section>;
}
