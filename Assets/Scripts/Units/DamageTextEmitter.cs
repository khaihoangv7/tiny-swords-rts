using UnityEngine;
using TinyKingdom.Vision;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class DamageTextEmitter : MonoBehaviour
    {
        private const string Preference = "TinyKingdom.ShowDamageNumbers";
        private static bool? globalEnabled;
        private HealthComponent health;

        public static bool GlobalEnabled
        {
            get => globalEnabled ??= PlayerPrefs.GetInt(Preference, 0) != 0;
            set
            {
                globalEnabled = value;
                PlayerPrefs.SetInt(Preference, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCachedPreference() => globalEnabled = null;

        private void OnEnable()
        {
            health = GetComponent<HealthComponent>();
            health.DamageReceived += HandleDamage;
        }

        private void OnDisable()
        {
            if (health != null) health.DamageReceived -= HandleDamage;
        }

        private void HandleDamage(HealthComponent target, int actualLoss, bool guarded)
        {
            if (GlobalEnabled && (FogWorldVisibility.Instance == null
                || FogWorldVisibility.Instance.CanObserve(
                    target.GetComponent<UnitController>()?.Faction ?? Faction.Blue,
                    target.transform.position)))
                FloatingDamageText.Show(target.transform.position, actualLoss, guarded);
        }
    }
}
