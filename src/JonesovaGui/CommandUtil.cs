using CliWrap;
using CliWrap.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JonesovaGui
{
    static class CommandUtil
    {
        public static Task<CommandResult> RunAsync(
            this Command command, CommandRunner runner)
        {
            return runner.RunAsync(command);
        }

        public static Command AddArguments(this Command command, string arguments)
        {
            var existing = string.IsNullOrEmpty(command.Arguments) ? string.Empty : $"{command.Arguments} ";
            return command.WithArguments(existing + arguments);
        }

        public static Command AddArguments(this Command command,
            IEnumerable<string> arguments, bool escape = true)
        {
            return command.AddArguments(args => args.Add(arguments, escape));
        }

        public static Command AddArguments(this Command command,
            params string[] arguments)
        {
            return command.AddArguments(arguments.AsEnumerable());
        }

        public static Command AddArguments(this Command command,
            Action<ArgumentsBuilder> configure)
        {
            ArgumentsBuilder argumentsBuilder = new();
            configure(argumentsBuilder);
            return command.AddArguments(argumentsBuilder.Build());
        }
    }
}
