using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.Terrain
{
    // Visual shoreline effect only. The navigation grid never reads this tile.
    public sealed class AnimatedFoamTile : TileBase
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0.01f)] private float framesPerSecond = 10f;

        public void Configure(Sprite[] sprites, float speed)
        {
            frames = sprites;
            framesPerSecond = speed;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
            ref TileAnimationData tileAnimationData)
        {
            if (frames == null || frames.Length < 2)
                return false;

            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = framesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
