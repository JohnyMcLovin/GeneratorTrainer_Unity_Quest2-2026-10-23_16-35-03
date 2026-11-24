using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace GeneratorTrainer.Editor
{
    public static class TrainerBuilder
    {
        const string Root = "Assets/GeneratorTrainer";
        [Serializable] public class Model { public MeshDef[] meshes; public MatDef[] materials; public Pivot[] pivots; public Part[] parts; public Label[] labels; public Control[] controls; }
        [Serializable] public class MeshDef { public string name; public float[] vertices, normals, uv; public int[] triangles; }
        [Serializable] public class MatDef { public string name, texture; public float[] color; public float metallic, roughness; }
        [Serializable] public class Pivot { public string name, parent; public float[] position, rotation; }
        [Serializable] public class Part : Pivot { public string mesh, material; public float[] scale; }
        [Serializable] public class Label : Pivot { public string text; public float size; public float[] color; }
        [Serializable] public class Control { public string name, parent, action; public float[] position, size; public int index; }
        static Vector3 V(float[] a) { return new Vector3(a[0], a[1], a[2]); }
        static Color C(float[] a) { return new Color(a[0], a[1], a[2]); }
        static Transform Parent(Dictionary<string, Transform> nodes, Transform root, string p) { return string.IsNullOrEmpty(p) ? root : nodes[p]; }
        static GameObject Obj(string n, Transform parent, Vector3 p) { var o = new GameObject(n); o.transform.SetParent(parent, false); o.transform.localPosition = p; return o; }
        [MenuItem("Tools/Generator Trainer/1 - Create Desktop Demo")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && !pipeline.GetType().Name.Contains("Universal")) { EditorUtility.DisplayDialog("Render pipeline", "Use Built-in or URP for this kit. HDRP is not configured.", "OK"); return; }
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Source/TrainerModel.json");
            if (!source) { Debug.LogError("Import the complete Assets/GeneratorTrainer folder first."); return; }
            Model data = JsonUtility.FromJson<Model>(source.text);
            string folder = AssetDatabase.GenerateUniqueAssetPath(Root + "/Generated");
            AssetDatabase.CreateFolder(Root, Path.GetFileName(folder));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var meshes = new Dictionary<string, Mesh>(); var mats = new Dictionary<string, Material>();
            foreach (var d in data.meshes)
            {
                var m = new Mesh { name = d.name }; var v = new Vector3[d.vertices.Length / 3]; var n = new Vector3[v.Length]; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++) { v[i] = new Vector3(d.vertices[i*3],d.vertices[i*3+1],d.vertices[i*3+2]); n[i] = new Vector3(d.normals[i*3],d.normals[i*3+1],d.normals[i*3+2]); uv[i] = new Vector2(d.uv[i*2], d.uv[i*2+1]); }
                m.vertices = v; m.normals = n; m.uv = uv; m.triangles = d.triangles; m.RecalculateBounds(); m.RecalculateTangents();
                AssetDatabase.CreateAsset(m, folder + "/" + d.name + ".asset"); meshes[d.name] = m;
            }
            Shader shader = Shader.Find(pipeline == null ? "Standard" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Required Lit shader was not found.");
            foreach (var d in data.materials)
            {
                var m = new Material(shader) { name = d.name, color = C(d.color) };
                m.SetFloat("_Metallic", d.metallic); if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1-d.roughness); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1-d.roughness);
                if (!string.IsNullOrEmpty(d.texture))
                {
                    string path = Root + "/Textures/" + d.texture + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null) { importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = true; importer.maxTextureSize = 1024; importer.wrapMode = TextureWrapMode.Repeat; importer.SaveAndReimport(); }
                    m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                AssetDatabase.CreateAsset(m, folder + "/" + d.name + ".mat"); mats[d.name] = m;
            }
            var root = new GameObject("GeneratorTrainer"); var trainer = root.AddComponent<GeneratorTraining>(); var nodes = new Dictionary<string, Transform>();
            foreach (var d in data.pivots) { var o = Obj(d.name, Parent(nodes, root.transform, d.parent), V(d.position)); o.transform.localRotation = Quaternion.Euler(V(d.rotation)); nodes[d.name] = o.transform; }
            foreach (var d in data.parts)
            {
                var o = Obj(d.name, Parent(nodes, root.transform, d.parent), V(d.position)); o.transform.localRotation = Quaternion.Euler(V(d.rotation)); o.transform.localScale = V(d.scale);
                o.AddComponent<MeshFilter>().sharedMesh = meshes[d.mesh]; o.AddComponent<MeshRenderer>().sharedMaterial = mats[d.material];
                // Match geometry; decal planes have no colliders. Meshes are small and convex.
                if (d.mesh != "Quad") { var col = o.AddComponent<BoxCollider>(); col.size = Vector3.one; }
                nodes[d.name] = o.transform;
            }
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var d in data.labels)
            {
                var o = Obj(d.name, Parent(nodes, root.transform, d.parent), V(d.position)); o.transform.localRotation = Quaternion.Euler(V(d.rotation));
                var t = o.AddComponent<TextMesh>(); t.text = d.text; t.font = font; t.fontSize = 64; t.characterSize = d.size / 6f; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = C(d.color);
                o.GetComponent<MeshRenderer>().sharedMaterial = font.material; nodes[d.name] = o.transform;
            }
            foreach (var d in data.controls)
            {
                var o = Obj(d.name, Parent(nodes, root.transform, d.parent), V(d.position)); var c = o.AddComponent<BoxCollider>(); c.size = V(d.size);
                var ctrl = o.AddComponent<TrainerControl>(); ctrl.trainer = trainer; ctrl.action = d.action; ctrl.index = d.index; ctrl.reversePoke = d.action == "ReadPlate";
            }
            var zone = Obj("DieselFillZone", root.transform, new Vector3(1.16f,.68f,.79f)); var zcol = zone.AddComponent<SphereCollider>(); zcol.isTrigger = true; zcol.radius = .24f;
            zone.AddComponent<FuelZone>().trainer = trainer; nodes["CanPivot"].gameObject.AddComponent<DieselCan>();
            trainer.engine = root.AddComponent<AudioSource>(); trainer.engine.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Engine.wav"); trainer.engine.playOnAwake = false; trainer.engine.loop = true; trainer.engine.spatialBlend = 1; trainer.engine.volume = .25f; trainer.engine.minDistance = 1; trainer.engine.maxDistance = 12;
            // Keep the scene object linked to the prefab, then apply the model fixes (door, labels, signs, handle).
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, folder + "/GeneratorTrainer.prefab", InteractionMode.AutomatedAction);
            TrainerModelFixes.Apply(folder + "/GeneratorTrainer.prefab");
            var camera = new GameObject("DesktopCamera"); camera.tag = "MainCamera"; var cam = camera.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.farClipPlane = 50; cam.backgroundColor = new Color(.12f,.15f,.17f); cam.clearFlags = CameraClearFlags.SolidColor;
            camera.AddComponent<AudioListener>(); camera.AddComponent<TrainerOrbit>(); camera.transform.position = new Vector3(1.1f,2.0f,4.6f); camera.transform.LookAt(new Vector3(-.3f,1,0));
            var light = new GameObject("WorkshopLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.25f; light.transform.rotation = Quaternion.Euler(45,-35,0); light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.48f,.50f,.53f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "WorkshopFloor"; floor.transform.position = new Vector3(0,-.06f,0); floor.transform.localScale = new Vector3(12,.1f,12); floor.GetComponent<Renderer>().sharedMaterial = mats["DarkMetal"];
            // Outdoor vent cue behind the generator, matching the safe exhaust direction.
            var vent = GameObject.CreatePrimitive(PrimitiveType.Cube); vent.name = "OutdoorVentMarker"; vent.transform.position = new Vector3(.62f,2,-1.35f); vent.transform.localScale = new Vector3(.6f,.55f,.06f); vent.GetComponent<Renderer>().sharedMaterial = mats["Green"];
            var vtext = Obj("OutdoorLabel", vent.transform, new Vector3(0,0,-.55f)); vtext.transform.localRotation = Quaternion.identity; vtext.transform.localScale = new Vector3(1/.6f,1/.55f,1/.06f);
            var tm = vtext.AddComponent<TextMesh>(); tm.text = "OUTDOORS\nEXHAUST VENT"; tm.font = font; tm.fontSize = 64; tm.characterSize = .04f / 6f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; vtext.GetComponent<Renderer>().sharedMaterial = font.material;
            // Opposite face label visible from the operating side.
            var front = UnityEngine.Object.Instantiate(vtext, vent.transform); front.name = "OutdoorLabelFront"; front.transform.localPosition = new Vector3(0,0,.55f); front.transform.localRotation = Quaternion.Euler(0,180,0);
            EditorSceneManager.SaveScene(root.scene, folder + "/GeneratorTraining.unity"); AssetDatabase.SaveAssets(); Selection.activeGameObject = root;
            Debug.Log("Generator Trainer created in " + folder + ". Press Play. Left click controls; right drag to orbit; scroll to zoom. For Quest see SETUP.md.");
        }
    }
}
