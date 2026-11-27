"use client";

import { useEffect, useState } from "react";

type Finding = {
  key: string;
  severity: "error" | "warning";
  fieldPath: string;
  message: string;
};

export type InvoiceDraftEvaluation = {
  draftId: string;
  revision: number;
  edition: string;
  isSubmissionAuthorized: boolean;
  findings: Finding[];
  formHasBlockingErrors?: boolean;
  selectedEnvironment?: "Sandbox" | "Production" | "Unselected";
};

type PanelProps = {
  draftId: string;
  revision: number;
  dirty: boolean;
  onSave: () => Promise<boolean>;
  onPrepared?: (invoiceId: string) => void;
  onDraftChanged?: () => Promise<void>;
};

export async function readInvoiceDraftValidation(draftId: string): Promise<InvoiceDraftEvaluation> {
  const response = await fetch("/api/invoices/workspaces/" + encodeURIComponent(draftId) + "/validation", {
    cache: "no-store",
    credentials: "same-origin",
    signal: AbortSignal.timeout(20000),
  });

  if (!response.ok) {
    const message = response.status === 401
      ? "نشست کاربری منقضی شده است."
      : response.status === 404
        ? "پیش‌نویس یا مرجع در محدوده دسترسی شما یافت نشد."
        : "دریافت وضعیت اعتبارسنجی ممکن نشد.";
    throw new Error(message);
  }

  return response.json() as Promise<InvoiceDraftEvaluation>;
}

export function blockingInvoiceDraftFindings(report: InvoiceDraftEvaluation) {
  return report.findings.filter(finding => finding.severity === "error" ||
    (finding.severity === "warning" && finding.key !== "invoice.rules.not_approved"));
}

export class InvoiceDraftPreparationError extends Error {
  constructor(readonly code: string, message: string) { super(message); }
}

export async function prepareInvoiceDraft(draftId: string, revision: number): Promise<string> {
  const response = await fetch("/api/invoices/workspaces/"+encodeURIComponent(draftId)+"/prepare",{
    method:"POST",credentials:"same-origin",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({expectedRevision:revision}),signal:AbortSignal.timeout(20000)
  });
  if (!response.ok) {
    const failure = await response.json().catch(() => null) as {error?: unknown} | null;
    const errorCode = typeof failure?.error === "string" ? failure.error : "";
    const messages: Record<string,string> = {
      issue_date_adjusted: "زمان صدور در پیش‌نویس به‌روز شد. تاریخ و ساعت را دوباره بررسی کنید.",
      official_item_identifier_invalid: "کالا، شناسه رسمی یا واحد اندازه‌گیری یکی از ردیف‌ها معتبر نیست.",
      fiscal_memory_not_owned_active_or_classified: "حافظهٔ مالیاتی انتخاب‌شده برای این مؤدی فعال یا معتبر نیست.",
      sandbox_memory_not_ready: "حافظهٔ آزمایشی انتخاب‌شده آمادهٔ ارسال نیست.",
      buyer_not_owned: "خریدار صورتحساب را دوباره از فهرست خریداران این مشتری انتخاب کنید.",
      invoice_type2_fields_outside_pattern: "برای صورتحساب نوع دوم با الگوی فروش، اطلاعات خریدار و تسویه را دوباره بررسی کنید.",
      invoice_financial_calculation_invalid: "مقادیر اقلام و جمع نهایی صورتحساب را بررسی کنید.",
      invoice_submission_window_elapsed: "مهلت ۱۲ روزهٔ ارسال گذشته است. تاریخ و ساعت واقعی صدور را بررسی کنید.",
      invoice_snapshot_draft_invalid: "اطلاعات پیش‌نویس با مؤدی انتخاب‌شده هم‌خوانی ندارد؛ فاکتور را دوباره باز و ذخیره کنید.",
      invoice_buyer_required: "خریدار صورتحساب انتخاب نشده است.",
      invoice_lines_required: "حداقل یک ردیف کالا یا خدمت لازم است.",
      invoice_memory_selection_invalid: "یک حافظه مالیاتی معتبر برای همین محیط انتخاب کنید.",
      invoice_pattern_not_supported_for_submission: "این ترکیب نوع و الگوی صورتحساب برای ارسال پشتیبانی نمی‌شود.",
      invoice_contract_number_required: "برای الگوی پیمانکاری، شماره قرارداد را تکمیل کنید.",
      invoice_line_snapshot_incomplete: "برای همه ردیف‌ها کالا، شناسه رسمی ۱۳ رقمی، واحد اندازه‌گیری، مقدار و نرخ مالیات را تکمیل و پیش‌نویس را ذخیره کنید.",
      invoice_settlement_required: "روش و مبلغ تسویه را تکمیل و پیش‌نویس را دوباره ذخیره کنید.",
      invoice_settlement_mismatch: "مبالغ نقدی و نسیه با جمع صورتحساب تطابق ندارند؛ روش تسویه را دوباره انتخاب و ذخیره کنید."
    };
    if (messages[errorCode]) throw new InvoiceDraftPreparationError(errorCode, messages[errorCode]);
    throw new InvoiceDraftPreparationError(errorCode, response.status===409 ?
      "این نسخه قبلاً ثابت شده یا تغییر کرده است." + (errorCode ? ` (${errorCode})` : "") :
      "ثبت نسخه ثابت داخلی رد شد." + (errorCode ? `؛ علت: ${errorCode}` : ""));
  }
  const result = await response.json();
  if(result.invoiceId!==draftId || result.internalSnapshotCreated!==true || result.fiscalSubmissionAuthorized!==false ||
    result.signedPacketAvailable!==false || result.environmentSubmissionEnabled!==false)
    throw new Error("پاسخ آماده‌سازی از نظر ایمنی معتبر نیست.");
  return result.invoiceId;
}

function environmentLabel(environment: InvoiceDraftEvaluation["selectedEnvironment"]): string {
  if (environment === "Sandbox") return "Sandbox (آزمایشی)";
  if (environment === "Production") return "Production (واقعی)";
  return "انتخاب نشده";
}

function FindingSummary({ report, findings }: { report: InvoiceDraftEvaluation; findings: Finding[] }) {
  const errorCount = findings.filter(finding => finding.severity === "error").length;
  const warningCount = findings.filter(finding => finding.severity === "warning").length;

  return (
    <div className="invoice-validation__summary">
      <strong>ویرایش {report.revision} · قواعد {report.edition}</strong>
      <small>حافظه انتخابی: {environmentLabel(report.selectedEnvironment)}</small>
      <span>{errorCount} خطا · {warningCount} هشدار</span>
    </div>
  );
}

export function FindingList({ findings }: { findings: Finding[] }) {
  if (findings.length === 0) {
    return <p>در این پیش‌بررسی خطایی پیدا نشد؛ تأیید رسمی نیست.</p>;
  }

  return (
    <ul>
      {findings.map((finding, index) => (
        <li
          key={`${finding.key}-${index}`}
          className={`invoice-validation__${finding.severity}`}
        >
          <strong>{finding.severity === "error" ? "خطا" : "هشدار"}</strong>
          <span>{finding.message}</span>
          <code dir="ltr">{finding.fieldPath}</code>
        </li>
      ))}
    </ul>
  );
}

function ValidationWarning({ report }: { report: InvoiceDraftEvaluation }) {
  const heading = report.formHasBlockingErrors
    ? "فرم دارای خطای تکمیل است"
    : "فرم برای پیش‌بررسی داخلی بررسی شد؛ هنوز قطعی و قابل ارسال نیست";

  return (
    <p className="invoice-validation__warning">
      <strong>{heading}.</strong>
      <span>
        {" حتی اگر خطای فرم صفر باشد، صدور قطعی مستلزم شناسه رسمی ۱۳ رقمی تمام اقلام، "}
        {"حافظه معتبر در محیط انتخابی، تولید شناسه یکتای مالیاتی صورتحساب، "}
        {"اعتبارسنجی قواعد معتبر و آماده‌سازی بسته امضاشده است. این پیش‌بررسی به‌تنهایی "}
        {"صورتحساب را ارسال نمی‌کند؛ پس از ثبت نسخه ثابت، دکمه ارسال در جزئیات صورتحساب نمایش داده می‌شود."}
      </span>
    </p>
  );
}

function ValidationReport({ report }: { report: InvoiceDraftEvaluation }) {
  const findings = report.findings ?? [];

  return (
    <div className="invoice-validation__findings">
      <FindingSummary report={report} findings={findings} />
      <FindingList findings={findings} />
      <ValidationWarning report={report} />
    </div>
  );
}

export default function InvoiceValidationPanel({
  draftId,
  revision,
  dirty,
  onSave,
  onPrepared,
  onDraftChanged,
}: PanelProps) {
  const [report, setReport] = useState<InvoiceDraftEvaluation | null>(null);
  const [loading, setLoading] = useState(false);
  const [checkingAfterSave, setCheckingAfterSave] = useState(false);
  const [error, setError] = useState("");
  const [preparing, setPreparing] = useState(false);

  async function refresh() {
    if (dirty) {
      setError("ابتدا تغییرات فرم را ذخیره کنید.");
      return;
    }

    setLoading(true);
    setError("");

    try {
      const checks = await readInvoiceDraftValidation(draftId);

      // A report from another draft revision is stale and cannot be shown.
      if (checks.revision !== revision) { // revision!==revision
        setReport(null);
        setError("نسخه پیش‌نویس تغییر کرده است؛ آن را مجدداً باز کنید.");
        return;
      }

      setReport(checks);
    } catch (cause) {
      setError((cause as Error).message);
    } finally {
      setLoading(false);
    }
  }

  function reviewDraft() {
    if (!dirty) {
      void refresh();
      return;
    }

    setCheckingAfterSave(true);
    void onSave().then(saved => {
        // if(!saved), keep the review result closed and show the form error.
        if (!saved) {
          setCheckingAfterSave(false);
          setError("ذخیره تغییرات انجام نشد؛ خطای نمایش‌داده‌شده در فرم را بررسی کنید.");
        }
      })
      .catch(cause => {
        setCheckingAfterSave(false);
        setError((cause as Error).message);
    });
  }

  useEffect(() => {
    setReport(null);
  }, [draftId, revision]);

  useEffect(() => {
    if (checkingAfterSave && !dirty) {
      setCheckingAfterSave(false);
      void refresh();
    }
  }, [revision, dirty, checkingAfterSave]);


  async function prepareInternalSnapshot() {
    if (!report || dirty || preparing || !onPrepared || report.revision !== revision ||
        !["Sandbox", "Production"].includes(report.selectedEnvironment ?? "Unselected") ||
        blockingInvoiceDraftFindings(report).length > 0) return;
    setPreparing(true);setError("");
    try {
      const invoiceId = await prepareInvoiceDraft(draftId, revision);
      onPrepared(invoiceId);
    } catch (cause) {
      if (cause instanceof InvoiceDraftPreparationError && cause.code === "issue_date_adjusted") {
        try { await onDraftChanged?.(); }
        catch { setError("پیش‌نویس تغییر کرده است؛ آن را دوباره باز کنید."); return; }
      }
      setError(cause instanceof Error ? cause.message : "آماده‌سازی انجام نشد.");
    }
    finally{setPreparing(false);}
  }

  const busy = loading || checkingAfterSave;
  const buttonLabel = busy ? "در حال بررسی نهایی…" : "بررسی نهایی";

  return (
    <section className="invoice-validation" aria-labelledby="invoice-validation-title" aria-busy={busy || preparing}>
      <div className="invoice-validation__heading">
        <div>
          <h3 id="invoice-validation-title">پیش‌بررسی و سوابق ارجاع</h3>
          <p>پس از تکمیل اطلاعات، این دکمه را بزنید تا تغییرات ذخیره و بررسی نهایی انجام شود.</p>
        </div>
      </div>
      <div className="invoice-validation__action" style={{display:"flex",justifyContent:"center",width:"100%"}}>
        <button
          type="button"
          className="identity-primary"
          disabled={busy || preparing}
          onClick={reviewDraft}
        >
          {buttonLabel}
        </button>
      </div>

      {(busy || preparing) && <p className="invoice-entry-loading" role="status" aria-live="polite">
        <span className="argin-loader-spinner" aria-hidden="true"/>
        {preparing ? "در حال ثبت نسخه ثابت…" : checkingAfterSave ? "در حال ذخیره و بررسی پیش‌نویس…" : "در حال بررسی پیش‌نویس…"}
      </p>}
      {dirty && (
        <p role="status" className="invoice-validation__warning">
          فرم از آخرین نسخه ذخیره‌شده تغییر کرده است؛ ابتدا ذخیره کنید.
        </p>
      )}
      {error && <p role="alert" className="form-error">{error}</p>}
      {report && <ValidationReport report={report} />}
      {(report?.selectedEnvironment === "Sandbox" || report?.selectedEnvironment === "Production") && (
        <div className="invoice-validation__preparation">
          <button type="button" className="invoice-validation__prepare-button"
            disabled={dirty || busy || preparing || !onPrepared ||
              report.revision!==revision || blockingInvoiceDraftFindings(report).length > 0}
            onClick={()=>void prepareInternalSnapshot()}>
            {preparing?"در حال ثبت نسخه ثابت…":report.selectedEnvironment === "Production"
              ? "ثبت نسخه ثابت و ادامه به ارسال Production"
              : "ثبت نسخه ثابت داخلی برای آماده‌سازی Sandbox"}
          </button>
          <p className="invoice-validation__warning">نسخه ثابت داخلی، ارسال مالیاتی نیست؛
          شناسه مالیاتی و بسته امضاشده هنوز ایجاد نشده‌اند.</p>
        </div>
      )}


    </section>
  );
}
