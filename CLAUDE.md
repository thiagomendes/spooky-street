# Working notes for Claude Code sessions

- Contract: `Spooky-Street-Spec.md`. Record evidence per increment in `docs/S00x-*.md`.
- Git flow (owner's choice): branch from `main` as `feature/<topic>` or `fix/<topic>` (`docs/<topic>` for documents
  only), push the branch (CI runs on `feature/**` and `fix/**`), then `git merge --no-ff` into `main` and push once CI is green. No PRs needed.
  Delete the local branch after merging; `repo-hygiene.yml` deletes merged `feature/*`, `fix/*`, `docs/*` and `design/*` remote branches
  on the next push to `main` (sessions cannot delete remote refs). Never push a branch before it has its own
  commit, or hygiene will treat it as merged.
- TestFlight upload (costs macOS minutes, use only for changes worth testing on the phone, and tell the owner):
  put `[testflight]` in the message of the `--no-ff` merge commit pushed to `main`. The workflow checks the commit
  is on main, builds, uploads and polls processing. Sessions cannot dispatch or cancel workflows, push tags or
  delete refs (403); read runs and logs with the GitHub MCP tools.
- Actions budget matters: add `[skip ci]` to intermediate commits and push without it only when the branch is
  ready for validation. Never re-trigger CI just to check something a previous run already proved.
- Never claim a Unity, Xcode, TestFlight or device pass without that run's evidence. Unity checks run only
  in GitHub Actions (`ci.yml`), not in the cloud container (Unity hosts are blocked there).
- Before pushing run `./tools/check-local.sh` (needs `pip install -r tools/requirements-ci.txt` and .NET 8:
  `apt-get install -y dotnet-sdk-8.0`): it catches the cheap failures before a 45-minute Unity run. Never loosen,
  skip or disable a test to get green; fix the cause.
- New files under `Assets/` need `.meta` files: run `python3 tools/unity_meta.py`. Leave existing metas alone:
  their GUIDs are what scenes and prefabs point to.
- `Assets/SpookyStreet/Core` must stay engine-free (`noEngineReferences`, C# 9); put testable logic there and
  test it in `Assets/SpookyStreet/Tests/EditMode/Core` (compiled by both Unity and `tools/core-tests`).
- Pip is a girl (D30): she/her in every text and brief; feminine forms in Portuguese and other gendered languages.
- Every user-facing string is a key in `Assets/SpookyStreet/Content/Resources/Localization/en.json` with
  translator context, referenced through `LocalizationKeys`. Never key saved progress off display text.
- Changing the Unity patch: update `ProjectVersion.txt` and both workflow image tags + digests together.
- Signing secrets live only in the `testflight` environment; PR jobs must never reference them.
- North star (D26, D27): `docs/narrative-bible.md` (checklist), `docs/world-and-cases.md`, `docs/mystery-first.md`,
  `docs/engagement.md`, `docs/roadmap.md`. Every stage or case plan passes the bible's checklist first; the child
  makes the deductions; hints never give answers; Kids-category engagement guardrails are hard rules.
- Portability (D25, `docs/portability.md`): one codebase for iPhone first, later iPad, Mac, Android, PC/Steam and
  Switch. Input only via Input System actions, prompts per control scheme, any aspect ratio and safe area,
  URP quality tiers, platform services behind interfaces.
- Spend no money; never submit for App Review or external TestFlight testing.
- Agents (owner policy, 2026-11-24): the main session orchestrates and decides; the product stays with the owner.
  Use the profiles in `.claude/agents/` (`artista-3d`, `dev-jogo`, `textos-e-traducao`, `revisor-visual`): each one
  fixes its model and effort (Opus 5.5 only: high for 3D and graphics, medium for everything else; owner's choice
  on cost and quality) and carries its area's rules. Delegate only large, independent work; do small reads, edits
  and docs directly. At most 2 agents at once (each re-reads the project and five at once hit the weekly limit).
  Continue an agent with SendMessage when the topic is the same. A brief states the goal and who it is for, the
  rules with their reasons, what was ruled out, what "done" means (tests, CI run, frames), what is forbidden
  (merging to main, `[testflight]`, deleting refs) and a short report format; leave the plan to the agent.
- Scope: deliver what was asked, in the scope asked. Decide routine choices yourself and ask only when different
  readings would lead to very different work. If the request looks wrong or there is a better way, say so in one
  sentence and carry on with what was asked, without narrowing, widening or reshaping the task. Finish the whole
  task; if something cannot be done, do the rest and say clearly what is missing and why.
- Reports to the owner are in Brazilian Portuguese and plain game language (what the child sees, hears or does),
  starting with the result; no branch names, run numbers or internal codes (owner, 2026-11-23).
- Anything that needs an owner decision goes through the question tool (AskUserQuestion), never buried in a
  report: two to four options with the recommended one first and marked "(Recomendado)". It notifies the owner's
  phone and he answers there (owner, 2026-11-27).
- Art is 100% original (spec v1.3): Blender Python scripts in `tools/blender/` (headless Blender 4.0.2, Eevee
  renders work); review every asset by rendering and looking at the image before it enters the game.
