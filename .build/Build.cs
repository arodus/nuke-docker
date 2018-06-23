using System;
using System.IO;
using System.Linq;
using Nuke.CodeGeneration;
using Nuke.Common.Git;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Xunit;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Nuke.Docker.Generator;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.OpenCover.OpenCoverTasks;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.Tools.Xunit.XunitTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.ChangeLog.ChangelogTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Pack);

    readonly string ToolNamespace = "Nuke.Docker";
    readonly string DockerCliDocsRepository = "https://github.com/docker/docker.github.io.git";

    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository GitRepository;
    [Solution] readonly Solution Solution;

    [Parameter("ApiKey for the specified source.")] readonly string ApiKey;
    [Parameter("Indicates to push to nuget.org feed.")] readonly bool NuGet;
    [Parameter] readonly string DockerDocGitBranch = "master";

    Project DockerProject => Solution.GetProject("Nuke.Docker");

    AbsolutePath DefinitonRepositoryPath => TemporaryDirectory / "definition-repository";

    string SpecificationPath => DockerProject.Directory / "specifications";
    string GenerationBaseDirectory => DockerProject.Directory / "Generated";

    string ChangelogFile => RootDirectory / "CHANGELOG.md";

    string Source => NuGet
        ? "https://api.nuget.org/v3/index.json"
        : "https://www.myget.org/F/nukebuild/api/v2/package";

    string SymbolSource => NuGet
        ? "https://nuget.smbsrc.net"
        : "https://www.myget.org/F/nukebuild/symbols/api/v2/package";

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(OutputDirectory);
            DeleteDirectory(SpecificationPath);
            DeleteDirectory(GenerationBaseDirectory);
            DeleteDirectory(DefinitonRepositoryPath);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => DefaultDotNetRestore);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(x => DefaultDotNetBuild.EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var xUnitSettings = new Xunit2Settings()
                .AddTargetAssemblies(GlobFiles(SolutionDirectory / "tests", $"*/bin/{Configuration}/net4*/Nuke.*.Tests.dll").NotEmpty())
                .AddResultReport(Xunit2ResultFormat.Xml, OutputDirectory / "tests.xml");

            if (IsWin)
            {
                OpenCover(s => DefaultOpenCover
                    .SetOutput(OutputDirectory / "coverage.xml")
                    .SetTargetSettings(xUnitSettings));
            }
            else
                Xunit2(s => xUnitSettings);
        });

    Target Clone => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            Git($"clone {DockerCliDocsRepository} -b {DockerDocGitBranch} --single-branch --depth 1 {DefinitonRepositoryPath}");
        });

    Target Generate => _ => _
        .DependsOn(Clone)
        .Executes(() =>
        {
            var reference = Git($"rev-parse --short {DockerDocGitBranch}", DefinitonRepositoryPath, redirectOutput: true).Single();

            var commandsToSkip = new[]
                                 {
                                     "docker_container_cp",
                                     "docker_cp"
                                 };

            var generatorSettings = new SpecificationGeneratorSettings
                                    {
                                        CommandsToSkip = commandsToSkip,
                                        OutputFolder = SpecificationPath,
                                        Reference = reference,
                                        DefinitonFolder = DefinitonRepositoryPath / "_data" / "engine-cli",
                                    };

            SpecificationGenerator.GenerateSpecifications(generatorSettings);
            CodeGenerator.GenerateCode(GlobFiles(SpecificationPath, "*.json").ToArray(), GenerationBaseDirectory, useNestedNamespaces: false,
                baseNamespace: ToolNamespace, repository: GitRepository);
        });

    Target CompilePlugin => _ => _
        .DependsOn(Generate)
        .Executes(() =>
        {
            DotNetRestore(s => DefaultDotNetRestore.SetProjectFile(DockerProject));
            DotNetBuild(s => DefaultDotNetBuild.SetProjectFile(DockerProject).EnableNoRestore());
        });

    Target Changelog => _ => _
        .OnlyWhen(() => InvokedTargets.Contains(nameof(Changelog)))
        .Executes(() =>
        {
            FinalizeChangelog(ChangelogFile, GitVersion.SemVer, GitRepository);

            Git($"add {ChangelogFile}");
            Git($"commit -m \"Finalize {Path.GetFileName(ChangelogFile)} for {GitVersion.SemVer}.\" -m \"+semver: skip\"");
            Git($"tag -f {GitVersion.SemVer}");
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

            DotNetPack(s => DefaultDotNetPack
                .SetProject(DockerProject)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetVersion(GitVersion.NuGetVersionV2)
                .SetPackageReleaseNotes(releaseNotes));
        });

    Target Push => _ => _
        .DependsOn(Pack)
        .Requires(() => ApiKey)
        .Requires(() => !GitHasUncommitedChanges())
        .Requires(() => !NuGet || GitVersionAttribute.Bump.HasValue)
        .Requires(() => !NuGet || Configuration.EqualsOrdinalIgnoreCase("release"))
        .Requires(() => !NuGet || GitVersion.BranchName.Equals("master"))
        .Executes(() =>
        {
            GlobFiles(OutputDirectory, "*.nupkg")
                .Where(x => !x.EndsWith(".symbols.nupkg")).NotEmpty()
                .ForEach(x => DotNetNuGetPush(s => s
                    .SetTargetPath(x)
                    .SetSource(Source)
                    .SetSymbolSource(SymbolSource)
                    .SetApiKey(ApiKey)));
        });
}
