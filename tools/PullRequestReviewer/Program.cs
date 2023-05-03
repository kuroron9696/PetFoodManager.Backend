﻿using System.Text.RegularExpressions;
using LibGit2Sharp;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException("引数が無効です");

        Console.WriteLine($"HEAD branch name: {args[0]}");
        Console.WriteLine($"Base branch name: {args[1]}");
        var repoPath = Repository.Discover(Path.GetFullPath(Directory.GetCurrentDirectory()));
        using (var repo = new Repository(repoPath))
        {
            foreach (var branch in repo.Branches)
            {
                Console.WriteLine(branch.ToString());
            }

            if (repo.Branches[args[0]] == null)
            {
                Console.WriteLine($"Branch {args[0]} not found.");
                return;
            }

            if (repo.Branches[args[1]] == null)
            {
                Console.WriteLine($"Branch {args[0]} not found.");
                return;
            }

            var latestCommit = repo.Branches[args[0]].Tip;
            var baseCommit = repo.Branches[args[1]].Tip;

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