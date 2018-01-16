using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraryEx
{
    public interface ICommandToken { }

    public interface IDataParam { }
    public class DataParam<Param> : IDataParam
    {
        public DataParam(Param value) => Value = value;
        public Param Value { get; private set; }
    }

    public interface ICommandDispatchClient
    {
        string Name { get; }
        Dictionary<string, (string[] reservations, string[] subCommands)> CommandToReservationsAndSubCommandsTable { get; }
        ICommandProxy StartCommand(string command, Dictionary<string, IDataParam> parameters, ICommandToken commandToken);
    }

    public interface ICommandDispatchManager
    {
        ICommandProxy DispatchCommand(string name, string command, Dictionary<string, IDataParam> parameters, ICommandToken commandToken = null);
    }

    /// <summary>
    /// This class assumes that 'InitializeCommandDispatchManager' will not be used in parallel with 'AddClient' function.
    /// InitializeCommandDispatchManager is only to be used once all clients have been added to the _clients list(using the AddClient function)
    /// </summary>
    public static class CommandDispatcher
    {
        private static object _lock = new object();
        private static List<ICommandDispatchClient> _clients = new List<ICommandDispatchClient>();
        public static void AddClient(ICommandDispatchClient client) { lock (_lock) { _clients.Add(client); } }

        public static ICommandDispatchManager CmdDispatchMgr { get; private set; } = new CommandDispatchManager();

        public static ICommandProxy InitializeCommandDispatchManager() { lock (_lock) { return (CmdDispatchMgr as CommandDispatchManager).HarvestCommands(_clients.ToArray()); } }

        #region Datatypes

        /// <summary>
        /// Dispatches commands to respective Clients based on Access and Reservations
        /// </summary>
        private class CommandDispatchManager : ICommandDispatchManager
        {

            #region Datatypes

            private class CommandToken : ICommandToken
            {
                private static ulong _lastId;
                static CommandToken() { _lastId = 1; }

                public CommandToken() => ID = _lastId++;

                public ulong ID { get; private set; }
            }

            public class DispatcherOutputParams : IOutputParams
            {
                public ICommandStatus CommandStatus { get; set; }
            }

            #endregion

            private ActiveObjectPart _aop;

            #region Constructor and Destructor

            public CommandDispatchManager() => (_aop = new ActiveObjectPart("CommandDispatchManager", TimeSpan.FromMilliseconds(5)) { ServiceFunc = Poll }).Initialize();

            #endregion

            private List<string> _finishedRunningCmds = new List<string>();
            private void Poll()
            {
                foreach (var runningCmd in _runningCommands) { if (runningCmd.Value.cmdStatus.IsComplete) { _finishedRunningCmds.Add(runningCmd.Key); } }
                foreach (var finishedRunningCmd in _finishedRunningCmds)
                {
                    var reservations = _commandToClientAndReservationsMap[finishedRunningCmd].reservations;
                    SetReservationsTo(string.Empty, reservations);
                    //Evaluate the Commands based on the reservation changes
                    EvaluateCommandDisableReason(reservations);

                    _runningCommands.Remove(finishedRunningCmd);
                }
                _finishedRunningCmds.Clear();
            }

            private Dictionary<string, (ICommandDispatchClient client, string[] reservations)> _commandToClientAndReservationsMap = new Dictionary<string, (ICommandDispatchClient, string[])>();
            private Dictionary<string, string> _reservationToReasonMap = new Dictionary<string, string>();
            private Dictionary<string, string[]> _reservationToCommandsMap = new Dictionary<string, string[]>();
            private Dictionary<string, RefObjectPublisher<string>> _commandToDisableReasonMap = new Dictionary<string, RefObjectPublisher<string>>();
            public ICommandProxy HarvestCommands(ICommandDispatchClient[] clients) => _aop.CreateCommand("HarvestCommand", _ => PerformHarvestCommands(clients));
            private string PerformHarvestCommands(ICommandDispatchClient[] clients)
            {
                _commandToClientAndReservationsMap.Clear();
                _reservationToReasonMap.Clear();
                _commandToDisableReasonMap.Clear();
                _reservationToCommandsMap.Clear();

                //Create a temporary table with fullcommandName(controller+command) and tuple of 'client,reservations,subcommands'
                var cmdToClientAndReservationsAndSubCommands = new Dictionary<string, (ICommandDispatchClient client, string[] reservations, string[] subCommands)>();
                foreach (var client in clients)
                {
                    foreach (var kvp in client.CommandToReservationsAndSubCommandsTable)
                    {
                        string fullNameCommand = $"{client.Name}.{kvp.Key}";

                        cmdToClientAndReservationsAndSubCommands[fullNameCommand] = (client, kvp.Value.reservations, kvp.Value.subCommands);
                        //Create a new table(one to one) with command & DisableReason
                        _commandToDisableReasonMap[fullNameCommand] = new RefObjectPublisher<string>() { Object = string.Empty };
                    }
                }

                //Create a new table with fullcommandName(controller+command) & 'tuple of dispatchClient and reservationsList' with subcommands(if any) needs to be translated to corresponding reservations
                List<string> commands = cmdToClientAndReservationsAndSubCommands.Keys.ToList();
                while (cmdToClientAndReservationsAndSubCommands.Count > _commandToClientAndReservationsMap.Count)
                {
                    List<string> harvestCompletedCmds = new List<string>();
                    foreach (var cmd in commands)
                    {
                        if (TryGetAllReservations(cmd, cmdToClientAndReservationsAndSubCommands, out var rsvtns))
                        {
                            _commandToClientAndReservationsMap[cmd] = (cmdToClientAndReservationsAndSubCommands[cmd].client, rsvtns);
                            harvestCompletedCmds.Add(cmd);
                        }
                    }
                    foreach (var item in harvestCompletedCmds) { commands.Remove(item); }
                }

                //Create a new table(one to one) with reservation & reason map
                foreach (var clientAndReservations in _commandToClientAndReservationsMap.Values) { foreach (string reservation in clientAndReservations.reservations) { _reservationToReasonMap[reservation] = string.Empty; } }

                //Create a new table(one to many) (for reverse lookup) with reservation & commands
                _reservationToCommandsMap = _reservationToReasonMap.ToDictionary(a => a.Key, b => _commandToClientAndReservationsMap.Where(x => x.Value.reservations.Contains(b.Key)).Select(y => y.Key).ToArray());
                //Add the same to the ModelObserverCollection so that UI can subscribe if required
                ModelObserverCollection.Add(_commandToDisableReasonMap.ToDictionary(a => a.Key, b => new RefObjectObserver<string>(b.Value) as IRefObjectObserver));
                return string.Empty;
            }

            private bool TryGetAllReservations(string command, Dictionary<string, (ICommandDispatchClient client, string[] reservations, string[] subCommands)> cmdToClientAndReservationsAndSubCommands, out string[] rsvtns)
            {
                rsvtns = new string[0];
                if (!cmdToClientAndReservationsAndSubCommands.TryGetValue(command, out var clientAndReservationsAndSubCommands)) { return false; }

                List<string> rsvtnList = new List<string>(clientAndReservationsAndSubCommands.reservations);
                foreach (string subCommand in clientAndReservationsAndSubCommands.subCommands)
                {
                    if (!TryGetAllReservations(subCommand, cmdToClientAndReservationsAndSubCommands, out var reservations)) { return false; }
                    rsvtnList.AddRange(reservations);
                }
                rsvtns = rsvtnList.ToArray();
                return true;
            }

            private void SetReservationsTo(string reservationReason, string[] reservations)
            {
                if (reservations == null) return;
                foreach (var reservation in reservations) { _reservationToReasonMap[reservation] = reservationReason; }
            }

            private Dictionary<string, (ICommandStatus cmdStatus, ICommandToken cmdToken)> _runningCommands = new Dictionary<string, (ICommandStatus, ICommandToken)>();
            public ICommandProxy DispatchCommand(string name, string command, Dictionary<string, IDataParam> parameters, ICommandToken commandToken = null) => _aop.CreateCommand("DispatchCommand", x => PerformDispatchCommand(name, command, parameters, commandToken, x));
            private string PerformDispatchCommand(string name, string command, Dictionary<string, IDataParam> parameters, ICommandToken commandToken, ICommandInteraction commandParams)
            {
                //Create commandToken if not an existing request
                commandToken = (commandToken as CommandToken) ?? new CommandToken();

                string fullNameCommand = $"{name}.{command}";
                //Check if the command is already in the runningCommands list
                if (_runningCommands.ContainsKey(fullNameCommand)) return $"Command '{command}' registered by {name} is currently running";

                //lookup for the command
                if (!_commandToClientAndReservationsMap.TryGetValue(fullNameCommand, out var resTable)) { return $"Command '{command}' not found in '{name}'"; }

                //Check if the current command(subcommand) issued matches in token with an already issued command(if so then it can bypass the reservations check and setting)
                if (_runningCommands.Values.Any(x => x.cmdToken == commandToken)) { }
                else
                {
                    //Check if reservations for the given command are free
                    if (!resTable.reservations.All((x) => _reservationToReasonMap[x].IsNullOrEmpty())) return $"Command '{command}' cannot be executed since some resources are reserved.(Reserved items:'{string.Join(",", resTable.reservations.Where(x => !_reservationToReasonMap[x].IsNullOrEmpty()).Select(y => $"[{y}={_reservationToReasonMap[y]}]").ToArray())}')";

                    //Set all the reservations for the given command to busy
                    SetReservationsTo(fullNameCommand, resTable.reservations);

                    //Evaluate the Commands based on the reservation changes
                    EvaluateCommandDisableReason(resTable.reservations);
                }

                //Add the command to runningCommands
                commandParams.SetOutputParams(new DispatcherOutputParams() { CommandStatus = (_runningCommands[fullNameCommand] = (resTable.client.StartCommand(command, parameters, commandToken).Start(), commandToken)).cmdStatus });
                return string.Empty;
            }

            private void EvaluateCommandDisableReason(string[] reservations)
            {
                List<string> commands = new List<string>();
                foreach (string reservation in reservations) { commands.AddRange(_reservationToCommandsMap[reservation]); }
                commands = commands.Distinct().ToList();

                StringBuilder disableReason = new StringBuilder(string.Empty);
                foreach (string command in commands)
                {
                    disableReason.Clear();
                    string[] cmdReservations = _commandToClientAndReservationsMap[command].reservations.ToArray();
                    int itemIndex = 1;
                    foreach (string cmdReservation in cmdReservations)
                    {
                        if (!_reservationToReasonMap[cmdReservation].IsNullOrEmpty()) { disableReason.Append($"{itemIndex++}. {_reservationToReasonMap[cmdReservation]}\n"); }
                    }
                    _commandToDisableReasonMap[command].Object = disableReason.ToString().TrimEnd(new char[] { '\n' });
                }
            }
        }

        #endregion
    }
}
