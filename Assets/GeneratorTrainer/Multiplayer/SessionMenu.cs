using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Simple start screen (Unity IMGUI): Create session, or type a code and Join.
    // While in a session: a small bar at the top shows the code and the player count; press Esc to free the
    // mouse and a "Leave session" button appears.
    public sealed class SessionMenu : MonoBehaviour
    {
        public SessionLauncher launcher;
        string joinCode = "";
        GUIStyle title, text, button, field, bar, noticeStyle;

        void Awake() { if (launcher == null) launcher = GetComponent<SessionLauncher>(); }

        void OnGUI()
        {
            if (launcher == null) return;
            Styles();
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;

            if (launcher.State == SessionState.InSession) { SessionBar(w); return; }
            leaveButtonScreen = Rect.zero;

            var panel = new Rect(w / 2 - 260, h / 2 - 190, 520, 380);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 30, panel.y + 24, panel.width - 60, panel.height - 48));
            GUILayout.Label("Generator Repair - Multiplayer", title);
            GUILayout.Space(16);

            if (launcher.State == SessionState.Connecting)
            {
                GUILayout.Label(launcher.Message, text);
            }
            else
            {
                if (GUILayout.Button("Create session", button, GUILayout.Height(52))) launcher.CreateSession();
                GUILayout.Space(18);
                GUILayout.Label("or join with a code:", text);
                GUILayout.BeginHorizontal();
                GUI.SetNextControlName("JoinCode");
                joinCode = SessionLauncher.Normalize(GUILayout.TextField(joinCode, 8, field, GUILayout.Height(48), GUILayout.Width(250)));
                bool enter = Event.current.type == EventType.KeyDown &&
                             (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                             GUI.GetNameOfFocusedControl() == "JoinCode";
                if (GUILayout.Button("Join", button, GUILayout.Height(48)) || enter) launcher.JoinSession(joinCode);
                GUILayout.EndHorizontal();
                GUILayout.Space(14);
                if (!string.IsNullOrEmpty(launcher.Message)) GUILayout.Label(launcher.Message, text);
            }
            GUILayout.EndArea();
        }

        void SessionBar(float w)
        {
            int players = launcher.PlayerCount;
            string status = "Session code: " + launcher.Code + "     Players " + players + "/" + SessionLauncher.MaxPlayers +
                            (players < SessionLauncher.MaxPlayers ? "     waiting for Player 2..." : "");
            GUI.Label(new Rect(w / 2 - 400, 12, 800, 40), status, bar);
            string notice = launcher.ActiveNotice;
            if (notice.Length > 0) GUI.Label(new Rect(w / 2 - 400, 58, 800, 40), notice, noticeStyle);
            if (!DesktopPlayerController.CursorLocked)
            {
                var leave = new Rect(w / 2 - 110, 106, 220, 44);
                float s = Screen.height / 1080f;
                leaveButtonScreen = new Rect(leave.x * s, leave.y * s, leave.width * s, leave.height * s);
                if (GUI.Button(leave, "Leave session", button)) launcher.LeaveSession();
            }
            else leaveButtonScreen = Rect.zero;
        }

        // Screen area of the Leave button while it is shown (GUI pixels, origin top-left).
        static Rect leaveButtonScreen;

        // True if a click at this position is meant for an on-screen button, so it must not re-capture the mouse.
        public static bool IsOverButton(Vector2 guiPosition)
        {
            return leaveButtonScreen.width > 0 && leaveButtonScreen.Contains(guiPosition);
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            text = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            bar = new GUIStyle(GUI.skin.box) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bar.normal.textColor = Color.white;
            noticeStyle = new GUIStyle(bar) { fontSize = 20 };
            noticeStyle.normal.textColor = new Color(1f, 0.84f, 0.4f);
        }
    }
}
