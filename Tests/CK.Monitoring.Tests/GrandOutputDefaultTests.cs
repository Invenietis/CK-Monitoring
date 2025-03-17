using Shouldly;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Monitoring.Tests;

[TestFixture]
public class GrandOutputDefaultTests
{
    [Test]
    public async Task applying_empty_configuration_and_disposing_Async()
    {
        TestHelper.InitalizePaths();
        GrandOutput.EnsureActiveDefault( new GrandOutputConfiguration() );
        Debug.Assert( GrandOutput.Default != null );
        await GrandOutput.Default.DisposeAsync();
        GrandOutput.Default.ShouldBeNull();
    }



}
