// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using Nuke.CodeGeneration;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Tools.Xunit;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Nuke.Docker.Generator;
using Nuke.GitHub;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.OpenCover.OpenCoverTasks;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.Tools.Xunit.XunitTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.GitHub.GitHubTasks;

class Build : NukeBuild
{
    const string c_addonRepoOwner = "nuke-build";
    const string c_addonRepoName = "docker";
    const string c_addonName = "Docker";
    const string c_toolNamespace = "Nuke.Docker";

    const string c_regenerationRepoOwner = "docker";
    const string c_regenerationRepoName = "docker.github.io";
    const string c_regenerationRepoBranch = "master";

    public static int Main() => Execute<Build>(x => x.Pack);

    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;
    [Solution] readonly Solution Solution;
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";
    [Parameter("Api key to push packages to NuGet.org.")] readonly string NuGetApiKey;
    [Parameter("Api key to access the GitHub.")] readonly string GitHubApiKey;
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Project DockerProject => Solution.GetProject(c_toolNamespace).NotNull();

    AbsolutePath DefinitionRepositoryPath => TemporaryDirectory / "definition-repository";
    string SpecificationDirectory => DockerProject.Directory / "specifications";
    string GenerationBaseDirectory => DockerProject.Directory / "Generated";
    string ChangelogFile => RootDirectory / "CHANGELOG.md";

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(OutputDirectory);
        });

    Target CleanGeneratedFiles => _ => _
        .Executes(() =>
        {
            DeleteDirectory(GenerationBaseDirectory);
            DeleteDirectory(SpecificationDirectory);
            DeleteDirectory(DefinitionRepositoryPath);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s =>  s.SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.GetNormalizedAssemblyVersion())
                .SetFileVersion(GitVersion.GetNormalizedFileVersion())
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var xUnitSettings = new Xunit2Settings()
                .AddTargetAssemblies(GlobFiles(Solution.Directory / "tests", $"*/bin/{Configuration}/net4*/Nuke.*.Tests.dll").NotEmpty())
                .AddResultReport(Xunit2ResultFormat.Xml, OutputDirectory / "tests.xml")
                .SetFramework("net461");
         
                Xunit2(s => xUnitSettings);
        });

    Target Clone => _ => _
        .DependsOn(CleanGeneratedFiles)
        .Executes(() =>
        {
            Git(
                $"clone https://github.com/{c_regenerationRepoOwner}/{c_regenerationRepoName}.git -b {c_regenerationRepoBranch} --single-branch --depth 1 {DefinitionRepositoryPath}");
        });

    Target GenerateSpecifications => _ => _
        .DependsOn(Clone)
        .Executes(() =>
        {
            var reference = Git($"rev-parse --short {c_regenerationRepoBranch}", DefinitionRepositoryPath).Single().Text;
            var commandsToSkip = new[]
                                 {
                                     "docker_container_cp",
                                     "docker_cp"
                                 };

            var generatorSettings = new SpecificationGeneratorSettings
                                    {
                                        CommandsToSkip = commandsToSkip,
                                        OutputFolder = SpecificationDirectory,
                                        Reference = reference,
                                        DefinitonFolder = DefinitionRepositoryPath / "_data" / "engine-cli"
                                    };

            SpecificationGenerator.GenerateSpecifications(generatorSettings);
        });

    Target Generate => _ => _
        .After(GenerateSpecifications)
        .Executes(() =>
        {
            CodeGenerator.GenerateCode(SpecificationDirectory, GenerationBaseDirectory, baseNamespace: c_toolNamespace,
                gitRepository: GitRepository.SetBranch("master"));
        });

    Target CompilePlugin => _ => _
        .DependsOn(Generate, Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(DockerProject));
            DotNetBuild(s => s.SetProjectFile(DockerProject)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.GetNormalizedAssemblyVersion())
                .SetFileVersion(GitVersion.GetNormalizedFileVersion())
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(CompilePlugin)
        .Executes(() =>
        {
            var releaseNotes = ExtractChangelogSectionNotes(ChangelogFile)
                .Select(x => x.Replace("- ", "\u2022 ").Replace("`", string.Empty).Replace(",", "%2C"))
                .Concat(string.Empty)
                .Concat($"Full changelog at {GitRepository.GetGitHubBrowseUrl(ChangelogFile)}")
                .JoinNewLine();

            DotNetPack(s => s
                .SetProject(DockerProject)
                .SetOutputDirectory(OutputDirectory)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetVersion(GitVersion.NuGetVersionV2)
                .SetPackageReleaseNotes(releaseNotes));
        });

    Target Push => _ => _
        .DependsOn(Pack)
        .Requires(() => NuGetApiKey)
        .Requires(() => GitHasCleanWorkingCopy())
        .Requires(() => Configuration.EqualsOrdinalIgnoreCase("release"))
        .Requires(() => IsReleaseBranch || IsMasterBranch)
        .Executes(() =>
        {
            GlobFiles(OutputDirectory, "*.nupkg")
                .Where(x => !x.EndsWith(".symbols.nupkg")).NotEmpty()
                .ForEach(x => DotNetNuGetPush(s => s
                    .SetTargetPath(x)
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetSymbolSource("https://nuget.smbsrc.net/")
                    .SetApiKey(NuGetApiKey)));
        });

    Target PrepareRelease => _ => _
        .Before(CompilePlugin)
        .DependsOn(Changelog, Clean)
        .Executes(() =>
        {
            var releaseBranch = IsReleaseBranch ? GitRepository.Branch : $"release/v{GitVersion.MajorMinorPatch}";
            var isMasterBranch = IsMasterBranch;
            var pushMaster = false;
            if (!isMasterBranch && !IsReleaseBranch) Git($"checkout -b {releaseBranch}");

            if (!GitHasCleanWorkingCopy())
            {
                Git($"add {ChangelogFile}");
                Git($"commit -m \"Finalize v{GitVersion.MajorMinorPatch}\"");
                pushMaster = true;
            }

            if (!isMasterBranch)
            {
                Git("checkout master");
                Git($"merge --no-ff --no-edit {releaseBranch}");
                Git($"branch -D {releaseBranch}");
                pushMaster = true;
            }

            if (IsReleaseBranch) Git($"push origin --delete {releaseBranch}");
            if (pushMaster) Git("push origin master");
        });

    Target Release => _ => _
        .Requires(() => GitHubApiKey)
        .DependsOn(Push)
        .After(PrepareRelease)
        .Executes(async () =>
        {
            var releaseNotes = new[]
                               {
                                   $"- [NuGet](https://www.nuget.org/packages/{c_toolNamespace}/{GitVersion.SemVer})",
                                   $"- [Changelog](https://github.com/{c_addonRepoOwner}/{c_addonRepoName}/blob/{GitVersion.MajorMinorPatch}/CHANGELOG.md)"
                               };

            await PublishRelease(x => x
                .SetToken(GitHubApiKey)
                .SetArtifactPaths(GlobFiles(OutputDirectory, "*.nupkg").ToArray())
                .SetRepositoryName(c_addonRepoName)
                .SetRepositoryOwner(c_addonRepoOwner)
                .SetCommitSha("master")
                .SetName($"NUKE {c_addonName} v{GitVersion.MajorMinorPatch}")
                .SetTag($"{GitVersion.MajorMinorPatch}")
                .SetReleaseNotes(releaseNotes.Join("\n"))
            );
        });

    Target Changelog => _ => _
        .OnlyWhen(ShouldUpdateChangelog)
        .Executes(() =>
        {
            FinalizeChangelog(ChangelogFile, GitVersion.MajorMinorPatch, GitRepository);
        });

    bool ShouldUpdateChangelog()
    {
        bool TryGetChangelogSectionNotes(string tag, out string[] sectionNotes)
        {
            sectionNotes = new string[0];
            try
            {
                sectionNotes = ExtractChangelogSectionNotes(ChangelogFile, tag).ToArray();
                return sectionNotes.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        var nextSectionAvailable = TryGetChangelogSectionNotes("vNext", out _);
        var semVerSectionAvailable = TryGetChangelogSectionNotes(GitVersion.MajorMinorPatch, out _);
        if (semVerSectionAvailable)
        {
            ControlFlow.Assert(!nextSectionAvailable, $"{GitVersion.MajorMinorPatch} is already in changelog.");
            return false;
        }

        return nextSectionAvailable;
    }

    bool IsReleaseBranch => GitRepository.Branch.NotNull().StartsWith("release/");

    bool IsMasterBranch => GitRepository.Branch == "master";
}
