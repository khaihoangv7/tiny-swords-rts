using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public readonly struct BuildingPresentationProfile
    {
        private readonly Vector2 fireAnchor0;
        private readonly Vector2 fireAnchor1;
        private readonly Vector2 fireAnchor2;

        public float FireScale { get; }
        public float ExplosionScale { get; }
        public Vector2 ExplosionOffset0 { get; }
        public Vector2 ExplosionOffset1 { get; }
        public float LifeBarWidthScale { get; }
        public float GarrisonCountClearance { get; }

        public BuildingPresentationProfile(float fireScale, Vector2 fire0,
            Vector2 fire1, Vector2 fire2, float explosionScale,
            Vector2 explosion0, Vector2 explosion1, float lifeBarWidthScale,
            float garrisonCountClearance)
        {
            FireScale = fireScale;
            fireAnchor0 = fire0;
            fireAnchor1 = fire1;
            fireAnchor2 = fire2;
            ExplosionScale = explosionScale;
            ExplosionOffset0 = explosion0;
            ExplosionOffset1 = explosion1;
            LifeBarWidthScale = lifeBarWidthScale;
            GarrisonCountClearance = garrisonCountClearance;
        }

        public Vector2 FireAnchor(int index)
            => index switch
            {
                0 => fireAnchor0,
                1 => fireAnchor1,
                _ => fireAnchor2
            };
    }

    // One presentation-size mapping shared by Fire, Explosion and building UI.
    // Gameplay transforms, HP and footprints never depend on these values.
    public static class BuildingPresentationProfiles
    {
        private static readonly BuildingPresentationProfile Small = new(
            1.8f,
            new Vector2(-0.32f, 0.20f), new Vector2(0f, 0.58f),
            new Vector2(0.32f, 0.20f),
            1.35f, new Vector2(-0.08f, 0.03f), new Vector2(0.08f, 0.08f),
            1f, 0.72f);

        private static readonly BuildingPresentationProfile Medium = new(
            2.4f,
            new Vector2(-0.62f, 0.22f), new Vector2(0f, 0.72f),
            new Vector2(0.62f, 0.28f),
            1.75f, new Vector2(-0.12f, 0.04f), new Vector2(0.12f, 0.10f),
            1.25f, 0.72f);

        private static readonly BuildingPresentationProfile GoldMine = new(
            2.47f,
            new Vector2(-0.585f, 0.13f), new Vector2(0f, 0.546f),
            new Vector2(0.585f, 0.156f),
            2.08f, new Vector2(-0.13f, 0.026f), new Vector2(0.13f, 0.104f),
            1.15f, 0.6f);

        private static readonly BuildingPresentationProfile Large = new(
            3.2f,
            new Vector2(-1f, 0.28f), new Vector2(0f, 0.92f),
            new Vector2(1f, 0.34f),
            2.4f, new Vector2(-0.18f, 0.05f), new Vector2(0.18f, 0.12f),
            1.8f, 0.72f);

        public static BuildingPresentationProfile For(BuildingType type)
            => type switch
            {
                BuildingType.Castle => Large,
                BuildingType.GoldMine => GoldMine,
                BuildingType.House or BuildingType.Tower => Small,
                _ => Medium
            };
    }

    // Presentation only: HP, destruction and Fog remain authoritative elsewhere.
    public sealed class BuildingDamagePresentation : MonoBehaviour
    {
        public const float FireThreshold = 0.70f;
        public const float FramesPerSecond = 10f;

        private IDefensiveBuildingHost host;
        private BuildingPresentationCatalog catalog;
        private GameObject fireRoot;
        private SpriteLoopPlayer[] fires;
        private bool burning;
        private bool destroyed;

        public bool IsBurning => burning;
        public bool IsConfigured => host != null;

        public void Configure(IDefensiveBuildingHost owner)
        {
            if (host != null)
            {
                host.DefenseHostHealthChanged -= Refresh;
                host.DefenseHostDestroyed -= HandleDestroyed;
            }
            host = owner;
            catalog = BuildingPresentationCatalog.Load();
            if (host == null || catalog == null) return;
            if (fireRoot == null) CreateFireEmitters();
            host.DefenseHostHealthChanged += Refresh;
            host.DefenseHostDestroyed += HandleDestroyed;
            Refresh();
        }

        private void OnDestroy()
        {
            if (host == null) return;
            host.DefenseHostHealthChanged -= Refresh;
            host.DefenseHostDestroyed -= HandleDestroyed;
        }

        private void Update()
        {
            if (!burning || fireRoot == null || host == null) return;
            bool presented = FogWorldVisibility.Instance == null
                || FogWorldVisibility.Instance.CanObserve(host.Faction, transform.position);
            if (fireRoot.activeSelf != presented) fireRoot.SetActive(presented);
        }

        private void CreateFireEmitters()
        {
            fireRoot = new GameObject("BuildingDamageFire");
            fireRoot.transform.SetParent(transform, false);
            Sprite[][] sources = { catalog.Fire01, catalog.Fire02, catalog.Fire03 };
            BuildingPresentationProfile profile = BuildingPresentationProfiles.For(host.Type);
            fires = new SpriteLoopPlayer[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var emitter = new GameObject("Fire_0" + (i + 1));
                emitter.transform.SetParent(fireRoot.transform, false);
                Vector2 anchor = profile.FireAnchor(i);
                emitter.transform.localPosition = new Vector3(anchor.x, anchor.y, -0.05f);
                emitter.transform.localScale = Vector3.one * profile.FireScale;
                fires[i] = emitter.AddComponent<SpriteLoopPlayer>();
                fires[i].Configure(sources[i], FramesPerSecond, 17, i * 0.07f);
            }
            fireRoot.SetActive(false);
        }

        private void Refresh()
        {
            if (host == null || fireRoot == null || destroyed) return;
            bool shouldBurn = host.IsAlive && host.IsDefenseReady && host.MaxHP > 0
                && host.CurrentHP / host.MaxHP < host.FireThreshold;
            burning = shouldBurn;
            if (!shouldBurn) fireRoot.SetActive(false);
        }

        private void HandleDestroyed()
        {
            if (destroyed) return;
            destroyed = true;
            burning = false;
            if (fireRoot != null) fireRoot.SetActive(false);
            if (catalog != null && host != null
                && FogWorldVisibility.ShouldPresentDeath(host.Faction, transform.position))
                BuildingExplosionVfx.Spawn(transform.position, host.Type, host.Faction, catalog);
        }

    }

    public sealed class SpriteLoopPlayer : MonoBehaviour
    {
        private Sprite[] frames;
        private SpriteRenderer visual;
        private float frameDuration;
        private float elapsed;

        public void Configure(Sprite[] source, float fps, int sortingOrder, float phase)
        {
            frames = source ?? System.Array.Empty<Sprite>();
            frameDuration = 1f / Mathf.Max(1f, fps);
            elapsed = Mathf.Max(0f, phase);
            visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sortingOrder = sortingOrder;
            if (frames.Length > 0)
                visual.sprite = frames[Mathf.FloorToInt(elapsed / frameDuration) % frames.Length];
        }

        private void Update()
        {
            if (visual == null || frames == null || frames.Length == 0) return;
            elapsed += Time.deltaTime;
            visual.sprite = frames[Mathf.FloorToInt(elapsed / frameDuration) % frames.Length];
        }
    }

    public sealed class BuildingExplosionVfx : MonoBehaviour
    {
        private const float SecondDelay = 0.06f;
        private Sprite[] first;
        private Sprite[] second;
        private SpriteRenderer firstVisual;
        private SpriteRenderer secondVisual;
        private float elapsed;
        private Faction faction;

        public static void Spawn(Vector3 position, BuildingType type, Faction owner,
            BuildingPresentationCatalog source)
        {
            if (source == null || source.Explosion01.Length == 0
                || source.Explosion02.Length == 0) return;
            BuildingPresentationProfile profile = BuildingPresentationProfiles.For(type);
            var root = new GameObject("BuildingExplosionVfx");
            root.transform.position = new Vector3(position.x, position.y, -0.1f);
            root.transform.localScale = Vector3.one * profile.ExplosionScale;
            BuildingExplosionVfx effect = root.AddComponent<BuildingExplosionVfx>();
            effect.faction = owner;
            effect.Configure(source, profile);
        }

        private void Configure(BuildingPresentationCatalog source,
            BuildingPresentationProfile profile)
        {
            first = source.Explosion01;
            second = source.Explosion02;
            firstVisual = Make("Explosion_01", profile.ExplosionOffset0);
            secondVisual = Make("Explosion_02", profile.ExplosionOffset1);
            firstVisual.sprite = first[0];
            secondVisual.enabled = false;
        }

        private SpriteRenderer Make(string name, Vector2 offset)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 18;
            return renderer;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            bool presented = FogWorldVisibility.Instance == null
                || FogWorldVisibility.Instance.CanObserve(faction, transform.position);
            int firstFrame = Mathf.FloorToInt(elapsed * BuildingDamagePresentation.FramesPerSecond);
            firstVisual.enabled = presented && firstFrame < first.Length;
            if (firstFrame < first.Length) firstVisual.sprite = first[firstFrame];

            float secondTime = elapsed - SecondDelay;
            if (secondTime >= 0f)
            {
                int secondFrame = Mathf.FloorToInt(
                    secondTime * BuildingDamagePresentation.FramesPerSecond);
                secondVisual.enabled = presented && secondFrame < second.Length;
                if (secondFrame < second.Length) secondVisual.sprite = second[secondFrame];
            }
            if (firstFrame >= first.Length
                && secondTime >= second.Length / BuildingDamagePresentation.FramesPerSecond)
                Destroy(gameObject);
        }
    }
}
