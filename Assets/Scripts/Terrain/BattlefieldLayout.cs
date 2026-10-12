using System;
using TinyKingdom.Economy;
using UnityEngine;

namespace TinyKingdom.Terrain
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Battlefield Layout")]
    public sealed class BattlefieldLayout : ScriptableObject
    {
        public const int Width = 60;
        public const int Height = 44;
        public static readonly Vector2 BlueCastleStart = new(13f, 19.5f);
        public static readonly Vector2 RedCastleStart = new(47f, 35.5f);
        public Vector2 blueCastle = BlueCastleStart;
        public Vector2 redCastle = RedCastleStart;
        public Vector2[] bluePawns;
        public Vector2[] redPawns;
        public PawnAnimationSet redPawnAnimations;
        public Vector2Int mineOrigin = new(29, 21);
        public BattlefieldResource[] resources;

        // Hand-authored shoreline, shared by the editor painter and navigation baker.
        // Terrain is still baked into separate visual and logical assets.
        public static bool IsLand(int x, int y)
        {
            if (y < 2 || y > Height-3) return false;
            if (IsPond(x,y)) return false;
            int edge = Mathf.Min(y - 2, Height-3-y);
            int inset = edge == 0 ? 7 : edge == 1 ? 4 : edge <= 4 ? 3 : 2;
            return x >= inset && x <= Width-1-inset;
        }

        public static bool IsPond(int x,int y)
            => x>=27 && x<=32 && ((y>=36 && y<=38) || (y>=5 && y<=7));

        public static bool IsShoreline(int x, int y) => IsLand(x, y)
            && (!IsLand(x - 1, y) || !IsLand(x + 1, y)
                || !IsLand(x, y - 1) || !IsLand(x, y + 1));
    }

    [Serializable]
    public struct BattlefieldResource
    {
        public string name;
        public ResourceType type;
        public Vector2 position;
        public int variant;
    }
}
