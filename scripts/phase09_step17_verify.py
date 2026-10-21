#!/usr/bin/env python3
"""Step17 acceptance preflight; it does not mutate databases or deploy services."""
import os
import subprocess
import sys
sys.dont_write_bytecode = True
import phase09_verify as verify

def main():
    verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
    verify.run(['node', '--test', 'apps/web/tests/phase09-step15-key-ui.test.mjs',
                'apps/web/tests/phase08-step19-security-regression.test.mjs'], verify.pure_environment())
    sha = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    expected = os.environ.get('PHASE09_EXPECTED_SHA')
    if expected and sha != expected:
        raise verify.SafeError(f'commit mismatch: expected {expected}, got {sha}')
    print('Step17 source preflight passed.')
    print('Commit: ' + sha)
    print('Run the macOS and Ubuntu operator commands in docs/phases/phase-09.md for build, HTTP/PG, restore and browser evidence.')
    print('No migration, deployment or release action was performed.')

if __name__ == '__main__':
    try:
        main()
    except (verify.SafeError, subprocess.SubprocessError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
