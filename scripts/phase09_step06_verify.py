#!/usr/bin/env python3
"""Step06 source checks; --postgres additionally writes synthetic fixtures to the existing test DB."""
import argparse
import getpass
import os
import sys

sys.dont_write_bytecode = True
import phase09_verify as verify


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--postgres', action='store_true')
    args = parser.parse_args()
    verify.run([sys.executable, 'tests/phase09_verify_test.py'], verify.pure_environment())
    verify.run(['bash', 'scripts/phase09-verify.sh', 'focused', '--filter',
                'FullyQualifiedName~Phase09Signing'], verify.pure_environment())
    if args.postgres:
        from phase09_apply_migration import read_terminal
        print('Writes synthetic generation journal/audit fixtures to argintaxpayer_test only.')
        print('No migration, real Vault, tax-authority call or private-key export is performed.')
        user = read_terminal('PostgreSQL user [argintaxpayer]: ') or 'argintaxpayer'
        password = getpass.getpass('Test database password: ')
        if not password:
            raise verify.SafeError('An explicit test password is required.')
        quote = lambda value: '"' + value.replace('"', '""') + '"'
        os.environ['ConnectionStrings__ArginTaxPayerDb'] = (
            'Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;'
            f'Username={quote(user)};Password={quote(password)};SSL Mode=Disable')
        env = verify.guard_database()
        verify.run(['bash', 'scripts/phase09-verify.sh', 'integration', '--filter',
                    'FullyQualifiedName~Phase09SigningGenerationPostgresTests'], env)
    else:
        print('Step06 PostgreSQL tests NOT RUN. Use --postgres with the existing test tunnel.')
    print('Step06 checks finished. Portal encoding, live custody and owner acceptance remain separate gates.')


if __name__ == '__main__':
    try:
        main()
    except verify.SafeError as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(2)
    except (EOFError, KeyboardInterrupt):
        print('\nCancelled.', file=sys.stderr)
        sys.exit(130)
