using System.Diagnostics;
using Docfx;
using Docfx.Dotnet;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Documentation;

internal class Program
{
    private const string BaseUrl = "http://localhost:8080";

    private static async Task Main(string[] args)
    {
        await DotnetApiCatalog.GenerateManagedReferenceYamlFiles("../../../docfx.json").ConfigureAwait(false);
        await Docset.Build("../../../docfx.json").ConfigureAwait(false);

#if DEBUG
        await RunDocumentation(args).ConfigureAwait(false);
#endif
    }

    private static async Task RunDocumentation(string[] args)
    {
        var webServerTask = CreateHostBuilder(args).Build().RunAsync(CancellationToken.None);

        var openBrowser = new ProcessStartInfo($"{BaseUrl}/index.html") { UseShellExecute = true };

        try
        {
            Process.Start(openBrowser);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        await webServerTask.ConfigureAwait(false);
    }

    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            webBuilder =>
            {
                webBuilder.UseStartup<Program>();
                webBuilder.UseUrls(BaseUrl); // Set your URL and port
            });

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        string contentRootPath = env.ContentRootPath;
        string staticFilesPath = Path.Combine(contentRootPath, "../../../_site");

        app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(staticFilesPath),
                RequestPath = "" // Important:  This makes the _site content available at the root.
            });

        app.UseRouting(); // Required, even if empty.
        app.UseEndpoints(_ => { }); // Required, even if empty.
    }
}