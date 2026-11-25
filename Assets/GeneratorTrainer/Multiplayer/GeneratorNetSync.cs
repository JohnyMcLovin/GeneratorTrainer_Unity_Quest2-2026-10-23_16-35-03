using Fusion;
using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Makes the existing generator ONE shared object for both players (Photon Fusion, Shared Mode).
    //
    // - The computer with state authority over the generator (the session creator, Player 1) is the only one that
    //   runs the procedure: TrainingState.Act() for clicks and Tick() for the timer, fuel pour and battery charge.
    // - The other computer does not apply its clicks locally. It sends them here as RPCs ("Player 2 pressed control
    //   #7") and the authority applies them exactly as if they had been clicked there.
    // - After every network tick the authority copies its TrainingState into the [Networked] values below; the other
    //   computer copies them back into its own TrainingState every frame. The existing GeneratorTraining.Update()
    //   then draws the door, lamps, display etc. from that state, so the generator code itself barely changes.
    // - Component locks: pressing a control locks it to that player while the mouse button is held plus
    //   LockHoldAfterRelease seconds. The authority decides, so two simultaneous presses cannot both win.
    //
    // Sits on the generator's root object in the MultiplayerDesktop scene, next to a NetworkObject.
    // Both players can operate every control; "authority" only says whose computer keeps the shared state.
    [RequireComponent(typeof(GeneratorTraining))]
    public sealed class GeneratorNetSync : NetworkBehaviour
    {
        public const float LockHoldAfterRelease = 1f;
        const float MaxHoldSeconds = 15f;   // safety: a lock whose release message got lost does not stay forever
        const int MaxControls = 32;

        public static GeneratorNetSync Instance { get; private set; }

        // ---- Shared generator state (a mirror of TrainingState) ----
        [Networked] public int FaultId { get; set; }
        [Networked] public int TrippedBreaker { get; set; }
        [Networked] public int Step { get; set; }
        [Networked] public int Mode { get; set; }
        [Networked] public int WrongCount { get; set; }
        [Networked] public int Flags { get; set; }   // all bools + the four breakers, one bit each (see Pack/Unpack)
        [Networked] public float Elapsed { get; set; }
        [Networked] public float PenaltySeconds { get; set; }
        [Networked] public float FuelTime { get; set; }
        [Networked] public float ChargeTime { get; set; }
        [Networked] public NetworkString<_256> Feedback { get; set; }
        [Networked] public int HasState { get; set; }   // 0 until the authority has written the state once

        // ---- Locks: player number (1 or 2) holding each control, 0 = free ----
        [Networked, Capacity(MaxControls)] public NetworkArray<int> LockOwner { get; }

        GeneratorTraining trainer;
        TrainerControl[] controls;
        // Authority-only lock bookkeeping (not networked).
        readonly PlayerRef[] lockPlayer = new PlayerRef[MaxControls];
        readonly bool[] lockHeld = new bool[MaxControls];
        readonly float[] lockSince = new float[MaxControls];
        readonly float[] lockUntil = new float[MaxControls];

        void Awake()
        {
            trainer = GetComponent<GeneratorTraining>();
            // Same prefab on both computers -> same hierarchy order -> the same index means the same control.
            controls = GetComponentsInChildren<TrainerControl>(true);
            if (controls.Length > MaxControls) Debug.LogError("[GeneratorNetSync] More than " + MaxControls + " controls; raise MaxControls.");
        }

        public override void Spawned()
        {
            Instance = this;
            trainer.InteractFilter = FilterInteract;
            trainer.simulate = HasStateAuthority;
            if (HasStateAuthority)
            {
                // Rule 1: creating a session starts a fresh run at 0:00 (time spent in the menu does not count).
                // HasState == 0 means the shared state was never written in this session, i.e. we just created it.
                if (HasState == 0) trainer.RestartRun();
                WriteState();
            }
        }

        // Step A1 with the PPE table: completes (the same "PPE" action as the old board button) as soon as every
        // player in the session wears safety glasses AND ear defenders. Waits until all players' objects have arrived.
        const string DefaultA1Feedback = "Confirm eye and ear protection on the PPE button.";
        const string TableA1Feedback = "Put on safety glasses and ear defenders from the PPE table (every player).";

        void CompleteA1WhenEveryoneWearsPPE()
        {
            if (PPEStation.Instance == null) return;
            var s = trainer.state;
            if (s.step != 0 || s.Complete) return;
            if (s.feedback == DefaultA1Feedback) s.feedback = TableA1Feedback;   // the board button no longer exists here
            // Right after RESET / a new session every player's computer first takes the PPE off (NetworkedPlayer);
            // wait a moment so the old "wearing" state cannot complete A1 of the new run instantly.
            if (s.elapsed < 1.5f) return;

            int inSession = 0;
            foreach (var p in Runner.ActivePlayers) inSession++;
            int seen = 0, equipped = 0;
            foreach (var p in NetworkedPlayer.Players)
            {
                if (p == null || p.Object == null || !p.Object.IsValid) continue;
                seen++;
                if (p.FullyEquipped) equipped++;
            }
            if (seen == 0 || seen < inSession || equipped < seen) return;
            trainer.Interact("PPE", 0);   // -> "Eye and ear protection confirmed.", step A2
        }

        // Rule 2: when the partner first joins and nobody has completed a step yet, restart at 0:00 so the time
        // Player 1 spent waiting does not count. Once the first step is done, a run is never reset automatically
        // (leaving / rejoining keeps the run and the timer; only the RESET button restarts it).
        bool partnerJoined;

        void RestartWhenPartnerJoins()
        {
            if (partnerJoined) return;
            int players = 0;
            foreach (var p in Runner.ActivePlayers) players++;
            if (players < 2) return;
            partnerJoined = true;
            if (trainer.state.step == 0 && !trainer.state.Complete) trainer.RestartRun();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            if (trainer) { trainer.InteractFilter = null; trainer.simulate = true; }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            RestartWhenPartnerJoins();
            CompleteA1WhenEveryoneWearsPPE();
            WriteState();
            ExpireLocks();
        }

        public override void Render()
        {
            // If the authority moves to this computer (e.g. Player 1 left), it starts running the procedure.
            trainer.simulate = HasStateAuthority;
            if (!HasStateAuthority && HasState != 0) ReadState();
        }

        // ================= Interaction (called by DesktopInteractor) =================

        // Returns false if the control is locked by the other player (nothing happens).
        public bool Press(TrainerControl control)
        {
            int id = IndexOf(control);
            if (id < 0) { control.Press(); return true; }
            if (LockedByOther(control) != 0) return false;
            if (HasStateAuthority)
            {
                if (!TryLock(Runner.LocalPlayer, id)) return false;
                control.Press();   // -> GeneratorTraining.Interact -> runs here (this computer is the authority)
            }
            else RPC_Press(id);
            return true;
        }

        public void Release(TrainerControl control)
        {
            int id = IndexOf(control);
            if (id < 0) { control.Release(); return; }
            if (HasStateAuthority) { control.Release(); ReleaseLock(Runner.LocalPlayer, id); }
            else RPC_Release(id);
        }

        // Player number (1 or 2) of the OTHER player if they hold this control, otherwise 0.
        public int LockedByOther(TrainerControl control)
        {
            int id = IndexOf(control);
            if (id < 0 || Object == null || !Object.IsValid) return 0;
            int owner = LockOwner.Get(id);
            return owner != 0 && owner != LocalNumber() ? owner : 0;
        }

        // Any other code path that calls GeneratorTraining.Interact directly (e.g. the XR binding later) is
        // forwarded to the authority as well, so the shared state is never changed on the mirroring computer.
        bool FilterInteract(string action, int index)
        {
            if (Object == null || !Object.IsValid || HasStateAuthority) return false;
            RPC_Interact(action, index);
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Press(int id, RpcInfo info = default)
        {
            if (id < 0 || id >= controls.Length) return;
            if (TryLock(info.Source, id)) trainer.Interact(controls[id].action, controls[id].index);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Release(int id, RpcInfo info = default)
        {
            if (id >= 0 && id < controls.Length) ReleaseLock(info.Source, id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Interact(string action, int index)
        {
            trainer.Interact(action, index);
        }

        // ================= Locks (authority only) =================

        bool TryLock(PlayerRef player, int id)
        {
            float now = Time.unscaledTime;
            bool takenByOther = lockPlayer[id] != PlayerRef.None && lockPlayer[id] != player &&
                                (lockHeld[id] || now < lockUntil[id]);
            if (takenByOther) return false;
            lockPlayer[id] = player;
            lockHeld[id] = true;
            lockSince[id] = now;
            LockOwner.Set(id, NumberOf(player));
            return true;
        }

        void ReleaseLock(PlayerRef player, int id)
        {
            if (lockPlayer[id] != player) return;
            lockHeld[id] = false;
            lockUntil[id] = Time.unscaledTime + LockHoldAfterRelease;
        }

        void ExpireLocks()
        {
            float now = Time.unscaledTime;
            for (int id = 0; id < controls.Length; id++)
            {
                if (lockPlayer[id] == PlayerRef.None) continue;
                bool expired = lockHeld[id] ? now - lockSince[id] > MaxHoldSeconds : now >= lockUntil[id];
                if (expired || !IsInSession(lockPlayer[id]))
                {
                    lockPlayer[id] = PlayerRef.None;
                    lockHeld[id] = false;
                    LockOwner.Set(id, 0);
                }
            }
        }

        bool IsInSession(PlayerRef player)
        {
            foreach (var p in Runner.ActivePlayers) if (p == player) return true;
            return false;
        }

        // Two players only: this computer's own number, or the other one.
        int NumberOf(PlayerRef player)
        {
            int local = LocalNumber();
            return player == Runner.LocalPlayer ? local : 3 - local;
        }

        static int LocalNumber()
        {
            var me = NetworkedPlayer.Local;
            return me != null && me.Number > 0 ? me.Number : 1;
        }

        int IndexOf(TrainerControl control)
        {
            if (control == null || controls == null) return -1;
            for (int i = 0; i < controls.Length; i++) if (controls[i] == control) return i;
            return -1;
        }

        // ================= State copy =================

        void WriteState()
        {
            var s = trainer.state;
            FaultId = (int)s.fault;
            TrippedBreaker = s.trippedBreaker;
            Step = s.step;
            Mode = s.mode;
            WrongCount = s.wrongCount;
            Flags = Pack(s);
            Elapsed = s.elapsed;
            PenaltySeconds = s.penaltySeconds;
            FuelTime = s.fuelTime;
            ChargeTime = s.chargeTime;
            if (Feedback.Value != s.feedback) Feedback = s.feedback ?? "";
            HasState = 1;
        }

        void ReadState()
        {
            var s = trainer.state;
            s.fault = (GeneratorTrainer.Fault)FaultId;
            s.trippedBreaker = TrippedBreaker;
            s.step = Step;
            s.mode = Mode;
            s.wrongCount = WrongCount;
            Unpack(Flags, s);
            s.elapsed = Elapsed;
            s.penaltySeconds = PenaltySeconds;
            s.fuelTime = FuelTime;
            s.chargeTime = ChargeTime;
            string feedback = Feedback.Value;
            if (s.feedback != feedback) s.feedback = feedback;
        }

        static int Pack(TrainingState s)
        {
            int f = 0;
            if (s.ppe) f |= 1 << 0;
            if (s.exhaustSafe) f |= 1 << 1;
            if (s.estopPressed) f |= 1 << 2;
            if (s.doorOpen) f |= 1 << 3;
            if (s.keyInserted) f |= 1 << 4;
            if (s.stopPressed) f |= 1 << 5;
            if (s.coverOpen) f |= 1 << 6;
            if (s.capRemoved) f |= 1 << 7;
            if (s.chargerConnected) f |= 1 << 8;
            if (s.fuelOK) f |= 1 << 9;
            if (s.batteryOK) f |= 1 << 10;
            if (s.running) f |= 1 << 11;
            if (s.pouring) f |= 1 << 12;
            for (int i = 0; i < s.breakers.Length && i < 4; i++) if (s.breakers[i]) f |= 1 << (13 + i);
            return f;
        }

        static void Unpack(int f, TrainingState s)
        {
            s.ppe = (f & (1 << 0)) != 0;
            s.exhaustSafe = (f & (1 << 1)) != 0;
            s.estopPressed = (f & (1 << 2)) != 0;
            s.doorOpen = (f & (1 << 3)) != 0;
            s.keyInserted = (f & (1 << 4)) != 0;
            s.stopPressed = (f & (1 << 5)) != 0;
            s.coverOpen = (f & (1 << 6)) != 0;
            s.capRemoved = (f & (1 << 7)) != 0;
            s.chargerConnected = (f & (1 << 8)) != 0;
            s.fuelOK = (f & (1 << 9)) != 0;
            s.batteryOK = (f & (1 << 10)) != 0;
            s.running = (f & (1 << 11)) != 0;
            s.pouring = (f & (1 << 12)) != 0;
            for (int i = 0; i < s.breakers.Length && i < 4; i++) s.breakers[i] = (f & (1 << (13 + i))) != 0;
        }
    }
}
