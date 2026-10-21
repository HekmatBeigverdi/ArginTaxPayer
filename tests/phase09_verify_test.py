"""Safety tests for the verifier; no database or SDK network access."""
import importlib.util
from pathlib import Path
import os
import sys
sys.dont_write_bytecode = True
import unittest
from unittest.mock import patch
import subprocess

ROOT = Path(__file__).resolve().parents[1]

class VerifierTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location('phase09_verify', ROOT / 'scripts/phase09_verify.py')
        cls.v = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.v)

    def test_exact_database_required(self):
        for name in ('argintaxpayer', 'argintaxpayer_test_copy', 'ARGINTAXPAYER_TEST'):
            with self.assertRaises(self.v.SafeError):
                self.v.parse_connection(f'Host=127.0.0.1;Database={name};Username=u;Password=private')

    def test_duplicate_alias_and_unknown_option_rejected(self):
        for suffix in (';Initial Catalog=argintaxpayer_test', ';Options=-c search_path=public', ';Service=prod'):
            with self.assertRaises(self.v.SafeError):
                self.v.parse_connection('Host=127.0.0.1;Database=argintaxpayer_test;Username=u;Password=x' + suffix)

    def test_quoted_password_and_custom_port(self):
        result = self.v.parse_connection('Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=u;Password="a;b""c";SSL Mode=Disable')
        self.assertEqual(result['password'], 'a;b"c')
        self.assertEqual(result['port'], '15432')

    def test_non_loopback_and_invalid_ports_rejected(self):
        for host, port in [('example.com', '5432'), ('127.0.0.1', '0'), ('127.0.0.1', '65536')]:
            with self.assertRaises(self.v.SafeError):
                self.v.parse_connection(f'Host={host};Port={port};Database=argintaxpayer_test;Username=u;Password=x')

    def test_guard_checks_actual_database_without_password_argument(self):
        cs = 'Host=127.0.0.1;Database=argintaxpayer_test;Username=u;Password=secret-canary'
        with patch.dict(os.environ, {'ConnectionStrings__ArginTaxPayerDb': cs, 'PGSERVICE': 'prod'}), patch.object(self.v.subprocess, 'run') as run:
            run.return_value = subprocess.CompletedProcess([], 0, 'argintaxpayer_test\n', '')
            env = self.v.guard_database()
            args, kwargs = run.call_args
            self.assertNotIn('secret-canary', str(args))
            self.assertNotIn('PGSERVICE', kwargs['env'])
            self.assertIn('current_database()', str(args))
            self.assertEqual(env['RUN_ARGINTAXPAYER_PG_INTEGRATION'], '1')

    def test_guard_rejects_actual_database_mismatch_and_redacts_provider_error(self):
        cs = 'Host=127.0.0.1;Database=argintaxpayer_test;Username=u;Password=secret-canary'
        for code, output in [(0, 'prod'), (1, 'secret-canary')]:
            with patch.dict(os.environ, {'ConnectionStrings__ArginTaxPayerDb': cs}), patch.object(self.v.subprocess, 'run') as run:
                run.return_value = subprocess.CompletedProcess([], code, output, 'secret-canary')
                with self.assertRaises(self.v.SafeError) as error:
                    self.v.guard_database()
                self.assertNotIn('secret-canary', str(error.exception))

    def test_pure_environment_removes_inherited_db_and_live_provider(self):
        with patch.dict(os.environ, {'RUN_ARGINTAXPAYER_PG_INTEGRATION': '1', 'ConnectionStrings__ArginTaxPayerDb':'prod', 'PGDATABASE':'prod'}):
            env = self.v.pure_environment()
        self.assertNotIn('ConnectionStrings__ArginTaxPayerDb', env)
        self.assertNotIn('PGDATABASE', env)
        self.assertEqual(env['RUN_ARGINTAXPAYER_PG_INTEGRATION'], '0')
        self.assertEqual(env['Ghasedak__EnableLiveSending'], 'false')

    def test_empty_discovery_is_not_success(self):
        for listing in ('No test matches', 'The following Tests are available:\n'):
            with self.assertRaises(self.v.SafeError):
                self.v.require_discovered_tests(listing)
        self.v.require_discovered_tests('The following Tests are available:\n    ArginTaxPayer.Tests.SomeTest\n')

    def test_import_pg_run_requires_all_selected_tests_without_skips(self):
        selectors = {
            'FullyQualifiedName~Phase09SigningImportPostgresTests': 5,
            'FullyQualifiedName~Phase09SigningGenerationPostgresTests|FullyQualifiedName~Phase09SigningImportPostgresTests': 10,
        }
        for selector, number in selectors.items():
            for total, executed, passed, skipped in ((number, number, number, 0),
                    (number, number - 1, number - 1, 1), (number - 1, number - 1, number - 1, 0)):
                with self.subTest(selector=selector, total=total, executed=executed):
                    def run(args, env=None, capture=False):
                        if '--list-tests' in args:
                            return '    ArginTaxPayer.Tests.Phase09SigningImportPostgresTests.Test'
                        if '--results-directory' in args:
                            folder = Path(args[args.index('--results-directory') + 1])
                            (folder / 'phase09.trx').write_text(
                                f'<TestRun><Counters total="{total}" executed="{executed}" passed="{passed}" '
                                f'failed="0" notExecuted="{skipped}" /></TestRun>')
                        return ''
                    with patch.object(self.v, 'run', side_effect=run):
                        if (total, executed, passed, skipped) == (number, number, number, 0):
                            self.v.dotnet_tests({'RUN_ARGINTAXPAYER_PG_INTEGRATION': '1'}, selector)
                        else:
                            with self.assertRaises(self.v.SafeError):
                                self.v.dotnet_tests({'RUN_ARGINTAXPAYER_PG_INTEGRATION': '1'}, selector)

    def test_generation_pg_run_requires_all_five_tests_without_skips(self):
        for total, executed, passed, skipped in ((5, 5, 5, 0), (5, 4, 4, 1), (4, 4, 4, 0)):
            with self.subTest(total=total, executed=executed):
                def run(args, env=None, capture=False):
                    if '--list-tests' in args:
                        return '    ArginTaxPayer.Tests.Phase09SigningGenerationPostgresTests.Test'
                    if '--results-directory' in args:
                        folder = Path(args[args.index('--results-directory') + 1])
                        (folder / 'phase09.trx').write_text(
                            f'<TestRun><Counters total="{total}" executed="{executed}" passed="{passed}" '
                            f'failed="0" notExecuted="{skipped}" /></TestRun>')
                    return ''
                with patch.object(self.v, 'run', side_effect=run):
                    if (total, executed, passed, skipped) == (5, 5, 5, 0):
                        self.v.dotnet_tests({'RUN_ARGINTAXPAYER_PG_INTEGRATION': '1'},
                                            'FullyQualifiedName~Phase09SigningGenerationPostgresTests')
                    else:
                        with self.assertRaises(self.v.SafeError):
                            self.v.dotnet_tests({'RUN_ARGINTAXPAYER_PG_INTEGRATION': '1'},
                                                'FullyQualifiedName~Phase09SigningGenerationPostgresTests')

    def test_pure_run_disables_inherited_live_vault_configuration(self):
        with patch.dict(os.environ, {
            'RUN_ARGINTAXPAYER_VAULT_INTEGRATION': '1',
            'PHASE09_VAULT_ADDRESS': 'https://production.invalid',
            'SigningKeyProtection__Enabled': 'true',
            'SigningKeyGeneration__Enabled': 'true',
            'SigningKeyImport__Enabled': 'true',
        }):
            env = self.v.pure_environment()
        self.assertEqual(env.get('RUN_ARGINTAXPAYER_VAULT_INTEGRATION'), '0')
        self.assertNotIn('PHASE09_VAULT_ADDRESS', env)
        self.assertNotIn('SigningKeyProtection__Enabled', env)
        self.assertNotIn('SigningKeyGeneration__Enabled', env)
        self.assertNotIn('SigningKeyImport__Enabled', env)

    def test_vault_guard_accepts_only_isolated_tls_endpoint_and_token_file_path(self):
        valid = {'PHASE09_VAULT_ADDRESS': 'https://localhost:18200/',
                 'PHASE09_VAULT_TOKEN_FILE': '/private/agent-token'}
        for address in ('http://localhost:18200/', 'https://production.invalid:18200/',
                        'https://localhost:8200/', 'https://u:p@localhost:18200/',
                        'https://localhost:18200/prefix', 'https://localhost:18200/?x=1'):
            with patch.dict(os.environ, dict(valid, PHASE09_VAULT_ADDRESS=address)):
                with self.assertRaises(self.v.SafeError):
                    self.v.guard_vault()
        with patch.dict(os.environ, valid):
            env = self.v.guard_vault()
        self.assertEqual(env['RUN_ARGINTAXPAYER_VAULT_INTEGRATION'], '1')
        self.assertEqual(env['PHASE09_VAULT_ADDRESS'], valid['PHASE09_VAULT_ADDRESS'])
        self.assertEqual(env['RUN_ARGINTAXPAYER_PG_INTEGRATION'], '0')

    def test_step04_pg_run_requires_all_six_passed_without_skips(self):
        # Simulate only the external dotnet process; exercise the real TRX gate.
        for total, executed, passed, failed, skipped in (
            (6, 6, 6, 0, 0), (6, 5, 5, 0, 1), (5, 5, 5, 0, 0),
            (6, 6, 5, 1, 0), (7, 7, 7, 0, 0),
        ):
            with self.subTest(total=total, executed=executed, passed=passed):
                def run(args, env=None, capture=False):
                    if '--list-tests' in args:
                        return '    ArginTaxPayer.Tests.Phase09SigningOperationPostgresTests.Test'
                    if '--results-directory' in args:
                        folder = Path(args[args.index('--results-directory') + 1])
                        (folder / 'phase09.trx').write_text(
                            '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                            f'<ResultSummary><Counters total="{total}" executed="{executed}" '
                            f'passed="{passed}" failed="{failed}" notExecuted="{skipped}" />'
                            '</ResultSummary></TestRun>')
                    return ''
                env = {'RUN_ARGINTAXPAYER_PG_INTEGRATION': '1'}
                with patch.object(self.v, 'run', side_effect=run):
                    if (total, executed, passed, failed, skipped) == (6, 6, 6, 0, 0):
                        self.v.dotnet_tests(env, 'FullyQualifiedName~Phase09SigningOperationPostgresTests')
                    else:
                        with self.assertRaises(self.v.SafeError):
                            self.v.dotnet_tests(env, 'FullyQualifiedName~Phase09SigningOperationPostgresTests')

    def test_vault_run_refuses_skipped_or_unexpected_test_count(self):
        for total, executed, passed, skipped in ((1, 1, 1, 0), (1, 0, 0, 1), (2, 2, 2, 0)):
            with self.subTest(total=total, executed=executed):
                def run(args, env=None, capture=False):
                    if '--list-tests' in args:
                        return '    ArginTaxPayer.Tests.Phase09SigningVaultIntegrationTests.Test'
                    if '--results-directory' in args:
                        folder = Path(args[args.index('--results-directory') + 1])
                        (folder / 'phase09.trx').write_text(
                            f'<TestRun><Counters total="{total}" executed="{executed}" passed="{passed}" '
                            f'failed="0" notExecuted="{skipped}" /></TestRun>')
                    return ''
                with patch.object(self.v, 'run', side_effect=run):
                    if (total, executed, passed, skipped) == (1, 1, 1, 0):
                        self.v.dotnet_tests({'RUN_ARGINTAXPAYER_VAULT_INTEGRATION': '1'},
                                            'FullyQualifiedName~Phase09SigningVaultIntegrationTests')
                    else:
                        with self.assertRaises(self.v.SafeError):
                            self.v.dotnet_tests({'RUN_ARGINTAXPAYER_VAULT_INTEGRATION': '1'},
                                                'FullyQualifiedName~Phase09SigningVaultIntegrationTests')

if __name__ == '__main__':
    unittest.main()
