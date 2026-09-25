using TinyKingdom.Units;

namespace TinyKingdom.AI
{
    public interface ICommander
    {
        Faction Faction { get; }
        bool CommanderEnabled { get; }
        void Think(CommanderDecisionContext context);
    }
}
