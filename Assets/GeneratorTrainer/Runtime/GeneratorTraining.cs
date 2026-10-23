using System.Collections.Generic;
using UnityEngine;
namespace GeneratorTrainer
{
    public sealed class GeneratorTraining : MonoBehaviour
    {
        public TrainingState state = new TrainingState();
        public AudioSource engine;
        public bool desktopControls = true;
        readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        readonly Dictionary<string, Material> lamps = new Dictionary<string, Material>();
        readonly Dictionary<string, Vector3> rest = new Dictionary<string, Vector3>();
        LineRenderer chargerCord;
        Transform Node(string name) { Transform v; return nodes.TryGetValue(name, out v) ? v : null; }
        void Awake()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            { nodes[t.name] = t; rest[t.name] = t.localPosition; }
            foreach (string n in new[] { "MCBLamp", "FUELLamp", "BATTERYLamp", "StartReadyLamp", "KeyRing" })
            { Transform t = Node(n); if (t) lamps[n] = t.GetComponent<Renderer>().material; }
        }
        void Start()
        {
            var cord = new GameObject("ChargerCable"); cord.transform.SetParent(transform, false);
            chargerCord = cord.AddComponent<LineRenderer>(); chargerCord.useWorldSpace = false; chargerCord.positionCount = 25; chargerCord.widthMultiplier = .014f;
            chargerCord.sharedMaterial = Node("ChargeSocket").GetComponent<Renderer>().sharedMaterial;
        }
        public void Interact(string action, int index)
        {
            if (action == "Reset") { state = new TrainingState(); return; }
            state.Act(action, index);
        }
        void Update()
        {
            state.Tick(Time.deltaTime);
            Rotate("DoorPivot", new Vector3(0, state.doorOpen ? -115 : 0, 0));
            Rotate("CoverPivot", new Vector3(state.coverOpen ? -125 : 0, 0, 0));
            Rotate("ExhaustPivot", new Vector3(0, state.exhaustSafe ? 180 : 0, 0));
            Rotate("ModePivot", new Vector3(0, 0, state.mode == 1 ? 45 : state.mode == 2 ? -45 : 0));
            for (int i = 0; i < 4; i++)
            {
                Rotate("BreakerPivot" + i, new Vector3(state.breakers[i] ? -25 : 25, 0, 0));
                Offset("BreakerPivot" + i, new Vector3(0, state.breakers[i] ? .025f : -.025f, 0));
            }
            Offset("EstopPivot", new Vector3(0, 0, state.estopPressed ? -.022f : 0));
            Offset("StopPivot", new Vector3(0, 0, state.stopPressed ? -.022f : 0));
            Offset("CapPivot", state.capRemoved ? new Vector3(.40f, -.08f, .12f) : Vector3.zero);
            Move("KeyProp", state.keyInserted ? new Vector3(-1.13f, 1.08f, .72f) : rest["KeyProp"]);
            Move("ChargerProp", state.chargerConnected ? new Vector3(-.73f, .51f, .79f) : rest["ChargerProp"]);
            // Optional grab mode disables automatic can positioning; trigger zone remains active.
            var can = Node("CanPivot");
            if (can && can.GetComponent<Rigidbody>() == null)
            {
                Move("CanPivot", state.pouring ? new Vector3(1.23f, .94f, .80f) : rest["CanPivot"]);
                Rotate("CanPivot", state.pouring ? new Vector3(0, 0, 120) : Vector3.zero);
            }
            Lamp("MCBLamp", state.McbOK); Lamp("FUELLamp", state.FuelLampOK);
            Lamp("BATTERYLamp", state.batteryOK); Lamp("StartReadyLamp", state.Ready);
            Lamp("KeyRing", state.keyInserted, true);
            Text("DisplayText", state.running ? "400 V   50 Hz\nNO FAULT" : state.keyInserted ? "MANUAL / STANDBY" : "PANEL OFF");
            Text("StepText", Wrap(state.Current, 28)); Text("FeedbackText", Wrap(state.feedback, 35));
            if (engine) { if (state.running && !engine.isPlaying) engine.Play(); else if (!state.running && engine.isPlaying) engine.Stop(); }
            if (chargerCord)
            {
                Vector3 start = new Vector3(-1.73f,.44f,.78f), end = Node("ChargerProp").localPosition;
                for (int i = 0; i < 25; i++) { float t = i / 24f; Vector3 p = Vector3.Lerp(start,end,t); p.y -= Mathf.Sin(t*Mathf.PI)*.22f; p.z += Mathf.Sin(t*Mathf.PI)*.12f; chargerCord.SetPosition(i,p); }
            }
            if (desktopControls) DesktopClick();
        }
        void Rotate(string n, Vector3 e) { var t = Node(n); if (t) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(e), 1 - Mathf.Exp(-10 * Time.deltaTime)); }
        void Move(string n, Vector3 p) { var t = Node(n); if (t) t.localPosition = Vector3.Lerp(t.localPosition, p, 1 - Mathf.Exp(-10 * Time.deltaTime)); }
        void Offset(string n, Vector3 d) { if (rest.ContainsKey(n)) Move(n, rest[n] + d); }
        void Lamp(string n, bool on, bool offDark = false)
        {
            Material m; if (!lamps.TryGetValue(n, out m)) return;
            Color c = on ? new Color(.035f, .9f, .16f) : offDark ? new Color(.025f,.05f,.03f) : new Color(.95f, .035f, .015f);
            m.color = c; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * (offDark && !on ? 0 : 1.4f));
        }
        void Text(string n, string value) { var t = Node(n); if (t) { var m = t.GetComponent<TextMesh>(); if (m && m.text != value) m.text = value; } }
        static string Wrap(string value, int width)
        {
            var result = new System.Text.StringBuilder(); int line = 0;
            foreach (string word in value.Split(' ')) { if (line + word.Length > width) { result.Append('\n'); line = 0; } if (line > 0) { result.Append(' '); line++; } result.Append(word); line += word.Length; }
            return result.ToString();
        }
        void DesktopClick()
        {
            bool click = false; Vector2 pos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) { click = mouse.leftButton.wasPressedThisFrame; pos = mouse.position.ReadValue(); }
#elif ENABLE_LEGACY_INPUT_MANAGER
            click = Input.GetMouseButtonDown(0); pos = Input.mousePosition;
#endif
            if (!click || Camera.main == null) return;
            // Ignore fuel trigger volumes; solid cabinet colliders occlude hidden controls.
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(pos), out hit, 15, ~0, QueryTriggerInteraction.Ignore))
            { var c = hit.collider.GetComponentInParent<TrainerControl>(); if (c) c.Click(); }
        }
        void OnDestroy() { foreach (var m in lamps.Values) if (m) Destroy(m); }
    }
}
