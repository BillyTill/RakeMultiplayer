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
                if (target == "/camera")
                {
                    string view = Get(args, "view");
                    string pitch = Get(args, "pitch");
                    string look = Get(args, "look");
                    SendJson(client, RunOnMain(delegate { return RakePlayerController.BridgeCamera(view, pitch, look); }));
                    return;
                }
                if (target == "/tp")
                {
                    float tx, ty, tz;
                    float.TryParse(Get(args, "x"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out tx);
                    float.TryParse(Get(args, "z"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out tz);
                    if (!float.TryParse(Get(args, "y"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out ty)) ty = 0.5f;
                    SendJson(client, RunOnMain(delegate { return RakePlayerController.BridgeTeleport(tx, ty, tz); }));
                    return;
                }
                if (target == "/probe")
                {
                    float pyaw, pheight;
                    float.TryParse(Get(args, "yaw"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out pyaw);
                    if (!float.TryParse(Get(args, "height"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out pheight)) pheight = 1.25f;
                    SendJson(client, RunOnMain(delegate { return RakePlayerController.BridgeProbe(pyaw, pheight); }));
                    return;
                }
                if (target == "/gfx")
                {
                    string shadows = Get(args, "shadows");
                    string shadowDist = Get(args, "shadowdist");
                    string far = Get(args, "far");
                    string lod = Get(args, "lod");
                    string maxLod = Get(args, "maxlod");
                    string aa = Get(args, "aa");
                    string texLimit = Get(args, "texlimit");
                    string pixelLights = Get(args, "pixellights");
                    string trees = Get(args, "trees");
                    string preset = Get(args, "preset");
                    string fog = Get(args, "fog");
                    string softVeg = Get(args, "softveg");

                    SendJson(client, RunOnMain(delegate
                    {
                        float f;
                        int i;

                        if (!string.IsNullOrEmpty(shadows))
                        {
                            int.TryParse(shadows, out i);
                            LightShadows quality = i <= 0 ? LightShadows.None
                                : (i == 1 ? LightShadows.Hard : LightShadows.Soft);
                            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
                            for (int l = 0; l < lights.Length; l++)
                            {
                                if (lights[l] == null || lights[l].type != LightType.Directional) continue;
                                lights[l].shadows = quality;
                            }
                        }

                        if (!string.IsNullOrEmpty(shadowDist) &&
                            float.TryParse(shadowDist, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out f))
                        {
                            QualitySettings.shadowDistance = f;
                        }

                        if (!string.IsNullOrEmpty(far) &&
                            float.TryParse(far, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out f))
                        {
                            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                            for (int c = 0; c < cameras.Length; c++)
                            {
                                if (cameras[c] != null && cameras[c].enabled) cameras[c].farClipPlane = f;
                            }
                        }

                        if (!string.IsNullOrEmpty(lod) &&
                            float.TryParse(lod, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out f))
                        {
                            QualitySettings.lodBias = f;
                        }

                        if (!string.IsNullOrEmpty(maxLod))
                        {
                            int.TryParse(maxLod, out i);
                            QualitySettings.maximumLODLevel = i;
                        }

                        if (!string.IsNullOrEmpty(aa))
                        {
                            int.TryParse(aa, out i);
                            QualitySettings.antiAliasing = i;
                        }

                        if (!string.IsNullOrEmpty(texLimit))
                        {
                            int.TryParse(texLimit, out i);
                            QualitySettings.masterTextureLimit = i;
                        }

                        if (!string.IsNullOrEmpty(pixelLights))
                        {
                            int.TryParse(pixelLights, out i);
                            QualitySettings.pixelLightCount = i;
                        }

                        if (!string.IsNullOrEmpty(fog))
                        {
                            int.TryParse(fog, out i);
                            RenderSettings.fog = i != 0;
                        }

                        if (!string.IsNullOrEmpty(softVeg))
                        {
                            int.TryParse(softVeg, out i);
                            QualitySettings.softVegetation = i != 0;
                        }

                        if (!string.IsNullOrEmpty(trees))
                        {
                            bool draw = trees != "0";
                            Terrain[] terrains = Terrain.activeTerrains;
                            for (int t2 = 0; t2 < terrains.Length; t2++)
                            {
                                if (terrains[t2] != null) terrains[t2].drawTreesAndFoliage = draw;
                            }
                        }

                        if (!string.IsNullOrEmpty(preset))
                        {
                            int index = GraphicsProfiles.IndexOf(preset);
                            GraphicsProfiles.Select(index);
                        }

                        Camera main = Camera.main;
                        var sb = new System.Text.StringBuilder("{\"ok\":true");
                        sb.Append(",\"far\":").Append(main != null ? main.farClipPlane.ToString("0.#",
                            System.Globalization.CultureInfo.InvariantCulture) : "-");
                        sb.Append(",\"lodBias\":").Append(QualitySettings.lodBias.ToString("0.##",
                            System.Globalization.CultureInfo.InvariantCulture));
                        sb.Append(",\"maxLod\":").Append(QualitySettings.maximumLODLevel);
                        sb.Append(",\"aa\":").Append(QualitySettings.antiAliasing);
                        sb.Append(",\"texLimit\":").Append(QualitySettings.masterTextureLimit);
                        sb.Append(",\"pixelLights\":").Append(QualitySettings.pixelLightCount);
                        sb.Append(",\"shadowDistance\":").Append(QualitySettings.shadowDistance.ToString("0.#",
                            System.Globalization.CultureInfo.InvariantCulture));
                        sb.Append(",\"fog\":").Append(RenderSettings.fog ? "true" : "false");
                        sb.Append(",\"preset\":\"").Append(GraphicsProfiles.Current.Key).Append('"');
                        sb.Append('}');
                        return sb.ToString();
                    }));
                    return;
                }
                if (target == "/raycast")
                {
                    float rx, ry;
                    float.TryParse(Get(args, "x"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out rx);
                    float.TryParse(Get(args, "y"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out ry);

                    SendJson(client, RunOnMain(delegate
                    {
                        var data = new UnityEngine.EventSystems.PointerEventData(
                            UnityEngine.EventSystems.EventSystem.current);
                        data.position = new Vector2(rx, ry);
                        data.pressPosition = data.position;

                        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                        var sb = new System.Text.StringBuilder(256);
                        sb.Append("{\"ok\":true,\"hits\":[");

                        bool first = true;
                        for (int i = 0; i < canvases.Length; i++)
                        {
                            var raycaster = canvases[i].GetComponent<UnityEngine.UI.GraphicRaycaster>();
                            if (raycaster == null || !canvases[i].isActiveAndEnabled) continue;

                            hits.Clear();
                            raycaster.Raycast(data, hits);
                            for (int h = 0; h < hits.Count; h++)
                            {
                                var go = hits[h].gameObject;
                                if (go == null) continue;
                                if (!first) sb.Append(',');
                                first = false;
                                sb.Append("{\"canvas\":\"").Append(Esc(canvases[i].name))
                                  .Append("\",\"name\":\"").Append(Esc(go.name))
                                  .Append("\",\"button\":")
                                  .Append(go.GetComponent<UnityEngine.UI.Button>() != null ? "true" : "false")
                                  .Append('}');
                            }
                        }

                        sb.Append("]}");
                        return sb.ToString();
                    }));
                    return;
                }
                if (target == "/kill")
                {
                    SendJson(client, RunOnMain(delegate
                    {
                        var spawner = PlayerSpawner.Instance;
                        var local = spawner != null ? spawner.LocalPlayer : null;
                        if (local == null) return "{\"ok\":false,\"error\":\"no local player\"}";

                        var fps = local.GetComponent<FPSController>();
                        if (fps == null) return "{\"ok\":false,\"error\":\"no fps\"}";

                        fps.KillPlayer();
                        return "{\"ok\":true,\"dead\":" + (PlayerDeathSystem.IsDead(local.PlayerId) ? "true" : "false") + "}";
                    }));
                    return;
                }
                if (target == "/hit")
                {
                    string kind = Get(args, "type");
                    if (string.IsNullOrEmpty(kind)) kind = "RakeAttack";

                    SendJson(client, RunOnMain(delegate
                    {
                        var rakeGo = RakeHighlight.FindRake();
                        var ai = rakeGo != null ? rakeGo.GetComponent<NetworkedRakeAI>() : null;
                        if (ai == null) return "{\"ok\":false,\"error\":\"no rake\"}";

                        var spawner = PlayerSpawner.Instance;
                        var local = spawner != null ? spawner.LocalPlayer : null;
                        if (local == null) return "{\"ok\":false,\"error\":\"no local player\"}";

                        bool hit = ai.AttackPlayer(local.transform, 10, kind);
                        return "{\"ok\":" + (hit ? "true" : "false") +
                               ",\"ragdoll\":" + (PlayerRagdoll.IsActive ? "true" : "false") +
                               ",\"hp\":" + (local.GetComponent<FPSController>() != null ? local.GetComponent<FPSController>().Health.ToString() : "-") + "}";
                    }));
                    return;
                }
                if (target == "/tpp")
                {
                    float px, py, pz;
                    float.TryParse(Get(args, "x"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out px);
                    float.TryParse(Get(args, "y"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out py);
                    float.TryParse(Get(args, "z"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out pz);

                    SendJson(client, RunOnMain(delegate
                    {
                        var spawner = PlayerSpawner.Instance;
                        var local = spawner != null ? spawner.LocalPlayer : null;
                        if (local == null) return "{\"ok\":false,\"error\":\"no local player\"}";

                        // A CharacterController overwrites a transform move, so detach it
                        // for the single frame the teleport takes.
                        var controller = local.GetComponent<CharacterController>();
                        bool wasEnabled = controller != null && controller.enabled;
                        if (controller != null) controller.enabled = false;

                        local.transform.position = new Vector3(px, py, pz);

                        if (controller != null) controller.enabled = wasEnabled;

                        return "{\"ok\":true,\"pos\":\"" +
                               local.transform.position.ToString("F1") + "\"}";
                    }));
                    return;
                }
                if (target == "/clickat")
                {
                    float cx, cy;
                    float.TryParse(Get(args, "x"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out cx);
                    float.TryParse(Get(args, "y"), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out cy);

                    SendJson(client, RunOnMain(delegate
                    {
                        var data = new UnityEngine.EventSystems.PointerEventData(
                            UnityEngine.EventSystems.EventSystem.current);
                        data.position = new Vector2(cx, cy);
                        data.pressPosition = data.position;

                        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();

                        for (int i = 0; i < canvases.Length; i++)
                        {
                            var raycaster = canvases[i].GetComponent<UnityEngine.UI.GraphicRaycaster>();
                            if (raycaster == null || !canvases[i].isActiveAndEnabled) continue;

                            hits.Clear();
                            raycaster.Raycast(data, hits);
                            for (int h = 0; h < hits.Count; h++)
                            {
                                var go = hits[h].gameObject;
                                if (go == null) continue;

                                var button = go.GetComponent<UnityEngine.UI.Button>();
                                if (button == null) button = go.GetComponentInParent<UnityEngine.UI.Button>();
                                if (button == null) continue;

                                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, data,
                                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                                return "{\"ok\":true,\"clicked\":\"" + Esc(button.name) + "\"}";
                            }
                        }

                        return "{\"ok\":false,\"error\":\"no button under the pointer\"}";
                    }));
                    return;
                }
                if (target == "/look")
                {
                    SendJson(client, RunOnMain(delegate
                    {
                        var spawner = PlayerSpawner.Instance;
                        var local = spawner != null ? spawner.LocalPlayer : null;
                        if (local == null) return "{\"ok\":false,\"error\":\"no local player\"}";

                        var fps = local.GetComponent<FPSController>();
                        var looks = local.GetComponentsInChildren<CameraMouseLook>(true);

                        var sb = new StringBuilder(512);
                        sb.Append("{\"ok\":true,\"count\":").Append(looks.Length);
                        sb.Append(",\"fpsLook\":\"").Append(
                            fps != null && fps.camMouseLook != null ? PathOf(fps.camMouseLook.transform) : "-")
                          .Append("\",\"items\":[");
                        for (int i = 0; i < looks.Length; i++)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append("{\"path\":\"").Append(Esc(PathOf(looks[i].transform)))
                              .Append("\",\"bControl\":").Append(looks[i].bControl ? "true" : "false")
                              .Append(",\"enabled\":").Append(looks[i].enabled ? "true" : "false")
                              .Append('}');
                        }
                        sb.Append("]}");
                        return sb.ToString();
                    }));
                    return;
                }
                if (target == "/lobby")
                {
                    SendJson(client, RunOnMain(delegate
                    {
                        RakeNetworkManager.Instance?.ServerReturnToLobby();
                        return "{\"ok\":true}";
                    }));
                    return;
                }
                if (target == "/leave")
                {
                    SendJson(client, RunOnMain(delegate
                    {
                        UI.MultiplayerMenu.ExitToMainMenu("dev bridge");
                        return "{\"ok\":true}";
                    }));
                    return;
                }
                if (target == "/ai")
                {
                    string go = Get(args, "go");
                    string attack = Get(args, "atk");

                    SendJson(client, RunOnMain(delegate
                    {
                        var rake = RakeHighlight.FindRake();
                        var ai = rake != null ? rake.GetComponent<NetworkedRakeAI>() : null;
                        if (ai == null) return "{\"ok\":false,\"error\":\"no rake\"}";

                        string forced = "";
                        if (!string.IsNullOrEmpty(go)) forced = ai.ForceTask() ? "started" : "inactive";
                        if (!string.IsNullOrEmpty(attack)) forced = ai.ForceGrabNow() ? "attack" : "inactive";

                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        Vector3 pos = ai.transform.position;

                        return "{\"ok\":true" +
                               ",\"pos\":\"" + pos.x.ToString("F1", culture) + ", " +
                                                    pos.y.ToString("F1", culture) + ", " +
                                                    pos.z.ToString("F1", culture) + "\"" +
                               ",\"dist\":" + ai.DistanceToNearestHunter().ToString("F1", culture) +
                               ",\"forced\":\"" + forced + "\"" +
                               ",\"ai\":\"" + Esc(ai.GetAiStatusReport().Trim()) + "\"}";
                    }));
                    return;
                }
                if (target == "/phase")
                {
                    string skip = Get(args, "skip");

                    SendJson(client, RunOnMain(delegate
                    {
                        var dayNight = DayNightManager.Instance;
                        if (dayNight == null) return "{\"ok\":false,\"error\":\"no cycle\"}";

                        if (!string.IsNullOrEmpty(skip)) dayNight.SkipPhase();

                        return "{\"ok\":true,\"phase\":\"" + dayNight.CurrentPhase +
                               "\",\"night\":" + dayNight.NightNumber +
                               ",\"running\":" + (dayNight.IsRunning ? "true" : "false") +
                               ",\"scene\":\"" + UnityEngine.Application.loadedLevelName + "\"}";
                    }));
                    return;
                }
                if (target == "/stats")
                {
                    string on = Get(args, "on");
                    SendJson(client, RunOnMain(delegate
                    {
                        var stats = UI.PerfStats.Instance;
                        if (stats == null) return "{\"ok\":false}";
                        if (!string.IsNullOrEmpty(on))
                        {
                            stats.SetVisible(on == "1" || string.Equals(on, "true", StringComparison.OrdinalIgnoreCase));
                        }
                        return stats.BridgeStats();
                    }));
                    return;
                }
                if (target == "/climb")
                {
                    SendJson(client, RunOnMain(delegate { return RakePlayerController.BridgeClimb(); }));
                    return;
                }
                if (target == "/space")
                {
                    SendJson(client, RunOnMain(delegate
                    {
                        RakePlayerController.BridgeSpace();
                        return "{\"ok\":true}";
                    }));
                    return;
                }
                if (target == "/move")
                {
                    string w = Get(args, "w");
                    bool forward = w == "1" || string.Equals(w, "true", StringComparison.OrdinalIgnoreCase);
                    SendJson(client, RunOnMain(delegate
                    {
                        float yaw;
                        float.TryParse(Get(args, "yaw"), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out yaw);
                        RakePlayerController.BridgeMove(forward, yaw);
                        return "{\"ok\":true,\"forward\":" + (forward ? "true" : "false") + "}";
                    }));
                    return;
                }
                if (target == "/pause")
                {
                    string on = Get(args, "on");
                    SendJson(client, RunOnMain(delegate
                    {
                        var menu = UnityEngine.Object.FindObjectOfType<GamePauseMenu>();
                        if (menu == null) return "{\"ok\":false,\"error\":\"no pause menu\"}";

                        bool want = on == "1" || string.Equals(on, "true", StringComparison.OrdinalIgnoreCase);
                        menu.isPaused = want;

                        var fps = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
                        var controller = fps != null ? fps.GetComponent<FPSController>() : null;

                        if (want)
                        {
                            if (menu.menu != null) menu.menu.SetActive(true);
                            if (controller != null) controller.SetPlayerDisableState("PAUSEMENU");
                            Time.timeScale = 0f;
                            Cursor.lockState = CursorLockMode.None;
                            Cursor.visible = true;
                        }
                        else
                        {
                            if (menu.menu != null) menu.menu.SetActive(false);
                            if (controller != null) controller.SetPlayerDisableState("BEFOREPAUSE");
                            Time.timeScale = 1f;
                        }

                        return "{\"ok\":true,\"paused\":" + (menu.isPaused ? "true" : "false") + "}";
                    }));
                    return;
                }
                if (target == "/hud")
                {
                    SendJson(client, RunOnMain(delegate { return RakeHudUI.BridgePulse(); }));
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

                var spawner = PlayerSpawner.Instance;
                var local = spawner != null ? spawner.LocalPlayer : null;
                var fps = local != null ? local.GetComponent<FPSController>() : null;
                var look = local != null ? local.GetComponentInChildren<CameraMouseLook>() : null;
                var move = local != null ? local.GetComponent<PlayerMovement>() : null;

                var pauseMenu = UnityEngine.Object.FindObjectOfType<GamePauseMenu>();
                sb.Append(",\"pausePanel\":").Append(
                    pauseMenu != null && pauseMenu.menu != null && pauseMenu.menu.activeSelf ? "true" : "false");
                sb.Append(",\"paused\":").Append(pauseMenu != null && pauseMenu.isPaused ? "true" : "false");
                sb.Append(",\"ragdoll\":").Append(PlayerRagdoll.IsActive ? "true" : "false");
                sb.Append(",\"dead\":").Append(local != null && PlayerDeathSystem.IsDead(local.PlayerId) ? "true" : "false");
                sb.Append(",\"hp\":").Append(fps != null ? fps.Health.ToString() : "-");
                sb.Append(",\"bControl\":").Append(look != null && look.bControl ? "true" : "false");
                sb.Append(",\"canMove\":").Append(move != null && move.bCanMove ? "true" : "false");
                sb.Append(",\"lookEnabled\":").Append(look != null && look.enabled ? "true" : "false");
                sb.Append(",\"camEuler\":\"").Append(look != null && look.camera != null ? look.camera.eulerAngles.ToString("F0") : "-").Append("\"");
                sb.Append(",\"targetEuler\":\"").Append(look != null ? look.m_CameraTargetRot.eulerAngles.ToString("F0") : "-").Append("\"");
                sb.Append(",\"charEuler\":\"").Append(local != null ? local.transform.eulerAngles.ToString("F0") : "-").Append("\"");
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

        private static string PathOf(Transform t)
        {
            if (t == null) return "-";
            var sb = new StringBuilder(64);
            while (t != null)
            {
                if (sb.Length > 0) sb.Insert(0, '/');
                sb.Insert(0, t.name);
                t = t.parent;
            }
            return sb.ToString();
        }

        private static string Esc(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }
}
