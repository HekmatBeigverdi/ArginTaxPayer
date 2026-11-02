"use client";
import {useState,type FormEvent} from "react";
export default function SmsTopupAction({customerUserId}:{customerUserId:string}){
 const [open,setOpen]=useState(false),[amountToman,setAmountToman]=useState("50000"),[busy,setBusy]=useState(false),[error,setError]=useState("");
 async function submit(e:FormEvent){e.preventDefault();setBusy(true);setError("");try{
  const response=await fetch("/api/phase12/subscriptions/sms-topups/start",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({amountIrr:Number(amountToman)*10,customerUserId})});
  const data=await response.json().catch(()=>null) as {topupId?:string;token?:string;paymentUrl?:string}|null;
  if(!response.ok||!data?.token||!data.paymentUrl||!data.topupId)throw new Error("آغاز پرداخت SEP انجام نشد؛ تنظیمات ترمینال و callback را بررسی کنید.");
  const form=document.createElement("form");form.method="POST";form.action=data.paymentUrl;form.style.display="none";
  const field=document.createElement("input");field.type="hidden";field.name="Token";field.value=data.token;form.appendChild(field);document.body.appendChild(form);form.submit();
 }catch(error){setError(error instanceof Error?error.message:"خطای ارتباط با درگاه");setBusy(false)}}
 return <div className="sms-topup-action"><button type="button" onClick={()=>setOpen(x=>!x)} disabled={busy}>{open?"بستن":"شارژ با SEP"}</button>
  {open&&<form onSubmit={submit}><label>مبلغ شارژ (تومان)<input required type="number" min={1000} max={10000000000} step={1000} value={amountToman} onChange={e=>setAmountToman(e.target.value)}/></label><button type="submit" disabled={busy}>{busy?"در حال اتصال…":"ادامه به درگاه"}</button>{error&&<p role="alert">{error}</p>}</form>}
 </div>;
}
