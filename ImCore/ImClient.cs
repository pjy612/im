﻿using CSRedis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NewLife.Caching;

public class ImClientInfo
{
    private Guid? _clientId;

    public Guid clientId
    {
        get => _clientId ?? Guid.NewGuid();
        set => _clientId = value;
    }

    public long uid { get; set; }
    public long realUid { get; set; }
    public string uname { get; set; }
    public string Ip { get; set; }
    public string referer { get; set; }
    public string UA { get; set; }
    public Version version { get; set; }
    public string token => $"{uid}{Ip}{referer}{UA}".MD5();
    public long roomid { get; set; }
    /// <summary>
    /// bilipush 连接key
    /// </summary>
    public string key { get; set; }
}

/// <summary>
/// im 核心类实现的配置所需
/// </summary>
public class ImClientOptions
{
    /// <summary>
    /// CSRedis 对象，用于存储数据和发送消息
    /// </summary>
    public CSRedisClient Redis { get; set; }

    /// <summary>
    /// 负载的服务端
    /// </summary>
    public string[] Servers { get; set; }

    /// <summary>
    /// websocket请求的路径，默认值：/ws
    /// </summary>
    public string PathMatch { get; set; } = "/ws";
}

public class ImSendEventArgs : EventArgs
{
    /// <summary>
    /// 发送者的客户端id
    /// </summary>
    public Guid SenderClientId { get; }

    /// <summary>
    /// 接收者的客户端id
    /// </summary>
    public List<Guid> ReceiveClientId { get; } = new List<Guid>();

    /// <summary>
    /// imServer 服务器节点
    /// </summary>
    public string Server { get; }

    /// <summary>
    /// 消息
    /// </summary>
    public object Message { get; }

    /// <summary>
    /// 是否回执
    /// </summary>
    public bool Receipt { get; }

    internal ImSendEventArgs(string server, Guid senderClientId, object message, bool receipt = false)
    {
        this.Server         = server;
        this.SenderClientId = senderClientId;
        this.Message        = message;
        this.Receipt        = receipt;
    }
}

/// <summary>
/// im 核心类实现
/// </summary>
public class ImClient
{
    protected CSRedisClient _redis;
    protected string[] _servers;
    protected string _redisPrefix;
    protected string _pathMatch;

    /// <summary>
    /// 推送消息的事件，可审查推向哪个Server节点
    /// </summary>
    public EventHandler<ImSendEventArgs> OnSend;

    public string heartOncejs(object val)
    {
        return @"
        try
        {
            if(typeof(heartTimeout)!='undefined'){
                clearTimeout(heartTimeout);
            }
            BiliPush.gsocket.send(JSON.stringify({ type: 'heart',data: '" + val + @"'}));
        }
        catch(e){ }";
    }

    //__LIVE_USER_LOGIN_STATUS__
    public string postUserjs(string token)
    {
        return @"
try {
    API.live_user.get_info_in_room(Info.roomid).then((response) => {
        let {
            uid,
            uname
        } = response.data.info;
        BiliPush.gsocket.send(
            JSON.stringify({
                type: 'user_check',
                token: '" + token + @"',
                data: Object.assign({
                    uid,
                    uname
                }, {
                    roomid: location.href.match(/(\d+)/)[1],
                    storm: CONFIG.AUTO_LOTTERY_CONFIG.STORM
                })
            })
        );
    });
} catch (e) {}";
    }

    public string heartjs(object val)
    {
        return @"
try{
if(typeof(heartTimeout)!='undefined'){
    clearTimeout(heartTimeout);
}
heartTimeout = setTimeout(()=>{
    try
    {
        BiliPush.gsocket.send(JSON.stringify({ type: 'heart',data: '" + val + @"'}));
    }
    catch(e){ }
},30e3);
}
catch(e){ }";
    }

    public string messagejs(string msg) => $@"
        try
        {{
            window.alertdialog('魔改助手消息','{msg}');
        }}
        catch(e){{ }}";

    public string toastjs(string message, string type, int ms)
    {
        return $@"
        try
        {{
            window.toast('{message}','{type}',{ms});
        }}
        catch(e){{}}";
    }

    public string giftJs(List<long> roomIds)
    {
        return $@"
        try
        {{
            window.toast('遗漏礼物检测ing...','info',5000);
            let roomIds = [{roomIds.Join(",")}];
            for(let roomId of roomIds){{
                BiliPushUtils.Check.run(roomId);
            }}
        }}
        catch(e){{}}";
    }

    public const string reloadjs = @"
try
{
localStorage.setItem('LIVE_PLAYER_STATUS',JSON.stringify({type:'html5',timeStamp:ts_ms()}));
var volume = localStorage.getItem('videoVolume')||0;
if(volume==0){
    localStorage.setItem('videoVolume',0.1);
}
    var reload = false;
    if(livePlayer){
        livePlayer.reload();
        if($('.bilibili-live-player-video').length==0){
            reload = true;
        }
    }else{
        reload = true;
    }
    if(reload){        
if(location.href.match(/(\d+)/) && location.href.match(/(\d+)/)[1]==21438956){
location.reload();
}else{
location.replace('https://live.bilibili.com/21438956');
}
    }
}
catch(e){ }";

    public const string forceReloadjs = @"
try
{    
    localStorage.setItem('LIVE_PLAYER_STATUS',JSON.stringify({type:'html5',timeStamp:ts_ms()}));
    var volume = localStorage.getItem('videoVolume')||0;
    if(volume==0){
        localStorage.setItem('videoVolume',0.1);
    }
if(location.href.match(/(\d+)/) && location.href.match(/(\d+)/)[1]==21438956){
location.reload();
}else{
location.replace('https://live.bilibili.com/21438956');
}
}
catch(e){ }";

    public const string flushPage = @"
try
{
location.reload();
}
catch(e){ }";

    public const string onlyReloadjs = @"
try
{    
if(location.href.match(/(\d+)/) && location.href.match(/(\d+)/)[1]==21438956){
location.reload();
}else{
location.replace('https://live.bilibili.com/21438956');
}
}
catch(e){ }";

    public static string setVolJs(decimal vol)
    {
        return $@"
try
{{    
    localStorage.setItem('LIVE_PLAYER_STATUS',JSON.stringify({{type:'html5',timeStamp:ts_ms()}}));
    localStorage.setItem('videoVolume',{vol});
}}
catch(e){{ }}";
    }

    public static string jumpToRoom(long roomId, bool changeVol = false, decimal vol = 0.1m, bool forceReload = false)
    {
        return $@"
try
{{    
    localStorage.setItem('LIVE_PLAYER_STATUS',JSON.stringify({{type:'html5',timeStamp:ts_ms()}}));
    if(location.href.match(/(\d+)/) && location.href.match(/(\d+)/)[1]=='{roomId}'){{
        {(changeVol ?
            @"localStorage.setItem('videoVolume', " + vol + @");" :
            @"")}
        {(forceReload ? "location.reload();" : "livePlayer.reload();")}
    }}else{{
        {(changeVol ?
            @"localStorage.setItem('videoVolume', " + vol + @");" :
            @"if(volume>=0.5){localStorage.setItem('videoVolume', 0.1);}")}
        location.replace('https://live.bilibili.com/{roomId}');
    }}
}}
catch(e){{ }}";
    }

    public static string dmStorm(string msg, string roomId = "",int time=5, bool force = false)
    {
        return $@"
try
{{    
    console.log('触发DD节奏风暴[{roomId}]:{msg}');
    function sendDm(msg,roomid = 0){{
        {(force ? "" : "if (CONFIG && !CONFIG.DD_BP_CONFIG.DM_STORM){return;}")}
        if (!roomid)
        {{
            roomid = BilibiliLive.ROOMID;
        }}
        BiliPushUtils.ajaxWithCommonArgs({{
            method: 'POST',
            url: 'msg/send',
            data: {{
                color: 16777215,
                fontsize:25,
                mode:1,
                msg:msg,
                roomid:roomid,
                bubble:0,
                rnd:Math.round(new Date().valueOf() /1000),
            }}
            ,roomid:roomid
        }});
    }}
    setTimeout(()=>sendDm('{msg}','{roomId}'),parseInt(Math.random()*{time}*1e3));
}}
catch(e){{ }}";
    }

    /// <summary>
    /// 初始化 imclient
    /// </summary>
    /// <param name="options"></param>
    public ImClient(ImClientOptions options)
    {
        if (options.Redis         == null) throw new ArgumentException("ImClientOptions.Redis 参数不能为空");
        if (options.Servers.Any() == false) throw new ArgumentException("ImClientOptions.Servers 参数不能为空");
        _redis       = options.Redis;
        _servers     = options.Servers;
        _redisPrefix = $"wsim{options.PathMatch.Replace('/', '_')}";
        _pathMatch   = options.PathMatch ?? "/ws";
    }

    /// <summary>
    /// 负载分区规则：取clientId后四位字符，转成10进制数字0-65535，求模
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <returns></returns>
    protected string SelectServer(Guid clientId)
    {
        var servers_idx = int.Parse(clientId.ToString("N").Substring(28), NumberStyles.HexNumber) % _servers.Length;
        if (servers_idx >= _servers.Length) servers_idx = 0;
        return _servers[servers_idx];
    }

    /// <summary>
    /// ImServer 连接前的负载、授权，返回 ws 目标地址，使用该地址连接 websocket 服务端
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <param name="clientMetaData">客户端相关信息，比如ip</param>
    /// <returns>websocket 地址：ws://xxxx/ws?token=xxx</returns>
    public string PrevConnectServer(Guid clientId, ImClientInfo clientMetaData)
    {
        var server = SelectServer(clientId);
        var token = clientMetaData.token;
        _redis.Set($"{_redisPrefix}Token{token}", clientMetaData, 30);
        return $"wss://{server}{_pathMatch}?token={token}";
    }

    /// <summary>
    /// ImServer 连接前的负载、授权，返回 ws 目标地址，使用该地址连接 websocket 服务端
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <param name="clientMetaData">客户端相关信息，比如ip</param>
    /// <returns>websocket 地址：ws://xxxx/ws?token=xxx</returns>
    public async Task<string> PrevConnectServerAsync(Guid clientId, ImClientInfo clientMetaData)
    {
        var server = SelectServer(clientId);
        var token = clientMetaData.token;
        await _redis.SetAsync($"{_redisPrefix}Token{token}", clientMetaData, 30);
        return $"wss://{server}{_pathMatch}?token={token}";
    }

    /// <summary>
    /// 向指定的多个客户端id发送消息
    /// </summary>
    /// <param name="senderClientId">发送者的客户端id</param>
    /// <param name="receiveClientId">接收者的客户端id</param>
    /// <param name="message">消息</param>
    /// <param name="receipt">是否回执</param>
    public void SendMessage(Guid senderClientId, IEnumerable<Guid> receiveClientId, object message, bool receipt = false)
    {
        receiveClientId = receiveClientId.Distinct().ToArray();
        Dictionary<string, ImSendEventArgs> redata = new Dictionary<string, ImSendEventArgs>();

        foreach (var uid in receiveClientId)
        {
            string server = SelectServer(uid);
            if (redata.ContainsKey(server) == false) redata.Add(server, new ImSendEventArgs(server, senderClientId, message, receipt));
            redata[server].ReceiveClientId.Add(uid);
        }
        var messageJson = JsonConvert.SerializeObject(message);
        Console.WriteLine($"imClient 推送消息:{messageJson}");
        foreach (var sendArgs in redata.Values)
        {
            OnSend?.Invoke(this, sendArgs);
            _redis.Publish($"{_redisPrefix}Server{sendArgs.Server}",
                JsonConvert.SerializeObject((senderClientId, sendArgs.ReceiveClientId, messageJson, sendArgs.Receipt)));
        }
    }

    /// <summary>
    /// 向全部客户端id发送消息
    /// </summary>
    /// <param name="senderClientId">发送者的客户端id</param>
    /// <param name="receiveClientId">接收者的客户端id</param>
    /// <param name="message">消息</param>
    /// <param name="receipt">是否回执</param>
    public void SendMessageOnline(object message, bool receipt = false)
    {
        var sender = Guid.NewGuid();
        var messageJson = JsonConvert.SerializeObject(message);
        Console.WriteLine($"imClient 推送消息:{messageJson}");
        foreach (var server in _servers)
        {
            OnSend?.Invoke(this, new ImSendEventArgs(server, sender, message, receipt));
            _redis.Publish($"{_redisPrefix}Server{server}",
                JsonConvert.SerializeObject((sender, new List<Guid>(), messageJson, receipt)));
        }
    }

    /// <summary>
    /// 获取所在线客户端id
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Guid> GetClientListByOnline()
    {
        return _redis.HKeys($"{_redisPrefix}Online").Select(a => Guid.TryParse(a, out var tryguid) ? tryguid : Guid.Empty).Where(a => a != Guid.Empty);
    }

    /// <summary>
    /// 获取所在线客户端信息
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, string> GetAllClientDataByOnline()
    {
        return _redis.HGetAll($"{_redisPrefix}OnlineData");
    }

    /// <summary>
    /// 事件订阅
    /// </summary>
    /// <param name="online">上线</param>
    /// <param name="offline">下线</param>
    public void EventBus(
        Action<(Guid clientId, string clientMetaData)> online,
        Action<(Guid clientId, string clientMetaData)> offline)
    {
        _redis.Subscribe(
            ($"evt_{_redisPrefix}Online", msg => online(JsonConvert.DeserializeObject<(Guid clientId, string clientMetaData)>(msg.Body))),
            ($"evt_{_redisPrefix}Offline", msg => offline(JsonConvert.DeserializeObject<(Guid clientId, string clientMetaData)>(msg.Body))));
    }

    #region 群聊频道，每次上线都必须重新加入

    /// <summary>
    /// 加入群聊频道，每次上线都必须重新加入
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <param name="chan">群聊频道名</param>
    public void JoinChan(Guid clientId, string chan)
    {
        _redis.StartPipe(a => a
            .HSet($"{_redisPrefix}Chan{chan}", clientId.ToString(), 0)
            .HSet($"{_redisPrefix}Client{clientId}", chan, 0)
            .HIncrBy($"{_redisPrefix}ListChan", chan, 1));
    }

    /// <summary>
    /// 离开群聊频道
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <param name="chans">群聊频道名</param>
    public void LeaveChan(Guid clientId, params string[] chans)
    {
        Task.Run(() =>
        {
            try
            {
                if (chans?.Any() != true) return;
                using (var pipe = _redis.StartPipe())
                {
                    foreach (var chan in chans)
                        pipe
                            .HDel($"{_redisPrefix}Chan{chan}", clientId.ToString())
                            .HDel($"{_redisPrefix}Client{clientId}", chan)
                            .Eval($"if redis.call('HINCRBY', KEYS[1], '{chan}', '-1') <= 0 then redis.call('HDEL', KEYS[1], '{chan}') end return 1",
                                $"{_redisPrefix}ListChan");
                }
            }
            catch (Exception ex)
            {
            }
        });
    }

    /// <summary>
    /// 获取群聊频道所有客户端id（测试）
    /// </summary>
    /// <param name="chan">群聊频道名</param>
    /// <returns></returns>
    public Guid[] GetChanClientList(string chan)
    {
        return _redis.HKeys($"{_redisPrefix}Chan{chan}").Select(a => Guid.Parse(a)).ToArray();
    }

    /// <summary>
    /// 清理群聊频道的离线客户端（测试）
    /// </summary>
    /// <param name="chan">群聊频道名</param>
    public void ClearChanClient(string chan)
    {
        var websocketIds = _redis.HKeys($"{_redisPrefix}Chan{chan}");
        var offline = new List<string>();
        var span = websocketIds.AsSpan();
        var start = span.Length;
        while (start > 0)
        {
            start = start - 10;
            var length = 10;
            if (start < 0)
            {
                length = start + 10;
                start  = 0;
            }
            var slice = span.Slice(start, length);
            var hvals = _redis.HMGet($"{_redisPrefix}Online", slice.ToArray().Select(b => b.ToString()).ToArray());
            for (var a = length - 1; a >= 0; a--)
            {
                if (string.IsNullOrEmpty(hvals[a]))
                {
                    offline.Add(span[start + a]);
                    span[start + a] = null;
                }
            }
        }
        //删除离线订阅
        if (offline.Any()) _redis.HDel($"{_redisPrefix}Chan{chan}", offline.ToArray());
    }

    /// <summary>
    /// 获取所有群聊频道和在线人数
    /// </summary>
    /// <returns>频道名和在线人数</returns>
    public IEnumerable<(string chan, long online)> GetChanList()
    {
        var ret = _redis.HGetAll<long>($"{_redisPrefix}ListChan");
        return ret.Select(a => (a.Key, a.Value));
    }

    /// <summary>
    /// 获取用户参与的所有群聊频道
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <returns></returns>
    public string[] GetChanListByClientId(Guid clientId)
    {
        return _redis.HKeys($"{_redisPrefix}Client{clientId}");
    }

    /// <summary>
    /// 获取群聊频道的在线人数
    /// </summary>
    /// <param name="chan">群聊频道名</param>
    /// <returns>在线人数</returns>
    public long GetChanOnline(string chan)
    {
        return _redis.HGet<long>($"{_redisPrefix}ListChan", chan);
    }

    /// <summary>
    /// 发送群聊消息，所有在线的用户将收到消息
    /// </summary>
    /// <param name="senderClientId">发送者的客户端id</param>
    /// <param name="chan">群聊频道名</param>
    /// <param name="message">消息</param>
    public void SendChanMessage(Guid senderClientId, string chan, object message)
    {
        var websocketIds = _redis.HKeys($"{_redisPrefix}Chan{chan}");
        SendMessage(Guid.Empty, websocketIds.Where(a => !string.IsNullOrEmpty(a)).Select(a => Guid.TryParse(a, out var tryuuid) ? tryuuid : Guid.Empty).ToArray(), message);
    }

    #endregion
}