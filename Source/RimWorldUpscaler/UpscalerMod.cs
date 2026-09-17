using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorldUpscaler
{
    public sealed class UpscalerMod : Mod
    {
        internal static UpscalerMod Instance;
        internal readonly UpscalerSettings Settings;

        private static readonly QualityPreset[] QualityChoices =
        {
            QualityPreset.Performance,
            QualityPreset.Balanced,
            QualityPreset.Quality,
            QualityPreset.UltraQuality,
            QualityPreset.Native,
            QualityPreset.Supersample125,
            QualityPreset.Supersample150
        };

        public UpscalerMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<UpscalerSettings>();
            Settings.Validate();
            LongEventHandler.ExecuteWhenFinished(() => UpscalerController.Initialize(this));
        }

        public override string SettingsCategory() => "RimWorld Upscaler";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Scale the colony view while keeping menus and interface text at display resolution.");
            listing.Gap(8f);
            listing.CheckboxLabeled("Enable render scaling", ref Settings.Enabled);
            if (listing.ButtonText("Scaling method: " + FilterLabel(Settings.Filter)))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("FSR 1 (EASU + RCAS)", () => Settings.Filter = UpscaleFilter.Fsr1),
                    new FloatMenuOption("Bilinear (simple scaling)", () => Settings.Filter = UpscaleFilter.Bilinear)
                }));
            }
            if (listing.ButtonText("Render resolution: " + QualityLabel(Settings.Quality)))
            {
                var options = new List<FloatMenuOption>();
                foreach (QualityPreset quality in QualityChoices)
                {
                    QualityPreset captured = quality;
                    options.Add(new FloatMenuOption(QualityLabel(captured), () => Settings.Quality = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * Settings.RenderScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * Settings.RenderScale));
            listing.Label($"Colony render: {width} x {height}  ->  Display / interface: {Screen.width} x {Screen.height}");
            if (Settings.RenderScale < 1f)
                listing.Label("Below 100% saves GPU work but loses fine detail.");
            else if (Settings.RenderScale > 1f)
                listing.Label("Supersampling improves detail but costs more GPU power and memory.");
            else
                listing.Label("Native uses the display resolution; RCAS can still add sharpening.");

            bool ultrawide = Screen.height > 0 && Screen.width / (float)Screen.height >= 3f;
            if (ultrawide && listing.ButtonText("Apply sharper ultrawide preset (77% + 70% sharpening)"))
            {
                Settings.Enabled = true;
                Settings.Filter = UpscaleFilter.Fsr1;
                Settings.Quality = QualityPreset.UltraQuality;
                Settings.Sharpness = 0.7f;
                Settings.ShowOverlay = true;
                WriteSettings();
            }

            if (Settings.Filter == UpscaleFilter.Fsr1)
            {
                listing.Label($"RCAS sharpening: {Settings.Sharpness:P0}");
                Settings.Sharpness = listing.Slider(Settings.Sharpness, 0f, 1f);
                listing.Label("Sharper: Ultra Quality 70%. Maximum clarity: Native 35% or 125% at 20%.");
            }
            listing.CheckboxLabeled("Show rendering status and FPS", ref Settings.ShowOverlay);
            listing.Label("Ctrl+F8 toggles normal rendering. Changes take effect immediately.");
            listing.Label("Status: " + UpscalerController.Status);
            listing.GapLine();
            listing.Label("Safety and compatibility");
            listing.Label($"Renderer: {SystemInfo.graphicsDeviceType} | {SystemInfo.graphicsDeviceName}");
            if (listing.ButtonText("Reset to safe defaults"))
            {
                Settings.ResetToSafeDefaults();
                WriteSettings();
            }
            listing.Label("Colony maps only. Unsupported views or rendering errors return to normal rendering.");
            listing.End();
        }

        public override void WriteSettings()
        {
            Settings.Validate();
            base.WriteSettings();
        }

        internal static string FilterLabel(UpscaleFilter filter) => filter == UpscaleFilter.Fsr1 ? "FSR 1" : "Bilinear";

        internal static string QualityLabel(QualityPreset quality)
        {
            string name;
            switch (quality)
            {
                case QualityPreset.UltraQuality: name = "Ultra Quality"; break;
                case QualityPreset.Supersample125: name = "Supersampling"; break;
                case QualityPreset.Supersample150: name = "High supersampling"; break;
                default: name = quality.ToString(); break;
            }
            return $"{name} ({UpscalerSettings.ScaleFor(quality):P0} resolution per axis)";
        }
    }
}
