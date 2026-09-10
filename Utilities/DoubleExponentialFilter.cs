using System.Windows;

namespace AirGestureAI.Utilities
{
    /// <summary>
    /// Implements Double Exponential Smoothing (Holt's Linear Trend) to filter noise and jitter from coordinates.
    /// </summary>
    public class DoubleExponentialFilter
    {
        private readonly double _alpha; // Smoothing factor for data
        private readonly double _beta;  // Smoothing factor for trend (velocity)
        private bool _isInitialized;

        private double _smoothedX;
        private double _smoothedY;
        private double _trendX;
        private double _trendY;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleExponentialFilter"/> class.
        /// </summary>
        /// <param name="alpha">Data smoothing factor (0.0 to 1.0]. Lower is smoother but slower.</param>
        /// <param name="beta">Trend smoothing factor (0.0 to 1.0]. Lower is smoother but slower.</param>
        public DoubleExponentialFilter(double alpha = 0.20, double beta = 0.15)
        {
            _alpha = alpha;
            _beta = beta;
        }

        /// <summary>
        /// Resets the filter to uninitialized state.
        /// </summary>
        public void Reset()
        {
            _isInitialized = false;
        }

        /// <summary>
        /// Inputs a raw coordinates point and returns the smoothed coordinates point.
        /// </summary>
        /// <param name="rawPoint">The raw index finger coordinates on screen.</param>
        /// <returns>The smoothed coordinates.</returns>
        public Point Filter(Point rawPoint)
        {
            if (!_isInitialized)
            {
                _smoothedX = rawPoint.X;
                _smoothedY = rawPoint.Y;
                _trendX = 0;
                _trendY = 0;
                _isInitialized = true;
                return rawPoint;
            }

            double prevSmoothedX = _smoothedX;
            double prevSmoothedY = _smoothedY;

            // Holt's linear trend equations
            _smoothedX = (_alpha * rawPoint.X) + ((1 - _alpha) * (prevSmoothedX + _trendX));
            _smoothedY = (_alpha * rawPoint.Y) + ((1 - _alpha) * (prevSmoothedY + _trendY));

            _trendX = (_beta * (_smoothedX - prevSmoothedX)) + ((1 - _beta) * _trendX);
            _trendY = (_beta * (_smoothedY - prevSmoothedY)) + ((1 - _beta) * _trendY);

            return new Point(_smoothedX, _smoothedY);
        }
    }
}
