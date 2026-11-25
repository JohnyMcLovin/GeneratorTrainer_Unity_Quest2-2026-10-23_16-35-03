using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace GeneratorTrainer.Multiplayer
{
    // Session menu for VR players (the desktop menu is drawn on the monitor, which a headset does not show).
    // A panel floating in front of the player, operated with the controller ray + trigger:
    //   not connected: "Create session", a keypad to type the 6-character code, "Join", and status messages;
    //   in a session:  the code, the player count, notices and "Leave session". It stays visible until both players
    //                  are in (so the code can be read out), then hides; the left controller's MENU button toggles it.
    // Built from Unity UI in code. Uses the same SessionLauncher as the desktop menu.
    public sealed class VRSessionMenu : MonoBehaviour
    {
        public SessionLauncher launcher;
        public float distance = 1.3f;

        const string Keys = "ABCDEFGJKLMNPQRSTUVWXYZ23456789";   // same alphabet as the generated codes
        const float Width = 900, Height = 760, PixelSize = 0.0011f;   // ~1 m wide

        Canvas canvas;
        Material uiMaterial;
        Font font;
        Text status, codeText, sessionText, noticeText;
        GameObject idleGroup, sessionGroup;
        string code = "";
        bool shown = true, wasInSession, autoHidden;
        string lastNotice = "";
        InputAction menuToggle;

        void Start()
        {
            if (launcher == null) launcher = GetComponent<SessionLauncher>();
            Build();
            // Any controller's MENU button (on Quest only the left controller has one; in the XR Device Simulator the
            // M key presses it on whichever controller is currently being moved).
            menuToggle = new InputAction("MenuToggle", binding: "<XRController>/menuButton");
            menuToggle.Enable();
            PlaceInFront();
        }

        void OnDestroy()
        {
            if (menuToggle != null) { menuToggle.Disable(); menuToggle.Dispose(); }
            if (uiMaterial) Destroy(uiMaterial);
            if (canvas) Destroy(canvas.gameObject);
        }

        void Update()
        {
            bool inSession = launcher.State == SessionState.InSession;
            // Entering the session: keep showing the panel (the creator must read the code to the partner), placed in
            // front of the new spawn position. It hides itself once both players are in; MENU toggles it after that.
            if (inSession && !wasInSession) { shown = true; autoHidden = false; PlaceInFront(); }
            if (inSession && !autoHidden && launcher.PlayerCount >= SessionLauncher.MaxPlayers) { shown = false; autoHidden = true; }
            if (!inSession && !shown) { shown = true; PlaceInFront(); }
            wasInSession = inSession;

            if (inSession && menuToggle.WasPressedThisFrame()) { shown = !shown; if (shown) PlaceInFront(); }
            string notice = launcher.ActiveNotice;
            if (inSession && notice.Length > 0 && notice != lastNotice) { shown = true; PlaceInFront(); }   // show news
            lastNotice = notice;

            if (canvas.gameObject.activeSelf != shown) canvas.gameObject.SetActive(shown);
            if (!shown) return;

            idleGroup.SetActive(launcher.State == SessionState.Idle);
            sessionGroup.SetActive(inSession);
            status.text = launcher.State == SessionState.Connecting ? launcher.Message
                        : launcher.State == SessionState.Idle ? launcher.Message : "";
            codeText.text = code.PadRight(6, '_');
            if (inSession)
            {
                int players = launcher.PlayerCount;
                sessionText.text = "Session code: " + launcher.Code + "\nPlayers " + players + "/" + SessionLauncher.MaxPlayers +
                                   (players < SessionLauncher.MaxPlayers ? "  - waiting for Player 2" : "");
                noticeText.text = notice;
            }
        }

        void PlaceInFront()
        {
            var rig = VRRig.Local;
            if (rig == null || rig.head == null || canvas == null) return;
            Vector3 forward = rig.head.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            canvas.transform.position = rig.head.position + forward * distance + Vector3.down * 0.1f;
            canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // ---------------- construction ----------------

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            uiMaterial = new Material(Graphic.defaultGraphicMaterial);
            uiMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);   // never hidden inside the generator

            var root = new GameObject("VRSessionMenu", typeof(RectTransform));
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 300;
            if (VRRig.Local != null && VRRig.Local.head != null) canvas.worldCamera = VRRig.Local.head.GetComponent<Camera>();
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();   // lets the controller rays click the buttons
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(Width, Height);
            root.transform.localScale = Vector3.one * PixelSize;

            var bg = Panel(rootRect, "Background", new Color(0.06f, 0.08f, 0.11f, 0.94f));
            Stretch(bg);

            Label(rootRect, "Generator Repair - Multiplayer", 40, FontStyle.Bold, Color.white, new Vector2(0, -50), new Vector2(860, 60));
            status = Label(rootRect, "", 26, FontStyle.Normal, new Color(1f, 0.84f, 0.4f), new Vector2(0, -110), new Vector2(860, 70));

            // Not connected: Create, keypad, Join.
            idleGroup = Group(rootRect, "Idle");
            var idle = (RectTransform)idleGroup.transform;
            ButtonAt(idle, "Create session", new Vector2(0, -190), new Vector2(420, 70), () => launcher.CreateSession(), 32);
            Label(idle, "or join with a code:", 26, FontStyle.Normal, new Color(0.75f, 0.82f, 0.9f), new Vector2(0, -260), new Vector2(860, 40));
            var codeBox = Panel(idle, "CodeBox", new Color(0.12f, 0.16f, 0.21f, 1f));
            Place(codeBox, new Vector2(-110, -320), new Vector2(380, 70));
            codeText = Label(codeBox, "", 44, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(380, 70));
            Stretch(codeText.rectTransform);
            ButtonAt(idle, "Join", new Vector2(200, -320), new Vector2(180, 70), () => launcher.JoinSession(code), 32);

            var keypad = new GameObject("Keypad", typeof(RectTransform)).GetComponent<RectTransform>();
            keypad.SetParent(idle, false);
            Place(keypad, new Vector2(0, -400), new Vector2(800, 300));
            var grid = keypad.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(92, 66);
            grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;
            grid.childAlignment = TextAnchor.UpperCenter;
            foreach (char k in Keys)
            {
                char key = k;
                MakeButton(keypad, key.ToString(), () => { if (code.Length < 6) code += key; }, 30);
            }
            MakeButton(keypad, "<-", () => { if (code.Length > 0) code = code.Substring(0, code.Length - 1); }, 30);

            // In a session: code, players, notice, Leave.
            sessionGroup = Group(rootRect, "Session");
            var session = (RectTransform)sessionGroup.transform;
            sessionText = Label(session, "", 34, FontStyle.Bold, Color.white, new Vector2(0, -230), new Vector2(860, 110));
            noticeText = Label(session, "", 26, FontStyle.Normal, new Color(1f, 0.84f, 0.4f), new Vector2(0, -330), new Vector2(860, 80));
            ButtonAt(session, "Leave session", new Vector2(0, -440), new Vector2(360, 70), () => launcher.LeaveSession(), 30);
            Label(session, "Left controller MENU button: hide / show this panel", 22, FontStyle.Normal, new Color(0.6f, 0.68f, 0.74f),
                  new Vector2(0, -530), new Vector2(860, 40));

            Label(rootRect, "Point with a controller and pull the TRIGGER", 22, FontStyle.Normal, new Color(0.6f, 0.68f, 0.74f),
                  new Vector2(0, -(Height - 50)), new Vector2(860, 40));
        }

        GameObject Group(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go;
        }

        RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.material = uiMaterial;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        Text Label(RectTransform parent, string value, int size, FontStyle style, Color color, Vector2 topCentre, Vector2 boxSize)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = color; t.material = uiMaterial;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = value;
            Place(t.rectTransform, topCentre, boxSize);
            return t;
        }

        void ButtonAt(RectTransform parent, string label, Vector2 topCentre, Vector2 size, UnityEngine.Events.UnityAction onClick, int fontSize)
        {
            var b = MakeButton(parent, label, onClick, fontSize);
            Place((RectTransform)b.transform, topCentre, size);
        }

        Button MakeButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick, int fontSize)
        {
            var go = new GameObject("Button " + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.material = uiMaterial;
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.32f, 0.45f);
            colors.highlightedColor = new Color(0.28f, 0.5f, 0.7f);
            colors.pressedColor = new Color(1f, 0.84f, 0.4f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(go.transform, false);
            text.font = font; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold; text.color = Color.white;
            text.material = uiMaterial; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        // Position a box by its top-centre point, measured from the top-centre of the parent.
        static void Place(RectTransform r, Vector2 topCentre, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = topCentre;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
