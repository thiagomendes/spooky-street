# Art bible (S000.1 greybox baseline)

1. **Style** — Handmade neighbourhood after sunset (spec §6): chunky primitive silhouettes, matte painted
   timber, paper-cutout UI cards with hard ink shadows, simple line icons. Greybox shapes are allowed until
   S003, but every colour and shape is deliberate.
2. **Palette** — source of truth is `Assets/SpookyStreet/Runtime/App/Palette.cs`.
   Spec colours: twilight sky `#344767`, house indigo `#394461`, pumpkin amber `#F6AD58` (the active point of
   interest: lit window, lamps, primary button), cloth silver `#DAE2EE` (June's clue only — never Pip, never UI
   chrome), warm paper `#F4E6C8` (UI cards, Pip's moth fuzz). Helpers: ink `#1E2233`, roof shadow `#283044`,
   horizon `#515665`, hedge `#2E4A43`, timber `#6B5A4E`, path `#7E6C5F`, Pip cape `#C58A46`.
3. **Mood** — Cool moonlight from behind the street rim-lights the silhouettes; warm amber pools (attic window,
   porch lamp) carry the eye; shadows stay blue-indigo, never black. The brightest sustained value on screen is
   the active point of interest (spec §6), never UI chrome.

Review gate: every visible change is captured by `SpookyStreet.Tests.Visual` in CI and scored with the
craft-game-floors visual scorecard (all six axes ≥ 3) before a TestFlight build.
