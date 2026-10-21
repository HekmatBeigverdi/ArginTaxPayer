#!/usr/bin/env python3
"""Step09 local source checks; no database, provider or authority access is required."""
import argparse
import sys

sys.dont_write_bytecode = True
import phase09_verify as verify


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    args = parser.parse_args()
    verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
    verify.run(['bash', 'scripts/phase09-verify.sh', 'focused', '--filter',
                'FullyQualifiedName~Phase09OfflineSigning'], verify.pure_environment())
    print('Step09 checks finished. No database, provider or authority access was attempted.')


if __name__ == '__main__':
    try:
        main()
    except verify.SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
    except (EOFError, KeyboardInterrupt):
        print('\nCancelled.', file=sys.stderr)
        sys.exit(130)
