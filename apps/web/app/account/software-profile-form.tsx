"use client";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
type Profile={userId:string;firstName:string;lastName:string;companyName:string|null;address:string;hasLogo:boolean;complete:boolean;revision:number};
const empty:Profile={userId:"",firstName:"",lastName:"",companyName:null,address:"",hasLogo:false,complete:false,revision:0};
export default function SoftwareProfileForm({required=false}:{required?:boolean}){
  const router=useRouter();
  const [profile,setProfile]=useState<Profile>(empty),[loading,setLoading]=useState(true);
  const [busy,setBusy]=useState(false),[error,setError]=useState(""),[notice,setNotice]=useState("");
  const load=useCallback(async()=>{
    setLoading(true);setError("");
    try{const res=await fetch("/api/software-profile/me",{cache:"no-store"});
      if(!res.ok)throw new Error("دریافت اطلاعات پروفایل ناموفق بود.");
      setProfile(await res.json() as Profile);
    }catch(e){setError((e as Error).message)}finally{setLoading(false)}
  },[]);
  useEffect(()=>{void load()},[load]);
  const set=(key:"firstName"|"lastName"|"companyName"|"address",value:string)=>
    setProfile(old=>({...old,[key]:value}));
  async function save(e:FormEvent){
    e.preventDefault();setBusy(true);setError("");setNotice("");
    try{
      const res=await fetch("/api/software-profile/me",{method:"PUT",
        headers:{"Content-Type":"application/json"},
        body:JSON.stringify({firstName:profile.firstName,lastName:profile.lastName,
          companyName:profile.companyName,address:profile.address,expectedRevision:profile.revision})});
      if(!res.ok)throw new Error(res.status===409?"اطلاعات در نشست دیگری تغییر کرده؛ بازخوانی کنید.":
        res.status===400?"نام، نام خانوادگی و نشانی را با طول معتبر وارد کنید.":"ذخیره پروفایل انجام نشد.");
      setProfile(await res.json() as Profile);setNotice("پروفایل ذخیره شد.");
      router.refresh();
    }catch(e){setError((e as Error).message)}finally{setBusy(false)}
  }
  async function upload(file:File){
    if(!/^image\/(png|jpeg|webp)$/.test(file.type)||file.size>5*1024*1024){
      setError("تصویر ورودی باید PNG، JPG یا WebP و حداکثر ۵ مگابایت باشد.");return;
    }
    setBusy(true);setError("");setNotice("");
    try{
      const img=new Image();const source=URL.createObjectURL(file);
      try{await new Promise<void>((resolve,reject)=>{
        img.onload=()=>resolve();img.onerror=()=>reject(new Error("تصویر خوانده نشد."));img.src=source;
      });}finally{URL.revokeObjectURL(source);}
      if(!img.naturalWidth||!img.naturalHeight||img.naturalWidth>8000||img.naturalHeight>8000)
        throw new Error("ابعاد فایل تصویر مجاز نیست.");
      const canvas=document.createElement("canvas");canvas.width=512;canvas.height=512;
      const ctx=canvas.getContext("2d");if(!ctx)throw new Error("پردازش تصویر ممکن نشد.");
      const scale=Math.min(448/img.naturalWidth,448/img.naturalHeight,1);
      const w=Math.max(1,Math.round(img.naturalWidth*scale)),h=Math.max(1,Math.round(img.naturalHeight*scale));
      ctx.clearRect(0,0,512,512);ctx.drawImage(img,(512-w)/2,(512-h)/2,w,h);
      const blob=await new Promise<Blob>((resolve,reject)=>canvas.toBlob(b=>b?resolve(b):
        reject(new Error("تبدیل تصویر ناموفق بود.")),"image/png"));
      if(blob.size>350000)throw new Error("خروجی PNG بزرگ است؛ لوگوی ساده‌تر یا با جزئیات کمتر انتخاب کنید.");
      const res=await fetch("/api/software-profile/logo",{method:"POST",headers:{"Content-Type":"image/png"},body:blob});
      if(!res.ok)throw new Error("ذخیره لوگو انجام نشد؛ ابتدا مشخصات پروفایل را تکمیل کنید.");
      setProfile(old=>({...old,hasLogo:true,revision:old.revision+1}));
      setNotice("لوگو در قالب PNG استاندارد ۵۱۲×۵۱۲ ذخیره شد.");
    }catch(e){setError((e as Error).message)}finally{setBusy(false)}
  }
  return <section className="intro" id="profile"><h2>{required?"تکمیل اولیه پروفایل":"مشخصات پروفایل کاربر نرم‌افزار"}</h2>
    {required&&<p role="status">برای استفاده از بخش‌های پنل ابتدا مشخصات هویتی و نشانی خود را کامل کنید. نام شرکت و لوگو برای حساب شخصی اختیاری هستند.</p>}
    {loading?<p>در حال دریافت اطلاعات…</p>:<form className="identity-form" onSubmit={save}>
      <label>نام <input required maxLength={100} minLength={2} value={profile.firstName} onChange={e=>set("firstName",e.target.value)}/></label>
      <label>نام خانوادگی <input required maxLength={100} minLength={2} value={profile.lastName} onChange={e=>set("lastName",e.target.value)}/></label>
      <label>نام شرکت / مؤسسه (اختیاری) <input maxLength={256} value={profile.companyName??""} onChange={e=>set("companyName",e.target.value)}/></label>
      <label>نشانی <textarea required minLength={2} maxLength={500} value={profile.address} onChange={e=>set("address",e.target.value)}/></label>
      <button className="identity-primary" type="submit" disabled={busy}>{busy?"در حال ثبت…":"ذخیره پروفایل"}</button>
    </form>}
    {!loading&&profile.complete&&<div><h3>لوگوی شرکت / مؤسسه برای فاکتور</h3>
      <p className="field-hint">PNG، JPEG یا WebP، حداکثر ۵ مگابایت ورودی؛ تبدیل خودکار به PNG استاندارد ۵۱۲×۵۱۲ با پس‌زمینه شفاف، حداکثر ۳۵۰ کیلوبایت خروجی.</p>
      <input type="file" accept="image/png,image/jpeg,image/webp" disabled={busy}
        onChange={e=>{const file=e.target.files?.[0];if(file)void upload(file);e.target.value="";}}/>
      {profile.hasLogo&&<img src={"/api/software-profile/logo?v="+profile.revision} alt="لوگوی فعلی شرکت" width={128} height={128}/>}
    </div>}
    {error&&<p role="alert" className="party-error">{error}</p>}
    {notice&&<p role="status" className="party-success">{notice}</p>}
  </section>
}
