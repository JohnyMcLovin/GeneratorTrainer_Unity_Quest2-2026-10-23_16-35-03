using System.IO;
using GeneratorTrainer.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeneratorTrainer.Editor
{
    // Milestone 2: builds Assets/Scenes/MultiplayerDesktop.unity, a first-person desktop scene around the SAME
    // generator prefab as the other scenes, plus the DesktopPlayer prefab that Milestone 3 will network.
    // Nothing existing is modified: the only change is a per-scene override that turns off the generator's own
    // mouse click (desktopControls), because the player's crosshair interactor replaces it here.
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

            // Generator: prefab instance; the crosshair interactor replaces its built-in mouse click in this scene.
            var gen = (GameObject)PrefabUtility.InstantiatePrefab(generatorPrefab, scene);
            gen.transform.position = Vector3.zero;
            var trainer = gen.GetComponent<GeneratorTraining>();
            trainer.desktopControls = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trainer);

            BuildEnvironment();

            // Local player, in front of the control panel, facing the generator.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(-0.4f, 0.05f, 3.2f);
            player.transform.rotation = Quaternion.Euler(0, 180, 0);

            // Static preview of the other player's avatar so its look can be judged before networking exists.
            // Remove it (or rebuild the scene) once Milestone 3 spawns real players.
            var preview = new GameObject("AvatarPreview (Player 2, remove in Milestone 3)");
            preview.transform.position = new Vector3(2.4f, 0, 2.3f);
            preview.transform.rotation = Quaternion.LookRotation(new Vector3(-1.6f, 0, -1.4f));
            var previewAvatar = preview.AddComponent<PlayerAvatar>();
            previewAvatar.role = PlayerRole.Operator;
            previewAvatar.playerNumber = 2;
            previewAvatar.isLocalPlayer = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MultiplayerSceneBuilder] Saved " + ScenePath + " and " + PlayerPrefabPath +
                      ". Press Play: WASD walk, mouse look, left-click controls, Shift faster, Esc frees the mouse.");
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
            head.AddComponent<AudioListener>();

            var controller = root.AddComponent<DesktopPlayerController>();
            controller.head = head.transform;
            var interactor = root.AddComponent<DesktopInteractor>();
            interactor.view = cam;
            root.AddComponent<Crosshair>().interactor = interactor;
            var avatar = root.AddComponent<PlayerAvatar>();
            avatar.role = PlayerRole.Instructions; avatar.playerNumber = 1; avatar.isLocalPlayer = true; avatar.head = head.transform;

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
