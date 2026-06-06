using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Ensures ToolDiscoveryService cache is invalidated on every domain reload.
    /// Without this, new tools added via package updates are invisible until
    /// the user manually toggles a tool group or restarts the Editor.
    /// </summary>
    [InitializeOnLoad]
    internal static class ToolDiscoveryCacheInvalidator
    {
        static ToolDiscoveryCacheInvalidator()
        {
            // Defer to after serialization so services are available
            EditorApplication.delayCall += () =>
            {
                MCPServiceLocator.ToolDiscovery.InvalidateCache();
            };
        }
    }
}
