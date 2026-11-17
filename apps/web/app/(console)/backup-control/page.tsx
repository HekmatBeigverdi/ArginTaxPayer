import {redirect} from "next/navigation";
import {currentIdentity,identityUiEnabled} from "../../../lib/identity";
import BackupControlPanel from "../../backup-control-panel";
export const dynamic="force-dynamic";
export default async function BackupControlPage(){
 if(!identityUiEnabled())redirect("/");
 const actor=await currentIdentity();if(!actor)redirect("/identity");if(!actor.roles.includes("Level1"))redirect("/dashboard");
 return <div className="console-page console-settings-page" dir="rtl"><div className="console-page-heading"><div><p className="app-kicker">مدیریت سامانه</p><h1>بکاپ و بازیابی دیتابیس</h1><p>زمان‌بندی، فایل‌ها و وضعیت سرویس‌های پشتیبان</p></div></div><BackupControlPanel/></div>;
}
