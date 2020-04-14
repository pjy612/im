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
                appcur.UseWebSockets();
            }
            appcur.Use((ctx, next) =>
                imserv.Acceptor(ctx, next));
        });
        return app;
    }

    public static readonly ECMAScriptPacker packer = new ECMAScriptPacker(ECMAScriptPacker.PackerEncoding.Numeric, true, false);

    public static string encodeJs(this string js)
    {
        return packer.Pack(js);
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
    public dynamic data { get; set; }
    public dynamic ext { get; set; }
}

class ImServer : ImClient
{
    protected string _server { get; set; }

    public ImServer(ImServerOptions options) : base(options)
    {
        _server = options.Server;
        new TimerX(state => { Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} 当前客户数：{_clients.Count}"); }, null, 1000, 5000) {Async = true};
        new TimerX(async state =>
        {
            blackIps.Clear();
            await _redis.DelAsync("preBlack", "blacklist");
        }, null, 100, 10 * 60_000) {Async = true};
        new TimerX(async state =>
        {
            string[] keys = (await scanAllKeysAsync("heart:error:*")).ToArray();
            if (keys.Any())
            {
                await _redis.DelAsync(keys);
            }
        }, null, 100, 60 * 60_000) {Async = true};
        string[] tmps = scanAllKeys("heart:check:*").ToArray();
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
                    foreach (var action in ProcessCollectionUsers.GetConsumingEnumerable())
                    {
                        await Task.Run(action);
                        await Task.Delay(100);
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
                string cmd = Console.ReadLine();
                var ops = cmd.Split(" ");
                string code = ops[0];
                if (code == "1")
                {
                    string[] strings = _clients.Values.OrderBy(r => r.clientMetaData.version).ThenBy(r => r.clientMetaData.Ip).ThenBy(r => r.clientMetaData.uid).Select(r =>
                    {
                        ImClientInfo client = r.clientMetaData;
                        return $"{client.version},{r.heartChecked},{client.uid},{client.realUid},{client.uname},{client.Ip}";
                    }).ToArray();
                    File.WriteAllLines("onlineUsers.txt", strings);
                }
                else if (code == "2")
                {
                    string[] keys = (await scanAllKeysAsync("heart:error:*")).ToArray();
                    var lines = keys.AsParallel().Select(r => $"{r}\t{_redis.Get(r)}").ToArray();
                    File.WriteAllLines("heartErrorUsers.txt", lines);
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
            }
        });
        _redis.Subscribe(($"{_redisPrefix}Server{_server}", RedisSubScribleMessage));
    }

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
    public BlockingCollection<Action> ProcessCollection = new BlockingCollection<Action>();
    public BlockingCollection<Action> ProcessCollectionUsers = new BlockingCollection<Action>();
    private List<long> hearErrorUids = new List<long>();
    private HashSet<string> blackIps = new HashSet<string>();

    public class ImServerClient
    {
        public WebSocket socket;
        public Guid clientId => clientMetaData.clientId;
        public ImClientInfo clientMetaData;
        public bool heartChecked { get; set; }
        public bool heartSuccess { get; set; }
        public DateTime heartSuccessLimit { get; set; } = DateTime.MinValue;

        public ImServerClient(WebSocket socket, ImClientInfo clientMetaData)
        {
            this.socket         = socket;
            this.clientMetaData = clientMetaData;
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
            string ip = context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress.ToString();
            string UA = context.Request.Headers["User-Agent"].FirstOrDefault();
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
            await _redis.DelAsync($"{_redisPrefix}Token{token}");
            blackIps.Remove(ip);
            await _redis.HDelAsync("preBlack", ip);
            var data = JsonConvert.DeserializeObject<ImClientInfo>(token_value);

            //Console.WriteLine($"客户{data.clientId}接入:{data.clientMetaData}");
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var cli = new ImServerClient(socket, data);
            var wslist = _clients.GetOrAdd(data.clientId, cli);
            if (cli.clientMetaData.version > limitVer)
            {
                await SendStringAsync(socket, new Message()
                {
                    type = "common",
                    data = postUserjs().encodeJs()
                });
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
                while (socket.State == WebSocketState.Open && _clients.ContainsKey(data.clientId))
                {
                    var incoming = await ReceiveStringAsync(socket);
                    int code = await ReceiveData(cli, incoming);
                    if (code == 0)
                    {
                        break;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                socket.Abort();
            }
            _clients.TryRemove(data.clientId, out var oldwslist);
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

    private async Task<int> ReceiveData(ImServerClient client, string msg)
    {
        string heartKey = $"heart:check:{client.clientMetaData.uid}";
        try
        {
            Monitor.Enter(client);
            if (!msg.IsNullOrWhiteSpace())
            {
                var socket = client.socket;
                if (msg == "ping")
                {
                    await SendStringAsync(socket, new Message()
                    {
                        type = "pong"
                    });
                }
                else
                {
                    Message message = JsonConvert.DeserializeObject<Message>(msg);
                    if (message.type == "heart")
                    {
                        string heartdata = await _redis.GetAsync<string>(heartKey);
                        if (heartdata.IsNullOrWhiteSpace()) return 0;
                        if (message.data != heartdata)
                        {
                            long heartErrorCount = await _redis.GetAsync<long>($"heart:error:{client.clientMetaData.realUid}");
                            if (heartErrorCount >= 5)
                            {
                                await SendStringAsync(socket, new Message() {type = "common", data = toastjs("限制连接，请稍后重试", "error", 5_000)});
                                client.socket.Abort();
                                _clients.TryRemove(client.clientId, out var oldwslist);
                                TimerX.Delay(async state => { await _redis.EvalAsync($"if redis.call('INCRBY', KEYS[1], '-1') <= 0 then redis.call('DEL', KEYS[1]) end return 1", $"heart:error:{client.clientMetaData.realUid}"); }, 60000);
                                return 1;
                            }
                            else
                            {
                                await _redis.IncrByAsync($"heart:error:{client.clientMetaData.realUid}");
                            }
                            XTrace.WriteLine($"{client.clientMetaData.uid},{client.clientMetaData.realUid},{client.clientMetaData.Ip},[db:{heartdata}]:[req:{message.data}]");
                            return 1;
                        }
                        client.heartSuccess      = true;
                        client.heartSuccessLimit = DateTime.Now.AddMinutes(2);
                        await _redis.EvalAsync($"if redis.call('INCRBY', KEYS[1], '-1') <= 0 then redis.call('DEL', KEYS[1]) end return 1", $"heart:error:{client.clientMetaData.realUid}");
                        var value = $"{new Random().Next(999999)}";
                        await _redis.SetAsync(heartKey, value, 50);
                        await SendStringAsync(socket, new Message()
                        {
                            type = "common",
                            data = heartjs(value)
                        });
                    }
                    else if (message.type == "userInfo")
                    {
                        JToken messageData = message.data;
                        JToken extData = message.ext;
                        long uid = (messageData?.Value<long>("UID")).ToLong(); 
                        long roomid = (messageData?.Value<long>("ROOMID")).ToLong();
                        long extUid = (extData?.Value<long>("uid")).ToLong();
                        long extRoomId = (extData?.Value<long>("roomid")).ToLong();
                        if (uid != extUid && roomid != extRoomId && extUid > 0 && extRoomId > 0)
                        {
                            XTrace.WriteLine($"回调疑似伪造:client:{client.clientMetaData.uid},req:{message.data?["UID"]},{msg}");
                            //await SendStringAsync(socket, new Message() { type = "common", data = toastjs("拒绝访问", "error", 5_000) });
                            return 1;
                        }
                        if (JsonConfig<ManagerOptions>.Current.BlackUids?.Contains(extUid) == true)
                        {
                            await SendStringAsync(socket, new Message() { type = "common", data = toastjs("拒绝访问", "error", 5_000) });
                            return 0;
                        }
                        if (uid == client.clientMetaData.uid && client.clientMetaData.uid == extUid)
                        {
                            client.clientMetaData.realUid = extUid;
                            if (roomid != 21438956)
                            {
                                XTrace.WriteLine($"用户ROOMID异常回调疑似伪造:client:{client.clientMetaData.uid},req:{message.data?["UID"]},{msg}");
                                await SendStringAsync(socket, new Message() {type = "common", data = toastjs("拒绝访问", "error", 5_000)});
                                return 0;
                            }
                            //踢掉其他人
                            List<KeyValuePair<Guid, ImServerClient>> oldOnline = _clients.Where(r =>
                                (r.Value.clientId != client.clientId) && (r.Value.clientMetaData.uid == client.clientMetaData.realUid || r.Value.clientMetaData.realUid == client.clientMetaData.realUid)
                            ).ToList();
                            if (oldOnline.Any())
                            {
                                oldOnline.ForEach(async c =>
                                {
                                    if (c.Value.clientMetaData.version > limitVer)
                                    {
                                        await SendStringAsync(c.Value.socket, new Message() {type = "common", data = toastjs("检测到其他端请求，bilipush断开连接！", "error", 5_000)});
                                        c.Value.socket.Abort();
                                        _clients.TryRemove(c.Key, out var tmp);
                                    }
                                });
                            }
                            if (JsonConfig<ManagerOptions>.Current.BlackUids?.Contains(client.clientMetaData.realUid) == true)
                            {
                                await SendStringAsync(socket, new Message() {type = "common", data = toastjs("拒绝访问", "error", 5_000)});
                                return 0;
                            }
                            ProcessCollectionUsers.Add(async () =>
                            {
                                List<string> list = await keysAsync("RR:*");
                                if (list.Any())
                                {
                                    var roomIds = list.Select(r => r.Replace("RR:", "").ToLong()).Distinct().ToList();
                                    await SendStringAsync(socket, new Message()
                                    {
                                        type = "common",
                                        data = giftJs(roomIds)
                                    });
                                }
                            });
                            client.heartChecked = true;
                            string value = $"{new Random().Next(999999)}";
                            string heartdata = await _redis.GetAsync<string>(heartKey);
                            if (heartdata.IsNullOrWhiteSpace())
                            {
                                await _redis.SetAsync(heartKey, value);
                            }
                            else
                            {
                                value = heartdata;
                            }
                            await SendStringAsync(socket, new Message()
                            {
                                type = "common",
                                data = heartOncejs(value)
                            });
                        }
                        else
                        {
                            await SendStringAsync(socket, new Message() {type = "common", data = toastjs("拒绝访问", "error", 5_000)});
                            XTrace.WriteLine($"用户UID异常回调疑似伪造:client:{client.clientMetaData.uid},req:{message.data?["UID"]},{msg}");
                            return 0;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            XTrace.WriteException(e);
            return 0;
        }
        finally
        {
            Monitor.Exit(client);
        }
        return 1;
    }

    private Task SendStringAsync(System.Net.WebSockets.WebSocket socket, Message data)
    {
        return SendStringAsync(socket, JsonConvert.SerializeObject(data));
    }

    private async Task<string> ReceiveStringAsync(System.Net.WebSockets.WebSocket socket, CancellationToken ct = default(CancellationToken))
    {
        var buffer = new ArraySegment<byte>(new byte[BufferSize]);
        using (var ms = new MemoryStream())
        {
            WebSocketReceiveResult result;
            do
            {
                ct.ThrowIfCancellationRequested();

                result = await socket.ReceiveAsync(buffer, ct);
                ms.Write(buffer.Array, buffer.Offset, result.Count);
            } while (!result.EndOfMessage);

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

    private Task SendStringAsync(System.Net.WebSockets.WebSocket socket, object data, CancellationToken ct = default(CancellationToken))
    {
        string messagejson = JsonConvert.SerializeObject(data);
        var buffer = Encoding.UTF8.GetBytes(messagejson);
        var segment = new ArraySegment<byte>(buffer);
        if (socket.State == WebSocketState.Open)
        {
            return socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
        }
        return Task.CompletedTask;
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
        ProcessCollection.Add(async () =>
        {
            try
            {
                var data = JsonConvert.DeserializeObject<(Guid senderClientId, Guid[] receiveClientId, string content, bool receipt)>(e.Body);
                Trace.WriteLine($"收到消息：{data.content}"                                             + (data.receipt ? "【需回执】" : ""));
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} 收到需推送消息：{data.content}" + (data.receipt ? "【需回执】" : ""));

                var outgoing = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data.content));
                if (!data.receiveClientId.Any())
                {
                    data.receiveClientId = _clients.Keys.ToArray();
                }
                foreach (var clientId in data.receiveClientId)
                {
                    if (_clients.TryGetValue(clientId, out var client) == false)
                    {
                        //Console.WriteLine($"websocket{clientId} 离线了，{data.content}" + (data.receipt ? "【需回执】" : ""));
                        if (data.senderClientId != Guid.Empty && clientId != data.senderClientId && data.receipt)
                            SendMessage(clientId, new[] {data.senderClientId}, new
                            {
                                data.content,
                                receipt = "用户不在线"
                            });
                        continue;
                    }
                    try
                    {
                        if (client.clientMetaData.realUid == 0) continue;
                        long heartErrorCount = await _redis.GetAsync<long>($"heart:error:{client.clientMetaData.realUid}");
                        if (heartErrorCount >= 5)
                        {
                            client.socket.Abort();
                            _clients.TryRemove(client.clientId, out var oldwslist);
                            continue;
                        }
                        //新版且没检测心跳 禁用
                        if (client.socket.State == WebSocketState.Open && client.heartSuccessLimit <= DateTime.Now && client.clientMetaData.version > limitVer)
                        {
                            if (client.heartSuccess)
                            {
                                if (await _redis.ExistsAsync($"heart:lock:{client.clientMetaData.realUid}")) continue;
                                await _redis.SetAsync($"heart:lock:{client.clientMetaData.realUid}", 0, 3, RedisExistence.Nx);
                                await _redis.IncrByAsync($"heart:error:{client.clientMetaData.realUid}");
                                continue;
                            }
                            if (client.heartChecked)
                            {
                                client.socket.Abort();
                                _clients.TryRemove(client.clientId, out var oldwslist);
                            }
                            continue;
                        }
                        if (JsonConfig<ManagerOptions>.Current.BlackUids.Contains(client.clientMetaData.realUid))
                        {
                            await SendStringAsync(client.socket, new Message() {type = "common", data = toastjs("拒绝访问", "error", 5_000)});
                            client.socket.Abort();
                            _clients.TryRemove(client.clientId, out var oldwslist);
                            continue;
                        }
                        //如果接收消息人是发送者，并且接收者只有1个以下，则不发送
                        //只有接收者为多端时，才转发消息通知其他端
                        if (clientId == data.senderClientId) continue;
                        //关闭的不发送
                        if (client.socket.State != WebSocketState.Open)
                        {
                            client.socket.Abort();
                            continue;
                        }
                        await client.socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None);
                        if (data.senderClientId != Guid.Empty && clientId != data.senderClientId && data.receipt)
                            SendMessage(clientId, new[] {data.senderClientId}, new
                            {
                                data.content,
                                receipt = "发送成功"
                            });
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteLine($"{client.clientMetaData.realUid} 发送失败");
                        XTrace.WriteException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"订阅方法出错了：{ex.Message}");
                XTrace.WriteException(ex);
            }
        });
    }
}

[JsonConfigFile("server.json", 10_000)]
public class ManagerOptions : JsonConfig<ManagerOptions>
{
    public ManagerOptions()
    {
        BlackUids = new List<long>();
    }

    public List<long> BlackUids { get; set; }
    public string limitVer { get; set; } = "2.4.4.5";
    public string lastVer { get; set; } = "2.4.4.9";
}
#endif