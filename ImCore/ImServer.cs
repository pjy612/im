using System.Collections.Generic;
using System.IO;
using CSRedis;
using NewLife.Json;
using Newtonsoft.Json.Linq;
#if ns20
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dean.Edwards;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Threading;

public static class ImServerExtenssions
{
    static bool isUseWebSockets = false;

    /// <summary>
    /// 启用 ImServer 服务端
    /// </summary>
    /// <param name="app"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseImServer(this IApplicationBuilder app, ImServerOptions options)
    {
        app.Map(options.PathMatch, appcur =>
        {
            var imserv = new ImServer(options);
            if (isUseWebSockets == false)
            {
                isUseWebSockets = true;
                var webSocketOptions = new WebSocketOptions()
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(60),
                    ReceiveBufferSize = 4 * 1024
                };
                appcur.UseWebSockets(webSocketOptions);
            }
            appcur.Use(async (ctx, next) =>
                await imserv.Acceptor(ctx, next));
        });
        return app;
    }

    public static string encodeJs(this string js)
    {
        try
        {
            using (var packer = new ECMAScriptPacker(ECMAScriptPacker.PackerEncoding.Numeric, true, false))
            {
                return packer.Pack(js);
            }
        }
        catch (Exception e)
        {
            XTrace.WriteException(e);
            return js;
        }
    }
}

/// <summary>
/// im 核心类实现的配置所需
/// </summary>
public class ImServerOptions : ImClientOptions
{
    /// <summary>
    /// 设置服务名称，它应该是 servers 内的一个
    /// </summary>
    public string Server { get; set; }
}

public class Message
{
    public int code { get; set; }
    public string type { get; set; }
    public string token { get; set; }
    public dynamic data { get; set; }
    public dynamic ext { get; set; }
}

class ImServer : ImClient
{
    protected string _server { get; set; }

    public ImServer(ImServerOptions options) : base(options)
    {
        _server = options.Server;
        new TimerX(async state =>
        {
            _clients.Where(r => r.Value.connetcDateTime.AddSeconds(30) < DateTime.Now && r.Value.clientMetaData.realUid == 0).AsParallel().ForAll(r =>
            {
                r.Value.socket.Abort();
                _clients.TryRemove(r.Key, out var x);
            });
            long canEnterRoom = await _redis.GetAsync<long>("jroom");
            Dictionary<long, int> roomUserCount = _clients.Where(r => r.Value.clientMetaData.roomid > 0)
                .Select(r => r.Value.clientMetaData.roomid)
                .Where(r => r == 21438956 || r == canEnterRoom)
                .GroupBy(r => r).ToDictionary(r => r.Key, r => r.Count());
            int trueRoom = roomUserCount.Values.Sum();
            string roomCountStr = roomUserCount.Select(r => new
            {
                Key = r.Key,
                Value = r.Value,
                o = (r.Key == 21438956 ? 0 : r.Key)
            }).OrderBy(k => k.o).Select(r => $"{r.Key}:{r.Value}").Join("|");

            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} 当前客户数：{_clients.Count},指定房间：{trueRoom},其他房间：{_clients.Count - trueRoom},房间详情：{roomCountStr}");
            if (_offlineClients.Any())
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} 维护期用户数:{_offlineClients.Count}");
            }
        }, null, 1000, 5000)
        { Async = true };
        //        new TimerX(state =>
        //        {
        //            var message = new Message()
        //            {
        //                type = "pong"
        //            };
        //            //Console.WriteLine("心跳反馈");
        //            _clients.AsParallel().ForAll(async client => { await SendStringAsync(client.Value.socket, message); });
        //            //Console.WriteLine("心跳结束");
        //        }, null, 1_000, 60_000);
        new TimerX(async state =>
        {
            blackIps.Clear();
            await _redis.DelAsync("preBlack", "blacklist");
        }, null, 100, 10 * 60_000);
        new TimerX(state =>
       {
           _clients.AsParallel().ForAll(async r =>
           {
               bool inblack = await _redis.SIsMemberAsync("bpblack", r.Value.clientMetaData.Ip);
               if (inblack)
               {
                   r.Value.socket.Abort();
                   _clients.TryRemove(r.Key, out var x);
               }
           });
       }, null, 100, 10_000);
        new TimerX(async state =>
        {
            string[] keys = (await scanAllKeysAsync("heart:error:*")).ToArray();
            if (keys.Any())
            {
                await _redis.DelAsync(keys);
            }
        }, null, 100, 60 * 60_000);
        string[] tmps = scanAllKeys("heart:check:*").ToArray();
        if (tmps.Any())
        {
            _redis.Del(tmps);
        }
        tmps = scanAllKeys($"connect:*").ToArray();
        if (tmps.Any())
        {
            _redis.Del(tmps);
        }
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    foreach (Action action in ProcessCollection.GetConsumingEnumerable())
                    {
                        await Task.Run(action);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        });
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    if (CheckLostClients.Any())
                    {
                        HashSet<ImServerClient> hashClients = new HashSet<ImServerClient>();
                        while (CheckLostClients.TryDequeue(out var tmp))
                        {
                            hashClients.Add(tmp);
                        }
                        if (hashClients.Any())
                        {
                            List<string> list = await scanAllKeysAsync("RR:*");
                            if (list.Any())
                            {
                                var roomIds = list.Select(r => r.Replace("RR:", "").ToLong()).Distinct().ToList();
                                if (roomIds.Any())
                                {
                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} 发送补漏消息 客户[{hashClients.Count}] 礼物数[{roomIds.Count}]");
                                    string encodeJs = giftJs(roomIds).encodeJs();
                                    Message message = new Message()
                                    {
                                        type = "common",
                                        data = encodeJs
                                    };
                                    var msg = JsonConvert.SerializeObject(message);
                                    hashClients.AsParallel().ForAll(async client => { await SendStringAsync(client.socket, msg); });
                                }
                            }
                        }
                    }
                    await Task.Delay(5000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        });
        Task.Run(() =>
        {
            while (true)
            {
                string cmd = Console.ReadLine();
                var ops = cmd.Split(" ");
                string code = ops[0];
                if (code == "1")
                {
                    try
                    {
                        string[] strings = _clients.Values.OrderBy(r => r.clientMetaData.version).ThenBy(r => r.clientMetaData.Ip).ThenBy(r => r.clientMetaData.uid).Select(r =>
                        {
                            ImClientInfo client = r.clientMetaData;
                            return $"脚本版本：{client.version},心跳检测：{r.heartChecked},心跳成功：{r.heartSuccess},用户信息检测：{r.userChecked},风暴开关：{r.storm},请求Uid：{client.uid},回调Uid：{client.realUid},用户名：{client.uname},所在房间：{client.roomid},用户Ip：{client.Ip}";
                        }).ToArray();
                        File.WriteAllLines("onlineUsers.txt", strings);
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
                else if (code == "2")
                {
                    try
                    {
                        string[] keys = (scanAllKeys("heart:error:*")).ToArray();
                        var lines = keys.AsParallel().Select(r => $"{r}\t{_redis.Get(r)}").ToArray();
                        File.WriteAllLines("heartErrorUsers.txt", lines);
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
                else if (code == "3")
                {
                    try
                    {
                        if (ops.Length > 1)
                        {
                            long uid = ops[1].ToLong();
                            KeyValuePair<Guid, ImServerClient>[] client = _clients.Where(r => r.Value.clientMetaData.uid == uid || r.Value.clientMetaData.realUid == uid).ToArray();
                            if (client.Any())
                            {
                                foreach (var keyValuePair in client)
                                {
                                    keyValuePair.Value.socket.Abort();
                                    _clients.TryRemove(keyValuePair.Key, out var oldwslist);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
                else if (code == "4")
                {
                    try
                    {
                        long canEnterRoom = _redis.Get<long>("jroom");
                        KeyValuePair<Guid, ImServerClient>[] client = _clients.Where(r => r.Value.clientMetaData.roomid != canEnterRoom && r.Value.clientMetaData.roomid != 21438956
                        ).ToArray();
                        if (client.Any())
                        {
                            string[] strings = client.Select(r => r.Value).OrderBy(r => r.clientMetaData.version).ThenBy(r => r.clientMetaData.Ip).ThenBy(r => r.clientMetaData.uid).Select(r =>
                            {
                                ImClientInfo clientInfo = r.clientMetaData;
                                return $"脚本版本：{clientInfo.version},心跳检测：{r.heartChecked},心跳成功：{r.heartSuccess},用户信息检测：{r.userChecked},风暴开关：{r.storm},请求Uid：{clientInfo.uid},回调Uid：{clientInfo.realUid},用户名：{clientInfo.uname},所在房间：{clientInfo.roomid},用户Ip：{clientInfo.Ip}";
                            }).ToArray();
                            File.WriteAllLines("blackUsers.txt", strings);
                        }
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
                else if (code == "44")
                {
                    try
                    {
                        _clients.Where(r => r.Value.connetcDateTime.AddSeconds(20) < DateTime.Now && r.Value.clientMetaData.realUid == 0).AsParallel().ForAll(r =>
                        {
                            r.Value.socket.Abort();
                            _clients.TryRemove(r.Key, out var x);
                        });
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
                else if (code == "tp")
                {
                    try
                    {
                        if (ops.Length > 1)
                        {
                            long tpRoom = ops[1].ToLong();
                            if (tpRoom > 0)
                            {
                                Console.WriteLine(tpRoom);
                                if (ops.Length > 2)
                                {
                                    long op = ops[2].ToLong();
                                    if (op == 1)
                                    {
                                        long canEnterRoom = _redis.Get<long>("jroom");
                                        KeyValuePair<Guid, ImServerClient>[] client = _clients.Where(r => r.Value.clientMetaData.roomid > 0
                                         && r.Value.clientMetaData.roomid != canEnterRoom && r.Value.clientMetaData.roomid != 21438956
                                        ).ToArray();
                                        if (client.Any())
                                        {
                                            string encodeJs = jumpToRoom(tpRoom).encodeJs();
                                            Message message = new Message()
                                            {
                                                type = "common",
                                                data = encodeJs
                                            };
                                            var msg = JsonConvert.SerializeObject(message);
                                            client.AsParallel().ForAll(async keyValuePair => { await SendStringAsync(keyValuePair.Value.socket, msg); });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
                else if (code == "on")
                {
                    if (_offlineClients.Any())
                    {
                        _offlineClients.AsParallel().ForAll(keyValuePair =>
                        {
                            keyValuePair.Value.socket.Abort();
                            _offlineClients.TryRemove(keyValuePair.Key, out var old);
                        });
                    }
                }
                else if (code == "off")
                {
                    if (_offlineClients.Any())
                    {
                        string[] strings = _offlineClients.Values.OrderBy(r => r.clientMetaData.version).ThenBy(r => r.clientMetaData.Ip).ThenBy(r => r.clientMetaData.uid).Select(r =>
                        {
                            ImClientInfo client = r.clientMetaData;
                            return $"脚本版本：{client.version},请求Uid：{client.uid},用户Ip：{client.Ip}";
                        }).ToArray();
                        File.WriteAllLines("offlineUsers.txt", strings);
                    }
                }
                else if (code == "999")
                {
                    try
                    {
                        long canEnterRoom = _redis.Get<long>("jroom");
                        KeyValuePair<Guid, ImServerClient>[] client = _clients.Where(r => r.Value.clientMetaData.roomid > 0
                         && r.Value.clientMetaData.roomid != canEnterRoom && r.Value.clientMetaData.roomid != 21438956
                        ).ToArray();
                        if (client.Any())
                        {
                            foreach (var keyValuePair in client)
                            {
                                _redis.SAdd("bpblack", keyValuePair.Value.wsIp);
                                keyValuePair.Value.socket.Abort();
                                _clients.TryRemove(keyValuePair.Key, out var oldwslist);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteException(e);
                    }
                }
            }
        });
        _redis.Subscribe(($"{_redisPrefix}Server{_server}", RedisSubScribleMessage));
    }

    static ConcurrentQueue<ImServerClient> CheckLostClients = new ConcurrentQueue<ImServerClient>();

    List<string> scanAllKeys(string pattern)
    {
        List<string> keys = new List<string>();
        long cursor = 0;
        do
        {
            RedisScan<string> redisScan = _redis.Scan(cursor, pattern);
            if (redisScan.Items.Any())
            {
                keys.AddRange(redisScan.Items);
            }
            cursor = redisScan.Cursor;
        } while (cursor != 0);
        return keys;
    }

    async Task<List<string>> keysAsync(string pattern)
    {
        List<string> keys = new List<string>();
        string[] keysAsync = await _redis.KeysAsync(pattern);
        if (keysAsync.Any())
        {
            keys.AddRange(keysAsync);
        }
        return keys;
    }

    async Task<List<string>> scanAllKeysAsync(string pattern)
    {
        List<string> keys = new List<string>();
        long cursor = 0;
        do
        {
            RedisScan<string> redisScan = await _redis.ScanAsync(cursor, pattern);
            if (redisScan.Items.Any())
            {
                keys.AddRange(redisScan.Items);
            }
            cursor = redisScan.Cursor;
        } while (cursor != 0);
        return keys;
    }

    const int BufferSize = 4096;
    public ConcurrentDictionary<Guid, ImServerClient> _clients = new ConcurrentDictionary<Guid, ImServerClient>();
    public ConcurrentDictionary<Guid, ImServerClient> _offlineClients = new ConcurrentDictionary<Guid, ImServerClient>();
    public BlockingCollection<Action> ProcessCollection = new BlockingCollection<Action>();
    public BlockingCollection<Action> ProcessCollectionUsers = new BlockingCollection<Action>();
    private List<long> hearErrorUids = new List<long>();
    private HashSet<string> blackIps = new HashSet<string>();

    public class ImServerClient
    {
        public WebSocket socket;
        public Guid clientId => clientMetaData.clientId;
        public ImClientInfo clientMetaData;
        public bool userChecked { get; set; }
        public bool heartChecked { get; set; }
        public bool heartSuccess { get; set; }
        public string ticket { get; set; }
        public string wsIp { get; set; }
        public DateTime heartSuccessLimit { get; set; } = DateTime.MinValue;
        public DateTime connetcDateTime { get; set; }
        public string heartKey { get; set; }
        public bool storm { get; set; }

        public SemaphoreSlim readLock = new SemaphoreSlim(1, 1);

        public ImServerClient(WebSocket socket, ImClientInfo clientMetaData)
        {
            this.socket = socket;
            this.clientMetaData = clientMetaData;
            this.connetcDateTime = DateTime.Now;
        }

        protected bool Equals(ImServerClient other)
        {
            return Equals(socket, other.socket) && clientId.Equals(other.clientId) && string.Equals(clientMetaData, other.clientMetaData);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (socket != null ? socket.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ clientId.GetHashCode();
                hashCode = (hashCode * 397) ^ (clientMetaData != null ? clientMetaData.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    private static Version limitVer
    {
        get
        {
            if (Version.TryParse(JsonConfig<ManagerOptions>.Current.limitVer, out Version tmp))
            {
                return tmp;
            }
            return new Version("2.4.4.5");
        }
    }

    internal async Task Acceptor(HttpContext context, Func<Task> next)
    {
        //Console.WriteLine($"context.WebSockets.IsWebSocketRequest:{context.WebSockets.IsWebSocketRequest}");
        if (!context.WebSockets.IsWebSocketRequest) return;
        try
        {
            string ip = context.Request.Headers?["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress.ToString();
            string UA = context.Request.Headers?["User-Agent"].FirstOrDefault();
            if (UA == null)
            {
                XTrace.WriteLine($"无UA恶意连接,{ip}");
                await _redis.SAddAsync("bpblack", ip);
                return;
            }
            if (UA.ToLower().Contains("firefox")) return;
            //string[] sMembers = await _redis.SMembersAsync("blacklist");
            bool inblack = await _redis.SIsMemberAsync("blacklist", ip);
            if (inblack)
            {
                if (!blackIps.Contains(ip))
                {
                    blackIps.Add(ip);
                    Console.WriteLine($"检测到多次异常黑名单IP尝试访问:{ip}");
                }
                return;
            }
            string token = context.Request.Query["token"];
            var origin = context.Request.Headers["Origin"];
            if (string.IsNullOrEmpty(token)) return;
            int errCount = await _redis.HGetAsync<int>("preBlack", ip);
            var token_value = await _redis.GetAsync($"{_redisPrefix}Token{token}");
            await _redis.DelAsync($"{_redisPrefix}Token{token}");
            if (string.IsNullOrEmpty(token_value))
            {
                await _redis.HIncrByAsync("preBlack", ip);
                if (errCount >= 3)
                {
                    Console.WriteLine($"{ip}频繁错误！！！次数:{errCount}");
                    await _redis.SAddAsync("blacklist", ip);
                    await _redis.HDelAsync("preBlack", ip);
                }
                return;
                //                throw new Exception($@"
                //授权错误：用户需通过 ImHelper.PrevConnectServer 获得包含 token 的连接
                //IP:{ip},
                //origin:{origin},
                //token:{token},
                //Header:{string.Join("&", context.Request.Headers.Select(r => $"{r.Key}={r.Value}"))},
                //Param:{string.Join("&", context.Request.Query.Select(r => $"{r.Key}={r.Value}"))},
                //");
            }
            blackIps.Remove(ip);
            await _redis.HDelAsync("preBlack", ip);
            var data = JsonConvert.DeserializeObject<ImClientInfo>(token_value);

            //Console.WriteLine($"客户{data.clientId}接入:{data.clientMetaData}");
            CancellationToken ct = context.RequestAborted;
            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

            var cli = new ImServerClient(socket, data);
            cli.wsIp = ip;

            if (cli.clientMetaData.version <= limitVer)
            {
                await SendStringAsync(socket, new Message()
                {
                    type = "msg",
                    data = "当前版本过低，该脚本已从GreasyFork下架。<br>如果仍要使用可前往 github 寻找更新<br>但是请注意！新脚本包含恶意代码，本人并不希望你继续使用，谢谢"
                });
                socket.Abort();
                return;
            }
            if (_clients.Any(r => r.Value.clientMetaData.uid == data.uid && r.Value.clientMetaData.realUid == 0 && r.Value.connetcDateTime.AddMinutes(1) >= DateTime.Now))
            {
                await SendStringAsync(socket, new Message() { type = "common", data = toastjs("请勿重复连接...", "info", 10_000).encodeJs() });
                socket.Abort();
                return;
            }
            else
            {
                _clients.Where(r => r.Value.clientMetaData.uid == data.uid && r.Value.clientMetaData.realUid == 0).AsParallel().ForAll(r =>
                {
                    r.Value.socket.Abort();
                    _clients.TryRemove(r.Key, out var x);
                });
            }
            if (ManagerOptions.Current.AdminUids.Contains(data.uid) || ManagerOptions.Current.openClient == 1)
            {
                var wslist = _clients.GetOrAdd(data.clientId, cli);
                if (!ManagerOptions.Current.AdminUids.Contains(data.uid))
                {
//                    await SendStringAsync(socket, new Message() { type = "common", data = toastjs("魔改服务器将于2020-08-13到期。", "info", 10_000).encodeJs() });
//                    await SendStringAsync(socket, new Message() { type = "common", data = toastjs("魔改脚本包含恶意代码，感谢头铁的您的使用和支持。", "info", 10_000).encodeJs() });
//                    await SendStringAsync(socket, new Message() { type = "common", data = toastjs("感谢 抖M DD们一直以来的捧场，HDDM有缘再会。", "info", 10_000).encodeJs() });
                    //await SendStringAsync(socket, new Message() { type = "common", data = toastjs("魔改脚本包含恶意代码，请您慎重考虑是否使用", "error", 10_000).encodeJs() });
                    //            await SendStringAsync(socket, new Message() {type = "common", data = toastjs("脚本因举报被从greasyfork下架删除", "error", 10_000).encodeJs()});
                    //            await SendStringAsync(socket, new Message() {type = "common", data = toastjs("目前脚本变更至：https://github.com/pjy612/Bilibili-LRHH", "error", 10_000).encodeJs()});
                    //            await SendStringAsync(socket, new Message() {type = "common", data = toastjs("长江大佬分流：https://www.mlge.xyz/uncategorized/44.html", "error", 10_000).encodeJs()});
                }
                cli.ticket = Guid.NewGuid().ToString().Replace("-", "");
                await SendStringAsync(socket, new Message()
                {
                    type = "common",
                    data = postUserjs(cli.ticket).encodeJs()
                });
            }
            else
            {
                _offlineClients.GetOrAdd(data.clientId, cli);
                await SendStringAsync(socket, new Message() { type = "common", data = messagejs("BiLiPush维护中...").encodeJs() });
            }
            //            await Task.Run(() =>
            //            {
            //                try
            //                {
            //                    _redis.StartPipe().HSet($"{_redisPrefix}OnlineData", data.clientId.ToString(), data.clientMetaData)
            //                        .HIncrBy($"{_redisPrefix}Online", data.clientId.ToString(), 1)
            //                        .Publish($"evt_{_redisPrefix}Online", token_value).EndPipe();
            //                }
            //                catch (Exception e)
            //                {
            //                    Console.WriteLine(e.Message);
            //                }
            //            });
            //Console.WriteLine($"当前客户数：{_clients.Count}");
            //await SyncOnlineData();
            var buffer = new byte[BufferSize];
            var seg = new ArraySegment<byte>(buffer);
            try
            {
                while (true)
                {
                    try
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }
                        if (socket.State != WebSocketState.Open || socket.CloseStatus.HasValue)
                        {
                            break;
                        }
                        if (!_clients.ContainsKey(data.clientId) && !_offlineClients.ContainsKey(data.clientId))
                        {
                            break;
                        }
                        //var incoming = await socket.ReceiveAsync(seg, CancellationToken.None);
                        var Receive = await ReceiveStringAsync(socket, ct);
                        if (Receive == "ping")
                        {
                            continue;
                        }
                        int code = await ReceiveData(cli, Receive);
                        if (code == 0)
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                    }
                    catch (WebSocketException e)
                    {
                        XTrace.WriteLine($"{cli.clientMetaData.realUid} WebSocketException [{socket.State}][{socket.CloseStatus}]{socket.CloseStatusDescription}");
                        //XTrace.WriteLine($"{cli.clientMetaData.uid} ReceiveData WebSocketException Error!");
                        XTrace.WriteException(e);
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteLine($"{cli.clientMetaData.uid} ReceiveData Exception Error!");
                        XTrace.WriteException(ex);
                    }
                }
            }
            catch (WebSocketException e)
            {
                XTrace.WriteLine($"{cli.clientMetaData.uid} WebSocket WebSocketException!");
                XTrace.WriteException(e);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"{cli.clientMetaData.uid} WebSocket Exception!");
                XTrace.WriteException(ex);
            }
            finally
            {
                socket.Abort();
            }
            //XTrace.WriteLine($"{data.realUid},{data.uname},{data.roomid} 掉线断开，原因:[{socket.State}][{socket.CloseStatus}]{socket.CloseStatusDescription}");
            _clients.TryRemove(data.clientId, out var oldclient);
            _offlineClients.TryRemove(data.clientId, out var oldoffclient);
            //await _redis.EvalAsync($"if redis.call('HINCRBY', KEYS[1], '{data.clientId}', '-1') <= 0 then redis.call('HDEL', KEYS[1], '{data.clientId}') end return 1",$"{_redisPrefix}Online");
            //LeaveChan(data.clientId, GetChanListByClientId(data.clientId));
            //await SyncOnlineData();
            //Console.WriteLine($"客户{data.clientId}下线:{data.clientMetaData}");
            //Console.WriteLine($"当前客户数：{_clients.Count}");
            //await _redis.PublishAsync($"evt_{_redisPrefix}Offline", token_value);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static List<long> heartErrorUids = new List<long>();
    private static SemaphoreSlim receiveLock = new SemaphoreSlim(100, 100);

    private async Task<int> ReceiveData(ImServerClient client, string msg)
    {
        Random random = new Random(Guid.NewGuid().GetHashCode());
        //await client.readLock.WaitAsync();
        //await receiveLock.WaitAsync();
        try
        {
            if (!msg.IsNullOrWhiteSpace())
            {
                var socket = client.socket;
                {
                    Message message = new Message();
                    try
                    {
                        message = JsonConvert.DeserializeObject<Message>(msg);
                    }
                    catch (Exception e)
                    {
                        XTrace.WriteLine($"异常请求！！！建议拉黑，{client.clientMetaData.Ip},{client.clientMetaData.uid}:{msg}");
                        XTrace.WriteException(e);
                        return 1;
                    }
                    if (message.type == "heart")
                    {
                        #region 心跳检测

                        //                        string heartdata = client.heartKey;
                        //                        if (heartdata.IsNullOrWhiteSpace()) return 1;
                        //                        long heartErrorCount = await _redis.GetAsync<long>($"heart:error:{client.clientMetaData.realUid}");
                        //                        if (heartErrorCount >= 5)
                        //                        {
                        //                            client.heartSuccess = false;
                        //                            await SendStringAsync(socket, new Message() { type = "common", data = toastjs("检测到异常推送已关闭", "error", 5_000).encodeJs() });
                        //                            if (!heartErrorUids.Contains(client.clientMetaData.realUid))
                        //                            {
                        //                                heartErrorUids.Add(client.clientMetaData.realUid);
                        //                                TimerX.Delay(async state =>
                        //                                {
                        //                                    heartErrorUids.Remove(client.clientMetaData.realUid);
                        //                                    await _redis.EvalAsync($"if redis.call('INCRBY', KEYS[1], '-1') <= 0 then redis.call('DEL', KEYS[1]) end return 1", $"heart:error:{client.clientMetaData.realUid}");
                        //                                }, 60000);
                        //                            }
                        //                            return 1;
                        //                        }
                        //                        if (message.data != heartdata)
                        //                        {
                        //                            client.heartSuccess = false;
                        //                            await SendStringAsync(socket, new Message() { type = "common", data = toastjs("检测到异常推送已关闭", "error", 5_000).encodeJs() });
                        //                            await _redis.IncrByAsync($"heart:error:{client.clientMetaData.realUid}");
                        //                        }
                        //                        else
                        //                        {
                        //                            client.heartSuccess = true;
                        //                            //client.heartSuccessLimit = DateTime.Now.AddMinutes(2);
                        //                            await _redis.EvalAsync($"if redis.call('INCRBY', KEYS[1], '-1') <= 0 then redis.call('DEL', KEYS[1]) end return 1", $"heart:error:{client.clientMetaData.realUid}");
                        //                        }
                        //                        //                        client.heartKey = $"{random.Next(999999)}";
                        //                        //                        await SendStringAsync(socket, new Message()
                        //                        //                        {
                        //                        //                            type = "common",
                        //                        //                            data = heartjs(client.heartKey).encodeJs()
                        //                        //                        });

                        #endregion
                    }
                    else if (message.type == "user_check")
                    {
                        try
                        {
                            if (client.ticket != message.token) return 1;
                            JToken messageData = message.data;
                            JToken extData = message.ext;
                            long uid = (messageData?.Value<long?>("uid")).ToLong();
                            long roomid = (messageData?.Value<long?>("roomid")).ToLong();
                            string uname = (messageData?.Value<string>("uname"));
                            bool storm = (messageData?.Value<bool>("storm")) ?? false;
                            //                            long extUid = (extData?.Value<long?>("uid")).ToLong();
                            //                            long extRoomId = (extData?.Value<long?>("roomid")).ToLong();
                            if (uid != client.clientMetaData.uid)
                            {
                                //                                if (!ManagerOptions.Current.AdminUids.Contains(uid))
                                //                                {
                                //                                    if (!ManagerOptions.Current.BadUids.Contains(uid))
                                //                                    {
                                //                                        ManagerOptions.Current.BadUids.Add(uid);
                                //                                        ManagerOptions.Current.SaveAsync();
                                //                                    }
                                //                                }
                                //XTrace.WriteLine($"回调疑似伪造:client:{client.clientMetaData.uid},req:{uid},{msg}");
                                //                                await _redis.SAddAsync("blacklist", client.clientMetaData.Ip);
                                await SendStringAsync(socket, new Message() { type = "common", data = flushPage.encodeJs() });
                                return 1;
                            }
                            if (roomid > 0)
                            {
                                client.clientMetaData.roomid = roomid;
                            }
                            if (!uname.IsNullOrWhiteSpace())
                            {
                                client.clientMetaData.uname = uname;
                            }
                            if (uid == 0)
                            {
                                await SendStringAsync(socket, new Message() { type = "common", data = flushPage.encodeJs() });
                                await SendStringAsync(socket, new Message() { type = "common", data = toastjs("检测到异常推送已关闭", "error", 5_000).encodeJs() });
                                return 1;
                            }
                            if (JsonConfig<ManagerOptions>.Current.BlackUids?.Contains(uid) == true)
                            {
                                await SendStringAsync(socket, new Message() { type = "common", data = flushPage.encodeJs() });
                                await SendStringAsync(socket, new Message() { type = "common", data = toastjs("检测到异常推送已关闭", "error", 5_000).encodeJs() });
                                return 1;
                            }
                            if (uid == client.clientMetaData.uid)
                            {
                                client.clientMetaData.realUid = uid;
                                client.storm = storm;
                                if (ManagerOptions.Current.jroom == 1)
                                {
                                    long canEnterRoom = await _redis.GetAsync<long>("jroom");
                                    if (!(roomid == 21438956 || (canEnterRoom > 0 && roomid == canEnterRoom)))
                                    {
                                        //string roomIds = new List<long>() {21438956, canEnterRoom}.Distinct().ToList().Join();
                                        //await SendStringAsync(socket, new Message() {type = "common", data = toastjs($"bilipush仅支持直播间{roomIds}", "error", 5_000).encodeJs()});
                                        await SendStringAsync(socket, new Message() { type = "common", data = jumpToRoom(21438956).encodeJs() });
                                        client.userChecked = false;
                                        return 1;
                                    }
                                    else
                                    {
                                        int ddtp = await _redis.GetAsync<int>("ddtp");
                                        if (ddtp == 1)
                                        {
                                            if (roomid != canEnterRoom && canEnterRoom > 0)
                                            {
                                                await SendStringAsync(socket, new Message() { type = "common", data = toastjs($"DD传送门已启动，准备进行折跃...", "info", 5_000).encodeJs() });
                                                await Task.Delay(2000);
                                                await SendStringAsync(socket, new Message() { type = "common", data = jumpToRoom(canEnterRoom).encodeJs() });
                                            }
                                            return 1;
                                        }
                                        else
                                        {
                                            if (roomid != canEnterRoom && canEnterRoom > 0)
                                            {
                                                if (!await _redis.SIsMemberAsync("jroomdone", uid))
                                                {
                                                    await SendStringAsync(socket, new Message() { type = "common", data = toastjs($"DD传送门已启动，准备进行折跃...", "info", 5_000).encodeJs() });
                                                    await Task.Delay(2000);
                                                    await SendStringAsync(socket, new Message() { type = "common", data = jumpToRoom(canEnterRoom).encodeJs() });
                                                    return 1;
                                                }
                                            }
                                            await _redis.SAddAsync("jroomdone", uid);
                                        }
                                    }
                                    if (roomid == canEnterRoom && roomid != 21438956)
                                    {
                                        await SendStringAsync(socket, new Message() { type = "common", data = toastjs("欢迎体验DD传送门...", "info", 10_000).encodeJs() });
                                    }
                                }
                                //                                if (JsonConfig<ManagerOptions>.Current.AdminUids?.Contains(uid) != true)
                                //                                {
                                //                                    //踢掉其他人
                                //                                    List<KeyValuePair<Guid, ImServerClient>> oldOnline = _clients.Where(r =>
                                //                                        (r.Value.clientId != client.clientId) && (r.Value.clientMetaData.uid == client.clientMetaData.realUid || r.Value.clientMetaData.realUid == client.clientMetaData.realUid)
                                //                                    ).ToList();
                                //                                    if (oldOnline.Any())
                                //                                    {
                                //                                        oldOnline.ForEach(async c =>
                                //                                        {
                                //                                            if (c.Value.clientMetaData.version > limitVer)
                                //                                            {
                                //                                                await SendStringAsync(c.Value.socket, new Message() {type = "common", data = toastjs("检测到其他端请求，bilipush推送已关闭！", "error", 5_000).encodeJs()});
                                //                                                c.Value.userChecked = false;
                                //                                            }
                                //                                        });
                                //                                    }
                                //                                }
                                if (ManagerOptions.Current.BlackUids?.Contains(client.clientMetaData.realUid) == true)
                                {
                                    await SendStringAsync(socket, new Message() { type = "common", data = toastjs("检测到异常推送已关闭", "error", 5_000).encodeJs() });
                                    return 1;
                                }
                                client.userChecked = client.heartSuccess = true;
                                bool checkLost = false;
                                if (ManagerOptions.Current.AdminUids.Contains(client.clientMetaData.realUid))
                                {
                                    checkLost = true;
                                }
                                else
                                {
//                                    string enterKey = $"connect:{client.clientMetaData.realUid}";
//                                    bool b = await _redis.SetAsync(enterKey, 1, TimeSpan.FromMinutes(5), RedisExistence.Nx);
//                                    long enterCount = 1;
//                                    if (!b)
//                                    {
//                                        enterCount = await _redis.IncrByAsync(enterKey, 1);
//                                    }
//                                    if (enterCount > 1)
//                                    {
//                                        if (ManagerOptions.Current.BadUids.Contains(client.clientMetaData.realUid))
//                                        {
//                                            //坏用户不用存
//                                        }
//                                        else
//                                        {
//                                            if (random.NextDouble() >= enterCount * 0.1)
//                                            {
                                                checkLost = true;
//                                            }
//                                        }
//                                    }
                                }
                                if (checkLost)
                                {
                                    if (!CheckLostClients.Contains(client))
                                        CheckLostClients.Enqueue(client);
                                    //                                    ProcessCollectionUsers.Add(async () =>
                                    //                                    {
                                    //                                        List<string> list = await keysAsync("RR:*");
                                    //                                        if (list.Any())
                                    //                                        {
                                    //                                            var roomIds = list.Select(r => r.Replace("RR:", "").ToLong()).Distinct().ToList();
                                    //                                            await SendStringAsync(socket, new Message()
                                    //                                            {
                                    //                                                type = "common",
                                    //                                                data = giftJs(roomIds).encodeJs()
                                    //                                            });
                                    //                                        }
                                    //                                    });
                                }
                                //                                client.heartChecked = true;
                                //                                if (JsonConfig<ManagerOptions>.Current.AdminUids?.Contains(uid) == true)
                                //                                {
                                //                                    await _redis.DelAsync($"heart:error:{client.clientMetaData.realUid}");
                                //                                }
                                //                                await _redis.EvalAsync($"if redis.call('INCRBY', KEYS[1], '-1') <= 0 then redis.call('DEL', KEYS[1]) end return 1", $"heart:error:{client.clientMetaData.realUid}");
                                //                                string heartdata = client.heartKey;
                                //                                if (heartdata.IsNullOrWhiteSpace())
                                //                                {
                                //                                    client.heartKey = $"{random.Next(999999)}";
                                //                                }
                                //                                await SendStringAsync(socket, new Message()
                                //                                {
                                //                                    type = "common",
                                //                                    data = heartOncejs(client.heartKey).encodeJs()
                                //                                });
                            }
                            else
                            {
                                await SendStringAsync(socket, new Message() { type = "common", data = toastjs("检测到异常推送已关闭", "error", 5_000).encodeJs() });
                                XTrace.WriteLine($"用户UID异常回调疑似伪造:client:{client.clientMetaData.uid},req:{uid},{msg}");
                                if (uid > 0 && !ManagerOptions.Current.AdminUids.Contains(uid))
                                {
                                    if (!ManagerOptions.Current.BadUids.Contains(uid))
                                    {
                                        ManagerOptions.Current.BadUids.Add(uid);
                                        ManagerOptions.Current.SaveAsync();
                                    }
                                }
                                return 1;
                            }
                        }
                        catch (Exception e)
                        {
                            XTrace.WriteLine($"userInfo Json请求异常！{msg}");
                            XTrace.WriteException(e);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            XTrace.WriteException(e);
            //return 1;
        }
        finally
        {
            //client.readLock.Release();
            //receiveLock.Release();
        }
        return 1;
    }

    private async Task SendStringAsync(System.Net.WebSockets.WebSocket socket, Message data)
    {
        await SendStringAsync(socket, JsonConvert.SerializeObject(data));
    }

    private async Task<string> ReceiveStringAsync(System.Net.WebSockets.WebSocket socket, CancellationToken ct = default(CancellationToken))
    {
        var buffer = new ArraySegment<byte>(new byte[BufferSize]);
        using (var ms = new MemoryStream())
        {
            WebSocketReceiveResult result;
            try
            {
                do
                {
                    ct.ThrowIfCancellationRequested();
                    result = await socket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                } while (!result.CloseStatus.HasValue && socket.State != WebSocketState.Aborted && !result.EndOfMessage);
            }
            catch (OperationCanceledException e)
            {
                return null;
            }

            ms.Seek(0, SeekOrigin.Begin);
            if (result.MessageType != WebSocketMessageType.Text)
            {
                return null;
            }

            using (var reader = new StreamReader(ms, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }

    private async Task SendStringAsync(System.Net.WebSockets.WebSocket socket, object data, CancellationToken ct = default(CancellationToken))
    {
        try
        {
            string messagejson = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(messagejson);
            var segment = new ArraySegment<byte>(buffer);
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
            }
        }
        catch (Exception e)
        {
        }
    }
    //    async Task SyncOnlineData()
    //    {
    //        string key = $"{_redisPrefix}OnlineData";
    //        try
    //        {
    //            Dictionary<string, string> hGetAll = await _redis.HGetAllAsync(key) ?? new Dictionary<string, string>();
    //            using (var pipe = _redis.StartPipe())
    //            {
    ////                List<ImServerClient> clientList = _clients.Where(r => !hGetAll.ContainsKey(r.Key.ToString())).SelectMany(r => r.Value.Values).Distinct().ToList();
    ////                object[] param = clientList.Where(r => !hGetAll.ContainsKey(r.clientId.ToString())).SelectMany(r => new object[] {r.clientId, r.clientMetaData}).ToArray();
    ////                if (param.Any())
    ////                {
    ////                    pipe.HMSet(key, param);
    ////                }
    ////                var outKeys = hGetAll.Keys.Where(k => _clients.Keys.All(c => c.ToString() != k)).ToArray();
    ////                if (outKeys.Any()) pipe.HDel(key, outKeys);
    //            }
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e.Message);
    //        }
    //    }

    internal void RedisSubScribleMessage(CSRedis.CSRedisClient.SubscribeMessageEventArgs e)
    {
        var data = JsonConvert.DeserializeObject<(Guid senderClientId, Guid[] receiveClientId, string content, bool receipt)>(e.Body);
        Trace.WriteLine($"收到消息：{data.content}");
        var outgoing = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data.content));
        bool publishAll = false;
        try
        {
            Message o = JsonConvert.DeserializeObject<Message>(JsonConvert.DeserializeObject<string>(data.content));
            if (o.type == "raffle")
            {
                if (o.data.raffle_type == "STORM")
                {   
                    data.receiveClientId = _clients.Where(r => r.Value.storm).Select(r => r.Key).ToArray();
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} 收到需推送 风暴消息[{data.receiveClientId.Length}]：{data.content}");
                    Task.Run(() => { PublishMessage(data, publishAll, outgoing); });
                    return;
                }
            }
            else
            {
                publishAll = true;
            }
        }
        catch
        {
        }
        if (!data.receiveClientId.Any())
        {
            data.receiveClientId = _clients.Keys.ToArray();
        }
        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{(publishAll ? "【全服广播】" : "")}收到需推送消息 [{data.receiveClientId.Length}] ：{data.content} {(data.receipt ? "【需回执】" : "")}");
        ProcessCollection.Add(() => { PublishMessage(data, publishAll, outgoing); });
    }

    private void PublishMessage((Guid senderClientId, Guid[] receiveClientId, string content, bool receipt) data, bool publishAll, ArraySegment<byte> outgoing)
    {
        try
        {

            Random random = new Random(Guid.NewGuid().GetHashCode());
            long canEnterRoom = _redis.Get<long>("jroom");
            data.receiveClientId.AsParallel().ForAll(async clientId =>
            {
                if (_clients.TryGetValue(clientId, out var client) == false)
                {
                    //Console.WriteLine($"websocket{clientId} 离线了，{data.content}" + (data.receipt ? "【需回执】" : ""));
                    if (data.senderClientId != Guid.Empty && clientId != data.senderClientId && data.receipt)
                        SendMessage(clientId, new[] { data.senderClientId }, new
                        {
                            data.content,
                            receipt = "用户不在线"
                        });
                    return;
                }
                try
                {
                    if (publishAll)
                    {
                        await client.socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None);
                        return;
                    }
                    if (client.clientMetaData.realUid == 0) return;
                    if (JsonConfig<ManagerOptions>.Current.AdminUids?.Contains(client.clientMetaData.realUid) != true)
                    {
                        //没跟上队伍的才进行跳转和记录 一致的就不管了
                        if (canEnterRoom != client.clientMetaData.roomid)
                        {
                            if (!await _redis.SIsMemberAsync("jroomdone", client.clientMetaData.realUid))
                            {
                                await SendStringAsync(client.socket, new Message() { type = "common", data = toastjs("检测到您已脱离DD队伍，礼物推送停止，请自行追赶...", "info", 2000).encodeJs() });
                                return;
                            }
                        }
                        long heartErrorCount = await _redis.GetAsync<long>($"heart:error:{client.clientMetaData.realUid}");
                        if (heartErrorCount >= 5)
                        {
                            return;
                        }
                        //新版且没检测心跳 禁用
                        //                            if (client.socket.State == WebSocketState.Open && client.heartSuccessLimit <= DateTime.Now && client.clientMetaData.version > limitVer)
                        //                            {
                        //                                if (client.heartSuccess)
                        //                                {
                        //                                    if (await _redis.ExistsAsync($"heart:lock:{client.clientMetaData.realUid}")) continue;
                        //                                    await _redis.SetAsync($"heart:lock:{client.clientMetaData.realUid}", 0, 3, RedisExistence.Nx);
                        //                                    await _redis.IncrByAsync($"heart:error:{client.clientMetaData.realUid}");
                        //                                }
                        //                                continue;
                        //                            }
                        //黑名单不推送
                        if (JsonConfig<ManagerOptions>.Current.BlackUids.Contains(client.clientMetaData.realUid))
                        {
                            return;
                        }
                        if (ManagerOptions.Current.BadUids.Contains(client.clientMetaData.realUid))
                        {
                            if (!(random.NextDouble() <= ManagerOptions.Current.badRate))
                            {
                                return;
                            }
                        }
                        if (!client.userChecked || !client.heartSuccess) return;
                    }
                    //如果接收消息人是发送者，并且接收者只有1个以下，则不发送
                    //只有接收者为多端时，才转发消息通知其他端
                    if (clientId == data.senderClientId) return;
                    //关闭的不发送
                    if (client.socket.State != WebSocketState.Open)
                    {
                        return;
                    }
                    await client.socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None);
                    if (data.senderClientId != Guid.Empty && clientId != data.senderClientId && data.receipt)
                        SendMessage(clientId, new[] { data.senderClientId }, new
                        {
                            data.content,
                            receipt = "发送成功"
                        });
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    XTrace.WriteLine($"{client.clientMetaData.realUid} 发送失败");
                    XTrace.WriteException(ex);
                }
            });
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"订阅方法出错了：{ex.Message}");
            XTrace.WriteException(ex);
        }
    }
}

[JsonConfigFile("server.json", 10_000)]
public class ManagerOptions : JsonConfig<ManagerOptions>
{
    public ManagerOptions()
    {
        BlackUids = new List<long>();
        AdminUids = new List<long>();
        BadUids = new List<long>();
        openTime = new List<OpenTime>();
    }

    public List<long> BlackUids { get; set; }
    public List<long> BadUids { get; set; }
    public double badRate { get; set; } = 0.3;
    public List<long> AdminUids { get; set; }
    public string limitVer { get; set; } = "2.4.4.5";
    public string lastVer { get; set; } = "2.4.4.9";
    public int openClient { get; set; } = 1;
    public double breakLimit { get; set; } = 1;
    public int jroom { get; set; } = 1;
    public List<OpenTime> openTime { get; set; }
}

public class OpenTime
{
    public DateTime s { get; set; }
    public DateTime e { get; set; }
}
#endif