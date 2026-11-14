using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.QBittorrent;

/// <summary>
/// Intercepts Jellyfin's web client index.html and injects a sidebar script tag so that
/// the qBittorrent Manager page appears in Jellyfin 12's avatar/profile dropdown menu.
/// </summary>
public sealed class SidebarScriptStartupFilter : IStartupFilter
{
    // Substring checked to avoid injecting twice (e.g. when File Transformation already added it).
    private const string ScriptMarker = "api/qbittorrent/sidebar.js";
    private const string ScriptTag = "<script src=\"/api/qbittorrent/sidebar.js\" defer></script>";
    private const long MaxPageBytes = 1024 * 1024;

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.Use(async (context, nextHandler) =>
            {
                if (!IsIndexRequest(context.Request))
                {
                    await nextHandler(context);
                    return;
                }

                // Strip headers that could produce a compressed, partial, or 304 response.
                context.Request.Headers.Remove("Accept-Encoding");
                context.Request.Headers.Remove("Range");
                context.Request.Headers.Remove("If-Range");
                context.Request.Headers.Remove("If-None-Match");
                context.Request.Headers.Remove("If-Modified-Since");

                var originalBody = context.Response.Body;
                using var buffer = new MemoryStream();
                context.Response.Body = buffer;

                try
                {
                    await nextHandler(context);
                }
                finally
                {
                    context.Response.Body = originalBody;
                }

                buffer.Seek(0, SeekOrigin.Begin);

                var isHtml = context.Response.StatusCode == StatusCodes.Status200OK
                    && (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false);

                if (!isHtml || buffer.Length > MaxPageBytes || context.Response.Headers.ContainsKey("Content-Encoding"))
                {
                    await buffer.CopyToAsync(originalBody);
                    return;
                }

                string html;
                using (var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    html = await reader.ReadToEndAsync();

                if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase)
                    && !html.Contains(ScriptMarker, StringComparison.Ordinal))
                {
                    html = html.Replace("</head>", ScriptTag + "\n</head>", StringComparison.OrdinalIgnoreCase);
                }

                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength = bytes.Length;
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.Remove("Last-Modified");
                context.Response.Headers.Remove("Accept-Ranges");
                await originalBody.WriteAsync(bytes);
            });

            next(builder);
        };
    }

    private static bool IsIndexRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) && request.Path.Value is { } path &&
        (string.Equals(path, "/web/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/web/index.html", StringComparison.OrdinalIgnoreCase));
}
