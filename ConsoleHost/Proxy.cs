﻿using BuskerProxy.Handlers;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Web.Http;

[assembly: OwinStartup(typeof(BuskerProxy.Host.Proxy))]

namespace BuskerProxy.Host
{
    public class Proxy
    {
        static List<IDisposable> apps = new List<IDisposable>();
        
        public static void Start(string proxyAddress)
        {
            try
            {
                // Start OWIN proxy host 
                apps.Add(WebApp.Start<Proxy>(proxyAddress));
                Trace.TraceInformation("Listening on:" + proxyAddress);
                Trace.WriteLine("Set your IE proxy to:" + proxyAddress);
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if(ex.InnerException!=null)
                    message+=":"+ex.InnerException.Message;
                Trace.TraceInformation(message );
            }
        }

        public static void Stop()
        {
            foreach (var app in apps)
            {
                if (app != null)
                    app.Dispose();
            }
        }


        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            appBuilder.MapSignalR();
            
            // Configure Web API for self-host. 
            HttpConfiguration httpconfig = new HttpConfiguration();
            RegisterRoutes(httpconfig);
            appBuilder.UseWebApi(httpconfig);
        }

        private void RegisterRoutes(HttpConfiguration config)
        {
            //anything with busker in the name send to the static file handler
            config.Routes.MapHttpRoute(
                name: "Busker",
                routeTemplate: "{*path}",
                defaults: new { path = RouteParameter.Optional },
                constraints: new { isLocal = new HostConstraint { Host = "busker" } },
                handler: new StaticFileHandler()
            );

            config.Routes.MapHttpRoute(
                    name: "ConfigAzureAuth",
                    routeTemplate: "config/azureauth",
                    defaults: new { controller = "ConfigAzureAuth" }
                );

            config.Routes.MapHttpRoute(
                    name: "Proxy",
                    routeTemplate: "{*path}",
                    handler: HttpClientFactory.CreatePipeline
                        (
                            innerHandler: new HttpClientHandler(), // will never get here if proxy is doing its job
                            handlers: new DelegatingHandler[] 
                            { 
                                new PortStripHandler(),
                                new LoggingHandler(),
                                new AzureAuthHandler(),
                                new ProxyHandler() 
                            }
                        ),
                    defaults: new { path = RouteParameter.Optional },
                    constraints: null
                );
        }
    }
}
