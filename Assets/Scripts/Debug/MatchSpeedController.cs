using System;
using TinyKingdom.Match;
using TinyKingdom.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyKingdom.Debugging
{
    // Presentation-only observer state. Gameplay visibility must never query this flag.
    public static class DebugObserverView
    {
        public static bool IsEnabled { get; private set; }
        public static event Action<bool> Changed;

        public static void SetEnabled(bool enabled)
        {
            if (IsEnabled == enabled) return;
            IsEnabled = enabled;
            Changed?.Invoke(IsEnabled);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsEnabled = false;
            Changed = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MatchSpeedController : MonoBehaviour
    {
        public const float DefaultSpeed = 1f;
        private static readonly float[] SpeedValues = { 1f, 2f, 5f, 10f };

        private float baselineFixedDeltaTime;
        private float desiredSpeed = DefaultSpeed;
        private MatchManager subscribedMatch;

        public static MatchSpeedController Instance { get; private set; }
        public static System.Collections.Generic.IReadOnlyList<float> SupportedSpeeds
            => SpeedValues;
        public float DesiredSpeed => desiredSpeed;
        public float BaselineFixedDeltaTime => baselineFixedDeltaTime;

        public event Action<float> SpeedChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one active MatchSpeedController is allowed.", this);
                enabled = false;
                return;
            }

            Instance = this;
            baselineFixedDeltaTime = Mathf.Max(0.0001f, Time.fixedDeltaTime);
            ApplySpeed(DefaultSpeed, true);
        }

        private void Update()
        {
            ResolveMatch();
            if (subscribedMatch != null && subscribedMatch.State == MatchState.Ended)
            {
                if (!Mathf.Approximately(desiredSpeed, DefaultSpeed))
                    ApplySpeed(DefaultSpeed, true);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (GameInputBindings.WasPressed(GameAction.Speed1)) SetSpeed(1f);
            else if (GameInputBindings.WasPressed(GameAction.Speed2)) SetSpeed(2f);
            else if (GameInputBindings.WasPressed(GameAction.Speed5)) SetSpeed(5f);
            else if (GameInputBindings.WasPressed(GameAction.Speed10)) SetSpeed(10f);
        }

        public bool SetSpeed(float speed)
        {
            if (!IsSupported(speed)) return false;
            ResolveMatch();
            if (subscribedMatch != null && subscribedMatch.State == MatchState.Ended)
                return false;
            ApplySpeed(speed, false);
            return true;
        }

        public static bool IsSupported(float speed)
        {
            for (int i = 0; i < SpeedValues.Length; i++)
                if (Mathf.Approximately(SpeedValues[i], speed)) return true;
            return false;
        }

        private void ResolveMatch()
        {
            MatchManager current = MatchManager.Instance;
            if (current == subscribedMatch) return;
            if (subscribedMatch != null) subscribedMatch.MatchEnded -= HandleMatchEnded;
            subscribedMatch = current;
            if (subscribedMatch != null) subscribedMatch.MatchEnded += HandleMatchEnded;
        }

        private void HandleMatchEnded(MatchResult _)
            => ApplySpeed(DefaultSpeed, true);

        private void ApplySpeed(float speed, bool force)
        {
            if (!force && Mathf.Approximately(desiredSpeed, speed)
                && Mathf.Approximately(Time.timeScale, speed)) return;

            desiredSpeed = speed;
            Time.timeScale = speed;
            // Keep the fixed-step workload near its baseline real-time cadence.
            // The current project has no FixedUpdate/Rigidbody gameplay authority.
            Time.fixedDeltaTime = baselineFixedDeltaTime * speed;
            SpeedChanged?.Invoke(desiredSpeed);
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            RestoreBaseline();
        }

        private void OnDestroy()
        {
            if (subscribedMatch != null) subscribedMatch.MatchEnded -= HandleMatchEnded;
            if (Instance != this) return;
            RestoreBaseline();
            Instance = null;
        }

        private void RestoreBaseline()
        {
            desiredSpeed = DefaultSpeed;
            Time.timeScale = DefaultSpeed;
            if (baselineFixedDeltaTime > 0f)
                Time.fixedDeltaTime = baselineFixedDeltaTime;
        }
    }
}
