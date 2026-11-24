using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace GeneratorTrainer
{
    // The Expert's station. The Expert cannot see or touch the generator and is NOT told the fault:
    // the Technician reports which warning lights are red, the Expert works out the fault code from the
    // diagnostic guide and selects it (buttons, or keys 1/2/3, 0 to clear), and the manual then shows the
    // steps for that fault code. A wrong code gives a wrong manual, exactly as in reality.
    // Shown only in Collaborative mode (F1 switches modes).
    //
    // Where it appears (one PC, no networking yet):
    // - Built player with two monitors: always on the second monitor.
    // - One monitor (and the Unity editor): Tab switches the window between the Technician view and the
    //   Expert screen. While the Expert screen is up, clicks do not reach the generator.
    // - Headset through Quest Link: on the PC monitor, while the Technician sees the generator in VR.
    public sealed class ExpertScreen : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static bool CoversMainScreen { get; private set; }

        public GeneratorTraining trainer;

        Canvas canvas;
        Text code, title, detail, run, lastAction, manualTitle, manual, repairTitle, repair, footer;
        readonly Image[] buttons = new Image[4];   // 0-2 = fault codes, 3 = clear
        bool showOnMain, onSecondDisplay;
        static bool subscribed;

        static readonly Color Bg = new Color(0.07f, 0.09f, 0.12f, 1f);
        static readonly Color Card = new Color(0.12f, 0.15f, 0.19f, 1f);
        static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f);
        static readonly Color Accent = new Color(0.96f, 0.77f, 0.26f);
        static readonly Color ButtonIdle = new Color(0.2f, 0.25f, 0.31f, 1f);
        static readonly Color ButtonOn = new Color(0.85f, 0.55f, 0.1f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!subscribed) { SceneManager.sceneLoaded += (s, m) => Attach(); subscribed = true; }
            Attach();
        }

        static void Attach()
        {
            if (!AutoCreate) return;
            var t = FindAnyObjectByType<GeneratorTraining>();
            if (t == null || FindAnyObjectByType<ExpertScreen>() != null) return;
            new GameObject("ExpertScreen").AddComponent<ExpertScreen>().trainer = t;
        }

        void Start()
        {
            if (trainer == null) trainer = FindAnyObjectByType<GeneratorTraining>();
            Build();
            // Buttons need an EventSystem; XR scenes already have one (XR Interaction Setup).
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            // A second monitor in a built player becomes the Expert monitor.
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
                canvas.targetDisplay = 1;
                onSecondDisplay = true;
            }
        }

        void OnDisable() { CoversMainScreen = false; }

        void Update()
        {
            if (trainer == null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1))
                trainer.mode = trainer.Collaborative ? TrainerMode.Solo : TrainerMode.Collaborative;
            if (Input.GetKeyDown(KeyCode.Tab) && trainer.Collaborative) showOnMain = !showOnMain;
            if (canvas != null && canvas.enabled)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) trainer.ExpertDiagnose(0);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) trainer.ExpertDiagnose(1);
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) trainer.ExpertDiagnose(2);
                if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) trainer.ExpertDiagnose(-1);
            }
#endif
            if (!trainer.Collaborative) showOnMain = false;
        }

        void LateUpdate()
        {
            if (trainer == null || canvas == null) return;
            bool xr = XRSettings.isDeviceActive;    // overlay canvases draw on the PC monitor only, never in the headset
            bool visible = trainer.Collaborative && (onSecondDisplay || xr || showOnMain);
            canvas.enabled = visible;
            CoversMainScreen = visible && !onSecondDisplay && !xr;
            if (visible) Refresh();
        }

        // ---------- layout ----------

        public void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;   // above the Technician's task panel
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            Panel(root, "Background", 0, 0, 1, 1, Bg);

            // Left column: diagnosis, guide, run.
            Label(root, font, "EXPERT STATION", 40, FontStyle.Bold, Color.white, 0.025f, 0.905f, 0.47f, 0.965f);
            Label(root, font, "You cannot see or touch the generator. Ask the Technician what the panel shows, "
                + "choose the fault code, then guide them through the manual one step at a time.",
                21, FontStyle.Normal, Muted, 0.025f, 0.835f, 0.47f, 0.905f);

            var db = (RectTransform)Panel(root, "DiagnosisBox", 0.025f, 0.6f, 0.47f, 0.825f, Card).transform;
            Label(db, font, "FAULT CODE  (your diagnosis)", 18, FontStyle.Bold, Muted, 0.04f, 0.8f, 0.96f, 0.95f);
            code = Label(db, font, "", 60, FontStyle.Bold, Accent, 0.04f, 0.45f, 0.38f, 0.82f);
            title = Label(db, font, "", 32, FontStyle.Bold, Color.white, 0.38f, 0.5f, 0.97f, 0.8f);
            detail = Label(db, font, "", 20, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f), 0.04f, 0.05f, 0.96f, 0.46f);

            string[] names = { "1   F-101  MCB", "2   F-201  FUEL", "3   F-301  BATTERY", "0   clear" };
            int[] values = { 0, 1, 2, -1 };
            for (int i = 0; i < 4; i++)
            {
                float x0 = 0.025f + i * 0.1125f;
                int value = values[i];
                buttons[i] = Button(root, font, names[i], x0, 0.53f, x0 + 0.105f, 0.585f, () => trainer.ExpertDiagnose(value));
            }

            var gb = (RectTransform)Panel(root, "GuideBox", 0.025f, 0.3f, 0.47f, 0.51f, Card).transform;
            Label(gb, font, "DIAGNOSTIC GUIDE", 18, FontStyle.Bold, Muted, 0.04f, 0.82f, 0.96f, 0.96f);
            var guide = new StringBuilder();
            guide.Append("Ask the Technician to open the control-panel door and read the three warning lights ")
                 .Append("(top to bottom: MCB, FUEL, BATTERY). A red light means:\n");
            foreach (Fault f in new[] { Fault.MCB, Fault.Fuel, Fault.Battery })
                guide.Append("      ").Append(TrainingState.LampOf(f)).Append(" light red   ->   ")
                     .Append(TrainingState.CodeOf(f)).Append("  ").Append(TrainingState.TitleOf(f)).Append('\n');
            Label(gb, font, guide.ToString(), 20, FontStyle.Normal, Color.white, 0.04f, 0.04f, 0.96f, 0.82f);

            var rb = (RectTransform)Panel(root, "RunBox", 0.025f, 0.08f, 0.47f, 0.28f, Card).transform;
            Label(rb, font, "RUN", 18, FontStyle.Bold, Muted, 0.04f, 0.78f, 0.96f, 0.95f);
            run = Label(rb, font, "", 24, FontStyle.Bold, Color.white, 0.04f, 0.42f, 0.96f, 0.78f);
            lastAction = Label(rb, font, "", 21, FontStyle.Normal, Color.white, 0.04f, 0.06f, 0.96f, 0.42f);

            // Right column: manual for the chosen fault code, and that fault's repair steps in full.
            manualTitle = Label(root, font, "", 26, FontStyle.Bold, Color.white, 0.5f, 0.905f, 0.975f, 0.965f);
            var mb = (RectTransform)Panel(root, "ManualBox", 0.5f, 0.36f, 0.975f, 0.895f, Card).transform;
            manual = Label(mb, font, "", 21, FontStyle.Normal, Color.white, 0.03f, 0.02f, 0.97f, 0.98f);
            manual.lineSpacing = 1.12f;
            var cb = (RectTransform)Panel(root, "RepairBox", 0.5f, 0.08f, 0.975f, 0.34f, new Color(0.2f, 0.17f, 0.07f, 1f)).transform;
            repairTitle = Label(cb, font, "", 18, FontStyle.Bold, Accent, 0.03f, 0.84f, 0.97f, 0.96f);
            repair = Label(cb, font, "", 19, FontStyle.Normal, Color.white, 0.03f, 0.03f, 0.97f, 0.84f);

            footer = Label(root, font, "", 17, FontStyle.Normal, Muted, 0.025f, 0.015f, 0.975f, 0.06f);
        }

        static Image Panel(RectTransform parent, string name, float x0, float y0, float x1, float y1, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Label(RectTransform parent, Font font, string text, int size, FontStyle style, Color color,
            float x0, float y0, float x1, float y1)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = color; t.text = text;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        static Image Button(RectTransform parent, Font font, string text, float x0, float y0, float x1, float y1,
            UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(parent, "Button", x0, y0, x1, y1, ButtonIdle);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            var t = Label((RectTransform)img.transform, font, text, 18, FontStyle.Bold, Color.white, 0, 0, 1, 1);
            t.alignment = TextAnchor.MiddleCenter;
            return img;
        }

        // ---------- content ----------

        public void Refresh()
        {
            var s = trainer.state;
            int d = s.expertDiagnosis;
            bool chosen = d >= 0;
            var f = (Fault)Mathf.Max(0, d);

            Set(code, chosen ? TrainingState.CodeOf(f) : "?");
            Set(title, chosen ? TrainingState.TitleOf(f) : "NOT CHOSEN YET");
            Set(detail, chosen ? TrainingState.DetailOf(f)
                : "Find out which warning light is red, then choose the matching fault code below.");
            for (int i = 0; i < 4; i++)
                buttons[i].color = (i < 3 && d == i) || (i == 3 && !chosen) ? ButtonOn : ButtonIdle;

            string penalty = s.wrongCount == 0 ? "no wrong actions" : s.wrongCount + " wrong  (+" + s.penaltySeconds.ToString("0") + " s)";
            Set(run, (s.Complete ? "FINISHED   " : "Time ") + TrainingHud.Clock(s.elapsed) + "     " + penalty
                + "     Total " + TrainingHud.Clock(s.TotalTime));
            Set(lastAction, string.IsNullOrEmpty(s.lastAction) ? "Technician has not acted yet."
                : "Technician's last action:  " + Friendly(s.lastAction) + "  ->  " + ResultText(s.lastResult));

            int count = 0;
            for (int i = 0; i < TrainingState.Instructions.Length; i++) if (TrainingState.InProcedure(i, d)) count++;
            Set(manualTitle, chosen ? "MANUAL:  " + TrainingState.CodeOf(f) + "  " + TrainingState.TitleOf(f) + "  (" + count + " steps)"
                : "MANUAL:  general steps (choose a fault code for the repair)");
            Set(manual, ManualList(d));
            Set(repairTitle, chosen ? "REPAIR STEPS FOR " + TrainingState.CodeOf(f) : "REPAIR STEPS");
            Set(repair, chosen ? RepairText(d) : "Choose a fault code to see how to repair it.");
            Set(footer, "F1: Solo / Collaborative     1 / 2 / 3: choose fault code     0: clear     "
                + (onSecondDisplay ? "Expert screen is on the second monitor" : "Tab: switch between Technician view and Expert screen"));
        }

        static string ManualList(int diagnosis)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < TrainingState.Instructions.Length; i++)
            {
                if (i == 9 && diagnosis < 0)
                    sb.Append("<color=#f5c542>  ?    Repair: choose the fault code first</color>\n");
                if (!TrainingState.InProcedure(i, diagnosis)) continue;
                bool repairStep = i >= 9 && i <= 19;
                sb.Append(repairStep ? "<color=#f5c542>  " : "<color=#e6ebf0>  ")
                  .Append(TrainingState.Instructions[i]).Append("</color>\n");
            }
            return sb.ToString();
        }

        static string RepairText(int diagnosis)
        {
            var sb = new StringBuilder();
            for (int i = 9; i <= 19; i++)
                if (TrainingState.InProcedure(i, diagnosis))
                    sb.Append(TrainingState.Instructions[i].Substring(0, 2)).Append("   ").Append(TrainingState.Manual[i]).Append('\n');
            return sb.ToString();
        }

        static string Friendly(string action)
        {
            switch (action)
            {
                case "ReadPlate": return "read the instruction plate";
                case "Inspect": return "CHECK";
                case "Verify": return "checked the display";
                case "Estop": return "E-STOP";
                case "Fuel": return "diesel can";
                default: return action;
            }
        }

        static string ResultText(string result)
        {
            switch (result)
            {
                case "correct": return "correct";
                case "wrong": return "WRONG (+" + TrainingState.PenaltyPerWrong.ToString("0") + " s)";
                case "emergency": return "emergency stop";
                case "started": return "started";
                case "after_end": return "after the end";
                case "recovery": return "released (no penalty)";
                case "locked": return "ignored, control locked";
                default: return result;
            }
        }

        static void Set(Text t, string value) { if (t.text != value) t.text = value; }
    }
}
