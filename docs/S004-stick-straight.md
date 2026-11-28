# S004 fix: the left stick held straight walks straight

Branch `fix/touch-stick-straight` (from `main` 3d6536c). Not merged. Reported on the iPhone by the owner's son,
then the owner (2026-11-28): "quando você segura em linha reta, a Pip sai meio que zigue-zagueando".

## What the child notices

- **Held straight, Pip runs straight.** A thumb that trembles and sits a little crooked on the floating stick now
  runs Pip in a line. Before, she drifted and curved away within a few metres.
- **The camera stays calm.** It no longer swings with every little sway of Pip. After a real turn it still comes
  round behind her on a long run, a little more gently than before, and it stops a few degrees short of dead behind.
- **Turns feel the same.** A quick 90° swing of the thumb turns her in about 0.2 s, as before (the filter adds at
  most two frames). Walk or run strength is untouched.
- Gamepad and keyboard: the calmer camera applies to every scheme. The stick filter is touch only, because a
  gamepad's sprung stick does not tremble like a thumb on glass, and keyboard directions are exact already.

## Cause

Movement is camera-relative, and the auto-recenter chased Pip's heading at full speed (90°/s cap, `MoveTowardsAngle`).
Near zero that is a gain of about 60/s: one degree of thumb wobble put Pip one degree off, the camera caught up within
the same frame, the move basis turned with it, and the next wobble added on top. So any crooked or trembling thumb
became a steady turn. With the recenter switched off in the same simulation the drift fell from 7.2 m to 0.38 m, so
the loop was the main cause. The rest was the thumb itself.

## Change

- `Core/Movement/CameraOrbitModel.cs`: auto-recenter now follows a smoothed heading (0.4 s), turns proportionally
  (2.5°/s per degree off, still capped at 90°/s), and does nothing within a 6° dead zone. Manual orbit, the recenter
  button and cuts are unchanged.
- `Core/Movement/StickSteering.cs` (new): light tremor smoothing (0.06 s, skipped for changes over 25°, so a deliberate
  turn has no lag) and magnetism onto the 8 main directions: exact within 6°, fading out by 14°, so every angle stays
  reachable.
- `Runtime/Controls/TouchControls.cs`: `FloatingStick.Value` is the steadied value, stepped once per frame.
  `RawValue` keeps the thumb exactly as dragged. Radius 110 and dead zone 0.12 are unchanged.
- Ruled out: latching the move basis while the stick is held. It breaks a child who keeps pointing at where Pip goes
  on screen while the camera swings (and the cutscene walks and test helpers that steer the same way): the basis
  drifts up to the re-take threshold, then snaps, which is a zigzag of its own.

## Numbers (Core `StraightStickTests`, 40 seeded thumbs)

The thumb model: tremor re-aimed every 60–180 ms within ±7°, a slow ±3° wander (±10° in all), and a push jittering
between 70% and 100% of the radius. The simulation runs the real motor, Pip's facing at 720°/s and the camera
recenter, in the game's frame order. The band is 25 cm over 10 m.

| Sideways drift over 10 m | Mean | Worst |
|---|---|---|
| Before (old recenter, raw stick) | 3.74 m | 7.22 m |
| Calmer camera only | 0.20 m | 0.38 m |
| **After (calmer camera + stick filter), 60 fps** | **0.017 m** | **0.085 m** |
| After, 30 fps | 0.020 m | 0.088 m |

- A 90° thumb swing in 0.1 s: Pip's run points the new way within 0.22 s in the worst case. The filter adds 33 ms at
  most compared with the raw stick.
- Long run after a turn, with the child pointing at the screen: after 4 s the lens is 7° off dead behind her
  (dead zone 6°), and her run strays 0.36 m.
- PlayMode `StraightStickPlayTests`: in the scene, a thumb dragged 4° crooked and trembling ±5° on the real floating
  stick for 1.2 s. Asserted: less than 0.15 m sideways, heading and lens both within 3°.

## Still open

- Feel on the phone is not proven until the owner or his son tries a TestFlight build.
- A thumb within 6° of a main direction always goes exactly along it. To aim a few degrees off one, the child pushes
  a little further; the pull is gone by 14°.
- Holding the stick to one side still circles Pip around, as in any camera-relative game with a recentering camera.
