#!/bin/zsh
# Stop only processes launched by the ArginTaxPayer dev-up script.
emulate -L zsh
STATE="$HOME/.argintaxpayer-test"
for service in web api autossh; do
  pidfile="$STATE/$service.pid"
  if [[ -f "$pidfile" ]]; then
    pid="$(< "$pidfile")"
    if [[ "$pid" == <-> ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      print "Stopped $service (PID $pid)"
    fi
    rm -f "$pidfile"
  fi
done
print 'All tracked ArginTaxPayer development launchers were signaled to stop.'
