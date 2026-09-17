using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorldUpscaler
{
    public sealed class UpscalerMod : Mod
    {
        internal static UpscalerMod Instance;
        internal readonly UpscalerSettings Settings;
        private Vector2 scrollPosition;
        private float settingsHeight = 940f;

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

        private static readonly int[] FrameRateChoices = { 0, 30, 60, 90, 120, 144, 165, 240 };

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
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, settingsHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listing.Begin(viewRect);
            listing.Label("Scale the colony view while keeping menus and interface text at display resolution.");
            listing.Gap(8f);
            listing.CheckboxLabeled("Enable render scaling", ref Settings.Enabled,
                "This affects only the colony view. Turn it off to return immediately to normal RimWorld rendering.");
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
                listing.Label("Below 100% improves GPU performance but loses fine detail. Higher percentages are sharper.");
            else if (Settings.RenderScale > 1f)
                listing.Label("Supersampling improves edges and fine detail, but uses substantially more GPU power and memory.");
            else
                listing.Label("Native renders the colony at display resolution. RCAS can still add controlled sharpening.");

            bool ultrawide = Screen.height > 0 && Screen.width / (float)Screen.height >= 3f;
            if (ultrawide)
            {
                listing.Label("Sharper ultrawide starting point: FSR 1, Ultra Quality, 70% sharpening.");
                if (listing.ButtonText("Apply sharper ultrawide preset"))
                {
                    Settings.Enabled = true;
                    Settings.Filter = UpscaleFilter.Fsr1;
                    Settings.Quality = QualityPreset.UltraQuality;
                    Settings.Sharpness = 0.7f;
                    Settings.ShowOverlay = true;
                    WriteSettings();
                }
            }

            if (Settings.Filter == UpscaleFilter.Fsr1)
            {
                listing.Label($"RCAS sharpening: {Settings.Sharpness:P0}");
                Settings.Sharpness = listing.Slider(Settings.Sharpness, 0f, 1f);
                listing.Label("If the image is blurry, try Ultra Quality at 70%. For maximum clarity, use Native or 125% supersampling with lighter sharpening.");
            }
            listing.CheckboxLabeled("Show rendering status and FPS", ref Settings.ShowOverlay);
            listing.Gap(8f);
            listing.Label("Ctrl+F8 toggles native rendering for a quick comparison. Settings take effect immediately.");
            listing.Label("Status: " + UpscalerController.Status);
            listing.GapLine();

            listing.Label("Frame-rate limit");
            if (listing.ButtonText("FPS cap: " + FrameRateLabel(Settings.FrameRateCap)))
            {
                var options = new List<FloatMenuOption>();
                foreach (int cap in FrameRateChoices)
                {
                    int captured = cap;
                    options.Add(new FloatMenuOption(FrameRateLabel(captured), () => Settings.FrameRateCap = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (Settings.FrameRateCap > 0)
            {
                listing.CheckboxLabeled("Override VSync to enforce this cap", ref Settings.OverrideVSyncForFrameRateCap,
                    "Unity lets VSync take priority over its software FPS cap. Enable this only when you want this mod to disable VSync and enforce the selected cap.");
            }
            listing.Label(UpscalerController.FrameRateLimitStatus);
            listing.GapLine();

            listing.Label("Safety and compatibility");
            listing.Label("The mod starts disabled, does not alter saves, and automatically returns to native rendering outside colony maps, on unsupported graphics APIs, when another mod owns the camera output, or after a rendering error.");
            listing.Label($"Detected renderer: {SystemInfo.graphicsDeviceType} | {SystemInfo.graphicsDeviceName}");
            if (listing.ButtonText("Reset to safe defaults"))
            {
                Settings.ResetToSafeDefaults();
                WriteSettings();
            }
            listing.GapLine();
            listing.Label("Windows / DirectX 11 preview. FSR 1 is spatial scaling; it does not generate frames or change simulation speed. Lower resolution may not increase FPS when the CPU is the limit.");
            listing.Label("DLSS is not included because RimWorld's built-in render pipeline does not provide the temporal motion, depth, and jitter integration needed for a stable DLSS result.");
            settingsHeight = listing.CurHeight + 12f;
            listing.End();
            Widgets.EndScrollView();
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

        internal static string FrameRateLabel(int cap) => cap <= 0 ? "Off / game default" : cap + " FPS";
    }
}
