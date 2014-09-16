﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;


namespace NuGet.Services.Metrics.Core
{
    public class PackageStatsHandler
    {
        public const string SqlConfigurationKey = "Metrics.SqlServer";
        public const string CommandTimeoutKey = "Metrics.CommandTimeout";
        public const string CatalogIndexUrlKey = "Metrics.CatalogIndexUrl";
        public const string IsLocalCatalogKey = "Metrics.IsLocalCatalog";

        private readonly MetricsStorage _metricsStorage;
        private int _count = 0;
        private const string HTTPPost = "POST";
        private static readonly PathString Root = new PathString("/");
        private static readonly PathString DownloadEvent = new PathString("/DownloadEvent");

        public PackageStatsHandler(NameValueCollection appSettings)
        {
            string connectionString = appSettings[SqlConfigurationKey];
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Metrics.SqlServer is not present in the configuration");
            }

            var commandTimeoutString = appSettings[PackageStatsHandler.CommandTimeoutKey];
            int commandTimeout = 0;
            if (!String.IsNullOrEmpty(commandTimeoutString))
            {
                Int32.TryParse(commandTimeoutString, out commandTimeout);
            }

            string isLocalCatalogString = appSettings[PackageStatsHandler.IsLocalCatalogKey];
            bool isLocalCatalog = false;
            if (!String.IsNullOrEmpty(isLocalCatalogString))
            {
                isLocalCatalog = Boolean.TryParse(isLocalCatalogString, out isLocalCatalog);
            }

            string catalogIndexUrl = appSettings[PackageStatsHandler.CatalogIndexUrlKey];
            if(String.IsNullOrEmpty(catalogIndexUrl))
            {
                // CatalogIndexUrl is not provided. Assume that database should be used for storing package statistics
                _metricsStorage = new DatabaseMetricsStorage(connectionString, commandTimeout);
            }
            else
            {
                _metricsStorage = new CatalogMetricsStorage(connectionString, commandTimeout, catalogIndexUrl, isLocalCatalog);
            }
        }

        public async Task Invoke(IOwinContext context)
        {
            if (context.Request.Path.StartsWithSegments(Root))
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync("NuGet Metrics Service: OK");
            }
            else if (context.Request.Path.StartsWithSegments(DownloadEvent))
            {
                if (context.Request.Method != HTTPPost)
                {
                    context.Response.Headers.Add("Allow", new[] { "POST" });
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    await context.Response.WriteAsync("Only HTTP POST requests are accepted");
                    return;
                }

                // TODO: NEED TO ADD CHECK TO ENSURE THAT THE STREAM IS NOT TOO LONG
                //       Note that Stream 'IOwinRequest.Body' does not support length
                using (var streamReader = new StreamReader(context.Request.Body))
                {
                    try
                    {
                        var jsonString = await streamReader .ReadToEndAsync();
                        var jToken = JToken.Parse(jsonString);
                        Task.Run(() => ProcessJToken(jToken));
                        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                await context.Response.WriteAsync("Page is not found");                
            }
        }

        private async Task ProcessJToken(JToken jToken)
        {
            if (jToken is JObject)
            {
                await ProcessJObject((JObject)jToken);
            }
            else if (jToken is JArray)
            {
                await ProcessJArray((JArray)jToken);
            }
        }

        private async Task ProcessJArray(JArray jArray)
        {
            foreach (var item in jArray)
            {
                if (item is JObject)
                {
                    await ProcessJObject((JObject)item);
                }
            }
        }

        private async Task ProcessJObject(JObject jObject)
        {
            Interlocked.Increment(ref _count);
            int count = _count;
            Trace.WriteLine("Processing count : " + count);
            try
            {
                await _metricsStorage.AddPackageDownloadStatistics(jObject);
                Trace.TraceInformation("Package Download Statistics processed successfully");
            }
            catch (Exception ex)
            {
                // Catch all exceptions
                Trace.TraceError(ex.ToString());
            }
            Trace.WriteLine("Completed processing for count: " + count);
        }
    }
}
