using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeneratorTrainer.Editor
{
    // Builds Assets/Scenes/DesktopDemo.unity: the desktop test scene (mouse + orbit camera) using the SAME
    // generator prefab as the XR scenes and the Quest build, so fixes to the prefab show up everywhere.
    public static class DesktopSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DesktopDemo.unity";
        const string PrefabPath = "Assets/GeneratorTrainer/Generated/GeneratorTrainer.prefab";
        const string Generated = "Assets/GeneratorTrainer/Generated";

        [MenuItem("Tools/Generator Trainer/6 - Create Desktop Test Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var gen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            gen.transform.position = Vector3.zero;
            gen.GetComponent<GeneratorTraining>().desktopControls = true;

            var camera = new GameObject("DesktopCamera") { tag = "MainCamera" };
            var cam = camera.AddComponent<Camera>();
            cam.nearClipPlane = .05f; cam.farClipPlane = 50;
            cam.backgroundColor = new Color(.12f, .15f, .17f); cam.clearFlags = CameraClearFlags.SolidColor;
            camera.AddComponent<AudioListener>();
            camera.AddComponent<TrainerOrbit>();
            camera.transform.position = new Vector3(1.1f, 2.0f, 4.6f);
            camera.transform.LookAt(new Vector3(-.3f, 1, 0));

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

            // Outdoor vent marker behind the generator: where the exhaust must point (step A2).
            var vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = "OutdoorVentMarker";
            vent.transform.position = new Vector3(.62f, 2, -1.35f);
            vent.transform.localScale = new Vector3(.6f, .55f, .06f);
            vent.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/Green.mat");
            VentLabel(vent.transform, "OutdoorLabel", -.55f, Quaternion.identity);
            VentLabel(vent.transform, "OutdoorLabelFront", .55f, Quaternion.Euler(0, 180, 0));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Desktop test scene saved to " + ScenePath + ". Press Play: left-click controls, right-drag to orbit, scroll to zoom.");
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
