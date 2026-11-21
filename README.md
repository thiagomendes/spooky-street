# Spooky Street

Single-player, third-person neighborhood mystery for iPhone (TestFlight first) built with Unity 6.3 LTS,
URP, Input System and Cinemachine. Product and acceptance contract: [`Spooky-Street-Spec.md`](Spooky-Street-Spec.md).

- Current increment: **S000 walking skeleton** — [`docs/S000-walking-skeleton.md`](docs/S000-walking-skeleton.md)
- One-time CI/TestFlight setup: [`docs/ci-setup.md`](docs/ci-setup.md)
- Decisions: [`docs/decisions.md`](docs/decisions.md) · Asset provenance: [`docs/asset-provenance.md`](docs/asset-provenance.md)

## Open and play

Open the repository root with **Unity 6000.3.28f1**, open `Assets/SpookyStreet/Scenes/LanternLane.unity`,
press Play. Keyboard: WASD/arrows walk, Space jumps, right-mouse-drag orbits, R recenters, Esc pauses.
Gamepad: left stick, south button, right stick, start. Touch: left-side floating stick, Jump button,
drag elsewhere to orbit.

## Checks without Unity

```bash
./tools/check-local.sh
```

## Pipeline

PR → **CI** (static checks + engine-free tests; Unity EditMode/PlayMode when licence secrets exist) →
merge → Actions → **Build for TestFlight** (Linux Unity export → macOS sign/upload → processing status).
