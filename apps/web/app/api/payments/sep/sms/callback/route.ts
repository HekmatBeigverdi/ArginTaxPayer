import {NextRequest,NextResponse} from "next/server";
import {identityApiOrigin} from "../../../../../../lib/identity";
export const dynamic="force-dynamic";

export async function POST(request:NextRequest){
 if(request.headers.get("content-length")===null||Number(request.headers.get("content-length"))>8192)
  return NextResponse.json({error:"invalid_callback_size"},{status:413});
 let incoming:FormData;try{incoming=await request.formData()}catch{return NextResponse.json({error:"invalid_callback"},{status:400})}
 const form=new FormData();for(const [key,value] of incoming.entries()){
  if(typeof value!=="string"||key.length>40||value.length>256)return NextResponse.json({error:"invalid_callback"},{status:400});
  form.append(key,value);
 }
 try{
  const response=await fetch(identityApiOrigin()+"/api/payments/sep/sms/callback",{method:"POST",body:form,cache:"no-store",redirect:"manual",signal:AbortSignal.timeout(30000)});
  const location=response.headers.get("location");if(response.status>=300&&response.status<400&&location){
   const target=new URL(location);if(target.origin!==request.nextUrl.origin)return NextResponse.json({error:"invalid_return_target"},{status:502});
   return NextResponse.redirect(target,303);
  }
  return NextResponse.json({error:"callback_not_processed"},{status:response.status||502});
 }catch{return NextResponse.json({error:"payment_verification_unavailable"},{status:502})}
}
