using System.Text.Json;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddRouteExtensions<UserModel>("User")
    .AddRouteExtensions<ABTest>("ABTest")
    .AddClusterExtensions<Service>("Service")
    //Error type
    .AddRouteExtensions<More>("More");

var app = builder.Build();

app.MapReverseProxy();

app.UseRouting();
app.UseEndpoints(endpoints =>
{

    endpoints.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.Use((context, next) =>
        {
            var proxyFeature = context.Features.Get<IReverseProxyFeature>();

            var user = proxyFeature?.Route.Config.GetExtension<UserModel>();
            Console.WriteLine(user?.Name);

            var ab = proxyFeature?.Route.Config.GetExtension<ABTest>();
            Console.WriteLine(JsonSerializer.Serialize(ab));

            var more = proxyFeature?.Route.Config.GetExtension<More>();
            Console.WriteLine(more?.Information);

            var service = proxyFeature?.Cluster.Config.GetExtension<Service>();
            Console.WriteLine(service?.State);

            return next();
        });
    });
});

app.Run();


public class UserModel : IConfigExtension
{
    public string Name { get; set; }
}

public class More : IConfigExtension
{
    public string Information { get; set; }
}

public class ABTest : IConfigExtension
{
    public Dictionary<string, double> ABTests { get; set; }
}

public class Service : IConfigExtension
{
    public string State { get; set; }
}
