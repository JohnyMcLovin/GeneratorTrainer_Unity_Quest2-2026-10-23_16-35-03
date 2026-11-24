using System.Collections.Generic;
using UnityEngine;

namespace GeneratorTrainer.Multiplayer
{
    public enum PlayerRole { Instructions, Operator }

    // Placeholder avatar built from Unity primitives at runtime (no imported assets):
    // a capsule body, a head with a dark visor showing where the player looks, role colour and a name tag.
    // The owner never sees their own avatar (isLocalPlayer). Only position/rotation will be synced in
    // Milestone 3, so the visuals can later be swapped for a nicer model without touching networking code.
    public sealed class PlayerAvatar : MonoBehaviour
    {
        public PlayerRole role = PlayerRole.Instructions;
        [Tooltip("1 or 2. Shown on the name tag.")] public int playerNumber = 1;
        [Tooltip("True for the avatar of the player at this computer: it is hidden from their own camera.")]
        public bool isLocalPlayer = true;
        [Tooltip("The player's head/camera transform; the avatar's head follows its up/down look.")]
        public Transform head;

        public static readonly Color InstructionsColor = new Color(0.20f, 0.45f, 0.95f);
        public static readonly Color OperatorColor = new Color(0.98f, 0.55f, 0.12f);

        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Material> materials = new List<Material>();
        Transform headPivot, nameTag;
        TextMesh tagText;
        Renderer bodyRenderer, headRenderer;
        bool built;

        void Awake() { Build(); }

        void Build()
        {
            if (built) return;
            built = true;
            var visuals = new GameObject("AvatarVisuals").transform;
            visuals.SetParent(transform, false);

            bodyRenderer = Part(PrimitiveType.Capsule, visuals, "Body", new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.75f, 0.5f));

            headPivot = new GameObject("HeadPivot").transform;
            headPivot.SetParent(visuals, false);
            headPivot.localPosition = new Vector3(0, 1.62f, 0);
            headRenderer = Part(PrimitiveType.Sphere, headPivot, "Head", Vector3.zero, Vector3.one * 0.28f);
            var visor = Part(PrimitiveType.Cube, headPivot, "Visor", new Vector3(0, 0.02f, 0.12f), new Vector3(0.22f, 0.08f, 0.06f));
            visor.material.color = new Color(0.05f, 0.06f, 0.08f);

            nameTag = new GameObject("NameTag").transform;
            nameTag.SetParent(visuals, false);
            nameTag.localPosition = new Vector3(0, 2.0f, 0);
            tagText = nameTag.gameObject.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tagText.font = font;
            tagText.fontSize = 64;
            tagText.characterSize = 0.012f;
            tagText.anchor = TextAnchor.MiddleCenter;
            tagText.alignment = TextAlignment.Center;
            var tagRenderer = nameTag.GetComponent<MeshRenderer>();
            tagRenderer.sharedMaterial = font.material;
            renderers.Add(tagRenderer);

            ApplyRole();
            ApplyVisibility();
        }

        Renderer Part(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // No colliders: the avatar must not block rays (interaction) or the player's own movement.
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            materials.Add(r.material);   // per-avatar material instance, destroyed with the avatar
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderers.Add(r);
            return r;
        }

        public void SetRole(PlayerRole newRole, int number)
        {
            role = newRole;
            playerNumber = number;
            if (built) ApplyRole();
        }

        public void SetLocal(bool local)
        {
            isLocalPlayer = local;
            if (built) ApplyVisibility();
        }

        void ApplyRole()
        {
            Color c = role == PlayerRole.Instructions ? InstructionsColor : OperatorColor;
            bodyRenderer.material.color = c;
            headRenderer.material.color = Color.Lerp(c, Color.white, 0.35f);
            tagText.color = Color.Lerp(c, Color.white, 0.5f);
            tagText.text = "Player " + playerNumber + " - " + (role == PlayerRole.Instructions ? "Instructions" : "Operator");
        }

        void ApplyVisibility()
        {
            foreach (var r in renderers) if (r) r.enabled = !isLocalPlayer;
        }

        void LateUpdate()
        {
            if (!built || isLocalPlayer) return;
            // Head tilts with the player's look direction.
            if (head) headPivot.rotation = head.rotation;
            // Name tag always faces whoever is looking at it.
            var cam = Camera.main;
            if (cam) nameTag.rotation = Quaternion.LookRotation(nameTag.position - cam.transform.position, Vector3.up);
        }

        void OnDestroy() { foreach (var m in materials) if (m) Destroy(m); }
    }
}
