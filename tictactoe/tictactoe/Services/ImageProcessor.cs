using Android.Content;
using Android.Provider;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.Shapes;
using OpenCvSharp;
using OpenCvSharp.XPhoto;
using System.IO;
using System.Threading.Tasks;
using tictactoe.Models;
using tictactoe.Services;
using static Android.Content.Res.Resources;

namespace tictactoe.Services
{
    public class ImageProcessor : IImageProcessor
    {

        private static async Task<string> SaveToGalleryAsync(Context context, Mat image, string fileName)
        {
            var values = new ContentValues();
            values.Put(MediaStore.MediaColumns.DisplayName, fileName);
            values.Put(MediaStore.MediaColumns.MimeType, "image/png");
            values.Put(MediaStore.MediaColumns.RelativePath, "Pictures/tictactoe/");

            var uri = context.ContentResolver.Insert(
                MediaStore.Images.Media.ExternalContentUri,
                values
            );

            using var stream = context.ContentResolver.OpenOutputStream(uri);

            byte[] pngBytes = image.ToBytes(".png");
            await stream.WriteAsync(pngBytes, 0, pngBytes.Length);
            await stream.FlushAsync();

            return uri.ToString();
        }


        List<int> ClusterLinesAggressive(List<int> positions, int tolerance, int mergeTolerance)
        {
            if (positions.Count == 0) return positions;

            var sorted = positions.OrderBy(x => x).ToList();
            var clusters = new List<List<int>>();
            var currentCluster = new List<int> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - currentCluster.Last() <= tolerance)
                    currentCluster.Add(sorted[i]);
                else
                {
                    clusters.Add(currentCluster);
                    currentCluster = new List<int> { sorted[i] };
                }
            }
            clusters.Add(currentCluster);

            var averages = clusters.Select(c => (int)c.Average()).ToList();

            var merged = new List<int>();
            merged.Add(averages[0]);

            for (int i = 1; i < averages.Count; i++)
            {
                if (averages[i] - merged.Last() <= mergeTolerance)
                {
                    // merge by taking the midpoint
                    int mid = (merged.Last() + averages[i]) / 2;
                    merged[merged.Count - 1] = mid;
                }
                else
                {
                    merged.Add(averages[i]);
                }
            }

            return merged;
        }
        private double ComputeAverageSpacing(List<int> positions)
        {
            if (positions.Count < 2)
                return 0;

            positions.Sort();
            double sum = 0;
            int count = 0;

            for (int i = 1; i < positions.Count; i++)
            {
                sum += (positions[i] - positions[i - 1]);
                count++;
            }

            return sum / count;
        }

        public async Task<Game> ProcessImageAsync(string boardImagePath)
        {
            var mat = Cv2.ImRead(boardImagePath, ImreadModes.Color);

            
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            Scalar meanVal = Cv2.Mean(gray);
            double brightness = meanVal.Val0;

            using var denoise = new Mat();
            Cv2.BilateralFilter(gray, denoise, 9, 75, 75);

            int blockSize, constantC;
            if (brightness > 180) { blockSize = 31; constantC = 2; }
            else if (brightness > 90) { blockSize = 51; constantC = 3; }
            else { blockSize = 71; constantC = 8; }

            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(
                denoise, thresh, 255,
                AdaptiveThresholdTypes.MeanC,
                ThresholdTypes.BinaryInv,
                blockSize, constantC
            );

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(7, 7));
            using var morph = new Mat();
            Cv2.MorphologyEx(thresh, morph, MorphTypes.Close, kernel);

            // Debug save
            var context = Android.App.Application.Context;
            await SaveToGalleryAsync(context, morph, "debug_grid_mask.png");

            
         
            using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            using var opened = new Mat();

            // 2 iteráció
            Cv2.MorphologyEx(morph, opened, MorphTypes.Open, openKernel, iterations: 2);


            // Debug save
            await SaveToGalleryAsync(context, opened, "debug_opening.png");

            
            var maskForRotation = opened.Clone();


            LineSegmentPoint[] hough = Cv2.HoughLinesP(
                opened,          // binary mask
                1,              // rho
                Math.PI / 180,  // theta
                80,             // threshold (votes) - tuned to pick strong lines
                Math.Max(30, opened.Width / 10), // minLineLength 
                Math.Max(5, opened.Width / 200)   // maxLineGap
            );

            // debug: visualize Hough lines and compute simple stats
            var linesForDebug = hough ?? Array.Empty<LineSegmentPoint>();

            // create color overlay for visualization
            using var overlay = new Mat();
            Cv2.CvtColor(opened, overlay, ColorConversionCodes.GRAY2BGR);


           
            var angles = new List<double>();
            var lengths = new List<double>();
            var horizontalDetected = new List<LineSegmentPoint>();
            var verticalDetected = new List<LineSegmentPoint>();
            var obliqueDetected = new List<LineSegmentPoint>();

            foreach (var ln in linesForDebug)
            {
                double dx = ln.P2.X - ln.P1.X;
                double dy = ln.P2.Y - ln.P1.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                Scalar color;

                if (Math.Abs(angleDeg) > 85) // vertical-ish
                {
                    verticalDetected.Add(ln);
                    color = new Scalar(0, 255, 0);  // green
                }
                else if (Math.Abs(angleDeg) < 5) // horizontal-ish
                {
                    horizontalDetected.Add(ln);
                    color = new Scalar(255, 0, 0); // blue
                }
                else
                {
                    obliqueDetected.Add(ln);
                    color = new Scalar(0, 255, 255); // yellow
                }

                int thickness = Math.Min(6, Math.Max(1, (int)(length / Math.Max(1, opened.Width / 200.0))));
                Cv2.Line(overlay, ln.P1, ln.P2, color, thickness);
            }

            // Save debug overlay
            await SaveToGalleryAsync(context, overlay, "debug_hough_overlay.png");

            // If both vertical and horizontal are above 20 then it doesnt require rotation, 20 may seem high at first but this to avoid cases where the image
            // was taken so that the 2 lines of an "X" are the horizontal and vertical axis
            bool hasStableOrientation = horizontalDetected.Count > 20 && verticalDetected.Count > 20;

            var mask = opened.Clone();
            if (hasStableOrientation)
            {
                
                int tester = horizontalDetected.Count;
                int tester2 = verticalDetected.Count;

                mask = opened.Clone();
                await SaveToGalleryAsync(context, mask, "debug_rotation_skipped.png");
            }
            else
            {
                if (obliqueDetected.Count > 0)
                {
                    // pick the longest oblique line (already filtered earlier)
                    var longestLine = obliqueDetected
                        .OrderByDescending(ln =>
                        {
                            double dx0 = ln.P2.X - ln.P1.X;
                            double dy0 = ln.P2.Y - ln.P1.Y;
                            return dx0 * dx0 + dy0 * dy0;
                        })
                        .First();

                    // compute its angle (degrees)
                    double dx = longestLine.P2.X - longestLine.P1.X;
                    double dy = longestLine.P2.Y - longestLine.P1.Y;
                    double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI; // -180..180

                    // compute new canvas size so we don't crop corners
                    int w = opened.Width;
                    int h = opened.Height;
                    double rad = Math.Abs(angleDeg * Math.PI / 180.0);
                    double cos = Math.Cos(rad);
                    double sin = Math.Sin(rad);
                    int newW = (int)Math.Round(h * sin + w * cos);
                    int newH = (int)Math.Round(h * cos + w * sin);

                    // rotation matrix about the original center
                    var center = new Point2f(w / 2f, h / 2f);
                    var rotMat = Cv2.GetRotationMatrix2D(center, angleDeg, 1.0);

                    // adjust translation so rotated image is centered in new canvas
                    rotMat.Set(0, 2, rotMat.Get<double>(0, 2) + (newW / 2.0 - center.X));
                    rotMat.Set(1, 2, rotMat.Get<double>(1, 2) + (newH / 2.0 - center.Y));

                    // warp into the expanded canvas
                    var rotatedMask = new Mat(newH, newW, opened.Type(), Scalar.All(0));
                    Cv2.WarpAffine(
                        opened,
                        rotatedMask,
                        rotMat,
                        new OpenCvSharp.Size(newW, newH),
                        InterpolationFlags.Nearest,
                        BorderTypes.Constant,
                        Scalar.All(0)
                    );

                    // Save rotated mask for debug
                    await SaveToGalleryAsync(context, rotatedMask, "debug_grid_mask_rotated.png");

                    // hough again for debug
                    var afterLines = Cv2.HoughLinesP(rotatedMask, 1, Math.PI / 180.0, 80, Math.Max(30, rotatedMask.Width / 10), Math.Max(5, rotatedMask.Width / 200)) ?? Array.Empty<LineSegmentPoint>();
                    using var overlayAfter = new Mat();
                    Cv2.CvtColor(rotatedMask, overlayAfter, ColorConversionCodes.GRAY2BGR);

                    foreach (var ln in afterLines)
                    {
                        double dx2 = ln.P2.X - ln.P1.X;
                        double dy2 = ln.P2.Y - ln.P1.Y;
                        double a = Math.Atan2(dy2, dx2) * 180.0 / Math.PI;

                        Scalar color = (Math.Abs(a) < 10) ? new Scalar(255, 0, 0) : (Math.Abs(a) > 80 ? new Scalar(0, 255, 0) : new Scalar(0, 255, 255));
                        int thickness = Math.Min(6, Math.Max(1, (int)(Math.Sqrt(dx2 * dx2 + dy2 * dy2) / Math.Max(1, rotatedMask.Width / 200.0))));
                        Cv2.Line(overlayAfter, ln.P1, ln.P2, color, thickness);
                    }

                    // draw the chosen original line transformed into the rotated canvas (red)
                    // apply rotMat: [a b c; d e f] * [x;y;1]
                    // these are ONLY for debug and visualization
                    double a00 = rotMat.Get<double>(0, 0), a01 = rotMat.Get<double>(0, 1), a02 = rotMat.Get<double>(0, 2);
                    double a10 = rotMat.Get<double>(1, 0), a11 = rotMat.Get<double>(1, 1), a12 = rotMat.Get<double>(1, 2);
                    OpenCvSharp.Point rp1 = new OpenCvSharp.Point((int)Math.Round(a00 * longestLine.P1.X + a01 * longestLine.P1.Y + a02), (int)Math.Round(a10 * longestLine.P1.X + a11 * longestLine.P1.Y + a12));
                    OpenCvSharp.Point rp2 = new OpenCvSharp.Point((int)Math.Round(a00 * longestLine.P2.X + a01 * longestLine.P2.Y + a02), (int)Math.Round(a10 * longestLine.P2.X + a11 * longestLine.P2.Y + a12));
                    Cv2.Line(overlayAfter, rp1, rp2, new Scalar(0, 0, 255), 3);

                    await SaveToGalleryAsync(context, overlayAfter, "debug_hough_after_rotation.png");

                    // give downstream code the corrected mask
                    mask = rotatedMask; // use 'mask' variable from here on
                }
                else
                {
                    // fallback: no oblique lines, continue with original morph
                    mask = opened.Clone();
                    await SaveToGalleryAsync(context, mask, "debug_rotation_no_lines.png");
                }
            }


            


            
            LineSegmentPoint[] lines = Cv2.HoughLinesP(
                mask,       // binary mask
                1,           // rho resolution
                Math.PI / 180, // theta resolution
                120,         // min votes
                70,          // min line length
                10           // max gap
            );


            var verticalLines = new List<LineSegmentPoint>();
            var horizontalLines = new List<LineSegmentPoint>();

            foreach (var line in lines)
            {
                double dx = line.P2.X - line.P1.X;
                double dy = line.P2.Y - line.P1.Y;
                double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                if (Math.Abs(angle) > 80) verticalLines.Add(line);      // strict vertical
                else if (Math.Abs(angle) < 10) horizontalLines.Add(line); // strict horizontal
            }

   
            List<int> verticalPositions = verticalLines
                .Select(l => (l.P1.X + l.P2.X) / 2)
                .ToList();

            List<int> horizontalPositions = horizontalLines
                .Select(l => (l.P1.Y + l.P2.Y) / 2)
                .ToList();

            double avgV = ComputeAverageSpacing(verticalPositions);
            double avgH = ComputeAverageSpacing(horizontalPositions);

            // fallback if average is too small (bad detection)
            if (avgV < 5) avgV = 25;
            if (avgH < 5) avgH = 25;

            // dynamic tolerance & mergeTolerance
            int tolV = (int)(avgV * 0.75);         // ~40% of expected tile width
            int mergeV = (int)(avgV * 5);       // tiles closer than ~1.2× avg should merge

            int tolH = (int)(avgH * 0.75);
            int mergeH = (int)(avgH * 5);

            verticalPositions = ClusterLinesAggressive(verticalPositions, tolV, mergeV);
            horizontalPositions = ClusterLinesAggressive(horizontalPositions, tolH, mergeH);



            verticalPositions = verticalPositions
                .Where(x => x > 5 && x < mask.Width - 5)
                .ToList();

            horizontalPositions = horizontalPositions
                .Where(y => y > 5 && y < mask.Height - 5)
                .ToList();

            var cellROIs = new List<OpenCvSharp.Rect>();
            for (int i = 0; i < verticalPositions.Count - 1; i++)
            {
                for (int j = 0; j < horizontalPositions.Count - 1; j++)
                {
                    int x = verticalPositions[i];
                    int y = horizontalPositions[j];
                    int w = verticalPositions[i + 1] - x;
                    int h = horizontalPositions[j + 1] - y;
                    cellROIs.Add(new OpenCvSharp.Rect(x, y, w, h));
                }
            }


            using var debug = new Mat();
            Cv2.CvtColor(mask, debug, ColorConversionCodes.GRAY2BGR);
            foreach (var roi in cellROIs)
            {
                Cv2.Rectangle(debug, roi, Scalar.Red, 2);
            }
            await SaveToGalleryAsync(context, debug, "debug_cells_gomoku.png");


            var roiScores = new List<(OpenCvSharp.Rect roi, double score)>();
            foreach (var roi in cellROIs)
            {
                int x = Math.Max(0, roi.X);
                int y = Math.Max(0, roi.Y);
                int w = Math.Min(mask.Width - x, roi.Width);
                int h = Math.Min(mask.Height - y, roi.Height);

                if (w <= 0 || h <= 0) continue;

                
                var fullMat = new Mat(mask, new OpenCvSharp.Rect(x, y, w, h));

                int cropW = w; // 100% width
                int cropH = h; // 100% height
                if (!hasStableOrientation)
                {
                    cropW = (int)(w * 0.90); // 90% width
                    cropH = (int)(h * 0.90); // 90% height
                }

                

                int offsetX = (w - cropW) / 2;
                int offsetY = (h - cropH) / 2;

                // ensure valid crop
                if (cropW <= 0 || cropH <= 0) continue;

                var innerMat = new Mat(fullMat, new OpenCvSharp.Rect(offsetX, offsetY, cropW, cropH));

                double nonZeroRatio = Cv2.CountNonZero(innerMat) / (double)(cropW * cropH);

                roiScores.Add((roi, nonZeroRatio));
            }


            // stop if empty
            if (roiScores.Count == 0)
                return new Game();

            var anchor = roiScores.OrderByDescending(s => s.score).First();
            double anchorScore = anchor.score;

            int total = roiScores.Count;
            int side = (int)Math.Sqrt(total); // 15 for 15x15

            int anchorIndex = roiScores.IndexOf(anchor);
            int ar = anchorIndex / side;
            int ac = anchorIndex % side;

            bool IsNeighbor(int r, int c, int nr, int nc)
            {
                if (nr < 0 || nr >= side || nc < 0 || nc >= side)
                    return false;

                // 4-directional adjacency
                return Math.Abs(r - nr) + Math.Abs(c - nc) == 1;
            }

            var neighborScores = new List<double>();

            for (int i = 0; i < roiScores.Count; i++)
            {
                int r = i / side;
                int c = i % side;

                if (IsNeighbor(ar, ac, r, c))
                    neighborScores.Add(roiScores[i].score);
            }

            double neighborAvg = neighborScores.Count > 0 ? neighborScores.Average() : anchorScore;

            double adaptiveThreshold =
                (anchorScore * 0.50) +
                (neighborAvg * 0.30);

            //int gridSize = Math.Max(cols, rows); //so the box is gridSize*gridSize, find one good point then proceed, probaby one of the maximums or minimums in the way that is bigger
            //identify which one is an X or O, could happen before this
            var useful = new List<(OpenCvSharp.Rect rect, int index, Mat img)>();

            for (int i = 0; i < roiScores.Count; i++)
            {
                var (roi, score) = roiScores[i];

                if (score >= adaptiveThreshold)
                {
                    var roiMat = new Mat(mask, roi);
                    useful.Add((roi, i, roiMat));

                    await SaveToGalleryAsync(context, roiMat, $"roi_useful_{roi.X}_{roi.Y}.png");
                }
            }
            // build an intermediate list with ROI center coordinates
            var usefulMatsList = useful
                .Select(u => new
                {
                    Rect = u.rect,
                    Index = u.index,
                    Img = u.img,
                    Cx = u.rect.X + u.rect.Width / 2.0,
                    Cy = u.rect.Y + u.rect.Height / 2.0
                })
                .ToList();

            // get sorted distinct column centers (X) and row centers (Y)
            var distinctColCenters = usefulMatsList.Select(t => t.Cx).Distinct().OrderBy(x => x).ToList();
            var distinctRowCenters = usefulMatsList.Select(t => t.Cy).Distinct().OrderBy(y => y).ToList();

            if (distinctColCenters.Count == 0 || distinctRowCenters.Count == 0)
            {
                // nothing useful found
                return new Game();
            }

            int usedColsCount = distinctColCenters.Count;
            int usedRowsCount = distinctRowCenters.Count;

            // helper: find index of closest center
            int IndexOfClosest(List<double> centers, double value)
            {
                int best = 0;
                double bestDist = Math.Abs(centers[0] - value);
                for (int i = 1; i < centers.Count; i++)
                {
                    double d = Math.Abs(centers[i] - value);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
                return best;
            }

            // create grid and place Mats
            var grid = new Mat[usedRowsCount, usedColsCount];

            foreach (var it in usefulMatsList)
            {
                int colIdx = IndexOfClosest(distinctColCenters, it.Cx);
                int rowIdx = IndexOfClosest(distinctRowCenters, it.Cy);

                // place ROI mat into reconstructed grid cell
                grid[rowIdx, colIdx] = it.Img;
            }

            // create finalBoard placeholder (string) with same dims
            int[,] finalBoard = new int[usedRowsCount, usedColsCount];


            for (int r = 0; r < usedRowsCount; r++)
            {
                for (int c = 0; c < usedColsCount; c++)
                {
                    var cell = grid[r, c];
                    if (cell == null)
                    {
                        finalBoard[r, c] = 0;   // empty slot
                        continue;
                    }

                    int symbol = ClassifySymbol(cell);
                    finalBoard[r, c] = symbol;

                }
            }

            string resultToPass = "Not finished";
            string nextMoveToPass = "";


            int rows = finalBoard.GetLength(0);
            int cols = finalBoard.GetLength(1);


            int[,] boardToPass = new int[15, 15];


            (int r, int c)? firstX = null;

            for (int r0 = 0; r0 < rows; r0++)
            {
                for (int c0 = 0; c0 < cols; c0++)
                {
                    if (finalBoard[r0, c0] == 1)   // X found
                    {
                        firstX = (r0, c0);
                        break;
                    }
                }
                if (firstX != null) break;
            }

            // fallback: if there is NO X at all, just anchor to first non-empty
            if (firstX == null)
            {
                for (int r0 = 0; r0 < rows; r0++)
                {
                    for (int c0 = 0; c0 < cols; c0++)
                    {
                        if (finalBoard[r0, c0] != 0)
                        {
                            firstX = (r0, c0);
                            break;
                        }
                    }
                    if (firstX != null) break;
                }
            }

            // if still no symbol at all → empty game
            if (firstX == null)
            {
                var emptyGame = new Game
                {
                    Board = boardToPass,
                    Result = "Not finished",
                    NextMove = "X"
                };
                return emptyGame;
            }

            (int fx, int fy) = firstX.Value;

            // target center of 15×15 grid
            int center2 = 7;   // boardToPass[7,7] is middle


            int shiftR = center2 - fx;
            int shiftC = center2 - fy;


            for (int r0 = 0; r0 < rows; r0++)
            {
                for (int c0 = 0; c0 < cols; c0++)
                {
                    int newR = r0 + shiftR;
                    int newC = c0 + shiftC;

                    if (newR >= 0 && newR < 15 && newC >= 0 && newC < 15)
                        boardToPass[newR, newC] = finalBoard[r0, c0];
                }
            }


            bool CheckFive(int player)
            {
                for (int r = 0; r < 15; r++)
                {
                    for (int c = 0; c < 15; c++)
                    {
                        if (boardToPass[r, c] != player) continue;

                        // right
                        if (c <= 15 - 5 &&
                            Enumerable.Range(0, 5).All(i => boardToPass[r, c + i] == player))
                            return true;

                        // down
                        if (r <= 15 - 5 &&
                            Enumerable.Range(0, 5).All(i => boardToPass[r + i, c] == player))
                            return true;

                        // diag 
                        if (r <= 15 - 5 && c <= 15 - 5 &&
                            Enumerable.Range(0, 5).All(i => boardToPass[r + i, c + i] == player))
                            return true;

                        // diag 
                        if (r >= 4 && c <= 15 - 5 &&
                            Enumerable.Range(0, 5).All(i => boardToPass[r - i, c + i] == player))
                            return true;
                    }
                }
                return false;
            }

            bool xWin = CheckFive(1);
            bool oWin = CheckFive(2);

            if (xWin && !oWin)
                resultToPass = "X wins";
            else if (oWin && !xWin)
                resultToPass = "O wins";
            else if (xWin && oWin)
                resultToPass = "Invalid board";
            else
                resultToPass = "Not finished";

            //whose turn
            int countX = 0, countO = 0;
            foreach (int v in boardToPass)
            {
                if (v == 1) countX++;
                if (v == 2) countO++;
            }

            if (!xWin && !oWin)
            {
                nextMoveToPass = countX > countO ? "O" : "X";
            }
            else
            {
                nextMoveToPass = "-"; // game over
            }


            var game = new Game
            {
                Board = boardToPass,
                Result = resultToPass,
                NextMove = nextMoveToPass


            };
            
            try
            {
                return game;
            }
            catch (Exception e)
            {
                ;
                throw;
            }
            


        }
        int ClassifySymbol(Mat roi)
        {
            // Resize to 50×50 for stable classification
            Mat resized = new Mat();
            Cv2.Resize(roi, resized, new OpenCvSharp.Size(50, 50));

            int rows = resized.Rows;
            int cols = resized.Cols;

            int marginR = (int)(rows * 0.05); // remove 5% top + 5% bottom
            int marginC = (int)(cols * 0.05); // remove 5% left + 5% right

            int r0 = marginR;
            int r1 = rows - marginR;
            int c0 = marginC;
            int c1 = cols - marginC;

            double[] h = new double[rows];
            double[] v = new double[cols];

            for (int r = r0; r < r1; r++)
            {
                for (int c = c0; c < c1; c++)
                {
                    if (resized.At<byte>(r, c) > 0)
                    {
                        h[r]++;
                        v[c]++;
                    }
                }
            }

            double hMax = h.Max();
            double vMax = v.Max();

            double diag1 = 0;
            double diag2 = 0;

            for (int i = r0; i < r1; i++)
            {
                int c_main = i; // main diagonal
                if (c_main >= c0 && c_main < c1)
                {
                    if (resized.At<byte>(i, c_main) > 0)
                        diag1++;
                }

                int c_anti = (cols - 1 - i);
                if (c_anti >= c0 && c_anti < c1)
                {
                    if (resized.At<byte>(i, c_anti) > 0)
                        diag2++;
                }
            }

            double diagStrength = Math.Max(diag1, diag2);


            double white = Cv2.CountNonZero(roi);

            double hvPeak = (hMax + vMax) / 2.0; // strong for O
            double diag = diagStrength;          // strong for X

            double hvThreshold = 16;  // lower more likely to detect O
            double diagThreshold = 13; // lower more likey to still cassify as O even with diagonals

            if (hvPeak > hvThreshold && diag < diagThreshold && white > 120)
                return 2; // O
            else
                return 1; // X
        }

 

            }

}
