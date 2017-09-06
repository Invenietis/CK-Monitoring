using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using CK.Core;
using FluentAssertions;
using System.IO;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class CKMonWriterClientTests
    {
        [Test]
        public void testing_CKMonWriterClient_output()
        {
            TestHelper.InitalizePaths();
            var path = Path.Combine( LogFile.RootLogPath, "CKMonWriterClient" );
            if( Directory.Exists( path ) ) Directory.Delete( path, true );
            var m = new ActivityMonitor( false );
            var client = m.Output.RegisterClient( new CKMonWriterClient( "CKMonWriterClient", 3 ) );
            client.IsOpened.Should().BeTrue();
            m.Info( "Info n°1." );
            m.Info( "Info n°2." );
            m.Info( "Info n°3." );

            Directory.EnumerateFiles( path ).Should().HaveCount( 1 );

        }
    }
}
