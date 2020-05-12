using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BiliEntity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NewLife.Caching;
using NewLife.Serialization;
using NewLife.Threading;
using Newtonsoft.Json;
using RestSharp;
using XCode.Cache;

namespace web.Controllers
{
    [Route("room")]
    public class RoomController : Controller
    {
        public RoomController()
        {
        }

        [HttpGet("v1/Room/room_init")]
        public object room_init(long id)
        {
            if (id > 0)
            {
                RoomInitList roomInitList = RoomInitList.FindByKey(id);
                if (roomInitList == null)
                {
                    RoomQueue.Instance.QueueRoomSet.Add(id);
                    return new {code = -1};
                }
                else
                {
                    var entity = RoomSort.FindByKey(id);
                    if (entity == null || entity.LastUpdateTime < DateTime.Today.AddDays(-1))
                    {
                        RoomQueue.Instance.QueueRoomSet.Add(id);
                    }
                }
                return new {code = -1, data = roomInitList};
            }
            return new {code = -1};
        }

        [HttpGet("v1/Danmu/getConf")]
        public async Task<object> getConf(long room_id)
        {
            RestClient client = new RestClient();
            RestRequest req = new RestRequest($"https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id={room_id}&platform=pc&player=web", Method.GET);
            IRestResponse<object> task = await client.ExecuteGetTaskAsync<object>(req);
            return task.Data;
        }

        [HttpPost("v1/Room/room_init_list")]
        public object room_init_list([FromBody] List<long> ids)
        {
            if (ids != null && ids.Any())
            {
                Task.Run(() =>
                {
                    ids.RemoveAll(r => RoomQueue.Instance.QueueRoomSet.Contains(r));
                    ids.RemoveAll(r => RoomQueue.Instance.ProcessCollection.Contains(r));
                    List<long> allRoomIds = RoomQueue.Instance.GetAllRoomIds();
                    ids.RemoveAll(r => allRoomIds.Contains(r));
                    if (ids.Any())
                    {
                        ids.AsParallel().ForAll(r => RoomQueue.Instance.QueueRoomSet.Add(r));
                    }
                });
                return new {code = 0};
            }
            return new {code = -1};
        }

        [HttpGet("v1/Room/room/all")]
        public object room_all()
        {
            var ids = RoomQueue.Instance.GetAllRoomIds();
            return new
            {
                code = 0,
                data = ids.Distinct().ToList()
            };
        }

        [HttpGet("v1/Room/sort/force")]
        public async Task<RoomSort> room_sort_ex(long id)
        {
            return await RoomQueue.Instance.RoomSortForce(id, true);
//            RoomInitList room = RoomInitList.FindByKey(id);
//            if (room == null)
//            {
//                RoomInit init = RoomQueue.GetRoomInitByRoomId(id);
//                if (init?.Code == 0)
//                {
//                    room.Uid            = init.Data.Uid;
//                    room.Message        = JsonConvert.SerializeObject(init);
//                    room.LastUpdateTime = DateTime.Now.AddMinutes(30);
//                    room.SaveAsync();
//                    GuardTop guardTop = GuardTop.FindByKey(room.Uid);
//                    FollowNum followNum = FollowNum.FindByKey(room.Uid);
//                    FanGifts fan = FanGifts.FindByKey(room.RoomID);
//                    RoomRsp<GiftTopDto> fanGiftsByRoomId = RoomQueue.GetFanGiftsByRoomId(id);
//                    if (fanGiftsByRoomId.Code == 0)
//                    {
//                        fan.Num            = fanGiftsByRoomId.Data.List.Count;
//                        fan.Message        = JsonConvert.SerializeObject(fanGiftsByRoomId);
//                        fan.LastUpdateTime = DateTime.Now.AddMinutes(30);
//                        fan.SaveAsync();
//                    }
//                    RoomRsp<GuardTopDto> dto = RoomQueue.GetGuardTopByUid(room.Uid);
//                    if (dto.Code == 0)
//                    {
//                        guardTop.Num            = dto.Data?.Info?.Num ?? 0;
//                        guardTop.Data           = JsonConvert.SerializeObject(dto);
//                        guardTop.LastUpdateTime = DateTime.Now.AddMinutes(30);
//                        guardTop.SaveAsync();
//                    }
//                    RoomRsp<FollowNumDto> followNumByUid = RoomQueue.GetFollowNumByUid(room.Uid);
//                    if (followNumByUid.Code == 0)
//                    {
//                        followNum.Num            = followNumByUid.Data.Fc;
//                        followNum.Data           = JsonConvert.SerializeObject(followNumByUid);
//                        followNum.LastUpdateTime = DateTime.Now.AddMinutes(30);
//                        followNum.SaveAsync();
//                    }
//                    RoomQueue.GetFollowNumByUid(init.Data.Uid);
//                    RoomQueue.GetGuardTopByUid(init.Data.Uid);
//
//                    var entity = RoomSort.FindByKey(id);
//                    entity.Uid            = room.Uid;
//                    entity.FansNum        = fan?.Num       ?? 0;
//                    entity.FollowNum      = followNum?.Num ?? 0;
//                    entity.GuardNum       = guardTop?.Num  ?? 0;
//                    entity.LastUpdateTime = DateTime.Now.AddMinutes(30);
//                    entity.SaveAsync();
//                }
//            }
//            return JsonConvert.DeserializeObject<RoomInit>(room?.Message);
        }

//        [HttpGet("v1/Room/sort")]
//        public async Task<object> room_sort(int page = 0, int size = 5000, bool full = false)
//        {
//            if (size <= 1000) size = 1000;
//            List<RoomUserDataDto> roomUserDataDtos = await room_sort_nocahce();
//            dynamic data = new System.Dynamic.ExpandoObject();
//            data.code        = 0;
//            data.page        = page;
//            data.size        = size;
//            data.page_count  = (roomUserDataDtos.Count - 1) / size + 1;
//            data.total_count = roomUserDataDtos.Count;
//            List<RoomUserDataDto> sorted = roomUserDataDtos.Skip(page * size).Take(size).ToList();
//            data.room_ids = sorted.Select(r => r.room_id).ToArray();
//            if (full)
//            {
//                data.data = sorted.ToArray();
//            }
//            return data;
//        }

        [HttpGet("v1/Room/sort/all")]
        public async Task<object> room_sort_all()
        {
            var sorted = (await room_sort_nocahce()).Select(r => r.room_id).ToArray();
            //List<RoomUserDataDto> sorted = await room_sort_nocahce();
            return new
            {
                code = 0,
                data = sorted
            };
        }

//        [HttpGet("v1/Room/sort/list")]
//        public async Task<object> room_sort_list(int page = 0, int size = 5000)
//        {
//            if (size <= 1000) size = 1000;
//            List<RoomUserDataDto> roomUserDataDtos = await room_sort_nocahce();
//            roomUserDataDtos.ForEach(r =>
//            {
//                if (!staticRoomList.Contains(r.room_id))
//                {
//                    staticRoomList.Add(r.room_id);
//                }
//            });
//            dynamic data = new System.Dynamic.ExpandoObject();
//            data.code        = 0;
//            data.page        = page;
//            data.size        = size;
//            data.page_count  = (staticRoomList.Count - 1) / size + 1;
//            data.total_count = staticRoomList.Count;
//            data.roomid      = staticRoomList.Skip(page * size).Take(size).ToList();
//            return data;
//        }
        private static SemaphoreSlim cacheLock = new SemaphoreSlim(1, 1);


        private async Task<List<RoomUserDataDto>> room_sort_nocahce()
        {
            await cacheLock.WaitAsync();
            try
            {
                return RoomQueue.Instance.GetSort()
                    .Where(r => (r.fans_num > 3 && r.follow_num > 300) || r.guard_num > 0)
                    .OrderByDescending(r => r.guard_num).ThenByDescending(r => r.fans_num).ThenByDescending(r => r.follow_num).ToList();
            }
            finally
            {
                cacheLock.Release();
            }
        }
    }
}