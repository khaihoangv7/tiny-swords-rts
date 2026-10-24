using System;
using UnityEngine;

namespace TinyKingdom.Debugging
{
    public static class CheatAccess
    {
        private const string Preference = "TinyKingdom.AllowCheats";
        private static bool? allowed;
        public static event Action<bool> Changed;

        public static bool Allowed
        {
            get => allowed ??= PlayerPrefs.GetInt(Preference, 0) != 0;
            set
            {
                allowed = value;
                PlayerPrefs.SetInt(Preference, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) DebugObserverView.SetEnabled(false);
                Changed?.Invoke(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCachedPreference()
        {
            allowed = null;
            Changed = null;
        }
    }
}
