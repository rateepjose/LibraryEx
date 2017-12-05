using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraryEx
{
    /// <summary>
    /// Dipatches commands to respective Clients based on Access and Reservations
    /// </summary>
    public class CommandDispatchManager
    {

        #region Datatypes

        public interface ICommandDispatchClient
        {
            string Name { get; }
            Dictionary<string, string[]> CommandReservationTable { get; }
            ICommandProxy StartCommand(string command, Dictionary<string, string> parameters);
        }

        public class DispatcherOutputParams : IOutputParams
        {
            public ICommandStatus CommandStatus { get; set; }
        }

        #endregion

        private ActiveObjectPart _aop;
        public ICommandDispatchClient[] Clients { get; private set; } = new ICommandDispatchClient[0];

        #region Constructor and Destructor

        public CommandDispatchManager(ICommandDispatchClient[] clients )
        {
            Clients = clients ?? new ICommandDispatchClient[0];
            _aop = new ActiveObjectPart("CommandDispatchManager") { ServiceFunc = Poll };
            _aop.Initialize();
        }

        #endregion

        private List<string> _finishedRunningCmds = new List<string>();
        private void Poll()
        {
            foreach (var runningCmd in _runningCommands) { if (runningCmd.Value.IsComplete) { _finishedRunningCmds.Add(runningCmd.Key); } }
            foreach (var finishedRunningCmd in _finishedRunningCmds)
            {
                var reservations = _commandToReservationsMap[finishedRunningCmd].reservations;
                SetReservationsTo(string.Empty, reservations);
                //Evaluate the Commands based on the reservation changes
                EvaluateCommandDisableReason(reservations);

                _runningCommands.Remove(finishedRunningCmd);
            }
            _finishedRunningCmds.Clear();
        }

        private Dictionary<string, (ICommandDispatchClient client,string[] reservations)> _commandToReservationsMap = new Dictionary<string, (ICommandDispatchClient, string[])>();
        private Dictionary<string, string> _reservationToReasonMap = new Dictionary<string, string>();
        private Dictionary<string, string[]> _reservationToCommandsMap = new Dictionary<string, string[]>();
        private Dictionary<string, RefObjectPublisher<string>> _commandToDisableReasonMap = new Dictionary<string, RefObjectPublisher<string>>();
        public ICommandProxy HarvestCommands() => _aop.CreateCommand("HarvestCommand", _ => PerformHarvestCommands());
        private string PerformHarvestCommands()
        {
            _commandToReservationsMap.Clear();
            _reservationToReasonMap.Clear();
            _commandToDisableReasonMap.Clear();
            _reservationToCommandsMap.Clear();

            foreach (var client in Clients)
            {
                foreach (var kvp in client.CommandReservationTable)
                {
                    string fullNameCommand = $"{client.Name}.{kvp.Key}";
                    //Create a new table(one to many) with fullcommandName(controller+command) & 'tuple of reservationsList and dispatchClient'
                    _commandToReservationsMap[fullNameCommand] = (client, kvp.Value);
                    //Create a new table(one to one) with reservation & reason map
                    foreach (var reservation in kvp.Value) { _reservationToReasonMap[reservation] = string.Empty; }
                    //Create a new table(one to one) with command & DisableReason
                    _commandToDisableReasonMap[fullNameCommand] = new RefObjectPublisher<string>() { Object = string.Empty };
                }
            }
            //Create a new table(one to many) (for reverse lookup) with reservation & commands
            _reservationToCommandsMap = _reservationToReasonMap.ToDictionary(a => a.Key, b => _commandToReservationsMap.Where(x => x.Value.reservations.Contains(b.Key)).Select(y => y.Key).ToArray());
            //Add the same to the ModelObserverCollection so that UI can subscribe if required
            ModelObserverCollection.AddModelObserverToCollection(_commandToDisableReasonMap.ToDictionary(a => a.Key, b => new RefObjectObserver<string>(b.Value) as IRefObjectObserver));
            return string.Empty;
        }

        private void SetReservationsTo(string reservationReason, string[] reservations)
        {
            if (reservations == null) return;
            foreach (var reservation in reservations) { _reservationToReasonMap[reservation] = reservationReason; }
        }

        private Dictionary<string, ICommandStatus> _runningCommands = new Dictionary<string, ICommandStatus>();
        public ICommandProxy DispatchCommand(string name, string command, Dictionary<string, string> parameters) => _aop.CreateCommand("DispatchCommand", x => PerformDispatchCommand(name, command, parameters, x));
        private string PerformDispatchCommand(string name, string command, Dictionary<string, string> parameters, ICommandInteraction commandParams)
        {
            string fullNameCommand = $"{name}.{command}";
            //Check if the command is already in the runningCommands list
            if (_runningCommands.ContainsKey(fullNameCommand)) return $"Command '{command}' registered by {name} is currently running";

            //lookup for the command
            if (!_commandToReservationsMap.TryGetValue(fullNameCommand, out var resTable)) { return $"Command '{command}' not found in '{name}'"; }

            //Check if reservations for the given command are free
            if (!resTable.reservations.All((x) => _reservationToReasonMap[x].IsNullOrEmpty())) return $"Command '{command}' cannot be executed since some resources are reserved.(Reserved items:'{string.Join(",", resTable.reservations.Where(x => !_reservationToReasonMap[x].IsNullOrEmpty()).Select(y => $"[{y}={_reservationToReasonMap[y]}]").ToArray())}')";

            //Set all the reservations for the given command to busy
            SetReservationsTo(fullNameCommand, resTable.reservations);

            //Evaluate the Commands based on the reservation changes
            EvaluateCommandDisableReason(resTable.reservations);

            //Add the command to runningCommands
            commandParams.SetOutputParams(new DispatcherOutputParams() { CommandStatus = _runningCommands[fullNameCommand] = resTable.client.StartCommand(command, parameters).Start() });
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
                string[] cmdReservations = _commandToReservationsMap[command].reservations.ToArray();
                int itemIndex = 1;
                foreach (string cmdReservation in cmdReservations)
                {
                    if (!_reservationToReasonMap[cmdReservation].IsNullOrEmpty()) { disableReason.Append($"{itemIndex++}. {_reservationToReasonMap[cmdReservation]}\n"); }
                }
                _commandToDisableReasonMap[command].Object = disableReason.ToString().TrimEnd(new char[] { '\n' });
            }
        }
    }
}
