using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    public enum SessionState { Idle, Connecting, InSession }

    // Starts and stops the Photon Fusion connection (Shared Mode, 2 players).
    // Create: makes a random 6-character code and opens a session with that name.
    // Join:   enters the session with the typed code. Shared Mode would silently CREATE a session for an unknown
    //         code, so we check who created the room: the creator becomes the room's "master client". If joining
    //         made us the master client, no session with that code existed -> leave and report it.
    // After connecting, each computer spawns its OWN player (Shared Mode: every client owns its player object).
    public sealed class SessionLauncher : MonoBehaviour
    {
        public const int MaxPlayers = 2;
        // No look-alike characters (0/O, 1/I) and no H (H toggles the instruction window while typing).
        const string CodeAlphabet = "ABCDEFGJKLMNPQRSTUVWXYZ23456789";
        const int CodeLength = 6;

        public static SessionLauncher Instance { get; private set; }

        [Tooltip("The DesktopPlayer prefab (has a NetworkObject).")] public NetworkObject playerPrefab;
        [Tooltip("Camera that shows the generator behind the menu until your player spawns.")] public Camera overviewCamera;
        [Tooltip("0 = session creator (Player 1), 1 = the player who joins (Player 2).")] public Transform[] spawnPoints;

        public SessionState State { get; private set; }
        public string Code { get; private set; } = "";
        public string Message { get; private set; } = "";
        public int PlayerCount { get { return runner != null && runner.IsRunning ? runner.ActivePlayers.Count() : 0; } }

        NetworkRunner runner;

        void Awake()
        {
            Instance = this;
            // Keep simulating while the window is not focused (two game windows on one PC).
            Application.runInBackground = true;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            // Connection lost or Photon shut the runner down.
            if (State == SessionState.InSession && (runner == null || !runner.IsRunning))
            {
                Cleanup();
                Message = "Disconnected from the session.";
            }
        }

        public async void CreateSession()
        {
            if (State != SessionState.Idle) return;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                string code = NewCode();
                var result = await StartRunner(code, "Creating session...");
                if (!result.Ok) { await Fail("Could not create the session: " + Describe(result)); return; }
                if (runner.IsSharedModeMasterClient) { EnterSession(code); return; }
                await StopRunner();   // extremely unlikely: that code is already in use, try another
            }
            await Fail("Could not create a session. Please try again.");
        }

        public async void JoinSession(string typedCode)
        {
            if (State != SessionState.Idle) return;
            string code = Normalize(typedCode);
            if (code.Length == 0) { Message = "Type the session code first."; return; }
            var result = await StartRunner(code, "Joining session " + code + "...");
            if (!result.Ok)
            {
                await Fail(result.ShutdownReason == ShutdownReason.GameIsFull
                    ? "Session " + code + " already has " + MaxPlayers + " players."
                    : "Could not join: " + Describe(result));
                return;
            }
            if (runner.IsSharedModeMasterClient) { await Fail("No session with code " + code + ". Check the code and try again."); return; }
            EnterSession(code);
        }

        public async void LeaveSession()
        {
            if (State != SessionState.InSession) return;
            await StopRunner();
            Message = "You left the session.";
        }

        // Called by the local NetworkedPlayer when it has spawned (its own camera takes over).
        public void ShowOverview(bool show)
        {
            if (overviewCamera) overviewCamera.gameObject.SetActive(show);
        }

        async Task<StartGameResult> StartRunner(string code, string status)
        {
            State = SessionState.Connecting;
            Message = status;
            Code = code;
            var go = new GameObject("NetworkRunner");
            runner = go.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;   // Shared Mode: each client moves its own player directly
            var provider = go.AddComponent<NetworkObjectProviderDefault>();
            return await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = code,
                PlayerCount = MaxPlayers,
                ObjectProvider = provider,
            });
        }

        void EnterSession(string code)
        {
            State = SessionState.InSession;
            Code = code;
            Message = "";
            int slot = runner.IsSharedModeMasterClient ? 0 : 1;
            Transform point = spawnPoints != null && spawnPoints.Length > slot ? spawnPoints[slot] : null;
            Vector3 position = point ? point.position : Vector3.zero;
            Quaternion rotation = point ? point.rotation : Quaternion.identity;
            runner.Spawn(playerPrefab, position, rotation, runner.LocalPlayer);
        }

        async Task Fail(string message)
        {
            await StopRunner();
            Message = message;
        }

        async Task StopRunner()
        {
            var r = runner;
            runner = null;
            if (r != null)
            {
                if (r.IsRunning) await r.Shutdown();
                if (r) Destroy(r.gameObject);
            }
            Cleanup();
        }

        void Cleanup()
        {
            if (runner != null) { Destroy(runner.gameObject); runner = null; }
            State = SessionState.Idle;
            Code = "";
            ShowOverview(true);
        }

        static string Describe(StartGameResult result)
        {
            return string.IsNullOrEmpty(result.ErrorMessage) ? result.ShutdownReason.ToString()
                : result.ShutdownReason + " (" + result.ErrorMessage + ")";
        }

        static string NewCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++) chars[i] = CodeAlphabet[Random.Range(0, CodeAlphabet.Length)];
            return new string(chars);
        }

        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var clean = new System.Text.StringBuilder();
            foreach (char c in code.ToUpperInvariant()) if (char.IsLetterOrDigit(c)) clean.Append(c);
            return clean.ToString();
        }
    }
}
