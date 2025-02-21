﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using Svg;
using System.Drawing.Imaging;
using System.Drawing;
using SkiaSharp;
using SkiaSharp.Extended.Svg;
using System.Diagnostics;
using ImageMagick;
using System.Management;
using System.Globalization;
using Svg.Skia;
using Aspose.Svg;
using Aspose.Svg.Converters;
using Aspose.Svg.Saving;


namespace GithubBadges.Middlewares
{
    public static class ImageHelper
    {
        public static string ConvertToSVG(IFormFile file, string fileName) // fileName is unique
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            string extension = Path.GetExtension(file.FileName).ToLower();
            string[] convertExtensions = { ".png", ".jpg", ".jpeg" };
            string svgContent = string.Empty;

            if (convertExtensions.Contains(extension))
            {
                string mimeType = (extension == ".jpg" || extension == ".jpeg") ? "image/jpeg" : "image/png";
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    file.CopyTo(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                int newHeight = 100;
                int newWidth = GetWidthByHeight(newHeight, fileBytes);

                string base64Image = Convert.ToBase64String(fileBytes);

                svgContent = $@"
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{newWidth}px"" height=""{newHeight}px"" x=""0"" y=""0"">
  <defs>
    <clipPath id=""clip-{fileName}"">
      <rect width=""100%"" height=""100%"" rx=""8"" />
    </clipPath>
  </defs>
  <image href=""data:{mimeType};base64,{base64Image}"" width=""100%"" height=""100%"" 
         clip-path=""url(#clip-{fileName})"" preserveAspectRatio=""xMidYMid meet"" />
</svg>";
            }
            else if (extension.Equals(".svg"))
            {
                //Console.WriteLine("Parsing svg to png");
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    file.CopyTo(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                string subSvgContent = Encoding.UTF8.GetString(fileBytes);

                // Console.WriteLine($"\nInitial SVG:\n{subSvgContent}\n----------------");
                subSvgContent = RemoveComments(subSvgContent);
                subSvgContent = Regex.Replace(subSvgContent, @"<\?xml[^>]+\?>", string.Empty, RegexOptions.IgnoreCase);
                subSvgContent = AddClipPathAttribute(subSvgContent, fileName);

                string viewBoxString = "";

                int newHeight = 100;
                int newWidth = GetWidthByHeight(newHeight, subSvgContent);

                //Console.WriteLine($"-------------\nDimensions:\nHeight: {newHeight}\nWidth: {newWidth}------------\n");

                subSvgContent = TrimViewBox(subSvgContent, out viewBoxString);
                viewBoxString = $"viewBox=\"{viewBoxString}\"";

                //Console.WriteLine(subSvgContent);

                
                // OK. nothing WRONG till this point


                //byte[] svgInImageBytes = ConvertSvgToPng(subSvgContent); // ok, I don't really need this, but it's for dimension...


/*                newHeight = 65;
                newWidth = 284;*/

                // Console.WriteLine($"\nSubSVG:\n{subSvgContent}\n----------------");

                subSvgContent = ResizeSVG(subSvgContent, newWidth, newHeight);

                subSvgContent = Regex.Replace(subSvgContent, @"<svg\b([^>]*)>", match =>
                {
                    string innerTag = match.Value;
                    innerTag = Regex.Replace(innerTag, @"\b(width|height)\s*=\s*""[^""]*""", string.Empty, RegexOptions.IgnoreCase);
                    return innerTag.Replace("<svg", "<svg width=\"100%\" height=\"100%\"");
                }, RegexOptions.IgnoreCase);

                // Console.WriteLine($"\nSubSvg after change:\n{subSvgContent}\n----------------");
                // Console.WriteLine($"\nDetected viewbox:\n{viewBoxString}\n----------------");

                //string base64Image = Convert.ToBase64String(svgInImageBytes);

                svgContent = $@"
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{newWidth}px"" height=""{newHeight}px"" {viewBoxString} x=""0"" y=""0"">
  <defs>
    <clipPath id=""clip-{fileName}"">
      <rect width=""100%"" height=""100%"" rx=""8"" />
    </clipPath>
  </defs>
  {subSvgContent}
</svg>";

                // Console.WriteLine($"\nFinal SVG:\n{svgContent}\n----------------");
                /*                //Console.WriteLine("Final SVG:");
                                //Console.WriteLine(svgContent);*/
                /*                Console.WriteLine("");
                                                Console.WriteLine($"Computed dimensions: width:{newWidth} height:{newHeight}");
                                                Console.WriteLine(subSvgContent);*/
            }


            if (string.IsNullOrWhiteSpace(svgContent))
            {
                // Console.WriteLine("Something went wrong :(");
                throw new NotSupportedException("Unsupported file type for conversion to SVG.");
            }

            // Console.WriteLine(svgContent);
            return svgContent;
        }

        public static string TrimViewBox(string svgContent, out string viewBoxString)
        {
            // think of logic...
            // we're gonna get string before viewbox, 
            // viewbox element
            // string after viewbox?
            viewBoxString = "";

            Regex regex = new Regex(@"(<svg\b[^>]*?)\s*viewBox\s*=\s*""([^""]*)""([^>]*>)", RegexOptions.IgnoreCase);
            Match match = regex.Match(svgContent);

            if (match.Success)
            {
                viewBoxString = match.Groups[2].Value.Trim();
                string replacement = match.Groups[1].Value + match.Groups[3].Value;

                svgContent = regex.Replace(svgContent, replacement, 1);
            }

            return svgContent;
        }

        public static byte[] ConvertSvgToPng(string svgContent)
        {
/*            Console.WriteLine("Convert following to png");
            Console.WriteLine(svgContent);
            Console.WriteLine("\n------------------------");*/


            // TestSKSvg();
            var svg = new Svg.Skia.SKSvg();
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent)))
            {
                svg.Load(stream);
            }

            if (svg.Picture == null)
            {
                throw new Exception("Failed to load SVG content.");
            }

            SKRect svgRect = svg.Picture.CullRect;
            int width = (int)Math.Ceiling(svgRect.Width);
            int height = (int)Math.Ceiling(svgRect.Height);

            using (var bitmap = new SKBitmap(width, height))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.DrawPicture(svg.Picture);


                }

                using (var image = SKImage.FromBitmap(bitmap))
                {
                    using (var data = image.Encode(SKEncodedImageFormat.Png, quality: 100))
                    {
                        return data.ToArray();
                    }
                }
            }
        }

        /*        public static byte[] ConvertSvgToPng(string svgContent)
                {
                    using (var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent)))
                    {
                        var readSettings = new MagickReadSettings
                        {
                            Format = MagickFormat.Svg,
                            Density = new Density(300),
                            BackgroundColor = MagickColors.Transparent
                        };

                        using (var image = new MagickImage(svgStream, readSettings))
                        {
                            image.Format = MagickFormat.Png;
                            return image.ToByteArray();
                        }
                    }
                }*/

        public static string AddClipPathAttribute(string svgContent, string fileName)
        {
            if (string.IsNullOrWhiteSpace(svgContent))
                return svgContent;

            int svgStartIndex = svgContent.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (svgStartIndex == -1)
                return svgContent;

            int svgTagEndIndex = svgContent.IndexOf('>', svgStartIndex);
            if (svgTagEndIndex == -1)
                return svgContent;

            string firstSvgTag = svgContent.Substring(svgStartIndex, svgTagEndIndex - svgStartIndex + 1);

            if (!Regex.IsMatch(firstSvgTag, @"\bclip-path\s*=", RegexOptions.IgnoreCase))
            {
                string updatedTag = firstSvgTag.TrimEnd('>', ' ') + $" clip-path=\"url(#clip-{fileName})\">";
                svgContent = svgContent.Replace(firstSvgTag, updatedTag);
            }

            return svgContent;
        }

        public static string RemoveComments(string svgContent)
        {
            if (string.IsNullOrWhiteSpace(svgContent))
                return svgContent;

            return Regex.Replace(svgContent, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
        }

        public static int GetWidthByHeight(int height, byte[] fileBytes)
        {
            using (var msForDimensions = new MemoryStream(fileBytes))
            {
                using (var image = SixLabors.ImageSharp.Image.Load(msForDimensions))
                {
                    return (int)(image.Width * (height / (double)image.Height));
                }
            }
        }

        public static string UpdatePosition(string svg, double x, double y)
        {
            int svgStartIndex = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (svgStartIndex != -1)
            {
                int svgTagEndIndex = svg.IndexOf('>', svgStartIndex);
                if (svgTagEndIndex != -1)
                {
                    string firstSvgTag = svg.Substring(svgStartIndex, svgTagEndIndex - svgStartIndex + 1);

                    bool hasX = Regex.IsMatch(firstSvgTag, @"\bx\s*=", RegexOptions.IgnoreCase);
                    bool hasY = Regex.IsMatch(firstSvgTag, @"\by\s*=", RegexOptions.IgnoreCase);
                    string modifiedTag = firstSvgTag;

                    if (hasX && hasY)
                    {
                        modifiedTag = Regex.Replace(
                            modifiedTag,
                            @"\bx\s*=\s*[""'][^""']*[""']",
                            $"x=\"{x}\"",
                            RegexOptions.IgnoreCase);
                        modifiedTag = Regex.Replace(
                            modifiedTag,
                            @"\by\s*=\s*[""'][^""']*[""']",
                            $"y=\"{y}\"",
                            RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        string tagWithoutClose = firstSvgTag.TrimEnd('>');
                        if (!hasX)
                        {
                            tagWithoutClose += $" x=\"{x}\"";
                        }
                        if (!hasY)
                        {
                            tagWithoutClose += $" y=\"{y}\"";
                        }
                        modifiedTag = tagWithoutClose + ">";
                    }

                    svg = svg.Replace(firstSvgTag, modifiedTag);
                }
            }
            return svg;
        }

        public static int GetWidthByHeight(int height, string svgContent)
        {
            var (originalWidth, originalHeight) = GetSvgDimensions(svgContent);
            double scaleFactor = (double)height / originalHeight;
            return (int)Math.Round(originalWidth * scaleFactor);
        }

        public static int GetHeightByWidth(int width, string svgContent)
        {
            var (originalWidth, originalHeight) = GetSvgDimensions(svgContent);
            double scaleFactor = (double)width / originalWidth;
            return (int)Math.Round(originalHeight * scaleFactor);
        }

        private static (double Width, double Height) GetSvgDimensions(string svgContent)
        {
            // Console.WriteLine(svgContent);

            if (string.IsNullOrWhiteSpace(svgContent))
                throw new ArgumentNullException(nameof(svgContent));

            var widthMatch = Regex.Match(svgContent, "width\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            var heightMatch = Regex.Match(svgContent, "height\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);


            if (widthMatch.Success && heightMatch.Success)
            {
                double width = ParseDimension(widthMatch.Groups[1].Value);
                double height = ParseDimension(heightMatch.Groups[1].Value);
                return (width, height);
            }

            var viewBoxMatch = Regex.Match(svgContent, "viewBox\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (viewBoxMatch.Success)
            {
                var parts = viewBoxMatch.Groups[1].Value
                    .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4 &&
                    double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double vbWidth) &&
                    double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double vbHeight))
                {
                    return (vbWidth, vbHeight);
                }
                else
                {
                    throw new ArgumentException("SVG viewBox attribute does not contain valid values.");
                }
            }

            throw new ArgumentException("SVG content does not contain valid width/height attributes or a viewBox attribute.");
        }

        private static double ParseDimension(string dimension)
        {
            string numericPart = Regex.Replace(dimension, "[^0-9.\\-]", "");
            if (double.TryParse(numericPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }

            throw new FormatException($"Unable to parse dimension: {dimension}");
        }

        // ISSUE WITH RESIZE <= solved

        public static string ResizeSVG(string svg, double newWidth, double newHeight)
        {
            string newWidthStr = newWidth.ToString("0.##") + "px";
            string newHeightStr = newHeight.ToString("0.##") + "px";

            Regex svgTagRegex = new Regex(@"<svg\b[^>]*>", RegexOptions.IgnoreCase);
            Match match = svgTagRegex.Match(svg);
            if (!match.Success)
            {
                return svg;
            }

            string originalSvgTag = match.Value;
            string updatedSvgTag = originalSvgTag;

            if (Regex.IsMatch(originalSvgTag, @"\bwidth\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
            {
                updatedSvgTag = Regex.Replace(
                    updatedSvgTag,
                    @"\bwidth\s*=\s*""[^""]*""",
                    $"width=\"{newWidthStr}\"",
                    RegexOptions.IgnoreCase
                );
            }
            else
            {
                updatedSvgTag = updatedSvgTag.Replace("<svg", $"<svg width=\"{newWidthStr}\"");
            }

            if (Regex.IsMatch(originalSvgTag, @"\bheight\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
            {
                updatedSvgTag = Regex.Replace(
                    updatedSvgTag,
                    @"\bheight\s*=\s*""[^""]*""",
                    $"height=\"{newHeightStr}\"",
                    RegexOptions.IgnoreCase
                );
            }
            else
            {
                updatedSvgTag = updatedSvgTag.Replace("<svg", $"<svg height=\"{newHeightStr}\"");
            }

            string result = svg.Substring(0, match.Index)
                            + updatedSvgTag
                            + svg.Substring(match.Index + match.Length);
            return result;
        }

        // => USE ResizeSVG()
        /*public static string Resize(string svg, double newWidth, double newHeight)
        {
            // Console.WriteLine(svg);
            // ok, so we need to grab three things: outer <svg> tag, rect tag in <defs>, <image> tag
            // this point, every tag are guarenteed to have "width" and "height" attribute
            // both tag will be adjusted to newWidth and newHeight
            var svgTagMatch = Regex.Match(svg, @"<svg\b[^>]*>", RegexOptions.IgnoreCase);
            var rectTagMatch = Regex.Match(svg, @"<rect\b[^>]*>", RegexOptions.IgnoreCase);
            var imageTagMatch = Regex.Match(svg, @"<image\b[^>]*>", RegexOptions.IgnoreCase);
            if (!svgTagMatch.Success && !rectTagMatch.Success && !imageTagMatch.Success)
            {
                return svg;
            }

            *//**********************************
             *            SVG TAG             * 
             **********************************//*
            {
                string svgTag = svgTagMatch.Value;
                string updatedSvgTag = svgTag;
                //Console.WriteLine(updatedSvgTag);

                if (Regex.IsMatch(svgTag, @"\bwidth\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
                {
                    updatedSvgTag = Regex.Replace(
                        updatedSvgTag,
                        @"\bwidth\s*=\s*""[^""]*""",
                        $"width=\"{newWidth.ToString("0.##", CultureInfo.InvariantCulture)}px\"",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    int insertIndex = updatedSvgTag.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) + 4;
                    updatedSvgTag = updatedSvgTag.Insert(insertIndex, $" width=\"{newWidth.ToString("0.##", CultureInfo.InvariantCulture)}px\"");
                }

                if (Regex.IsMatch(svgTag, @"\bheight\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
                {
                    updatedSvgTag = Regex.Replace(
                        updatedSvgTag,
                        @"\bheight\s*=\s*""[^""]*""",
                        $"height=\"{newHeight.ToString("0.##", CultureInfo.InvariantCulture)}px\"",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    int insertIndex = updatedSvgTag.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) + 4;
                    updatedSvgTag = updatedSvgTag.Insert(insertIndex, $" height=\"{newHeight.ToString("0.##", CultureInfo.InvariantCulture)}px\"");
                }

                svg = svg.Replace(svgTag, updatedSvgTag);
*//*                Console.WriteLine("----- Updated SVG -----");
                Console.WriteLine(updatedSvgTag);*//*
            }


            *//**********************************
             *            RECT TAG            * 
             **********************************//*
            {
                string rectTag = rectTagMatch.Value;
                string updatedRectTag = rectTag;
                if (Regex.IsMatch(rectTag, @"\bwidth\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
                {
                    updatedRectTag = Regex.Replace(
                        updatedRectTag,
                        @"\bwidth\s*=\s*""[^""]*""",
                        $"width=\"{newWidth.ToString("0.##", CultureInfo.InvariantCulture)}\"",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    int insertIndex = updatedRectTag.IndexOf("<rect", StringComparison.OrdinalIgnoreCase) + 4;
                    updatedRectTag = updatedRectTag.Insert(insertIndex, $" width=\"{newWidth.ToString("0.##", CultureInfo.InvariantCulture)}\"");
                }

                if (Regex.IsMatch(updatedRectTag, @"\bheight\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
                {
                    updatedRectTag = Regex.Replace(
                        updatedRectTag,
                        @"\bheight\s*=\s*""[^""]*""",
                        $"height=\"{newHeight.ToString("0.##", CultureInfo.InvariantCulture)}\"",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    int insertIndex = updatedRectTag.IndexOf("<rect", StringComparison.OrdinalIgnoreCase) + 4;
                    updatedRectTag = updatedRectTag.Insert(insertIndex, $" height=\"{newHeight.ToString("0.##", CultureInfo.InvariantCulture)}\"");
                }
                svg = svg.Replace(rectTag, updatedRectTag);
*//*                Console.WriteLine("----- Updated RECT -----");
                Console.WriteLine(updatedRectTag);
                Console.WriteLine($"Current: {svg}");*//*
            }

            *//**********************************
             *           IMAGE TAG            * 
             **********************************//*
            {
                string imageTag = imageTagMatch.Value;
                string updatedImageTag = imageTag;
                if (Regex.IsMatch(imageTag, @"\bwidth\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
                {
                    updatedImageTag = Regex.Replace(
                        updatedImageTag,
                        @"\bwidth\s*=\s*""[^""]*""",
                        $"width=\"{newWidth.ToString("0.##", CultureInfo.InvariantCulture)}px\"",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    int insertIndex = updatedImageTag.IndexOf("<image", StringComparison.OrdinalIgnoreCase) + 4;
                    updatedImageTag = updatedImageTag.Insert(insertIndex, $" width=\"{newWidth.ToString("0.##", CultureInfo.InvariantCulture)}px\"");
                }

                if (Regex.IsMatch(updatedImageTag, @"\bheight\s*=\s*""[^""]*""", RegexOptions.IgnoreCase))
                {
                    updatedImageTag = Regex.Replace(
                        updatedImageTag,
                        @"\bheight\s*=\s*""[^""]*""",
                        $"height=\"{newHeight.ToString("0.##", CultureInfo.InvariantCulture)}px\"",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    int insertIndex = updatedImageTag.IndexOf("<image", StringComparison.OrdinalIgnoreCase) + 4;
                    updatedImageTag = updatedImageTag.Insert(insertIndex, $" height=\"{newHeight.ToString("0.##", CultureInfo.InvariantCulture)}px\"");
                }
                svg = svg.Replace(imageTag, updatedImageTag);
*//*                Console.WriteLine("----- Updated IMAGE -----");
                Console.WriteLine(updatedImageTag);*//*
            }

            return svg;
        }*/
    }
}
