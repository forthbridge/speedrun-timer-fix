using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpeedrunTimerFix;

public sealed class ModOptions : OptionsTemplate
{
    public static readonly ModOptions Instance = new();

    public static readonly Color WarnRed = new(0.85f, 0.35f, 0.4f);

    public override string ValidationString() => base.ValidationString() + (fixedUpdateTimer.Value ? " FIXED" : " FREE") + (fixedUpdateTimer.Value ? (compensateFixedFramerate.Value ? " COMPENSATED" : " UNCOMPENSATED") : "");

    #region Options

    public static Configurable<bool> includeMilliseconds = Instance.config.Bind("includeMilliseconds", true, new ConfigurableInfo(
        "When checked, timers will include milliseconds. Purely visual, time is tracked the same regardless.",
        null, "", "Include Milliseconds?"));

    public static Configurable<bool> dontFade = Instance.config.Bind("dontFade", false, new ConfigurableInfo(
        "When checked, the timer won't fade out, making it more visible all the time.",
        null, "", "Don't Fade?"));


    public static Configurable<bool> extraTimers = Instance.config.Bind("extraTimers", true, new ConfigurableInfo(
        "When checked, adds additional timing info (Completed & Lost) onto the slugcat select menu.",
        null, "", "Extra Timers?"));

    public static Configurable<bool> formatTimers = Instance.config.Bind("formatTimers", true, new ConfigurableInfo(
        "When checked, timers will be formatted in Hours:Minutes:Seconds:Milliseconds. When unchecked, they will instead show frames.",
        null, "", "Format Timers?"));


    public static Configurable<bool> showOriginalTimer = Instance.config.Bind("showOriginalTimer", false, new ConfigurableInfo(
        "When checked, displays the original built-in timer below the new one in game and beside on the select screen.",
        null, "", "Show Original Timer?"));



    public static Configurable<bool> fixedUpdateTimer = Instance.config.Bind("fixedUpdateTimer", true, new ConfigurableInfo(
        "When checked, the timer will update within the fixed timestep (40hz Physics Update). When unchecked, will update every frame (RawUpdate).",
        null, "", "Fixed Update Timer?"));

    public static Configurable<bool> compensateFixedFramerate = Instance.config.Bind("compensateFixedFramerate", true, new ConfigurableInfo(
        "When checked, considers the current fixed framerate when calculating delta time. Only affects the fixed update timer.",
        null, "", "Compensate Fixed Framerate?"));



    public static readonly Configurable<Color> timerColor = Instance.config.Bind("timerColor", Color.white, new ConfigurableInfo(
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

        AddCheckBox(includeMilliseconds, (string)includeMilliseconds.info.Tags[0]);
        AddCheckBox(formatTimers, (string)formatTimers.info.Tags[0]);
        DrawCheckBoxes(ref Tabs[tabIndex]);


        AddCheckBox(extraTimers, (string)extraTimers.info.Tags[0]);
        AddCheckBox(dontFade, (string)dontFade.info.Tags[0]);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddCheckBox(showOriginalTimer, (string)showOriginalTimer.info.Tags[0]);
        DrawCheckBoxes(ref Tabs[tabIndex]);



        AddCheckBox(fixedUpdateTimer, (string)fixedUpdateTimer.info.Tags[0]);
        AddCheckBox(compensateFixedFramerate, (string)compensateFixedFramerate.info.Tags[0]);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        Vector2 offset = new(0.0f, -150.0f);

        var _timerColor = new OpColorPicker(timerColor, new Vector2(225f + offset.x, 159.0f + offset.y));
        Tabs[tabIndex].AddItems(_timerColor, new OpLabel(new Vector2(225f + offset.x, 317.0f + offset.y), new Vector2(150.0f + offset.x, 16.0f + offset.y), timerColor.info.Tags[0].ToString()));

        DrawBox(ref Tabs[tabIndex]);
    }

    public override void Update()
    {
        base.Update();

        OpCheckBox fixedUpdateTimerCheckBox = (OpCheckBox)Tabs[0].items.Where(item => item is OpCheckBox checkBox && checkBox.cfgEntry == fixedUpdateTimer).FirstOrDefault();
        OpLabel fixedUpdateTimerLabel = (OpLabel)Tabs[0].items.Where(item => item is OpLabel label && label.text == fixedUpdateTimer.info.Tags[0].ToString()).FirstOrDefault();

        fixedUpdateTimerCheckBox.colorEdge = fixedUpdateTimerCheckBox.GetValueBool() ? Color.green : Color.red;
        fixedUpdateTimerLabel.color = fixedUpdateTimerCheckBox.GetValueBool() ? Color.green : Color.red;


        OpCheckBox compensateFixedFramerateCheckBox = (OpCheckBox)Tabs[0].items.Where(item => item is OpCheckBox checkBox && checkBox.cfgEntry == compensateFixedFramerate).FirstOrDefault();
        OpLabel compensateFixedFramerateLabel = (OpLabel)Tabs[0].items.Where(item => item is OpLabel label && label.text == compensateFixedFramerate.info.Tags[0].ToString()).FirstOrDefault();

        compensateFixedFramerateCheckBox.colorEdge = compensateFixedFramerateCheckBox.GetValueBool() ? Color.green : Color.red;
        compensateFixedFramerateLabel.color = compensateFixedFramerateCheckBox.GetValueBool() ? Color.green : Color.red;
    }

}