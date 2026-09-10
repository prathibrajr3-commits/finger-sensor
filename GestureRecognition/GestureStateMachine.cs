using System;
using AirGestureAI.Configuration;
using AirGestureAI.Models;

namespace AirGestureAI.GestureRecognition
{
    /// <summary>
    /// Implements explicit state transitions for gesture lifecycle management.
    /// States: Idle -> Detecting -> Validating -> Recognized -> Cooldown.
    /// </summary>
    public sealed class GestureStateMachine
    {
        private readonly AppConfig _config;

        private EngineGestureState _state = EngineGestureState.Idle;
        private GestureType _activeGestureType = GestureType.None;
        private DateTime _cooldownStartTime = DateTime.MinValue;

        /// <summary>Gets the current state of the gesture state machine.</summary>
        public EngineGestureState CurrentState => _state;

        /// <summary>Gets the gesture type currently being processed or recognized.</summary>
        public GestureType ActiveGesture => _activeGestureType;

        /// <summary>Occurs when the state machine state changes.</summary>
        public event Action<EngineGestureState, GestureType>? StateChanged;

        /// <summary>
        /// Initializes a new instance of <see cref="GestureStateMachine"/>.
        /// </summary>
        public GestureStateMachine(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Resets the state machine back to Idle.
        /// </summary>
        public void Reset()
        {
            TransitionTo(EngineGestureState.Idle, GestureType.None);
        }

        /// <summary>
        /// Updates the state machine with the candidate classification and validation result for the frame.
        /// Returns true if a gesture was officially recognized on this update tick.
        /// </summary>
        public bool Update(GestureResult candidate, bool isValidated, out GestureType recognizedGesture)
        {
            recognizedGesture = GestureType.None;
            var now = DateTime.UtcNow;

            // Handle Cooldown state
            if (_state == EngineGestureState.Cooldown)
            {
                double cooldownElapsed = (now - _cooldownStartTime).TotalMilliseconds;
                if (cooldownElapsed >= _config.GestureCooldownMs)
                {
                    TransitionTo(EngineGestureState.Idle, GestureType.None);
                }
                else
                {
                    return false; // Still cooling down
                }
            }

            // If candidate is None or invalid, return to Idle
            if (candidate == null || candidate.Gesture == GestureType.None)
            {
                if (_state != EngineGestureState.Idle && _state != EngineGestureState.Cooldown)
                {
                    TransitionTo(EngineGestureState.Idle, GestureType.None);
                }
                return false;
            }

            // State Machine Logic
            switch (_state)
            {
                case EngineGestureState.Idle:
                    TransitionTo(EngineGestureState.Detecting, candidate.Gesture);
                    break;

                case EngineGestureState.Detecting:
                    if (candidate.Gesture == _activeGestureType)
                    {
                        TransitionTo(EngineGestureState.Validating, candidate.Gesture);
                    }
                    else
                    {
                        TransitionTo(EngineGestureState.Detecting, candidate.Gesture);
                    }
                    break;

                case EngineGestureState.Validating:
                    if (candidate.Gesture != _activeGestureType)
                    {
                        TransitionTo(EngineGestureState.Detecting, candidate.Gesture);
                    }
                    else if (isValidated)
                    {
                        TransitionTo(EngineGestureState.Recognized, candidate.Gesture);
                        recognizedGesture = candidate.Gesture;

                        // Enter Cooldown immediately after recognition
                        _cooldownStartTime = now;
                        TransitionTo(EngineGestureState.Cooldown, candidate.Gesture);
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void TransitionTo(EngineGestureState nextState, GestureType gesture)
        {
            if (_state == nextState && _activeGestureType == gesture) return;

            _state = nextState;
            _activeGestureType = gesture;
            StateChanged?.Invoke(_state, _activeGestureType);
        }
    }
}
