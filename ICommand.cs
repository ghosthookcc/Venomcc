using System;
using Venomcc.Utility.Command;

namespace Venomcc.ICommand
{
    public enum CommandScope
    {
        local,
        network,
    }

    public struct CommandInfo
    {
        string? commandName { get; }
        List<string>? commandArgs { get; set;  }
        public CommandScope scope { get; }

        ICommand? command;

        public CommandInfo(string commandName, List<string>? commandArgs, CommandScope scope)
        {
            if (CommandUtilities.commands.ContainsKey(commandName))
            {
                this.commandName = commandName;
                command = CommandUtilities.commands[commandName];
            }
            if (commandArgs != null)
            {
                this.commandArgs = commandArgs;
            }
            this.scope = scope;
        }
    }

    public interface ICommand
    {
        void SelectCommand(string commandName); 
        void Execute();       
    }

    public abstract class Command : ICommand
    {
        private Command instance;
        private CommandInfo selectedCommand;
        public void SelectCommand(string commandName)
        {
            if (CommandUtilities.commands.ContainsKey(commandName))
            {
                instance = CommandUtilities.commands[commandName];
                selectedCommand = instance.getSelectedCommand();
            }
        }
        public abstract void Execute();

        public CommandInfo getSelectedCommand()
        {
            return selectedCommand;
        }

        public Command(CommandInfo selectedCommand)
        {
            this.selectedCommand = selectedCommand;
            instance = this;
        }
    }

    public class listCons : Command
    {
        public listCons(CommandInfo selectedCommand) : base(selectedCommand)
        {
            
        }

        public override void Execute()
        {
            throw new NotImplementedException();
        }
    }
}
