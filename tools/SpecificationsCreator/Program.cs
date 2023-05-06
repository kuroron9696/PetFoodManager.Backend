using System.Collections.Concurrent;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using static Microsoft.SemanticKernel.AI.ChatCompletion.ChatHistory;

namespace PetFoodManager.Backend.Tools.SpecificationsCreator
{
    /// <summary>
    /// 指定したフォーマットでドキュメントを生成するツール
    /// </summary>
    public class Program
    {
        private static readonly ILogger<Program> s_logger;
        private static readonly Random s_random;
        private static readonly IKernel s_kernel;
        private static readonly IChatCompletion s_chatCompletionService;
        private static readonly MSBuildWorkspace s_workSpace;
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
            MSBuildLocator.RegisterDefaults();
            s_workSpace = MSBuildWorkspace.Create();
        }

        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            var outDir = args[0];
            await CreateSpecificationsAsync(outDir);
        }

        /// <summary>
        /// ドキュメントを生成する
        /// </summary>
        /// <param name="outDir"></param>
        /// <returns></returns>
        private static async Task CreateSpecificationsAsync(string outDir)
        {
            var systemPrompt = File.ReadAllText("tools/SpecificationsCreator/prompt.txt");
            var solution = await s_workSpace.OpenSolutionAsync(Path.Combine(Path.GetFullPath(Directory.GetCurrentDirectory()), "PetFoodManager.Backend.sln")).ConfigureAwait(false);
            var controllerDocuments = new List<Document>();
            var tasks = new ConcurrentBag<Task>();

            foreach (var project in solution.Projects)
            {
                controllerDocuments.AddRange(project.Documents.Where(e => e.Name.ToLower().Contains("controller")).ToList());
            }

            Parallel.ForEach(controllerDocuments, document =>
            {
                tasks.Add(Task.Run(async () =>
                {
                    var content = File.ReadAllText(document.FilePath);
                    var name = $"{document.Project.Name}/{document.Name}";

                    // APIのレートリミット対策
                    var waitingSeconds = s_random.Next(0, 180);
                    s_logger.LogInformation($"Wait for {waitingSeconds} seconds to avoid rate limits. / Subject: {name}");
                    await Task.Delay(waitingSeconds * 1000);

                    s_logger.LogInformation($"Now Creating... / Subject: {name}");

                    // GPTへリクエスト
                    var message = $"# Name\n{name}\n" + $"# Content\n{content}\n";
                    var chat = s_chatCompletionService.CreateNewChat();
                    chat.AddMessage(AuthorRoles.System, systemPrompt);
                    chat.AddMessage(AuthorRoles.User, message);

                    var specification = $"## {name}\n";
                    try
                    {
                        specification = await s_chatCompletionService.GenerateMessageAsync(chat, new ChatRequestSettings { MaxTokens = s_maxTokens });
                    }
                    catch (Exception ex)
                    {
                        s_logger.LogError($"An error occured. (Message: {ex.Message}) / Subject: {name}");
                        specification += "エラーが発生したため、ドキュメントを作成できませんでした。";
                    }

                    if (!Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);
                    File.WriteAllText($"{outDir}/{Path.GetFileNameWithoutExtension(document.Name)}Specification.txt", specification);
                    s_logger.LogInformation($"Done! / Subject: {name}");
                }));
            });

            await Task.WhenAll(tasks);
        }
    }
}