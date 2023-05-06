using PetFoodManager.Backend.Api.Domains.Dtos;

namespace PetFoodManager.Backend.Api.Usecases
{
    /// <summary>
    /// 天気を取得するユースケース
    /// </summary>
    public interface IReadWeatherForecastsUsecase
    {
        /// <summary>
        /// 実行する
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<WeatherForecast>> ExecuteAsync();
    }
}