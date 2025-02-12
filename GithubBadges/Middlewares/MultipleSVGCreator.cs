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


            Console.WriteLine($"Constrain width and height: {fitContent}");

            foreach (var image in imageObjects)
            {
                // Console.WriteLine("yeet");
                //string badgeSvg = Encoding.UTF8.GetString(image.imageInByte); 
                //= SingleSVGCreator.Create(image.folderName, image.imageInByte, image.imageExtension);

                // WARNING: INITIAL HEIGHT OF SVG IS ALWAYS 100

                string badgeSvg = new string(image.imageInSvg);
                int newHeight = 40;
                int newWidth = newHeight; // fallback
                if (!fitContent)
                {
                    newWidth = ImageHelper.GetWidthByHeight(newHeight, badgeSvg);
                }
                //int badgeWidth = ImageHelper.GetWidthByHeight(40, badgeSvg);
                badgeSvg = ImageHelper.ResizeSVG(badgeSvg, newWidth, newHeight);
                badgeSvgs.Add((badgeSvg, newWidth));
            }

            /*            foreach (var badgeSvg in badgeSvgs)
                        {
                            Console.WriteLine(badgeSvg.svg);
                        }*/

            // ENHANCED LOGIC
            // Note that ALWAYS row & col <= badgeSvgs.Count

            // 1. if both are 0, meaning undefined, we're gonna automatically display flow
            if (row == 0 && col == 0)
            {
                row = 1;
                col = badgeSvgs.Count;
            }
            // 2. if row > 0 and col is 0, we're gonna adjust col
            else if (row > 0 && col == 0)
            {
                col = badgeSvgs.Count / row;
                if (badgeSvgs.Count % row > 0)
                {
                    col++;
                }
            }
            // 3. if col > 0 and row is 0, we're gonna adjust row
            else if (col > 0 && row == 0)
            {
                row = badgeSvgs.Count / col;
                if (badgeSvgs.Count % col > 0)
                {
                    row++;
                }
            }

            // check under-sized grid
            if (row * col < badgeSvgs.Count)
            {
                throw new ArgumentException(
                    $"Invalid grid dimensions: Number of items exceeds the number of cell of the grid."
                );
            }
            // check oversized grid
            //Console.WriteLine(requiredRows);


            // check valid cols
            int requiredRows = (int)Math.Ceiling(badgeSvgs.Count / (double)col);
            if (row != requiredRows)
            {
                throw new ArgumentException(
                    $"Invalid grid dimensions: Exactly {requiredRows} rows are needed for {badgeSvgs.Count} items."
                );
            }

            //Console.WriteLine($"Row: {row} Column: {col}");

            //***********************************************
            // in this point, row and column are always valid.
            //***********************************************

            // TODO: CHECK LOGICAL SPECIFICATIONS HERE
            // TODO: CHECK LOGICAL SPECIFICATIONS HERE
            // TODO: CHECK LOGICAL SPECIFICATIONS HERE
            // TODO: CHECK LOGICAL SPECIFICATIONS HERE


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
