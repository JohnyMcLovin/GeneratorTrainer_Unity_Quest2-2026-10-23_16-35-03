using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace GeneratorTrainer.Multiplayer
{
    // The LOCAL VR player (Milestone 7): the official XR Interaction Toolkit rig (XR Origin from the Starter Assets:
    // head + two controllers with rays, smooth move on the left stick, snap turn / teleport on the right stick).
    // It is not a network object - it exists in the scene, switched on only in VR mode, so it can already be used for
    // the session menu before connecting. After connecting, a small network "presence" (VRPresence + NetworkedPlayer)
    // follows this rig so the other player sees an avatar.
    //
    // Generator interaction: every TrainerControl gets an XRSimpleInteractable. Point the ray at a control and pull
    // the TRIGGER: the press goes through GeneratorNetSync.Press/Release (component locks + forwarding to the computer
    // that runs the procedure) - exactly the same path as the desktop mouse. The ray turns red over a control the
    // other player is operating.
    public sealed class VRRig : MonoBehaviour
    {
        public static VRRig Local { get; private set; }

        public XROrigin origin;
        public Transform head;        // the VR camera
        public Transform leftHand;    // left controller (the tablet goes here)
        public Transform rightHand;   // right controller

        static readonly Color LockedColor = new Color(1f, 0.25f, 0.2f, 1f);

        readonly List<XRRayInteractor> rays = new List<XRRayInteractor>();
        readonly Dictionary<XRInteractorLineVisual, Gradient> normalColours = new Dictionary<XRInteractorLineVisual, Gradient>();
        readonly HashSet<TrainerControl> pressed = new HashSet<TrainerControl>();
        Gradient lockedGradient;

        void Awake()
        {
            Local = this;
            if (origin == null) origin = GetComponentInChildren<XROrigin>(true);
            if (head == null && origin != null && origin.Camera != null) head = origin.Camera.transform;
            if (leftHand == null) leftHand = FindChild(origin.transform, "Left Controller");
            if (rightHand == null) rightHand = FindChild(origin.transform, "Right Controller");
            lockedGradient = new Gradient();
            lockedGradient.SetKeys(new[] { new GradientColorKey(LockedColor, 0f), new GradientColorKey(LockedColor, 1f) },
                                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        void OnDestroy() { if (Local == this) Local = null; }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        void Start()
        {
            UseTriggerToSelect();
            BindGeneratorControls();
            BindPPEItems();
            MakeFloorTeleportable();
            foreach (var ray in origin.GetComponentsInChildren<XRRayInteractor>(true))
            {
                if (ray.name.Contains("Teleport")) continue;   // the teleport arc keeps its own colours
                rays.Add(ray);
                var line = ray.GetComponent<XRInteractorLineVisual>();
                if (line) normalColours[line] = line.validColorGradient;
            }
        }

        // Starter Assets select with the GRIP; for pressing buttons the TRIGGER is more natural, so the controllers'
        // "select" uses the trigger ("activate") action instead.
        void UseTriggerToSelect()
        {
            foreach (var c in origin.GetComponentsInChildren<ActionBasedController>(true))
            {
                c.selectAction = c.activateAction;
                c.selectActionValue = c.activateActionValue;
            }
        }

        void BindGeneratorControls()
        {
            foreach (var control in FindObjectsByType<TrainerControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (control.GetComponent<XRBaseInteractable>() != null) continue;
                var interactable = control.gameObject.AddComponent<XRSimpleInteractable>();
                var c = control;
                interactable.selectEntered.AddListener(_ => OnPress(c));
                interactable.selectExited.AddListener(_ => OnRelease(c));
            }
        }

        // PPE table (step A1): point at glasses / ear defenders and pull the trigger to put them on.
        void BindPPEItems()
        {
            foreach (var item in FindObjectsByType<PPEItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.GetComponent<XRBaseInteractable>() != null) continue;
                var interactable = item.gameObject.AddComponent<XRSimpleInteractable>();
                var kind = item.kind;
                interactable.selectEntered.AddListener(_ => { if (PPEStation.Instance != null) PPEStation.Instance.Take(kind); });
            }
        }

        void OnPress(TrainerControl control)
        {
            var sync = GeneratorNetSync.Instance;
            bool accepted = true;
            if (sync != null) accepted = sync.Press(control);
            else control.Press();
            if (accepted) pressed.Add(control);
        }

        void OnRelease(TrainerControl control)
        {
            if (!pressed.Remove(control)) return;
            var sync = GeneratorNetSync.Instance;
            if (sync != null) sync.Release(control); else control.Release();
        }

        // Teleporting needs a TeleportationArea on the floor (added here, only in VR, so desktop builds stay unchanged).
        void MakeFloorTeleportable()
        {
            var floor = GameObject.Find("WorkshopFloor");
            if (floor == null || floor.GetComponent<TeleportationArea>() != null) return;
            var area = floor.AddComponent<TeleportationArea>();
            area.interactionLayers = 1 << 31;   // the Starter Assets teleport interactor uses interaction layer 31 ("Teleport")
        }

        // Place the rig at a spawn point (feet at the point, looking along its forward).
        public void MoveTo(Vector3 position, Quaternion rotation)
        {
            if (origin == null) return;
            origin.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        }

        void Update()
        {
            // Red ray over a control that the other player is operating (component lock), otherwise normal colours.
            var sync = GeneratorNetSync.Instance;
            foreach (var ray in rays)
            {
                var line = ray.GetComponent<XRInteractorLineVisual>();
                if (line == null || !normalColours.ContainsKey(line)) continue;
                bool locked = false;
                RaycastHit hit;
                if (sync != null && ray.TryGetCurrent3DRaycastHit(out hit) && hit.collider != null)
                {
                    var control = hit.collider.GetComponentInParent<TrainerControl>();
                    locked = control != null && sync.LockedByOther(control) != 0;
                }
                line.validColorGradient = locked ? lockedGradient : normalColours[line];
            }
        }
    }
}
