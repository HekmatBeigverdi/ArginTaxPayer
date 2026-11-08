"use client";

import { useCallback, useEffect, useState } from "react";
import ExistingKeyImport from "./existing-key-import";
import SigningKeyGeneration from "./signing-key-generation";
import SigningConnectionTest from "./signing-connection-test";
import OperationMessageBox from "./operation-message-box";
import SigningChangeRequestPanel from "./signing-change-request-panel";
import SandboxFiscalMemoryPanel from "./sandbox-fiscal-memory-panel";
import FiscalMemoryAuthorityActions from "./fiscal-memory-authority-actions";

type Memory = { id: string; taxpayerId: string; memoryIdentifier: string; status: number; revision: number;
  authorityEnvironment: 1 | 2 | null; lastConnectionTestSucceeded: boolean | null; lastConnectionTestAtUtc: string | null;
  canVerifyAndActivate?: boolean };
type KeyStatus = {
  hasKey: boolean; lifecycleState: number | null; recoveryAvailable: boolean;
  fingerprintSha256: string | null; configured: boolean; storageReady: boolean;
};
type Method = "existing" | "generate";

export default function TaxpayerConnectionPanel({ taxpayerId, memories, loadingMemories, onLoadingChange, isLevel1 = false, onMemoriesChanged }: {
  taxpayerId: string; memories: Memory[]; loadingMemories: boolean;
  onLoadingChange?: (value: boolean) => void;
  isLevel1?: boolean; onMemoriesChanged?: () => Promise<void>;
}) {
  const [method, setMethod] = useState<Method>("existing");
  const [environment, setEnvironment] = useState<"production" | "sandbox">("production");
  const [selection, setSelection] = useState("");
  // A UI tab is a display filter only, never a tax-authority routing toggle.
  // Legacy unclassified memories may use the isolated, one-time read-only probe.
  const productionMemories = memories.filter(m => m.authorityEnvironment === 1);
  const unclassifiedMemories = memories.filter(m => m.authorityEnvironment == null);
  const productionSelectableMemories = [...productionMemories, ...unclassifiedMemories];
  const unclassifiedCount = memories.filter(m => m.authorityEnvironment == null).length;
  const memoryId = productionSelectableMemories.some(m => m.id === selection) ? selection : productionSelectableMemories[0]?.id ?? "";
  const currentProductionMemory = productionSelectableMemories.find(m => m.id === memoryId);
  const classifiedSandboxMemories = memories.filter(m => m.authorityEnvironment === 2);
  const [sandboxSelection,setSandboxSelection] = useState("");
  const sandboxMemoryId = classifiedSandboxMemories.some(m => m.id === sandboxSelection) ? sandboxSelection : classifiedSandboxMemories[0]?.id ?? "";
  const selectedMemoryId = environment === "production" ? memoryId : sandboxMemoryId;
  const [allowExistingKeyEntry, setAllowExistingKeyEntry] = useState(false);
  useEffect(() => { setAllowExistingKeyEntry(false); }, [taxpayerId, selectedMemoryId]);
  const currentMemory = environment === "production" ? currentProductionMemory : classifiedSandboxMemories.find(m => m.id === sandboxMemoryId);
  const [testedMemoryId,setTestedMemoryId] = useState("");
  // Pass exactly the selected fiscal memory so all three independent actions
  // act on the same memory; never silently switch to a different key.
  const chosen = currentMemory ? [currentMemory] : [];
  const [status, setStatus] = useState<KeyStatus | null>(null);
  const [statusError, setStatusError] = useState("");
  const [reading, setReading] = useState(false);
  const [revision, setRevision] = useState(0);

  const refresh = useCallback(() => { setRevision(v => v + 1); setTestedMemoryId(""); }, []);
  const memoriesChanged = async () => { await onMemoriesChanged?.(); refresh(); };
  useEffect(() => {
    if (!selectedMemoryId) { setStatus(null); setStatusError(""); return; }
    let active = true;
    setReading(true); setStatusError(""); setStatus(null);
    void fetch(`/api/signing/${taxpayerId}/${selectedMemoryId}`, {
      credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(15000)
    }).then(async response => {
      const data = await response.json();
      if (!response.ok) throw new Error("دریافت وضعیت کلید امکان‌پذیر نیست.");
      if (active) setStatus(data as KeyStatus);
    }).catch(() => { if (active) setStatusError("وضعیت کلید دریافت نشد؛ بازخوانی کنید."); })
      .finally(() => { if (active) setReading(false); });
    return () => { active = false; };
  }, [taxpayerId, selectedMemoryId, revision]);

  const statusLabel = !status ? "نامشخص" : status.recoveryAvailable ? "نیازمند بازیابی" :
    !status.hasKey ? "ثبت نشده" : status.lifecycleState === 2 ? "آماده برای آزمون" : "در انتظار تکمیل";

  return <div className="taxpayer-connection-workspace" dir="rtl">
    <div className="signing-panel-heading">
      <div>
        <h2>کلید و اتصال به سامانه مؤدیان</h2>
        <p>روش تأمین کلید را انتخاب کنید. تست اتصال، مستقل از روش انتخاب‌شده و فقط‌خواندنی است.</p>
      </div>
      <button type="button" className="identity-secondary" disabled={reading || loadingMemories}
        onClick={()=>{refresh();void onMemoriesChanged?.();}}>بازخوانی وضعیت</button>
    </div>

    <div className="signing-method-tabs" role="tablist" aria-label="انتخاب محیط حافظه مالیاتی">
      <button type="button" role="tab" aria-selected={environment === "production"}
        aria-controls="taxpayer-production-connection" id="taxpayer-production-tab"
        onClick={()=>setEnvironment("production")}>حافظه اصلی (Production)</button>
      <button type="button" role="tab" aria-selected={environment === "sandbox"}
        aria-controls="taxpayer-sandbox-connection" id="taxpayer-sandbox-tab"
        onClick={()=>setEnvironment("sandbox")}>حافظه آزمایشی (Sandbox)</button>
    </div>
    {environment === "sandbox" && <SandboxFiscalMemoryPanel taxpayerId={taxpayerId}/>}
    <div id={environment === "production"?"taxpayer-production-connection":"taxpayer-sandbox-connection"} role="tabpanel"
      aria-labelledby={environment === "production"?"taxpayer-production-tab":"taxpayer-sandbox-tab"}>
    <h3>{environment === "production"?"کلید حافظه اصلی (Production)":"کلید حافظه ثبت‌شده Sandbox"}</h3>
    <FiscalMemoryAuthorityActions taxpayerId={taxpayerId} isLevel1={isLevel1} memories={memories}
      selectedMemory={currentMemory??null} testedMemoryId={testedMemoryId} onChanged={memoriesChanged}/>
    {unclassifiedCount > 0 && <OperationMessageBox kind="warning" title="حافظه با محیط نامشخص">
      {unclassifiedCount} حافظه قدیمی را می‌توانید در تب Production انتخاب کنید تا کلید خصوصی همان حافظه را ثبت و آزمون فقط‌خواندنی Production انجام دهید. اگر پاسخ سازمان فعال‌بودن حافظه و تطابق هویت را تأیید کند، محیط ثبت می‌شود و مالک مؤدی می‌تواند بررسی و فعال‌سازی نهایی را از همین پنل انجام دهد.
    </OperationMessageBox>}
    <div className="signing-status-bar">
      <label htmlFor="signing-memory-selector">حافظه مالیاتی
        <select id="signing-memory-selector" dir="ltr" disabled={reading || loadingMemories}
          value={selectedMemoryId} onChange={e => { if(environment === "production")setSelection(e.target.value);else setSandboxSelection(e.target.value);setMethod("existing");setTestedMemoryId(""); }}>
          {environment === "production" ? <>
            {productionSelectableMemories.length === 0 && <option value="">حافظه Production تأییدشده‌ای وجود ندارد</option>}
            {productionSelectableMemories.map(m => <option key={m.id} value={m.id}>{m.memoryIdentifier}{m.authorityEnvironment == null ? " — محیط نامشخص (آزمون یک‌باره)" : ""}</option>)}
          </> : <>
            {classifiedSandboxMemories.length === 0 && <option value="">حافظه ثبت‌شده Sandbox وجود ندارد</option>}
            {classifiedSandboxMemories.map(m => <option key={m.id} value={m.id}>{m.memoryIdentifier}</option>)}
          </> }
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
        <ExistingKeyImport key={taxpayerId + ":" + selectedMemoryId} taxpayerId={taxpayerId}
          memories={chosen} loadingMemories={loadingMemories} onLoadingChange={onLoadingChange}
          hideMemorySelector allowExistingKeyEntry={allowExistingKeyEntry}
          onCompleted={() => { setAllowExistingKeyEntry(false); refresh(); }}/>
      </>}
    </div>
    <div id="signing-generate-panel" role="tabpanel" aria-labelledby="signing-generate-tab"
      hidden={method !== "generate"} className="signing-method-panel">
      {method === "generate" && <>
        <p className="signing-method-intro">اگر کلید ندارید، نرم‌افزار جفت‌کلید می‌سازد. فایل خصوصی فقط یک‌بار تحویل داده می‌شود؛ سپس کلید عمومی را در کارپوشه ثبت کنید.</p>
        <SigningKeyGeneration key={taxpayerId + ":" + selectedMemoryId} taxpayerId={taxpayerId}
          memories={chosen} loadingMemories={loadingMemories} hideMemorySelector onCompleted={refresh}/>
        <p className="signing-method-next">مراحل: ساخت کلید ← نگهداری فایل دانلودشده ← ثبت کلید عمومی در کارپوشه ← تست اتصال</p>
      </>}
    </div>
    <div className="signing-test-panel">
      <SigningConnectionTest key={taxpayerId + ":" + selectedMemoryId} taxpayerId={taxpayerId}
        memories={chosen} loadingMemories={loadingMemories} hideMemorySelector
        oneTimeEnvironment={currentMemory?.authorityEnvironment == null ? "Production" : undefined}
        onReenterPrivateKey={()=>{setMethod("existing");setAllowExistingKeyEntry(true);setTestedMemoryId("");}}
        onTestCompleted={authenticated=>{
          if (authenticated) setAllowExistingKeyEntry(false);
          if(onMemoriesChanged)void onMemoriesChanged().catch(()=>undefined);
        }}
        onAuthenticated={authenticated=>{
          setTestedMemoryId(authenticated?selectedMemoryId:"");
          if (authenticated) setAllowExistingKeyEntry(false);
        }}/>
    </div>
    {selectedMemoryId && <SigningChangeRequestPanel taxpayerId={taxpayerId} memoryId={selectedMemoryId}/>}
    </div>
  </div>;
}
