using Android.Content;
using Android.Provider;
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



        public async Task<Game> ProcessImageAsync2(string boardImagePath)
        {
            var mat = Cv2.ImRead(boardImagePath, ImreadModes.Color);

            // 1. Convert to grayscale for single-channel processing
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

            // Compute simple brightness metric
            Scalar meanVal = Cv2.Mean(gray);
            double brightness = meanVal.Val0; // 0=dark, 255=bright


            // 2. Bilateral filter
            // Reduces noise while preserving grid edges
            using var denoise = new Mat();
            Cv2.BilateralFilter(gray, denoise, 9, 75, 75);

            // 3. Adaptive threshold
            // Converts grayscale to strong black and white
            // BLOCK SIZE (51):
            //   Increase = thicker grid, helps when flash is ON
            //   Decrease = less aggressive, helps in low-light or noisy images
            //
            // CONSTANT C (7):
            //   Increase = more white turns to black (keeps grid but noise too)
            //   Decrease = more black turns to white (thickens bright grid lines)
            // Dynamic tuning
            int blockSize;
            int constantC;
            int kernelSize;

            // Bright image (flash) = grid already white → lower threshold aggression
            if (brightness > 180)
            {
                blockSize = 31;
                constantC = 2;
                kernelSize = 7; //5
                ;
            }
            // Medium brightness = default, best resuts so far
            else if (brightness > 90)
            {
                blockSize = 51;
                constantC = 3;
                kernelSize = 7;
                ;
            }
            // Dark image (no flash) = aggressive to keep faint grid
            else
            {
                blockSize = 71;
                constantC = 8;
                kernelSize = 7; //9
                ;
            }


            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(
                denoise,
                thresh,
                255,
                AdaptiveThresholdTypes.MeanC,
                ThresholdTypes.BinaryInv,
                blockSize,   // Odd number. Try 31→71 depending on lighting.
                constantC     // Try 3→15 depending on visibility of grid.
            );

            // 4. Morphological closing
            // Connects broken grid segments
            // KERNEL SIZE (7x7):
            //   Increase = thicker lines and better continuity (good for faint grid)
            //   Decrease = thinner lines and less noise (good for dark/no-flash)
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect,
                new OpenCvSharp.Size(7, 7)
            );
            using var morph = new Mat();
            Cv2.MorphologyEx(thresh, morph, MorphTypes.Close, kernel);

            // Debug output of grid mask
            var context = Android.App.Application.Context;
            await SaveToGalleryAsync(context, morph, "debug_grid_mask.png");
            //ROTATE CORRECTLY!!!!!!!!!!


            // 1. Detect line segments on the binary mask
            LineSegmentPoint[] lines = Cv2.HoughLinesP(
                morph,        // binary mask
                1,
                Math.PI / 180,
                120,          // votes threshold, increase to ignore noise
                70,           // min line length, ignore short lines from X/O
                10            // max gap
            );

            // 2. Separate vertical and horizontal lines with narrow angle tolerance
            var verticalLines = new List<LineSegmentPoint>();
            var horizontalLines = new List<LineSegmentPoint>();

            foreach (var line in lines)
            {
                double dx = line.P2.X - line.P1.X;
                double dy = line.P2.Y - line.P1.Y;
                double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                if (Math.Abs(angle) > 80) verticalLines.Add(line);   // strict vertical
                else if (Math.Abs(angle) < 10) horizontalLines.Add(line); // strict horizontal
            }

            // 3. Compute line positions (average of endpoints) for clustering
            List<int> verticalPositions = verticalLines
                .Select(l => (l.P1.X + l.P2.X) / 2)
                .ToList();

            List<int> horizontalPositions = horizontalLines
                .Select(l => (l.P1.Y + l.P2.Y) / 2)
                .ToList();

            // 4. Cluster nearby lines and take the average per cluster
            verticalPositions = ClusterLines(verticalPositions, 7);   // tolerance ~7px
            horizontalPositions = ClusterLines(horizontalPositions, 7);

            // 5. Generate cell ROIs from intersections
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

            // 6. Optional: draw ROIs on the mask for debugging
            using var debug = new Mat();
            Cv2.CvtColor(morph, debug, ColorConversionCodes.GRAY2BGR);
            foreach (var roi in cellROIs)
            {
                Cv2.Rectangle(debug, roi, Scalar.Red, 2);
            }
            await SaveToGalleryAsync(context, debug, "debug_cells_clean.png");




            return new Game();

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
        List<int> ClusterLines(List<int> positions, int tolerance)
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

            return clusters.Select(c => (int)c.Average()).OrderBy(x => x).ToList();
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

            //// --- 2. Detect long lines for rotation ---
            //LineSegmentPoint[] lines = Cv2.HoughLinesP(morph, 1, Math.PI / 180, 120, 70, 10);

            //// Select the longest vertical-ish line
            //LineSegmentPoint bestLine = new LineSegmentPoint();
            //double bestLength = 0;
            //foreach (var line in lines)
            //{
            //    double dx = line.P2.X - line.P1.X;
            //    double dy = line.P2.Y - line.P1.Y;
            //    double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            //    if (Math.Abs(angle) > 75) // near vertical
            //    {
            //        double len = Math.Sqrt(dx * dx + dy * dy);
            //        if (len > bestLength) { bestLength = len; bestLine = line; }
            //    }
            //}

            //// Fallback: longest line if no vertical
            //if (bestLine == null && lines.Length > 0)
            //    bestLine = lines.OrderByDescending(l => Math.Sqrt(Math.Pow(l.P2.X - l.P1.X, 2) + Math.Pow(l.P2.Y - l.P1.Y, 2))).First();

            //// --- 3. Rotate board based on dominant line ---
            //double dxBest = bestLine.P2.X - bestLine.P1.X;
            //double dyBest = bestLine.P2.Y - bestLine.P1.Y;
            //double rotationDeg = Math.Atan2(dyBest, dxBest) * 180.0 / Math.PI - 90.0;

            //var center = new Point2f(mat.Width * 0.5f, mat.Height * 0.5f);
            //var rotMat = Cv2.GetRotationMatrix2D(center, -rotationDeg, 1.0);
            //double cos = Math.Abs(rotMat.At<double>(0, 0));
            //double sin = Math.Abs(rotMat.At<double>(0, 1));
            //int newWidth = (int)(mat.Height * sin + mat.Width * cos);
            //int newHeight = (int)(mat.Height * cos + mat.Width * sin);
            //rotMat.Set(0, 2, rotMat.At<double>(0, 2) + (newWidth / 2.0 - center.X));
            //rotMat.Set(1, 2, rotMat.At<double>(1, 2) + (newHeight / 2.0 - center.Y));

            //using var rotatedMat = new Mat();
            //Cv2.WarpAffine(mat, rotatedMat, rotMat, new OpenCvSharp.Size(newWidth, newHeight),
            //               InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(255));

            //// Rotate the mask too for consistent line detection
            //using var rotatedMask = new Mat();
            //Cv2.WarpAffine(morph, rotatedMask, rotMat, new OpenCvSharp.Size(newWidth, newHeight),
            //               InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));

            //// --- 4. Detect lines again on rotated mask ---
            //LineSegmentPoint[] rotatedLines = Cv2.HoughLinesP(rotatedMask, 1, Math.PI / 180, 120, 70, 10);
            //var verticalLines = new List<LineSegmentPoint>();
            //var horizontalLines = new List<LineSegmentPoint>();

            //foreach (var line in rotatedLines)
            //{
            //    double dx = line.P2.X - line.P1.X;
            //    double dy = line.P2.Y - line.P1.Y;
            //    double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            //    if (Math.Abs(angle) > 80) verticalLines.Add(line);
            //    else if (Math.Abs(angle) < 10) horizontalLines.Add(line);
            //}

            //// --- 5. Cluster positions ---
            //List<int> verticalPositions = verticalLines.Select(l => (l.P1.X + l.P2.X) / 2).ToList();
            //List<int> horizontalPositions = horizontalLines.Select(l => (l.P1.Y + l.P2.Y) / 2).ToList();

            //verticalPositions = ClusterLines(verticalPositions, 7);
            //horizontalPositions = ClusterLines(horizontalPositions, 7);

            //// --- 6. Generate cell ROIs ---
            //var cellROIs = new List<OpenCvSharp.Rect>();
            //for (int i = 0; i < verticalPositions.Count - 1; i++)
            //{
            //    for (int j = 0; j < horizontalPositions.Count - 1; j++)
            //    {
            //        int x = verticalPositions[i];
            //        int y = horizontalPositions[j];
            //        int w = verticalPositions[i + 1] - x;
            //        int h = horizontalPositions[j + 1] - y;
            //        cellROIs.Add(new OpenCvSharp.Rect(x, y, w, h));
            //    }
            //}

            //// --- 7. Draw debug ROIs ---
            //using var debug = new Mat();
            //Cv2.CvtColor(rotatedMask, debug, ColorConversionCodes.GRAY2BGR);
            //foreach (var roi in cellROIs)
            //    Cv2.Rectangle(debug, roi, Scalar.Red, 2);

            //await SaveToGalleryAsync(context, debug, "debug_cells_rotated.png");

            // --- 1. Detect line segments on the binary mask ---
            LineSegmentPoint[] lines = Cv2.HoughLinesP(
                morph,       // binary mask
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
            verticalPositions = ClusterLinesAggressive(verticalPositions, 7, 80);   // tweak tolerance as needed
            horizontalPositions = ClusterLinesAggressive(horizontalPositions, 7,80);

            // --- 5. Optional: filter out lines near board edges to remove extra X/O artifacts ---
            verticalPositions = verticalPositions
                .Where(x => x > 5 && x < morph.Width - 5)
                .ToList();

            horizontalPositions = horizontalPositions
                .Where(y => y > 5 && y < morph.Height - 5)
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
            Cv2.CvtColor(morph, debug, ColorConversionCodes.GRAY2BGR);
            foreach (var roi in cellROIs)
            {
                Cv2.Rectangle(debug, roi, Scalar.Red, 2);
            }
            await SaveToGalleryAsync(context, debug, "debug_cells_gomoku.png");

            //// --- 8. Return or process the cell ROIs ---
            //return new Game
            //{
            //    Cells = cellROIs.Select(r => new GameCell { Rect = r }).ToList()
            //};


            return new Game();
        }

    }

}
