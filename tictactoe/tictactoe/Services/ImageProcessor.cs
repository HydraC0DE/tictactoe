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

            // 1st pass: normal clustering
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

            // 2nd pass: merge clusters that are still very close
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

            // --- 1. Preprocessing: grayscale, bilateral filter, adaptive threshold ---
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

            //rotate here
            // --- Morphological opening to clean up noise ---
            using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            using var opened = new Mat();

            // Try 2–3 iterations
            Cv2.MorphologyEx(morph, opened, MorphTypes.Open, openKernel, iterations: 2);


            // Debug save
            await SaveToGalleryAsync(context, opened, "debug_opening.png");

            // Use 'opened' instead of 'morph' from this point forward
            var maskForRotation = opened.Clone();


            // 1) Hough detect (use the morph mask you already computed)
            LineSegmentPoint[] hough = Cv2.HoughLinesP(
                opened,          // binary mask
                1,              // rho
                Math.PI / 180,  // theta
                80,             // threshold (votes) - tuned to pick strong lines
                Math.Max(30, opened.Width / 10), // minLineLength (tweakable)
                Math.Max(5, opened.Width / 200)   // maxLineGap
            );

            // debug: visualize Hough lines and compute simple stats
            var linesForDebug = hough ?? Array.Empty<LineSegmentPoint>();

            // Create color overlay for visualization
            using var overlay = new Mat();
            Cv2.CvtColor(opened, overlay, ColorConversionCodes.GRAY2BGR);


            // Collect stats
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

            // Compute diagnostics (median, weighted mean by length, longest)
            double medianAngle = 0.0;
            double weightedMean = 0.0;
            double longest = 0.0;
            int count = angles.Count;

            if (count > 0)
            {
                var sortedAngles = angles.OrderBy(a => a).ToList();
                medianAngle = (count % 2 == 1) ? sortedAngles[count / 2] : (sortedAngles[count / 2 - 1] + sortedAngles[count / 2]) / 2.0;

                // weighted mean (weight = line length)
                double sumw = 0, sumwa = 0;
                for (int i = 0; i < count; i++)
                {
                    double dx = linesForDebug[i].P2.X - linesForDebug[i].P1.X;
                    double dy = linesForDebug[i].P2.Y - linesForDebug[i].P1.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    sumw += len;
                    sumwa += (NormalizeAngleForMean(angles[i]) * len); // helper below
                    if (len > longest) longest = len;
                }
                if (sumw > 0) weightedMean = sumwa / sumw;
                // normalize back to -180..180 for display
                weightedMean = NormalizeAngleFromMean(weightedMean);
            }

            // Save debug overlay
            await SaveToGalleryAsync(context, overlay, "debug_hough_overlay.png");

            // DECISION: If ANY good horizontal OR vertical lines exist → SKIP ROTATION
            bool hasStableOrientation = horizontalDetected.Count > 10 && verticalDetected.Count > 10;

            var mask = opened.Clone();
            if (hasStableOrientation)
            {
                int tester = horizontalDetected.Count;
                int tester2 = verticalDetected.Count;
                ;
                // Keep original image — store morph into mask for downstream logic
                mask = opened.Clone();
                await SaveToGalleryAsync(context, mask, "debug_rotation_skipped.png");

                // → Continue pipeline using `mask`, later
            }
            else
            {
                // ------------------- Robust rotation using chosen oblique line -------------------
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

                    // warp into the expanded canvas (important: use newW/newH here)
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

                    // Re-run Hough on rotated mask to confirm alignment and draw overlay
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


            // Helper local functions (put these as private methods in your class if the compiler complains)
            double NormalizeAngleForMean(double ang)
            {
                // Map angle into range [-90,90) then shift so mean works around 0; this helps when angles wrap around ±180
                double a = ang;
                while (a <= -90) a += 180;
                while (a > 90) a -= 180;
                return a;
            }
            double NormalizeAngleFromMean(double ang)
            {
                double a = ang;
                while (a <= -180) a += 360;
                while (a > 180) a -= 360;
                return a;
            }
            //rotate finsihed


            // --- 1. Detect line segments on the binary mask ---
            LineSegmentPoint[] lines = Cv2.HoughLinesP(
                mask,       // binary mask
                1,           // rho resolution
                Math.PI / 180, // theta resolution
                120,         // min votes
                70,          // min line length
                10           // max gap
            );

            // --- 2. Separate vertical and horizontal lines ---
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

            // --- 3. Compute average positions for clustering ---
            List<int> verticalPositions = verticalLines
                .Select(l => (l.P1.X + l.P2.X) / 2)
                .ToList();

            List<int> horizontalPositions = horizontalLines
                .Select(l => (l.P1.Y + l.P2.Y) / 2)
                .ToList();

            // --- 4. Cluster nearby lines ---
            double avgV = ComputeAverageSpacing(verticalPositions);
            double avgH = ComputeAverageSpacing(horizontalPositions);

            // fallback if average is too small (bad detection)
            if (avgV < 5) avgV = 25;
            if (avgH < 5) avgH = 25;

            // dynamic tolerance & mergeTolerance
            int tolV = (int)(avgV * 0.75);         // ~40% of expected tile width
            int mergeV = (int)(avgV * 5);       // tiles closer than ~1.2× avg should merge

            int tolH = (int)(avgH * 0.60);
            int mergeH = (int)(avgH * 5);

            verticalPositions = ClusterLinesAggressive(verticalPositions, tolV, mergeV);
            horizontalPositions = ClusterLinesAggressive(horizontalPositions, tolH, mergeH);


            // --- 5. Optional: filter out lines near board edges to remove extra X/O artifacts ---
            verticalPositions = verticalPositions
                .Where(x => x > 5 && x < mask.Width - 5)
                .ToList();

            horizontalPositions = horizontalPositions
                .Where(y => y > 5 && y < mask.Height - 5)
                .ToList();

            // --- 6. Generate cell ROIs from intersections ---
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

            // --- 7. Optional: draw ROIs for debug ---
            using var debug = new Mat();
            Cv2.CvtColor(mask, debug, ColorConversionCodes.GRAY2BGR);
            foreach (var roi in cellROIs)
            {
                Cv2.Rectangle(debug, roi, Scalar.Red, 2);
            }
            await SaveToGalleryAsync(context, debug, "debug_cells_gomoku.png");

            // --- 8. Fill Game.Board using contour solidity analysis ---



            //// INPUTS:
            //// - mask: thresholded binary image (Mat)
            //// - cellROIs: list of Rect for each grid cell (15×15 total)
            //// - context: Android context for SaveToGalleryAsync

            var roiScores = new List<(OpenCvSharp.Rect roi, double score)>();

            // ---------------------------------------------------------------
            // Step 1 — Compute whiteness score for every ROI
            // ---------------------------------------------------------------
            foreach (var roi in cellROIs)
            {
                int x = Math.Max(0, roi.X);
                int y = Math.Max(0, roi.Y);
                int w = Math.Min(mask.Width - x, roi.Width);
                int h = Math.Min(mask.Height - y, roi.Height);

                if (w <= 0 || h <= 0) continue;

                var roiMat = new Mat(mask, new OpenCvSharp.Rect(x, y, w, h));
                double nonZeroRatio = Cv2.CountNonZero(roiMat) / (double)(w * h);

                roiScores.Add((roi, nonZeroRatio));
            }

            // stop if empty
            if (roiScores.Count == 0)
                return new Game();

            // ---------------------------------------------------------------
            // Step 2 — pick the anchor ROI (highest whiteness)
            // ---------------------------------------------------------------
            var anchor = roiScores.OrderByDescending(s => s.score).First();
            double anchorScore = anchor.score;

            // ---------------------------------------------------------------
            // Step 3 — find adjacent neighbors of the anchor
            // ---------------------------------------------------------------
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

            // ---------------------------------------------------------------
            // Step 4 — compute weighted adaptive threshold
            // ---------------------------------------------------------------
            double adaptiveThreshold =
                (anchorScore * 0.40) +
                (neighborAvg * 0.60);

            // ---------------------------------------------------------------
            // Step 5 — return only ROIs above threshold
            // ---------------------------------------------------------------
            var usefulROIs = new List<Mat>();

            foreach (var (roi, score) in roiScores)
            {
                if (score >= adaptiveThreshold)
                {
                    var roiMat = new Mat(mask, roi);
                    usefulROIs.Add(roiMat);

                    await SaveToGalleryAsync(context, roiMat, $"roi_useful_{roi.X}_{roi.Y}.png");
                }
            }

            var distinctX = usefulROIs.Select(r => r.Cols).Distinct().ToList();
            var distinctY = usefulROIs.Select(r => r.Rows).Distinct().ToList();

            int cols = distinctX.Count;
            int rows = distinctY.Count;

            int gridSize = Math.Max(cols, rows); //so the box is gridSize*gridSize, find one good point then proceed, probaby one of the maximums or minimums in the way that is bigger
            //identify which one is an X or O, could happen before this
            ;


            var game = new Game();
            return game;

        }

    }

}
