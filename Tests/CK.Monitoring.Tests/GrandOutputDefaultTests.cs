using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
