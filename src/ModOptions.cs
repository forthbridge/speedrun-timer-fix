using Menu.Remix.MixedUI;
using UnityEngine;

namespace SpeedrunTimerFix;

public sealed class ModOptions : OptionsTemplate
{
    public static ModOptions Instance { get; } = new();

    public static void RegisterOI()
    {
        if (MachineConnector.GetRegisteredOI(Plugin.MOD_ID) != Instance)
        {
            MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Instance);
        }
    }


    public static Configurable<bool> ShowMilliseconds = Instance.config.Bind(nameof(ShowMilliseconds), true, new ConfigurableInfo(
        "When checked, timers will show milliseconds. This is purely visual and time is tracked the same regardless.",
        null, "", "Show Milliseconds?"));

    public static Configurable<bool> PreventTimerFading = Instance.config.Bind(nameof(PreventTimerFading), false, new ConfigurableInfo(
        "When checked, the IGT won't fade out, making it more visible all the time.",
        null, "", "Prevent Timer Fading?"));

    public static Configurable<bool> ShowCompletedAndLost = Instance.config.Bind(nameof(ShowCompletedAndLost), false, new ConfigurableInfo(
        "When checked, shows completed time (cycles where the player survived) and lost time (cycles where the player died) on the select menu.",
        null, "", "Show Completed & Lost Time?"));


    public static Configurable<bool> ShowOldTimer = Instance.config.Bind(nameof(ShowOldTimer), false, new ConfigurableInfo(
        "When checked, displays the old IGT below the new one in game and beside on the select screen.",
        null, "", "Show Legacy Timer?"));
 
    public static Configurable<bool> ShowFixedUpdateTimer = Instance.config.Bind(nameof(ShowFixedUpdateTimer), false, new ConfigurableInfo(
        "When checked, shows a timer that updates in FixedUpdate. In theory, this accounts for lag across systems. However, it is not recommended to use this as it is affected by glitches that cause dropped frames.",
        null, "", "Show Fixed Update Timer?"));


    public static readonly Configurable<Color> TimerColor = Instance.config.Bind(nameof(TimerColor), Color.white, new ConfigurableInfo(
        "...",
        null, "", "Timer Color"));
    

    private const int NUMBER_OF_TABS = 1;

    public override void Initialize()
    {
        base.Initialize();
        Tabs = new OpTab[NUMBER_OF_TABS];
        int tabIndex = -1;

        AddTab(ref tabIndex, "General");

        AddNewLine(-1);

        AddCheckBox(ShowMilliseconds);
        AddCheckBox(PreventTimerFading);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(ShowCompletedAndLost);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(ShowOldTimer);
        AddCheckBox(ShowFixedUpdateTimer);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddNewLine(1);

        var offset = new Vector2(0.0f, -100.0f);

        var _timerColor = new OpColorPicker(TimerColor, new Vector2(225f + offset.x, 159.0f + offset.y));
        Tabs[tabIndex].AddItems(_timerColor, new OpLabel(new Vector2(225f + offset.x, 317.0f + offset.y), new Vector2(150.0f + offset.x, 16.0f + offset.y), TimerColor.info.Tags[0].ToString()));

        DrawBox(ref Tabs[tabIndex]);
    }
}