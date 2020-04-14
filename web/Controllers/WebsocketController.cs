﻿using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiliEntity;
using Microsoft.AspNetCore.Cors.Infrastructure;
using NewLife.Caching;
using NewLife.Json;
using NewLife.Threading;
using Newtonsoft.Json;

namespace web.Controllers
{
    [Route("ws")]
    public class WebSocketController : Controller
    {
        public string Ip => this.Request.Headers["X-Real-IP"].FirstOrDefault() ?? this.Request.HttpContext.Connection.RemoteIpAddress.ToString();
        public string referer => this.Request.Headers["Referer"].FirstOrDefault();
        public string UA => this.Request.Headers["User-Agent"].FirstOrDefault();

        /// <summary>
        /// 获取websocket分区
        /// </summary>
        /// <param name="websocketId">本地标识，若无则不传，接口会返回新的，请保存本地localStoregy重复使用</param>
        /// <param name="uid"></param>
        /// <returns></returns>
        [HttpPost("pre-connect")]
        public object preConnect([FromForm] Guid? websocketId, [FromForm] int? uid, [FromForm] string version)
        {
            Version lastVersion = new Version("2.4.4.0");
            if(Version.TryParse(JsonConfig<ManagerOptions>.Current.lastVer,out var tmp))
            {
                lastVersion = tmp;
            }
            Version.TryParse(version, out Version vers);
            if (UA.ToLower().Contains("firefox"))
            {
                return new {code = -1, msg = "请勿使用火狐浏览器"};
            }
            if (!uid.HasValue)
            {
                return new {code = -1, msg = $"请更新脚本至v{lastVersion}或以上"};
            }
            if (vers == null || vers < lastVersion)
            {
                return new {code = -1, msg = $"请更新脚本至v{lastVersion}或以上"};
            }
            string refUrl = referer.Split("?", StringSplitOptions.RemoveEmptyEntries)[0];
            int[] rooms = new[] {21438956};
            bool roomCheck = false;
            foreach (var room in rooms)
            {
                if (refUrl.Contains($"live.bilibili.com/{room}"))
                {
                    roomCheck = true;
                }
            }
            if (!roomCheck)
            {
                return new {code = -1, msg = $"bilipush仅支持直播间{rooms.Join(",")}"};
            }
            if (websocketId == null)
            {
                string guid = Cache.Default.Get<string>($"UserGuid_{uid.Value}");
                if (guid.IsNullOrWhiteSpace())
                {
                    websocketId = Guid.NewGuid();
                    Cache.Default.Set($"UserGuid_{uid.Value}", websocketId.Value.ToString(), TimeSpan.FromHours(1));
                }
                else
                {
                    websocketId = new Guid(guid);
                }
            }
            var wsserver = ImHelper.PrevConnectServer(websocketId.Value, new ImClientInfo {uid = uid.Value, Ip = Ip, referer = referer, version = vers});
            return new
            {
                code        = 0,
                server      = wsserver,
                websocketId = websocketId
            };
        }

        [HttpGet("OnlineData")]
        public object OnlineData()
        {
            var onlineClients = ImHelper.GetAllClientDataByOnline().OrderBy(r => r.Value).ToDictionary(r => r.Key, r => r.Value);
            return new
            {
                code  = 0,
                count = onlineClients.Count,
                data  = onlineClients
            };
        }

        [HttpPost("post_raffle")]
        public object PostRaffle([FromForm] string msg)
        {
            ImHelper.SendMessageOnline(msg);
            return new
            {
                code = 0
            };
        }

        [HttpPost("post_msg")]
        public object PostMsg([FromForm] string msg)
        {
            ImHelper.SendMessageOnline(JsonConvert.SerializeObject(new {code = 0, type = "msg", data = msg}));
            return new
            {
                code = 0
            };
        }

        [HttpPost("post_reload")]
        public object PostReload()
        {
            ImHelper.SendMessageOnline(JsonConvert.SerializeObject(new {code = 0, type = "reload"}));
            return new
            {
                code = 0
            };
        }

        [HttpPost("post_common_reload")]
        public object PostCommonReload(bool force = false)
        {
            ImHelper.SendMessageOnline(JsonConvert.SerializeObject(new {code = 0, type = "common", data = force ? ImClient.forceReloadjs : ImClient.reloadjs}));
            return new
            {
                code = 0
            };
        }
        [HttpPost("post_common_only_reload")]
        public object PostCommonOnlyReload()
        {
            ImHelper.SendMessageOnline(JsonConvert.SerializeObject(new {code = 0, type = "common", data = ImClient.onlyReloadjs}));
            return new
            {
                code = 0
            };
        }

        [HttpPost("post_set_vol")]
        public object PostSetVol(decimal vol = 0.1m)
        {
            ImHelper.SendMessageOnline(JsonConvert.SerializeObject(new { code = 0, type = "common", data = ImClient.setVolJs(vol) }));
            return new
            {
                code = 0
            };
        }

        /// <summary>
        /// 群聊，获取群列表
        /// </summary>
        /// <returns></returns>
        [HttpPost("get-channels")]
        public object getChannels()
        {
            return new
            {
                code     = 0,
                channels = ImHelper.GetChanList()
            };
        }

        /// <summary>
        /// 群聊，绑定消息频道
        /// </summary>
        /// <param name="websocketId">本地标识，若无则不传，接口会返回，请保存本地重复使用</param>
        /// <param name="channel">消息频道</param>
        /// <returns></returns>
        [HttpPost("subscr-channel")]
        public object subscrChannel([FromForm] Guid websocketId, [FromForm] string channel)
        {
            ImHelper.JoinChan(websocketId, channel);
            return new
            {
                code = 0
            };
        }

        /// <summary>
        /// 群聊，发送频道消息，绑定频道的所有人将收到消息
        /// </summary>
        /// <param name="channel">消息频道</param>
        /// <param name="content">发送内容</param>
        /// <returns></returns>
        [HttpPost("send-channelmsg")]
        public object sendChannelmsg([FromForm] Guid websocketId, [FromForm] string channel, [FromForm] string message)
        {
            ImHelper.SendChanMessage(websocketId, channel, message);
            return new
            {
                code = 0
            };
        }

        /// <summary>
        /// 单聊
        /// </summary>
        /// <param name="senderWebsocketId">发送者</param>
        /// <param name="receiveWebsocketId">接收者</param>
        /// <param name="message">发送内容</param>
        /// <param name="isReceipt">是否需要回执</param>
        /// <returns></returns>
        [HttpPost("send-msg")]
        public object sendmsg([FromForm] Guid senderWebsocketId, [FromForm] Guid receiveWebsocketId, [FromForm] string message, [FromForm] bool isReceipt = false)
        {
            //var loginUser = 发送者;
            //var recieveUser = User.Get(receiveWebsocketId);

            //if (loginUser.好友 != recieveUser) throw new Exception("不是好友");

            ImHelper.SendMessage(senderWebsocketId, new[] {receiveWebsocketId}, message, isReceipt);

            //loginUser.保存记录(message);
            //recieveUser.保存记录(message);

            return new
            {
                code = 0
            };
        }
    }
}