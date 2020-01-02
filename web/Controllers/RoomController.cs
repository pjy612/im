using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiliEntity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public DateTime LastCache = DateTime.MinValue;
        private TimerX reloadCacheTimer = null;
        public RoomController()
        {
            reloadCacheTimer = new TimerX(state =>
            {
                if (LastCache < DateTime.Now)
                {
                    Cache.Default.Set(roomSortCacheKey, room_sort_nocahce(), TimeSpan.FromMinutes(1));
                    LastCache = DateTime.Now.AddMinutes(1);
                }
            },null,0,30_000);
        }

        [HttpGet("v1/Room/room_init")]
        public object room_init(long id)
        {
            RoomQueue.RoomInitQueue.Value.Enqueue(id);
            RoomInitList roomInitList = RoomInitList.FindByKey(id);
            if (roomInitList == null)
            {
                return new {code = -1};
            }
            return JsonConvert.DeserializeObject<RoomInit>(roomInitList.Message);
        }
        const string roomSortCacheKey = nameof(room_sort);
        [HttpGet("v1/Room/sort")]
        public object room_sort(int page = 0, int size = 5000, bool full = false)
        {
            if (size <= 1000) size = 1000;
            List<RoomUserDataDto> roomUserDataDtos = Cache.Default.Get<List<RoomUserDataDto>>(roomSortCacheKey) ?? new List<RoomUserDataDto>();
            if (!roomUserDataDtos.Any())
            {
                roomUserDataDtos = room_sort_nocahce();
                Cache.Default.Set(roomSortCacheKey, roomUserDataDtos, TimeSpan.FromMinutes(1));
                LastCache = DateTime.Now.AddMinutes(1);
            }
            dynamic data = new System.Dynamic.ExpandoObject();
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
        private List<long> staticRoomList = new List<long>();
        [HttpGet("v1/Room/list")]
        public object room_sort(int page = 0, int size = 5000)
        {
            if (size <= 1000) size = 1000;
            List<RoomUserDataDto> roomUserDataDtos = Cache.Default.Get<List<RoomUserDataDto>>(roomSortCacheKey) ?? new List<RoomUserDataDto>();
            if (!roomUserDataDtos.Any())
            {
                roomUserDataDtos = room_sort_nocahce();
                Cache.Default.Set(roomSortCacheKey, roomUserDataDtos, TimeSpan.FromMinutes(1));
                LastCache = DateTime.Now.AddMinutes(1);
            }
            roomUserDataDtos.ForEach(r =>
            {
                if (!staticRoomList.Contains(r.room_id))
                {
                    staticRoomList.Add(r.room_id);
                }
            });
            dynamic data = new System.Dynamic.ExpandoObject();
            data.code = 0;
            data.page        = page;
            data.size        = size;
            data.page_count  = (staticRoomList.Count - 1) / size + 1;
            data.total_count = staticRoomList.Count;
            data.roomid = staticRoomList.Skip(page * size).Take(size).ToList();
            return data;
        }

        private List<RoomUserDataDto> room_sort_nocahce()
        {
            IList<RoomInitList> allRoom = RoomInitList.FindAllWithCache();
            List<RoomInit> roomInits = allRoom.AsParallel().Select(r => r.Data).ToList();
            IList<GuardTop> guardTops = GuardTop.FindAllWithCache();
            IList<FollowNum> followNums = FollowNum.FindAllWithCache();
            IList<FanGifts> fans = FanGifts.FindAllWithCache();
            List<RoomUserDataDto> userDatas = allRoom.Select(r =>
            {
                RoomInit roomInit = roomInits.FirstOrDefault(x => x.Data.RoomId == r.RoomID);
                return new RoomUserDataDto
                {
                    room_id    = r.RoomID,
                    uid        = roomInit?.Data.Uid                                               ?? 0,
                    fans_num   = fans.FirstOrDefault(x => x.RoomID == r.RoomID)?.Num              ?? 0,
                    follow_num = followNums.FirstOrDefault(x => x.Uid == roomInit?.Data.Uid)?.Num ?? 0,
                    guard_num  = guardTops.FirstOrDefault(x => x.Uid == roomInit?.Data.Uid)?.Num  ?? 0,
                };
            }).ToList();
            return userDatas.OrderByDescending(r => r.guard_num).ThenByDescending(r => r.fans_num).ThenByDescending(r => r.fans_num).ToList();
        }
    }
}