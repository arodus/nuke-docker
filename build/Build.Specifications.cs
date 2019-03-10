// Copyright 2019 Maintainers and Contributors of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Docker.Generator;
using static Nuke.Common.Tools.Git.GitTasks;

partial class Build
{
    PathConstruction.AbsolutePath DefinitionRepositoryPath => TemporaryDirectory / "definition-repository";

    Target Specifications => _ => _
        .DependentFor(Generate)
        .Executes(() =>
        {
            if (Directory.Exists(DefinitionRepositoryPath))
                FileSystemTasks.DeleteDirectory(DefinitionRepositoryPath);
            
            var repository = "https://github.com/docker/docker.github.io.git";
            var branch = "master";
            Git($"clone {repository} -b {branch} --single-branch --depth 1 {DefinitionRepositoryPath}");

            var reference = Git($"rev-parse --short {branch}", DefinitionRepositoryPath).Single().Text;

            var settings = new SpecificationGeneratorSettings
                           {
                               CommandsToSkip = new[]
                                                {
                                                    "docker_container_cp",
                                                    "docker_cp"
                                                },
                               OutputFolder = SourceDirectory / "Nuke.Docker",
                               Reference = reference,
                               DefinitionFolder = DefinitionRepositoryPath / "_data" / "engine-cli"
                           };

            SpecificationGenerator.GenerateSpecifications(settings);
        });
}
