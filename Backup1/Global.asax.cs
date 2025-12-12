using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using IdentityMetadataFetcher.Iis.Modules;

namespace MvcDemo
{
    public class Global : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteTable.Routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
