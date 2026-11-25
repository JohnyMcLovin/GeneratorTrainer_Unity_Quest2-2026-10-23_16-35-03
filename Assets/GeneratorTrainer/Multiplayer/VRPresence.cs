using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Network presence of a VR player (on the VRPlayer network prefab, next to NetworkedPlayer and PlayerAvatar).
    // On the VR player's own computer it follows the local VR rig every frame: standing where the headset is, facing
    // where the headset looks. The NetworkTransform on the same object sends that to the other player, who sees the
    // avatar there. Head and hand tracking for the avatar come in Milestone 8; for now the avatar's head tilts with
    // the headset (synced as NetworkedPlayer.Pitch).
    public sealed class VRPresence : MonoBehaviour
    {
        [Tooltip("Head pivot of this object; the avatar's head follows it.")] public Transform head;

        [System.NonSerialized] public bool isLocal;
        public float HeadPitch { get; private set; }

        void LateUpdate()
        {
            if (!isLocal) return;
            var rig = VRRig.Local;
            if (rig == null || rig.head == null) return;
            Vector3 h = rig.head.position;
            float floorY = rig.origin != null ? rig.origin.transform.position.y : 0f;
            transform.SetPositionAndRotation(new Vector3(h.x, floorY, h.z), Quaternion.Euler(0f, rig.head.eulerAngles.y, 0f));
            float pitch = rig.head.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            HeadPitch = pitch;
            if (head) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
