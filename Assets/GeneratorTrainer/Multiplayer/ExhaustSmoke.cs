using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    // Thin grey exhaust smoke from the generator's exhaust mouth while the engine runs (from F2 START until G1 STOP,
    // or an emergency stop). It reads the shared TrainingState.running, so both players see the same smoke.
    // The emitter is attached to the exhaust pipe's pivot, so the smoke always leaves the pipe in the direction it
    // points - after step A2, towards the vent in the back wall. Purely visual: it never changes the procedure.
    // A light particle effect (a few dozen soft particles), fine for the Meta Quest 2.
    public sealed class ExhaustSmoke : MonoBehaviour
    {
        [Tooltip("Particle material (soft round texture, alpha blended). Set by the scene builder so it is included in builds.")]
        public Material material;

        GeneratorTraining trainer;
        ParticleSystem smoke;

        void Start()
        {
            trainer = FindAnyObjectByType<GeneratorTraining>();
            if (trainer == null) return;
            Transform pivot = null;
            foreach (var t in trainer.GetComponentsInChildren<Transform>(true)) if (t.name == "ExhaustPivot") { pivot = t; break; }
            if (pivot == null) return;

            var go = new GameObject("ExhaustSmokeEmitter");
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = new Vector3(0f, 0.32f, 0.3f);   // just outside the exhaust mouth
            go.transform.localRotation = Quaternion.identity;              // particles leave along the pipe's outlet (+Z)
            smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);
            main.gravityModifier = -0.04f;   // warm exhaust rises a little
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var emission = smoke.emission;
            emission.rateOverTime = 18f;

            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.03f;

            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3.5f));

            var colour = smoke.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 0f), new GradientColorKey(new Color(0.75f, 0.75f, 0.75f), 1f) },
                         new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.25f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;
        }

        void Update()
        {
            if (smoke == null || trainer == null) return;
            bool running = trainer.state.running;
            if (running && !smoke.isEmitting) smoke.Play();
            else if (!running && smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // last puffs drift away
        }
    }
}
