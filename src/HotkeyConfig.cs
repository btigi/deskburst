namespace DeskBurst
{
    public class HotkeyConfig
    {
        public ModifierConfig Modifiers { get; set; } = new();
        public string Key { get; set; } = "F";

        public class ModifierConfig
        {
            public bool Control { get; set; } = true;
            public bool Alt { get; set; } = true;
            public bool Shift { get; set; } = false;
            public bool Windows { get; set; } = false;
        }
    }
} 