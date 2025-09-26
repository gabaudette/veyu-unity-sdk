using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Veyu
{
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

        public static void LogEvent(string name, object meta = null) => Log("event", name, meta);
        public static void LogInput(string name, object meta = null) => Log("input", name, meta);
        public static void LogSystem(string name, object meta = null) => Log("system", name, meta);

        public static async Task Save()
        {
            await EndSession();
            Debug.Log($"Veyu: Session saved locally at {_sessionFilePath}");
        }

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
