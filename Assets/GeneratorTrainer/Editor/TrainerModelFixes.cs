using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GeneratorTrainer.Editor
{
    // Fixes applied to the generated GeneratorTrainer prefab(s):
    // 1. The closed door covers the whole panel: it is moved in front of the controls and turned into a
    //    shallow box (rim pieces), so nothing pokes through it. Its upper window gets clear glass.
    // 2. Panel labels are larger and sharper, and are depth-tested so they no longer show through objects.
    // 3. Text that reads mirrored is turned around (TextMesh) or its texture is flipped (decal quads).
    // 4. The door handle is a yellow (Amber) capsule grip, like the handle in the earlier OBJ scene.
    // Safe to run more than once: every step checks whether it was already applied.
    public static class TrainerModelFixes
    {
        const string GeneratedRoot = "Assets/GeneratorTrainer";
        const float LabelScale = 1.6f;     // panel labels become 60% taller
        const int SharpFontSize = 96;      // higher font resolution, same characterSize-based size

        // Checked with renders from the front (24 Nov): the TextMesh labels read correctly, but the textures on
        // the generated decal quads (PPE, DIESEL ONLY, instructions plate) read mirrored.
        public static bool FlipTextMeshes = false;
        public static bool FlipDecalQuads = true;

        [MenuItem("Tools/Generator Trainer/5 - Apply Model Fixes")]
        public static void ApplyAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("GeneratorTrainer t:Prefab", new[] { GeneratedRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Generated")) continue;
                Apply(path);
            }
            AssetDatabase.SaveAssets();
        }

        public static void Apply(string prefabPath)
        {
            string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var log = new List<string>();
            try
            {
                var nodes = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!nodes.ContainsKey(t.name)) nodes[t.name] = t;

                FixLabels(root, folder, log);
                if (FlipDecalQuads) FlipQuadMesh(folder, log);
                FixDoor(root, nodes, folder, log);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) nodes[t.name] = t;
                FixWindow(nodes, folder, log);
                FixHandle(nodes, folder, log);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("[TrainerModelFixes] " + prefabPath + ":\n  " + string.Join("\n  ", log));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------- labels ----------

        static void FixLabels(GameObject root, string folder, List<string> log)
        {
            var mat = LabelMaterial(folder);
            int bigger = 0, sharper = 0, flipped = 0;
            foreach (var tm in root.GetComponentsInChildren<TextMesh>(true))
            {
                var r = tm.GetComponent<MeshRenderer>();
                if (r && r.sharedMaterial != mat) r.sharedMaterial = mat;
                if (tm.fontSize == SharpFontSize) continue;  // already done
                bool onBoard = tm.transform.parent && tm.transform.parent.name == "BoardPivot";
                float k = (float)tm.fontSize / SharpFontSize;
                tm.characterSize *= k * (onBoard ? 1f : LabelScale);
                tm.fontSize = SharpFontSize;
                sharper++;
                if (!onBoard) bigger++;
                if (FlipTextMeshes) { tm.transform.localRotation *= Quaternion.Euler(0, 180, 0); flipped++; }
            }
            log.Add("labels: " + bigger + " panel labels enlarged x" + LabelScale + ", " + sharper + " sharpened, "
                + flipped + " turned to read correctly, depth-tested material " + AssetDatabase.GetAssetPath(mat));
        }

        // Same as the built-in font material, but with normal depth testing so text is hidden behind solid parts.
        static Material LabelMaterial(string folder)
        {
            string path = folder + "/LabelText.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shader = Shader.Find("GeneratorTrainer/3D Text");
            if (mat == null)
            {
                mat = new Material(shader) { name = "LabelText" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void FlipQuadMesh(string folder, List<string> log)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Quad.asset");
            if (mesh == null || mesh.name.EndsWith("_readable")) return;
            var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++) uv[i].x = 1f - uv[i].x;
            mesh.uv = uv;
            mesh.name += "_readable";
            EditorUtility.SetDirty(mesh);
            log.Add("decal quads: texture flipped horizontally so signs read correctly");
        }

        // ---------- door ----------

        static readonly string[] Movable = { "KeyProp", "ChargerProp", "CanPivot", "BoardPivot", "DieselFillZone" };

        static void FixDoor(GameObject root, Dictionary<string, Transform> nodes, string folder, List<string> log)
        {
            Transform door;
            if (!nodes.TryGetValue("DoorPivot", out door)) { log.Add("door: DoorPivot missing, skipped"); return; }
            if (nodes.ContainsKey("DoorRimTop")) { log.Add("door: already fixed"); return; }

            var rootT = root.transform;
            // Door footprint in root space: local x 0..0.85, y -0.65..0.65 around the hinge, closed rotation.
            Vector3 hinge = rootT.InverseTransformPoint(door.position);
            float x0 = hinge.x, x1 = hinge.x + 0.85f, y0 = hinge.y - 0.65f, y1 = hinge.y + 0.65f;
            float doorBack = hinge.z - 0.0225f;
            float front = doorBack;
            string frontmost = "";

            // Visible parts and clickable regions inside the footprint must end up behind the closed door.
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) Consider(r.transform, r.bounds, ref front, ref frontmost);
            foreach (var c in root.GetComponentsInChildren<BoxCollider>(true))
                if (c.GetComponent<TrainerControl>() || c.GetComponent<Renderer>()) Consider(c.transform, BoxBounds(c), ref front, ref frontmost);

            void Consider(Transform t, Bounds worldBounds, ref float f, ref string who)
            {
                if (t.IsChildOf(door)) return;
                foreach (var m in Movable) if (nodes.TryGetValue(m, out var mt) && t.IsChildOf(mt)) return;
                Vector3 lo = rootT.InverseTransformPoint(worldBounds.min), hi = rootT.InverseTransformPoint(worldBounds.max);
                Vector3 mn = Vector3.Min(lo, hi), mx = Vector3.Max(lo, hi);
                bool inside = mx.x > x0 + 0.01f && mn.x < x1 - 0.01f && mx.y > y0 + 0.01f && mn.y < y1 - 0.01f;
                if (!inside || mn.z > doorBack + 0.4f) return;            // ignore far-away things
                if (mx.z > f) { f = mx.z; who = t.name; }
            }

            float shift = Mathf.Max(0, front + 0.008f - doorBack);
            door.localPosition += new Vector3(0, 0, shift);

            var paint = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Paint.mat");
            var box = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Box.asset");
            float zc = -0.0225f - shift / 2f, depth = shift + 0.002f;
            Part("DoorRimLeft", door, new Vector3(0.01f, 0, zc), new Vector3(0.02f, 1.3f, depth), box, paint);
            Part("DoorRimRight", door, new Vector3(0.84f, 0, zc), new Vector3(0.02f, 1.3f, depth), box, paint);
            Part("DoorRimTop", door, new Vector3(0.425f, 0.64f, zc), new Vector3(0.85f, 0.02f, depth), box, paint);
            Part("DoorRimBottom", door, new Vector3(0.425f, -0.64f, zc), new Vector3(0.85f, 0.02f, depth), box, paint);
            log.Add("door: moved " + (shift * 100).ToString("0.0") + " cm forward (front-most panel item: " + frontmost
                + "), rim added");
        }

        // Clear glass in the upper door window (display, START/STOP, mode knob, key visible with the door shut).
        // The glass keeps a collider, so controls cannot be clicked through the closed door.
        static void FixWindow(Dictionary<string, Transform> nodes, string folder, List<string> log)
        {
            Transform door;
            if (!nodes.TryGetValue("DoorPivot", out door)) return;
            Transform glass;
            if (!nodes.TryGetValue("DoorWindowGlass", out glass))
            {
                if (nodes.TryGetValue("DoorWindowPanel", out var oldPanel)) Object.DestroyImmediate(oldPanel.gameObject);
                glass = Part("DoorWindowGlass", door, Vector3.zero, Vector3.one,
                    AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Box.asset"), null).transform;
            }
            // Opening between the side bars (x 0.07-0.78) and between the top and bottom panels (y 0.09-0.5).
            glass.localPosition = new Vector3(0.425f, 0.295f, 0);
            glass.localScale = new Vector3(0.71f, 0.41f, 0.006f);
            var r = glass.GetComponent<MeshRenderer>();
            r.sharedMaterial = GlassMaterial(folder);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            log.Add("window: clear glass in the upper door window");
        }

        static Material GlassMaterial(string folder)
        {
            string path = folder + "/Glass.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = "Glass" };
                AssetDatabase.CreateAsset(mat, path);
            }
            // Standard shader in Transparent mode: faint blue tint, glossy reflections.
            mat.color = new Color(0.75f, 0.85f, 0.9f, 0.07f);
            mat.SetFloat("_Mode", 3);
            mat.SetFloat("_Glossiness", 0.92f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // World bounds of a box collider from its transform (works in prefab editing scenes without physics).
        static Bounds BoxBounds(BoxCollider c)
        {
            var b = new Bounds(c.transform.TransformPoint(c.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = c.center + Vector3.Scale(c.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(c.transform.TransformPoint(corner));
            }
            return b;
        }

        static GameObject Part(string name, Transform parent, Vector3 pos, Vector3 scale, Mesh mesh, Material mat)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = pos;
            o.transform.localScale = scale;
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = mat;
            o.AddComponent<BoxCollider>().size = Vector3.one;
            return o;
        }

        // ---------- handle ----------

        static void FixHandle(Dictionary<string, Transform> nodes, string folder, List<string> log)
        {
            Transform handle;
            if (!nodes.TryGetValue("DoorHandle", out handle)) { log.Add("handle: DoorHandle missing, skipped"); return; }
            var amber = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Amber.mat");
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var capsule = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            handle.GetComponent<MeshFilter>().sharedMesh = capsule;
            handle.GetComponent<MeshRenderer>().sharedMaterial = amber;
            // Vertical grip: 5 cm thick, 24 cm long, standing 4 cm off the door surface.
            handle.localScale = new Vector3(0.05f, 0.12f, 0.05f);
            handle.localPosition = new Vector3(handle.localPosition.x, handle.localPosition.y, 0.0225f + 0.04f);
            var col = handle.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col, true);   // clicks go to the DoorHandleHit region
            // The click region must cover the whole grip: at least 17 x 28 cm, in front of the door.
            if (nodes.TryGetValue("DoorHandleHit", out var hit))
            {
                var box = hit.GetComponent<BoxCollider>();
                hit.localPosition = new Vector3(handle.localPosition.x, handle.localPosition.y, hit.localPosition.z);
                box.size = new Vector3(Mathf.Max(box.size.x, 0.17f), Mathf.Max(box.size.y, 0.28f), box.size.z);
            }
            log.Add("handle: yellow capsule grip 5 x 24 cm, click region 17 x 28 cm");
        }
    }
}
