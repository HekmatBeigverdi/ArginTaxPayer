#!/usr/bin/env python3
"""Step12 local source checks; no database, Vault or authority access is required."""
import sys
sys.dont_write_bytecode = True
import phase09_verify as verify

if __name__ == '__main__':
    try:
        verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
        verify.run(['bash', 'scripts/phase09-verify.sh', 'focused', '--filter',
                    'FullyQualifiedName~Phase09SigningRecovery'], verify.pure_environment())
        print('Step12 checks finished. No database, Vault or authority access was attempted.')
    except verify.SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
