"""Exercise smoke process cleanup without publishing or starting the real API."""
import os
from pathlib import Path
import signal
import subprocess
import tempfile
import time
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "scripts/phase18-step05-production-negative-smoke.sh"


class NegativeSmokeCleanupTests(unittest.TestCase):
    def test_success_stops_the_api_child(self):
        self.run_smoke(interrupt=False)

    def test_termination_stops_the_api_child(self):
        self.run_smoke(interrupt=True)

    def run_smoke(self, interrupt):
        with tempfile.TemporaryDirectory(prefix="argin-smoke-test-") as directory:
            root = Path(directory)
            (root / "scripts").mkdir()
            script = root / "scripts" / SCRIPT.name
            script.write_text(SCRIPT.read_text())
            bin_dir = root / "bin"
            bin_dir.mkdir()
            programs = {
                "git": '#!/bin/sh\ncase "$1" in branch) echo phase/18-production-deployment;; rev-parse) echo test-sha;; esac\n',
                "lsof": "#!/bin/sh\nexit 1\n",
                "ss": "#!/bin/sh\nexit 0\n",
                "dotnet": '''#!/usr/bin/env python3
import os, pathlib, signal, sys, time
if sys.argv[1] == 'publish':
    pathlib.Path(sys.argv[sys.argv.index('-o') + 1]).mkdir(parents=True, exist_ok=True)
else:
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(0))
    pathlib.Path(os.environ['SMOKE_TEST_PID']).write_text(str(os.getpid()))
    while True:
        time.sleep(0.05)
''',
                "curl": '''#!/usr/bin/env python3
import os, pathlib, sys, time
while not pathlib.Path(os.environ['SMOKE_TEST_PID']).exists():
    time.sleep(0.01)
if os.environ.get('SMOKE_TEST_INTERRUPT'):
    time.sleep(0.2)
url = sys.argv[-1]
print('200' if url.endswith('/health/live') else '503' if url.endswith('/health/ready') else '404', end='')
''',
            }
            for name, content in programs.items():
                executable = bin_dir / name
                executable.write_text(content)
                executable.chmod(0o755)
            pid_file = root / "api.pid"
            env = {**os.environ, "PATH": f"{bin_dir}:{os.environ['PATH']}",
                   "SMOKE_TEST_PID": str(pid_file)}
            if interrupt:
                env["SMOKE_TEST_INTERRUPT"] = "1"
            process = subprocess.Popen(["bash", str(script)], env=env,
                                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            api_pid = None
            try:
                deadline = time.monotonic() + 5
                while not pid_file.exists() and time.monotonic() < deadline:
                    time.sleep(0.02)
                if not pid_file.exists():
                    output, _ = process.communicate(timeout=2)
                    self.fail(f"API child did not start: {output}")
                api_pid = int(pid_file.read_text())
                if interrupt:
                    process.terminate()
                output, _ = process.communicate(timeout=10)
                self.assertEqual(process.returncode, 143 if interrupt else 0, output)
                deadline = time.monotonic() + 2
                while time.monotonic() < deadline:
                    try:
                        os.kill(api_pid, 0)
                    except ProcessLookupError:
                        break
                    time.sleep(0.02)
                else:
                    self.fail(f"API child {api_pid} survived smoke cleanup")
            finally:
                if process.poll() is None:
                    process.kill()
                    process.communicate()
                if api_pid:
                    try:
                        os.kill(api_pid, signal.SIGTERM)
                    except ProcessLookupError:
                        pass


if __name__ == "__main__":
    unittest.main()
