#!/usr/bin/env bash
# Publish Phase07 GitHub Release from a verified merged main branch.
# Explicit operator command only; no DB, deployments, or force pushes.
set -Eeuo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
tag="phase-07-v1.0.0"
repo="HekmatBeigverdi/ArginTaxPayer"
[[ "$(git branch --show-current)" == "main" ]] || { echo 'Require main branch.' >&2; exit 2; }
[[ -z "$(git status --porcelain)" ]] || { echo 'Refusing dirty worktree.' >&2; exit 2; }
command -v gh >/dev/null || { echo 'Install/authenticate GitHub CLI (gh).' >&2; exit 2; }
gh auth status >/dev/null || { echo 'GitHub CLI authentication required.' >&2; exit 2; }
[[ "$(gh repo view --json nameWithOwner --jq .nameWithOwner)" == "$repo" ]] || {
 echo 'Wrong GitHub repository.' >&2; exit 2;
}
git fetch origin main --tags
[[ "$(git rev-parse HEAD)" == "$(git rev-parse refs/remotes/origin/main)" ]] || {
 echo 'main does not match origin/main. Do not publish.' >&2; exit 2;
}
[[ -f docs/phases/phase-07-release-notes.md ]] || { echo 'Release notes missing.' >&2; exit 2; }
[[ -f docs/phases/phase-07-invoice-domain-rule-engine.md ]] || exit 2
if gh release view "$tag" -R "$repo" >/dev/null 2>&1; then
 echo "Release already exists: $tag"; exit 0
fi
if git show-ref --verify --quiet "refs/tags/$tag" ||    git ls-remote --exit-code --tags origin "refs/tags/$tag" >/dev/null 2>&1; then
 echo "Tag exists but release missing. Verify tag target manually; refusing automatic retag." >&2
 exit 2
fi
sha="$(git rev-parse HEAD)"
echo "Release candidate: $sha on main"
echo "Target repo: $repo | annotated tag: $tag"
read -r -p 'Type RELEASE to publish this exact SHA: ' approval
[[ "$approval" == "RELEASE" ]] || { echo 'Canceled.'; exit 2; }
git tag -a "$tag" "$sha" -m "ArginTaxPayer Phase 07 — Invoice Domain and Rule Engine"
git push origin "refs/tags/$tag"
gh release create "$tag" -R "$repo" --verify-tag   --title "ArginTaxPayer — Phase 07: Invoice Domain and Rule Engine"   --notes-file docs/phases/phase-07-release-notes.md
gh release view "$tag" -R "$repo" --json tagName,url,name,isDraft,isPrerelease
echo "Git main SHA: $sha"
