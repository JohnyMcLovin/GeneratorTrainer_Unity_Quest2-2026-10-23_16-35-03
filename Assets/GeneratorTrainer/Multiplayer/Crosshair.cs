using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Screen-centre dot. It turns yellow over a generator control and shows the control's name.
    // While the cursor is free (after Esc) it shows how to get back into the game instead.
    public sealed class Crosshair : MonoBehaviour
    {
        public DesktopInteractor interactor;
        public Color idle = new Color(1f, 1f, 1f, 0.75f);
        public Color active = new Color(1f, 0.84f, 0.2f, 1f);
        [Tooltip("Colour when the other player is operating the control (component lock).")]
        public Color busy = new Color(1f, 0.25f, 0.2f, 1f);

        GUIStyle label, hint;

        void Awake() { if (interactor == null) interactor = GetComponent<DesktopInteractor>(); }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
                hint = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            }
            float scale = Screen.height / 1080f;
            label.fontSize = Mathf.RoundToInt(22 * scale);
            hint.fontSize = Mathf.RoundToInt(24 * scale);
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);

            if (!DesktopPlayerController.CursorLocked)
            {
                Shadowed(new Rect(c.x - 400, c.y - 30, 800, 60), "Click to look around   |   Esc frees the mouse", hint, Color.white);
                return;
            }

            TrainerControl hovered = interactor != null ? interactor.Hovered : null;
            int lockedBy = interactor != null ? interactor.HoveredLockedBy : 0;
            Color colour = hovered ? (lockedBy != 0 ? busy : active) : idle;
            float size = Mathf.Max(4f, (hovered ? 10f : 6f) * scale);
            Color old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(c.x - size / 2 - 1, c.y - size / 2 - 1, size + 2, size + 2), Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(c.x - size / 2, c.y - size / 2, size, size), Texture2D.whiteTexture);
            GUI.color = old;

            if (hovered)
            {
                string text = ControlLabels.For(hovered) + (lockedBy != 0 ? "  -  In use by Player " + lockedBy : "");
                Shadowed(new Rect(c.x - 300, c.y + 14 * scale, 600, 40 * scale), text, label, colour);
            }
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }
    }
}
