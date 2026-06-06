using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Ensures ToolDiscoveryService cache is invalidated on every domain reload
    /// and re-registers tools with the HTTP server so new/changed tools appear.
    /// </summary>
    [InitializeOnLoad]
    internal static class ToolDiscoveryCacheInvalidator
    {
        static ToolDiscoveryCacheInvalidator()
        {
            EditorApplication.delayCall += () =>
            {
                MCPServiceLocator.ToolDiscovery.InvalidateCache();

                // Re-register tools with HTTP server if connected
                try
                {
                    var transportManager = MCPServiceLocator.TransportManager;
                    var client = transportManager.GetClient(TransportMode.Http);
                    if (client != null && client.IsConnected)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await client.ReregisterToolsAsync().ConfigureAwait(false);
                                McpLog.Info("Re-registered tools with HTTP server after domain reload");
                            }
                            catch (Exception ex)
                            {
                                McpLog.Warn($"Failed to reregister tools after domain reload: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"ToolDiscoveryCacheInvalidator: {ex.Message}");
                }
            };
        }
    }
}
