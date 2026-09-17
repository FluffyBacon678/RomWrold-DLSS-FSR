using System;
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
        private float settingsHeight = 560f;

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
            listing.Label("Render the colony at a lower resolution while keeping the interface at display resolution.");
            listing.Gap(8f);
            listing.CheckboxLabeled("Enable upscaling", ref Settings.Enabled);
            if (listing.ButtonText("Upscaling: " + FilterLabel(Settings.Filter)))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("FSR 1 (EASU + RCAS)", () => Settings.Filter = UpscaleFilter.Fsr1),
                    new FloatMenuOption("Bilinear (simple scaling)", () => Settings.Filter = UpscaleFilter.Bilinear)
                }));
            }
            if (listing.ButtonText("Quality: " + QualityLabel(Settings.Quality)))
            {
                var options = new List<FloatMenuOption>();
                foreach (QualityPreset quality in Enum.GetValues(typeof(QualityPreset)))
                {
                    QualityPreset captured = quality;
                    options.Add(new FloatMenuOption(QualityLabel(captured), () => Settings.Quality = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * Settings.RenderScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * Settings.RenderScale));
            listing.Label($"World: {width} x {height}  ->  Display / interface: {Screen.width} x {Screen.height}");
            if (Settings.Filter == UpscaleFilter.Fsr1)
            {
                listing.Label($"Sharpening: {Settings.Sharpness:P0}");
                Settings.Sharpness = listing.Slider(Settings.Sharpness, 0f, 1f);
            }
            listing.CheckboxLabeled("Show rendering status and FPS", ref Settings.ShowOverlay);
            listing.Gap(8f);
            listing.Label("Ctrl+F8 toggles native rendering for a quick comparison. Settings take effect immediately.");
            listing.Label("Status: " + UpscalerController.Status);
            listing.GapLine();
            listing.Label("Windows / DirectX 11 preview. FSR 1 is spatial upscaling; it does not generate frames or change simulation speed. Lower resolution may not increase FPS when the CPU is the limit.");
            settingsHeight = listing.CurHeight + 12f;
            listing.End();
            Widgets.EndScrollView();
        }

        internal static string FilterLabel(UpscaleFilter filter) => filter == UpscaleFilter.Fsr1 ? "FSR 1" : "Bilinear";

        internal static string QualityLabel(QualityPreset quality)
        {
            string name = quality == QualityPreset.UltraQuality ? "Ultra Quality" : quality.ToString();
            return $"{name} ({UpscalerSettings.ScaleFor(quality):P0} resolution per axis)";
        }
    }
}
