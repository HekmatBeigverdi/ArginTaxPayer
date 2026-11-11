"use client";

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import ExistingKeyImport from "./existing-key-import";
import SigningKeyGeneration from "./signing-key-generation";
import SigningConnectionTest from "./signing-connection-test";
import OperationMessageBox from "./operation-message-box";
import SigningChangeRequestPanel from "./signing-change-request-panel";
import SandboxFiscalMemoryPanel from "./sandbox-fiscal-memory-panel";
import FiscalMemoryAuthorityActions from "./fiscal-memory-authority-actions";
import ConnectionSummary from "./connection-summary";
import FiscalMemoryChangeRequestPanel from "./fiscal-memory-change-request-panel";
import { getConnectionPresentation, historyWithLegacyAttempt, projectConnectionHistory, sandboxSourceChoice, type ConnectionHistory } from "../../lib/fiscal-connection-status";

type Memory = { id: string; taxpayerId: string; memoryIdentifier: string; status: number; revision: number;
  authorityEnvironment: 1 | 2 | null; lastConnectionTestSucceeded: boolean | null; lastConnectionTestAtUtc: string | null;
  canVerifyAndActivate?: boolean };
type KeyStatus = { hasKey: boolean; lifecycleState: number | null; recoveryAvailable: boolean;
  fingerprintSha256: string | null; configured: boolean; storageReady: boolean; connectionHistory: ConnectionHistory };
type Legacy = { memoryIdentifier: string; keyCustodyReady: boolean; publicFingerprintSha256?: string };
const activationLabels: Record<number,string> = {1:"در انتظار بررسی و فعال‌سازی",2:"فعال",3:"تعلیق",4:"بازنشسته"};

export default function TaxpayerConnectionPanel({ taxpayerId, memories, loadingMemories, onLoadingChange, isLevel1 = false, onMemoriesChanged, memoryRegistration }: {
  taxpayerId: string; memories: Memory[]; loadingMemories: boolean; onLoadingChange?: (value: boolean) => void;
  isLevel1?: boolean; onMemoriesChanged?: () => Promise<void>; memoryRegistration?: ReactNode;
}) {
  const [method,setMethod] = useState<"existing"|"generate">("existing");
  // A UI tab is a display filter only; persisted bindings select every authority operation.
  const [environment,setEnvironment] = useState<"production"|"sandbox">("production");
  const [selection,setSelection] = useState("");
  const [sandboxSelection,setSandboxSelection] = useState("");
  const productionSelectableMemories = memories.filter(m=>m.authorityEnvironment===1 || m.authorityEnvironment==null);
  const classifiedSandboxMemories = memories.filter(m=>m.authorityEnvironment===2);
  const available = environment==="production" ? productionSelectableMemories : classifiedSandboxMemories;
  const selected = environment==="production" ? selection : sandboxSelection;
  const selectedMemoryId = available.some(m=>m.id===selected) ? selected : available[0]?.id ?? "";
  const currentMemory = available.find(m=>m.id===selectedMemoryId);
  const chosen = currentMemory ? [currentMemory] : [];
  const [managing,setManaging] = useState(false);
  const [source,setSource] = useState<""|"legacy"|"registered">("");
  const [testedMemoryId,setTestedMemoryId] = useState("");
  const [status,setStatus] = useState<KeyStatus|null>(null);
  const [statusError,setStatusError] = useState("");
  const [reading,setReading] = useState(false);
  const [revision,setRevision] = useState(0);
  const [legacy,setLegacy] = useState<Legacy|null>(null);
  const [legacyReading,setLegacyReading] = useState(true);
  const [legacyError,setLegacyError] = useState("");
  const entry = useRef<HTMLDivElement>(null);
  const refresh = useCallback(()=>{setRevision(v=>v+1);setTestedMemoryId("");},[]);
  const changed = async()=>{await onMemoriesChanged?.();refresh();};

  useEffect(()=>{setManaging(false);setSource("");setTestedMemoryId("");setMethod("existing");},[taxpayerId,selectedMemoryId,environment]);
  useEffect(()=>{
    if(!managing)return;
    entry.current?.scrollIntoView({block:"start",behavior:"smooth"});
    entry.current?.querySelector<HTMLTextAreaElement>("textarea:not(:disabled)")?.focus({preventScroll:true});
  },[managing,method]);
  useEffect(()=>{
    const controller=new AbortController();setLegacyReading(true);setLegacyError("");setLegacy(null);
    void fetch(`/api/sandbox-memory/${taxpayerId}`,{credentials:"same-origin",cache:"no-store",signal:controller.signal})
      .then(async r=>{if(!r.ok)throw Error();return r.json();})
      .then(data=>{if(!controller.signal.aborted)setLegacy(data.data??null);})
      .catch(()=>{if(!controller.signal.aborted)setLegacyError("وضعیت حافظهٔ آزمایشی دریافت نشد؛ بازخوانی کنید.");})
      .finally(()=>{if(!controller.signal.aborted)setLegacyReading(false);});
    return()=>controller.abort();
  },[taxpayerId,revision]);
  useEffect(()=>{
    const controller=new AbortController();setStatus(null);setStatusError("");
    if(!selectedMemoryId){setReading(false);return()=>controller.abort();}
    setReading(true);
    void fetch(`/api/signing/${taxpayerId}/${selectedMemoryId}`,{credentials:"same-origin",cache:"no-store",signal:controller.signal})
      .then(async r=>{if(!r.ok)throw Error();return r.json();})
      .then(data=>{if(!controller.signal.aborted)setStatus({...data,connectionHistory:projectConnectionHistory(data.connectionHistory)});})
      .catch(()=>{if(!controller.signal.aborted)setStatusError("وضعیت کلید دریافت نشد؛ بازخوانی کنید.");})
      .finally(()=>{if(!controller.signal.aborted)setReading(false);});
    return()=>controller.abort();
  },[taxpayerId,selectedMemoryId,revision]);

  const choice = sandboxSourceChoice(legacy,currentMemory?{memoryIdentifier:currentMemory.memoryIdentifier,hasKey:status?.hasKey}:null,environment==="sandbox"?2:1);
  const useLegacy = environment==="sandbox" && (choice==="legacy" || (choice==="choice"||choice==="separate")&&source==="legacy" || !legacy && !currentMemory);
  const summaryHistory=historyWithLegacyAttempt(status?.connectionHistory,
    currentMemory?.lastConnectionTestSucceeded??null,currentMemory?.lastConnectionTestAtUtc??null);
  const view = getConnectionPresentation(status?.hasKey===true,summaryHistory);
  const setupOpen = managing || status!==null && !status.hasKey;
  const openEntry = ()=>{setMethod("existing");setManaging(true);setTestedMemoryId("");};

  return <div className="taxpayer-connection-workspace" dir="rtl">
    <div className="signing-panel-heading"><div><h2>حافظه مالیاتی و اتصال</h2>
      <p>هر محیط شناسه، کلید و آزمون مستقل دارد. محیط مورد نظر را انتخاب کنید.</p></div>
      <button type="button" className="identity-secondary" disabled={reading||loadingMemories||legacyReading}
        onClick={()=>{refresh();void onMemoriesChanged?.();}}>بازخوانی وضعیت</button></div>
    <div className="signing-method-tabs" role="tablist" aria-label="محیط حافظه مالیاتی">
      <button type="button" role="tab" id="taxpayer-production-tab" aria-controls="taxpayer-production-connection"
        aria-selected={environment==="production"} onClick={()=>setEnvironment("production")}>واقعی (Production)</button>
      <button type="button" role="tab" id="taxpayer-sandbox-tab" aria-controls="taxpayer-sandbox-connection"
        aria-selected={environment==="sandbox"} onClick={()=>setEnvironment("sandbox")}>آزمایشی (Sandbox)</button>
    </div>
    <div role="tabpanel" id={environment==="production"?"taxpayer-production-connection":"taxpayer-sandbox-connection"}
      aria-labelledby={environment==="production"?"taxpayer-production-tab":"taxpayer-sandbox-tab"}>
      {environment==="sandbox"&&legacyError&&<p role="alert">{legacyError}</p>}
      {available.length>1&&<label>حافظه مالیاتی<select value={selectedMemoryId} disabled={reading||loadingMemories}
        onChange={e=>{if(environment==="production")setSelection(e.target.value);else setSandboxSelection(e.target.value);}}>
        {available.map(m=><option key={m.id} value={m.id}>{m.memoryIdentifier}{m.authorityEnvironment==null?" — محیط تأییدنشده":""}</option>)}
      </select></label>}
      {(choice==="choice"||choice==="separate")&&<div className="connection-source-choice">
        {choice==="choice"&&<p role="status">برای این شناسه دو کلید ثبت شده است. پیکربندی مورد نظر را صریحاً انتخاب کنید؛ کلیدها خودکار جایگزین نمی‌شوند.</p>}
        <label>پیکربندی حافظه<select value={source} onChange={e=>{setSource(e.target.value as ""|"legacy"|"registered");setManaging(false);}}>
          <option value="">انتخاب پیکربندی برای بررسی</option>
          <option value="legacy">Sandbox — {legacy?.memoryIdentifier} · {legacy?.publicFingerprintSha256?.slice(0,12)} (کلید آزمایشی موجود)</option>
          <option value="registered">{currentMemory?.memoryIdentifier} · {status?.fingerprintSha256?.slice(0,12)} (کلید دیگر حافظه)</option>
        </select></label></div>}
      {environment==="sandbox"&&(legacyError||choice==="loading"&&statusError) ? <p role="alert">{legacyError||statusError}</p> : environment==="sandbox"&&(legacyReading||choice==="loading") ? <p role="status">در حال دریافت وضعیت حافظهٔ آزمایشی…</p> : (choice==="choice"||choice==="separate")&&!source ? <p role="status">پیکربندی مورد نظر را برای نمایش وضعیت و آزمون انتخاب کنید.</p> : useLegacy ?
        <SandboxFiscalMemoryPanel key={taxpayerId} taxpayerId={taxpayerId}/> : <>
        {!currentMemory ? <div className="connection-empty"><p>در این محیط حافظه‌ای برای تنظیم انتخاب نشده است.</p>
          {environment==="production"&&memoryRegistration}</div> : <article className="connection-card" aria-label="وضعیت اتصال حافظه انتخاب‌شده">
          {statusError&&<OperationMessageBox kind="error" title="دریافت وضعیت انجام نشد">{statusError}</OperationMessageBox>}
          {reading&&<p role="status">در حال بررسی وضعیت کلید…</p>}
          {status&&<ConnectionSummary identifier={currentMemory.memoryIdentifier} environment={environment==="production"?"Production":"Sandbox"}
            hasKey={status.hasKey} history={summaryHistory} legacyFallbackAvailable={currentMemory.lastConnectionTestAtUtc!==null}
            activationLabel={activationLabels[currentMemory.status]??"نامشخص"}/>}
          {currentMemory.authorityEnvironment==null&&<p role="status" className="field-hint">محیط این حافظه هنوز تأیید نشده است؛ تعیین صریح محیط پیش از استفاده در صورتحساب لازم است.</p>}
          {status?.hasKey&&!managing&&<div className="connection-actions">
            <button type="button" className="identity-secondary" onClick={openEntry}>مدیریت تنظیمات</button>
            {(view.needsKeyAction||status.recoveryAvailable)&&<button type="button" className="identity-primary" onClick={openEntry}>بررسی و بازیابی کلید</button>}
          </div>}
          {setupOpen&&<div className="connection-setup" ref={entry}>
            <div className="signing-panel-heading"><h3>{status?.hasKey?"مدیریت کلید همین حافظه":"تنظیم کلید حافظه"}</h3>
              {status?.hasKey&&<button type="button" className="identity-secondary" onClick={()=>setManaging(false)}>بستن تنظیمات</button>}</div>
            <div className="signing-method-tabs" role="tablist" aria-label="روش تنظیم کلید">
              <button type="button" role="tab" aria-selected={method === "existing"} onClick={()=>setMethod("existing")}>ورود کلید موجود</button>
              <button type="button" role="tab" aria-selected={method === "generate"} onClick={()=>setMethod("generate")}>ساخت کلید جدید</button>
            </div>
            {method === "existing" && <ExistingKeyImport key={taxpayerId+":"+selectedMemoryId} taxpayerId={taxpayerId}
              memories={chosen} loadingMemories={loadingMemories} onLoadingChange={onLoadingChange} hideMemorySelector
              allowExistingKeyEntry={managing} onCompleted={()=>{setManaging(false);refresh();}}/>}
            {method === "generate" && <SigningKeyGeneration key={taxpayerId+":"+selectedMemoryId} taxpayerId={taxpayerId}
              memories={chosen} loadingMemories={loadingMemories} hideMemorySelector onCompleted={()=>{setManaging(false);refresh();}}/>}
            <details><summary>درخواست بررسی یا تغییر کلید</summary><SigningChangeRequestPanel taxpayerId={taxpayerId} memoryId={selectedMemoryId}/></details>
          </div>}
          <SigningConnectionTest key={taxpayerId+":"+selectedMemoryId} taxpayerId={taxpayerId} memories={chosen}
            loadingMemories={loadingMemories||reading} hideMemorySelector compact
            oneTimeEnvironment={currentMemory.authorityEnvironment==null?"Production":undefined}
            onReenterPrivateKey={openEntry} onTestCompleted={authenticated=>{if(authenticated)setManaging(false);void changed();}}
            onAuthenticated={authenticated=>setTestedMemoryId(authenticated?selectedMemoryId:"")}/>
          {environment==="production"&&status&&<FiscalMemoryChangeRequestPanel taxpayerId={taxpayerId}
            memoryId={selectedMemoryId} memoryIdentifier={currentMemory.memoryIdentifier} history={summaryHistory}/>}
          {(managing||currentMemory.status===1&&status?.connectionHistory.lastAttempt?.status==="Authenticated")&&
            <FiscalMemoryAuthorityActions taxpayerId={taxpayerId} isLevel1={isLevel1} memories={chosen} selectedMemory={currentMemory}
              testedMemoryId={status?.connectionHistory.lastAttempt?.status==="Authenticated"?selectedMemoryId:testedMemoryId} onChanged={changed}/>}
        </article>}
      </>}
    </div>
  </div>;
}
