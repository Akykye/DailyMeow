﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DailyDuty.Configuration;
using DailyDuty.UserInterface.OverlayWindows;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using KamiLib;

namespace DailyDuty.System;

internal class ConfigurationManager : IDisposable
{
    private readonly CharacterConfiguration nullCharacterConfiguration = new();

    private CharacterConfiguration? backingCharacterConfiguration;

    public CharacterConfiguration CharacterConfiguration => CharacterDataLoaded ? backingCharacterConfiguration : nullCharacterConfiguration;

    [MemberNotNullWhen(returnValue: true, nameof(backingCharacterConfiguration))]
    public bool CharacterDataLoaded { get; private set; }

    public event EventHandler<CharacterConfiguration>? OnCharacterDataLoaded;
    public event EventHandler? OnCharacterDataUnloaded;

    public ConfigurationManager()
    {
        OnCharacterDataLoaded += LoadOverlayWindows;
        OnCharacterDataUnloaded += UnloadOverlayWindows;
        
        Service.ClientState.Login += OnLogin;
        Service.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        if (Service.ClientState.IsLoggedIn)
        {
            CharacterConfiguration.SaveBackup();
        }
        
        Service.ClientState.Login -= OnLogin;
        Service.ClientState.Logout -= OnLogout;
        
        OnCharacterDataLoaded -= LoadOverlayWindows;
        OnCharacterDataUnloaded -= UnloadOverlayWindows;
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        PluginLog.Verbose($"Logging into Character '{Service.ClientState.LocalPlayer?.Name.TextValue}'");
        
        backingCharacterConfiguration = CharacterConfiguration.Load(Service.ClientState.LocalContentId);
        backingCharacterConfiguration.Save();
        
        CharacterDataLoaded = true;
        OnCharacterDataLoaded?.Invoke(this, CharacterConfiguration);
        Service.ChatManager.ResetZoneTimer();
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        PluginLog.Verbose($"Logging out of '{CharacterConfiguration.CharacterData.Name}'");

        CharacterConfiguration.SaveBackup();
        CharacterDataLoaded = false;
        OnCharacterDataUnloaded?.Invoke(this, EventArgs.Empty);
    }
    
    public void Save()
    {
        if (CharacterDataLoaded)
        {
            CharacterConfiguration.Save();
        }
    }

    /// <summary>
    /// For initiating login if plugin is reloaded
    /// </summary>
    public void TryLogin()
    {
        if (Service.ClientState.IsLoggedIn)
        {
            OnLogin(this, EventArgs.Empty);
        }
    }
    
    private void LoadOverlayWindows(object? sender, CharacterConfiguration e)
    {
        KamiCommon.WindowManager.AddWindow(new TimersOverlayWindow());
        KamiCommon.WindowManager.AddWindow(new TodoOverlayWindow());
    }
    
    private void UnloadOverlayWindows(object? sender, EventArgs e)
    {
        var windowList = new List<Window>();
        
        windowList.AddRange(KamiCommon.WindowManager.GetWindows().OfType<TimersOverlayWindow>());
        windowList.AddRange(KamiCommon.WindowManager.GetWindows().OfType<TodoOverlayWindow>());
        
        foreach (var overlay in windowList)
        {
            KamiCommon.WindowManager.RemoveWindow(overlay);
        }
    }
}