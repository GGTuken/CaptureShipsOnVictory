using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace CaptureShipsOnVictory
{
    /// <summary>
    /// Global mod settings exposed via Mod Configuration Menu (MCM v5).
    /// Maximum ships loot per battle is configurable; WARN: 100 is an engine limitation, may cause crash.
    /// </summary>
    internal sealed class Settings : AttributeGlobalSettings<Settings>
    {
        private int _maxShipsLootPerBattle = 25;

        public override string Id => "CaptureShipsOnVictory_Settings";
        public override string DisplayName => "Capture Ships On Victory";
        public override string FolderName => "CaptureShipsOnVictory";
        public override string FormatType => "json";

        /// <summary>
        /// Maximum number of ships the player can receive as loot after a single naval victory.
        /// WARN: 100 is an engine limitation, may cause crash.
        /// </summary>
        [SettingPropertyInteger("Max ships in loot per battle", 1, 420, Order = 0, RequireRestart = false,
            HintText = "Maximum number of ships the player can receive as loot after a naval victory. WARN: 100 is an engine limitation, may cause crash.")]
        [SettingPropertyGroup("General")]
        public int MaxShipsLootPerBattle
        {
            get => _maxShipsLootPerBattle;
            set
            {
                if (_maxShipsLootPerBattle != value)
                {
                    _maxShipsLootPerBattle = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
