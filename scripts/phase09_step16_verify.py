#!/usr/bin/env python3
"""Step16 source and browser-UI regression checks; live HTTP/PG gates stay operator-run."""
import sys
sys.dont_write_bytecode = True
import phase09_verify as verify

if __name__ == '__main__':
    try:
        verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
        verify.run(['node', '--test', 'apps/web/tests/phase09-step15-key-ui.test.mjs',
                    'apps/web/tests/phase08-step19-security-regression.test.mjs'], verify.pure_environment())
        verify.run(['bash', 'scripts/phase09-verify.sh', 'focused', '--filter',
                    'FullyQualifiedName~Phase09SecurityRegression'], verify.pure_environment())
        print('Step16 checks finished. Live HTTP, PostgreSQL, Vault and authority gates were not attempted.')
    except verify.SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
