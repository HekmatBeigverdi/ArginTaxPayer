"use client";
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';

type Memory = { id: string; memoryIdentifier: string; status: number };
type State = { configured: boolean; freshLogin: boolean; operationId: string | null;
  hasKey: boolean; fingerprintSha256: string | null; lifecycleState: number | null };
function message(code: string, status: number) {
  if (code === 'reauthentication_required') return 'برای ورود کلید، دوباره وارد حساب شوید و ظرف پنج دقیقه اقدام کنید.';
  if (status === 401) return 'نشست ورود منقضی شده است؛ دوباره وارد شوید.';
  if (status === 403) return 'ورود کلید فقط با حساب مالک مؤدی مجاز است؛ دسترسی مدیریتی کافی نیست.';
  if (code === 'invalid_key_or_public_mismatch') return 'کلید یا رمز آن معتبر نیست، قالب پشتیبانی نمی‌شود، یا کلید عمومی با کلید خصوصی تطابق ندارد.';
  if (code === 'key_change_requires_review') return 'برای این حافظه کلید ثبت شده است؛ تعویض آن به درخواست و تأیید تغییر نیاز دارد.';
  if (code === 'recovery_required') return 'نتیجه ذخیره قطعی نیست. برای پیگیری همان عملیات، همان کلید را دوباره وارد کنید؛ کلید دیگری جایگزین نکنید.';
  if (status === 413) return 'حجم اطلاعات کلید بیش از حد مجاز است.';
  if (status === 429) return 'تعداد درخواست‌ها زیاد است؛ یک دقیقه بعد تلاش کنید.';
  return 'سرویس نگهداری امن کلید در دسترس نیست یا نتیجه درخواست مشخص نشد. بازخوانی کنید؛ برای پیگیری، همان کلید را دوباره وارد کنید.';
}

export default function ExistingKeyImport({ taxpayerId, memories, loadingMemories, onLoadingChange }:
  { taxpayerId: string; memories: Memory[]; loadingMemories: boolean; onLoadingChange?: (value: boolean) => void }) {
  const [selected, setSelected] = useState('');
  const memoryId = memories.some(m => m.id === selected) ? selected : memories[0]?.id ?? '';
  const endpoint = `/api/signing/${taxpayerId}/${memoryId}`;
  const [state, setState] = useState<State | null>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [consent, setConsent] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const privateKey = useRef<HTMLTextAreaElement>(null);
  const password = useRef<HTMLInputElement>(null);
  const publicKey = useRef<HTMLTextAreaElement>(null);
  const operation = useRef('');
  const sequence = useRef(0);
  const clearSecrets = useCallback(() => {
    if (privateKey.current) privateKey.current.value = '';
    if (password.current) password.current.value = '';
  }, []);
  const reload = useCallback(async () => {
    if (!memoryId) return;
    const current = ++sequence.current;
    setLoading(true); setError(''); onLoadingChange?.(true);
    try {
      const res = await fetch(endpoint, { cache: 'no-store', credentials: 'same-origin', signal: AbortSignal.timeout(15000) });
      const data = await res.json();
      if (!res.ok) throw new Error(message(data.error, res.status));
      if (current !== sequence.current) return;
      setState(data);
      operation.current = data.operationId || operation.current || crypto.randomUUID();
    } catch (ex) {
      if (current === sequence.current) { setState(null); setError((ex as Error).message); }
    } finally {
      if (current === sequence.current) { setLoading(false); onLoadingChange?.(false); }
    }
  }, [endpoint, memoryId, onLoadingChange]);
  useEffect(() => {
    setState(null); setNotice(''); setConsent(false); operation.current = ''; clearSecrets();
    void reload();
    return () => { sequence.current++; clearSecrets(); };
  }, [reload, clearSecrets]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy || loading || !state?.configured || !state.freshLogin || !consent) return;
    setBusy(true); setError(''); setNotice('');
    let payload = JSON.stringify({ operationId: operation.current,
      privateKey: privateKey.current?.value ?? '', password: password.current?.value ?? '',
      publicKey: publicKey.current?.value ?? '', acceptServerCustody: consent });
    clearSecrets();
    try {
      const res = await fetch(endpoint, { method: 'POST', credentials: 'same-origin', cache: 'no-store',
        headers: { 'Content-Type': 'application/json' }, body: payload, signal: AbortSignal.timeout(35000) });
      const data = await res.json();
      if (!res.ok) throw new Error(message(data.error, res.status));
      setNotice('کلید با نگهداری رمزگذاری‌شده ثبت شد.' +
        (data.publicKeyCompared ? ' کلید عمومی ارائه‌شده نیز تطابق دارد.' : '') + ' اتصال به سامانه مؤدیان هنوز بررسی نشده است.');
      await reload();
    } catch (ex) {
      setError(ex instanceof Error && ex.name !== 'TimeoutError' ? ex.message : message('', 503));
    } finally { payload = ''; clearSecrets(); setBusy(false); }
  }

  if (loadingMemories) return <p className="field-hint">در حال دریافت حافظه مالیاتی…</p>;
  if (!memoryId) return <p className="field-hint">ابتدا کد حافظه مالیاتی را در بخش «حافظه‌های مالیاتی» ثبت کنید.</p>;
  const sealed = state?.hasKey && state.lifecycleState !== 1;
  const canImport = state?.configured === true && state.freshLogin === true && !busy && !loading;
  return <div className="existing-key-import" aria-busy={loading || busy}>
    <div className="taxpayer-section-heading">
      <label>حافظه مالیاتی
        <select value={memoryId} disabled={busy || loading} onChange={e => setSelected(e.target.value)}>
          {memories.map(m => <option key={m.id} value={m.id}>{m.memoryIdentifier}</option>)}
        </select>
      </label>
      <button type="button" className="identity-secondary" disabled={loading || busy} onClick={() => void reload()}>بازخوانی وضعیت کلید</button>
    </div>
    {loading && <p role="status" className="field-hint">در حال دریافت وضعیت کلید…</p>}
    {error && <p role="alert" className="party-error">{error}</p>}
    {notice && <p role="status" className="party-success">{notice}</p>}
    {state?.fingerprintSha256 && <p className="field-hint">اثر انگشت کلید عمومی (SHA-256): <bdi dir="ltr">{state.fingerprintSha256}</bdi></p>}
    {sealed ? <p className="field-hint">کلید این حافظه ثبت شده است. تعریف کلید جایگزین به تأیید درخواست تغییر نیاز دارد.</p> : state && !loading && <>
      {!state.configured && <p role="status" className="field-hint">API در حال اجرا قابلیت ورود کلید را غیرفعال گزارش می‌کند. تنظیمات SigningKeyProtection و SigningKeyImport را در فرایند API بررسی و سرویس را مجدداً راه‌اندازی کنید. سلامت دیتابیس به معنی فعال‌بودن Vault نیست.</p>}
      {!state.freshLogin && <p className="field-hint">برای ورود کلید دوباره وارد حساب شوید و ظرف پنج دقیقه اقدام کنید.</p>}
      <form onSubmit={submit} autoComplete="off">
        <p className="field-hint">متن فایل TXT کلید خصوصی قبلی را وارد کنید. قالب قابل قبول RSA 2048 و PKCS#8 با عنوان PRIVATE KEY یا ENCRYPTED PRIVATE KEY است. RSA PRIVATE KEY پشتیبانی نمی‌شود.</p>
        <label>متن کلید خصوصی<textarea ref={privateKey} required maxLength={65536} rows={5} dir="ltr"
          autoComplete="off" spellCheck={false} disabled={!canImport} /></label>
        <label>رمز فایل کلید (فقط برای کلید رمزدار)<input ref={password} type="password" maxLength={1024}
          autoComplete="new-password" disabled={!canImport} /></label>
        <label>کلید عمومی قبلی برای تطبیق (اختیاری)<textarea ref={publicKey} maxLength={16384} rows={3}
          dir="ltr" spellCheck={false} disabled={!canImport} /></label>
        <label className="signing-custody-consent"><input type="checkbox" checked={consent} disabled={!canImport}
          onChange={e => setConsent(e.target.checked)} />اجازه می‌دهم کلید خصوصی به‌صورت رمزگذاری‌شده در سامانه نگهداری شود.</label>
        <button type="submit" className="identity-primary" disabled={!canImport || !consent}>
          {busy ? 'در حال ثبت کلید…' : 'ورود کلید موجود'}</button>
      </form>
    </>}
  </div>;
}
