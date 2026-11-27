# The living street: Lantern Lane on Halloween night (design for D33)

Status: **design, for the owner's approval** (2026-11-27). Docs only. Nothing here is built yet. It answers D33
(`docs/decisions.md`) and is grounded in `docs/narrative-bible.md` (the checklist is in §11), `docs/world-and-cases.md`,
`docs/engagement.md`, `docs/mystery-first.md`, `Spooky-Street-Spec.md` v1.4 (§4 density, §6 premium bar, §8
performance), `docs/portability.md`, `docs/roadmap.md`, `docs/S004-plan.md`, `docs/graphics-agent-standards.md`
and the layout in code (`Core/World/EnvironmentLayout`, `CaseRouteLayout`, `CollisionRules`, `Core/Street`).

## Resumo para o dono

**O resultado:** a Rua das Lanternas vira uma rua de Halloween de verdade, cheia de gente, sem deixar de ser o
palco do mistério. A rua fica umas três vezes maior: continua para os dois lados, ganha uma pracinha na esquina,
quintais nos fundos e uma viela entre as casas. Lá longe já se veem os telhados da próxima rua e o portão do
parque dos próximos casos, ainda fechados.

**O que a criança vê:** grupos de crianças fantasiadas com um pai ou uma mãe indo de porta em porta, vizinhos
passeando, o Sr. Vale dando doces do caldeirão para quem passa. A casa da coruja está apagada, então os grupos
passam reto, como numa rua americana de verdade: lanterna acesa na varanda quer dizer "aqui tem doce".

**O que a criança faz quando quer:** toca a campainha das casas (cada uma reage de um jeito), faz uma lanterna de
papel para a varanda da Pip, entra na pequena caminhada das lanternas, pesca maçã com a boca, entalha uma
abóbora, tira foto no painel de rosto vazado, brinca de esconde-esconde com a June e ajuda em pequenas histórias
(o irmãozinho que se afastou do pai, juntar os amigos para uma foto). Há também lugares para entrar que não são
caso: uma festa na casa de um vizinho e a loja de máscaras de papel.

**O que ela ganha:** nunca moedas, nunca contagem, nunca pressa. Ganha lembranças que ficam na rua: a varanda da
Pip, que começa com caixas de mudança, vai se enfeitando com a abóbora, a lanterna e a foto que a criança fez.
Quando a lanterna da Pip acende, os grupos passam a tocar a campainha dela, e ela dá doces também. É a história
da Pip, a menina nova na rua, virando vizinha de todo mundo.

**O que não muda:** o mistério continua sendo o centro. Nada do mundo livre fica perto das pistas, ninguém do
passeio dá testemunho, as dicas do rádio continuam iguais e nenhuma brincadeira ajuda ou atrapalha o caso.

**Como chega:** primeiro uma versão de teste no TestFlight com 6 a 8 pessoas andando, 3 portas e 1 brincadeira,
para medir se o iPhone aguenta e se distrai do mistério. Depois o resto em duas etapas. São cerca de 18 dias de
agente para o teste e cerca de 40 para o restante.

**Preciso que você decida 5 coisas** (§10): se a multidão já aparece durante o mistério; se em Candlebrook o
Halloween dura a semana toda; qual brincadeira entra no teste; quando o teste entra na fila; e se a rua pode ter
vizinhos com nome que não fazem parte de um caso. Para cada uma há uma recomendação.

## 1. The idea in one paragraph

Pip is the new kid on the street, and Halloween is the one night a whole street opens its doors. The free world is
therefore not a second game beside the mystery; it is **Pip meeting her neighbours, door by door**, which is the
belonging arc of the bible (§3) played out in the open. The mysteries stay the spine (D26, D27). Around them, the
street is dense with people who have their own evening, small things to do that tie into the Night of Lanterns,
and a home porch that fills up with the child's own work. Everything the child does there is remembered, and none of
it touches a clue.

**Three story anchors** keep it coherent rather than filler:

1. **Lantern Week** (canon proposal, owner decision 2). In Candlebrook, Halloween lasts the whole week of the
   season (`world-and-cases.md` §5: every case happens on an evening of one Halloween week). Every evening, a porch
   with a lit lantern gives treats; a dark porch does not. That is the American porch-light custom, in this
   town's own words. It explains why trick-or-treaters walk every case's evening, and why they skip the shut owl
   house in case 01.
2. **The Lantern Walk.** The big Night of Lanterns has not happened for years, but the street's children still do a
   little evening walk with paper lanterns, led by a grown-up. It is small at first. It grows by a lantern or two after
   every solved case (the street remembers) and becomes the festival's parade in the finale.
3. **Pip's porch.** Today it holds moving boxes, the candy bowl, the bat mobile and the dress-up trunk (S004). In the
   free world it becomes the child's own Halloween porch: a lantern she made, a pumpkin she carved, a photo she took.
   Once her lantern is lit, groups ring her bell too, and Pip gives treats. That is the new kid becoming a neighbour.

## 2. The map

### 2.1 What exists

The case 01 block is 44 × 36 m (`EnvironmentLayout.BlockHalfX/Z` = 22 × 18), origin at the plaza, north +Z. Lantern
Lane runs west to east through the plaza: Pip's arrival porch on the south side (0, −13), the owl house on the
north side (0, +13) with its side yard and treehouse to the west, Bea's bike corner at (−7.2, −1.3), Vale's bench at
(+8, 0), the sidewalk along z = 3.5. Boundary hedges close all four edges (x = ±21.5, z = 17.5 and the south row).
Five neighbour houses stand beyond the hedges as skyline only. The owl house interior lives far up the lane at
z = 60 (D24.1).

### 2.2 How it grows

**The heart stays as it is.** The case 01 block is called the *heart* below. Its geometry, clue targets,
sightlines, paper bats and tests stay unchanged. Only two changes are made there: the west and east boundary hedges
open where the lane runs through (a gap about 6 m wide at z −3…+3), and the north hedge stays closed, so the owl
house's yard, the side yard and the treehouse get no new approach (CASE-03 and the mandatory route are untouched).

The lane continues on both sides, and each end **bends**, so from any point the child sees the heart plus at most
one end. That is a level-design choice for coherence, because each end reads as its own corner, and for
performance, because sectors can be culled (§8).

| Area | Size (approx.) | What it is | Connects to |
|---|---|---|---|
| **Heart** (case 01) | 44 × 36 m, unchanged | the mystery's block | both ends through the hedge gaps |
| **East lane** | x +22…+46 | three porch houses (two north, one south): the slice's trick-or-treat doors | the Green |
| **The Green** (pocket park) | x +46…+64, z −10…+14 | a corner green with string lights, the lantern table, the photo board, the apple tub, a low wall to sit on | a closed park gate toward Hollow Park |
| **West lane and the bend** | x −22…−48 | three more porch houses, the party house; the lane climbs gently and bends north | stone steps toward Bramble Row, closed by a cider table |
| **Side alley** | x −30, z −4…−22 | a narrow, lantern-lit passage between two south houses (2.4 m wide, camera-safe, D24 band clear) | the backyards |
| **Backyards** | x −44…+46, z −22…−36 | fenced yards behind the south row: washing lines, a pumpkin patch, a garden shed, Pip's own backyard with a tyre swing | the alley (west) and the Green (east) |

The walkable area grows from about 1,600 m² to about 4,800 m² (≈ 3×). Spec §4's density rule still holds
everywhere: a meaningful detail every ~1.5 m of path, a decision or response every 15–25 s, and space is added only
where it gives a new use, a route or a view. The backyards are where the moveset earns its keep: fence tops, shed
roofs and washing-line posts make a rooftop-and-fence route from the alley to the Green, so the vertical playground
(spec §4) continues beyond the heart.

**Toward the next places, without spoiling them.** The world map stays a connected walk (world-and-cases §1).

- **West: Bramble Row.** Past the bend, tall narrow roofs with washing lines show on the skyline, and stone steps
  climb toward them. The steps are closed by a cider table where two Bramble Row grown-ups chat (a party table
  "visually closes" a passage, spec §4). No light moves on those roofs before case 02: the walking lantern is case 02's
  own intro.
- **East: Hollow Park.** Beyond the Green, an iron park gate stands closed with a lantern on its post, and the
  hilltop's dark curve and a tree are visible beyond it. There are no kites and no bandstand wind wheel: those belong
  to case 03.
- Each case's own design doc decides when its gate opens. The living street only frames the gates.

### 2.3 Plan sketch (north up, not to scale; x in metres)

```
                     tall roofs + washing lines (Bramble Row skyline, closed)
                              ^
                        stone steps, closed by the cider table
                              |
 x: -64        -48         -22                     0                    +22         +46           +64
               .  bend  .   +---------------- north hedge (closed) ----------------+
              .  [W1]  [PARTY]|  side yard   OWL HOUSE (dark porch)                 | [E1]   [E2]  +---------------+
             .               |  treehouse    yard, gate, fence                     |              |   THE GREEN   |    park gate
            .                |                                                     |              | lantern table |==> (closed)
  ===========================  gap  Bea's bike    PLAZA (maple)     Vale's bench   gap ===========| photo board   |  Hollow Park
     LANTERN LANE  (west)    |                                                     |  (east)      | apple tub     |  hill beyond
                 [W2]  [W3]  |                  Pip's porch (arrival)              |     [E3]     | low wall      |
                  |  alley   +------------------- south hedge ---------------------+      |       +-------+-------+
                  |   ||                                                                  |               |
 ---------------+-----||------------------------------------------------------------------+---------------+---------
   BACKYARDS    |  fence tops, washing-line posts, garden shed, pumpkin patch, PIP'S BACKYARD (tyre swing)  |
 ---------------+------------------------------------------------------------------------------------------+---------

 [E1] [E2] [E3]  the slice's doors (Mr. Fogg, Nana Pearl, the Abbotts)
 [W1] [W2] [W3]  later doors                [PARTY]  the Hallorans' party house (§5.1)
 The mask maker's shop front (§5.2) stands on the Green's south-west corner, beside E3, at scale.
 Their interiors stand far from the street, like the owl house interior (D24.1).
```

## 3. People of the street

### 3.1 Who is out tonight, and why

Every person has a reason to be on the street that night (world-and-cases §2 rule 1). Everyone is friendly and
spooky-cute: humans in Halloween costumes with the cast's big friendly eyes (D17, D18).

| Group | Who | Why they are here | What they do (loop) |
|---|---|---|---|
| **Trick-or-treat groups** (2–3 children + 1 grown-up) | unnamed families from the crowd kit (§7) | Lantern Week | walk a door-to-door route, wait at a lit porch, cheer, move on; they skip dark porches (the owl house in case 01) |
| **Door neighbours** (named) | Mr. Fogg, Nana Pearl, the Abbotts (slice); four more at scale (§4.1) | they give treats from their porch | open the door when a group or Pip rings; each has a door gag |
| **Strollers** | unnamed couples and grandparents | an evening walk to see the porches | stroll a long loop, stop to look at decorations, wave at Pip |
| **Future-case residents** | Coach Amara (scarecrow) and Theo (dragon, 7) with his dad; the Penn twins (skeleton, ghost sheet) with their mum; Mrs. Ortega after the case 01 stamp | they live nearby (Hollow Park, Bramble Row) and come for the porches | Amara leads the Lantern Walk; the Penns trick-or-treat; Theo watches the moths around the lamps |
| **Recurring cast** | Bea, Mr. Vale, June, Aunt Kit | as their case says | see §3.3: their arcs are not changed |

### 3.2 Future-case residents appear early, without their mysteries

- **Coach Amara** leads the Lantern Walk because she is the street's coach: short, cheerful commands ("Lanterns up!
  Nice and slow!"). She never mentions kites or the park's wind.
- **Theo** is shy and stays near his dad; his small side story is §4.6. He never shows the wind wheel he will hide in
  case 03.
- **The Penn twins** trick-or-treat with their mum and argue happily over who is the spookier ghost. They never talk
  about rooftops.
- **Mrs. Ortega** comes down from Bramble Row only after case 01's stamp: she walks the lane lighting the post
  lanterns with a long pole, which is her love of keeping lamps lit (world-and-cases §2). Her costume is an
  owl-and-moon witch, and during case 01 an owl motif in the crowd would sit too close to Q-moved's answer, so she
  waits for the stamp. She never climbs and never shows the roof-lamp trolley.
- **The rule:** a future resident may show their desire and their costume, never their mystery's object, place or
  secret. Each later case's design doc may adjust what its residents do on Lantern Lane, and that doc owns the
  change.

### 3.3 The recurring cast keep their arcs

| Character | During case 01 | After the stamp | Never before their case |
|---|---|---|---|
| Bea | at her bike corner (her testimony's position) | rides the Lantern Walk's loop on her bike now and then, waving (her lookout arc) | — |
| Mr. Vale | on his bench, and now he hands sweets from his cauldron to passing groups (he is a watcher, so the street comes to him) | the same, with his saved sweet (D29) | he never leaves his bench before case 05 |
| June | in the attic (she is the missing witness) | outside at her treehouse way (D29); she offers hide and seek (§4.7) | — |
| Aunt Kit | absent | at her booth; children gather for the owl's show on the hour | she never tells a Player's name in free play (one per case, world-and-cases §2) |

### 3.4 Behaviour rules for everyone in the crowd

1. **Nobody testifies.** Passers-by never speak about the knocks, the owl house, June or the case. They do not react
   to the knocks (they are busy with their own evening, and the knocks are a quiet house sound). Only Bea and Vale are
   witnesses.
2. **No glances toward the case.** The tier-1 hint uses a witness's glance and a breeze toward an area (spec §5). No
   crowd idle, head turn or point ever faces a hint area, so nothing looks like a hint.
3. **Out of the clue zones.** Walker routes keep at least 1.5 m from every examine target that can become a sticker,
   the decoy included (the same rule as the paper bats, S004 §2.1 rule 2), stay out of the side-yard corridor, the
   treehouse, the bridge and the workshop, and never stand in the bench-to-door sightline while Pip sits.
4. **They make way.** Walkers slow and step aside when Pip comes within about 1 m in front of them. They never block
   the ramp, the gate, a doorway or a narrow passage, and Pip never gets stuck on one. The camera boom ignores them.
5. **No knocks.** At doors people ring bells and call "Trick or treat!" Nobody knocks, and no crowd sound has a
   three-beat rhythm. The case's knocks keep their meaning, and the crowd's sound ducks under each knock cycle.
6. **Reserved looks.** No crowd costume uses cloth silver (June's cape and its clues), lavender foil (the decoy), a
   hero's costume theme (bat cape, pumpkin, star witch, mummy, fortune teller), a future resident's theme (scarecrow,
   dragon, pirate, gingerbread witch, wizard, ghost-in-overalls) or one of the Players' puppet motifs before its case
   (owl, fox, moth, moon, bell). Pip stays the only bat cape on the street, so a child always finds her.
7. **Cutscenes are staged.** While a cutscene plays, the crowd holds authored positions (or stays out of shot). The
   opening's arrival composition keeps the owl house's negative space clear.

## 4. Free activities

Each is optional, always available (no time of day, no limited event), playable with touch, gamepad and keyboard
(D25), readable without words (D23.1), and never counted in front of the child. What the child gets is **a reaction,
a picture and a change in the street**, never currency. Each is saved in a small `neighbourhood.json` beside the
street file (the `StreetStore` pattern: atomic write, previous copy, recovery). Replay case keeps it; a confirmed New
game clears it, like everything else (D31).

**The street page.** The notebook gains one scrapbook page for the street: a drawing of Pip's porch as it is now, the
neighbours she has met (face discs) and the photos she took. It follows the keepsakes rules: no empty slots, no
silhouettes of things not yet done, no "x of y" (S004 §2.1). Only the grown-ups page may say "neighbours met: 5".

| # | Activity | What the child does | What she gets | How the street remembers | Story tie |
|---|---|---|---|---|---|
| 4.1 | **Trick-or-treat doors** | walks up a lit porch, rings the bell (Use) | a door gag, a treat that shows in Pip's pail (no number), the neighbour's face on the street page | on a second visit the neighbour greets Pip by name ("Back again, Pip?"); a third gives a different gag | belonging: meeting the street door by door |
| 4.2 | **The paper lantern table** (at the Green) | picks a shape (moon, bat, star, pumpkin) and a colour from pictures; Pip folds it | a lantern she carries in the Lantern Walk | it hangs lit on Pip's porch; from then on groups ring **her** bell and Pip hands them a sweet from her candy bowl | the Night of Lanterns; Pip becomes a neighbour |
| 4.3 | **The Lantern Walk** (a loop of about 2 min) | joins the line behind Coach Amara and walks (no steering test; she can step out any time) | a cheer at the end; Amara saves a spot | after the first walk, children wave to Pip; after each solved case the walk gains a lantern (and later, a lantern shaped like the woken machine) | grows into the finale's parade |
| 4.4 | **Apple bobbing** (tub at the Green) | leans in with Use when an apple bobs close | an apple in the pail and a dripping, giggling Pip | the tub keeps an apple with a bite out of it | Halloween tradition |
| 4.5 | **Pumpkin carving** (at Nana Pearl's porch table at scale) | picks a face from four drawings; Nana does the cutting, Pip scoops and draws | her pumpkin | it glows on Pip's porch steps | Pip's porch fills up |
| 4.6 | **Photo board** (at the Green) | puts her face through a painted face-in-hole board of a lantern parade | a photo on the street page | the photo is pinned to Pip's front door | rhymes with the troupe photo and the finale's new photo, without replacing it |
| 4.7 | **Hide and seek with June** (after the case 01 stamp) | June hides in one of six spots in the backyards, the alley and the Green; she taps her knee in her rhythm when Pip is close | June's laugh and a new spot next time | June's greeting changes ("Bet you can't find me tonight!") | June's arc: from rule-bender to partner |
| 4.8 | **Small side stories** (§4.9) | helps a neighbour with a need the world shows | a thank-you, a face on the street page | the person's greeting changes | each tied to a resident's desire |

**Rules for every activity.**

- **No fail state and no clock.** Apple bobbing has a wide, slow window that widens after each miss and always
  succeeds by the third try. The Lantern Walk is a loop you can join on any lap. Hide and seek has no countdown: June
  waits as long as it takes and gives her knee-tap rhythm more often if Pip wanders off.
- **No pressure to finish.** Doors are not marked as done; there is no list of doors. Missing the Walk means it comes
  round again. Nothing is lost by not playing today (engagement §2.3).
- **Placed, fixed rewards** (engagement §2.5): every treat, gag and picture is authored; nothing is random.
- **Mechanics serve exploration** (bible §5): the doors and the hide-and-seek spots lead the child along the new
  streets, fence tops and vantage points.

### 4.9 Side stories

Each side story is raised by the world, never by a task list: a grown-up looks around and calls a name; a child
stands alone at a photo board. There is no quest log and no marker. The radio never hints them, because hints are
for the case (§6).

1. **The little brother who wandered off.** Theo's dad stands at the Green, turning around: "Have you seen a little
   dragon?" Theo is not lost or scared: he is watching the moths circle a lamp in the backyards, and he does not want
   to leave yet. Pip finds him, he follows her back (June's follow path is reused), and his dad lifts him onto his
   shoulders. Theo's greeting afterwards: a shy wave. Theo's case 03 secret is untouched.
2. **A photo with friends.** After the case 01 stamp, Bea asks for a photo at the board, but "we need everyone!"
   Pip brings June, and Mr. Vale waves from his bench, so the board's photo is taken at the bench end of the Green. It
   is a small echo of the finale's troupe photo with Pip in it; the finale keeps the big one.
3. **Rule for later side stories:** no side story may echo a case clue pattern. No cloth caught on something high,
   no light at a window, no sound from a shut house, no cat on a roof (case 02's mistaken ghost). The free world never
   teaches a child to solve a case by a different route.

### 4.10 Sample lines (drafts, in the characters' voices; keys and translator context come with the build)

Short sentences, common words, no fear words; Pip is a girl (D30). Portuguese stays within +35 %.

| Speaker | English | pt-BR |
|---|---|---|
| Pip (at a door) | Trick or treat! | Doce ou travessura! |
| Mr. Fogg (booming, silly) | Who rings my bell? A little bat! | Quem tocou a campainha? Uma morceguinha! |
| Mr. Fogg (again) | Back again, Pip? One more, then! | Voltou, Pip? Então mais um! |
| Nana Pearl (slow, warm) | Oh, a new face! Welcome to the street, dear. | Ah, um rosto novo! Bem-vinda à rua, querida. |
| The Abbotts (two dads, a duet) | Trick or treat? We pick… treat! | Doce ou travessura? Escolhemos… doce! |
| Coach Amara | Lanterns up! Nice and slow! | Lanternas pra cima! Bem devagar! |
| Theo's dad | Have you seen a little dragon? | Você viu um dragãozinho por aí? |
| Theo | The moths are dancing. Look! | As mariposas estão dançando. Olha! |
| June | Count to ten. No peeking! | Conta até dez. Sem espiar! |
| June (found) | You found me! Again? | Você me achou! De novo? |
| Bea | A photo! We need everyone! | Uma foto! Chama todo mundo! |

## 5. Enterable spaces that are not a case

Both are **bigger on the inside** (D24.1): the street-scale house fronts stay unchanged, and a 0.5 s page-turn at the
front door leads to a roomy, closed interior built for the third-person camera (D24: real walls and ceiling, a 2 m
boom fits, nothing in the 1.6–2.6 m band over walkable paths). They stand away from the street like the owl house
interior, so there is no scene loading. From outside, their windows show a lit interior card.

### 5.1 The Hallorans' Halloween party (west lane)

- **What it is:** a family's living room and kitchen dressed for a party: paper garlands, a snack table, music, a
  cardboard castle the children built (a small vertical playground with soft blocks), grown-ups chatting in pairs,
  and a dance corner where Pip dances when she presses Use (one new Pip clip).
- **What the child gets:** a dance with the other children, a party hat from the snack table that stays on only
  while inside (it never competes with Pip's earned costume pieces), and a picture for the street page.
- **Story tie:** on the wall hangs a framed poster of an old Night of Lanterns, the whole street lit. Grown-ups may
  remember the festival as a happy thing ("Those lanterns! Every porch!"). They never name a Player and never say
  where a machine is: that lore is Aunt Kit's to tell, one name per case.
- **Street remembers:** after Pip's first dance, the party waves her in at the door and the window card shows her
  photo on their fridge.

### 5.2 The mask maker's (east corner, at scale)

- **What it is:** Ms. Odile's paper-mask workshop, closed for business tonight and open as a try-on party. Nothing
  is sold and there is no till (spec §1: no currencies). Masks hang on strings; a big mirror stands at one end.
- **What the child does:** tries masks in the mirror (a cat, a pumpkin, a friendly ghost, a moon-faced jester); the
  mirror "snaps" a picture for the street page. The masks go back on the hooks at the door ("Back on the hook!"), so
  they never compete with the keepsake costume pieces.
- **Why masks and not sewing:** a mending room would put torn costumes next to the case's key clue (June's cape
  tear). Paper masks fit the felt-and-paper style and stay clear of the case.

## 6. Coherence rules (hard rules for every free-world item)

1. **Never evidence.** Nothing in the free world can become a sticker, a testimony or a notebook entry of a case. No
   free-world object looks like a clue of the current case (no silver cloth, no foil streamer, no owl motif in case
   01, no three-beat knocks, no lit attic windows).
2. **Never blocks a clue.** No walker, activity, Use target or interior entrance lies on a clue's path, within 1.5 m
   of an examine target, or on the mandatory route's narrow parts. The Use target picker prefers case targets when
   both are in range, and free-world targets are placed so that never happens in the heart.
3. **Hints are unchanged.** The radio's tiers, areas and the stuck-deduction musing stay exactly as D26 set them. They
   never point at a free-world thing, and a free-world thing never points at a hint area.
4. **The heart's tests stay green.** Route, sightline (CASE-03), paper bat placement, collision and density tests of
   case 01 are unchanged; new tests prove the crowd's exclusion zones, the hedge gaps and that the bench view is the
   same.
5. **Story facts hold.** Machines stay mechanical; nobody is hurt or punished; Vale stays on his bench until case 05;
   Kit names one Player per case; the fifth Player of the troupe photo is never met in free play (the finale owns
   that face).
6. **The mystery stays the main line.** The case's question, cutscenes and notebook keep their priority. Activities
   pause while the notebook or a cutscene is open, and a case question raised by the world always wins the screen.
7. **Engagement guardrails** (engagement §2) apply in full: no ads, purchases, timers, streaks, daily rewards,
   limited events, random rewards, social sharing or totals in the child's flow.
8. **Strings** live in `en.json` and `pt-BR.json` with identical translator contexts, keyed by stable IDs
   (`street.free.*` for memories, `line.<speaker>.*` for neighbours), never keyed off display text.

## 7. Production: the crowd costume kit

**What we reuse.** Every shipped character already shares Pip's skeleton (same bone names and axes) plus costume
bones (`tools/blender/export/witness_export.py`, `junekit_export.py`), and the art bible sets kids at about 4.5 heads
and adults at about 6. The crowd kit extends that pipeline in `tools/blender/` (graphics standards §1: never restart
a pipeline).

**Three base bodies** on Pip's skeleton, each with its own bind proportions:

| Body | Height | Built from | Clips |
|---|---|---|---|
| Small child | ≈ 1.05 m, 4.5 heads | Pip's and Bea's proportions | Stroll, Idle, HoldOutPail, Cheer, Wave, LookUp |
| Older child (scale phase) | ≈ 1.3 m | June's proportions | the same set |
| Grown-up | ≈ 1.7 m, 6 heads | Vale's and Kit's proportions | Stroll, Idle, HandOutTreat, Wave, ChatLoop, HandOnShoulder |

The clips are in place, start and end on the rest loop's frame 0 so they blend (graphics standards §2), and a pair
(parent and child) walks side by side; there is no hand-holding in the slice, because matched pairs cost a lot to
animate well.

**Swappable parts.** Each body takes a head piece (hat, mask, hood, ears), a torso overlay (sheet, cape, printed suit,
tunic), a hand prop (pail, lantern, broom, wand, paper bag) and a palette variant from the v1.3 Halloween families.
Parts are skinned to the base skeleton and **merged into one skinned mesh per walker at spawn**, on one shared crowd
atlas, so each walker is one draw call. Faces are painted on the atlas with a two-frame blink.

**Costume themes** (none of them reserved, §3.4 rule 6): ghost sheet, skeleton suit, black cat, green or plum witch
(no stars, no silver), friendly spider, candy corn, patchwork giant, cardboard robot, bumblebee, frog prince, and a
paper-crown king or queen. Pumpkins, bats, capes with wings, wizards and dragons stay out, because the cast and the
future residents wear them.

**Targets.** Slice: 2 bodies, 6 costumes, at least 8 distinct walkers. Scale: 3 bodies, 11 costumes, 3 palettes and
4 props, of which at least 24 authored combinations are used, with at least 16 silhouettes that differ at phone size
from the gameplay camera (3.8 m, 50°).

**Art standards** (`docs/graphics-agent-standards.md`, `docs/art-bible.md`): original and made by code; same
materials, outline, palette and roughness as the cast; silhouette first at phone size; finished anatomy (fingers,
shoes, folds) at LOD0; hands never pass through costumes in any frame; review sheets rendered from the exported FBX
(gameplay distance, close, side or back, 8+ frames per clip), then the CI frames in game lighting; premium scorecard
4 of 5 on every axis for anything called finished. A mass-produced look is the main art risk (§9), so every costume is
reviewed on its own before it joins the kit.

**Houses.** New porch houses come from the existing modular porch kit (floor, steps, post, rail, wall, window, door,
roof, corner), varied by roof pitch, rail rhythm, paint and decor (spec §6). Each door has a bell, a porch lantern
(`*_Glow` under the attic window's level) and a door gag rig (a hinged part, never a blend shape).

## 8. iPhone performance budget

**Targets** (spec §8): stable 60 fps on the owner's iPhone; after 15 minutes of traversal, 95th-percentile frame time
≤ 18 ms and 99th ≤ 33.3 ms, against the 16.7 ms ideal; Android reference 30 fps. **No device frame time has been
recorded yet**, so the slice's first job is to measure the current game and the slice on the phone (§9).

| Item | High tier | Medium | Low |
|---|---|---|---|
| Fully animated walkers in view (LOD0 up to 8 m, LOD1 8–18 m) | 8 | 6 | 4 |
| Paper standees in view beyond 18 m | 12 | 8 | 6 |
| Walkers alive per sector (the rest wait off-screen on their loop) | 24 | 18 | 12 |
| LOD0 / LOD1 triangles per walker | ≈ 7k / ≈ 2.5k | same | LOD1 only |
| Draw calls | 1 per walker; standees in one instanced call | same | same |
| Shadows | a soft paper disc under each walker; no real-time crowd shadow | same | same |
| Crowd CPU (logic + animation + skinning dispatch) | ≤ 1.2 ms | ≤ 1.0 ms | ≤ 0.8 ms |
| Crowd GPU | ≤ 1.3 ms | ≤ 1.0 ms | ≤ 0.8 ms |
| Crowd textures | one 2048 atlas (no normal map past LOD0) | 1024 | 1024 |

**How it stays cheap.**

- **Walkers follow authored loops, not a navigation mesh.** An engine-free `Core` crowd clock (like `BoothShowClock`)
  gives each walker's position as a pure function of time: deterministic at 30 and 60 fps, frozen by pause, nothing
  saved, testable in `tools/core-tests`. A small yield rule handles Pip in front of them.
- **Animation culling.** Off-screen walkers advance along their loop with no animation evaluation; their Playables
  graph pauses. Walkers beyond 12 m update their animation at 15 Hz.
- **Paper standees** for impostors: beyond 18 m a walker becomes a flat paper cut-out with a 4-frame bob, which fits
  the felt-and-paper style (the sky's clouds already work this way, D22).
- **Sectors.** The bends mean each view holds the heart plus one end. Each end and the backyards are separate
  static-batched roots that are culled as a whole when out of view; interiors are culled entirely while Pip is
  outside.
- **Audio.** Crowd voices play as one babble loop per group (at most 4 at once), plus doorbells and the party music,
  all ducked under the knocks.
- **Order of cuts** if the phone is short (spec §8: shadows and translucency before evidence): standees first, then
  the walker count, then LOD1 distance, then the south hedge row behind the arrival camera (saves ≈ 16k triangles,
  S000.4). Evidence silhouettes are never cut.

## 9. Phasing, estimates and risks

Estimates are in agent-days (one Opus session ≈ 1 day of work, CI included). At most two agents work at once
(CLAUDE.md), so calendar time is about half. Agent profiles: `artista-3d`, `dev-jogo`, `textos-e-traducao`,
`revisor-visual`.

### Phase 0: this design (now)

The owner approves it or changes it (§10). Afterwards D33 is updated with the decisions, and `world-and-cases.md`
gets the canon lines (Lantern Week, the Lantern Walk, the free-world neighbours rule).

### Phase 1: test slice on TestFlight (≈ 18 agent-days, one build)

Scope: the east lane and the start of the Green, 6–8 walkers, 3 trick-or-treat doors, 1 activity (recommended: the
paper lantern table, owner decision 3), a frame-time readout.

| Work | Who | Days |
|---|---|---|
| Core crowd clock, loops, yield rule, exclusion zones, tests at 30/60 fps | dev-jogo | 2 |
| Crowd kit v1: small child and grown-up bodies, 6 costumes, clips, review sheets | artista-3d | 4 |
| East lane: three porch houses from the kit, lane extension, the Green's first corner, hedge gap, route and sightline tests | artista-3d 2 + dev-jogo 1 | 3 |
| Doors: bell, three gags, treats in the pail, first and repeat lines, vignettes, voices, pt-BR | dev-jogo 1.5 + textos-e-traducao 0.5 | 2 |
| Lantern table: shape and colour picker, Pip's porch lantern, groups ringing Pip's bell, her treat | dev-jogo 2 + artista-3d 1 | 3 |
| `neighbourhood.json` and the street page | dev-jogo | 1.5 |
| Performance: LOD1, paper standees, culling, quality tiers, a frame-time readout (p95, p99) behind the parent gate in TestFlight builds only | dev-jogo | 1.5 |
| CI run, frames for clipped text and costume clipping, evidence doc | revisor-visual | 1 |

**Done means:** localisation and Core tests pass; case 01's existing tests are unchanged and green; one green CI run
with frames reviewed; a TestFlight build in which the owner reads the frame times and plays case 01 with the crowd.
**Gate to phase 2:** the phone meets the targets above (or the order of cuts gets it there), and the owner (later a
family playtest) finds the crowd lively without pulling attention from the mystery.

### Phase 2: scale (≈ 40 agent-days, two builds)

| Build | Work | Days |
|---|---|---|
| **Scale A** | west lane and the bend (3 houses), backyards and side alley with fence-top routes and Pip's backyard (8); the Green finished with photo board, apple tub and the park gate (2.5); crowd kit v2 with the older-child body and 24 combinations (4); four more doors and Pip's own door, 8 in all (2.5); the Lantern Walk loop that grows per case (2); apple bobbing (1.5); pumpkin carving (2); crowd audio (1.5) | 24 |
| **Scale B** | the Hallorans' party interior (3.5); the mask maker's interior (3); hide and seek with June (2); the two side stories (3.5); localisation pass and the owner's review page EN, pt-BR and context (1.5); device performance pass and frames (2) | 15.5 |

Total for D33: about **58 agent-days**, roughly a month of calendar time with two agents. That is about the size of
S004 and case 02 together, so the owner should weigh it against the season's order (decision 4).

### Risks

| Risk | Likelihood | Effect | Soft net |
|---|---|---|---|
| The phone cannot carry the crowd (there is no device baseline yet) | medium | stutter, heat | measure first in the slice; quality tiers; the order of cuts; standees |
| The crowd or the activities pull the child away from the mystery | medium | slower cases, missed clues | exclusion zones, no testimony, no glances; owner decision 1 can keep the heart quiet until the stamp; watched in the family playtest (D33's revisit condition) |
| A mass-produced crowd looks generic next to the hero cast | medium | premium bar missed | each costume reviewed alone; fewer, better costumes before more; scorecard 4/5 |
| A free-world thing reads as a clue or a hint | low, if the rules hold | breaks D26 | §3.4 and §6 rules, the conclusion lint on new keys, a spoiler check per new item |
| Scope competes with case 02 and the weekly agent limit | high | the season slips | two agents at once; phase gates; the slice can stand alone |
| Case 01's tests or saves regress | low | broken case | the heart stays unchanged; a separate save file; case 01 tests run unchanged |
| More strings to translate | certain | review time | short lines; one review page per build |

## 10. Owner decisions

Five decisions need the owner. Routine choices taken without blocking are listed after them, and the owner may
change any of them.

1. **The crowd during case 01's mystery.**
   (a) Busy from the first minute: groups and strollers fill the lane and the ends, and at most three walkers at a
   time pass through the heart, all outside the clue zones. The dark owl house is skipped.
   (b) The ends are busy from the start, but the heart stays quiet (only Vale's visitors) until the case is closed;
   then the crowd comes in for the owl's show.
   (c) No crowd until the case is closed.
   **Recommended (a):** case 01 is most of a first session, so (b) or (c) would hide the density the owner asked for.
   The rules in §3.4 keep the clues clear, and (b) remains the fallback if the playtest shows distraction.
2. **Lantern Week (canon).**
   (a) In Candlebrook, Halloween lasts the whole week: every evening, a lit porch lantern means treats, so the
   street is alive in every case.
   (b) Trick-or-treating happens only on case 01's night; later cases have a quieter street.
   **Recommended (a):** it fits the one-week timeline, explains the dark owl house, and ties the porch lanterns to
   the Night of Lanterns.
3. **The slice's one activity.**
   (a) The paper lantern table (Pip's porch lights up and groups ring her bell).
   (b) Pumpkin carving.
   (c) The photo board.
   **Recommended (a):** it tests the story tie, the crowd coming to Pip and the street remembering in one piece.
4. **When the slice is built.**
   (a) Next: after the two branches already in flight (the accessibility pass and the sticker memories) are merged,
   before case 02's design, and the family playtest then watches both the mystery and the crowd.
   (b) After the family playtest of the mystery alone, then the slice.
   **Recommended (a):** the owner asked for density after build 52, and a playtest with the crowd answers D33's own
   revisit question.
5. **Named neighbours who are not part of a case.** The cast rule allows one new face per case (world-and-cases §2,
   rule 3).
   (a) Allow a small set of named free-world neighbours (Mr. Fogg, Nana Pearl, the Abbotts, the Hallorans, Ms. Odile,
   and later a few more), each written with the cast framework and never a witness or a clue. The one-new-face rule
   stays for cases.
   (b) Keep passers-by unnamed. Doors are opened by generic grown-ups.
   **Recommended (a):** being greeted by name, door by door, is what makes the new kid belong, and children remember
   people with names.

**Routine choices taken (reversible):** the heart is unchanged except for two hedge gaps; no new paper bats (the 30
stay as tested); free-world memory goes in its own file; New game clears it and Replay keeps it (as D31); bells,
never knocks; the reserved-looks list (§3.4 rule 6); Mrs. Ortega appears only after case 01's stamp; Vale stays on his
bench; masks instead of a mending room; walkers on authored loops rather than a navigation mesh; paper standees as
impostors; the frame-time readout only in TestFlight builds behind the parent gate.

## 11. Narrative bible checklist (§6), run on this design

| # | Check | Result |
|---|---|---|
| 1 | Kind, villain-free true story explaining every clue and testimony | Case 01's true story is unchanged. The free world adds no clue or testimony; its own small stories (the wandering little brother, the photo, Lantern Week) are kind and need no explanation. |
| 2 | Questions raised by the world, not tasks | Case questions unchanged. Side stories are raised by what a person visibly needs; there is no quest log and no task list. |
| 3 | Answers only from placed stickers | Nothing in the free world becomes a sticker or a notebook entry of a case (§6 rule 1). The street page is a scrapbook, not a deduction page. |
| 4 | Diegetic attraction, no precision moves on a clue's path | No free-world item is on a clue's path or within 1.5 m of a target. Doors attract by their lit lanterns, June by her knee-tap rhythm; fence-top routes are optional. |
| 5 | Testimony as perception, partial, from a stated position | Only Bea and Vale testify; passers-by never speak about the case (§3.4 rule 1). |
| 6 | No vignette, caption, line or replay states a conclusion | Free-world lines are greetings and gags. The conclusion lint covers the new keys in English and Portuguese. Grown-ups never name a Player or a machine's place. |
| 7 | Hints to areas, moves only | The radio is unchanged and ignores the free world; crowd glances never face a hint area (§3.4 rule 2). |
| 8 | At most one decoy | Unchanged. No crowd costume uses foil or silver, so the free world adds no accidental decoy. |
| 9 | Reveal pays off the child's clues and action | Unchanged. After the stamp, children gather for the owl's show the child caused. |
| 10 | No deadlocks in any order, with tests | Free-world state lives in its own file and changes no case fact; walkers yield and never block; case 01's route and order tests are unchanged and new exclusion-zone tests are added. |
| 11 | The street visibly remembers | Pip's porch fills with her lantern, pumpkin and photo; groups ring her bell; neighbours greet her by name; the Lantern Walk grows after every case. |
| 12 | Seeds and continuity | Bramble Row's roofs and Hollow Park's gate frame the next places without their mysteries; future residents show desires, not secrets; Vale, Kit and the fifth Player keep their arcs (§3.3, §6 rule 5); the timeline is the one Halloween week. |
| 13 | New mechanics serve the mystery or exploration | Doors, hide and seek and fence-top routes teach the larger map and its vantage points; the lantern ties to the season's spine; dancing, masks and apples are small touch rewards (spec §6 *Interactions*). |
| 14 | Strings in `en.json` with context, stable IDs | Every new line is a key with translator context in `en.json` and `pt-BR.json`; memory IDs are `street.free.*`; nothing is keyed off display text. |
| 15 | Engagement guardrails | No currency, counter, timer, streak, limited event, random reward or sharing; the Walk is a loop, not an event; totals only on the grown-ups page. |
| 16 | Cutscenes short, skippable, rewatchable, six purposes, never ahead | The design adds no cutscene. During case cutscenes the crowd is staged (§3.4 rule 7). Any later free-world moment is played in the world, not as a cutscene. |

All sixteen pass, as long as the rules of §3.4 and §6 hold. Each later item (a new door, side story or interior)
runs this checklist again in its own design note before it is built.
