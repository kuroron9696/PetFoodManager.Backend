using LibGit2Sharp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using static Microsoft.SemanticKernel.AI.ChatCompletion.ChatHistory;

public static class Program
{
    /// <summary>
    /// コードの差分からレビューを作成しファイルに保存する
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("引数が無効です。2つの引数が必要です: baseBranchName, headBranchName");
        }

        Console.WriteLine($"Base branch name: {args[0]}");
        Console.WriteLine($"HEAD branch name: {args[1]}");

        var review = await CreateReviewAsync(args[0], args[1]);
        Console.WriteLine(review);
        File.WriteAllText("comments.txt", $"{review}");
    }

    /// <summary>
    /// レビューを作成する
    /// </summary>
    /// <param name="baseBranchName"></param>
    /// <param name="headBranchName"></param>
    /// <returns></returns>
    private static async Task<string> CreateReviewAsync(string baseBranchName, string headBranchName)
    {
        var kernel = new KernelBuilder().Configure(c =>
        {
            c.AddOpenAIChatCompletionService("chat", "gpt-3.5-turbo", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }).Build();

        var chatCompletionService = kernel.GetService<IChatCompletion>();
        var systemPrompt = File.ReadAllText("tools/PullRequestReviewer/prompt.txt");

        // リポジトリのパスを取得
        var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));

        // Gitリポジトリを操作して、指定されたブランチ間の差分を取得
        using (var repo = new Repository(repoPath))
        {
            var baseCommit = repo.Branches[baseBranchName].Tip;
            var latestCommit = repo.Branches[headBranchName].Tip;

            var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

            // 変更されたファイルのパスを取り出す
            var changedFilePaths = diff.Where(e => e.Status == ChangeKind.Added || e.Status == ChangeKind.Modified).Select(e => e.Path).ToList();

            var responses = new List<string>();
            foreach (var changedFilePath in changedFilePaths)
            {
                // 対象ファイルの内容取得
                var content = File.ReadAllText(changedFilePath);

                // 差分取得
                var patch = repo.Diff.Compare<Patch>(baseCommit.Tree, latestCommit.Tree, new List<string> { changedFilePath });
                var patchContent = patch.Content;

                // GPTへリクエスト
                var message = $"# Name\n{changedFilePath}\n" + $"# Content\n{content}\n" + $"# Diff\n{patchContent}";
                var chat = chatCompletionService.CreateNewChat();
                chat.AddMessage(AuthorRoles.System, systemPrompt);
                chat.AddMessage(AuthorRoles.User, message);

                Console.WriteLine($"# {changedFilePath}");
                Console.WriteLine("Now Reviewing...");
                var response = await chatCompletionService.GenerateMessageAsync(chat, new ChatRequestSettings { MaxTokens = 1000 });
                Console.WriteLine("Done!");
                Console.WriteLine(response);
                responses.Add(response);
            }

            return string.Join("\n\n", responses);
        }
    }
}