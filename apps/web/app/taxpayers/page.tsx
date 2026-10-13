import { redirect } from "next/navigation";
export const dynamic = "force-dynamic";
// Legacy admin bookmark. Customer accounts are the navigation root.
export default function LegacyTaxpayersPage() {
  redirect("/users");
}
