"use client";

import "./subscription-workspace.css";
import PlanCatalog from "./plan-catalog";
import InvoiceActionToast from "../invoices/invoice-action-toast";

import { Fragment, useCallback, useEffect, useState, type FormEvent } from "react";

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
  isActivated:boolean; collections?:CollectionHistory[];
};
type CollectionHistory = {
  collectionId:string; amountIrr:number; channel:number;
  receiptReference:string; receivedAtUtc:string;
};
type RevenueRow = {
  allocationId:string; collectionId:string; partnerUserId:string|null;
  grossCollectedIrr:number; approvedCostIrr:number; partnerShareIrr:number;
  rootShareIrr:number; postedAtUtc:string;
};
type RevenueReport = { items:RevenueRow[]; totalGrossIrr:number;
  totalPartnerShareIrr:number; totalRootShareIrr:number };
type SettlementRow = { allocationId:string; partnerUserId:string;
  earnedIrr:number; settledIrr:number; outstandingIrr:number };
type SettlementReport = { items:SettlementRow[];totalEarnedIrr:number;
  totalSettledIrr:number;totalOutstandingIrr:number };
const amount = (n:number) => new Intl.NumberFormat("fa-IR").format(n)+" ریال";
const temporalPlanNames:Record<number,string>={1:"یک‌ساله",3:"یک‌ماهه",4:"سه‌ماهه",5:"شش‌ماهه"};
const planName = (kind:number,units:number|null) => kind===2
  ? `بسته ${new Intl.NumberFormat("fa-IR").format(units??0)} تایی`
  : (temporalPlanNames[kind]??"پلن نرم‌افزار");
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
  const [pendingLoading,setPendingLoading]=useState(false);
  const [receiptOrderId,setReceiptOrderId]=useState<string|null>(null);
  const [receiptDrafts,setReceiptDrafts]=useState<Record<string,{amount:string;reference:string;channel:number}>>({});
  const [allocationId,setAllocationId]=useState("");
  const [settlementAmount,setSettlementAmount]=useState("");
  const [bankRef,setBankRef]=useState("");
  const [settlementReason,setSettlementReason]=useState("");
  const [originalEventId,setOriginalEventId]=useState("");
  const [revenue,setRevenue]=useState<RevenueReport|null>(null);
  const [settlements,setSettlements]=useState<SettlementReport|null>(null);

  const request = useCallback(async (path:string, method:"GET"|"POST"="GET", data?:object) => {
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
    setPendingLoading(true);setNotice(null);
    try{setPendingOrders(await request("cash-pending") as PendingOrder[]);}
    catch(e){setNotice({kind:"error",text:e instanceof Error?e.message:"خطای سفارش‌ها"});}
    finally{setPendingLoading(false);}
  },[request]);
  const load = useCallback(async () => {
    setBusy(true);setNotice(null);
    try {
      const [r,s] = await Promise.all([
        request("revenue-ledger?page=1&pageSize=15"),
        request("settlements/balances")
      ]);
      setRevenue(r as RevenueReport);setSettlements(s as SettlementReport);
    } catch(error) {
      setNotice({kind:"error",text:error instanceof Error?error.message:"خطای دریافت گزارش"});
    } finally {setBusy(false);}
  },[request]);
  useEffect(()=>{
    if(tab==="revenue"||tab==="settlements")void load();
    if(tab==="collections")void loadPending();
  },[tab,load,loadPending]);

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
          onClick={()=>{setTab(key);setNotice(null);}} disabled={busy}>{names[key]}</button>)}
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
      <table className="console-table phase12-collection-table">
        <thead><tr><th>کاربر</th><th>نام پلن</th><th>تاریخ ثبت</th><th>مبلغ کل</th><th>دریافتی</th><th>مانده</th><th>وضعیت</th><th>عملیات</th></tr></thead>
        <tbody>
        {pendingLoading&&<tr><td colSpan={8}><p className="phase12-finance-loading" role="status" aria-live="polite">
          <span aria-hidden="true"/>در حال دریافت درخواست‌ها…
        </p></td></tr>}
        {pendingOrders.map(o=>{
          const outstanding=Math.max(0,o.grossIrr-o.collectedIrr);
          const collections=o.collections??[];
          const draft=receiptDrafts[o.orderId]??{amount:String(outstanding),reference:"",channel:1};
          return <Fragment key={o.orderId}>
            <tr>
              <td className="phase12-customer-cell" dir="auto" title={o.customerContact??o.customerUserId}>
                <strong>{o.customerDisplayName||"پروفایل تکمیل‌نشده"}</strong>
                {o.customerContact&&<small dir="ltr">{o.customerContact}</small>}
              </td>
              <td>{planName(o.planKind,o.invoiceUnitLimit)}</td>
              <td>{new Intl.DateTimeFormat("fa-IR",{dateStyle:"short"}).format(new Date(o.issuedAtUtc))}</td>
              <td>{amount(o.grossIrr)}</td><td>{amount(o.collectedIrr)}</td><td>{amount(outstanding)}</td>
              <td><span className={o.isActivated?"phase12-order-status phase12-order-status-active":"phase12-order-status"}>
                {o.isActivated?"فعال":"در انتظار تأیید"}</span></td>
              <td className="phase12-collection-actions">
                {!o.isActivated && <button type="button" disabled={busy} onClick={()=>void runAction(
                  async()=>{await request("paid-orders/activate","POST",{orderId:o.orderId});
                    setPendingOrders(await request("cash-pending") as PendingOrder[]);},
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
              <td colSpan={8}><form onSubmit={e=>void submit(e,async()=>{
                if(outstanding<=0) return;
                await request("collections/manual","POST",{
                  orderId:o.orderId,customerUserId:o.customerUserId,channel:draft.channel,
                  amountIrr:Number(draft.amount),receiptReference:draft.reference
                });
                setPendingOrders(await request("cash-pending") as PendingOrder[]);
                setReceiptDrafts(v=>{const next={...v};delete next[o.orderId];return next;});
              },"دریافت ثبت شد و مانده به‌روزرسانی شد.")}>
                {collections.length>0 ? <div className="phase12-collection-history">
                    <strong>تاریخچه دریافت‌ها</strong>
                    <div className="console-table-scroll"><table className="console-table">
                      <thead><tr><th>تاریخ</th><th>روش</th><th>مرجع رسید</th><th>مبلغ</th></tr></thead>
                      <tbody>{collections.map(item=><tr key={item.collectionId}>
                        <td>{new Intl.DateTimeFormat("fa-IR",{dateStyle:"short",timeStyle:"short"}).format(new Date(item.receivedAtUtc))}</td>
                        <td>{item.channel===1?"نقدی":item.channel===2?"واریز بانکی":"درگاه"}</td>
                        <td>{item.receiptReference}</td><td>{amount(item.amountIrr)}</td>
                      </tr>)}</tbody>
                    </table></div>
                  </div> : <p>هنوز دریافتی برای این سفارش ثبت نشده است.</p>}
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
      <h3>گزارش درآمد و تخصیص</h3>
      <button type="button" disabled={busy} onClick={()=>void load()}>بازخوانی گزارش</button>
      {revenue && <><p>سهم Level 2: {amount(revenue.totalPartnerShareIrr)}
        {level1 && <> | سهم Level 1: {amount(revenue.totalRootShareIrr)}</>}</p>
        <div className="console-table-scroll phase12-finance-report"><table className="console-table">
          <thead><tr><th>تخصیص</th><th>شریک</th><th>سهم شریک</th>{level1&&<th>سهم مدیر اصلی</th>}</tr></thead>
          <tbody>{revenue.items.map(x=><tr key={x.allocationId}>
            <td><code>{x.allocationId.slice(0,8)}</code></td><td>{x.partnerUserId?.slice(0,8)||"مستقیم"}</td>
            <td>{amount(x.partnerShareIrr)}</td>{level1&&<td>{amount(x.rootShareIrr)}</td>}
          </tr>)}</tbody>
        </table></div></>}
    </section>}
    {tab==="settlements" && <section><h3>مانده تسویه شرکا</h3>
      <button type="button" disabled={busy} onClick={()=>void load()}>به‌روزرسانی</button>
      {settlements && <><p>کل سهم: {amount(settlements.totalEarnedIrr)} | تسویه‌شده:
        {amount(settlements.totalSettledIrr)} | مانده: {amount(settlements.totalOutstandingIrr)}</p>
        <div className="console-table-scroll phase12-finance-report"><table className="console-table">
          <thead><tr><th>شناسه تخصیص</th><th>شریک</th><th>سهم</th><th>مانده</th></tr></thead>
          <tbody>{settlements.items.map(x=><tr key={x.allocationId}>
            <td><code>{x.allocationId.slice(0,8)}</code></td><td>{x.partnerUserId.slice(0,8)}</td>
            <td>{amount(x.earnedIrr)}</td><td>{amount(x.outstandingIrr)}</td></tr>)}</tbody></table></div></>}
      {level1 && <form onSubmit={e=>void submit(e,()=>request("settlements/record","POST",{
        allocationId,amountIrr:Number(settlementAmount),bankReference:bankRef,reason:settlementReason
      }),"سند تسویه ثبت شد. گزارش را بازخوانی کنید.")}>
        <h3>ثبت تسویه تأییدشده</h3>
        <label>شناسه تخصیص<input required value={allocationId} onChange={e=>setAllocationId(e.target.value)}/></label>
        <label>مبلغ (ریال)<input type="number" required min={1} value={settlementAmount}
          onChange={e=>setSettlementAmount(e.target.value)}/></label>
        <label>شماره پیگیری بانکی<input required value={bankRef} onChange={e=>setBankRef(e.target.value)}/></label>
        <label>شرح<input required value={settlementReason} onChange={e=>setSettlementReason(e.target.value)}/></label>
        <button type="submit" disabled={busy}>ثبت سند تسویه</button>
      </form>}
      {level1 && <form onSubmit={e=>void submit(e,()=>request("settlements/reverse","POST",{
        allocationId,originalPaymentId:originalEventId,
        reversalReference:bankRef,reason:settlementReason
      }),"سند برگشت ثبت شد. گزارش را بازخوانی کنید.")}>
        <h3>برگشت سند تسویه</h3>
        <label>شناسه تخصیص<input required value={allocationId} onChange={e=>setAllocationId(e.target.value)}/></label>
        <label>شناسه سند اصلی<input required value={originalEventId} onChange={e=>setOriginalEventId(e.target.value)}/></label>
        <label>مرجع برگشت<input required value={bankRef} onChange={e=>setBankRef(e.target.value)}/></label>
        <label>دلیل برگشت<input required value={settlementReason} onChange={e=>setSettlementReason(e.target.value)}/></label>
        <button type="submit" disabled={busy}>ثبت برگشت</button>
      </form>}
    </section>}
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
          setPendingOrders(await request("cash-pending") as PendingOrder[]);
        },"درخواست رد و لغو شد.");
      }}
    />}
  </section>;
}
