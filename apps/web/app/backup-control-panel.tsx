"use client";

import {useCallback,useEffect,useRef,useState} from "react";
import ConsoleLoader from "./components/console-loader";

type Settings={enabled:boolean;localScheduleKind:1|2;intervalHours:number;remoteEnabled:boolean;monthlyRetentionCount:number;localPath:string;remoteDirectory:string;ftpHost:string;ftpPort:number;ftpUsername:string;ftpPasswordConfigured:boolean};
type Artifact={artifactId:string;createdAtUtc:string;encryptedBytes:number;archiveValidated:boolean;checksumMatches:boolean;filePath:string;databaseName:string};
type RemoteArtifact={name:string;modifiedUtc:string;sizeBytes:number;isFile:boolean};
type Operation={status:string;message?:string|null;currentStage?:string|null;completedSteps?:number|null;totalSteps?:number|null;progressPercent?:number|null;manualStartRequired?:boolean};
const stageLabels:Record<string,string>={"validate-bundle":"بررسی ساختار ZIP و checksum","verify-upload":"بررسی کلید رمزنگاری و محتوای آرشیو","publish-import":"ثبت نسخهٔ آپلودشده","stop-services":"توقف سرویس برنامه","prepare-backup":"آماده‌سازی بکاپ","dump-encrypt":"دریافت و رمزگذاری داده‌ها","verify-archive":"اعتبارسنجی فایل بکاپ","publish-local":"ذخیره نسخه محلی","publish-remote":"ارسال نسخه به فضای ریموت","validate-local":"بررسی فایل محلی","download-remote":"دریافت فایل ریموت","validate-remote":"اعتبارسنجی نسخه ریموت","restore-isolated":"بازیابی در دیتابیس جداگانه","stage-review":"آماده‌سازی برای بررسی","pre-restore-backup":"تهیه بکاپ پیش از بازیابی","switch-database":"انتخاب دیتابیس بازیابی","activate-recovery":"فعال‌سازی حالت بررسی بازیابی"};
async function call(path:string,method="GET",body?:unknown){const res=await fetch("/api/backup-control/"+path,{method,cache:"no-store",headers:body?{"Content-Type":"application/json"}:{},body:body?JSON.stringify(body):undefined});const data=await res.json().catch(()=>({}));if(!res.ok)throw new Error(data.error||"درخواست ناموفق بود");return data;}
function OperationProgress({operation}:{operation:Operation|null}){
 const activeSeen=useRef(false);
 const [showSuccess,setShowSuccess]=useState(false);
 useEffect(()=>{
  if(operation?.status==="running"||operation?.status==="queued"){
   activeSeen.current=true;
   setShowSuccess(false);
   return;
  }
  if(operation?.status==="succeeded"&&activeSeen.current){
   activeSeen.current=false;
   setShowSuccess(true);
   const timer=window.setTimeout(()=>setShowSuccess(false),10_000);
   return()=>window.clearTimeout(timer);
  }
  if(operation?.status!=="succeeded")setShowSuccess(false);
 },[operation?.status]);
 if(!operation?.status||(operation.status==="succeeded"&&!showSuccess))return null;
 const running=operation.status==="running";
 const queued=operation.status==="queued";
 const percent=operation.status==="succeeded"?100:Math.max(0,Math.min(99,operation.progressPercent??0));
 const label=queued?"در صف اجرا":running?(stageLabels[operation.currentStage||""]||"در حال انجام عملیات بکاپ"):operation.status==="succeeded"?"عملیات با موفقیت انجام شد":operation.status==="failed"?(stageLabels[operation.currentStage||""]||"عملیات با خطا متوقف شد"):"بدون عملیات فعال";
 const hasSteps=Number.isFinite(operation.totalSteps)&&Number(operation.totalSteps)>0;
 return <div className={`backup-progress${running?" is-running":""}${operation.status==="failed"?" is-failed":""}`} role="status" aria-live="polite" aria-atomic="true" aria-busy={running}>
  <div className="backup-progress__heading"><div><span className="backup-progress__eyebrow">{queued?"منتظر شروع":running?"عملیات در حال اجرا":operation.status==="failed"?"نیازمند بررسی":operation.status==="succeeded"?"عملیات کامل شد":"وضعیت آخرین عملیات"}</span><strong>{label}</strong>{operation.status==="succeeded"&&operation.currentStage&&<small>آخرین مرحله: {stageLabels[operation.currentStage]||operation.currentStage}</small>}</div><span className="backup-progress__percent">{percent}٪</span></div>
  <div className="backup-progress__track"><progress aria-label="پیشرفت عملیات بکاپ" max={100} value={percent}/>{running&&<span className="backup-progress__spinner" aria-hidden="true"/>}</div>
  <div className="backup-progress__footer">{hasSteps&&<span>{Math.min(operation.completedSteps??0,Number(operation.totalSteps))} از {operation.totalSteps} مرحله</span>}<span>{queued?"عملیات پس از آزادشدن کنترلر آغاز می‌شود":running?"این صفحه وضعیت عملیات را خودکار به‌روزرسانی می‌کند":bundleErrors[operation.message||""]||operation.message||""}</span></div>
 </div>;
}
const bundleErrors:Record<string,string>={backup_bundle_empty:"فایل ZIP خالی است.",backup_bundle_invalid_manifest:"مشخصات بکاپ معتبر نیست.",backup_bundle_size_mismatch:"اندازهٔ آرشیو با مشخصات آن مطابقت ندارد.",backup_artifact_not_verified:"نسخهٔ انتخاب‌شده موجود نیست یا سالم نیست.",backup_operation_in_progress:"عملیات دیگری در حال انجام است؛ پس از پایان دوباره تلاش کنید.",backup_bundle_transfer_busy:"یک انتقال یا اعتبارسنجی فایل در حال انجام است؛ کمی بعد دوباره تلاش کنید.",backup_artifact_already_exists:"این نسخه قبلاً در فهرست فایل‌های محلی وجود دارد؛ از همان نسخه برای آماده‌سازی بازیابی استفاده کنید.",backup_bundle_key_unavailable:"کلید رمزنگاری این نسخه روی سرور نصب نشده است.",backup_bundle_wrong_key_or_corrupt_archive:"کلید رمزنگاری با این نسخه مطابقت ندارد یا آرشیو آسیب دیده است.",backup_bundle_checksum_mismatch:"checksum فایل با مشخصات آن مطابقت ندارد.",backup_bundle_invalid_manifest_or_schema:"مشخصات بکاپ یا نسخهٔ ساختار دیتابیس معتبر نیست.",backup_bundle_invalid_entries:"بسته باید فقط شامل آرشیو رمز‌شده و manifest متناظر باشد.",backup_bundle_requires_archive_and_manifest:"بسته باید دقیقاً دو فایل آرشیو و manifest داشته باشد.",backup_bundle_too_large:"حجم فایل بیش از حد مجاز است.",backup_bundle_invalid_archive:"محتوای ZIP یا آرشیو بکاپ معتبر نیست.",backup_bundle_insufficient_space:"فضای کافی برای دریافت و بررسی بکاپ وجود ندارد."};
export default function BackupControlPanel(){
 const [uploadFile,setUploadFile]=useState<File|null>(null);const [uploadPercent,setUploadPercent]=useState<number|null>(null);
 const [restoreCapabilities,setRestoreCapabilities]=useState({canStage:false,canActivate:false});
 const [settings,setSettings]=useState<Settings|null>(null);const [files,setFiles]=useState<Artifact[]>([]);const [remoteFiles,setRemoteFiles]=useState<RemoteArtifact[]>([]);const [operation,setOperation]=useState<Operation|null>(null);const [recovery,setRecovery]=useState<any>(null);const [restoreConfirmation,setRestoreConfirmation]=useState("");const [password,setPassword]=useState("");const [busy,setBusy]=useState(false);const [error,setError]=useState("");const [notice,setNotice]=useState("");
 const refresh=useCallback(async()=>{try{const s=await call("settings");const [a,o,r]=await Promise.all([call("artifacts/local"),call("operation"),call("recovery")]);setSettings(s);setFiles(a.files||[]);setRestoreCapabilities({canStage:a.canStageRestore===true,canActivate:a.canActivateRestore===true});setOperation(o);setRecovery(r);setError("");if(s.remoteEnabled&&s.ftpPasswordConfigured){try{const remote=await call("artifacts/remote");setRemoteFiles(remote.files||[])}catch{setRemoteFiles([])}}else setRemoteFiles([]);}catch(e){setError(e instanceof Error?e.message:"خطا در دریافت اطلاعات");}},[]);
 useEffect(()=>{void refresh()},[refresh]);
 useEffect(()=>{if(operation?.status!=="running"&&operation?.status!=="queued")return;const timer=setInterval(()=>void refresh(),3000);return()=>clearInterval(timer)},[operation?.status,refresh]);
 async function submit(path:string,method:string,body?:unknown){setBusy(true);setError("");setNotice("");try{await call(path,method,body);setNotice("تغییرات ذخیره شد.");await refresh()}catch(e){setError(e instanceof Error?e.message:"درخواست ناموفق بود");void refresh()}finally{setBusy(false)}}
 async function uploadBundle(){
  if(!uploadFile)return;
  if(!uploadFile.name.toLowerCase().endsWith(".zip")){setError("فایل ZIP انتخاب کنید.");return;}
  setBusy(true);setError("");setNotice("");setUploadPercent(0);
  try{
   await new Promise<void>((resolve,reject)=>{
    const request=new XMLHttpRequest();request.open("POST","/api/backup-control/artifacts/upload");request.timeout=30*60*1000;request.setRequestHeader("Content-Type","application/zip");
    request.upload.onprogress=e=>{if(e.lengthComputable)setUploadPercent(Math.floor(e.loaded/e.total*100));};
    request.onerror=()=>reject(new Error("ارتباط هنگام آپلود قطع شد؛ وضعیت عملیات را بررسی کنید."));request.ontimeout=()=>reject(new Error("زمان ارسال به پایان رسید؛ وضعیت عملیات را بررسی کنید."));
    request.onload=()=>{let data:any={};try{data=JSON.parse(request.responseText)}catch{};if(request.status>=200&&request.status<300)resolve();else reject(new Error(bundleErrors[data.error]||data.error||"آپلود ناموفق بود."));};request.send(uploadFile);
   });
   setNotice("ارسال کامل شد؛ اعتبارسنجی ZIP روی سرور در حال انجام است. پس از تأیید، نسخه به فهرست محلی اضافه می‌شود.");setUploadFile(null);await refresh();
  }catch(e){setError(e instanceof Error?e.message:"آپلود ناموفق بود.");}finally{setBusy(false);setUploadPercent(null);}
 }
 if(!settings)return <div className="backup-panel" dir="rtl">{error?<div role="alert" className="backup-alert"><span>{error}</span><button onClick={()=>void refresh()}>تلاش دوباره</button></div>:<div className="console-settings-card"><ConsoleLoader label="در حال دریافت تنظیمات و وضعیت بکاپ…"/></div>}</div>;
 return <div className="backup-panel" dir="rtl">
  {error&&<div role="alert" className="backup-alert"><span>{error}</span><button onClick={()=>void refresh()}>تلاش دوباره</button></div>}{notice&&<p role="status" className="backup-notice">{notice}</p>}
  <OperationProgress operation={operation}/>
  <section className="console-settings-card"><div className="console-settings-card-title"><h2>زمان‌بندی بکاپ</h2><p>ایجاد منظم نسخهٔ محلی از پایگاه داده</p></div><label className="backup-check"><input type="checkbox" checked={settings.enabled} onChange={e=>setSettings({...settings,enabled:e.target.checked})}/> بکاپ خودکار فعال باشد</label>
   <label>زمان‌بندی<select value={settings.localScheduleKind} onChange={e=>setSettings({...settings,localScheduleKind:Number(e.target.value) as Settings["localScheduleKind"]})}><option value={1}>روزانه</option><option value={2}>هر چند ساعت</option></select></label>
   {settings.localScheduleKind===2&&<label>فاصلهٔ ساعت<input type="number" min="1" max="24" value={settings.intervalHours} onChange={e=>setSettings({...settings,intervalHours:Number(e.target.value)})}/></label>}
   <p className="backup-help">مسیر بکاپ داخل سرور: <code dir="ltr">{settings.localPath}</code> <span>نگهداری محلی: ۷ روز</span></p>
  </section>
  <section className="console-settings-card"><div className="console-settings-card-title"><h2>بکاپ خارج از سرور</h2><p>ذخیرهٔ رمزگذاری‌شده در مقصد FTPS</p></div><label className="backup-check"><input type="checkbox" checked={settings.remoteEnabled} onChange={e=>setSettings({...settings,remoteEnabled:e.target.checked})}/> ارسال به FTPS فعال باشد</label>
   <p className="backup-help">اتصال Explicit FTPS روی پورت {settings.ftpPort}</p><label>سرور<input dir="ltr" value={settings.ftpHost} onChange={e=>setSettings({...settings,ftpHost:e.target.value})}/></label><label>کاربر<input dir="ltr" value={settings.ftpUsername} onChange={e=>setSettings({...settings,ftpUsername:e.target.value})}/></label>
   <label>مسیر ریموت<input dir="ltr" value={settings.remoteDirectory} onChange={e=>setSettings({...settings,remoteDirectory:e.target.value})}/></label>
   <label>رمز FTP {settings.ftpPasswordConfigured&&<small>(تنظیم شده)</small>}<input type="password" autoComplete="new-password" value={password} onChange={e=>setPassword(e.target.value)}/></label>
   <label>تعداد نقطه‌های ماهانه<input type="number" min="1" max="120" value={settings.monthlyRetentionCount} onChange={e=>setSettings({...settings,monthlyRetentionCount:Number(e.target.value)})}/></label>
   <div className="backup-actions"><button disabled={busy} onClick={()=>void submit("settings","PUT",settings)}>ذخیره تنظیمات</button><button disabled={busy||!password} onClick={async()=>{await submit("ftps-password","PUT",{password});setPassword("")}}>ذخیره رمز FTP</button></div>
  </section>
  <section className="console-settings-card"><div className="console-settings-card-title"><h2>عملیات و بازیابی</h2><p>مدیریت نسخه‌ها و بازگردانی کنترل‌شدهٔ دیتابیس</p></div>
   {recovery?.recoveryMode?<><p role="alert" className="backup-recovery-alert">برنامه در حالت بررسی بازیابی است. پس از بررسی دیتابیس، برای فعال‌سازی دوباره عبارت زیر را وارد کنید: <code dir="ltr">ENABLE_BUSINESS_OPERATIONS</code></p><label>عبارت تأیید<input dir="ltr" value={restoreConfirmation} onChange={e=>setRestoreConfirmation(e.target.value)}/></label><button disabled={busy||operation?.status==="running"||operation?.status==="queued"||restoreConfirmation!=="ENABLE_BUSINESS_OPERATIONS"} onClick={()=>void submit("recovery/enable-business","POST",{confirmation:restoreConfirmation})}>تأیید بررسی و فعال‌سازی برنامه</button></>:<>
   <div className="backup-actions"><button disabled={busy||operation?.status==="running"||operation?.status==="queued"} onClick={()=>{if(confirm("بکاپ دستی شروع شود؟ دسترسی کاربران عادی و ادمین سطح ۲ تا فعال‌سازی دستی ادمین سطح ۱ محدود می‌شود."))void submit("operations/manual","POST",{})}}>گرفتن بکاپ دستی و ورود به حالت نگهداری</button>
   {recovery?.stageId&&<button disabled={!restoreCapabilities.canActivate||busy||operation?.status==="running"||operation?.status==="queued"} onClick={()=>{if(confirm("دیتابیس پشتیبان جایگزین دیتابیس فعلی شود؟ API متوقف می‌شود و برنامه تا بررسی مدیر غیرفعال می‌ماند."))void submit("restore/activate","POST",{stageId:recovery.stageId,confirmation:"ACTIVATE_RESTORE"})}}>فعال‌کردن بازیابی انتخاب‌شده</button>}
   {operation?.manualStartRequired&&operation?.status!=="running"&&operation?.status!=="queued"&&<button disabled={busy} onClick={()=>{if(confirm("حالت نگهداری پایان یابد و دسترسی کاربران دوباره فعال شود؟"))void submit("services/start","POST",{})}}>پایان نگهداری و فعال‌سازی برنامه</button>}</div>
   </>}
   <h3>آپلود بستهٔ بکاپ</h3>
   <p className="backup-help">بستهٔ ZIP شامل فایل رمز‌شده و manifest را انتخاب کنید. کلید متناظر باید روی سرور نصب باشد. آپلود، دیتابیس فعلی را تغییر نمی‌دهد.</p>
   <label>فایل ZIP<input type="file" accept=".zip,application/zip" disabled={busy} onChange={e=>setUploadFile(e.target.files?.[0]||null)}/></label>
   <button disabled={busy||!uploadFile||operation?.status==="running"||operation?.status==="queued"||recovery?.recoveryMode} onClick={()=>void uploadBundle()}>آپلود و اعتبارسنجی ZIP</button>
   {uploadPercent!==null&&<div role="status" aria-live="polite"><p>{uploadPercent<100?"ارسال فایل":"ارسال کامل شد؛ منتظر پاسخ سرور"} — {uploadPercent}٪</p><progress aria-label="درصد ارسال فایل ZIP" max={100} value={uploadPercent}/></div>}
   {!restoreCapabilities.canStage&&<p className="backup-help">برای آماده‌سازی بازیابی، اتصال مجزای بازیابی دیتابیس باید روی سرور تنظیم شود.</p>}
   {!restoreCapabilities.canActivate&&<p className="backup-help">فعال‌سازی دیتابیس بازیابی در محیط محلی مک پشتیبانی نمی‌شود؛ فعال‌سازی را پس از تنظیم سرویس‌های Ubuntu انجام دهید.</p>}
   <h3>فایل‌های محلی</h3><ul className="backup-artifact-list">{files.map(f=><li key={f.artifactId}><div><strong>دیتابیس: <bdi>{f.databaseName}</bdi></strong><code dir="ltr">{f.filePath}</code><small>{new Date(f.createdAtUtc).toLocaleString("fa-IR")} — {Math.ceil(f.encryptedBytes/1024/1024)} MB — {f.archiveValidated&&f.checksumMatches?"سالم":"نیازمند بررسی"}</small></div><a href={"/api/backup-control/artifacts/download/"+f.artifactId} download>دانلود ZIP</a><button disabled={!restoreCapabilities.canStage||busy||operation?.status==="running"||operation?.status==="queued"||!f.archiveValidated||!f.checksumMatches||recovery?.recoveryMode} onClick={()=>{if(confirm("بکاپ در دیتابیس جداگانه بازیابی و برای بررسی آماده شود؟"))void submit("restore/stage-local","POST",{artifactId:f.artifactId})}}>آماده‌سازی بازیابی</button></li>)}</ul>
   <h3>فایل‌های ریموت</h3><ul className="backup-artifact-list">{remoteFiles.map(f=><li key={f.name}><div><code dir="ltr">{f.name}</code><small>{new Date(f.modifiedUtc).toLocaleString("fa-IR")} — {Math.ceil(f.sizeBytes/1024/1024)} MB</small></div><button disabled={!restoreCapabilities.canStage||busy||operation?.status==="running"||operation?.status==="queued"||recovery?.recoveryMode} onClick={()=>{if(confirm("نسخه ریموت دانلود و در دیتابیس جداگانه برای بررسی آماده شود؟"))void submit("restore/stage-remote","POST",{name:f.name})}}>آماده‌سازی بازیابی</button></li>)}</ul>
  </section>
 </div>;
}
