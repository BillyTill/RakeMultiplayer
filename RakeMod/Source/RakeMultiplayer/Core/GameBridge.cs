using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using RakeMultiplayer.UI;

namespace RakeMultiplayer.Core
{
    /// <summary>
    /// Local-only control bridge for development and the game MCP.
    /// Listens on 127.0.0.1:8765 and exposes the live uGUI tree, screenshots,
    /// button clicks and menu navigation. Disabled unless -rakemcp is passed or
    /// Diagnostics/GameBridge is enabled in the BepInEx config.
    /// </summary>
    public class GameBridge : MonoBehaviour
    {
        public int Port = 8765;

        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;
        private readonly Queue<Action> _mainQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        private string _shotPath;

        private void Start()
        {
            _shotPath = Path.Combine(Application.dataPath, "..\\bridge_screenshot.png");
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _running = true;
                _thread = new Thread(ListenLoop);
                _thread.IsBackground = true;
                _thread.Name = "RakeGameBridge";
                _thread.Start();
                RakeMultiplayerPlugin.Log?.LogInfo("[Bridge] listening on http://127.0.0.1:" + Port + "/");
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError("[Bridge] failed to start: " + ex.Message);
            }
        }

        private void Update()
        {
            while (true)
            {
                Action action = null;
                lock (_queueLock)
                {
                    if (_mainQueue.Count == 0) break;
                    action = _mainQueue.Dequeue();
                }
                try { action(); }
                catch (Exception ex) { RakeMultiplayerPlugin.Log?.LogError("[Bridge] command: " + ex); }
            }
        }

        private void OnDestroy()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
            try { if (_thread != null) _thread.Join(500); } catch { }
        }

        // ---------------------------------------------------------------- server

        private void ListenLoop()
        {
            while (_running)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch
                {
                    if (!_running) break;
                    continue;
                }

                try { HandleClient(client); }
                catch (Exception ex) { RakeMultiplayerPlugin.Log?.LogWarning("[Bridge] client: " + ex.Message); }
                finally { try { client.Close(); } catch { } }
            }
        }

        private void HandleClient(TcpClient client)
        {
            client.ReceiveTimeout = 10000;
            client.SendTimeout = 10000;

            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII);
            string requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine)) return;

            string[] parts = requestLine.Split(' ');
            string method = parts.Length > 0 ? parts[0] : "GET";
            string target = parts.Length > 1 ? parts[1] : "/";

            int contentLength = 0;
            string header;
            while (!string.IsNullOrEmpty(header = reader.ReadLine()))
            {
                if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(header.Substring(15).Trim(), out contentLength);
                }
            }

            string body = string.Empty;
            if (contentLength > 0)
            {
                char[] buffer = new char[contentLength];
                int read = 0;
                while (read < contentLength)
                {
                    int n = reader.Read(buffer, read, contentLength - read);
                    if (n <= 0) break;
                    read += n;
                }
                body = new string(buffer, 0, read);
            }

            if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                SendText(client, 204, "text/plain", string.Empty);
                return;
            }

            string query = string.Empty;
            int q = target.IndexOf('?');
            if (q >= 0)
            {
                query = target.Substring(q + 1);
                target = target.Substring(0, q);
            }
            var args = ParseQuery(query);

            try
            {
                if (target == "/" || target == "/state") { SendJson(client, StateJson()); return; }
                if (target == "/ui") { SendJson(client, UiJson()); return; }
                if (target == "/screenshot")
                {
                    byte[] png = CapturePng();
                    if (png == null) { SendJson(client, "{\"ok\":false,\"error\":\"capture failed\"}"); return; }
                    SendBytes(client, 200, "image/png", png);
                    return;
                }
                if (target == "/click")
                {
                    string name = Get(args, "name");
                    SendJson(client, RunOnMain(delegate { return ClickByName(name); }));
                    return;
                }
                if (target == "/menu")
                {
                    string screen = Get(args, "screen");
                    if (string.IsNullOrEmpty(screen)) screen = Get(args, "action");
                    SendJson(client, RunOnMain(delegate { return ShowScreen(screen); }));
                    return;
                }
                if (target == "/input")
                {
                    string name = Get(args, "name");
                    string value = Get(args, "value");
                    SendJson(client, RunOnMain(delegate { return SetInput(name, value); }));
                    return;
                }
                if (target == "/canvases")
                {
                    SendJson(client, CanvasesJson());
                    return;
                }
                if (target == "/loading")
                {
                    string hide = Get(args, "hide");
                    if (hide == "1")
                    {
                        SendJson(client, RunOnMain(delegate { LoadingScreen.Hide(); return "{\"ok\":true}"; }));
                        return;
                    }
                    string title = Get(args, "title");
                    string subtitle = Get(args, "subtitle");
                    string status = Get(args, "status");
                    SendJson(client, RunOnMain(delegate
                    {
                        LoadingScreen.Show(title, subtitle, status, false, false);
                        return "{\"ok\":true}";
                    }));
                    return;
                }
                if (target == "/close")
                {
                    SendJson(client, RunOnMain(delegate
                    {
                        var menu = MultiplayerMenu.Instance;
                        if (menu != null && MultiplayerMenu.IsOpen) menu.ToggleMenu();
                        return "{\"ok\":true}";
                    }));
                    return;
                }

                SendJson(client, "{\"ok\":false,\"error\":\"unknown endpoint\"}");
            }
            catch (Exception ex)
            {
                SendJson(client, "{\"ok\":false,\"error\":\"" + Esc(ex.Message) + "\"}");
            }
        }

        // ---------------------------------------------------------------- commands

        private string StateJson()
        {
            return RunOnMain(delegate
            {
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"scene\":\"").Append(Esc(Application.loadedLevelName)).Append("\",");
                sb.Append("\"level\":").Append(Application.loadedLevel).Append(",");
                sb.Append("\"menuOpen\":").Append(MultiplayerMenu.IsOpen ? "true" : "false").Append(",");
                sb.Append("\"menuVisible\":").Append(MultiplayerMenu.Instance != null && MultiplayerMenu.Instance.IsMenuVisible ? "true" : "false").Append(",");
                sb.Append("\"panel\":\"").Append(Esc(ActivePanel())).Append("\",");
                sb.Append("\"screen\":\"").Append(Screen.width).Append("x").Append(Screen.height).Append("\",");
                sb.Append("\"fps\":").Append(Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime))).Append(",");
                sb.Append("\"timeScale\":").Append(Time.timeScale.ToString("0.##", CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"server\":").Append(RakeMultiplayerPlugin.isServer ? "true" : "false").Append(",");
                sb.Append("\"connected\":").Append(RakeMultiplayerPlugin.isConnected ? "true" : "false").Append(",");
                sb.Append("\"quality\":").Append(QualitySettings.GetQualityLevel()).Append(",");
                sb.Append("\"vsync\":").Append(QualitySettings.vSyncCount).Append(",");
                sb.Append("\"aa\":").Append(QualitySettings.antiAliasing).Append(",");
                sb.Append("\"fullscreen\":").Append(Screen.fullScreen ? "true" : "false");
                sb.Append("}");
                return sb.ToString();
            });
        }

        private string UiJson()
        {
            return RunOnMain(delegate
            {
                var builder = RuntimeUIBuilder.Instance;
                if (builder == null || builder.Canvas == null) return "{\"ok\":false,\"error\":\"no canvas\"}";

                var sb = new StringBuilder();
                int count = 0;
                sb.Append("{\"ok\":true,\"nodes\":[");
                AppendNode(builder.Canvas.transform, sb, 0, ref count);
                sb.Append("]}");
                return sb.ToString();
            });
        }

        private void AppendNode(Transform t, StringBuilder sb, int depth, ref int count)
        {
            if (t == null || depth > 8 || count > 400) return;

            var rt = t as RectTransform;
            bool active = t.gameObject.activeSelf;
            var text = t.GetComponent<Text>();
            var button = t.GetComponent<Button>();
            var input = t.GetComponent<InputField>();

            if (count > 0) sb.Append(",");
            count++;

            sb.Append("{\"name\":\"").Append(Esc(t.name)).Append("\",");
            sb.Append("\"active\":").Append(active ? "true" : "false");
            if (rt != null)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.position);
                float scale = 1f;
                var canvas = t.GetComponentInParent<Canvas>();
                if (canvas != null) scale = canvas.scaleFactor;
                sb.Append(",\"x\":").Append(screen.x.ToString("0", CultureInfo.InvariantCulture));
                sb.Append(",\"y\":").Append(screen.y.ToString("0", CultureInfo.InvariantCulture));
                sb.Append(",\"w\":").Append((rt.rect.width * scale).ToString("0", CultureInfo.InvariantCulture));
                sb.Append(",\"h\":").Append((rt.rect.height * scale).ToString("0", CultureInfo.InvariantCulture));
            }
            if (text != null) sb.Append(",\"text\":\"").Append(Esc(text.text)).Append("\"");
            if (button != null)
            {
                sb.Append(",\"button\":true");
                sb.Append(",\"interactable\":").Append(button.interactable ? "true" : "false");
            }
            if (input != null) sb.Append(",\"input\":true");
            sb.Append("}");

            for (int i = 0; i < t.childCount; i++)
            {
                AppendNode(t.GetChild(i), sb, depth + 1, ref count);
            }
        }

        private string ClickByName(string name)
        {
            var builder = RuntimeUIBuilder.Instance;
            if (builder == null || builder.Canvas == null) return "{\"ok\":false,\"error\":\"no canvas\"}";
            if (string.IsNullOrEmpty(name)) name = GetArgFromQueryFallback("name");

            var buttons = builder.Canvas.GetComponentsInChildren<Button>(true);
            var names = new StringBuilder();
            int listed = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (b == null) continue;
                bool match = b.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase);
                if (!match)
                {
                    var label = b.GetComponentInChildren<Text>();
                    match = label != null && label.text != null &&
                            label.text.Equals(name, StringComparison.OrdinalIgnoreCase);
                }
                if (match)
                {
                    b.onClick.Invoke();
                    return "{\"ok\":true,\"clicked\":\"" + Esc(b.gameObject.name) + "\"}";
                }
                if (listed < 40)
                {
                    if (listed++ > 0) names.Append(",");
                    names.Append("\"").Append(Esc(b.gameObject.name)).Append("\"");
                }
            }
            return "{\"ok\":false,\"error\":\"button not found\",\"buttons\":[" + names + "]}";
        }

        private string SetInput(string name, string value)
        {
            var builder = RuntimeUIBuilder.Instance;
            if (builder == null || builder.Canvas == null) return "{\"ok\":false,\"error\":\"no canvas\"}";

            var inputs = builder.Canvas.GetComponentsInChildren<InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                var field = inputs[i];
                if (field == null) continue;
                if (!field.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                field.text = value;
                field.onEndEdit.Invoke(value);
                return "{\"ok\":true,\"input\":\"" + Esc(name) + "\"}";
            }
            return "{\"ok\":false,\"error\":\"input not found\"}";
        }

        private string ShowScreen(string screen)
        {
            var menu = MultiplayerMenu.Instance;
            if (menu == null) return "{\"ok\":false,\"error\":\"menu not found\"}";

            if (string.Equals(screen, "close", StringComparison.OrdinalIgnoreCase))
            {
                if (MultiplayerMenu.IsOpen) menu.ToggleMenu();
                return "{\"ok\":true,\"screen\":\"closed\"}";
            }

            menu.BridgeShow(screen);
            return "{\"ok\":true,\"screen\":\"" + Esc(screen) + "\",\"panel\":\"" + Esc(ActivePanel()) + "\"}";
        }

        private string CanvasesJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"ok\":true,\"canvases\":[");
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null) continue;
                if (i > 0) sb.Append(",");
                sb.Append("{\"name\":\"").Append(Esc(c.gameObject.name)).Append("\",");
                sb.Append("\"order\":").Append(c.sortingOrder).Append(",");
                sb.Append("\"mode\":\"").Append(c.renderMode.ToString()).Append("\",");
                sb.Append("\"active\":").Append(c.gameObject.activeInHierarchy ? "true" : "false").Append(",");
                sb.Append("\"layer\":").Append(c.sortingLayerName == null ? "" : Esc(c.sortingLayerName));
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private string ActivePanel()
        {
            var builder = RuntimeUIBuilder.Instance;
            if (builder == null || builder.Canvas == null) return string.Empty;

            var container = builder.Canvas.transform.Find("MainContainer");
            if (container == null) return string.Empty;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.gameObject.activeSelf && child.name.EndsWith("Panel", StringComparison.OrdinalIgnoreCase))
                    return child.name;
            }
            return string.Empty;
        }

        // ---------------------------------------------------------------- helpers

        private string RunOnMain(Func<string> action)
        {
            string result = null;
            Exception error = null;
            var done = new ManualResetEvent(false);

            lock (_queueLock)
            {
                _mainQueue.Enqueue(delegate
                {
                    try { result = action(); }
                    catch (Exception ex) { error = ex; }
                    finally { done.Set(); }
                });
            }

            if (!done.WaitOne(8000))
                return "{\"ok\":false,\"error\":\"main thread timeout\"}";

            if (error != null)
                return "{\"ok\":false,\"error\":\"" + Esc(error.Message) + "\"}";

            return result ?? "{\"ok\":true}";
        }

        private byte[] CapturePng()
        {
            byte[] bytes = null;
            var done = new ManualResetEvent(false);

            lock (_queueLock)
            {
                _mainQueue.Enqueue(delegate { StartCoroutine(CaptureRoutine(delegate(byte[] b) { bytes = b; done.Set(); })); });
            }

            done.WaitOne(12000);
            return bytes;
        }

        private IEnumerator CaptureRoutine(Action<byte[]> done)
        {
            try { if (File.Exists(_shotPath)) File.Delete(_shotPath); } catch { }

            // Let panel fade/settle animations finish before the capture.
            yield return new WaitForSeconds(0.35f);
            yield return new WaitForEndOfFrame();
            Application.CaptureScreenshot(_shotPath);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (!File.Exists(_shotPath)) continue;
                try
                {
                    byte[] data = File.ReadAllBytes(_shotPath);
                    try { File.Delete(_shotPath); } catch { }
                    done(data);
                    yield break;
                }
                catch { }
            }
            done(null);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return result;

            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                if (string.IsNullOrEmpty(pairs[i])) continue;
                int eq = pairs[i].IndexOf('=');
                string key = eq >= 0 ? pairs[i].Substring(0, eq) : pairs[i];
                string value = eq >= 0 ? pairs[i].Substring(eq + 1) : string.Empty;
                result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value.Replace("+", " "));
            }
            return result;
        }

        private static string Get(Dictionary<string, string> args, string key)
        {
            string value;
            return args.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static string GetArgFromQueryFallback(string key)
        {
            return string.Empty;
        }

        private static void SendJson(TcpClient client, string json)
        {
            SendBytes(client, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        }

        private static void SendText(TcpClient client, int code, string type, string text)
        {
            SendBytes(client, code, type, Encoding.UTF8.GetBytes(text));
        }

        private static void SendBytes(TcpClient client, int code, string type, byte[] body)
        {
            try
            {
                var stream = client.GetStream();
                string reason = code == 200 ? "OK" : (code == 204 ? "No Content" : "OK");
                string header = "HTTP/1.1 " + code + " " + reason + "\r\n" +
                                "Content-Type: " + type + "\r\n" +
                                "Content-Length: " + body.Length + "\r\n" +
                                "Cache-Control: no-store\r\n" +
                                "Access-Control-Allow-Origin: *\r\n" +
                                "Connection: close\r\n\r\n";
                byte[] h = Encoding.ASCII.GetBytes(header);
                stream.Write(h, 0, h.Length);
                if (body.Length > 0) stream.Write(body, 0, body.Length);
                stream.Flush();
            }
            catch { }
        }

        private static string Esc(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }
}
