# Comic Surfer VR — Master Reference
**v2 — updated 2026-05-25. Verified against all .cs files after ComicWorld fix session.**

---

## Project Overview

Unity 6 (6000.3.8f1), URP, Meta Quest, Android build target.
XR Interaction Toolkit 3.0.8 (Starter Assets) — namespace `UnityEngine.XR.Interaction.Toolkit.Interactables`.
All positions are **world-space, meters, Y-up**.

**Two scenes:**
- Scene 0 `Assets/Karen Room.unity` (build index 0) — thief intro
- Scene 1 `Assets/ComicWorld.unity` (build index 1) — main game

**Do not touch / confirmed working:**
- Karen Room: thief sequence (`ThiefSpawner`), book portal auto-trigger (`BookPortalTrigger`), time tunnel transition (`TimeTunnelTransition`)
- ComicWorld: dialogue auto-advance (`DialogueSystem.autoAdvanceDelay=4f`), Phase 1 immediate start, controller input (`XR Origin` + `InputActionManager`)

---

## Story

**Player is Karen.** A thief jumps out of her favorite comic book, steals her phone, and jumps back in. She follows him into the comic world.

Inside, she discovers the story of **Harriet** — a teen who witnessed her friend Ava's accident and developed psychogenic blindness. A witch friend named **Devi** hypnotizes Harriet to help her recover by finding her "happiest moment" — a childhood teddy bear.

The thief (also inside the comic) thinks the teddy bear's eyes are made of gold and tries to steal it.

---

## Phase Flow

The table shows trigger → owner → what happens. **ComicWorldManager** owns all phase transitions.

| Phase | What triggers it | Script that calls it | What happens |
|---|---|---|---|
| **Arrive** | Scene loads | `ComicWorldManager.BeginGame()` | Arrival dialogue (non-blocking), 35 grey BG frames spawn, Frame 1 activates immediately |
| **Phase 1** | Immediately after arrive | `ComicWorldManager.StartPhase1()` | Player grabs Frame 1 edges (both controllers); FrameEnlarger scales frame; teddy scales from flat→3D |
| **Phase 2** | Teddy grabbed | `TeddyBear2Dto3D.OnTeddyGrabbed` → `ComicWorldManager.OnTeddyGrabbedPhase1()` | Frame 1 shatters, Frame 2 appears at same position, grey BG frames shatter, pastel BG frames spawn, Frames 3-16 activate |
| **Phase 3** | After Phase 2 dialogue | `DialogueSystem.OnDialogueComplete` → `ComicWorldManager.StartPhase3()` | FramePuzzleIntro dialogue; player grabs Frames 3-8 and orders them in FrameOrderPuzzle |
| **Phase 4** | Puzzle solved | `FrameOrderPuzzle` calls `ComicWorldManager.OnPuzzleSolved()` | PuzzleSolvedDialogue; after dialogue → thief appears (`ThiefComicWorld.Appear()`), Frames 9-12 readable |
| **Phase 5** | Thief reaches final waypoint (4th) and disappears | `ThiefComicWorld` calls `ComicWorldManager.StartPhase5()` directly | SpiritRisesDialogue; `GoldSpirit.PlaySpiritRise()` starts (spirit rises 1.5m over 2s, then moves to teddy over 1s) |
| **Phase 6** | Spirit finishes entering teddy | `GoldSpirit` calls `ComicWorldManager.StartPhase6()` directly (after `OnSpiritEntered` fires) | FinalChallengeDialogue; `FinalChallenge.StartChallenge()` — 90-second timer |
| **Phase 7** | Timer expires or win condition met | `FinalChallenge` calls `ComicWorldManager.StartPhase7(bool won)` | Win: Frame 1 vibrant + WinDialogue + HarrietEnding. Lose: Frame 1 B&W + LoseDialogue |

**Note on pointing:** `SpiritRisesDialogue` says "It's pointing somewhere. I should follow." — `TeddyBearController.StartPointing()` is designed for Phase 5 but is **not yet called from code**. It should be wired to `GoldSpirit.OnSpiritEntered`. See Pending Manual Tasks.

---

## Event Wiring Map

| Event | Fired by | Listened to by | When |
|---|---|---|---|
| `ThiefSpawner.OnSequenceComplete` | `ThiefSpawner` (end of coroutine) | `BookPortalTrigger` | Thief jumps back into book in Karen Room |
| `TeddyBear2Dto3D.OnTeddyGrabbed` | `TeddyBear2Dto3D.OnGrabbed()` | `ComicWorldManager.OnTeddyGrabbedPhase1()` | Player grabs 3D teddy in Phase 1 |
| `DialogueSystem.OnDialogueComplete` | `DialogueSystem` (end of line array) | `ComicWorldManager.StartPhase3()` after Phase 2 dialogue; `ComicWorldManager.StartPhase4()` after puzzle dialogue; `ComicWorldManager.ShowHarrietEnding()` on win | After each dialogue sequence |
| `GoldSpirit.OnSpiritEntered` | `GoldSpirit` (end of particle animation) | **Nothing currently subscribed** — intended to trigger `TeddyBearController.StartPointing()` | Spirit finishes entering teddy |
| `ComicFrame.OnFrameShattered` | `ComicFrame.ShatterFrame()` | Nothing (particle is self-contained) | Frame shatters |
| `ComicFrame.OnFramePlaced` | `ComicFrame.NotifyPlaced()` | `FrameOrderPuzzle` | Frame dropped in snap zone |

---

## Dialogue Arrays (GameDialogue.cs)

All are `static string[]` on `public static class GameDialogue`.

| Array name | Used in |
|---|---|
| `BookGlowingPrompt` | Karen Room — book starts glowing (GlowingBook) |
| `ArrivalDialogue` | ComicWorldManager.BeginGame() — on scene load |
| `Frame2Dialogue` | ComicWorldManager.StartPhase2() |
| `TeddyTooFarDialogue` | Not yet wired — intended hint when player can't reach teddy |
| `TeddyGrabbedDialogue` | TeddyBear2Dto3D.OnGrabbed() |
| `FramePuzzleIntro` | ComicWorldManager.StartPhase3() |
| `PuzzleSolvedDialogue` | ComicWorldManager.OnPuzzleSolved() |
| `ThiefAppearsDialogue` | ComicWorldManager.StartPhase4() |
| `SpiritRisesDialogue` | ComicWorldManager.StartPhase5() |
| `FinalChallengeDialogue` | ComicWorldManager.StartPhase6() |
| `WinDialogue` | ComicWorldManager.StartPhase7(won=true) |
| `LoseDialogue` | ComicWorldManager.StartPhase7(won=false) |
| `HarrietEnding` | ComicWorldManager.ShowHarrietEnding() — only on win |

---

## Frame Content & Colors

| Frame | Content | Color |
|---|---|---|
| 1 | Couple holding teddy bear (shatters in Phase 1; reused in FinalChallengeArea) | warm pink |
| 2 | Girl's birthday, parents give teddy | warm pink |
| 3 | Dad wakes teen Harriet, "Time to get ready for school" | light blue |
| 4 | Harriet walks to school, sees Ava in mom's car | light blue |
| 5 | Big oil truck accident | red/orange |
| 6 | Harriet shocked, red light on face | red/orange |
| 7 | Ava taken in ambulance, Harriet calls parents | gray/blue |
| 8 | Hospital, doctor diagnoses psychogenic blindness | gray/blue |
| 9 | Devi sees Harriet's empty seat at school | purple |
| 10 | Devi visits house, dad says she's at kite hill | purple |
| 11 | Devi at kite hill with Harriet's mom | purple |
| 12 | Devi hypnotizes Harriet ("Your eyes will find your happiest moment") | purple |
| 13 | Devi's first day at school | muted purple |
| 14 | Devi tells Harriet she sees magic in her eyes | muted purple |
| 15 | Harriet shows Devi her room with the teddy bear | muted purple-gray |
| 16 | Devi has hypnosis powers, connects Harriet with teddy | muted purple-gray |

---

## All Scripts

| File | Purpose |
|---|---|
| `ThiefSpawner.cs` | Karen Room thief animation sequence. Fires `OnSequenceComplete` at end. **Do not modify.** |
| `BookPortalTrigger.cs` | On `open_book`. Auto-triggers 3s after `OnSequenceComplete` → loads ComicWorld. No player input needed. **Do not modify.** |
| `TimeTunnelTransition.cs` | Singleton, DontDestroyOnLoad. 8 colored spinning panels + blink + fade. `PlayTransition(sceneName)`. **Do not modify.** |
| `GameDialogue.cs` | Static class. All 13 dialogue string arrays. See Dialogue Arrays table above. |
| `DialogueSystem.cs` | Singleton, world-space Canvas at (0,1.5,2). `autoAdvanceDelay=4f`. `ShowDialogue(string[])`. `OnDialogueComplete` event. |
| `ComicWorldManager.cs` | Master phase controller. Singleton. Owns all `StartPhaseN()` methods. |
| `ComicFrame.cs` | Per-frame component. `frameIndex`, `ShatterFrame()`, `OnFrameShattered` event. `OnValidate()` for editor texture preview. |
| `FrameEnlarger.cs` | On Frame1. Two simultaneous XRGrab grabs (LeftEdgeGrab + RightEdgeGrab), scales frame as controllers separate, calls `TeddyBear2Dto3D.UpdateTransitionState(t)`. |
| `TeddyBear2Dto3D.cs` | Manages flat→3D Z-scale (0.01→1) on TeddyBear root. On grab: detaches from Frame1 (`SetParent(null)`), fires `OnTeddyGrabbed`, shows `TeddyGrabbedDialogue`. |
| `TeddyBearController.cs` | Pose swap (Normal/Raised), eye color lerp (material slot index 3 = eyes), arm-rotation `StartPointing(Transform target)`. |
| `FrameOrderPuzzle.cs` | 6 snap zones, correct order [3,4,5,6,7,8]. Wrong → red flash + reset. Calls `ComicWorldManager.OnPuzzleSolved()`. |
| `ThiefComicWorld.cs` | State machine: Hidden→Appearing→Idle→Fleeing→Disappeared. Flees when player within `fleeDistance=2`. After reaching 4th (final) waypoint → disappears → calls `StartPhase5()`. |
| `GoldSpirit.cs` | Gold particle system. `PlaySpiritRise(startPos, teddyTarget)` → rises 1.5m over 2s → moves to teddy over 1s → fires `OnSpiritEntered` → calls `StartPhase6()`. |
| `FinalChallenge.cs` | 90s timer. Win: phone grabbed + both cutouts placed. Calls `StartPhase7(bool won)`. |
| `FlowingGround.cs` | Scrolls `material.mainTextureOffset`. Attach to `FlowingGround` plane. |
| `FloatScript.cs` | Sin-wave bobbing (Y), drift (X), tilt (Z). Randomized per-frame via `Random.Range`. `ResetOrigin()`. |
| `BackgroundFrameSpawner.cs` | `SpawnInitialBackgroundFrames()` (35 grey), `ShatterAllBackgroundFrames()` (staggered bursts), `SpawnNewBackgroundFrames()` (25 pastel). |
| `GlowingBook.cs` | Karen Room. Pulses book material emission. References `UIInstructions` and `TableSnapZone`. |
| `UIInstructions.cs` | World-space hint text. Used by `GlowingBook`. |
| `BookPageFlipper.cs` | Karen Room. Animates book pages flipping. |
| `JenVRBody.cs` | VR body tracking (hands/head follow XR rig). |
| `TableSnapZone.cs` | Karen Room table snap zone for placing the book. |

---

## ComicWorld Scene Hierarchy

```
ComicWorld
├── XR Origin (VR)                  ← InputActionManager + XRI Default Input Actions [REQUIRED]
├── PLAYER_SPAWN_POINT              ← (0, 0, 0)
├── ComicWorldManager               ← 6 spawn point Transform children
├── DialogueSystem                  ← world-space canvas at (0, 1.5, 2), autoAdvanceDelay=4s
├── BackgroundFrameSpawner
├── StarfieldParticles
├── FlowingGround                   ← 20×20 plane at y=0
├── Frame1  [ACTIVE by default]     ← (0, 1.5, 3), frameIndex=1, FrameEnlarger + TeddyBear
│   ├── FrameVisual (Quad)
│   ├── LeftEdgeGrab                ← XRGrabInteractable
│   ├── RightEdgeGrab               ← XRGrabInteractable
│   └── TeddyBear                   ← TeddyBear2Dto3D (root scale overridden to 1,1,0.01 by script)
│       ├── PoseNormal              ← Armature.001 + bear.002 (white fabric), scale=14
│       ├── PoseRaised              ← Armature + bear.009 (brown fabric, eyeRenderer), scale=14
│       └── GoldPointLight
├── Frame2  [inactive]              ← (0, 1.5, 3), frameIndex=2, static visual only
├── Frame3–Frame8  [inactive]       ← puzzle frames, right side (X: 4–7, Z: 0.5–4.5)
├── Frame9–Frame12  [inactive]      ← thief area, behind player (X: -2–2, Z: -3–-5.5)
├── Frame13–Frame16  [inactive]     ← Harriet background, left (X: -4–-6, Z: -4–2)
├── FrameOrderPuzzle                ← 6 SnapZone children at (5,1,0), spaced 1.2m apart
├── ThiefWaypoints                  ← 4 WP_ children around (-5 to -8, 0, -1 to 3)
├── PLACEHOLDER_ThiefComicWorld     [inactive] ← replace with Thief prefab
├── GoldSpirit                      [inactive]
└── FinalChallengeArea              [inactive] ← frameIndex=1 collision harmless while inactive
    ├── Frame1_Final                ← frameIndex=1 (same as root Frame1 — no conflict while inactive)
    ├── Cutout1
    ├── Cutout2
    └── PLACEHOLDER_Phone           ← XRGrabInteractable, needs Inspector wiring
```

---

## Karen Room Scene Hierarchy

```
Karen Room
├── open_book                       ← BookPortalTrigger, XRSimpleInteractable, BoxCollider (0.35, 0.30, 0.25)
├── ThiefSpawner
└── TimeTunnelTransition            ← singleton, DontDestroyOnLoad
    └── TunnelCanvas
        ├── TunnelPanel_Red/Blue/Gold/Green/Purple/White/Cyan/Magenta
        ├── BlinkOverlay
        └── FadeOverlay
```

---

## Key Technical Notes

- **InputActionManager** must be on XR Origin in EVERY scene — ComicWorld was missing it; controllers were completely dead.
- **Frame1 must start active** in ComicWorld scene so Phase 1 begins immediately without waiting for dialogue.
- **DialogueSystem.autoAdvanceDelay = 4f** — lines auto-advance after 4 seconds if controller trigger never fires.
- **BeginGame()** calls `StartPhase1()` immediately, does not `yield` on dialogue completion.
- **TeddyBear sizing**: scale armature children (`PoseNormal/PoseRaised`, scale=14), NOT TeddyBear root — `TeddyBear2Dto3D.Awake()` overwrites root scale to `(1, 1, 0.01)` and that's intentional.
- **Eye material**: use `eyeRenderer.materials[eyeMaterialIndex]` (index 3), not `.material` which only gets slot 0.
- **Frame1_Final vs root Frame1**: both have `frameIndex=1`. `FinalChallengeArea` is inactive until Phase 6, so `GetFrameByIndex(1)` correctly finds root Frame1 in Phases 1-5.
- **URP textures**: `_BaseMap` property, not `_MainTex`.
- **sharedMaterial in editor**: `ComicFrame.ApplyTexture()` uses `sharedMaterial` to avoid per-instance copies in edit mode; safe because ComicFrames don't share materials.

---

## Pending Manual Tasks

- [ ] Wire `GoldSpirit.OnSpiritEntered` → `TeddyBearController.StartPointing(someTarget)` — currently no listener (see Event Wiring Map)
- [ ] Verify bear pose: brown (`bear.009`) = PoseRaised, white (`bear.002`) = PoseNormal — swap in Inspector if visually wrong
- [ ] Tune `Hand Yaw Offset` on TeddyBearController (default -90°; flip to 90° if arm points wrong way)
- [ ] Verify `Eye Material Index = 3` on TeddyBearController (eyes slot on bear.009)
- [ ] Assign 16 frame textures: select each Frame in Hierarchy → ComicFrame `frameTexture` slot → drag image from Project
- [ ] Wire `PLACEHOLDER_Phone` XRGrabInteractable.selectEntered → `FinalChallenge.OnPhoneGrabbed()` in Inspector
- [ ] Replace `PLACEHOLDER_ThiefComicWorld` capsule with actual Thief prefab from Karen Room; re-wire 4 waypoints to ThiefWaypoints children
- [ ] Tune FrameOrderPuzzle snap zone positions if needed (currently at world X: 5, Y: 1, Z: 0, spaced 1.2m)
- [ ] Test full build on headset after above

---

## Assets

- `Assets/Materials/ComicFrames/` — Frame1–Frame16 placeholder materials, TeddyBear_Placeholder, Thief_Placeholder, Frame1_BW
- `Assets/Materials/GalaxyNightSkybox.mat` — procedural dark navy skybox
- `Assets/Scripts/_Backups/` — `.cs.bak` backups of ThiefSpawner, GlowingBook, TableSnapZone (won't compile, safe to ignore)
- `ComicSurfer.apk` — last built APK (consider gitignoring — large binary)

---

## Known Issues / Watch Out For

- `GameObject.Find()` cannot find inactive objects — use `FindObjectsByType` with `FindObjectsInactive.Include`, or find an active parent then use `transform.Find("ChildName")`.
- Blender FBX exports at 100x scale (centimeters) — armature needs `localScale=(14,14,14)` in Unity to show correct size.
- Two Karen Room scene files exist: `Assets/Karen Room.unity` (working, build index 0) and `Assets/Scenes/Karen Room.unity` (extra copy at a later index — harmless but do not set as build index 0).
- `BookPortalTrigger` requires `XRSimpleInteractable` component but does NOT use player interaction — it auto-triggers from `ThiefSpawner.OnSequenceComplete`.
- `TeddyTooFarDialogue` in GameDialogue is not wired to anything yet.
