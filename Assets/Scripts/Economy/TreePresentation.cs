using UnityEngine;

namespace TinyKingdom.Economy
{
    // Loops the verified eight-frame Tree tag; depletion swaps to the matching stump.
    public sealed class TreePresentation : MonoBehaviour
    {
        private ResourceNode node;
        private Sprite[] frames;
        private int frame;
        private float elapsed;

        public void Configure(ResourceNode source, Sprite[] animationFrames)
        {
            node = source;
            frames = animationFrames;
        }

        private void Update()
        {
            if (node == null || !node.IsAvailable || frames == null || frames.Length < 2) return;
            elapsed += Time.deltaTime;
            while (elapsed >= 0.1f)
            {
                elapsed -= 0.1f;
                frame = (frame + 1) % frames.Length;
                node.SetPresentationSprite(frames[frame]);
            }
        }
    }
}
