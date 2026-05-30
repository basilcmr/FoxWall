using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using pylorak.Windows;
using pylorak.Windows.NetStat;

namespace pylorak.TinyWall
{
    internal class DashboardServer : IDisposable
    {
        private HttpListener? Listener;
        private Thread? ServerThread;
        private bool IsRunning = false;
        private readonly int Port = 5678;
        private readonly string WebRoot;
        private readonly TinyWallController Controller;
        private bool PanicActive = false;
        private readonly TrafficHistoryLogger HistoryLogger;
        private readonly Dictionary<string, string> HashCache = new();

        public DashboardServer(TinyWallController ctrl)
        {
            this.Controller = ctrl;
            // Web files will be copied to a 'Web' directory alongside the executable
            this.WebRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Web");
            this.HistoryLogger = new TrafficHistoryLogger();
        }

        private string GetFileHashCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            lock (HashCache)
            {
                if (HashCache.TryGetValue(path, out string? cached)) return cached;

                string hash = "";
                try
                {
                    string resolved = WildcardHelper.ResolveWildcardPath(path);
                    if (File.Exists(resolved))
                    {
                        hash = Hasher.HashFileSha1(resolved) ?? "";
                    }
                }
                catch {}

                HashCache[path] = hash;
                return hash;
            }
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                Listener = new HttpListener();
                Listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                Listener.Prefixes.Add($"http://localhost:{Port}/");
                
                // Allow only local requests for high security
                IsRunning = true;
                Listener.Start();

                ServerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "FoxWallDashboardServer"
                };
                ServerThread.Start();
                Utils.Log($"FoxWall Dashboard Server started on http://localhost:{Port}/", Utils.LOG_ID_GUI);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex, Utils.LOG_ID_GUI);
                IsRunning = false;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            try
            {
                Listener?.Stop();
                Listener?.Close();
            }
            catch { }

            if (ServerThread != null && ServerThread.IsAlive)
            {
                ServerThread.Join(1000);
            }
        }

        private void ListenLoop()
        {
            while (IsRunning && Listener != null && Listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = Listener.GetContext();
                    ThreadPool.QueueUserWorkItem((state) => HandleRequest(context));
                }
                catch
                {
                    if (!IsRunning) break;
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Restrict request source strictly to loopback interface for security
            if (!request.IsLocal)
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.Close();
                return;
            }

            try
            {
                string urlPath = request.Url?.AbsolutePath ?? "/";
                
                // CORS Headers just in case
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.Close();
                    return;
                }

                // Routing
                if (urlPath.StartsWith("/api/"))
                {
                    HandleApiRequest(urlPath, request, response);
                }
                else
                {
                    HandleStaticFileRequest(urlPath, response);
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
                    response.ContentType = "application/json";
                    response.ContentLength64 = data.Length;
                    response.OutputStream.Write(data, 0, data.Length);
                }
                catch { }
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private void HandleStaticFileRequest(string urlPath, HttpListenerResponse response)
        {
            if (urlPath == "/" || string.IsNullOrEmpty(urlPath))
            {
                urlPath = "/index.html";
            }

            string filePath = Path.Combine(WebRoot, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                byte[] data = Encoding.UTF8.GetBytes("404 - Not Found");
                response.ContentLength64 = data.Length;
                response.OutputStream.Write(data, 0, data.Length);
                return;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            response.ContentType = ext switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };

            byte[] fileBytes = File.ReadAllBytes(filePath);
            response.ContentLength64 = fileBytes.Length;
            response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
        }

        private void HandleApiRequest(string urlPath, HttpListenerRequest request, HttpListenerResponse response)
        {
            response.ContentType = "application/json";
            response.StatusCode = (int)HttpStatusCode.OK;

            object? responseData = null;

            switch (urlPath)
            {
                case "/api/status":
                    // Aggregate stats
                    responseData = new
                    {
                        mode = GlobalInstances.TinyWallControllerInstance?.ActiveModeName ?? "Normal",
                        locked = GlobalInstances.Controller.IsServerLocked,
                        rxSpeed = HistoryLogger.CurrentRx,
                        txSpeed = HistoryLogger.CurrentTx,
                        // [FoxWall Enhancement] - Add Physical Speed Status Properties
                        rxSpeedPhysical = HistoryLogger.CurrentPhysicalRx,
                        txSpeedPhysical = HistoryLogger.CurrentPhysicalTx,
                        // [FoxWall Enhancement] - End of Physical Speed Status Properties
                        panicActive = PanicActive,
                        version = GetVersion()
                    };
                    break;

                case "/api/connections":
                    responseData = GetActiveConnections();
                    break;

                case "/api/logs":
                    responseData = GetFirewallLogs();
                    break;

                case "/api/analytics/history":
                    {
                        DateTime end = DateTime.Now;
                        DateTime start = end.AddMinutes(-5);

                        string range = request.QueryString["range"] ?? "5m";
                        if (range == "custom")
                        {
                            if (DateTime.TryParse(request.QueryString["start"], out DateTime parsedStart))
                                start = parsedStart;
                            if (DateTime.TryParse(request.QueryString["end"], out DateTime parsedEnd))
                                end = parsedEnd;
                        }
                        else
                        {
                            // Support fully dynamic duration like '12h', '10m', '3d', '1w'
                            if (range.Length > 1 && char.IsLetter(range[range.Length - 1]))
                            {
                                char unit = range[range.Length - 1];
                                string numStr = range.Substring(0, range.Length - 1);
                                if (int.TryParse(numStr, out int val) && val > 0)
                                {
                                    if (unit == 'm') start = end.AddMinutes(-val);
                                    else if (unit == 'h') start = end.AddHours(-val);
                                    else if (unit == 'd') start = end.AddDays(-val);
                                    else if (unit == 'w') start = end.AddDays(-val * 7);
                                }
                            }
                        }
                        responseData = HistoryLogger.GetHistory(start, end);
                        break;
                    }

                case "/api/action/panic":
                    if (request.HttpMethod == "POST")
                    {
                        PanicActive = !PanicActive;
                        responseData = new { active = PanicActive };
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    }
                    break;

                case "/api/action/whitelist":
                    if (request.HttpMethod == "POST")
                    {
                        string? path = request.QueryString["path"];
                        if (!string.IsNullOrEmpty(path))
                        {
                            // Whitelist via WCF Controller
                            var exSubj = new ExecutableSubject(path);
                            var exceptions = GlobalInstances.AppDatabase.GetExceptionsForApp(exSubj, true, out _);
                            Controller.AddExceptions(exceptions);
                            responseData = new { success = true };
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    }
                    break;

                case "/api/action/terminate":
                    if (request.HttpMethod == "POST")
                    {
                        string? pidStr = request.QueryString["pid"];
                        if (uint.TryParse(pidStr, out uint pid) && pid != 0)
                        {
                            try
                            {
                                using var proc = Process.GetProcessById((int)pid);
                                proc.Kill();
                                responseData = new { success = true };
                            }
                            catch (Exception ex)
                            {
                                responseData = new { success = false, error = ex.Message };
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    }
                    break;

                case "/api/action/open-folder":
                    if (request.HttpMethod == "POST")
                    {
                        string? path = request.QueryString["path"];
                        if (!string.IsNullOrEmpty(path))
                        {
                            try
                            {
                                string resolved = WildcardHelper.ResolveWildcardPath(path);
                                if (File.Exists(resolved))
                                {
                                    Process.Start("explorer.exe", $"/select,\"{resolved}\"");
                                    responseData = new { success = true };
                                }
                                else if (Directory.Exists(resolved))
                                {
                                    Process.Start("explorer.exe", $"\"{resolved}\"");
                                    responseData = new { success = true };
                                }
                                else
                                {
                                    responseData = new { success = false, error = "File or folder does not exist." };
                                }
                            }
                            catch (Exception ex)
                            {
                                responseData = new { success = false, error = ex.Message };
                            }
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    }
                    break;


                // [FoxWall Enhancement] - Start of PC Power Manager API Endpoints
                case "/api/power/status":
                    responseData = PowerScheduler.Instance.GetStatus();
                    break;

                case "/api/power/schedule":
                    if (request.HttpMethod == "POST")
                    {
                        try
                        {
                            string? actionStr = request.QueryString["action"];
                            string? triggerStr = request.QueryString["trigger"];
                            string? valueStr = request.QueryString["value"];
                            string? exactTimeStr = request.QueryString["exactTime"];
                            string? modeStr = request.QueryString["mode"];
                            string? canCancelStr = request.QueryString["canCancel"];

                            // Chained trigger parameters
                            string? chainTriggerStr = request.QueryString["chainTrigger"];
                            string? chainValueStr = request.QueryString["chainValue"];
                            string? chainExactTimeStr = request.QueryString["chainExactTime"];
                            string? graceSecondsStr = request.QueryString["graceSeconds"];

                            if (string.IsNullOrEmpty(actionStr) || string.IsNullOrEmpty(triggerStr))
                            {
                                response.StatusCode = (int)HttpStatusCode.BadRequest;
                                responseData = new { success = false, error = "Action and trigger are required parameters." };
                                break;
                            }

                            if (triggerStr.Equals("exact", StringComparison.OrdinalIgnoreCase))
                            {
                                triggerStr = "ExactTime";
                            }

                            if (chainTriggerStr != null && chainTriggerStr.Equals("exact", StringComparison.OrdinalIgnoreCase))
                            {
                                chainTriggerStr = "ExactTime";
                            }

                            PowerAction action = (PowerAction)Enum.Parse(typeof(PowerAction), actionStr, true);
                            TriggerType trigger = (TriggerType)Enum.Parse(typeof(TriggerType), triggerStr, true);

                            int value = 0;
                            if (!string.IsNullOrEmpty(valueStr))
                            {
                                int.TryParse(valueStr, out value);
                            }

                            ExecutionMode mode = ExecutionMode.Smart;
                            if (!string.IsNullOrEmpty(modeStr))
                            {
                                Enum.TryParse(modeStr, true, out mode);
                            }

                            bool canCancel = true;
                            if (!string.IsNullOrEmpty(canCancelStr))
                            {
                                bool.TryParse(canCancelStr, out canCancel);
                            }

                            int graceSeconds = 300;
                            if (!string.IsNullOrEmpty(graceSecondsStr))
                            {
                                int.TryParse(graceSecondsStr, out graceSeconds);
                            }

                            bool hasChainTrigger = false;
                            TriggerType chainTrigger = TriggerType.Duration;
                            int chainValue = 0;

                            if (!string.IsNullOrEmpty(chainTriggerStr))
                            {
                                hasChainTrigger = true;
                                Enum.TryParse(chainTriggerStr, true, out chainTrigger);
                                if (!string.IsNullOrEmpty(chainValueStr))
                                {
                                    int.TryParse(chainValueStr, out chainValue);
                                }
                            }

                            PowerScheduler.Instance.StartSchedule(
                                action, 
                                trigger, 
                                value, 
                                exactTimeStr, 
                                mode, 
                                canCancel, 
                                hasChainTrigger, 
                                chainTrigger, 
                                chainValue, 
                                chainExactTimeStr,
                                graceSeconds
                            );
                            responseData = new { success = true };
                        }
                        catch (Exception ex)
                        {
                            responseData = new { success = false, error = ex.Message };
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    }
                    break;

                case "/api/power/cancel":
                    if (request.HttpMethod == "POST")
                    {
                        try
                        {
                            string? password = request.QueryString["password"];

                            // If FoxWall settings are currently password locked, enforce verification
                            if (GlobalInstances.Controller.IsServerLocked)
                            {
                                if (string.IsNullOrEmpty(password))
                                {
                                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                                    responseData = new { success = false, error = "Password required to cancel schedule." };
                                    break;
                                }

                                if (GlobalInstances.Controller.TryUnlockServer(password) != MessageType.UNLOCK)
                                {
                                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                                    responseData = new { success = false, error = "Incorrect security password." };
                                    break;
                                }
                            }

                            PowerScheduler.Instance.Cancel();
                            responseData = new { success = true };
                        }
                        catch (Exception ex)
                        {
                            response.StatusCode = (int)HttpStatusCode.Forbidden;
                            responseData = new { success = false, error = ex.Message };
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    }
                    break;
                // [FoxWall Enhancement] - End of PC Power Manager API Endpoints

                default:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }

            if (responseData != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responseData));
                response.ContentLength64 = data.Length;
                response.OutputStream.Write(data, 0, data.Length);
            }
        }

        private string GetFileDescription(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try
            {
                string resolved = WildcardHelper.ResolveWildcardPath(path);
                if (File.Exists(resolved))
                {
                    var info = FileVersionInfo.GetVersionInfo(resolved);
                    return info.FileDescription ?? "";
                }
            }
            catch {}
            return "";
        }

        private List<object> GetActiveConnections()
        {
            var results = new List<object>();
            var procCache = new Dictionary<uint, string>();
            var packageList = new UwpPackageList();
            var servicePids = new ServicePidMap();

            try
            {
                TcpTable tcpTable = NetStat.GetExtendedTcp4Table(false);
                foreach (TcpRow row in tcpTable)
                {
                    string path = GetPathFromPidCached(procCache, row.ProcessId);
                    string name = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(name)) name = "System / Services";

                    results.Add(new
                    {
                        ProcessName = name,
                        Path = path,
                        AppName = GetFileDescription(path),
                        Pid = row.ProcessId,
                        Protocol = "TCP",
                        LocalAddress = row.LocalEndPoint.Address.ToString(),
                        LocalPort = row.LocalEndPoint.Port,
                        RemoteAddress = row.RemoteEndPoint.Address.ToString(),
                        RemotePort = row.RemoteEndPoint.Port,
                        State = row.State.ToString(),
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        FileHash = GetFileHashCached(path)
                    });
                }

                // UDP Sockets
                UdpTable udpTable = NetStat.GetExtendedUdp4Table(false);
                foreach (UdpRow row in udpTable)
                {
                    string path = GetPathFromPidCached(procCache, row.ProcessId);
                    string name = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(name)) name = "System / Services";

                    results.Add(new
                    {
                        ProcessName = name,
                        Path = path,
                        AppName = GetFileDescription(path),
                        Pid = row.ProcessId,
                        Protocol = "UDP",
                        LocalAddress = row.LocalEndPoint.Address.ToString(),
                        LocalPort = row.LocalEndPoint.Port,
                        RemoteAddress = "*",
                        RemotePort = 0,
                        State = "Listening",
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        FileHash = GetFileHashCached(path)
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.LogException(ex, Utils.LOG_ID_GUI);
            }

            return results;
        }

        private List<object> GetFirewallLogs()
        {
            var results = new List<object>();
            try
            {
                var response = GlobalInstances.Controller.BeginReadFwLog();
                var logEntries = pylorak.TinyWall.Controller.EndReadFwLog(response.Response);

                foreach (var entry in logEntries)
                {
                    if (entry.AppPath == null) continue;
                    
                    results.Add(new
                    {
                        Time = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        ProcessName = Path.GetFileName(entry.AppPath) ?? "System",
                        Path = entry.AppPath,
                        AppName = GetFileDescription(entry.AppPath),
                        Pid = entry.ProcessId,
                        Protocol = entry.Protocol.ToString(),
                        Direction = entry.Direction.ToString(),
                        LocalAddress = entry.LocalIp,
                        LocalPort = entry.LocalPort,
                        RemoteAddress = entry.RemoteIp,
                        RemotePort = entry.RemotePort,
                        Action = entry.Event.ToString().Contains("BLOCKED") ? "Blocked" : "Allowed",
                        FileHash = GetFileHashCached(entry.AppPath)
                    });
                }
            }
            catch { }

            return results;
        }

        private string GetPathFromPidCached(Dictionary<uint, string> cache, uint pid)
        {
            if (cache.TryGetValue(pid, out string path))
                return path;

            string resolvedPath = Utils.GetPathOfProcessUseTwService(pid, GlobalInstances.Controller);
            cache.Add(pid, resolvedPath);
            return resolvedPath;
        }

        private string GetVersion()
        {
            try
            {
                // version.json is located in the root application directory or one parent folder up
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
                if (!File.Exists(path))
                {
                    path = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? "", "version.json");
                }
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Version", out JsonElement val))
                    {
                        return val.GetString() ?? "1.2.3";
                    }
                }
            }
            catch { }
            return "1.2.3";
        }

        public void Dispose()
        {
            Stop();
            HistoryLogger.Dispose();
        }
    }
}
