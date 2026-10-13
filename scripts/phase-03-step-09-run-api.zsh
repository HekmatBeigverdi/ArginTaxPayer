#!/bin/zsh
# Step 09 — Start the *same* Development API process with its private Ghasedak
# environment. Never echo credentials, recipient numbers or OTP secrets.
set -e
repo_root="${0:A:h:h}"
cd "$repo_root"
settings="$repo_root/.env.ghasedak.local"

if [[ ! -f "$settings" ]]; then
  print -u2 "FAIL: .env.ghasedak.local is missing in the repository root."
  exit 1
fi

if [[ -n "$(lsof -nP -t -iTCP:15089 -sTCP:LISTEN 2>/dev/null)" ]]; then
  print -u2 "FAIL: port 15089 is already occupied by another API process."
  print -u2 "Inspect: lsof -nP -iTCP:15089 -sTCP:LISTEN"
  print -u2 "Stop the OLD API in its terminal (Ctrl+C), then retry."
  exit 1
fi

if [[ -z "${ConnectionStrings__ArginTaxPayerDb:-}" ]]; then
  read -rs 'db_password?Test database password: '; print
  export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${db_password};SSL Mode=Prefer"
  unset db_password
fi
if [[ "${ConnectionStrings__ArginTaxPayerDb}" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 "FAIL: connection string does not explicitly target argintaxpayer_test."
  exit 1
fi

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true

pepper_file="$HOME/.argintaxpayer-test/otp-pepper"
if [[ ! -s "$pepper_file" ]]; then
  print -u2 "FAIL: missing stable OTP pepper file at ~/.argintaxpayer-test/otp-pepper"
  print -u2 'Create once: mkdir -p ~/.argintaxpayer-test && chmod 700 ~/.argintaxpayer-test && openssl rand -hex 32 > ~/.argintaxpayer-test/otp-pepper && chmod 600 ~/.argintaxpayer-test/otp-pepper'
  exit 1
fi
export Identity__OtpPepper="$(< "$pepper_file")"

# Private operator-supplied shell assignments. File must be trusted, ignored
# by git and accessible only by its owner.
chmod 600 "$settings"
set -a
source "$settings"
set +a

# A local-file sender must not shadow the real provider in this runbook.
if [[ "${Identity__OtpDeliveryMode:-}" == "LocalFile" ]]; then
  print -u2 "FAIL: Identity__OtpDeliveryMode=LocalFile; change it to Ghasedak for live provider testing."
  exit 1
fi
unset Identity__OtpLocalFile

print 'API preflight (values are never printed):'
[[ -n "${Ghasedak__ApiKey:-}" ]] && print '  API key: configured' || print '  API key: MISSING'
[[ "${Ghasedak__TemplateName:-}" == "TaxPayerActivationCode" ]] && print '  Template: configured' || print '  Template: INVALID'
[[ -n "${Ghasedak__TestRecipientMobile:-}" ]] && print '  Test recipient: configured' || print '  Test recipient: MISSING'
[[ ${#Identity__OtpPepper} -ge 32 ]] && print '  OTP pepper: configured' || print '  OTP pepper: INVALID'
print "  Live SMS sending: ${Ghasedak__EnableLiveSending:-false}"
if [[ -z "${Ghasedak__ApiKey:-}" || "${Ghasedak__TemplateName:-}" != "TaxPayerActivationCode" || -z "${Ghasedak__TestRecipientMobile:-}" || ${#Identity__OtpPepper} -lt 32 ]]; then
  print -u2 'FAIL: fix the missing settings in .env.ghasedak.local before launching.'
  exit 1
fi

print 'Starting the Development API on 127.0.0.1:15089 with these settings.'
exec dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
