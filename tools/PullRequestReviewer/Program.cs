using System.Text.RegularExpressions;
using LibGit2Sharp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using static Microsoft.SemanticKernel.AI.ChatCompletion.ChatHistory;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException("引数が無効です");

        Console.WriteLine($"HEAD branch name: {args[0]}");
        Console.WriteLine($"Base branch name: {args[1]}");

        var review = await CreateReviewAsync(args[0], args[1]);
        Console.WriteLine($"::set-output name=reviewComment::{review}");
        File.WriteAllText("review.env", $"{review}");
    }

    private static async Task<string> CreateReviewAsync(string headBranchName, string baseBranchName)
    {
        var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));

        var kernel = new KernelBuilder().Configure(c =>
        {
            c.AddOpenAIChatCompletionService("chat", "gpt-3.5-turbo", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }).Build();

        var chatCompletionService = kernel.GetService<IChatCompletion>();

        var maxTokens = 1024;
        var systemPrompt = $"Provide some feedback and suggestions for improvement to source codes from user. Answer in Japanese. Answers must be contained in {maxToken - 100} tokens.";

        using (var repo = new Repository(repoPath))
        {
            var latestCommit = repo.Branches[headBranchName].Tip;
            var baseCommit = repo.Branches[baseBranchName].Tip;

            var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

            var addedFiles = diff.Where(e => e.Status == ChangeKind.Added || e.Status == ChangeKind.Modified).Select(e => e.Path).ToList();

            var addedCSharpFiles = addedFiles.Where(e => e.EndsWith(".cs")).ToList();

            var classPattern = @"\bclass\s+(\w+)";
            var interfacePattern = @"\binterface\s+(\w+)";

            var responses = new List<string>();
            foreach (var file in addedCSharpFiles)
            {
                var content = File.ReadAllText(file);
                if (Regex.IsMatch(content, classPattern) || Regex.IsMatch(content, interfacePattern))
                {
                    var chat = chatCompletionService.CreateNewChat();
                    chat.AddMessage(AuthorRoles.System, systemPrompt);
                    chat.AddMessage(AuthorRoles.User, content);
                    var response = await chatCompletionService.GenerateMessageAsync(chat, new ChatRequestSettings { MaxTokens = maxTokens });
                    responses.Add(response);
                }
            }

            return string.Join("\n", responses);
        }
    }
}