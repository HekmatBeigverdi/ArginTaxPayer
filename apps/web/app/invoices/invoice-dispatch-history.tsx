"use client";

import { useCallback, useEffect, useState } from "react";

type Attempt = {
  number: number;
  startedAtUtc: string;
  endedAtUtc: string | null;
  result: string;
};

type Receipt = {
  kind: "InitialReceipt" | "InquiryEvidence";
  observedAtUtc: string;
  hasAuthorityReference: boolean;
};

type History = {
  environment: "Sandbox";
  state: string;
  invoiceRevision: number | null;
  lastUpdatedAtUtc: string | null;
  attempts: Attempt[];
  receipts: Receipt[];
  recoveryAction: string;
  recoveryCode: string;
  mayRetry: false;
  mayInquire: false;
};

const statusNames: Record<string, string> = {
  NotPrepared: "آماده‌سازی نشده",
  Prepared: "آماده‌شده",
  Reserved: "آماده ارسال",
  Sending: "در حال ارسال",
  Acknowledged: "شماره رهگیری دریافت شده",
  Indeterminate: "نتیجه نامشخص",
  RetryEligible: "نیازمند بررسی ارسال مجدد",
  FailedTerminal: "خطای نهایی",
  Signed: "امضاشده",
};

const formatDate = (value: string | null) =>
  value ? new Date(value).toLocaleString("fa-IR") : "—";

const historyUrl = (taxpayerId: string, invoiceId: string) =>
  "/api/invoice-dispatch/" +
  encodeURIComponent(taxpayerId) +
  "/" +
  encodeURIComponent(invoiceId) +
  "/history";

function isHistoryPayload(value: unknown): value is History {
  return Boolean(
    value &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      (value as History).environment === "Sandbox",
  );
}

function AttemptsTable({ attempts }: { attempts: Attempt[] }) {
  if (attempts.length === 0) return <p>تلاشی ثبت نشده است.</p>;

  return (
    <div className="table-scroll" style={{ maxHeight:230, overflow: "auto" }}>
      <table className="console-table">
        <thead>
          <tr>
            <th>شماره</th>
            <th>شروع</th>
            <th>پایان</th>
            <th>نتیجه</th>
          </tr>
        </thead>
        <tbody>
          {attempts.map((attempt, index) => (
            <tr key={`${attempt.number}:${index}`}>
              <td>{attempt.number}</td>
              <td>{formatDate(attempt.startedAtUtc)}</td>
              <td>{formatDate(attempt.endedAtUtc)}</td>
              <td>
                <bdi dir="ltr">{attempt.result}</bdi>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReceiptsTable({ receipts }: { receipts: Receipt[] }) {
  if (receipts.length === 0) return <p>رسیدی ثبت نشده است.</p>;

  return (
    <div className="table-scroll">
      <table className="console-table">
        <thead>
          <tr>
            <th>نوع</th>
            <th>زمان</th>
            <th>مرجع سازمان</th>
          </tr>
        </thead>
        <tbody>
          {receipts.map((receipt, index) => (
            <tr key={`${receipt.observedAtUtc}:${index}`}>
              <td>
                {receipt.kind === "InitialReceipt"
                  ? "پاسخ اولیه ارسال"
                  : "شاهد استعلام"}
              </td>
              <td>{formatDate(receipt.observedAtUtc)}</td>
              <td>
                {receipt.hasAuthorityReference ? "ثبت شده" : "ثبت نشده"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function HistoryContent({ history }: { history: History }) {
  return (
    <>
      <p style={{ margin: "7px 0" }}>
        وضعیت: <strong>{statusNames[history.state] ?? "نیازمند بررسی"}</strong>
        {" — "}نسخه: {history.invoiceRevision ?? "—"}
        {" — "}آخرین تغییر: <time>{formatDate(history.lastUpdatedAtUtc)}</time>
      </p>

      {history.state === "Indeterminate" && (
        <p role="alert">
          نتیجه درخواست قبلی مشخص نیست. بدون دریافت شواهد معتبر، ارسال مجدد مجاز
          نیست.
        </p>
      )}

      <p className="field-hint" role="status">
        آخرین شاهد ثبت‌شده: {history.receipts.length
          ? formatDate(history.receipts.reduce((latest, item) =>
              item.observedAtUtc > latest ? item.observedAtUtc : latest,
              history.receipts[0].observedAtUtc))
          : "هنوز شاهدی ثبت نشده"}
        {" — "}تعداد شواهد: {history.receipts.length}
      </p>

      <h4 style={{ margin: "10px 0 5px", fontSize: 12 }}>
        تلاش‌های ارسال ({history.attempts.length})
      </h4>
      <AttemptsTable attempts={history.attempts} />

      <h4 style={{ margin: "10px 0 5px", fontSize: 12 }}>
        شواهد و پاسخ‌ها ({history.receipts.length})
      </h4>
      <ReceiptsTable receipts={history.receipts} />

      <p className="field-hint">
        این تاریخچه صرفاً داده‌های ثبت‌شده را نمایش می‌دهد و به معنی پذیرش قطعی
        مالیاتی نیست.
      </p>
    </>
  );
}

export default function InvoiceDispatchHistory({
  taxpayerId,
  invoiceId,
}: {
  taxpayerId: string;
  invoiceId: string;
}) {
  const [history, setHistory] = useState<History | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(
    async (signal?: AbortSignal) => {
      setPending(true);
      setError("");

      try {
        const response = await fetch(historyUrl(taxpayerId, invoiceId), {
          cache: "no-store",
          credentials: "same-origin",
          signal,
        });

        if (!response.ok) {
          throw Error(
            response.status === 401
              ? "نشست ورود منقضی شده است؛ دوباره وارد شوید."
              : `دریافت تاریخچه ارسال ناموفق بود (HTTP ${response.status}).`,
          );
        }

        const body: unknown = await response.json();
        if (!isHistoryPayload(body)) {
          throw Error("ساختار پاسخ تاریخچه معتبر نیست.");
        }

        if (!signal?.aborted) setHistory(body);
      } catch (caughtError) {
        if (!signal?.aborted) {
          setHistory(null);
          setError((caughtError as Error).message);
        }
      } finally {
        if (!signal?.aborted) setPending(false);
      }
    },
    [invoiceId, taxpayerId],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);

    return () => controller.abort();
  }, [load]);

  return (
    <section
      dir="rtl"
      aria-label="جزئیات تاریخچه ارسال"
      className="invoice-dispatch-history"
      style={{ background:"transparent", padding: "4px 12px", fontSize: 12, lineHeight: 1.55 }}
    >
      <button
        type="button"
        className="identity-secondary"
        style={{ padding: "5px 9px", fontSize: 12 }}
        disabled={pending}
        onClick={() => void load()}
      >
        {pending ? "در حال دریافت…" : "تازه‌سازی تاریخچه"}
      </button>

      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}

      {history && <HistoryContent history={history} />}
    </section>
  );
}
