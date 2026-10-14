"use client";

import Link from "next/link";
import ConsoleLoader from "./console-loader";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState, type ReactNode } from "react";

type Item = { title: string; path: string; glyph: string };
const base: Item[] = [
  { title: "داشبورد", path: "/dashboard", glyph: "▦" },
  { title: "خریداران و مؤدیان", path: "/parties", glyph: "♧" },
  { title: "حساب و امنیت", path: "/account", glyph: "⚙" }
];

export default function ConsoleShell({ roles, userId, children }:
  { roles: string[]; userId: string; children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const [navigatingTo, setNavigatingTo] = useState<string | null>(null);
  const [exiting, setExiting] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const admin = roles.includes("Level1") || roles.includes("Level2");
  const role = roles.includes("Level1") ? "مدیر سطح یک" :
    roles.includes("Level2") ? "مدیر سطح دو" : "کاربر پنل";
  const navigation = admin ? [base[0],
    { title: "کاربران نرم‌افزار", path: "/users", glyph: "♟" }, base[1], base[2]] : base;
  useEffect(() => { setMenuOpen(false); setNavigatingTo(null); }, [pathname]);
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
  const activeName = navigation.find(n => pathname === n.path || pathname.startsWith(n.path + "/"))?.title ?? "پنل";
  return <div className="app-shell argin-console" dir="rtl" lang="fa">
    <aside className="app-sidebar">
      <Link href="/dashboard" onClick={() => { if (pathname !== "/dashboard") setNavigatingTo("/dashboard"); }} className="app-brand"><span className="app-brand-mark">آ</span>
        <span><strong>آرگین</strong><small>سامانه یکپارچه مؤدیان</small></span></Link>
      <p className="app-nav-heading">فضای کاری</p>
      <nav className="app-nav" aria-label="ناوبری اصلی">
        {navigation.map(n => <Link key={n.path} href={n.path}
          onClick={() => { if (pathname !== n.path) setNavigatingTo(n.path); }}
          aria-current={pathname === n.path ? "page" : undefined}>
          <span className="console-nav-label"><span aria-hidden="true">{n.glyph}</span>{n.title}</span>
          <span aria-hidden="true">←</span></Link>)}
      </nav>
      <footer className="app-sidebar-footer"><span>نسخه توسعه</span><p>دسترسی‌ها براساس نقش و مالکیت کنترل می‌شوند.</p></footer>
    </aside>
    <div className="app-main">
      <header className="app-topbar">
        <div className="console-crumb">پنل آرگین / <strong>{activeName}</strong></div>
        <div ref={menuRef} className="console-account">
          <button type="button" className="console-account-trigger" aria-haspopup="menu"
            aria-expanded={menuOpen} onClick={() => setMenuOpen(v => !v)}>
            <span className="console-avatar" aria-hidden="true">آ</span>
            <span className="console-account-name"><strong>حساب من</strong><small>{role}</small></span>
            <span aria-hidden="true">⌄</span>
          </button>
          {menuOpen && <div className="console-account-dropdown" role="menu" aria-label="حساب کاربری">
            <p className="console-menu-identity">شناسه حساب <bdi dir="ltr">{userId.slice(0, 8)}…</bdi></p>
            <Link role="menuitem" href="/account" onClick={() => setMenuOpen(false)}>پروفایل و حساب من</Link>
            <Link role="menuitem" href="/account#security" onClick={() => setMenuOpen(false)}>امنیت و نشست‌ها</Link>
            <div className="console-menu-divider"/>
            <button role="menuitem" type="button" onClick={() => void logout()} disabled={exiting}>
              {exiting ? "در حال خروج…" : "خروج از حساب"}
            </button>
          </div>}
        </div>
      </header>
      <div className="app-content console-content-host" id="console-content" aria-busy={Boolean(navigatingTo)}>
        {children}
        {navigatingTo && <ConsoleLoader full label="در حال انتقال به بخش انتخاب‌شده…" />}
      </div>
    </div>
  </div>;
}
