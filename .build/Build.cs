// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.IO;
using System.Linq;
using Nuke.CodeGeneration;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Core;
using Nuke.Core.Tooling;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Pack);

    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository GitRepository;
    [Solution] readonly Solution Solution;

    string GeneratorProjectFile => Solution.GetProject("Nuke.Docker.Generator");
    string DockerProjectFile => Solution.GetProject("Nuke.Docker");

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
            DotNetBuild(s => DefaultDotNetBuild
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => DefaultDotNetTest
                .EnableNoBuild()
                .EnableNoRestore()
                .SetResultsDirectory(OutputDirectory));
        });

    Target Generate => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var metadataJsonFile = TemporaryDirectory / "Docker.json";
            var commandsToSkip = new[]
                                 {
                                     "docker_container_cp",
                                     "docker_cp"
                                 };

            DotNetRun(s => s
                .SetProjectFile(GeneratorProjectFile)
                .SetConfiguration(Configuration)
                .SetWorkingDirectory(SolutionDirectory)
                .EnableNoBuild()
                .EnableNoRestore()
                .EnableNoLaunchProfile()
                .SetApplicationArguments(
                    $"{metadataJsonFile} --skip={commandsToSkip.Aggregate(string.Empty, (current, next) => $"{current}+{next}").TrimStart(trimChar: '+')}"));

            CodeGenerator.GenerateCode(
                new string[] { metadataJsonFile },
                Path.GetDirectoryName(DockerProjectFile),
                useNestedNamespaces: false,
                baseNamespace: "Nuke.Docker",
                repository: GitRepository);
        });

    Target Addon => _ => _
        .DependsOn(Generate)
        .Executes(() =>
        {
            DotNetBuild(s => DefaultDotNetBuild
                .ResetNoRestore()
                .SetProjectFile(DockerProjectFile)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Addon)
        .Executes(() =>
        {
            DotNetPack(s => DefaultDotNetPack
                .SetProject(DockerProjectFile)
                .EnableNoBuild()
                .EnableNoRestore());
        });
}