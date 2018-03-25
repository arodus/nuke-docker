using System;
using System.Linq;
using Nuke.CodeGeneration;
using Nuke.Common.Git;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Core;
using Nuke.Core.Tooling;

using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;

class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Pack);

    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository GitRepository;
    
    Target Clean => _ => _
            .Executes(() =>
            {
                DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
                EnsureCleanDirectory(OutputDirectory);
            });

    Target Generate => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var generatorProjectFile = SourceDirectory / "Nuke.Docker.Generator" / "Nuke.Docker.Generator.csproj";
            var metadataJsonFile = TemporaryDirectory / "Docker.json";

            var commandsToSkip = new[]
                                 {
                                     "docker_container_cp",
                                     "docker_plugin_install",
                                     "docker_plugin_set",
                                     "docker_service_scale",
                                     "docker_cp"
                                 };

            DotNetRun(x => x.SetConfiguration(Configuration)
                .SetWorkingDirectory(SolutionDirectory)
                .SetProjectFile(generatorProjectFile)
                .SetApplicationArguments(
                    $"{metadataJsonFile} --skip={commandsToSkip.Aggregate(string.Empty, (current, next) => $"{current}+{next}").TrimStart(trimChar: '+')}"));

            CodeGenerator.GenerateCode(new string[] { metadataJsonFile }, SourceDirectory / "Nuke.Docker", useNestedNamespaces: false,
                baseNamespace: "Nuke.Docker", repository: GitRepository);
        });

    string DockerProject => SourceDirectory / "Nuke.Docker" / "Nuke.Docker.csproj";

    Target Compile => _ => _
        .DependsOn(Generate)
        .Executes(() =>
        {
            DotNetRestore(s => DefaultDotNetRestore.SetProjectFile(DockerProject));
            DotNetBuild(s => DefaultDotNetBuild.SetProjectFile(DockerProject).EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(s => DefaultDotNetPack
                .SetProject(DockerProject)
                .EnableNoBuild()
                .EnableNoRestore());
        });
}
