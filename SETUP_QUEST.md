# Meta Quest 2 setup

Target: your Unity 2023.1.22f1 project, Built-in or URP, XR Interaction Toolkit 2.5.4, XR Hands 1.3.0. Keep the base model and hand rig at scale 1. Model units are metres. The automatic hand-binding command checks for XRI 2.5.x and does not modify other versions.

## 1 Install dependencies

In **Window > Package Manager**, use **+ > Add package by name**:

| Package | Version / requirement |
|---|---|
| `com.unity.xr.interaction.toolkit` | 2.5.4 |
| `com.unity.xr.hands` | 1.3.0 |
| `com.unity.xr.openxr` | A Unity-compatible version, 1.6.0 or newer |
| `com.unity.xr.management` | A Unity-compatible version, 4.3.1 or newer |

Allow dependency installation and Input System activation/restart if Unity requests it. Import **Starter Assets** and **Hands Interaction Demo** from the XR Interaction Toolkit Samples tab. Import **HandVisualizer** from XR Hands Samples. Install the Shader Graph version compatible with your Unity editor if the sample's hand materials require it. Use Project Validation to resolve sample prerequisites.

Official sample setup: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.5/manual/samples-hands-interaction-demo.html
XR Hands: https://docs.unity3d.com/Packages/com.unity.xr.hands@1.3/manual/index.html

## 2 Verify hand tracking before adding the generator

Open `Assets/Samples/XR Interaction Toolkit/2.5.4/Hands Interaction Demo/HandsDemoScene`. Save a copy in your own folder. Test the sample's hands and interactions first. Use its existing XR Origin, XR Interaction Manager, input configuration and hand interactors. Confirm Poke Interactors and Direct Interactors are present and active.

Do not add an unrelated second XR Origin. Avoid copying only the visible hand meshes; those alone do not provide tracking or interaction.

## 3 Add this trainer to the working hands scene

1. Generate the desktop scene once as described in START_HERE, to create the prefab and materials.
2. Return to your saved copy of HandsDemoScene.
3. Drag the generated `GeneratorTrainer.prefab` into the Hierarchy. Place it on the floor, leaving walking room around its left door, front panel, right filler and instruction board. Its centre is at ground level, front facing local +Z.
4. Remove or move sample demo objects that overlap the generator. Keep the working XR rig and interaction/input managers.
5. Select the **GeneratorTrainer** root in the Hierarchy.
6. Choose **Tools > Generator Trainer > 2 - Enable Hand Poke on Selected Trainer**. This adds real XRI interactables, poke filters and persistent event bindings to all controls and disables desktop mouse input for that instance. Save the scene.
7. Poke the physical controls with the index fingertip. Key insertion, cap removal and charger connection are assisted: poke the prop or its socket. Lift your finger away before pressing again.
8. Optional: choose **3 - Optional Grabbable Diesel Can** after hand-poke setup. At D3, pinch-grab the can using the sample's Direct Interactor, position its body close to the filler, and tilt it by more than 60 degrees. Hold it there for five seconds. Uprighting or removing the can pauses filling. This disables the can's assisted poke target; undo that editor command to restore the assisted mode. Re-run hand setup only before enabling the optional grab.
9. Do not keep the desktop demo's DesktopCamera/AudioListener active alongside the XR camera. If you instead copied the sample rig into the generated demo, disable DesktopCamera first.

Controls are bound on the scene instance. To reuse the configured trainer in other scenes, save it as a prefab variant or apply the changes to your generated prefab intentionally.

## 4 Standalone Android build

Install **Android Build Support**, including **Android SDK & NDK Tools** and **OpenJDK**, for your editor through Unity Hub.

In **File > Build Settings**, switch to **Android**, then add your saved hands-and-generator scene to Scenes In Build. Under **Project Settings > XR Plug-in Management**, select the Android tab and enable **OpenXR** and initialization on startup. Under **OpenXR**, enable **Meta Quest Support**, **Hand Tracking Subsystem** and **Meta Hand Tracking Aim**. Add Oculus Touch Controller Profile if you also want controller support.

Use **Project Validation** and resolve blocking issues. In Android Player settings use **IL2CPP** and **ARM64**. Configure the application identifier for your project. Enable developer mode on the Quest, connect by USB, accept the USB debugging prompt, select the device, then **Build And Run**. Enable hand tracking in the headset's settings and put the controllers down when testing hands.

## 5 Unity Play Mode through Quest Link / Air Link

Play Mode runs a PC application. Android OpenXR settings alone do not configure it.

1. Establish a working Meta Quest Link session first. The headset must enter the PC Link environment. A grey Pair button with "Not connected" is not a working connection. Resolve pairing or test USB Link before troubleshooting the Unity scene.
2. Enable OpenXR for the **PC/Standalone** tab in XR Plug-in Management. Use the Meta/Oculus runtime as the active PC OpenXR runtime. Leave Mock Runtime disabled for a real headset session.
3. Start the Link session, then press Unity Play. Confirm an active XR camera and a single XR Origin.
4. Hand availability through Link depends on the installed runtime and its hand-tracking support/configuration. A standalone Android build is the direct test for this Quest hand workflow. Moving simulated hands in the editor does not prove the headset is receiving hand tracking.

If the hands render only in one eye with Built-in + Single Pass Instanced, the XRI 2.5 sample documents a known hand-shader issue. Test **Multi-pass** on the platform you are actually running, or use URP with suitable materials. This does not fix an unpaired Link connection.

Known issues: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.5/manual/index.html

## 6 Acceptance check on the headset

Complete the 27 steps in PROCEDURE.md. Confirm that the world-space instruction board is readable, each poke triggers only once until the finger is withdrawn, door/cover movement does not trap the finger selection, and the fuel-can trigger works if the physical mode is enabled. Check both eyes, audio, floor height and comfortable reach. If controls are hard to target, enlarge their BoxCollider size on the `*Hit` objects while preserving separation between adjacent controls.
