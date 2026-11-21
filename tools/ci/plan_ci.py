#!/usr/bin/env python3
"""Decide whether the expensive Unity job must run for this CI event.

Prints `unity=true|false` and `reason=...` lines for $GITHUB_OUTPUT. Rules:
  1. workflow_dispatch always runs Unity.
  2. If no Unity-relevant path changed since the comparison base, skip.
  3. If this commit (or, for a --no-ff merge, the merged branch tip with an identical tree)
     already has a successful Unity check run, skip: the same tree was tested.
Environment: GITHUB_EVENT_NAME, GITHUB_REPOSITORY, GITHUB_SHA, BEFORE_SHA, PR_BASE_SHA,
GH_TOKEN (checks: read). Without a token rule 3 is skipped (conservative: run).
"""
import json
import os
import subprocess
import urllib.parse
import urllib.request

UNITY_PATHS = ("Assets/", "Packages/", "ProjectSettings/", "ci/unity/", ".github/workflows/ci.yml")
UNITY_CHECK = "Unity compile + EditMode/PlayMode tests"
ZERO = "0" * 40


def git(*args):
    result = subprocess.run(["git", *args], capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else None


def comparison_base(event, head):
    if event == "pull_request" and os.environ.get("PR_BASE_SHA"):
        return git("merge-base", os.environ["PR_BASE_SHA"], head)
    before = os.environ.get("BEFORE_SHA", "")
    if before and before != ZERO and git("cat-file", "-e", before + "^{commit}") is not None:
        return before
    # New branch: compare with where it left main.
    return git("merge-base", "origin/main", head)


def unity_check_passed(sha):
    token = os.environ.get("GH_TOKEN")
    repo = os.environ.get("GITHUB_REPOSITORY")
    if not token or not repo:
        return False
    query = urllib.parse.urlencode({"check_name": UNITY_CHECK, "filter": "all"})
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repo}/commits/{sha}/check-runs?{query}",
        headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json"})
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            runs = json.load(response).get("check_runs", [])
    except OSError:
        return False
    return any(run.get("conclusion") == "success" for run in runs)


def decide():
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    head = os.environ.get("GITHUB_SHA") or git("rev-parse", "HEAD")
    if event == "workflow_dispatch":
        return True, "manual run"

    base = comparison_base(event, head)
    if base and base != head:
        changed = (git("diff", "--name-only", base, head) or "").splitlines()
        relevant = [path for path in changed if path.startswith(UNITY_PATHS)]
        if not relevant:
            return False, f"no Unity-relevant changes since {base[:12]}"
    elif base == head:
        return False, "no changes"

    tree = git("rev-parse", head + "^{tree}")
    candidates = [head]
    second_parent = git("rev-parse", "--verify", "--quiet", head + "^2")
    if second_parent:
        candidates.append(second_parent)
    for sha in candidates:
        if git("rev-parse", sha + "^{tree}") == tree and unity_check_passed(sha):
            return False, f"identical tree already passed Unity at {sha[:12]}"
    return True, "Unity-relevant changes not yet validated"


def main():
    unity, reason = decide()
    print(f"unity={'true' if unity else 'false'}")
    print(f"reason={reason}")


if __name__ == "__main__":
    main()
