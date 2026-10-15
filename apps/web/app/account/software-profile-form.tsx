"use client";
import { useCallback, useEffect, useRef, useState, type DragEvent, type FormEvent } from "react";
import { useRouter } from "next/navigation";

type Profile = { userId: string; firstName: string; lastName: string;
  companyName: string | null; address: string; hasLogo: boolean;
  complete: boolean; revision: number };
const initial: Profile = { userId: "", firstName: "", lastName: "",
  companyName: null, address: "", hasLogo: false, complete: false, revision: 0 };

function postLogo(blob: Blob, report: (percent: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", "/api/software-profile/logo");
    xhr.setRequestHeader("Content-Type", "image/png");
    xhr.timeout = 30000;
    xhr.upload.onprogress = event => {
      if (event.lengthComputable && event.total > 0) {
        report(Math.min(99, Math.round(event.loaded * 100 / event.total)));
      }
    };
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) { report(100); resolve(); }
      else reject(new Error(xhr.status === 413 || xhr.status === 415 ?
        "حجم یا فرمت تصویر توسط سرور رد شد." :
        xhr.status === 401 ? "برای بارگذاری مجدد وارد حساب شوید." :
        "آپلود لوگو ناموفق بود (کد " + xhr.status + ")."));
    };
    xhr.onerror = () => reject(new Error("اتصال هنگام آپلود لوگو قطع شد."));
    xhr.ontimeout = () => reject(new Error("زمان آپلود به پایان رسید."));
    xhr.onabort = () => reject(new Error("آپلود لغو شد."));
    xhr.send(blob);
  });
}

async function toNormalizedPng(file: File): Promise<Blob> {
  if (!["image/png", "image/jpeg", "image/webp"].includes(file.type) ||
      file.size > 5 * 1024 * 1024 || file.size === 0)
    throw new Error("PNG، JPG یا WebP با حجم حداکثر ۵ مگابایت انتخاب کنید.");
  const source = URL.createObjectURL(file);
  const img = new Image();
  try {
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error("فایل انتخابی تصویر معتبر نیست."));
      img.src = source;
    });
  } finally { URL.revokeObjectURL(source); }
  if (!img.naturalWidth || !img.naturalHeight ||
      img.naturalWidth > 8000 || img.naturalHeight > 8000)
    throw new Error("ابعاد تصویر مجاز نیست.");
  const canvas = document.createElement("canvas");
  canvas.width = 512; canvas.height = 512;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("پردازش تصویر ممکن نشد.");
  const scale = Math.min(448 / img.naturalWidth, 448 / img.naturalHeight, 1);
  const w = Math.max(1, Math.round(img.naturalWidth * scale));
  const h = Math.max(1, Math.round(img.naturalHeight * scale));
  ctx.clearRect(0, 0, 512, 512);
  ctx.drawImage(img, (512 - w) / 2, (512 - h) / 2, w, h);
  const result = await new Promise<Blob>((resolve, reject) =>
    canvas.toBlob(blob => blob ? resolve(blob) :
      reject(new Error("تبدیل تصویر به PNG ناموفق بود.")), "image/png"));
  if (result.size > 350000)
    throw new Error("لوگوی خروجی بیش از ۳۵۰ کیلوبایت است. تصویر ساده‌تر انتخاب کنید.");
  return result;
}

export default function SoftwareProfileForm({ required = false }: { required?: boolean }) {
  const router = useRouter();
  const fileInput = useRef<HTMLInputElement>(null);
  const [profile, setProfile] = useState<Profile>(initial);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const [filename, setFilename] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const res = await fetch("/api/software-profile/me", { cache: "no-store" });
      if (!res.ok) throw new Error("دریافت پروفایل انجام نشد.");
      setProfile(await res.json() as Profile);
    } catch (e) { setError((e as Error).message); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  function update(key: "firstName" | "lastName" | "companyName" | "address", value: string) {
    setProfile(p => ({ ...p, [key]: value }));
    setError(""); setNotice("");
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || uploading) return;
    setSaving(true); setError(""); setNotice("");
    try {
      const res = await fetch("/api/software-profile/me", {
        method: "PUT", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          firstName: profile.firstName, lastName: profile.lastName,
          companyName: profile.companyName, address: profile.address,
          expectedRevision: profile.revision
        })
      });
      if (!res.ok) throw new Error(res.status === 409 ?
        "پروفایل در جای دیگری تغییر کرده؛ ابتدا بازخوانی کنید." :
        res.status === 400 ? "نام، نام خانوادگی و نشانی را با طول معتبر وارد کنید." :
        "ذخیره اطلاعات پروفایل ناموفق بود.");
      setProfile(await res.json() as Profile);
      setNotice("اطلاعات پروفایل با موفقیت ذخیره شد.");
      router.refresh();
    } catch (e) { setError((e as Error).message); }
    finally { setSaving(false); }
  }

  async function upload(file: File) {
    if (uploading || saving) return;
    setUploading(true); setProgress(null);
    setFilename(file.name); setError(""); setNotice("");
    try {
      const blob = await toNormalizedPng(file);
      setProgress(0);
      await postLogo(blob, setProgress);
      // Read actual persisted revision; uploading increments profile revision in the API.
      await load();
      setNotice("لوگو با موفقیت آپلود و به PNG استاندارد تبدیل شد.");
    } catch (e) {
      setProgress(null);
      setError((e as Error).message);
    } finally { setUploading(false); }
  }
  function drop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault(); setDragging(false);
    if (uploading || saving || !profile.complete) return;
    const file = event.dataTransfer.files.item(0);
    if (file) void upload(file);
  }

  return <div className="console-profile-layout">
    <section className="console-settings-card" id="profile">
      <div className="console-settings-card-title">
        <h2>مشخصات فردی و سازمانی</h2>
        <p>اطلاعات حساب ورود شما، مستقل از پرونده خریدار و مؤدی</p>
      </div>
      {required && !profile.complete && !loading &&
        <p className="console-profile-required" role="status">
          پیش از استفاده از بخش‌های پنل، نام، نام خانوادگی و نشانی را تکمیل کنید.
        </p>}
      {loading ? <p className="field-hint">در حال دریافت اطلاعات پروفایل…</p> :
        <form className="console-profile-form" onSubmit={save}>
          <div className="console-profile-two-col">
            <label>نام <input required minLength={2} maxLength={100}
              autoComplete="given-name" value={profile.firstName}
              onChange={e => update("firstName", e.target.value)}/></label>
            <label>نام خانوادگی <input required minLength={2} maxLength={100}
              autoComplete="family-name" value={profile.lastName}
              onChange={e => update("lastName", e.target.value)}/></label>
          </div>
          <label>نام شرکت / مؤسسه <span className="console-optional">(اختیاری)</span>
            <input maxLength={256} autoComplete="organization"
              value={profile.companyName ?? ""}
              onChange={e => update("companyName", e.target.value)}/></label>
          <label>نشانی
            <textarea rows={3} required minLength={2} maxLength={500}
              autoComplete="street-address" value={profile.address}
              onChange={e => update("address", e.target.value)}/></label>
          <div className="console-profile-actions">
            <button type="submit" className="identity-primary" disabled={saving || uploading}>
              {saving ? "در حال ذخیره…" : "ذخیره تغییرات"}
            </button>
            <button type="button" className="identity-secondary" disabled={saving || uploading}
              onClick={() => void load()}>بازخوانی</button>
          </div>
        </form>}
    </section>

    <section className="console-settings-card">
      <div className="console-settings-card-title">
        <h2>لوگوی شرکت / مؤسسه</h2>
        <p>برای نمایش در سربرگ فاکتورهای آینده</p>
      </div>
      <input ref={fileInput} id="software-profile-logo" className="console-file-visually-hidden"
        type="file" accept="image/png,image/jpeg,image/webp"
        disabled={uploading || saving || !profile.complete}
        onChange={e => {
          const file = e.target.files?.item(0);
          if (file) void upload(file);
          e.target.value = "";
        }}/>
      <div className={"console-logo-dropzone" + (dragging ? " is-dragging" : "") +
          (!profile.complete || uploading || saving ? " is-disabled" : "")}
        role="button" tabIndex={profile.complete && !uploading && !saving ? 0 : -1}
        aria-label="انتخاب یا رها کردن تصویر لوگوی شرکت"
        aria-disabled={!profile.complete || uploading || saving}
        onDragOver={e => { e.preventDefault(); if (profile.complete && !uploading) setDragging(true); }}
        onDragLeave={e => { if (!e.currentTarget.contains(e.relatedTarget as Node)) setDragging(false); }}
        onDrop={drop}
        onClick={() => { if (profile.complete && !uploading && !saving) fileInput.current?.click(); }}
        onKeyDown={e => {
          if ((e.key === "Enter" || e.key === " ") && profile.complete && !uploading && !saving) {
            e.preventDefault(); fileInput.current?.click();
          }
        }}>
        <span className="console-logo-upload-icon" aria-hidden="true">↑</span>
        <strong>{uploading ? "در حال ارسال لوگو…" :
          "تصویر را اینجا رها کنید یا برای انتخاب کلیک کنید"}</strong>
        <small>PNG، JPG، WebP • حداکثر ۵ مگابایت</small>
        <small>خروجی PNG شفاف، ۵۱۲ × ۵۱۲ پیکسل</small>
        {!profile.complete && <small>ابتدا مشخصات پروفایل را ذخیره کنید.</small>}
      </div>
      {uploading && <div className="console-upload-status" role="status" aria-live="polite">
        <div><span>{filename}</span><strong>{progress === null ? "در حال پردازش…" : progress + "٪"}</strong></div>
        <progress max={100} value={progress === null ? undefined : progress}
          aria-label="پیشرفت آپلود لوگو"/>
      </div>}
      {profile.hasLogo && <div className="console-logo-preview">
        <img src={"/api/software-profile/logo?v=" + profile.revision}
          alt="لوگوی ذخیره‌شده شرکت" width={72} height={72}/>
        <div><strong>لوگوی فعلی</strong><small>ذخیره‌شده و آماده استفاده در فاکتور</small></div>
      </div>}
    </section>
    {error && <p role="alert" className="party-error">{error}</p>}
    {notice && <p role="status" className="party-success">{notice}</p>}
  </div>;
}
