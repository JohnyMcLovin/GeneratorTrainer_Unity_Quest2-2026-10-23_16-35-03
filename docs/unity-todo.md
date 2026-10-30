# Unity to-do

Changes that need someone with the Unity editor (2023.1.22f1) open. Tick and add your name and date when done.

## Before more scene work

- [ ] Everyone: `git lfs install`, then fresh clone or `git lfs pull`. Check textures and the engine sound load.
- [ ] Merge `anvar` into `main` **in Unity** (LFS conversion + `PanelDoor` + XR Interaction Setup). Open `GeneratorTraining.unity` afterwards, check the door, exhaust and XR rig, then save and commit.
- [ ] Re-save `Assets/GeneratorModel/Scripts/Generator/ExhaustPipe.cs` as UTF-8 (currently Windows-1252, CRLF).
- [ ] Add `GeneratorTraining.unity` to **File > Build Settings > Scenes In Build**.
- [ ] Run **Tools > Generator Trainer > 4 - Run Procedure Checks**. Note pass/fail here.

## Decide D9 (panel base), then

- [ ] If kit prefab: place `Assets/GeneratorTrainer/Generated/GeneratorTrainer.prefab` in the scene, remove the duplicate OBJ `Assembly_Body` / `Part_*` instances, check scale and colliders.
- [ ] If OBJ parts: check each `Part_*` import has colliders and the right pivot; wire an XRI interactable per control.
- [ ] Hide the world-space training board (step text, PPE/CHECK/RESET) from the Technician. Keep a debug toggle.

## Quest 2 (Nurislam, early Week 3)

- [ ] Package Manager: add **XR Plugin Management** and **OpenXR** (Meta Quest feature group) per `SETUP_QUEST.md`. XR Hands only if we use hands (D10).
- [ ] Switch platform to Android; set company and product name, minimum API level required by OpenXR.
- [ ] Empty XR scene builds and runs on the headset (smoke test).
- [ ] Generator scene runs on the headset; note frame rate.

## Later

- [ ] Photon package import (Almaz), after D5.
- [ ] Wire scene through `Tools > Generator > Wire Scene` once that Editor script exists.
- [ ] Clean repo root: move `START_HERE.md`, `SETUP_QUEST.md`, `PROCEDURE.md`, `Preview.png`, `Panel_Detail.png` to `docs/kit/`; delete root `Models/` (not used by Unity). Can be done outside Unity, but agree first.
