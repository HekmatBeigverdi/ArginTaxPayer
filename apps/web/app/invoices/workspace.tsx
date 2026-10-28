"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { PersianDatePicker } from "../catalog/persian-date-picker";
import { PersianTimePicker } from "../components/persian-time-picker";
import SearchableCatalogPicker from "./searchable-catalog-picker";
import InvoiceValidationPanel from "./invoice-validation-panel";
import InvoiceHistoryPanel from "./invoice-history-panel";
import InvoiceSendReadinessPanel from "./invoice-send-readiness-panel";
import InvoiceFormalPreview from "./invoice-formal-preview";
import InvoiceDispatchHistory from "./invoice-dispatch-history";
import InvoiceImportPanel, {type ImportValues} from "./invoice-import-panel";
import BulkInvoiceImportPanel from "./bulk-invoice-import-panel";
import {invoicePreview} from "./invoice-financial-preview.mjs";

type Taxpayer = {
  id: string;
  displayName: string;
  status: number;
};

type Invoice = {
  id: string;
  customerUserId: string;
  taxpayerId: string;
  revision: number;
  kind?: "workspace" | "internalSnapshot";
  lastCompletedStep?: number;
  state: string;
  subject: string;
  issueDateUtc: string;
  updatedAtUtc: string;
  reviewState?: "WorkingDraft" | "Draft" | "InReview" | "ChangesRequested" | "Approved";
  systemCode?: string;
  sellerName?: string | null;
  buyerName?: string | null;
  authorityStatus?: string;
};

type CatalogItem = {
  id: string;
  title: string;
  internalCode?: string;
  officialCode?: string | null;
  baseUnitId?: string;
};

type HeaderOptions = {
  taxpayer: {
    id: string;
    displayName: string;
    normalizedIdentifier: string;
  };
  memories: { id: string; memoryIdentifier: string; status: number }[];
  sandboxMemories?: {
    id: string;
    memoryIdentifier: string;
    environment: "Sandbox";
  }[];
};

type FormMetadata = {
  edition: string;
  verifiedForSubmission: boolean;
  entries: { type: number; pattern: number | null; requiredFields: string[] }[];
};
type CatalogPage = { items: CatalogItem[] };
type Account = {
  customerUserId: string;
  companyName?: string | null;
  personName?: string | null;
};

type FiscalEntry = {
  id: string;
  customerUserId: string;
  type: "buyer" | "taxpayer";
  displayName: string;
  identifier: string;
  kind: 1 | 2;
  status: number;
};

type FiscalPage = { items: FiscalEntry[]; total: number };

type Row = {
  key: number;
  productId: string;
  unitId?: string;
  catalogCodeSnapshot?: string;
  officialIdentifierSnapshot?: string;
  name: string;
  quantity: string;
  unitPrice: string;
  discount: string;
  vatRate: string;
};

type Step = 0 | 1 | 2;

type SavedEntryLine = {
  catalogItemId: string | null;
  unitId?: string | null;
  catalogCodeSnapshot?: string | null;
  officialIdentifierSnapshot?: string | null;
  description: string;
  quantity: number;
  unitPriceRial: number;
  discountPerUnitRial: number;
  vatRatePercent: number | null;
};

type SavedEntry = {
  id: string;
  customerUserId: string;
  taxpayerId: string;
  revision: number;
  lastCompletedStep: number;
  issueDateUtc: string;
  updatedAtUtc: string;
  buyerPartyId: string | null;
  buyerDisplayName?: string | null;
  type: number;
  pattern: number;
  lines: SavedEntryLine[];
  invoiceDescription?: string | null;
  settlementMethod?: number;
  cashAmountRial?: number | null;
  creditAmountRial?: number | null;
  otherTaxAndLegalFeesRial?: number | null;
  contractNumber?: string | null;
  serviceBillId?: string | null;
  flightType?: number | null;
  goldConstructionFeeRial?: number | null;
  goldSellerProfitRial?: number | null;
  goldCommissionRial?: number | null;
  goldTotalFeeProfitRial?: number | null;
  fiscalMemoryId?: string | null;
  sandboxFiscalMemoryId?: string | null;
};

const headings = ["سربرگ صورتحساب", "اقلام صورتحساب", "مرور نهایی و تسویه"] as const;
type Settlement = 0 | 1 | 2 | 3;

const PATTERN_LABELS: Record<number, string> = {
  1: "فروش",
  2: "فروش ارز",
  3: "طلا و جواهر",
  4: "پیمانکاری",
  5: "قبوض خدماتی",
  6: "بلیت هواپیما",
  7: "صادرات",
  8: "بارنامه",
  9: "فرآورده‌های نفتی",
  11: "بورس کالا",
  13: "خدمات بیمه‌ای",
  14: "فروش زنجیره"
};

const FIELD_LABELS: Record<string, string> = {
  contractNumber: "شماره قرارداد",
  serviceBillId: "شناسه قبض خدماتی",
  flightType: "نوع پرواز",
  goldConstructionFeeRial: "اجرت ساخت (ریال)",
  goldSellerProfitRial: "سود فروشنده (ریال)",
  goldCommissionRial: "حق‌العمل (ریال)",
  goldTotalFeeProfitRial: "جمع اجرت و سود و حق‌العمل (ریال)"
};

const GOLD_FIELDS = [
  "goldConstructionFeeRial",
  "goldSellerProfitRial",
  "goldCommissionRial",
  "goldTotalFeeProfitRial"
] as const;

const errorCodes: Record<number, string> = {
  400: "ورودی پیش‌نویس معتبر نیست.", 401: "نشست منقضی شده است؛ دوباره وارد شوید.",
  403: "برای این پرونده دسترسی ندارید.", 404: "مؤدی یا صورتحساب یافت نشد.",
  409: "نسخه این پیش‌نویس در محل دیگری تغییر کرده است. برای جلوگیری از بازنویسی اطلاعات، ذخیره انجام نشد؛ پیش‌نویس را دوباره باز کنید.",
  503: "سرویس فعلاً در دسترس نیست."
};

async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, { credentials: "same-origin", cache: "no-store", ...init });
  if (!response.ok) throw new Error(errorCodes[response.status] || "خطای سرویس (" + response.status + ")");
  return response.json() as Promise<T>;
}
const tehranDateTime = (instant: Date) => {
  const parts = new Intl.DateTimeFormat("en-CA-u-nu-latn", {
    timeZone: "Asia/Tehran", year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", hourCycle: "h23"
  }).formatToParts(instant);
  const get = (type: string) => parts.find(part => part.type === type)?.value ?? "00";
  return {date: `${get("year")}-${get("month")}-${get("day")}`,
    time: `${get("hour")}:${get("minute")}`};
};
const tehranLocalToUtc = (date: string, time: string) => {
  const [year, month, day] = date.split("-").map(Number);
  const [hour, minute] = time.split(":").map(Number);
  const target = Date.UTC(year, month - 1, day, hour, minute);
  let utc = target;
  for (let attempt = 0; attempt < 3; attempt++) {
    const local = tehranDateTime(new Date(utc));
    const [localYear, localMonth, localDay] = local.date.split("-").map(Number);
    const [localHour, localMinute] = local.time.split(":").map(Number);
    const represented = Date.UTC(localYear, localMonth - 1, localDay, localHour, localMinute);
    const correction = target - represented;
    utc += correction;
    if (correction === 0) break;
  }
  return new Date(utc);
};
const toFaDate = (value: string) => new Date(value).toLocaleDateString("fa-IR-u-ca-persian", {timeZone:"UTC"});
const amount = (value: string) => {
  const n = Number(value);
  return Number.isFinite(n) && n >= 0 ? n : 0;
};
const fmt = (value: number) => value.toLocaleString("fa-IR", { maximumFractionDigits: 2 });

export default function InvoiceEntryWorkspace({actorId, isAdmin}: {actorId: string; isAdmin: boolean}) {
  // List and filtering state.
  const [taxpayers, setTaxpayers] = useState<Taxpayer[]>([]);
  const [taxpayerId, setTaxpayerId] = useState("");
  const [filterCustomer, setFilterCustomer] = useState("");
  const [filterTaxpayer, setFilterTaxpayer] = useState("");
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const requestSeq = useRef(0);
  const [search, setSearch] = useState("");
  const [searchHelpOpen, setSearchHelpOpen] = useState(false);
  const [reviewFilter, setReviewFilter] = useState("");
  const [listPage, setListPage] = useState(1);
  const listPageSize = 15;

  // Modal and draft lifecycle state.
  const [modal, setModal] = useState(false);
  const [editing, setEditing] = useState<SavedEntry | null>(null);
  const [step, setStep] = useState<Step>(0);
  const [savedSnapshot,setSavedSnapshot] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const dialog = useRef<HTMLDivElement>(null);
  const openButton = useRef<HTMLButtonElement>(null);
  const [importOpen,setImportOpen] = useState(false);
  const [bulkOpen,setBulkOpen] = useState(false);

  // Header, buyer, and invoice form state.
  const [headerCustomer, setHeaderCustomer] = useState(isAdmin ? "" : actorId);
  const [memoryId,setMemoryId] = useState("");
  const [headerOptions,setHeaderOptions] = useState<HeaderOptions|null>(null);
  const [headerLoading,setHeaderLoading] = useState(false);
  const [headerError,setHeaderError] = useState("");
  const [buyerId, setBuyerId] = useState("");
  const [buyerQuery, setBuyerQuery] = useState("");
  const [buyers, setBuyers] = useState<FiscalEntry[]>([]);
  const [buyerLoading, setBuyerLoading] = useState(false);
  const [buyerOpen, setBuyerOpen] = useState(false);
  const [buyerActive, setBuyerActive] = useState(0);
  const [date, setDate] = useState(() => tehranDateTime(new Date()).date);
  const [hour, setHour] = useState(() => tehranDateTime(new Date()).time);
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
  const [otherTaxInput,setOtherTaxInput] = useState("0");
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
      .then(data => { if (live) {
        const items = data.items ?? [];
        setCatalog(items);
        const byId = new Map(items.map(item => [item.id, item]));
        setRows(current => current.map(row => {
          const item = byId.get(row.productId);
          return item ? {...row, officialIdentifierSnapshot: row.officialIdentifierSnapshot ?? item.officialCode ?? undefined} : row;
        }));
      } })
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
        if (importOpen) {event.preventDefault();setImportOpen(false);return;}
        // Open inner popovers get the first Escape, regardless of focus.
        // In particular the catalog picker can be open while a different
        // control is focused, so bubbling suppression alone is insufficient.
        if (dialog.current?.querySelector('[data-invoice-catalog-open="true"]')) {
          event.preventDefault();
          event.stopPropagation();
          window.dispatchEvent(new Event("invoice-catalog-dismiss"));
          return;
        }
        if (!busy && (!unsavedRef.current || window.confirm("تغییرات ذخیره‌نشده از بین می‌روند. پنجره بسته شود؟"))) setModal(false);
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
  }, [modal, busy, importOpen]);

  function open() {
    setEditing(null);
    setDetail(null);
    const currentTehran = tehranDateTime(new Date());
    setStep(0); setDate(currentTehran.date); setHour(currentTehran.time);
    setBuyerId(""); setBuyerQuery(""); setBuyerOpen(false); setInvoiceType(1); setPattern(1);
    setMemoryId("");setHeaderOptions(null);
    setConditionalFields({});
    setHeaderCustomer(isAdmin ? (filterCustomer || "") : actorId);
    setTaxpayerId(isAdmin ? "" : taxpayerId);
    setRows([]); setInvoiceDescription(""); setSettlement(0); setCashInput("");
    setOtherTaxInput("0");
    setSavedSnapshot(null); setError(""); setNotice(""); setModal(true);
  }
  useEffect(() => {
    const params=new URLSearchParams(window.location.search);
    const workspaceId=params.get("workspaceId");
    if(params.get("issueDateAdjusted")==="1")
      setNotice("زمان پیش‌نویس اصلاح که در آینده بود با زمان فعلی سامانه جایگزین شد؛ پیش از ثبت، تاریخ و ساعت واقعی را بررسی کنید.");
    if(workspaceId&&/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(workspaceId))
      void editDraft({id:workspaceId});
  }, []);
  async function editDraft(draft: Pick<Invoice,"id">) {
    setBusy(true); setError("");
    try {
      const saved = await getJson<SavedEntry>("/api/invoices/workspaces/"+draft.id);
      setEditing(saved);
      setSavedSnapshot(null);
      setHeaderCustomer(saved.customerUserId);
      setTaxpayerId(saved.taxpayerId);
      const savedTehran = tehranDateTime(new Date(saved.issueDateUtc));
      setDate(savedTehran.date);
      setHour(savedTehran.time);
      // Preserve the regular memory value for older drafts, then override it
      // only when the draft explicitly points at a Sandbox memory.
      setMemoryId(saved.fiscalMemoryId ?? "");
      if (saved.sandboxFiscalMemoryId) {
        setMemoryId("sandbox:"+saved.sandboxFiscalMemoryId);
      }
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
      setOtherTaxInput(String(saved.otherTaxAndLegalFeesRial ?? 0));
      setRows(saved.lines.map((line,index)=>({
        key:index+1,productId:line.catalogItemId ?? "",
        unitId:line.unitId ?? undefined,catalogCodeSnapshot:line.catalogCodeSnapshot ?? undefined,
         officialIdentifierSnapshot:line.officialIdentifierSnapshot ?? undefined,
        name:line.description,quantity:String(line.quantity),
        unitPrice:String(line.unitPriceRial),discount:String(line.discountPerUnitRial),
        vatRate:String(line.vatRatePercent ?? "")
      })));
      nextKey.current = saved.lines.length;
      setStep(Math.min(2,Math.max(0,saved.lastCompletedStep+1)) as Step);
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
    const issued = tehranLocalToUtc(date,hour);
    if(Number.isNaN(issued.valueOf()) || tehranDateTime(issued).date!==date ||
        tehranDateTime(issued).time!==hour) {
      setError("تاریخ یا ساعت صدور معتبر نیست.");return false;
    }
    if(issued.getTime()>Date.now()) {
      setError("تاریخ و زمان صدور صورتحساب نمی‌تواند در آینده باشد.");return false;
    }
    if(rows.some(r=>!r.name.trim() || !Number.isFinite(Number(r.quantity)) ||
        Number(r.quantity)<0 || Number(r.unitPrice)<0 ||
        Number(r.discount)<0 || Number(r.discount)>Number(r.unitPrice) ||
        Number(r.vatRate)<0 || Number(r.vatRate)>=1000)) {
      setError("شرح یا مقادیر یکی از ردیف‌ها معتبر نیست.");return false;
    }
    if(persistedStep===2 && rows.some(r=>!/^\d{13}$/.test(r.officialIdentifierSnapshot??""))) {
      setError("شناسه رسمی هر کالا یا خدمت باید ۱۳ رقم باشد؛ اعتبار آن هنگام ارسال توسط سامانه مالیاتی بررسی می‌شود.");return false;
    }
    try { invoicePreview(rows); }
    catch { setError("مقادیر عددی ردیف‌ها یا دقت آن‌ها معتبر نیست."); return false; }
    if (persistedStep===2 && settlement===0) {
      setError("برای تکمیل مراحل، روش تسویه صورتحساب را انتخاب کنید.");return false;
    }
    const parsedOtherTax = Number(otherTaxInput);
    const otherTax = parsedOtherTax;
    const invoiceTotal = calculated.total + otherTax;
    const cash = settlement === 1 ? invoiceTotal : settlement === 2 ? 0 :
      settlement === 3 ? roundRial(Number(cashInput)) : null;
    const credit = settlement === 1 ? 0 : settlement === 2 ? invoiceTotal :
      settlement === 3 ? roundRial(invoiceTotal - Number(cashInput)) : null;
    if (settlement === 3 && (cashInput.trim() === "" || !Number.isFinite(cash) ||
        Number(cash) <= 0 || Number(credit) <= 0 || Number(cash) >= invoiceTotal)) {
      setError("برای روش نقدی و نسیه، مبلغ نقدی باید بیشتر از صفر و کمتر از جمع صورتحساب باشد.");
      return false;
    }
    if (otherTaxInput.trim() === "" || !Number.isFinite(parsedOtherTax) ||
        otherTax < 0 || !Number.isInteger(otherTax)) {
      setError("مجموع سایر مالیات و وجوه قانونی باید عدد صحیح نامنفی باشد.");
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
    if(memoryId && !(memoryId.startsWith("sandbox:")
      ? headerOptions.sandboxMemories?.some(m=>m.id===memoryId.slice(8))
      : headerOptions.memories.some(m=>m.id===memoryId))) {
      setError("حافظه مالیاتی انتخاب‌شده به مؤدی صادرکننده تعلق ندارد.");return false;
    }
    if (buyerQuery.trim() && !buyerId) {setError("خریدار را از فهرست خریداران ثبت‌شده انتخاب کنید.");return false;}
    const payload = {
      id:editing?.id ?? null,
      taxpayerId, expectedRevision:editing?.revision ?? 0,
      lastCompletedStep:persistedStep, issueDateUtc:issued.toISOString(),
      buyerPartyId:buyerId || null,
      // Production memories use the regular field (fiscalMemoryId:memoryId || null);
      // Sandbox memories must stay isolated from that foreign-key field.
      fiscalMemoryId:memoryId&&!memoryId.startsWith("sandbox:")?memoryId:null,
      sandboxFiscalMemoryId:memoryId.startsWith("sandbox:")?memoryId.slice(8):null,
      type:invoiceType,pattern,
      ...Object.fromEntries(["contractNumber","serviceBillId","flightType",
        "goldConstructionFeeRial","goldSellerProfitRial","goldCommissionRial","goldTotalFeeProfitRial"]
        .map(k=>[k, !needed.includes(k) || !conditionalFields[k]?.trim() ? null :
          k==="contractNumber"||k==="serviceBillId" ? conditionalFields[k].trim() : Number(conditionalFields[k])])),
      invoiceDescription:invoiceDescription.trim() || null,
      settlementMethod:settlement, cashAmountRial:cash, creditAmountRial:credit,
      otherTaxAndLegalFeesRial:otherTax,
      lines:rows.map(r=>({
        catalogItemId:r.productId || null,unitId:r.unitId || null,
        description:r.name.trim(),
        quantity:Number(r.quantity),unitPriceRial:Number(r.unitPrice),
        discountPerUnitRial:Number(r.discount),
        vatRatePercent:r.vatRate==="" ? null : Number(r.vatRate),
        officialIdentifierSnapshot:r.officialIdentifierSnapshot??null
      }))
    };
    setBusy(true);setError("");
    try {
      const saved = await getJson<SavedEntry>("/api/invoices/workspaces",{
        method:"POST", headers:{"Content-Type":"application/json"},
        body:JSON.stringify(payload)
      });
      setEditing(saved);
      setRows(current=>current.map((row,i)=>({...row,
        officialIdentifierSnapshot:saved.lines[i]?.officialIdentifierSnapshot??undefined})));
      setSavedSnapshot(draftSnapshot);
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
  function close() { if (importOpen) {setImportOpen(false);return;} if (!busy && (!unsavedChanges || window.confirm("تغییرات ذخیره‌نشده از بین می‌روند. پنجره بسته شود؟"))) setModal(false); }
  function addRow(item?: CatalogItem) {
    nextKey.current++;
    setRows(current => current.concat({
      key: nextKey.current, productId: item?.id ?? "",
      unitId:item?.baseUnitId,catalogCodeSnapshot:item?.internalCode,
      officialIdentifierSnapshot:item?.officialCode ?? undefined,
      name: item?.title ?? "",
      quantity:"1", unitPrice:"0", discount:"0", vatRate:"0"
    }));
  }
  function applyImportedRows(imported: ImportValues[]) {
    setRows(current => current.concat(imported.map(value => ({
      key:++nextKey.current,productId:"",name:value.description,
      quantity:value.quantity,unitPrice:value.unitPrice,
      discount:value.discount,vatRate:value.vatRate
    }))));
    setNotice(imported.length+" ردیف از پیش‌نمایش به فرم اضافه شد؛ ذخیره پیش‌نویس هنوز انجام نشده است.");
  }
  function updateRow(key: number, changes: Partial<Row>) {
    setRows(current => current.map(row => row.key === key ? {...row,...changes} : row));
  }
  const roundRial = (value:number) => Math.round((value + Number.EPSILON) * 100) / 100;
  const calculated = useMemo(() => {
    try { return invoicePreview(rows); }
    catch { return {base:0,discount:0,vat:0,total:0}; }
  }, [rows]);
  const otherTax = amount(otherTaxInput);
  const invoiceTotal = calculated.total + otherTax;
  const cashPreview = settlement === 1 ? invoiceTotal : settlement === 2 ? 0 :
    settlement === 3 ? roundRial(amount(cashInput)) : 0;
  const creditPreview = settlement === 2 ? invoiceTotal : settlement === 1 ? 0 :
    settlement === 3 ? roundRial(Math.max(0,invoiceTotal-cashPreview)) : 0;
  const settlementError = settlement === 3 &&
    (cashInput.trim() === "" || !Number.isFinite(Number(cashInput)) ||
      cashPreview <= 0 || cashPreview >= invoiceTotal);
  const draftSnapshot = JSON.stringify({
    headerCustomer,taxpayerId,memoryId,buyerId,date,hour,invoiceType,pattern,
    conditionalFields,rows,
    invoiceDescription,settlement,cashInput,otherTaxInput
  });
  useEffect(() => {
    if (modal && savedSnapshot === null) setSavedSnapshot(draftSnapshot);
  }, [modal, savedSnapshot, draftSnapshot]);
  const unsavedChanges = modal && savedSnapshot !== null &&
    draftSnapshot !== savedSnapshot;
  const validationDirty = editing !== null && (
    editing.taxpayerId!==taxpayerId || (()=>{const saved=tehranDateTime(new Date(editing.issueDateUtc));return saved.date!==date||saved.time!==hour;})() ||
    (editing.buyerPartyId??"")!==buyerId || (editing.sandboxFiscalMemoryId?"sandbox:"+editing.sandboxFiscalMemoryId:(editing.fiscalMemoryId??""))!==memoryId ||
    editing.type!==invoiceType || editing.pattern!==pattern ||
    (editing.invoiceDescription??"")!==invoiceDescription.trim() ||
    Number(editing.otherTaxAndLegalFeesRial??0)!==otherTax ||
    (editing.settlementMethod??0)!==settlement ||
    rows.length!==editing.lines.length ||
    rows.some((r,i)=>r.name.trim()!==editing.lines[i]?.description ||
      r.officialIdentifierSnapshot!==(editing.lines[i]?.officialIdentifierSnapshot??undefined) ||
      Number(r.quantity)!==editing.lines[i]?.quantity ||
      Number(r.unitPrice)!==editing.lines[i]?.unitPriceRial ||
      Number(r.discount)!==editing.lines[i]?.discountPerUnitRial ||
      (r.vatRate===""?null:Number(r.vatRate))!==editing.lines[i]?.vatRatePercent ||
      (r.productId||null)!==editing.lines[i]?.catalogItemId) ||
    settlement===3 && Number(cashInput)!==(editing.cashAmountRial??0)
  );
  const unsavedRef = useRef(false);
  unsavedRef.current = unsavedChanges;
  useEffect(()=>{
    if(!modal) return;
    const beforeUnload=(event:BeforeUnloadEvent)=>{
      if(!unsavedRef.current)return;
      event.preventDefault();
      event.returnValue="";
    };
    window.addEventListener("beforeunload",beforeUnload);
    return ()=>window.removeEventListener("beforeunload",beforeUnload);
  },[modal]);
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
          onClick={()=>setBulkOpen(true)}>ورود گروهی صورتحساب‌ها</button>
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
          <th>وضعیت داخلی</th><th>وضعیت سازمان</th><th>نسخه</th><th>عملیات</th></tr></thead>
        <tbody>{filtered.length === 0 ? <tr><td colSpan={isAdmin ? 10 : 9}>صورتحسابی برای نمایش وجود ندارد.</td></tr> :
          filtered.map(item => <tr key={item.id} style={{backgroundColor:item.authorityStatus==="SUCCESS"?"rgba(22,163,74,.10)":undefined}}>
            <td><button type="button" className="console-row-action" title="کپی شناسه برای پشتیبانی" disabled={!item.systemCode} onClick={() => {void navigator.clipboard.writeText(item.systemCode || "").then(()=>setNotice("شناسه سیستمی کپی شد."));}}><bdi dir="ltr">{item.systemCode || "در انتظار تخصیص"}</bdi> ⧉</button></td>
            {isAdmin && <td>{accounts.find(a=>a.customerUserId===item.customerUserId)?.companyName ||
              accounts.find(a=>a.customerUserId===item.customerUserId)?.personName || "حساب مشتری"}</td>}
            <td>{item.sellerName || taxpayers.find(t=>t.id===item.taxpayerId)?.displayName || "مؤدی ثبت‌شده"}</td>
            <td>{item.buyerName || "ثبت نشده"}</td>
            <td>{toFaDate(item.issueDateUtc)}</td><td>{({Original:"اصلی",Correction:"اصلاحی",Corrective:"اصلاحی",Cancellation:"ابطالی",Cancel:"ابطالی",ReturnFromSale:"برگشت از فروش",Return:"برگشت از فروش"} as Record<string,string>)[item.subject]??item.subject}</td>
            <td>{item.reviewState === "WorkingDraft" ? "در حال تکمیل" :
              item.reviewState === "InReview" ? "در انتظار بازبینی" :
              item.reviewState === "ChangesRequested" ? "نیازمند اصلاح" :
              item.reviewState === "Approved" ? "تأیید داخلی" : "پیش‌نویس داخلی"}</td>
            <td><span style={{color:item.authorityStatus==="SUCCESS"?"#15803d":
              item.authorityStatus==="FAILED"?"#b91c1c":undefined,fontWeight:600}}>
              {item.authorityStatus==="SUCCESS"?"تأیید شده در Sandbox":
               item.authorityStatus==="FAILED"?"رد شده توسط سازمان":
               item.authorityStatus==="PENDING"?"در انتظار بررسی":
               item.authorityStatus==="ACKNOWLEDGED"?"کد رهگیری دریافت شد":
               item.authorityStatus==="INDETERMINATE"?"نتیجه نامشخص":
               item.authorityStatus==="RESERVED"?"آماده ارسال":
               item.authorityStatus==="PREPARED"?"آماده‌سازی شده":
               item.authorityStatus==="SENDING"?"در حال ارسال":"ارسال نشده"}
            </span></td>
            <td>{item.revision}</td>
            <td>{item.kind === "workspace" ? <button type="button" className="console-row-action"
              onClick={() => void editDraft(item)}>ویرایش و ادامه</button> :
              <button type="button" className="console-row-action" onClick={()=>setDetail(item)}>مشاهده</button>}</td>
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
    {bulkOpen && <BulkInvoiceImportPanel onClose={()=>setBulkOpen(false)} onChanged={()=>void reload()}/>}
    {detail && <div className="invoice-register__overlay" onMouseDown={e => {
      if (e.currentTarget === e.target) {setDetail(null);void reload();}
    }}>
      <div className="invoice-register__detail-modal" role="dialog" aria-modal="true"
        aria-label="جزئیات صورتحساب">
        <div className="invoice-register__dialog-header">
          <h2>جزئیات صورتحساب</h2>
          <button type="button" className="identity-secondary" onClick={() => {setDetail(null);void reload();}}
            aria-label="بستن جزئیات">×</button>
        </div>
        <details name="invoice-detail-sections" className="fiscal-section invoice-detail-section" open key={"invoice-section:"+detail.id}><summary>صورتحساب فروش</summary>
        <div className="fiscal-section-content invoice-detail-section__content">
        <InvoiceFormalPreview key={"formal:"+detail.id} taxpayerId={detail.taxpayerId} invoiceId={detail.id} reference={detail.systemCode??""} authorityStatus={detail.authorityStatus}/>
        </div>
        </details>
        <details name="invoice-detail-sections" className="fiscal-section invoice-detail-section"><summary>مشخصات داخلی و فنی صورتحساب</summary>
        <div className="fiscal-section-content invoice-detail-section__content">
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
        </details>
        <details name="invoice-detail-sections" className="fiscal-section invoice-detail-section"><summary>{detail.authorityStatus==="SUCCESS"||detail.authorityStatus==="FAILED"||detail.authorityStatus==="PENDING"||detail.authorityStatus==="ACKNOWLEDGED"?"استعلام و نتیجه سازمان":"ارسال به سامانه مؤدیان"}</summary>
        <div className="fiscal-section-content invoice-detail-section__content">
        <InvoiceSendReadinessPanel key={"readiness:"+detail.id}
          invoiceId={detail.id} taxpayerId={detail.taxpayerId}
          isWorkspace={detail.kind === "workspace"} hideHistory={true} onCompleted={()=>void reload()}/>
        </div>
        </details>
        <details name="invoice-detail-sections" className="fiscal-section invoice-detail-section"><summary>تاریخچه ارسال و استعلام</summary>
          <div className="fiscal-section-content invoice-detail-section__content">
          <InvoiceDispatchHistory taxpayerId={detail.taxpayerId} invoiceId={detail.id}/>
          </div>
        </details>
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
                {(headerOptions?.sandboxMemories??[]).map(m=><option key={"sandbox:"+m.id} value={"sandbox:"+m.id}>
                  {m.memoryIdentifier} — Sandbox (آزمایشی)
                </option>)}
              </select>
              {memoryId.startsWith("sandbox:")&&<small className="invoice-register__hint">
                حافظه مالیاتی آزمایشی انتخاب شده است؛ این پیش‌نویس فقط برای مسیر Sandbox در نظر گرفته می‌شود.
                انتخاب Sandbox به معنای آماده‌بودن ارسال صورتحساب نیست.
              </small>}
              {headerLoading && <small role="status">در حال دریافت اطلاعات مؤدی و حافظه مالیاتی…</small>}
              {headerError && <small role="alert" className="form-error">{headerError}</small>}
              {headerOptions && <small className="invoice-register__hint">
                شناسه ثبت‌شده مؤدی: <bdi dir="ltr">{headerOptions.taxpayer.normalizedIdentifier}</bdi>
                {headerOptions.memories.length===0 && !headerOptions.sandboxMemories?.length ? " — حافظه مالیاتی ثبت نشده است." : ""}
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
                    <strong>{b.displayName}</strong><small>{b.kind===2?"شخص حقوقی":"شخص حقیقی"} · <span dir="ltr">{b.identifier}</span></small>
                  </button>)}
                </div>}
                {buyerId && <small className="invoice-register__selected-buyer">
                  خریدار انتخاب‌شده: {chosenBuyer?.displayName || buyerQuery}
                  {chosenBuyer ? ` — ${chosenBuyer.kind===2?"حقوقی":"حقیقی"}` : ""}</small>}
              </div>
              <label htmlFor="invoice-date">تاریخ صدور شمسی</label>
              <PersianDatePicker id="invoice-date" value={date} onChange={setDate} floating/>
              <label htmlFor="invoice-hour">ساعت صدور (تهران)</label>
              <PersianTimePicker id="invoice-hour" value={hour} onChange={setHour} timeZoneLabel="تهران" />
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
                  <option key={p.pattern} value={p.pattern!}>{PATTERN_LABELS[p.pattern!] ?? ("الگوی "+p.pattern)}</option>)}
              </select>
              {(formMetadata?.entries.find(p => p.type === invoiceType && p.pattern === pattern)?.requiredFields ?? [])
                .flatMap(field => field === "goldFields" ? GOLD_FIELDS : [field])
                .map(field => <div key={field} className="invoice-register__conditional-field">
                <label htmlFor={"invoice-conditional-"+field}>{FIELD_LABELS[field]}</label>
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
              <div className="invoice-register__row-tools" style={{display:"flex",alignItems:"center",gap:8,flexWrap:"wrap",marginBottom:8}}>
                <label htmlFor="invoice-kind">نوع قلم</label>
                <select id="invoice-kind" value={kind} onChange={e=>setKind(e.target.value as "products"|"services")} style={{maxWidth:125}}>
                  <option value="products">کالا</option><option value="services">خدمت</option>
                </select>
                <button type="button" className="identity-secondary" onClick={()=>setImportOpen(true)} disabled={busy}>
                  <span aria-hidden="true">↥ </span>ورود از فایل
                </button>
                <button type="button" className="identity-secondary" onClick={()=>addRow()} disabled={busy}>+ ردیف جدید</button>
                <strong style={{marginInlineStart:"auto"}}>اقلام ({rows.length})</strong>
              </div>
              <div className="invoice-register__catalog-picker" style={{marginBottom:8}}>
                <label htmlFor="invoice-catalog-search">انتخاب کالا / خدمت</label>
                <SearchableCatalogPicker items={catalog} value={catalogQuery}
                  onQueryChange={setCatalogQuery} loading={catalogBusy} onChoose={addRow}/>
                <small className="field-hint">شناسه ۱۳ رقمی (sstid) را از اطلاعات کالا وارد کنید. نرم‌افزار قالب ۱۳ رقمی را کنترل می‌کند؛ اعتبار شناسه و نرخ را سامانه مالیاتی بررسی می‌کند.</small>
              </div>
              {importOpen && <InvoiceImportPanel disabled={busy} onImport={applyImportedRows}
                onClose={()=>setImportOpen(false)}/>}
              <div className="console-table-scroll" style={{maxHeight:"min(48vh,440px)",overflow:"auto",border:"1px solid var(--border,#d3dce0)",borderRadius:8}}>
                <table className="console-table" style={{width:"100%",minWidth:770,tableLayout:"fixed",borderCollapse:"separate",borderSpacing:0}}>
                  <thead style={{position:"sticky",top:0,zIndex:1,background:"var(--surface,#fff)"}}>
                    <tr><th style={{width:44}}>ردیف</th><th style={{width:"28%"}}>شرح کالا یا خدمت</th>
                    <th>مقدار</th><th>فی واحد (ریال)</th><th>تخفیف واحد</th><th>VAT (%)</th><th style={{width:56}}>عملیات</th></tr>
                  </thead>
                  <tbody>{rows.length===0?<tr><td colSpan={7}>هنوز ردیفی اضافه نشده است.</td></tr>:
                    rows.map((row,index)=><tr key={row.key}>
                      <td>{index+1}</td>
                      <td className="invoice-register__description-cell"><input aria-label={"شرح ردیف "+(index+1)} title={row.productId?("کد داخلی: "+(row.catalogCodeSnapshot||"—")):""}
                        style={{width:"100%",minWidth:0}} value={row.name} maxLength={256}
                        onChange={e=>updateRow(row.key,{name:e.target.value})}/>
                        <input aria-label={"شناسه رسمی ۱۳ رقمی ردیف "+(index+1)}
                          placeholder="شناسه رسمی ۱۳ رقمی" inputMode="numeric" maxLength={13}
                          dir="ltr" style={{width:"100%",minWidth:0,marginTop:4}}
                          value={row.officialIdentifierSnapshot??""}
                          onChange={e=>updateRow(row.key,{officialIdentifierSnapshot:e.target.value.replace(/\D/g,"").slice(0,13)})}/>
                      </td>
                      <td><input aria-label={"مقدار ردیف "+(index+1)} style={{width:"100%",minWidth:0}} dir="ltr" type="number" min="0.00000001" step="any"
                        value={row.quantity} onChange={e=>updateRow(row.key,{quantity:e.target.value})}/></td>
                      <td><input aria-label={"قیمت واحد ردیف "+(index+1)} style={{width:"100%",minWidth:0}} dir="ltr" type="number" min="0" step="any"
                        value={row.unitPrice} onChange={e=>updateRow(row.key,{unitPrice:e.target.value})}/></td>
                      <td><input aria-label={"تخفیف ردیف "+(index+1)} style={{width:"100%",minWidth:0}} dir="ltr" type="number" min="0" step="any"
                        value={row.discount} onChange={e=>updateRow(row.key,{discount:e.target.value})}/></td>
                      <td><input aria-label={"VAT ردیف "+(index+1)} style={{width:"100%",minWidth:0}} dir="ltr" type="number" min="0" step="any"
                        value={row.vatRate} onChange={e=>updateRow(row.key,{vatRate:e.target.value})}/></td>
                      <td><button type="button" aria-label={"حذف ردیف "+(index+1)} title="حذف ردیف"
                        className="console-row-action" style={{padding:"4px 7px"}} disabled={busy}
                        onClick={()=>setRows(current=>current.filter(x=>x.key!==row.key))}>×</button></td>
                    </tr>)}</tbody>
                </table>
              </div>
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
                    <dt>تاریخ و زمان</dt><dd>{new Intl.DateTimeFormat("fa-IR-u-ca-persian",{timeZone:"Asia/Tehran",year:"numeric",month:"2-digit",day:"2-digit",hour:"2-digit",minute:"2-digit",hourCycle:"h23"}).format(tehranLocalToUtc(date,hour))}</dd>
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
                    <dt>سایر مالیات و وجوه قانونی (ریال)</dt>
                    <dd><input id="invoice-other-tax" type="number" min="0" step="1" dir="ltr"
                      value={otherTaxInput} onChange={e=>setOtherTaxInput(e.target.value)} /></dd>
                    <dt className="invoice-register__grand">جمع صورتحساب</dt>
                    <dd className="invoice-register__grand">{fmt(invoiceTotal)} ریال</dd>
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
              {editing && <InvoiceHistoryPanel workspaceId={editing.id}/>}
              {editing && <InvoiceValidationPanel
                draftId={editing.id} taxpayerId={editing.taxpayerId}
                revision={editing.revision} dirty={validationDirty}
                onSave={()=>saveWorkspace(step===2 && settlement===0 ? 1 : step,false,false,true)}
                onDraftChanged={async()=>{
                  const refreshed=await getJson<SavedEntry>("/api/invoices/workspaces/"+editing.id);
                  const refreshedTehran=tehranDateTime(new Date(refreshed.issueDateUtc));
                  setEditing(refreshed);setDate(refreshedTehran.date);setHour(refreshedTehran.time);
                  setSavedSnapshot(null);
                }}
                onPrepared={()=>{setModal(false);setNotice("نسخه ثابت داخلی ثبت شد؛ ارسال مالیاتی هنوز غیرفعال است.");void reload();}}/>}
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
