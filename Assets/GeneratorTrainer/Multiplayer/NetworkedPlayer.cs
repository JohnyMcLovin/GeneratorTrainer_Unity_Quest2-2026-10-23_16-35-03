using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Network side of the DesktopPlayer prefab (Photon Fusion, Shared Mode).
    // The computer that spawned this player has "state authority" over it: it enables the camera, controls and
    // crosshair, and hides its own avatar. On the other computer the same object is a remote copy: controls and
    // camera stay off and the avatar is shown. Position and facing are synced by the NetworkTransform on the
    // same object; this script syncs head tilt (looking up/down), the player number and the role.
    //
    // Numbering rule - "someone always holds the instructions":
    // - The session creator starts as Player 1 (Instructions), whoever joins as Player 2 (Operator).
    // - A player who is alone in the session (for longer than AloneGraceSeconds) becomes Player 1, so when Player 1
    //   leaves, the remaining player takes over the instructions. Someone who joins later is never alone -> Player 2.
    // - Safety net: if both ever have the same number, the room's master client takes 1 and the other takes 2.
    // The tablet follows the role: created on this computer when its own player holds the instructions.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedPlayer : NetworkBehaviour
    {
        const float AloneGraceSeconds = 0.75f;   // ignore the moment right after joining, before the other player appears

        [Networked] public int Number { get; set; }     // 1 = holds the instructions, 2 = operator
        [Networked] public int Role { get; set; }       // (int)PlayerRole
        [Networked] public float Pitch { get; set; }    // degrees, + = looking down
        // PPE for step A1 (taken from the PPE table). Synced so the table, the avatar and the A1 check agree everywhere.
        [Networked] public NetworkBool WearsGlasses { get; set; }
        [Networked] public NetworkBool WearsEarProtection { get; set; }

        public static NetworkedPlayer Local { get; private set; }
        static readonly List<NetworkedPlayer> All = new List<NetworkedPlayer>();
        public static IReadOnlyList<NetworkedPlayer> Players { get { return All; } }

        public bool Wears(PPEKind kind)
        {
            if (Object == null || !Object.IsValid) return false;
            return kind == PPEKind.Glasses ? WearsGlasses : WearsEarProtection;
        }
        public bool FullyEquipped { get { return Wears(PPEKind.Glasses) && Wears(PPEKind.EarProtection); } }

        // Called on the owner's computer (PPE table). Applied in the next network tick.
        public void PutOn(PPEKind kind)
        {
            if (kind == PPEKind.Glasses) wantGlasses = true; else wantEarProtection = true;
        }

        bool wantGlasses, wantEarProtection;
        float lastRunTime = -1f;
        bool shownGlasses, shownEars, ppeShownOnce;
        GeneratorTraining trainer;

        public PlayerAvatar avatar;
        [Tooltip("Head transform: the camera on desktop, the head pivot on VR. Remote copies tilt it by Pitch.")]
        public Transform head;

        [Header("Desktop player only (empty on the VR player)")]
        public DesktopPlayerController controller;
        public DesktopInteractor interactor;
        public Crosshair crosshair;
        public Camera view;
        public AudioListener listener;
        public CharacterController body;

        [Header("VR player only (empty on the desktop player)")]
        public VRPresence vr;

        public bool IsVR { get { return vr != null; } }

        int shownNumber = -1, shownRole = -1;
        float aloneSince = -1f;

        public override void Spawned()
        {
            All.Add(this);
            bool local = HasStateAuthority;
            if (local)
            {
                Local = this;
                Runner.SetPlayerObject(Runner.LocalPlayer, Object);
                SetNumber(Runner.IsSharedModeMasterClient ? 1 : 2);
            }

            // The prefab starts with all of these switched off; only the owner turns them on.
            if (body) body.enabled = local;
            if (controller) controller.enabled = local;
            if (interactor) interactor.enabled = local;
            if (crosshair) crosshair.enabled = local;
            if (view) view.enabled = local;
            if (listener) listener.enabled = local;
            if (vr) vr.isLocal = local;   // a VR presence follows the local VR rig
            avatar.SetLocal(local);

            if (local && SessionLauncher.Instance) SessionLauncher.Instance.ShowOverview(false);
            ApplyIdentity();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            if (Local == this) { Local = null; AudioListener.volume = 1f; }
            // The other player's avatar disappears when they leave.
            else if (Local != null && shownNumber > 0 && SessionLauncher.Instance && SessionLauncher.Instance.State == SessionState.InSession)
                SessionLauncher.Instance.Notify("Player " + shownNumber + " left the session.");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            Pitch = vr ? vr.HeadPitch : controller ? controller.Pitch : 0f;
            UpdatePPE();

            int desired = DesiredNumber();
            if (desired != Number)
            {
                bool promoted = desired == 1;
                SetNumber(desired);
                if (promoted && SessionLauncher.Instance)
                    SessionLauncher.Instance.Notify("You are now Player 1 and hold the instructions (tablet: Tab).");
            }
        }

        // Owner only: apply "put on" requests, and take everything off when a new run starts (RESET or a new
        // session) - the run timer only ever counts up within a run, so a drop means a fresh run.
        void UpdatePPE()
        {
            if (trainer == null) trainer = FindAnyObjectByType<GeneratorTraining>();
            if (trainer != null)
            {
                float t = trainer.state.elapsed;
                if (lastRunTime >= 0f && t + 0.5f < lastRunTime)
                {
                    WearsGlasses = false;
                    WearsEarProtection = false;
                    wantGlasses = wantEarProtection = false;
                }
                lastRunTime = t;
            }
            if (wantGlasses) { WearsGlasses = true; wantGlasses = false; }
            if (wantEarProtection) { WearsEarProtection = true; wantEarProtection = false; }
        }

        int DesiredNumber()
        {
            bool alone = Runner.ActivePlayers.Count() <= 1;
            if (alone)
            {
                if (aloneSince < 0f) aloneSince = Time.unscaledTime;
                if (Time.unscaledTime - aloneSince >= AloneGraceSeconds) return 1;
                return Number;
            }
            aloneSince = -1f;

            NetworkedPlayer other = null;
            foreach (var p in All) if (p != this && p.Object != null && p.Object.IsValid) other = p;
            if (other == null || other.Number == 0) return Number;   // the other player's object has not arrived yet
            if (other.Number == Number) return Runner.IsSharedModeMasterClient ? 1 : 2;
            return Number;
        }

        void SetNumber(int number)
        {
            Number = number;
            Role = (int)(number == 1 ? PlayerRole.Instructions : PlayerRole.Operator);
        }

        public override void Render()
        {
            // Remote copy: tilt the head the way its owner is looking (the avatar's head follows this transform).
            Transform h = head ? head : controller ? controller.head : null;
            if (!HasStateAuthority && h)
                h.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
            ApplyIdentity();
            ApplyPPE();
        }

        // Avatar shows the worn PPE (for the other player); the owner hears everything quieter with ear defenders.
        void ApplyPPE()
        {
            bool g = WearsGlasses, e = WearsEarProtection;
            if (ppeShownOnce && g == shownGlasses && e == shownEars) return;
            ppeShownOnce = true;
            shownGlasses = g;
            shownEars = e;
            avatar.SetPPE(g, e);
            if (HasStateAuthority) AudioListener.volume = e ? 0.35f : 1f;
        }

        void ApplyIdentity()
        {
            if (Number == shownNumber && Role == shownRole) return;
            shownNumber = Number;
            shownRole = Role;
            if (Number <= 0) return;
            avatar.SetRole((PlayerRole)Role, Number);
            gameObject.name = "Player " + Number + (HasStateAuthority ? " (you)" : "");

            // Milestone 5: the tablet exists only on the computer of the player holding the instructions.
            // Desktop: on the camera. VR: in the left hand (the local VR rig's left controller).
            if (HasStateAuthority)
            {
                Transform anchor = IsVR ? (VRRig.Local != null ? VRRig.Local.leftHand : null) : (view ? view.transform : null);
                if (anchor == null) return;
                var tablet = anchor.GetComponent<InstructionTablet>();
                bool wantTablet = (PlayerRole)Role == PlayerRole.Instructions;
                if (wantTablet && tablet == null)
                {
                    tablet = anchor.gameObject.AddComponent<InstructionTablet>();
                    if (IsVR) tablet.UseVRPose();   // before its Start runs (next frame)
                }
                else if (!wantTablet && tablet != null) Destroy(tablet);
            }
        }
    }
}
