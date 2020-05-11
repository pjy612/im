using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IWebHost webHost = CreateWebHostBuilder(args).Build();

            await webHost.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();
            return WebHost.CreateDefaultBuilder(args)
                //.UseConfiguration(config)
                //.UseKestrel(options =>
                //{
                //    options.ListenAnyIP(5001);
                //    options.ListenAnyIP(5000, listenOptions => { listenOptions.UseHttps("bilipush.pfx", "bilipush"); });
                //})
                .UseStartup<Startup>();
        }
    }
}