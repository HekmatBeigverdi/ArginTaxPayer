"use client";

import {useCallback,useState} from "react";
import Image from "next/image";
import IdentityFlow,{type AuthVisualState} from "./identity-flow";

const visualCopy:Record<AuthVisualState,{eyebrow:string;title:string;body:string}>={
 login:{eyebrow:"ورود امن و ساده",title:"امور مالیاتی، یکپارچه و روشن",body:"با اطمینان وارد فضای کاری خود شوید و کارها را از یک نقطه مدیریت کنید."},
 register:{eyebrow:"شروعی مطمئن",title:"حساب آرگین، همراه کسب‌وکار شما",body:"حساب خود را بسازید و ابزارهای موردنیازتان را در یک فضای امن در دسترس داشته باشید."},
 recovery:{eyebrow:"بازیابی دسترسی",title:"دوباره به مسیرتان برگردید",body:"با تأیید شماره همراه، دسترسی حساب را امن و سریع بازیابی کنید."},
};

const visualArt:Record<AuthVisualState,{src:string;width:number;height:number}>={
 login:{src:"/identity-login.png",width:1536,height:1024},
 register:{src:"/identity-register.png",width:1536,height:1024},
 recovery:{src:"/identity-recovery.png",width:1536,height:1024},
};

function IdentityIllustration({state}:{state:AuthVisualState}){
 const art=visualArt[state];
 return <Image className="identity-visual__art identity-visual__float"
  src={art.src} width={art.width} height={art.height} alt="" unoptimized/>;
}

export default function IdentityScreen({restrictedLogin=false}:{restrictedLogin?:boolean}){
 const [visualState,setVisualState]=useState<AuthVisualState>("login");
 const copy=visualCopy[visualState];
 const handleVisualStateChange=useCallback((state:AuthVisualState)=>setVisualState(state),[]);
 return <div className={"identity-screen identity-screen--"+visualState}>
  <section className="identity-screen__form" aria-label="ورود و ثبت‌نام">
   <div className="identity-screen__form-intro">
    <span className="identity-kicker">حساب کاربری</span>
    <h1 className="identity-screen__welcome">به آرگین خوش آمدید</h1>
    <p className="identity-muted">برای ورود به فضای کاری، اطلاعات حساب خود را وارد کنید.</p>
   </div>
   <IdentityFlow restrictedLogin={restrictedLogin} onVisualStateChange={handleVisualStateChange}/>
  </section>
  <aside className="identity-screen__visual" aria-hidden="true" data-state={visualState}>
   <div className="identity-screen__mesh"/>
   <div className="identity-screen__visual-brand"><span className="identity-screen__brand-mark">آ</span><span>آرگین</span></div>
   <div className="identity-screen__art-wrap"><IdentityIllustration state={visualState}/></div>
   <div className="identity-screen__visual-copy" key={visualState}>
    <span>{copy.eyebrow}</span><h2>{copy.title}</h2><p>{copy.body}</p>
   </div>
   <div className="identity-screen__visual-foot"><span/>مدیریت مالیاتی، با اطمینان</div>
  </aside>
 </div>;
}

