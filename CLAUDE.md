# Working notes for Claude Code sessions

- Contract: `Spooky-Street-Spec.md`. Record evidence per increment in `docs/S00x-*.md`.
- Git flow (owner's choice): branch from `main` as `feature/<topic>` or `fix/<topic>`, push the branch (CI runs
  on `feature/**` and `fix/**`), then `git merge --no-ff` into `main` and push once CI is green. No PRs needed.
  Delete the local branch after merging. The session's git proxy refuses remote branch deletion (403), so tell
  the owner which merged remote branches to delete at github.com/thiagomendes/spooky-street/branches.
- Actions budget matters: add `[skip ci]` to intermediate commits and push without it only when the branch is
  ready for validation. Never re-trigger CI just to check something a previous run already proved.
- Never claim a Unity, Xcode, TestFlight or device pass without that run's evidence. Unity checks run only
  in GitHub Actions (`ci.yml`), not in the cloud container (Unity hosts are blocked there).
- Before pushing run `./tools/check-local.sh` (needs `pip install -r tools/requirements-ci.txt` and .NET 8:
  `apt-get install -y dotnet-sdk-8.0`).
- New files under `Assets/` need `.meta` files: run `python3 tools/unity_meta.py`; never rewrite existing metas.
- `Assets/SpookyStreet/Core` must stay engine-free (`noEngineReferences`, C# 9); put testable logic there and
  test it in `Assets/SpookyStreet/Tests/EditMode/Core` (compiled by both Unity and `tools/core-tests`).
- Every user-facing string is a key in `Assets/SpookyStreet/Content/Resources/Localization/en.json` with
  translator context, referenced through `LocalizationKeys`. Never key saved progress off display text.
- Changing the Unity patch: update `ProjectVersion.txt` and both workflow image tags + digests together.
- Signing secrets live only in the `testflight` environment; PR jobs must never reference them.
- Spend no money; never submit for App Review or external TestFlight testing.
