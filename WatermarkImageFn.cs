using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using SixLabors.Primitives;
using System;

namespace DemoFn
{
    public static class WatermarkImageFn
    {
        [FunctionName("WatermarkImageFn")]
        public static void Run([BlobTrigger("images/{name}", Connection = "Storage_Connection")]Stream image,
            [Blob("outputs/{name}", FileAccess.Write)] Stream outImage,
            string name,
            ILogger log)
        {
            Image<Rgba32> img = Image.Load(image);
            Font font = SystemFonts.CreateFont("Arial", 6); // for scaling water mark size is largly ignored.
            using (var img2 = img.Clone(ctx => ctx.ApplyScalingWaterMark(font, "Insurance Portal", Rgba32.Azure, 5, false)))
            {
                img2.SaveAsJpeg(outImage);
            }
        }

        public static IImageProcessingContext<TPixel> ApplyScalingWaterMark<TPixel>(this IImageProcessingContext<TPixel> processingContext, Font font, string text, TPixel color, float padding, bool wordwrap)
           where TPixel : struct, IPixel<TPixel>
        {
            if (wordwrap)
            {
                return processingContext.ApplyScalingWaterMarkWordWrap(font, text, color, padding);
            }
            else
            {
                return processingContext.ApplyScalingWaterMarkSimple(font, text, color, padding);
            }
        }

        public static IImageProcessingContext<TPixel> ApplyScalingWaterMarkSimple<TPixel>(this IImageProcessingContext<TPixel> processingContext, Font font, string text, TPixel color, float padding)
            where TPixel : struct, IPixel<TPixel>
        {
            return processingContext.Apply(img =>
            {
                float targetWidth = img.Width - (padding * 2);
                float targetHeight = img.Height - (padding * 2);

                // measure the text size
                SizeF size = TextMeasurer.Measure(text, new RendererOptions(font));

                //find out how much we need to scale the text to fill the space (up or down)
                float scalingFactor = Math.Min(img.Width / size.Width, img.Height / size.Height);

                //create a new font
                Font scaledFont = new Font(font, scalingFactor * font.Size);

                var center = new PointF(img.Width / 2, img.Height / 2);
                var textGraphicOptions = new TextGraphicsOptions(true)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                img.Mutate(i => i.DrawText(textGraphicOptions, text, scaledFont, color, center));
            });
        }

        public static IImageProcessingContext<TPixel> ApplyScalingWaterMarkWordWrap<TPixel>(this IImageProcessingContext<TPixel> processingContext, Font font, string text, TPixel color, float padding)
            where TPixel : struct, IPixel<TPixel>
        {
            return processingContext.Apply(img =>
            {
                float targetWidth = img.Width - (padding * 2);
                float targetHeight = img.Height - (padding * 2);

                float targetMinHeight = img.Height - (padding * 3); // must be with in a margin width of the target height

                // now we are working i 2 dimensions at once and can't just scale because it will cause the text to
                // reflow we need to just try multiple times

                var scaledFont = font;
                SizeF s = new SizeF(float.MaxValue, float.MaxValue);

                float scaleFactor = (scaledFont.Size / 2);// everytime we change direction we half this size
                int trapCount = (int)scaledFont.Size * 2;
                if (trapCount < 10)
                {
                    trapCount = 10;
                }

                bool isTooSmall = false;

                while ((s.Height > targetHeight || s.Height < targetMinHeight) && trapCount > 0)
                {
                    if (s.Height > targetHeight)
                    {
                        if (isTooSmall)
                        {
                            scaleFactor = scaleFactor / 2;
                        }

                        scaledFont = new Font(scaledFont, scaledFont.Size - scaleFactor);
                        isTooSmall = false;
                    }

                    if (s.Height < targetMinHeight)
                    {
                        if (!isTooSmall)
                        {
                            scaleFactor = scaleFactor / 2;
                        }
                        scaledFont = new Font(scaledFont, scaledFont.Size + scaleFactor);
                        isTooSmall = true;
                    }
                    trapCount--;

                    s = TextMeasurer.Measure(text, new RendererOptions(scaledFont)
                    {
                        WrappingWidth = targetWidth
                    });
                }

                var center = new PointF(padding, img.Height / 2);
                var textGraphicOptions = new TextGraphicsOptions(true)
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    WrapTextWidth = targetWidth
                };
                img.Mutate(i => i.DrawText(textGraphicOptions, text, scaledFont, color, center));
            });
        }

    }
}
