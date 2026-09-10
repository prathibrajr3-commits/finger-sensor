using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.GestureAI
{
    /// <summary>
    /// Records a sequence of hand landmark frames for temporal analysis.
    /// </summary>
    public sealed class GestureSequence
    {
        private readonly Queue<HandData> _frames = new();
        private readonly int _windowSize;

        /// <summary>Initializes a new instance of <see cref="GestureSequence"/>.</summary>
        public GestureSequence(int windowSize = 15) => _windowSize = windowSize;

        /// <summary>Gets the number of frames currently buffered.</summary>
        public int FrameCount => _frames.Count;

        /// <summary>Adds a frame and trims old frames outside the window.</summary>
        public void AddFrame(HandData frame)
        {
            if (frame == null) return;
            _frames.Enqueue(frame);
            while (_frames.Count > _windowSize) _frames.Dequeue();
        }

        /// <summary>Gets a snapshot of the current frame buffer.</summary>
        public HandData[] GetFrames() => _frames.ToArray();
    }

    /// <summary>
    /// Logs recognized gesture sequences with timestamps.
    /// </summary>
    public sealed class GestureHistory
    {
        private readonly List<(DateTime Time, string Label, double Confidence)> _history = new();

        /// <summary>Gets all recorded history entries.</summary>
        public IReadOnlyList<(DateTime Time, string Label, double Confidence)> Entries => _history;

        /// <summary>Records a recognized gesture sequence.</summary>
        public void Record(string label, double confidence)
        {
            _history.Add((DateTime.Now, label, confidence));
            if (_history.Count > 500) _history.RemoveAt(0); // Cap history
        }
    }

    /// <summary>
    /// Computes motion dynamics (velocity, acceleration, curvature) from hand coordinate sequences.
    /// </summary>
    public sealed class MotionTrajectory
    {
        /// <summary>
        /// Estimates the dominant direction of movement from a sequence of hand positions.
        /// </summary>
        public string EstimateDirection(HandData[] frames)
        {
            if (frames.Length < 2) return "Stationary";
            
            float dxSum = 0, dySum = 0;
            for (int i = 1; i < frames.Length; i++)
            {
                var curr = GetPosition(frames[i]);
                var prev = GetPosition(frames[i - 1]);
                dxSum += curr.X - prev.X;
                dySum += curr.Y - prev.Y;
            }

            float absX = Math.Abs(dxSum);
            float absY = Math.Abs(dySum);

            if (absX < 0.05 && absY < 0.05) return "Stationary";

            if (absX > absY)
                return dxSum > 0 ? "SwipeRight" : "SwipeLeft";
            else
                return dySum > 0 ? "SwipeDown" : "SwipeUp";
        }

        private static (float X, float Y) GetPosition(HandData frame)
        {
            if (frame.PalmCenter != null) return (frame.PalmCenter.X, frame.PalmCenter.Y);
            if (frame.Wrist != null) return (frame.Wrist.X, frame.Wrist.Y);
            return (0.5f, 0.5f);
        }
    }

    /// <summary>
    /// Classifies gestures based on trajectory dynamics, velocity, acceleration, and curvature.
    /// </summary>
    public sealed class TemporalRecognizer
    {
        private readonly MotionTrajectory _trajectory = new();

        /// <summary>Classifies the gesture from a sequence of frames using physical calculations.</summary>
        public (string Label, double Confidence) Recognize(HandData[] frames)
        {
            if (frames == null || frames.Length < 3) return ("Buffering", 0.0);

            // 1. Calculate physical motion variables
            var (avgVel, maxAcc, avgCurvature) = CalculateDynamics(frames);

            // 2. Check Static Pose Matches (e.g. Pinch)
            var latest = frames[^1];
            if (latest.IsDetected)
            {
                // Pinch: Thumb and Index Tips are very close
                if (latest.ThumbTip != null && latest.IndexTip != null)
                {
                    double pinchDist = CalculateDistance(latest.ThumbTip, latest.IndexTip);
                    if (pinchDist < 0.06)
                    {
                        double confidence = Math.Clamp(1.0 - (pinchDist / 0.06), 0.5, 1.0);
                        return ("Pinch", confidence);
                    }
                }
            }

            // 3. Check Dynamic Motions (Swipes)
            string direction = _trajectory.EstimateDirection(frames);
            if (direction != "Stationary")
            {
                // Calculate confidence based on velocity and curvature (straight swipes should have low curvature)
                double speed = Math.Sqrt(avgVel.X * avgVel.X + avgVel.Y * avgVel.Y);
                if (speed > 0.005)
                {
                    double curvaturePenalty = Math.Max(0.0, 1.0 - avgCurvature * 2.0);
                    double confidence = Math.Clamp(0.5 + curvaturePenalty * 0.4 + speed * 10, 0.5, 0.98);
                    return (direction, confidence);
                }
            }

            return ("Stationary", 0.95);
        }

        private static (System.Numerics.Vector2 AvgVelocity, float MaxAcceleration, float AvgCurvature) CalculateDynamics(HandData[] frames)
        {
            int n = frames.Length;
            var velocities = new System.Numerics.Vector2[n - 1];
            var accelerations = new System.Numerics.Vector2[Math.Max(0, n - 2)];

            // Calculate velocities
            for (int i = 0; i < n - 1; i++)
            {
                var p1 = GetPosVec(frames[i]);
                var p2 = GetPosVec(frames[i + 1]);
                velocities[i] = p2 - p1;
            }

            // Calculate accelerations
            float maxAccMag = 0;
            for (int i = 0; i < n - 2; i++)
            {
                accelerations[i] = velocities[i + 1] - velocities[i];
                float accMag = accelerations[i].Length();
                if (accMag > maxAccMag) maxAccMag = accMag;
            }

            // Calculate Curvature: |v_i x v_{i-1}| / |v_i|^3
            float curvatureSum = 0;
            int curvatureCount = 0;
            for (int i = 1; i < velocities.Length; i++)
            {
                var v1 = velocities[i - 1];
                var v2 = velocities[i];
                float crossProduct = Math.Abs(v1.X * v2.Y - v1.Y * v2.X);
                float lenV1 = v1.Length();
                if (lenV1 > 0.0001f)
                {
                    float curv = crossProduct / (lenV1 * lenV1 * lenV1);
                    curvatureSum += curv;
                    curvatureCount++;
                }
            }

            var avgVel = velocities.Length > 0 
                ? new System.Numerics.Vector2(velocities.Average(v => v.X), velocities.Average(v => v.Y)) 
                : System.Numerics.Vector2.Zero;

            float avgCurv = curvatureCount > 0 ? curvatureSum / curvatureCount : 0f;

            return (avgVel, maxAccMag, avgCurv);
        }

        private static System.Numerics.Vector2 GetPosVec(HandData frame)
        {
            if (frame.PalmCenter != null) return new System.Numerics.Vector2(frame.PalmCenter.X, frame.PalmCenter.Y);
            if (frame.Wrist != null) return new System.Numerics.Vector2(frame.Wrist.X, frame.Wrist.Y);
            return new System.Numerics.Vector2(0.5f, 0.5f);
        }

        private static double CalculateDistance(Landmark l1, Landmark l2)
        {
            double dx = l1.X - l2.X;
            double dy = l1.Y - l2.Y;
            double dz = l1.Z - l2.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    /// <summary>
    /// Predicts the next likely gesture based on recent history.
    /// </summary>
    public sealed class GesturePredictor
    {
        /// <summary>Predicts the next gesture given the recent history list.</summary>
        public string Predict(GestureHistory history)
        {
            if (history.Entries.Count == 0) return "Unknown";
            
            // Return the most frequent dynamic gesture in the last 10 entries as the prediction
            var recent = history.Entries.Skip(Math.Max(0, history.Entries.Count - 10)).ToList();
            var groups = recent
                .Where(e => e.Label != "Stationary" && e.Label != "Buffering")
                .GroupBy(e => e.Label)
                .OrderByDescending(g => g.Count());

            return groups.FirstOrDefault()?.Key ?? "Stationary";
        }
    }

    /// <summary>
    /// Central temporal gesture recognition engine.
    /// Computes velocity, acceleration, curvature, and evaluates coordinates dynamically.
    /// </summary>
    public sealed class GestureSequenceEngine
    {
        private readonly GestureSequence _sequence = new();
        private readonly TemporalRecognizer _recognizer = new();
        private readonly GestureHistory _history = new();
        private readonly GesturePredictor _predictor = new();

        /// <summary>Gets the gesture history log.</summary>
        public GestureHistory History => _history;

        /// <summary>Adds a frame and attempts temporal gesture recognition.</summary>
        public async Task<(string Label, double Confidence)> ProcessFrameAsync(HandData frame, CancellationToken ct = default)
        {
            _sequence.AddFrame(frame);
            await Task.Delay(1, ct);

            if (_sequence.FrameCount < 3) return ("Buffering", 0.0);

            var (label, confidence) = _recognizer.Recognize(_sequence.GetFrames());
            _history.Record(label, confidence);
            
            if (label != "Stationary" && label != "Buffering")
            {
                Logger.Info($"GestureSequenceEngine: Recognized '{label}' ({confidence:P0}) via temporal kinematics");
            }
            return (label, confidence);
        }
    }
}
