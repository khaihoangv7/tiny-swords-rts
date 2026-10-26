#if UNITY_EDITOR
using System;
using System.Reflection;
using TinyKingdom.Economy;
using TinyKingdom.UI;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.Debugging
{
    // Editor-only driver for the Phase 7E.7 batch fixture. The Editor update
    // callback can stop during headless Play Mode, so advance it from game time.
    public sealed class Phase7E7GoldLogisticsPlayDriver : MonoBehaviour
    {
        private const string Pending = "Phase7E7.GoldLogistics.PlayValidation";
        private MethodInfo tick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isBatchMode || EditorPrefs.GetInt(Pending, -1)
                != System.Diagnostics.Process.GetCurrentProcess().Id) return;
            // These HUD presenters require an interactive canvas and are not
            // part of the headless Gold logistics fixture.
            foreach (TopLeftHudPresenter hud in FindObjectsByType<TopLeftHudPresenter>())
                hud.enabled = false;
            foreach (ControlGroupHudPresenter hud
                in FindObjectsByType<ControlGroupHudPresenter>())
                hud.enabled = false;
            foreach (SheepPresentation sheep in FindObjectsByType<SheepPresentation>())
                sheep.enabled = false;
            new GameObject("Phase7E7GoldLogisticsPlayDriver")
                .AddComponent<Phase7E7GoldLogisticsPlayDriver>();
        }

        private void Awake()
        {
            Type validator = Type.GetType("TinyKingdom.EditorTools."
                + "Phase7E7GoldLogisticsValidation, Assembly-CSharp-Editor");
            tick = validator?.GetMethod("Tick",
                BindingFlags.Static | BindingFlags.NonPublic);
        }

        private void Update() => tick?.Invoke(null, null);
    }
}
#endif
