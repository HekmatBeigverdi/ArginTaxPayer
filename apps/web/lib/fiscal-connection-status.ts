export type ConnectionAttempt = { status: string; testedAtUtc: string };
export type ConnectionHistory = { available: boolean; lastAttempt: ConnectionAttempt | null; lastSuccess: ConnectionAttempt | null };
const statuses = new Set(['Authenticated','AuthenticationRejected','NotAuthorized','KeyNotReady','VaultUnavailable',
  'EndpointUnavailable','TimedOut','UnsupportedProfile','Indeterminate']);

export function projectConnectionHistory(value: unknown): ConnectionHistory {
  const row = value && typeof value === 'object' ? value as Record<string, unknown> : {};
  const attempt = (v: unknown): ConnectionAttempt | null => {
    if (!v || typeof v !== 'object') return null;
    const item = v as Record<string, unknown>;
    return typeof item.status === 'string' && statuses.has(item.status) &&
      typeof item.testedAtUtc === 'string' && item.testedAtUtc.length <= 40 && Number.isFinite(Date.parse(item.testedAtUtc))
      ? { status: item.status, testedAtUtc: item.testedAtUtc } : null;
  };
  const lastSuccess = attempt(row.lastSuccess);
  return { available: row.available === true, lastAttempt: attempt(row.lastAttempt),
    lastSuccess: lastSuccess?.status === 'Authenticated' ? lastSuccess : null };
}

export function historyWithLegacyAttempt(history: ConnectionHistory | null | undefined,
  succeeded: boolean | null, testedAtUtc: string | null): ConnectionHistory {
  const current=history??{available:false,lastAttempt:null,lastSuccess:null};
  if(current.lastAttempt||!testedAtUtc||!Number.isFinite(Date.parse(testedAtUtc))||succeeded===null)return current;
  const attempt={status:succeeded?'Authenticated':'Indeterminate',testedAtUtc};
  return { ...current,lastAttempt:attempt,lastSuccess:succeeded?attempt:current.lastSuccess };
}

export function getConnectionPresentation(hasKey: boolean, history: ConnectionHistory | null | undefined) {
  const status = history?.lastAttempt?.status;
  const needsKeyAction = status === 'KeyNotReady' || status === 'VaultUnavailable';
  const state = !hasKey ? 'setup' : status === 'Authenticated' ? 'verified' : needsKeyAction ? 'key-error' :
    status === 'AuthenticationRejected' ? 'authentication-error' : status ? 'service-error' : 'ready';
  const label = state === 'setup' ? 'نیازمند تنظیم کلید' : state === 'verified' ? 'اتصال موفق' :
    state === 'key-error' ? 'کلید نیازمند بررسی است' : state === 'authentication-error' ? 'احراز هویت تأیید نشد' :
    state === 'service-error' ? 'آخرین آزمون کامل نشد' : 'کلید ثبت شده؛ اتصال را آزمون کنید';
  return { state, label, needsKeyAction, lastSuccess: history?.lastSuccess ?? null };
}

export function sandboxSourceChoice(legacy: { memoryIdentifier: string; keyCustodyReady: boolean } | null,
  regular: { memoryIdentifier: string; hasKey?: boolean } | null, environment: number) {
  if (environment !== 2 || !legacy) return 'registered';
  if (!regular) return 'legacy';
  if (legacy.memoryIdentifier !== regular.memoryIdentifier) return 'separate';
  if (regular.hasKey === undefined) return 'loading';
  return regular.hasKey ? 'choice' : 'legacy';
}

// Presentation only: never replace an already saved invoice's internal binding.
export function visibleInvoiceMemories<T extends {id:string;memoryIdentifier:string;status:number;authorityEnvironment:1|2|null}>(
  memories:T[], legacy: {memoryIdentifier:string}[], selectedId:string):T[] {
  return memories.filter(memory=>memory.id===selectedId || memory.authorityEnvironment!==2 || memory.status===2 ||
    !legacy.some(existing=>existing.memoryIdentifier===memory.memoryIdentifier));
}
