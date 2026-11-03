export type DashboardIconName = "invoice" | "parties" | "subscription" | "calendar" | "message" | "catalog" | "settings" | "ownership" | "history" | "modules";

const paths: Record<DashboardIconName, string[]> = {
  catalog: [
    "m12 3 9 5-9 5-9-5z",
    "m3 12 9 5 9-5M3 16l9 5 9-5M12 13v8",
  ],
  settings: [
    "M4 7h16M4 17h16M8 4v6M16 14v6",
  ],
  ownership: [
    "M12 3 3 7v5c0 5 9 9 9 9s9-4 9-9V7z",
    "m8 12 3 3 5-5",
  ],
  history: [
    "M3 11a9 9 0 1 1 2.6 7M3 4v7h7",
    "M12 7v5l3 2",
  ],
  modules: [
    "M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z",
  ],
  invoice: [
    "M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z",
    "M14 3v6h6M8 13h8M8 17h5",
  ],
  parties: [
    "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2",
    "M16 3a4 4 0 0 1 0 8M22 21v-2a4 4 0 0 0-3-3.87",
    "M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
  ],
  subscription: [
    "M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2",
    "M2 10h20M6 15h3M16 15h2",
  ],
  calendar: [
    "M8 2v4M16 2v4M3 10h18",
    "M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2",
    "m9 16 2 2 4-4",
  ],
  message: [
    "M21 11.5a8.5 8.5 0 0 1-8.5 8.5H4l-2 2V11.5A8.5 8.5 0 0 1 10.5 3h2a8.5 8.5 0 0 1 8.5 8.5",
    "M7 9h9M7 13h6",
  ],
};

export default function DashboardIcon({ name }: { name: DashboardIconName }) {
  return (
    <svg
      width="24" height="24" viewBox="0 0 24 24" fill="none"
      stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"
      aria-hidden="true" focusable="false"
    >
      {paths[name].map(path => <path key={path} d={path} />)}
    </svg>
  );
}
