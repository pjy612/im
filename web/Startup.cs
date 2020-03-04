using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using aspCore.Extensions;
using BiliEntity;
using Microsoft.AspNetCore.Cors.Infrastructure;
using NewLife.Log;
using NewLife.Threading;
using Swashbuckle.AspNetCore.Swagger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new Info()); });
            services.Add(ServiceDescriptor.Transient<ICorsService, WildcardCorsService>());
            services.AddCors(options =>
            {
                options.AddPolicy("free", cors =>
                    cors.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().AllowCredentials()
                );
                options.AddPolicy("bilibili", cors =>
                    cors.WithOrigins("*.bilibili.com", "*.localhost")
                        .AllowAnyHeader().AllowAnyMethod()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowCredentials()
                );
            });
        }


        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            XTrace.UseConsole();
//            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
//            Console.OutputEncoding = Encoding.GetEncoding("GB2312");
//            Console.InputEncoding  = Encoding.GetEncoding("GB2312");
//            loggerFactory.AddConsole(LogLevel.Error);

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseCors("bilibili");
//            app.UseCors("free");
            app.UseMvc();
            app.UseSwagger().UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "HKERP API V1"); });

            string redis = Configuration.GetSection("cfg:redis").Get<string>()         ?? "127.0.0.1:6379,poolsize=5";
            string[] servers = Configuration.GetSection("cfg:servers").Get<string[]>() ?? new string[] {"bilipush.1024dream.net:7777"};
            RedisHelper.Initialization(new CSRedis.CSRedisClient(redis));
            //RedisHelper.Del("UserMap");
            ImHelper.Initialization(new ImClientOptions
            {
                Redis   = RedisHelper.Instance,
                Servers = servers
            });

            ImHelper.Instance.OnSend += (s, e) =>
                Console.WriteLine($"ImClient.SendMessage(server={e.Server},data={JsonConvert.SerializeObject(e.Message)})");

            ImHelper.EventBus(
                t =>
                {
//                    Console.WriteLine(t.clientId + "上线了");
//                    var onlineUids = ImHelper.GetClientListByOnline();
//                    ImHelper.SendMessage(t.clientId, onlineUids, $"用户{t.clientId}上线了");
                },
                t =>
                {
                    //Console.WriteLine(t.clientId + "下线了");
                });

            IList<RoomInitList> roomInitLists = RoomInitList.FindAll(RoomInitList._.Uid.IsNull());
            foreach (var roomInitList in roomInitLists)
            {
                roomInitList.Uid = roomInitList.Data.Data.Uid;
                roomInitList.SaveAsync();
            }
            //            TimerX timerX = new TimerX(state =>
            //            {
            //                Console.WriteLine($"RoomInitQueue:${RoomQueue.RoomInitQueue.Value.Statistics}");
            //                Console.WriteLine($"GetFanGiftsQueue:${RoomQueue.GetFanGiftsQueue.Value.Statistics}");
            //                Console.WriteLine($"GetFollowNumQueue:${RoomQueue.GetFollowNumQueue.Value.Statistics}");
            //                Console.WriteLine($"GuardTopQueue:${RoomQueue.GuardTopQueue.Value.Statistics}");
            //                Console.WriteLine($"RoomSortQueue:${RoomQueue.RoomSortQueue.Value.Statistics}");
            //            }, null, 5_000, 5_000) {Async = true};
            TimerX autoCheck = new TimerX(state =>
                {
                    IList<RoomInitList> rooms = RoomInitList.FindAll(RoomInitList._.RoomID.NotIn(RoomSort.FindSQL(null, null, RoomSort._.RoomID)), selects: RoomInitList._.RoomID);
                    List<long> updateIds = new List<long>();
                    if (rooms.Any())
                    {
                        updateIds.AddRange(rooms.Select(r => r.RoomID));
                    }
                    IList<RoomSort> roomSorts = RoomSort.FindAll(RoomSort._.LastUpdateTime < DateTime.Now.AddDays(-3), selects: RoomSort._.RoomID);
                    if (roomSorts.Any())
                    {
                        updateIds.AddRange(roomSorts.Select(r => r.RoomID));
                    }
                    if (updateIds.Any())
                    {
                        updateIds.ForEach(r => RoomQueue.QueueLazy.Value.QueueRoomSet.Add(r));
                    }
                }, null, TimeSpan.FromSeconds(10).TotalMilliseconds.ToInt(), TimeSpan.FromMinutes(10).TotalMilliseconds.ToInt())
                {Async = true, CanExecute = () => !RoomQueue.QueueLazy.Value.QueueRoomSet.Any()};


            //            new TimerX(state =>
            //            {
            //                IList<RoomInitList> allRoom = RoomInitList.FindAll();
            //                int i = 0;
            //                int size = 100;
            //                List<RoomInitList> rooms;
            //                do
            //                {
            //                    rooms = allRoom.Skip(i * size).Take(size).ToList();
            //                    if(!rooms.Any()) break;
            //                    List<long> roomIds = rooms.Select(r=>r.RoomID).ToList();
            //                    List<long> uids = rooms.Select(r=>r.Uid).ToList();
            //                    IList<GuardTop> guardTops = GuardTop.FindAll(GuardTop._.Uid.In(uids));
            //                    IList<FollowNum> followNums = FollowNum.FindAll(FollowNum._.Uid.In(uids));
            //                    IList<FanGifts> fans = FanGifts.FindAll(FanGifts._.RoomID.In(roomIds));
            //                    foreach (RoomInitList r in rooms)
            //                    {
            //                        new RoomSort()
            //                        {
            //                            RoomID    = r.RoomID,
            //                            Uid       = r.Uid,
            //                            FansNum   = fans.FirstOrDefault(x => x.RoomID == r.RoomID)?.Num ?? 0,
            //                            FollowNum = followNums.FirstOrDefault(x => x.Uid == r.Uid)?.Num ?? 0,
            //                            GuardNum  = guardTops.FirstOrDefault(x => x.Uid == r.Uid)?.Num  ?? 0,
            //                        }.SaveAsync();
            //                    }
            //                    i++;
            //                    Thread.Sleep(100);
            //                } while (rooms.Any());
            //            }, null, 5000, 10 * 60_000);
        }
    }
}