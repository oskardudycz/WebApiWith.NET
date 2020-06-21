using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logging.Default
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
                
                // Configure logging 
                .ConfigureLogging(logging =>
                {
                    // clear configuration to make sure that only desired providers are logged
                    logging.ClearProviders();
                    // add console logger
                    logging.AddConsole(options => options.IncludeScopes = true);
                    // add debug logger
                    logging.AddDebug();
                });
    }
}