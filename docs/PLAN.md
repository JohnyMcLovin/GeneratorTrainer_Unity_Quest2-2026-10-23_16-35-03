# Generator Trainer: tentative plan (re-baselined Fri 30 Oct 2026)

Status: TENTATIVE. Written while the team was out of contact. Anything marked `[confirm]` needs a teammate or the professor to agree before it counts as decided. Source of truth for roles and grading: the team plan page and the tracker; this file is the repo-side copy.

## 0. Where we really are

The tracker (read 30 Oct) shows one task done: model placed in a scene (Anvar). Everything else, including all of original Week 2, is still "to do". Nurislam has a first interactable-generator prototype and the repo exists. **Audited 30 Oct**: the repo is a generated "Generator Trainer" kit (Unity 2023.1.22f1, XRI 2.5.4) with a working single-player, desktop-click, hard-coded 27-step procedure; plus the OBJ model with separate part meshes, `ExhaustPipe.cs` on `main` and `PanelDoor.cs` (door hinge) on branch `anvar`. Missing: Quest XR setup, Photon, fault config, wrong-action handling, logging, Expert view.

Fixed dates (from the plan page): feature freeze Sun 15 Nov, demo and submission Sun 22 Nov. Original Week 2 (desktop prototype) is lost, so Weeks 3-5 are compressed. Do not move the two fixed dates; cut scope instead (section 6).

## 1. Open decisions

| # | Decision | Default if nobody answers | Who |
|---|---|---|---|
| D1 | Expert on PC and Technician in headset, or both headsets? | PC + headset | Professor |
| D2 | Headsets: how many, which model, how to book (repo name says Quest 2) | Quest 2, 1 headset | Professor |
| D3 | Log format | One CSV per run (spec in section 5) | Professor |
| D4 | Photon (Fusion or PUN2, plus Photon Voice) acceptable as the external API? | Yes | Professor |
| D5 | Three sharing conditions (manual, always-on, adaptive) still needed? | No, one mode | Professor |
| D6 | Better 3D model coming? Parts separate meshes? | Stay on the free asset | Professor |
| D7 | Who owns task logic if Dinmukhamed is unreachable | Anvar writes the pure-C# core (section 3); Dinmukhamed extends it | Team `[confirm]` |
| D8 | Base the panel on the kit's generated `GeneratorTrainer` prefab (all controls, pivots, lamps, display already animate) or on the separate OBJ parts with per-part XRI scripts (`ExhaustPipe`, `PanelDoor`)? | Kit prefab for the panel; keep `PanelDoor`/`ExhaustPipe` ideas as the XR interaction layer on top | Team `[confirm]` |
| D9 | Which XR backend for Quest 2: OpenXR + Meta feature, or Oculus XR plugin? Hands or controllers? | OpenXR, controllers first (hands optional) | Nurislam `[confirm]` |

Send D1-D6 to the professor first: they change what gets built.

## 2. Architecture (so people can work apart)

Three layers, one direction of dependency.

1. **Objects** (door, key, switches, breakers, cap, cable, buttons). Freely interactable. Each only does its own physical behaviour and raises an event with a step ID. They never know about step order.
2. **Core** (plain C#, no `UnityEngine`): step graph loaded from `docs/steps.csv`, fault config, StepManager (decides correct vs wrong), penalty rules, CSV logger. Unit-testable outside Unity.
3. **Views and network** (Expert screen, Photon sync, voice). Read state through an interface (`IRunState`), so the whole run works single-player before networking exists.

Rules:
- Out-of-order action is **not blocked**. It raises `OnWrongAction(stepId, reason)`: log it, add a time penalty, briefly lock that control, do not advance. The run never ends early.
- Real physical interlocks live in the object (mode switch ignores input until the key is in).
- Fault config = which of MCB / fuel / battery is faulty. StepManager builds the required path from it; the Expert's manual reads the same config.
- Shared event names: `OnStepCompleted(stepId)`, `OnWrongAction(stepId, reason)`. Reasons: `OutOfOrder`, `WrongPanel`, `WrongFault`, `StartWithRedLamp`, `WrongPort`.

## 3. Weekly plan

### Week 2 remainder: Fri 30 Oct - Sun 1 Nov

| Task | Needs Unity? |
|---|---|
| Audit repo (section 1) | No |
| Commit `docs/PLAN.md`, `docs/steps.csv`; fix `.gitignore`/LFS | No |
| Send D1-D5 to the professor | No |
| Message the team: status request, the contract (section 3), D8, headset booking | No |
| Write Core: `StepIds`, step loader for `steps.csv`, `FaultConfig`, `StepManager`, `PenaltyPolicy`, `RunLogger` (CSV) | No |
| Unit tests for Core (`dotnet test`), including: wrong order, each of 3 faults, START with red lamp | No |
| Install the project's exact Unity version on your own laptop (Personal licence, free); add Android Build Support if you want to build for Quest from it | Setup |

Done when: Core compiles and tests pass; plan, steps and are pushed on a branch; professor and team have been messaged.

### Week 3: Mon 2 - Sun 8 Nov: desktop prototype, one fault end to end

| Owner | Task | Unity? |
|---|---|---|
| Anvar | `GeneratorState` MonoBehaviour: active fault, lamp colours, engine on/off. Connect Core to the scene through a thin adapter. Run the MCB fault on desktop with simple click/raycast interaction as a stand-in | Yes (laptop is fine) |
| Anvar | Editor script `Tools > Generator > Wire Scene` that adds components to named objects, so the scene is wired in one click on any machine | Yes |
| Nurislam | Panel controls on PC: door, key, mode switch, breakers, STOP/START (replaces the stand-in) | Yes |
| Nurislam | **Headset smoke test**: an empty XR scene builds and runs on the Quest. De-risk this early | Lab/headset |
| Almaz | Two machines join one Photon room; Technician position and one synced bool | Yes |
| Dinmukhamed | Review and extend Core: logger fields, timer stub; start Expert screen layout (fault codes, manual) | Mostly no |

Milestone (Sun 8 Nov): MCB fault fixable start to finish on one PC, with a CSV written. Tag `milestone-w3`.

### Week 4: Mon 9 - Sun 15 Nov: all faults, headset, network, freeze

- Fuel and battery faults (Anvar), plus the D3 fuel-can prop and trigger zone.
- Interactions usable in the headset: grab, press, turn (Nurislam).
- Generator state synced both ways; Expert sees fault codes and manual live (Almaz, Dinmukhamed).
- Countdown timer, penalties, control lockout (Dinmukhamed).
- Voice (Almaz). Cut first if late (section 6).
- Thu/Fri: integration run, Expert on PC and Technician in the headset. Two internal test runs recorded (logs plus screen capture).
- **Sun 15 Nov: feature freeze.** Tag `freeze`.

### Week 5: Mon 16 - Sun 22 Nov: fix, film, submit

- Fix only what the test runs found. No new features.
- Demo video showing both sides and the headset (Nurislam).
- Rehearse the live demonstration twice; decide who says what (everyone).
- Final logs checked complete for all three faults.
- Contribution record exported from the tracker (Anvar).
- Submit Unity project, video and logs. Confirm the exact deadline and format with the professor `[confirm]`.

## 4. Log format (proposal, D4)

One CSV per run: `session_id, timestamp, role, module, action, step_id, correct, reason, penalty_s, module_start, module_end, total_run_s, idle_s, wrong_count_module`.

## 5. Cut list and minimum viable demo

Minimum viable demo: Expert on PC and Technician (headset, or desktop if the headset fails) finish **one** fault together over the network, with a complete CSV.

Cut in this order if behind: voice (use a phone call during the demo; it is not a graded line) > audio polish > PPE, exhaust and E-STOP steps (A1-A3) > battery fault > Expert manual styling. Do not cut: logs, wrong-action handling, the sync, the demo video.

## 6. Audit table (filled Fri 30 Oct)

| Area | Exists in repo? | Notes |
|---|---|---|
| Unity version and packages | Partly | 2023.1.22f1. Only XRI 2.5.4 (+ Starter Assets, XR Device Simulator samples). **No XR Plugin Management, no OpenXR/Oculus, no XR Hands**: cannot run on Quest yet. No scenes in Build Settings. |
| Generator model placed, scaled, colliders | Yes | Kit model as JSON (generated prefab) and as OBJ: body + separate meshes per moving part (answers most of D7). `main` scene uses OBJ `Assembly_Body` + exhaust; generated prefab has box colliders, OBJ colliders **[check in Unity]**. |
| Door / key / mode switch | Yes (kit) / door also on `anvar` | Kit: door, snap-assisted key, MAN/OFF by single press. `anvar`: `PanelDoor.cs` hinge on `Part_DoorPivot` with XRI handle. Two implementations; pick one (D9). |
| Breakers and cover | Yes (kit) | 4 breakers + cover animate. Tripped breaker hard-coded to #2. |
| Fuel cap, cable, buttons | Yes (kit) | Cap, fuel can + trigger zone (5 s pour), charger cable (8 s charge), START, STOP, E-STOP, PPE/CHECK/RESET board buttons. One filler only, so no `WrongPort`. |
| Lamps and display | Yes (kit) | MCB/FUEL/BATTERY/START-ready lamps, key ring light, `TextMesh` display. |
| XR rig | Partly (`anvar`) | `anvar` scene has XR Interaction Setup + XR Device Simulator (desktop). Kit has an untested hand-poke binder menu. No headset build done. |
| Photon | No | Nothing. |
| Logger | No | Nothing. |
| Step logic / Core | Partly | Kit `TrainingState`: pure C#, linear 27 steps, all 3 faults always on, no step IDs, no fault config, no `any` groups, no wrong-action events, penalties, timer. Shows the procedure to the Technician (wrong for Scenario B). Replace with Core. |
| Expert view | No | Nothing. |
| Repo hygiene | Needs work | No README/docs (added now). Binaries raw on `main`, converted to LFS on `anvar`. Duplicate kit docs and `Models/` at repo root. |

**What this changes in the plan**

- Panel interactions are mostly done in desktop form. Week 3 shifts from building controls to (a) replacing `TrainingState` with Core and (b) turning `TrainerControl.action` strings into `OnStepCompleted` / `OnWrongAction` with step IDs.
- Headset setup is the biggest unknown: add XR Management + OpenXR, Android build, first Quest build. Move Nurislam's smoke test to the start of Week 3.
- Hide the kit's guided training board for the Technician (Scenario B: only the Expert sees the procedure). Its text can feed the Expert manual.
- Merge `anvar` (LFS conversion + door) into `main` early and in Unity, before more people touch `GeneratorTraining.unity`.

## 7. Commit hygiene (matters for the contribution record)

The course requires a record of each member's contribution. Each person commits under their own name, or add `Co-authored-by:` trailers when pushing on someone's behalf. Branch per person, small frequent merges, one person edits the main scene at a time. Tag each milestone.
