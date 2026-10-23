using UnityEngine;
namespace GeneratorTrainer
{
    public sealed class TrainerOrbit : MonoBehaviour
    {
        public Vector3 target = new Vector3(-.2f, 1, 0);
        public float yaw = 15, pitch = 12, distance = 4.8f;
        void LateUpdate()
        {
            Vector2 d = Vector2.zero; float scroll = 0; bool held = false;
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null) { held = m.rightButton.isPressed; d = m.delta.ReadValue() * .15f; scroll = m.scroll.ReadValue().y / 120; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            held = Input.GetMouseButton(1); d = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3; scroll = Input.mouseScrollDelta.y;
#endif
            if (held) { yaw += d.x; pitch = Mathf.Clamp(pitch - d.y, -15, 70); }
            distance = Mathf.Clamp(distance - scroll * .3f, 1.8f, 8);
            transform.position = target + Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward * distance;
            transform.LookAt(target);
        }
    }
}
