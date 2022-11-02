using Microsoft.AspNetCore;

namespace PetFoodManager.Backend.Api
{
    /// <summary>
    /// メインプログラム
    /// </summary>
    public class Program
    {
        /// <summary>
        /// メイン
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// ホストビルダー生成
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
          WebHost.CreateDefaultBuilder(args).ConfigureAppConfiguration((hostingContext, config) =>
          {
              config.SetBasePath(Directory.GetCurrentDirectory());
              config.AddJsonFile(Path.GetFullPath(Path.Combine(@"../appsettings.json")));
              config.AddEnvironmentVariables();

              if (hostingContext.HostingEnvironment.IsDevelopment())
              {
                  config.AddUserSecrets<Program>();
              }
          }).UseStartup<Startup>();
    }
}