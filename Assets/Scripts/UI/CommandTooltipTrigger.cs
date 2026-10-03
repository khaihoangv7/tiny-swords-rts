using TinyKingdom.Buildings;
using TinyKingdom.Units;
using TinyKingdom.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyKingdom.UI
{
    public sealed class CommandTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private enum TooltipKind { None, Unit, Building, Simple }

        private CommandTooltipPresenter presenter;
        private CommandTooltipData data;
        private Object source;
        private TooltipKind kind;
        private string cachedTitle;
        private string cachedDetails;
        private string cachedHotkey;
        private bool hasCommandAudio;
        private bool commandAvailable;
        private bool playAcceptedAudioHere;

        public bool HasTooltipData => data != null;

        internal void ConfigureUnit(CommandTooltipPresenter owner, UnitCombatData definition)
        {
            presenter = owner;
            if (source == definition && kind == TooltipKind.Unit) return;
            source = definition;
            kind = TooltipKind.Unit;
            data = CommandTooltipDataBuilder.ForUnit(definition);
        }

        internal void ConfigureBuilding(CommandTooltipPresenter owner,
            BuildingData definition, string displayName)
        {
            presenter = owner;
            if (source == definition && kind == TooltipKind.Building
                && cachedTitle == displayName) return;
            source = definition;
            kind = TooltipKind.Building;
            cachedTitle = displayName;
            data = CommandTooltipDataBuilder.ForBuilding(definition, displayName);
        }

        internal void ConfigureSimple(CommandTooltipPresenter owner,
            string title, string details, string hotkey = "")
        {
            presenter = owner;
            if (source == null && kind == TooltipKind.Simple
                && cachedTitle == title && cachedDetails == details
                && cachedHotkey == hotkey) return;
            source = null;
            kind = TooltipKind.Simple;
            cachedTitle = title;
            cachedDetails = details;
            cachedHotkey = hotkey;
            data = CommandTooltipDataBuilder.Simple(title, details, hotkey);
        }

        internal void ConfigureScout(CommandTooltipPresenter owner, string details)
        {
            presenter = owner;
            if (source == null && kind == TooltipKind.Simple
                && cachedTitle == "Scout" && cachedDetails == details
                && cachedHotkey == "") return;
            source = null;
            kind = TooltipKind.Simple;
            cachedTitle = "Scout";
            cachedDetails = details;
            cachedHotkey = "";
            data = CommandTooltipDataBuilder.Scout(details);
        }

        internal void ConfigureCommandAudio(bool available, bool playAcceptedHere)
        {
            hasCommandAudio = true;
            commandAvailable = available;
            playAcceptedAudioHere = playAcceptedHere;
        }

        internal void Clear()
        {
            presenter?.Hide(this);
            source = null;
            kind = TooltipKind.None;
            cachedTitle = null;
            cachedDetails = null;
            cachedHotkey = null;
            data = null;
            hasCommandAudio = false;
            commandAvailable = false;
            playAcceptedAudioHere = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (data != null) presenter?.Show(this, data);
        }

        public void OnPointerExit(PointerEventData eventData) => presenter?.Hide(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!hasCommandAudio || eventData.button != PointerEventData.InputButton.Left) return;
            if (!commandAvailable)
                GameplayAudioService.RequestUi(GameplayAudioEvent.CommandRejected);
            else if (playAcceptedAudioHere)
                GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick);
        }

        private void OnDisable() => presenter?.Hide(this);
        private void OnDestroy() => presenter?.Hide(this);
    }
}
