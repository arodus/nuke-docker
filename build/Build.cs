// Copyright 2018 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.GitHub;
using Octokit;
using static Nuke.CodeGeneration.CodeGenerator;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.GitHub.GitHubTasks;

// ReSharper disable HeapView.DelegateAllocation

partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Pack);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter] readonly string Source = "https://api.nuget.org/v3/index.json";
    [Parameter] readonly string SymbolSource = "https://nuget.smbsrc.net/";

    [Parameter("ApiKey for the specified source.")] readonly string ApiKey;
    [Parameter] readonly string GitHubToken;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    // [GitVersion] readonly GitVersion GitVersion;

    [Parameter] readonly string SemanticVersion;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("*/bin", "*/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    IEnumerable<string> SpecificationFiles => GlobFiles(SourceDirectory, "*/*.json");

    Target Generate => _ => _
        .OnlyWhenDynamic(() => SpecificationFiles.Any())
        .Executes(() =>
        {
            string GetNamespace(string specificationFile)
                => Solution.Projects.Single(x => IsDescendantPath(x.Directory, specificationFile)).Name;

            GenerateCode(
                SpecificationFiles.ToList(),
                outputFileProvider: x => x.DefaultOutputFile,
                namespaceProvider: x => GetNamespace(x.SpecificationFile),
                sourceFileProvider: x => GitRepository.GetGitHubBrowseUrl(x.SpecificationFile));
        });

    Target Restore => _ => _
        .After(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .SetProperty("ReplacePackageReferences", false));
        });

    Target Compile => _ => _
        .DependsOn(Restore, Generate)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetProperty("ReplacePackageReferences", false));
        });

    IEnumerable<Nuke.Common.ProjectModel.Project> TestProjects => Solution.GetProjects("*.Tests");

    Target Test => _ => _
        .DependsOn(Compile)
        .OnlyWhenDynamic(() => TestProjects.Any())
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .SetLogger("trx")
                .SetResultsDirectory(OutputDirectory)
                .CombineWith(
                    TestProjects, (cs, v) => cs
                        .SetProjectFile(v)));
        });

    string ChangelogFile => RootDirectory / "CHANGELOG.md";

    IEnumerable<string> ChangelogSectionNotes => ExtractChangelogSectionNotes(ChangelogFile);

    Target Changelog => _ => _
        .After(Test)
        .Before(Pack)
        .OnlyWhenDynamic(() =>
            GitRepository.IsOnMasterBranch() ||
            GitRepository.IsOnReleaseBranch() ||
            GitRepository.IsOnHotfixBranch())
        .Executes(() =>
        {
            FinalizeChangelog(ChangelogFile, SemanticVersion, GitRepository);
            Git($"add {ChangelogFile}");
            Git($"commit -m \"Finalize {Path.GetFileName(ChangelogFile)} for v{SemanticVersion}\"");
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .EnableNoBuild()
                .SetConfiguration(Configuration)
                .EnableIncludeSymbols()
                .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
                .SetOutputDirectory(OutputDirectory)
                .SetVersion(SemanticVersion)
                .SetPackageReleaseNotes(GetNuGetReleaseNotes(ChangelogFile, GitRepository)));
        });

    Target Publish => _ => _
        .DependsOn(Changelog)
        .DependsOn(Pack)
        .Requires(() => SemanticVersion)
        .Requires(() => ApiKey)
        .Requires(() => GitHasCleanWorkingCopy())
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Executes(async () =>
        {
            DotNetNuGetPush(s => s
                    .SetSource(Source)
                    .SetSymbolSource(SymbolSource)
                    .SetApiKey(ApiKey)
                    .CombineWith(
                        OutputDirectory.GlobFiles("*.nupkg").NotEmpty(), (cs, v) => cs
                            .SetTargetPath(v)),
                degreeOfParallelism: 5,
                completeOnFailure: true);

            Git($"tag {SemanticVersion}");
            Git($"push origin {GitRepository.Branch} {SemanticVersion}");

            return;

            await PublishRelease(s => s
                .SetToken(GitHubToken)
                .SetRepositoryOwner(GitRepository.GetGitHubOwner())
                .SetRepositoryName(GitRepository.GetGitHubName())
                .SetCommitSha(GitRepository.Branch)
                .SetTag($"{SemanticVersion}")
                .SetName($"v{SemanticVersion}")
                .SetReleaseNotes(GetNuGetReleaseNotes(ChangelogFile, GitRepository)));
        });

    bool GitHubMilestoneClosed(bool mustExist) => GetMilestone().Result?.State.Value == ItemState.Closed;

    async Task<Milestone> GetMilestone()
    {
        var client = new GitHubClient(new ProductHeaderValue(nameof(NukeBuild))) { Credentials = new Credentials(GitHubToken) };
        var milestones = await client.Issue.Milestone.GetAllForRepository(
            GitRepository.GetGitHubOwner(),
            GitRepository.GetGitHubName());
        return milestones.FirstOrDefault(x => x.Title == $"v{SemanticVersion}");
    }
}
