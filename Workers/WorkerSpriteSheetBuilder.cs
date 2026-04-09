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
    private const int Rows = 4;
    private const float BakeScale = 4f;
    private static readonly GeneratedFrameSpec[] BaseFrameLayout =
    {
        new(0, 0, 2, 1, false),
        new(1, 0, 2, 0, false),
        new(2, 0, 2, 2, false),
        new(3, 0, 2, 0, false),
        new(0, 1, 1, 7, false),
        new(1, 1, 1, 6, false),
        new(2, 1, 1, 8, false),
        new(3, 1, 1, 6, false),
        new(0, 2, 0, 13, false),
        new(1, 2, 0, 12, false),
        new(2, 2, 0, 14, false),
        new(3, 2, 0, 12, false),
        new(0, 3, 3, 7, true),
        new(1, 3, 3, 6, true),
        new(2, 3, 3, 8, true),
        new(3, 3, 3, 6, true),
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
        int outputHeight = FrameHeight * Rows;

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

    private void DrawFrame(SpriteBatch spriteBatch, Farmer renderWorker, GeneratedFrameSpec frame)
    {
        renderWorker.FacingDirection = frame.FacingDirection;
        renderWorker.FarmerSprite.setCurrentSingleFrame(frame.FarmerFrame, 32000, secondaryArm: false, flip: frame.Flip);

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
