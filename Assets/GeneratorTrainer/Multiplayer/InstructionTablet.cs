using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GeneratorTrainer.Multiplayer
{
    // Player 1's instruction tablet: a small device held in the lower right of the view.
    // Tab raises it to the centre for reading and lowers it again. It shows the current step, the tip, the
    // generator's latest message, a progress bar while fuelling / charging, and time + penalties - all read from the
    // shared TrainingState, so it updates whichever player performs a step.
    //
    // Built from Unity UI in code (no imported model). Add it to any transform to attach it there: on desktop the
    // player's camera; in VR (Milestone 7) it can go on the left controller instead.
    // Only the computer of the player holding the instructions creates it, so the other player can never see it.
    public sealed class InstructionTablet : MonoBehaviour
    {
        public GeneratorTraining trainer;
        [Tooltip("Read the Tab key to raise/lower the tablet (desktop).")] public bool desktopToggle = true;

        // Pose relative to the attach point (camera), lowered and raised.
        public Vector3 loweredPosition = new Vector3(0.17f, -0.15f, 0.42f);
        public Vector3 loweredRotation = new Vector3(14f, 14f, 0f);
        public Vector3 raisedPosition = new Vector3(0f, -0.035f, 0.36f);
        public Vector3 raisedRotation = Vector3.zero;
        public float moveSpeed = 10f;

        const float Width = 640, Height = 440, PixelSize = 0.0004f;   // 640 px = 0.256 m wide

        public bool Raised { get; private set; }

        // VR (Milestone 7): held in the left hand; the Y button hides / shows it instead of raising it.
        [System.NonSerialized] public bool vrMode;
        UnityEngine.InputSystem.InputAction vrToggle;

        // Pose on the left controller: a little above the hand, tilted back towards the eyes like a held tablet.
        public static readonly Vector3 VRPosition = new Vector3(0f, 0.07f, 0.06f);
        public static readonly Vector3 VRRotation = new Vector3(40f, 0f, 0f);

        // Call before Start when attaching to a VR controller.
        public void UseVRPose()
        {
            vrMode = true;
            desktopToggle = false;
            loweredPosition = raisedPosition = VRPosition;
            loweredRotation = raisedRotation = VRRotation;
        }

        Transform tablet;
        Material uiMaterial;
        Text header, instruction, tip, message, progressLabel, status;
        RectTransform progressRow, progressFill;

        void Start()
        {
            if (trainer == null) trainer = FindAnyObjectByType<GeneratorTraining>();
            Build();
            tablet.localPosition = loweredPosition;
            tablet.localRotation = Quaternion.Euler(loweredRotation);
            if (vrMode)
            {
                vrToggle = new UnityEngine.InputSystem.InputAction("TabletToggle", binding: "<XRController>{LeftHand}/secondaryButton");
                vrToggle.Enable();
            }
        }

        void Update()
        {
            if (desktopToggle && DesktopInput.TabPressed) Raised = !Raised;
            if (vrToggle != null && vrToggle.WasPressedThisFrame()) tablet.gameObject.SetActive(!tablet.gameObject.activeSelf);
            float k = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
            tablet.localPosition = Vector3.Lerp(tablet.localPosition, Raised ? raisedPosition : loweredPosition, k);
            tablet.localRotation = Quaternion.Slerp(tablet.localRotation, Quaternion.Euler(Raised ? raisedRotation : loweredRotation), k);
            if (trainer != null) Refresh(trainer.state);
        }

        public void SetRaised(bool raised) { Raised = raised; }

        void Refresh(TrainingState s)
        {
            Set(header, s.Complete ? "ALL STEPS DONE" : "STEP " + s.Position + " / " + s.RequiredCount + "   |   " + TrainingHud.Phase(s.Current));
            Set(instruction, s.Current);
            string t = s.Complete ? TrainingHud.DoneTip : (s.step < TrainingHud.Tips.Length ? TrainingHud.Tips[s.step] : "");
            Set(tip, t.Length > 0 ? "Tip: " + t : "");
            Set(message, s.feedback);

            // Live progress for the two timed steps (same durations as TrainingState.Tick: 5 s fuel, 8 s charge).
            float progress = -1f; string what = "";
            if (s.pouring && s.step == 15) { progress = s.fuelTime / 5f; what = "Fuelling"; }
            else if (s.chargerConnected && s.step == 18) { progress = s.chargeTime / 8f; what = "Charging battery"; }
            bool showBar = progress >= 0f;
            if (progressRow.gameObject.activeSelf != showBar) progressRow.gameObject.SetActive(showBar);
            if (showBar)
            {
                progress = Mathf.Clamp01(progress);
                progressFill.anchorMax = new Vector2(progress, 1f);
                Set(progressLabel, what + "  " + Mathf.RoundToInt(progress * 100) + "%");
            }

            string penalty = s.wrongCount == 0 ? "no wrong actions" : s.wrongCount + " wrong (+" + s.penaltySeconds.ToString("0") + " s)";
            Set(status, s.Complete
                ? "Finished: " + TrainingHud.Clock(s.elapsed) + " + " + s.penaltySeconds.ToString("0") + " s penalty = " + TrainingHud.Clock(s.TotalTime)
                : "Time " + TrainingHud.Clock(s.elapsed) + "   |   " + penalty + "   |   Total " + TrainingHud.Clock(s.TotalTime)
                  + (vrMode ? "        Y: hide" : "        Tab: " + (Raised ? "lower" : "raise")));
        }

        static void Set(Text t, string value) { if (t.text != value) t.text = value; }

        // ---------------- construction ----------------

        void Build()
        {
            // Drawn on top of the scene so the tablet never disappears inside the generator when standing close.
            uiMaterial = new Material(Graphic.defaultGraphicMaterial);
            uiMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("InstructionTablet", typeof(RectTransform));
            tablet = root.transform;
            tablet.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;   // sharper text in world space
            var rootRect = (RectTransform)tablet;
            rootRect.sizeDelta = new Vector2(Width, Height);
            tablet.localScale = Vector3.one * PixelSize;

            // Bezel (device body), screen, and a small home button.
            var bezel = Box("Bezel", rootRect, new Color(0.07f, 0.075f, 0.085f, 1f));
            Stretch(bezel, 0, 0, 0, 0);
            var screen = Box("Screen", bezel, new Color(0.05f, 0.13f, 0.18f, 1f));
            Stretch(screen, 22, 22, 22, 46);
            var home = Box("HomeButton", bezel, new Color(0.22f, 0.24f, 0.27f, 1f));
            home.anchorMin = home.anchorMax = new Vector2(0.5f, 0f);
            home.sizeDelta = new Vector2(56, 14);
            home.anchoredPosition = new Vector2(0, 23);

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(screen, false);
            Stretch(content, 22, 22, 16, 14);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            header = Line(content, font, 20, FontStyle.Bold, new Color(0.55f, 0.75f, 0.85f));
            instruction = Line(content, font, 34, FontStyle.Bold, Color.white);
            tip = Line(content, font, 21, FontStyle.Normal, new Color(1f, 0.84f, 0.4f));
            message = Line(content, font, 21, FontStyle.Normal, new Color(0.62f, 0.9f, 1f));

            progressRow = new GameObject("Progress", typeof(RectTransform)).GetComponent<RectTransform>();
            progressRow.SetParent(content, false);
            progressRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var barBack = Box("BarBack", progressRow, new Color(0.12f, 0.2f, 0.25f, 1f));
            Stretch(barBack, 0, 0, 0, 0);
            progressFill = Box("BarFill", barBack, new Color(0.15f, 0.8f, 0.35f, 1f));
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            progressLabel = Line(progressRow, font, 19, FontStyle.Bold, Color.white);
            progressLabel.alignment = TextAnchor.MiddleCenter;
            Stretch(progressLabel.rectTransform, 0, 0, 0, 0);
            progressRow.gameObject.SetActive(false);

            status = Line(content, font, 16, FontStyle.Normal, new Color(0.6f, 0.68f, 0.74f));
        }

        RectTransform Box(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.material = uiMaterial;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        Text Line(RectTransform parent, Font font, int size, FontStyle style, Color color)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.material = uiMaterial;
            t.lineSpacing = 1.05f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform r, float left, float right, float top, float bottom)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        void OnDestroy()
        {
            if (vrToggle != null) { vrToggle.Disable(); vrToggle.Dispose(); }
            if (uiMaterial) Destroy(uiMaterial);
            if (tablet) Destroy(tablet.gameObject);
        }
    }
}
