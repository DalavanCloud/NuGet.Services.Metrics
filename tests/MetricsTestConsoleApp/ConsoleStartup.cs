﻿using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Owin;
using NuGet.Services.Metrics.Core;
using Owin;
using System.Collections.Specialized;

namespace MetricsTestConsoleApp
{
    internal class ConsoleStartup
    {
        private PackageStatsHandler _packageStatsHandler;
        private const string ConnectionString = "Data Source=(LocalDB)\\v11.0;Integrated Security=SSPI;Initial Catalog=NuGetGallery";
        private const int CommandTimeout = 5;
        private const string CatalogIndexUrl = "http://localhost:8000/CatalogMetricsStorage";
        private const bool IsLocalCatalog = true;
        public void Configuration(IAppBuilder appBuilder)
        {
            NameValueCollection appSettings = new NameValueCollection();
            appSettings.Add(PackageStatsHandler.SqlConfigurationKey, ConnectionString);
            appSettings.Add(PackageStatsHandler.CommandTimeoutKey, CommandTimeout.ToString());
            appSettings.Add(PackageStatsHandler.CatalogIndexUrlKey, CatalogIndexUrl);
            appSettings.Add(PackageStatsHandler.IsLocalCatalogKey, IsLocalCatalog.ToString());

            _packageStatsHandler = new PackageStatsHandler(appSettings);
            appBuilder.Run(Invoke);
        }

        private async Task Invoke(IOwinContext context)
        {
            var requestUri = context.Request.Uri;
            Trace.WriteLine("Request received : " + requestUri.AbsoluteUri);

            await _packageStatsHandler.Invoke(context);
            Trace.WriteLine("Request accepted. Processing...");
        }
    }
}
