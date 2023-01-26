﻿using System;

namespace FancyLighting.Config;

public static class DefaultOptions
{
    public const Preset ConfigPreset = Preset.DefaultPreset;

    public const bool UseSmoothLighting = true;
    public const bool UseLightMapBlurring = true;
    public const RenderMode LightMapRenderMode = RenderMode.Bilinear;
    public const int NormalMapsStrength = 0;
    public const bool QualityNormalMaps = false;
    public const bool FineNormalMaps = false;
    public const bool RenderOnlyLight = false;

    public const bool UseAmbientOcclusion = true;
    public const bool DoNonSolidAmbientOcclusion = true;
    public const bool DoTileEntityAmbientOcclusion = false;
    public const int AmbientOcclusionRadius = 4;
    public const int AmbientOcclusionIntensity = 35;

    public const bool UseFancyLightingEngine = true;
    public const bool FancyLightingEngineUseTemporal = true;
    public const bool FancyLightingEngineMakeBrighter = false;
    public const int FancyLightingEngineLightLoss = 50;

    public const bool UseCustomSkyColors = true;

    public const int ThreadCount = 8; // Used for the DefaultValue attribute in LightingConfig
    public static int RuntimeDefaultThreadCount => Environment.ProcessorCount;
    public const bool UseHiDefFeatures = false;
}