import ConsoleLoader from "../components/console-loader";
export default function Loading() {
  return <div className="console-route-loading">
    <ConsoleLoader full label="در حال آماده‌سازی صفحه…" />
  </div>;
}
