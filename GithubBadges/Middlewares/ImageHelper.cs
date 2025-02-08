using System;
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


namespace GithubBadges.Middlewares
{
    public static class ImageHelper
    {
        public static string ConvertToSVG(IFormFile file)
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
    <clipPath id=""clip"">
      <rect width=""{newWidth}"" height=""{newHeight}"" rx=""8"" />
    </clipPath>
  </defs>
  <image href=""data:{mimeType};base64,{base64Image}"" width=""{newWidth}px"" height=""{newHeight}px"" 
         clip-path=""url(#clip)"" preserveAspectRatio=""xMidYMid meet"" />
</svg>";
            }
            else if (extension.Equals(".svg"))
            {
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    file.CopyTo(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                string subSvgContent = Encoding.UTF8.GetString(fileBytes);
                subSvgContent = RemoveComments(subSvgContent);
                subSvgContent = AddClipPathAttribute(subSvgContent);
                subSvgContent = Regex.Replace(subSvgContent, @"<\?xml[^>]+\?>", string.Empty, RegexOptions.IgnoreCase);

                byte[] svgInImageBytes = ConvertSvgToPng(subSvgContent);

                int newHeight = 100;
                int newWidth = GetWidthByHeight(newHeight, svgInImageBytes);

                string base64Image = Convert.ToBase64String(svgInImageBytes);

                svgContent = $@"
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{newWidth}px"" height=""{newHeight}px"" x=""0"" y=""0"">
  <defs>
    <clipPath id=""clip"">
      <rect width=""{newWidth}"" height=""{newHeight}"" rx=""8"" />
    </clipPath>
  </defs>
  <image href=""data:image/png;base64,{base64Image}"" width=""{newWidth}px"" height=""{newHeight}px"" 
         clip-path=""url(#clip)"" preserveAspectRatio=""xMidYMid meet"" />
</svg>";

                /*                Console.WriteLine("");
                                Console.WriteLine($"Computed dimensions: width:{newWidth} height:{newHeight}");
                                Console.WriteLine(subSvgContent);

                                svgContent = "asdf";*/
            }


            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new NotSupportedException("Unsupported file type for conversion to SVG.");
            }

            Console.WriteLine(svgContent);
            return svgContent;
        }

        public static byte[] ConvertSvgToPng(string svgContent)
        {
            using (var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent)))
            {
                var readSettings = new MagickReadSettings
                {
                    Format = MagickFormat.Svg,
                    Density = new Density(300)
                };

                using (var image = new MagickImage(svgStream, readSettings))
                {
                    image.Format = MagickFormat.Png;
                    return image.ToByteArray();
                }
            }
        }

        public static string AddClipPathAttribute(string svgContent)
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
                string updatedTag = firstSvgTag.TrimEnd('>', ' ') + " clip-path=\"url(#clip)\">";
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
            if (string.IsNullOrWhiteSpace(svgContent))
                throw new ArgumentNullException(nameof(svgContent));

            var widthMatch = Regex.Match(svgContent, "width\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            var heightMatch = Regex.Match(svgContent, "height\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

            if (!widthMatch.Success || !heightMatch.Success)
                throw new ArgumentException("SVG content does not contain valid width and height attributes.");

            double originalWidth = ParseDimension(widthMatch.Groups[1].Value);
            double originalHeight = ParseDimension(heightMatch.Groups[1].Value);

            return (originalWidth, originalHeight);
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

        public static string Resize(string svg, double newWidth, double newHeight)
        {
            var svgTagMatch = Regex.Match(svg, @"<svg\b[^>]*>", RegexOptions.IgnoreCase);
            if (!svgTagMatch.Success)
            {
                return svg;
            }

            string svgTag = svgTagMatch.Value;
            string updatedSvgTag = svgTag;

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

            return svg.Replace(svgTag, updatedSvgTag);
        }
    }
}
