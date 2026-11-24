using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // First-person desktop player: WASD to walk, Shift to walk faster, mouse to look.
    // Esc frees the mouse cursor; clicking the game view locks it again (that click does not operate anything).
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopPlayerController : MonoBehaviour
    {
        [Tooltip("Child transform holding the camera, at eye height. Pitch (looking up/down) is applied here.")]
        public Transform head;
        public float walkSpeed = 2.2f;
        public float fastSpeed = 3.6f;
        [Tooltip("Degrees of turn per pixel of mouse movement.")]
        public float lookSensitivity = 0.12f;
        public float gravity = -9.81f;

        public static bool CursorLocked { get { return Cursor.lockState == CursorLockMode.Locked; } }
        // Current up/down look angle in degrees (+ = looking down). Synced to the other player by NetworkedPlayer.
        public float Pitch { get { return pitch; } }
        // Frame on which a click re-locked the cursor, so the interactor can ignore that click.
        public static int LockedOnFrame { get; private set; } = -1;

        CharacterController body;
        float pitch, fallSpeed;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (head == null) { var cam = GetComponentInChildren<Camera>(); if (cam) head = cam.transform; }
        }

        void OnEnable() { SetCursorLocked(true); }
        void OnDisable() { SetCursorLocked(false); }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (DesktopInput.EscapePressed) SetCursorLocked(false);
            else if (!CursorLocked && DesktopInput.ClickDown) { SetCursorLocked(true); LockedOnFrame = Time.frameCount; }

            if (CursorLocked)
            {
                Vector2 look = DesktopInput.Look() * lookSensitivity;
                transform.Rotate(0f, look.x, 0f, Space.Self);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                if (head) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector2 input = DesktopInput.Move();
            Vector3 move = transform.right * input.x + transform.forward * input.y;
            if (move.sqrMagnitude > 1f) move.Normalize();
            move *= DesktopInput.Fast ? fastSpeed : walkSpeed;

            if (body.isGrounded && fallSpeed < 0f) fallSpeed = -1f;   // small push keeps us grounded on slopes/steps
            fallSpeed += gravity * Time.deltaTime;
            move.y = fallSpeed;
            body.Move(move * Time.deltaTime);
        }
    }
}
