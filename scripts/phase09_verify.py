"""Cross-platform phase verifier. Never applies migrations or starts services."""
import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
BASE = 'b8f307ed06041d902aed9d8432d3b082e1cd714a'

class SafeError(Exception):
    """An operator-safe message that contains no configuration values."""


def parse_connection(text):
    # Strict Npgsql key/value subset. Reject unsupported settings rather than
    # silently letting psql validate a different destination than the test runner.
    aliases = {'host':'host', 'server':'host', 'port':'port', 'database':'database',
               'initial catalog':'database', 'username':'username', 'user id':'username',
               'password':'password', 'ssl mode':'sslmode', 'timeout':'timeout'}
    values = {}
    pos = 0
    while pos < len(text):
        if text[pos:].strip(' ;\t\r\n') == '':
            break
        match = re.match(r'\s*([^=;]+)\s*=\s*', text[pos:])
        if not match:
            raise SafeError('Invalid test connection format.')
        key = aliases.get(match[1].strip().lower())
        if key is None or key in values:
            raise SafeError('Unsupported or duplicate test connection option.')
        pos += match.end()
        if pos < len(text) and text[pos] in ('"', "'"):
            quote = text[pos]
            pos += 1
            out = []
            while True:
                if pos >= len(text):
                    raise SafeError('Invalid quoted connection value.')
                char = text[pos]
                pos += 1
                if char == quote:
                    if pos < len(text) and text[pos] == quote:
                        out.append(quote)
                        pos += 1
                    else:
                        break
                else:
                    out.append(char)
            value = ''.join(out)
            while pos < len(text) and text[pos].isspace():
                pos += 1
            if pos < len(text) and text[pos] != ';':
                raise SafeError('Invalid quoted connection separator.')
        else:
            end = text.find(';', pos)
            if end == -1:
                end = len(text)
            value = text[pos:end].strip()
            pos = end
        values[key] = value
        if pos < len(text):
            pos += 1
    if values.get('database') != 'argintaxpayer_test':
        raise SafeError('Refusing database other than argintaxpayer_test.')
    if values.get('host') not in ('127.0.0.1', 'localhost', '::1'):
        raise SafeError('Use a loopback PostgreSQL endpoint or approved local tunnel.')
    if not values.get('username') or not values.get('password'):
        raise SafeError('Explicit test database username/password required.')
    port = values.setdefault('port', '5432')
    if not port.isascii() or not port.isdigit() or not 1 <= int(port) <= 65535:
        raise SafeError('Invalid test database port.')
    mode = values.setdefault('sslmode', 'Prefer').lower().replace(' ', '-')
    if mode not in ('disable', 'allow', 'prefer', 'require', 'verify-ca', 'verify-full'):
        raise SafeError('Unsupported SSL mode.')
    values['sslmode'] = mode
    return values


def pure_environment():
    env = {k:v for k,v in os.environ.items()
           if not k.lower().startswith('connectionstrings__') and not k.startswith('PG')}
    env['RUN_ARGINTAXPAYER_PG_INTEGRATION'] = '0'
    env['Ghasedak__EnableLiveSending'] = 'false'
    return env


def guard_database():
    connection = os.environ.get('ConnectionStrings__ArginTaxPayerDb', '')
    values = parse_connection(connection)
    env = pure_environment()
    pg = {'PGHOST':values['host'], 'PGPORT':values['port'],
          'PGDATABASE':values['database'], 'PGUSER':values['username'],
          'PGPASSWORD':values['password'], 'PGSSLMODE':values['sslmode'],
          'PGCONNECT_TIMEOUT':'10', 'PGPASSFILE':os.devnull}
    env.update(pg)
    try:
        result = subprocess.run(['psql', '-X', '-w', '-A', '-t', '-q', '-v', 'ON_ERROR_STOP=1',
                                 '-c', 'SELECT current_database();'], env=env,
                                capture_output=True, text=True, timeout=15)
    except (OSError, subprocess.TimeoutExpired):
        raise SafeError('Test database guard unavailable; check psql and private test configuration.') from None
    if result.returncode or result.stdout.strip() != 'argintaxpayer_test':
        raise SafeError('Actual database guard failed; no integration tests started.')
    # psql variables are not passed to the .NET runner; it gets only the checked
    # Npgsql string, not an alternative PG environment.
    env = pure_environment()
    env['ConnectionStrings__ArginTaxPayerDb'] = connection
    env['RUN_ARGINTAXPAYER_PG_INTEGRATION'] = '1'
    print('Database guard: argintaxpayer_test confirmed (read-only query).')
    return env


def require_discovered_tests(listing):
    if not re.search(r'^\s+ArginTaxPayer\.Tests\.', listing, re.M):
        raise SafeError('No matching .NET tests discovered; refusing empty success.')


def run(args, env=None, capture=False):
    try:
        result = subprocess.run(args, cwd=ROOT, env=env, text=True,
                                stdout=subprocess.PIPE if capture else None,
                                stderr=subprocess.PIPE if capture else None)
    except OSError:
        raise SafeError('Required command unavailable: ' + args[0]) from None
    if result.returncode:
        # Never echo captured database/provider/configuration errors.
        raise SafeError('Verification command failed: ' + args[0])
    return result.stdout if capture else ''


def preflight():
    sha = run(['git', 'rev-parse', 'HEAD'], capture=True).strip()
    run(['git', 'merge-base', '--is-ancestor', BASE, 'HEAD'])
    if run(['git', 'status', '--porcelain'], capture=True).strip():
        raise SafeError('Working tree must be clean before verification; commit intended changes first.')
    doc = (ROOT / 'docs/phases/phase-09.md').read_text()
    section = doc.split('## 5.')[1].split('## 6.')[0]
    if re.findall(r'^\| (\d{2}) \|', section, re.M) != [f'{i:02}' for i in range(1,19)]:
        raise SafeError('Expected the approved ordered 18-step plan.')
    print('Commit: ' + sha)
    print('Branch: ' + run(['git', 'branch', '--show-current'], capture=True).strip())
    print('Preflight: baseline ancestry, clean checkout and 18-step plan confirmed.')
    return sha


def dotnet_tests(env, test_filter=None):
    run(['dotnet', 'restore', 'ArginTaxPayer.sln'], env)
    run(['dotnet', 'build', 'ArginTaxPayer.sln', '-c', 'Release', '--no-restore'], env)
    args = ['dotnet', 'test', 'ArginTaxPayer.sln', '-c', 'Release', '--no-build']
    if test_filter:
        args += ['--filter', test_filter]
    require_discovered_tests(run(args + ['--list-tests'], env, capture=True))
    with tempfile.TemporaryDirectory(prefix='argin-phase09-results-') as results:
        run(args + ['--logger', 'trx;LogFileName=phase09.trx', '--results-directory', results], env)
        reports = list(Path(results).rglob('*.trx'))
        counts = [node.attrib for report in reports for node in ET.parse(report).iter()
                  if node.tag.rsplit('}',1)[-1] == 'Counters']
        if not counts or sum(int(c.get('executed',0)) for c in counts) == 0:
            raise SafeError('No executed tests in TRX; refusing empty success.')
        for c in counts:
            print('TRX: total={total}, executed={executed}, passed={passed}, failed={failed}, notExecuted={notExecuted}'.format(
                **{k:c.get(k,'0') for k in ('total','executed','passed','failed','notExecuted')}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['preflight','pure','focused','integration','full'])
    parser.add_argument('--filter', dest='test_filter')
    args = parser.parse_args()
    if args.mode == 'focused' and not args.test_filter:
        raise SafeError('focused requires --filter with an existing test class.')
    before = preflight()
    if args.mode in ('integration','full'):
        env = guard_database()  # Must happen before build or tests.
    else:
        env = pure_environment()
        print('PostgreSQL integration DISABLED; opt-in early-return tests do not prove DB acceptance.')
    if args.mode != 'preflight':
        dotnet_tests(env, args.test_filter)
    if args.mode in ('pure','full'):
        run(['npm','--prefix','apps/web','ci','--no-audit','--no-fund'], env)
        run(['npm','--prefix','apps/web','test'], env)
        run(['npm','--prefix','apps/web','run','typecheck'], env)
        run(['npm','--prefix','apps/web','run','build'], env)
    after = preflight()
    if before != after:
        raise SafeError('Commit changed during verification.')
    print('Verification finished. Browser, live authority, restore and owner approval are separate.')

if __name__ == '__main__':
    try:
        main()
    except SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
