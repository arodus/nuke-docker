// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace Nuke.Docker.Generator
{
    [UsedImplicitly]
    internal class CommandDefinition : DefinitionBase
    {
        [YamlMember(Alias = "command")] public string Command { get; set; }
        [YamlMember(Alias = "aliases")] public string Alias { get; set; }
        [YamlMember(Alias = "short")] public string ShortDescription { get; set; }
        [YamlMember(Alias = "long")] public string LongDescription { get; set; }
        [YamlMember(Alias = "usage")] public string Usage { get; set; }

        [YamlMember(Alias = "pname")] public string ParentName { get; set; }

        [YamlMember(Alias = "plink")] public string ParentLink { get; set; }
        [YamlMember(Alias = "examples")] public string Examples { get; set; }

        [YamlMember(Alias = "options")]
        public List<ArgumentDefinition> Arguments { get; set; } = new List<ArgumentDefinition>();

        [YamlMember(Alias = "inherited_options")]
        public List<ArgumentDefinition> InheritedArguments { get; set; } = new List<ArgumentDefinition>();

        [YamlMember(Alias = "cname")] public List<string> ChildNames { get; set; } = new List<string>();
        [YamlMember(Alias = "clink")] public List<string> ChildLinks { get; set; } = new List<string>();
        [YamlIgnore] public string ReferenceUrl { get; set; }
    }
}