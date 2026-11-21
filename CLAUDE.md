# Working notes for Claude Code sessions

- Contract: `Spooky-Street-Spec.md`. Work one increment per branch; record evidence in `docs/S00x-*.md`.
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
