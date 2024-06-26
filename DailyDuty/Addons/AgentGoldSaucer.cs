﻿using System;
using DailyDuty.System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using KamiLib.Hooking;

namespace DailyDuty.Addons;

public unsafe class GoldSaucerEventArgs : EventArgs
{
    public GoldSaucerEventArgs(int* data, byte eventID)
    {
        Data = data;
        EventID = eventID;
    }
    
    public int* Data;
    public byte EventID;
}

public unsafe class AgentGoldSaucer : IDisposable
{
    private static AgentGoldSaucer? _instance;
    public static AgentGoldSaucer Instance => _instance ??= new AgentGoldSaucer();
    
    [Signature("E8 ?? ?? ?? ?? 80 A7 ?? ?? ?? ?? ?? 48 8D 8F ?? ?? ?? ?? 44 89 AF", DetourName = nameof(ProcessNetworkPacket))]
    private readonly Hook<Delegates.Other.GoldSaucerUpdate>? goldSaucerUpdateHook = null;

    public event EventHandler<GoldSaucerEventArgs>? GoldSaucerUpdate;

    private AgentGoldSaucer()
    {
        SignatureHelper.Initialise(this);

        AddonManager.AddAddon(this);
        
        goldSaucerUpdateHook?.Enable();
    }

    public void Dispose()
    {
        goldSaucerUpdateHook?.Dispose();
    }

    private void* ProcessNetworkPacket(void* a1, byte* a2, uint a3, ushort a4, void* a5, int* data, byte eventID)
    {
        Safety.ExecuteSafe(() =>
        {
            GoldSaucerUpdate?.Invoke(this, new GoldSaucerEventArgs(data, eventID));
        });

        return goldSaucerUpdateHook!.Original(a1, a2, a3, a4, a5, data, eventID);
    }
}