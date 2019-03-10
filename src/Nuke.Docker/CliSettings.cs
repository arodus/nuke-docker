// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Linq;
using Nuke.Common.Tooling;

namespace Nuke.Docker
{
    public partial class CliSettings
    {
        public Arguments CreateArguments()
        {
            return ConfigureArguments(new Arguments());
        }

        public override Action<OutputType, string> CustomLogger => throw new NotSupportedException();
    }
}
