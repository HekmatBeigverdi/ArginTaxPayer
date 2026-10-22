import { NextRequest, NextResponse } from 'next/server';
import { ACCESS_COOKIE, identityApiOrigin, identityUiEnabled } from '../../../../../../lib/identity';
import { isSameLocalOrigin } from '../../../../../../lib/identity-origin.mjs';

type Context = { params: Promise<{ taxpayerId: string; memoryId: string }> };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const allowedStatuses = new Set(['Authenticated', 'AuthenticationRejected', 'NotAuthorized',
  'KeyNotReady', 'VaultUnavailable', 'EndpointUnavailable', 'TimedOut',
  'UnsupportedProfile', 'Indeterminate']);
const allowedCodes = new Set(['authority_authenticated', 'authority_authentication_rejected',
  'not_authorized', 'key_not_ready', 'vault_unavailable', 'authority_unavailable',
  'authority_timeout', 'unsupported_authentication_profile', 'authority_result_indeterminate']);
const errors = new Set(['unauthorized', 'forbidden', 'not_found', 'service_unavailable',
  'authority_timeout']);
const fail = (status: number, error: string) => NextResponse.json({ error }, {
  status, headers: { 'Cache-Control': 'no-store', Pragma: 'no-cache' }
});

async function hasRequestBody(req: NextRequest): Promise<boolean> {
  const declaredLength = req.headers.get('content-length');
  if (declaredLength !== null && (!/^\d+$/.test(declaredLength) || Number(declaredLength) > 0))
    return true;
  if (!req.body) return false;
  const reader = req.body.getReader();
  try {
    const first = await reader.read();
    const bodyBytes = first.done ? 0 : first.value?.byteLength ?? 0;
    return bodyBytes > 0;
  } finally {
    await reader.cancel().catch(() => undefined);
  }
}

export const dynamic = 'force-dynamic';
export async function POST(req: NextRequest, ctx: Context) {
  if (!identityUiEnabled()) return fail(404, 'unavailable');
  const { taxpayerId, memoryId } = await ctx.params;
  if (!uuid.test(taxpayerId) || !uuid.test(memoryId)) return fail(404, 'not_found');
  if (!isSameLocalOrigin(req.headers.get('origin'), req.headers.get('host'), req.nextUrl.protocol))
    return fail(403, 'origin_not_allowed');
  const bearer = req.cookies.get(ACCESS_COOKIE)?.value;
  if (!bearer) return fail(401, 'unauthorized');
  // No client-supplied endpoint, profile, signing bytes or secrets are accepted.
  if (await hasRequestBody(req)) return fail(413, 'request_too_large');
  try {
    const upstream = await fetch(identityApiOrigin() +
      `/api/dev/signing/${taxpayerId}/${memoryId}/connection-test`, {
      method: 'POST', headers: { Authorization: 'Bearer ' + bearer },
      redirect: 'manual', cache: 'no-store', signal: AbortSignal.timeout(30000)
    });
    if (!upstream.ok) {
      const data = await upstream.json().catch(() => null);
      return fail([401, 403, 404, 413, 415, 429, 503, 504].includes(upstream.status) ?
        upstream.status : 502, errors.has(data?.error) ? data.error : 'request_rejected');
    }
    const data = await upstream.json().catch(() => null);
    if (!data || !allowedStatuses.has(data.status) || !allowedCodes.has(data.safeCode) ||
      typeof data.testedAtUtc !== 'string' || !Number.isFinite(Date.parse(data.testedAtUtc))) {
      return fail(502, 'invalid_response');
    }
    // Fail closed: do not trust externally supplied success flags.
    const authenticated = data.status === 'Authenticated' &&
      data.safeCode === 'authority_authenticated' && data.authenticationConfirmed === true;
    const fiscalInformation = data.fiscalInformation;
    const hasFiscalInformation = fiscalInformation &&
      typeof fiscalInformation.nameTrade === 'string' &&
      typeof fiscalInformation.fiscalStatus === 'string' &&
      typeof fiscalInformation.economicCode === 'string' &&
      typeof fiscalInformation.nationalId === 'string';
    if ((data.status === 'Authenticated') !== authenticated ||
        (data.status !== 'Authenticated' && data.authenticationConfirmed === true) ||
        (authenticated && !hasFiscalInformation)) {
      return fail(502, 'invalid_response');
    }
    return NextResponse.json({
      status: data.status, safeCode: data.safeCode, testedAtUtc: data.testedAtUtc,
      authenticationConfirmed: authenticated,
      portalRegistrationConfirmed: false, fiscalMemoryActivationConfirmed: false,
      fiscalInformation: authenticated ? fiscalInformation : null
    }, { status: 200, headers: { 'Cache-Control': 'no-store', Pragma: 'no-cache' } });
  } catch {
    return fail(503, 'service_unavailable');
  }
}
