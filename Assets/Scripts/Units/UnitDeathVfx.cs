using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Units
{
    // Presentation-only playback of the verified Dead then Buried tags (frames 0-13).
    public sealed class UnitDeathVfx : MonoBehaviour
    {
        private static readonly float[] FrameDurations =
        {
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.5f,
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f
        };
        private static Sprite[] runtimeFrames;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private int frame;
        private float remaining;

        public static void Spawn(Vector3 position)
        {
            Sprite[] source = ResolveFrames();
            if (source.Length != 14) return;
            var obj = new GameObject("UnitDeathVfx");
            obj.transform.position = position + new Vector3(0f, 0.05f, 0f);
            obj.AddComponent<UnitDeathVfx>().Configure(source);
        }

        public static int RuntimeFrameCount => ResolveFrames().Length;

        private static Sprite[] ResolveFrames()
        {
            if (runtimeFrames != null && runtimeFrames.Length == 14) return runtimeFrames;
            PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
            if (catalog?.DeadFrames != null && catalog.DeadFrames.Length == 14
                && !System.Array.Exists(catalog.DeadFrames, sprite => sprite == null))
                return runtimeFrames = catalog.DeadFrames;

            TextAsset atlasBytes = Resources.Load<TextAsset>("Presentation/DeadRuntime");
            if (atlasBytes == null || atlasBytes.bytes == null) return System.Array.Empty<Sprite>();
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "DeadRuntime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!texture.LoadImage(atlasBytes.bytes, false)
                || texture.width != 896 || texture.height != 256)
            {
                Object.Destroy(texture);
                return System.Array.Empty<Sprite>();
            }

            runtimeFrames = new Sprite[14];
            for (int i = 0; i < runtimeFrames.Length; i++)
            {
                int row = i / 7;
                int column = i % 7;
                runtimeFrames[i] = Sprite.Create(texture,
                    new Rect(column * 128, (1 - row) * 128, 128, 128),
                    new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect);
                runtimeFrames[i].name = "Dead_" + i;
            }
            return runtimeFrames;
        }

        private void Configure(Sprite[] source)
        {
            frames = source;
            visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sortingOrder = 18;
            visual.sprite = frames[0];
            remaining = FrameDuration(0);
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            while (remaining <= 0f)
            {
                frame++;
                if (frame >= frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                visual.sprite = frames[frame];
                remaining += FrameDuration(frame);
            }
        }

        public static float FrameDurationAt(int index)
            => index >= 0 && index < FrameDurations.Length ? FrameDurations[index] : 0f;

        public static float RuntimeTotalDuration
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < FrameDurations.Length; i++) total += FrameDurations[i];
                return total;
            }
        }

        private static float FrameDuration(int index) => FrameDurationAt(index);
    }
}
