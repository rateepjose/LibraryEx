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
        public ICommandDispatchClient[] Clients { get; private set; }

        #region Constructor and Destructor

        public CommandDispatchManager(ICommandDispatchClient[] clients )
        {
            Clients = clients ?? new ICommandDispatchClient[0];
            _aop = new ActiveObjectPart("CommandDispatchManager") { ServiceFunc = Poll };
        }

        #endregion

        private List<string> _finishedRunningCmds = new List<string>();
        private void Poll()
        {
            foreach (var runningCmd in _runningCommands) { if (runningCmd.Value.IsComplete) { _finishedRunningCmds.Add(runningCmd.Key); } }
            foreach (var finishedRunningCmd in _finishedRunningCmds)
            {
                var reservations = _commandAndReservationTable[finishedRunningCmd].reservations;
                SetReservationsTo(string.Empty, reservations);
                _runningCommands.Remove(finishedRunningCmd);
            }
        }

        private Dictionary<string, (ICommandDispatchClient client,string[] reservations)> _commandAndReservationTable = new Dictionary<string, (ICommandDispatchClient, string[])>();
        private Dictionary<string, string> _reservationAndReasonMap = new Dictionary<string, string>();
        public ICommandProxy HarvestCommands() => _aop.CreateCommand("HarvestCommand", _ => PerformHarvestCommands());
        private string PerformHarvestCommands()
        {
            foreach (var client in Clients)
            {
                foreach (var kvp in client.CommandReservationTable)
                {
                    _commandAndReservationTable[$"{client.Name}.{kvp.Key}"] = (client, kvp.Value);
                    foreach (var reservation in kvp.Value) { _reservationAndReasonMap[reservation] = string.Empty; }
                }
            }
            return string.Empty;
        }

        private void SetReservationsTo(string reservationReason, string[] reservations)
        {
            if (reservations == null) return;
            foreach (var reservation in reservations) { _reservationAndReasonMap[reservation] = reservationReason; }
        }

        private Dictionary<string, ICommandStatus> _runningCommands = new Dictionary<string, ICommandStatus>();
        public ICommandProxy DispatchCommand(string name, string command, Dictionary<string, string> parameters) => _aop.CreateCommand("DispatchCommand", x => PerformDispatchCommand(name, command, parameters, x));
        private string PerformDispatchCommand(string name, string command, Dictionary<string, string> parameters, ICommandParams commandParams)
        {
            string nameCommandKey = $"{name}.{command}";
            //Check if the command is already in the runningCommands list
            if (!_runningCommands.ContainsKey(nameCommandKey)) return $"Command '{command}' registered by {name} is currently running";

            //lookup for the command
            if (!_commandAndReservationTable.TryGetValue(nameCommandKey, out var resTable)) { return $"Command '{command}' not found in '{name}'"; }

            //Check if reservations for the given command are free
            if (!resTable.reservations.All((x) => _reservationAndReasonMap[x].IsNullOrEmpty())) return $"Command '{command}' cannot be executed since some resources are reserved.(Reserved items:'{string.Join(",", resTable.reservations.Where(x => !_reservationAndReasonMap[x].IsNullOrEmpty()).Select(y => $"[{y}={_reservationAndReasonMap[y]}]").ToArray())}')";

            //Set all the reservations for the given command to busy
            SetReservationsTo(nameCommandKey, resTable.reservations);

            //Add the command to runningCommands
            commandParams.SetOutputParams(new DispatcherOutputParams() { CommandStatus = _runningCommands[nameCommandKey] = resTable.client.StartCommand(command, parameters).Start() });
            return string.Empty;
        }
    }
}
