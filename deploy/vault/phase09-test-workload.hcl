# Review template only; this task does not install Vault or apply policy.
# Name this policy argin-signing in the isolated test Vault.
# Agent authentication/renewal is provisioned separately; never supply a root token to the app.
path "auth/token/lookup-self" {
  capabilities = ["read"]
}
path "auth/token/renew-self" {
  capabilities = ["update"]
}
path "argin-transit-test/keys/signing-test-kek" {
  capabilities = ["read"]
}
path "argin-transit-test/encrypt/signing-test-kek" {
  capabilities = ["update"]
}
path "argin-transit-test/decrypt/signing-test-kek" {
  capabilities = ["update"]
}
path "argin-transit-test/rewrap/signing-test-kek" {
  capabilities = ["update"]
}
path "argin-signing-test/data/keys/*" {
  capabilities = ["create", "read", "update"]
}
# Intentionally no secret deletion/destroy, key export/backup/configuration,
# token creation, policy administration, mount management or wildcard Transit privileges.
