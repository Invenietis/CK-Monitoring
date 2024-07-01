using Microsoft.Extensions.Hosting;

namespace CK
{
    /// <summary>
    /// Adds extension methods on <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    public static class HostApplicationBuilderMonitoringExtensions
    {
        /*
         * NOT AVAILABLE IN .NET6
         * But not sure that this is the good pattern anyway:
         * - new .NET8 HostApplicationBuilder is the way to go.
         * - CK.AppIdentity needs to be accounted.
         * - 2 possible ways
         *   - CK-Monitoring is top section: it is configured once, "externally" at the start of the program.
         *   - CK-Monitoring is a CK-AppIdentity section: it is the build of the AppIdentityService that can configure
         *     the GrandOutput... it's late... very late.
         *     Moreover this section could really only be in the "Local" one... that is rather useless.
         *  => It seems that the CK-Monitoring should be the one and only one top-level configuration with CK-AppIdentity section.
         *     UseCKMonitoring is/becomes... useless!
         * 
                public static IHostApplicationBuilder UseCKMonitoring( this IHostApplicationBuilder builder )
                {
                    return builder;
                }
        */
    }
}
