//===================================================
//  License: Apache-2.0
//  Contributors: yiyungent@gmail.com
//  Project: https://moeci.com/PluginCore
//  GitHub: https://github.com/yiyungent/PluginCore
//===================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PluginCore.IPlugins.Infrastructure;

namespace PluginCore.AspNetCore.AdminUI;

public class PluginCoreAdminUIMiddleware
{
    private const string EmbeddedFileNamespace = "PluginCore.AspNetCore.node_modules.plugincore_admin_frontend.dist";

    private readonly PluginCoreAdminUIOptions _options;
    private readonly StaticFileMiddleware _staticFileMiddleware;


    public PluginCoreAdminUIMiddleware(
        RequestDelegate next,
        IWebHostEnvironment hostingEnv,
        ILoggerFactory loggerFactory,
        PluginCoreAdminUIOptions options)
    {
        _options = options ?? new PluginCoreAdminUIOptions();

        _staticFileMiddleware = CreateStaticFileMiddleware(next, hostingEnv, loggerFactory, options);
    }

    public async Task Invoke(HttpContext httpContext)
    {
        var httpMethod = httpContext.Request.Method;
        var path = httpContext.Request.Path.Value;

        // If the RoutePrefix is requested (with or without trailing slash), redirect to index URL
        if (httpMethod == "GET" && Regex.IsMatch(path, $"^/?{Regex.Escape(_options.RoutePrefix)}/?$", RegexOptions.IgnoreCase))
        {
            // Use relative redirect to support proxy environments
            var relativeIndexUrl = string.IsNullOrEmpty(path) || path.EndsWith("/")
                ? "index.html"
                : $"{path.Split('/').Last()}/index.html";

            RespondWithRedirect(httpContext.Response, relativeIndexUrl);
            return;
        }

        if (httpMethod == "GET" && Regex.IsMatch(path, $"^/{Regex.Escape(_options.RoutePrefix)}/?index.html$", RegexOptions.IgnoreCase))
        {
            await RespondWithIndexHtml(httpContext.Response);
            return;
        }

        await _staticFileMiddleware.Invoke(httpContext);
    }

    private StaticFileMiddleware CreateStaticFileMiddleware(
        RequestDelegate next,
        IWebHostEnvironment hostingEnv,
        ILoggerFactory loggerFactory,
        PluginCoreAdminUIOptions options)
    {
        Config.PluginCoreConfig pluginCoreConfig = Config.PluginCoreConfigFactory.Create();
        IFileProvider fileProvider = null;
        switch (pluginCoreConfig.FrontendMode?.ToLower())
        {
            case "localembedded":
                fileProvider = new EmbeddedFileProvider(typeof(PluginCoreAdminUIMiddleware).GetTypeInfo().Assembly,
                    EmbeddedFileNamespace);
                break;
            case "localfolder":
                string absoluteRootPath = PluginPathProvider.PluginCoreAdminDir();
                fileProvider = new PhysicalFileProvider(absoluteRootPath);
                break;
            case "remotecdn":
                fileProvider = new PluginCoreAdminUIRemoteFileProvider(pluginCoreConfig.RemoteFrontend);
                break;
            default:
                fileProvider = new EmbeddedFileProvider(typeof(PluginCoreAdminUIMiddleware).GetTypeInfo().Assembly,
                    EmbeddedFileNamespace);
                break;
        }

        var staticFileOptions = new StaticFileOptions
        {
            RequestPath = string.IsNullOrEmpty(options.RoutePrefix) ? string.Empty : $"/{options.RoutePrefix}",
            FileProvider = fileProvider,
        };

        return new StaticFileMiddleware(next, hostingEnv, Options.Create(staticFileOptions), loggerFactory);
    }

    private void RespondWithRedirect(HttpResponse response, string location)
    {
        response.StatusCode = 301;
        response.Headers["Location"] = location;
    }

    private async Task RespondWithIndexHtml(HttpResponse response)
    {
        response.StatusCode = 200;
        response.ContentType = "text/html;charset=utf-8";

        using (var stream = _options.IndexStream())
        {
            // Inject arguments before writing to response
            var htmlBuilder = new StringBuilder(new StreamReader(stream).ReadToEnd());
            foreach (var entry in GetIndexArguments())
            {
                htmlBuilder.Replace(entry.Key, entry.Value);
            }

            await response.WriteAsync(htmlBuilder.ToString(), Encoding.UTF8);
        }
    }

    private IDictionary<string, string> GetIndexArguments()
    {
        return new Dictionary<string, string>()
        {
            //{ "%(DocumentTitle)", _options.DocumentTitle },
            //{ "%(HeadContent)", _options.HeadContent },
            //{ "%(ConfigObject)", JsonSerializer.Serialize(_options.ConfigObject, _jsonSerializerOptions) },
            //{ "%(OAuthConfigObject)", JsonSerializer.Serialize(_options.OAuthConfigObject, _jsonSerializerOptions) },
            //{ "%(Interceptors)", JsonSerializer.Serialize(_options.Interceptors) },
        };
    }

}
