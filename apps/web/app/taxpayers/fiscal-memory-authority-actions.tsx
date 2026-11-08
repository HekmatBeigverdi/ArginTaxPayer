"use client";
import {useState} from "react";
import OperationMessageBox from "./operation-message-box";

type Memory={id:string;memoryIdentifier:string;status:number;revision:number;authorityEnvironment:1|2|null;canVerifyAndActivate?:boolean};
export default function FiscalMemoryAuthorityActions({taxpayerId,isLevel1,memories,selectedMemory,testedMemoryId,onChanged}:{
 taxpayerId:string;isLevel1:boolean;memories:Memory[];selectedMemory:Memory|null;testedMemoryId:string;onChanged?:()=>Promise<void>;
}){
 const [environments,setEnvironments]=useState<Record<string,string>>({});
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState("");
 const [notice,setNotice]=useState("");
 if(!isLevel1&&!selectedMemory?.canVerifyAndActivate)return null;
 const mutate=async(memory:Memory,action:"classify-authority"|"verify-and-activate")=>{
  const authorityEnvironment=Number(environments[memory.id]);
  if(busy||action==="classify-authority"&&![1,2].includes(authorityEnvironment)||
   action==="verify-and-activate"&&(testedMemoryId!==memory.id||memory.status!==1))return;
  setBusy(true);setError("");setNotice("");
  try{
   const response=await fetch(`/api/taxpayers/${taxpayerId}/memories/${memory.id}/${action}`,{
    method:"POST",credentials:"same-origin",cache:"no-store",headers:{"Content-Type":"application/json"},
    body:JSON.stringify({...(action==="classify-authority"?{authorityEnvironment}:{}),expectedRevision:memory.revision})
   });
   const value=await response.json().catch(()=>({}));
   if(!response.ok){
    const code=typeof value.error==="string"?value.error:"request_rejected";
    const messages:Record<string,string>={
     authority_memory_inactive_or_taxpayer_mismatch:"سازمان فعال‌بودن حافظه و تطابق هویت آن با مؤدی را تأیید نکرد؛ اطلاعات کارپوشه را بررسی کنید.",
     memory_already_classified_or_changed:"محیط قبلاً ثبت شده یا اطلاعات تغییر کرده است؛ وضعیت حافظه‌ها را بازخوانی کنید.",
     memory_dispatch_history_requires_review:"این حافظه سابقه عملیات ارسال دارد؛ تعیین محیط آن به بررسی سوابق نیاز دارد.",
     memory_not_pending_or_changed:"حافظه در انتظار بررسی نیست یا اطلاعات آن تغییر کرده است؛ وضعیت را بازخوانی کنید.",
     stale_memory_revision:"نسخه اطلاعات حافظه تغییر کرده است؛ وضعیت را بازخوانی کنید.",
     memory_scope_denied:"این عملیات فقط برای مالک مؤدی یا مدیر سطح ۱ مجاز است.",
     key_not_ready:"کلید آماده نیست؛ ابتدا نگهداری امن کلید را تکمیل کنید.",
     authority_authentication_rejected:"سازمان احراز هویت حافظه را رد کرد؛ کلید و شناسه ثبت‌شده را بررسی کنید.",
     authority_unavailable:"اتصال به سازمان در دسترس نیست؛ بعداً دوباره بررسی کنید.",
     authority_memory_not_active:"سازمان وضعیت فعال حافظه را تأیید نکرد؛ اطلاعات حافظه در کارپوشه را بررسی کنید.",
     authority_identity_mismatch:"شناسه مالک حافظه با مؤدی ثبت‌شده تطابق ندارد.",
     authority_verification_failed:"احراز هویت و اطلاعات حافظه از سازمان تأیید نشد؛ اتصال و وضعیت کلید را بررسی کنید.",
     subscription_required:"اشتراک معتبر برای فعال‌سازی حافظه لازم است.",
     conflict:"اطلاعات تغییر کرده است؛ وضعیت حافظه‌ها را بازخوانی کنید.",
     forbidden:"این عملیات فقط برای مالک مؤدی یا مدیر سطح ۱ مجاز است.",
     unauthorized:"نشست ورود معتبر نیست؛ دوباره وارد شوید."
    };
    throw Error(messages[code]??`عملیات حافظه انجام نشد (${code}).`);
   }
   await onChanged?.();
   setNotice(action==="classify-authority"?"محیط حافظه صریحاً ثبت شد؛ اکنون می‌توانید کلید همان حافظه را آماده کنید.":
    "هویت و وضعیت فعال حافظه با پاسخ سازمان بررسی و فعال‌سازی محلی ثبت شد.");
  }catch(e){setError((e as Error).message);}
  finally{setBusy(false);}
 };
 return <section aria-label="تأیید محیط و فعال‌سازی حافظه" className="signing-test-panel">
  {isLevel1&&memories.filter(memory=>memory.authorityEnvironment===null).map(memory=><div key={memory.id}>
   <strong>ثبت محیط حافظه <bdi dir="ltr">{memory.memoryIdentifier}</bdi></strong>
   <p>محیطی را انتخاب کنید که این شناسه و کلید آن در کارپوشه همان محیط ثبت شده‌اند.</p>
   <select aria-label={"محیط حافظه "+memory.memoryIdentifier} value={environments[memory.id]??""} disabled={busy}
    onChange={event=>setEnvironments(current=>({...current,[memory.id]:event.target.value}))}>
    <option value="">انتخاب صریح محیط</option><option value="1">Production — اصلی</option><option value="2">Sandbox — آزمایشی</option>
   </select>
   <button type="button" className="identity-secondary" disabled={busy||!["1","2"].includes(environments[memory.id]??"")}
    onClick={()=>mutate(memory,"classify-authority")}>ثبت محیط حافظه</button>
  </div>)}
  {selectedMemory?.canVerifyAndActivate&&selectedMemory.status===1&&selectedMemory.authorityEnvironment!==null&&<div>
   <p>پس از تست اتصال موفق، فعال‌سازی را جداگانه درخواست کنید؛ سرور دوباره وضعیت فعال و هویت مؤدی را از سازمان بررسی می‌کند.</p>
   <button type="button" className="identity-secondary" disabled={busy||testedMemoryId!==selectedMemory.id}
    onClick={()=>mutate(selectedMemory,"verify-and-activate")}>بررسی سازمان و فعال‌سازی حافظه</button>
  </div>}
  {notice&&<OperationMessageBox kind="success" title="وضعیت حافظه به‌روز شد">{notice}</OperationMessageBox>}
  {error&&<OperationMessageBox kind="error" title="عملیات حافظه انجام نشد">{error}</OperationMessageBox>}
 </section>;
}
