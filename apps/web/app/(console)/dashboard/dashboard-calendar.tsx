"use client";

import { useEffect, useState } from "react";

const timeZone = "Asia/Tehran";
const calendars = [
  { label: "شمسی", calendar: "persian", locale: "fa-IR", direction: "rtl" },
  { label: "میلادی", calendar: "gregory", locale: "en-GB", direction: "ltr" },
  { label: "قمری", calendar: "islamic-umalqura", locale: "fa-IR", direction: "rtl" },
] as const;

export function calendarDatesFor(date: Date) {
  return calendars.map(calendar => {
    const formatter = new Intl.DateTimeFormat(calendar.locale, {
      calendar: calendar.calendar,
      timeZone,
      weekday: "long",
      day: "numeric",
      month: "long",
      year: "numeric",
    });
    const parts = formatter.formatToParts(date);
    const part = (type: Intl.DateTimeFormatPartTypes) => (
      parts.find(value => value.type === type)?.value ?? ""
    );
    const text = calendar.direction === "ltr"
      ? formatter.format(date)
      : `${part("weekday")}، ${part("day")} ${part("month")} ${part("year")}`;

    return { ...calendar, text, day: part("day"), month: part("month") };
  });
}

function CalendarIllustration({ day, month }: { day: string; month: string }) {
  return (
    <svg viewBox="0 0 180 160" fill="none" aria-hidden="true" focusable="false">
      <circle cx="87" cy="83" r="65" fill="#e1f2ef" />
      <circle cx="151" cy="35" r="11" fill="#ffcf73" />
      <circle cx="24" cy="115" r="5" fill="#ed9d86" />
      <path d="m24 40 3 6 6 3-6 3-3 6-3-6-6-3 6-3z" fill="#91c6dd" />
      <path d="m145 114 2 5 5 2-5 2-2 5-2-5-5-2 5-2z" fill="#77bdb0" />
      <g transform="rotate(-7 90 80)">
        <rect x="41" y="29" width="108" height="121" rx="17" fill="#b9dcd6" />
        <rect x="34" y="21" width="108" height="121" rx="17" fill="#fff" stroke="#d2e6e2" />
        <path d="M34 38a17 17 0 0 1 17-17h74a17 17 0 0 1 17 17v20H34z" fill="#218e86" />
        <path d="M60 14v18M116 14v18" stroke="#224b5b" strokeWidth="7" strokeLinecap="round" />
        <path d="M60 14v10M116 14v10" stroke="#a6d1c8" strokeWidth="3" strokeLinecap="round" />
        <text x="88" y="48" textAnchor="middle" fill="#fff" fontSize="13" fontWeight="700"
          style={{ fontFamily: "inherit" }} direction="rtl">{month}</text>
        <text x="88" y="104" textAnchor="middle" fill="#224b5b" fontSize="39" fontWeight="800"
          style={{ fontFamily: "inherit" }} direction="rtl">{day}</text>
        <rect x="58" y="118" width="23" height="5" rx="2.5" fill="#d6eae5" />
        <rect x="88" y="118" width="23" height="5" rx="2.5" fill="#f4c471" />
      </g>
      <circle cx="141" cy="130" r="18" fill="#efad67" stroke="#fff" strokeWidth="4" />
      <path d="m134 130 5 5 9-10" stroke="#fff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export default function DashboardCalendar() {
  // Read the browser clock after hydration so server/client dates cannot disagree.
  const [today, setToday] = useState<Date | null>(null);

  useEffect(() => {
    const updateToday = () => setToday(new Date());
    updateToday();
    const timer = window.setInterval(updateToday, 60_000);
    return () => window.clearInterval(timer);
  }, []);

  const dates = today ? calendarDatesFor(today) : [];

  return (
    <section className="argin-dashboard__calendar" aria-label="تقویم امروز" aria-busy={!today}>
      <div className="argin-dashboard__calendar-art">
        <CalendarIllustration day={dates[0]?.day ?? "…"} month={dates[0]?.month ?? "امروز"} />
      </div>
      <div className="argin-dashboard__calendar-content">
        <div className="argin-dashboard__calendar-heading">
          <h3>امروز در تقویم</h3>
          <span>به وقت تهران</span>
        </div>
        {today ? (
          <dl className="argin-dashboard__calendar-dates">
            {dates.map(date => (
              <div key={date.calendar}>
                <dt>{date.label}</dt>
                <dd>
                  <time dateTime={today.toISOString()} lang={date.locale} dir={date.direction}>
                    {date.text}
                  </time>
                </dd>
              </div>
            ))}
          </dl>
        ) : <p className="argin-dashboard__kpi-note">در حال آماده‌سازی تقویم…</p>}
      </div>
    </section>
  );
}
