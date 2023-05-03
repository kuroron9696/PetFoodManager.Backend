using System.Text.RegularExpressions;
using LibGit2Sharp;

class Program
{
    static void Main()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
        var repoPath = Repository.Discover(projectRoot);
        using (var repo = new Repository(repoPath))
        {
            var baseCommit = repo.Branches["origin/develop"].Tip;
            var latestCommit = repo.Head.Tip;

            var diff = repo.Diff.Compare<TreeChanges>(baseCommit.Tree, latestCommit.Tree);

            var addedFiles = diff.Where(e => e.Status == ChangeKind.Added).Select(e => e.Path);

            var addedCSharpFiles = addedFiles.Where(e => e.EndsWith(".cs"));

            var addedClasses = new List<string>();

            var addedInterfaces = new List<string>();

            var classPattern = new Regex(@"¥bclass¥s+(¥w+)");
            var interfacePattern = new Regex(@"¥binterface¥s+(¥w+)");

            foreach (var file in addedCSharpFiles)
            {
                var content = File.ReadAllText(file);
                addedClasses.AddRange(classPattern.Matches(content).Select(e => e.Groups[1].Value));
                addedInterfaces.AddRange(classPattern.Matches(content).Select(e => e.Groups[1].Value));
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