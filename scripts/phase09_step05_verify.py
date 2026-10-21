#!/usr/bin/env python3
"""Run Step05 checks without a DB; --vault additionally opts into an isolated synthetic Vault test."""
import argparse
import sys

sys.dont_write_bytecode = True
import phase09_verify as verify


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', action='store_true')
    args = parser.parse_args()
    verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
    verify.run(['bash', 'scripts/phase09-verify.sh', 'focused',
                '--filter', 'FullyQualifiedName~Phase09Signing'], verify.pure_environment())
    if args.vault:
        env = verify.guard_vault()
        verify.run(['bash', 'scripts/phase09-verify.sh', 'vault'], env)
    else:
        print('Live Vault test NOT RUN. Configure the isolated TLS test provider before using --vault.')
    print('Step05 checks finished; owner acceptance and live-provider evidence remain separate.')


if __name__ == '__main__':
    try:
        main()
    except verify.SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
    except KeyboardInterrupt:
        print('\nCancelled.', file=sys.stderr)
        sys.exit(130)
