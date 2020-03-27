using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BiliEntity;
using NewLife.Http;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serialization.Json;
using XCode;

namespace web
{
    public class RoomQueue
    {
        public readonly BlockingCollection<long> ProcessCollection = new BlockingCollection<long>();
        public readonly HashSet<long> QueueRoomSet = new HashSet<long>();
        public readonly List<long> RoomNeedLoad = new List<long>();
        private static RoomQueue _instance;
        public static RoomQueue Instance => _instance ?? (_instance = new RoomQueue());

        public RoomQueue()
        {
            var timerX = new TimerX(state =>
            {
                long[] ids = QueueRoomSet.ToArray();
                ids.AsParallel().ForAll(id =>
                {
                    QueueRoomSet.Remove(id);
                    if (!ProcessCollection.Contains(id))
                    {
                        ProcessCollection.Add(id);
                    }
                });
            }, null, 1000, 1000);
            Task.Run(async () =>
            {
                foreach (var item in ProcessCollection.GetConsumingEnumerable())
                {
                    await RoomSortForce(item);
                }
            });
            Task.Run(() =>
            {
                do
                {
                    if (RoomNeedLoad.Count > 0 || QueueRoomSet.Count > 0 || ProcessCollection.Count > 0)
                    {
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} RoomNeedLoad:{RoomQueue.Instance.RoomNeedLoad.Count},RoomQueue:{RoomQueue.Instance.QueueRoomSet.Count},Process:{RoomQueue.Instance.ProcessCollection.Count}");
                    }
                    Thread.Sleep(1000);
                } while (true);
            });
            Console.WriteLine($"RoomQueue Init");
        }

        public async Task<RoomSort> RoomSortForce(long id, bool force = false)
        {
            RoomInitList room = RoomInitList.FindByKey(id);
            if (room == null)
            {
                RoomInit init = RoomQueue.GetRoomInitByRoomId(id);
                if (init?.Code == 0)
                {
                    room     = new RoomInitList() {RoomID = id};
                    room.Uid = init.Data.Uid;
                    //room.Message        = JsonConvert.SerializeObject(init);
                    room.Message        = "";
                    room.LastUpdateTime = DateTime.Now;
                    room.SaveAsync();
                    goto beginNext;
                }
                return null;
            }
            beginNext:
            RoomSort entity = RoomSort.FindByKey(id) ?? new RoomSort() {RoomID = id};
            FanGifts fan = FanGifts.FindByKey(room.RoomID);
            GuardTop guardTop = GuardTop.FindByKey(room.Uid);
            FollowNum followNum = FollowNum.FindByKey(room.Uid);
            if (fan == null || fan?.LastUpdateTime < DateTime.Today.AddDays(1) || force)
            {
                RoomRsp<GiftTopDto> fanGiftsByRoomId = await RoomQueue.GetFanGiftsByRoomId(room.RoomID);
                if (fanGiftsByRoomId?.Code == 0)
                {
                    fan                = new FanGifts() {RoomID = room.RoomID};
                    fan.Num            = fanGiftsByRoomId.Data.List.Count;
                    fan.Message        = "";
                    fan.LastUpdateTime = DateTime.Now;
                    fan.SaveAsync();
                }
            }
            if (guardTop == null || guardTop?.LastUpdateTime < DateTime.Today.AddDays(1) || force)
            {
                RoomRsp<GuardTopDto> dto = await RoomQueue.GetGuardTopByUid(room.Uid);
                if (dto?.Code == 0)
                {
                    guardTop     = new GuardTop() {Uid = room.Uid};
                    guardTop.Num = dto.Data?.Info?.Num ?? 0;
                    //guardTop.Data           = JsonConvert.SerializeObject(dto);
                    guardTop.Data           = "";
                    guardTop.LastUpdateTime = DateTime.Now;
                    guardTop.SaveAsync();
                }
            }
            if (followNum == null || followNum?.LastUpdateTime < DateTime.Today.AddDays(1) || force)
            {
                RoomRsp<FollowNumDto> followNumByUid = await RoomQueue.GetFollowNumByUid(room.Uid);
                if (followNumByUid?.Code == 0)
                {
                    followNum     = new FollowNum() {Uid = id};
                    followNum.Num = followNumByUid.Data.Fc;
                    //followNum.Data           = JsonConvert.SerializeObject(followNumByUid);
                    followNum.Data           = "";
                    followNum.LastUpdateTime = DateTime.Now;
                    followNum.SaveAsync();
                }
            }
            if (followNum != null && guardTop != null && fan != null)
            {
                entity.Uid            = room.Uid;
                entity.FansNum        = fan?.Num       ?? 0;
                entity.FollowNum      = followNum?.Num ?? 0;
                entity.GuardNum       = guardTop?.Num  ?? 0;
                entity.LastUpdateTime = DateTime.Now;
                entity.SaveAsync();
                return entity;
            }
            return null;
        }

        public static RoomInit GetRoomInitByRoomId(long roomid)
        {
            try
            {
                RestClient client = new RestClient();
                RestRequest request = new RestRequest($"https://api.live.bilibili.com/room/v1/Room/room_init?id={roomid}", Method.GET);
                IRestResponse<RoomRsp<RoomInfoData>> execute = client.Execute<RoomRsp<RoomInfoData>>(request);
                if (execute.StatusCode == HttpStatusCode.OK)
                {
                    if (execute.Data != null && execute.Data.Code == 0)
                    {
                        return execute.Data.ToJson().ToJsonEntity<RoomInit>();
                    }
                    else
                    {
                        Console.WriteLine($"GetRoomInitByRoomId:{roomid}:{execute.Content}");
                    }
                }
            }
            catch (Exception e)
            {
                XTrace.WriteException(e);
            }
            return null;
        }

        public static async Task<RoomRsp<GiftTopDto>> GetFanGiftsByRoomId(long roomid)
        {
            try
            {
                RestClient client = new RestClient();
                RestRequest request = new RestRequest($"https://api.live.bilibili.com/AppRoom/getGiftTop?room_id={roomid}", Method.GET);
                IRestResponse<RoomRsp<GiftTopDto>> execute = await client.ExecuteTaskAsync<RoomRsp<GiftTopDto>>(request);
                if (execute.StatusCode == HttpStatusCode.OK)
                {
                    if (execute.Data.Code == 0)
                    {
                        return execute.Data;
                    }
                    else
                    {
                        Console.WriteLine($"GetFanGiftsByRoomId:{roomid}:{execute.Content}");
                    }
                    FanGifts.FindByKey(roomid)?.Delete();
                }
            }
            catch (Exception e)
            {
                XTrace.WriteException(e);
            }
            return null;
        }

        public static async Task<RoomRsp<GuardTopDto>> GetGuardTopByUid(long uid)
        {
            try
            {
                RestClient client = new RestClient();
                RestRequest request = new RestRequest($"https://api.live.bilibili.com/guard/topList?ruid={uid}&page=1", Method.GET);
                IRestResponse<RoomRsp<GuardTopDto>> execute = await client.ExecuteTaskAsync<RoomRsp<GuardTopDto>>(request);
                if (execute.StatusCode == HttpStatusCode.OK)
                {
                    if (execute.Data != null && execute.Data.Code == 0)
                    {
                        return execute.Data;
                    }
                    else
                    {
                        Console.WriteLine($"GetGuardTopByUid:{uid}:{execute.Content}");
                    }
                    GuardTop.FindByKey(uid)?.Delete();
                }
            }
            catch (Exception e)
            {
                XTrace.WriteException(e);
            }
            return null;
        }

        public static async Task<RoomRsp<FollowNumDto>> GetFollowNumByUid(long uid)
        {
            try
            {
                RestClient client = new RestClient();
                RestRequest request = new RestRequest($"https://api.live.bilibili.com/relation/v1/Feed/GetUserFc?follow={uid}", Method.GET);
                IRestResponse<RoomRsp<FollowNumDto>> execute = await client.ExecuteTaskAsync<RoomRsp<FollowNumDto>>(request);
                if (execute.StatusCode == HttpStatusCode.OK)
                {
                    if (execute.Data != null && execute.Data.Code == 0)
                    {
                        return execute.Data;
                    }
                    else
                    {
                        Console.WriteLine($"GetFollowNumByUid:{uid}:{execute.Content}");
                    }
                    FollowNum.FindByKey(uid)?.Delete();
                }
            }
            catch (Exception e)
            {
                XTrace.WriteException(e);
            }
            return null;
        }
    }

    public class FollowNumDto
    {
        /// <summary>
        /// Examples: 2456199
        /// </summary>
        [JsonProperty("fc")]
        public int Fc { get; set; }
    }

    public class RoomInit
    {
        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Examples: {"room_id":15167351,"short_id":0,"uid":388563921,"need_p2p":0,"is_hidden":false,"is_locked":false,"is_portrait":false,"live_status":0,"hidden_till":0,"lock_till":0,"encrypted":false,"pwd_verified":false,"live_time":-62170012800,"room_shield":0,"is_sp":0,"special_type":0}
        /// </summary>
        [JsonProperty("data")]
        public RoomInfoData Data { get; set; }

        /// <summary>
        /// Examples: "ok"
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// Examples: "ok"
        /// </summary>
        [JsonProperty("msg")]
        public string Msg { get; set; }
    }

    public class RoomInfoData
    {
        /// <summary>
        /// Examples: false
        /// </summary>
        [JsonProperty("encrypted")]
        public bool Encrypted { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("hidden_till")]
        public long HiddenTill { get; set; }

        /// <summary>
        /// Examples: false
        /// </summary>
        [JsonProperty("is_hidden")]
        public bool IsHidden { get; set; }

        /// <summary>
        /// Examples: false
        /// </summary>
        [JsonProperty("is_locked")]
        public bool IsLocked { get; set; }

        /// <summary>
        /// Examples: false
        /// </summary>
        [JsonProperty("is_portrait")]
        public bool IsPortrait { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("is_sp")]
        public long IsSp { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("live_status")]
        public long LiveStatus { get; set; }

        /// <summary>
        /// Examples: -62170012800
        /// </summary>
        [JsonProperty("live_time")]
        public long LiveTime { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("lock_till")]
        public long LockTill { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("need_p2p")]
        public int NeedP2p { get; set; }

        /// <summary>
        /// Examples: false
        /// </summary>
        [JsonProperty("pwd_verified")]
        public bool PwdVerified { get; set; }

        /// <summary>
        /// Examples: 15167351
        /// </summary>
        [JsonProperty("room_id")]
        public long RoomId { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("room_shield")]
        public long RoomShield { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("short_id")]
        public long ShortId { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("special_type")]
        public long SpecialType { get; set; }

        /// <summary>
        /// Examples: 388563921
        /// </summary>
        [JsonProperty("uid")]
        public long Uid { get; set; }
    }

    public class RoomRsp<T>
    {
        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Examples: {"unlogin":0,"uname":"死灵狸猫","rank":1,"coin":1400000,"list":[{"uid":696935,"uname":"死灵狸猫","face":"https://i2.hdslb.com/bfs/face/e22c5fdc6df3fa04856b9fbed31a6630a391ef1d.jpg","rank":1,"score":1400000,"guard_level":0,"isSelf":1,"coin":1400000},{"uid":668831,"uname":"ashastraea","face":"https://i2.hdslb.com/bfs/face/a0ceccd7f54134fa8a7c0919653c8b310e9d9c27.jpg","rank":2,"score":141800,"guard_level":0,"isSelf":0,"coin":141800},{"uid":267887,"uname":"绅士小箱子","face":"https://i1.hdslb.com/bfs/face/ab8168357ed48abd7c954bb8f2e98df6cf28fa13.jpg","rank":3,"score":58500,"guard_level":0,"isSelf":0,"coin":58500},{"uid":3006555,"uname":"xiaozhou103683","face":"https://i1.hdslb.com/bfs/face/d7cdb62d663c33b5ceb11d45972c5271013dec0d.jpg","rank":4,"score":52400,"guard_level":0,"isSelf":0,"coin":52400},{"uid":3755580,"uname":"包心菜国王","face":"https://i1.hdslb.com/bfs/face/f90035c3090c915fb31b14475dc4576c88236c30.gif","rank":5,"score":40700,"guard_level":0,"isSelf":0,"coin":40700},{"uid":14348416,"uname":"艾洛耶斯","face":"https://i0.hdslb.com/bfs/face/310366f2eaaf8012e93c8bdb510f40982f8356a5.jpg","rank":6,"score":26700,"guard_level":0,"isSelf":0,"coin":26700},{"uid":5730289,"uname":"暗影惊魂","face":"https://i0.hdslb.com/bfs/face/0e93512a80af33957a456a7eff5363ff187b15a3.jpg","rank":7,"score":7100,"guard_level":0,"isSelf":0,"coin":7100}],"rank_text":"1"}
        /// </summary>
        [JsonProperty("data")]
        public T Data { get; set; }

        /// <summary>
        /// Examples: "OK"
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// Examples: "OK"
        /// </summary>
        [JsonProperty("msg")]
        public string Msg { get; set; }
    }

    public class GiftRankItem
    {
        /// <summary>
        /// Examples: 1400000, 141800, 58500, 52400, 40700
        /// </summary>
        [JsonProperty("coin")]
        public int Coin { get; set; }

        /// <summary>
        /// Examples: "https://i2.hdslb.com/bfs/face/e22c5fdc6df3fa04856b9fbed31a6630a391ef1d.jpg", "https://i2.hdslb.com/bfs/face/a0ceccd7f54134fa8a7c0919653c8b310e9d9c27.jpg", "https://i1.hdslb.com/bfs/face/ab8168357ed48abd7c954bb8f2e98df6cf28fa13.jpg", "https://i1.hdslb.com/bfs/face/d7cdb62d663c33b5ceb11d45972c5271013dec0d.jpg", "https://i1.hdslb.com/bfs/face/f90035c3090c915fb31b14475dc4576c88236c30.gif"
        /// </summary>
        [JsonProperty("face")]
        public string Face { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("guard_level")]
        public int GuardLevel { get; set; }

        /// <summary>
        /// Examples: 1, 0
        /// </summary>
        [JsonProperty("isSelf")]
        public int IsSelf { get; set; }

        /// <summary>
        /// Examples: 1, 2, 3, 4, 5
        /// </summary>
        [JsonProperty("rank")]
        public int Rank { get; set; }

        /// <summary>
        /// Examples: 1400000, 141800, 58500, 52400, 40700
        /// </summary>
        [JsonProperty("score")]
        public int Score { get; set; }

        /// <summary>
        /// Examples: 696935, 668831, 267887, 3006555, 3755580
        /// </summary>
        [JsonProperty("uid")]
        public int Uid { get; set; }

        /// <summary>
        /// Examples: "死灵狸猫", "ashastraea", "绅士小箱子", "xiaozhou103683", "包心菜国王"
        /// </summary>
        [JsonProperty("uname")]
        public string Uname { get; set; }
    }

    public class GiftTopDto
    {
        /// <summary>
        /// Examples: 1400000
        /// </summary>
        [JsonProperty("coin")]
        public int Coin { get; set; }

        /// <summary>
        /// Examples: [{"uid":696935,"uname":"死灵狸猫","face":"https://i2.hdslb.com/bfs/face/e22c5fdc6df3fa04856b9fbed31a6630a391ef1d.jpg","rank":1,"score":1400000,"guard_level":0,"isSelf":1,"coin":1400000},{"uid":668831,"uname":"ashastraea","face":"https://i2.hdslb.com/bfs/face/a0ceccd7f54134fa8a7c0919653c8b310e9d9c27.jpg","rank":2,"score":141800,"guard_level":0,"isSelf":0,"coin":141800},{"uid":267887,"uname":"绅士小箱子","face":"https://i1.hdslb.com/bfs/face/ab8168357ed48abd7c954bb8f2e98df6cf28fa13.jpg","rank":3,"score":58500,"guard_level":0,"isSelf":0,"coin":58500},{"uid":3006555,"uname":"xiaozhou103683","face":"https://i1.hdslb.com/bfs/face/d7cdb62d663c33b5ceb11d45972c5271013dec0d.jpg","rank":4,"score":52400,"guard_level":0,"isSelf":0,"coin":52400},{"uid":3755580,"uname":"包心菜国王","face":"https://i1.hdslb.com/bfs/face/f90035c3090c915fb31b14475dc4576c88236c30.gif","rank":5,"score":40700,"guard_level":0,"isSelf":0,"coin":40700},{"uid":14348416,"uname":"艾洛耶斯","face":"https://i0.hdslb.com/bfs/face/310366f2eaaf8012e93c8bdb510f40982f8356a5.jpg","rank":6,"score":26700,"guard_level":0,"isSelf":0,"coin":26700},{"uid":5730289,"uname":"暗影惊魂","face":"https://i0.hdslb.com/bfs/face/0e93512a80af33957a456a7eff5363ff187b15a3.jpg","rank":7,"score":7100,"guard_level":0,"isSelf":0,"coin":7100}]
        /// </summary>
        [JsonProperty("list")]
        public IList<GiftRankItem> List { get; set; }

        /// <summary>
        /// Examples: 1
        /// </summary>
        [JsonProperty("rank")]
        public int Rank { get; set; }

        /// <summary>
        /// Examples: "1"
        /// </summary>
        [JsonProperty("rank_text")]
        public string RankText { get; set; }

        /// <summary>
        /// Examples: "死灵狸猫"
        /// </summary>
        [JsonProperty("uname")]
        public string Uname { get; set; }

        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("unlogin")]
        public int Unlogin { get; set; }
    }

    public class GuardTopDto
    {
        /// <summary>
        /// Examples: {"num":263,"page":26,"now":1,"achievement_level":2}
        /// </summary>
        [JsonProperty("info")]
        public GuardPageInfo Info { get; set; }

        /// <summary>
        /// Examples: [{"uid":347672,"ruid":433351,"rank":1,"username":"鯊魚甜椒","face":"http://i1.hdslb.com/bfs/face/b6a2c0911146ad2ccf0b1aa09dc24920000a3df2.jpg","is_alive":0,"guard_level":2,"guard_sub_level":0},{"uid":1519536,"ruid":433351,"rank":2,"username":"不知易","face":"http://i1.hdslb.com/bfs/face/192a121e8cf4f4eafb266e314d1aa141ebdcb6fd.jpg","is_alive":0,"guard_level":2,"guard_sub_level":0},{"uid":22257358,"ruid":433351,"rank":3,"username":"布兰緹什","face":"http://i1.hdslb.com/bfs/face/2c3f79c62a472d816e739105840ab3c9263a586a.jpg","is_alive":0,"guard_level":2,"guard_sub_level":0},{"uid":25802063,"ruid":433351,"rank":4,"username":"墨炎之殇","face":"http://i2.hdslb.com/bfs/face/f449aa31e26964362dfda1a8e0519bcc30ef1ba2.jpg","is_alive":0,"guard_level":2,"guard_sub_level":0},{"uid":132464,"ruid":433351,"rank":5,"username":"北极瘦企鹅","face":"http://i1.hdslb.com/bfs/face/41fef23e5b0cca0b3488549bf5c3b18e71be8ef4.jpg","is_alive":1,"guard_level":3,"guard_sub_level":0},{"uid":317000,"ruid":433351,"rank":6,"username":"adogsama","face":"http://i1.hdslb.com/bfs/face/08cabd600d65302deecb738a8ca709f7ccd3c99a.jpg","is_alive":1,"guard_level":3,"guard_sub_level":0},{"uid":6512369,"ruid":433351,"rank":7,"username":"lavenirz","face":"http://i1.hdslb.com/bfs/face/4b3d9c6e8972425f0588b1ffea78f0a0e200c2a3.jpg","is_alive":1,"guard_level":3,"guard_sub_level":0},{"uid":7123831,"ruid":433351,"rank":8,"username":"紫小陌23333","face":"http://i0.hdslb.com/bfs/face/2842211195fcf2d5c1f09df12969a70c4ecf481e.jpg","is_alive":1,"guard_level":3,"guard_sub_level":0},{"uid":7300924,"ruid":433351,"rank":9,"username":"白狐澪桜","face":"http://i2.hdslb.com/bfs/face/6ccc5f5d4d9f7ac465afa14971e0c1e2e88e59ed.jpg","is_alive":1,"guard_level":3,"guard_sub_level":0},{"uid":11544959,"ruid":433351,"rank":10,"username":"DenggelYonany","face":"http://i2.hdslb.com/bfs/face/4da9b4f4165217710bee85c0ff79fbe7708e1f76.jpg","is_alive":1,"guard_level":3,"guard_sub_level":0}]
        /// </summary>
        [JsonProperty("list")]
        public IList<GuardItem> List { get; set; }

        /// <summary>
        /// Examples: [{"uid":911480,"ruid":433351,"rank":1,"username":"无火的残渣","face":"http://i0.hdslb.com/bfs/face/c8eba82a9be40d5bbebadf0e731815620f9a8c5a.jpg","is_alive":0,"guard_level":1,"guard_sub_level":0},{"uid":15071604,"ruid":433351,"rank":2,"username":"大王大小姐","face":"http://i0.hdslb.com/bfs/face/958e00c3088f6cc513c8b9b1b54164ac4b264e7b.jpg","is_alive":0,"guard_level":1,"guard_sub_level":0},{"uid":245957,"ruid":433351,"rank":3,"username":"Poodle一直睡","face":"http://i1.hdslb.com/bfs/face/f5871bc61005e18c9ce79ebecd067f33438bedbc.jpg","is_alive":0,"guard_level":2,"guard_sub_level":0}]
        /// </summary>
        [JsonProperty("top3")]
        public IList<GuardItem> Top3 { get; set; }

        public class GuardPageInfo
        {
            /// <summary>
            /// Examples: 2
            /// </summary>
            [JsonProperty("achievement_level")]
            public int AchievementLevel { get; set; }

            /// <summary>
            /// Examples: 1
            /// </summary>
            [JsonProperty("now")]
            public int Now { get; set; }

            /// <summary>
            /// Examples: 263
            /// </summary>
            [JsonProperty("num")]
            public int Num { get; set; }

            /// <summary>
            /// Examples: 26
            /// </summary>
            [JsonProperty("page")]
            public int Page { get; set; }
        }

        public class GuardItem
        {
            /// <summary>
            /// Examples: "http://i1.hdslb.com/bfs/face/b6a2c0911146ad2ccf0b1aa09dc24920000a3df2.jpg", "http://i1.hdslb.com/bfs/face/192a121e8cf4f4eafb266e314d1aa141ebdcb6fd.jpg", "http://i1.hdslb.com/bfs/face/2c3f79c62a472d816e739105840ab3c9263a586a.jpg", "http://i2.hdslb.com/bfs/face/f449aa31e26964362dfda1a8e0519bcc30ef1ba2.jpg", "http://i1.hdslb.com/bfs/face/41fef23e5b0cca0b3488549bf5c3b18e71be8ef4.jpg"
            /// </summary>
            [JsonProperty("face")]
            public string Face { get; set; }

            /// <summary>
            /// Examples: 2, 3
            /// </summary>
            [JsonProperty("guard_level")]
            public int GuardLevel { get; set; }

            /// <summary>
            /// Examples: 0
            /// </summary>
            [JsonProperty("guard_sub_level")]
            public int GuardSubLevel { get; set; }

            /// <summary>
            /// Examples: 0, 1
            /// </summary>
            [JsonProperty("is_alive")]
            public int IsAlive { get; set; }

            /// <summary>
            /// Examples: 1, 2, 3, 4, 5
            /// </summary>
            [JsonProperty("rank")]
            public int Rank { get; set; }

            /// <summary>
            /// Examples: 433351
            /// </summary>
            [JsonProperty("ruid")]
            public int Ruid { get; set; }

            /// <summary>
            /// Examples: 347672, 1519536, 22257358, 25802063, 132464
            /// </summary>
            [JsonProperty("uid")]
            public int Uid { get; set; }

            /// <summary>
            /// Examples: "鯊魚甜椒", "不知易", "布兰緹什", "墨炎之殇", "北极瘦企鹅"
            /// </summary>
            [JsonProperty("username")]
            public string Username { get; set; }
        }
    }


    public class RoomUserDataDto : Entity<RoomUserDataDto>
    {
        public long room_id { get; set; }
        public long uid { get; set; }
        public long fans_num { get; set; }
        public long follow_num { get; set; }
        public long guard_num { get; set; }
    }
}