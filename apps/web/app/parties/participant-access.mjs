/** UI eligibility only; the API independently enforces roles, MFA and ownership. */
export function canRegisterParticipant(customer) {
  return Boolean(customer?.profileComplete);
}
