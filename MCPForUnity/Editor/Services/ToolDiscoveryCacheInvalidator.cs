using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// On domain reload, invalidates tool discovery cache and registers
    /// all discovered tools with the local HTTP MCP server via /register-tools.
    /// Retries to handle server startup timing.
    /// </summary>
    [InitializeOnLoad]
    internal static class ToolDiscoveryCacheInvalidator
    {
        private const int MaxRetries = 5;
        private const int RetryDelayMs = 2000;

        static ToolDiscoveryCacheInvalidator()
        {
            EditorApplication.delayCall += () =>
            {
                MCPServiceLocator.ToolDiscovery.InvalidateCache();
                _ = System.Threading.Tasks.Task.Run(RegisterToolsWithRetry);
            };
        }

        private static async System.Threading.Tasks.Task RegisterToolsWithRetry()
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(RetryDelayMs);
                    RegisterTools();
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxRetries)
                        McpLog.Warn($"Failed to register tools after {MaxRetries} attempts: {ex.Message}");
                }
            }
        }

        private static void RegisterTools()
        {
            var discovery = MCPServiceLocator.ToolDiscovery;
            var allTools = discovery.DiscoverAllTools();

            string projectHash = UnityEditor.PlayerSettings.productGUID.ToString("N");

            var payload = new JObject
            {
                ["project_id"] = projectHash,
                ["tools"] = new JArray(allTools.Select(t =>
                    new JObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description ?? "",
                        ["structured_output"] = t.StructuredOutput,
                        ["requires_polling"] = t.RequiresPolling,
                        ["poll_action"] = t.PollAction ?? "status",
                        ["max_poll_seconds"] = t.MaxPollSeconds,
                        ["parameters"] = new JArray(
                            (t.Parameters ?? new List<ParameterMetadata>()).Select(p =>
                                new JObject
                                {
                                    ["name"] = char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1),
                                    ["description"] = p.Description ?? "",
                                    ["type"] = p.Type ?? "string",
                                    ["required"] = p.Required,
                                    ["default_value"] = p.DefaultValue
                                }))
                    }))
            };

            string json = payload.ToString(Formatting.None);
            string url = "http://127.0.0.1:6400/register-tools";

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 5000;

            byte[] data = Encoding.UTF8.GetBytes(json);
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
                stream.Write(data, 0, data.Length);

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string result = reader.ReadToEnd();
                McpLog.Info($"Registered {allTools.Count} tools with HTTP server: {result}");
            }
        }
    }
}
