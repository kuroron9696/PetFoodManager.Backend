using System.Collections.Concurrent;
using System.Text.Json;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using static Microsoft.SemanticKernel.AI.ChatCompletion.ChatHistory;

namespace PetFoodManager.Backend.Tools.PullRequestReviewer
{
    /// <summary>
    /// 変更が加わったコードをGPTに投げてレビューしてもらうツール
    /// </summary>
    public class Program
    {
        private static readonly ILogger<Program> s_logger;
        private static readonly Random s_random;
        private static readonly IKernel s_kernel;
        private static readonly IChatCompletion s_chatCompletionService;
        private static readonly int s_maxTokens;
        private static readonly string s_serviceId = "ChatCompletionService";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        static Program()
        {
            var apiKey = Environment.GetEnvironmentVariable("API_KEY");
            var modelName = Environment.GetEnvironmentVariable("MODEL_NAME");
            var maxTokensExists = int.TryParse(Environment.GetEnvironmentVariable("MAX_TOKENS"), out s_maxTokens);
            var useAzureExists = bool.TryParse(Environment.GetEnvironmentVariable("USE_AZURE"), out var useAzure);
            var baseUrl = Environment.GetEnvironmentVariable("BASE_URL");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(modelName) || !maxTokensExists || !useAzureExists)
                throw new InvalidOperationException("環境変数が不足しています。");

            if (useAzure && string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("Azure OpenAI Serviceを使用する場合はBASE_URLが必要です。");

            s_logger = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            }).CreateLogger<Program>();
            s_kernel = new KernelBuilder().Configure(c =>
            {
                if (useAzure)
                {
                    c.AddAzureChatCompletionService(s_serviceId, modelName, baseUrl, apiKey);
                }
                else
                {
                    c.AddOpenAIChatCompletionService(s_serviceId, modelName, apiKey);
                }
            }).Build();
            s_chatCompletionService = s_kernel.GetService<IChatCompletion>();
            var seed = Environment.TickCount;
            s_random = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed))).Value;
        }

        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("引数が無効です。2つの引数が必要です: BaseBranchName, SourceBranchName");
            }

            s_logger.LogInformation($"Base branch name: {args[0]} / Source branch name: {args[1]}");

            await CreateReviewCommentAsync(args[0], args[1]);
        }

        /// <summary>
        /// レビューを作成する
        /// </summary>
        /// <param name="baseBranchName"></param>
        /// <param name="sourceBranchName"></param>
        /// <returns></returns>
        private static async Task CreateReviewCommentAsync(string baseBranchName, string sourceBranchName)
        {
            // レビュー用プロンプトを取得
            var systemPrompt = File.ReadAllText("tools/PullRequestReviewer/prompt.txt");

            // リポジトリのパスを取得
            var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));

            // Gitリポジトリを操作して、指定されたブランチ間の差分を取得
            using (var repo = new Repository(repoPath))
            {
                var baseCommit = repo.Branches[baseBranchName].Tip;
                var latestCommit = repo.Branches[sourceBranchName].Tip;

                var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

                // 変更されたファイルのパスを取り出す
                var changedFilePaths = diff.Where(e => e.Status == ChangeKind.Added || e.Status == ChangeKind.Modified).Select(e => e.Path).ToList();

                var reviewComments = new ConcurrentBag<string>();
                var tasks = new ConcurrentBag<Task>();

                Parallel.ForEach(changedFilePaths, changedFilePath =>
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        // APIのレートリミット対策
                        var waitingSeconds = s_random.Next(0, 180);
                        s_logger.LogInformation($"Wait for {waitingSeconds} seconds to avoid rate limits. / Subject: {changedFilePath}");
                        await Task.Delay(waitingSeconds * 1000);

                        s_logger.LogInformation($"Now Reviewing... / Subject: {changedFilePath}");

                        // 対象ファイルの内容取得
                        var content = File.ReadAllText(changedFilePath);

                        // 差分取得
                        var patch = repo.Diff.Compare<Patch>(baseCommit.Tree, latestCommit.Tree, new List<string> { changedFilePath });
                        var patchContent = patch.Content;

                        // GPTへリクエスト
                        var message = $"# Name\n{changedFilePath}\n" + $"# Content\n{content}\n" + $"# Diff\n{patchContent}";

                        var chat = s_chatCompletionService.CreateNewChat();
                        chat.AddMessage(AuthorRoles.System, systemPrompt);
                        chat.AddMessage(AuthorRoles.User, message);

                        var reviewComment = $"## {changedFilePath}\n";
                        try
                        {
                            reviewComment = await s_chatCompletionService.GenerateMessageAsync(chat, new ChatRequestSettings { MaxTokens = s_maxTokens });
                        }
                        catch (Exception ex)
                        {
                            s_logger.LogError($"An error occured. (Message: {ex.Message}) / Subject: {changedFilePath}");
                            reviewComment += "エラーが発生したため、レビューできませんでした。";
                        }

                        reviewComments.Add(reviewComment);

                        s_logger.LogInformation($"Done! / Subject: {changedFilePath}");
                    }));
                });

                await Task.WhenAll(tasks);
                File.WriteAllText("result.json", JsonSerializer.Serialize(new Result { Comment = string.Join("\n\n", reviewComments) }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
            }
        }
    }
}