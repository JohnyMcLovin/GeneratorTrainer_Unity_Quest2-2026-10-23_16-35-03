# Generator Trainer for Unity

A simplified reconstruction of your reference generator: weathered blue steel panels, a dark skid, ventilation grids, a hinged control door, coloured buttons, warning lights, sockets and an exhaust. The attached troubleshooting document is implemented as a 27-step guided exercise.

## Open it in Unity

1. Keep your existing Unity **2023.1.22f1** project. Use a copy of the project for the first import.
2. Extract this ZIP. Import **GeneratorTrainer.unitypackage** using **Assets > Import Package > Custom Package**. If that menu is grey, select **Assets** in the Project window first. Alternatively copy the included `Assets/GeneratorTrainer` folder into your project's `Assets` folder.
3. Wait until script compilation finishes. Choose **Tools > Generator Trainer > 1 - Create Desktop Demo**. Save any existing scene when prompted.
4. This creates mesh and material assets, **GeneratorTrainer.prefab**, and **GeneratorTraining.unity** in a new `Assets/GeneratorTrainer/Generated` folder. Repeating the command uses a new folder, preserving earlier generated assets.
5. Press **Play**. Left-click the controls, right-drag to orbit, and scroll to zoom. Follow the world-space training board to the left of the machine. Start by clicking **PPE**. **CHECK** confirms observation steps; **RESET** restarts the attempt.
6. Run **Tools > Generator Trainer > 4 - Run Procedure Checks** to execute the supplied logic checks in your installed Unity editor.

For your Meta Quest 2, follow **SETUP_QUEST.md** after the desktop check. The headset rig and XR packages are not bundled with the model.

## What is included

- Editable C# source and editor scene builder, requiring no XR packages for the desktop demo.
- 155 visible mesh parts, approximately 5,936 triangles before text labels; metres, Y-up, front controls facing +Z.
- Original 1024px worn paint/steel textures, 512px signage textures, and an original synthesized engine loop.
- Separate door, cover, cap, key, selectors, four breakers, E-STOP, START/STOP, three fault indicators and START-ready lamp.
- Snap-assisted removable props; optional pinch-grabbable fuel can with a filler trigger zone.
- Built-in render pipeline and URP material creation; HDRP is not configured.
- Static textured OBJ/MTL exports with the door open and closed, for external modelling tools. These contain no training logic. Use the Unity package for interaction.
- Textured geometry previews, the full step mapping, and Quest setup instructions.

The Unity prefab and scene are generated locally by the menu command; they are not pre-generated serialized scene files inside this archive. Model geometry and textures are included, not downloaded at runtime.

## Interaction scope

This is a guided training prototype. PPE is a confirmation button. Inspection steps are explicitly acknowledged. The door and breaker cover animate around hinges. The key, fuel cap and charger plug snap between their resting and attached positions when poked or clicked; they are not freeform hand-grab objects by default. Touching the fuel can starts a five-second assisted pour. The optional physical can can instead be grabbed, positioned in the filler trigger, and tilted.

The mode knob moves between OFF and MAN in the required sequence. AUTO is labelled to match the reference, but automatic generator operation is outside this exercise. Switch twirling, cap threads, fluid simulation and electrical circuitry are represented by training states rather than physical simulations. Side power sockets are decorative; the BAT. CHARGE socket is interactive.

The exercise starts with the E-STOP and panel STOP pressed, breaker 2 down, low fuel and a discharged battery. Running is blocked until the training prerequisites are satisfied. An emergency interruption stops the engine immediately; press RESET to restart the guided exercise.

## Validation status

Geometry, UVs, node references, package structure and C# syntax were checked outside Unity. Texture previews were rendered from the supplied geometry. Unity compilation, Play Mode, and Quest device tests could not be run in the creation environment. The included procedure checks are ready to run in your editor; they are not a substitute for headset interaction testing.

This represents the supplied training scenario, not a certified operating procedure for a particular real generator. Refuelling and charging times are shortened for the exercise.
