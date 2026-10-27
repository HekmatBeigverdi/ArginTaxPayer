"use client";

import { useEffect, useState } from "react";

type Finding = {
  key: string;
  severity: "error" | "warning";
  fieldPath: string;
  message: string;
};

type Evaluation = {
  draftId: string;
  revision: number;
  edition: string;
  isSubmissionAuthorized: boolean;
  findings: Finding[];
  formHasBlockingErrors?: boolean;
  selectedEnvironment?: "Sandbox" | "Production" | "Unselected";
};

type Reference = {
  id: string;
  subject: string;
  state: string;
  issueDateUtc: string;
  revision: number;
  fiscalIdentifierVerified: boolean;
  fiscalIdentifier: string | null;
};

type Subject = "Original" | "Correction" | "Cancellation" | "SalesReturn";

type PanelProps = {
  draftId: string;
  taxpayerId: string;
  revision: number;
  dirty: boolean;
  onSave: () => Promise<boolean>;
  onPrepared?: () => void;
  onDraftChanged?: () => Promise<void>;
};

const SUBJECT_OPTIONS: ReadonlyArray<{ value: Subject; label: string }> = [
  { value: "Original", label: "اصلی" },
  { value: "Correction", label: "اصلاحی" },
  { value: "Cancellation", label: "ابطالی" },
  { value: "SalesReturn", label: "برگشت از فروش" },
];

async function readJson<T>(url: string): Promise<T> {
  const response = await fetch(url, {
    cache: "no-store",
    credentials: "same-origin",
  });

  if (!response.ok) {
    const message = response.status === 401
      ? "نشست کاربری منقضی شده است."
      : response.status === 404
        ? "پیش‌نویس یا مرجع در محدوده دسترسی شما یافت نشد."
        : "دریافت وضعیت اعتبارسنجی ممکن نشد.";
    throw new Error(message);
  }

  return response.json() as Promise<T>;
}

function environmentLabel(environment: Evaluation["selectedEnvironment"]): string {
  if (environment === "Sandbox") return "Sandbox (آزمایشی)";
  if (environment === "Production") return "Production (واقعی)";
  return "انتخاب نشده";
}

function FindingSummary({ report, findings }: { report: Evaluation; findings: Finding[] }) {
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

function FindingList({ findings }: { findings: Finding[] }) {
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

function ValidationWarning({ report }: { report: Evaluation }) {
  const heading = report.formHasBlockingErrors
    ? "فرم دارای خطای تکمیل است"
    : "فرم برای پیش‌بررسی داخلی بررسی شد؛ هنوز قطعی و قابل ارسال نیست";

  return (
    <p className="invoice-validation__warning">
      <strong>{heading}.</strong>
      <span>
        {" حتی اگر خطای فرم صفر باشد، صدور قطعی مستلزم شناسه رسمی ۱۳ رقمی تمام اقلام، "}
        {"حافظه معتبر در محیط انتخابی، تولید شناسه یکتای مالیاتی صورتحساب، "}
        {"اعتبارسنجی قواعد معتبر و آماده‌سازی بسته امضاشده است. امکان ارسال Sandbox با "}
        {"تست اتصال حافظه به‌تنهایی ایجاد نمی‌شود."}
      </span>
    </p>
  );
}

function ValidationReport({ report }: { report: Evaluation }) {
  const findings = report.findings ?? [];

  return (
    <div className="invoice-validation__findings">
      <FindingSummary report={report} findings={findings} />
      <FindingList findings={findings} />
      <ValidationWarning report={report} />
    </div>
  );
}

function ReferenceHistory({ references }: { references: Reference[] }) {
  if (references.length === 0) return null;

  return (
    <div className="invoice-validation__history">
      <strong>سوابق داخلی همین مؤدی ({references.length} رکورد)</strong>
      <ul>
        {references.map(reference => (
          <li key={reference.id}>
            <bdi dir="ltr">{reference.id.slice(0, 8)}…</bdi>
            <span>
              {reference.subject} · {reference.state} · نسخه {reference.revision}
            </span>
            <small>شناسه مالیاتی رسمی: تأیید نشده</small>
          </li>
        ))}
      </ul>
    </div>
  );
}

function ReferenceSection({
  subject,
  references,
  selectedReference,
  onSubjectChange,
  onReferenceChange,
}: {
  subject: Subject;
  references: Reference[];
  selectedReference: string;
  onSubjectChange: (value: Subject) => void;
  onReferenceChange: (value: string) => void;
}) {
  const hasReferenceSubject = subject !== "Original";

  return (
    <div className="invoice-validation__reference">
      <label htmlFor="invoice-reference-subject">
        موضوع صورتحساب برای بررسی ارجاع
      </label>
      <select
        id="invoice-reference-subject"
        value={subject}
        onChange={event => onSubjectChange(event.target.value as Subject)}
      >
        {SUBJECT_OPTIONS.map(option => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>

      {hasReferenceSubject && (
        <>
          <label htmlFor="invoice-reference-candidate">
            صورتحساب مرتبط (فقط سابقه داخلی)
          </label>
          <select
            id="invoice-reference-candidate"
            value={selectedReference}
            onChange={event => onReferenceChange(event.target.value)}
          >
            <option value="">مرجع تأییدشده‌ای در دسترس نیست</option>
            {references.map(reference => (
              <option key={reference.id} value={reference.id} disabled>
                {reference.id.slice(0, 8)} · {reference.subject} · {reference.state}
              </option>
            ))}
          </select>
          <p className="invoice-validation__warning">
            مرجع فقط با شناسه مالیاتی معتبر، سابقه ثبت رسمی و بررسی زنجیره قابل انتخاب است.
            رکوردهای محلی فعلی چنین شواهدی ندارند و عمداً غیرفعال‌اند.
          </p>
          <ReferenceHistory references={references} />
        </>
      )}
    </div>
  );
}

export default function InvoiceValidationPanel({
  draftId,
  taxpayerId,
  revision,
  dirty,
  onSave,
  onPrepared,
  onDraftChanged,
}: PanelProps) {
  const [report, setReport] = useState<Evaluation | null>(null);
  const [references, setReferences] = useState<Reference[]>([]);
  const [subject, setSubject] = useState<Subject>("Original");
  const [selectedReference, setSelectedReference] = useState("");
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
      const [checks, source] = await Promise.all([
        readJson<Evaluation>(`/api/invoices/workspaces/${draftId}/validation`),
        readJson<Reference[]>(`/api/invoices/references/${taxpayerId}`),
      ]);

      // A report from another draft revision is stale and cannot be shown.
      if (checks.revision !== revision) { // revision!==revision
        setReport(null);
        setError("نسخه پیش‌نویس تغییر کرده است؛ آن را مجدداً باز کنید.");
        return;
      }

      setReport(checks);
      setReferences(source);
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

  function selectSubject(value: Subject) {
    setSubject(value);
    setSelectedReference("");
  }

  useEffect(() => {
    setReport(null);
    setReferences([]);
    setSubject("Original");
    setSelectedReference("");
  }, [draftId, revision, taxpayerId]);

  useEffect(() => {
    if (checkingAfterSave && !dirty) {
      setCheckingAfterSave(false);
      void refresh();
    }
  }, [revision, dirty, checkingAfterSave]);


  async function prepareInternalSnapshot() {
    if (!report || dirty || preparing || !onPrepared || report.revision !== revision ||
        report.selectedEnvironment !== "Sandbox" ||
        report.findings.some(f => f.severity === "error" ||
          (f.severity === "warning" && f.key !== "invoice.rules.not_approved"))) return;
    setPreparing(true);setError("");
    try {
      const response = await fetch("/api/invoices/workspaces/"+draftId+"/prepare",{
        method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json"},
        body:JSON.stringify({expectedRevision:revision}),signal:AbortSignal.timeout(20000)
      });
      if (!response.ok) {
        const failure = await response.json().catch(() => null) as {error?: unknown} | null;
        const errorCode = typeof failure?.error === "string" ? failure.error : "";
        if(errorCode==="issue_date_adjusted") {
          await onDraftChanged?.();
          setError("طبق جدول ۴ مستند V7.9، زمان آینده در پیش‌نویس اصلاح با زمان فعلی UTC جایگزین شد. نسخه تازه را دوباره بررسی کنید و سپس ثبت نسخه ثابت را تکرار کنید.");
          return;
        }
        throw new Error(response.status===409 ?
          "این نسخه قبلاً ثابت شده یا تغییر کرده است." + (errorCode ? ` (${errorCode})` : "") :
          "ثبت نسخه ثابت داخلی رد شد." + (errorCode ? `؛ علت: ${errorCode}` : ""));
      }
      const result = await response.json();
      if(result.internalSnapshotCreated!==true || result.fiscalSubmissionAuthorized!==false ||
        result.signedPacketAvailable!==false || result.environmentSubmissionEnabled!==false)
        throw new Error("پاسخ آماده‌سازی از نظر ایمنی معتبر نیست.");
      onPrepared();
    }catch(e){setError(e instanceof Error?e.message:"آماده‌سازی انجام نشد.");}
    finally{setPreparing(false);}
  }

  const busy = loading || checkingAfterSave;
  const buttonLabel = busy
    ? "در حال ذخیره/بررسی…"
    : dirty
      ? "ذخیره تغییرات و بررسی"
      : "بررسی پیش‌نویس ذخیره‌شده";

  return (
    <section className="invoice-validation" aria-labelledby="invoice-validation-title">
      <div className="invoice-validation__heading">
        <div>
          <h3 id="invoice-validation-title">پیش‌بررسی و سوابق ارجاع</h3>
          <p>این بررسی برای تکمیل فرم است؛ مجوز ارسال مالیاتی صادر نمی‌کند.</p>
        </div>
        <button
          type="button"
          className="identity-secondary"
          disabled={busy}
          onClick={reviewDraft}
        >
          {buttonLabel}
        </button>
      </div>

      {dirty && (
        <p role="status" className="invoice-validation__warning">
          فرم از آخرین نسخه ذخیره‌شده تغییر کرده است؛ ابتدا ذخیره کنید.
        </p>
      )}
      {error && <p role="alert" className="form-error">{error}</p>}
      {report && <ValidationReport report={report} />}
      {report?.selectedEnvironment === "Sandbox" && (
        <div className="invoice-validation__preparation">
          <button type="button" className="identity-secondary"
            disabled={dirty || busy || preparing || !onPrepared ||
              report.revision!==revision || report.findings.some(f=>
                f.severity==="error"||(f.severity==="warning"&&f.key!=="invoice.rules.not_approved"))}
            onClick={()=>void prepareInternalSnapshot()}>
            {preparing?"در حال ثبت نسخه ثابت…":"ثبت نسخه ثابت داخلی برای آماده‌سازی Sandbox"}
          </button>
          <p className="invoice-validation__warning">نسخه ثابت داخلی، ارسال مالیاتی نیست؛
          شناسه مالیاتی و بسته امضاشده هنوز ایجاد نشده‌اند.</p>
        </div>
      )}


      <ReferenceSection
        subject={subject}
        references={references}
        selectedReference={selectedReference}
        onSubjectChange={selectSubject}
        onReferenceChange={setSelectedReference}
      />
    </section>
  );
}
