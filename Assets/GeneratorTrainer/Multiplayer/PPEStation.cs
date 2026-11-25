using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    public enum PPEKind { Glasses, EarProtection }

    // Marks a clickable PPE item on the table (desktop crosshair / VR ray + trigger).
    public sealed class PPEItem : MonoBehaviour
    {
        public PPEKind kind;
    }

    // Step A1 as a real action (multiplayer scene only): a PPE table with two sets of safety glasses and ear
    // defenders. Each player takes one of each (click / trigger) and "wears" it:
    //   - glasses: a light frame at the edges of the desktop view; ear defenders: all sounds quieter;
    //   - the player's avatar shows glasses / ear defenders, so the partner can see who is equipped.
    // A1 completes automatically when EVERYONE in the session wears both (GeneratorNetSync), and a new run (RESET or a
    // new session) puts everything back on the table (NetworkedPlayer).
    // The table needs no network data of its own: it shows "2 minus the number of players wearing it" per item type.
    // The board's old PPE button is hidden here; the single-player scenes keep it.
    public sealed class PPEStation : MonoBehaviour
    {
        public static PPEStation Instance { get; private set; }
        const int Sets = 2;

        readonly List<GameObject> glasses = new List<GameObject>();
        readonly List<GameObject> earProtection = new List<GameObject>();
        readonly List<Material> materials = new List<Material>();

        void Awake()
        {
            Instance = this;
            Build();
            HideBoardPPEButton();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var m in materials) if (m) Destroy(m);
            if (vrLens) Destroy(vrLens);
            if (lensMaterial) Destroy(lensMaterial);
        }

        // ---------------- taking / availability ----------------

        public static string Label(PPEKind kind) { return kind == PPEKind.Glasses ? "Safety glasses" : "Ear defenders"; }

        public int Taken(PPEKind kind)
        {
            int n = 0;
            foreach (var p in NetworkedPlayer.Players)
                if (p != null && p.Object != null && p.Object.IsValid && p.Wears(kind)) n++;
            return n;
        }

        public int Available(PPEKind kind) { return Mathf.Max(0, Sets - Taken(kind)); }

        // Text for the crosshair while pointing at an item.
        public string HoverText(PPEKind kind)
        {
            var me = NetworkedPlayer.Local;
            if (me == null) return Label(kind) + "  -  join a session first";
            if (me.Wears(kind)) return Label(kind) + "  -  you are already wearing them";
            return Label(kind) + "  -  click to put on";
        }

        public void Take(PPEKind kind)
        {
            var me = NetworkedPlayer.Local;
            if (me == null || me.Wears(kind) || Available(kind) <= 0) return;
            me.PutOn(kind);
        }

        // Tablet tip for step A1: what to do and who is still missing what.
        public string TipText()
        {
            var sb = new StringBuilder("Every player takes safety glasses and ear defenders from the PPE table and puts them on.");
            var missing = new List<string>();
            foreach (var p in NetworkedPlayer.Players)
            {
                if (p == null || p.Object == null || !p.Object.IsValid || p.Number <= 0) continue;
                string need = (!p.Wears(PPEKind.Glasses) ? "glasses" : "") +
                              (!p.Wears(PPEKind.Glasses) && !p.Wears(PPEKind.EarProtection) ? " + " : "") +
                              (!p.Wears(PPEKind.EarProtection) ? "ear defenders" : "");
                if (need.Length > 0) missing.Add("Player " + p.Number + ": " + need);
            }
            if (missing.Count > 0) sb.Append("  Still missing - ").Append(string.Join(", ", missing)).Append('.');
            return sb.ToString();
        }

        void Update()
        {
            Show(glasses, Available(PPEKind.Glasses));
            Show(earProtection, Available(PPEKind.EarProtection));

            // VR: the screen overlay in OnGUI is not visible in a headset, so the glasses become a thin tinted "lens"
            // panel just in front of the eyes, attached to the VR camera.
            var me = NetworkedPlayer.Local;
            bool vrGlasses = me != null && me.IsVR && me.Wears(PPEKind.Glasses) && VRRig.Local != null && VRRig.Local.head != null;
            if (vrGlasses && vrLens == null) vrLens = BuildVRLens(VRRig.Local.head);
            if (vrLens != null && vrLens.activeSelf != vrGlasses) vrLens.SetActive(vrGlasses);
        }

        GameObject vrLens;
        UnityEngine.Material lensMaterial;

        GameObject BuildVRLens(Transform head)
        {
            lensMaterial = new UnityEngine.Material(UnityEngine.UI.Graphic.defaultGraphicMaterial);
            lensMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);   // always in front
            var root = new GameObject("VRGlassesLens", typeof(RectTransform));
            root.transform.SetParent(head, false);
            root.transform.localPosition = new Vector3(0f, 0f, 0.12f);   // 12 cm in front of the eyes
            root.transform.localRotation = Quaternion.identity;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;   // below the tablet and the VR menu
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1000, 700);
            root.transform.localScale = Vector3.one * 0.0004f;   // 0.4 m x 0.28 m: covers the whole field of view
            LensImage(rect, new Color(0.95f, 0.85f, 0.35f, 0.07f), Vector2.zero, Vector2.one);              // faint tint
            var frame = new Color(0.08f, 0.08f, 0.08f, 0.5f);
            LensImage(rect, frame, new Vector2(0f, 0.97f), Vector2.one);                                    // top rim
            LensImage(rect, frame, Vector2.zero, new Vector2(1f, 0.03f));                                   // bottom rim
            LensImage(rect, frame, Vector2.zero, new Vector2(0.02f, 1f));                                   // left rim
            LensImage(rect, frame, new Vector2(0.98f, 0f), Vector2.one);                                    // right rim
            LensImage(rect, frame, new Vector2(0.48f, 0.9f), new Vector2(0.52f, 1f));                       // bridge
            return root;
        }

        void LensImage(RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Lens", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.material = lensMaterial;
            img.raycastTarget = false;   // must not block the controller rays on the menu
            var r = (RectTransform)go.transform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static void Show(List<GameObject> items, int count)
        {
            for (int i = 0; i < items.Count; i++)
            {
                bool visible = i < count;
                if (items[i].activeSelf != visible) items[i].SetActive(visible);
            }
        }

        // Desktop only: a light glasses frame around the view and a small "what I am wearing" line.
        // (VR players feel the ear defenders as quieter sound and see the items disappear from the table.)
        void OnGUI()
        {
            var me = NetworkedPlayer.Local;
            if (me == null || me.IsVR) return;
            bool g = me.Wears(PPEKind.Glasses), e = me.Wears(PPEKind.EarProtection);
            float w = Screen.width, h = Screen.height, t = Mathf.Max(6f, h * 0.012f);
            Color old = GUI.color;
            if (g)
            {
                GUI.color = new Color(0.95f, 0.85f, 0.35f, 0.07f);   // faint safety-yellow tint
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.55f);   // frame
                GUI.DrawTexture(new Rect(0, 0, w, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, h - t, w, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, t, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w - t, 0, t, h), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(w / 2 - t, 0, t * 2, h * 0.06f), Texture2D.whiteTexture);   // bridge
            }
            GUI.color = old;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * h / 1080f) };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            string text = "PPE:  glasses " + (g ? "[ON]" : "[ - ]") + "    ear defenders " + (e ? "[ON]" : "[ - ]");
            GUI.Label(new Rect(20 + t, h - 40 * h / 1080f - t, 600, 40 * h / 1080f), text, style);
        }

        // ---------------- construction (simple shapes, like the avatars) ----------------

        void Build()
        {
            // Table, facing the spawn points (players look towards -Z at the generator).
            Box(transform, "TableTop", new Vector3(0, 0.74f, 0), new Vector3(1.1f, 0.04f, 0.5f), new Color(0.35f, 0.36f, 0.38f), true);
            Box(transform, "TableBase", new Vector3(0, 0.36f, 0), new Vector3(1.0f, 0.72f, 0.42f), new Color(0.22f, 0.23f, 0.25f), true);
            Box(transform, "SignBoard", new Vector3(0, 1.0f, -0.23f), new Vector3(1.1f, 0.45f, 0.03f), new Color(0.1f, 0.3f, 0.6f), true);
            var sign = new GameObject("SignText");
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0, 1.0f, -0.20f);
            sign.transform.localRotation = Quaternion.Euler(0, 180f, 0);   // readable from the spawn side
            var tm = sign.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; tm.fontSize = 64; tm.characterSize = 0.012f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;
            tm.text = "PPE STATION\nput on safety glasses\nand ear defenders";
            sign.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            for (int i = 0; i < Sets; i++)
            {
                float x = i == 0 ? -0.27f : 0.27f;
                glasses.Add(Glasses(new Vector3(x - 0.1f, 0.78f, 0.05f)));
                earProtection.Add(EarDefenders(new Vector3(x + 0.11f, 0.8f, 0.05f)));
            }
        }

        GameObject Glasses(Vector3 position)
        {
            var root = Item("SafetyGlasses", PPEKind.Glasses, position, new Vector3(0.2f, 0.1f, 0.14f));
            var yellow = new Color(1f, 0.85f, 0.2f);
            Box(root.transform, "Frame", new Vector3(0, 0.02f, 0), new Vector3(0.16f, 0.015f, 0.012f), new Color(0.1f, 0.1f, 0.1f), false);
            Box(root.transform, "LensL", new Vector3(-0.042f, 0, 0), new Vector3(0.065f, 0.04f, 0.006f), yellow, false);
            Box(root.transform, "LensR", new Vector3(0.042f, 0, 0), new Vector3(0.065f, 0.04f, 0.006f), yellow, false);
            Box(root.transform, "ArmL", new Vector3(-0.08f, 0.02f, -0.05f), new Vector3(0.008f, 0.008f, 0.1f), new Color(0.1f, 0.1f, 0.1f), false);
            Box(root.transform, "ArmR", new Vector3(0.08f, 0.02f, -0.05f), new Vector3(0.008f, 0.008f, 0.1f), new Color(0.1f, 0.1f, 0.1f), false);
            return root;
        }

        GameObject EarDefenders(Vector3 position)
        {
            var root = Item("EarDefenders", PPEKind.EarProtection, position, new Vector3(0.2f, 0.12f, 0.14f));
            var red = new Color(0.85f, 0.15f, 0.1f);
            Box(root.transform, "Band", new Vector3(0, 0.05f, 0), new Vector3(0.15f, 0.015f, 0.03f), new Color(0.1f, 0.1f, 0.1f), false);
            Cup(root.transform, "CupL", new Vector3(-0.07f, 0, 0), red);
            Cup(root.transform, "CupR", new Vector3(0.07f, 0, 0), red);
            return root;
        }

        GameObject Item(string name, PPEKind kind, Vector3 position, Vector3 hitSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.AddComponent<PPEItem>().kind = kind;
            var hit = go.AddComponent<BoxCollider>();   // generous hit box: easier to point at than the thin parts
            hit.size = hitSize;
            return go;
        }

        void Cup(Transform parent, string name, Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            go.transform.localScale = new Vector3(0.07f, 0.02f, 0.07f);
            Tint(go, color);
        }

        GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!solid) Destroy(go.GetComponent<Collider>());   // only the item's own hit box is clickable
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Tint(go, color);
            return go;
        }

        void Tint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            var m = r.material;
            m.color = color;
            materials.Add(m);
        }

        // The board's PPE button is replaced by the table in this scene (single-player scenes keep it).
        void HideBoardPPEButton()
        {
            var trainer = FindAnyObjectByType<GeneratorTraining>();
            if (trainer == null) return;
            foreach (var t in trainer.GetComponentsInChildren<Transform>(true))
                if (t.name == "BoardPPE" || t.name == "BoardPPELabel" || t.name == "BoardPPEHit")
                    t.gameObject.SetActive(false);
        }
    }
}
