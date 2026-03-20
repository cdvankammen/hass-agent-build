using System;
using System.Collections.Generic;
using Xunit;
using HASS.Agent.Core;

namespace HASS.Agent.Core.Tests
{
    public class LegacyCommandMapperTests
    {
        [Fact]
        public void PowershellCommand_IsMapped_WithArgsConcatenated()
        {
            var cfg = new ConfiguredCommand
            {
                Id = Guid.NewGuid(),
                Name = "PS",
                Type = "PowershellCommand",
                Command = "powershell.exe -NoProfile -Command",
                Args = "Write-Output 'hi'",
                EntityType = null
            };

            var mapped = LegacyCommandMapper.MapConfiguredCommands(new List<ConfiguredCommand> { cfg });
            Assert.Single(mapped);
            Assert.Equal("powershell", mapped[0].EntityType, ignoreCase: true);
            Assert.Contains("Write-Output", mapped[0].Command);
        }

        [Fact]
        public void KeyCommand_Preserves_KeyCode_And_Keys()
        {
            var cfg = new ConfiguredCommand
            {
                Id = Guid.NewGuid(),
                Name = "Key",
                Type = "KeyCommand",
                KeyCode = "VK_MEDIA_PLAY",
                Keys = new List<string> { "Ctrl", "Alt", "K" },
                EntityType = null
            };

            var mapped = LegacyCommandMapper.MapConfiguredCommands(new List<ConfiguredCommand> { cfg });
            Assert.Single(mapped);
            Assert.Equal("key", mapped[0].EntityType, ignoreCase: true);
            Assert.Equal("VK_MEDIA_PLAY", mapped[0].KeyCode);
            Assert.Equal(3, mapped[0].Keys.Count);
        }

        [Fact]
        public void Map_NullList_Returns_Empty()
        {
            var mapped = LegacyCommandMapper.MapConfiguredCommands(null);
            Assert.NotNull(mapped);
            Assert.Empty(mapped);
        }

        [Fact]
        public void UnknownType_IsPreserved_AsEntityType()
        {
            var cfg = new ConfiguredCommand
            {
                Id = Guid.NewGuid(),
                Name = "Unknown",
                Type = "SomeUnknownType",
                Command = "do something"
            };

            var mapped = LegacyCommandMapper.MapConfiguredCommands(new List<ConfiguredCommand> { cfg });
            Assert.Single(mapped);
            Assert.Equal("SomeUnknownType", mapped[0].EntityType);
        }

        [Fact]
        public void MultipleKeysCommand_Copies_Keys()
        {
            var cfg = new ConfiguredCommand
            {
                Id = Guid.NewGuid(),
                Name = "Multiple",
                Type = "MultipleKeysCommand",
                Keys = new List<string> { "A", "B" }
            };

            var mapped = LegacyCommandMapper.MapConfiguredCommands(new List<ConfiguredCommand> { cfg });
            Assert.Single(mapped);
            Assert.Equal(2, mapped[0].Keys.Count);
            // deep copy check
            cfg.Keys.Add("C");
            Assert.Equal(2, mapped[0].Keys.Count);
        }

        [Fact]
        public void CommandConverter_Roundtrip_Preserves_Args()
        {
            var original = new ConfiguredCommand
            {
                Id = Guid.NewGuid(),
                Name = "RT",
                Command = "echo",
                Args = "hi",
                KeyCode = "",
                Keys = new List<string>()
            };

            var model = CommandConverter.ToCommandModel(original);
            var back = CommandConverter.FromCommandModel(model);
            Assert.Equal(original.Command, back.Command);
            Assert.Equal(original.Args, back.Args);
        }

        [Fact]
        public void PowershellCommand_Quotes_Args_When_Spaces()
        {
            var cfg = new ConfiguredCommand
            {
                Id = Guid.NewGuid(),
                Name = "PS2",
                Type = "PowershellCommand",
                Command = "pwsh",
                Args = "Write-Output 'hello world'"
            };

            var mapped = LegacyCommandMapper.MapConfiguredCommands(new List<ConfiguredCommand> { cfg });
            Assert.Single(mapped);
            // Expect args to be quoted because they contain spaces
            Assert.Equal("pwsh \"Write-Output 'hello world'\"", mapped[0].Command);
        }
    }
}
