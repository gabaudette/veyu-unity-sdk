using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/*
 * Copyright (c) 2025, Veyu Playtest Software.
 * All rights reserved.
 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Veyu
{
    /// <summary>
    /// Veyu SDK for logging user interactions and system events in Unity games for them 
    /// to be read by Veyu Playtest services.
    /// Logs are stored locally in JSON Lines format or/and can be uploaded to a cloud service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usage:
    /// </para>
    /// <para>
    /// 1. Initialize the SDK in your main scene:
    ///   VeyuSdk.Init("optional_session_id");
    /// </para>
    /// <para>
    /// 2. Log events using:
    ///   VeyuSdk.LogEvent("event_name", event_data);
    /// </para>
    /// <para>
    /// 3. Save the session (locally):
    ///   await VeyuSdk.Save();
    /// <para>or</para> Upload the session (cloud): 
    ///  await VeyuSdk.Upload();
    /// </para>
    /// 4. Ensure to call Save or Upload before application exit to avoid data loss.
    /// </remarks>
    public static class VeyuSdk
    {
        private static readonly List<LogEntry> _buffer = new();

        private static string _sessionFilePath;

        private static bool _initialized = false;

        private static float _flushInterval = 5f;

        private class VeyuRunner : MonoBehaviour
        {
            private float _lastFlushTime = 0f;

            private void Update()
            {
                if (Time.realtimeSinceStartup - _lastFlushTime > _flushInterval)
                {
                    _ = Flush();
                    _lastFlushTime = Time.realtimeSinceStartup;
                }
            }

            private void OnApplicationQuit()
            {
                _ = Flush();
            }
        }

        [Serializable]
        private class LogEntry
        {
            public string type;
            public string name;
            public string timestamp;
            public object meta;
        }

        /// <summary>
        /// Initializes the Veyu SDK.
        /// If sessionId is not provided, a unique one will be generated.
        /// Flush interval controls how often logs are written to disk. (default 5 seconds)
        /// </summary>
        /// <param name="sessionId">
        /// Optional session ID. If not provided, a unique one will be generated.
        /// </param>
        /// <param name="flushInterval">
        /// Flush interval controls how often logs are written to disk. (default 5 seconds)
        /// </param>
        public static void Init(string sessionId = null, float flushInterval = 5f)
        {
            if (_initialized)
            {
                return;
            }

            _flushInterval = flushInterval;

            string logFilesDirectory = Path.Combine(Application.persistentDataPath, "veyu-logs");
            if (!Directory.Exists(logFilesDirectory))
            {
                Directory.CreateDirectory(logFilesDirectory);
            }

            sessionId ??= $"veyu_{Guid.NewGuid()}_{DateTime.Now:yyyyMMdd_HHmmss}";
            _sessionFilePath = Path.Combine(logFilesDirectory, $"veyu_session_{sessionId}.jsonl");

            var runner = new GameObject("VeyuRunner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.hideFlags = HideFlags.HideAndDontSave;
            runner.AddComponent<VeyuRunner>();

            _initialized = true;

            LogSystem("session_start", new Dictionary<string, object>
            {
                {"build_version", Application.version},
                {"platform", Application.platform.ToString()},
                {"timestamp", DateTime.UtcNow.ToString("o")}
            });
        }

        /// <summary>
        /// Logs an event with optional metadata.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="meta"></param>
        public static void LogEvent(string name, object meta = null) => Log("event", name, meta);

        /// <summary>
        /// Logs a user input event with optional metadata.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="meta"></param>
        public static void LogInput(string name, object meta = null) => Log("input", name, meta);

        /// <summary>
        /// Log a system event with optional metadata.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="meta"></param>
        public static void LogSystem(string name, object meta = null) => Log("system", name, meta);

        /// <summary>
        /// Saves the current session logs to locally.
        /// </summary>
        public static async Task Save()
        {
            await EndSession();
            Debug.Log($"Veyu: Session saved locally at {_sessionFilePath}");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// This function is not yet implemented. DO NOT USE
        /// </remarks>
        public static async Task Upload()
        {
            await EndSession();
            Debug.Log($"Veyu: Uploading session to cloud (not implemented) from {_sessionFilePath}");
            await Task.Delay(500);
            Debug.Log("Veyu: Upload complete (placeholder)");
        }

        private static async Task EndSession()
        {
            LogSystem("session_end", new { timestamp = DateTime.UtcNow.ToString("o") });
            await Flush();
        }

        private static void Log(string type, string name, object meta)
        {
            if (!_initialized)
            {
                Debug.LogError("VeyuSdk is not initialized. Call VeyuSdk.Init() before logging events.");
                return;
            }

            var logEntry = new LogEntry
            {
                type = type,
                name = name,
                timestamp = DateTime.UtcNow.ToString("o"),
                meta = meta ?? new Dictionary<string, object>()
            };

            _buffer.Add(logEntry);
        }

        private static async Task Flush()
        {
            if (_buffer.Count == 0 || string.IsNullOrEmpty(_sessionFilePath))
            {
                return;
            }

            try
            {
                using (var sw = new StreamWriter(_sessionFilePath, append: true))
                {
                    foreach (var entry in _buffer)
                    {
                        string json = JsonUtility.ToJson(entry, false);
                        await sw.WriteLineAsync(json);
                    }
                }

                _buffer.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Veyu: Failed to flush logs. Exception: {ex}");
                _buffer.Clear();
            }
        }
    }
}
