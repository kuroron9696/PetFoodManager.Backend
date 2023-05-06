using PetFoodManager.Backend.Api.Domains.Dtos;
using PetFoodManager.Backend.Api.Usecases;
using PetFoodManager.Backend.Common.Cores.Attributes;

namespace PetFoodManager.Backend.Api.Domains
{
    /// <summary>
    /// 天気を取得するインタラクタ
    /// </summary>
    [UnitTestSubject]
    public class ReadWeatherForecastsInteractor : IReadWeatherForecastsUsecase
    {
        private static readonly string[] Summaries = new[]
        {
          "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        /// <summary>
        /// 実行する
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<WeatherForecast>> ExecuteAsync()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}