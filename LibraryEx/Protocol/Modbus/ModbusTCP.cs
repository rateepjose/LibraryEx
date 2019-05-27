using LibraryEx.DataManipulations;
using LibraryEx.SharedData;
using System;
using System.Net.Sockets;
using System.Threading;

namespace LibraryEx.Modbus.TCP
{
    /// <summary>
    /// ADU-TCP packet consists of bytes in the following order 'MbapHeader[7byte]|PDU[2-253bytes]'.
    /// </summary>
    public class ADU
    {
        public class MbapHeader
        {
            public static long _transactionIdentifier = 1;
            public UInt16 TransactionIdentifier { get => Utils.GetWordFrmBigEndArr(Buffer, 0); set => Utils.SetWordToBigEndArr(value, Buffer, 0); }
            public UInt16 ProtocolIdentifier { get => Utils.GetWordFrmBigEndArr(Buffer, 2); set => Utils.SetWordToBigEndArr(value, Buffer, 2); }
            public UInt16 DataLength { get => GetDataLength(Buffer); set => Utils.SetWordToBigEndArr(value, Buffer, 4); }
            private const byte _unitIdentifierConst = 0xFF;//Fixed as per Modbus.org documentation(not used for TCP/IP)
            public byte UnitIdentifier { get => Buffer[6]; private set => Buffer[6] = value; }
            public SharedSubArray<byte> Buffer { get; private set; }

            private const int _mbapHeaderSize = 7;
            public static int Size => _mbapHeaderSize;
            private MbapHeader(SharedSubArray<byte> buffer) { Buffer = buffer; UnitIdentifier = _unitIdentifierConst; }
            public void UpdateTransactionIdentifier() => TransactionIdentifier = (UInt16)Interlocked.Increment(ref _transactionIdentifier); //TODO:need logic to avoid zero

            public static UInt16 GetDataLength(SharedSubArray<byte> buffer) => Utils.GetWordFrmBigEndArr(buffer, 4);

            public static string Create(SharedSubArray<byte> buffer, out MbapHeader mbap) { try { mbap = new MbapHeader(buffer); return string.Empty; } catch (Exception ex) { mbap = null; return ex.Message; } }
        }

        public MbapHeader MBAP { get; private set; }
        public PDU PDU { get; private set; }
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Used for both 'packets that are re-created from network bytes' and 'creation of packet'
        /// </summary>
        /// <param name="buffer"></param>
        private ADU(byte[] buffer) => Buffer = buffer;

        public static string CreateReadCoils(UInt16 startAddress, UInt16 numberOfCoils, out ADU adu)
        {
            try
            {
                var buffer = new byte[MbapHeader.Size + PDU.ReadCoilsRequest.GetSize()];
                //Create PDU
                var ec = PDU.ReadCoilsRequest.Create(new SharedSubArray<byte>(buffer, MbapHeader.Size), startAddress, numberOfCoils, out var pdu);
                if (!ec.IsNullOrEmpty()) { adu = null; return ec; }
                //Create MBAPHeader
                ec = MbapHeader.Create(new SharedSubArray<byte>(buffer, 0, MbapHeader.Size), out var mbapHeader);
                if (!ec.IsNullOrEmpty()) { adu = null; return ec; }
                mbapHeader.DataLength = (UInt16)(PDU.ReadCoilsRequest.GetSize() + 1);
                adu = new ADU(buffer) { MBAP = mbapHeader, PDU = pdu };
                return string.Empty;
            }
            catch (Exception ex) { adu = null; return ex.Message; }
        }

        public static string CreateFromByteArray(byte[] arr, CommMode commMode, out ADU adu)
        {
            //Create MbapHeader from array
            var ec = MbapHeader.Create(new SharedSubArray<byte>(arr, 0, MbapHeader.Size), out var mbapHeader);
            if (!ec.IsNullOrEmpty()) { adu = null; return ec; }
            //Create PDU from array
            var subArray = new SharedSubArray<byte>(arr, MbapHeader.Size);
            ec = PDU.CreateFromByteArray(subArray, commMode, out var pdu);
            if (!ec.IsNullOrEmpty()) { adu = null; return ec; }
            //If here, then we have a valid adu packet
            adu = new ADU(arr) { MBAP = mbapHeader, PDU = pdu, };
            return string.Empty;
        }

    }

    public class Client
    {
        private volatile bool _runThread = true;
        private readonly object _lock = new object();
        private const int _defaultPort = 502;
        private readonly int _port;
        private readonly Thread _thread;
        private TimeSpan _transactionTimeout;
        private TcpClient _client;
        private readonly string _ipAddress;
        public Client(string ipAddress, TimeSpan transactionTimeout, int port = _defaultPort)
        {
            _ipAddress = ipAddress;
            _transactionTimeout = transactionTimeout;
            _port = port;
            _client = new TcpClient();
            (_thread = new Thread(() => { while (_runThread) { try { lock (_lock) { Poll(); } } catch { } Thread.Sleep(100); } }) { IsBackground = true, }).Start();
        }

        private void Poll()
        {
            if (!_connectRequested) { return; }
            //Todo:reconnect logic
        }

        private string RunSafeFunc(Func<string> runFunc)
        {
            try { lock (_lock) { return runFunc(); } }
            catch (Exception ex) { return ex.Message; }
        }

        public string Connect() => RunSafeFunc(PerformConnect);
        private bool _connectRequested;
        public bool Connected => _client.Connected;
        private string PerformConnect()
        {
            try
            {
                _connectRequested = true;
                try { _client?.Close(); Thread.Sleep(10); } catch { }
                (_client = new TcpClient()).Connect(_ipAddress, _port);
                return string.Empty;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public string Disconnect() => RunSafeFunc(PerformDisconnect);
        private string PerformDisconnect()
        {
            _connectRequested = false;
            try { _client?.Close(); Thread.Sleep(10); return string.Empty; }
            catch (Exception ex) { return ex.Message; }
        }

        private string SendBytes(byte[] dataToServer)
        {
            try { var ns = _client.GetStream(); ns.Write(dataToServer, 0, dataToServer.Length); return string.Empty; }
            catch (Exception ex) { return ex.Message; }
        }

        private string ReceiveBytes(int byteCount, int timeout, out byte[] dataFromServer)
        {
            try
            {
                var oldTimeout = _client.ReceiveTimeout;
                _client.ReceiveTimeout = timeout;
                var ns = _client.GetStream();
                byte[] data = new byte[byteCount];
                var bytesReceived = ns.Read(data, 0, byteCount);
                if (bytesReceived != byteCount) { dataFromServer = null; return $"Failed to receive '{byteCount}' bytes from Server. [Total received= '{bytesReceived}'; Raw Byte Data='{BitConverter.ToString(data)}'] "; }
                dataFromServer = data;
                return string.Empty;
            }
            catch (Exception ex) { dataFromServer = null; return ex.Message; }
        }
        private string ReceiveAduBytes(TimeSpan timeout, out byte[] dataFromServer)
        {
            try
            {
                //TODO: ReceiveBytes can be optimized to take in a fixed 260(7+253) byte array and hence prevent creation of two additional arrays and a copy. This is under the assumption that packet sizes will be split if they exceed 260 bytes per request/response
                //Get the first set of bytes(expecting MBAPHeader)
                var ec = ReceiveBytes(ADU.MbapHeader.Size, timeout.Milliseconds, out var headerBytes);
                if (!ec.IsNullOrEmpty()) { dataFromServer = null; return ec; }

                var packetSize = ADU.MbapHeader.GetDataLength(new SharedSubArray<byte>(headerBytes, 0));

                //Get the remaining bytes(pdu)
                var remainingPacketBytes = packetSize - 1;
                ec = ReceiveBytes(remainingPacketBytes, timeout.Milliseconds, out var pduBytes);
                if (!ec.IsNullOrEmpty()) { dataFromServer = null; return ec; }

                var aduBytes = new byte[ADU.MbapHeader.Size + pduBytes.Length];
                Array.Copy(headerBytes, aduBytes, ADU.MbapHeader.Size);
                Array.Copy(pduBytes, 0, aduBytes, ADU.MbapHeader.Size, pduBytes.Length);
                dataFromServer = aduBytes;
                return string.Empty;
            }
            catch (Exception ex) { dataFromServer = null; return ex.Message; }
        }

        public string SendReadCoilsCommand(UInt16 startAddress, UInt16 numberOfCoils) => RunSafeFunc(() => PerformSendReadCoilsCommand(startAddress, numberOfCoils));
        private string PerformSendReadCoilsCommand(UInt16 startAddress, UInt16 numberOfCoils)
        {
            var ec = ADU.CreateReadCoils(startAddress, numberOfCoils, out var aduRequest);
            if (!ec.IsNullOrEmpty()) { return ec; }

            ec = PerformSendCommand(aduRequest, out var aduResponse);
            if (!ec.IsNullOrEmpty()) { return ec; }

            //Todo: get the output coils
            return string.Empty;
        }

        public string SendCommand(ADU aduRequest, out ADU aduResponse) { try { lock (_lock) { return PerformSendCommand(aduRequest, out aduResponse); } } catch (Exception ex) { aduResponse = null; return ex.Message; } }
        private string PerformSendCommand(ADU aduRequest, out ADU aduResponse)
        {
            var ec = SendBytes(aduRequest.Buffer);
            if (!ec.IsNullOrEmpty()) { aduResponse = null; return ec; }

            ec = ReceiveAduBytes(TimeSpan.FromMilliseconds(500), out var dataFromServer);
            if (!ec.IsNullOrEmpty()) { aduResponse = null; return ec; }

            ec = ADU.CreateFromByteArray(dataFromServer, CommMode.Client, out var aduRsp);
            if (!ec.IsNullOrEmpty()) { aduResponse = null; return ec; }

            aduResponse = aduRsp;
            return string.Empty;
        }
    }

}