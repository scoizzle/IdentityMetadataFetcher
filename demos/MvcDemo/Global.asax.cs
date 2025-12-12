using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using IdentityMetadataFetcher.Iis.Modules;
using MvcDemo.Utilities;

namespace MvcDemo
{
    public class Global : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            
            // Clear any existing TraceLogBuffer listeners to prevent duplicates
            var existingListeners = new List<TraceListener>();
            foreach (TraceListener listener in Trace.Listeners)
            {
                if (!(listener is TraceLogBuffer))
                {
                    existingListeners.Add(listener);
                }
            }
            
            // Clear all listeners
            Trace.Listeners.Clear();
            
            // Re-add the non-TraceLogBuffer listeners
            foreach (var listener in existingListeners)
            {
                Trace.Listeners.Add(listener);
            }
            
            // Now add our singleton TraceLogBuffer once
            Trace.Listeners.Add(TraceLogBuffer.Instance);
            
            RouteTable.Routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            RouteTable.Routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
