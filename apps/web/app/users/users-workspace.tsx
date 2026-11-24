"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import ConsoleLoader from "../components/console-loader";
import SearchableSelect from "../components/searchable-select";


type Account = {
  customerUserId: string; rootLevel1UserId: string | null; partnerLevel2UserId: string | null;
  acquisitionChannel: number; revision: number; mobile: string | null; email: string | null;
  createdAtUtc: string; isActive: boolean; taxpayerCount: number;
  roles?: string[]; mfaEnabled?: boolean; hasMfaState?: boolean;
  personName?: string | null; companyName?: string | null; profileComplete?: boolean;
};
type Partner = { userId: string; mobile: string | null; email: string | null };
type SubscriptionStatus = { subscriptionId:string; kind:number; validityDays:number; invoiceUnitLimit:number|null;
  availableUnits:number|null; consumedUnits:number|null; expiresAtUtc:string|null; state:number; revision:number; priceIrr:number; partnerUserId:string|null } | null;
type SubscriptionHistory = { id:string; kind:number; previousValidityDays:number|null; newValidityDays:number|null;
  previousInvoiceUnitLimit:number|null; newInvoiceUnitLimit:number|null; negativeGrossIrr:number; reason:string; actorUserId:string; createdAtUtc:string };
type CancellationPreview = { originalPriceIrr:number; deductedIrr:number; refundedIrr:number; remainingDays:number|null; remainingUnits:number|null; fullRefund:boolean };
type AccountPage = { items: Account[]; page: number; pageSize: number; total: number };
const acquisition: Record<number,string> = {
  0: "نیازمند تعیین مالکیت", 1: "ثبت‌نام مستقیم", 2: "ثبت توسط سطح ۱",
  3: "ثبت توسط سطح ۲", 4: "معرفی تأییدشده", 5: "بازیابی مالکیت کاربر قدیمی"
};
const PAGE_SIZES = [10, 20, 50];
const errorLabel = (status: number) => status === 401 ? "نشست ورود معتبر نیست." :
  status === 403 ? "دسترسی به این اطلاعات مجاز نیست." :
  status === 404 ? "کاربر یا دسترسی به او یافت نشد." : "دریافت اطلاعات ناموفق بود.";
async function load<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: "no-store", credentials: "same-origin" });
  if (!res.ok) throw new Error(errorLabel(res.status));
  // A nullable endpoint (for example, a customer without an active plan) may
  // legitimately return an empty body. Do not turn that state into a JSON
  // parsing error in the users workspace.
  const body = await res.text();
  return (body.trim() ? JSON.parse(body) : null) as T;
}

function UsersActionIcon({ kind }: { kind: "add" | "refresh" }) {
  return kind === "add" ? <svg className="users-action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path d="M12 5v14M5 12h14" />
  </svg> : <svg className="users-action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path d="M20 11a8 8 0 0 0-14.8-4L3 10M3 5v5h5M4 13a8 8 0 0 0 14.8 4L21 14m0 5v-5h-5" />
  </svg>;
}

function ActionIcon({ kind }: { kind: "add" | "refresh" }) {
  return kind === "add" ? <svg className="users-action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path d="M12 5v14M5 12h14" />
  </svg> : <svg className="users-action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path d="M20 11a8 8 0 0 0-14.8-4L3 10M3 5v5h5M4 13a8 8 0 0 0 14.8 4L21 14m0 5v-5h-5" />
  </svg>;
}

export default function UsersWorkspace({ level1 }: { level1: boolean }) {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [total, setTotal] = useState(0);
  const [selected, setSelected] = useState<Account | null>(null);
  const [search, setSearch] = useState("");
  const [ownershipFilter, setOwnershipFilter] = useState("all");
  const [roleFilter, setRoleFilter] = useState("all");
  const [resetTarget, setResetTarget] = useState<Account | null>(null);
  const [resetting, setResetting] = useState(false);
  const [resetError, setResetError] = useState("");
  const [resetNotice, setResetNotice] = useState("");
  const resetDialogRef = useRef<HTMLDialogElement>(null);
  const createDialogRef = useRef<HTMLDialogElement>(null);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState("");
  const [createNotice, setCreateNotice] = useState("");
  const [createForm, setCreateForm] = useState({ mobile: "", firstName: "", lastName: "", companyName: "", address: "", password: "", passwordConfirm: "" });
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [modalError, setModalError] = useState("");
  const [partnerId, setPartnerId] = useState("");
  const [partners, setPartners] = useState<Partner[]>([]);
  const [partnerLoadError, setPartnerLoadError] = useState("");
  const [transferReason, setTransferReason] = useState("");
  const [transferKind, setTransferKind] = useState(1);
  const [savingOwner, setSavingOwner] = useState(false);
  const [transferNotice, setTransferNotice] = useState("");
  const [transferHistory, setTransferHistory] = useState<{ id: string; previousPartnerLevel2UserId: string | null; newPartnerLevel2UserId: string | null; explanation: string; changedAtUtc: string }[] | null>(null);
  const [subscription,setSubscription]=useState<SubscriptionStatus>(null);
  const [subscriptionLoading,setSubscriptionLoading]=useState(false);
  const [subscriptionError,setSubscriptionError]=useState("");
  const [subscriptionReason,setSubscriptionReason]=useState("");
  const [subscriptionValue,setSubscriptionValue]=useState("");
  const [subscriptionDaysValue,setSubscriptionDaysValue]=useState("");
  const [subscriptionBusy,setSubscriptionBusy]=useState(false);
  const [subscriptionHistory,setSubscriptionHistory]=useState<SubscriptionHistory[]>([]);
  const [cancellationPreview,setCancellationPreview]=useState<CancellationPreview|null>(null);
  const [cancelReason,setCancelReason]=useState("");
  const cancelDialogRef = useRef<HTMLDialogElement>(null);
  const dialogRef = useRef<HTMLDialogElement>(null);
  const requestRef = useRef(0);

  const refresh = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (search.trim()) params.set("query", search.trim());
      if (ownershipFilter !== "all") params.set("ownership", ownershipFilter);
      if (roleFilter !== "all") params.set("role", roleFilter);
      const result = await load<AccountPage>("/api/customer-owners/accounts/page?" + params);
      setAccounts(result.items); setTotal(result.total);
      // A revoked partner must not keep a customer's information in a previously opened dialog.
      setSelected(previous => {
        if (previous && !result.items.some(x => x.customerUserId === previous.customerUserId)) {
          dialogRef.current?.close();
          return null;
        }
        return previous ? result.items.find(x => x.customerUserId === previous.customerUserId) || null : null;
      });
    } catch (e) {
      setAccounts([]); setTotal(0); setSelected(null); dialogRef.current?.close();
      setError((e as Error).message);
    } finally { setLoading(false); }
  }, [page, pageSize, search, ownershipFilter, roleFilter]);

  useEffect(() => {
    if (level1) void load<Partner[]>("/api/customer-owners/partners")
      .then(setPartners).catch(() => setPartnerLoadError("فهرست شرکای مجاز دریافت نشد."));
    void refresh();
    const focus = () => void refresh();
    const visibility = () => { if (document.visibilityState === "visible") void refresh(); };
    window.addEventListener("focus", focus);
    document.addEventListener("visibilitychange", visibility);
    return () => {
      window.removeEventListener("focus", focus);
      document.removeEventListener("visibilitychange", visibility);
    };
  }, [refresh]);

  function requestMfaReset(account: Account) {
    setResetTarget(account); setResetError(""); setResetNotice("");
    resetDialogRef.current?.showModal();
  }

  async function resetMfa(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!resetTarget || resetting) return;
    setResetting(true); setResetError("");
    try {
      const response = await fetch("/api/identity/mfa/level2/reset", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ targetUserId: resetTarget.customerUserId }),
      });
      if (!response.ok) {
        setResetError(response.status === 409 ? "وضعیت حساب تغییر کرده یا MFA قبلاً ریست شده است. فهرست را بازخوانی کنید."
          : response.status === 403 ? "ریست فقط توسط مدیر سطح ۱ و برای حساب ادمین سطح ۲ مجاز است."
          : response.status === 404 ? "حساب ادمین مقصد یافت نشد." : "ریست MFA انجام نشد؛ وضعیت ورود و ارتباط را بررسی کنید.");
        return;
      }
      resetDialogRef.current?.close(); setResetTarget(null);
      setResetNotice("MFA ریست شد و نشست‌های قبلی باطل شدند. کاربر باید با رمز عبور وارد شود و Authenticator را دوباره تنظیم کند تا دسترسی سطح ۲ فعال شود.");
      await refresh();
    } catch { setResetError("ارتباط با سرور برقرار نشد. پیش از تلاش دوباره فهرست را بازخوانی کنید."); }
    finally { setResetting(false); }
  }

  function openCreate() {
    setCreateForm({ mobile: "", firstName: "", lastName: "", companyName: "", address: "", password: "", passwordConfirm: "" });
    setCreateError("");
    createDialogRef.current?.showModal();
  }

  async function createCustomer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (creating) return;
    if (createForm.password !== createForm.passwordConfirm) {
      setCreateError("کلمه عبور و تکرار آن یکسان نیستند.");
      return;
    }
    if (createForm.password.length < 8) {
      setCreateError("کلمه عبور باید حداقل ۸ نویسه باشد.");
      return;
    }
    setCreating(true); setCreateError("");
    try {
      const { passwordConfirm: _passwordConfirm, ...payload } = createForm;
      const response = await fetch("/api/identity/admin/customers", {
        method: "POST", credentials: "same-origin", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!response.ok) {
        setCreateError(response.status === 409 ? "این شماره همراه قبلاً ثبت شده است." :
          response.status === 403 ? "برای ثبت کاربر دسترسی مدیر سطح ۱ یا ۲ لازم است." :
          response.status === 401 ? "نشست مدیر معتبر نیست؛ دوباره وارد شوید." :
          response.status === 400 && (await response.clone().json().catch(() => ({}))).error === "invalid_customer_password"
            ? "کلمه عبور باید حداقل ۸ نویسه باشد." :
          response.status === 400 && (await response.clone().json().catch(() => ({}))).error === "invalid_customer_profile"
            ? "نام، نام خانوادگی و نشانی را بررسی کنید." :
          response.status >= 500 && (await response.clone().json().catch(() => ({}))).error === "customer_provisioning_migration_required"
            ? "ساختار پایگاه‌داده به‌روز نیست؛ migrationهای پایگاه‌داده را اجرا و API را restart کنید." :
          response.status >= 500 ? "سرویس ثبت کاربر در دسترس نیست؛ لاگ API را بررسی کنید." :
          "اطلاعات کاربر را بررسی کنید.");
        return;
      }
      createDialogRef.current?.close();
      setCreateNotice("کاربر با موفقیت ثبت شد و می‌تواند با کد پیامکی وارد شود.");
      await refresh();
    } catch { setCreateError("ارتباط با سرور برقرار نشد."); }
    finally { setCreating(false); }
  }

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const activePage = Math.min(page, totalPages);
  const pageRows = accounts;
  const firstIndex = total ? (activePage - 1) * pageSize + 1 : 0;
  const lastIndex = Math.min(activePage * pageSize, total);

  function close() {
    requestRef.current++;
    if (dialogRef.current?.open) dialogRef.current.close();
    setSelected(null); setModalError("");
  }

  function open(account: Account) {
    setSelected(account); setModalError("");
    setPartnerId(account.partnerLevel2UserId || "");
    setTransferReason(""); setTransferNotice(""); setTransferHistory(null);
    setSubscription(null);setSubscriptionHistory([]);setCancellationPreview(null);setCancelReason("");setSubscriptionError("");setSubscriptionReason("");setSubscriptionValue("");setSubscriptionDaysValue("");setSubscriptionLoading(true);
    dialogRef.current?.showModal();
    requestRef.current++;
    void load<SubscriptionStatus>("/api/customer-owners/"+account.customerUserId+"/subscription-adjustment")
      .then(setSubscription).catch(e=>setSubscriptionError((e as Error).message)).finally(()=>setSubscriptionLoading(false));
    void load<SubscriptionHistory[]>("/api/customer-owners/"+account.customerUserId+"/subscription-adjustment/history")
      .then(setSubscriptionHistory).catch(() => setSubscriptionHistory([]));
  }

  async function adjustSubscription(event:FormEvent<HTMLFormElement>) {
    event.preventDefault(); if(!selected||!subscription||subscriptionBusy||!subscriptionReason.trim())return;
    const quantityPlan=subscription.kind===2;
    const unitValue=subscriptionValue.trim()?Number(subscriptionValue):null;
    const daysValue=quantityPlan?(subscriptionDaysValue.trim()?Number(subscriptionDaysValue):null):Number(subscriptionValue);
    if((unitValue!==null&&(!Number.isSafeInteger(unitValue)||unitValue<=0))||
      (daysValue!==null&&(!Number.isSafeInteger(daysValue)||daysValue<=0||daysValue>366))||
      (quantityPlan&&unitValue===null&&daysValue===null)){
      setSubscriptionError(quantityPlan?"سهمیه جدید یا روزهای باقی‌مانده را وارد کنید.":"روز اعتبار جدید معتبر نیست.");return;
    }
    setSubscriptionBusy(true);setSubscriptionError("");
    try {
      const payload=quantityPlan?{invoiceUnitLimit:unitValue,validityDays:daysValue}:{validityDays:daysValue};
      const response=await fetch("/api/customer-owners/"+selected.customerUserId+"/subscription-adjustment",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json"},body:JSON.stringify({...payload,subscriptionId:subscription.subscriptionId,expectedRevision:subscription.revision,operationKey:crypto.randomUUID(),reason:subscriptionReason.trim()})});
      if(!response.ok)throw new Error(response.status===409?"وضعیت اشتراک تغییر کرده است؛ پرونده را بازخوانی کنید.":"اصلاح پلن انجام نشد.");
      setSubscription(await response.json());setSubscriptionReason("");setSubscriptionValue("");setSubscriptionDaysValue("");
    } catch(e){setSubscriptionError(e instanceof Error?e.message:"ارتباط با سرور برقرار نشد.");} finally{setSubscriptionBusy(false);}
  }
  function openCancellation(){
    if(!subscription||subscriptionBusy)return;
    setCancellationPreview(null);setCancelReason("");setSubscriptionError("");cancelDialogRef.current?.showModal();
  }
  async function previewCancellation(fullRefund:boolean){
    if(!selected||!subscription||subscriptionBusy)return;
    setSubscriptionBusy(true);setSubscriptionError("");
    try {const response=await fetch("/api/customer-owners/"+selected.customerUserId+"/subscription-cancellation/preview",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json"},body:JSON.stringify({subscriptionId:subscription.subscriptionId,expectedRevision:subscription.revision,fullRefund})});if(!response.ok)throw new Error(response.status===409?"وضعیت اشتراک تغییر کرده است؛ پرونده را بازخوانی کنید.":"محاسبه برگشت انجام نشد.");setCancellationPreview(await response.json() as CancellationPreview);}catch(e){setSubscriptionError(e instanceof Error?e.message:"ارتباط با سرور برقرار نشد.");}finally{setSubscriptionBusy(false);}
  }
  async function cancelSubscription(){
    if(!selected||!subscription||!cancellationPreview||subscriptionBusy||!cancelReason.trim()||!window.confirm("مبلغ برگشتی نمایش‌داده‌شده تأیید و اشتراک لغو شود؟"))return;
    setSubscriptionBusy(true);setSubscriptionError("");
    try {const response=await fetch("/api/customer-owners/"+selected.customerUserId+"/subscription-cancellation",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json"},body:JSON.stringify({subscriptionId:subscription.subscriptionId,expectedRevision:subscription.revision,operationKey:crypto.randomUUID(),reason:cancelReason.trim(),fullRefund:cancellationPreview.fullRefund})});if(!response.ok)throw new Error(response.status===409?"وضعیت اشتراک تغییر کرده است؛ پرونده را بازخوانی کنید.":"لغو پلن انجام نشد.");const result=await response.json().catch(()=>null) as {remainingDays?:number;remainingUnits?:number}|null;const history=await load<SubscriptionHistory[]>("/api/customer-owners/"+selected.customerUserId+"/subscription-adjustment/history");setSubscriptionHistory(history);setSubscription(null);setCancellationPreview(null);setCancelReason("");cancelDialogRef.current?.close();setSubscriptionError("");setTransferNotice(result?.remainingDays!==undefined?`پلن لغو شد؛ ${result.remainingDays} روز باقی‌مانده در سابقه مالی ثبت شد.`:result?.remainingUnits!==undefined?`پلن لغو شد؛ ${result.remainingUnits} سهمیه باقی‌مانده در سابقه مالی ثبت شد.`:"پلن لغو شد و اثر مالی آن ثبت گردید.");}catch(e){setSubscriptionError(e instanceof Error?e.message:"ارتباط با سرور برقرار نشد.");}finally{setSubscriptionBusy(false);}
  }

  async function assignOwner(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!level1 || !selected || savingOwner || !transferReason.trim()) return;
    const target = partnerId.trim();
    if (target && !/^[0-9a-f]{8}-[0-9a-f-]{27,}$/i.test(target)) {
      setModalError("شناسه ادمین سطح ۲ معتبر نیست."); return;
    }
    if (!window.confirm("انتساب تجاری این کاربر تغییر کند؟ این عملیات سوابق فروش قبلی را تغییر نمی‌دهد.")) return;
    setSavingOwner(true); setModalError(""); setTransferNotice("");
    try {
      const response = await fetch("/api/customer-owners/" + selected.customerUserId + "/transfer", {
        method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          partnerLevel2UserId: target || null,
          expectedRevision: selected.revision,
          reason: transferKind,
          explanation: transferReason.trim()
        })
      });
      if (!response.ok) throw new Error(errorLabel(response.status));
      setTransferNotice("مالکیت تجاری با تأیید سطح ۱ ثبت شد؛ تغییر در تاریخچه محفوظ است.");
      setTransferReason("");
      await refresh();
    } catch (e) { setModalError((e as Error).message); }
    finally { setSavingOwner(false); }
  }

  async function readHistory() {
    if (!selected || !level1) return;
    setModalError("");
    try {
      setTransferHistory(await load("/api/customer-owners/" + selected.customerUserId + "/history"));
    } catch (e) { setModalError((e as Error).message); }
  }

  const ownerLabel = (a: Account) => !a.rootLevel1UserId ? "نیازمند بررسی" :
    a.partnerLevel2UserId ? "شریک سطح ۲" : "مستقیم سطح ۱";

  return <section className="users-panel" aria-label="فهرست کاربران نرم‌افزار">
    <div className="users-toolbar">
      <div><h2>{level1 ? "کاربران ثبت‌نام‌شده" : "مشتریان من"}</h2>
        <p>هر ردیف صرفاً یک حساب ورود نرم‌افزار است؛ خریداران و مؤدیان در فهرست مالیاتی مستقل مدیریت می‌شوند.</p></div>
      <div className="users-toolbar-actions">
        <button type="button" className="users-button users-button--icon" disabled={loading} onClick={() => void refresh()}>
          <ActionIcon kind="refresh" />بازخوانی کاربران
        </button>
        <button type="button" className="identity-primary users-button--icon" onClick={openCreate}>
          <ActionIcon kind="add" />ثبت کاربر جدید
        </button>
      </div>
    </div>

    <div className="users-filters">
      <label>جست‌وجو
        <input type="search" placeholder="نام کاربری، موبایل، ایمیل یا شناسه"
          value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/>
      </label>
      {level1 && <label>نقش کاربر
        <select value={roleFilter} onChange={e => { setRoleFilter(e.target.value); setPage(1); }}>
          <option value="all">همه نقش‌ها</option>
          <option value="customer">کاربران عادی</option>
          <option value="level2">ادمین‌های سطح ۲</option>
        </select>
      </label>}
      {level1 && <label>مالکیت تجاری
        <select value={ownershipFilter} onChange={e => { setOwnershipFilter(e.target.value); setPage(1); }}>
          <option value="all">همه کاربران</option>
          <option value="root">مستقیم سطح ۱</option>
          <option value="partner">متعلق به شرکا</option>
          <option value="unresolved">نیازمند تعیین مالکیت</option>
        </select>
      </label>}
      <label>تعداد در صفحه
        <select value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}>
          {PAGE_SIZES.map(n => <option key={n} value={n}>{n}</option>)}
        </select>
      </label>
    </div>

    {error && <p role="alert" className="form-error">{error}</p>}
    {resetNotice && <p role="status">{resetNotice}</p>}
    {createNotice && <p role="status">{createNotice}</p>}
    <div className="users-table-scroll">
      <table className="users-table">
        <thead><tr>
          <th scope="col">نام شخص / شرکت</th><th scope="col">ایمیل / موبایل</th><th scope="col">وضعیت</th>
          <th scope="col">مالکیت تجاری</th>
          <th scope="col">ثبت‌نام</th><th scope="col">عملیات</th>
          {level1 && <th scope="col">ریست MFA</th>}
        </tr></thead>
        <tbody>
          {loading ? <tr><td colSpan={level1 ? 7 : 6}><ConsoleLoader label="در حال دریافت کاربران…" /></td></tr> :
            pageRows.length === 0 ? <tr><td colSpan={level1 ? 7 : 6}>کاربری با این معیارها پیدا نشد.</td></tr> :
            pageRows.map((a, i) => <tr key={a.customerUserId}>
              <td><strong>{a.companyName || a.personName || "پروفایل تکمیل‌نشده"}</strong>
                {!a.profileComplete && <small>هنوز وارد نشده / تکمیل‌نشده</small>}</td>
              <td><span className="users-contact-truncated" title={[a.mobile,a.email].filter(Boolean).join(" / ")} dir="ltr">{a.mobile || a.email || "فاقد اطلاعات تماس"}</span></td>
              <td><span className={a.isActive ? "users-state" : "users-state users-state-muted"}>
                {a.isActive ? "فعال" : "غیرفعال"}</span>
                {a.roles?.includes("PendingLevel2") ? <small>ادمین سطح ۲ — منتظر تنظیم MFA</small>
                  : a.roles?.includes("Level2") ? <small>ادمین سطح ۲</small> : null}</td>
              <td><span className={!a.rootLevel1UserId ? "users-state users-state-warning" : ""}>
                {ownerLabel(a)}</span></td>
              <td>{new Date(a.createdAtUtc).toLocaleDateString("fa-IR")}</td>
              <td><button className="users-action" type="button" onClick={() => void open(a)}>
                مشاهده پرونده
              </button></td>
              {level1 && <td>{a.roles?.some(role => ["Level2", "PendingLevel2"].includes(role))
                && !a.roles.includes("Level1") ? <button className="users-action" type="button"
                  disabled={!a.isActive || resetting || (!a.hasMfaState && a.roles.includes("PendingLevel2"))}
                  onClick={() => requestMfaReset(a)}>ریست MFA</button> : "—"}</td>}
            </tr>)}
        </tbody>
      </table>
    </div>

    <div className="users-pagination" aria-label="صفحه‌بندی کاربران">
      <span>نمایش {firstIndex.toLocaleString("fa-IR")} تا {lastIndex.toLocaleString("fa-IR")} از {total.toLocaleString("fa-IR")} کاربر</span>
      <div>
        <button type="button" disabled={activePage <= 1 || loading} onClick={() => setPage(p => p - 1)}>قبلی</button>
        <span>صفحه {activePage.toLocaleString("fa-IR")} از {totalPages.toLocaleString("fa-IR")}</span>
        <button type="button" disabled={activePage >= totalPages || loading} onClick={() => setPage(p => p + 1)}>بعدی</button>
      </div>
    </div>

    <dialog className="users-dialog users-dialog--compact" ref={createDialogRef} dir="rtl"
      onCancel={event => { if (creating) event.preventDefault(); }} aria-labelledby="create-customer-heading">
      <form className="identity-form users-dialog-content" onSubmit={createCustomer}>
        <div className="users-dialog-head"><div><p>مدیریت کاربران</p><h2 id="create-customer-heading">ثبت کاربر جدید</h2></div>
          <button type="button" className="users-dialog-close" disabled={creating} onClick={() => createDialogRef.current?.close()} aria-label="بستن">×</button></div>
        <label>شماره همراه<input required dir="ltr" inputMode="tel" value={createForm.mobile} onChange={e => setCreateForm(f => ({ ...f, mobile: e.target.value }))} /></label>
        <label>نام<input required value={createForm.firstName} onChange={e => setCreateForm(f => ({ ...f, firstName: e.target.value }))} /></label>
        <label>نام خانوادگی<input required value={createForm.lastName} onChange={e => setCreateForm(f => ({ ...f, lastName: e.target.value }))} /></label>
        <label>نام شرکت / مؤسسه<input value={createForm.companyName} onChange={e => setCreateForm(f => ({ ...f, companyName: e.target.value }))} /></label>
        <label>کلمه عبور<input required type="password" minLength={8} value={createForm.password} onChange={e => setCreateForm(f => ({ ...f, password: e.target.value }))} /></label>
        <label>تکرار کلمه عبور<input required type="password" minLength={8} value={createForm.passwordConfirm} onChange={e => setCreateForm(f => ({ ...f, passwordConfirm: e.target.value }))} /></label>
        <label>نشانی<textarea className="users-dialog-textarea" required rows={2} value={createForm.address} onChange={e => setCreateForm(f => ({ ...f, address: e.target.value }))} /></label>
        {createError && <p role="alert" className="form-error">{createError}</p>}
        <button className="identity-primary" disabled={creating}>{creating ? "در حال ثبت…" : "ثبت کاربر"}</button>
        <button type="button" className="users-button" disabled={creating} onClick={() => createDialogRef.current?.close()}>انصراف</button>
      </form>
    </dialog>

    {level1 && <dialog className="users-dialog" ref={resetDialogRef} dir="rtl"
      aria-labelledby="mfa-reset-heading" onCancel={event => { if (resetting) event.preventDefault(); }}
      onClose={() => { setResetTarget(null); setResetError(""); }}>
      <form className="identity-form users-dialog-content" onSubmit={resetMfa}>
        <h2 id="mfa-reset-heading">تأیید ریست MFA ادمین سطح ۲</h2>
        <p>حساب مقصد: <bdi dir="ltr">{resetTarget?.mobile || resetTarget?.email || resetTarget?.customerUserId}</bdi></p>
        <p>Authenticator و کدهای بازیابی قبلی و تمام نشست‌های ورود این حساب باطل می‌شوند.
          کاربر باید با رمز عبور وارد شود و MFA را دوباره تنظیم کند؛ تا آن زمان دسترسی مدیریتی غیرفعال است.</p>
        {resetError && <p role="alert" className="form-error">{resetError}</p>}
        <button className="identity-primary" disabled={resetting}>{resetting ? "در حال ریست…" : "تأیید و ریست MFA"}</button>
        <button type="button" className="users-button" disabled={resetting} onClick={() => resetDialogRef.current?.close()}>انصراف</button>
      </form>
    </dialog>}

    <dialog className="users-dialog" ref={dialogRef} onClose={close}
      onClick={event => {
        if (event.target !== event.currentTarget) return;
        const r = event.currentTarget.getBoundingClientRect();
        if (event.clientX < r.left || event.clientX > r.right ||
            event.clientY < r.top || event.clientY > r.bottom) close();
      }}
      aria-labelledby="user-detail-heading" dir="rtl">
      <div className="users-dialog-head">
        <div><p>پرونده حساب کاربری</p><h2 id="user-detail-heading">{selected?.mobile || selected?.email || "مشخصات کاربر"}</h2></div>
        <button type="button" className="users-dialog-close" onClick={close} aria-label="بستن پرونده">×</button>
      </div>
      {selected && <div className="users-dialog-content">
        <dl className="users-details">
          <div><dt>شناسه حساب</dt><dd dir="ltr">{selected.customerUserId}</dd></div>
          <div><dt>تلفن همراه</dt><dd dir="ltr">{selected.mobile || "—"}</dd></div>
          <div><dt>ایمیل</dt><dd dir="ltr">{selected.email || "—"}</dd></div>
          <div><dt>وضعیت</dt><dd>{selected.isActive ? "فعال" : "غیرفعال"}</dd></div>
          <div><dt>مالکیت</dt><dd>{ownerLabel(selected)}</dd></div>
          <div><dt>شیوه ثبت‌نام</dt><dd>{acquisition[selected.acquisitionChannel] || "نامشخص"}</dd></div>
          <div><dt>تاریخ ثبت‌نام</dt><dd>{new Date(selected.createdAtUtc).toLocaleString("fa-IR")}</dd></div>
        </dl>
        {level1 && <section className="users-owner-edit">
          <h3>انتساب تجاری این کاربر</h3>
          <p>ثبت‌نام عمومی زیرمجموعه سطح ۱ است. واگذاری به شریک سطح ۲ فقط با تأیید شما انجام می‌شود.</p>
          <form onSubmit={assignOwner}>
            <label htmlFor="owner-partner-id">مالک تجاری مشتری
              <SearchableSelect id="owner-partner-id" value={partnerId}
                options={[...(partnerId&&!partners.some(p=>p.userId===partnerId)?[{value:partnerId,label:`شریک فعلی — ${partnerId.slice(0,8)}…`,searchText:partnerId}]:[]),
                  ...partners.map(p=>({value:p.userId,label:`${p.mobile||p.email||p.userId} — ادمین سطح ۲`,searchText:p.userId}))]}
                selectedLabel={partnerId?undefined:"ادمین سطح ۱ — مشتری مستقیم"} placeholder="جست‌وجوی همکار…"
                onChange={setPartnerId} />
            </label>
            {partnerLoadError && <p role="alert" className="form-error">{partnerLoadError}</p>}
            <label htmlFor="owner-transfer-kind">علت انتساب
              <select id="owner-transfer-kind" value={transferKind} onChange={e => setTransferKind(Number(e.target.value))}>
                <option value={1}>معرفی مشتری توسط شریک و تأیید سطح ۱</option>
                <option value={2}>اصلاح مالکیت اشتباه</option>
                <option value={3}>غیرفعال شدن شریک قبلی</option>
                <option value={4}>سایر دلایل</option>
              </select>
            </label>
            <label htmlFor="owner-transfer-reason">شرح و مستندات
              <textarea id="owner-transfer-reason" rows={2} maxLength={1000} required
                value={transferReason} onChange={e => setTransferReason(e.target.value)}/>
            </label>
            <button className="users-button" type="submit"
              disabled={savingOwner || !!partnerLoadError || !transferReason.trim() ||
                (!!selected.rootLevel1UserId && partnerId.trim() === (selected.partnerLevel2UserId || ""))}>
              {savingOwner ? "در حال ثبت…" : selected.rootLevel1UserId ? "تأیید تغییر انتساب" : "ثبت اولیه مالکیت کاربر قدیمی"}
            </button>
          </form>
          <button className="users-action" type="button" onClick={() => void readHistory()}>مشاهده تاریخچه انتساب</button>
          {transferNotice && <p role="status" className="auth-info">{transferNotice}</p>}
          {transferHistory && <ul className="users-owner-history">{transferHistory.length === 0 ?
            <li>سابقه انتقال قبلی موجود نیست.</li> : transferHistory.map(item =>
            <li key={item.id}>
              {new Date(item.changedAtUtc).toLocaleString("fa-IR")} —
              از {item.previousPartnerLevel2UserId || "سطح ۱"} به {item.newPartnerLevel2UserId || "سطح ۱"} —
              {item.explanation}
            </li>)}</ul>}
        </section>}
        {level1 && <section className="users-owner-edit" aria-label="مدیریت اشتراک کاربر">
          <h3>اصلاح پلن کاربر</h3>
          {subscriptionLoading?<p>در حال دریافت اشتراک…</p>:subscriptionError?<p role="alert" className="form-error">{subscriptionError}</p>:!subscription?<p>اشتراک فعال برای این کاربر وجود ندارد.</p>:<>
            <p>پلن فعلی: {subscription.kind===2?`تعدادی — سهمیه باقی‌مانده ${subscription.availableUnits??0} از ${subscription.invoiceUnitLimit}، روز باقی‌مانده ${subscription.expiresAtUtc?Math.max(0,Math.ceil((Date.parse(subscription.expiresAtUtc)-Date.now())/86400000)):subscription.validityDays}`:`زمانی — ${subscription.validityDays} روز`}</p>
            <form onSubmit={adjustSubscription}>
              <label>{subscription.kind===2?"سهمیه کل جدید (اختیاری)":"روز اعتبار جدید"}<input type="number" min={1} required={subscription.kind!==2} value={subscriptionValue} onChange={e=>setSubscriptionValue(e.target.value)}/></label>
              {subscription.kind===2&&<label>روزهای باقی‌مانده جدید (اختیاری)<input type="number" min={1} max={366} value={subscriptionDaysValue} onChange={e=>setSubscriptionDaysValue(e.target.value)}/></label>}
              <label>علت اصلاح یا لغو<textarea rows={2} maxLength={1000} required value={subscriptionReason} onChange={e=>setSubscriptionReason(e.target.value)}/></label>
              <button className="users-button" disabled={subscriptionBusy}>{subscriptionBusy?"در حال ثبت…":"اصلاح پلن"}</button>
              <button type="button" className="users-button" disabled={subscriptionBusy} onClick={openCancellation}>لغو پلن</button>
          </form>
          {subscriptionHistory.length > 0 && <ul className="users-owner-history"><li>تاریخچه اصلاح و لغو اشتراک</li>{subscriptionHistory.map(item =>
            <li key={item.id}>{new Date(item.createdAtUtc).toLocaleString("fa-IR")} — {item.kind===2?"لغو پلن":"اصلاح پلن"} — اثر مالی: {item.negativeGrossIrr.toLocaleString("fa-IR")} ریال — {item.reason}</li>)}</ul>}
          </>}
          {!subscriptionLoading && !subscriptionError && !subscription && subscriptionHistory.length > 0 && <ul className="users-owner-history"><li>تاریخچه اصلاح و لغو اشتراک</li>{subscriptionHistory.map(item =>
            <li key={item.id}>{new Date(item.createdAtUtc).toLocaleString("fa-IR")} — {item.kind===2?"لغو پلن":"اصلاح پلن"} — اثر مالی: {item.negativeGrossIrr.toLocaleString("fa-IR")} ریال — {item.reason}</li>)}</ul>}
        </section>}
        <p className="field-hint">این پنجره فقط اطلاعات حساب ورود و انتساب تجاری را مدیریت می‌کند؛ سوابق خریدار و مؤدی در بخش مالیاتی مستقل هستند.</p>
      </div>}
      <div className="users-dialog-footer"><button type="button" className="users-button"
        onClick={close}>بستن</button></div>
    </dialog>
    <dialog className="users-dialog" ref={cancelDialogRef} dir="rtl">
      <div className="users-dialog-head"><div><p>لغو اشتراک</p><h2>محاسبه برگشت وجه</h2></div>
        <button type="button" className="users-dialog-close" onClick={()=>cancelDialogRef.current?.close()} aria-label="بستن">×</button></div>
      <div className="users-dialog-content">
        {!cancellationPreview ? <>
          <p>نوع برگشت را انتخاب کنید:</p>
          <div className="users-dialog-footer">
            <button type="button" className="users-button" disabled={subscriptionBusy} onClick={()=>void previewCancellation(false)}>برگشت بر اساس مانده سرویس</button>
            <button type="button" className="users-button" disabled={subscriptionBusy} onClick={()=>void previewCancellation(true)}>برگشت کامل وجه</button>
          </div>
        </> : <>
          <dl className="users-details">
            <div><dt>قیمت پلن</dt><dd>{cancellationPreview.originalPriceIrr.toLocaleString("fa-IR")} ریال</dd></div>
            <div><dt>مقدار کسرشده</dt><dd>{cancellationPreview.deductedIrr.toLocaleString("fa-IR")} ریال</dd></div>
            <div><dt>مبلغ بازگشت</dt><dd><strong>{cancellationPreview.refundedIrr.toLocaleString("fa-IR")} ریال</strong></dd></div>
            <div><dt>مانده سرویس</dt><dd>{cancellationPreview.remainingDays!==null?`${cancellationPreview.remainingDays} روز`: `${cancellationPreview.remainingUnits??0} سهمیه`}</dd></div>
          </dl>
          <p>{cancellationPreview.fullRefund?"برگشت کامل وجه انتخاب شده است.":"برگشت بر اساس مانده سرویس انتخاب شده است."}</p>
          <button type="button" className="users-button" disabled={subscriptionBusy} onClick={()=>void previewCancellation(!cancellationPreview.fullRefund)}>تغییر نوع برگشت</button>
          <label className="users-cancel-reason" htmlFor="cancel-reason">علت لغو<textarea className="users-dialog-textarea" id="cancel-reason" rows={4} maxLength={1000} required value={cancelReason} onChange={e=>setCancelReason(e.target.value)}/></label>
          <button type="button" className="users-button" disabled={subscriptionBusy||!cancelReason.trim()} onClick={()=>void cancelSubscription()}>تأیید لغو پلن</button>
          {subscriptionHistory.length > 0 && <ul className="users-owner-history"><li>آخرین سوابق اشتراک</li>{subscriptionHistory.slice(0,5).map(item =>
            <li key={item.id}>{new Date(item.createdAtUtc).toLocaleString("fa-IR")} — {item.kind===2?"لغو":"اصلاح"} — {item.reason}</li>)}</ul>}
        </>}
      </div>
    </dialog>
  </section>;
}
