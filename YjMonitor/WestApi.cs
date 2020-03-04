using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RestSharp;

namespace YjMonitorNet
{
    public class WestApi
    {
        private static RestClient _client = new RestClient("https://api.expublicsite.com:23333");
        private const string authName = "pjy612";
        private const string accessKey = "cd2c74521efd4ec1bdab06d9194a7d78";
        public static List<TvGiftDTO> GetTv()
        {
            RestRequest request = new RestRequest("bilibili/raffle/v1/getList");
            var execute = _client.Execute<HttpRspDTO<List<TvGiftDTO>>>(request);
            request.AddQueryParameter(nameof(authName), authName);
            request.AddQueryParameter(nameof(accessKey), accessKey);
            if (execute.Data.Code == 0)
            {
                return execute.Data.Data;
            }
            return new List<TvGiftDTO>();
        }
        public static List<GuardGiftDTO> GetGuard()
        {
            RestRequest request = new RestRequest("bilibili/guard/v1/getList");
            request.AddQueryParameter(nameof(authName), authName);
            request.AddQueryParameter(nameof(accessKey), accessKey);
            var execute = _client.Execute<HttpRspDTO<List<GuardGiftDTO>>>(request);
            if (execute.Data.Code == 0)
            {
                return execute.Data.Data;
            }
            return new List<GuardGiftDTO>();
        }
        public static List<StormGiftDTO> GetStorm()
        {
            RestRequest request = new RestRequest("bilibili/beatStorm/v1/getList");
            request.AddQueryParameter(nameof(authName), authName);
            request.AddQueryParameter(nameof(accessKey), accessKey);
            var execute = _client.Execute<HttpRspDTO<List<StormGiftDTO>>>(request);
            if (execute.Data.Code == 0)
            {
                return execute.Data.Data;
            }
            return new List<StormGiftDTO>();
        }
        //pkLottery
    }

    public class HttpRspDTO<T>
    {
        /// <summary>
        /// Examples: 0
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Examples: "Success"
        /// </summary>
        [JsonProperty("msg")]
        public string Msg { get; set; }

        /// <summary>
        /// Examples: []
        /// </summary>
        [JsonProperty("data")]
        public T Data { get; set; }
    }
    public class TvGiftDTO
    {
        /// <summary>
        /// Examples: 740417
        /// </summary>
        [JsonProperty("Id")]
        public long Id { get; set; }

        /// <summary>
        /// Examples: 13204342
        /// </summary>
        [JsonProperty("RoomId")]
        public long RoomId { get; set; }

        /// <summary>
        /// Examples: "蛋霸霸OKing"
        /// </summary>
        [JsonProperty("Sender")]
        public string Sender { get; set; }

        /// <summary>
        /// Examples: 30405
        /// </summary>
        [JsonProperty("GiftId")]
        public long GiftId { get; set; }

        /// <summary>
        /// Examples: "33地图抽奖"
        /// </summary>
        [JsonProperty("GiftName")]
        public string GiftName { get; set; }

        /// <summary>
        /// Examples: "GIFT_30405"
        /// </summary>
        [JsonProperty("GiftTypeForJoin")]
        public string GiftTypeForJoin { get; set; }

        /// <summary>
        /// Examples: 1583214198
        /// </summary>
        [JsonProperty("Time")]
        public long Time { get; set; }

        /// <summary>
        /// Examples: 1583214318
        /// </summary>
        [JsonProperty("WaitEndTime")]
        public long WaitEndTime { get; set; }

        /// <summary>
        /// Examples: 1583214378
        /// </summary>
        [JsonProperty("EndTime")]
        public long EndTime { get; set; }




    }

    public class StormGiftDTO
    {

        /// <summary>
        /// Examples: 2135233950012
        /// </summary>
        [JsonProperty("Id")]
        public long Id { get; set; }

        /// <summary>
        /// Examples: 38227
        /// </summary>
        [JsonProperty("RoomId")]
        public long RoomId { get; set; }

        /// <summary>
        /// Examples: 1
        /// </summary>
        [JsonProperty("Count")]
        public long Count { get; set; }

        /// <summary>
        /// Examples: "糟了，是心动的感觉！"
        /// </summary>
        [JsonProperty("Content")]
        public string Content { get; set; }

        /// <summary>
        /// Examples: 1583217721
        /// </summary>
        [JsonProperty("Time")]
        public long Time { get; set; }


    }

    public class GuardGiftDTO
    {

        /// <summary>
        /// Examples: 2135021
        /// </summary>
        [JsonProperty("Id")]
        public long Id { get; set; }

        /// <summary>
        /// Examples: 11437906
        /// </summary>
        [JsonProperty("RoomId")]
        public long RoomId { get; set; }

        /// <summary>
        /// Examples: "小萝卜嘎嘣脆"
        /// </summary>
        [JsonProperty("MasterName")]
        public string MasterName { get; set; }

        /// <summary>
        /// Examples: 302118485
        /// </summary>
        [JsonProperty("MasterId")]
        public long MasterId { get; set; }

        /// <summary>
        /// Examples: "恋e奶优"
        /// </summary>
        [JsonProperty("Sender")]
        public string Sender { get; set; }

        /// <summary>
        /// Examples: 165583530
        /// </summary>
        [JsonProperty("SenderId")]
        public long SenderId { get; set; }

        /// <summary>
        /// Examples: 1583212952
        /// </summary>
        [JsonProperty("Time")]
        public long Time { get; set; }

        /// <summary>
        /// Examples: 1583214152
        /// </summary>
        [JsonProperty("EndTime")]
        public long EndTime { get; set; }

        /// <summary>
        /// Examples: 3
        /// </summary>
        [JsonProperty("Type")]
        public long Type { get; set; }


    }
}
