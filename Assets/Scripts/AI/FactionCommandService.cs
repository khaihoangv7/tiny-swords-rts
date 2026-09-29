using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Shared gameplay authority for human/Rule-Based/future RL command sources.
    // Phase 7A exposes only the Move and Gather actions needed by its policies.
    public sealed class FactionCommandService : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Red;
        [SerializeField] private FactionKnowledge knowledge;

        public Faction Faction => faction;
        public FactionKnowledge Knowledge => knowledge;

        public void Configure(Faction owner, FactionKnowledge factionKnowledge)
        {
            faction = owner;
            knowledge = factionKnowledge;
        }

        public CommanderCommandResult TryMove(UnitController actor, Vector3 destination)
        {
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            UnitMovement movement = actor.GetComponent<UnitMovement>();
            if (movement == null) return CommanderCommandResult.RejectedInvalidActor;
            if (!movement.TryIssueMove(destination))
                return CommanderCommandResult.RejectedUnreachable;
            actor.GetComponent<PawnGatherer>()?.CancelJob();
            return CommanderCommandResult.Accepted;
        }

        public CommanderCommandResult TryGather(UnitController actor, ResourceNode target)
        {
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            PawnGatherer gatherer = actor.GetComponent<PawnGatherer>();
            if (gatherer == null) return CommanderCommandResult.RejectedWrongUnitType;
            if (target == null) return CommanderCommandResult.RejectedInvalidTarget;
            if (knowledge == null || !knowledge.IsResourceVisible(target))
                return CommanderCommandResult.RejectedNotObserved;
            if (!target.IsAvailable) return CommanderCommandResult.RejectedInvalidTarget;
            return gatherer.Gather(target)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        private CommanderCommandResult ValidateActor(UnitController actor)
        {
            if (MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing)
                return CommanderCommandResult.RejectedMatchNotPlaying;
            if (actor == null || !actor.IsAlive || !actor.IsWorldParticipant)
                return CommanderCommandResult.RejectedInvalidActor;
            if (actor.Faction != faction) return CommanderCommandResult.RejectedNotOwned;
            return CommanderCommandResult.Accepted;
        }
    }
}
