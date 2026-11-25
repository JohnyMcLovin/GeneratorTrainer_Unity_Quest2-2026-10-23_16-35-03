using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeneratorTrainer.Editor
{
    // Builds the workshop around the generator in the multiplayer scene (called by MultiplayerSceneBuilder).
    // Chunk 1: room - concrete floor, two-tone painted walls, ceiling with steel beams, roller door, windows.
    // Chunk 2: lighting - glowing ceiling lamp panels, two soft lamps without shadows, warmer indoor ambient light.
    //
    // Built from simple shapes and a few materials, all marked static so Unity can batch them - light enough for the
    // Meta Quest 2. Walls and ceiling do not cast shadows, so the main (sun) light still lights the room and the
    // generator keeps its shadow on the floor. Walls have colliders: nobody walks or teleports out of the room.
    // Generated materials and the concrete texture live in Assets/GeneratorTrainer/Generated/Environment.
    public static class WorkshopEnvironmentBuilder
    {
        const string Folder = "Assets/GeneratorTrainer/Generated/Environment";

        // Room interior (metres). The generator stands around x -2..2, z -0.6..1.1, its controls face +Z.
        public const float MinX = -6f, MaxX = 6f, BackZ = -1.6f, FrontZ = 6.5f, Height = 4.2f;
        const float Wall = 0.2f;          // wall thickness
        const float DadoHeight = 1.3f;    // lower painted band

        static Material concrete, wallLower, wallUpper, stripe, ceiling, beam, doorMat, doorFrame, windowGlass, windowFrame, lampPanel;

        public static Transform Build(Transform floor)
        {
            Materials();
            var root = new GameObject("Workshop").transform;

            // ---- Floor: concrete instead of the dark metal plate (same object, still the teleport floor in VR).
            if (floor != null)
            {
                floor.GetComponent<Renderer>().sharedMaterial = concrete;
                floor.localScale = new Vector3(MaxX - MinX + 2f, 0.1f, FrontZ - BackZ + 2f);
                floor.position = new Vector3((MinX + MaxX) / 2f, -0.06f, (BackZ + FrontZ) / 2f);
                MakeStatic(floor.gameObject);
            }

            // ---- Walls: back (behind the generator), front (roller door), left and right (windows).
            float midX = (MinX + MaxX) / 2f, midZ = (BackZ + FrontZ) / 2f, width = MaxX - MinX, depth = FrontZ - BackZ;
            PaintedWall(root, "BackWall", new Vector3(midX, 0, BackZ - Wall / 2f), width + 2 * Wall, Wall, true);
            FrontWallWithDoor(root, width);
            PaintedWall(root, "LeftWall", new Vector3(MinX - Wall / 2f, 0, midZ), depth, Wall, false);
            PaintedWall(root, "RightWall", new Vector3(MaxX + Wall / 2f, 0, midZ), depth, Wall, false);
            Windows(root, MinX, +1);
            Windows(root, MaxX, -1);

            // ---- Ceiling with steel beams.
            Box(root, "Ceiling", new Vector3(midX, Height + 0.05f, midZ), new Vector3(width + 2 * Wall, 0.1f, depth + 2 * Wall), ceiling, true, false);
            for (float x = MinX + 1.5f; x < MaxX; x += 3f)
                Box(root, "Beam", new Vector3(x, Height - 0.15f, midZ), new Vector3(0.18f, 0.3f, depth), beam, false, false);
            Box(root, "BeamLong", new Vector3(midX, Height - 0.35f, 2.2f), new Vector3(width, 0.14f, 0.14f), beam, false, false);

            // ---- Chunk 2: lighting.
            Lighting(root);

            MakeStaticRecursive(root.gameObject);
            return root;
        }

        // ---------------- room parts ----------------

        // A wall with a darker lower band, a thin yellow line and a lighter upper part.
        static void PaintedWall(Transform root, string name, Vector3 baseCentre, float length, float thickness, bool alongX)
        {
            var wall = new GameObject(name).transform;
            wall.SetParent(root, false);
            Vector3 size(float h) => alongX ? new Vector3(length, h, thickness) : new Vector3(thickness, h, length);
            Box(wall, "Lower", baseCentre + Vector3.up * (DadoHeight / 2f), size(DadoHeight), wallLower, true, false);
            Box(wall, "Line", baseCentre + Vector3.up * (DadoHeight + 0.03f), size(0.06f) + (alongX ? new Vector3(0, 0, 0.01f) : new Vector3(0.01f, 0, 0)), stripe, false, false);
            float upper = Height - DadoHeight - 0.06f;
            Box(wall, "Upper", baseCentre + Vector3.up * (DadoHeight + 0.06f + upper / 2f), size(upper), wallUpper, true, false);
        }

        // Front wall (behind the spawn points) with a large closed roller door in the middle.
        static void FrontWallWithDoor(Transform root, float width)
        {
            const float doorWidth = 3.6f, doorHeight = 3.2f;
            float z = FrontZ + Wall / 2f;
            float side = (width - doorWidth) / 2f;
            PaintedWall(root, "FrontWallLeft", new Vector3(MinX + side / 2f - Wall / 2f, 0, z), side + Wall, Wall, true);
            PaintedWall(root, "FrontWallRight", new Vector3(MaxX - side / 2f + Wall / 2f, 0, z), side + Wall, Wall, true);
            Box(root, "FrontWallAboveDoor", new Vector3(0, doorHeight + (Height - doorHeight) / 2f, z), new Vector3(doorWidth, Height - doorHeight, Wall), wallUpper, true, false);

            var door = new GameObject("RollerDoor").transform;
            door.SetParent(root, false);
            Box(door, "DoorBack", new Vector3(0, doorHeight / 2f, z + 0.02f), new Vector3(doorWidth, doorHeight, 0.06f), doorMat, true, false);
            for (float y = 0.2f; y < doorHeight; y += 0.2f)   // horizontal slats
                Box(door, "Slat", new Vector3(0, y, z - 0.02f), new Vector3(doorWidth - 0.1f, 0.035f, 0.03f), doorFrame, false, false);
            Box(door, "FrameLeft", new Vector3(-doorWidth / 2f - 0.08f, doorHeight / 2f, z - 0.05f), new Vector3(0.16f, doorHeight, 0.1f), stripe, false, false);
            Box(door, "FrameRight", new Vector3(doorWidth / 2f + 0.08f, doorHeight / 2f, z - 0.05f), new Vector3(0.16f, doorHeight, 0.1f), stripe, false, false);
            Box(door, "FrameTop", new Vector3(0, doorHeight + 0.08f, z - 0.05f), new Vector3(doorWidth + 0.32f, 0.16f, 0.1f), stripe, false, false);
        }

        // Three windows with daylight on a side wall. 'inward' = +1 for the left wall (faces +X), -1 for the right wall.
        static void Windows(Transform root, float wallX, int inward)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = BackZ + 1.6f + i * 2.6f;
                float x = wallX + inward * 0.01f;
                var w = new GameObject("Window").transform;
                w.SetParent(root, false);
                Box(w, "Glass", new Vector3(x, 2.4f, z), new Vector3(0.02f, 1.1f, 1.8f), windowGlass, false, false);
                Box(w, "FrameTop", new Vector3(x + inward * 0.02f, 2.97f, z), new Vector3(0.05f, 0.06f, 1.9f), windowFrame, false, false);
                Box(w, "FrameBottom", new Vector3(x + inward * 0.04f, 1.83f, z), new Vector3(0.1f, 0.06f, 1.9f), windowFrame, false, false);
                Box(w, "FrameLeft", new Vector3(x + inward * 0.02f, 2.4f, z - 0.92f), new Vector3(0.05f, 1.2f, 0.06f), windowFrame, false, false);
                Box(w, "FrameRight", new Vector3(x + inward * 0.02f, 2.4f, z + 0.92f), new Vector3(0.05f, 1.2f, 0.06f), windowFrame, false, false);
                Box(w, "Mullion", new Vector3(x + inward * 0.02f, 2.4f, z), new Vector3(0.04f, 1.1f, 0.04f), windowFrame, false, false);
            }
        }

        // ---------------- chunk 2: lighting ----------------

        static void Lighting(Transform root)
        {
            // Glowing ceiling lamp panels (no real light each - cheap on the Quest).
            var lamps = new GameObject("CeilingLamps").transform;
            lamps.SetParent(root, false);
            foreach (float x in new[] { -3.5f, 0f, 3.5f })
                foreach (float z in new[] { 0.6f, 3.6f })
                {
                    Box(lamps, "LampHousing", new Vector3(x, Height - 0.33f, z), new Vector3(1.3f, 0.06f, 0.36f), beam, false, false);
                    Box(lamps, "LampPanel", new Vector3(x, Height - 0.365f, z), new Vector3(1.2f, 0.01f, 0.28f), lampPanel, false, false);
                }

            // Two soft lamps without shadows for warm indoor light (real lights are expensive on the Quest).
            foreach (var p in new[] { new Vector3(-2.2f, Height - 0.6f, 1.8f), new Vector3(2.4f, Height - 0.6f, 2.6f) })
            {
                var go = new GameObject("RoomLight");
                go.transform.SetParent(root, false);
                go.transform.position = p;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 1.1f;
                l.color = new Color(1f, 0.93f, 0.82f);
                l.shadows = LightShadows.None;
            }

            // Indoor ambient light: brighter from above, darker from the floor.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.58f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.26f);
        }

        // ---------------- helpers ----------------

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider, bool castShadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;   // room must not block the light
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void MakeStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        static void MakeStaticRecursive(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<Light>() == null) MakeStatic(t.gameObject);
        }

        // ---------------- materials ----------------

        static void Materials()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));
            concrete = Mat("Concrete", new Color(0.78f, 0.77f, 0.74f), ConcreteTexture(), new Vector2(7f, 5f), 0.15f);
            wallLower = Mat("WallLower", new Color(0.33f, 0.43f, 0.5f), null, Vector2.one, 0.2f);
            wallUpper = Mat("WallUpper", new Color(0.8f, 0.8f, 0.77f), null, Vector2.one, 0.1f);
            stripe = Mat("SafetyYellow", new Color(0.95f, 0.75f, 0.1f), null, Vector2.one, 0.3f);
            ceiling = Mat("Ceiling", new Color(0.36f, 0.37f, 0.39f), null, Vector2.one, 0.05f);
            beam = Mat("SteelBeam", new Color(0.25f, 0.27f, 0.3f), null, Vector2.one, 0.35f, 0.6f);
            doorMat = Mat("RollerDoor", new Color(0.55f, 0.58f, 0.6f), null, Vector2.one, 0.4f, 0.5f);
            doorFrame = Mat("RollerDoorSlat", new Color(0.42f, 0.45f, 0.48f), null, Vector2.one, 0.4f, 0.5f);
            windowFrame = Mat("WindowFrame", new Color(0.2f, 0.21f, 0.23f), null, Vector2.one, 0.4f, 0.4f);
            windowGlass = Mat("WindowDaylight", new Color(0.72f, 0.85f, 0.95f), null, Vector2.one, 0.8f);
            Emissive(windowGlass, new Color(0.75f, 0.87f, 1f) * 1.1f);
            lampPanel = Mat("LampPanel", Color.white, null, Vector2.one, 0.5f);
            Emissive(lampPanel, new Color(1f, 0.96f, 0.88f) * 1.6f);
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Color color, Texture2D texture, Vector2 tiling, float smoothness, float metallic = 0f)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            m.mainTexture = texture;
            m.mainTextureScale = tiling;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Emissive(Material m, Color emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
        }

        // A simple generated concrete texture (soft noise + small specks), saved once as a PNG asset.
        static Texture2D ConcreteTexture()
        {
            string path = Folder + "/Concrete.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var rng = new System.Random(7);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Tileable noise: sample on a circle in both directions.
                    float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(3f + Mathf.Cos(u) * 1.6f, 3f + Mathf.Sin(v) * 1.6f + Mathf.Sin(u) * 0.8f) * 0.6f
                            + Mathf.PerlinNoise(9f + Mathf.Cos(u) * 5f, 9f + Mathf.Sin(v) * 5f) * 0.4f;
                    float g = 0.78f + (n - 0.5f) * 0.22f;
                    if (rng.NextDouble() < 0.012) g -= 0.12f;   // specks
                    tex.SetPixel(x, y, new Color(g, g * 0.99f, g * 0.96f));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
