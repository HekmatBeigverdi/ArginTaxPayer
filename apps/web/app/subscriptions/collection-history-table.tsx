"use client";
import {useEffect,useState} from "react";
import FinancePagination from "./finance-pagination";
import {amount} from "./finance-format";
type Collection={collectionId:string;amountIrr:number;channel:number;receiptReference:string;receivedAtUtc:string};
type Report={items:Collection[];totalCount:number};
export default function CollectionHistoryTable({orderId,request}:{orderId:string;request:(path:string)=>Promise<unknown>}) {
  const [page,setPage]=useState(1);
  const [report,setReport]=useState<Report|null>(null);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState("");
  useEffect(()=>{
    let cancelled=false;setLoading(true);setError("");
    void request(`collections/history?orderId=${encodeURIComponent(orderId)}&page=${page}&pageSize=10`)
      .then(value=>{if(!cancelled)setReport(value as Report);})
      .catch(reason=>{if(!cancelled){setReport(null);setError(reason instanceof Error?reason.message:"خطای دریافت تاریخچه");}})
      .finally(()=>{if(!cancelled)setLoading(false);});
    return ()=>{cancelled=true;};
  },[request,orderId,page]);
  return <div className="phase12-collection-history">
    <strong>تاریخچه دریافت‌ها</strong>
    {error&&<p role="alert">{error}</p>}
    <div className="console-table-scroll"><table className="console-table" aria-busy={loading}>
      <thead><tr><th>تاریخ</th><th>روش</th><th>مرجع رسید</th><th>مبلغ</th></tr></thead>
      <tbody>{loading?<tr><td colSpan={4}><p className="phase12-finance-loading" role="status"><span aria-hidden="true"/>در حال دریافت تاریخچه…</p></td></tr>
        :report?.items.map(item=><tr key={item.collectionId}>
          <td>{new Intl.DateTimeFormat("fa-IR",{dateStyle:"short",timeStyle:"short",timeZone:"Asia/Tehran"}).format(new Date(item.receivedAtUtc))}</td>
          <td>{item.channel===1?"نقدی":item.channel===2?"واریز بانکی":"درگاه"}</td>
          <td>{item.receiptReference}</td><td>{amount(item.amountIrr)}</td>
        </tr>)}</tbody>
    </table></div>
    {!loading&&report?.totalCount===0&&<p>هنوز دریافتی برای این سفارش ثبت نشده است.</p>}
    <FinancePagination page={page} totalCount={report?.totalCount??0} loading={loading}
      onPageChange={next=>{setLoading(true);setPage(next);}}/>
  </div>;
}
