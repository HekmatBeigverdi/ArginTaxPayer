from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class BackupStateDirectoryTests(unittest.TestCase):
    def test_systemd_pins_recovery_state_directory_to_private_mode(self):
        unit = (ROOT / 'ops/phase18-systemd/argintaxpayer-backup.service').read_text()
        self.assertRegex(unit, r'(?m)^StateDirectory=argintaxpayer/backup-controller$')
        self.assertRegex(unit, r'(?m)^StateDirectoryMode=0700$')

    def test_helper_install_repairs_existing_state_directory_without_restarting_service(self):
        installer = (ROOT / 'scripts/phase18-install-deploy-helper.sh').read_text()
        self.assertRegex(installer, r'install -d -o argintaxsvc -g argintaxsvc -m 0700 /var/lib/argintaxpayer/backup-controller')
        self.assertNotRegex(installer, r'systemctl restart argintaxpayer-backup\.service')


if __name__ == '__main__':
    unittest.main()
