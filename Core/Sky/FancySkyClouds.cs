using System.Reflection;
using FancyLighting.VFX;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace FancyLighting.Core.Sky;

public static class FancySkyClouds
{
    private const int BlurPassCount = 5;
    private const float Mix = 0.7f;
    private static readonly Vector3 _baseCloudColor = new(
        198 / 255f,
        224 / 255f,
        244 / 255f
    );

    private static SamplerState _prevSamplerState = SamplerState.LinearClamp;

    private static FullscreenEffect _extractLuminanceEffect;
    private static FullscreenEffect _generateGradientsEffect;
    private static SpriteBatchEffect _cloudShadingEffect;

    private static BlurRenderer _blurRenderer;

    private static Texture2D[] _vanillaCloudTextures;
    private static Texture2D[] _fancyCloudTextures;
    private static bool _overrideCloudTextures;

    internal static void Load()
    {
        var effect = EffectLoader.Load("CloudShading");
        _extractLuminanceEffect = new(effect, "ExtractLuminance");
        _generateGradientsEffect = new(effect, "GenerateGradients");
        _cloudShadingEffect = new(effect, "CloudShading");

        _blurRenderer = new();

        AddHooks();
    }

    internal static void Unload()
    {
        Dispose();

        _prevSamplerState = null;
        _extractLuminanceEffect = null;
        _generateGradientsEffect = null;
        _cloudShadingEffect = null;
        _blurRenderer = null;
    }

    internal static void Dispose()
    {
        _blurRenderer?.Dispose();

        if (_vanillaCloudTextures is not null && _fancyCloudTextures is not null)
        {
            for (var i = 0; i < _fancyCloudTextures.Length; ++i)
            {
                if (
                    i < TextureAssets.Cloud.Length
                    && _vanillaCloudTextures[i] is not null
                    && !ReferenceEquals(
                        _fancyCloudTextures[i],
                        TextureAssets.Cloud[i].Value
                    )
                )
                {
                    _fancyCloudTextures[i]?.Dispose();
                }
            }
        }

        _vanillaCloudTextures = null;
        _fancyCloudTextures = null;
    }

    private static void AddHooks()
    {
        var mainClass = typeof(Main);
        var detourMethod = mainClass.GetMethod(
            "<DrawSurfaceBG>g__DrawCloud|1826_0",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (detourMethod is not null)
        {
            try
            {
                MonoModHooks.Add(detourMethod, _Main_DrawCloud);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }

        On_Main.DrawSurfaceBG += _Main_DrawSurfaceBG;
        IL_Main.DrawSurfaceBG += IL_Main_DrawSurfaceBG;
    }

    private static void _Main_DrawSurfaceBG(On_Main.orig_DrawSurfaceBG orig, Main self)
    {
        if (!MainGraphics.DoingCapture)
        {
            SettingsSystem._useFancyClouds = false;
        }

        if (!SettingsSystem._useFancyClouds)
        {
            _overrideCloudTextures = false;
            orig(self);
            return;
        }

        UpdateCloudTextures();

        var gamma = PostProcessing.ContentGamma();

        var baseColor = _baseCloudColor;
        ColorUtils.GammaToLinear(ref baseColor);
        baseColor /= ColorUtils.Luma(baseColor);
        ColorUtils.LinearToGamma(ref baseColor);

        var zoomWithFlipping = MainGraphics.InCameraMode
            ? Vector2.One
            : new Vector2(1f, MathF.Sign(Main.GameViewMatrix.TransformationMatrix.M22));

        var hour = GameTimeUtils.CalculateCurrentHour();
        var (skyLightAngle, _, skyLightMult) =
            FancySkyLighting.CalculateSkyLightAngleAndMultiplier(hour);
        var normalMapSkyGradientMult = zoomWithFlipping;

        _cloudShadingEffect
            .SetParameter("InverseGamma", 1f / gamma)
            .SetParameter("BaseColor", baseColor)
            .SetParameter(
                "SkyLightGradient",
                -normalMapSkyGradientMult
                    * new Vector2(
                        (float)Math.Cos(skyLightAngle),
                        (float)Math.Sin(skyLightAngle)
                    )
            )
            .SetParameter("SkyLightMult", Mix * (float)skyLightMult);

        _overrideCloudTextures = true;
        try
        {
            orig(self);
        }
        finally
        {
            _overrideCloudTextures = true;
        }
    }

    private static void IL_Main_DrawSurfaceBG(ILContext context)
    {
        try
        {
            var cursor = new ILCursor(context);

            var beginMethod = typeof(FancySkyClouds)
                .GetMethod(nameof(Begin), BindingFlags.NonPublic | BindingFlags.Static)
                .AssertNotNull();
            var endMethod = typeof(FancySkyClouds)
                .GetMethod(nameof(End), BindingFlags.NonPublic | BindingFlags.Static)
                .AssertNotNull();
            const float Layer1Mult = 0.6f;
            const float Layer2Mult = 1f;
            const float Layer3Mult = 1f;
            const float Layer4Mult = 1f;
            const float Layer5Mult = 1f;

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(13)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer1Mult);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(13),
                instruction => instruction.OpCode == OpCodes.Ldc_I4,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // cloud background
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(21)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer2Mult);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(21),
                instruction => instruction.OpCode == OpCodes.Ldarg_0,
                instruction => instruction.OpCode == OpCodes.Ldfld,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // cloud background
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(22)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer3Mult);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(22),
                instruction => instruction.OpCode == OpCodes.Ldarg_0,
                instruction => instruction.OpCode == OpCodes.Ldfld,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(23)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer4Mult);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(23),
                instruction => instruction.OpCode == OpCodes.Ldc_I4,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);

            // individual clouds
            cursor.GotoNext(
                MoveType.AfterLabel,
                instruction => instruction.MatchLdcI4(0),
                instruction => instruction.MatchStloc(31)
            );
            cursor.Emit(OpCodes.Ldc_R4, Layer5Mult);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Call, beginMethod);
            cursor.GotoNext(
                MoveType.After,
                instruction => instruction.MatchLdloc(31),
                instruction => instruction.OpCode == OpCodes.Ldc_I4,
                instruction => instruction.OpCode == OpCodes.Blt
            );
            cursor.Emit(OpCodes.Call, endMethod);
        }
        catch (Exception)
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<FancyLightingMod>(), context);
        }
    }

    private delegate void orig_Main_DrawCloud(int cloudIndex, Color color, float yOffset);

    private static void _Main_DrawCloud(
        orig_Main_DrawCloud orig,
        int cloudIndex,
        Color color,
        float yOffset
    )
    {
        // This code is adapted from vanilla

        if (!_overrideCloudTextures)
        {
            orig(cloudIndex, color, yOffset);
            return;
        }

        var cloud = Main.cloud[cloudIndex];
        var texture = _fancyCloudTextures[cloud.type];
        var vanillaTexture = _vanillaCloudTextures[cloud.type] ?? texture;
        var position = new Vector2(
            cloud.position.X + (vanillaTexture.Width * 0.5f),
            yOffset + (vanillaTexture.Height * 0.5f)
        );
        var paddingX = (texture.Width - vanillaTexture.Width) / 2;
        var paddingY = (texture.Height - vanillaTexture.Height) / 2;
        var sourceRectangle = new Rectangle(
            paddingX,
            paddingY,
            texture.Width - (2 * paddingX),
            texture.Height - (2 * paddingY)
        );
        var rotation = cloud.rotation;
        var origin = new Vector2(
            (texture.Width * 0.5f) - paddingX,
            (texture.Height * 0.5f) - paddingY
        );
        var scale = cloud.scale;
        var effects = cloud.spriteDir;
        var drawData = new DrawData(
            texture,
            position,
            sourceRectangle,
            color,
            rotation,
            origin,
            scale,
            effects
        );
        var modCloud = cloud.ModCloud;
        if (
            modCloud == null
            || modCloud.Draw(Main.spriteBatch, cloud, cloudIndex, ref drawData)
        )
        {
            drawData.Draw(Main.spriteBatch);
        }
    }

    private static void UpdateCloudTextures()
    {
        var rendered = false;
        var sbParams = Main.spriteBatch.GetParameters();

        var textureCount = TextureAssets.Cloud.Length;
        ArrayUtils.MakeSizePreserveContents(ref _vanillaCloudTextures, textureCount);
        ArrayUtils.MakeSizePreserveContents(ref _fancyCloudTextures, textureCount);

        for (var i = 0; i < textureCount; ++i)
        {
            var vanillaTexture = TextureAssets.Cloud[i].Value;
            ref var savedVanillaTexture = ref _vanillaCloudTextures[i];

            if (savedVanillaTexture is null)
            {
                _fancyCloudTextures[i] = vanillaTexture;
            }
            else if (!ReferenceEquals(savedVanillaTexture, vanillaTexture))
            {
                ref var fancyTexture = ref _fancyCloudTextures[i];
                fancyTexture?.Dispose();
                fancyTexture = vanillaTexture;
                savedVanillaTexture = null;
            }
        }

        foreach (var cloud in Main.cloud)
        {
            if (!cloud.active)
            {
                continue;
            }

            var textureIndex = cloud.type;
            if (textureIndex < 0 || textureIndex >= textureCount)
            {
                continue;
            }

            if (_vanillaCloudTextures[textureIndex] is not null)
            {
                continue;
            }

            var vanillaTexture = TextureAssets.Cloud[textureIndex].Value;
            _fancyCloudTextures[textureIndex] = GenerateFancyCloudTexture(
                vanillaTexture,
                wrap: false
            );

            if (!rendered)
            {
                Main.spriteBatch.End();
            }

            rendered = true;
            _vanillaCloudTextures[textureIndex] = vanillaTexture;
        }

        _blurRenderer.Dispose();

        if (rendered)
        {
            Blitter.BlitOrSwap(
                ref MainGraphics.ScreenTarget,
                ref MainGraphics.ScreenTargetSwap
            );
            MainGraphics.AssignScreenTargets();
            Blitter.Blit(MainGraphics.ScreenTargetSwap, MainGraphics.ScreenTarget);
            Main.spriteBatch.Begin(sbParams);
        }
    }

    private static Texture2D GenerateFancyCloudTexture(
        Texture2D vanillaCloudTexture,
        bool wrap
    )
    {
        const int BlurPadding = 128;
        // use padding of 2 instead of 1 to preserve alignment of double-size pixels in texture with 2x2 blocks used for ddx/ddy
        const int FinalPadding = 2;

        var gamma = PostProcessing.ContentGamma();

        var samplerState = wrap
            ? CustomSamplerStates.LinearWrapUClampV
            : SamplerState.LinearClamp;
        var blurWidth = vanillaCloudTexture.Width + (wrap ? 0 : 2 * BlurPadding);
        var blurHeight = vanillaCloudTexture.Height + (2 * BlurPadding);
        var finalWidth = vanillaCloudTexture.Width + (wrap ? 0 : 2 * FinalPadding);
        var finalHeight = vanillaCloudTexture.Height + (2 * FinalPadding);

        var luminanceTarget = new RenderTarget2D(
            Main.graphics.GraphicsDevice,
            blurWidth,
            blurHeight,
            false,
            SurfaceFormat.Single,
            DepthFormat.None
        );
        var fancyTexture = new RenderTarget2D(
            Main.graphics.GraphicsDevice,
            finalWidth,
            finalHeight
        );

        _extractLuminanceEffect
            .SetParameter("Gamma", gamma)
            .SetParameter(
                "Scale",
                new Vector2(
                    (float)blurWidth / vanillaCloudTexture.Width,
                    (float)blurHeight / vanillaCloudTexture.Height
                )
            );
        Blitter.Blit(vanillaCloudTexture, luminanceTarget, _extractLuminanceEffect);

        _blurRenderer.Blur(
            luminanceTarget,
            luminanceTarget,
            BlurPassCount,
            redOnly: true,
            additiveBlend: true,
            additiveBlendMinLevel: 2,
            format: SurfaceFormat.Single,
            samplerState: samplerState
        );

        _generateGradientsEffect
            .SetParameter("Gamma", gamma)
            .SetParameter("InverseGamma", 1f / gamma)
            .SetParameter(
                "Scale",
                new Vector2(
                    (float)finalWidth / blurWidth,
                    (float)finalHeight / blurHeight
                )
            )
            .SetParameter(
                "CloudScale",
                new Vector2(
                    (float)finalWidth / vanillaCloudTexture.Width,
                    (float)finalHeight / vanillaCloudTexture.Height
                )
            );
        MainGraphics.ResetSavedTextures();
        MainGraphics.SetTexture(8, vanillaCloudTexture, SamplerState.PointClamp);
        Blitter.Blit(luminanceTarget, fancyTexture, _generateGradientsEffect);
        MainGraphics.RestoreSavedTextures();

        luminanceTarget.Dispose();
        return fancyTexture;
    }

    private static void Begin(float mult, bool wrap)
    {
        if (!SettingsSystem._useFancyClouds)
        {
            return;
        }

        var sbParams = Main.spriteBatch.GetParameters();
        _prevSamplerState = sbParams.samplerState;
        Main.spriteBatch.End();

        var newSamplerState = wrap
            ? CustomSamplerStates.LinearWrapUClampV
            : SamplerState.LinearClamp;

        var cloudShadingStrength = Math.Clamp(
            PreferencesConfig.Instance.CloudShadingMultiplier(),
            0f,
            1f
        );

        _cloudShadingEffect.SetParameter(
            "NormalMapStrength",
            mult * cloudShadingStrength
        );
        SpriteBatchEffectLoader.Apply(_cloudShadingEffect);
        Main.spriteBatch.Begin(
            sbParams with
            {
                samplerState = newSamplerState,
                customEffect = null,
            }
        );
    }

    private static void End()
    {
        if (!SettingsSystem._useFancyClouds)
        {
            return;
        }

        var sbParams = Main.spriteBatch.GetParameters();
        Main.spriteBatch.End();
        SpriteBatchEffectLoader.Reset();
        Main.spriteBatch.Begin(sbParams with { samplerState = _prevSamplerState });
    }
}
