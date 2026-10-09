using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerSpriteSheetBuilder
{
    private const int FrameWidth = 16;
    private const int FrameHeight = 32;
    private const int FramesPerRow = 4;
    private const float BakeScale = 4f;
    private static readonly GeneratedFrameSpec[] BaseFrameLayout =
    {
        new(0, 0, 2, 0, false),
        new(1, 0, 2, 1, false),
        new(2, 0, 2, 0, false),
        new(3, 0, 2, 2, false),
        new(0, 1, 1, 6, false),
        new(1, 1, 1, 7, false),
        new(2, 1, 1, 6, false),
        new(3, 1, 1, 8, false),
        new(0, 2, 0, 12, false),
        new(1, 2, 0, 13, false),
        new(2, 2, 0, 12, false),
        new(3, 2, 0, 14, false),
        new(0, 3, 3, 6, true),
        new(1, 3, 3, 7, true),
        new(2, 3, 3, 6, true),
        new(3, 3, 3, 8, true),
    };

    private readonly IMonitor monitor;

    public WorkerSpriteSheetBuilder(IMonitor monitor)
    {
        this.monitor = monitor;
    }

    public Texture2D BuildSheet(WorkerAppearanceData appearance)
    {
        GraphicsDevice graphicsDevice = Game1.graphics.GraphicsDevice;
        int outputWidth = FrameWidth * FramesPerRow;
        int outputHeight = FrameHeight * ((WorkerFishingAnimationPolicy.FirstSheetFrame
            + WorkerFishingAnimationPolicy.FramesPerDirection * 4) / FramesPerRow);

        using RenderTarget2D renderTarget = new(
            graphicsDevice,
            outputWidth,
            outputHeight,
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None);
        using SpriteBatch spriteBatch = new(graphicsDevice);

        Farmer renderWorker = new();
        renderWorker.Name = TestWorkerDefinition.DisplayName;
        renderWorker.displayName = TestWorkerDefinition.DisplayName;
        renderWorker.currentLocation = Game1.currentLocation;
        renderWorker.Position = Vector2.Zero;
        appearance.ApplyTo(renderWorker);

        RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
        bool previousDrawingForUi = FarmerRenderer.isDrawingForUI;

        try
        {
            graphicsDevice.SetRenderTarget(renderTarget);
            graphicsDevice.Clear(Color.Transparent);

            FarmerRenderer.isDrawingForUI = true;
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect: null,
                transformMatrix: Matrix.CreateScale(1f / BakeScale));

            foreach (GeneratedFrameSpec frame in BaseFrameLayout)
            {
                this.DrawFrame(spriteBatch, renderWorker, frame);
            }

            // Every profession needs these shared slash poses for scythe actions.
            {
                for (int facing = 0; facing < 4; facing++)
                {
                    renderWorker.FarmerSprite.getAnimationFromIndex(
                        WorkerCombatSwingPolicy.SwordAnimation(facing), renderWorker.FarmerSprite,
                        WorkerCombatSwingPolicy.SwordAnimationIntervalMilliseconds,
                        WorkerCombatSwingPolicy.PoseCount, flip: false, secondaryArm: false);
                    // Copy before DrawFrame replaces the renderer's animation.
                    // Loading metadata does not execute any sword/damage callbacks.
                    FarmerSprite.AnimationFrame[] poses = renderWorker.FarmerSprite.CurrentAnimation
                        .Take(WorkerCombatSwingPolicy.PoseCount).ToArray();
                    for (int pose = 0; pose < poses.Length; pose++)
                    {
                        int index = WorkerCombatSwingPolicy.SheetFrame(facing, pose);
                        this.DrawFrame(spriteBatch, renderWorker, new GeneratedFrameSpec(
                            index % FramesPerRow, index / FramesPerRow, facing,
                            poses[pose].frame, poses[pose].flip), poses[pose]);
                    }
                }
            }

            foreach (WorkerWorkAnimationKind kind in new[] { WorkerWorkAnimationKind.Water, WorkerWorkAnimationKind.Gather, WorkerWorkAnimationKind.Axe })
            {
                for (int facing = 0; facing < 4; facing++)
                {
                    renderWorker.FarmerSprite.getAnimationFromIndex(
                        WorkerWorkAnimationPolicy.NativeAnimation(kind, facing), renderWorker.FarmerSprite,
                        80, WorkerWorkAnimationPolicy.FramesPerDirection, flip: false, secondaryArm: false);
                    FarmerSprite.AnimationFrame[] poses = renderWorker.FarmerSprite.CurrentAnimation
                        .Where(frame => kind != WorkerWorkAnimationKind.Gather || frame.milliseconds > 0).ToArray();
                    for (int pose = 0; pose < WorkerWorkAnimationPolicy.FramesPerDirection; pose++)
                    {
                        FarmerSprite.AnimationFrame native = poses[Math.Min(pose, poses.Length - 1)];
                        int index = WorkerWorkAnimationPolicy.SheetFrame(kind, facing, pose);
                        this.DrawFrame(spriteBatch, renderWorker, new GeneratedFrameSpec(
                            index % FramesPerRow, index / FramesPerRow, facing, native.frame, native.flip), native);
                    }
                }
            }

            // Raw fishing frames only; no vanilla rod animation callbacks run.
            for (int facing = 0; facing < 4; facing++)
            for (int pose = 0; pose < WorkerFishingAnimationPolicy.FramesPerDirection; pose++)
            {
                int index = WorkerFishingAnimationPolicy.FirstSheetFrame
                    + WorkerCombatSwingPolicy.DirectionRow(facing) * WorkerFishingAnimationPolicy.FramesPerDirection + pose;
                this.DrawFrame(spriteBatch, renderWorker, new GeneratedFrameSpec(index % FramesPerRow,
                    index / FramesPerRow, facing, WorkerFishingAnimationPolicy.NativeFrame(facing, pose), facing == 3));
            }

            spriteBatch.End();
        }
        finally
        {
            FarmerRenderer.isDrawingForUI = previousDrawingForUi;
            graphicsDevice.SetRenderTargets(previousTargets);
        }

        Color[] outputPixels = new Color[outputWidth * outputHeight];
        renderTarget.GetData(outputPixels);

        Texture2D spriteSheet = new(graphicsDevice, outputWidth, outputHeight);
        spriteSheet.SetData(outputPixels);

        this.monitor.Log(
            $"Generated worker sprite sheet from the saved appearance at {outputWidth}x{outputHeight}.",
            LogLevel.Trace);

        return spriteSheet;
    }

    public Texture2D BuildPortrait(Texture2D spriteSheet)
    {
        const int faceSize = 16;
        const int portraitSize = 64;
        Color[] sheetPixels = new Color[spriteSheet.Width * spriteSheet.Height];
        spriteSheet.GetData(sheetPixels);
        Color[] portraitPixels = new Color[portraitSize * portraitSize];
        Color background = new(104, 70, 48);

        for (int y = 0; y < portraitSize; y++)
        for (int x = 0; x < portraitSize; x++)
        {
            Color facePixel = sheetPixels[(y / (portraitSize / faceSize)) * spriteSheet.Width + x / (portraitSize / faceSize)];
            portraitPixels[y * portraitSize + x] = Color.Lerp(background, facePixel, facePixel.A / 255f);
        }

        Texture2D portrait = new(Game1.graphics.GraphicsDevice, portraitSize, portraitSize);
        portrait.SetData(portraitPixels);
        return portrait;
    }

    private void DrawFrame(SpriteBatch spriteBatch, Farmer renderWorker, GeneratedFrameSpec frame,
        FarmerSprite.AnimationFrame? workPose = null)
    {
        renderWorker.FacingDirection = frame.FacingDirection;
        renderWorker.FarmerSprite.setCurrentSingleFrame(frame.FarmerFrame, 32000, secondaryArm: false, flip: frame.Flip);
        if (workPose is FarmerSprite.AnimationFrame nativePose)
        {
            // Preserve vanilla arm/flip/position metadata, but never attach its
            // player damage or movement callbacks to a worker rendering proxy.
            nativePose.frameStartBehavior = null;
            nativePose.frameEndBehavior = null;
            renderWorker.FarmerSprite.currentAnimation[0] = nativePose;
        }

        Vector2 position = new(
            frame.Column * FrameWidth * BakeScale,
            frame.Row * FrameHeight * BakeScale);

        renderWorker.FarmerRenderer.draw(
            spriteBatch,
            renderWorker.FarmerSprite.CurrentAnimationFrame,
            renderWorker.FarmerSprite.CurrentFrame,
            renderWorker.FarmerSprite.SourceRect,
            position,
            Vector2.Zero,
            0f,
            Color.White,
            0f,
            1f,
            renderWorker);
    }

    private readonly record struct GeneratedFrameSpec(int Column, int Row, int FacingDirection, int FarmerFrame, bool Flip);
}
