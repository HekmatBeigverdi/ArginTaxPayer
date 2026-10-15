import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from "../../../../lib/identity";
import { isSameLocalOrigin } from "../../../../lib/identity-origin.mjs";
type Context = { params: Promise<{ path: string[] }> };
async function handle(req: NextRequest, ctx: Context, method: "GET"|"PUT"|"POST") {
  if (!identityUiEnabled()) return NextResponse.json({error:"not_found"},{status:404});
  const {path}=await ctx.params;
  if (path.length !== 1 || !["me","logo"].includes(path[0]) ||
      path[0] === "me" && method === "POST" ||
      path[0] === "logo" && method === "PUT") return NextResponse.json({error:"not_found"},{status:404});
  const token=req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({error:"unauthenticated"},{status:401});
  if (method !== "GET" && !isSameLocalOrigin(
    req.headers.get("origin"),req.headers.get("host"),req.nextUrl.protocol))
    return NextResponse.json({error:"origin_not_allowed"},{status:403});
  let body: string|ArrayBuffer|undefined;
  if (method === "PUT") {
    if (!(req.headers.get("content-type")||"").startsWith("application/json"))
      return NextResponse.json({error:"invalid_type"},{status:415});
    body=await req.text();
    if (!body || body.length>3000) return NextResponse.json({error:"bad_payload"},{status:400});
    try { JSON.parse(body); } catch { return NextResponse.json({error:"bad_json"},{status:400}); }
  }
  if (method === "POST") {
    if (req.headers.get("content-type") !== "image/png" || Number(req.headers.get("content-length")||0)>350000)
      return NextResponse.json({error:"invalid_logo_format"},{status:415});
    body=await req.arrayBuffer();
    if (body.byteLength>350000) return NextResponse.json({error:"logo_too_large"},{status:413});
  }
  try {
    const res=await fetch(identityApiOrigin()+"/api/dev/software-profile/"+path[0],{
      method,redirect:"manual",cache:"no-store",signal:AbortSignal.timeout(10000),
      headers:{Authorization:"Bearer "+token,...(method==="PUT"?{"Content-Type":"application/json"}:
        method==="POST"?{"Content-Type":"image/png"}:{})},body
    });
    if (!res.ok) return NextResponse.json({error:"profile_request_failed"},
      {status:[400,401,403,404,409,413,415].includes(res.status)?res.status:502});
    const bytes=await res.arrayBuffer();
    if (bytes.byteLength>400000) return NextResponse.json({error:"oversize_response"},{status:502});
    return new NextResponse(bytes,{status:res.status,headers:{"Content-Type":path[0]==="logo" && method==="GET"?"image/png":"application/json","Cache-Control":"private, no-store","X-Content-Type-Options":"nosniff"}});
  }catch {return NextResponse.json({error:"backend_unavailable"},{status:503});}
}
export const dynamic="force-dynamic";
export const GET=(r:NextRequest,c:Context)=>handle(r,c,"GET");
export const PUT=(r:NextRequest,c:Context)=>handle(r,c,"PUT");
export const POST=(r:NextRequest,c:Context)=>handle(r,c,"POST");
