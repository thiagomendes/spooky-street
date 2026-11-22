# Portability premises (D25)

The game ships first on iPhone. Every change keeps one codebase that can later build for other platforms without
rewrites.

## Platform order (owner-approved, when the time comes)

1. iPad, alongside iPhone (4:3 layout).
2. Mac (Apple Silicon), from the iOS build (mouse, keyboard, gamepad, resizable window).
3. Android (device variety, performance tiers, Google Play).
4. PC/Steam (including Steam Deck) and Nintendo Switch (gamepad-first menus, graphics options, certification).

Consoles need approved developer accounts, devkits and possibly a paid Unity plan. That is an owner decision, since
the current rule is to spend no money.

## Rules for every change

- **Logic stays in `Core`.** It is engine-free and has no platform references. Platform specifics live in thin
  Runtime adapters.
- **Input goes through the Input System actions only.** Never read touches, keys or buttons directly in gameplay
  code. The one exception is a component whose job is a touch gesture, such as the parent gate; give it a gamepad
  and keyboard alternative.
- **Prompts and glyphs are per control scheme.** Every on-screen prompt, tip or button icon is a localization key
  or glyph chosen from the active scheme (touch, gamepad, keyboard and mouse). Never bake "tap" or "WASD" into
  shared text.
- **UI respects any aspect ratio and the safe area.** That covers 19.5:9 phones, 4:3 iPad and 16:9/16:10 screens.
  Anchor layouts; never use fixed pixel positions.
- **Graphics scale through URP quality tiers and asset LODs.** No feature may require a specific device.
  Expensive effects need a cheaper fallback.
- **Saves and settings go through the save abstraction** (`Core/Save`). No platform paths in gameplay code.
- **Platform services stay behind interfaces** and are optional: achievements, cloud save, store, haptics, audio
  session.
- **Art and audio come from scripts,** so higher or lower quality variants can be exported from the same source.
