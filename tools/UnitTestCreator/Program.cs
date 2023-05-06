using System.Collections.Concurrent;
using System.Text.Json;
using LibGit2Sharp;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using PetFoodManager.Backend.Common.Cores.Attributes;
using PetFoodManager.Backend.Common.Dtos;
using static Microsoft.SemanticKernel.AI.ChatCompletion.ChatHistory;

namespace PetFoodManager.Backend.Tools.UnitTestCreator
{
    /// <summary>
    /// プルリク作成時にユニットテストをGPTに作成してもらうツール
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
            if (args.Length != 2)
            {
                throw new ArgumentException("引数が無効です。2つの引数が必要です: BaseBranchName, SourceBranchName");
            }

            s_logger.LogInformation($"Base branch name: {args[0]} / Source branch name: {args[1]}");

            await CreateUnitTestAsync(args[0], args[1]);
        }

        /// <summary>
        /// ユニットテストを作成する
        /// </summary>
        /// <param name="baseBranchName"></param>
        /// <param name="sourceBranchName"></param>
        /// <returns></returns>
        private static async Task CreateUnitTestAsync(string baseBranchName, string sourceBranchName)
        {
            // レビュー用プロンプトを取得
            var systemPrompt = File.ReadAllText("tools/UnitTestCreator/prompt.txt");

            // リポジトリのパスを取得
            var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));
            var solution = await s_workSpace.OpenSolutionAsync(Path.Combine(Path.GetFullPath(Directory.GetCurrentDirectory()), "PetFoodManager.Backend.sln")).ConfigureAwait(false);

            // Gitリポジトリを操作して、指定されたブランチ間の差分を取得
            using (var repo = new Repository(repoPath))
            {
                var baseCommit = repo.Branches[baseBranchName].Tip;
                var latestCommit = repo.Branches[sourceBranchName].Tip;

                var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

                // 変更されたファイルのパスを取り出す(.csのみ)
                var changedFilePaths = diff.Where(e => e.Status == ChangeKind.Added || e.Status == ChangeKind.Modified).Select(e => e.Path).Where(e => e.EndsWith(".cs")).ToList();

                var unitTests = new ConcurrentBag<string>();
                var tasks = new ConcurrentBag<Task>();

                Parallel.ForEach(changedFilePaths, changedFilePath =>
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        // 対象ファイルの内容取得
                        var content = File.ReadAllText(changedFilePath);

                        // ユニットテストの対象かどうか判別
                        var syntaxTree = CSharpSyntaxTree.ParseText(content);
                        var rootNode = syntaxTree.GetRoot();
                        var isSubject = rootNode.DescendantNodes().OfType<AttributeSyntax>().Any(e => e.Name.ToString().Contains(nameof(UnitTestSubject)));

                        if (isSubject)
                        {
                            var project = solution.Projects.FirstOrDefault(e => changedFilePath.Contains(e.Name));
                            var document = project.Documents.FirstOrDefault(e => e.FilePath.Contains(changedFilePath));
                            var semanticModel = await document.GetSemanticModelAsync();

                            // 対象ファイルが参照しているクラスとインターフェースを取得
                            var referenceSymbols = semanticModel.SyntaxTree.GetRoot().DescendantNodes()
                                .Select(n => semanticModel.GetSymbolInfo(n).Symbol)
                                .Where(s => s != null && s.Kind == SymbolKind.NamedType)
                                .OfType<INamedTypeSymbol>()
                                .Where(nt => nt.TypeKind == TypeKind.Class || nt.TypeKind == TypeKind.Interface)
                                .Distinct()
                                .ToList();

                            var relatedFileContents = string.Empty;

                            // 参照しているクラスとインターフェースの中身を取得
                            foreach (var referenceSymbol in referenceSymbols)
                            {
                                var referenceSyntaxReference = referenceSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                                if (referenceSyntaxReference != null)
                                {
                                    var path = referenceSyntaxReference.SyntaxTree.FilePath;
                                    if (!path.Contains(changedFilePath))
                                    {
                                        relatedFileContents += File.ReadAllText(referenceSyntaxReference.SyntaxTree.FilePath);
                                        relatedFileContents += "\n---\n";
                                    }
                                }
                            }

                            // APIのレートリミット対策
                            var waitingSeconds = s_random.Next(0, 180);
                            s_logger.LogInformation($"Wait for {waitingSeconds} seconds to avoid rate limits. / Subject: {changedFilePath}");
                            await Task.Delay(waitingSeconds * 1000);

                            s_logger.LogInformation($"Now Creating... / Subject: {changedFilePath}");

                            // GPTへリクエスト
                            var message = $"# Name\n{changedFilePath}\n" + $"# Content\n{content}\n" + $"# Related Files\n{relatedFileContents}\n";
                            var chat = s_chatCompletionService.CreateNewChat();
                            chat.AddMessage(AuthorRoles.System, systemPrompt);
                            chat.AddMessage(AuthorRoles.User, message);

                            var unitTest = $"## {changedFilePath}\n";
                            try
                            {
                                unitTest = await s_chatCompletionService.GenerateMessageAsync(chat, new ChatRequestSettings { MaxTokens = s_maxTokens });
                            }
                            catch (Exception ex)
                            {
                                s_logger.LogError($"An error occured. (Message: {ex.Message}) / Subject: {changedFilePath}");
                                unitTest += "エラーが発生したため、テストを作成できませんでした。";
                            }

                            unitTests.Add(unitTest);

                            s_logger.LogInformation($"Done! / Subject: {changedFilePath}");
                        }
                    }));
                });

                await Task.WhenAll(tasks);
                File.WriteAllText("result.json", JsonSerializer.Serialize(new Result { Comment = string.Join("\n\n", unitTests) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
        }
    }
}