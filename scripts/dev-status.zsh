#!/bin/zsh
# Read-only local status; no secrets or complete connection strings printed.
emulate -L zsh
STATE="$HOME/.argintaxpayer-test"
for spec in "15432:PostgreSQL tunnel" "18081:Adminer tunnel" "15089:.NET API" "3000:Next.js"; do
  port="${spec%%:*}"
  label="${spec#*:}"
  if lsof -nP -iTCP:$port -sTCP:LISTEN >/dev/null 2>&1; then
    print "RUNNING $label (127.0.0.1:$port)"
  else
    print "STOPPED $label (port $port)"
  fi
done
if curl --noproxy '*' -fsS http://127.0.0.1:15089/health/ready > /dev/null 2>&1; then
  print 'READY PostgreSQL through .NET API'
else
  print 'NOT READY PostgreSQL or API'
fi
if curl --noproxy '*' -fsS http://127.0.0.1:15089/health/dev-identity 2>/dev/null | grep -q '"otpPepperConfigured":true'; then
  print 'READY OTP environment'
else
  print 'NOT READY OTP environment'
fi
if curl --noproxy "*" -fsS --max-time 3 http://127.0.0.1:18081/ > /dev/null 2>&1; then
  print "READY Adminer at http://127.0.0.1:18081"
else
  print "NOT READY Adminer (VPS service or tunnel unavailable)"
fi
print "Log files: $STATE/api.log, $STATE/web.log, $STATE/autossh.log, $STATE/adminer.log"
