using Microsoft.AspNetCore.Mvc;
using PetFoodManager.Backend.Api.Domains.Dtos;
using PetFoodManager.Backend.Api.Usecases;
using PetFoodManager.Backend.Common.Cores.Attributes;

namespace PetFoodManager.Backend.Api.Controllers;

/// <summary>
/// サンプルプログラム
/// </summary>
[ApiController]
[Route("[controller]")]
[UnitTestSubject]
public class WeatherForecastsController : ControllerBase
{
    private readonly IReadWeatherForecastsUsecase _readWeatherForecastsUsecase;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="readWeatherForecastsUsecase"></param>
    public WeatherForecastsController(IReadWeatherForecastsUsecase readWeatherForecastsUsecase)
    {
        _readWeatherForecastsUsecase = readWeatherForecastsUsecase;
    }

    /// <summary>
    /// 取得する
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WeatherForecast>>> GetAsync()
        => Ok(await _readWeatherForecastsUsecase.ExecuteAsync());
}
