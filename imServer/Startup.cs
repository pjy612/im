using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using aspCore.Extensions;
using imServer.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace imServer
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json")
                .AddEnvironmentVariables();
            Config = builder.Build();
        }

        public IConfiguration Config;

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ImServerOption>(Config.GetSection(CONFIG.OPTIONS));
            services.Add(ServiceDescriptor.Transient<ICorsService, WildcardCorsService>());
            services.AddCors(options =>
            {
                options.AddPolicy("free", cors =>
                    cors.AllowAnyHeader().AllowAnyMethod()
                        .AllowAnyOrigin()
                        .AllowCredentials()
                );
                options.AddPolicy("bilibili", cors =>
                    cors
                        .WithOrigins("*.bilibili.com","*.localhost")
                        .AllowAnyHeader().AllowAnyMethod()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowCredentials()
                );
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
//            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
//            Console.OutputEncoding = Encoding.GetEncoding("GB2312");
//            Console.InputEncoding  = Encoding.GetEncoding("GB2312");
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            var config = app.ApplicationServices.GetRequiredService<IOptions<ImServerOption>>().Value;
            //app.UseCors("free");
            app.UseCors("bilibili");
            app.UseImServer(new ImServerOptions
            {
                Redis   = new CSRedis.CSRedisClient(config.CSRedisClient),
                Servers = config.Servers.Split(";"),
                Server  = config.Server
            });
        }
    }
}