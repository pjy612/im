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
            List<RoomUserDataDto> roomUserDataDtos = room_sort_nocahce();
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
            return userDatas.OrderByDescending(r => r.guard_num).ThenByDescending(r => r.fans_num).ThenByDescending(r => r.fans_num).ToList();
        }
    }
}