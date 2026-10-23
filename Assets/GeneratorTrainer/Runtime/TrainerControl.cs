using UnityEngine;
namespace GeneratorTrainer
{
    public sealed class TrainerControl : MonoBehaviour
    {
        public GeneratorTraining trainer;
        public string action;
        public int index;
        public bool reversePoke;
        bool held;
        float last = -10;
        public void Press()
        {
            if (held || Time.unscaledTime - last < .25f || trainer == null) return;
            held = true; last = Time.unscaledTime; trainer.Interact(action, index);
        }
        public void Release() { held = false; }
        public void Click() { Press(); Release(); }
        void OnDisable() { held = false; }
    }
}
