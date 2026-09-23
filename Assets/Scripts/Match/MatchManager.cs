using System;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Match
{
    public enum MatchState { Preparing, Playing, Ended }
    public enum MatchResult { None, BlueVictory, RedVictory }

    // Gameplay authority for the match lifecycle. Presentation observes this
    // service; it never decides the winner.
    public sealed class MatchManager : MonoBehaviour
    {
        [SerializeField] private Faction humanFaction = Faction.Blue;
        [SerializeField, Min(0f)] private float resultPresentationDelay = 0.85f;

        private CastleDropOff blueMainCastle;
        private CastleDropOff redMainCastle;

        public static MatchManager Instance { get; private set; }
        public static bool AllowsGameplay => Instance == null
            || Instance.State == MatchState.Playing;

        public MatchState State { get; private set; } = MatchState.Preparing;
        public MatchResult Result { get; private set; } = MatchResult.None;
        public bool IsMatchEnded => State == MatchState.Ended;
        public Faction HumanFaction => humanFaction;
        public Faction? WinningFaction => Result switch
        {
            MatchResult.BlueVictory => Faction.Blue,
            MatchResult.RedVictory => Faction.Red,
            _ => null
        };
        public Faction? LosingFaction => Result switch
        {
            MatchResult.BlueVictory => Faction.Red,
            MatchResult.RedVictory => Faction.Blue,
            _ => null
        };
        public float ResultPresentationDelay => resultPresentationDelay;
        public CastleDropOff BlueMainCastle => blueMainCastle;
        public CastleDropOff RedMainCastle => redMainCastle;
        public bool HasBothMainCastles => blueMainCastle != null && redMainCastle != null;

        public event Action<MatchResult> MatchEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active MatchManager is allowed.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            Unsubscribe(blueMainCastle, HandleBlueCastleDestroyed);
            Unsubscribe(redMainCastle, HandleRedCastleDestroyed);
            if (Instance == this) Instance = null;
        }

        public void Configure(Faction playerFaction, float presentationDelay = 0.85f)
        {
            humanFaction = playerFaction;
            resultPresentationDelay = Mathf.Max(0f, presentationDelay);
        }

        public bool RegisterMainCastle(CastleDropOff castle)
        {
            if (castle == null || castle.Type != BuildingType.Castle || State == MatchState.Ended)
                return false;

            if (castle.Faction == Faction.Blue)
            {
                if (blueMainCastle != null && blueMainCastle != castle) return false;
                if (blueMainCastle == null)
                {
                    blueMainCastle = castle;
                    blueMainCastle.DefenseHostDestroyed += HandleBlueCastleDestroyed;
                }
            }
            else
            {
                if (redMainCastle != null && redMainCastle != castle) return false;
                if (redMainCastle == null)
                {
                    redMainCastle = castle;
                    redMainCastle.DefenseHostDestroyed += HandleRedCastleDestroyed;
                }
            }

            if (HasBothMainCastles && State == MatchState.Preparing)
                State = MatchState.Playing;
            return true;
        }

        public static MatchResult ResultForDestroyedCastle(Faction destroyedFaction)
            => destroyedFaction == Faction.Red
                ? MatchResult.BlueVictory : MatchResult.RedVictory;

        private void HandleBlueCastleDestroyed() => Resolve(Faction.Blue);
        private void HandleRedCastleDestroyed() => Resolve(Faction.Red);

        private void Resolve(Faction destroyedFaction)
        {
            // The first authoritative Main Castle death locks the result. This
            // also deterministically handles repeated or same-frame callbacks.
            if (State == MatchState.Ended) return;
            Result = ResultForDestroyedCastle(destroyedFaction);
            State = MatchState.Ended;
            MatchEnded?.Invoke(Result);
        }

        private static void Unsubscribe(CastleDropOff castle, Action callback)
        {
            if (castle != null) castle.DefenseHostDestroyed -= callback;
        }
    }
}
