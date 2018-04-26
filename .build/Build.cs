using System;
using System.Linq;
using Nuke.CodeGeneration;
using Nuke.Common.Git;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Xunit;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.OpenCover.OpenCoverTasks;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.Tools.Xunit.XunitTasks;
using static Nuke.Common.Tools.Git.GitTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Pack);

    [Parameter("ApiKey for the specified source.")] readonly string ApiKey;
    [Parameter("Indicates to push to nuget.org feed.")] readonly bool NuGet;

    string Source => NuGet
        ? "https://api.nuget.org/v3/index.json"
        : "https://www.myget.org/F/nukebuild/api/v2/package";

    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository GitRepository;

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(OutputDirectory);
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
        .DependsOn(Compile)
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

    Target Generate => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var generatorProjectFile = SourceDirectory / "Nuke.Docker.Generator" / "Nuke.Docker.Generator.csproj";
            var metadataJsonFile = TemporaryDirectory / "Docker.json";

            var commandsToSkip = new[]
                                 {
                                     "docker_container_cp",
                                     "docker_cp"
                                 };

            DotNetRun(x => x
                .SetConfiguration(Configuration)
                .SetWorkingDirectory(SolutionDirectory)
                .SetProjectFile(generatorProjectFile)
                .EnableNoBuild()
                .EnableNoRestore()
                .EnableNoLaunchProfile()
                .SetFramework("netcoreapp2.0")
                .SetApplicationArguments(
                    $"{metadataJsonFile} --skip={commandsToSkip.Aggregate(string.Empty, (current, next) => $"{current}+{next}").TrimStart(trimChar: '+')}"));

            CodeGenerator.GenerateCode(new string[] { metadataJsonFile }, SourceDirectory / "Nuke.Docker", useNestedNamespaces: false,
                baseNamespace: "Nuke.Docker", repository: GitRepository);
        });

    string DockerProject => SourceDirectory / "Nuke.Docker" / "Nuke.Docker.csproj";

    Target CompilePlugin => _ => _
        .DependsOn(Generate)
        .Executes(() =>
        {
            DotNetRestore(s => DefaultDotNetRestore.SetProjectFile(DockerProject));
            DotNetBuild(s => DefaultDotNetBuild.SetProjectFile(DockerProject).EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(CompilePlugin)
        .Executes(() =>
        {
            DotNetPack(s => DefaultDotNetPack
                .SetProject(DockerProject)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetVersion(GitVersion.NuGetVersionV2));
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
            GlobFiles(OutputDirectory, "*.nupkg").NotEmpty()
                .Where(x => !x.EndsWith("symbols.nupkg"))
                .ForEach(x => DotNetNuGetPush(s => s
                    .SetTargetPath(x)
                    .SetSource(Source)
                    .SetApiKey(ApiKey)));
        });
}
