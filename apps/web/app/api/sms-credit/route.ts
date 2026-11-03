import {NextResponse} from "next/server";
import {cookies} from "next/headers";
import {ACCESS_COOKIE,currentIdentity,identityApiOrigin,identityUiEnabled} from "../../../lib/identity";
export const dynamic="force-dynamic";
export async function GET(){
 if(!identityUiEnabled())return NextResponse.json({error:"disabled"},{status:404});
 if(!await currentIdentity())return NextResponse.json({error:"unauthenticated"},{status:401});
 const token=(await cookies()).get(ACCESS_COOKIE)?.value;
 if(!token)return NextResponse.json({error:"unauthenticated"},{status:401});
 try{const r=await fetch(identityApiOrigin()+"/api/dev/subscriptions/sms-credit",{headers:{Authorization:"Bearer "+token},cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(10000)});return new NextResponse(await r.text(),{status:r.status,headers:{"Content-Type":"application/json","Cache-Control":"no-store"}})}catch{return NextResponse.json({error:"backend_unavailable"},{status:502})}
}
