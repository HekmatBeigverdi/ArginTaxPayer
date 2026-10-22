"use client";

import { useState } from "react";

type Kind = "success" | "error" | "warning" | "info";
type Props = {
  kind: Kind;
  title: string;
  children: React.ReactNode;
  detail?: string;
  dismissible?: boolean;
};
const icon: Record<Kind, string> = { success: "✓", error: "!", warning: "!", info: "i" };

/** Consistent, dismissible Persian result box; never render raw key/token payloads. */
export default function OperationMessageBox({ kind, title, children, detail, dismissible = true }: Props) {
  const [closed, setClosed] = useState(false);
  if (closed) return null;
  return <div className={`signing-message signing-message--${kind}`}
    role={kind === "error" ? "alert" : "status"} dir="rtl">
    <span className="signing-message__icon" aria-hidden="true">{icon[kind]}</span>
    <div className="signing-message__body">
      <strong className="signing-message__title">{title}</strong>
      <div className="signing-message__text">{children}</div>
      {detail && <small className="signing-message__detail">{detail}</small>}
    </div>
    {dismissible && <button className="signing-message__close" type="button"
      aria-label="بستن پیام" onClick={() => setClosed(true)}>×</button>}
  </div>;
}
