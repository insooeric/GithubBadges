using GithubBadges.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GithubBadges.Middlewares
{
    public static class MultipleSVGCreator
    {
        public static string Create(List<ImageObject> imageObjects, int row, int col, bool fitContent)
        {
            const double targetHeight = 40;
            const double gap = 5;

            List<(string svg, double width)> badgeSvgs = new List<(string svg, double width)>();
            foreach (var image in imageObjects)
            {
                string badgeSvg = Encoding.UTF8.GetString(image.imageInByte); //= SingleSVGCreator.Create(image.folderName, image.imageInByte, image.imageExtension);
                badgeSvg = ImageHelper.Resize(badgeSvg, ImageHelper.GetWidthByHeight(40, badgeSvg), 40);
                var widthMatch = Regex.Match(
                    badgeSvg,
                    @"<svg[^>]*\bwidth\s*=\s*[""']([\d\.]+)(?:\s*px)?[""']",
                    RegexOptions.IgnoreCase);

                double badgeWidth = targetHeight;
                if (!fitContent && widthMatch.Success &&
                    double.TryParse(widthMatch.Groups[1].Value, out double parsedWidth))
                {
                    badgeWidth = parsedWidth;
                }

                badgeSvgs.Add((badgeSvg, badgeWidth));
            }

            foreach (var badgeSvg in badgeSvgs)
            {
                Console.WriteLine(badgeSvg.svg);
            }

            if (row == 1 && col == 1)
            {
                col = badgeSvgs.Count;
            }

            double[] columnWidths = new double[col];
            for (int j = 0; j < col; j++)
            {
                double maxWidth = 0;
                for (int i = 0; i < row; i++)
                {
                    int index = i * col + j;
                    if (index < badgeSvgs.Count)
                    {
                        maxWidth = Math.Max(maxWidth, badgeSvgs[index].width);
                    }
                }
                columnWidths[j] = maxWidth;
            }

            double overallWidth = 0;
            double[] columnOffsets = new double[col];
            for (int j = 0; j < col; j++)
            {
                columnOffsets[j] = overallWidth;
                overallWidth += columnWidths[j];
                if (j < col - 1)
                {
                    overallWidth += gap;
                }
            }

            double overallHeight = 0;
            double[] rowOffsets = new double[row];
            for (int i = 0; i < row; i++)
            {
                rowOffsets[i] = overallHeight;
                overallHeight += targetHeight;
                if (i < row - 1)
                {
                    overallHeight += gap;
                }
            }

            StringBuilder svgBuilder = new StringBuilder();
            svgBuilder.AppendLine(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{overallWidth}\" height=\"{overallHeight}\" viewBox=\"0 0 {overallWidth} {overallHeight}\">");

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    int index = i * col + j;
                    if (index >= badgeSvgs.Count)
                    {
                        continue;
                    }

                    var badge = badgeSvgs[index];
                    double cellX = columnOffsets[j];
                    double cellY = rowOffsets[i];

                    string positionedBadgeSvg = ImageHelper.UpdatePosition(badge.svg, cellX, cellY);

                    svgBuilder.AppendLine(positionedBadgeSvg);
                }
            }

            svgBuilder.AppendLine("</svg>");
            return svgBuilder.ToString();
        }
    }
}
