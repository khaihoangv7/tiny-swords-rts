using System;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TinyKingdom.EditorTools
{
    public static class UnitPresentationValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Unit Presentation Assets")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
            foreach (string faction in new[] { "Blue", "Red" })
            foreach (string name in new[] { "Warrior", "Archer", "Lancer" })
            {
                string path = $"Assets/UnitAnimations/{faction}{name}.asset";
                UnitAnimationSet set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(path);
                Require(set != null, $"Missing animation set {path}");
                int idle = name switch { "Warrior" => 8, "Archer" => 6, _ => 12 };
                Require(set.Idle.Length == idle && set.Run.Length == (name == "Archer" ? 4 : 6),
                    $"Wrong Idle/Run frames: {path}");
                Require(set.AttackFor(Vector2.right).Length == (name == "Archer" ? 8 : name == "Warrior" ? 4 : 3),
                    $"Wrong Attack frames: {path}");
                if (name == "Archer")
                    Require(Mathf.Abs(set.AttackFramesPerSecond - 6.666667f) < 0.001f,
                        $"Archer Shoot playback no longer matches the 0.7-second release: {path}");
                if (name == "Warrior")
                    Require(set.Guard.Length == 6
                        && set.AttackAlternate.Length == 4,
                        $"Warrior Guard or alternate attack frames missing: {path}");
                foreach (Sprite sprite in set.Idle.Concat(set.Run).Concat(set.AttackFor(Vector2.right)))
                    Require(sprite != null, $"Missing sprite in {path}");
                if (name == "Warrior")
                    foreach (Sprite sprite in set.AttackAlternate)
                        Require(sprite != null, $"Missing alternate attack sprite in {path}");
                if (name == "Lancer")
                    foreach (Vector2 direction in new[] { Vector2.down, new Vector2(1, -1),
                        Vector2.up, new Vector2(1, 1) })
                        Require(set.AttackFor(direction).Length == 3, $"Directional attack missing: {path}");

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Units/{faction}{name}Test.prefab");
                Require(prefab != null && prefab.GetComponent<UnitAnimator>() != null
                    && prefab.GetComponent<DamageTextEmitter>() != null
                    && (prefab.GetComponent<UnitGuard>() != null) == (name == "Warrior"),
                    $"Prefab presentation components incorrect: {faction}{name}");
                Require(new SerializedObject(prefab.GetComponent<UnitAnimator>())
                    .FindProperty("animationSet").objectReferenceValue == set,
                    $"Prefab animation reference incorrect: {faction}{name}");
            }

            foreach (string faction in new[] { "Blue", "Red" })
            foreach ((string unit, string action, int count, int cell) in new[] {
                ("Warrior", "Attack1", 4, 192), ("Warrior", "Attack2", 4, 192),
                ("Archer", "Shoot", 8, 192),
                ("Lancer", "Idle", 12, 320), ("Lancer", "Right_Attack", 3, 320) })
            {
                string stem = $"{unit}_{action}";
                string path = $"Assets/Units/{faction} Units/{unit}/{stem}.png";
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Require(frames.Length == count && frames.All(frame =>
                    frame.rect.width == cell && frame.rect.height == cell),
                    $"Broken full-cell slicing: {path}; count={frames.Length}; "
                    + string.Join(", ", frames.Take(3).Select(sprite =>
                        $"{sprite.name}:{sprite.rect.width}x{sprite.rect.height}")));
            }

            Require(CombatDamage.Calculate(18, UnitType.Lancer, UnitType.Warrior) == 27
                && CombatDamage.Calculate(18, UnitType.Lancer, UnitType.Warrior,
                    AttackDelivery.Melee, true) == 20
                && CombatDamage.Calculate(10, UnitType.Archer, UnitType.Warrior,
                    AttackDelivery.Projectile, true) == 4
                && CombatDamage.Calculate(10, UnitType.Archer, UnitType.Lancer) == 13,
                "Counter or Guard multipliers changed");

            Require(UnityEngine.Object.FindAnyObjectByType<SelectionSummaryUI>() != null
                && UnityEngine.Object.FindAnyObjectByType<EventSystem>()?.GetComponent<InputSystemUIInputModule>() != null
                && UnityEngine.Object.FindAnyObjectByType<GuardInputController>() != null,
                "Map presentation UI/Input objects missing");
            foreach (UnitController unit in UnityEngine.Object.FindObjectsByType<UnitController>())
            {
                UnitCombatData data = unit.GetComponent<HealthComponent>()?.CombatData;
                UnitAnimator animator = unit.GetComponent<UnitAnimator>();
                Require(data != null && animator != null
                    && unit.GetComponent<DamageTextEmitter>() != null
                    && (unit.GetComponent<UnitGuard>() != null) == (data.UnitType == UnitType.Warrior)
                    && new SerializedObject(animator).FindProperty("animationSet").objectReferenceValue
                        == AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(
                            $"Assets/UnitAnimations/{unit.Faction}{data.UnitType}.asset"),
                    $"Map unit missing presentation or data reference: {unit.name}");
            }
            Debug.Log("Unit presentation asset validation passed: full-frame slicing, six animation sets/prefabs, Map UI and Guard calculations. Play Mode interaction remains manual.");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
