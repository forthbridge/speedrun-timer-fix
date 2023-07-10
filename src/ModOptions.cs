using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;

namespace SpeedrunTimerFix;

public sealed class ModOptions : OptionsTemplate
{
    public static readonly ModOptions Instance = new();

    public static readonly Color WarnRed = new(0.85f, 0.35f, 0.4f);
    public static readonly Color ValidGreen = new(159 / 255.0f, 1.0f, 150 / 255.0f);

    public override string ValidationString() => base.ValidationString() + (CompensateFixedFramerate.Value ? " COMPENSATED" : " UNCOMPENSATED") + (LagSimulation.Value ? " LAGSIM" : "");

    #region Options

    public static Configurable<bool> IncludeMilliseconds = Instance.config.Bind(nameof(IncludeMilliseconds), true, new ConfigurableInfo(
        "When checked, timers will include milliseconds. Purely visual, time is tracked the same regardless.",
        null, "", "Include Milliseconds?"));

    public static Configurable<bool> DontFade = Instance.config.Bind(nameof(DontFade), false, new ConfigurableInfo(
        "When checked, the timer won't fade out, making it more visible all the time.",
        null, "", "Don't Fade?"));


    public static Configurable<bool> ExtraTimers = Instance.config.Bind(nameof(ExtraTimers), true, new ConfigurableInfo(
        "When checked, adds additional timing info (Completed & Lost) onto the slugcat select menu.",
        null, "", "Extra Timers?"));

    public static Configurable<bool> ShowOriginalTimer = Instance.config.Bind(nameof(ShowOriginalTimer), false, new ConfigurableInfo(
        "When checked, displays the original built-in timer below the new one in game and beside on the select screen.",
        null, "", "Show Original Timer?"));

 
    public static Configurable<bool> ShowFreeUpdateTimer = Instance.config.Bind(nameof(ShowFreeUpdateTimer), false, new ConfigurableInfo(
        "When checked, shows a timer that updates on TimeTick, same as the built-in one. It is not recommended to use this as it doesn't account for lag.",
        null, "", "Show Free Update Timer?"));

    public static Configurable<bool> FormatTimers = Instance.config.Bind(nameof(FormatTimers), true, new ConfigurableInfo(
        "When checked, timers will be formatted in Hours:Minutes:Seconds:Milliseconds. When unchecked, they will instead show frames.",
        null, "", "Format Timers?"));


    public static Configurable<bool> LagSimulation = Instance.config.Bind(nameof(LagSimulation), false, new ConfigurableInfo(
        "When checked, pressing L will simulate a lag spike.",
        null, "", "Lag Simulation?"));

    public static Configurable<bool> CompensateFixedFramerate = Instance.config.Bind(nameof(CompensateFixedFramerate), true, new ConfigurableInfo(
        "When checked, considers the current fixed framerate when calculating delta time. Only affects the fixed update timer.",
        null, "", "Compensate Fixed Framerate?"));


    public static readonly Configurable<Color> TimerColor = Instance.config.Bind(nameof(TimerColor), Color.white, new ConfigurableInfo(
        "...",
        null, "", "Timer Color"));

    #endregion

    private const int NUMBER_OF_TABS = 1;

    public override void Initialize()
    {
        base.Initialize();
        Tabs = new OpTab[NUMBER_OF_TABS];
        int tabIndex = -1;

        AddTab(ref tabIndex, "General");

        AddNewLine(-1);

        AddCheckBox(IncludeMilliseconds);
        AddCheckBox(DontFade);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(ExtraTimers);
        AddCheckBox(ShowOriginalTimer);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(LagSimulation);
        AddCheckBox(ShowFreeUpdateTimer);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(FormatTimers);
        AddCheckBox(CompensateFixedFramerate);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddNewLine(1);

        Vector2 offset = new(0.0f, -150.0f);

        var _timerColor = new OpColorPicker(TimerColor, new Vector2(225f + offset.x, 159.0f + offset.y));
        Tabs[tabIndex].AddItems(_timerColor, new OpLabel(new Vector2(225f + offset.x, 317.0f + offset.y), new Vector2(150.0f + offset.x, 16.0f + offset.y), TimerColor.info.Tags[0].ToString()));

        DrawBox(ref Tabs[tabIndex]);
    }

    public override void Update()
    {
        base.Update();


        if (GetConfigurable(FormatTimers, out OpCheckBox checkBox))
            checkBox.colorEdge = checkBox.GetValueBool() ? ValidGreen : WarnRed;

        if (GetLabel(FormatTimers, out OpLabel label))
            label.color = checkBox.GetValueBool() ? ValidGreen : WarnRed;


        if (GetConfigurable(CompensateFixedFramerate, out checkBox))
            checkBox.colorEdge = checkBox.GetValueBool() ? ValidGreen : WarnRed;

        if (GetLabel(CompensateFixedFramerate, out label))
            label.color = checkBox.GetValueBool() ? ValidGreen : WarnRed;


        if (GetConfigurable(LagSimulation, out checkBox))
            checkBox.colorEdge = !checkBox.GetValueBool() ? ValidGreen : WarnRed;

        if (GetLabel(LagSimulation, out label))
            label.color = !checkBox.GetValueBool() ? ValidGreen : WarnRed;
    }
}