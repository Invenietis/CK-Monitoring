using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Monitoring
{
    public sealed partial class IdentityCard
    {
        internal void LocalInitialize( IActivityMonitor monitor, bool isDefaultGrandOutput )
        {
            static (string Key, string Value)[] GetAppIdentityInfos()
            {
                var coreId = CoreApplicationIdentity.Instance;
                var id = coreId.DomainName;
                if( coreId.EnvironmentName.Length > 0 ) id += '/' + coreId.EnvironmentName;
                id += '/' + coreId.PartyName;
                return new[] { ("AppIdentity", id),
                               ("AppIdentity/InstanceId", CoreApplicationIdentity.InstanceId),
                               ("AppIdentity/ContextIdentifier", coreId.ContextIdentifier) };
            }

            bool appIdentityInitialized = CoreApplicationIdentity.IsInitialized;
            try
            {
                var infos = new List<(string, string)>( 64 );
                // If the Core identity is ready, do it now.
                if( appIdentityInitialized )
                {
                    infos.AddRangeArray( GetAppIdentityInfos() );
                }

                // Following may more likely to throw (ok... not that much likely, but who knows).
                // Track loaded assemblies versions only for the default GrandOutput.
                if( isDefaultGrandOutput )
                {
                    AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                    foreach( var a in AppDomain.CurrentDomain.GetAssemblies() )
                    {
                        infos.AddRange( GetAssemblyInfos( a ) );
                    }
                }
                AddTimeZone( monitor, infos );
                AddEnvironment( infos );

                infos.Add( ("IdentityCardVersion", CurrentVersion.ToString()) );

                Add( infos );
            }
            catch( Exception ex )
            {
                monitor.Fatal( "Unable to build identity card.", ex );
                // If an error occurred during initialization, ensures
                // that the CoreApplicationIdentity will be registered.
                // The fact that IdentityCardVersion may not be registered is "normal":
                // this version applies to the content of the identity card and if an error
                // occurred, the content is not the one expected.
                appIdentityInitialized = false;
            }
            if( !appIdentityInitialized )
            {
                CoreApplicationIdentity.OnInitialized( () => Add( GetAppIdentityInfos() ) );
            }
        }

        static void AddEnvironment( List<(string Key, string Value)> infos )
        {
            infos.Add( ("IdentityCardVersion", CurrentVersion.ToString()) );
            infos.Add( ("MachineName", Environment.MachineName) );
            infos.Add( ("UserName", Environment.UserName) );
            infos.Add( ("OSVersion", Environment.OSVersion.ToString()) );
            if( OperatingSystem.IsWindows() ) infos.Add( ("OS", "Windows") );
            else if( OperatingSystem.IsLinux() ) infos.Add( ("OS", "Linux") );
            else if( OperatingSystem.IsMacOS() ) infos.Add( ("OS", "MacOS") );
            else if( OperatingSystem.IsBrowser() ) infos.Add( ("OS", "Browser") );
            else if( OperatingSystem.IsAndroid() ) infos.Add( ("OS", "Android") );
            else if( OperatingSystem.IsFreeBSD() ) infos.Add( ("OS", "FreeBSD") );
            else if( OperatingSystem.IsIOS() ) infos.Add( ("OS", "IOS") );
            else if( OperatingSystem.IsMacCatalyst() ) infos.Add( ("OS", "MacCatalyst") );
            else if( OperatingSystem.IsTvOS() ) infos.Add( ("OS", "TvOS") );
            else if( OperatingSystem.IsWatchOS() ) infos.Add( ("OS", "WatchOS") );
        }

        static IEnumerable<(string Key, string Value)> AddTimeZone( IActivityMonitor monitor, IEnumerable<(string Key, string Value)> infos )
        {
            var tz = TimeZoneInfo.Local;
            infos = infos.Append( ("TimeZone", tz.ToSerializedString()) );
            if( tz.HasIanaId )
            {
                infos = infos.Append( ("TimeZone/Id", tz.Id) );
                if( TimeZoneInfo.TryConvertIanaIdToWindowsId( tz.Id, out string? winId ) )
                {
                    infos = infos.Append( ("TimeZone/Id/Windows", winId) );
                }
                else
                {
                    monitor.Warn( $"No Windows time zone found for the IANA identifier '{tz.Id}'." );
                }
            }
            else
            {
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById( tz.Id );
                    infos = infos.Append( ("TimeZone/Id/Windows", tz.Id) );
                }
                catch( Exception ex )
                {
                    monitor.Warn( $"Unable to find the time zone '{tz.Id}' among existing system time zones.", ex );
                    infos = infos.Append( ("TimeZone/Id/Custom", tz.Id) );
                }
                if( TimeZoneInfo.TryConvertWindowsIdToIanaId( tz.Id, out string? ianaId ) )
                {
                    infos = infos.Append( ("TimeZone/Id", ianaId) );
                }
                else
                {
                    monitor.Warn( $"No IANA time zone found for the identifier '{tz.Id}'." );
                }
            }
            return infos;
        }

        internal void LocalUninitialize( bool isDefaultGrandOutput )
        {
            if( isDefaultGrandOutput )
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            }
        }

        void OnAssemblyLoad( object? sender, AssemblyLoadEventArgs args ) => Add( GetAssemblyInfos( args.LoadedAssembly ) );

        IEnumerable<(string Key, string Value)> GetAssemblyInfos( Assembly assembly )
        {
            var name = assembly.GetName();
            var info = (AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute( assembly, typeof( AssemblyInformationalVersionAttribute ) );
            string prefix = $"Assembly/{name.Name}";
            string metaPrefix = prefix + "/Meta/";
            var metas = Attribute.GetCustomAttributes( assembly, typeof( AssemblyMetadataAttribute ) ).Cast<AssemblyMetadataAttribute>();
            if( metas.Any( m => m.Key == "Serviceable" ) ) return Enumerable.Empty<(string Key, string Value)>();
            string vInfo = info?.InformationalVersion
                           ?? name.Version?.ToString()
                           ?? "(null)";
            return metas.Cast<AssemblyMetadataAttribute>().Select( m => (metaPrefix + m.Key, m.Value ?? "(null)") )
                                                          .Prepend( (prefix, vInfo) );
        }

    }
}
