//------------------------------------------------------------------------------
// <copyright file="ColorStreamRenderer.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace BodyPaint
{
    using Microsoft.Kinect;
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using System.Collections.Generic;
    using System;

    /// <summary>
    /// This class renders the current color stream frame.
    /// </summary>
    public class ColorStreamRenderer : Object2D
    {
        /// <summary>
        /// This child responsible for rendering the color stream's skeleton.
        /// </summary>
        private readonly SkeletonStreamRenderer skeletonStream;
        
        /// <summary>
        /// The last frame of color data.
        /// </summary>
        private byte[] colorData;
        private short[] depthData;

        /// <summary>
        /// The color frame as a texture.
        /// </summary>
        private Texture2D colorTexture;

        /// <summary>
        /// The back buffer where color frame is scaled as requested by the Size.
        /// </summary>
        private RenderTarget2D backBuffer;
        
        /// <summary>
        /// This Xna effect is used to swap the Red and Blue bytes of the color stream data.
        /// </summary>
        private Effect kinectColorVisualizer;

        /// <summary>
        /// Whether or not the back buffer needs updating.
        /// </summary>
        private bool needToRedrawBackBuffer = true;

        Dictionary<JointType, Color>[] ColouredJoints = new Dictionary<JointType, Color>[6];

        Random random = new Random();
        public ColorStreamRenderer(Game game)
            : base(game)
        {
            this.skeletonStream = new SkeletonStreamRenderer(game, this.SkeletonToColorMap);

            for (int sk = 0; sk < 6; sk++)
            {
                ColouredJoints[sk] = new Dictionary<JointType, Color>();
                for (int jt = 0; jt < 20; jt++)
                {
                    Color r = new Color((byte)random.Next(255), (byte)random.Next(255), (byte)random.Next(255), 255);
                    ColouredJoints[sk].Add((JointType)jt, r);
                }
            }
        }

        /// <summary>
        /// Initializes the necessary children.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            this.Size = new Vector2(Game.GraphicsDevice.Viewport.Width, Game.GraphicsDevice.Viewport.Height);
        }

        /// <summary>
        /// The update method where the new color frame is retrieved.
        /// </summary>
        /// <param name="gameTime">The elapsed game time.</param>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // If the sensor is not found, not running, or not connected, stop now
            if (null == this.Chooser.Sensor ||
                false == this.Chooser.Sensor.IsRunning ||
                KinectStatus.Connected != this.Chooser.Sensor.Status)
            {
                return;
            }

            using (var frame = this.Chooser.Sensor.ColorStream.OpenNextFrame(0))
            {
                // Sometimes we get a null frame back if no data is ready
                if (frame == null)
                {
                    return;
                }

                // Reallocate values if necessary
                if (this.colorData == null || this.colorData.Length != frame.PixelDataLength)
                {
                    this.colorData = new byte[frame.PixelDataLength];

                    this.colorTexture = new Texture2D(
                        this.Game.GraphicsDevice, 
                        frame.Width, 
                        frame.Height, 
                        false, 
                        SurfaceFormat.Color);

                    this.backBuffer = new RenderTarget2D(
                        this.Game.GraphicsDevice, 
                        frame.Width, 
                        frame.Height, 
                        false, 
                        SurfaceFormat.Color, 
                        DepthFormat.None,
                        this.Game.GraphicsDevice.PresentationParameters.MultiSampleCount, 
                        RenderTargetUsage.PreserveContents);            
                }

                frame.CopyPixelDataTo(this.colorData);

                DrawOverColour();

                this.needToRedrawBackBuffer = true;
            }

            // Update the skeleton renderer
            this.skeletonStream.Update(gameTime);
        }


        void DrawOverColour() //THIS ONE
        {
            using (var frame = this.Chooser.Sensor.DepthStream.OpenNextFrame(0))
            {
                // Sometimes we get a null frame back if no data is ready
                if (frame == null)
                {
                    return;
                }

                // Reallocate values if necessary
                if (this.depthData == null || this.depthData.Length != frame.PixelDataLength)
                {

                    this.depthData = new short[frame.PixelDataLength];

                }
                frame.CopyPixelDataTo(this.depthData);
            }

            if (SkeletonStreamRenderer.skeletonData == null)
                return;

            //get skeleton points
            Dictionary<JointType, Vector2>[] SkeletonPoints2D = new Dictionary<JointType, Vector2>[6];
            for (int sk = 0; sk < 6; sk++)
            {
                SkeletonPoints2D[sk] = new Dictionary<JointType, Vector2>();

                Skeleton skeleton = SkeletonStreamRenderer.skeletonData[sk];
                foreach (Joint jt in skeleton.Joints)
                {
                    SkeletonPoints2D[sk].Add(jt.JointType, SkeletonToColorMap(jt.Position));
                }
            }

            //iterate through colour pixels.
            for (int y = 0; y < 480; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    int d = x + y * 640;
                    int id = (this.depthData[d] & 0x07) - 1;
                    Vector2 pixel = new Vector2(x, y);

                    if (id == - 1 )
                        continue;

                    double _R = 0;
                    double _G = 0;
                    double _B = 0;
                    double _A = 0;
                    double _sum = 0, coeff;
                    for (int j = 0; j < 20; j++)
                    {
                        Vector2 pos = SkeletonPoints2D[id][(JointType)j];
                        double dist = Vector2.DistanceSquared(pos, pixel);
                        if (dist > 150*150)
                            continue;

                        Color jointColor = ColouredJoints[id][(JointType)j];
                        coeff = 1.0 / (dist + 1);
                        _R += jointColor.R * coeff;
                        _G += jointColor.G * coeff;
                        _B += jointColor.B * coeff;
                        _A += jointColor.A * coeff;
                        _sum += coeff;
                    }

                    _R /= _sum;
                    _G /= _sum;
                    _B /= _sum;
                    _A /= _sum;

                    Color color = new Color((byte)_R, (byte)_G, (byte)_B, (byte)_A);

                    this.colorData[4 * d + 0] = (byte)(((255 - color.A) * this.colorData[4 * d + 0] + color.A * color.B) / 255);
                    this.colorData[4 * d + 1] = (byte)(((255 - color.A) * this.colorData[4 * d + 1] + color.A * color.G) / 255);
                    this.colorData[4 * d + 2] = (byte)(((255 - color.A) * this.colorData[4 * d + 2] + color.A * color.R) / 255);

                }
            }
        }

        /// <summary>
        /// This method renders the color and skeleton frame.
        /// </summary>
        /// <param name="gameTime">The elapsed game time.</param>
        public override void Draw(GameTime gameTime)
        {
            // If we don't have the effect load, load it
            if (null == this.kinectColorVisualizer)
            {
                this.LoadContent();
            }

            // If we don't have a target, don't try to render
            if (null == this.colorTexture)
            {
                return;
            }

            if (this.needToRedrawBackBuffer)
            {
                // Set the backbuffer and clear
                this.Game.GraphicsDevice.SetRenderTarget(this.backBuffer);
                this.Game.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0);

                this.colorTexture.SetData<byte>(this.colorData);

                // Draw the color image
                this.SharedSpriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, this.kinectColorVisualizer);
                this.SharedSpriteBatch.Draw(this.colorTexture, Vector2.Zero, Color.White);
                this.SharedSpriteBatch.End();

                // Draw the skeleton
                this.skeletonStream.Draw(gameTime);

                // Reset the render target and prepare to draw scaled image
                this.Game.GraphicsDevice.SetRenderTargets(null);

                // No need to re-render the back buffer until we get new data
                this.needToRedrawBackBuffer = false;
            }

            // Draw the scaled texture
            this.SharedSpriteBatch.Begin();
            this.SharedSpriteBatch.Draw(
                this.backBuffer,
                new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y),
                null,
                Color.White);
            this.SharedSpriteBatch.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// This method loads the Xna effect.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            // This effect is necessary to remap the BGRX byte data we get
            // to the XNA color RGBA format.
            this.kinectColorVisualizer = Game.Content.Load<Effect>("KinectColorVisualizer");
        }

        /// <summary>
        /// This method is used to map the SkeletonPoint to the color frame.
        /// </summary>
        /// <param name="point">The SkeletonPoint to map.</param>
        /// <returns>A Vector2 of the location on the color frame.</returns>
        private Vector2 SkeletonToColorMap(SkeletonPoint point)
        {
            if ((null != Chooser.Sensor) && (null != Chooser.Sensor.ColorStream))
            {
                // This is used to map a skeleton point to the color image location
                var colorPt = Chooser.Sensor.MapSkeletonPointToColor(point, Chooser.Sensor.ColorStream.Format);
                return new Vector2(colorPt.X, colorPt.Y);
            }

            return Vector2.Zero;
        }
    }
}
