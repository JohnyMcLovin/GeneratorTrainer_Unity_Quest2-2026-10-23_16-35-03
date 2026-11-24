using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace GeneratorTrainer
{
    // Panel that keeps the current task, the system feedback and a tip in view.
    // It creates itself in any scene that contains a GeneratorTraining, so no scene has to be edited.
    // Desktop / XR Device Simulator: pinned to the top-left corner of the screen.
    // Headset: a world-space panel that smoothly follows the head (below eye level), because
    // text locked rigidly to the face is uncomfortable in VR.
    // Press H (keyboard) or Y (left Quest controller) to hide or show it.
    // Set TrainingHud.AutoCreate = false (e.g. for the Technician in the two-player mode) to disable it.
    public sealed class TrainingHud : MonoBehaviour
    {
        public static bool AutoCreate = true;

        public GeneratorTraining trainer;
        public bool showTips = true;
        [Tooltip("Metres in front of the head (headset mode).")] public float distance = 1.1f;
        [Tooltip("Metres below eye level (headset mode).")] public float below = 0.28f;
        [Tooltip("Higher = follows the head faster (headset mode).")] public float followSpeed = 3f;

        static readonly string[] Tips =
        {
            "Select the PPE button on the training board to the left of the generator.",                  // A1
            "The exhaust pipe is on top of the generator. Select it to turn it toward the outdoor vent.",  // A2
            "The red E-STOP is on the front, to the right of the control panel. Select it to release it.", // A3
            "Select the door handle to open the control-panel door.",                                    // A4
            "The instruction plate is on the inside of the open door. Select it after reading.",          // A5
            "Look at the MCB, FUEL and BATTERY lamps on the panel, then select CHECK on the board.",      // A6
            "Select the key on the low shelf next to the panel, or the INSERT KEY socket.",               // B1
            "Select the round MAN / OFF / AUTO knob to turn it to MAN.",                                  // B2
            "Select the red STOP button on the panel to pull it out.",                                    // B3
            "Select the MCB COVER below the START and STOP buttons to lift it.",                          // C1
            "Look at the four breakers: the tripped one points down. Then select CHECK.",                 // C2
            "Select the breaker that points down to flip it ON. The others are already ON.",              // C3
            "Select the cover again to close it. The MCB lamp turns green.",                              // C4
            "Go to the fuel filler at the right end (DIESEL ONLY / NO SMOKING sign), then select CHECK.",  // D1
            "Select the filler cap to remove it.",                                                        // D2
            "Select the DIESEL can next to the filler. Filling takes about 5 seconds.",                   // D3
            "Select the cap again to put it back. The FUEL lamp turns green.",                            // D4
            "Select the charger plug on the low shelf, or the BAT. CHARGE socket on the panel.",          // E1
            "Nothing to press: charging takes about 8 seconds. Watch the BATTERY lamp.",                  // E2
            "Select the plug or the socket again to disconnect the charger.",                             // E3
            "All three lamps and the small lamp next to START must be green. Then select CHECK.",         // F1
            "Select the green START button.",                                                             // F2
            "Look at the display and listen to the engine, then select the display.",                     // F3
            "Select the red STOP button on the panel.",                                                   // G1
            "Select the mode knob to turn it to OFF.",                                                    // G2
            "Select the key in the socket to remove it.",                                                 // G3
            "Select the door handle to close the door.",                                                  // G4
        };
        const string DoneTip = "Exercise complete. Select RESET on the training board to start again.";

        Canvas canvas;
        RectTransform root, panel;
        Text header, task, feedback, tip, footer;
        Camera cam;
        bool worldSpace, visible = true, yWasDown, snap = true;
        static bool subscribed;

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
            if (t == null || FindAnyObjectByType<TrainingHud>() != null) return;
            new GameObject("TrainingHud").AddComponent<TrainingHud>().trainer = t;
        }

        void Start()
        {
            if (trainer == null) trainer = FindAnyObjectByType<GeneratorTraining>();
            Build();
            SetMode(XRSettings.isDeviceActive);
        }

        void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Drawn on top of the generator geometry so the panel is never hidden inside the model.
            var mat = new Material(Graphic.defaultGraphicMaterial);
            mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);

            canvas = gameObject.AddComponent<Canvas>();
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)transform;

            panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root, false);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.11f, 0.82f);
            bg.material = mat;
            bg.raycastTarget = false;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 16, 16);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.sizeDelta = new Vector2(560, 0);

            header = Line(font, mat, 20, FontStyle.Bold, new Color(0.62f, 0.69f, 0.76f));
            task = Line(font, mat, 32, FontStyle.Bold, Color.white);
            feedback = Line(font, mat, 22, FontStyle.Normal, new Color(0.62f, 0.86f, 1f));
            tip = Line(font, mat, 22, FontStyle.Normal, new Color(1f, 0.84f, 0.4f));
            footer = Line(font, mat, 16, FontStyle.Normal, new Color(0.55f, 0.6f, 0.66f));
            footer.text = "Select = point and pull the trigger (or click)   |   Y / H: hide";
        }

        Text Line(Font font, Material mat, int size, FontStyle style, Color color)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(panel, false);
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.material = mat;
            t.lineSpacing = 1.05f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void SetMode(bool xr)
        {
            worldSpace = xr;
            if (xr)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                root.sizeDelta = new Vector2(560, 10);
                root.localScale = Vector3.one * 0.0008f; // 560 px = 0.45 m wide
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                snap = true;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                root.localScale = Vector3.one;
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
                panel.anchoredPosition = new Vector2(28, -28);
            }
        }

        void LateUpdate()
        {
            if (trainer == null) { canvas.enabled = false; return; }
            HandleToggle();
            canvas.enabled = visible;
            if (!visible) return;

            if (XRSettings.isDeviceActive != worldSpace) SetMode(XRSettings.isDeviceActive);
            Refresh();
            if (worldSpace) Follow();
        }

        void HandleToggle()
        {
            bool toggle = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            toggle |= Input.GetKeyDown(KeyCode.H);
#endif
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool y = left.isValid && left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool down) && down;
            if (y && !yWasDown) toggle = true;
            yWasDown = y;
            if (toggle) { visible = !visible; snap = true; }
        }

        void Refresh()
        {
            var s = trainer.state;
            int total = TrainingState.Instructions.Length;
            string h = s.Complete ? "ALL STEPS DONE" : "STEP " + (s.step + 1) + " / " + total + "   |   " + Phase(s.Current);
            Set(header, h);
            Set(task, s.Current);
            Set(feedback, s.feedback);
            string t = s.Complete ? DoneTip : (s.step < Tips.Length ? Tips[s.step] : "");
            Set(tip, showTips && t.Length > 0 ? "Tip: " + t : "");
            tip.gameObject.SetActive(showTips && t.Length > 0);
        }

        static void Set(Text t, string value) { if (t.text != value) t.text = value; }

        static string Phase(string current)
        {
            switch (current.Length > 0 ? current[0] : ' ')
            {
                case 'A': return "SAFETY CHECKS";
                case 'B': return "ACTIVATE";
                case 'C': return "MCB FAULT";
                case 'D': return "FUEL FAULT";
                case 'E': return "BATTERY FAULT";
                case 'F': return "START";
                case 'G': return "SHUTDOWN";
                default: return "";
            }
        }

        void Follow()
        {
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam == null) return;
            var head = cam.transform;
            Vector3 fwd = head.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) fwd = head.up;
            fwd.y = 0;
            fwd.Normalize();

            Vector3 target = head.position + fwd * distance + Vector3.down * below;
            float k = snap ? 1f : 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
            Vector3 away = transform.position - head.position;
            if (away.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away, Vector3.up), k);
            snap = false;
        }
    }
}
