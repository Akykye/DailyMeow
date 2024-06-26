﻿using System;
using DailyDuty.System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiLib.Hooking;

namespace DailyDuty.Addons;

public record AOZContentResultArgs(uint CompletionType, bool Successful);

public unsafe class AddonAozContentResult : IDisposable
{
    private static AddonAozContentResult? _instance;
    public static AddonAozContentResult Instance => _instance ??= new AddonAozContentResult();
    
    [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 17 BA ?? ?? ?? ?? 4D 8B E8 4C 8B F9", DetourName = nameof(OnSetup))]
    private readonly Hook<Delegates.Addon.OnSetup>? onSetupHook = null!;

    public event EventHandler<AOZContentResultArgs>? Setup;

    private AddonAozContentResult()
    {
        SignatureHelper.Initialise(this);
        AddonManager.AddAddon(this);

        onSetupHook.Enable();
    }
    
    public void Dispose()
    {
        onSetupHook?.Dispose();
    }

    private nint OnSetup(AtkUnitBase* addon, int valueCount, AtkValue* values)
    {
        var result = onSetupHook!.Original(addon, valueCount, values);

        Safety.ExecuteSafe(() =>
        {
            Setup?.Invoke(this, new AOZContentResultArgs(values[109].UInt, values[111].Byte != 0));
            
        });

        return result;
    }
}