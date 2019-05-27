using LibraryEx.DataManipulations;
using LibraryEx.SharedData;
using System;
using System.IO.Ports;
using System.Threading;

namespace LibraryEx.Modbus.RTU
{
    /// <summary>
    /// ADU-RTU packet consists of bytes in the following order 'Address[1byte]|PDU[2-253bytes]|CRC[2bytes]'.
    /// </summary>
    public class ADU
    {
        public byte[] Buffer { get; private set; }
        public byte Address { get => Buffer[0]; private set => Buffer[0] = value; }
        public PDU PDU { get; private set; }
        public UInt16 CRC { get => Utils.GetWordFrmSmallEndArr(Buffer, Buffer.Length - 2); private set => Utils.SetWordToSmallEndArr(value, Buffer, Buffer.Length - 2); }

        #region CRC Api

        public UInt16 GenerateCRC()
        {
            UInt16 crc = 0xFFFF;
            bool isLsbOne = false;
            for (int b = 0; b < Buffer.Length - 2; b++)
            {
                crc ^= Buffer[b];
                for (int bit = 0; bit < 8; ++bit)
                {
                    isLsbOne = ((crc & 1) != 0);
                    crc >>= 1;
                    if (isLsbOne) { crc ^= 0xA001; }
                }
            }
            return crc;
        }

        private static readonly byte[] _crcHiLUT =
            {
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
                0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81,
                0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
                0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
                0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
                0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                0x40
            };
        private static readonly byte[] _crcLoLUT =
            {
                0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4,
                0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
                0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD,
                0x1D, 0x1C, 0xDC, 0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
                0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7,
                0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
                0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE,
                0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
                0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2,
                0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
                0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB,
                0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
                0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0, 0x50, 0x90, 0x91,
                0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
                0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88,
                0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
                0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80,
                0x40
            };
        public UInt16 GenerateCrcUsingLut()
        {
            byte hiCrc = 0xFF;
            byte loCrc = 0xFF;
            byte index = 0;
            for (int b = 0; b < Buffer.Length - 2; b++)
            {
                index = (byte)(loCrc ^ Buffer[b]);
                loCrc = (byte)(hiCrc ^ _crcHiLUT[index]);
                hiCrc = _crcLoLUT[index];
            }
            return Utils.MakeWord(hiCrc, loCrc);
        }

        #endregion

        /// <summary>
        /// Used for both 'packets that are re-created from network bytes' and 'creation of packet'
        /// </summary>
        /// <param name="buffer"></param>
        private ADU(byte[] buffer) => Buffer = buffer;

        private delegate string CreatePduDelegate(SharedSubArray<byte> buffer, out PDU pdu);
        private static string CreateRequest(byte deviceAddress, int pduSize, CreatePduDelegate createPdu, out ADU adu)
        {
            try
            {
                var buffer = new byte[sizeof(byte) + pduSize + sizeof(UInt16)];
                //Create the PDU (PDU Starts after first byte in the adu buffer followed by crc)
                var pduSharedBuffer = new SharedSubArray<byte>(buffer, 1, pduSize);
                var ec = createPdu(pduSharedBuffer, out var pdu);
                if (!ec.IsNullOrEmpty()) { adu = null; return ec; }
                //Create the ADU
                adu = new ADU(buffer) { Address = deviceAddress, PDU = pdu, CRC = 0xFFFF };
                adu.CRC = adu.GenerateCrcUsingLut();
                return string.Empty;
            }
            catch (Exception ex) { adu = null; return ex.Message; }
        }

        #region Factory Commands to create packet from user request

        #region ReadHoldingRegisters

        public static string CreateReadHoldingRegistersRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfRegisters, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.ReadHoldingRegisterRequest.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.ReadHoldingRegisterRequest.Create(x, startAddress, numberOfRegisters, out y), out adu);

        #endregion

        #region WriteSingleRegister

        public static string CreateWriteSingleRegisterRequest(byte deviceAddress, UInt16 registerAddress, UInt16 registerValue, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.WriteSingleRegisterRequestResponse.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.WriteSingleRegisterRequestResponse.Create(x, registerAddress, registerValue, out y), out adu);

        #endregion

        #region Diagnostics

        public static string CreateDiagnosticsRequest(byte deviceAddress, PDU.DiagnosticsRequestResponse.SubFunctionCode subFunction, UInt16 subData, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.DiagnosticsRequestResponse.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.DiagnosticsRequestResponse.Create(x, subFunction, subData, out y), out adu);

        #endregion

        #region WriteMultipleRegisters

        public static string CreateWriteMultipleRegistersRequest(byte deviceAddress, UInt16 startAddress, UInt16[] registers, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.WriteMultipleRegistersRequest.GetSize(registers.Length), (SharedSubArray<byte> x, out PDU y) => PDU.WriteMultipleRegistersRequest.Create(x, startAddress, registers, out y), out adu);

        #endregion

        #region ReadCoils

        public static string CreateReadCoilsRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfCoils, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.ReadCoilsRequest.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.ReadCoilsRequest.Create(x, startAddress, numberOfCoils, out y), out adu);

        #endregion

        #region ReadDiscreteInputs

        public static string CreateReadDiscreteInputsRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfInputs, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.ReadDiscreteInputsRequest.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.ReadDiscreteInputsRequest.Create(x, startAddress, numberOfInputs, out y), out adu);

        #endregion

        #region WriteSingleCoil

        public static string CreateWriteSingleCoilRequest(byte deviceAddress, UInt16 outputAddress, bool outputValue, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.WriteSingleCoilRequestResponse.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.WriteSingleCoilRequestResponse.Create(x, outputAddress, outputValue, out y), out adu);

        #endregion

        #region WriteMultipleCoils

        public static string CreateWriteMultipleCoilRequest(byte deviceAddress, UInt16 startAddress, bool[] coils, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.WriteMultipleCoilsRequest.GetSize(coils.Length), (SharedSubArray<byte> x, out PDU y) => PDU.WriteMultipleCoilsRequest.Create(x, startAddress, coils, out y), out adu);

        #endregion

        #region ReadInputRegisters

        public static string CreateReadInputRegistersRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfRegisters, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.ReadInputRegistersRequest.GetSize(), (SharedSubArray<byte> x, out PDU y) => PDU.ReadInputRegistersRequest.Create(x, startAddress, numberOfRegisters, out y), out adu);

        #endregion

        #region WriteReadMultipleRegisters

        public static string CreateWriteReadMultipleRegistersRequest(byte deviceAddress, UInt16 readStartAddress, UInt16 numberOfReadRegisters, UInt16 writeStartAddress, UInt16[] registers, out ADU adu) =>
            CreateRequest(deviceAddress, PDU.WriteReadMultipleRegistersRequest.GetSize(registers.Length), (SharedSubArray<byte> x, out PDU y) => PDU.WriteReadMultipleRegistersRequest.Create(x, readStartAddress, numberOfReadRegisters, writeStartAddress, registers, out y), out adu);

        #endregion

        #endregion

        #region Factory Command to create packet from byte array
        public static string CreateFromByteArray(byte[] arr, CommMode commMode, out ADU adu)
        {
            try
            {
                //Create the packet from the received array
                UInt16 crc;
                adu = new ADU(arr);
                //Verify if the CRC is valid
                if (adu.CRC != (crc = adu.GenerateCrcUsingLut())) { adu = null; return $"Received CRC['{adu.CRC}'] does not match calculated CRC['{crc}']"; }
                //Create PDU from byte array (start after the first byte[stationAddress] and the last two bytes[crc]
                string ec = PDU.CreateFromByteArray(new SharedSubArray<byte>(arr, 1, arr.Length - 2 - 1), commMode, out var pdu);
                if (!ec.IsNullOrEmpty()) { adu = null; return ec; }
                adu.PDU = pdu;
                return string.Empty;
            }
            catch (Exception ex) { adu = null; return ex.Message; }
        }

        #endregion
    }


    public class Client
    {
        private SerialPort _serial;
        private Thread _thread;
        private readonly object _lock = new object();
        private const int _timeoutReadWrite = 500;
        private volatile bool _runThread = true;
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly int _dataBits;
        private readonly StopBits _stopBits;

        public Client(uint portNumber, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _serial = new SerialPort();
            _portName = $"COM{portNumber}";
            _baudRate = baudRate;
            _parity = parity;
            _dataBits = dataBits;
            _stopBits = stopBits;
            (_thread = new Thread(() => { while (_runThread) { try { lock (_lock) { Poll(); } } catch { } Thread.Sleep(100); } }) { IsBackground = true, }).Start();
        }

        /// <summary>
        /// Currently this function contains nothing but can be expanded to send Diagnostic/Ping commands to keep server from 
        /// disconnecting the client in cases where they are expected to send some data at regular intervals
        /// </summary>
        private void Poll()
        {
            if (!_connectRequested) { return; }
        }

        private string RunSafeFunc(Func<string> runFunc)
        {
            try { lock (_lock) { return runFunc(); } }
            catch (Exception ex) { return ex.Message; }
        }

        public string Connect() => RunSafeFunc(PerformConnect);
        private bool _connectRequested = false;
        private string PerformConnect()
        {
            _connectRequested = true;
            try { _serial.Close(); } catch { }
            try
            {
                (_serial = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits) { ReadTimeout = _timeoutReadWrite, WriteTimeout = _timeoutReadWrite }).Open();
                _serial.DiscardInBuffer();
                _serial.DiscardOutBuffer();
            }
            catch (Exception ex) { return ex.Message; }
            return string.Empty;
        }

        public string Disconnect() => RunSafeFunc(PerformDisconnect);
        private string PerformDisconnect()
        {
            _connectRequested = false;
            try { _serial.Close(); Thread.Sleep(10); return string.Empty; }
            catch (Exception ex) { return ex.Message; }
        }

        private string SendCommandAndGetResponse(ADU aduCmd, out ADU aduRsp)
        {
            try
            {
                //Clear the stream.
                Flush();
                //Write the data
                _serial.Write(aduCmd.Buffer, 0, aduCmd.Buffer.Length);
                //Read the data
                var ec = ReadBytes(out var arrRsp);
                if (!ec.IsNullOrEmpty()) { aduRsp = null; return ec; }
                //Create the aduResponse packet from bytearray
                ec = ADU.CreateFromByteArray(arrRsp, CommMode.Client, out var adu);
                if(!ec.IsNullOrEmpty()) { aduRsp = null; return ec; }

                aduRsp = adu;
                return string.Empty;
            }
            catch (Exception ex) { aduRsp = null; return ex.Message; }
        }

        private const int _maxSize = 1 + 253 + 2;//addr+MaxPdu+crc
        private string ReadBytes(out byte[] arr, int timeOut = _timeoutReadWrite)
        {
            try
            {
                _serial.ReadTimeout = _timeoutReadWrite;
                var firstByte = _serial.ReadByte();
                if (-1 == firstByte) { arr = null; return string.Empty; }
                var remainingBytes = _serial.BytesToRead;
                byte[] arrTmp = new byte[remainingBytes + 1];
                arrTmp[0] = (byte)firstByte;
                _serial.Read(arrTmp, 1, arrTmp.Length - 1);
                arr = arrTmp;
                return string.Empty;
            }
            catch (Exception ex) { arr = null; return ex.Message; }
        }

        private void Flush() { if (ReadBytes(out var arr, 0).IsNullOrEmpty()) { System.Diagnostics.Trace.WriteLine($"Bytes Flushed= '{BitConverter.ToString(arr)}'"); }  }

        private delegate string CreateAduDelegate(out ADU adu);
        private string SendRequestAndGetResponse(CreateAduDelegate createAdu, out PDU pdu)
        {
            try
            {
                lock (_lock)
                {
                    //Create the ADU-RTU packet
                    var ec = createAdu(out var aduReq);
                    if (!ec.IsNullOrEmpty()) { pdu = null; return ec; }

                    //Send the command and get response
                    ec = SendCommandAndGetResponse(aduReq, out var aduRsp);
                    if (!ec.IsNullOrEmpty()) { pdu = null; return ec; }

                    pdu = aduRsp.PDU;
                    return string.Empty;
                }
            }
            catch (Exception ex) { pdu = null; return ex.Message; }
        }

        #region Requests to Server

        public string SendReadHoldingRegistersRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfRegisters, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateReadHoldingRegistersRequest(deviceAddress, startAddress, numberOfRegisters, out a), out pdu);

        public string SendWriteSingleRegisterRequest(byte deviceAddress, UInt16 registerAddress, UInt16 registerValue, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateWriteSingleRegisterRequest(deviceAddress, registerAddress, registerValue, out a), out pdu);

        public string SendDiagnosticsRequest(byte deviceAddress, PDU.DiagnosticsRequestResponse.SubFunctionCode subFunction, UInt16 subData, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateDiagnosticsRequest(deviceAddress, subFunction, subData, out a), out pdu);

        public string SendWriteMultipleRegistersRequest(byte deviceAddress, UInt16 startAddress, UInt16[] registers, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateWriteMultipleRegistersRequest(deviceAddress, startAddress, registers, out a), out pdu);

        public string SendReadCoilsRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfCoils, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateReadCoilsRequest(deviceAddress, startAddress, numberOfCoils, out a), out pdu);

        public string SendReadDiscreteInputsRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfInputs, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateReadDiscreteInputsRequest(deviceAddress, startAddress, numberOfInputs, out a), out pdu);

        public string SendWriteSingleCoilRequest(byte deviceAddress, UInt16 outputAddress, bool outputValue, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateWriteSingleCoilRequest(deviceAddress, outputAddress, outputValue, out a), out pdu);

        public string SendWriteMultipleCoilsRequest(byte deviceAddress, UInt16 startAddress, bool[] coils, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateWriteMultipleCoilRequest(deviceAddress, startAddress, coils, out a), out pdu);

        public string SendReadInputRegistersRequest(byte deviceAddress, UInt16 startAddress, UInt16 numberOfRegisters, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateReadInputRegistersRequest(deviceAddress, startAddress, numberOfRegisters, out a), out pdu);

        public string SendWriteReadMultipleRegistersRequest(byte deviceAddress, UInt16 readStartAddress, UInt16 numberOfReadRegisters, UInt16 writeStartAddress, UInt16[] registers, out PDU pdu) =>
            SendRequestAndGetResponse((out ADU a) => ADU.CreateWriteReadMultipleRegistersRequest(deviceAddress, readStartAddress, numberOfReadRegisters, writeStartAddress, registers, out a), out pdu);

        #endregion
    }
}

