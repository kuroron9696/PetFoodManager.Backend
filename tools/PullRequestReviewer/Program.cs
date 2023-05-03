using System.Text.RegularExpressions;
using LibGit2Sharp;

public class Program
{
    public static void Main()
    {
        var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));
        using (var repo = new Repository(repoPath))
        {
            var baseCommit = repo.Branches["origin/develop"].Tip;
            var latestCommit = repo.Head.Tip;

            var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

            var addedFiles = diff.Where(e => e.Status == ChangeKind.Added || e.Status == ChangeKind.Modified).Select(e => e.Path).ToList();

            var addedCSharpFiles = addedFiles.Where(e => e.EndsWith(".cs")).ToList();

            var addedClasses = new List<string>();

            var addedInterfaces = new List<string>();

            var classPattern = new Regex(@"\bclass\s+(\w+)");
            var interfacePattern = new Regex(@"\binterface\s+(\w+)");

            foreach (var file in addedCSharpFiles)
            {
                var content = File.ReadAllText(file);
                Console.WriteLine(content);
                addedClasses.AddRange(classPattern.Matches(content).Select(e => e.Groups[1].Value));
                addedInterfaces.AddRange(interfacePattern.Matches(content).Select(e => e.Groups[1].Value));
            }

            Console.WriteLine("Added classes:");

            foreach (var addedClass in addedClasses)
            {
                Console.WriteLine($"- {addedClass}");
            }

            Console.WriteLine("Added interfaces:");

            foreach (var addedInterface in addedInterfaces)
            {
                Console.WriteLine($"- {addedInterface}");
            }
        }
    }
}