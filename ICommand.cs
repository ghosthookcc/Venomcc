using System;
using System.Runtime.InteropServices;
using Venomcc.ICommand;
using Venomcc.Utility.Command;

namespace Venomcc.ICommand
{
    public enum CommandScope
    {
        local,
        network,
    }

    public interface ICommand
    {
        CommandScope scope { get; }
        void setArguments(List<string>? args);
        void addArgument(string? arg);
        void Execute();       
    }

    public class CommandInterpreter
    {
        private Stack<ICommand> _commandHistory = new Stack<ICommand>();

        public void Execute(string commandName, List<string>? args = null)
        {
            ICommand? commandToRun = CommandUtilities.getCommand(commandName);
            if (commandToRun != null) 
            {
                if (args != null)
                {
                    commandToRun.setArguments(args);
                }
                commandToRun.Execute();
                _commandHistory.Push(commandToRun);
            }
        }

        public void Execute(ICommand command) 
        { 
            command.Execute(); 
            _commandHistory.Push(command); 
        }

        public CommandInterpreter()
        {

        }
    }

    public class listCons : ICommand
    {
        CommandScope ICommand.scope => CommandScope.local;
        public void setArguments(List<string>? args = null)
        {
            if (args != null)
            {
                
            }
        }
        public void addArgument(string? arg = null)
        {
            if (arg != null)
            {

            }
        }
        
        public void Execute()
        {

        }

        public listCons()
        {

        }
    }
}
