using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring.Tests;

[TestFixture]
public class IdentityCardTests
{
    [Test]
    public void OnChange_event_is_triggered()
    {
        var id = new IdentityCard();
        int callCount = 0;
        id.OnChanged += ev =>
        {
            ++callCount;
            ev.IdentityCard.Should().BeSameAs( id );
            ev.AddedInfo.Should().BeEquivalentTo( new[] { ("a", "b") } );
        };
        id.OnChanged += ev => ++callCount;

        var e = id.Add( "a", "b" );
        Debug.Assert( e != null );
        e.AddedInfo.Count.Should().Be( 1 );
        e.Identities["a"].Should().BeEquivalentTo( new[] { "b" } );

        callCount.Should().Be( 2 );
    }

    [Test]
    public void duplicated_added_infos_are_ignored()
    {
        var id = new IdentityCard();
        int callCount = 0;
        id.OnChanged += ev => ++callCount;

        id.Add( "a", "b" ).Should().NotBeNull();
        id.Add( "a", "b" ).Should().BeNull();
        id.Add( ("a", "b") ).Should().BeNull();
        id.Add( ("a", "b"), ("a", "b"), ("a", "b"), ("a", "b") ).Should().BeNull();
        callCount.Should().Be( 1 );

        id.Add( "a", "c" ).Should().NotBeNull();
        var c = id.Add( ("a", "d"), ("A", "b"), ("A", "c") );
        Debug.Assert( c != null );
        c.AddedInfo.Should().BeEquivalentTo( new[] { ("a", "d"), ("A", "b"), ("A", "c") } );

        callCount.Should().Be( 3 );

        c = id.Add( ("a", "d"), ("B", "1"), ("B", "2"), ("A", "c"), ("B", "3") );
        Debug.Assert( c != null );
        c.AddedInfo.Should().BeEquivalentTo( new[] { ("B", "1"), ("B", "2"), ("B", "3") } );
        callCount.Should().Be( 4 );

        c = id.Add( ("C", "0"), ("B", "1"), ("B", "2"), ("A", "c"), ("B", "3") );
        Debug.Assert( c != null );
        c.AddedInfo.Should().BeEquivalentTo( new[] { ("C", "0") } );
        callCount.Should().Be( 5 );

        id.Identities.Should().HaveCount( 4 );
        id.Identities.Keys.Should().BeEquivalentTo( new[] { "a", "A", "B", "C" } );
    }

    [Test]
    public void key_and_value_cannot_contain_the_9_first_chracters_and_keys_cannot_contain_newlines()
    {
        var id = new IdentityCard();
        id.Add( null!, "valid" ).Should().BeNull();
        id.Add( "", "valid" ).Should().BeNull();
        id.Add( "A\u0000A", "valid" ).Should().BeNull();
        id.Add( "A\u0001A", "valid" ).Should().BeNull();
        id.Add( "A\u0002A", "valid" ).Should().BeNull();
        id.Add( "A\u0003A", "valid" ).Should().BeNull();
        id.Add( "A\u0004A", "valid" ).Should().BeNull();
        id.Add( "A\u0005A", "valid" ).Should().BeNull();
        id.Add( "A\u0006A", "valid" ).Should().BeNull();
        id.Add( "A\u0007A", "valid" ).Should().BeNull();
        id.Add( "A\u0008A", "valid" ).Should().BeNull();

        id.Add( "A\u0009A", "valid" ).Should().NotBeNull();

        id.Add( "valid", null! ).Should().BeNull();
        id.Add( "valid", "" ).Should().BeNull();
        id.Add( "valid", "A\u0000A" ).Should().BeNull();
        id.Add( "valid", "A\u0001A" ).Should().BeNull();
        id.Add( "valid", "A\u0002A" ).Should().BeNull();
        id.Add( "valid", "A\u0003A" ).Should().BeNull();
        id.Add( "valid", "A\u0004A" ).Should().BeNull();
        id.Add( "valid", "A\u0005A" ).Should().BeNull();
        id.Add( "valid", "A\u0006A" ).Should().BeNull();
        id.Add( "valid", "A\u0007A" ).Should().BeNull();
        id.Add( "valid", "A\u0008A" ).Should().BeNull();

        id.Add( "valid", "A\u0009A" ).Should().NotBeNull();

        id.Add( "A\rA", "valid" ).Should().BeNull();
        id.Add( "A\nA", "valid" ).Should().BeNull();
    }

    [Test]
    public void packing_and_unpacking_added_infos()
    {
        Check( new[] { ("A", "B"), ("C", "D") } );
        Check( new[] { ("A", "B"), ("C", "D"), ("E", "F") } );

        static void Check( (string, string)[] a )
        {
            var s = IdentityCard.Pack( a );
            var r = (IReadOnlyList<(string, string)>?)IdentityCard.TryUnpack( s );
            r.Should().BeEquivalentTo( a );
        }
    }

    [Test]
    public void packing_and_unpacking_identities()
    {
        var id = new IdentityCard();
        Check( id );
        id.Add( "A", "B" );
        Check( id );
        id.Add( "A", "C" );
        Check( id );
        id.Add( "A", "D" );
        Check( id );
        id.Add( "A", "E" );
        Check( id );
        id.Add( "X", "Y" );
        Check( id );
        id.Add( "U", "T" );
        Check( id );
        id.Add( "M", "N" );
        Check( id );
        id.Add( "M", "O" );
        Check( id );
        id.Add( "M", "P" );
        Check( id );
        id.Add( "ANOTHER", "Piece" );
        Check( id );
        id.Add( "OF", "DATA" );
        Check( id );
        id.Add( "ANOTHER", "another piece" );
        Check( id );
        id.Add( "OF", "another DATA" );
        Check( id );

        static void Check( IdentityCard c )
        {
            var s = c.ToString();
            var r = IdentityCard.TryCreate( s );
            Debug.Assert( r != null );
            r.Identities.Should().BeEquivalentTo( c.Identities );

            if( s.Length > 0 )
            {
                var i = (IReadOnlyDictionary<string, IReadOnlyCollection<string>>?)IdentityCard.TryUnpack( s );
                i.Should().BeEquivalentTo( c.Identities );
            }
        }
    }

    [Test]
    public async Task CoreApplicationIdentity_initialize_injects_CoreApplicationIdentity_values_Async()
    {
        await using var g = new GrandOutput( new GrandOutputConfiguration() );
        CoreApplicationIdentity.Initialize();
        g.IdentityCard.Identities["AppIdentity"].Should().NotBeEmpty();
        g.IdentityCard.Identities["AppIdentity/InstanceId"].Single().Should().Be( CoreApplicationIdentity.InstanceId );
        g.IdentityCard.Identities["AppIdentity/ContextualId"].Single().Should().Be( CoreApplicationIdentity.Instance.ContextualId );
        // The empty ContextDescriptor cannot be a valid value.
        if( CoreApplicationIdentity.Instance.ContextDescriptor.Length > 0 )
        {
            g.IdentityCard.Identities["AppIdentity/ContextDescriptor"].Single().Should().Be( CoreApplicationIdentity.Instance.ContextDescriptor );
        }
    }

    [Test]
    public async Task ActivityMonitorSimpleSenderExtension_AddIdentityInformation_updates_the_identity_card_Async()
    {
        string folder = TestHelper.PrepareLogFolder( "AddIdentityInformation" );
        var textConf = new Handlers.TextFileConfiguration() { Path = "AddIdentityInformation", AutoFlushRate = 1 };

        await using var g = new GrandOutput( new GrandOutputConfiguration() { TimerDuration = TimeSpan.FromMilliseconds( 15 ), Handlers = { textConf } } );
        var m = new ActivityMonitor( ActivityMonitorOptions.SkipAutoConfiguration );
        g.EnsureGrandOutputClient( m );
        m.AddIdentityInformation( "Hello", "World!" );
        m.AddIdentityInformation( "Hello", "World2" );
        m.AddIdentityInformation( "Hello", "World!" );

        Thread.Sleep( 100 );

        g.IdentityCard.Identities["Hello"].Should().NotBeEmpty( "The identity card has been updated." );
        g.IdentityCard.Identities["Hello"].Should().BeEquivalentTo( new[] { "World!", "World2" }, o => o.WithoutStrictOrdering() );

        string tempFile = Directory.EnumerateFiles( folder ).Single();
        var lines = TestHelper.FileReadAllText( tempFile ).Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries );
        var helloUpdateLines = lines.Where( l => l.Contains( $"Hello{ActivityMonitorSimpleSenderExtension.IdentityCard.KeySeparator}World!" ) );
        helloUpdateLines.Should().HaveCount( 1, "The Hello line has been added only once." );

        var hello2UpdateLines = lines.Where( l => l.Contains( $"Hello{ActivityMonitorSimpleSenderExtension.IdentityCard.KeySeparator}World2" ) );
        hello2UpdateLines.Should().HaveCount( 1, "The Hello World2 line has been added only once." );
    }
}
