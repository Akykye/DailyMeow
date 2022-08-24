﻿using System;
using DailyDuty.Configuration.Components;
using DailyDuty.Configuration.Enums;
using DailyDuty.Configuration.ModuleSettings;
using DailyDuty.Interfaces;
using DailyDuty.Modules.Enums;
using DailyDuty.System.Localization;
using DailyDuty.UserInterface.Components;
using DailyDuty.UserInterface.Components.InfoBox;
using DailyDuty.Utilities;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility.Signatures;

namespace DailyDuty.Modules;

internal class CustomDelivery : IModule
{
    public ModuleName Name => ModuleName.CustomDelivery;
    public IConfigurationComponent ConfigurationComponent { get; }
    public IStatusComponent StatusComponent { get; }
    public ILogicComponent LogicComponent { get; }
    public ITodoComponent TodoComponent { get; }
    public ITimerComponent TimerComponent { get; }

    private static CustomDeliverySettings Settings => Service.ConfigurationManager.CharacterConfiguration.CustomDelivery;
    public GenericSettings GenericSettings => Settings;

    public CustomDelivery()
    {
        ConfigurationComponent = new ModuleConfigurationComponent(this);
        StatusComponent = new ModuleStatusComponent(this);
        LogicComponent = new ModuleLogicComponent(this);
        TodoComponent = new ModuleTodoComponent(this);
        TimerComponent = new ModuleTimerComponent(this);
    }

    public void Dispose()
    {
        LogicComponent.Dispose();
    }

    private class ModuleConfigurationComponent : IConfigurationComponent
    {
        public IModule ParentModule { get; }
        public ISelectable Selectable => new ConfigurationSelectable(ParentModule, this);

        private readonly InfoBox optionsInfoBox = new();
        private readonly InfoBox completionConditionsInfoBox = new();
        private readonly InfoBox notificationOptionsInfoBox = new();

        public ModuleConfigurationComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            optionsInfoBox
                .AddTitle(Strings.Configuration.Options)
                .AddConfigCheckbox(Strings.Common.Enabled, Settings.Enabled)
                .Draw();

            completionConditionsInfoBox
                .AddTitle(Strings.Configuration.MarkCompleteWhen)
                .BeginTable(0.40f)
                .AddActions(
                    Actions.GetConfigComboAction(Enum.GetValues<ComparisonMode>(), Settings.ComparisonMode, ComparisonModeExtensions.GetLocalizedString),
                    Actions.GetSliderInt(Strings.Common.Allowances, Settings.NotificationThreshold, 0, 12, 100.0f))
                .EndTable()
                .Draw();

            notificationOptionsInfoBox
                .AddTitle(Strings.Configuration.NotificationOptions)
                .AddConfigCheckbox(Strings.Configuration.OnLogin, Settings.NotifyOnLogin)
                .AddConfigCheckbox(Strings.Configuration.OnZoneChange, Settings.NotifyOnZoneChange)
                .Draw();
        }
    }

    private class ModuleStatusComponent : IStatusComponent
    {
        public IModule ParentModule { get; }

        public ISelectable Selectable =>
            new StatusSelectable(ParentModule, this, ParentModule.LogicComponent.GetModuleStatus);

        private readonly InfoBox statusInfoBox = new();
        private readonly InfoBox targetInfoBox = new();

        public ModuleStatusComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public void Draw()
        {
            if (ParentModule.LogicComponent is not ModuleLogicComponent logicModule) return;

            var moduleStatus = logicModule.GetModuleStatus();
            var allowances = logicModule.GetRemainingAllowances();

            statusInfoBox
                .AddTitle(Strings.Status.Label)
                .BeginTable()

                .AddRow(
                    Strings.Status.ModuleStatus, 
                    moduleStatus.GetLocalizedString(), 
                    secondColor: moduleStatus.GetStatusColor())

                .AddRow(
                    Strings.Common.Allowances, 
                    allowances.ToString(), 
                    secondColor: moduleStatus.GetStatusColor())

                .EndTable()
                .Draw();

            targetInfoBox
                .AddTitle(Strings.Common.Target)
                .BeginTable()

                .AddRow(
                    Strings.Common.Mode,
                    Settings.ComparisonMode.Value.GetLocalizedString())

                .AddRow(
                    Strings.Common.Target,
                    Settings.NotificationThreshold.Value.ToString())

                .EndTable()
                .Draw();
        }
    }

    private unsafe class ModuleLogicComponent : ILogicComponent
    {
        public IModule ParentModule { get; }
        public DalamudLinkPayload? DalamudLinkPayload => null;

        private delegate int GetCustomDeliveryAllowancesDelegate(byte* array);

        [Signature("0F B6 41 20 4C 8B C1")]
        private readonly GetCustomDeliveryAllowancesDelegate getCustomDeliveryAllowances = null!;

        [Signature("48 8D 0D ?? ?? ?? ?? 41 0F BA EC", ScanType = ScanType.StaticAddress)]
        private readonly byte* staticArrayPointer = null!;

        public ModuleLogicComponent(IModule parentModule)
        {
            ParentModule = parentModule;
            SignatureHelper.Initialise(this);
        }

        public void Dispose()
        {
        }

        public string GetStatusMessage() => Strings.Module.CustomDelivery.AllowancesRemaining;


        public DateTime GetNextReset() => Time.NextWeeklyReset();

        public void DoReset()
        {
            // Do nothing
        }

        public ModuleStatus GetModuleStatus()
        {
            switch (Settings.ComparisonMode.Value)
            {
                case ComparisonMode.LessThan when Settings.NotificationThreshold.Value > GetRemainingAllowances():
                case ComparisonMode.EqualTo when Settings.NotificationThreshold.Value == GetRemainingAllowances():
                case ComparisonMode.LessThanOrEqual when Settings.NotificationThreshold.Value >= GetRemainingAllowances():
                    return ModuleStatus.Complete;

                default:
                    return ModuleStatus.Incomplete;
            }
        }

        public int GetRemainingAllowances()
        {
            return 12 - getCustomDeliveryAllowances(staticArrayPointer);
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

        public string GetShortTaskLabel() => Strings.Module.CustomDelivery.Label;

        public string GetLongTaskLabel() => Strings.Module.CustomDelivery.Label;
    }


    private class ModuleTimerComponent : ITimerComponent
    {
        public IModule ParentModule { get; }

        public ModuleTimerComponent(IModule parentModule)
        {
            ParentModule = parentModule;
        }

        public TimeSpan GetTimerPeriod() => TimeSpan.FromDays(7);
    }
}