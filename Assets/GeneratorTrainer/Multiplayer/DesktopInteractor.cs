using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Crosshair interaction for the desktop player. A ray from the centre of the player's own camera finds the
    // generator control in front of it (the existing TrainerControl). Mouse down calls Press(), mouse up (or
    // looking away) calls Release() - the same entry point the old mouse click and the XR binding use.
    // In Milestone 4 the network lock for a control hooks in here, between Press and Release.
    public sealed class DesktopInteractor : MonoBehaviour
    {
        public Camera view;
        [Tooltip("How far away (metres) a control can be operated.")]
        public float reach = 2.6f;

        public TrainerControl Hovered { get; private set; }
        public TrainerControl Pressed { get; private set; }

        void Awake() { if (view == null) view = GetComponentInChildren<Camera>(); }

        void Update()
        {
            Hovered = null;
            bool locked = DesktopPlayerController.CursorLocked;
            if (locked && view != null)
            {
                // Trigger volumes (the fuel zone) are ignored; solid cabinet colliders block hidden controls.
                RaycastHit hit;
                var ray = new Ray(view.transform.position, view.transform.forward);
                if (Physics.Raycast(ray, out hit, reach, ~0, QueryTriggerInteraction.Ignore))
                    Hovered = hit.collider.GetComponentInParent<TrainerControl>();
            }

            bool canClick = locked && Time.frameCount != DesktopPlayerController.LockedOnFrame;
            if (canClick && DesktopInput.ClickDown && Hovered != null)
            {
                // In a session the press goes through the network bridge, which checks the component lock and
                // forwards the action to the computer that runs the procedure. Offline it is a plain Press().
                var sync = GeneratorNetSync.Instance;
                bool accepted = sync != null ? sync.Press(Hovered) : PressLocal(Hovered);
                if (accepted) Pressed = Hovered;
            }
            if (Pressed != null && (!DesktopInput.ClickHeld || Hovered != Pressed)) ReleasePressed();
        }

        // Player number (1 or 2) of the other player if they are operating the hovered control, otherwise 0.
        public int HoveredLockedBy
        {
            get { var sync = GeneratorNetSync.Instance; return sync != null && Hovered != null ? sync.LockedByOther(Hovered) : 0; }
        }

        static bool PressLocal(TrainerControl control) { control.Press(); return true; }

        void ReleasePressed()
        {
            if (Pressed != null)
            {
                var sync = GeneratorNetSync.Instance;
                if (sync != null) sync.Release(Pressed); else Pressed.Release();
            }
            Pressed = null;
        }

        void OnDisable() { ReleasePressed(); Hovered = null; }
    }
}
