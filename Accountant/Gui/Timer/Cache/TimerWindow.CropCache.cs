using System;
using System.Globalization;
using System.Numerics;
using Accountant.Classes;
using Accountant.Gui.Helper;
using Accountant.Gui.Timer.Cache;
using Accountant.Timers;
using Dalamud.Bindings.ImGui;

namespace Accountant.Gui.Timer;

public partial class TimerWindow
{
    internal sealed partial class CropCache : BaseCache
    {
        private readonly PlotCropTimers    _plotCrops;
        private readonly PrivateCropTimers _privateCrops;

        public CropCache(TimerWindow window, ConfigFlags requiredFlags, PlotCropTimers plotCrops, PrivateCropTimers privateCrops)
            : base("园圃", requiredFlags, window)
        {
            _plotCrops            =  plotCrops;
            _privateCrops         =  privateCrops;
            _plotCrops.Changed    += Resetter;
            _privateCrops.Changed += Resetter;
        }

        protected override void DrawTooltip()
        {
            if (Accountant.Config.ShowCropTooltip)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Outdoor crops only refresh every 63 minutes on a ward-specific update timer.\n"
                  + "Any timer may be delayed by up to 63 minutes.\n"
                  + "Fertilizing a plant during these delays will automatically trigger updates.\n"
                  + "They will still grow out/wilt/wither in order, and grown-out plants do not wither anymore.\n"
                  + "Indoors, clearing the house and re-entering should automatically trigger updates.\n"
                  + "You can disable this tooltip in the configuration.");
                ImGui.EndTooltip();
            }
        }

        private static string TimeSpanString2(DateTime target, DateTime now)
        {
            if (target == DateTime.MinValue)
                return "已经";
            if (target == DateTime.UnixEpoch)
                return "未知";
            if (target == DateTime.MaxValue)
                return "永不";

            return target < now ? "Already" : TimeSpanString(target - now, 3);
        }

        private static Action GenerateTooltip(PlantInfo plant, CacheObject ret, string plantName, DateTime fin, DateTime wilt, DateTime wither)
        {
            var plantTimeString = plant.PlantTime.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            return () =>
            {
                ImGui.BeginTooltip();
                using var _ = ImGuiRaii.PushColor(ImGuiCol.Button, ret.Color.TextToHeader().Value());
                if (Dalamud.GetIcon(ret.Icon, out var icon))
                {
                    ImGui.Image(icon.Handle, Vector2.One * icon.Height / 2);
                    ImGui.SameLine();
                    ImGui.Button(plantName, Vector2.UnitY * icon.Height / 2 - Vector2.UnitX);
                }
                else
                {
                    ImGui.Dummy(new Vector2(ImGui.GetFrameHeightWithSpacing()));
                    ImGui.SameLine();
                    ImGui.Button(plantName, Vector2.UnitY * ImGui.GetFrameHeightWithSpacing() / 2 - Vector2.UnitX);
                }

                ImGui.BeginGroup();
                ImGui.Text("种植:");
                ImGui.Text("护理:");
                ImGui.Text("成熟:");
                ImGui.Text("冒紫烟:");
                ImGui.Text("枯萎:");
                if (plant.Position != Vector3.Zero)
                    ImGui.Text("位置:");
                ImGui.EndGroup();
                ImGui.SameLine();
                if (!plant.AccuratePlantTime)
                {
                    ImGui.BeginGroup();
                    ImGui.Text("<");
                    ImGui.NewLine();
                    ImGui.Text(fin != DateTime.MaxValue && fin > DateTime.Now ? "<" : "  ");
                    ImGui.EndGroup();
                }
                else
                {
                    ImGui.Text("  ");
                }

                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.Text(plantTimeString);
                ImGui.Text(plant.LastTending.ToLocalTime().ToString(CultureInfo.CurrentCulture));
                ImGui.Text(TimeSpanString2(fin, DateTime.UtcNow));
                ImGui.Text(fin < wilt ? "永不" : TimeSpanString2(wilt,     DateTime.UtcNow));
                ImGui.Text(fin < wither ? "永不" : TimeSpanString2(wither, DateTime.UtcNow));
                if (plant.Position != Vector3.Zero)
                    ImGui.Text(FormattableString.Invariant($"({plant.Position.X:F1}, {plant.Position.Y:F1}, {plant.Position.Z:F1})"));
                ImGui.EndGroup();
                ImGui.EndTooltip();
            };
        }

        protected override void UpdateInternal()
        {
            if (Accountant.Config.OrderByCrop)
                UpdateByCrop();
            else
                UpdateByOwner();
        }
    }
}
