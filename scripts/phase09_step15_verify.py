#!/usr/bin/env python3
"""Step15 local UI/source checks; no API, database or key custody call is made."""
import sys
sys.dont_write_bytecode = True
import phase09_verify as verify

if __name__ == '__main__':
    try:
        verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
        verify.run(['node', '--test', 'apps/web/tests/phase09-step15-key-ui.test.mjs'], verify.pure_environment())
        print('Step15 checks finished. No API, database, Vault or authority access was attempted.')
    except verify.SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
