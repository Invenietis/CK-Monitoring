using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.Core;

namespace CK.Monitoring
{
    public class StupidStringClient : ActivityMonitorTextHelperClient
    {
        public class Entry
        {
            public readonly LogLevel Level;
            public readonly CKTrait Tags;
            public readonly string Text;
            public readonly object? Exception;
            public readonly DateTimeStamp LogTime;

            public Entry( ref ActivityMonitorLogData d )
            {
                Level = d.Level;
                Tags = d.Tags;
                Text = d.Text;
                Exception = (object?)d.Exception ?? d.ExceptionData;
                LogTime = d.LogTime;
            }

            public Entry( IActivityLogGroup d )
                : this( ref d.Data )
            {
            }

            public override string ToString()
            {
                return String.Format( "{0} - {1} - {2} - {3}", LogTime, Level, Text, Exception != null ? Exception.ToString() : "<no exception>" );
            }
        }
        public readonly List<Entry> Entries;
        public StringWriter Writer { get; private set; }
        public bool WriteTags { get; private set; }
        public bool WriteConclusionTraits { get; private set; }

        public StupidStringClient( bool writeTags = false, bool writeConclusionTraits = false )
        {
            Entries = new List<Entry>();
            Writer = new StringWriter();
            WriteTags = writeTags;
            WriteConclusionTraits = writeConclusionTraits;
        }

        protected override void OnEnterLevel( ref ActivityMonitorLogData data )
        {
            Entries.Add( new Entry( ref data ) );
            Writer.WriteLine();
            Writer.Write( data.MaskedLevel.ToString() + ": " + data.Text );
            if( WriteTags ) Writer.Write( "-[{0}]", data.Tags.ToString() );
            if( data.Exception != null ) Writer.Write( "Exception: " + data.Exception.Message );
            else if( data.ExceptionData != null ) Writer.Write( "Exception: " + data.ExceptionData.Message );
        }

        protected override void OnContinueOnSameLevel( ref ActivityMonitorLogData data )
        {
            Entries.Add( new Entry( ref data ) );
            Writer.Write( data.Text );
            if( WriteTags ) Writer.Write( "-[{0}]", data.Tags.ToString() );
            if( data.Exception != null ) Writer.Write( "Exception: " + data.Exception.Message );
            else if( data.ExceptionData != null ) Writer.Write( "Exception: " + data.ExceptionData.Message );
        }

        protected override void OnLeaveLevel( LogLevel level )
        {
            Writer.Flush();
        }

        protected override void OnGroupOpen( IActivityLogGroup g )
        {
            Entries.Add( new Entry( g ) );
            Writer.WriteLine();
            Writer.Write( new String( '+', g.Data.Depth + 1 ) );
            Writer.Write( "{1} ({0})", g.Data.MaskedLevel, g.Data.Text );
            if( g.Data.Exception != null ) Writer.Write( "Exception: " + g.Data.Exception.Message );
            else if( g.Data.ExceptionData != null ) Writer.Write( "Exception: " + g.Data.ExceptionData.Message );
            if( WriteTags ) Writer.Write( "-[{0}]", g.Data.Tags.ToString() );
        }

        protected override void OnGroupClose( IActivityLogGroup g, IReadOnlyList<ActivityLogGroupConclusion> conclusions )
        {
            Writer.WriteLine();
            Writer.Write( new String( '-', g.Data.Depth + 1 ) );
            if( WriteConclusionTraits )
            {
                Writer.Write( String.Join( ", ", conclusions.Select( c => c.Text + "-/[/" + c.Tag.ToString() + "/]/" ) ) );
            }
            else
            {
                Writer.Write( String.Join( ", ", conclusions.Select( c => c.Text ) ) );
            }
        }

        public string ToStringFromWriter()
        {
            return Writer.ToString();
        }

        public override string ToString()
        {
            return String.Join( Environment.NewLine, Entries );
        }
    }

}
