using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPForUnity.Editor.Tools.HappenLabs
{
    /// <summary>
    /// Play mode control for automated QA.
    /// Provides structured enter/exit/pause/resume with async readiness polling,
    /// replacing the unsafe execute_code workaround.
    /// </summary>
    [McpForUnityTool("manage_play_mode", AutoRegister = false, Group = "happenlabs")]
    public static class ManagePlayMode
    {
        private const string ActionEnter = "enter";
        private const string ActionExit = "exit";
        private const string ActionPause = "pause";
        private const string ActionResume = "resume";
        private const string ActionGetState = "get_state";

        private const float DefaultTimeoutSeconds = 30f;
        private const float PollIntervalMs = 100f;

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            string action = @params.Value<string>("action")?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("Missing required parameter: action. Valid: enter, exit, pause, resume, get_state");

            switch (action)
            {
                case ActionGetState:
                    return GetState();
                case ActionEnter:
                    return EnterPlayMode(@params);
                case ActionExit:
                    return ExitPlayMode(@params);
                case ActionPause:
                    return PausePlayMode();
                case ActionResume:
                    return ResumePlayMode();
                default:
                    return new ErrorResponse($"Unknown action '{action}'. Valid: enter, exit, pause, resume, get_state");
            }
        }

        private static object GetState()
        {
            var state = BuildStateObject();
            return new SuccessResponse("Retrieved play mode state.", state);
        }

        private static object EnterPlayMode(JObject @params)
        {
            if (EditorApplication.isPlaying)
                return new SuccessResponse("Already in Play mode.", BuildStateObject());

            if (EditorApplication.isCompiling)
                return new ErrorResponse("Cannot enter Play mode while compiling. Wait for compilation to finish.");

            float timeout = @params.Value<float?>("timeout") ?? DefaultTimeoutSeconds;

            EditorApplication.isPlaying = true;

            // Poll until ready or timeout
            double startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                if (EditorApplication.timeSinceStartup - startTime > timeout)
                {
                    var partialState = BuildStateObject();
                    return new ErrorResponse(
                        $"Timed out after {timeout}s waiting for Play mode to stabilize. " +
                        $"isPlaying={EditorApplication.isPlaying}, isCompiling={EditorApplication.isCompiling}",
                        partialState);
                }

                // Brief wait — in real usage this runs on the main thread via EditorApplication.update
                // For MCP synchronous calls, we rely on the Editor processing the transition
                System.Threading.Thread.Sleep((int)PollIntervalMs);
                EditorApplication.Step();
            }

            var finalState = BuildStateObject();
            return new SuccessResponse("Entered Play mode successfully.", finalState);
        }

        private static object ExitPlayMode(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                return new SuccessResponse("Already out of Play mode.", BuildStateObject());

            float timeout = @params.Value<float?>("timeout") ?? DefaultTimeoutSeconds;

            EditorApplication.isPlaying = false;

            double startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - startTime > timeout)
                {
                    return new ErrorResponse(
                        $"Timed out after {timeout}s waiting for Play mode to exit.",
                        BuildStateObject());
                }

                System.Threading.Thread.Sleep((int)PollIntervalMs);
                EditorApplication.Step();
            }

            return new SuccessResponse("Exited Play mode successfully.", BuildStateObject());
        }

        private static object PausePlayMode()
        {
            if (!EditorApplication.isPlaying)
                return new ErrorResponse("Cannot pause: not in Play mode.");

            EditorApplication.isPaused = !EditorApplication.isPaused;
            return new SuccessResponse(
                EditorApplication.isPaused ? "Paused." : "Resumed.",
                BuildStateObject());
        }

        private static object ResumePlayMode()
        {
            if (!EditorApplication.isPlaying)
                return new ErrorResponse("Cannot resume: not in Play mode.");

            EditorApplication.isPaused = false;
            return new SuccessResponse("Resumed Play mode.", BuildStateObject());
        }

        private static JObject BuildStateObject()
        {
            var scenes = new JArray();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                scenes.Add(new JObject
                {
                    ["name"] = s.name,
                    ["path"] = s.path,
                    ["isLoaded"] = s.isLoaded,
                    ["rootCount"] = s.rootCount
                });
            }

            return new JObject
            {
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["isCompiling"] = EditorApplication.isCompiling,
                ["timeScale"] = Time.timeScale,
                ["loadedSceneCount"] = SceneManager.sceneCount,
                ["activeScene"] = SceneManager.GetActiveScene().name,
                ["scenes"] = scenes
            };
        }
    }
}
