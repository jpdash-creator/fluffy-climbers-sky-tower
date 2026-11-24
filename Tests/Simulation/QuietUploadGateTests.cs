using FluffyClimbers.Economy;
using Xunit;

namespace FluffyClimbers.Simulation.Tests
{
    /// <summary>Final Change 3: quiet saves (play time, bundle timer, settings) ask for a cloud upload at most once per 60 seconds.</summary>
    public class QuietUploadGateTests
    {
        [Fact]
        public void NothingIsDueWithoutARequest()
        {
            var g = new QuietUploadGate();
            Assert.False(g.Due(0.0));
            Assert.False(g.Due(1e9));
        }

        [Fact]
        public void TheFirstRequestIsDueAtOnceThenOneUploadPerMinute()
        {
            var g = new QuietUploadGate(60.0);
            g.Request();
            Assert.True(g.Due(5.0));
            g.Uploaded(5.0);
            Assert.False(g.Pending);
            for (int t = 6; t < 65; t++) { g.Request(); Assert.False(g.Due(t)); }   // a timer that saves every second
            Assert.True(g.Due(65.0));
            g.Uploaded(65.0);
            g.Request();
            Assert.False(g.Due(124.9));
            Assert.True(g.Due(125.0));
        }

        [Fact]
        public void AnyUploadCoversWaitingRequestsAndClearResets()
        {
            var g = new QuietUploadGate(60.0);
            g.Request();
            g.Uploaded(10.0);   // a normal change upload went out
            Assert.False(g.Pending);
            g.Request();
            g.Clear();
            Assert.False(g.Pending);
            g.Request();
            Assert.True(g.Due(0.0));
        }
    }
}
