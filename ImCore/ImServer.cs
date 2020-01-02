using System.Collections.Generic;
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

class ImServer : ImClient
{
    protected string _server { get; set; }

    public ImServer(ImServerOptions options) : base(options)
    {
        _server = options.Server;
        _redis.Del("preBlack", "blacklist");
        _redis.Subscribe(($"{_redisPrefix}Server{_server}", RedisSubScribleMessage));
    }

    const int BufferSize = 4096;
    ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ImServerClient>> _clients = new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ImServerClient>>();

    class ImServerClient
    {
        public WebSocket socket;
        public Guid clientId;
        public string clientMetaData;

        public ImServerClient(WebSocket socket, Guid clientId, string clientMetaData)
        {
            this.socket         = socket;
            this.clientId       = clientId;
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

    internal async Task Acceptor(HttpContext context, Func<Task> next)
    {
        //Console.WriteLine($"context.WebSockets.IsWebSocketRequest:{context.WebSockets.IsWebSocketRequest}");
        if (!context.WebSockets.IsWebSocketRequest) return;
        try
        {
            string ip = context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress.ToString();
            string UA = context.Request.Headers["User-Agent"].FirstOrDefault();
            if (UA.ToLower().Contains("firefox")) return;
            string[] sMembers = await _redis.SMembersAsync("blacklist");
            if (sMembers.Any(r => ip.Contains(r)))
            {
                Console.WriteLine($"检测到多次异常黑名单IP尝试访问:{ip}");
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
                    return;
                }
                throw new Exception($@"
授权错误：用户需通过 ImHelper.PrevConnectServer 获得包含 token 的连接
IP:{ip},
origin:{origin},
token:{token},
Header:{string.Join("&", context.Request.Headers.Select(r => $"{r.Key}={r.Value}"))},
Param:{string.Join("&", context.Request.Query.Select(r => $"{r.Key}={r.Value}"))},
");
            }
            await _redis.DelAsync($"{_redisPrefix}Token{token}");

            await _redis.HDelAsync("preBlack", ip);
            var data = JsonConvert.DeserializeObject<(Guid clientId, string clientMetaData)>(token_value);
            Console.WriteLine($"客户{data.clientId}接入:{data.clientMetaData}");
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var cli = new ImServerClient(socket, data.clientId, data.clientMetaData);
            var newid = Guid.NewGuid();
            var wslist = _clients.GetOrAdd(data.clientId, cliid => new ConcurrentDictionary<Guid, ImServerClient>());
            wslist.TryAdd(newid, cli);
            try
            {
                _redis.StartPipe().HSet($"{_redisPrefix}OnlineData", data.clientId.ToString(), data.clientMetaData)
                    .HIncrBy($"{_redisPrefix}Online", data.clientId.ToString(), 1)
                    .Publish($"evt_{_redisPrefix}Online", token_value).EndPipe();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            Console.WriteLine($"当前客户数：{_clients.Count}");
            await SyncOnlineData();
            var buffer = new byte[BufferSize];
            var seg = new ArraySegment<byte>(buffer);
            try
            {
                while (socket.State == WebSocketState.Open && _clients.ContainsKey(data.clientId))
                {
                    var incoming = await socket.ReceiveAsync(seg, CancellationToken.None);
                    var outgoing = new ArraySegment<byte>(buffer, 0, incoming.Count);
                }
                socket.Abort();
            }
            catch
            {
            }
            wslist.TryRemove(newid, out var oldcli);
            if (wslist.Any() == false) _clients.TryRemove(data.clientId, out var oldwslist);
            await _redis.EvalAsync($"if redis.call('HINCRBY', KEYS[1], '{data.clientId}', '-1') <= 0 then redis.call('HDEL', KEYS[1], '{data.clientId}') end return 1",
                $"{_redisPrefix}Online");
            LeaveChan(data.clientId, GetChanListByClientId(data.clientId));
            await SyncOnlineData();
            Console.WriteLine($"客户{data.clientId}下线:{data.clientMetaData}");
            Console.WriteLine($"当前客户数：{_clients.Count}");
            await _redis.PublishAsync($"evt_{_redisPrefix}Offline", token_value);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    async Task SyncOnlineData()
    {
        string key = $"{_redisPrefix}OnlineData";
        try
        {
            Dictionary<string, string> hGetAll = await _redis.HGetAllAsync(key) ?? new Dictionary<string, string>();
            using (var pipe = _redis.StartPipe())
            {
                List<ImServerClient> clientList = _clients.Where(r => !hGetAll.ContainsKey(r.Key.ToString())).SelectMany(r => r.Value.Values).Distinct().ToList();
                object[] param = clientList.Where(r => !hGetAll.ContainsKey(r.clientId.ToString())).SelectMany(r => new object[] {r.clientId, r.clientMetaData}).ToArray();
                if (param.Any())
                {
                    pipe.HMSet(key, param);
                }
                var outKeys = hGetAll.Keys.Where(k => _clients.Keys.All(c => c.ToString() != k)).ToArray();
                if (outKeys.Any()) pipe.HDel(key, outKeys);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    void RedisSubScribleMessage(CSRedis.CSRedisClient.SubscribeMessageEventArgs e)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<(Guid senderClientId, Guid[] receiveClientId, string content, bool receipt)>(e.Body);
            Trace.WriteLine($"收到消息：{data.content}"      + (data.receipt ? "【需回执】" : ""));
            Console.WriteLine($"收到需推送消息：{data.content}" + (data.receipt ? "【需回执】" : ""));

            var outgoing = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data.content));
            if (!data.receiveClientId.Any())
            {
                data.receiveClientId = _clients.Keys.ToArray();
            }
            foreach (var clientId in data.receiveClientId)
            {
                if (_clients.TryGetValue(clientId, out var wslist) == false)
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

                ImServerClient[] sockarray = wslist.Values.ToArray();

                //如果接收消息人是发送者，并且接收者只有1个以下，则不发送
                //只有接收者为多端时，才转发消息通知其他端
                if (clientId == data.senderClientId && sockarray.Length <= 1) continue;

                foreach (var sh in sockarray)
                    sh.socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None);

                if (data.senderClientId != Guid.Empty && clientId != data.senderClientId && data.receipt)
                    SendMessage(clientId, new[] {data.senderClientId}, new
                    {
                        data.content,
                        receipt = "发送成功"
                    });
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"订阅方法出错了：{ex.Message}");
        }
    }
}

#endif