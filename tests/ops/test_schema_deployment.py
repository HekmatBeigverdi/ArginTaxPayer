import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from contextlib import redirect_stdout
from types import SimpleNamespace
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
        self.database_calls = []
        self.applied = self.expected[:1]
        self.failure = None
        owner = self
        class Database:
            def identity(self): owner.database_calls.append('identity'); return 'argintaxpayer_prod|argin_prod_migrator|true'
            def applied(self): owner.database_calls.append('applied'); return owner.applied.copy()
            def backup(self, path):
                owner.database_calls.append('backup')
                owner.events.append('backup')
                if owner.failure == 'backup': raise RuntimeError('backup failed')
            def apply(self, path):
                owner.database_calls.append('apply')
                owner.events.append('apply')
                if owner.failure == 'apply': raise RuntimeError('migration failed')
                if owner.failure != 'verification': owner.applied = owner.expected.copy()
        self.db = Database()

    def reviewed_recovery_fixture(self):
        state_dir = self.root / 'backup-controller'
        state_dir.mkdir(mode=0o700, exist_ok=True)
        state_dir.chmod(0o700)
        stage_id = '3236fe03-f4b8-4f76-a12e-91b18b74a40d'
        database = 'recovery_abcdef123456'
        selection = self.root / 'backup-db.env'
        selection.write_text(f'Backup__ActiveDatabase={database}\n')
        selection.chmod(0o640)
        (state_dir / 'recovery-state.json').write_text(json.dumps({
            'recoveryMode': False, 'stageId': stage_id, 'activeDatabase': database,
        }))
        (state_dir / 'restore-stage.json').write_text(json.dumps({
            'stageId': stage_id, 'databaseName': database, 'status': 'staged',
        }))
        for path in state_dir.iterdir():
            path.chmod(0o600)
        return selection, state_dir, database

    def review_selection(self, selection, state_dir):
        with patch.object(h, 'ACTIVE_BACKUP_DB', selection), \
             patch.object(h, 'RECOVERY_STATE_DIR', state_dir), \
             patch.object(h, 'ROOT_UID', os.getuid()), \
             patch.object(h, 'trusted'), \
             patch.object(h.pwd, 'getpwnam', return_value=SimpleNamespace(pw_uid=os.getuid())):
            return h.reviewed_recovery_selection()

    def test_no_active_restore_selection_preserves_normal_deploy_path(self):
        with patch.object(h, 'ACTIVE_BACKUP_DB', self.root / 'missing.env'):
            self.assertIsNone(h.reviewed_recovery_selection())

    def test_explicitly_reviewed_selection_requires_matching_recovery_and_stage(self):
        selection, state_dir, database = self.reviewed_recovery_fixture()
        self.assertEqual(self.review_selection(selection, state_dir), database)

    def test_root_preflight_command_reports_the_reviewed_database(self):
        selection, state_dir, database = self.reviewed_recovery_fixture()
        output = io.StringIO()
        with patch.object(h, 'ACTIVE_BACKUP_DB', selection), \
             patch.object(h, 'RECOVERY_STATE_DIR', state_dir), \
             patch.object(h, 'ROOT_UID', os.getuid()), \
             patch.object(h, 'trusted'), patch.object(h.os, 'geteuid', return_value=0), \
             patch.object(h.pwd, 'getpwnam', return_value=SimpleNamespace(pw_uid=os.getuid())), \
             redirect_stdout(output):
            h.main(['--check-recovery-selection'])
        self.assertEqual(output.getvalue(), f'[PASS] Restored database selection is reviewed: {database}\n')

    def test_non_root_cannot_run_recovery_selection_preflight(self):
        with patch.object(h.os, 'geteuid', return_value=501), \
             self.assertRaisesRegex(h.Refused, 'root-owned deployment preflight'):
            h.main(['--check-recovery-selection'])

    def test_recovery_mode_still_blocks_deploy(self):
        selection, state_dir, _ = self.reviewed_recovery_fixture()
        state_path = state_dir / 'recovery-state.json'
        state = json.loads(state_path.read_text())
        state['recoveryMode'] = True
        state_path.write_text(json.dumps(state))
        with self.assertRaises(h.Refused):
            self.review_selection(selection, state_dir)

    def test_recovery_state_must_be_owned_by_backup_service(self):
        selection, state_dir, _ = self.reviewed_recovery_fixture()
        with patch.object(h, 'ACTIVE_BACKUP_DB', selection), \
             patch.object(h, 'RECOVERY_STATE_DIR', state_dir), \
             patch.object(h, 'ROOT_UID', os.getuid()), \
             patch.object(h, 'trusted'), \
             patch.object(h.pwd, 'getpwnam', return_value=SimpleNamespace(pw_uid=os.getuid() + 1)):
            with self.assertRaises(h.Refused): h.reviewed_recovery_selection()

    def test_mismatched_recovery_database_or_stage_is_rejected(self):
        selection, state_dir, _ = self.reviewed_recovery_fixture()
        state_path = state_dir / 'recovery-state.json'
        stage_path = state_dir / 'restore-stage.json'
        for path, field, value in (
            (state_path, 'activeDatabase', 'recovery_111111111111'),
            (stage_path, 'stageId', 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'),
            (stage_path, 'databaseName', 'recovery_222222222222'),
        ):
            with self.subTest(field=field):
                original_state = json.loads(state_path.read_text())
                original_stage = json.loads(stage_path.read_text())
                changed = json.loads(path.read_text())
                changed[field] = value
                path.write_text(json.dumps(changed))
                with self.assertRaises(h.Refused): self.review_selection(selection, state_dir)
                state_path.write_text(json.dumps(original_state)); state_path.chmod(0o600)
                stage_path.write_text(json.dumps(original_stage)); stage_path.chmod(0o600)

    def test_malformed_selection_and_symlinked_state_are_rejected(self):
        selection, state_dir, _ = self.reviewed_recovery_fixture()
        selection.write_text('Backup__ActiveDatabase=recovery_abcdef123456\nUnexpected=value\n')
        with self.assertRaisesRegex(h.Refused, 'Invalid restored database selection'):
            self.review_selection(selection, state_dir)
        selection.write_text('Backup__ActiveDatabase=recovery_abcdef123456\n')
        state_path = state_dir / 'recovery-state.json'
        content = state_path.read_text()
        state_path.unlink()
        state_path.symlink_to(self.root / 'recovery-state-target.json')
        (self.root / 'recovery-state-target.json').write_text(content)
        with self.assertRaises(h.Refused): self.review_selection(selection, state_dir)

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

    def test_active_restore_selection_blocks_deploy_before_database_access(self):
        selection = self.root / 'backup-db.env'
        selection.write_text('Backup__ActiveDatabase=recovery_abcdef123456\n')
        with patch.object(h, 'ACTIVE_BACKUP_DB', selection):
            with self.assertRaises(h.Refused):
                self.run_migration()
        self.assertEqual(self.events, [])

    def current_release_with_migrations(self):
        current = self.root / 'current-release'
        schema = current / 'api/schema'
        schema.mkdir(parents=True)
        (schema / 'migrations.json').write_bytes((self.release / 'api/schema/migrations.json').read_bytes())
        (schema / 'migrations.sql').write_bytes((self.release / 'api/schema/migrations.sql').read_bytes())
        return current

    def run_reviewed_release(self, current_release):
        selection, state_dir, _ = self.reviewed_recovery_fixture()
        with patch.object(h, 'ACTIVE_BACKUP_DB', selection), \
             patch.object(h, 'RECOVERY_STATE_DIR', state_dir), \
             patch.object(h, 'ROOT_UID', os.getuid()), \
             patch.object(h, 'trusted'), \
             patch.object(h.pwd, 'getpwnam', return_value=SimpleNamespace(pw_uid=os.getuid())):
            return h.migrate_release(self.release, self.db,
                                     lambda: self.events.append('stop'),
                                     lambda: self.events.append('restart-current'),
                                     current_release=current_release)

    def test_reviewed_recovery_with_identical_migrations_skips_database(self):
        current = self.current_release_with_migrations()
        self.assertFalse(self.run_reviewed_release(current))
        self.assertEqual(self.events, [])
        self.assertEqual(self.database_calls, [])

    def test_reviewed_recovery_refuses_changed_manifest_or_sql_before_stopping(self):
        current = self.current_release_with_migrations()
        candidate_manifest = self.release / 'api/schema/migrations.json'
        candidate_sql = self.release / 'api/schema/migrations.sql'
        for path, changed in (
            (candidate_manifest, json.dumps(self.expected + ['20261113100000_New']).encode()),
            (candidate_sql, b'SELECT 2;'),
        ):
            with self.subTest(path=path.name):
                original = path.read_bytes()
                path.write_bytes(changed)
                with self.assertRaisesRegex(h.Refused, 'migration target'):
                    self.run_reviewed_release(current)
                self.assertEqual(self.events, [])
                self.assertEqual(self.database_calls, [])
                path.write_bytes(original)

    def test_reviewed_release_migrations_require_exact_approval_and_active_database(self):
        current = self.current_release_with_migrations()
        self.applied = self.expected.copy()
        self.expected = self.expected + [
            '20261123100000_AddInvoiceBusinessSerials',
            '20261123133602_AddCatalogVatRatePercent',
        ]
        (self.release / 'api/schema/migrations.json').write_text(json.dumps(self.expected))
        approval = self.root / 'recovery-migration-approved'
        self.db.identity = lambda: 'recovery_abcdef123456|argin_prod_app|true'
        with patch.object(h, 'RECOVERY_MIGRATION_APPROVAL', approval), \
             patch.object(h, 'BACKUPS', self.root / 'backups'):
            for target, sha in [('recovery_000000000000', self.release.name),
                                ('recovery_abcdef123456', 'b' * 40)]:
                approval.write_text(f'{target}\n{sha}\n')
                approval.chmod(0o600)
                with self.assertRaisesRegex(h.Refused, 'approval does not match'):
                    self.run_reviewed_release(current)
                self.assertEqual(self.database_calls, [])
            approval.write_text(f'recovery_abcdef123456\n{self.release.name}\n')
            self.assertTrue(self.run_reviewed_release(current))
            self.assertEqual(self.events, ['stop', 'backup', 'apply'])
            self.events.clear()
            self.assertFalse(self.run_reviewed_release(current))
            self.assertEqual(self.events, [])

    def test_invoice_deletion_migration_is_approved_only_after_prior_reviewed_delta(self):
        self.expected = self.expected + [
            '20261123100000_AddInvoiceBusinessSerials',
            '20261123133602_AddCatalogVatRatePercent',
        ]
        (self.release / 'api/schema/migrations.json').write_text(json.dumps(self.expected))
        current = self.current_release_with_migrations()
        self.applied = self.expected.copy()
        self.expected = self.expected + ['20261123140000_AddInvoiceDeletionMarkers']
        (self.release / 'api/schema/migrations.json').write_text(json.dumps(self.expected))

        approval = self.root / 'recovery-migration-approved'
        self.db.identity = lambda: 'recovery_abcdef123456|argin_prod_app|true'
        with patch.object(h, 'RECOVERY_MIGRATION_APPROVAL', approval), \
             patch.object(h, 'BACKUPS', self.root / 'backups'):
            approval.write_text(f'recovery_abcdef123456\n{"b" * 40}\n')
            approval.chmod(0o600)
            with self.assertRaisesRegex(h.Refused, 'approval does not match'):
                self.run_reviewed_release(current)
            self.assertEqual(self.events, [])
            approval.write_text(f'recovery_abcdef123456\n{self.release.name}\n')
            self.assertTrue(self.run_reviewed_release(current))
            self.assertEqual(self.events, ['stop', 'backup', 'apply'])

    def test_reviewed_recovery_rejects_any_migration_outside_exact_release_delta(self):
        current = self.current_release_with_migrations()
        self.expected = self.expected + [
            '20261123100000_AddInvoiceBusinessSerials',
            '20261123133602_AddCatalogVatRatePercent',
            '20261124100000_Unreviewed',
        ]
        (self.release / 'api/schema/migrations.json').write_text(json.dumps(self.expected))
        with self.assertRaisesRegex(h.Refused, 'migration target'):
            self.run_reviewed_release(current)
        self.assertEqual(self.events, [])
        self.assertEqual(self.database_calls, [])

    def test_reviewed_recovery_requires_a_current_release_for_comparison(self):
        selection, state_dir, _ = self.reviewed_recovery_fixture()
        with patch.object(h, 'ACTIVE_BACKUP_DB', selection), \
             patch.object(h, 'RECOVERY_STATE_DIR', state_dir), \
             patch.object(h, 'ROOT_UID', os.getuid()), \
             patch.object(h, 'trusted'), \
             patch.object(h.pwd, 'getpwnam', return_value=SimpleNamespace(pw_uid=os.getuid())):
            with self.assertRaisesRegex(h.Refused, 'current release'):
                h.migrate_release(self.release, self.db,
                                  lambda: self.events.append('stop'),
                                  lambda: self.events.append('restart-current'))
        self.assertEqual(self.events, [])
        self.assertEqual(self.database_calls, [])

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
        for name in ('api.tar.gz', 'web.tar.gz', 'backup.tar.gz'):
            (self.root / name).write_bytes(b'archive')
        digest = hashlib.sha256(b'archive').hexdigest()
        (self.root / 'SHA256SUMS').write_text(''.join(f'{digest}  {name}\n' for name in ('api.tar.gz', 'web.tar.gz', 'backup.tar.gz')))
        (self.root / 'build-info.txt').write_text('version=1.0 (Build 1)\nsha='+'a'*40+'\n')
        h.verify_bundle(self.root, 'a'*40)
        with self.assertRaises(h.Refused): h.verify_bundle(self.root, 'b'*40)


if __name__ == '__main__':
    unittest.main()
