using System.Linq;
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

    public static Color WarnRed { get; } = new(0.85f, 0.35f, 0.4f);


    // Configurables

    public static Configurable<bool> ShowMilliseconds { get; } = Instance.config.Bind(nameof(ShowMilliseconds), true, new ConfigurableInfo(
        "When checked, IGT timer will show milliseconds. This is purely visual and time is tracked the same regardless. Does not affect timers shown in menus which depend on speedrun verification being enabled instead.",
        null, "", "Show IGT Milliseconds?"));

    public static Configurable<bool> PreventTimerFading { get; } = Instance.config.Bind(nameof(PreventTimerFading), false, new ConfigurableInfo(
        "When checked, the IGT won't fade out, making it more visible all the time.",
        null, "", "Prevent Timer Fading?"));

    public static Configurable<bool> ShowCompletedAndLost { get; } = Instance.config.Bind(nameof(ShowCompletedAndLost), false, new ConfigurableInfo(
        "When checked, shows completed time (cycles where the player survived) and lost time (cycles where the player died) on the select menu.",
        null, "", "Show Completed\n& Lost Time?"));

    public static Configurable<bool> ShowOldTimer { get; } = Instance.config.Bind(nameof(ShowOldTimer), false, new ConfigurableInfo(
        "When checked, displays the old IGT below the new one in game and beside on the select screen.",
        null, "", "Show Legacy Timer?"));
 
    public static Configurable<bool> ShowFixedUpdateTimer { get; } = Instance.config.Bind(nameof(ShowFixedUpdateTimer), false, new ConfigurableInfo(
        "(CURRENTLY BROKEN)\n" + "When checked, shows a timer that updates in the fixed update loop. In theory, this accounts for lag across systems. However, it is not recommended to use this as it is affected by glitches that cause dropped frames.",
        null, "", "Show Lag\nCompensating Timer?"));

    public static Configurable<bool> ShowTimerInSleepScreen { get; } = Instance.config.Bind(nameof(ShowTimerInSleepScreen), false, new ConfigurableInfo(
        "When checked, the speedrun timer will be shown in the sleep screen.",
        null, "", "Show Timer in\nSleep Screen?"));

    public static Configurable<bool> ShowTotTime { get; } = Instance.config.Bind(nameof(ShowTotTime), false, new ConfigurableInfo(
        "When checked, the totTime variable (relevant for several calculations) will be displayed below the new one in game and beside on the select screen.",
        null, "", "Show totTime?"));


    public static readonly string[] TimerPositions =
    [
        "Top (Default)",
        "Top Left",
        "Top Right",
        "Bottom Left",
        "Bottom Right",
        "Bottom"
    ];

    public static Configurable<string> TimerPosition { get; } = Instance.config.Bind(nameof(TimerPosition), TimerPositions[0], new ConfigurableInfo(
        "Allows the position of the IGT to be changed between several presets.",
        null, "", "Timer Position"));


    public static Configurable<Color> TimerColor { get; } = Instance.config.Bind(nameof(TimerColor), Color.white, new ConfigurableInfo(
        "...",
        null, "", "Timer Color"));
    

    private const int NUMBER_OF_TABS = 1;

    public override void Initialize()
    {
        base.Initialize();

        Tabs = new OpTab[NUMBER_OF_TABS];
        var tabIndex = -1;

        AddTab(ref tabIndex, "General");

        AddNewLine(-1);

        AddCheckBox(ShowMilliseconds);
        AddCheckBox(PreventTimerFading);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(ShowTimerInSleepScreen);
        AddCheckBox(ShowCompletedAndLost);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(ShowOldTimer);
        AddCheckBox(ShowTotTime);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(ShowFixedUpdateTimer);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddNewLine(1);
        DrawBox(ref Tabs[tabIndex]);


        AddNewLine(3);

        var timerPositions = TimerPositions.Select(pos => new ListItem(pos, pos)).ToList();

        AddTextLabel("Timer Position");
        DrawTextLabels(ref Tabs[tabIndex], new Vector2(120.0f, 0.0f));

        AddNewLine(1);
        AddComboBox(TimerPosition, timerPositions, "");
        DrawComboBoxes(ref Tabs[tabIndex], 80.0f);


        var offset = new Vector2(-125.0f, -150.0f);

        var timerColor = new OpColorPicker(TimerColor, new Vector2(225f + offset.x, 159.0f + offset.y));
        Tabs[tabIndex].AddItems(timerColor, new OpLabel(new Vector2(280.0f + offset.x, 317.0f + offset.y), new Vector2(150.0f + offset.x, 16.0f + offset.y), TimerColor.info.Tags[0].ToString()));


        if (GetConfigurable(ShowFixedUpdateTimer, out OpCheckBox checkBox))
        {
            checkBox.colorEdge = WarnRed;
        }

        if (GetLabel(ShowFixedUpdateTimer, out var label))
        {
            label.color = WarnRed;
        }
    }
}
