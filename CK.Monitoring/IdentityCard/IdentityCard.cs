using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CK.Monitoring
{
    /// <summary>
    /// Simple thread safe collector of identity informations. See <see cref="Identities"/>.
    /// This is a "convergent data type": Add methods are both idempotent and commutative.
    /// <para>
    /// The characters 0 to 8 (NUl, SOH, STX, ETX, EOT, ENQ, ACK, BEL, BS) are invalid in a key and in a value.
    /// </para> 
    /// <para>
    /// A key cannot contain newlines: the Unicode Standard, Sec. 5.8, Recommendation R4 and Table 5-2 state
    /// that the CR, LF, CRLF, NEL, LS, FF, and PS sequences are considered newline functions.
    /// </para>
    /// <para>
    /// This identity card has been designed to be used independently of the GrandOuput: each GrandOuput has its own identity card.
    /// </para>
    /// <para>
    /// Thread safe change tracking can be done "on the outside" thanks to the returned values of the <see cref="Add(ValueTuple{string, string}[])"/>
    /// and <see cref="Add(string, string)"/> methods or "behind" by using the <see cref="OnChanged"/> event.
    /// </para>
    /// <para>
    /// Since identity is mostly stable during the life of the application (it will be updated during application start), we use
    /// snapshots of an internal dictionary as the exposed Identities and a simple lock to update them.
    /// </para>
    /// </summary>
    public sealed class IdentityCard
    {
        // We use the _card dictionary instance as the lock.
        readonly Dictionary<string, string[]> _card;
        Action<IdentiCardChangedEvent>? _onChange;
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> _exposed;
        string? _toString;

        /// <summary>
        /// Holds tags related to identity tags.
        /// </summary>
        public static class Tags
        {
            /// <summary>
            /// Gets the tag that identify an <see cref="IdentityCard"/> snapshot.
            /// The log text is the identity's packed <see cref="ToString()"/>.
            /// </summary>
            public static readonly CKTrait IdentityCard = ActivityMonitor.Tags.Context.FindOrCreate( nameof( IdentityCard ) );

            /// <summary>
            /// Gets the tag that identify an addition to an <see cref="IdentityCard"/>.
            /// The log text is the added key/value pairs packed with <see cref="Pack(IReadOnlyList{ValueTuple{string, string}})"/>.
            /// </summary>
            public static readonly CKTrait IdentityCardAdd = ActivityMonitor.Tags.Context.FindOrCreate( nameof( IdentityCardAdd ) );
        }

        /// <summary>
        /// Initializes a new empty <see cref="IdentityCard"/>.
        /// </summary>
        public IdentityCard()
            : this( new Dictionary<string, string[]>() )
        {
        }

        IdentityCard( Dictionary<string, string[]> card )
        {
            _card = card;
            _exposed = card.AsIReadOnlyDictionary<string, string[], IReadOnlyCollection<string>>();
        }

        /// <summary>
        /// Event raised on change.
        /// Caution: this event can be raised concurrently.
        /// </summary>
        public event Action<IdentiCardChangedEvent>? OnChanged
        {
            add { _onChange += value; }
            remove { _onChange -= value; }
        }

        /// <summary>
        /// Gets an immutable capture of the current identity informations.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Identities => _exposed;

        /// <summary>
        /// Adds a single identity information.
        /// <para>
        /// The characters 0 to 8 (NUl, SOH, STX, ETX, EOT, ENQ, ACK, BEL, BSP) are invalid in a key and in a value.
        /// </para> 
        /// <para>
        /// A key cannot contain newlines: the Unicode Standard, Sec. 5.8, Recommendation R4 and Table 5-2 state
        /// that the CR, LF, CRLF, NEL, LS, FF, and PS sequences are considered newline functions.
        /// </para>
        /// </summary>
        /// <param name="key">The identity key. Must not be null, empty or white space or contain any line delimiter.</param>
        /// <param name="value">The identity information. Must not be null, empty or white space.</param>
        /// <returns>The new <see cref="Identities"/> if the value has been added, or null if it was already present.</returns>
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Add( string key, string value )
        {
            IReadOnlyDictionary<string, IReadOnlyCollection<string>>? result = null;
            lock( _card )
            {
                if( !DoAdd( key, value ) ) return result;
                var copy = new Dictionary<string, string[]>( _card );
                _exposed = result = copy.AsIReadOnlyDictionary<string, string[], IReadOnlyCollection<string>>();
                _toString = null;
            }
            _onChange?.Invoke( new IdentiCardChangedEvent( this, new[] { (key, value) }, result ) );
            return result;
        }

        /// <summary>
        /// Adds multiple identity information at once. See <see cref="Add(string, string)"/> for allowed characters
        /// in key and value strings.
        /// </summary>
        /// <param name="info">Multiple identity informations.</param>
        /// <returns>The key value pairs actually added and the modified identities: resp. empty an null if no modification occurred.</returns>
        public (IReadOnlyList<(string Key, string Value)> Added, IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Changed) Add( params (string Key, string Value)[] info )
        {
            IReadOnlyDictionary<string, IReadOnlyCollection<string>>? newOne = null;
            List<(string, string)>? applied = null;
            bool atLeastOne = false;
            bool atLeastNotOne = false;
            lock( _card )
            {
                for( int i = 0; i < info.Length; ++i )
                {
                    ref var kv = ref info[i];
                    if( !DoAdd( kv.Key, kv.Value ) )
                    {
                        if( atLeastOne && applied == null )
                        {
                            applied = info.Take( i ).ToList();
                        }
                        atLeastNotOne = true;
                    }
                    else
                    {
                        if( atLeastNotOne && applied == null ) applied = new List<(string, string)>();
                        if( applied != null ) applied.Add( kv );
                        atLeastOne = true;
                    }
                }
                if( atLeastOne )
                {
                    var copy = new Dictionary<string, string[]>( _card );
                    _exposed = newOne = copy.AsIReadOnlyDictionary<string, string[], IReadOnlyCollection<string>>();
                    _toString = null;
                }
            }
            if( atLeastOne )
            {
                Debug.Assert( newOne != null );
                var r = (IReadOnlyList<(string Key, string Value)>?)applied ?? info;
                _onChange?.Invoke( new IdentiCardChangedEvent( this, r, newOne ) );
                return (r, newOne);
            }
            return (Array.Empty<(string, string)>(), null);
        }

        bool DoAdd( string key, string value )
        {
            Debug.Assert( Monitor.IsEntered( _card ) );
            Throw.CheckNotNullOrEmptyArgument( key );
            Throw.CheckNotNullOrEmptyArgument( value );
            Throw.CheckArgument( "Invalid newline character in identity key.", key.AsSpan().IndexOfAny( "\r\n\f\u0085\u2028\u2029" ) < 0 );
            Throw.CheckArgument( "First 8 characters (NUl, SOH, STX, ETX, EOT, ENQ, ACK, BEL, BSP) cannot appear.",
                                  key.AsSpan().IndexOfAny( "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008" ) < 0 );
            Throw.CheckArgument( "First 8 characters (NUl, SOH, STX, ETX, EOT, ENQ, ACK, BEL, BSP) cannot appear.",
                                  value.AsSpan().IndexOfAny( "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008" ) < 0 );
            return DoAdd( _card, key, value );
        }

        static bool DoAdd( Dictionary<string, string[]> card, string key, string value )
        {
            if( card.TryGetValue( key, out var exist ) )
            {
                if( exist.Contains( value ) ) return false;
                Array.Resize( ref exist, exist.Length + 1 );
                exist[^1] = value;
                card[key] = exist;
            }
            else
            {
                card.Add( key, new[] { value } );
            }
            return true;
        }

        /// <summary>
        /// Packs multiple key/values in a single string separated by STX (0x02) characters.
        /// This relies on the fact that STX character doesn't appear in the strings and this is not tested
        /// since this is to be used for identity information that are necessarily valid.
        /// <para>
        /// Use <see cref="TryUnpack(ReadOnlySpan{char})"/> to unpack the resulting string.
        /// </para>
        /// </summary>
        /// <param name="values">The values to pack in one string. Must not be empty.</param>
        /// <returns>The packed string (empty for no values).</returns>
        public static string Pack( IReadOnlyList<(string Key, string Value)> values )
        {
            Throw.CheckArgument( values != null && values.Count > 0 );
            if( values.Count == 1 )
            {
                var single = values[0];
                return String.Create( single.Key.Length + 1 + single.Value.Length, single, ( s, v ) =>
                {
                    v.Key.CopyTo( s );
                    s[v.Key.Length] = '\u0002';
                    s = s.Slice( v.Key.Length + 1 );
                    v.Value.CopyTo( s );
                } );
            }
            int len = 0;
            foreach( var v in values ) len += v.Key.Length + v.Value.Length + 2;
            return String.Create( len, values, ( s, values ) =>
            {
                foreach( var v in values )
                {
                    s[0] = '\u0002';
                    s = s.Slice( 1 );
                    v.Key.CopyTo( s );
                    s[v.Key.Length] = '\u0002';
                    s = s.Slice( v.Key.Length + 1 );
                    v.Value.CopyTo( s );
                    s = s.Slice( v.Value.Length );
                }
            } );
        }

        /// <summary>
        /// Packs <see cref="Identities"/> in a single string separated by SOH (0x01) and STX (0x02) characters.
        /// This relies on the fact that ETX character doesn't appear in the strings and this is not tested
        /// since this is to be used for identity information that are necessarily valid.
        /// <para>
        /// <see cref="TryUnpack(ReadOnlySpan{char})"/> or <see cref="TryCreate(ReadOnlySpan{char})"/> can be used
        /// to unpack the string.
        /// </para>
        /// </summary>
        /// <param name="identities">The identities to pack in one string.</param>
        /// <returns>The packed string (empty for no values).</returns>
        public static string Pack( IReadOnlyDictionary<string, IReadOnlyCollection<string>> identities )
        {
            int len = identities.Count;
            foreach( var kv in identities )
            {
                len += kv.Key.Length + kv.Value.Count;
                foreach( var v in kv.Value )
                {
                    len += v.Length;
                }
            }
            return String.Create( len, identities, (s, identities ) =>
            {
                foreach( var kv in identities )
                {
                    s[0] = '\u0001';
                    s = s.Slice( 1 );
                    kv.Key.CopyTo( s );
                    s = s.Slice( kv.Key.Length );
                    foreach( var v in kv.Value )
                    {
                        s[0] = '\u0002';
                        s = s.Slice( 1 );
                        v.CopyTo( s );
                        s = s.Slice( v.Length );
                    }
                }
            } );
        }

        /// <summary>
        /// Gets the identities packed with <see cref="Pack(IReadOnlyDictionary{string, IReadOnlyCollection{string}})"/>.
        /// </summary>
        /// <returns>The identities as a SOH (0x01) and STX (0x02) characters separated string.</returns>
        public override string ToString() => _toString ??= Pack( _exposed );

        /// <summary>
        /// Tries to unpack non empty IReadOnlyList&lt;(string, string)&gt; or a IReadOnlyDictionary&lt;string, IReadOnlyCollection&lt;string&gt;&gt;
        /// previously packed.
        /// </summary>
        /// <param name="s">The packed string. When empty, null is returned.</param>
        /// <returns>A read only list of tuples or dictionary of strings, or null if the string cannot be unpacked.</returns>
        public static object? TryUnpack( ReadOnlySpan<char> s )
        {
            if( s.Length < 3 ) return null;
            char c = s[0];
            if( c == '\u0001' )
            {
                return s.Length < 4 ? null : DoTryUnpackIdentities( s )?.AsIReadOnlyDictionary<string, string[], IReadOnlyCollection<string>>();
            }
            int idx = 0;
            string key;
            if( c == '\u0002' )
            {
                var kv = new List<(string, string)>();
                for(; ; )
                {
                    s = s.Slice( idx + 1 );
                    idx = s.IndexOf( '\u0002' );
                    if( idx <= 0 ) return null;
                    key = s.Slice( 0, idx ).ToString();
                    s = s.Slice( idx + 1 );
                    idx = s.IndexOf( '\u0002' );
                    if( idx == 0 ) return null;
                    if( idx < 0 )
                    {
                        if( s.Length == 0 ) return null;
                        kv.Add( (key, s.ToString()) );
                        return kv;
                    }
                    kv.Add( (key, s.Slice( 0, idx ).ToString()) );
                }
            }
            idx = s.IndexOf( '\u0002' );
            if( idx <= 0 ) return null;
            key = s.Slice( 0, idx ).ToString();
            s = s.Slice( idx + 1 );
            if( s.Length == 0 ) return null;
            return new[] { (key, s.ToString()) };
        }

        /// <summary>
        /// Tries to create an identity card from its packed identities provided by <see cref="Pack(IReadOnlyDictionary{string, IReadOnlyCollection{string}})"/>
        /// or its <see cref="ToString()"/>.
        /// </summary>
        /// <param name="identities">Packed string to parse.</param>
        /// <returns>The identity card or null.</returns>
        public static IdentityCard? TryCreate( ReadOnlySpan<char> identities )
        {
            if( identities.Length == 0 ) return new IdentityCard();
            if( identities.Length < 4 ) return null;
            if( identities[0] != '\u0001' ) return null;
            var d = DoTryUnpackIdentities( identities );
            return d == null ? null : new IdentityCard( d );
        }

        static Dictionary<string, string[]>? DoTryUnpackIdentities( ReadOnlySpan<char> s )
        {
            var card = new Dictionary<string, string[]>();
            s = s.Slice( 1 );
            int idx = s.IndexOf( '\u0002' );
            if( idx <= 0 ) return null;
            string key;
            for(; ; )
            {
                key = s.Slice( 0, idx ).ToString();
                s = s.Slice( idx + 1 );
                idx = s.IndexOfAny( '\u0001', '\u0002' );
                if( idx <= 0 ) break;
                if( idx == 0 ) return null;
                if( idx < 0 ) break;
                more:
                char next = s[idx];
                DoAdd( card, key, s.Slice( 0, idx ).ToString() );
                s = s.Slice( idx + 1 );
                idx = s.IndexOfAny( '\u0001', '\u0002' );
                if( idx == 0 ) return null;
                if( idx < 0 ) break;
                if( next == '\u0001' ) continue;
                goto more;
            }
            if( idx < 0 )
            {
                DoAdd( card, key, s.ToString() );
                return card;
            }
            return null;
        }
    }
}
