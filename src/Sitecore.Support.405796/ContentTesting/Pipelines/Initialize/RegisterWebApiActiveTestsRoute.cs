using Sitecore.ContentTesting.Configuration;
using Sitecore.ContentTesting.Diagnostics;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System.Web.Http;
using System.Web.Routing;

namespace Sitecore.Support.ContentTesting.Pipelines.Initialize
{
    public class RegisterWebApiActiveTestsRoute
    {
        public virtual void Process(PipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (!Settings.IsAutomaticContentTestingEnabled)
            {
                return;
            }

            this.RegisterActiveTestsRoute(RouteTable.Routes, args);
        }

        protected virtual void RegisterActiveTestsRoute(RouteCollection routes, PipelineArgs args)
        {
            const string rootName = "Sitecore.Support.405796 - ActiveTests";
            var existing = routes[rootName];
            if (existing != null)
            {
                Logger.Warn($"Route '{rootName}' has already been added. Ensure only a single route processor for Content Testing.");
                return;
            }

            routes.MapHttpRoute(rootName, Settings.CommandRoutePrefix + "Tests/GetActiveTests",
                new { controller = "Tests405796", action = "GetActiveTestsWithScSiteParam" });
        }
    }
}