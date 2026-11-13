using TinyKingdom.Match;
using TinyKingdom.Units;

namespace TinyKingdom.AI
{
    public enum RlEpisodeEnd { None, Termination, Truncation, Abort }

    // Session bookkeeping only. World reset belongs to the scene bootstrap;
    // this type never pretends that clearing counters resets a running match.
    public sealed class RlEpisode
    {
        public int Seed { get; private set; }
        public int DecisionCount { get; private set; }
        public int LastActionId { get; private set; } = -1;
        public RlEpisodeEnd EndKind { get; private set; }
        public float TerminalReward { get; private set; }
        public bool Active { get; private set; }

        public void Start(int seed)
        {
            Seed = seed;
            DecisionCount = 0;
            LastActionId = -1;
            EndKind = RlEpisodeEnd.None;
            TerminalReward = 0f;
            Active = true;
        }

        public void RecordDecision(int actionId)
        {
            if (!Active) return;
            LastActionId = actionId;
            DecisionCount++;
        }

        public bool EndMatch(Faction owner, MatchResult result)
        {
            if (!Active || result == MatchResult.None) return false;
            TerminalReward = result switch
            {
                MatchResult.BlueVictory => owner == Faction.Blue ? 1f : -1f,
                MatchResult.RedVictory => owner == Faction.Red ? 1f : -1f,
                _ => 0f
            };
            EndKind = RlEpisodeEnd.Termination;
            Active = false;
            return true;
        }

        public bool Truncate()
        {
            if (!Active) return false;
            TerminalReward = 0f;
            EndKind = RlEpisodeEnd.Truncation;
            Active = false;
            return true;
        }

        public bool EndDraw()
        {
            if (!Active) return false;
            TerminalReward = 0f;
            EndKind = RlEpisodeEnd.Termination;
            Active = false;
            return true;
        }

        public bool Abort()
        {
            if (!Active) return false;
            TerminalReward = 0f;
            EndKind = RlEpisodeEnd.Abort;
            Active = false;
            return true;
        }
    }
}
