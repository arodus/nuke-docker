// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Core.Tooling;

namespace Nuke.Docker
{
    [PublicAPI]
    [Serializable]
    public abstract class DockerSettings : ToolSettings
    {
        public CliSettings CliSettings { get; internal set; }

        protected string GetCliSettings()
        {
            return string.Empty;
        }

        protected override Arguments ConfigureArguments(Arguments arguments)
        {
            if (CliSettings != null)
            {
                arguments = CliSettings.CreateArguments().Concatenate(arguments);
            }

            return base.ConfigureArguments(arguments);
        }
    }
}
