#!/usr/bin/env python3
"""Phase09 Step21 source verification; no migration, database or service mutation."""
import subprocess
import sys

sys.dont_write_bytecode = True


def run(command):
    print("$ " + " ".join(command))
    subprocess.run(command, check=True)


def main():
    run([
        "dotnet", "test", "tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj",
        "--filter",
        "FullyQualifiedName~Phase09SigningKeyGenerationTests|"
        "FullyQualifiedName~Phase09SigningGenerationHttpTests",
        "--no-restore",
    ])
    run([
        "node", "--test",
        "apps/web/tests/phase09-step21-key-download.test.mjs",
        "apps/web/tests/phase09-step21-generation-ui.test.mjs",
    ])
    run(["npm", "--prefix", "apps/web", "run", "typecheck"])
    changed = subprocess.check_output(
        ["git", "diff", "--name-only", "origin/phase/09-cryptographic-keys-signing...HEAD"],
        text=True,
    ).splitlines()
    migrations = [path for path in changed if "/Migrations/" in path or path.endswith("ModelSnapshot.cs")]
    if migrations:
        raise RuntimeError("Step21 must not change migrations: " + ", ".join(migrations))
    run(["git", "diff", "--check", "origin/phase/09-cryptographic-keys-signing...HEAD"])
    print("Step21 source verification passed; no migration change detected.")


if __name__ == "__main__":
    try:
        main()
    except (subprocess.CalledProcessError, RuntimeError) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        sys.exit(2)
