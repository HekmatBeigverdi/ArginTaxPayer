"use client";

import { useCallback, useEffect, useState } from "react";
import ExistingKeyImport from "./existing-key-import";
import SigningKeyGeneration from "./signing-key-generation";
import SigningConnectionTest from "./signing-connection-test";
import OperationMessageBox from "./operation-message-box";
import SigningChangeRequestPanel from "./signing-change-request-panel";
import SandboxFiscalMemoryPanel from "./sandbox-fiscal-memory-panel";

type Memory = { id: string; taxpayerId: string; memoryIdentifier: string; status: number; revision: number };
type KeyStatus = {
  hasKey: boolean; lifecycleState: number | null; recoveryAvailable: boolean;
  fingerprintSha256: string | null; configured: boolean; storageReady: boolean;
};
type Method = "existing" | "generate";

export default function TaxpayerConnectionPanel({ taxpayerId, memories, loadingMemories, onLoadingChange }: {
  taxpayerId: string; memories: Memory[]; loadingMemories: boolean;
  onLoadingChange?: (value: boolean) => void;
}) {
  const [method, setMethod] = useState<Method>("existing");
  const [environment, setEnvironment] = useState<"production" | "sandbox">("production");
  const [selection, setSelection] = useState("");
  const memoryId = memories.some(m => m.id === selection) ? selection : memories[0]?.id ?? "";
  const currentMemory = memories.find(m => m.id === memoryId);
  // Pass exactly the selected fiscal memory so all three independent actions
  // act on the same memory; never silently switch to a different key.
  const chosen = currentMemory ? [currentMemory] : [];
  const [status, setStatus] = useState<KeyStatus | null>(null);
  const [statusError, setStatusError] = useState("");
  const [reading, setReading] = useState(false);
  const [revision, setRevision] = useState(0);

  const refresh = useCallback(() => setRevision(v => v + 1), []);
  useEffect(() => {
    if (!memoryId) { setStatus(null); setStatusError(""); return; }
    let active = true;
    setReading(true); setStatusError(""); setStatus(null);
    void fetch(`/api/signing/${taxpayerId}/${memoryId}`, {
      credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(15000)
    }).then(async response => {
      const data = await response.json();
      if (!response.ok) throw new Error("دریافت وضعیت کلید امکان‌پذیر نیست.");
      if (active) setStatus(data as KeyStatus);
    }).catch(() => { if (active) setStatusError("وضعیت کلید دریافت نشد؛ بازخوانی کنید."); })
      .finally(() => { if (active) setReading(false); });
    return () => { active = false; };
  }, [taxpayerId, memoryId, revision]);

  const statusLabel = !status ? "نامشخص" : status.recoveryAvailable ? "نیازمند بازیابی" :
    !status.hasKey ? "ثبت نشده" : status.lifecycleState === 2 ? "آماده برای آزمون" : "در انتظار تکمیل";

  return <div className="taxpayer-connection-workspace" dir="rtl">
    <div className="signing-panel-heading">
      <div>
        <h2>کلید و اتصال به سامانه مؤدیان</h2>
        <p>روش تأمین کلید را انتخاب کنید. تست اتصال، مستقل از روش انتخاب‌شده و فقط‌خواندنی است.</p>
      </div>
      <button type="button" className="identity-secondary" disabled={reading || loadingMemories}
        onClick={refresh}>بازخوانی وضعیت</button>
    </div>

    <div className="signing-method-tabs" role="tablist" aria-label="انتخاب محیط حافظه مالیاتی">
      <button type="button" role="tab" aria-selected={environment === "production"}
        aria-controls="taxpayer-production-connection" id="taxpayer-production-tab"
        onClick={()=>setEnvironment("production")}>حافظه اصلی (Production)</button>
      <button type="button" role="tab" aria-selected={environment === "sandbox"}
        aria-controls="taxpayer-sandbox-connection" id="taxpayer-sandbox-tab"
        onClick={()=>setEnvironment("sandbox")}>حافظه آزمایشی (Sandbox)</button>
    </div>
    {environment === "sandbox" ? <div id="taxpayer-sandbox-connection" role="tabpanel"
      aria-labelledby="taxpayer-sandbox-tab"><SandboxFiscalMemoryPanel taxpayerId={taxpayerId}/></div> :
    <div id="taxpayer-production-connection" role="tabpanel"
      aria-labelledby="taxpayer-production-tab">
    <div className="signing-status-bar">
      <label htmlFor="signing-memory-selector">حافظه مالیاتی
        <select id="signing-memory-selector" dir="ltr" disabled={reading || loadingMemories}
          value={memoryId} onChange={e => { setSelection(e.target.value); setMethod("existing"); }}>
          {memories.length === 0 && <option value="">حافظه‌ای ثبت نشده</option>}
          {memories.map(m => <option key={m.id} value={m.id}>{m.memoryIdentifier}</option>)}
        </select>
      </label>
      <div><small>وضعیت کلید</small><strong>{reading ? "در حال بررسی…" : statusLabel}</strong></div>
      <div><small>نگهداری امن</small><strong>{status ? status.configured && status.storageReady ? "آماده" : "غیرفعال" : "نامشخص"}</strong></div>
    </div>
    {statusError && <OperationMessageBox key={statusError + revision} kind="error"
      title="وضعیت کلید دریافت نشد">{statusError}</OperationMessageBox>}
    {status?.recoveryAvailable && <OperationMessageBox key={memoryId} kind="warning"
      title="عملیات کلید نیمه‌تمام است">برای جلوگیری از ایجاد کلید تکراری، تب «کلید موجود» را باز و عملیات ذخیره‌شده را بازیابی کنید.</OperationMessageBox>}

    <div className="signing-method-tabs" role="tablist" aria-label="انتخاب روش کلید">
      <button type="button" id="signing-existing-tab" role="tab" aria-selected={method === "existing"}
        aria-controls="signing-existing-panel"
        onClick={() => setMethod("existing")}>کلید خصوصی موجود</button>
      <button type="button" id="signing-generate-tab" role="tab" aria-selected={method === "generate"}
        aria-controls="signing-generate-panel"
        onClick={() => setMethod("generate")}>ساخت کلید جدید</button>
    </div>
    <div id="signing-existing-panel" role="tabpanel" aria-labelledby="signing-existing-tab"
      hidden={method !== "existing"} className="signing-method-panel">
      {method === "existing" && <>
        <p className="signing-method-intro">اگر قبلاً کلید خصوصی خود را دارید، همان کلید را ثبت کنید؛ نیازی به تولید جفت‌کلید جدید نیست.</p>
        <ExistingKeyImport key={taxpayerId + ":" + memoryId} taxpayerId={taxpayerId}
          memories={chosen} loadingMemories={loadingMemories} onLoadingChange={onLoadingChange}
          hideMemorySelector onCompleted={refresh}/>
      </>}
    </div>
    <div id="signing-generate-panel" role="tabpanel" aria-labelledby="signing-generate-tab"
      hidden={method !== "generate"} className="signing-method-panel">
      {method === "generate" && <>
        <p className="signing-method-intro">اگر کلید ندارید، نرم‌افزار جفت‌کلید می‌سازد. فایل خصوصی فقط یک‌بار تحویل داده می‌شود؛ سپس کلید عمومی را در کارپوشه ثبت کنید.</p>
        <SigningKeyGeneration key={taxpayerId + ":" + memoryId} taxpayerId={taxpayerId}
          memories={chosen} loadingMemories={loadingMemories} hideMemorySelector onCompleted={refresh}/>
        <p className="signing-method-next">مراحل: ساخت کلید ← نگهداری فایل دانلودشده ← ثبت کلید عمومی در کارپوشه ← تست اتصال</p>
      </>}
    </div>
    <div className="signing-test-panel">
      <SigningConnectionTest key={taxpayerId + ":" + memoryId} taxpayerId={taxpayerId}
        memories={chosen} loadingMemories={loadingMemories} hideMemorySelector/>
    </div>
    {memoryId && <SigningChangeRequestPanel taxpayerId={taxpayerId} memoryId={memoryId}/>}
    </div>}
  </div>;
}
