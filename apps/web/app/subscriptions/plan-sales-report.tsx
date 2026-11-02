"use client";

import {useEffect,useState} from "react";
import FinancePagination from "./finance-pagination";
import {amount,planName} from "./finance-format";

type Sale = {
  orderId:string; customerDisplayName:string|null; customerContact:string|null;
  partnerDisplayName:string|null; planCode:string; planKind:number; invoiceUnitLimit:number|null;
  issuedAtUtc:string; grossIrr:number; collectedIrr:number; isActivated:boolean;
};
type PartnerOption={partnerUserId:string;displayName:string};
type SalesReport = {
  items:Sale[]; totalGrossIrr:number; totalCollectedIrr:number; totalOutstandingIrr:number;
  totalCount:number; page:number; pageSize:number;partners:PartnerOption[];
};
type Props = {request:(path:string)=>Promise<unknown>};
const date=(value:string)=>new Intl.DateTimeFormat("fa-IR",{
  dateStyle:"short",timeZone:"Asia/Tehran"
}).format(new Date(value));

export default function PlanSalesReport({request}:Props) {
  const [period,setPeriod]=useState<"month"|"year">("month");
  const [page,setPage]=useState(1);
  const [revision,setRevision]=useState(0);
  const [report,setReport]=useState<SalesReport|null>(null);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState<string|null>(null);
  const [partnerUserId,setPartnerUserId]=useState("");
  const [partners,setPartners]=useState<PartnerOption[]>([]);
  useEffect(()=>{
    let cancelled=false;
    let redirected=false;
    setLoading(true);setReport(null);setError(null);
    const partnerQuery=partnerUserId?`&partnerUserId=${encodeURIComponent(partnerUserId)}`:"";
    void request(`sales-report?period=${period}&page=${page}&pageSize=10${partnerQuery}`)
      .then(value=>{
        if(cancelled)return;
        const result=value as SalesReport;
        const lastPage=Math.max(1,Math.ceil(result.totalCount/10));
        if(page>lastPage){redirected=true;setPage(lastPage);return;}
        setPartners(result.partners??[]);setReport(result);
      })
      .catch(reason=>{if(!cancelled)setError(reason instanceof Error?reason.message:"خطای دریافت گزارش فروش");})
      .finally(()=>{if(!cancelled&&!redirected)setLoading(false);});
    return ()=>{cancelled=true;};
  },[request,period,page,revision,partnerUserId]);
  return <section className="phase12-sales-report" aria-label="گزارش فروش پلن‌ها">
    <div className="phase12-plan-toolbar phase12-sales-toolbar">
      <div><h3>گزارش فروش پلن‌ها و درآمد انتظاری</h3></div>
      <label>دوره گزارش<select value={period} onChange={e=>{
        setLoading(true);setReport(null);setPeriod(e.target.value as "month"|"year");setPage(1);
      }}>
        <option value="month">ماه جاری</option><option value="year">سال جاری تا امروز</option>
      </select></label>
      <label>ادمین سطح ۲<select value={partnerUserId} onChange={e=>{
        setLoading(true);setReport(null);setPartnerUserId(e.target.value);setPage(1);
      }}>
        <option value="">همه ادمین‌ها</option>
        {partners.map(partner=><option key={partner.partnerUserId} value={partner.partnerUserId}>{partner.displayName}</option>)}
      </select></label>
      <button type="button" disabled={loading} onClick={()=>{setLoading(true);setRevision(v=>v+1);}}>بازخوانی فروش‌ها</button>
    </div>
    <p>مبنای دوره، تاریخ ثبت فروش در تقویم شمسی و ساعت تهران است. دریافتی، مجموع پرداخت‌های تأییدشدهٔ فروش‌های همین دوره تا امروز است.</p>
    {error&&<p role="alert" className="phase12-finance-notice phase12-finance-notice-error">{error}</p>}
    {report&&
      <div className="phase12-sales-totals" aria-label="جمع کل دوره">
        <p>جمع فروش دوره: <strong>{amount(report.totalGrossIrr)}</strong></p>
        <p>جمع دریافت نقدی و بانکی: <strong>{amount(report.totalCollectedIrr)}</strong></p>
        <p>طلب دوره: <strong>{amount(report.totalOutstandingIrr)}</strong></p>
      </div>}
      <div className="console-table-scroll phase12-finance-report"><table className="console-table phase12-collection-table" aria-busy={loading}>
        <thead><tr><th>کاربر</th><th>ادمین سطح ۲</th><th>نام پلن</th><th>تاریخ فروش</th>
          <th>مبلغ فروش</th><th>دریافتی</th><th>طلب</th><th>وضعیت پرداخت</th><th>وضعیت پلن</th></tr></thead>
        <tbody>{loading&&<tr><td colSpan={9}><p role="status" className="phase12-finance-loading">
          <span aria-hidden="true"/>در حال دریافت گزارش فروش…</p></td></tr>}
        {!loading&&report?.items.map(sale=>{
          const outstanding=Math.max(0,sale.grossIrr-sale.collectedIrr);
          return <tr key={sale.orderId} style={{backgroundColor:outstanding===0?"rgba(22,163,74,.10)":undefined}}>
            <td className="phase12-customer-cell" dir="auto"><strong>{sale.customerDisplayName||"پروفایل تکمیل‌نشده"}</strong>
              {sale.customerContact&&<small dir="ltr">{sale.customerContact}</small>}</td>
            <td dir="auto">{sale.partnerDisplayName||"فروش مستقیم"}</td>
            <td>{sale.planCode}<small className="phase12-plan-kind">{planName(sale.planKind,sale.invoiceUnitLimit)}</small></td>
            <td>{date(sale.issuedAtUtc)}</td><td>{amount(sale.grossIrr)}</td>
            <td>{amount(sale.collectedIrr)}</td><td>{amount(outstanding)}</td>
            <td>{outstanding===0?"تسویه کامل":sale.collectedIrr>0?"پرداخت جزئی":"پرداخت‌نشده"}</td>
            <td>{sale.isActivated?"فعال":"در انتظار تأیید"}</td>
          </tr>;
        })}</tbody>
      </table></div>
      {!loading&&report?.items.length===0&&<p className="phase12-finance-empty">فروشی در این دوره ثبت نشده است.</p>}
      <FinancePagination page={page} totalCount={report?.totalCount??0} loading={loading}
        onPageChange={next=>{setLoading(true);setPage(next);}}/>
  </section>;
}
