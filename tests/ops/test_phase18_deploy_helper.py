import hashlib
from contextlib import contextmanager
import importlib.util
import io
import os
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('release_helper', ROOT / 'ops/phase18-deploy/argin-taxpayer-release.py')


@contextmanager
def root_owned_metadata():
    # This sandbox remaps root ownership; retain real modes, paths and symlinks.
    original_stat, original_lstat = Path.stat, Path.lstat
    def rooted_stat(path, *args, **kwargs):
        fields = list(original_stat(path, *args, **kwargs))
        fields[4] = 0
        return os.stat_result(fields)
    def rooted_lstat(path, *args, **kwargs):
        fields = list(original_lstat(path, *args, **kwargs))
        fields[4] = 0
        return os.stat_result(fields)
    with patch.object(Path, 'stat', rooted_stat), patch.object(Path, 'lstat', rooted_lstat):
        yield


class ImplementationTests(unittest.TestCase):
    def test_helper_is_available(self):
        self.assertTrue(Path(SPEC.origin).is_file(), 'restricted helper is not implemented')


@unittest.skipUnless(Path(SPEC.origin).is_file(), 'helper not implemented yet')
class HelperTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.h = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(cls.h)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def archive(self, name, kind=tarfile.REGTYPE, link='', data=b'payload'):
        dest = self.root / 'input.tar.gz'
        with tarfile.open(dest, 'w:gz') as archive:
            entry = tarfile.TarInfo(name)
            entry.type, entry.linkname = kind, link
            entry.size = len(data) if kind == tarfile.REGTYPE else 0
            entry.mode = 0o4777
            archive.addfile(entry, io.BytesIO(data) if entry.size else None)
        return dest

    def test_sha_rejects_shell_and_traversal(self):
        for sha in ['../x', 'a'*40+';id', 'A'*40, 'a'*39, 'a'*40+'\n']:
            with self.subTest(sha=sha), self.assertRaises(self.h.Refused):
                self.h.validate_sha(sha)
        self.assertEqual(self.h.validate_sha('a'*40), 'a'*40)

    def test_extraction_discards_archive_permissions(self):
        output = self.root / 'out'
        output.mkdir()
        self.h.extract_archive(self.archive('web/package.json'), output, 'web')
        target = output / 'web/package.json'
        self.assertEqual(target.read_bytes(), b'payload')
        self.assertEqual(target.stat().st_mode & 0o7777, 0o700)

    def test_archive_cannot_escape_or_use_links_devices_or_secrets(self):
        for name, kind, link in [
            ('../outside', tarfile.REGTYPE, ''),
            ('/web/file', tarfile.REGTYPE, ''),
            ('web/../outside', tarfile.REGTYPE, ''),
            ('api/file', tarfile.REGTYPE, ''),
            ('web/link', tarfile.SYMTYPE, '/etc/passwd'),
            ('web/link', tarfile.LNKTYPE, '/etc/passwd'),
            ('web/fifo', tarfile.FIFOTYPE, ''),
            ('web/device', tarfile.CHRTYPE, ''),
            ('web/.env.production', tarfile.REGTYPE, ''),
            ('web/.npmrc', tarfile.REGTYPE, ''),
        ]:
            with self.subTest(name=name, kind=kind):
                out = Path(tempfile.mkdtemp(dir=self.root))
                with self.assertRaises(self.h.Refused):
                    self.h.extract_archive(self.archive(name, kind, link), out, 'web')
                self.assertFalse((self.root / 'outside').exists())

    def test_archive_size_limit(self):
        out = self.root / 'out'
        out.mkdir()
        with patch.object(self.h, 'MAX_EXPANDED_BYTES', 2):
            with self.assertRaises(self.h.Refused):
                self.h.extract_archive(self.archive('web/file'), out, 'web')

    def test_pax_metadata_is_bounded_before_tarfile_parses_it(self):
        archive = self.root / 'metadata.tar.gz'
        with tarfile.open(archive, 'w:gz', format=tarfile.PAX_FORMAT) as stream:
            item = tarfile.TarInfo('web/file')
            item.pax_headers = {'comment': 'x' * (1024 * 1024)}
            stream.addfile(item, io.BytesIO())
        out = self.root / 'out'
        out.mkdir()
        with self.assertRaises(self.h.Refused):
            self.h.extract_archive(archive, out, 'web')

    def test_trusted_path_rejects_writable_lexical_symlink_ancestor(self):
        (self.root / 'link').symlink_to('/usr/bin')
        self.root.chmod(0o777)
        with root_owned_metadata():
            with self.assertRaises(self.h.Refused):
                self.h.trusted(self.root / 'link/python3')

    def test_trusted_path_accepts_root_controlled_system_symlink(self):
        with root_owned_metadata():
            self.h.trusted(Path('/bin/sh'))

    def test_duplicate_archive_file_is_rejected(self):
        archive = self.root / 'duplicate.tar.gz'
        with tarfile.open(archive, 'w:gz') as tar:
            for _ in range(2):
                item = tarfile.TarInfo('web/file')
                tar.addfile(item, io.BytesIO())
        out = self.root / 'out'
        out.mkdir()
        with self.assertRaises(self.h.Refused):
            self.h.extract_archive(archive, out, 'web')

    def bundle(self):
        for name in ('api.tar.gz', 'web.tar.gz'):
            (self.root / name).write_bytes(name.encode())
        sums = ''.join(hashlib.sha256((self.root / n).read_bytes()).hexdigest()+'  '+n+'\n'
                       for n in ('api.tar.gz', 'web.tar.gz'))
        (self.root / 'SHA256SUMS').write_text(sums)
        (self.root / 'build-info.txt').write_text('sha='+'a'*40+'\n')

    def test_bundle_validates_hash_and_embedded_sha(self):
        self.bundle()
        self.h.verify_bundle(self.root, 'a'*40)
        (self.root / 'api.tar.gz').write_bytes(b'tampered')
        with self.assertRaises(self.h.Refused):
            self.h.verify_bundle(self.root, 'a'*40)

    def test_manifest_cannot_reference_another_path(self):
        self.bundle()
        with (self.root / 'SHA256SUMS').open('a') as stream:
            stream.write('0'*64+'  ../../etc/passwd\n')
        with self.assertRaises(self.h.Refused):
            self.h.verify_bundle(self.root, 'a'*40)

    def test_wrong_source_sha_is_rejected(self):
        self.bundle()
        with self.assertRaises(self.h.Refused):
            self.h.verify_bundle(self.root, 'b'*40)

    def test_existing_release_can_be_retried_only_with_matching_metadata(self):
        self.bundle()
        release = self.root / 'release'
        release.mkdir()
        for name in ('build-info.txt', 'SHA256SUMS'):
            (release / name).write_bytes((self.root / name).read_bytes())
        self.h.verify_existing_release(release, self.root)
        (release / 'SHA256SUMS').write_text('different')
        with self.assertRaises(self.h.Refused):
            self.h.verify_existing_release(release, self.root)

    def test_failed_restart_restores_previous_symlink(self):
        old, new = self.root / ('a'*40), self.root / ('b'*40)
        old.mkdir()
        new.mkdir()
        current = self.root / 'current'
        current.symlink_to(old)
        def restart():
            if current.resolve() == new:
                raise RuntimeError('service failed')
        with self.assertRaises(self.h.Refused):
            self.h.promote(new, current, restart, lambda: True)
        self.assertEqual(current.resolve(), old)

    def test_disconnect_immediately_after_symlink_switch_rolls_back(self):
        old, new = self.root / ('a'*40), self.root / ('b'*40)
        old.mkdir()
        new.mkdir()
        current = self.root / 'current'
        current.symlink_to(old)
        atomic = self.h.atomic_current
        def switch_then_disconnect(link, target):
            atomic(link, target)
            if target == new:
                raise self.h.Refused('SSH disconnected')
        with patch.object(self.h, 'atomic_current', switch_then_disconnect):
            with self.assertRaises(self.h.Refused):
                self.h.promote(new, current, lambda: None, lambda: True)
        self.assertEqual(current.resolve(), old)

    def test_upload_rejects_wrong_owner(self):
        (self.root / 'file').write_bytes(b'data')
        fd = os.open(self.root, os.O_RDONLY | os.O_DIRECTORY)
        self.addCleanup(os.close, fd)
        with self.assertRaises(self.h.Refused):
            self.h.snapshot_file(fd, 'file', self.root / 'copy', os.getuid()+1, 100)

    def test_upload_snapshot_rejects_symlink_and_fifo(self):
        source = self.root / 'source'
        source.mkdir()
        (self.root / 'secret').write_bytes(b'secret')
        os.symlink(self.root / 'secret', source / 'linked')
        os.mkfifo(source / 'fifo')
        fd = os.open(source, os.O_RDONLY | os.O_DIRECTORY)
        self.addCleanup(os.close, fd)
        for name in ('linked', 'fifo'):
            with self.subTest(name=name), self.assertRaises(self.h.Refused):
                self.h.snapshot_file(fd, name, self.root / 'copy', os.getuid(), 100)

    def test_upload_snapshot_is_private_and_independent(self):
        source = self.root / 'source'
        source.mkdir()
        (source / 'file').write_bytes(b'original')
        fd = os.open(source, os.O_RDONLY | os.O_DIRECTORY)
        self.addCleanup(os.close, fd)
        self.h.snapshot_file(fd, 'file', self.root / 'copy', os.getuid(), 100)
        (source / 'file').write_bytes(b'changed')
        self.assertEqual((self.root / 'copy').read_bytes(), b'original')
        self.assertEqual((self.root / 'copy').stat().st_mode & 0o777, 0o600)

    def test_final_copy_omits_npm_bin_links_and_rejects_external_links(self):
        source, target = self.root / 'source', self.root / 'target'
        (source / 'web/node_modules/.bin').mkdir(parents=True)
        (source / 'web/package.json').write_text('{}')
        os.symlink('../next/bin.js', source / 'web/node_modules/.bin/next')
        target.mkdir()
        self.h.copy_runtime_tree(source, target, os.getuid(), os.getgid())
        self.assertEqual((target / 'web/package.json').read_text(), '{}')
        self.assertFalse((target / 'web/node_modules/.bin/next').exists())
        os.symlink('/etc/passwd', source / 'web/secret')
        second = self.root / 'second'
        second.mkdir()
        with self.assertRaises(self.h.Refused):
            self.h.copy_runtime_tree(source, second, os.getuid(), os.getgid())

    def test_failed_health_restores_previous_symlink(self):
        releases = self.root / 'releases'
        releases.mkdir()
        old, new = releases / ('a'*40), releases / ('b'*40)
        old.mkdir()
        new.mkdir()
        current = self.root / 'current'
        current.symlink_to(old)
        attempts = []
        def restart():
            attempts.append(current.resolve())
        with self.assertRaises(self.h.Refused):
            self.h.promote(new, current, restart, lambda: current.resolve() == old)
        self.assertEqual(current.resolve(), old)
        self.assertEqual(attempts, [new, old])

    def test_failed_first_release_stops_services_and_removes_current(self):
        new = self.root / ('a'*40)
        new.mkdir()
        current = self.root / 'current'
        stopped = []
        with self.assertRaises(self.h.Refused):
            self.h.promote(new, current, lambda: None, lambda: False,
                           lambda: stopped.append(True))
        self.assertFalse(current.is_symlink())
        self.assertEqual(stopped, [True])

    def test_successful_promotion_retains_previous_release(self):
        old, new = self.root / ('a'*40), self.root / ('b'*40)
        old.mkdir()
        new.mkdir()
        current = self.root / 'current'
        current.symlink_to(old)
        self.h.promote(new, current, lambda: None, lambda: True)
        self.assertEqual(current.resolve(), new)
        self.assertTrue(old.is_dir())

if __name__ == '__main__':
    unittest.main()
