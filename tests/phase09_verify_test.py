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

if __name__ == '__main__':
    unittest.main()
