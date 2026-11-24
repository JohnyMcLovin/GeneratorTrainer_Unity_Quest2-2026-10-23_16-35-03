using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GeneratorTrainer.Multiplayer
{
    public enum SessionState { Idle, Connecting, InSession }

    // Starts and stops the Photon Fusion connection (Shared Mode, 2 players).
    // Create: makes a random 6-character code and opens a session with that name.
    // Join:   enters the session with the typed code. Shared Mode would silently CREATE a session for an unknown
    //         code, so we check who created the room: the creator becomes the room's "master client". If joining
    //         made us the master client, no session with that code existed -> leave and report it.
    // Fusion "takes over" the already-open scene (no reload), so the generator's NetworkObject in the scene becomes
    // the shared generator. After connecting, each computer spawns its OWN player (Shared Mode).
    // Leaving or losing the connection reloads the scene, so the menu, the generator and the procedure start clean.
    public sealed class SessionLauncher : MonoBehaviour
    {
        public const int MaxPlayers = 2;
        // No look-alike characters (0/O, 1/I) and no H (H toggles the instruction window while typing).
        const string CodeAlphabet = "ABCDEFGJKLMNPQRSTUVWXYZ23456789";
        const int CodeLength = 6;

        public static SessionLauncher Instance { get; private set; }
        static string carriedMessage = "";   // survives the scene reload after leaving / failing

        [Tooltip("The DesktopPlayer prefab (has a NetworkObject).")] public NetworkObject playerPrefab;
        [Tooltip("Camera that shows the generator behind the menu until your player spawns.")] public Camera overviewCamera;
        [Tooltip("0 = session creator (Player 1), 1 = the player who joins (Player 2).")] public Transform[] spawnPoints;

        public SessionState State { get; private set; }
        public string Code { get; private set; } = "";
        public string Message { get; private set; } = "";
        public int PlayerCount { get { return runner != null && runner.IsRunning ? runner.ActivePlayers.Count() : 0; } }

        NetworkRunner runner;
        bool restarting;
        string notice = "";
        float noticeUntil;

        // Short message under the session bar, e.g. "Player 1 left the session."
        public void Notify(string text, float seconds = 5f)
        {
            notice = text ?? "";
            noticeUntil = Time.unscaledTime + seconds;
        }

        public string ActiveNotice { get { return Time.unscaledTime < noticeUntil ? notice : ""; } }

        void Awake()
        {
            Instance = this;
            // Keep simulating while the window is not focused (two game windows on one PC).
            Application.runInBackground = true;
            Message = carriedMessage;
            carriedMessage = "";

            // Milestone 5 (multiplayer scene only): the procedure is shown on Player 1's tablet instead of the old
            // top-left window, and the training board on the generator no longer shows the current step.
            // Its PPE / CHECK / RESET buttons and feedback messages stay for both players.
            TrainingHud.AutoCreate = false;
            var trainer = FindAnyObjectByType<GeneratorTraining>();
            if (trainer) trainer.boardStepText = BoardText;
        }

        public const string BoardText = "Follow Player 1's instructions";

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TrainingHud.AutoCreate = true;   // other scenes keep their instruction window
        }

        void Update()
        {
            // Connection lost or Photon shut the runner down.
            if (State == SessionState.InSession && !restarting && (runner == null || !runner.IsRunning))
                Restart("Disconnected from the session.");
        }

        public async void CreateSession()
        {
            if (State != SessionState.Idle) return;
            string code = NewCode();
            var result = await StartRunner(code, "Creating session...");
            if (!result.Ok) { await StopAndRestart("Could not create the session: " + Describe(result)); return; }
            // Extremely unlikely: that code is already in use by another session.
            if (!runner.IsSharedModeMasterClient) { await StopAndRestart("Could not create a session. Please try again."); return; }
            EnterSession(code);
        }

        public async void JoinSession(string typedCode)
        {
            if (State != SessionState.Idle) return;
            string code = Normalize(typedCode);
            if (code.Length == 0) { Message = "Type the session code first."; return; }
            var result = await StartRunner(code, "Joining session " + code + "...");
            if (!result.Ok)
            {
                await StopAndRestart(result.ShutdownReason == ShutdownReason.GameIsFull
                    ? "Session " + code + " already has " + MaxPlayers + " players."
                    : "Could not join: " + Describe(result));
                return;
            }
            if (runner.IsSharedModeMasterClient) { await StopAndRestart("No session with code " + code + ". Check the code and try again."); return; }
            EnterSession(code);
        }

        public async void LeaveSession()
        {
            if (State != SessionState.InSession) return;
            await StopAndRestart("You left the session.");
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
            var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();   // takes over the open scene
            // Same as Fusion's own FusionBootstrap: the already-loaded scene is registered (Additive + take-over),
            // so it is not reloaded and its NetworkObjects (the generator) join the session.
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), LoadSceneMode.Additive);
            return await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = code,
                PlayerCount = MaxPlayers,
                ObjectProvider = provider,
                SceneManager = sceneManager,
                Scene = sceneInfo,
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

        async Task StopAndRestart(string message)
        {
            if (restarting) return;
            restarting = true;
            var r = runner;
            runner = null;
            if (r != null)
            {
                if (r.IsRunning) await r.Shutdown();
                if (r) Destroy(r.gameObject);
            }
            Restart(message);
        }

        // Reload the scene: fresh menu, fresh generator and procedure, no leftovers from the network session.
        void Restart(string message)
        {
            restarting = true;
            carriedMessage = message;
            if (runner != null) { Destroy(runner.gameObject); runner = null; }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
