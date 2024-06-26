﻿using System;
using System.Collections.Generic;
using System.Numerics;
using DailyDuty.Localization;
using KamiLib.Configuration;
using KamiLib.Drawing;

namespace DailyDuty.DataModels;

[Flags]
public enum TimerStyle
{
    Labelled = 0b10000,
    Days = 0b01000,
    Hours = 0b00100,
    Minutes = 0b00010,
    Seconds = 0b00001,

    Human = Labelled | Days | Hours | Minutes | Seconds,
    Full = Days | Hours | Minutes | Seconds,
    NoSeconds = Days | Hours | Minutes,
}

public class TimerSettings
{
    public Setting<TimerStyle> TimerStyle = new(DataModels.TimerStyle.Human);
    public Setting<Vector4> BackgroundColor = new(Colors.Black);
    public Setting<Vector4> ForegroundColor = new(Colors.Purple);
    public Setting<Vector4> TextColor = new(Colors.White);
    public Setting<Vector4> TimeColor = new(Colors.White);
    public Setting<int> Size = new(200);
    public Setting<bool> StretchToFit = new(true);
    public Setting<bool> UseCustomName = new(false);
    public Setting<string> CustomName = new(string.Empty);
    public Setting<bool> HideLabel = new(false);
    public Setting<bool> HideTime = new(false);
}

public static class TimerStyleExtensions
{
    public static string GetLabel(this TimerStyle style)
    {
        return style switch
        {
            TimerStyle.Human => Strings.Timers_HumanStyle,
            TimerStyle.Full => Strings.Timers_FullStyle,
            TimerStyle.NoSeconds => Strings.Timers_NoSecondsStyle,
            _ => string.Empty
        };
    }

    public static IEnumerable<TimerStyle> GetConfigurableStyles()
    {
        return new List<TimerStyle>
        {
            TimerStyle.Human,
            TimerStyle.Full,
            TimerStyle.NoSeconds,
        };
    }
}
