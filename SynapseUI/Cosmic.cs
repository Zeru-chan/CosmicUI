// ═══════════════════════════════════════════════════════════════════
//  Usage:
//    Cosmic.Initialize();
//    Cosmic.Attach();
//    Cosmic.Execute("print('hello')");
//    Cosmic.SetMaxFps(144);
//    Cosmic.Shutdown();
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

public static class Cosmic
{
    private const string CosmicRootPath = @"C:\Cosmic";

    /// <summary>Fired when a module connects. Provides the process ID.</summary>
    public static event Action<int> OnClientConnected;

    /// <summary>Fired when a module disconnects. Provides the process ID.</summary>
    public static event Action<int> OnClientDisconnected;

    /// <summary>Fired when a module sends console output. (pid, status, message) — status: 0=print, 1=warn, 2=error, 3=info</summary>
    public static event Action<int, int, string> OnOutput;

    /// <summary>Fired when a module reports user info. (pid, userId, gameId)</summary>
    public static event Action<int, int, int> OnUserInfo;

    private const int WsPort = 24950;

    private static HttpListener _listener;
    private static CancellationTokenSource _cts;
    private static readonly ConcurrentDictionary<int, _ModuleClient> _clients = new ConcurrentDictionary<int, _ModuleClient>();
    private static readonly ConcurrentDictionary<int, byte> _pendingAttachments = new ConcurrentDictionary<int, byte>();
    private static bool _initialized;

    private static readonly string[] _robloxProcessNames = { "RobloxPlayerBeta", "Windows10Universal" };

    /// <summary>Whether the WebSocket server is running.</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>Number of connected modules.</summary>
    public static int ClientCount => _clients.Count;

    /// <summary>
    /// Initialize Cosmic - starts the WebSocket server on port 24950.
    /// Call this once before using any other methods.
    /// </summary>
    private static readonly HttpClient _http = new HttpClient();

    private static Task _initTask;

    public static bool IsReady => _initTask != null && _initTask.IsCompleted;

    public static async Task Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        string cosmicDir = _GetCosmicDirectory();

        _initTask = Task.WhenAll(
            _DownloadFile("https://auth.cosmic.best/files/dll", Path.Combine(cosmicDir, "Cosmic-Module.dll")),
            _DownloadFile("https://auth.cosmic.best/files/injector", Path.Combine(cosmicDir, "Cosmic-Injector.exe"))
        );

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{WsPort}/");
        _listener.Start();
        IsRunning = true;

        Task.Run(() => _AcceptLoop(_cts.Token));

        await _initTask.ConfigureAwait(false);
    }

    private static string _GetCosmicDirectory()
    {
        Directory.CreateDirectory(CosmicRootPath);
        return CosmicRootPath;
    }

    private static async Task _DownloadFile(string url, string destPath)
    {
        var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
        try
        {
            File.WriteAllBytes(destPath, bytes);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// Shutdown Cosmic - stops the WebSocket server and disconnects all modules.
    /// </summary>
    public static void Shutdown()
    {
        if (!_initialized) return;
        IsRunning = false;
        _initialized = false;

        _cts?.Cancel();

        foreach (var client in _clients.Values)
        {
            try { client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None).Wait(1000); }
            catch { }
        }
        _clients.Clear();
        _pendingAttachments.Clear();

        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
    }

    /// <summary>
    /// Find all running Roblox processes and inject into each one.
    /// Returns a dictionary of pid to exit code (6 = success).
    /// </summary>
    public static Dictionary<int, int> Attach()
    {
        _EnsureInit();
        var results = new Dictionary<int, int>();
        foreach (int pid in GetRobloxProcesses())
        {
            if (_clients.ContainsKey(pid) || _pendingAttachments.ContainsKey(pid))
            {
                results[pid] = 7;
                continue;
            }

            try { results[pid] = Attach(pid); }
            catch { results[pid] = -1; }
        }
        return results;
    }

    /// <summary>
    /// Inject into a specific Roblox process by PID.
    /// Returns the injector exit code (6 = success).
    /// </summary>
    public static int Attach(int pid)
    {
        _EnsureInit();
        if (_clients.ContainsKey(pid) || !_pendingAttachments.TryAdd(pid, 0))
            return 7;

        string cosmicDir = _GetCosmicDirectory();
        string injector = Path.Combine(cosmicDir, "Cosmic-Injector.exe");

        if (!File.Exists(injector))
            throw new FileNotFoundException($"Injector not found at: {injector}");

        try
        {
            using (var proc = Process.Start(new ProcessStartInfo
            {
                FileName = injector,
                Arguments = pid.ToString(),
                WorkingDirectory = cosmicDir,
                UseShellExecute = true,
            }))
            {
                if (!proc.WaitForExit(60000))
                {
                    try { proc.Kill(); } catch { }
                    _pendingAttachments.TryRemove(pid, out _);
                    return -1;
                }

                if (proc.ExitCode != 6)
                    _pendingAttachments.TryRemove(pid, out _);
                else
                {
                    Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(delayTask =>
                    {
                        if (!_clients.ContainsKey(pid))
                        {
                            byte ignored;
                            _pendingAttachments.TryRemove(pid, out ignored);
                        }
                    });
                }

                return proc.ExitCode;
            }
        }
        catch
        {
            _pendingAttachments.TryRemove(pid, out _);
            throw;
        }
    }

    /// <summary>Get all running Roblox process IDs (whether injected or not).</summary>
    public static List<int> GetRobloxProcesses()
    {
        var pids = new List<int>();
        foreach (var name in _robloxProcessNames)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    pids.Add(p.Id);
                    p.Dispose();
                }
            }
            catch { }
        }
        return pids;
    }

    /// <summary>Get all currently connected (injected) module PIDs.</summary>
    public static List<int> GetClients()
    {
        _EnsureInit();
        return _clients.Keys.ToList();
    }

    /// <summary>Waits till the Module and Injector got downloaded</summary>
    public static async Task WaitForReady()
    {
        if (_initTask != null)
            await _initTask.ConfigureAwait(false);
    }

    /// <summary>Translate an injector exit code to a human-readable message.</summary>
    public static string GetAttachStatusMessage(int exitCode)
    {
        switch (exitCode)
        {
            case 6: return "Success";
            case -1: return "Initialization failure";
            case 0: return "Failed to open Roblox process";
            case 1: return "Roblox version mismatch";
            case 2: return "Module DLL not found or corrupt";
            case 3: return "Memory operation failed";
            case 4: return "PDB download failed";
            case 5: return "Injection timeout";
            case 7: return "Already injected";
            default: return $"Unknown exit code: {exitCode}";
        }
    }

    /// <summary>Execute a Luau script on all connected modules.</summary>
    public static void Execute(string script)
    {
        _EnsureInit();
        _BroadcastAsync(script).GetAwaiter().GetResult();
    }

    /// <summary>Execute a Luau script on all connected modules (async).</summary>
    public static async Task ExecuteAsync(string script)
    {
        _EnsureInit();
        await _BroadcastAsync(script).ConfigureAwait(false);
    }

    /// <summary>Execute a Luau script on a specific module by PID.</summary>
    public static void Execute(int pid, string script)
    {
        _EnsureInit();
        _SendToAsync(pid, script).GetAwaiter().GetResult();
    }

    /// <summary>Execute a Luau script on a specific module by PID (async).</summary>
    public static async Task ExecuteAsync(int pid, string script)
    {
        _EnsureInit();
        await _SendToAsync(pid, script).ConfigureAwait(false);
    }

    /// <summary>Set the maximum FPS for all connected modules.</summary>
    public static void SetMaxFps(int fps) => _SendSetting("Max-Fps", fps.ToString());

    /// <summary>Enable or disable FPS unlock for all connected modules.</summary>
    public static void SetUnlockFps(bool enabled) => _SendSetting("Unlock-Fps", enabled ? "true" : "false");

    /// <summary>Enable or disable auto-execute for all connected modules.</summary>
    public static void SetAutoExecute(bool enabled) => _SendSetting("Auto-Execute", enabled ? "true" : "false");

    /// <summary>Enable or disable console redirect for all connected modules.</summary>
    public static void SetConsoleRedirect(bool enabled) => _SendSetting("Console-Redirect", enabled ? "true" : "false");

    /// <summary>
    /// Set the console filter for all connected modules.
    /// Values: "MESSAGE_NONE", "MESSAGE_OUTPUT", "MESSAGE_WARN", "MESSAGE_ERROR"
    /// </summary>
    public static void SetConsoleFilter(string filter) => _SendSetting("Console-Filter", filter);

    private class _ModuleClient
    {
        public int Pid;
        public WebSocket Socket;
        public int UserId;
        public int GameId;
    }

    private static async Task _AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                string pidHeader = context.Request.Headers["process-id"];
                if (!int.TryParse(pidHeader, out int pid))
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var client = new _ModuleClient { Pid = pid, Socket = wsContext.WebSocket };
                _clients[pid] = client;
                _pendingAttachments.TryRemove(pid, out _);

                OnClientConnected?.Invoke(pid);

                _ = Task.Run(() => _HandleClient(client, ct));
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch { }
        }
    }

    private static async Task _HandleClient(_ModuleClient client, CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message;
                    if (result.EndOfMessage)
                    {
                        message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    }
                    else
                    {
                        using (var ms = new MemoryStream())
                        {
                            ms.Write(buffer, 0, result.Count);
                            while (!result.EndOfMessage)
                            {
                                result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                                ms.Write(buffer, 0, result.Count);
                            }
                            message = Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }

                    _ProcessMessage(client, message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch { }
        finally
        {
            _clients.TryRemove(client.Pid, out _);
            OnClientDisconnected?.Invoke(client.Pid);
            try { client.Socket.Dispose(); } catch { }
        }
    }

    private static void _ProcessMessage(_ModuleClient client, string message)
    {
        if (string.IsNullOrEmpty(message) || message[0] != '{') return;

        try
        {
            if (message.Contains("\"Status\"") && message.Contains("\"Message\""))
            {
                int status = _JsonInt(message, "Status");
                string msg = _JsonString(message, "Message");
                if (msg != null)
                    OnOutput?.Invoke(client.Pid, status, msg);
            }
            else if (message.Contains("\"UserId\"") && message.Contains("\"GameId\""))
            {
                client.UserId = _JsonInt(message, "UserId");
                client.GameId = _JsonInt(message, "GameId");
                OnUserInfo?.Invoke(client.Pid, client.UserId, client.GameId);
            }
        }
        catch { }
    }

    private static async Task _BroadcastAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = new List<Task>();
        foreach (var client in _clients.Values.ToArray())
        {
            if (client.Socket.State == WebSocketState.Open)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try { await client.Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false); }
                    catch { }
                }));
            }
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task _SendToAsync(int pid, string message)
    {
        if (_clients.TryGetValue(pid, out var client) && client.Socket.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await client.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static void _SendSetting(string name, object value)
    {
        _EnsureInit();

        string json;
        if (value is bool b)
            json = $"{{\"Name\":\"{name}\",\"Value\":{(b ? "true" : "false")}}}";
        else
            json = $"{{\"Name\":\"{name}\",\"Value\":\"{value}\"}}";

        _BroadcastAsync(json).GetAwaiter().GetResult();
    }

    private static void _EnsureInit()
    {
        if (!_initialized)
            throw new InvalidOperationException("Cosmic.Initialize() must be called first.");
    }

    private static string _JsonString(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;
        idx = json.IndexOf('"', idx + 1);
        if (idx < 0) return null;
        int end = json.IndexOf('"', idx + 1);
        if (end < 0) return null;
        return json.Substring(idx + 1, end - idx - 1)
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }

    private static int _JsonInt(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return 0;
        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return 0;
        idx++;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        int start = idx;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '-')) idx++;
        if (int.TryParse(json.Substring(start, idx - start), out int val))
            return val;
        return 0;
    }
}
