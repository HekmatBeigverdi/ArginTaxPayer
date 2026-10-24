"use client";

import {useEffect,useState} from "react";
import OperationMessageBox from "./operation-message-box";

function toPem(bytes:ArrayBuffer,label:string):string {
  const chars=Array.from(new Uint8Array(bytes),v=>String.fromCharCode(v)).join("");
  const encoded=btoa(chars).match(/.{1,64}/g)?.join("\n") ?? "";
  return "-----BEGIN "+label+"-----\n"+encoded+"\n-----END "+label+"-----\n";
}
function download(name:string,contents:string) {
  const url=URL.createObjectURL(new Blob([contents],{type:"application/x-pem-file"}));
  const element=document.createElement("a");
  element.href=url;element.download=name;
  document.body.appendChild(element);element.click();element.remove();
  window.setTimeout(()=>URL.revokeObjectURL(url),1000);
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
  const [publicKey,setPublicKey]=useState("");
  const [privateKey,setPrivateKey]=useState("");
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

  useEffect(()=>{
    setIdentifier("");setGenerated(false);setPublicKey("");setPrivateKey("");
    setCopied(false);setConsent(false);setError("");setRegistered(null);
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

  async function registerMemory(){
    if(!generated||!validIdentifier||registered||saving||!publicKey)return;
    setSaving(true);setError("");
    try {
      const response=await fetch("/api/sandbox-memory/"+encodeURIComponent(taxpayerId),{
        method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json"},
        body:JSON.stringify({memoryIdentifier:normalized,publicKeyPem:publicKey}),
        signal:AbortSignal.timeout(18000)
      });
      if(!response.ok)throw new Error(response.status===409?
        "برای این مؤدی حافظه آزمایشی ثبت شده است؛ بازخوانی کنید.":"ثبت شناسه آزمایشی انجام نشد.");
      const value=await response.json();
      if(value.environment!=="Sandbox"||!value.data)throw new Error("پاسخ ثبت Sandbox نامعتبر است.");
      setRegistered(value.data);setIdentifier(value.data.memoryIdentifier);
    }catch(e){setError(e instanceof Error?e.message:"ثبت شناسه ناموفق بود.");}
    finally {setSaving(false);}
  }

  async function generateKeys() {
    if (!consent || generating || registered) return;
    setGenerating(true);setError("");
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
      setPublicKey(p);setPrivateKey(s);setGenerated(true);setCopied(false);
      download("argin-sandbox-public.pem",p);
      download("argin-sandbox-private.pem",s);
    } catch(e) {
      setGenerated(false);setPublicKey("");setPrivateKey("");
      setError(e instanceof Error?e.message:"تولید کلید آزمایشی ناموفق بود.");
    } finally {setGenerating(false);}
  }


  async function storePrivateKey(){
    if(!registered||custodyReady||storing)return;
    setStoring(true);setError("");
    try{
      if(!privateFile)throw new Error("ابتدا فایل خصوصی Sandbox را انتخاب کنید.");
      if(privateFile.size>12000)throw new Error("حجم فایل کلید نامعتبر است.");
      const pem=await privateFile.text();
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
    }catch(e){setError(e instanceof Error?e.message:"ذخیره امن کلید ناموفق بود.");}
    finally{setStoring(false);}
  }


  async function verifyStoredKey(){
    if(!custodyReady||selfTesting)return;
    setSelfTesting(true);setSelfTestPassed(false);setError("");
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
          disabled={!consent || generating || !!registered} onClick={()=>void generateKeys()}>
          {generating?"در حال ایجاد جفت‌کلید…":"ساخت و دانلود جفت‌کلید Sandbox"}
        </button>
        {generated&&<div className="sandbox-memory-panel__files">
          <strong>جفت‌کلید ساخته شد.</strong>
          <button type="button" className="identity-secondary" onClick={()=>{
            download("argin-sandbox-public.pem",publicKey);
          }}>دانلود مجدد Public Key</button>
          <button type="button" className="identity-secondary" onClick={()=>{
            download("argin-sandbox-private.pem",privateKey);
          }}>دانلود Private Key</button>
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
          onChange={e=>setIdentifier(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g,""))}/>
        {identifier&&!validIdentifier&&<p role="status">قالب شناسه را بررسی کنید.</p>}
        {reading&&<p role="status">در حال بارگذاری اطلاعات Sandbox…</p>}
        {registered&&<p role="status">شناسه آزمایشی ثبت شده است. اثر انگشت کلید عمومی: <bdi dir="ltr">{registered.publicFingerprintSha256}</bdi></p>}
        <p className="field-hint">در این مرحله فقط شناسه آزمایشی و Public Key ثبت می‌شود. سپس می‌توانید کلید خصوصی را جداگانه و رمزگذاری‌شده ذخیره کنید. تغییر کلید نیازمند فرایند کنترل‌شده است.</p>
        <button type="button" className="identity-secondary"
          disabled={!generated||!validIdentifier||!!registered||saving||reading}
          onClick={()=>void registerMemory()}>
          {saving?"در حال ثبت…":registered?"ثبت شده":"ثبت شناسه و کلید عمومی Sandbox"}
        </button>
      </section>
      <section>
        <strong>۴. نگهداری رمزگذاری‌شده کلید خصوصی Sandbox</strong>
        <p>فایل <bdi dir="ltr">argin-sandbox-private.pem</bdi> را انتخاب کنید؛ سرور تطابق آن را با کلید عمومی ثبت‌شده کنترل می‌کند و فقط نسخه رمزگذاری‌شده را نگه می‌دارد. این عملیات کلید اصلی را تغییر نمی‌دهد.</p>
        <label>فایل Private Key آزمایشی
          <input type="file" accept=".pem" disabled={!registered||custodyReady||storing}
            onChange={e=>setPrivateFile(e.target.files?.[0]??null)}/>
        </label>
        <button type="button" className="identity-secondary"
          disabled={!registered||custodyReady||storing||!privateFile}
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
        <p>این دکمه نباید درخواست را به <bdi dir="ltr">tp.tax.gov.ir</bdi> ارسال کند. تا تأیید آدرس API اختصاصی Sandbox و آزمون ایمن آن، اتصال غیرفعال است.</p>
        <button type="button" className="identity-primary" disabled title="آدرس رسمی API سندباکس هنوز تأیید نشده است">
          تست اتصال Sandbox
        </button>
      </section>
    </div>
    {error&&<OperationMessageBox kind="error" title="خطای تولید کلید">{error}</OperationMessageBox>}
    <p className="sandbox-memory-panel__warning">این بخش در مرحله آماده‌سازی است: تولید کلید و ثبت مستقل شناسه و کلید عمومی انجام می‌شود؛ نگهداری رمزگذاری‌شده Private Key پس از ثبت شناسه فعال است؛ تست اتصال زنده تا تأیید API رسمی فعال نمی‌شود. حافظه اصلی بدون تغییر می‌ماند.</p>
  </section>;
}
