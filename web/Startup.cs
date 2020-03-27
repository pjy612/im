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
            
//            TimerX ShowState = new TimerX(state =>
//                {
//                    //if (queueLazyValue.RoomNeedLoad.Count > 0 || queueLazyValue.QueueRoomSet.Count > 0 || queueLazyValue.ProcessCollection.Count > 0)
//                    {
//                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} RoomNeedLoad:{RoomQueue.Instance.RoomNeedLoad.Count},RoomQueue:{RoomQueue.Instance.QueueRoomSet.Count},Process:{RoomQueue.Instance.ProcessCollection.Count}");
//                    }
//                }, null, 1_000, 1_000)
//                { Async = true };
            TimerX autoCheck = new TimerX(state =>
            {
                if (!RoomQueue.Instance.RoomNeedLoad.Any())
                {
                    IList<RoomInitList> rooms = RoomInitList.FindAll(RoomInitList._.RoomID.NotIn(RoomSort.FindSQL(null, null, RoomSort._.RoomID)), selects: RoomInitList._.RoomID);
                    if (rooms.Any())
                    {
                        RoomQueue.Instance.RoomNeedLoad.AddRange(rooms.Select(r => r.RoomID));
                    }
                    var dateLimit = DateTime.Today.AddDays(-1);
                    IList<RoomSort> roomSorts = RoomSort.FindAll(RoomSort._.LastUpdateTime < dateLimit, $"{RoomSort._.LastUpdateTime} asc", RoomSort._.RoomID, 0, 0);
                    if (roomSorts.Any())
                    {
                        RoomQueue.Instance.RoomNeedLoad.AddRange(roomSorts.Select(r => r.RoomID));
                    }
                    RoomQueue.Instance.RoomNeedLoad.RemoveAll(r => RoomQueue.Instance.ProcessCollection.Contains(r) || RoomQueue.Instance.QueueRoomSet.Contains(r));
                }
                var updateIds = RoomQueue.Instance.RoomNeedLoad.Distinct().Take(500).ToList();
                if (updateIds.Any())
                {
                    updateIds.Distinct().ToList().AsParallel().ForAll(r =>
                    {
                        if (!RoomQueue.Instance.ProcessCollection.Contains(r))
                        {
                            RoomQueue.Instance.ProcessCollection.Add(r);
                        }
                    });
                    RoomQueue.Instance.RoomNeedLoad.RemoveAll(c => updateIds.Contains(c));
                }
            }, null, 1000, 30_000)
            {
                Async      = true,
                CanExecute = () => RoomQueue.Instance.ProcessCollection.Count < 500
            };
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