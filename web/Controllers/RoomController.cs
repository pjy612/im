using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiliEntity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NewLife.Caching;
using NewLife.Serialization;
using NewLife.Threading;
using Newtonsoft.Json;
using XCode.Cache;

namespace web.Controllers
{
    [Route("room")]
    public class RoomController : Controller
    {
        private static readonly List<long> staticRoomList = new List<long>();

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
                    RoomQueue.QueueLazy.Value.QueueRoomSet.Add(id);
                    return new {code = -1};
                }
                return JsonConvert.DeserializeObject<RoomInit>(roomInitList.Message);
            }
            return new {code = -1};
        }

        [HttpGet("v1/Room/sort/force")]
        public object room_sort_ex(long id)
        {
            var entity = RoomSort.FindByKey(id);
            if (entity == null)
            {
                return RoomQueue.QueueLazy.Value.RoomSortForce(id);
            }
            return entity;
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

        const string roomSortCacheKey = nameof(room_sort);

        [HttpGet("v1/Room/sort")]
        public object room_sort(int page = 0, int size = 5000, bool full = false)
        {
            if (size <= 1000) size = 1000;
            List<RoomUserDataDto> roomUserDataDtos = room_sort_nocahce();
            dynamic data = new System.Dynamic.ExpandoObject();
            data.code        = 0;
            data.page        = page;
            data.size        = size;
            data.page_count  = (roomUserDataDtos.Count - 1) / size + 1;
            data.total_count = roomUserDataDtos.Count;
            List<RoomUserDataDto> sorted = roomUserDataDtos.Skip(page * size).Take(size).ToList();
            data.room_ids = sorted.Select(r => r.room_id).ToArray();
            if (full)
            {
                data.data = sorted.ToArray();
            }
            return data;
        }

        [HttpGet("v1/Room/all")]
        public object room_sort()
        {
            dynamic data = new System.Dynamic.ExpandoObject();
            List<RoomUserDataDto> sorted = room_sort_nocahce();
            data.code = 0;
            data.data = sorted.Select(r => r.room_id).ToArray();
            return data;
        }

        [HttpGet("v1/Room/list")]
        public object room_sort(int page = 0, int size = 5000)
        {
            if (size <= 1000) size = 1000;
            List<RoomUserDataDto> roomUserDataDtos = room_sort_nocahce();
            roomUserDataDtos.ForEach(r =>
            {
                if (!staticRoomList.Contains(r.room_id))
                {
                    staticRoomList.Add(r.room_id);
                }
            });
            dynamic data = new System.Dynamic.ExpandoObject();
            data.code        = 0;
            data.page        = page;
            data.size        = size;
            data.page_count  = (staticRoomList.Count - 1) / size + 1;
            data.total_count = staticRoomList.Count;
            data.roomid      = staticRoomList.Skip(page * size).Take(size).ToList();
            return data;
        }

        private List<RoomUserDataDto> room_sort_nocahce()
        {
            IList<RoomSort> findAllWithCache = RoomSort.FindAllWithCache();
            List<RoomUserDataDto> userDatas = findAllWithCache.Select(r => new RoomUserDataDto
            {
                room_id    = r.RoomID,
                uid        = r.Uid,
                fans_num   = r.FansNum,
                follow_num = r.FollowNum,
                guard_num  = r.GuardNum,
            }).ToList();
            return userDatas
                .Where(r => r.fans_num > 5 || r.follow_num > 300 || r.guard_num > 0)
                .OrderByDescending(r => r.guard_num).ThenByDescending(r => r.fans_num).ThenByDescending(r => r.fans_num).ToList();
        }
    }
}