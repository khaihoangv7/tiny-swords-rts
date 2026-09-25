namespace TinyKingdom.Units
{
    public enum Faction
    {
        Blue = 0,
        Red = 1,
        Goblin = 2
    }

    public static class FactionRelations
    {
        public static bool AreHostile(Faction first, Faction second)
            => first != second;

        public static bool IsPlayable(Faction faction)
            => faction is Faction.Blue or Faction.Red;
    }
}
