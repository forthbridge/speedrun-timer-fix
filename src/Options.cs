﻿using System.Collections.Generic;
using System.Linq;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;

namespace SpeedrunTimerFix
{
    // Based on the options script from SBCameraScroll by SchuhBaum
    // https://github.com/SchuhBaum/SBCameraScroll/blob/Rain-World-v1.9/SourceCode/MainModOptions.cs
    public class Options : OptionInterface
    {
        public static Options instance = new Options();
        private const string AUTHORS_NAME = "forthbridge";

        #region Options

        public static Configurable<bool> includeMilliseconds = instance.config.Bind("includeMilliseconds", true, new ConfigurableInfo(
            "When checked, timers will include milliseconds. Purely visual, time is tracked the same regardless.",
            null, "", "Include Milliseconds?"));

        public static Configurable<bool> dontFade = instance.config.Bind("dontFade", false, new ConfigurableInfo(
            "When checked, the timer won't fade out, making it more visible all the time.",
            null, "", "Don't Fade?"));


        public static Configurable<bool> extraTimers = instance.config.Bind("extraTimers", true, new ConfigurableInfo(
            "When checked, adds additional timing info (Completed & Lost) onto the slugcat select menu.",
            null, "", "Extra Timers?"));

        public static Configurable<bool> formatTimers = instance.config.Bind("formatTimers", true, new ConfigurableInfo(
            "When checked, timers will be formatted in Hours:Minutes:Seconds:Milliseconds. When unchecked, they will instead show frames.",
            null, "", "Format Timers?"));


        public static Configurable<bool> showOriginalTimer = instance.config.Bind("showOriginalTimer", false, new ConfigurableInfo(
            "When checked, displays the original built-in timer below the new one in game and beside on the select screen.",
            null, "", "Show Original Timer?"));



        public static Configurable<bool> fixedUpdateTimer = instance.config.Bind("fixedUpdateTimer", true, new ConfigurableInfo(
            "When checked, the timer will update within the fixed timestep (40hz Physics Update). When unchecked, will update every frame (RawUpdate).",
            null, "", "Fixed Update Timer?"));

        public static Configurable<bool> compensateFixedFramerate = instance.config.Bind("compensateFixedFramerate", true, new ConfigurableInfo(
            "When checked, considers the current fixed framerate when calculating delta time. Only affects the fixed update timer.",
            null, "", "Compensate Fixed Framerate?"));



        public static readonly Configurable<Color> timerColor = instance.config.Bind("timerColor", Color.white, new ConfigurableInfo(
            "...",
            null, "", "Timer Color"));


        #endregion

        #region Parameters

        private readonly float spacing = 20f;
        private readonly float fontHeight = 20f;
        private readonly int numberOfCheckboxes = 2;
        private readonly float checkBoxSize = 60.0f;

        private float CheckBoxWithSpacing => checkBoxSize + 0.25f * spacing;


        private Vector2 marginX = new();
        private Vector2 pos = new();

        private readonly List<float> boxEndPositions = new();

        private readonly List<Configurable<bool>> checkBoxConfigurables = new();
        private readonly List<OpLabel> checkBoxesTextLabels = new();

        private readonly List<OpLabel> textLabels = new();

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

        public override string ValidationString() => base.ValidationString() + (fixedUpdateTimer.Value ? " FIXED" : " FREE") + (fixedUpdateTimer.Value ? (compensateFixedFramerate.Value ? " COMPENSATED" : " UNCOMPENSATED") : "");

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



        #region UI Elements

        private void AddTab(ref int tabIndex, string tabName)
        {
            tabIndex++;
            Tabs[tabIndex] = new OpTab(this, tabName);
            InitializeMarginAndPos();

            AddNewLine();
            AddTextLabel(Plugin.MOD_NAME, bigText: true);
            DrawTextLabels(ref Tabs[tabIndex]);

            AddNewLine(0.5f);
            AddTextLabel("Version " + Plugin.VERSION, FLabelAlignment.Left);
            AddTextLabel("by " + AUTHORS_NAME, FLabelAlignment.Right);
            DrawTextLabels(ref Tabs[tabIndex]);

            AddNewLine();
            AddBox();
        }

        private void InitializeMarginAndPos()
        {
            marginX = new Vector2(50f, 550f);
            pos = new Vector2(50f, 600f);
        }

        private void AddNewLine(float spacingModifier = 1f)
        {
            pos.x = marginX.x;
            pos.y -= spacingModifier * spacing;
        }

        

        private void AddBox()
        {
            marginX += new Vector2(spacing, -spacing);
            boxEndPositions.Add(pos.y); // end position > start position
            AddNewLine();
        }

        private void DrawBox(ref OpTab tab)
        {
            marginX += new Vector2(-spacing, spacing);
            AddNewLine();

            float boxWidth = marginX.y - marginX.x;
            int lastIndex = boxEndPositions.Count - 1;

            tab.AddItems(new OpRect(pos, new Vector2(boxWidth, boxEndPositions[lastIndex] - pos.y)));
            boxEndPositions.RemoveAt(lastIndex);
        }



        private void AddCheckBox(Configurable<bool> configurable, string text)
        {
            checkBoxConfigurables.Add(configurable);
            checkBoxesTextLabels.Add(new OpLabel(new Vector2(), new Vector2(), text, FLabelAlignment.Left));
        }

        private void DrawCheckBoxes(ref OpTab tab) // changes pos.y but not pos.x
        {
            if (checkBoxConfigurables.Count != checkBoxesTextLabels.Count) return;

            float width = marginX.y - marginX.x;
            float elementWidth = (width - (numberOfCheckboxes - 1) * 0.5f * spacing) / numberOfCheckboxes;
            pos.y -= checkBoxSize;
            float _posX = pos.x;

            for (int checkBoxIndex = 0; checkBoxIndex < checkBoxConfigurables.Count; ++checkBoxIndex)
            {
                Configurable<bool> configurable = checkBoxConfigurables[checkBoxIndex];
                OpCheckBox checkBox = new(configurable, new Vector2(_posX, pos.y))
                {
                    description = configurable.info?.description ?? ""
                };
                tab.AddItems(checkBox);
                _posX += CheckBoxWithSpacing;

                OpLabel checkBoxLabel = checkBoxesTextLabels[checkBoxIndex];
                checkBoxLabel.pos = new Vector2(_posX, pos.y + 2f);
                checkBoxLabel.size = new Vector2(elementWidth - CheckBoxWithSpacing, fontHeight);
                tab.AddItems(checkBoxLabel);

                if (checkBoxIndex < checkBoxConfigurables.Count - 1)
                {
                    if ((checkBoxIndex + 1) % numberOfCheckboxes == 0)
                    {
                        AddNewLine();
                        pos.y -= checkBoxSize;
                        _posX = pos.x;
                    }
                    else
                    {
                        _posX += elementWidth - CheckBoxWithSpacing + 0.5f * spacing;
                    }
                }
            }

            checkBoxConfigurables.Clear();
            checkBoxesTextLabels.Clear();
        }


        private void AddTextLabel(string text, FLabelAlignment alignment = FLabelAlignment.Center, bool bigText = false)
        {
            float textHeight = (bigText ? 2f : 1f) * fontHeight;
            if (textLabels.Count == 0)
            {
                pos.y -= textHeight;
            }

            OpLabel textLabel = new(new Vector2(), new Vector2(20f, textHeight), text, alignment, bigText) // minimal size.x = 20f
            {
                autoWrap = true
            };
            textLabels.Add(textLabel);
        }

        private void DrawTextLabels(ref OpTab tab)
        {
            if (textLabels.Count == 0)
            {
                return;
            }

            float width = (marginX.y - marginX.x) / textLabels.Count;
            foreach (OpLabel textLabel in textLabels)
            {
                textLabel.pos = pos;
                textLabel.size += new Vector2(width - 20f, 0.0f);
                tab.AddItems(textLabel);
                pos.x += width;
            }

            pos.x = marginX.x;
            textLabels.Clear();
        }

        #endregion
    }
}