using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;

namespace CK.Monitoring.Tests;

[TestFixture]
public class GrandOutputDefaultTests
{
    [Test]
    public void applying_empty_configuration_and_disposing()
    {
        TestHelper.InitalizePaths();
        GrandOutput.EnsureActiveDefault( new GrandOutputConfiguration() );
        Debug.Assert( GrandOutput.Default != null );
        GrandOutput.Default.Dispose();
        GrandOutput.Default.Should().BeNull();
    }
}
