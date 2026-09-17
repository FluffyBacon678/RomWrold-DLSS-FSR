using System;
using UnityEngine;
using Verse;

namespace RimWorldUpscaler
{
    public enum UpscaleFilter { Fsr1, Bilinear }

    // Keep the first four numeric values stable so settings written by 0.1.2
    // continue to select the same render resolution after updating.
    public enum QualityPreset
    {
        UltraQuality,
        Quality,
        Balanced,
        Performance,
        Native,
        Supersample125,
        Supersample150
    }

    public sealed class UpscalerSettings : ModSettings
    {
        public bool Enabled;
        public bool ShowOverlay = true;
        public UpscaleFilter Filter = UpscaleFilter.Fsr1;
        public QualityPreset Quality = QualityPreset.Quality;
        public float Sharpness = 0.5f;

        public float RenderScale => ScaleFor(Quality);

        public void ResetToSafeDefaults()
        {
            Enabled = false;
            ShowOverlay = true;
            Filter = UpscaleFilter.Fsr1;
            Quality = QualityPreset.Quality;
            Sharpness = 0.5f;
        }

        public static float ScaleFor(QualityPreset quality)
        {
            switch (quality)
            {
                case QualityPreset.UltraQuality: return 1f / 1.3f;
                case QualityPreset.Balanced: return 1f / 1.7f;
                case QualityPreset.Performance: return 0.5f;
                case QualityPreset.Native: return 1f;
                case QualityPreset.Supersample125: return 1.25f;
                case QualityPreset.Supersample150: return 1.5f;
                default: return 1f / 1.5f;
            }
        }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(UpscaleFilter), Filter)) Filter = UpscaleFilter.Fsr1;
            if (!Enum.IsDefined(typeof(QualityPreset), Quality)) Quality = QualityPreset.Quality;
            Sharpness = float.IsNaN(Sharpness) || float.IsInfinity(Sharpness) ? 0.5f : Mathf.Clamp01(Sharpness);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", false);
            Scribe_Values.Look(ref ShowOverlay, "showOverlay", true);
            Scribe_Values.Look(ref Filter, "filter", UpscaleFilter.Fsr1);
            Scribe_Values.Look(ref Quality, "quality", QualityPreset.Quality);
            Scribe_Values.Look(ref Sharpness, "sharpness", 0.5f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Validate();
        }
    }
}
