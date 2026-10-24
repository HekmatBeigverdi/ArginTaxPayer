"use client";

import {useEffect,useState} from "react";
import OperationMessageBox from "./operation-message-box";

type KeyFiles = {publicPem:string;privatePem:string};
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
 * memory identifier or real connection-test endpoint. Generated key is only
 * delivered to operator; private PEM is not posted or logged. Until isolated
 * server custody and the official Sandbox API origin are installed, neither
 * identifier registration nor connection test may claim success.
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

  useEffect(()=>{
    setIdentifier("");setGenerated(false);setPublicKey("");setPrivateKey("");
    setCopied(false);setConsent(false);setError("");
  },[taxpayerId]);

  async function generateKeys() {
    if (!consent || generating) return;
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
        <p>کلید در مرورگر فعلی ساخته می‌شود و در قالب دو فایل PEM تحویل داده می‌شود. این نسخه هنوز کلید خصوصی را در سرور ذخیره نمی‌کند؛ فایل را در محلی امن نگه دارید.</p>
        <label className="sandbox-memory-panel__consent">
          <input type="checkbox" checked={consent} onChange={e=>setConsent(e.target.checked)}
            disabled={generating}/>
          تأیید می‌کنم این کلید فقط برای محیط آزمایشی ایجاد شود.
        </label>
        <button type="button" className="identity-primary"
          disabled={!consent || generating} onClick={()=>void generateKeys()}>
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
        <p className="field-hint">فعلاً فقط در فرم نگهداری می‌شود. تا آماده‌شدن ذخیره‌سازی مستقل و رمزگذاری‌شده Sandbox، امکان ثبت نهایی آن در پایگاه داده فعال نیست.</p>
        <button type="button" className="identity-secondary" disabled title="نگهداری مستقل Sandbox هنوز در سرور پیاده‌سازی نشده">
          ثبت شناسه در Sandbox
        </button>
      </section>
      <section>
        <strong>۴. تست اتصال Sandbox</strong>
        <p>این دکمه نباید درخواست را به <bdi dir="ltr">tp.tax.gov.ir</bdi> ارسال کند. تا تأیید آدرس API اختصاصی Sandbox و نگهداری امن کلید، اتصال غیرفعال است.</p>
        <button type="button" className="identity-primary" disabled title="آدرس رسمی API سندباکس و نگهداری امن کلید باید تأیید شود">
          تست اتصال Sandbox
        </button>
      </section>
    </div>
    {error&&<OperationMessageBox kind="error" title="خطای تولید کلید">{error}</OperationMessageBox>}
    <p className="sandbox-memory-panel__warning">این بخش در مرحله آماده‌سازی است: تولید و دانلود جفت‌کلید کار می‌کند؛ ثبت پایدار شناسه، نگهداری امن کلید و تست اتصال زنده هنوز فعال نشده‌اند. حافظه اصلی و کلید آن بدون تغییر باقی می‌مانند.</p>
  </section>;
}
