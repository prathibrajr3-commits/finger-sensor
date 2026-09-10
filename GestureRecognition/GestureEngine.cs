using System;
using AirGestureAI.Configuration;
using AirGestureAI.Models;
using AirGestureAI.Utilities;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Production Gesture Recognition Engine for AirGesture AI — Phase 6.
    /// Evaluates hand landmark feature vectors, validates stability, manages the gesture state machine,
    /// and fires lifecycle events for ScrollUp, ScrollDown, and OpenPalm gestures.
    /// </summary>
    public class GestureEngine : IGestureDetector
    {
        private readonly AppConfig _config;
        private readonly GestureFeatureExtractor _featureExtractor;
        private readonly GestureValidator _validator;
        private readonly GestureStateMachine _stateMachine;

        private EngineGestureState _lastState = EngineGestureState.Idle;
        private DateTime _lastLogTime = DateTime.MinValue;

        // ── IGestureDetector Events ───────────────────────────────────────────

        /// <inheritdoc/>
        public event EventHandler<GestureEventArgs>? GestureDetected;

        /// <inheritdoc/>
        public event EventHandler<GestureEventArgs>? GestureValidated;

        /// <inheritdoc/>
        public event EventHandler<GestureEventArgs>? GestureRecognized;

        /// <inheritdoc/>
        public event EventHandler<GestureEventArgs>? GestureCancelled;

        // ── IGestureDetector Properties ───────────────────────────────────────

        /// <inheritdoc/>
        public GestureType ActiveGesture => _stateMachine.ActiveGesture;

        /// <inheritdoc/>
        public EngineGestureState CurrentState => _stateMachine.CurrentState;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Initializes a new instance of <see cref="GestureEngine"/>.
        /// </summary>
        public GestureEngine(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _featureExtractor = new GestureFeatureExtractor(_config);
            _validator = new GestureValidator(_config);
            _stateMachine = new GestureStateMachine(_config);

            _stateMachine.StateChanged += OnStateMachineStateChanged;

            Logger.Info("GestureEngine created.");
        }

        // ── IGestureDetector Methods ──────────────────────────────────────────

        /// <inheritdoc/>
        public void Process(HandData handData)
        {
            if (handData == null || !handData.IsDetected)
            {
                if (_stateMachine.CurrentState != EngineGestureState.Idle && _stateMachine.CurrentState != EngineGestureState.Cooldown)
                {
                    Logger.Info("Hand lost — resetting gesture recognition engine.");
                    Reset();
                }
                return;
            }

            // 1. Feature Extraction
            var features = _featureExtractor.ExtractFeatures(handData);

            // 2. Gesture Classification
            var candidate = _featureExtractor.ClassifyGesture(features);

            // 3. Gesture Validation
            bool isValidated = _validator.Validate(candidate, features, out double elapsedMs);

            // 4. State Machine Update
            bool isRecognized = _stateMachine.Update(candidate, isValidated, out GestureType recognizedGesture);

            if (isRecognized && recognizedGesture != GestureType.None)
            {
                Logger.Info($"Gesture RECOGNIZED: {recognizedGesture} (confidence: {candidate.Confidence:P0})");
                var args = new GestureEventArgs(recognizedGesture, candidate.Confidence, EngineGestureState.Recognized);
                GestureRecognized?.Invoke(this, args);
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _featureExtractor.Reset();
            _validator.Reset();

            if (_stateMachine.CurrentState != EngineGestureState.Idle)
            {
                var prevGesture = _stateMachine.ActiveGesture;
                _stateMachine.Reset();

                if (prevGesture != GestureType.None)
                {
                    GestureCancelled?.Invoke(this, new GestureEventArgs(prevGesture, 0.0, EngineGestureState.Idle));
                }
            }

            Logger.Info("GestureEngine reset.");
        }

        private void OnStateMachineStateChanged(EngineGestureState state, GestureType gesture)
        {
            var now = DateTime.UtcNow;
            if (now - _lastLogTime >= TimeSpan.FromSeconds(0.5) || state == EngineGestureState.Recognized)
            {
                Logger.Info($"Gesture Engine State: {state} (active gesture: {gesture})");
                _lastLogTime = now;
            }

            var args = new GestureEventArgs(gesture, 1.0, state);

            switch (state)
            {
                case EngineGestureState.Detecting:
                    GestureDetected?.Invoke(this, args);
                    break;

                case EngineGestureState.Validating:
                    GestureValidated?.Invoke(this, args);
                    break;
            }

            _lastState = state;
        }
    }
}
