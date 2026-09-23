using System;
using UnityEngine;

namespace TinyKingdom.Presentation
{
    [Serializable]
    public sealed class SpriteFrameSet
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        public Sprite[] Frames => frames;

#if UNITY_EDITOR
        public void SetFrames(Sprite[] value) => frames = value ?? Array.Empty<Sprite>();
#endif
    }

    [CreateAssetMenu(menuName = "Tiny Kingdom/Presentation Asset Catalog")]
    public sealed class PresentationAssetCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] sheepIdle = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] sheepGrass = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] sheepMove = Array.Empty<Sprite>();
        [SerializeField] private SpriteFrameSet[] treeAnimations = Array.Empty<SpriteFrameSet>();
        [SerializeField] private Sprite[] stumpSprites = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] goldSprites = Array.Empty<Sprite>();
        [SerializeField] private SpriteFrameSet[] goldHighlights = Array.Empty<SpriteFrameSet>();
        [SerializeField] private Sprite[] deadFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite rallyFlag;

        public Sprite[] SheepIdle => sheepIdle;
        public Sprite[] SheepGrass => sheepGrass;
        public Sprite[] SheepMove => sheepMove;
        public Sprite[] DeadFrames => deadFrames;
        public Sprite RallyFlag => rallyFlag;
        public Sprite[] TreeFrames(int index)
            => index >= 0 && index < treeAnimations.Length && treeAnimations[index] != null
                ? treeAnimations[index].Frames : Array.Empty<Sprite>();
        public Sprite Tree(int index) => TreeFrames(index).Length > 0 ? TreeFrames(index)[0] : null;
        public Sprite Stump(int index) => At(stumpSprites, index);
        public Sprite Gold(int index) => At(goldSprites, index);
        public Sprite[] GoldHighlight(int index)
            => index >= 0 && index < goldHighlights.Length && goldHighlights[index] != null
                ? goldHighlights[index].Frames : Array.Empty<Sprite>();

        public static PresentationAssetCatalog Load()
            => Resources.Load<PresentationAssetCatalog>("Presentation/Phase5C21Presentation");

        private static Sprite At(Sprite[] source, int index)
            => source != null && index >= 0 && index < source.Length ? source[index] : null;

#if UNITY_EDITOR
        public void SetData(Sprite[] idle, Sprite[] grass, Sprite[] move,
            SpriteFrameSet[] trees, Sprite[] stumps, Sprite[] gold, SpriteFrameSet[] highlights,
            Sprite[] death)
        {
            sheepIdle = idle;
            sheepGrass = grass;
            sheepMove = move;
            treeAnimations = trees;
            stumpSprites = stumps;
            goldSprites = gold;
            goldHighlights = highlights;
            deadFrames = death;
        }

        public void SetRallyFlag(Sprite value) => rallyFlag = value;
#endif
    }
}
