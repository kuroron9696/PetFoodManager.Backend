using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octokit;

/// <summary>
/// GitHubのプルリクエストにコメントを投稿するツール
/// </summary>
public static class Program
{
    /// <summary>
    /// メイン処理
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException("引数が無効です。4つの引数が必要です: filePath, productName, repositoryId, prId");
        }

        var filePath = args[0];
        var productName = args[1];
        var repositoryId = long.Parse(args[2]);
        var prId = int.Parse(args[3]);

        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var githubClient = new GitHubClient(new ProductHeaderValue(productName))
        {
            Credentials = new Credentials(githubToken)
        };

        var json = File.ReadAllText(filePath);
        var data = JObject.Parse(json);

        await githubClient.Issue.Comment.Create(repositoryId, prId, data["Comments"].ToString());
    }
}
