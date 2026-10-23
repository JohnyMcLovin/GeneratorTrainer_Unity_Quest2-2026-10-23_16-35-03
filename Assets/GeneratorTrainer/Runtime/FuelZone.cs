using UnityEngine;
namespace GeneratorTrainer
{
    public sealed class FuelZone : MonoBehaviour
    {
        public GeneratorTraining trainer;
        void OnTriggerStay(Collider other)
        {
            var can = other.GetComponentInParent<DieselCan>();
            if (can && trainer && trainer.state.step == 15 && trainer.state.capRemoved)
            {
                bool tilted = Vector3.Dot(can.transform.up, Vector3.up) < .5f;
                if (tilted && !trainer.state.pouring) trainer.Interact("Fuel", 0);
                if (!tilted) trainer.state.pouring = false;
            }
        }
        void OnTriggerExit(Collider other)
        {
            // In optional physical-can mode filling requires the can to stay in the zone.
            if (other.GetComponentInParent<DieselCan>() && trainer && trainer.state.step == 15) trainer.state.pouring = false;
        }
    }
}
