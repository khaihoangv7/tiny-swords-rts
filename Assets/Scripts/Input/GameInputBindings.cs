using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyKingdom.Input
{
    public enum GameAction
    {
        AttackMove, Guard, Group1, Group2, Group3, Group4, Ungroup,
        Speed1, Speed2, Speed5, Speed10,
        PanLeft, PanRight, PanUp, PanDown,
        GroupAssign1, GroupAssign2, GroupAssign3, GroupAssign4
    }

    // One source of truth for keyboard shortcuts used by the existing input
    // controllers. Pointer commands remain with their current controllers.
    public static class GameInputBindings
    {
        private const string Prefix = "TinyKingdom.Binding.";
        private const string ShiftSuffix = ".Shift";
        private const string ControlSuffix = ".Control";
        public static bool ModalOpen { get; set; }
        public static bool Capturing { get; set; }
        public static int ActionCount => Enum.GetValues(typeof(GameAction)).Length;

        public static string Label(GameAction action) => action switch
        {
            GameAction.AttackMove => "Attack Move",
            GameAction.Guard => "Guard",
            GameAction.Group1 => "Select Group 1",
            GameAction.Group2 => "Select Group 2",
            GameAction.Group3 => "Select Group 3",
            GameAction.Group4 => "Select Group 4",
            GameAction.GroupAssign1 => "Assign Group 1",
            GameAction.GroupAssign2 => "Assign Group 2",
            GameAction.GroupAssign3 => "Assign Group 3",
            GameAction.GroupAssign4 => "Assign Group 4",
            GameAction.Ungroup => "Clear Selected Group",
            GameAction.Speed1 => "Speed 1x",
            GameAction.Speed2 => "Speed 2x",
            GameAction.Speed5 => "Speed 5x",
            GameAction.Speed10 => "Speed 10x",
            GameAction.PanLeft => "Camera Left",
            GameAction.PanRight => "Camera Right",
            GameAction.PanUp => "Camera Up",
            GameAction.PanDown => "Camera Down",
            _ => action.ToString()
        };

        public static Key Default(GameAction action) => action switch
        {
            GameAction.AttackMove => Key.A,
            GameAction.Guard => Key.G,
            GameAction.Group1 => Key.Digit1,
            GameAction.Group2 => Key.Digit2,
            GameAction.Group3 => Key.Digit3,
            GameAction.Group4 => Key.Digit4,
            GameAction.GroupAssign1 => Key.Digit1,
            GameAction.GroupAssign2 => Key.Digit2,
            GameAction.GroupAssign3 => Key.Digit3,
            GameAction.GroupAssign4 => Key.Digit4,
            GameAction.Ungroup => Key.U,
            GameAction.Speed1 => Key.F5,
            GameAction.Speed2 => Key.F6,
            GameAction.Speed5 => Key.F7,
            GameAction.Speed10 => Key.F8,
            GameAction.PanLeft => Key.LeftArrow,
            GameAction.PanRight => Key.RightArrow,
            GameAction.PanUp => Key.UpArrow,
            GameAction.PanDown => Key.DownArrow,
            _ => Key.None
        };

        public static Key Get(GameAction action)
        {
            if (IsGroupAssignment(action)
                && !PlayerPrefs.HasKey(Prefix + action))
                return Get(SelectionActionFor(action));
            int value = PlayerPrefs.GetInt(Prefix + action, (int)Default(action));
            Key key = (Key)value;
            return Enum.IsDefined(typeof(Key), key) && key != Key.None
                ? key : Default(action);
        }

        public static bool RequiresShift(GameAction action)
            => IsGroupAssignment(action)
                && !PlayerPrefs.HasKey(Prefix + action + ShiftSuffix)
                ? RequiresShift(SelectionActionFor(action))
                : PlayerPrefs.GetInt(Prefix + action + ShiftSuffix, 0) != 0;

        public static bool RequiresControl(GameAction action)
            => PlayerPrefs.GetInt(Prefix + action + ControlSuffix,
                IsGroupAssignment(action) ? 1 : 0) != 0;

        private static bool IsGroupAssignment(GameAction action)
            => action is >= GameAction.GroupAssign1 and <= GameAction.GroupAssign4;

        private static GameAction SelectionActionFor(GameAction assignment)
            => GameAction.Group1 + (assignment - GameAction.GroupAssign1);

        public static string Display(GameAction action)
        {
            Key key = Get(action);
            string label = key switch
            {
                Key.LeftArrow => "←",
                Key.RightArrow => "→",
                Key.UpArrow => "↑",
                Key.DownArrow => "↓",
                _ => key.ToString().StartsWith("Digit", StringComparison.Ordinal)
                    ? key.ToString().Substring(5) : key.ToString()
            };
            return (RequiresControl(action) ? "Ctrl+" : "")
                + (RequiresShift(action) ? "Shift+" : "") + label;
        }

        public static bool WasPressed(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return !ModalOpen && !Capturing && keyboard != null
                && keyboard[Get(action)].wasPressedThisFrame
                && ModifiersMatch(action, keyboard);
        }

        public static bool IsPressed(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return !ModalOpen && !Capturing && keyboard != null
                && keyboard[Get(action)].isPressed
                && ModifiersMatch(action, keyboard);
        }

        public static bool WasReleased(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard[Get(action)].wasReleasedThisFrame
                    || (RequiresShift(action) && keyboard[Get(action)].isPressed
                        && !keyboard.shiftKey.isPressed)
                    || (RequiresControl(action) && keyboard[Get(action)].isPressed
                        && !keyboard.ctrlKey.isPressed));
        }

        private static bool ModifiersMatch(GameAction action, Keyboard keyboard)
            => keyboard.shiftKey.isPressed == RequiresShift(action)
                && keyboard.ctrlKey.isPressed == RequiresControl(action);

        public static bool TrySet(GameAction action, Key key, out string reason)
            => TrySet(action, key, false, out reason);

        public static bool TrySet(GameAction action, Key key, bool shift,
            out string reason)
            => TrySet(action, key, shift, false, out reason);

        public static bool TrySet(GameAction action, Key key, bool shift,
            bool control, out string reason)
        {
            reason = null;
            if (!Enum.IsDefined(typeof(Key), key) || key == Key.None
                || key is Key.Escape or Key.LeftCtrl or Key.RightCtrl
                    or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
            {
                reason = "This key is reserved for system controls.";
                return false;
            }
            for (int i = 0; i < ActionCount; i++)
            {
                GameAction other = (GameAction)i;
                if (other == action || Get(other) != key
                    || RequiresShift(other) != shift
                    || RequiresControl(other) != control) continue;
                reason = $"{(control ? "Ctrl+" : "")}{(shift ? "Shift+" : "")}{key} is already assigned to {Label(other)}.";
                return false;
            }
            PlayerPrefs.SetInt(Prefix + action, (int)key);
            PlayerPrefs.SetInt(Prefix + action + ShiftSuffix, shift ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + action + ControlSuffix, control ? 1 : 0);
            PlayerPrefs.Save();
            return true;
        }

        public static void RestoreDefaults()
        {
            for (int i = 0; i < ActionCount; i++)
            {
                PlayerPrefs.DeleteKey(Prefix + (GameAction)i);
                PlayerPrefs.DeleteKey(Prefix + (GameAction)i + ShiftSuffix);
                PlayerPrefs.DeleteKey(Prefix + (GameAction)i + ControlSuffix);
            }
            PlayerPrefs.Save();
        }
    }
}
