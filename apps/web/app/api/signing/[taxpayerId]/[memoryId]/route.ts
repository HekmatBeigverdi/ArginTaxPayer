import { NextRequest, NextResponse } from 'next/server';
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from '../../../../../lib/identity';
import { isSameLocalOrigin } from '../../../../../lib/identity-origin.mjs';
import { readSigningBody } from '../../../../../lib/signing-import-body.mjs';

type Context = { params: Promise<{ taxpayerId: string; memoryId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const response = (status: number, value: unknown) => NextResponse.json(value,
  { status, headers: { 'Cache-Control': 'no-store' } });
const fail = (status: number, error: string) => response(status, { error });
const errors = new Set(['unauthorized', 'forbidden', 'not_found', 'reauthentication_required',
  'invalid_key_or_public_mismatch', 'key_change_requires_review', 'recovery_required',
  'integrity_failure', 'custody_unavailable', 'service_unavailable']);

async function proxy(req: NextRequest, context: Context, method: 'GET' | 'POST') {
  if (!identityUiEnabled()) return fail(404, 'unavailable');
  const { taxpayerId, memoryId } = await context.params;
  if (!uuid.test(taxpayerId) || !uuid.test(memoryId)) return fail(404, 'not_found');
  if (method === 'POST' && !isSameLocalOrigin(req.headers.get('origin'),
    req.headers.get('host'), req.nextUrl.protocol)) return fail(403, 'origin_not_allowed');
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return fail(401, 'unauthorized');
  let body: Uint8Array | undefined;
  try {
    if (method === 'POST') {
      if (req.headers.get('content-type')?.split(';')[0].trim().toLowerCase() !== 'application/json')
        return fail(415, 'json_required');
      if (Number(req.headers.get('content-length')) > 98304) return fail(413, 'request_too_large');
      body = await readSigningBody(req.body);
    }
    const upstream = await fetch(identityApiOrigin() + `/api/dev/signing/${taxpayerId}/${memoryId}`, {
      method, headers: { Authorization: 'Bearer ' + token,
        ...(body ? { 'Content-Type': 'application/json' } : {}) },
      ...(body ? { body: body as BodyInit } : {}), cache: 'no-store', redirect: 'manual',
      signal: AbortSignal.timeout(30000)
    });
    if (!upstream.ok) {
      const data = await upstream.json().catch(() => null);
      return fail([400, 401, 403, 404, 409, 413, 415, 429, 503].includes(upstream.status)
        ? upstream.status : 502, errors.has(data?.error) ? data.error : 'request_rejected');
    }
    const data = await upstream.json();
    // Project an allowlist even if an upstream response later changes.
    if (method === 'GET') return response(200, {
      storageReady: data.storageReady === true, configured: data.configured === true, generationConfigured: data.generationConfigured === true, freshLogin: data.freshLogin === true,
      operationId: typeof data.operationId === 'string' && uuid.test(data.operationId) ? data.operationId : null,
      hasKey: data.hasKey === true, fingerprintSha256: fingerprint(data.fingerprintSha256),
      lifecycleState: Number.isInteger(data.lifecycleState) ? data.lifecycleState : null, portalVerified: false
    });
    const key = data.publicKey;
    if (!key || !uuid.test(key.keyVersionId) || !fingerprint(key.fingerprintSha256))
      return fail(502, 'invalid_response');
    return response(upstream.status, { keyVersionId: key.keyVersionId,
      fingerprintSha256: fingerprint(key.fingerprintSha256), publicKeyCompared: data.publicKeyCompared === true,
      portalVerified: false });
  } catch (error) {
    return fail(error instanceof Error && error.message === 'request_too_large' ? 413 : 503,
      error instanceof Error && error.message === 'request_too_large' ? 'request_too_large' : 'service_unavailable');
  } finally { body?.fill(0); }
}
function fingerprint(value: unknown) {
  return typeof value === 'string' && /^[a-f0-9]{64}$/.test(value) ? value : null;
}
export const dynamic = 'force-dynamic';
export function GET(req: NextRequest, ctx: Context) { return proxy(req, ctx, 'GET'); }
export function POST(req: NextRequest, ctx: Context) { return proxy(req, ctx, 'POST'); }
