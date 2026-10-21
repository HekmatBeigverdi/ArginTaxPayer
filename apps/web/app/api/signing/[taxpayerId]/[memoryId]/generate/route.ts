import { NextRequest, NextResponse } from 'next/server';
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from '../../../../../../lib/identity';
import { isSameLocalOrigin } from '../../../../../../lib/identity-origin.mjs';
import { readSigningBody } from '../../../../../../lib/signing-import-body.mjs';

type Context = { params: Promise<{ taxpayerId: string; memoryId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const maximumBodyBytes = 8192;
const jsonResponse = (status: number, value: unknown) => NextResponse.json(value, {
  status, headers: { 'Cache-Control': 'no-store', Pragma: 'no-cache' }
});
const fail = (status: number, error: string) => jsonResponse(status, { error });
const errors = new Set(['unauthorized', 'forbidden', 'not_found', 'invalid_request',
  'key_change_requires_review', 'recovery_required', 'integrity_failure', 'custody_unavailable',
  'service_unavailable']);

async function proxy(req: NextRequest, context: Context) {
  if (!identityUiEnabled()) return fail(404, 'unavailable');
  const { taxpayerId, memoryId } = await context.params;
  if (!uuid.test(taxpayerId) || !uuid.test(memoryId)) return fail(404, 'not_found');
  if (!isSameLocalOrigin(req.headers.get('origin'), req.headers.get('host'), req.nextUrl.protocol))
    return fail(403, 'origin_not_allowed');
  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!token) return fail(401, 'unauthorized');
  if (req.headers.get('content-type')?.split(';')[0].trim().toLowerCase() !== 'application/json')
    return fail(415, 'json_required');
  if (Number(req.headers.get('content-length')) > maximumBodyBytes)
    return fail(413, 'request_too_large');

  let body: Uint8Array | undefined;
  try {
    body = await readSigningBody(req.body, maximumBodyBytes);
    const upstream = await fetch(identityApiOrigin() +
      `/api/dev/signing/${taxpayerId}/${memoryId}/generate`, {
      method: 'POST',
      headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
      body: body as BodyInit, cache: 'no-store', redirect: 'manual',
      signal: AbortSignal.timeout(30000)
    });
    if (!upstream.ok) {
      const data = await upstream.json().catch(() => null);
      return fail([400, 401, 403, 404, 409, 413, 415, 429, 503].includes(upstream.status)
        ? upstream.status : 502, errors.has(data?.error) ? data.error : 'request_rejected');
    }
    if (upstream.headers.get('content-type')?.toLowerCase().startsWith('application/zip')) {
      const headers = new Headers({
        'Cache-Control': 'no-store', Pragma: 'no-cache',
        'Content-Type': 'application/zip',
        'Content-Disposition': upstream.headers.get('content-disposition') ?? 'attachment; filename="signing-keys.zip"'
      });
      for (const name of ['content-length', 'x-signing-key-fingerprint', 'x-signing-key-version']) {
        const value = upstream.headers.get(name);
        if (value) headers.set(name, value);
      }
      return new NextResponse(upstream.body, { status: 200, headers });
    }
    const data = await upstream.json().catch(() => null);
    return jsonResponse(200, {
      status: data?.status === 'replayed' ? 'replayed' : 'created',
      privateHandoffAvailable: false,
      fingerprintSha256: typeof data?.publicKey?.fingerprintSha256 === 'string'
        ? data.publicKey.fingerprintSha256 : null
    });
  } catch (error) {
    return fail(error instanceof Error && error.message === 'request_too_large' ? 413 : 503,
      error instanceof Error && error.message === 'request_too_large' ? 'request_too_large' : 'service_unavailable');
  } finally {
    body?.fill(0);
  }
}

export const dynamic = 'force-dynamic';
export async function POST(req: NextRequest, ctx: Context) { return proxy(req, ctx); }
