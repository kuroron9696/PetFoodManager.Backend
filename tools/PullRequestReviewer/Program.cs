using System.Text.Json;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using static Microsoft.SemanticKernel.AI.ChatCompletion.ChatHistory;

namespace PullRequestReviewer
{
    /// <summary>
    /// 変更が加わったコードをGPTに投げてレビューしてもらうツール
    /// </summary>
    public class Program
    {
        private static readonly ILogger<Program> _logger;
        private static readonly IKernel _kernel;
        private static readonly IChatCompletion _chatCompletionService;
        private static readonly int _maxTokens;
        private static readonly string _serviceId = "ChatCompletionService";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        static Program()
        {
            bool.TryParse(Environment.GetEnvironmentVariable("USE_AZURE_OPENAI"), out var useAzureOpenAI);
            var apiKey = Environment.GetEnvironmentVariable("API_KEY");
            var modelName = Environment.GetEnvironmentVariable("MODEL_NAME");
            var baseUrl = Environment.GetEnvironmentVariable("BASE_URL");
            var maxTokensExists = int.TryParse(Environment.GetEnvironmentVariable("MAX_TOKENS"), out _maxTokens);

            if (useAzureOpenAI && string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("Azure OpenAIを使用する場合はBASE_URLが必要です。");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(modelName) || !maxTokensExists)
                throw new InvalidOperationException("環境変数が不足しています。");

            _kernel = new KernelBuilder().Configure(c =>
            {
                if (useAzureOpenAI)
                {
                    c.AddAzureChatCompletionService(_serviceId, modelName, baseUrl, apiKey);
                }
                else
                {
                    c.AddOpenAIChatCompletionService(_serviceId, modelName, apiKey);
                }
            }).Build();
            _chatCompletionService = _kernel.GetService<IChatCompletion>();
            _logger = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            }).CreateLogger<Program>();
        }

        /// <summary>
        /// コードの差分からレビューを作成しファイルに保存する
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("引数が無効です。2つの引数が必要です: BaseBranchName, SourceBranchName");
            }

            _logger.LogInformation($"Base branch name: {args[0]}");
            _logger.LogInformation($"Source branch name: {args[1]}");

            await CreateReviewCommentsAsync(args[0], args[1]);
        }

        /// <summary>
        /// レビューを作成する
        /// </summary>
        /// <param name="baseBranchName"></param>
        /// <param name="sourceBranchName"></param>
        /// <returns></returns>
        private static async Task CreateReviewCommentsAsync(string baseBranchName, string sourceBranchName)
        {
            // レビュー用プロンプトを取得
            var systemPrompt = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "prompt.txt"));

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

                var reviewComments = new List<string>();
                foreach (var changedFilePath in changedFilePaths)
                {
                    // 対象ファイルの内容取得
                    var content = File.ReadAllText(changedFilePath);

                    // 差分取得
                    var patch = repo.Diff.Compare<Patch>(baseCommit.Tree, latestCommit.Tree, new List<string> { changedFilePath });
                    var patchContent = patch.Content;

                    // GPTへリクエスト
                    var message = $"# Name\n{changedFilePath}\n" + $"# Content\n{content}\n" + $"# Diff\n{patchContent}";
                    var chat = _chatCompletionService.CreateNewChat();
                    chat.AddMessage(AuthorRoles.System, systemPrompt);
                    chat.AddMessage(AuthorRoles.User, message);

                    _logger.LogInformation($"# {changedFilePath}");
                    _logger.LogInformation("Now Reviewing...");
                    var reviewComment = await _chatCompletionService.GenerateMessageAsync(chat, new ChatRequestSettings { MaxTokens = _maxTokens });
                    _logger.LogInformation("Done!");
                    reviewComments.Add(reviewComment);
                }

                File.WriteAllText("result.json", JsonSerializer.Serialize(new Result { Comment = string.Join("\n\n", reviewComments) }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
            }
        }
    }
}