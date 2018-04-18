// Copyright Sebastian Karasek, Matthias Koch 2018.
// Distributed under the MIT License.
// https://github.com/nuke-build/docker/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Nuke.Docker.Generator.Tests
{
    public class UsageParserTests
    {
        [Theory]
        [InlineData("CONTAINER", true)]
        [InlineData("[OPTIONS]", true)]
        [InlineData("[REPOSITORY[:TAG]]", true)]
        [InlineData("SOURCE_IMAGE[:TAG]", true)]
        [InlineData("[SECRET...]", true)]
        [InlineData("[KEY=VALUE...]", true)]
        [InlineData("SRC_PATH", true)]
        [InlineData("container", false)]
        [InlineData("KEY=VALUE [KEY=VALUE...]", true)]
        [InlineData("self|NODE [NODE...]", true)]
        public void TestIsArgument(string usage, bool isArgument)
        {
            ParseSingle(usage).IsArgument.Should().Be(isArgument);
        }

        [Theory]
        [InlineData("docker container diff CONTAINER", 4)]
        [InlineData("docker attach [OPTIONS] CONTAINER", 4)]
        [InlineData("docker build [OPTIONS] PATH | URL | -", 4)]
        [InlineData("docker checkpoint create [OPTIONS] CONTAINER CHECKPOINT", 6)]
        [InlineData("docker commit [OPTIONS] CONTAINER [REPOSITORY[:TAG]]", 5)]
        [InlineData("docker config inspect [OPTIONS] CONFIG [CONFIG...]", 5)]
        [InlineData("docker container cp [OPTIONS] CONTAINER:SRC_PATH DEST_PATH|-", 6)]
        [InlineData("docker container create [OPTIONS] IMAGE [COMMAND] [ARG...]", 7)]
        [InlineData("docker container port CONTAINER [PRIVATE_PORT[/PROTO]]", 5)]
        [InlineData("docker image tag SOURCE_IMAGE[:TAG] TARGET_IMAGE[:TAG]", 5)]
        [InlineData("docker inspect [OPTIONS] NAME|ID [NAME|ID...]", 4)]
        [InlineData("docker plugin install [OPTIONS] PLUGIN [KEY=VALUE...]", 6)]
        [InlineData("docker pull [OPTIONS] NAME[:TAG|@DIGEST]", 4)]
        [InlineData("docker secret rm SECRET [SECRET...]", 4)]
        [InlineData("docker node inspect [OPTIONS] self|NODE [NODE...]", 5)]
        public void TestArgumentCount(string usage, int expectedArguments)
        {
            Parse(usage).Should().HaveCount(expectedArguments);
        }

        [Theory]
        [InlineData("SECRET [SECRET...]", true)]
        [InlineData("[CONFIG...]", true)]
        [InlineData("SECRET", false)]
        [InlineData("[KEY=VALUE...]", false)]
        [InlineData("KEY=VALUE [KEY=VALUE...]", false)]
        [InlineData("self|NODE [NODE...]", true)]
        public void TestIsList(string usage, bool isList)
        {
            ParseSingle(usage).IsList.Should().Be(isList);
        }

        [Theory]
        [InlineData("SECRET [SECRET...]", false)]
        [InlineData("[CONFIG...]", false)]
        [InlineData("SECRET", false)]
        [InlineData("[KEY=VALUE...]", true)]
        [InlineData("KEY=VALUE [KEY=VALUE...]", true)]
        public void TestIsDictionary(string usage, bool isDictionary)
        {
            ParseSingle(usage).IsDictionary.Should().Be(isDictionary);
        }

        [Theory]
        [InlineData("[REPOSITORY[:TAG]]", "Repository")]
        [InlineData("SOURCE_IMAGE[:TAG]", "SourceImage")]
        [InlineData("CONTAINER:SRC_PATH", "Container")]
        [InlineData("SECRET [SECRET...]", "Secret")]
        [InlineData("[CONFIG...]", "Config")]
        [InlineData("SECRET", "Secret")]
        [InlineData("[KEY=VALUE...]", "KeyValue")]
        public void TestName(string usage, string expectedName)
        {
            ParseSingle(usage).Name.Should().Be(expectedName);
        }

        private static UsageParameter ParseSingle(string parameter)
        {
            var parameters = UsageParser.Parse(parameter);
            parameters.Should().HaveCount(expected: 1);
            return parameters[index: 0];
        }

        private static List<UsageParameter> Parse(string usage)
        {
            return UsageParser.Parse(usage);
        }
    }
}