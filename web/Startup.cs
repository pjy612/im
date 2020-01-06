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
using aspCore.Extensions;
using BiliEntity;
using Microsoft.AspNetCore.Cors.Infrastructure;
using NewLife.Threading;
using Swashbuckle.AspNetCore.Swagger;

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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.GetEncoding("GB2312");
            Console.InputEncoding  = Encoding.GetEncoding("GB2312");
            loggerFactory.AddConsole(LogLevel.Error);

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseCors("bilibili");
            //app.UseCors("free");
            app.UseMvc();
            app.UseSwagger().UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "HKERP API V1"); });

            string redis = Configuration.GetSection("cfg:redis").Get<string>()         ?? "127.0.0.1:6379,poolsize=5";
            string[] servers = Configuration.GetSection("cfg:servers").Get<string[]>() ?? new string[] {"bilipush.1024dream.net:7777"};

            ImHelper.Initialization(new ImClientOptions
            {
                Redis   = new CSRedis.CSRedisClient(redis),
                Servers = servers
            });

            ImHelper.Instance.OnSend += (s, e) =>
                Console.WriteLine($"ImClient.SendMessage(server={e.Server},data={JsonConvert.SerializeObject(e.Message)})");

            ImHelper.EventBus(
                t =>
                {
                    Console.WriteLine(t.clientId + "上线了");
//                    var onlineUids = ImHelper.GetClientListByOnline();
//                    ImHelper.SendMessage(t.clientId, onlineUids, $"用户{t.clientId}上线了");
                },
                t => Console.WriteLine(t.clientId + "下线了"));

            IList<RoomInitList> roomInitLists = RoomInitList.FindAll(RoomInitList._.Uid.IsNull());
            foreach (var roomInitList in roomInitLists)
            {
                roomInitList.Uid = roomInitList.Data.Data.Uid;
                roomInitList.SaveAsync();
            }
            new TimerX(state =>
            {
                IList<RoomInitList> allRoom = RoomInitList.FindAll();
                IList<GuardTop> guardTops = GuardTop.FindAll();
                IList<FollowNum> followNums = FollowNum.FindAll();
                IList<FanGifts> fans = FanGifts.FindAll();
                foreach (RoomInitList r in allRoom)
                {
                    new RoomSort()
                    {
                        RoomID    = r.RoomID,
                        Uid       = r.Uid,
                        FansNum   = fans.FirstOrDefault(x => x.RoomID == r.RoomID)?.Num ?? 0,
                        FollowNum = followNums.FirstOrDefault(x => x.Uid == r.Uid)?.Num ?? 0,
                        GuardNum  = guardTops.FirstOrDefault(x => x.Uid == r.Uid)?.Num  ?? 0,
                    }.SaveAsync();
                }
            }, null, 100, 60_000);
        }
    }
}