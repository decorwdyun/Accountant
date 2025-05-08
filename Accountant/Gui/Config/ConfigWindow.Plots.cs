using System;
using System.Numerics;
using Accountant.Classes;
using Accountant.Enums;
using Accountant.Gui.Helper;
using Accountant.Manager;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace Accountant.Gui.Config;

public partial class ConfigWindow
{
    private void DrawDemolitionTab()
    {
        if (!ImGui.BeginTabItem("房屋拆除追踪器##AccountantTabs"))
            return;

        using var raii = ImGuiRaii.DeferredEnd(ImGui.EndTabItem);

        var size = ImGui.GetContentRegionAvail() with
        {
            X = 320 * ImGuiHelpers.GlobalScale + 6 * ImGui.GetStyle().CellPadding.X,
        };
        DrawDemolitionTable(size);
        ImGui.SameLine();
        DrawDemolitionData();
    }

    private ulong    _newPlotInfo = new PlotInfo(InternalHousingZone.Mist, 1, 1, 0).Value;
    private PlotInfo _selectedPlot;
    private string   _newPlayerName = string.Empty;
    private ushort   _newWorldId;

    private void DrawDemolitionData()
    {
        var       child = ImGui.BeginChild("##demoData", ImGui.GetContentRegionAvail(), true);
        using var raii  = ImGuiRaii.DeferredEnd(ImGui.EndChild);
        if (!child)
            return;

        if (!_demoManager.Data.TryGetValue(_selectedPlot, out var data))
        {
            _selectedPlot = default;
            return;
        }

        ImGui.InputTextWithHint("自定义房屋名", "不输入将会使用默认名字", ref data.Name, 128);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _timerWindow.ResetCache();
            _demoManager.Save();
        }

        if (ImGui.Checkbox("开启追踪", ref data.Tracked))
            _demoManager.Save();
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "The name entered here will replace all occurrences of the plot in the timer window.\n\nThe timer limit will show this house as due to be demolished in the timer window when the configured number of days passed since your last visit.\n\nThe warning timer limit will show notifications on the bottom right when the configured number of days passed since your last visit.\n\nYou need to manually add players that reset the timer when encountered within the house, otherwise the timer will not update.");

        var tmpDays = (int)data.DisplayFrom;
        if (ImGui.DragInt("在计时悬浮窗显示", ref tmpDays, 0.1f, 0, DemolitionManager.DefaultDisplayMax, "%i 天倒计时"))
        {
            tmpDays = Math.Clamp(tmpDays, 0, DemolitionManager.DefaultDisplayMax);
            if (tmpDays != data.DisplayFrom)
            {
                data.DisplayFrom = (byte)tmpDays;
                _demoManager.Save();
            }
        }

        tmpDays = data.DisplayWarningFrom;
        if (ImGui.DragInt("在右下角显示警报", ref tmpDays, 0.1f, 0, DemolitionManager.DefaultDisplayMax, "%i 天倒计时"))
        {
            tmpDays = Math.Clamp(tmpDays, 0, DemolitionManager.DefaultDisplayMax);
            if (tmpDays != data.DisplayWarningFrom)
            {
                data.DisplayWarningFrom = (byte)tmpDays;
                _demoManager.Save();
            }
        }

        var ctrl = ImGui.GetIO().KeyCtrl;
        var text = data.LastVisitDays switch
        {
            <= 1                                  => "上次访问: 24 小时内",
            > DemolitionManager.DefaultDisplayMax => $"上次访问: {DemolitionManager.DefaultDisplayMax} 天前",
            _                                     => $"上次访问: {data.LastVisitDays} 天前",
        };
        using (ImRaii.Disabled(!ctrl))
        {
            if (ImGui.Button(text))
            {
                data.LastVisit = DateTime.UtcNow;
                _demoManager.Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("按住 ctrl 键并点击这个按钮将强制更新状态为已进入房屋室内");

        ImGui.InputTextWithHint("##PlayerName", "玩家(房屋主人)名字...", ref _newPlayerName, 40);
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (_newWorldId == 0 && Dalamud.ClientState.LocalPlayer is not null)
            _newWorldId = (ushort)Dalamud.ClientState.LocalPlayer.HomeWorld.RowId;
        DrawWorldsCombo(ref _newWorldId);


        using (ImRaii.Disabled(_newPlayerName.Length == 0
                || _newWorldId == 0
                || data.CheckedPlayers.Contains(new PlayerInfo(_newPlayerName, _newWorldId))))
        {
            if (ImGui.Button("添加新玩家"))
            {
                data.CheckedPlayers.Add(new PlayerInfo(_newPlayerName, _newWorldId));
                _demoManager.Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Add the player from the inputs if it is valid and not already added.");

        ImGui.SameLine();

        using (ImRaii.Disabled(Dalamud.ClientState.LocalPlayer == null
                || data.CheckedPlayers.Contains(new PlayerInfo(Dalamud.ClientState.LocalPlayer))))
        {
            if (ImGui.Button("添加当前玩家"))
            {
                data.CheckedPlayers.Add(new PlayerInfo(Dalamud.ClientState.LocalPlayer!));
                _demoManager.Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("添加你当前的角色.");

        if (ImGui.BeginListBox("##list", ImGui.GetContentRegionAvail()))
        {
            raii.Push(ImGui.EndListBox);

            PlayerInfo? delete = null;
            var         idx    = 0;
            var         frame  = new Vector2(ImGui.GetFrameHeight());
            foreach (var player in data.CheckedPlayers)
            {
                using var id = ImRaii.PushId(idx++);

                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), frame))
                        delete = player;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("删除这个玩家.");

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted($"{player.Name}@{Accountant.GameData.GetWorldName(player.ServerId)}");
            }

            if (delete != null && data.CheckedPlayers.Remove(delete.Value))
                _demoManager.Save();
        }
    }

    private void DrawDemolitionTable(Vector2 size)
    {
        if (!ImGui.BeginTable("", 4, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.NoClip, size))
            return;

        using var table = ImGuiRaii.DeferredEnd(ImGui.EndTable);

        SetupPlotHeaders();
        ImGui.TableHeadersRow();
        ImGui.TableSetupScrollFreeze(0, 1);

        var idx = 0;
        foreach (var (value, _) in _demoManager.Data)
        {
            using var id = ImRaii.PushId(idx++);
            DrawPlotRow(value);
            ImGui.SameLine();
            if (ImGui.Selectable("", _selectedPlot == value, ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.SpanAllColumns))
                _selectedPlot = value;
        }

        var newPlot = PlotInfo.FromValue(_newPlotInfo);
        if (newPlot.ServerId == 0 && Dalamud.ClientState.LocalPlayer != null)
        {
            newPlot      = new PlotInfo(newPlot.Zone, newPlot.Ward, newPlot.Plot, (ushort)Dalamud.ClientState.LocalPlayer.CurrentWorld.RowId);
            _newPlotInfo = newPlot.Value;
        }

        DrawPlotInfoInput(111, ref _newPlotInfo);

        ImGui.TableNextColumn();
        var canAdd = _demoManager.CanAddPlot(newPlot);
        var tt = canAdd
            ? "添加你输入的房屋到追踪器."
            : "输入的地块已经添加过了.";
        using (ImRaii.Disabled(!canAdd))
        {
            if (ImGui.Button("添加房屋"))
                _demoManager.AddPlot(newPlot);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tt);

        ImGui.SameLine();
        var currentPlot = _demoManager.CurrentPlot;
        tt = (currentPlot.Valid(), _demoManager.CanAddPlot(currentPlot)) switch
        {
            (true, true)  => $"添加当前房屋 {currentPlot.ToName()} on {Accountant.GameData.GetWorldName(currentPlot.ServerId)}.",
            (false, _)    => "你不在可识别的房屋.",
            (true, false) => $"当前房屋 {currentPlot.ToName()} on {Accountant.GameData.GetWorldName(currentPlot.ServerId)} 已经添加过了",
        };
        using (ImRaii.Disabled(tt[0] != '添'))
        {
            if (ImGui.Button("添加当前房屋"))
            {
                var plot = _demoManager.CurrentPlot;
                if (plot.Valid())
                    _demoManager.AddPlot(plot);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tt);

        ImGui.SameLine();
        tt = (ImGui.GetIO().KeyCtrl, _demoManager.Data.ContainsKey(_selectedPlot)) switch
        {
            (true, true)   => "删除选中的房屋",
            (true, false)  => "请选择要删除的房屋",
            (false, false) => "选择一个房屋并且按住 ctrl 即可删除",
            (false, true)  => "按住 ctrl 即可删除选中的房屋",
        };
        using (ImRaii.Disabled(tt[0] != '删'))
        {
            if (ImGui.Button("删除选中的房屋") && _demoManager.Data.Remove(_selectedPlot))
                _demoManager.Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tt);
    }
}
