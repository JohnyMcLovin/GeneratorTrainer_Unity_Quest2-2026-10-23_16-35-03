# Full training procedure

This maps every instruction in the supplied **XR project 1 troubleshooting steps.docx** to the new Unity model. The document's combined battery wait/disconnect instruction is split into two prompts, giving 27 guided prompts. Each run has **one** fault, so only one of the repair parts C, D or E is required: a run is 20 steps (MCB, fuel) or 19 (battery). Parts A through G keep their order; inside A1-A3, A5-A6 and B2-B3 the steps may be done in any order, as in `docs/steps.csv`.

| Prompt | Source instruction | Unity action and feedback |
|---|---|---|
| A1 | Put on safety glasses and ear protection | Poke/click PPE on the training board. Both are confirmed together; blue mandatory-protection signage is visible. |
| A2 | Check exhaust points outdoors or to a vent | Poke/click exhaust. The outlet rotates toward the outdoor vent marker behind the unit. |
| A3 | Check side E-STOP; twist to release if pressed | Poke/click external red E-STOP. It moves to the released position. |
| A4 | Open control panel door with handle | Poke/click handle. Door hinges open. |
| A5 | Read operating instructions inside door | Read and poke/click the inside instruction plate. |
| A6 | Look at MCB, fuel, battery warning lights | The light of this run's fault is red, the other two are green. Press CHECK to acknowledge. |
| B1 | Insert key; ring becomes green | Poke/click loose key or key socket. Key snaps into place and ring lights green. |
| B2 | Turn mode selector to MAN | Poke/click mode knob. It rotates toward MAN. OFF and AUTO labels are also visible. |
| B3 | Ensure panel STOP is released | Poke/click panel STOP to pull it out. |
| C1 | Lift MCB cover | Poke/click cover; it pivots upward. |
| C2 | Find breaker in down/OFF position | Inspect four breakers and press CHECK. In MCB runs one random breaker is down. |
| C3 | Flip tripped breaker up/ON | Poke/click the breaker that is down. Selecting another is a wrong action. |
| C4 | Close cover and check MCB green | Poke/click cover. MCB green is visible after the fault is cleared. |
| D1 | Go to diesel filler, note no-smoking requirement | Inspect D / DIESEL ONLY / NO SMOKING label at right; press CHECK. |
| D2 | Remove fuel cap | Poke/click cap; it moves beside the filler. |
| D3 | Fill with diesel using fuel-can prop and trigger zone | Poke/click diesel can for a five-second assisted pour. Optional physical mode: grab can, bring it close to filler, tilt >60 degrees and hold. |
| D4 | Replace/tighten cap and check fuel green | Poke/click cap again; it snaps back. FUEL remains red until the cap is replaced. |
| E1 | Connect charger cable to BAT. CHARGE / JUMP START | Poke/click charger plug or BAT. CHARGE socket. Plug attaches; a flexible cable connects it to charger base. |
| E2a | Wait for battery green | Eight-second simulated charge; board shows progress. |
| E2b | Disconnect cable | Poke/click attached charger plug or socket. Plug returns to shelf. |
| F1 | All three fault lights and START light green | Press CHECK after inspecting. START readiness includes safety, key, mode, STOP, breakers, covers, fuel and battery. |
| F2 | Press green START | Poke/click START; engine loop begins. |
| F3 | Display on, no fault, engine running | Display reads 400 V / 50 Hz / NO FAULT. Listen to engine and poke/click the display to verify. |
| G1 | Press STOP | Poke/click panel STOP; engine sound stops. |
| G2 | Mode OFF | Poke/click mode knob; it moves to OFF. |
| G3 | Remove key | Poke/click key or socket; key returns to shelf and its ring darkens. |
| G4 | Close control door | Poke/click door handle; door closes and board confirms completion. |

## Behaviour and recovery

Each start and each RESET picks one fault at random (MCB with a random tripped breaker, fuel, or battery); the other two systems start healthy. Five-second refuelling and eight-second charging are shortened training durations. The diesel prop is the only accepted fuel prop. The exposed side power outlets are decorative and are separate from the battery charging socket.

Wrong actions are soft fails: the action has no effect on the procedure, it is logged, it adds a 10-second penalty, and the same control ignores presses for 2 seconds (E-STOP and STOP are never locked). The run is never ended by a wrong action.

The side E-STOP and the panel STOP stop a running engine immediately, without penalty. If that happens after START but before F3 is confirmed, the run goes back to F2: release the E-STOP or STOP (no penalty) and press START again. Pressing the E-STOP when nothing is running is a wrong action, and the door cannot be closed at G4 while the E-STOP is pressed. RESET starts a new attempt with a new fault; the log of the abandoned attempt ends with a `reset` row. The guide deliberately prevents skipping required steps; it is not a free-running generator simulation.

Observation actions are explicit confirmations. The prototype does not measure whether the learner has actually worn PPE, read a plate, listened to the sound, or checked the real environment. The outdoor vent marker represents the safe destination inside the virtual scene.

## Editing

- `TrainingState.cs`: conditions, prompt order, start guards and timing.
- `GeneratorTraining.cs`: visual state, door/cover animation, indicators, display, cable and sound.
- `TrainerControl.cs`: one activation per press/release; mouse and XR use the same handler.
- `FuelZone.cs` / `DieselCan.cs`: optional physical refuelling detection.
- `TrainerBuilder.cs`: model/material/prefab/desktop-scene construction.
- `TrainerHandsSetup.cs`: optional XRI 2.5.x scene binding and grab-can setup.
- `TrainerChecks.cs`: editor-executable flow and fault-condition checks (Tools > Generator Trainer > 4 - Run Procedure Checks).
- `TrainerXRBinding.cs`: at scene start, binds every control to XR select (controller ray + grip) in scenes with an XR Interaction Manager.
- `TrainingHud.cs` / `ExpertScreen.cs`: Technician task panel and Expert station (F1 switches Solo / Collaborative, Tab shows the Expert screen on one monitor).
- `Source/TrainerModel.json`: separate mesh definitions, parts, pivots, text labels and interaction targets.

After changing JSON geometry, run Create Desktop Demo again to generate a fresh revision. For ordinary design edits, edit the generated prefab directly. Materials and textures are ordinary Unity assets.
