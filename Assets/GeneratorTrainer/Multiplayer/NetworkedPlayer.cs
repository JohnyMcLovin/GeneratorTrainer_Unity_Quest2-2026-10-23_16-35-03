using Fusion;
using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Network side of the DesktopPlayer prefab (Photon Fusion, Shared Mode).
    // The computer that spawned this player has "state authority" over it: it enables the camera, controls and
    // crosshair, and hides its own avatar. On the other computer the same object is a remote copy: controls and
    // camera stay off and the avatar is shown. Position and facing are synced by the NetworkTransform on the
    // same object; this script syncs head tilt (looking up/down), the player number and the role.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedPlayer : NetworkBehaviour
    {
        [Networked] public int Number { get; set; }     // 1 = session creator, 2 = the player who joined
        [Networked] public int Role { get; set; }       // (int)PlayerRole
        [Networked] public float Pitch { get; set; }    // degrees, + = looking down

        public static NetworkedPlayer Local { get; private set; }

        public DesktopPlayerController controller;
        public DesktopInteractor interactor;
        public Crosshair crosshair;
        public PlayerAvatar avatar;
        public Camera view;
        public AudioListener listener;
        public CharacterController body;

        int shownNumber = -1, shownRole = -1;

        public override void Spawned()
        {
            bool local = HasStateAuthority;
            if (local)
            {
                Local = this;
                Runner.SetPlayerObject(Runner.LocalPlayer, Object);
                Number = Runner.IsSharedModeMasterClient ? 1 : 2;
                Role = (int)(Number == 1 ? PlayerRole.Instructions : PlayerRole.Operator);
            }

            // The prefab starts with all of these switched off; only the owner turns them on.
            body.enabled = local;
            controller.enabled = local;
            interactor.enabled = local;
            crosshair.enabled = local;
            view.enabled = local;
            listener.enabled = local;
            avatar.SetLocal(local);

            if (local && SessionLauncher.Instance) SessionLauncher.Instance.ShowOverview(false);
            ApplyIdentity();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this) Local = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority) Pitch = controller.Pitch;
        }

        public override void Render()
        {
            // Remote copy: tilt the head the way its owner is looking (the avatar's head follows this transform).
            if (!HasStateAuthority && controller.head)
                controller.head.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
            ApplyIdentity();
        }

        void ApplyIdentity()
        {
            if (Number == shownNumber && Role == shownRole) return;
            shownNumber = Number;
            shownRole = Role;
            if (Number > 0)
            {
                avatar.SetRole((PlayerRole)Role, Number);
                gameObject.name = "Player " + Number + (HasStateAuthority ? " (you)" : "");
            }
        }
    }
}
