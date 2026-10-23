"use client";

type KeyRow = {
  id: string;
  fingerprint: string;
  profile: string;
  lifecycle: string;
  selected: boolean;
  revision: number;
};

const demoKeys: KeyRow[] = [
  { id: "نسخه فعلی", fingerprint: "sha256:••••••••••••", profile: "Legacy", lifecycle: "آماده استفاده", selected: true, revision: 4 },
  { id: "نسخه قبلی", fingerprint: "sha256:••••••••••••", profile: "Legacy", lifecycle: "جایگزین‌شده", selected: false, revision: 3 },
];

export function KeyManagementWorkspace() {
  return (
    <main className="console-page key-management-page" dir="rtl">
      <header className="console-page-heading">
        <div><h1>مدیریت کلید امضای مودی</h1><p>نمایش وضعیت کلیدهای عمومی و درخواست تغییر کنترل‌شده</p></div>
        <span className="key-status-badge">محیط توسعه</span>
      </header>

      <section className="key-safety-note" aria-label="هشدار امنیتی">
        <strong>کلید خصوصی هرگز در این صفحه نمایش داده یا دانلود نمی‌شود.</strong>
        <span>تولید، ورود و چرخش کلید فقط پس از احراز مجدد و تأیید کنترل‌شده فعال خواهد شد.</span>
      </section>

      <section className="key-management-grid">
        <article className="key-panel">
          <div className="key-panel-heading"><div><h2>مشخصات حافظه مالیاتی</h2><p>محدوده از حساب کاربر و سرور تعیین می‌شود.</p></div></div>
          <dl className="key-facts">
            <div><dt>مودی</dt><dd>مودی انتخاب‌شده</dd></div>
            <div><dt>حافظه مالیاتی</dt><dd dir="ltr"><bdi>Fiscal memory</bdi></dd></div>
            <div><dt>نسخه revision</dt><dd dir="ltr"><bdi>4</bdi></dd></div>
            <div><dt>ثبت در مرجع</dt><dd>تأیید نشده</dd></div>
          </dl>
          <div className="key-actions" aria-label="عملیات کلید">
            <button type="button" disabled>تولید کلید جدید</button>
            <button type="button" disabled>ورود کلید موجود</button>
            <button type="button" disabled>درخواست چرخش</button>
          </div>
          <p className="key-disabled-help">عملیات در این گام به endpoint عملیاتی متصل نیست و پس از تکمیل گیت امنیتی فعال می‌شود.</p>
        </article>

        <article className="key-panel">
          <div className="key-panel-heading"><div><h2>تاریخچه نسخه‌های کلید</h2><p>اطلاعات عمومی و قابل ممیزی؛ بدون داده محرمانه</p></div></div>
          <div className="key-table-wrap"><table className="console-table key-table"><thead><tr><th>نسخه</th><th>اثر انگشت عمومی</th><th>وضعیت</th><th>revision</th></tr></thead><tbody>
            {demoKeys.map(key => <tr key={key.id}><td><strong>{key.id}</strong>{key.selected && <small className="key-selected">فعال</small>}</td><td dir="ltr"><bdi>{key.fingerprint}</bdi></td><td>{key.lifecycle}</td><td dir="ltr"><bdi>{key.revision}</bdi></td></tr>)}
          </tbody></table></div>
        </article>
      </section>
    </main>
  );
}
