using TinyKingdom.Units;

namespace TinyKingdom.AI
{
    public readonly struct CommanderDecisionContext
    {
        public CommanderDecisionContext(Faction faction, float time,
            FactionKnowledge knowledge, FactionCommandService commands)
        {
            Faction = faction;
            Time = time;
            Knowledge = knowledge;
            Commands = commands;
        }

        public Faction Faction { get; }
        public float Time { get; }
        public FactionKnowledge Knowledge { get; }
        public FactionCommandService Commands { get; }
    }
}
