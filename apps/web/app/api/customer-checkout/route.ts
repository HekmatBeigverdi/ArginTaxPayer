import { NextRequest,NextResponse } from "next/server";
import { cookies } from "next/headers";
import { ACCESS_COOKIE,currentIdentity,identityApiOrigin,identityUiEnabled } from "../../../lib/identity";
export const dynamic="force-dynamic";
async function relay(path:string,method:"GET"|"POST",body?:string){
 if(!identityUiEnabled())return NextResponse.json({error:"disabled"},{status:404});
 if(!await currentIdentity())return NextResponse.json({error:"unauthorized"},{status:401});
 const token=(await cookies()).get(ACCESS_COOKIE)?.value;
 if(!token)return NextResponse.json({error:"unauthorized"},{status:401});
 try{
  const res=await fetch(identityApiOrigin()+"/api/dev/subscriptions/"+path,{
   method,body,headers:{Authorization:"Bearer "+token,...(body?{"Content-Type":"application/json"}:{})},
   cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(10000)
  });
  return new NextResponse(await res.text(),{status:res.status,headers:{
   "Content-Type":"application/json","Cache-Control":"no-store"}});
 }catch{return NextResponse.json({error:"backend_unavailable"},{status:502});}
}
export async function GET(){return relay("customer-offers/me","GET");}
export async function POST(request:NextRequest){
 if(request.headers.get("origin") && request.headers.get("origin")!==request.nextUrl.origin)
  return NextResponse.json({error:"invalid_origin"},{status:403});
 const body=await request.text();
 if(body.length>1024)return NextResponse.json({error:"too_large"},{status:413});
 let data:unknown;
 try{data=JSON.parse(body);}catch{return NextResponse.json({error:"invalid_json"},{status:400});}
 if(!data||typeof data!=="object")return NextResponse.json({error:"invalid_payload"},{status:400});
 return relay("customer-checkout/cash","POST",body);
}
