import { redirect } from "next/navigation";
import { currentIdentity } from "../../../lib/identity";
import BuyerTaxpayerDirectory from "../../parties/buyer-taxpayer-directory";

export const dynamic = "force-dynamic";
export default async function PartiesPage() {
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const level1 = actor.roles.includes("Level1");
  const admin = level1 || actor.roles.includes("Level2");
  const customer = actor.roles.includes("Customer");
  if (!admin && !customer) redirect("/account");
  return <div className="console-page">
    <div className="console-page-heading"><div><p className="app-kicker">اطلاعات تجاری و مالیاتی</p>
      <h1>خریداران و مؤدیان</h1>
      <p>{admin ? "فهرست خریداران و مؤدیان مرتبط، ثبت حساب و مدیریت پرونده مالیاتی" :
        "مشاهده اشخاص و مؤدیان وابسته به حساب شما"}</p></div></div>
    <BuyerTaxpayerDirectory admin={admin} level1={level1}/>
  </div>;
}
