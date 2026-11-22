# Public repository: security review (2026-11-22)

The owner made `thiagomendes/spooky-street` public to lift the private-repo Actions minute limits. This review checks
that nothing secret is exposed.

## Checked and clean

- **Git history.** I searched all 99 commits on every branch for:
  - private key, certificate and password literals;
  - GitHub, AWS and Slack token formats;
  - `.p12`, `.p8`, `.pem`, `.key`, `.ulf`, `.mobileprovision`, `.keystore` and `.env` files.

  Nothing turned up. `.gitignore` blocks those file types, and `tools/ci/validate_project.py` rejects committed keys.
  The names of the signing secrets appear only in docs and workflows, never their values.
- **Secrets stay in GitHub.**
  - Apple signing and App Store Connect: the `testflight` environment.
  - Unity account: repository secrets.

  Workflows read them only as `${{ secrets.* }}`. GitHub masks them in logs, and fastlane runs with them masked
  (`***`).
- **Pull requests from forks.** `ci.yml` runs on `pull_request`, never `pull_request_target`, so workflows triggered
  from forks get no secrets. The Unity job sees no licence and skips itself. The TestFlight workflow runs only on
  pushes to `main` and manual dispatch.
- **Artifacts.** Artifacts of a public repo are downloadable by anyone signed in to GitHub. I checked each one:
  - CI logs carry the Unity licence client's status lines only: no email, serial or entitlement file. The account
    activation log goes to `/tmp` inside the container and is never uploaded.
  - The licence probe artifact is filtered by design and expires after 3 days.
  - Archive logs never contain the IPA or secrets. Xcode logs may show the Apple Team ID and the bundle id; neither is
    a secret, since both ship inside every signed app.

## Owner actions recommended (GitHub settings, not code)

1. **`testflight` environment.** Settings → Environments → testflight → Deployment branches: allow `main` only. Keep
   reviewers off, or the overnight builds stop.
2. **Actions for outside contributors.** Settings → Actions → General → "Fork pull request workflows from outside
   collaborators": choose **Require approval for all outside collaborators**.
3. **Your commit email is public.** The address in commit metadata is now visible. If you prefer, enable "Keep my
   email addresses private" in GitHub and use the `noreply` address for future commits. Past commits keep the old
   one.
4. **Old branch `feature/s000.2-models`.** It is unmerged and holds the abandoned CC0 KayKit assets. Publishing them
   is legal, but the branch can be deleted to avoid confusion. Sessions cannot delete refs; delete it in the GitHub
   UI.
5. **Billing.** Public repositories get standard GitHub-hosted runners, macOS included, without using the minutes
   quota. Keep the spending limit at $0 so nothing can charge.
