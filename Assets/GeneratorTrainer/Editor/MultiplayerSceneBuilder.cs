using System.IO;
using Fusion;
using GeneratorTrainer.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeneratorTrainer.Editor
{
    // Builds Assets/Scenes/MultiplayerDesktop.unity around the SAME generator prefab as the other scenes, plus the
    // networked DesktopPlayer prefab (Photon Fusion). The scene has a session menu (Create / Join with a code); each
    // computer spawns its own first-person player after connecting. The scene is added to Build Settings.
    // The generator itself is untouched: the only change is a per-scene override that turns off its own mouse click
    // (desktopControls), because the player's crosshair interactor replaces it here.
    // Batch: Unity.exe -batchmode -quit -projectPath . -executeMethod GeneratorTrainer.Editor.MultiplayerSceneBuilder.Create
    public static class MultiplayerSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MultiplayerDesktop.unity";
        public const string PrefabFolder = "Assets/GeneratorTrainer/Multiplayer/Prefabs";
        public const string PlayerPrefabPath = PrefabFolder + "/DesktopPlayer.prefab";
        const string GeneratorPrefabPath = "Assets/GeneratorTrainer/Generated/GeneratorTrainer.prefab";
        const string Generated = "Assets/GeneratorTrainer/Generated";

        [MenuItem("Tools/Generator Trainer/7 - Create Multiplayer Desktop Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            bool batch = Application.isBatchMode;
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!batch && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Multiplayer Desktop scene", ScenePath + " already exists. Rebuild it (and the DesktopPlayer prefab)?", "Rebuild", "Cancel"))
                return;

            var generatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorPrefabPath);
            if (generatorPrefab == null) { Debug.LogError("Generator prefab not found at " + GeneratorPrefabPath); return; }

            GameObject playerPrefab = BuildPlayerPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Save the empty scene first so it has a file identity (GUID). Fusion "bakes" scene NetworkObjects on
            // save and derives their IDs from that identity; a never-saved scene gives ID 0 -> "hash != 0" error.
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Generator: prefab instance; the crosshair interactor replaces its built-in mouse click in this scene.
            var gen = (GameObject)PrefabUtility.InstantiatePrefab(generatorPrefab, scene);
            gen.transform.position = Vector3.zero;
            var trainer = gen.GetComponent<GeneratorTraining>();
            trainer.desktopControls = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trainer);

            // Milestone 4: the generator becomes one shared network object (in this scene only; the prefab and the
            // other scenes are untouched). The master client (session creator) runs the procedure; if it leaves,
            // Fusion hands the generator to the remaining player.
            var genNetObj = gen.AddComponent<NetworkObject>();
            genNetObj.Flags |= NetworkObjectFlags.MasterClientObject;
            gen.AddComponent<GeneratorNetSync>();

            BuildEnvironment();

            // Milestone 3: players are no longer placed in the scene; each computer spawns its own player when it
            // connects. Until then an overview camera shows the generator behind the session menu.
            var overview = new GameObject("OverviewCamera") { tag = "MainCamera" };
            var overviewCam = overview.AddComponent<Camera>();
            overviewCam.nearClipPlane = .05f; overviewCam.farClipPlane = 50;
            overviewCam.clearFlags = CameraClearFlags.SolidColor; overviewCam.backgroundColor = new Color(.12f, .15f, .17f);
            overview.AddComponent<AudioListener>();
            overview.transform.position = new Vector3(1.1f, 2.0f, 4.6f);
            overview.transform.LookAt(new Vector3(-.3f, 1, 0));

            // Spawn points in front of the control panel, facing the generator: 0 = creator, 1 = joiner.
            var spawns = new GameObject("SpawnPoints").transform;
            var spawn1 = SpawnPoint(spawns, "Player1Spawn", new Vector3(-0.4f, 0.05f, 3.2f));
            var spawn2 = SpawnPoint(spawns, "Player2Spawn", new Vector3(1.4f, 0.05f, 3.2f));

            var session = new GameObject("Session");
            var launcher = session.AddComponent<SessionLauncher>();
            launcher.playerPrefab = playerPrefab.GetComponent<NetworkObject>();
            launcher.overviewCamera = overviewCam;
            launcher.spawnPoints = new[] { spawn1, spawn2 };
            session.AddComponent<SessionMenu>().launcher = launcher;

            // Milestone 7: VR. The local VR rig (official XR Interaction Toolkit Starter Assets) sits in the scene
            // switched off; SessionLauncher turns it on only when a headset runs the app (or the editor simulator
            // option is ticked). The VRPlayer network prefab is the avatar presence that follows that rig.
            launcher.vrRig = BuildVRRig(scene);
            launcher.vrPlayerPrefab = BuildVRPlayerPrefab().GetComponent<NetworkObject>();
            launcher.xrDeviceSimulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XRSimulatorPrefabPath);

            EditorSceneManager.SaveScene(scene, ScenePath);
            // Save once more so the bake also sees the objects' final file IDs.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MultiplayerSceneBuilder] Saved " + ScenePath + " and " + PlayerPrefabPath +
                      ". Press Play, then Create session or Join with a code. In game: WASD walk, mouse look, left-click controls, Esc frees the mouse.");
        }

        // ---------------- VR (Milestone 7) ----------------

        public const string VRPlayerPrefabPath = PrefabFolder + "/VRPlayer.prefab";
        const string StarterAssets = "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/Prefabs";
        const string XRInteractionSetupPath = StarterAssets + "/XR Interaction Setup.prefab";
        const string XROriginPath = StarterAssets + "/XR Origin (XR Rig).prefab";
        const string XRSimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/XR Device Simulator/XR Device Simulator.prefab";

        static VRRig BuildVRRig(UnityEngine.SceneManagement.Scene scene)
        {
            var setupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XRInteractionSetupPath);
            var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
            if (setupPrefab == null || originPrefab == null)
            {
                Debug.LogError("[MultiplayerSceneBuilder] XR Interaction Toolkit Starter Assets not found under " + StarterAssets + ". VR is not set up.");
                return null;
            }
            var root = new GameObject("VRRig (local, VR mode only)");
            // Input actions, interaction manager and the UI event system for the controller rays.
            var setup = (GameObject)PrefabUtility.InstantiatePrefab(setupPrefab, scene);
            setup.transform.SetParent(root.transform, false);
            // Head + two controllers, move / turn / teleport.
            var origin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab, scene);
            origin.transform.SetParent(root.transform, false);
            var rig = root.AddComponent<VRRig>();
            rig.origin = origin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            root.SetActive(false);   // desktop players never use it
            return rig;
        }

        static GameObject BuildVRPlayerPrefab()
        {
            var root = new GameObject("VRPlayer");
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0, 1.62f, 0);

            var presence = root.AddComponent<VRPresence>();
            presence.head = head;
            var avatar = root.AddComponent<PlayerAvatar>();
            avatar.role = PlayerRole.Instructions; avatar.playerNumber = 1; avatar.isLocalPlayer = true; avatar.head = head;

            var netObj = root.AddComponent<NetworkObject>();
            netObj.Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
            // VRPresence moves the object in LateUpdate (following the headset), like the desktop controller in Update.
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.ConfigFlags |= NetworkTransform.NetworkTransformFlags.DisableSharedModeInterpolation;
            var net = root.AddComponent<NetworkedPlayer>();
            net.avatar = avatar; net.head = head; net.vr = presence;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, VRPlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Transform SpawnPoint(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            t.rotation = Quaternion.Euler(0, 180, 0);
            return t;
        }

        // Builds need the scene in File > Build Settings; Fusion also identifies scenes by their build index.
        static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) { if (!s.enabled) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); } return; }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static GameObject BuildPlayerPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(PrefabFolder).Replace('\\', '/'), Path.GetFileName(PrefabFolder));

            var root = new GameObject("DesktopPlayer");
            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f; body.radius = 0.3f; body.center = new Vector3(0, 0.9f, 0);
            body.stepOffset = 0.3f; body.slopeLimit = 45f; body.skinWidth = 0.02f; body.minMoveDistance = 0f;

            var head = new GameObject("Head") { tag = "MainCamera" };
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0, 1.62f, 0);
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 50f; cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .15f, .17f);
            var listener = head.AddComponent<AudioListener>();

            var controller = root.AddComponent<DesktopPlayerController>();
            controller.head = head.transform;
            var interactor = root.AddComponent<DesktopInteractor>();
            interactor.view = cam;
            var crosshair = root.AddComponent<Crosshair>();
            crosshair.interactor = interactor;
            var avatar = root.AddComponent<PlayerAvatar>();
            avatar.role = PlayerRole.Instructions; avatar.playerNumber = 1; avatar.isLocalPlayer = true; avatar.head = head.transform;

            // Networking (Milestone 3). Everything local-only starts switched OFF; NetworkedPlayer.Spawned turns it on
            // only on the computer that owns this player, so a remote copy never moves, renders or grabs the mouse.
            var netObj = root.AddComponent<NetworkObject>();
            netObj.Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;   // avatar disappears when its player leaves
            // DesktopPlayerController moves the player in Update(). In Shared Mode, Fusion otherwise also smooths
            // (interpolates) the owner's own transform every frame, which overwrites that movement: you could only
            // look up/down (the head) but not walk or turn. Fusion's docs say to set this flag in exactly that case.
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.ConfigFlags |= NetworkTransform.NetworkTransformFlags.DisableSharedModeInterpolation;
            var net = root.AddComponent<NetworkedPlayer>();
            net.controller = controller; net.interactor = interactor; net.crosshair = crosshair; net.avatar = avatar;
            net.view = cam; net.listener = listener; net.body = body; net.head = head.transform;
            body.enabled = false; controller.enabled = false; interactor.enabled = false; crosshair.enabled = false;
            cam.enabled = false; listener.enabled = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Same simple environment as DesktopSceneBuilder: light, floor and the outdoor vent marker (step A2).
        static void BuildEnvironment()
        {
            var light = new GameObject("WorkshopLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.25f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .50f, .53f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "WorkshopFloor";
            floor.transform.position = new Vector3(0, -.06f, 0);
            floor.transform.localScale = new Vector3(12, .1f, 12);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/DarkMetal.mat");

            var vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = "OutdoorVentMarker";
            vent.transform.position = new Vector3(.62f, 2, -1.35f);
            vent.transform.localScale = new Vector3(.6f, .55f, .06f);
            vent.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/Green.mat");
            VentLabel(vent.transform, "OutdoorLabel", -.55f, Quaternion.identity);
            VentLabel(vent.transform, "OutdoorLabelFront", .55f, Quaternion.Euler(0, 180, 0));
        }

        static void VentLabel(Transform vent, string name, float z, Quaternion rotation)
        {
            var o = new GameObject(name);
            o.transform.SetParent(vent, false);
            o.transform.localPosition = new Vector3(0, 0, z);
            o.transform.localRotation = rotation;
            o.transform.localScale = new Vector3(1 / .6f, 1 / .55f, 1 / .06f);
            var tm = o.AddComponent<TextMesh>();
            tm.text = "OUTDOORS\nEXHAUST VENT";
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 96; tm.characterSize = .04f / 6f * 64f / 96f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            o.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/LabelText.mat");
        }
    }
}
