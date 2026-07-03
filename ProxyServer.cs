using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http; // Required for WriteAsync
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace ErectRoom
{
    public static class ProxyServer
    {
        // Thread-safe request counter for the specific endpoint
        private static int _challengeRequestCount = 0;

        public static async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var builder = WebApplication.CreateBuilder();

            // Set up certificates as configured previously
            System.Security.Cryptography.X509Certificates.X509Certificate2 proxyCert =
                CertificateUtility.CreateAndTrustCertificate("rec.net");

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(443, listenOptions =>
                {
                    listenOptions.UseHttps(proxyCert);
                });
            });

            builder.Services.AddReverseProxy()
                .LoadFromMemory(GetRoutes(), GetClusters())
                .AddTransforms(transformBuilderContext =>
                {
                    transformBuilderContext.AddRequestTransform(async transformContext =>
                    {
                        var httpRequest = transformContext.HttpContext.Request;
                        transformContext.ProxyRequest.Headers.Host = "tmb.tabbycluster.net";
                        transformContext.ProxyRequest.Headers.Add("X-Forwarded-Host", httpRequest.Host.Host);
                    });
                })
                .ConfigureHttpClient((context, handler) =>
                {
                    handler.PooledConnectionLifetime = TimeSpan.FromSeconds(90);
                    handler.PooledConnectionIdleTimeout = TimeSpan.FromSeconds(60);
                    handler.EnableMultipleHttp2Connections = true;
                });

            var app = builder.Build();

         
            app.Use(async (context, next) =>
            {
                string path = context.Request.Path.Value ?? string.Empty;

            
                if (path.Equals("/api/versioncheck/v4", StringComparison.OrdinalIgnoreCase))
                {
               
                    Interlocked.Exchange(ref _challengeRequestCount, 0);

                 
                    await next(context);
                    return;
                }

                if (path.Equals("/player/logout", StringComparison.OrdinalIgnoreCase))
                {
                
                    Interlocked.Exchange(ref _challengeRequestCount, 0);

                    await next(context);
                    return;
                }

             
                if (path.Equals("/eac/challenge", StringComparison.OrdinalIgnoreCase) &&
                    context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
               
                    int currentCount = Interlocked.Increment(ref _challengeRequestCount);

                    context.Response.ContentType = "text/plain";

                    if (currentCount == 1)
                    {
                 
                        await context.Response.WriteAsync("\"\"");
                        return; // Short-circuit
                    }
                    else // currentCount is 2 or higher
                    {
             
                        await context.Response.WriteAsync("==");
                        return; // Short-circuit
                    }
                }

                await next(context);
            });

         
            app.MapReverseProxy();

            await app.RunAsync(cancellationToken);
        }

        private static IReadOnlyList<RouteConfig> GetRoutes()
        {
            return new[]
            {
                new RouteConfig()
                {
                    RouteId = "global_catchall",
                    ClusterId = "upstream",
                    Match = new RouteMatch() { Hosts = new[] { "*" } }
                }
            };
        }

        private static IReadOnlyList<ClusterConfig> GetClusters()
        {
            return new[]
            {
                new ClusterConfig()
                {
                    ClusterId = "upstream",
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new DestinationConfig() { Address = "https://tmb.tabbycluster.net" } }
                    }
                }
            };
        }
    }
}