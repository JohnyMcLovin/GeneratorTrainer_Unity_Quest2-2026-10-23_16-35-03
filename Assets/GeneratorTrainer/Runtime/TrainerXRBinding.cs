using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace GeneratorTrainer
{
    // Makes every generator control usable in the headset. At scene start, in any scene that has an
    // XR Interaction Manager (e.g. KeyboardTest with XR Interaction Setup), each TrainerControl gets an
    // XRSimpleInteractable whose select events call Press/Release, the same path the desktop mouse uses.
    // With the XRI Starter Assets rig: point the controller ray at a control and press the GRIP button.
    // Done at runtime, so no prefab or scene has to carry saved bindings.
    public static class TrainerXRBinding
    {
        static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!subscribed) { SceneManager.sceneLoaded += (s, m) => Bind(); subscribed = true; }
            Bind();
        }

        // Returns how many controls were bound.
        public static int Bind()
        {
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null) return 0;   // desktop scenes: mouse only
            int bound = 0;
            foreach (var control in Object.FindObjectsByType<TrainerControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (control.GetComponent<XRBaseInteractable>() != null) continue;   // already bound (e.g. hand-poke setup)
                var interactable = control.gameObject.AddComponent<XRSimpleInteractable>();
                var c = control;
                interactable.selectEntered.AddListener(_ => c.Press());
                interactable.selectExited.AddListener(_ => c.Release());
                bound++;
            }
            if (bound > 0) Debug.Log("[TrainerXRBinding] " + bound + " generator controls bound to XR select.");
            return bound;
        }
    }
}
