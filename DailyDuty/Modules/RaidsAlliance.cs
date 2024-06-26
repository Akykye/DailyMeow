﻿using System.Collections.Generic;
using System.Linq;
using DailyDuty.Addons;
using DailyDuty.Configuration;
using DailyDuty.DataModels;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.Utilities;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Caching;
using KamiLib.ChatCommands;
using KamiLib.Configuration;
using KamiLib.Drawing;
using KamiLib.Misc;
using Lumina.Excel.GeneratedSheets;

namespace DailyDuty.Modules;

public class RaidsAllianceSettings : GenericSettings
{
    public List<TrackedRaid> TrackedRaids = new();
    public Setting<bool> EnableClickableLink = new(true);
}

public unsafe class RaidsAlliance : Module
{
    public override ModuleName Name => ModuleName.AllianceRaids;
    public override CompletionType CompletionType => CompletionType.Weekly;

    private static RaidsAllianceSettings Settings => Service.ConfigurationManager.CharacterConfiguration.RaidsAlliance;
    public override GenericSettings GenericSettings => Settings;

    public override DalamudLinkPayload? DalamudLinkPayload { get; }
    public override bool LinkPayloadActive => Settings.EnableClickableLink;

    private readonly AgentContentsFinder* contentsFinderAgentInterface = AgentContentsFinder.Instance();
    
    public RaidsAlliance()
    {
        DalamudLinkPayload = ChatPayloadManager.Instance.AddChatLink(ChatPayloads.AllianceRaidsDutyFinder, OpenDutyFinder);

        AddonContentsFinder.Instance.Refresh += OnSelectionChanged;
        Service.Chat.ChatMessage += OnChatMessage;
        Service.ConfigurationManager.OnCharacterDataLoaded += ConfigurationLoaded;
    }
    
    public override void Dispose()
    {
        AddonContentsFinder.Instance.Refresh -= OnSelectionChanged;
        Service.Chat.ChatMessage -= OnChatMessage;
        Service.ConfigurationManager.OnCharacterDataLoaded -= ConfigurationLoaded;
    }

    private void OnSelectionChanged(object? sender, nint e)
    {
        var enabledRaids = Settings.TrackedRaids.Where(raid => raid.Tracked).ToList();
        if(!enabledRaids.Any()) return;

        var contentFinderCondition = contentsFinderAgentInterface->SelectedDutyId;
        var trackedRaid = enabledRaids.FirstOrDefault(raid => raid.Duty.ContentFinderCondition == contentFinderCondition);

        if (trackedRaid != null)
        {
            var numCollectedRewards = contentsFinderAgentInterface->NumCollectedRewards;

            if (trackedRaid.CurrentDropCount != numCollectedRewards)
            {
                trackedRaid.CurrentDropCount = numCollectedRewards;
                Service.ConfigurationManager.Save();
            }
        }
    }

    private void OnChatMessage(XivChatType type, uint senderID, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // If module is enabled
        if (!Settings.Enabled) return;

        // If message is a loot message
        if (((int)type & 0x7F) != 0x3E) return;

        // If we are in a zone that we are tracking
        if (GetRaidForCurrentZone() is not { } trackedRaid) return;

        // If the message does NOT contain a player payload
        if (message.Payloads.FirstOrDefault(p => p is PlayerPayload) is PlayerPayload) return;

        // If the message DOES contain an item
        if (message.Payloads.FirstOrDefault(p => p is ItemPayload) is not ItemPayload { Item: { } item } ) return;

        switch (item.ItemUICategory.Row)
        {
            case 34: // Head
            case 35: // Body
            case 36: // Legs
            case 37: // Hands
            case 38: // Feet
            case 61 when item.ItemAction.Row == 0: // Miscellany with no itemAction
                    
                trackedRaid.CurrentDropCount += 1;
                Service.ConfigurationManager.Save();
                break;
        }
    }
    
    private void ConfigurationLoaded(object? sender, CharacterConfiguration e)
    {
        if (!Settings.TrackedRaids.Any() || IsDataStale())
        {
            PluginLog.Information("New Limited Alliance Raid Found. Reloading duty information.");
            
            Settings.TrackedRaids.Clear();

            foreach (var limitedDuty in DutyLists.Instance.LimitedAlliance)
            {
                var cfc = LuminaCache<ContentFinderCondition>.Instance.First(entry => entry.TerritoryType.Row == limitedDuty);
                
                Settings.TrackedRaids.Add(new TrackedRaid(cfc));
            }
            
            Service.ConfigurationManager.Save();
        }
    }

    public override void DoReset()
    {
        foreach (var raid in Settings.TrackedRaids)
        {
            raid.CurrentDropCount = 0;
        }
    }

    private static bool IsDataStale() => Settings.TrackedRaids.Any(trackedTask => !DutyLists.Instance.LimitedAlliance.Contains(trackedTask.Duty.TerritoryType));
    private void OpenDutyFinder(uint arg1, SeString arg2) => AgentContentsFinder.Instance()->OpenRegularDuty(GetFirstRaid());
    public override string GetStatusMessage() => $"{GetIncompleteCount()} {Strings.Raids_RaidRemaining}";
    private static int GetIncompleteCount() => Settings.TrackedRaids.Count(raid => raid.Tracked && raid.GetStatus() == ModuleStatus.Incomplete);
    public override ModuleStatus GetModuleStatus() => GetIncompleteCount() > 0 ? ModuleStatus.Incomplete : ModuleStatus.Complete;

    private static TrackedRaid? GetRaidForCurrentZone()
    {
        var currentZone = Service.ClientState.TerritoryType;
        var enabledRaids = Settings.TrackedRaids.Where(raid => raid.Tracked);
        var trackedRaidForZone = enabledRaids.FirstOrDefault(raid => raid.Duty.TerritoryType == currentZone);

        return trackedRaidForZone;
    }

    private static uint GetFirstRaid()
    {
        if (Settings.TrackedRaids.Any(raid => raid.GetStatus() == ModuleStatus.Incomplete))
        {
            return Settings.TrackedRaids.First(raid => raid.GetStatus() == ModuleStatus.Incomplete).Duty.ContentFinderCondition;
        }
        else
        {
            return Settings.TrackedRaids.First().Duty.ContentFinderCondition;
        }
    }
    
    protected override void DrawConfiguration()
    {
        InfoBox.Instance.DrawGenericSettings(this);

        if (Settings.TrackedRaids is { } trackedRaids)
        {
            InfoBox.Instance
                .AddTitle(Strings.Raids_TrackedRaids)
                .BeginTable(0.70f)
                .AddConfigurationRows(trackedRaids)
                .EndTable()
                .Draw();
        }
            
        InfoBox.Instance
            .AddTitle(Strings.Common_ClickableLink)
            .AddString(Strings.DutyFinder_ClickableLink)
            .AddConfigCheckbox(Strings.Common_Enabled, Settings.EnableClickableLink)
            .Draw();

        InfoBox.Instance.DrawNotificationOptions(this);
    }

    protected override void DrawStatus()
    {
        InfoBox.Instance.DrawGenericStatus(this);

        if (Settings.TrackedRaids.Any(raid => raid.Tracked))
        {
            InfoBox.Instance
                .AddTitle(Strings.Status_ModuleData)
                .BeginTable(0.70f)
                .AddDataRows(Settings.TrackedRaids.Where(raid => raid.Tracked))
                .EndTable()
                .Draw();
        }
        else
        {
            InfoBox.Instance
                .AddTitle(Strings.Status_ModuleData, out var innerWidth)
                .AddStringCentered(Strings.Raids_NothingTracked, innerWidth, Colors.Orange)
                .Draw();
        }
            
        InfoBox.Instance.DrawSuppressionOption(this);
    }
}