"use client";

import "./subscription-workspace.css";

import { useCallback, useEffect, useState, type FormEvent } from "react";

type Tab = "plans" | "orders" | "collections" | "revenue" | "settlements" | "sms";
const names: Record<Tab,string> = {
  plans:"تعریف پلن", orders:"سفارش مالی", collections:"ثبت دریافت",
  revenue:"گزارش درآمد", settlements:"تسویه شرکا", sms:"کیف پول پیامک"
};
type ResultState = { kind:"success"|"error"; text:string } | null;
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
const uuid = () => globalThis.crypto.randomUUID();

export default function SubscriptionWorkspace({level1}:{level1:boolean}) {
  const [tab,setTab]=useState<Tab>("plans");
  const [busy,setBusy]=useState(false);
  const [notice,setNotice]=useState<ResultState>(null);
  const [planCode,setPlanCode]=useState("");
  const [pricePlanId,setPricePlanId]=useState("");
  const [priceIrr,setPriceIrr]=useState("");
  const [activationId,setActivationId]=useState("");
  const [planKind,setPlanKind]=useState(1);
  const [validity,setValidity]=useState(365);
  const [units,setUnits]=useState(100);
  const [customerId,setCustomerId]=useState("");
  const [orderPlanId,setOrderPlanId]=useState("");
  const [orderKey,setOrderKey]=useState(uuid);
  const [orderId,setOrderId]=useState("");
  const [receiptRef,setReceiptRef]=useState("");
  const [receiptAmount,setReceiptAmount]=useState("");
  const [channel,setChannel]=useState(1);
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
    if(!response.ok) throw new Error("عملیات ناموفق بود (HTTP "+response.status+").");
    return response.json();
  },[]);
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
  useEffect(()=>{if(tab==="revenue"||tab==="settlements")void load();},[tab,load]);

  async function submit(event:FormEvent,action:()=>Promise<unknown>, success:string) {
    event.preventDefault();if(busy)return;
    setBusy(true);setNotice(null);
    try {await action();setNotice({kind:"success",text:success});}
    catch(error){setNotice({kind:"error",text:error instanceof Error?error.message:"خطای ارتباط"});}
    finally {setBusy(false);}
  }
  return <section className="console-settings-card phase12-finance" dir="rtl">
    <div className="console-settings-card-title"><h2>عملیات مالی</h2>
      <p>این محیط مخصوص توسعه است؛ هیچ پرداخت بانکی از این صفحه انجام نمی‌شود.</p></div>
    <div role="tablist" aria-label="بخش‌های مالی" style={{display:"flex",gap:8,flexWrap:"wrap",marginBottom:16}}>
      {(Object.keys(names) as Tab[]).map(key=>
        <button key={key} type="button" role="tab" aria-selected={tab===key}
          onClick={()=>{setTab(key);setNotice(null);}} disabled={busy}
          style={{padding:"8px 14px",borderRadius:9,border:"1px solid #b3c7be",
            background:tab===key?"#176c4e":"transparent",color:tab===key?"white":"inherit",
            cursor:"pointer"}}>{names[key]}</button>)}
    </div>
    {notice && <p role="status" style={{color:notice.kind==="error"?"#a93030":"#176c4e"}}>{notice.text}</p>}
    {tab==="plans" && <form onSubmit={e=>void submit(e,()=>request("plans","POST",{
      code:planCode,kind:planKind,validityDays:validity,invoiceUnitLimit:planKind===2?units:null
    }),"پلن غیرفعال ایجاد شد؛ فعال‌سازی و قیمت‌گذاری هنوز به API تکمیلی نیاز دارند.")}>
      <h3>تعریف پلن جدید</h3>
      <label>کد پلن (انگلیسی)<input required value={planCode} onChange={e=>setPlanCode(e.target.value)} maxLength={50}/></label>
      <label>نوع پلن<select value={planKind} onChange={e=>{const k=Number(e.target.value);setPlanKind(k);setValidity(k===3?30:k===4?90:k===5?182:365);}}>
        <option value={3}>ماهانه تک‌مؤدی</option><option value={4}>سه‌ماهه تک‌مؤدی</option>
        <option value={5}>شش‌ماهه تک‌مؤدی</option><option value={1}>سالانه تک‌مؤدی</option>
        <option value={2}>بسته تعدادی</option></select></label>
      <label>اعتبار پایه (روز مطابق قرارداد فعلی Backend)<input type="number" min={1} max={366}
        value={validity} onChange={e=>setValidity(Number(e.target.value))} required/></label>
      {planKind===2 && <label>تعداد صورتحساب<input type="number" min={1} step={1}
        value={units} onChange={e=>setUnits(Number(e.target.value))}/></label>}
      <p>مدت تقویمی در مدل دامنه تعریف شده؛ تا یکپارچه‌سازی کامل، اعتبار روزانه قرارداد فعلی Backend همچنان لازم است.</p>
      <button type="submit" disabled={busy}>{busy?"در حال ثبت...":"ایجاد پلن"}</button>
    </form>}
    {tab==="plans" && <form onSubmit={e=>void submit(e,()=>request("prices","POST",{
      planId:pricePlanId,priceIrr:Number(priceIrr),
      effectiveFromUtc:new Date().toISOString(),effectiveUntilUtc:null
    }),"نسخه تعرفه برای محدوده مدیریتی شما ثبت شد.")}>
      <h3>تعرفه اختصاصی مدیر</h3>
      <p>قیمت‌ها بر حسب ریال ثبت می‌شوند و با تعرفه سایر مدیران Level 2 مستقل‌اند.</p>
      <label>شناسه پلن<input required value={pricePlanId} onChange={e=>setPricePlanId(e.target.value)}/></label>
      <label>قیمت (ریال)<input required type="number" min={0} step={1} value={priceIrr} onChange={e=>setPriceIrr(e.target.value)}/></label>
      <button type="submit" disabled={busy}>ثبت نسخه تعرفه</button>
    </form>}
    {tab==="plans" && level1 && <form onSubmit={e=>void submit(e,()=>request("plans/activate","POST",{
      planId:activationId
    }),"پلن کاتالوگ فعال شد؛ اشتراک مشتری هنوز فعال نشده است.")}>
      <h3>فعال‌سازی پلن کاتالوگ توسط Level 1</h3>
      <label>شناسه پلن<input required value={activationId} onChange={e=>setActivationId(e.target.value)}/></label>
      <button type="submit" disabled={busy}>فعال‌سازی پلن</button>
    </form>}
    {tab==="orders" && <form onSubmit={e=>void submit(e,async()=>{
      const result=await request("commercial-orders","POST",{
        customerUserId:customerId,planId:orderPlanId,idempotencyKey:orderKey
      }) as {orderId:string};setOrderId(result.orderId);
    },"سفارش و صورتحساب مالی داخلی ثبت شد.")}>
      <h3>صدور سفارش مالی</h3>
      <label>شناسه مشتری<input required value={customerId} onChange={e=>setCustomerId(e.target.value)}/></label>
      <label>شناسه پلن فعال<input required value={orderPlanId} onChange={e=>setOrderPlanId(e.target.value)}/></label>
      <label>کلید یکتای سفارش<input required value={orderKey} onChange={e=>setOrderKey(e.target.value)}/></label>
      <button type="submit" disabled={busy}>ثبت سفارش</button>
      <button type="button" onClick={()=>setOrderKey(uuid())}>سفارش جدید</button>
      {orderId && <p>شناسه سفارش: <code dir="ltr">{orderId}</code></p>}
    </form>}
    {tab==="collections" && <form onSubmit={e=>void submit(e,()=>request("collections/manual","POST",{
      orderId,customerUserId:customerId,channel,amountIrr:Number(receiptAmount),
      receiptReference:receiptRef
    }),"دریافت تأییدشده توسط مدیر ثبت شد.")}>
      <h3>ثبت دریافت تأییدشده</h3>
      <p>ابتدا مستقل از سامانه، وصول وجه را بررسی کنید. ثبت این فرم به معنی تأیید حسابداری است.</p>
      <label>شناسه سفارش<input required value={orderId} onChange={e=>setOrderId(e.target.value)}/></label>
      <label>شناسه مشتری<input required value={customerId} onChange={e=>setCustomerId(e.target.value)}/></label>
      <label>روش دریافت<select value={channel} onChange={e=>setChannel(Number(e.target.value))}>
        <option value={1}>نقدی</option><option value={2}>واریز بانکی</option></select></label>
      <label>مبلغ (ریال)<input required type="number" min={1} value={receiptAmount}
        onChange={e=>setReceiptAmount(e.target.value)}/></label>
      <label>مرجع رسید<input required value={receiptRef} onChange={e=>setReceiptRef(e.target.value)}/></label>
      <button type="submit" disabled={busy}>تأیید و ثبت دریافت</button>
    </form>}
    {tab==="sms" && <section>
      <h3>کیف پول پیامک</h3>
      <p>اعتبار پیامک مستقل از سهمیه صورتحساب است. مدل مانده و قواعد جلوگیری از منفی‌شدن موجودی اضافه شده‌اند.</p>
      <p role="status">اتصال موجودی، شارژ و تاریخچه پیامک به Backend هنوز فعال نیست؛ از این صفحه امکان شارژ یا کسر واقعی وجود ندارد.</p>
    </section>}
    {tab==="revenue" && <section>
      <h3>گزارش درآمد و تخصیص</h3>
      <button type="button" disabled={busy} onClick={()=>void load()}>بازخوانی گزارش</button>
      {revenue && <><p>سهم Level 2: {amount(revenue.totalPartnerShareIrr)}
        {level1 && <> | سهم Level 1: {amount(revenue.totalRootShareIrr)}</>}</p>
        <div style={{overflowX:"auto"}}><table style={{width:"100%",textAlign:"right"}}>
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
        <div style={{overflowX:"auto"}}><table style={{width:"100%",textAlign:"right"}}>
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
  </section>;
}
