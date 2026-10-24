"use client";

import {useEffect,useState} from "react";
import OperationMessageBox from "./operation-message-box";

function toPem(bytes:ArrayBuffer,label:string):string {
  const chars=Array.from(new Uint8Array(bytes),v=>String.fromCharCode(v)).join("");
  const encoded=btoa(chars).match(/.{1,64}/g)?.join("\n") ?? "";
  return "-----BEGIN "+label+"-----\n"+encoded+"\n-----END "+label+"-----\n";
}
function download(name:string,contents:string) {
  const url="data:application/x-pem-file;charset=utf-8,"+encodeURIComponent(contents);
  const element=document.createElement("a");
  element.href=url;element.download=name;
  document.body.appendChild(element);element.click();element.remove();
}
function sandboxStorageKey(taxpayerId:string,suffix:string) {
  return "argin:sandbox:"+taxpayerId+":"+suffix;
}
function readSandboxStorage(key:string):string {
  try{return sessionStorage.getItem(key)??"";}catch{return "";}
}
function writeSandboxStorage(key:string,value:string) {
  try{sessionStorage.setItem(key,value);}catch{/* Storage may be disabled. */}
}
function removeSandboxStorage(key:string) {
  try{sessionStorage.removeItem(key);}catch{/* Storage may be disabled. */}
}
function registrationError(status:number,value:unknown):string {
  const code=value&&typeof value==="object"&&!Array.isArray(value)&&
    typeof (value as Record<string,unknown>).error==="string"
    ?(value as Record<string,string>).error:"";
  switch(code){
    case "invalid_sandbox_public_key":
      return "Public Key معتبر نیست؛ همان فایل Public Key تولیدشده را استفاده کنید.";
    case "invalid_sandbox_memory_input":
      return "قالب شناسه حافظه معتبر نیست؛ فقط حروف انگلیسی و عدد، بین ۶ تا ۳۲ کاراکتر وارد کنید.";
    case "sandbox_memory_already_registered":
      return "برای این مؤدی قبلاً حافظه Sandbox ثبت شده است؛ اطلاعات آن را بازخوانی کنید.";
    case "sandbox_test_database_required":
      return "اتصال آزمایشی به دیتابیس Sandbox برقرار نیست.";
    case "sandbox_scope_denied":
      return "نشست شما منقضی شده یا به این مؤدی دسترسی ندارید؛ دوباره وارد شوید.";
    case "api_unavailable":
      return "سرویس Sandbox در دسترس نیست؛ وضعیت سرویس توسعه را بررسی کنید.";
    default:
      return status===409?"برای این مؤدی حافظه آزمایشی قبلاً ثبت شده است.":"ثبت شناسه آزمایشی انجام نشد؛ جزئیات پاسخ: "+(code||"خطای نامشخص");
  }
}

/**
 * Standalone Sandbox onboarding UI. Does NOT reuse existing production key,
 * memory identifier or real connection-test endpoint. Generated key is first delivered to the operator. Optional private PEM
 * import is scoped to Sandbox and encrypted server-side. Live connection
 * stays blocked until the official Sandbox API profile is verified.
 */
export default function SandboxFiscalMemoryPanel({taxpayerId}:{taxpayerId:string}) {
  const [identifier,setIdentifier]=useState("");
  const [generated,setGenerated]=useState(false);
  const [generating,setGenerating]=useState(false);
  const [error,setError]=useState("");
  const [notice,setNotice]=useState("");
  const [publicKey,setPublicKey]=useState("");
  const [privateKey,setPrivateKey]=useState("");
  const [downloadablePrivateKey,setDownloadablePrivateKey]=useState("");
  const [privateDownloadUrl,setPrivateDownloadUrl]=useState("");
  const [showPrivate,setShowPrivate]=useState(false);
  const [copied,setCopied]=useState(false);
  const [consent,setConsent]=useState(false);
  const [registered,setRegistered]=useState<{memoryIdentifier:string;publicFingerprintSha256:string}|null>(null);
  const [saving,setSaving]=useState(false);
  const [storing,setStoring]=useState(false);
  const [privateFile,setPrivateFile]=useState<File|null>(null);
  const [custodyReady,setCustodyReady]=useState(false);
  const [reading,setReading]=useState(false);
  const [selfTesting,setSelfTesting]=useState(false);
  const [selfTestPassed,setSelfTestPassed]=useState(false);
  const [connectionBusy,setConnectionBusy]=useState(false);
  const [connectionResult,setConnectionResult]=useState<{status:string;message:string;details?:string}|null>(null);

  useEffect(()=>{
    const savedPublicKey=readSandboxStorage(sandboxStorageKey(taxpayerId,"public-key"));
    const savedIdentifier=readSandboxStorage(sandboxStorageKey(taxpayerId,"memory-id"));
    setIdentifier(savedIdentifier);setGenerated(savedPublicKey.length>0);setPublicKey(savedPublicKey);setPrivateKey("");
    setCopied(false);setConsent(false);setError("");setNotice("");setRegistered(null);setDownloadablePrivateKey("");
    setCustodyReady(false);setPrivateFile(null);
    const controller=new AbortController();
    setReading(true);
    void fetch("/api/sandbox-memory/"+encodeURIComponent(taxpayerId),{
      credentials:"same-origin",cache:"no-store",signal:controller.signal
    }).then(async r=>{if(!r.ok)throw new Error("وضعیت حافظه آزمایشی دریافت نشد.");return r.json();})
      .then(value=>{if(!controller.signal.aborted && value.data){
        setRegistered(value.data);setIdentifier(value.data.memoryIdentifier);
        setCustodyReady(value.data.keyCustodyReady===true);
      }}).catch(()=>{if(!controller.signal.aborted)setError("دریافت وضعیت حافظه Sandbox ناموفق بود.");})
      .finally(()=>{if(!controller.signal.aborted)setReading(false);});
    return ()=>controller.abort();
  },[taxpayerId]);

  useEffect(()=>{
    if(!downloadablePrivateKey){setPrivateDownloadUrl("");return;}
    const url="data:application/x-pem-file;charset=utf-8,"+encodeURIComponent(downloadablePrivateKey);
    setPrivateDownloadUrl(url);
  },[downloadablePrivateKey]);

  async function storePrivateKeyPem(pem:string){
    if(!pem.startsWith("-----BEGIN PRIVATE KEY-----")||pem.length>11999)
      throw new Error("فایل Private Key معتبر نیست.");
    const response=await fetch("/api/sandbox-memory/"+
      encodeURIComponent(taxpayerId)+"/private-key",{
      method:"POST",credentials:"same-origin",cache:"no-store",
      headers:{"Content-Type":"application/json"},
      body:JSON.stringify({privateKeyPem:pem}),signal:AbortSignal.timeout(25000)
    });
    if(!response.ok)throw new Error(response.status===409?
      "این کلید با Public Key ثبت‌شده تطابق ندارد یا قبلاً ذخیره شده است.":
      "ذخیره رمزگذاری‌شده کلید انجام نشد؛ تنظیمات نگهداری امن را بررسی کنید.");
    const value=await response.json();
    if(value.environment!=="Sandbox"||value.keyCustodyReady!==true)
      throw new Error("پاسخ نگهداری کلید Sandbox معتبر نیست.");
    setCustodyReady(true);setPrivateFile(null);setPrivateKey("");
    setShowPrivate(false);
  }

  async function registerMemory(){
    if(!generated||!validIdentifier||(registered!==null&&custodyReady)||saving||!publicKey)return;
    setSaving(true);setError("");setNotice("");
    try {
      const response=await fetch("/api/sandbox-memory/"+encodeURIComponent(taxpayerId),{
        method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json"},
        body:JSON.stringify({memoryIdentifier:normalized,publicKeyPem:publicKey}),
        signal:AbortSignal.timeout(18000)
      });
      const value=await response.json().catch(()=>null);
      if(!response.ok)throw new Error(registrationError(response.status,value));
      if(value.environment!=="Sandbox"||!value.data)throw new Error("پاسخ ثبت Sandbox نامعتبر است.");
      setRegistered(value.data);setIdentifier(value.data.memoryIdentifier);
      setNotice("شناسه و کلید عمومی Sandbox با موفقیت ثبت شد.");
      if(privateKey){
        try{await storePrivateKeyPem(privateKey);}
        catch(e){setError(e instanceof Error?e.message:"ذخیره امن کلید ناموفق بود.");}
      }
      removeSandboxStorage(sandboxStorageKey(taxpayerId,"public-key"));
      removeSandboxStorage(sandboxStorageKey(taxpayerId,"memory-id"));
    }catch(e){setError(e instanceof Error?e.message:"ثبت شناسه ناموفق بود.");}
    finally {setSaving(false);}
  }

  async function generateKeys() {
    if (!consent || generating || (registered!==null&&custodyReady)) return;
    setGenerating(true);setError("");setNotice("");
    try {
      if (!globalThis.crypto?.subtle) throw new Error("مرورگر از تولید امن کلید پشتیبانی نمی‌کند.");
      const pair=await crypto.subtle.generateKey({
        name:"RSASSA-PKCS1-v1_5",modulusLength:2048,
        publicExponent:new Uint8Array([1,0,1]),hash:"SHA-256"
      },true,["sign","verify"]);
      const [pub,priv]=await Promise.all([
        crypto.subtle.exportKey("spki",pair.publicKey),
        crypto.subtle.exportKey("pkcs8",pair.privateKey)
      ]);
      const p=toPem(pub,"PUBLIC KEY");
      const s=toPem(priv,"PRIVATE KEY");
      setPublicKey(p);setPrivateKey(s);setDownloadablePrivateKey(s);setGenerated(true);setCopied(false);
      writeSandboxStorage(sandboxStorageKey(taxpayerId,"public-key"),p);
      download("argin-sandbox-public.pem",p);
      window.setTimeout(()=>download("argin-sandbox-private.pem",s),250);
    } catch(e) {
      setGenerated(false);setPublicKey("");setPrivateKey("");setDownloadablePrivateKey("");
      setError(e instanceof Error?e.message:"تولید کلید آزمایشی ناموفق بود.");
    } finally {setGenerating(false);}
  }


  async function storePrivateKey(){
    if(!registered||custodyReady||storing)return;
    setStoring(true);setError("");setNotice("");
    try{
      const pem=privateKey||(privateFile?await privateFile.text():"");
      if(!pem)throw new Error("کلید خصوصی Sandbox در این نشست موجود نیست؛ فایل Private Key را انتخاب کنید.");
      await storePrivateKeyPem(pem);
    }catch(e){setError(e instanceof Error?e.message:"ذخیره امن کلید ناموفق بود.");}
    finally{setStoring(false);}
  }


  async function verifyStoredKey(){
    if(!custodyReady||selfTesting)return;
    setSelfTesting(true);setSelfTestPassed(false);setError("");setNotice("");
    try{
      const response=await fetch("/api/sandbox-memory/"+
        encodeURIComponent(taxpayerId)+"/key-self-test",{
        method:"POST",credentials:"same-origin",cache:"no-store",
        signal:AbortSignal.timeout(20000)
      });
      const value=await response.json();
      if(!response.ok||value.environment!=="Sandbox"||
        value.offlineKeyVerified!==true||value.authorityAuthenticated!==false)
        throw new Error("تأیید آفلاین کلید آزمایشی انجام نشد.");
      setSelfTestPassed(true);
    }catch(e){setError(e instanceof Error?e.message:"آزمون آفلاین کلید ناموفق بود.");}
    finally{setSelfTesting(false);}
  }


  async function testSandboxConnection(){
    if(!custodyReady||connectionBusy)return;
    setConnectionBusy(true);setConnectionResult(null);setError("");
    try{
      const response=await fetch("/api/sandbox-memory/"+
        encodeURIComponent(taxpayerId)+"/connection-test",{
        method:"POST",credentials:"same-origin",cache:"no-store",
        signal:AbortSignal.timeout(35000)
      });
      const value:unknown=await response.json();
      if(!value||typeof value!=="object"||Array.isArray(value))
        throw new Error("پاسخ آزمون اتصال نامعتبر است.");
      const result=value as Record<string,unknown>;
      if(result.environment!=="Sandbox")
        throw new Error("محیط پاسخ اتصال Sandbox معتبر نیست.");
      const status=typeof result.status==="string"?result.status:"Unavailable";
      const codes:Record<string,string>={
        Authenticated:"احراز هویت Sandbox و دریافت اطلاعات حافظه موفق بود؛ فعال‌سازی حافظه در کارپوشه تأیید نشده است.",
        AuthenticationRejected:"احراز هویت در سرور Sandbox رد شد؛ شناسه و ثبت Public Key را بررسی کنید.",
        EndpointUnavailable:"API آزمایشی در دسترس نیست یا پاسخ معتبر HTTP نداده است.",
        TimedOut:"اتصال Sandbox با پایان مهلت مواجه شد.",
        Indeterminate:"پاسخ Sandbox قطعی نبود؛ جزئیات اتصال باید بررسی شود.",
        Unavailable:"آدرس رسمی API Sandbox در تنظیمات سرور تأیید یا فعال نشده است.",
        KeyNotReady:"کلید خصوصی معتبر برای آزمون آماده نیست.",
        UnsupportedProfile:"پروفایل اتصال Sandbox از سمت سرور فعال نشده است.",
        NotAuthorized:"برای این مؤدی مجوز آزمون اتصال ندارید.",
        VaultUnavailable:"نگهداری امن کلید در دسترس نیست."
      };
      if(!Object.prototype.hasOwnProperty.call(codes,status))
        throw new Error("وضعیت ناشناخته از Sandbox دریافت شد.");
      const authenticated=response.ok&&status==="Authenticated"&&result.authenticationConfirmed===true;
      const info=result.fiscalInformation&&typeof result.fiscalInformation==="object"&&!Array.isArray(result.fiscalInformation)
        ?result.fiscalInformation as Record<string,unknown>:null;
      const detail=authenticated&&info?[
        info.nameTrade,info.fiscalStatus,info.economicCode,info.nationalId
      ].filter(v=>typeof v==="string").join(" | "):"";
      setConnectionResult({status:authenticated?"Authenticated":status,
        message:codes[status],details:detail});
    }catch(e){setError(e instanceof Error?e.message:"اتصال Sandbox ناموفق بود.");}
    finally{setConnectionBusy(false);}
  }

  const normalized=identifier.trim().toUpperCase();
  const validIdentifier=/^[A-Z0-9]{6,32}$/.test(normalized);
  return <section className="sandbox-memory-panel" dir="rtl" aria-label="حافظه مالیاتی Sandbox">
    <div className="sandbox-memory-panel__heading">
      <div>
        <h3>حافظه مالیاتی آزمایشی</h3>
        <p>کلیدهای این بخش مستقل از حافظه واقعی هستند؛ هیچ‌کدام از کلیدهای اصلی بازیابی یا جایگزین نمی‌شوند.</p>
      </div>
      <span>Sandbox</span>
    </div>
    <div className="sandbox-memory-panel__steps">
      <section>
        <strong>۱. تولید جفت‌کلید آزمایشی</strong>
        <p>کلید در مرورگر فعلی ساخته می‌شود و در قالب دو فایل PEM تحویل داده می‌شود. برای نگهداری سمت سرور، فایل خصوصی را پس از ثبت شناسه از بخش ۴ وارد کنید. فایل اولیه را در محلی امن نگه دارید.</p>
        <label className="sandbox-memory-panel__consent">
          <input type="checkbox" checked={consent} onChange={e=>setConsent(e.target.checked)}
            disabled={generating}/>
          تأیید می‌کنم این کلید فقط برای محیط آزمایشی ایجاد شود.
        </label>
        <button type="button" className="identity-primary"
          disabled={!consent || generating || (registered!==null&&custodyReady)} onClick={()=>void generateKeys()}>
          {generating?"در حال ایجاد جفت‌کلید…":"ساخت و دانلود جفت‌کلید Sandbox"}
        </button>
        {generated&&<div className="sandbox-memory-panel__files">
          <strong>جفت‌کلید ساخته شد.</strong>
          <button type="button" className="identity-secondary" onClick={()=>{
            download("argin-sandbox-public.pem",publicKey);
          }}>دانلود مجدد Public Key</button>
          {privateKey&&<button type="button" className="identity-secondary" onClick={()=>{
            download("argin-sandbox-private.pem",privateKey);
          }}>دانلود Private Key</button>}
          {!privateKey&&<p role="status">Public Key قبلی بازیابی شد؛ برای نگهداری امن، فایل Private Key دانلودشده را دوباره در بخش ۴ انتخاب کنید.</p>}
          <button type="button" className="identity-secondary" onClick={()=>{
            void navigator.clipboard.writeText(publicKey).then(()=>setCopied(true)).catch(()=>setError("کپی کلید عمومی انجام نشد."));
          }}>{copied?"کپی شد":"کپی کلید عمومی"}</button>
          <button type="button" className="identity-secondary" onClick={()=>setShowPrivate(v=>!v)}>
            {showPrivate?"پنهان کردن کلید خصوصی":"نمایش هشدار نگهداری کلید خصوصی"}
          </button>
          {showPrivate&&<p role="note">فایل خصوصی فقط برای Sandbox است. آن را در کارپوشه بارگذاری نکنید و برای محیط اصلی استفاده نکنید.</p>}
        </div>}
      </section>
      <section>
        <strong>۲. ثبت کلید عمومی در کارپوشه آزمایشی</strong>
        <p>پس از ورود به کارپوشه Sandbox، کلید عمومی را ثبت و شناسه آزمایشی حافظه مالیاتی را از سازمان دریافت کنید.</p>
        <a href="https://sandboxrc.tax.gov.ir" target="_blank" rel="noopener noreferrer">
          باز کردن کارپوشه آزمایشی
        </a>
      </section>
      <section>
        <strong>۳. شناسه حافظه مالیاتی Sandbox</strong>
        <label htmlFor="sandbox-memory-code">کد دریافتی از کارپوشه آزمایشی</label>
        <input id="sandbox-memory-code" type="text" dir="ltr" value={identifier}
          autoComplete="off" maxLength={32} spellCheck={false} placeholder="شناسه آزمایشی"
          onChange={e=>{const value=e.target.value.toUpperCase().replace(/[^A-Z0-9]/g,"");setIdentifier(value);writeSandboxStorage(sandboxStorageKey(taxpayerId,"memory-id"),value);}}/>
        {identifier&&!validIdentifier&&<p role="status">قالب شناسه را بررسی کنید.</p>}
        {reading&&<p role="status">در حال بارگذاری اطلاعات Sandbox…</p>}
        {registered&&<p role="status">شناسه آزمایشی ثبت شده است. اثر انگشت کلید عمومی: <bdi dir="ltr">{registered.publicFingerprintSha256}</bdi></p>}
        {registered&&!custodyReady&&<p className="sandbox-memory-panel__warning">Private Key این ثبت هنوز در نگهداری امن وجود ندارد؛ می‌توانید یک جفت‌کلید جدید بسازید و Public Key جدید را دوباره در کارپوشه بارگذاری کنید.</p>}
        <p className="field-hint">در این مرحله فقط شناسه آزمایشی و Public Key ثبت می‌شود. سپس می‌توانید کلید خصوصی را جداگانه و رمزگذاری‌شده ذخیره کنید. تغییر کلید نیازمند فرایند کنترل‌شده است.</p>
        <button type="button" className="identity-secondary"
          disabled={!generated||!validIdentifier||(registered!==null&&custodyReady)||saving||reading}
          onClick={()=>void registerMemory()}>
          {saving?"در حال ثبت…":registered&&custodyReady?"ثبت شده":registered?"ثبت/جایگزینی کلید عمومی":"ثبت شناسه و کلید عمومی Sandbox"}
        </button>
      </section>
      <section>
        <strong>۴. نگهداری رمزگذاری‌شده کلید خصوصی Sandbox</strong>
        <p>{privateKey?"کلید خصوصی تولیدشده در همین نشست آماده نگهداری امن است؛ نیازی به انتخاب فایل نیست.":"فایل argin-sandbox-private.pem را انتخاب کنید؛ سرور تطابق آن را با کلید عمومی ثبت‌شده کنترل می‌کند و فقط نسخه رمزگذاری‌شده را نگه می‌دارد."} این عملیات کلید اصلی را تغییر نمی‌دهد.</p>
        {downloadablePrivateKey&&<button type="button" className="identity-secondary" onClick={()=>download("argin-sandbox-private.pem",downloadablePrivateKey)}>
          دانلود Private Key همین نشست
        </button>}
        {privateDownloadUrl&&<>
          <a className="identity-secondary" href={privateDownloadUrl} download="argin-sandbox-private.pem">
            دانلود مستقیم Private Key
          </a>
          <button type="button" className="identity-secondary" onClick={()=>{
            void navigator.clipboard.writeText(downloadablePrivateKey).then(()=>setNotice("متن Private Key کپی شد؛ آن را فقط در فایل امن با پسوند pem ذخیره کنید.")).catch(()=>setError("کپی Private Key انجام نشد."));
          }}>کپی Private Key</button>
        </>}
        <label>فایل Private Key آزمایشی
          <input type="file" accept=".pem" disabled={!registered||custodyReady||storing}
            onChange={e=>setPrivateFile(e.target.files?.[0]??null)}/>
        </label>
        <button type="button" className="identity-secondary"
          disabled={!registered||custodyReady||storing||(!privateKey&&!privateFile)}
          onClick={()=>void storePrivateKey()}>
          {storing?"در حال رمزگذاری…":custodyReady?"کلید خصوصی امن ثبت شده":"ذخیره امن Private Key آزمایشی"}
        </button>
        {custodyReady&&<p role="status">کلید خصوصی Sandbox با رمزگذاری سمت سرور ذخیره شده است.</p>}
        <button type="button" className="identity-secondary" disabled={!custodyReady||selfTesting}
          onClick={()=>void verifyStoredKey()}>
          {selfTesting?"در حال آزمون رمزنگاری…":"تست صحت کلید ذخیره‌شده (آفلاین)"}
        </button>
        {selfTestPassed&&<p role="status">تطبیق جفت‌کلید و امضای چالش آفلاین موفق بود؛ این نتیجه تست اتصال سازمان نیست.</p>}
      </section>
      <section>
        <strong>۵. تست اتصال Sandbox</strong>
        <p>درخواست واقعی صرفاً با کلید Sandbox و آدرس API آزمایشی تأییدشده در تنظیمات سرور ارسال می‌شود. اتصال به سرور اصلی <bdi dir="ltr">tp.tax.gov.ir</bdi> ممنوع است. در صورت نبود پیکربندی معتبر، سرور نتیجه «غیرفعال» برمی‌گرداند.</p>
        <button type="button" className="identity-primary"
          disabled={!custodyReady||connectionBusy} onClick={()=>void testSandboxConnection()}>
          {connectionBusy?"در حال دریافت توکن و استعلام حافظه…":"تست اتصال واقعی Sandbox"}
        </button>
        {connectionResult&&<div role="status" className="sandbox-memory-panel__warning">
          <strong>{connectionResult.status==="Authenticated"?"اتصال Sandbox موفق":"نتیجه تست اتصال Sandbox"}</strong>
          <p>{connectionResult.message}</p>
          {connectionResult.details&&<p>{connectionResult.details}</p>}
        </div>}
      </section>
    </div>
    {notice&&<OperationMessageBox key={notice} kind="success" title="ثبت Sandbox انجام شد">{notice}</OperationMessageBox>}
    {error&&<OperationMessageBox kind="error" title="خطای عملیات Sandbox">{error}</OperationMessageBox>}
    <p className="sandbox-memory-panel__warning">این بخش در مرحله آماده‌سازی است: تولید کلید و ثبت مستقل شناسه و کلید عمومی انجام می‌شود؛ نگهداری رمزگذاری‌شده Private Key پس از ثبت شناسه فعال است؛ تست اتصال واقعی فقط با پیکربندی صریح و تأییدشده API Sandbox روی سرور قابل انجام است. حافظه اصلی بدون تغییر می‌ماند.</p>
  </section>;
}
