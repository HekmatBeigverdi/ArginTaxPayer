import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('schema_release', ROOT / 'ops/phase18-deploy/argin-taxpayer-release.py')
h = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(h)


class SchemaDeploymentTests(unittest.TestCase):
    expected = ['20261109010000_Previous', '20261112100000_Next']

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.release = self.root / ('a' * 40)
        schema = self.release / 'api/schema'
        schema.mkdir(parents=True)
        (schema / 'migrations.json').write_text(json.dumps(self.expected))
        (schema / 'migrations.sql').write_text('SELECT 1;')
        self.events = []
        self.applied = self.expected[:1]
        self.failure = None
        owner = self
        class Database:
            def identity(self): return 'argintaxpayer_prod|argin_prod_migrator|true'
            def applied(self): return owner.applied.copy()
            def backup(self, path):
                owner.events.append('backup')
                if owner.failure == 'backup': raise RuntimeError('backup failed')
            def apply(self, path):
                owner.events.append('apply')
                if owner.failure == 'apply': raise RuntimeError('migration failed')
                if owner.failure != 'verification': owner.applied = owner.expected.copy()
        self.db = Database()

    def run_migration(self):
        with patch.object(h, 'trusted'), patch.object(h, 'BACKUPS', self.root / 'backups'):
            return h.migrate_release(self.release, self.db,
                                     lambda: self.events.append('stop'),
                                     lambda: self.events.append('restart-current'))

    def test_backup_and_migration_precede_promotion_and_do_not_restart_new_app(self):
        self.assertTrue(self.run_migration())
        self.assertEqual(self.events, ['stop', 'backup', 'apply'])
        self.assertEqual(self.applied, self.expected)

    def test_matching_database_needs_no_backup_or_downtime(self):
        self.applied = self.expected.copy()
        self.assertFalse(self.run_migration())
        self.assertEqual(self.events, [])

    def test_unknown_or_holey_history_is_rejected_before_stopping(self):
        for history in ([self.expected[1]], ['20261113000000_Unknown']):
            self.applied = history
            with self.assertRaises(h.Refused): self.run_migration()
            self.assertEqual(self.events, [])

    def test_backup_or_sql_failure_restarts_current_without_promoting(self):
        for failure in ('backup', 'apply', 'verification'):
            with self.subTest(failure=failure):
                self.events = []
                self.failure = failure
                with self.assertRaises((RuntimeError, h.Refused)): self.run_migration()
                self.assertEqual(self.events[0], 'stop')
                self.assertEqual(self.events[-1], 'restart-current')
                if failure == 'backup': self.assertNotIn('apply', self.events)

    def test_partial_service_stop_failure_restarts_current(self):
        def stop():
            self.events.append('stop')
            raise RuntimeError('partial stop failure')
        with patch.object(h, 'trusted'), patch.object(h, 'BACKUPS', self.root / 'backups'):
            with self.assertRaises(RuntimeError):
                h.migrate_release(self.release, self.db, stop,
                                  lambda: self.events.append('restart-current'))
        self.assertEqual(self.events, ['stop', 'restart-current'])

    def test_wrong_database_identity_is_rejected_before_mutation(self):
        self.db.identity = lambda: 'argintaxpayer_test|postgres|false'
        with self.assertRaises(h.Refused): self.run_migration()
        self.assertEqual(self.events, [])

    def test_duplicate_or_unsorted_manifest_is_rejected(self):
        path = self.release / 'api/schema/migrations.json'
        for manifest in ([self.expected[0]] * 2, list(reversed(self.expected)), ['../escape']):
            path.write_text(json.dumps(manifest))
            with self.assertRaises(h.Refused): self.run_migration()
            self.assertEqual(self.events, [])

    def test_sql_is_restricted_and_client_cannot_write_cluster_files(self):
        database = h.MigrationDatabase({'CONTAINER': 'isolated-postgres',
                                       'PGPASSWORD': 'synthetic-private'})
        with patch.object(h.subprocess, 'run') as runner:
            database.apply(self.release / 'api/schema/migrations.sql')
            args, kwargs = runner.call_args
            self.assertIn('65534:65534', args[0])
            self.assertNotIn('synthetic-private', ' '.join(args[0]))
            payload = kwargs['input'].decode()
            self.assertRegex(payload, r'^\\restrict [a-f0-9]{64}\nSELECT 1;')
            self.assertIn('ON_ERROR_STOP=1', args[0])
            database.apply(self.release / 'api/schema/migrations.sql')
            self.assertNotEqual(kwargs['input'], runner.call_args.kwargs['input'])

    def test_versioned_build_info_is_accepted_but_wrong_sha_is_rejected(self):
        import hashlib
        for name in ('api.tar.gz', 'web.tar.gz'):
            (self.root / name).write_bytes(b'archive')
        digest = hashlib.sha256(b'archive').hexdigest()
        (self.root / 'SHA256SUMS').write_text(''.join(f'{digest}  {name}\n' for name in ('api.tar.gz', 'web.tar.gz')))
        (self.root / 'build-info.txt').write_text('version=1.0 (Build 1)\nsha='+'a'*40+'\n')
        h.verify_bundle(self.root, 'a'*40)
        with self.assertRaises(h.Refused): h.verify_bundle(self.root, 'b'*40)


if __name__ == '__main__':
    unittest.main()
