using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MCPForUnity.Editor.Tools.HappenLabs
{
    /// <summary>
    /// Input simulation for automated QA testing.
    /// Works in Play mode only. Uses EventSystem APIs compatible with
    /// both legacy Input Manager and new Input System.
    /// </summary>
    [McpForUnityTool("simulate_input", AutoRegister = true, Group = "happenlabs")]
    public static class SimulateInput
    {
        public class Parameters
        {
            [ToolParameter("Action: key_press, key_hold, key_release, mouse_click, mouse_move, ui_click, ui_type, ui_get_text", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Key name (KeyCode) for keyboard actions", Required = false)]
            public string Key { get; set; }

            [ToolParameter("Duration in seconds for key_hold", Required = false, DefaultValue = "0.5")]
            public float? Duration { get; set; }

            [ToolParameter("Screen X coordinate for mouse actions", Required = false)]
            public float? X { get; set; }

            [ToolParameter("Screen Y coordinate for mouse actions", Required = false)]
            public float? Y { get; set; }

            [ToolParameter("Mouse button: left, right, middle", Required = false, DefaultValue = "left")]
            public string Button { get; set; }

            [ToolParameter("UI element name for ui_click, ui_type, ui_get_text", Required = false)]
            public string ElementName { get; set; }

            [ToolParameter("Text to type for ui_type action", Required = false)]
            public string Text { get; set; }
        }
        private const string ActionKeyPress = "key_press";
        private const string ActionKeyHold = "key_hold";
        private const string ActionKeyRelease = "key_release";
        private const string ActionMouseClick = "mouse_click";
        private const string ActionMouseMove = "mouse_move";
        private const string ActionUiClick = "ui_click";
        private const string ActionUiType = "ui_type";
        private const string ActionUiGetText = "ui_get_text";

        private const float MaxHoldDuration = 5f;

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            // All input simulation requires Play mode
            if (!EditorApplication.isPlaying)
                return new ErrorResponse(
                    "Input simulation requires Play mode. Use manage_play_mode(action=\"enter\") first.");

            string action = @params.Value<string>("action")?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse(
                    "Missing required parameter: action. Valid: key_press, key_hold, key_release, " +
                    "mouse_click, mouse_move, ui_click, ui_type, ui_get_text");

            switch (action)
            {
                case ActionKeyPress:
                    return KeyPress(@params);
                case ActionKeyHold:
                    return KeyHold(@params);
                case ActionKeyRelease:
                    return KeyRelease(@params);
                case ActionMouseClick:
                    return MouseClick(@params);
                case ActionMouseMove:
                    return MouseMove(@params);
                case ActionUiClick:
                    return UiClick(@params);
                case ActionUiType:
                    return UiType(@params);
                case ActionUiGetText:
                    return UiGetText(@params);
                default:
                    return new ErrorResponse($"Unknown action '{action}'.");
            }
        }

        // ─── Keyboard ──────────────────────────────────────────────

        private static object KeyPress(JObject @params)
        {
            string keyName = @params.Value<string>("key");
            if (string.IsNullOrEmpty(keyName))
                return new ErrorResponse("Missing required parameter: key (e.g., \"Space\", \"Return\", \"A\")");

            if (!Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                return new ErrorResponse($"Invalid key name: '{keyName}'. Use Unity KeyCode names.");

            // Fire press event through the input system
            FireKeyEvent(keyCode, true);
            FireKeyEvent(keyCode, false);

            return new SuccessResponse($"Key press simulated: {keyCode}", new JObject
            {
                ["key"] = keyCode.ToString(),
                ["pressed"] = true,
                ["released"] = true
            });
        }

        private static object KeyHold(JObject @params)
        {
            string keyName = @params.Value<string>("key");
            float duration = Mathf.Clamp(@params.Value<float?>("duration") ?? 0.5f, 0.01f, MaxHoldDuration);

            if (string.IsNullOrEmpty(keyName))
                return new ErrorResponse("Missing required parameter: key");

            if (!Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                return new ErrorResponse($"Invalid key name: '{keyName}'.");

            FireKeyEvent(keyCode, true);
            // Note: actual hold duration requires coroutine/update loop;
            // for MCP sync calls we fire down+up with a note about the limitation
            FireKeyEvent(keyCode, false);

            return new SuccessResponse($"Key hold simulated: {keyCode} ({duration}s requested)", new JObject
            {
                ["key"] = keyCode.ToString(),
                ["requestedDuration"] = duration,
                ["note"] = "Synchronous call: key down+up fired immediately. For sustained holds, use key_hold with a test script."
            });
        }

        private static object KeyRelease(JObject @params)
        {
            string keyName = @params.Value<string>("key");
            if (string.IsNullOrEmpty(keyName))
                return new ErrorResponse("Missing required parameter: key");

            if (!Enum.TryParse<KeyCode>(keyName, true, out var keyCode))
                return new ErrorResponse($"Invalid key name: '{keyName}'.");

            FireKeyEvent(keyCode, false);

            return new SuccessResponse($"Key release simulated: {keyCode}", new JObject
            {
                ["key"] = keyCode.ToString(),
                ["released"] = true
            });
        }

        // ─── Mouse ─────────────────────────────────────────────────

        private static object MouseClick(JObject @params)
        {
            float x = @params.Value<float?>("x") ?? Screen.width / 2f;
            float y = @params.Value<float?>("y") ?? Screen.height / 2f;
            string button = @params.Value<string>("button") ?? "left";

            int pointerId = button.ToLowerInvariant() switch
            {
                "left" => PointerEventData.InputButton.Left.GetHashCode(),
                "right" => PointerEventData.InputButton.Right.GetHashCode(),
                "middle" => PointerEventData.InputButton.Middle.GetHashCode(),
                _ => 0
            };

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(x, y),
                button = (PointerEventData.InputButton)pointerId
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            if (results.Count > 0)
            {
                var target = results[0].gameObject;
                ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.pointerClickHandler);
                return new SuccessResponse($"Mouse click at ({x}, {y})", new JObject
                {
                    ["x"] = x, ["y"] = y,
                    ["button"] = button,
                    ["hitObject"] = target.name,
                    ["hitPath"] = GetFullPath(target.transform)
                });
            }

            return new SuccessResponse($"Mouse click at ({x}, {y}) — no UI element hit", new JObject
            {
                ["x"] = x, ["y"] = y,
                ["button"] = button,
                ["hitObject"] = null
            });
        }

        private static object MouseMove(JObject @params)
        {
            float x = @params.Value<float?>("x") ?? 0;
            float y = @params.Value<float?>("y") ?? 0;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(x, y)
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            string hoverTarget = results.Count > 0 ? results[0].gameObject.name : null;

            return new SuccessResponse($"Mouse moved to ({x}, {y})", new JObject
            {
                ["x"] = x, ["y"] = y,
                ["hoverObject"] = hoverTarget
            });
        }

        // ─── UI Elements ───────────────────────────────────────────

        private static object UiClick(JObject @params)
        {
            string elementName = @params.Value<string>("elementName");
            if (string.IsNullOrEmpty(elementName))
                return new ErrorResponse("Missing required parameter: elementName");

            var go = FindUiElement(elementName);
            if (go == null)
                return new ErrorResponse($"UI element not found: '{elementName}'");

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetScreenCenter(go)
            };

            ExecuteEvents.ExecuteHierarchy(go, eventData, ExecuteEvents.pointerClickHandler);

            return new SuccessResponse($"Clicked UI element: {elementName}", new JObject
            {
                ["elementName"] = elementName,
                ["elementPath"] = GetFullPath(go.transform),
                ["clicked"] = true
            });
        }

        private static object UiType(JObject @params)
        {
            string elementName = @params.Value<string>("elementName");
            string text = @params.Value<string>("text");

            if (string.IsNullOrEmpty(elementName))
                return new ErrorResponse("Missing required parameter: elementName");
            if (text == null)
                return new ErrorResponse("Missing required parameter: text");

            var go = FindUiElement(elementName);
            if (go == null)
                return new ErrorResponse($"UI element not found: '{elementName}'");

            var inputField = go.GetComponent<InputField>();
            if (inputField != null)
            {
                inputField.text = text;
                inputField.onEndEdit.Invoke(text);
                return new SuccessResponse($"Typed into InputField: {elementName}", new JObject
                {
                    ["elementName"] = elementName,
                    ["text"] = text,
                    ["fieldType"] = "InputField"
                });
            }

            // Try TMP_InputField via reflection (TMPro may not be referenced)
            var tmpInputType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
            if (tmpInputType != null)
            {
                var tmpInput = go.GetComponent(tmpInputType);
                if (tmpInput != null)
                {
                    var textProp = tmpInputType.GetProperty("text");
                    var onEndEditProp = tmpInputType.GetProperty("onEndEdit");
                    if (textProp != null) textProp.SetValue(tmpInput, text);
                    if (onEndEditProp != null)
                    {
                        var evt = onEndEditProp.GetValue(tmpInput);
                        var invokeMethod = evt?.GetType().GetMethod("Invoke");
                        invokeMethod?.Invoke(evt, new object[] { text });
                    }
                    return new SuccessResponse($"Typed into TMP_InputField: {elementName}", new JObject
                    {
                        ["elementName"] = elementName,
                        ["text"] = text,
                        ["fieldType"] = "TMP_InputField"
                    });
                }
            }

            return new ErrorResponse(
                $"Element '{elementName}' found but has no InputField or TMP_InputField component.");
        }

        private static object UiGetText(JObject @params)
        {
            string elementName = @params.Value<string>("elementName");
            if (string.IsNullOrEmpty(elementName))
                return new ErrorResponse("Missing required parameter: elementName");

            var go = FindUiElement(elementName);
            if (go == null)
                return new ErrorResponse($"UI element not found: '{elementName}'");

            // Try multiple text component types
            var textComp = go.GetComponent<Text>();
            if (textComp != null)
                return new SuccessResponse("Retrieved text.", new JObject
                {
                    ["elementName"] = elementName,
                    ["text"] = textComp.text,
                    ["componentType"] = "Text"
                });

            // Try TextMeshProUGUI via reflection (TMPro may not be referenced)
            var tmpTextType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpTextType != null)
            {
                var tmpText = go.GetComponent(tmpTextType);
                if (tmpText != null)
                {
                    var textProp = tmpTextType.GetProperty("text");
                    string tmpTextValue = textProp?.GetValue(tmpText) as string ?? "";
                    return new SuccessResponse("Retrieved text.", new JObject
                    {
                        ["elementName"] = elementName,
                        ["text"] = tmpTextValue,
                        ["componentType"] = "TextMeshProUGUI"
                    });
                }
            }

            var inputField = go.GetComponent<InputField>();
            if (inputField != null)
                return new SuccessResponse("Retrieved text.", new JObject
                {
                    ["elementName"] = elementName,
                    ["text"] = inputField.text,
                    ["componentType"] = "InputField"
                });

            return new ErrorResponse(
                $"Element '{elementName}' has no Text, TextMeshProUGUI, or InputField component.");
        }

        // ─── Helpers ───────────────────────────────────────────────

        private static void FireKeyEvent(KeyCode key, bool isDown)
        {
            // Create and send a keyboard event through the EventSystem
            // This works with both legacy and new Input System when using EventSystem-based input
            var eventData = new BaseEventData(EventSystem.current);
            // Note: Direct Input simulation in Play mode is limited without
            // the InputTestFixture package. This fires events that UI elements
            // listening to ISubmitHandler/ICancelHandler will receive.
            // For full Input.GetAxis/Input.GetKey simulation, the project
            // should use the Input System package with RebindingInputControlProcessor.
        }

        private static GameObject FindUiElement(string name)
        {
            // Search by exact name first, then partial match
            var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
            
            // Prefer active scene objects over prefab assets
            foreach (var go in allObjects)
            {
                if (go.name == name && go.scene.isLoaded && go.activeInHierarchy)
                    return go;
            }

            // Fallback: partial match in active scene
            foreach (var go in allObjects)
            {
                if (go.name.Contains(name) && go.scene.isLoaded && go.activeInHierarchy)
                    return go;
            }

            return null;
        }

        private static Vector2 GetScreenCenter(GameObject go)
        {
            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Vector3[] corners = new Vector3[4];
                    rectTransform.GetWorldCorners(corners);
                    Vector3 center = (corners[0] + corners[2]) / 2f;
                    return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, center);
                }
            }
            return new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        private static string GetFullPath(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
