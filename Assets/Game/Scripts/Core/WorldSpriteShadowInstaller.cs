using UnityEngine;

namespace MicroJam.Game
{
    public sealed class WorldSpriteShadowInstaller : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float opacity = 0.3f;
        [SerializeField, Min(0f)] private float verticalOffset = 0.09f;
        [SerializeField, Min(1f)] private float scaleMultiplier = 1.04f;
        [SerializeField, Range(0f, 4f)] private float edgeBlur = 1.5f;
        [SerializeField, Min(0.1f)] private float rescanInterval = 0.5f;
        [SerializeField] private Shader softShadowShader;

        private float nextScanTime;
        private Material softShadowMaterial;

        private void Start()
        {
            if (softShadowShader != null)
            {
                softShadowMaterial = new Material(softShadowShader) { name = "Runtime Soft Sprite Shadow" };
                softShadowMaterial.SetFloat("_BlurSize", edgeBlur);
            }
            InstallMissingShadows();
        }

        private void OnDestroy()
        {
            if (softShadowMaterial != null) Destroy(softShadowMaterial);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime) return;
            InstallMissingShadows();
        }

        private void InstallMissingShadows()
        {
            nextScanTime = Time.unscaledTime + rescanInterval;
            SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.GetComponent<SpriteShapeShadow>() != null ||
                    renderer.GetComponentInParent<HealthBar>() != null ||
                    renderer.name.Contains("Shadow AO"))
                {
                    continue;
                }

                Transform owner = FindSupportedOwner(renderer.transform);
                if (owner == null) continue;

                SpriteShapeShadow shadow = renderer.gameObject.AddComponent<SpriteShapeShadow>();
                shadow.Configure(renderer, softShadowMaterial, opacity, verticalOffset, scaleMultiplier);
            }
        }

        private static Transform FindSupportedOwner(Transform child)
        {
            PlayerMovement player = child.GetComponentInParent<PlayerMovement>();
            if (player != null) return player.transform;
            ResourceNode resource = child.GetComponentInParent<ResourceNode>();
            if (resource != null) return resource.transform;
            DinosaurAgent dinosaur = child.GetComponentInParent<DinosaurAgent>();
            if (dinosaur != null) return dinosaur.transform;
            CampfireInteraction campfire = child.GetComponentInParent<CampfireInteraction>();
            if (campfire != null) return campfire.transform;
            BuildingInstance building = child.GetComponentInParent<BuildingInstance>();
            return building != null ? building.transform : null;
        }

        private void OnValidate()
        {
            opacity = Mathf.Clamp01(opacity);
            verticalOffset = Mathf.Max(0f, verticalOffset);
            scaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            edgeBlur = Mathf.Clamp(edgeBlur, 0f, 4f);
            rescanInterval = Mathf.Max(0.1f, rescanInterval);
        }
    }

    internal sealed class SpriteShapeShadow : MonoBehaviour
    {
        private SpriteRenderer source;
        private SpriteRenderer shadow;
        private float opacity;
        private float verticalOffset;
        private float scaleMultiplier;

        public void Configure(SpriteRenderer sourceRenderer, Material shadowMaterial, float configuredOpacity, float offset, float scale)
        {
            source = sourceRenderer;
            opacity = configuredOpacity;
            verticalOffset = offset;
            scaleMultiplier = scale;
            CreateShadow();
            if (shadowMaterial != null) shadow.sharedMaterial = shadowMaterial;
            SyncShadow();
        }

        private void LateUpdate()
        {
            if (source == null)
            {
                if (shadow != null) Destroy(shadow.gameObject);
                Destroy(this);
                return;
            }

            SyncShadow();
        }

        private void CreateShadow()
        {
            GameObject shadowObject = new($"{source.name} Shadow AO");
            shadowObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            shadowObject.transform.SetParent(source.transform.parent, false);
            shadow = shadowObject.AddComponent<SpriteRenderer>();
        }

        private void SyncShadow()
        {
            if (shadow == null) return;

            Transform sourceTransform = source.transform;
            Transform shadowTransform = shadow.transform;
            shadowTransform.localPosition = sourceTransform.localPosition + Vector3.down * verticalOffset;
            shadowTransform.localRotation = sourceTransform.localRotation;
            Vector3 sourceScale = sourceTransform.localScale;
            shadowTransform.localScale = new Vector3(sourceScale.x * scaleMultiplier, sourceScale.y * scaleMultiplier, sourceScale.z);

            shadow.sprite = source.sprite;
            shadow.color = new Color(0f, 0f, 0f, opacity);
            shadow.flipX = source.flipX;
            shadow.flipY = source.flipY;
            shadow.drawMode = source.drawMode;
            shadow.size = source.size;
            shadow.sortingLayerID = source.sortingLayerID;
            shadow.sortingOrder = source.sortingOrder - 1;
            shadow.maskInteraction = source.maskInteraction;
            shadow.enabled = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
        }
    }
}
