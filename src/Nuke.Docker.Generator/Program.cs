// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;

namespace Nuke.Docker.Generator
{
    internal class Program
    {
        private static void Main()
        {
            var args = Environment.GetCommandLineArgs();

            SpecificationGenerator.GenerateSpecifications(new SpecificationGeneratorSettings
                                                          {
                                                              CommandsToSkip = new[]
                                                                               {
                                                                                   "docker_container_cp",
                                                                                   "docker_cp"
                                                                               },
                                                              OutputFolder = args[0],
                                                              DefinitonFolder = args[1],
                                                              Reference = args[2]
                                                          });
        }
    }
}