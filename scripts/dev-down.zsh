#!/bin/zsh
# Stop only processes launched by the ArginTaxPayer dev-up script.
emulate -L zsh
STATE="$HOME/.argintaxpayer-test"

stop_pid_tree() {
  local pid="$1"
  local child
  for child in ${(f)"$(pgrep -P "$pid" 2>/dev/null || true)"}; do
    stop_pid_tree "$child"
  done
  kill "$pid" 2>/dev/null || true
}

stop_service_listeners() {
  local port="$1"
  local marker="$2"
  local pid command
  for pid in ${(f)"$(lsof -nP -t -iTCP:$port -sTCP:LISTEN 2>/dev/null || true)"}; do
    command="$(ps -ww -p "$pid" -o command= 2>/dev/null || true)"
    if [[ "$command" == *"$marker"* ]]; then
      stop_pid_tree "$pid"
      print "Stopped $marker listener (PID $pid, port $port)"
    fi
  done
}

for service in web api adminer autossh; do
  pidfile="$STATE/$service.pid"
  if [[ -f "$pidfile" ]]; then
    pid="$(< "$pidfile")"
    if [[ "$pid" == <-> ]] && kill -0 "$pid" 2>/dev/null; then
      stop_pid_tree "$pid"
      print "Stopped $service (PID $pid)"
    fi
    rm -f "$pidfile"
  fi
done

# dotnet run and npm may orphan their actual listener after the launcher exits.
# Fall back to the exact project ports, but validate the process identity first.
stop_service_listeners 15089 'ArginTaxPayer.Api'
stop_service_listeners 3000 'next-server'
stop_service_listeners 15432 'autossh'
# Adminer forward uses the dedicated PID file; never kill an unrelated listener on 18081.

print 'All tracked ArginTaxPayer development launchers were signaled to stop.'
