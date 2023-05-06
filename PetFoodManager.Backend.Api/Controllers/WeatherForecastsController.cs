using Microsoft.AspNetCore.Mvc;
using PetFoodManager.Backend.Api.Domains.Dtos;
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
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastsController> _logger;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger"></param>
    public WeatherForecastsController(ILogger<WeatherForecastsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 取得する
    /// </summary>
    /// <returns></returns>
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
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

