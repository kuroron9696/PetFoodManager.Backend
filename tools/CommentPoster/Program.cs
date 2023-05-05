using System.Text.Json;
using Octokit;
using PetFoodManager.Backend.Tools.PullRequestReviewer;

namespace PetFoodManager.Backend.Tools.CommentPoster
{
    /// <summary>
    /// GitHubのプルリクエストにコメントを投稿するツール
    /// </summary>
    public class Program
    {
        private static readonly GitHubClient s_githubClient;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        static Program()
        {
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("環境変数が不足しています。");

            s_githubClient = new GitHubClient(new ProductHeaderValue(nameof(PetFoodManager.Backend)))
            {
                Credentials = new Credentials(token)
            };
        }

        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("引数が無効です。3つの引数が必要です: filePath, repositoryId, pullRequestId");
            }

            var filePath = args[0];
            var repositoryId = long.Parse(args[1]);
            var pullRequestId = int.Parse(args[2]);

            await PostCommentAsync(filePath, repositoryId, pullRequestId);
        }

        /// <summary>
        /// コメントを投稿する
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="repositoryId"></param>
        /// <param name="pullRequestId"></param>
        /// <returns></returns>
        private static async Task PostCommentAsync(string filePath, long repositoryId, int pullRequestId)
        {
            var json = File.ReadAllText(filePath);
            var result = JsonSerializer.Deserialize<Result>(json);
            await s_githubClient.Issue.Comment.Create(repositoryId, pullRequestId, result.Comment);
        }
    }
}
