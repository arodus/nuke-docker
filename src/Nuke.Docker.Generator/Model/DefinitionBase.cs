// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace Nuke.Docker.Generator.Model
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    internal abstract class DefinitionBase
    {
        [YamlMember(Alias = "deprecated")] public bool Deprecated { get; set; }
        [YamlMember(Alias = "experimental")] public bool Experimental { get; set; }

        [YamlMember(Alias = "experimentalcli")]
        public bool ExperimentalCli { get; set; }

        [YamlMember(Alias = "kubernetes")] public bool Kubernetes { get; set; }

        [YamlMember(Alias = "min_api_version")]
        public string MinApiVersion { get; set; }

        [YamlMember(Alias = "swarm")] public bool Swarm { get; set; }
    }
}