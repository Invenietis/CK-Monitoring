using FluentAssertions;
using NUnit.Framework;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class GrandOutputDefaultTests
    {
        [Test]
        public void applying_empty_configuration_and_disposing()
        {
            TestHelper.InitalizePaths();
            GrandOutput.EnsureActiveDefault( new GrandOutputConfiguration() );
            GrandOutput.Default.Should().NotBeNull();
            GrandOutput.Default.Dispose();
            GrandOutput.Default.Should().BeNull();
        }
    }
}
