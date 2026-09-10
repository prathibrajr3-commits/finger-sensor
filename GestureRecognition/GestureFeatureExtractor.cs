using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AirGestureAI.Configuration;
using AirGestureAI.Models;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Extracts hand features and evaluates initial candidate gesture classifications.
    /// </summary>
    public sealed class GestureFeatureExtractor
    {
        private readonly AppConfig _config;
        private readonly Queue<Tuple<DateTime, Point>> _positionHistory = new Queue<Tuple<DateTime, Point>>();
        private const int MaxHistorySize = 10;

        /// <summary>
        /// Initializes a new instance of <see cref="GestureFeatureExtractor"/>.
        /// </summary>
        public GestureFeatureExtractor(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Resets position history.
        /// </summary>
        public void Reset()
        {
            _positionHistory.Clear();
        }

        /// <summary>
        /// Extracts numerical features from hand tracking data.
        /// </summary>
        public HandFeatures ExtractFeatures(HandData handData)
        {
            var features = new HandFeatures
            {
                IsHandDetected = handData != null && handData.IsDetected
            };

            if (!features.IsHandDetected || handData == null)
            {
                features.Confidence = 0.0;
                return features;
            }

            // Determine palm center & wrist
            var wristPt = handData.Wrist != null ? new Point(handData.Wrist.X, handData.Wrist.Y) : new Point(0.5, 0.8);
            var palmPt  = handData.PalmCenter != null ? new Point(handData.PalmCenter.X, handData.PalmCenter.Y) : wristPt;
            
            features.Wrist = wristPt;
            features.PalmCenter = palmPt;
            features.Confidence = 1.0;

            // Track position over time to compute velocity
            var now = DateTime.UtcNow;
            _positionHistory.Enqueue(new Tuple<DateTime, Point>(now, palmPt));
            while (_positionHistory.Count > MaxHistorySize)
            {
                _positionHistory.Dequeue();
            }

            // Calculate velocity over history window
            if (_positionHistory.Count >= 2)
            {
                var oldest = _positionHistory.Peek();
                var newest = _positionHistory.Last();
                double dt = (newest.Item1 - oldest.Item1).TotalSeconds;

                if (dt > 0.01)
                {
                    features.VelocityX = (newest.Item2.X - oldest.Item2.X) / dt;
                    features.VelocityY = (newest.Item2.Y - oldest.Item2.Y) / dt;
                    features.MotionMagnitude = Math.Sqrt(features.VelocityX * features.VelocityX + features.VelocityY * features.VelocityY);
                }
            }

            // Evaluate finger extension
            EvaluateFingerExtensions(handData, features);

            // Compute stability score (inverse of speed jitter)
            features.StabilityScore = Math.Max(0.0, 1.0 - Math.Min(1.0, features.MotionMagnitude * 0.5));

            return features;
        }

        /// <summary>
        /// Classifies candidate gesture from extracted features.
        /// </summary>
        public GestureResult ClassifyGesture(HandFeatures features)
        {
            if (!features.IsHandDetected || features.Confidence < _config.GestureMinConfidence)
            {
                return GestureResult.None;
            }

            // 1. Open Palm Gesture — evaluated first (posture check, not motion-based)
            if (features.OpenPalmScore >= _config.OpenPalmScoreThreshold)
            {
                return new GestureResult(
                    GestureType.OpenPalm,
                    features.OpenPalmScore,
                    $"Open palm score {features.OpenPalmScore:P0} ≥ threshold {_config.OpenPalmScoreThreshold:P0}");
            }

            // 2 & 3. Scroll gestures — motion-based, require minimum movement
            double scrollVelThreshold = _config.ScrollThresholdY * 10.0; // normalised velocity / sec
            double minMag             = _config.GestureMinMotionMagnitude;

            // Scroll Up: palm moves toward top of screen (VelocityY < 0)
            if (features.VelocityY < -scrollVelThreshold && features.MotionMagnitude > minMag)
            {
                double conf = Math.Min(1.0, Math.Abs(features.VelocityY) / (scrollVelThreshold * 2.0));
                return new GestureResult(
                    GestureType.ScrollUp,
                    conf,
                    $"Upward motion vy={features.VelocityY:F2} mag={features.MotionMagnitude:F2}");
            }

            // Scroll Down: palm moves toward bottom of screen (VelocityY > 0)
            if (features.VelocityY > scrollVelThreshold && features.MotionMagnitude > minMag)
            {
                double conf = Math.Min(1.0, Math.Abs(features.VelocityY) / (scrollVelThreshold * 2.0));
                return new GestureResult(
                    GestureType.ScrollDown,
                    conf,
                    $"Downward motion vy={features.VelocityY:F2} mag={features.MotionMagnitude:F2}");
            }

            return GestureResult.None;
        }

        private static void EvaluateFingerExtensions(HandData handData, HandFeatures features)
        {
            if (handData.Landmarks != null && handData.Landmarks.Count >= 21)
            {
                var lm = handData.Landmarks;
                var wrist = new Point(lm[0].X, lm[0].Y);

                features.IsThumbExtended  = IsFingerExtended(lm[4],  lm[2],  wrist); // Thumb: Tip=4, MCP=2
                features.IsIndexExtended  = IsFingerExtended(lm[8],  lm[6],  wrist); // Index: Tip=8, PIP=6
                features.IsMiddleExtended = IsFingerExtended(lm[12], lm[10], wrist); // Middle: Tip=12, PIP=10
                features.IsRingExtended   = IsFingerExtended(lm[16], lm[14], wrist); // Ring: Tip=16, PIP=14
                features.IsPinkyExtended  = IsFingerExtended(lm[20], lm[18], wrist); // Pinky: Tip=20, PIP=18
            }
            else
            {
                // Fallback heuristic if full 21 landmarks not present
                bool defaultExt = handData.IndexTip != null;
                features.IsIndexExtended  = defaultExt;
                features.IsMiddleExtended = defaultExt;
                features.IsRingExtended   = defaultExt;
                features.IsPinkyExtended  = defaultExt;
                features.IsThumbExtended  = handData.ThumbTip != null;
            }

            // Compute open palm score (fraction of extended fingers)
            int extendedCount = 0;
            if (features.IsThumbExtended)  extendedCount++;
            if (features.IsIndexExtended)  extendedCount++;
            if (features.IsMiddleExtended) extendedCount++;
            if (features.IsRingExtended)   extendedCount++;
            if (features.IsPinkyExtended)  extendedCount++;

            features.OpenPalmScore = extendedCount / 5.0;
        }

        private static bool IsFingerExtended(Landmark tip, Landmark baseJoint, Point wrist)
        {
            double tipDist  = Distance(new Point(tip.X, tip.Y), wrist);
            double baseDist = Distance(new Point(baseJoint.X, baseJoint.Y), wrist);
            return tipDist > baseDist * 1.15;
        }

        private static double Distance(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
