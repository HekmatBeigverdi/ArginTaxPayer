"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { PersianDatePicker } from "../catalog/persian-date-picker";
import { PersianTimePicker } from "../components/persian-time-picker";
import SearchableCatalogPicker from "./searchable-catalog-picker";
import InvoiceValidationPanel from "./invoice-validation-panel";
import {invoicePreview} from "./invoice-financial-preview.mjs";

type Taxpayer = { id: string; displayName: string; status: number };
type Invoice = { id: string; customerUserId: string; taxpayerId: string; revision: number;
  kind?: "workspace"; lastCompletedStep?: number;
  state: string; subject: string; issueDateUtc: string; updatedAtUtc: string;
  reviewState?: "WorkingDraft" | "Draft" | "InReview" | "ChangesRequested" | "Approved";
  systemCode?: string; sellerName?: string | null; buyerName?: string | null };
type CatalogItem = { id: string; title: string; internalCode?: string; baseUnitId?: string };
type HeaderOptions = {taxpayer:{id:string;displayName:string;normalizedIdentifier:string};
  memories:{id:string;memoryIdentifier:string;status:number}[]};
type FormMetadata = { edition: string; verifiedForSubmission: boolean;
  entries: {type:number;pattern:number|null;requiredFields:string[]}[] };
type CatalogPage = { items: CatalogItem[] };
type Account = { customerUserId: string; companyName?: string | null; personName?: string | null };
type FiscalEntry = { id: string; customerUserId: string; type: "buyer" | "taxpayer"; displayName: string; identifier: string; status: number };
type FiscalPage = { items: FiscalEntry[]; total: number };
type Row = { key: number; productId: string; unitId?: string; catalogCodeSnapshot?: string; name: string; quantity: string;
  unitPrice: string; discount: string; vatRate: string };
type Step = 0 | 1 | 2;
type SavedEntryLine = { catalogItemId: string | null; unitId?: string | null; catalogCodeSnapshot?: string | null; description: string; quantity: number;
  unitPriceRial: number; discountPerUnitRial: number; vatRatePercent: number | null };
type SavedEntry = { id: string; customerUserId: string; taxpayerId: string;
  revision: number; lastCompletedStep: number; issueDateUtc: string; updatedAtUtc: string;
  buyerPartyId: string | null; buyerDisplayName?: string | null;
  type: number; pattern: number; lines: SavedEntryLine[];
  invoiceDescription?: string | null; settlementMethod?: number;
  cashAmountRial?: number | null; creditAmountRial?: number | null;
  contractNumber?: string|null;serviceBillId?: string|null;flightType?: number|null;
  goldConstructionFeeRial?: number|null;goldSellerProfitRial?: number|null;
  goldCommissionRial?: number|null;goldTotalFeeProfitRial?: number|null;
  fiscalMemoryId?: string|null; };
const headings = ["سربرگ صورتحساب", "اقلام صورتحساب", "مرور نهایی و تسویه"] as const;
type Settlement = 0 | 1 | 2 | 3;
const errorCodes: Record<number, string> = {
  400: "ورودی پیش‌نویس معتبر نیست.", 401: "نشست منقضی شده است؛ دوباره وارد شوید.",
  403: "برای این پرونده دسترسی ندارید.", 404: "مؤدی یا صورتحساب یافت نشد.",
  409: "تغییر هم‌زمان رخ داده است؛ فهرست را بازخوانی کنید.",
  503: "سرویس فعلاً در دسترس نیست."
};

async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, { credentials: "same-origin", cache: "no-store", ...init });
  if (!response.ok) throw new Error(errorCodes[response.status] || "خطای سرویس (" + response.status + ")");
  return response.json() as Promise<T>;
}
const todayUtc = () => new Date().toISOString().slice(0, 10);
const toFaDate = (value: string) => new Date(value).toLocaleDateString("fa-IR-u-ca-persian", {timeZone:"UTC"});
const amount = (value: string) => {
  const n = Number(value);
  return Number.isFinite(n) && n >= 0 ? n : 0;
};
const fmt = (value: number) => value.toLocaleString("fa-IR", { maximumFractionDigits: 2 });

export default function InvoiceEntryWorkspace({actorId, isAdmin}: {actorId: string; isAdmin: boolean}) {
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [taxpayerId, setTaxpayerId] = useState("");
  const [memoryId,setMemoryId] = useState("");
  const [headerOptions,setHeaderOptions] = useState<HeaderOptions|null>(null);
  const [headerLoading,setHeaderLoading] = useState(false);
  const [headerError,setHeaderError] = useState("");
  const [filterCustomer, setFilterCustomer] = useState("");
  const [filterTaxpayer, setFilterTaxpayer] = useState("");
  const [headerCustomer, setHeaderCustomer] = useState(isAdmin ? "" : actorId);
  const [buyerId, setBuyerId] = useState("");
  const [buyerQuery, setBuyerQuery] = useState("");
  const [buyers, setBuyers] = useState<FiscalEntry[]>([]);
  const [buyerLoading, setBuyerLoading] = useState(false);
  const [buyerOpen, setBuyerOpen] = useState(false);
  const [buyerActive, setBuyerActive] = useState(0);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const requestSeq = useRef(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [search, setSearch] = useState("");
  const [searchHelpOpen, setSearchHelpOpen] = useState(false);
  const [reviewFilter, setReviewFilter] = useState("");
  const [listPage, setListPage] = useState(1);
  const listPageSize = 50;
  const [modal, setModal] = useState(false);
  const [editing, setEditing] = useState<SavedEntry | null>(null);
  const [step, setStep] = useState<Step>(0);
  const dialog = useRef<HTMLDivElement>(null);
  const openButton = useRef<HTMLButtonElement>(null);
  const [date, setDate] = useState(todayUtc);
  const [hour, setHour] = useState("12:00");

  const [formMetadata, setFormMetadata] = useState<FormMetadata | null>(null);
  const [formMetadataError, setFormMetadataError] = useState("");
  const [conditionalFields,setConditionalFields] = useState<Record<string,string>>({});
  const [invoiceType, setInvoiceType] = useState(1);
  const [pattern, setPattern] = useState(1);
  const [kind, setKind] = useState<"products" | "services">("products");
  const [catalogQuery, setCatalogQuery] = useState("");
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [catalog, setCatalog] = useState<CatalogItem[]>([]);
  const [catalogBusy, setCatalogBusy] = useState(false);
  const [rows, setRows] = useState<Row[]>([]);
  const [invoiceDescription,setInvoiceDescription] = useState("");
  const [settlement,setSettlement] = useState<Settlement>(0);
  const [cashInput,setCashInput] = useState("");
  const nextKey = useRef(0);
  const [detail, setDetail] = useState<Invoice | null>(null);

  useEffect(() => {
    if (!modal || !taxpayerId) { setHeaderOptions(null);setHeaderLoading(false);return; }
    let current = true;
    setHeaderLoading(true);setHeaderError("");
    getJson<HeaderOptions>("/api/invoices/header-options/"+taxpayerId)
      .then(data => {if(current)setHeaderOptions(data);})
      .catch(()=>{if(current){setHeaderOptions(null);setHeaderError("دریافت اطلاعات مؤدی و حافظه مالیاتی ناموفق بود.");}})
      .finally(()=>{if(current)setHeaderLoading(false);});
    return ()=>{current=false;};
  },[modal,taxpayerId]);

  useEffect(() => {
    let live = true;
    getJson<FormMetadata>("/api/invoices/form-metadata")
      .then(data => { if (live && data.entries.length) setFormMetadata(data); })
      .catch(() => { if (live) setFormMetadataError("دریافت قواعد رسمی فرم ناموفق بود؛ انتخاب الگو موقتاً غیرفعال است."); });
    return () => { live = false; };
  }, []);

  useEffect(() => {
    let live = true;
    getJson<Taxpayer[]>("/api/taxpayers/list")
      .then(list => {
        if (!live) return;
        const active = list.filter(t => t.status === 1);
        setTaxpayers(active);
        setTaxpayerId(current => active.some(t => t.id === current) ? current :
          isAdmin ? "" : active[0]?.id ?? "");
      })
      .catch(e => { if (live) setError((e as Error).message); })

    getJson<Account[]>("/api/customer-owners/accounts")
      .then(list => { if (live && Array.isArray(list)) setAccounts(list); })
      .catch(() => {});
    return () => { live = false; };
  }, []);

  const reload = useCallback(async () => {
    const requestId = ++requestSeq.current;
    setLoading(true);
    try {
      const query = new URLSearchParams();
      if (filterCustomer) query.set("customerId", filterCustomer);
      if (filterTaxpayer) query.set("taxpayerId", filterTaxpayer);
      if (reviewFilter) query.set("reviewState", reviewFilter);
      if (search.trim()) query.set("search", search.trim());
      query.set("page", String(listPage));
      query.set("pageSize", String(listPageSize));
      const list = await getJson<Invoice[]>("/api/invoices/overview?" + query);
      if (requestId === requestSeq.current) { setInvoices(list); setError(""); }
    } catch (e) { if (requestId === requestSeq.current) setError((e as Error).message); }
    finally { if (requestId === requestSeq.current) setLoading(false); }
  }, [filterCustomer, filterTaxpayer, reviewFilter, search, listPage]);
  useEffect(() => {
    setDetail(null);
    setLoading(true);
    const timer = window.setTimeout(() => {void reload();}, search ? 280 : 0);
    return () => { window.clearTimeout(timer); requestSeq.current++; };
  }, [reload]);

  const [customerTaxpayers, setCustomerTaxpayers] = useState<Taxpayer[]>([]);
  const [taxpayerOwners, setTaxpayerOwners] = useState<Record<string,string>>({});
  const visibleTaxpayers = isAdmin ? customerTaxpayers : taxpayers;
  const chosenBuyer = buyers.find(b => b.id === buyerId);

  async function scopedTaxpayersForCustomer(customer: string): Promise<Taxpayer[]> {
    const result: Taxpayer[] = [];
    for (let page = 1; page <= 100; page++) {
      const params = new URLSearchParams({
        type:"taxpayer",customerId:customer,page:String(page),pageSize:"100"
      });
      const response = await getJson<FiscalPage>("/api/fiscal-participants?" + params);
      result.push(...response.items.map(t => ({
        id:t.id,displayName:t.displayName,status:t.status
      })));
      if (page * 100 >= response.total) return result;
    }
    throw new Error("تعداد مؤدیان بیش از حد مجاز صفحه‌بندی است.");
  }

  useEffect(() => {
    if (!isAdmin || !headerCustomer) {
      setCustomerTaxpayers([]);
      return;
    }
    let active = true;
    scopedTaxpayersForCustomer(headerCustomer).then(list => {
      if (!active) return;
      const eligible = list.filter(t => t.status === 1);
      setCustomerTaxpayers(eligible);
      setTaxpayerOwners(old => ({
        ...old, ...Object.fromEntries(eligible.map(t => [t.id,headerCustomer]))
      }));
    }).catch(() => {if(active)setCustomerTaxpayers([]);});
    return () => {active=false;};
  }, [isAdmin,headerCustomer]);

  useEffect(() => {
    if (!isAdmin || !filterCustomer) return;
    let active = true;
    scopedTaxpayersForCustomer(filterCustomer).then(list => {
      if (active) setTaxpayerOwners(old => ({
        ...old,...Object.fromEntries(list.map(t => [t.id,filterCustomer]))
      }));
    }).catch(()=>{});
    return ()=>{active=false;};
  }, [isAdmin,filterCustomer]);

  useEffect(() => {
    if (!modal || !headerCustomer) { setBuyers([]); return; }
    let active = true;
    setBuyerLoading(true);
    const params = new URLSearchParams({
      page:"1",pageSize:"100",type:"buyer",customerId:headerCustomer
    });
    if (buyerQuery.trim()) params.set("query",buyerQuery.trim());
    getJson<FiscalPage>("/api/fiscal-participants?" + params)
      .then(page => {if(active)setBuyers(page.items.filter(x=>x.status===1));})
      .catch(() => {if(active)setBuyers([]);})
      .finally(()=>{if(active)setBuyerLoading(false);});
    return ()=>{active=false;};
  }, [modal,headerCustomer,buyerQuery]);

  useEffect(() => {
    if (!modal || step !== 1 || !headerCustomer) return;
    let live = true;
    setCatalogBusy(true);
    const query = new URLSearchParams({page:"1",pageSize:"20",query:catalogQuery});
    getJson<CatalogPage>("/api/catalog/customers/" + headerCustomer + "/" + kind + "?" + query)
      .then(data => { if (live) setCatalog(data.items ?? []); })
      .catch(() => { if (live) setCatalog([]); })
      .finally(() => { if (live) setCatalogBusy(false); });
    return () => { live = false; };
  }, [modal, step, kind, headerCustomer, catalogQuery]);

  useEffect(() => {
    if (!modal) return;
    const before = document.activeElement;
    dialog.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        // Open inner popovers get the first Escape, regardless of focus.
        // In particular the catalog picker can be open while a different
        // control is focused, so bubbling suppression alone is insufficient.
        if (dialog.current?.querySelector('[data-invoice-catalog-open="true"]')) {
          event.preventDefault();
          event.stopPropagation();
          window.dispatchEvent(new Event("invoice-catalog-dismiss"));
          return;
        }
        if (!busy) setModal(false);
        return;
      }
      if (event.key !== "Tab" || !dialog.current) return;
      const elements = [...dialog.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),a[href]'
      )].filter(el => el.getClientRects().length > 0);
      if (!elements.length) return;
      const first = elements[0], last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault(); last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault(); first.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      if (before instanceof HTMLElement && before.isConnected) before.focus();
      else openButton.current?.focus();
    };
  }, [modal, busy]);

  function open() {
    setEditing(null);
    setDetail(null);
    setStep(0); setDate(todayUtc()); setHour("12:00");
    setBuyerId(""); setBuyerQuery(""); setBuyerOpen(false); setInvoiceType(1); setPattern(1);
    setMemoryId("");setHeaderOptions(null);
    setConditionalFields({});
    setHeaderCustomer(isAdmin ? (filterCustomer || "") : actorId);
    setTaxpayerId(isAdmin ? "" : taxpayerId);
    setRows([]); setInvoiceDescription(""); setSettlement(0); setCashInput("");
    setError(""); setNotice(""); setModal(true);
  }
  async function editDraft(draft: Invoice) {
    setBusy(true); setError("");
    try {
      const saved = await getJson<SavedEntry>("/api/invoices/workspaces/"+draft.id);
      setEditing(saved);
      setHeaderCustomer(saved.customerUserId);
      setTaxpayerId(saved.taxpayerId);
      setDate(saved.issueDateUtc.slice(0,10));
      setHour(saved.issueDateUtc.slice(11,16));
      setMemoryId(saved.fiscalMemoryId ?? "");
      setBuyerId(saved.buyerPartyId ?? "");
      setBuyerQuery(saved.buyerDisplayName ?? "");
      setBuyerOpen(false);
      setInvoiceType(saved.type); setPattern(saved.pattern);
      setConditionalFields(Object.fromEntries((["contractNumber","serviceBillId","flightType",
        "goldConstructionFeeRial","goldSellerProfitRial","goldCommissionRial","goldTotalFeeProfitRial"] as const)
        .map(key=>[key,saved[key] == null?"":String(saved[key])])));
      setInvoiceDescription(saved.invoiceDescription ?? "");
      setSettlement((saved.settlementMethod ?? 0) as Settlement);
      setCashInput(saved.settlementMethod === 3 ? String(saved.cashAmountRial ?? "") : "");
      setRows(saved.lines.map((line,index)=>({
        key:index+1,productId:line.catalogItemId ?? "",
        unitId:line.unitId ?? undefined,catalogCodeSnapshot:line.catalogCodeSnapshot ?? undefined,
        name:line.description,quantity:String(line.quantity),
        unitPrice:String(line.unitPriceRial),discount:String(line.discountPerUnitRial),
        vatRate:String(line.vatRatePercent ?? "")
      })));
      nextKey.current = saved.lines.length;
      setStep(Math.min(2,Math.max(0,saved.lastCompletedStep)) as Step);
      setDetail(null); setModal(true);
    } catch(e) {setError((e as Error).message);}
    finally {setBusy(false);}
  }
  async function saveWorkspace(completeStep: Step, exit: boolean, navigateToBuyers = false,
    preserveVisibleStep = false) {
    // Opening the settlement screen does not complete it. Persist the lines
    // as step 1, then navigate to step 2; enforce terms on final completion only.
    const persistedStep: Step = !exit && step===1 && completeStep===2 ? 1 : completeStep;
    if (busy || !taxpayerId || !headerCustomer) {
      setError("ابتدا مشتری و مؤدی صادرکننده را انتخاب کنید."); return false;
    }
    const issued = new Date(date+"T"+hour+":00.000Z");
    if(Number.isNaN(issued.valueOf()) || issued.toISOString().slice(0,10)!==date) {
      setError("تاریخ یا ساعت صدور معتبر نیست.");return false;
    }
    if(rows.some(r=>!r.name.trim() || !Number.isFinite(Number(r.quantity)) ||
        Number(r.quantity)<0 || Number(r.unitPrice)<0 ||
        Number(r.discount)<0 || Number(r.discount)>Number(r.unitPrice) ||
        Number(r.vatRate)<0 || Number(r.vatRate)>=1000)) {
      setError("شرح یا مقادیر یکی از ردیف‌ها معتبر نیست.");return false;
    }
    try { invoicePreview(rows); }
    catch { setError("مقادیر عددی ردیف‌ها یا دقت آن‌ها معتبر نیست."); return false; }
    if (persistedStep===2 && settlement===0) {
      setError("برای تکمیل مراحل، روش تسویه صورتحساب را انتخاب کنید.");return false;
    }
    const cash = settlement === 1 ? calculated.total : settlement === 2 ? 0 :
      settlement === 3 ? roundRial(Number(cashInput)) : null;
    const credit = settlement === 1 ? 0 : settlement === 2 ? calculated.total :
      settlement === 3 ? roundRial(calculated.total - Number(cashInput)) : null;
    if (settlement === 3 && (cashInput.trim() === "" || !Number.isFinite(cash) ||
        Number(cash) <= 0 || Number(credit) <= 0 || Number(cash) >= calculated.total)) {
      setError("برای روش نقدی و نسیه، مبلغ نقدی باید بیشتر از صفر و کمتر از جمع صورتحساب باشد.");
      return false;
    }
    const required = formMetadata?.entries.find(p=>p.type===invoiceType && p.pattern===pattern)?.requiredFields;
    if (!formMetadata || !required) {setError("قواعد انتخاب الگو بارگذاری نشده‌اند.");return false;}
    const needed = required.includes("goldFields") ? ["goldConstructionFeeRial","goldSellerProfitRial","goldCommissionRial","goldTotalFeeProfitRial"] : required;
    if (completeStep>=1 && needed.some(k=>!conditionalFields[k]?.trim() ||
      (k!=="contractNumber" && k!=="serviceBillId" &&
        (!Number.isFinite(Number(conditionalFields[k])) || Number(conditionalFields[k])<0)))) {
      setError("لطفاً فیلدهای الزامی این الگو را تکمیل کنید.");return false;
    }
    if (headerLoading || headerError || !headerOptions || headerOptions.taxpayer.id!==taxpayerId) {
      setError("اطلاعات مؤدی صادرکننده هنوز آماده نیست؛ مجدداً تلاش کنید.");return false;
    }
    if(memoryId && !headerOptions.memories.some(m=>m.id===memoryId)) {
      setError("حافظه مالیاتی انتخاب‌شده به مؤدی صادرکننده تعلق ندارد.");return false;
    }
    if (buyerQuery.trim() && !buyerId) {setError("خریدار را از فهرست خریداران ثبت‌شده انتخاب کنید.");return false;}
    const payload = {
      id:editing?.id ?? null,
      taxpayerId, expectedRevision:editing?.revision ?? 0,
      lastCompletedStep:persistedStep, issueDateUtc:issued.toISOString(),
      buyerPartyId:buyerId || null, fiscalMemoryId:memoryId || null, type:invoiceType,pattern,
      ...Object.fromEntries(["contractNumber","serviceBillId","flightType",
        "goldConstructionFeeRial","goldSellerProfitRial","goldCommissionRial","goldTotalFeeProfitRial"]
        .map(k=>[k, !needed.includes(k) || !conditionalFields[k]?.trim() ? null :
          k==="contractNumber"||k==="serviceBillId" ? conditionalFields[k].trim() : Number(conditionalFields[k])])),
      invoiceDescription:invoiceDescription.trim() || null,
      settlementMethod:settlement, cashAmountRial:cash, creditAmountRial:credit,
      lines:rows.map(r=>({
        catalogItemId:r.productId || null,unitId:r.unitId || null,
        description:r.name.trim(),
        quantity:Number(r.quantity),unitPriceRial:Number(r.unitPrice),
        discountPerUnitRial:Number(r.discount),
        vatRatePercent:r.vatRate==="" ? null : Number(r.vatRate)
      }))
    };
    setBusy(true);setError("");
    try {
      const saved = await getJson<SavedEntry>("/api/invoices/workspaces",{
        method:"POST", headers:{"Content-Type":"application/json"},
        body:JSON.stringify(payload)
      });
      setEditing(saved);
      setNotice("پیش‌نویس با نسخه "+saved.revision+" ذخیره شد. برای ادامه از «ویرایش» استفاده کنید.");
      if(exit || navigateToBuyers) setModal(false);
      else if(!preserveVisibleStep) setStep(completeStep);
      await reload();
      if(navigateToBuyers) window.location.assign("/parties");
      return true;
    } catch(e) {setError((e as Error).message);return false;}
    finally {setBusy(false);}
  }
  function nextStep() {
    if(step===0 && !taxpayerId){setError("مؤدی صادرکننده را انتخاب کنید.");return;}
    if(step<2) void saveWorkspace((step+1) as Step,false);
  }
  function close() { if (!busy) setModal(false); }
  function addRow(item?: CatalogItem) {
    nextKey.current++;
    setRows(current => current.concat({
      key: nextKey.current, productId: item?.id ?? "",
      unitId:item?.baseUnitId,catalogCodeSnapshot:item?.internalCode,
      name: item?.title ?? "",
      quantity:"1", unitPrice:"0", discount:"0", vatRate:"0"
    }));
  }
  function updateRow(key: number, changes: Partial<Row>) {
    setRows(current => current.map(row => row.key === key ? {...row,...changes} : row));
  }
  const roundRial = (value:number) => Math.round((value + Number.EPSILON) * 100) / 100;
  const calculated = useMemo(() => {
    try { return invoicePreview(rows); }
    catch { return {base:0,discount:0,vat:0,total:0}; }
  }, [rows]);
  const cashPreview = settlement === 1 ? calculated.total : settlement === 2 ? 0 :
    settlement === 3 ? roundRial(amount(cashInput)) : 0;
  const creditPreview = settlement === 2 ? calculated.total : settlement === 1 ? 0 :
    settlement === 3 ? roundRial(Math.max(0,calculated.total-cashPreview)) : 0;
  const settlementError = settlement === 3 &&
    (cashInput.trim() === "" || !Number.isFinite(Number(cashInput)) ||
      cashPreview <= 0 || cashPreview >= calculated.total);
  const validationDirty = editing !== null && (
    editing.taxpayerId!==taxpayerId || editing.issueDateUtc.slice(0,16)!==date+"T"+hour ||
    (editing.buyerPartyId??"")!==buyerId || editing.type!==invoiceType ||
    editing.pattern!==pattern || (editing.invoiceDescription??"")!==invoiceDescription.trim() ||
    (editing.settlementMethod??0)!==settlement ||
    rows.length!==editing.lines.length ||
    rows.some((r,i)=>r.name.trim()!==editing.lines[i]?.description ||
      Number(r.quantity)!==editing.lines[i]?.quantity ||
      Number(r.unitPrice)!==editing.lines[i]?.unitPriceRial ||
      Number(r.discount)!==editing.lines[i]?.discountPerUnitRial ||
      (r.vatRate===""?null:Number(r.vatRate))!==editing.lines[i]?.vatRatePercent) ||
    settlement===3 && Number(cashInput)!==(editing.cashAmountRial??0)
  );
  const filtered = invoices; // All filters are applied server-side before paging.

  return <div className="invoice-entry invoice-register" dir="rtl" lang="fa">
    <div className="invoice-register__toolbar">
      <div className="invoice-register__filters">
        {isAdmin && <>
          <label htmlFor="invoice-list-customer">مشتری نرم‌افزار</label>
          <select id="invoice-list-customer" value={filterCustomer}
            onChange={e=>{setFilterCustomer(e.target.value);setFilterTaxpayer("");setListPage(1);}}>
            <option value="">همه مشتریان زیرمجموعه</option>
            {accounts.map(a=><option key={a.customerUserId} value={a.customerUserId}>
              {a.companyName || a.personName || "حساب مشتری"}
            </option>)}
          </select>
        </>}
        <label htmlFor="invoice-list-taxpayer">مؤدی</label>
        <select id="invoice-list-taxpayer" value={filterTaxpayer}
          onChange={e=>{setFilterTaxpayer(e.target.value);setListPage(1);}}>
          <option value="">همه مؤدیان مجاز</option>
          {taxpayers.filter(t=>!filterCustomer || taxpayerOwners[t.id]===filterCustomer)
            .map(t=><option key={t.id} value={t.id}>{t.displayName}</option>)}
        </select>
        <label htmlFor="invoice-list-review">وضعیت بازبینی</label>
        <select id="invoice-list-review" value={reviewFilter}
          onChange={e=>{setReviewFilter(e.target.value);setListPage(1);}}>
          <option value="">همه وضعیت‌ها</option>
          <option value="WorkingDraft">در حال تکمیل</option>
          <option value="Draft">پیش‌نویس داخلی</option>
          <option value="InReview">در انتظار بازبینی</option>
          <option value="ChangesRequested">نیازمند اصلاح</option>
          <option value="Approved">تأیید داخلی</option>
        </select>
        <div className="invoice-register__search">
          <div className="invoice-register__search-label">
            <label htmlFor="invoice-list-search">جست‌وجو</label>
            <div className="invoice-register__help-wrap">
              <button type="button" className="invoice-register__help-trigger" aria-label="راهنمای فیلدهای قابل جست‌وجو" aria-expanded={searchHelpOpen} aria-controls="invoice-search-help" onClick={() => setSearchHelpOpen(v=>!v)}>?</button>
              {searchHelpOpen && <div id="invoice-search-help" className="invoice-register__help-tooltip" role="tooltip">
                جست‌وجو بر اساس نام خریدار یا فروشنده، شناسه ملی یا کد اقتصادی آن‌ها و شناسه سیستمی صورتحساب انجام می‌شود.
              </div>}
            </div>
          </div>
          <input id="invoice-list-search" type="search" value={search} maxLength={100}
            onChange={e => {setSearch(e.target.value);setListPage(1);}} placeholder="نام خریدار، فروشنده، کد اقتصادی یا شناسه سیستمی" />
        </div>
      </div>
      <div className="invoice-register__actions">
        <button type="button" className="identity-secondary" disabled={loading}
          onClick={() => void reload()}>بازخوانی</button>
        <button ref={openButton} type="button" className="identity-primary"
          disabled={loading} onClick={open}>+ افزودن صورتحساب</button>
      </div>
    </div>
    {error && !modal && <p role="alert" className="form-error">{error}</p>}
    {notice && <p role="status" className="auth-info">{notice}</p>}
    <div className="console-table-scroll invoice-register__table-wrap" aria-busy={loading} style={{position:"relative",minHeight:"220px"}}>
      {loading && <div className="argin-loader argin-loader-overlay invoice-register__loading" role="status" aria-live="polite">
        <span className="argin-loader-spinner" aria-hidden="true" />
        <strong>در حال بارگذاری صورتحساب‌ها…</strong>
        <span className="argin-loader-subtitle">لطفاً تا دریافت اطلاعات منتظر بمانید</span>
      </div>}
      <table className="console-table">
        <thead><tr><th>شناسه سیستمی</th>{isAdmin && <th>مشتری</th>}<th>مؤدی فروشنده</th><th>خریدار</th><th>تاریخ صدور</th><th>موضوع</th>
          <th>وضعیت داخلی</th><th>نسخه</th><th>عملیات</th></tr></thead>
        <tbody>{filtered.length === 0 ? <tr><td colSpan={isAdmin ? 9 : 8}>صورتحسابی برای نمایش وجود ندارد.</td></tr> :
          filtered.map(item => <tr key={item.id}>
            <td><button type="button" className="console-row-action" title="کپی شناسه برای پشتیبانی" disabled={!item.systemCode} onClick={() => {void navigator.clipboard.writeText(item.systemCode || "").then(()=>setNotice("شناسه سیستمی کپی شد."));}}><bdi dir="ltr">{item.systemCode || "در انتظار تخصیص"}</bdi> ⧉</button></td>
            {isAdmin && <td>{accounts.find(a=>a.customerUserId===item.customerUserId)?.companyName ||
              accounts.find(a=>a.customerUserId===item.customerUserId)?.personName || "حساب مشتری"}</td>}
            <td>{item.sellerName || taxpayers.find(t=>t.id===item.taxpayerId)?.displayName || "مؤدی ثبت‌شده"}</td>
            <td>{item.buyerName || "ثبت نشده"}</td>
            <td>{toFaDate(item.issueDateUtc)}</td><td>{item.subject}</td>
            <td>{item.reviewState === "WorkingDraft" ? "در حال تکمیل" :
              item.reviewState === "InReview" ? "در انتظار بازبینی" :
              item.reviewState === "ChangesRequested" ? "نیازمند اصلاح" :
              item.reviewState === "Approved" ? "تأیید داخلی" : "پیش‌نویس داخلی"}</td>
            <td>{item.revision}</td>
            <td>{item.kind === "workspace" ? <button type="button" className="console-row-action"
              onClick={() => void editDraft(item)}>ویرایش و ادامه</button> :
              <button type="button" className="console-row-action"
                onClick={() => setDetail(item)}>مشاهده</button>}</td>
          </tr>)}</tbody>
      </table>
    </div>
    <footer className="console-table-footer invoice-register__pager" aria-label="صفحه‌بندی صورتحساب‌ها">
      <span>{invoices.length} رکورد در صفحه جاری</span>
      <div>
        <button type="button" disabled={loading || listPage === 1}
          onClick={()=>setListPage(p=>Math.max(1,p-1))}>قبلی</button>
        <span>صفحه {listPage}</span>
        <button type="button" disabled={loading || invoices.length < listPageSize}
          onClick={()=>setListPage(p=>p+1)}>بعدی</button>
      </div>
    </footer>
    {detail && <div className="invoice-register__overlay" onMouseDown={e => {
      if (e.currentTarget === e.target) setDetail(null);
    }}>
      <div className="invoice-register__detail-modal" role="dialog" aria-modal="true"
        aria-label="جزئیات صورتحساب">
        <div className="invoice-register__dialog-header">
          <h2>جزئیات صورتحساب</h2>
          <button type="button" className="identity-secondary" onClick={() => setDetail(null)}
            aria-label="بستن جزئیات">×</button>
        </div>
        <dl className="invoice-register__totals">
          <dt>شناسه سیستمی</dt><dd dir="ltr">{detail.systemCode || "در انتظار تخصیص"}</dd>
          <dt>تاریخ</dt><dd>{toFaDate(detail.issueDateUtc)}</dd>
          <dt>موضوع</dt><dd>{detail.subject}</dd>
          <dt>وضعیت بازبینی</dt><dd>{detail.reviewState === "Approved" ? "تأیید داخلی" :
            detail.reviewState === "InReview" ? "در انتظار بازبینی" :
            detail.reviewState === "ChangesRequested" ? "نیازمند اصلاح" :
            detail.reviewState === "WorkingDraft" ? "در حال تکمیل" : "پیش‌نویس داخلی"}</dd>
          <dt>وضعیت سند فاز ۷</dt><dd>{detail.state}</dd>
          <dt>نسخه</dt><dd>{detail.revision}</dd>
        </dl>
        <p className="field-hint">این وضعیت به معنی تأیید سازمان امور مالیاتی نیست.</p>
      </div>
    </div>}

    {modal && <div className="invoice-register__overlay" onMouseDown={e => {
      if (e.target === e.currentTarget) close();
    }}>
      <div ref={dialog} tabIndex={-1} className="invoice-register__dialog"
        role="dialog" aria-modal="true" aria-labelledby="invoice-dialog-title">
        <header className="invoice-register__dialog-header">
          <div><h2 id="invoice-dialog-title">{editing ? "ویرایش و ادامه پیش‌نویس" : "افزودن صورتحساب"}</h2>
            <p>پیش‌نویس داخلی — بدون ارسال مالیاتی</p></div>
          <button className="identity-secondary" type="button" aria-label="بستن پنجره"
            disabled={busy} onClick={close}>×</button>
        </header>
        <nav className="invoice-register__steps" aria-label="مراحل ورود صورتحساب">
          {headings.map((title,index) => <span key={title}
            aria-current={step === index ? "step" : undefined}
            className={step === index ? "active" : ""}>{index+1}. {title}</span>)}
        </nav>
        <div className="invoice-register__form">
          <div className="invoice-register__dialog-body">
            {error && <p role="alert" className="form-error">{error}</p>}
            {step === 0 && <section aria-label="سربرگ صورتحساب" className="invoice-register__fields">
              {isAdmin && <>
                <label htmlFor="invoice-header-customer">مشتری نرم‌افزار</label>
                <select id="invoice-header-customer" value={headerCustomer} required
                  onChange={e=>{setHeaderCustomer(e.target.value);setTaxpayerId("");setMemoryId("");setHeaderOptions(null);setBuyerId("");setBuyerQuery("");setBuyerOpen(false);}}>
                  <option value="">انتخاب مشتری</option>
                  {accounts.map(a=><option key={a.customerUserId} value={a.customerUserId}>
                    {a.companyName || a.personName || "مشتری مجاز"}
                  </option>)}
                </select>
              </>}
              <label htmlFor="invoice-taxpayer">مؤدی صادرکننده / فروشنده</label>
              <select id="invoice-taxpayer" value={taxpayerId} required
                onChange={e=>{setTaxpayerId(e.target.value);setMemoryId("");setHeaderOptions(null);setBuyerId("");setBuyerQuery("");setBuyerOpen(false);}}>
                <option value="">انتخاب مؤدی فروشنده</option>
                {visibleTaxpayers.map(t => <option key={t.id} value={t.id}>{t.displayName}</option>)}
              </select>
              <label htmlFor="invoice-memory">حافظه مالیاتی فروشنده (در پیش‌نویس اختیاری)</label>
              <select id="invoice-memory" value={memoryId} disabled={headerLoading || !headerOptions}
                onChange={e=>setMemoryId(e.target.value)}>
                <option value="">بدون انتخاب حافظه مالیاتی</option>
                {(headerOptions?.memories ?? []).map(m=><option key={m.id} value={m.id}>
                  {m.memoryIdentifier} — {m.status}
                </option>)}
              </select>
              {headerLoading && <small role="status">در حال دریافت اطلاعات مؤدی و حافظه مالیاتی…</small>}
              {headerError && <small role="alert" className="form-error">{headerError}</small>}
              {headerOptions && <small className="invoice-register__hint">
                شناسه ثبت‌شده مؤدی: <bdi dir="ltr">{headerOptions.taxpayer.normalizedIdentifier}</bdi>
                {headerOptions.memories.length===0 ? " — حافظه مالیاتی ثبت نشده است." : ""}
              </small>}
              <label htmlFor="invoice-buyer-search">خریدار ثبت‌شده</label>
              <div className="invoice-register__buyer">
                <input id="invoice-buyer-search" type="search" value={buyerQuery}
                  role="combobox" aria-autocomplete="list" aria-controls="invoice-buyer-options"
                  aria-expanded={buyerOpen} aria-activedescendant={
                    buyerOpen && buyers[buyerActive] ? "invoice-buyer-option-"+buyers[buyerActive].id : undefined
                  }
                  placeholder="جست‌وجوی نام یا شناسه خریدار" autoComplete="off"
                  onFocus={()=>{setBuyerOpen(true);setBuyerActive(0);}}
                  onChange={e=>{setBuyerQuery(e.target.value);setBuyerId("");
                    setBuyerOpen(true);setBuyerActive(0);}}
                  onKeyDown={e=>{
                    if(e.key==="Escape"){setBuyerOpen(false);return;}
                    if(e.key==="ArrowDown"){e.preventDefault();setBuyerOpen(true);
                      setBuyerActive(i=>Math.min(Math.max(0,buyers.length-1),i+1));}
                    if(e.key==="ArrowUp"){e.preventDefault();
                      setBuyerActive(i=>Math.max(0,i-1));}
                    if(e.key==="Enter" && buyerOpen && buyers[buyerActive]){
                      e.preventDefault();const b=buyers[buyerActive];
                      setBuyerId(b.id);setBuyerQuery(b.displayName);setBuyerOpen(false);
                    }
                  }}/>
                {buyerOpen && <div role="listbox" id="invoice-buyer-options"
                    className="invoice-register__buyer-options"
                    aria-label="نتایج جست‌وجوی خریداران">
                  {buyerLoading && <p role="status">در حال جست‌وجو…</p>}
                  {!buyerLoading && buyers.length===0 && <p>خریدار فعال یافت نشد.</p>}
                  {buyers.map((b,i)=><button type="button" role="option"
                      key={b.id} id={"invoice-buyer-option-"+b.id}
                      aria-selected={buyerId===b.id || buyerOpen && buyerActive===i}
                      className={buyerActive===i?"active":""}
                      onMouseDown={e=>e.preventDefault()}
                      onClick={()=>{setBuyerId(b.id);setBuyerQuery(b.displayName);setBuyerOpen(false);}}>
                    <strong>{b.displayName}</strong><small dir="ltr">{b.identifier}</small>
                  </button>)}
                </div>}
                {buyerId && <small className="invoice-register__selected-buyer">
                  خریدار انتخاب‌شده: {chosenBuyer?.displayName || buyerQuery}</small>}
              </div>
              <label htmlFor="invoice-date">تاریخ صدور شمسی</label>
              <PersianDatePicker id="invoice-date" value={date} onChange={setDate} floating/>
              <label htmlFor="invoice-hour">ساعت صدور (UTC)</label>
              <PersianTimePicker id="invoice-hour" value={hour} onChange={setHour} timeZoneLabel="UTC" />
              <label htmlFor="invoice-type">نوع صورتحساب</label>
              <select id="invoice-type" disabled={!formMetadata || busy} value={invoiceType} onChange={e => {
                const next = Number(e.target.value);
                setInvoiceType(next);
                setPattern(formMetadata?.entries.find(p=>p.type===next)?.pattern ?? 1);
              }}><option value={1}>نوع اول</option><option value={2}>نوع دوم</option></select>
              <label htmlFor="invoice-pattern">الگوی صورتحساب</label>
              <select id="invoice-pattern" disabled={!formMetadata || busy} value={pattern}
                onChange={e => setPattern(Number(e.target.value))}>
                {(formMetadata?.entries.filter(p=>p.type===invoiceType && p.pattern!==null) ?? []).map(p =>
                  <option key={p.pattern} value={p.pattern!}>{({1:"فروش",2:"فروش ارز",3:"طلا و جواهر",4:"پیمانکاری",5:"قبوض خدماتی",6:"بلیت هواپیما",7:"صادرات",8:"بارنامه",9:"فرآورده‌های نفتی",11:"بورس کالا",13:"خدمات بیمه‌ای",14:"فروش زنجیره"} as Record<number,string>)[p.pattern!] ?? ("الگوی "+p.pattern)}</option>)}
              </select>
              {(formMetadata?.entries.find(p=>p.type===invoiceType && p.pattern===pattern)?.requiredFields ?? []).flatMap(field =>
                field==="goldFields" ? ["goldConstructionFeeRial","goldSellerProfitRial","goldCommissionRial","goldTotalFeeProfitRial"] : [field]
              ).map(field => <div key={field} className="invoice-register__conditional-field">
                <label htmlFor={"invoice-conditional-"+field}>{({
                  contractNumber:"شماره قرارداد",serviceBillId:"شناسه قبض خدماتی",flightType:"نوع پرواز",
                  goldConstructionFeeRial:"اجرت ساخت (ریال)",goldSellerProfitRial:"سود فروشنده (ریال)",
                  goldCommissionRial:"حق‌العمل (ریال)",goldTotalFeeProfitRial:"جمع اجرت و سود و حق‌العمل (ریال)"
                } as Record<string,string>)[field]}</label>
                {field==="flightType" ?
                  <select id={"invoice-conditional-"+field} value={conditionalFields[field] ?? ""}
                    onChange={e=>setConditionalFields(old=>({...old,[field]:e.target.value}))}>
                    <option value="">انتخاب کنید</option><option value="1">داخلی</option><option value="2">خارجی</option>
                  </select> :
                  <input id={"invoice-conditional-"+field} dir="ltr" inputMode="numeric"
                    value={conditionalFields[field] ?? ""} maxLength={field==="contractNumber"?12:field==="serviceBillId"?19:20}
                    onChange={e=>setConditionalFields(old=>({...old,[field]:e.target.value}))} />}
              </div>)}
              {formMetadataError && <p role="alert" className="form-error">{formMetadataError}</p>}
              {formMetadata && <p className="invoice-register__hint">قواعد انتخاب الگو: {formMetadata.edition} — صرفاً اعتبارسنجی داخلی؛ مجوز ارسال به سامانه مودیان نیست.</p>}
              {(formMetadata?.entries.find(p=>p.type===invoiceType && p.pattern===pattern)?.requiredFields.length ?? 0)>0 &&
                <p role="status" className="invoice-register__hint">این الگو به فیلدهای تکمیلی اختصاصی نیاز دارد؛ تکمیل و اعتبارسنجی آن‌ها در مراحل بعدی فرم انجام خواهد شد.</p>}
              <p className="invoice-register__hint">اطلاعات مرحله‌ای در پیش‌نویس خصوصی ذخیره می‌شوند؛ ثبت خریدار را می‌توانید بعداً تکمیل کنید. این پیش‌نویس مجوز ارسال مالیاتی ندارد.</p>
            </section>}
            {step === 1 && <section aria-label="اقلام صورتحساب">
              <p className="invoice-register__hint">ردیف‌های این مرحله در پیش‌نویس قابل‌ویرایش ذخیره می‌شوند؛ پیش از صدور مالیاتی به اعتبارسنجی رسمی نیاز دارند.</p>
              <div className="invoice-register__row-tools">
                <label htmlFor="invoice-kind">نوع قلم</label>
                <select id="invoice-kind" value={kind}
                  onChange={e => setKind(e.target.value as "products"|"services")}>
                  <option value="products">کالا</option><option value="services">خدمت</option>
                </select>
              </div>
              <div className="invoice-register__catalog-picker">
                <label htmlFor="invoice-catalog-search">انتخاب کالا / خدمت</label>
                <SearchableCatalogPicker items={catalog} value={catalogQuery}
                  onQueryChange={setCatalogQuery} loading={catalogBusy}
                  onChoose={addRow}/>
              </div>
              <div className="invoice-register__row-heading">
                <h3>ردیف‌های صورتحساب ({rows.length})</h3>
                <button type="button" className="identity-secondary" onClick={() => addRow()}>
                  + ردیف جدید</button>
              </div>
              {rows.length === 0 ? <p className="identity-muted">هنوز ردیفی اضافه نشده است.</p> :
                rows.map((row,index) => <div className="invoice-register__item" key={row.key}>
                  <strong>ردیف {index+1}</strong>
                  {row.productId && <small className="field-hint">کد کالا/خدمت هنگام انتخاب: <bdi dir="ltr">{row.catalogCodeSnapshot || "—"}</bdi> · واحد پایه ثبت‌شده: <bdi dir="ltr">{row.unitId || "پس از ذخیره تعیین می‌شود"}</bdi></small>}
                  <label>شرح<input value={row.name} maxLength={256}
                    onChange={e => updateRow(row.key,{name:e.target.value})}/></label>
                  <label>مقدار<input dir="ltr" type="number" min="0.00000001" step="any"
                    value={row.quantity} onChange={e => updateRow(row.key,{quantity:e.target.value})}/></label>
                  <label>فی واحد (ریال)<input dir="ltr" type="number" min="0" step="any"
                    value={row.unitPrice} onChange={e => updateRow(row.key,{unitPrice:e.target.value})}/></label>
                  <label>تخفیف واحد<input dir="ltr" type="number" min="0" step="any"
                    value={row.discount} onChange={e => updateRow(row.key,{discount:e.target.value})}/></label>
                  <label>نرخ VAT (%)<input dir="ltr" type="number" min="0" step="any"
                    value={row.vatRate} onChange={e => updateRow(row.key,{vatRate:e.target.value})}/></label>
                  <button type="button" className="identity-secondary"
                    onClick={() => setRows(current => current.filter(x => x.key !== row.key))}>حذف ردیف</button>
                </div>)}
              <p className="field-hint">برای مدیریت کاتالوگ به <Link href="/catalog">کالاها و خدمات</Link> مراجعه کنید.</p>
            </section>}
            {step === 2 && <section aria-label="مرور نهایی و تسویه صورتحساب"
              className="invoice-register__final">
              <div className="invoice-register__final-heading">
                <strong>مرور کلی صورتحساب</strong>
                <span>خلاصه مالی، روش تسویه و توضیحات</span>
              </div>
              <div className="invoice-register__final-grid">
                <section className="invoice-register__final-details" aria-label="مشخصات صورتحساب">
                  <h3>مشخصات کلی</h3>
                  <dl className="invoice-register__summary-info">
                    <dt>مؤدی / فروشنده</dt>
                    <dd>{visibleTaxpayers.find(t=>t.id===taxpayerId)?.displayName || "مؤدی انتخاب‌شده"}</dd>
                    <dt>خریدار</dt><dd>{buyerQuery || "هنوز انتخاب نشده"}</dd>
                    <dt>تاریخ و زمان</dt><dd dir="ltr">{date} {hour} UTC</dd>
                    <dt>نوع و الگو</dt><dd>نوع {invoiceType} · الگوی {pattern}</dd>
                    <dt>تعداد ردیف‌ها</dt><dd>{rows.length}</dd>
                  </dl>
                </section>
                <section aria-label="خلاصه مالی">
                  <h3>خلاصه مبالغ (ریال)</h3>
                  <dl className="invoice-register__totals">
                    <dt>مجموع مبلغ قبل از تخفیف</dt><dd>{fmt(calculated.base)}</dd>
                    <dt>مجموع تخفیفات</dt><dd>{fmt(calculated.discount)}</dd>
                    <dt>مجموع مبلغ پس از تخفیف</dt><dd>{fmt(calculated.base-calculated.discount)}</dd>
                    <dt>مجموع مالیات بر ارزش افزوده (محاسبه موقت)</dt><dd>{fmt(calculated.vat)}</dd>
                    <dt>سایر مالیات و عوارض (در این پیش‌نویس پشتیبانی نمی‌شود)</dt><dd>—</dd>
                    <dt className="invoice-register__grand">جمع صورتحساب</dt>
                    <dd className="invoice-register__grand">{fmt(calculated.total)} ریال</dd>
                  </dl>
                </section>
              </div>
              <div className="invoice-register__settlement">
                <div className="invoice-register__field">
                  <label htmlFor="invoice-settlement-method">روش تسویه</label>
                  <select id="invoice-settlement-method" value={settlement}
                    onChange={event=>{setSettlement(Number(event.target.value) as Settlement);setCashInput("");}}>
                    <option value={0}>نامعلوم</option>
                    <option value={1}>نقدی</option>
                    <option value={2}>نسیه</option>
                    <option value={3}>نقدی و نسیه</option>
                  </select>
                </div>
                <div className="invoice-register__field">
                  <label htmlFor="invoice-cash-amount">مبلغ نقدی (ریال)</label>
                  <input id="invoice-cash-amount" type="number" min="0" step="any"
                    dir="ltr" value={settlement===3 ? cashInput : String(cashPreview)}
                    disabled={settlement!==3}
                    onChange={e=>setCashInput(e.target.value)} />
                </div>
                <div className="invoice-register__field">
                  <label htmlFor="invoice-credit-amount">مبلغ نسیه (ریال)</label>
                  <input id="invoice-credit-amount" type="text" dir="ltr"
                    value={fmt(creditPreview)} readOnly aria-readonly="true" />
                </div>
              </div>
              {settlement===0 && <p role="status" className="field-hint">برای تکمیل مرحله نهایی، روش تسویه را مشخص کنید؛ ذخیره پیش‌نویس همچنان مجاز است.</p>}
              {settlementError && <p role="alert" className="form-error">
                برای تسویه نقدی و نسیه، مبلغ نقدی باید مثبت و کمتر از مبلغ صورتحساب باشد.
              </p>}
              <div className="invoice-register__description">
                <label htmlFor="invoice-description">توضیحات صورتحساب</label>
                <textarea id="invoice-description" rows={3} maxLength={1500}
                  value={invoiceDescription}
                  onChange={e=>setInvoiceDescription(e.target.value)}
                  placeholder="توضیحات، توافقات یا شرح تکمیلی فاکتور (اختیاری)"/>
                <small>{invoiceDescription.length} / ۱۵۰۰ نویسه</small>
              </div>
              {editing && <InvoiceValidationPanel
                draftId={editing.id} taxpayerId={editing.taxpayerId}
                revision={editing.revision} dirty={validationDirty}
                onSave={()=>saveWorkspace(step===2 && settlement===0 ? 1 : step,false,false,true)}/>}
              <p className="invoice-register__hint">
                توضیحات و تسویه در پیش‌نویس قابل‌ویرایش ذخیره می‌شوند.
                مبالغ مالیاتی و روش تسویه قبل از صدور رسمی باید در سرویس اعتبارسنجی
                بررسی شوند؛ ذخیره پیش‌نویس به معنای ارسال به سامانه مؤدیان نیست.
              </p>
            </section>}
          </div>
          <footer className="invoice-register__dialog-footer">
            <button type="button" className="identity-secondary" disabled={busy || !taxpayerId}
              onClick={() => void saveWorkspace(step===2 && settlement===0 ? 1 : step,true)}>ذخیره و ادامه بعداً</button>
            {step===0 && !buyerId && <button type="button" className="identity-secondary"
              disabled={busy || !taxpayerId} onClick={() => void saveWorkspace(0,true,true)}>
                ذخیره و ثبت خریدار</button>}
            <button type="button" className="identity-secondary" onClick={close}
              disabled={busy}>انصراف</button>
            {step > 0 && <button type="button" className="identity-secondary"
              onClick={() => {setError("");setStep((step-1) as Step);}} disabled={busy}>مرحله قبل</button>}
            {step < 2 ? <button type="button" className="identity-primary"
              disabled={busy || step === 0 && (!taxpayerId || !headerCustomer || !date)}
              onClick={nextStep}>مرحله بعد</button> :
              <button type="button" className="identity-primary"
                disabled={busy || !taxpayerId || settlementError || settlement===0}
                onClick={() => void saveWorkspace(2,true)}>
                {busy ? "در حال ذخیره…" : "ذخیره مراحل و خروج"}</button>}
          </footer>
        </div>
      </div>
    </div>}
  </div>;
}
