"use client";

import { useRef, useState, type ReactNode } from "react";

function toPem(bytes: ArrayBuffer, label: string) {
  const encoded = btoa(String.fromCharCode(...new Uint8Array(bytes)));
  return `-----BEGIN ${label}-----\n${encoded.match(/.{1,64}/g)?.join("\n")}\n-----END ${label}-----\n`;
}

function downloadPem(pem: string, filename: string) {
  const url = URL.createObjectURL(new Blob([pem], { type: "application/x-pem-file" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export default function ProductionKeyPreparation({ registration }:
  { registration: (publicKey: string) => ReactNode }) {
  const privateKey = useRef("");
  const [publicKey, setPublicKey] = useState("");
  const [privateDownloadStarted, setPrivateDownloadStarted] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState("");

  async function generate() {
    if (generating || publicKey) return;
    setGenerating(true);
    setError("");
    try {
      if (!globalThis.crypto?.subtle) {
        throw new Error("ساخت کلید به مرورگر سازگار و اتصال امن (HTTPS) نیاز دارد.");
      }
      const pair = await crypto.subtle.generateKey({
        name: "RSASSA-PKCS1-v1_5",
        modulusLength: 2048,
        publicExponent: new Uint8Array([1, 0, 1]),
        hash: "SHA-256"
      }, true, ["sign", "verify"]);
      const [publicBytes, privateBytes] = await Promise.all([
        crypto.subtle.exportKey("spki", pair.publicKey),
        crypto.subtle.exportKey("pkcs8", pair.privateKey)
      ]);
      privateKey.current = toPem(privateBytes, "PRIVATE KEY");
      setPublicKey(toPem(publicBytes, "PUBLIC KEY"));
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : "ساخت کلید انجام نشد.");
    } finally {
      setGenerating(false);
    }
  }

  return <div className="signing-key-generation">
    {!publicKey ? <>
      <p>جفت‌کلید را در همین مرورگر بسازید و هر دو فایل را دانلود کنید.</p>
      <button type="button" className="identity-primary" disabled={generating} onClick={() => void generate()}>
        {generating ? "در حال ساخت جفت‌کلید…" : "ساخت جفت‌کلید جدید"}
      </button>
    </> : <>
      <button type="button" className="identity-secondary"
        onClick={() => downloadPem(publicKey, "argin-production-public.pem")}>دانلود کلید عمومی</button>
      <button type="button" className="identity-secondary"
        onClick={() => {
          downloadPem(privateKey.current, "argin-production-private.pem");
          setPrivateDownloadStarted(true);
        }}>دانلود کلید خصوصی</button>
      <p>کلید عمومی را در کارپوشهٔ سازمان بارگذاری کنید تا کد حافظه را دریافت کنید.</p>
      <p>فایل خصوصی را در جای امن نگه دارید. بعد از ثبت کد، همین فایل را برای اتصال وارد کنید.</p>
      {privateDownloadStarted ? registration(publicKey) :
        <p className="field-hint">برای ادامه و ثبت کد، ابتدا فایل کلید خصوصی را دانلود کنید.</p>}
    </>}
    {error && <p role="alert" className="party-error">{error}</p>}
  </div>;
}
