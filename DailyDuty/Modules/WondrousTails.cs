﻿using System;
using DailyDuty.AddonOverlays;
using DailyDuty.DataModels;
using DailyDuty.DataStructures;
using DailyDuty.Interfaces;
using DailyDuty.Localization;
using DailyDuty.UserInterface.Components;
using DailyDuty.Utilities;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Configuration;
using KamiLib.InfoBoxSystem;
using KamiLib.Interfaces;
using KamiLib.Utilities;

namespace DailyDuty.Modules;

public class WondrousTailsSettings : GenericSettings
{
    public Setting<bool> InstanceNotifications = new(false);
    public Setting<bool> EnableClickableLink = new(false);
    public Setting<bool> UnclaimedBookWarning = new(true);
    public Setting<bool> OverlayEnabled = new(true);
    public Setting<bool> ResendOnCompletion = new(false);
}

internal class WondrousTails : IModule
{
    public ModuleName Name => ModuleName.WondrousTails;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static WondrousTailsSettings Settings => Service.ConfigurationManager.CharacterConfiguration.WondrousTails;
    public GenericSettings GenericSettings => Settings;

    private readonly DutyRouletteOverlay overlay = new();

    public WondrousTails()
    {
        ConfigurationComponent = new ModuleConfigurationComponent(this);
        StatusComponent = new ModuleStatusComponent(this);
        LogicComponent = new ModuleLogicComponent(this);
        TodoComponent = new ModuleTodoComponent(this);
        TimerComponent = new ModuleTimerComponent(this);
    }

    public void Dispose()
    {
        overlay.Dispose();
        LogicComponent.Dispose();
    }

    private class ModuleConfigurationComponent : IConfigurationComponent
    {
        public IModule ParentModule { get; }
        public ISelectable Selectable => new ConfigurationSelectable(ParentModule, this);

        public ModuleConfigurationComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            InfoBox.Instance
                .AddTitle(Strings.Configuration.Options)
                .AddConfigCheckbox(Strings.Common.Enabled, Settings.Enabled)
                .AddConfigCheckbox(Strings.Module.WondrousTails.Overlay, Settings.OverlayEnabled)
                .AddConfigCheckbox(Strings.Module.WondrousTails.DutyNotifications, Settings.InstanceNotifications)
                .AddIndent(1)
                .AddConfigCheckbox(Strings.Module.WondrousTails.ResendNotification, Settings.ResendOnCompletion)
                .AddIndent(-1)
                .AddConfigCheckbox(Strings.Module.WondrousTails.UnclaimedBookNotifications, Settings.UnclaimedBookWarning)
                .Draw();

            InfoBox.Instance
                .AddTitle(Strings.Module.WondrousTails.ClickableLinkLabel)
                .AddString(Strings.Module.WondrousTails.ClickableLink)
                .AddConfigCheckbox(Strings.Common.Enabled, Settings.EnableClickableLink)
                .Draw();

            InfoBox.Instance.DrawNotificationOptions(this);
        }
    }

    private class ModuleStatusComponent : IStatusComponent
    {
        public IModule ParentModule { get; }

        public ISelectable Selectable => new StatusSelectable(ParentModule, this, ParentModule.LogicComponent.Status);

        public ModuleStatusComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            if (ParentModule.LogicComponent is not ModuleLogicComponent logicModule) return;

            var moduleStatus = logicModule.GetModuleStatus();

            InfoBox.Instance
                .AddTitle(Strings.Status.Label)
                .BeginTable()
                .BeginRow()
                .AddString(Strings.Status.ModuleStatus)
                .AddString(moduleStatus.GetTranslatedString(), moduleStatus.GetStatusColor())
                .EndRow()
                .BeginRow()
                .AddString(Strings.Module.WondrousTails.Stamps)
                .AddString($"{WondrousTailsBook.Instance.Stickers} / 9", logicModule.GetModuleStatus().GetStatusColor())
                .EndRow()
                .EndTable()
                .Draw();
            
            InfoBox.Instance.DrawSuppressionOption(this);
        }
    }

    private unsafe class ModuleLogicComponent : ILogicComponent
    {
        public IModule ParentModule { get; }

        public DalamudLinkPayload DalamudLinkPayload => WondrousTailsBook.Instance.NeedsNewBook ? idyllshireTeleportPayload : openBookPayload;
        public bool LinkPayloadActive => Settings.EnableClickableLink;

        private delegate void UseItemDelegate(IntPtr a1, uint a2, uint a3 = 9999, uint a4 = 0, short a5 = 0);

        [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 41 B0 01 BA 13 00 00 00")]
        private readonly UseItemDelegate useItemFunction = null!;

        private IntPtr ItemContextMenuAgent => (IntPtr)Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.InventoryContext);
        private const uint WondrousTailsBookItemID = 2002023;

        private readonly DalamudLinkPayload openBookPayload;
        private readonly DalamudLinkPayload idyllshireTeleportPayload;

        private readonly WondrousTailsOverlay wondrousTailsOverlay = new();

        private bool dutyCompleted;
        private uint lastTerritoryType;

        public ModuleLogicComponent(IModule parentModule)
        {
            ParentModule = parentModule;

            SignatureHelper.Initialise(this);

            openBookPayload = ChatPayloadManager.Instance.AddChatLink(ChatPayloads.OpenWondrousTails, OpenWondrousTailsBook);
            idyllshireTeleportPayload = TeleportManager.Instance.GetPayload(TeleportLocation.Idyllshire);

            DutyState.Instance.DutyStarted += OnDutyStarted;
            DutyState.Instance.DutyCompleted += OnDutyCompleted;
            
            Service.ClientState.TerritoryChanged += OnZoneChange;
        }

        private void OnZoneChange(object? sender, ushort e)
        {
            if (!Settings.Enabled) return;
            if (!Settings.ResendOnCompletion) return;
            if (!dutyCompleted) return;

            dutyCompleted = false;
            OnDutyCompleted(lastTerritoryType);
        }

        public void Dispose()
        {
            DutyState.Instance.DutyStarted -= OnDutyStarted;
            DutyState.Instance.DutyCompleted -= OnDutyCompleted;
            
            Service.ClientState.TerritoryChanged -= OnZoneChange;

            wondrousTailsOverlay.Dispose();
        }
        
        public string GetStatusMessage()
        {
            if (Condition.IsBoundByDuty()) return string.Empty;
            
            if (Settings.UnclaimedBookWarning && WondrousTailsBook.Instance.NewBookAvailable)
            {
                return Strings.Module.WondrousTails.BookAvailable;
            }

            return string.Empty;
        }

        public DateTime GetNextReset() => Time.NextWeeklyReset();

        public void DoReset()
        {
            // Do nothing
        }

        public ModuleStatus GetModuleStatus()
        {
            if (Settings.UnclaimedBookWarning && WondrousTailsBook.Instance.NewBookAvailable) return ModuleStatus.Incomplete;

            return WondrousTailsBook.Instance.Complete ? ModuleStatus.Complete : ModuleStatus.Incomplete;
        }

        private void OpenWondrousTailsBook(uint arg1, SeString arg2)
        {
            if (ItemContextMenuAgent != IntPtr.Zero && WondrousTailsBook.PlayerHasBook)
            {
                useItemFunction(ItemContextMenuAgent, WondrousTailsBookItemID);
            }
        }
        
        private void OnDutyStarted(uint territory)
        {
            if (!Settings.InstanceNotifications) return;
            if (GetModuleStatus() == ModuleStatus.Complete) return;
            if (!WondrousTailsBook.PlayerHasBook) return;

            var node = WondrousTailsBook.Instance.GetTaskForDuty(territory);
            if (node == null) return;

            var buttonState = node.TaskState;
        
            switch (buttonState)
            {
                case ButtonState.Unavailable when WondrousTailsBook.Instance.Stickers > 0:
                    Chat.Print(Strings.Module.WondrousTails.Label, Strings.Module.WondrousTails.UnavailableMessage);
                    Chat.Print(Strings.Module.WondrousTails.Label, Strings.Module.WondrousTails.UnavailableRerollMessage.Format(WondrousTailsBook.Instance.SecondChance), Settings.EnableClickableLink ? DalamudLinkPayload : null);
                    break;

                case ButtonState.AvailableNow:
                    Chat.Print(Strings.Module.WondrousTails.Label, Strings.Module.WondrousTails.AvailableMessage, Settings.EnableClickableLink ? DalamudLinkPayload : null);
                    break;

                case ButtonState.Completable:
                    Chat.Print(Strings.Module.WondrousTails.Label, Strings.Module.WondrousTails.CompletableMessage);
                    break;

                case ButtonState.Unknown:
                default:
                    break;
            }
        }

        private void OnDutyCompleted(uint territory)
        {
            if (!Settings.InstanceNotifications) return;
            if (GetModuleStatus() == ModuleStatus.Complete) return;
            if (!WondrousTailsBook.PlayerHasBook) return;

            dutyCompleted = true;
            lastTerritoryType = territory;

            var node = WondrousTailsBook.Instance.GetTaskForDuty(territory);

            var buttonState = node?.TaskState;

            if (buttonState is ButtonState.Completable or ButtonState.AvailableNow)
            {
                Chat.Print(Strings.Module.WondrousTails.Label, Strings.Module.WondrousTails.ClaimableMessage, Settings.EnableClickableLink ? DalamudLinkPayload : null);
            }
        }
    }

    private class ModuleTodoComponent : ITodoComponent
    {
        public IModule ParentModule { get; }
        public CompletionType CompletionType => CompletionType.Weekly;
        public bool HasLongLabel => false;

        public ModuleTodoComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public string GetShortTaskLabel() => Strings.Module.WondrousTails.Label;

        public string GetLongTaskLabel() => Strings.Module.WondrousTails.Label;
    }


    private class ModuleTimerComponent : ITimerComponent
    {
        public IModule ParentModule { get; }

        public ModuleTimerComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public TimeSpan GetTimerPeriod() => TimeSpan.FromDays(7);

        public DateTime GetNextReset() => Time.NextWeeklyReset();
    }
}