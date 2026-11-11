import {NextRequest,NextResponse} from 'next/server';
import {ACCESS_COOKIE,identityApiOrigin,identityUiEnabled} from '../../../../../../lib/identity';
import {isSameLocalOrigin} from '../../../../../../lib/identity-origin.mjs';
import {readSigningBody} from '../../../../../../lib/signing-import-body.mjs';

export const dynamic='force-dynamic';
type Context={params:Promise<{taxpayerId:string}>};
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fail=(status:number,error:string)=>NextResponse.json({error},{status,headers:{'Cache-Control':'no-store'}});
const errors=new Set(['sandbox_scope_denied','sandbox_owner_reauthentication_required','sandbox_custody_consent_required',
  'sandbox_memory_changed','sandbox_key_pair_mismatch','sandbox_memory_not_registered','sandbox_key_protection_unavailable',
  'sandbox_test_database_required','sandbox_key_registration_conflict','invalid_private_key_input','invalid_sandbox_private_key']);

export async function POST(request:NextRequest,context:Context){
  if(!identityUiEnabled())return fail(404,'unavailable');
  const {taxpayerId}=await context.params;
  if(!uuid.test(taxpayerId))return fail(404,'not_found');
  if(!isSameLocalOrigin(request.headers.get('origin'),request.headers.get('host'),request.nextUrl.protocol))return fail(403,'origin_not_allowed');
  const bearer=request.cookies.get(ACCESS_COOKIE)?.value;
  if(!bearer)return fail(401,'unauthorized');
  if(!request.headers.get('content-type')?.toLowerCase().startsWith('application/json'))return fail(415,'json_required');
  if(Number(request.headers.get('content-length')??0)>16000)return fail(413,'payload_too_large');
  let body:Uint8Array|undefined;
  try{
    body=await readSigningBody(request.body);
    if(body.length>16000)return fail(413,'payload_too_large');
    const value:unknown=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(body));
    if(!value||typeof value!=='object'||Array.isArray(value))return fail(400,'invalid_payload');
    const row=value as Record<string,unknown>;
    if(Object.keys(row).length!==4||Object.keys(row).some(k=>!['memoryId','privateKeyPem','expectedRevision','acceptServerCustody'].includes(k))||
      typeof row.memoryId!=='string'||!uuid.test(row.memoryId)||
      typeof row.privateKeyPem!=='string'||row.privateKeyPem.length<100||row.privateKeyPem.length>11999||
      !row.privateKeyPem.startsWith('-----BEGIN PRIVATE KEY-----')||row.acceptServerCustody!==true||
      !Number.isSafeInteger(row.expectedRevision)||Number(row.expectedRevision)<1)return fail(400,'invalid_payload');
    const upstream=await fetch(identityApiOrigin()+`/api/dev/taxpayers/${taxpayerId}/sandbox-memory/private-key/restore`,{
      method:'POST',cache:'no-store',redirect:'manual',signal:AbortSignal.timeout(25000),
      headers:{Authorization:'Bearer '+bearer,'Content-Type':'application/json'},body:body as BodyInit});
    const raw=await upstream.text();
    if(raw.length>4096)return fail(502,'invalid_response');
    const result=JSON.parse(raw);
    if(!upstream.ok)return fail([400,401,403,404,409,429,503].includes(upstream.status)?upstream.status:502,
      errors.has(result?.error)?result.error:'request_rejected');
    if(result?.data?.keyCustodyReady!==true||!Number.isSafeInteger(result?.data?.revision))return fail(502,'invalid_response');
    return NextResponse.json({environment:'Sandbox',keyCustodyReady:true,revision:result.data.revision},
      {status:200,headers:{'Cache-Control':'no-store'}});
  }catch(error){return fail(error instanceof SyntaxError?400:503,error instanceof SyntaxError?'invalid_payload':'sandbox_key_storage_unavailable');}
  finally{body?.fill(0);}
}
