# Generator Trainer (Unity, Meta Quest 2)

Course project: a two-player collaborative VR training task. A diesel generator will not start; the team has to find and fix the fault, start it, and shut it down safely.

- **Technician** (Quest 2 headset): can touch and operate the generator, but has no fault codes or manual.
- **Expert** (PC): sees the fault codes and the manual, but cannot touch anything.

They have to talk to finish the task. Each run has one of three faults: tripped **MCB** breaker, empty **fuel** tank, or flat **battery**. Wrong actions are soft fails: they are logged and cost time, but never end the run.

> Status (24 Nov 2026): one PC works end to end. One random fault per run, wrong actions as soft fails (penalty + short lockout), run timer, CSV log, Technician task panel, and an Expert screen where the Expert chooses the fault code from what the Technician reports. Quest build (OpenXR) and XR controller binding exist but are not yet tested on a headset. Networking between two machines and voice are not built. See [`docs/PLAN.md`](docs/PLAN.md).
>
> **Which scene to open:** `Assets/Scenes/DesktopDemo.unity` (desktop, mouse) or `Assets/Scenes/KeyboardTest.unity` (XR rig, used by the Quest build). Do not demo `Assets/GeneratorTrainer/Generated/GeneratorTraining.unity`: it holds the older OBJ model, whose `ExhaustPipe.cs` and `PanelDoor.cs` move parts without any step checks, penalties or logging.

## Requirements

- Unity **2023.1.22f1** exactly (see `ProjectSettings/ProjectVersion.txt`). Do not upgrade.
- Android Build Support module (for Quest builds).
- [Git LFS](https://git-lfs.com): models, textures and audio are stored in LFS.
- .NET 8 SDK (optional, to run the Core unit tests without Unity).

## Getting started

```bash
git lfs install
```

```bash
git clone git@github.com:JohnyMcLovin/GeneratorTrainer_Unity_Quest2-2026-10-23_16-35-03.git
```

Then open the folder in Unity Hub with 2023.1.22f1 and open `Assets/GeneratorTrainer/Generated/GeneratorTraining.unity`.

If textures look missing or `.png` files are tiny text files, run `git lfs pull`.

## Repo layout

| Path | What |
|---|---|
| `Assets/GeneratorTrainer/` | Generator kit: runtime scripts, editor builder (`Tools > Generator Trainer`), generated prefab and scene, textures, engine audio |
| `Assets/GeneratorModel/` | Generator as OBJ: body plus a separate mesh per moving part, and team scripts (`Scripts/Generator/`) |
| `Assets/Scripts/Core/` | *(planned)* Pure C# step logic: step graph, fault config, penalties, CSV logger. No `UnityEngine`. |
| `Assets/Samples/`, `Assets/XRI/` | XR Interaction Toolkit 2.5.4 samples and settings |
| `docs/PLAN.md` | Plan, decisions, weekly schedule, audit summary |
| `docs/steps.csv` | **Authoritative step list** (`A1`..`G4`) |
| `docs/unity-todo.md` | Checklist of changes that need the Unity editor |
| `tests/Core.Tests/` | *(planned)* xUnit tests for Core, run with `dotnet test` |

## The procedure

Step IDs are the shared vocabulary in code, logs, commits and chat. Full list in [`docs/steps.csv`](docs/steps.csv).

| Phase | Steps | What |
|---|---|---|
| A Safety | A1-A6 | PPE, exhaust, E-STOP, open door, read plate, note red lamps |
| B Activate | B1-B3 | Key in, mode to MAN, STOP released |
| C MCB fault | C1-C4 | Lift cover, find tripped breaker, flip it on, close cover |
| D Fuel fault | D1-D4 | Go to filler, cap off, fill with diesel, cap on |
| E Battery fault | E1-E2 | Connect charger, wait for green, disconnect |
| F Start | F1-F3 | All lamps green, START, check display and engine |
| G Shutdown | G1-G4 | STOP, mode OFF, key out, close door |

Only the phase matching the run's fault is required (C, D or E).

## Architecture

1. **Objects** (door, key, switches, breakers, cap, cable, buttons): physical behaviour only. They raise `OnStepCompleted(stepId)` or `OnWrongAction(stepId, reason)` and know nothing about step order.
2. **Core** (plain C#): loads `steps.csv`, decides right vs wrong, applies penalties, writes the run log.
3. **Views and network** (Expert screen, Photon, voice): read state through `IRunState`. The run works single-player with networking off.

Wrong-action reasons: `OutOfOrder`, `WrongPanel`, `WrongFault`, `StartWithRedLamp`, `WrongPort`.

## Team

| Member | Area |
|---|---|
| Anvar | Core logic, scene wiring, project coordination |
| Nurislam | Panel interactions, headset setup, demo video |
| Almaz | Networking (Photon) and voice |
| Dinmukhamed | Timer, penalties, Expert screen |

Roles are tentative; see `docs/PLAN.md`.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md). Short version: branch per person, commit under your own name, one person edits the main scene at a time, binaries through LFS.

## Milestones

| Tag | Date | Goal |
|---|---|---|
| `milestone-w3` | Sun 8 Nov | MCB fault fixable start to finish on one PC, CSV written |
| `freeze` | Sun 15 Nov | Feature freeze: all faults, headset, network |
| `submit` | Sun 22 Nov | Demo and submission |

## Credits

The generator model, textures, engine sound and the first desktop prototype come from the generated "Generator Trainer" kit in `Assets/GeneratorTrainer/` (see its `START_HERE.md`). XR Interaction Toolkit samples are © Unity Technologies.
