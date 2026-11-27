"use client";

import SubscriptionStatus from "./subscription-status";

import Link from "next/link";
import Image from "next/image";
import ConsoleLoader from "./console-loader";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { trackSessionActivity } from "../../lib/session-activity";

type Item = { title: string; path: string; glyph: string };
type ProfileIdentity = {
  complete: boolean;
  firstName?: string;
  lastName?: string;
  companyName?: string | null;
};
const base: Item[] = [
  { title: "داشبورد", path: "/dashboard", glyph: "▦" },
  { title: "خریداران و مؤدیان", path: "/parties", glyph: "♧" },
  { title: "کالاها و خدمات", path: "/catalog", glyph: "▤" },
  { title: "صورتحساب‌ها", path: "/invoices", glyph: "▣" },
  { title: "پروفایل و حساب من", path: "/profile", glyph: "♙" },
  { title: "حساب و امنیت", path: "/account", glyph: "⚙" }
];

export default function ConsoleShell({ roles, userId, dashboardOnly = false, children }:
  { roles: string[]; userId: string; dashboardOnly?: boolean; children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const [navigatingTo, setNavigatingTo] = useState<string | null>(null);
  const [exiting, setExiting] = useState(false);
  const [profileState, setProfileState] = useState<"checking" | "complete" | "incomplete" | "error">("checking");
  const [profileRetry, setProfileRetry] = useState(0);
  const [accountName, setAccountName] = useState("");
  const [sessionBlocked, setSessionBlocked] = useState(false);
  const [newVersion, setNewVersion] = useState("");
  const appVersion = process.env.NEXT_PUBLIC_APP_VERSION || "development";
  const menuRef = useRef<HTMLDivElement>(null);
  const admin = roles.includes("Level1") || roles.includes("Level2");
  const role = roles.includes("Level1") ? "مدیر سطح یک" :
    roles.includes("Level2") ? "مدیر سطح دو" : "کاربر پنل";
  const about: Item = { title: "درباره ما", path: "/about", glyph: "ⓘ" };
  const navigation = dashboardOnly ? [base[0]] : admin ? [base[0],
    { title: "کاربران نرم‌افزار", path: "/users", glyph: "♟" },
    ...(roles.includes("Level1") ? [
      { title: "تأیید تغییر کلید", path: "/signing-approvals", glyph: "✓" },
      { title: "تأیید تغییر حافظه", path: "/memory-change-approvals", glyph: "↻" }
    ] : []),
    base[1], base[2], base[3],
    { title: "اشتراک‌ها و امور مالی", path: "/subscriptions", glyph: "▥" }, base[4], about] : [...base, about];
  const accountNavigation: Item[] = (dashboardOnly || admin) ? [] : [
    ...(roles.includes("Customer") ? [{ title: "پلن‌های نرم‌افزار", path: "/customer-plans", glyph: "▣" }] : []),
    ...(roles.includes("Customer") ? [{ title: "اعتبار پیامک", path: "/sms-credit", glyph: "✉" }] : []),
    { title: "پرداخت‌ها و اشتراک‌ها", path: "/financial-history", glyph: "▤" }
  ];
  useEffect(() => { setMenuOpen(false); setNavigatingTo(null); }, [pathname]);
  const recoveryPanel = pathname === "/backup-control" && roles.includes("Level1");
  const requiresProfile = !dashboardOnly && roles.includes("Customer") && !recoveryPanel;
  // Read the verified profile once per signed-in user (or explicit retry), NOT once
  // per navigation. Route changes must never reset a completed profile to checking.
  useEffect(() => {
    setAccountName("");
    if (!requiresProfile) setProfileState("complete");
    if (dashboardOnly) return;

    const controller = new AbortController();
    if (requiresProfile) setProfileState("checking");

    function updateProfile(data: ProfileIdentity) {
      const fullName = [data.firstName?.trim(), data.lastName?.trim()].filter(Boolean).join(" ");
      setAccountName(data.companyName?.trim() || fullName);
      if (requiresProfile) setProfileState(data.complete ? "complete" : "incomplete");
    }

    function onSaved(event: Event) {
      const detail = (event as CustomEvent<ProfileIdentity>).detail;
      if (typeof detail?.complete !== "boolean") return;
      controller.abort();
      updateProfile(detail);
    }

    window.addEventListener("argin:software-profile-saved", onSaved);
    fetch("/api/software-profile/me", { cache: "no-store", signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error("profile_unavailable");
        const data = await response.json() as ProfileIdentity;
        if (typeof data.complete !== "boolean") throw new Error("invalid_profile_response");
        if (!controller.signal.aborted) updateProfile(data);
      })
      .catch(() => {
        if (requiresProfile && !controller.signal.aborted) setProfileState("error");
      });
    return () => {
      controller.abort();
      window.removeEventListener("argin:software-profile-saved", onSaved);
    };
  }, [userId, requiresProfile, profileRetry, dashboardOnly]);

  useEffect(() => trackSessionActivity(() => router.replace("/identity"), setSessionBlocked), [router, userId]);

  useEffect(() => {
    let active = true;
    const checkVersion = async () => {
      try {
        const response = await fetch("/api/app-version", { cache: "no-store" });
        if (!response.ok) return;
        const current = await response.json() as { version?: unknown };
        if (active && typeof current.version === "string" && current.version !== appVersion) {
          setNewVersion(current.version);
        }
      } catch { /* Version checks must not interrupt normal console work. */ }
    };
    void checkVersion();
    const timer = window.setInterval(() => void checkVersion(), 5 * 60 * 1000);
    return () => { active = false; window.clearInterval(timer); };
  }, [appVersion]);

  useEffect(() => {
    if (dashboardOnly && pathname !== "/dashboard") router.replace("/dashboard");
    if (requiresProfile && profileState === "incomplete" && pathname !== "/profile") router.replace("/profile");
  }, [dashboardOnly, requiresProfile, profileState, pathname, router]);
  useEffect(() => {
    function dismiss(e: MouseEvent) { if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false); }
    function escape(e: KeyboardEvent) { if (e.key === "Escape") setMenuOpen(false); }
    document.addEventListener("mousedown", dismiss);
    document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("mousedown", dismiss); document.removeEventListener("keydown", escape); };
  }, []);
  async function logout() {
    if (exiting) return;
    setExiting(true);
    try {
      const result = await fetch("/api/identity/logout", {
        method: "POST", credentials: "same-origin", cache: "no-store"
      });
      if (!result.ok) throw new Error("خروج انجام نشد.");
      router.replace("/identity");
      router.refresh();
    } catch {
      setExiting(false);
      alert("خروج از حساب انجام نشد؛ اتصال را بررسی کنید.");
    }
  }
  const activeName = [...navigation, ...accountNavigation]
    .find(n => pathname === n.path || pathname.startsWith(n.path + "/"))?.title ?? "پنل";
  return <>
    {sessionBlocked && <ConsoleLoader label="در حال بررسی نشست…" />}
    <div className="app-shell argin-console" dir="rtl" lang="fa"
      style={sessionBlocked ? { display: "none" } : undefined}>
    <aside className="app-sidebar">
      <Link href="/dashboard" onClick={() => { if (pathname !== "/dashboard") setNavigatingTo("/dashboard"); }} className="app-brand"><Image className="argin-brand-logo argin-sidebar-logo" src="/brand/argin-logo.png" width={46} height={46} alt="نشان آرگین" priority unoptimized />
        <span><strong>آرگین</strong><small>سامانه یکپارچه مؤدیان</small></span></Link>
      <p className="app-nav-heading">فضای کاری</p>
      <nav className="app-nav" aria-label="ناوبری اصلی">
        {navigation.map(n => <Link key={n.path} href={n.path}
          onClick={() => { if (pathname !== n.path) setNavigatingTo(n.path); }}
          aria-current={pathname === n.path ? "page" : undefined}>
          <span className="console-nav-label"><span aria-hidden="true">{n.glyph}</span>{n.title}</span>
          <span aria-hidden="true">←</span></Link>)}
      </nav>
      <footer className="app-sidebar-footer" aria-label="شرکت‌های آرگین">
        <span>{dashboardOnly ? "نمایش محدود Production" : `نسخه ${appVersion}`}</span>
        <div className="app-sidebar-company-logos">
          <Image src="/brand/companies/negin-hesab-vira.png" width={38} height={38} alt="نگین حساب ویرا" />
          <Image src="/brand/companies/hamgam-afzar.png" width={38} height={38} alt="داده پردازی همگام افزار" />
        </div>
        <small className="app-sidebar-copyright">حقوق نرم افزار آرگین برای صاحبان نرم افزار محفوظ است</small>
      </footer>
    </aside>
    <div className="app-main">
      <header className="app-topbar">
        <div className="console-crumb">پنل آرگین / <strong>{activeName}</strong></div>
        <div className="console-account-actions">
          <div ref={menuRef} className="console-account">
            <button type="button" className="console-account-trigger" aria-haspopup="menu"
              aria-expanded={menuOpen} onClick={() => setMenuOpen(v => !v)}>
              <Image className="argin-brand-logo argin-header-logo" src="/brand/argin-logo.png" width={39} height={39} alt="" aria-hidden="true" unoptimized />
              <span className="console-account-name"><strong>حساب من</strong><small>{role}</small></span>
              <span aria-hidden="true">⌄</span>
            </button>
            {menuOpen && <div className="console-account-dropdown" role="menu" aria-label="حساب کاربری">
              {accountName && <p className="console-menu-identity"><strong><bdi>{accountName}</bdi></strong></p>}
              {!dashboardOnly && <>
              <Link role="menuitem" href="/profile" onClick={() => setMenuOpen(false)}>پروفایل و حساب من</Link>
              <Link role="menuitem" href="/account" onClick={() => setMenuOpen(false)}>حساب و امنیت</Link></>}
              {accountNavigation.map(item => <Link key={item.path} role="menuitem" href={item.path}
                aria-current={pathname === item.path ? "page" : undefined}
                onClick={() => setMenuOpen(false)}>{item.title}</Link>)}
              {!dashboardOnly && roles.includes("Level1") && <Link role="menuitem" href="/admin-settings" onClick={() => setMenuOpen(false)}>تنظیمات مدیریت</Link>}
              <div className="console-menu-divider"/>
              <button role="menuitem" type="button" onClick={() => void logout()} disabled={exiting}>
                {exiting ? "در حال خروج…" : "خروج از حساب"}
              </button>
            </div>}
          </div>
          {!dashboardOnly && roles.includes("Customer") && !admin && <SubscriptionStatus compact/>}
          {!dashboardOnly && roles.includes("Customer") && !admin && <Link href="/sms-credit" className="subscription-status-compact">خرید اعتبار پیامک</Link>}
        </div>
      </header>
      <div className="app-content console-content-host" id="console-content" aria-busy={Boolean(navigatingTo)}>
        {newVersion && <div className="app-update-notice" role="status">
          <span>نسخه جدید نرم‌افزار آماده است ({newVersion}). برای دریافت به‌روزرسانی، صفحه را تازه کنید.</span>
          <button type="button" onClick={() => window.location.reload()}>به‌روزرسانی</button>
        </div>}
        {(!requiresProfile || profileState === "complete" || pathname === "/profile") ? children :
          profileState === "checking" ?
            <ConsoleLoader label="در حال بررسی وضعیت پروفایل…" /> :
          profileState === "error" ?
            <section className="intro" role="alert">
              <h2>بررسی پروفایل ممکن نشد</h2>
              <p>ارتباط با سرویس پروفایل برقرار نشد. این وضعیت به معنی ناقص بودن پروفایل نیست.</p>
              <button type="button" className="identity-secondary"
                onClick={() => setProfileRetry(n => n + 1)}>تلاش مجدد</button>
            </section> :
            <section className="intro" role="status">
              <h2>تکمیل پروفایل الزامی است</h2>
              <p>برای ادامه نام، نام خانوادگی و نشانی خود را تکمیل کنید.</p>
              <Link href="/profile">رفتن به پروفایل و حساب من</Link>
            </section>}
        {navigatingTo && <ConsoleLoader full label="در حال انتقال به بخش انتخاب‌شده…" />}
      </div>
    </div>
  </div></>;
}
