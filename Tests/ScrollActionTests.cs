using System;
using System.Threading;
using AirGestureAI.Input;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class ScrollActionTests
    {
        [Fact]
        public void ScrollUp_DispatchesPositiveDelta_AndInvokesEvent()
        {
            var simulator = new WindowsInputSimulator();
            int dispatchedDelta = 0;
            simulator.ScrollDispatched += delta => dispatchedDelta = delta;

            simulator.ScrollUp();

            Assert.Equal(120, dispatchedDelta);
        }

        [Fact]
        public void ScrollDown_DispatchesNegativeDelta_AndInvokesEvent()
        {
            var simulator = new WindowsInputSimulator();
            int dispatchedDelta = 0;
            simulator.ScrollDispatched += delta => dispatchedDelta = delta;

            simulator.ScrollDown();

            Assert.Equal(-120, dispatchedDelta);
        }

        [Fact]
        public void ScrollUp_Debounce_PreventsRapidDuplicateCalls()
        {
            var simulator = new WindowsInputSimulator();
            int dispatchCount = 0;
            simulator.ScrollDispatched += _ => dispatchCount++;

            simulator.ScrollUp();
            // Immediate second call should be throttled by the 50ms debounce guard
            simulator.ScrollUp();

            Assert.Equal(1, dispatchCount);
        }

        [Fact]
        public void ScrollDown_Debounce_PreventsRapidDuplicateCalls()
        {
            var simulator = new WindowsInputSimulator();
            int dispatchCount = 0;
            simulator.ScrollDispatched += _ => dispatchCount++;

            simulator.ScrollDown();
            // Immediate second call should be throttled by the 50ms debounce guard
            simulator.ScrollDown();

            Assert.Equal(1, dispatchCount);
        }

        [Fact]
        public void ScrollUp_ExecutesSafelyWithoutException()
        {
            var simulator = new WindowsInputSimulator();
            var exception = Record.Exception(() => simulator.ScrollUp());
            Assert.Null(exception);
        }

        [Fact]
        public void ScrollDown_ExecutesSafelyWithoutException()
        {
            var simulator = new WindowsInputSimulator();
            var exception = Record.Exception(() => simulator.ScrollDown());
            Assert.Null(exception);
        }
    }
}
