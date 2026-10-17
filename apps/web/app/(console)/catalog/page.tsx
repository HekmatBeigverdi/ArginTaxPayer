import { currentIdentity } from "../../../lib/identity";
import CatalogWorkspace from "../../catalog/workspace";

export const dynamic = "force-dynamic";
export default async function CatalogPage() {
  const actor = await currentIdentity();
  if (!actor) return null; // Parent ConsoleLayout enforces authentication and redirects.
  return <div className="console-page console-catalog-page" dir="rtl" lang="fa">
    <div className="console-page-heading">
      <div><p className="app-kicker">اطلاعات پایه · کاتالوگ مشتری</p>
        <h1>کالاها و خدمات</h1>
        <p>تعاریف اختصاصی مشتری، واحدها و نگاشت شناسه مالیاتی</p>
      </div>
    </div>
    <CatalogWorkspace actorId={actor.userId} />
  </div>;
}
