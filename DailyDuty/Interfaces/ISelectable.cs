﻿using DailyDuty.Configuration.Enums;

namespace DailyDuty.Interfaces;

internal interface ISelectable
{
    ModuleName OwnerModuleName { get; }
    IDrawable Contents { get; }
    IModule ParentModule { get; }

    void DrawLabel();
}