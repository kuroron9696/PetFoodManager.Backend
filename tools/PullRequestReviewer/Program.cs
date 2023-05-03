using System.Text.RegularExpressions;
using LibGit2Sharp;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("引数が無効です");

        var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));
        using (var repo = new Repository(repoPath))
        {
            var baseCommit = repo.Branches[$"origin/{args[0]}"].Tip;
            var latestCommit = repo.Head.Tip;

            var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

            var addedFiles = diff.Where(e => e.Status == ChangeKind.Added || e.Status == ChangeKind.Modified).Select(e => e.Path).ToList();

            var addedCSharpFiles = addedFiles.Where(e => e.EndsWith(".cs")).ToList();

            var classPattern = @"\bclass\s+(\w+)";
            var interfacePattern = @"\binterface\s+(\w+)";

            foreach (var file in addedCSharpFiles)
            {
                var content = File.ReadAllText(file);
                if (Regex.IsMatch(content, classPattern) || Regex.IsMatch(content, interfacePattern))
                {
                    Console.WriteLine(file);
                    Console.WriteLine(content);
                    Console.WriteLine("---");
                }
            }
        }
    }
}