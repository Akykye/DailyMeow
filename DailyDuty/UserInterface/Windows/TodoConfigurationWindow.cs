﻿using System;
using System.Linq;
using System.Numerics;
using DailyDuty.Commands;
using DailyDuty.DataModels;
using DailyDuty.Localization;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using KamiLib;
using KamiLib.InfoBoxSystem;
using KamiLib.Utilities;

namespace DailyDuty.UserInterface.Windows;

internal class TodoConfigurationWindow : Window
{
    private static TodoOverlaySettings Settings => Service.ConfigurationManager.CharacterConfiguration.TodoOverlay;

    public TodoConfigurationWindow() : base("DailyDuty Todo Configuration", ImGuiWindowFlags.AlwaysVerticalScrollbar)
    {
        KamiCommon.CommandManager.AddCommand(new TodoWindowCommand());
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 350),
            MaximumSize = new Vector2(9999,9999)
        };
    }

    public override void PreOpenCheck()
    {
        if (!Service.ConfigurationManager.CharacterDataLoaded) IsOpen = false;
        if (Service.ClientState.IsPvP) IsOpen = false;
    }

    public override void Draw()
    {

        InfoBox.Instance
            .AddTitle(Strings.UserInterface.Todo.MainOptions)
            .AddConfigCheckbox(Strings.Common.Enabled, Settings.Enabled)
            .Draw();

        InfoBox.Instance
            .AddTitle(Strings.UserInterface.Todo.TaskSelection)
            .AddConfigCheckbox(Strings.UserInterface.Todo.ShowDailyTasks, Settings.ShowDailyTasks)
            .AddConfigCheckbox(Strings.UserInterface.Todo.ShowWeeklyTasks, Settings.ShowWeeklyTasks)
            .Draw();

        if (Settings.ShowDailyTasks.Value)
        {
            var enabledDailyTasks = Service.ModuleManager.GetTodoComponents(CompletionType.Daily)
                .Where(module => module.ParentModule.GenericSettings.Enabled.Value);

            InfoBox.Instance
                .AddTitle(Strings.UserInterface.Todo.DailyTasks)
                .BeginTable()
                .AddRows(enabledDailyTasks, Strings.UserInterface.Todo.NoTasksEnabled)
                .EndTable()
                .Draw();
        }

        if (Settings.ShowWeeklyTasks.Value)
        {
            var enabledWeeklyTasks = Service.ModuleManager.GetTodoComponents(CompletionType.Weekly)
                .Where(module => module.ParentModule.GenericSettings.Enabled.Value);

            InfoBox.Instance
                .AddTitle(Strings.UserInterface.Todo.WeeklyTasks)
                .BeginTable()
                .AddRows(enabledWeeklyTasks, Strings.UserInterface.Todo.NoTasksEnabled)
                .EndTable()
                .Draw();
        }

        InfoBox.Instance
            .AddTitle(Strings.UserInterface.Todo.TaskDisplay)
            .AddConfigCheckbox(Strings.UserInterface.Todo.HideCompletedTasks, Settings.HideCompletedTasks)
            .AddConfigCheckbox(Strings.UserInterface.Todo.HideUnavailable, Settings.HideUnavailableTasks)
            .AddConfigCheckbox(Strings.UserInterface.Todo.CompleteCategory, Settings.ShowCategoryAsComplete)
            .Draw();

        InfoBox.Instance
            .AddTitle(Strings.UserInterface.Todo.WindowOptions, out var innerWidth)
            .AddConfigCheckbox(Strings.UserInterface.Todo.HideWindowCompleted, Settings.HideWhenAllTasksComplete)
            .AddConfigCheckbox(Strings.UserInterface.Todo.HideWindowInDuty, Settings.HideWhileInDuty)
            .AddConfigCheckbox(Strings.UserInterface.Todo.LockWindow, Settings.LockWindowPosition)
            .AddConfigCheckbox(Strings.UserInterface.Todo.AutoResize, Settings.AutoResize)
            .AddConfigCombo(Enum.GetValues<WindowAnchor>(), Settings.AnchorCorner, WindowAnchorExtensions.GetTranslatedString, Strings.UserInterface.Todo.AnchorCorner, innerWidth / 2.0f)
            .AddDragFloat(Strings.UserInterface.Todo.Opacity, Settings.Opacity, 0.0f, 1.0f, innerWidth / 2.0f)
            .AddConfigColor(Strings.Common.Header, Strings.Common.Default, Settings.TaskColors.HeaderColor, Colors.White)
            .AddConfigColor(Strings.Common.Incomplete, Strings.Common.Default, Settings.TaskColors.IncompleteColor, Colors.Red)
            .AddConfigColor(Strings.Common.Complete, Strings.Common.Default, Settings.TaskColors.CompleteColor, Colors.Green)
            .AddConfigColor(Strings.Common.Unavailable, Strings.Common.Default, Settings.TaskColors.UnavailableColor, Colors.Orange)
            .Draw();
    }
    
    public override void OnClose()
    {
        Service.ConfigurationManager.Save();
    }
}