﻿using Accountant.Classes;
using Accountant.Manager;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace Accountant.Gui.Timer;

public class DemolitionWarning : IDisposable
{
    private readonly DemolitionManager _manager;
    private readonly IFramework        _framework;

    private DateTime _nextChange = DateTime.MinValue;

    private readonly List<(string Name, string Status, ColorId Color)> _warnings = [];
    public           ColorId                                           HeaderColor { get; private set; }
    private          Dictionary<PlotInfo, IActiveNotification>         _notifications = [];


    public IReadOnlyList<(string Name, string Status, ColorId Color)> Warnings
        => _warnings;

    public DemolitionWarning(DemolitionManager manager, IFramework framework)
    {
        _manager          =  manager;
        _framework        =  framework;
        _manager.Change   += OnChange;
        _framework.Update += OnFramework;
    }

    private void UpdateNextUpdate(DateTime now, TimeSpan timespan)
    {
        var timespanNextDay = TimeSpan.TicksPerDay - timespan.Ticks % TimeSpan.TicksPerDay;
        var nextUpdate      = now.AddTicks(timespanNextDay);
        if (nextUpdate < _nextChange)
            _nextChange = nextUpdate;
    }

    private void UpdateDisplay(PlotInfo plot, DemolitionManager.DemolitionInfo data, int days)
    {
        if (days < data.DisplayFrom)
            return;

        var status = days switch
        {
            > DemolitionManager.DefaultDisplayMax   => "已拆除",
            DemolitionManager.DefaultDisplayMax     => "<1 天",
            DemolitionManager.DefaultDisplayMax - 1 => "1 天",
            _                                       => $"{DemolitionManager.DefaultDisplayMax - days} 天",
        };
        var color = days > DemolitionManager.DefaultDisplayMax
            ? ColorId.TextCropWithered
            : days > data.DisplayWarningFrom
                ? ColorId.TextCropWilted
                : ColorId.TextCropGrowing;
        _warnings.Add((plot.Name, status, color));
        HeaderColor = (HeaderColor, color) switch
        {
            (ColorId.HeaderCropWithered, ColorId.TextCropWithered) => ColorId.HeaderCropWithered,
            (ColorId.HeaderCropWilted, ColorId.TextCropWithered)   => ColorId.HeaderCropWithered,
            (ColorId.HeaderCropGrowing, ColorId.TextCropWithered)  => ColorId.HeaderCropWithered,
            (ColorId.HeaderCropWithered, ColorId.TextCropWilted)   => ColorId.HeaderCropWithered,
            (ColorId.HeaderCropWilted, ColorId.TextCropWilted)     => ColorId.HeaderCropWilted,
            (ColorId.HeaderCropGrowing, ColorId.TextCropWilted)    => ColorId.HeaderCropWilted,
            (ColorId.HeaderCropWithered, ColorId.TextCropGrowing)  => ColorId.HeaderCropWithered,
            (ColorId.HeaderCropWilted, ColorId.TextCropGrowing)    => ColorId.HeaderCropWilted,
            (ColorId.HeaderCropGrowing, ColorId.TextCropGrowing)   => ColorId.HeaderCropGrowing,
            _                                                      => HeaderColor,
        };
    }

    private void UpdateNotifications(Dictionary<PlotInfo, IActiveNotification> notifications, PlotInfo plot,
        DemolitionManager.DemolitionInfo data, int days)
    {
        if (days < data.DisplayWarningFrom)
            return;

        var content = days switch
        {
            > DemolitionManager.DefaultDisplayMax =>
                $"你的房屋 {plot.Name} 可能已经被拆除了. 请使用房屋主人角色进入房屋室内，如果你不再拥有该房屋，请在设置界面移除。",
            DemolitionManager.DefaultDisplayMax =>
                $"你的房屋 {plot.Name} 将在 24 小时内被自动拆除. 请使用房屋主人角色进入房屋室内以重置计时器.",
            DemolitionManager.DefaultDisplayMax - 1 =>
                $"你的房屋 {plot.Name} 明天将会被自动拆除. 请使用房屋主人角色进入房屋室内以重置计时器.",
            _ => $"你的房屋 {plot.Name} 将会在 {DemolitionManager.DefaultDisplayMax - days} 天后被自动拆除. 请使用房屋主人角色进入房屋室内以重置计时器。",
        };

        if (!_notifications.Remove(plot, out var activeNotification))
        {
            var notification = new Notification
            {
                Title                       = "房屋拆除警报！即将遭殃！",
                Content                     = content,
                Icon                        = INotificationIcon.From(FontAwesomeIcon.ExclamationTriangle),
                Type                        = NotificationType.Warning,
                HardExpiry                  = DateTime.MaxValue,
                InitialDuration             = TimeSpan.MaxValue,
                ShowIndeterminateIfNoExpiry = true,
                Minimized                   = false,
                UserDismissable             = true,
                MinimizedText               = "房屋拆除警报！即将遭殃！",
            };
            notifications.Add(plot, Dalamud.Notifications.AddNotification(notification));
        }
        else
        {
            activeNotification.Content = content;
            notifications.Add(plot, activeNotification);
        }
    }

    private void OnFramework(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (_nextChange > now)
            return;

        _nextChange = DateTime.MaxValue;
        _warnings.Clear();
        var notifications = new Dictionary<PlotInfo, IActiveNotification>(_notifications.Count);
        HeaderColor = ColorId.HeaderCropGrowing;
        foreach (var (plot, data) in _manager.Data)
        {
            if (!data.Tracked)
                continue;

            var timespan = now - data.LastVisit;
            var days     = (int)(Math.Ceiling(timespan.TotalDays) + 0.5);
            UpdateNextUpdate(now, timespan);
            UpdateDisplay(plot, data, days);
            UpdateNotifications(notifications, plot, data, days);
        }

        DismissAll();
        _notifications = notifications;
    }

    private void OnChange()
        => _nextChange = DateTime.UtcNow;

    public void Dispose()
    {
        DismissAll();
        _manager.Change   -= OnChange;
        _framework.Update -= OnFramework;
    }

    private void DismissAll()
    {
        foreach (var notification in _notifications.Values)
            notification.DismissNow();
        _notifications.Clear();
    }
}
