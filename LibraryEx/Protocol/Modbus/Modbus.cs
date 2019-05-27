using LibraryEx.DataManipulations;
using LibraryEx.SharedData;
using System;

namespace LibraryEx.Modbus
{
    public enum CommMode : uint { Client, Server }

    /// <summary>
    /// PDU packet consist of bytes in the following order 'FunctionCode[1byte]|Data[1-252bytes]'
    /// </summary>
    public abstract class PDU
    {
        [Flags]
        public enum FunctionCode : byte
        {
            /// <summary> None or not defined - Not in modbus standard; used to indicate nothing was set in FunctionCode </summary>
            None = 0x00,
            /// <summary> Read Internal Bits or Physical coils </summary>
            /// <remarks> Bit access. Range from 1 to 2000 </remarks>
            ReadCoils = 0x01,
            /// <summary> Read Physical Discrete Inputs </summary>
            /// <remarks> bit access </remarks>
            ReadDiscreteInputs = 0x02,
            /// <summary> Read Holding Registers ['Internal Registers' or 'Physical Output Registers'] </summary>
            /// <remarks> 16 bits access </remarks>
            ReadHoldingRegisters = 0x03,
            /// <summary> Read Physical Input Registers </summary>
            /// <remarks> 16 bits access </remarks>
            ReadInputRegisters = 0x04,
            /// <summary> Write single 'Internal Bits' or 'Physical coils' </summary>
            /// <remarks> bit access </remarks>
            WriteSingleCoil = 0x05,
            /// <summary> Write Single Register ['Internal Registers' or 'Physical Output Registers'] </summary>
            /// <remarks> 16 bits access </remarks>
            WriteSingleRegister = 0x06,
            /// <summary> Diagnostics </summary>
            /// <remarks> 16 bits access </remarks>
            Diagnostics = 0x08,

            /// <summary> Write multiple 'Internal Bits' or 'Physical coils' </summary>
            /// <remarks> bit access </remarks>
            WriteMultipleCoils = 0x0F,


            /// <summary> Write Multiple Registers ['Internal Registers' or 'Physical Output Registers'] </summary>
            /// <remarks> 16 bits access </remarks>
            WriteMultipleRegs = 0x10,

            /// <summary> Read File record [File record access] </summary>
            ReadFileRecord = 0x14,
            /// <summary> Write File record [File record access] </summary>
            WriteFileRecord = 0x15,
            /// <summary> Mask Write Register ['Internal Registers' or 'Physical Output Registers'] </summary>
            /// <remarks> 16 bits access </remarks>
            MaskWriteRegister = 0x16,
            /// <summary> Read/Write Multiple Registers ['Internal Registers' or 'Physical Output Registers'] </summary>
            /// <remarks> 16 bits access. Write operation takes place first followed by read </remarks>
            WriteReadMultipleRegisters = 0x17,
            /// <summary> Read FIFO queue ['Internal Registers' or 'Physical Output Registers'] </summary>
            /// <remarks> 16 bits access </remarks>
            ReadFifoQueue = 0x18,

            /// <summary> Error Response - Used for slave/server as response to a request message to indicate exception response </summary>
            ErrorResponse = 0x80,
        }

        public FunctionCode FC { get => GetFunctionCode(_buffer); set => _buffer[0] = (byte)value; }
        public SharedSubArray<byte> Data { get; private set; }
        private readonly SharedSubArray<byte> _buffer;
        protected PDU(SharedSubArray<byte> buffer) { _buffer = buffer; Data = new SharedSubArray<byte>(_buffer, 1); }

        public static Obj GetPduAs<Obj>(PDU pdu) where Obj : PDU => (Obj)pdu;
        public static string VerifyPduAs<Obj>(PDU pdu, FunctionCode fc, out Obj castedPdu) where Obj : PDU
        {
            try
            {
                if (fc != pdu.FC) { castedPdu = null; return $"Received FC['{pdu.FC}'] does not match expected['{fc}']"; }
                castedPdu = GetPduAs<Obj>(pdu);
                return string.Empty;
            }
            catch (Exception ex) { castedPdu = null; return ex.Message; }
        }
        public static FunctionCode GetFunctionCode(SharedSubArray<byte> buffer) => (FunctionCode)buffer[0];

        #region Factory Commands to create packet from user request

        #region ReadCoils

        public class ReadCoilsRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + StartAddress + NumberOfCoils = 5
            public static int GetSize() => _size;
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfCoils { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private ReadCoilsRequest(SharedSubArray<byte> buffer) : base(buffer) { }
            private ReadCoilsRequest(SharedSubArray<byte> zeroAllocedBuffer, UInt16 startAddress, UInt16 numberOfCoils) : this(zeroAllocedBuffer) { FC = FunctionCode.ReadCoils; StartAddress = startAddress; NumberOfCoils = numberOfCoils; }

            /// <summary>
            /// Factory function to create PDU REQUEST for ReadCoils
            /// </summary>
            /// <param name="zeroAllocatedBuffer"></param>
            /// <param name="startAddress"></param>
            /// <param name="numberOfCoils"></param>
            /// <param name="pdu"></param>
            /// <returns></returns>
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfCoils, out PDU pdu)
            {
                try { pdu = new ReadCoilsRequest(zeroAllocatedBuffer, startAddress, numberOfCoils); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            /// <summary>
            /// Factory function to create PDU REQUEST for ReadCoils from received/existing valid ByteArray
            /// </summary>
            /// <param name="dataBuffer"></param>
            /// <param name="pdu"></param>
            /// <returns></returns>
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadCoilsRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class ReadCoilsResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(byte);//FC + ByteCount = 2+
            private static int GetNumberOfBytes(int numberOfCoils) => (numberOfCoils / 8) + ((numberOfCoils % 8) != 0 ? 1 : 0);
            public static int GetSize(int numberOfCoils) => _size + GetNumberOfBytes(numberOfCoils);
            public byte ByteCount { get => Data[0]; private set => Data[0] = value; }
            public SharedBitArray Coils { get; private set; }
            private ReadCoilsResponse(SharedSubArray<byte> buffer) : base(buffer) { Coils = new SharedBitArray(new SharedSubArray<byte>(Data, 1)); }
            private ReadCoilsResponse(SharedSubArray<byte> zeroAllocatedBuffer, bool[] coils) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.ReadCoils;
                ByteCount = (byte)GetNumberOfBytes(coils.Length);
                for (int i = 0; i < coils.Length; ++i) { Coils[i] = coils[i]; }
            }

            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadCoilsResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, bool[] outputs, out PDU pdu)
            {
                try { pdu = new ReadCoilsResponse(zeroAllocatedBuffer, outputs); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region ExceptionResponse

        public class ExceptionResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(ExceptionCode);//FC + ExceptionCode = 2
            public static int GetSize() => _size;

            public enum ExceptionCode : byte
            {
                IllegalFunctionCode = 0x01,
                IllegalRegisterAddress = 0x02,
                IllegalDataValue = 0x03,
                ServerFailure = 0x04,
                Acknowledge = 0x05,
                ServerBusy = 0x06,
                GatewayProblem_NotAvailable = 0x0A,
                GatewayProblem_TargetedDeviceFailed = 0x0B,
            }

            public ExceptionCode EC { get => (ExceptionCode)Data[0]; private set => Data[0] = (byte)value; }

            private ExceptionResponse(SharedSubArray<byte> buffer) : base(buffer) { }
            private ExceptionResponse(SharedSubArray<byte> zeroAllocatedBuffer, FunctionCode fc, ExceptionCode ec) : this(zeroAllocatedBuffer) { FC = FunctionCode.ErrorResponse | fc; EC = ec; }

            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ExceptionResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }

        }

        #endregion

        #region ReadDiscreteInputs

        public class ReadDiscreteInputsRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + StartAddress + NumberOfInputs = 5
            public static int GetSize() => _size;
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfInputs { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private ReadDiscreteInputsRequest(SharedSubArray<byte> buffer) : base(buffer) { }
            private ReadDiscreteInputsRequest(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfInputs) : this(zeroAllocatedBuffer) { FC = FunctionCode.ReadDiscreteInputs; StartAddress = startAddress; NumberOfInputs = numberOfInputs; }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfInputs, out PDU pdu)
            {
                try { pdu = new ReadDiscreteInputsRequest(zeroAllocatedBuffer, startAddress, numberOfInputs); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadDiscreteInputsRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class ReadDiscreteInputsResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(byte);//FC + ByteCount = 2+
            private static int GetNumberOfBytes(int numberOfInputs) => (numberOfInputs / 8) + ((numberOfInputs % 8) != 0 ? 1 : 0);
            public static int GetSize(int numberOfInputs) => _size + GetNumberOfBytes(numberOfInputs);
            public byte ByteCount { get => Data[0]; private set => Data[0] = value; }
            public SharedBitArray Inputs { get; private set; }
            private ReadDiscreteInputsResponse(SharedSubArray<byte> buffer) : base(buffer) => Inputs = new SharedBitArray(new SharedSubArray<byte>(Data, 1));
            private ReadDiscreteInputsResponse(SharedSubArray<byte> zeroAllocatedBuffer, bool[] inputs) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.ReadDiscreteInputs;
                ByteCount = (byte)GetNumberOfBytes(inputs.Length);
                for (int i = 0; i < inputs.Length; ++i) { Inputs[i] = inputs[i]; }
            }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, bool[] inputs, out PDU pdu)
            {
                try { pdu = new ReadDiscreteInputsResponse(zeroAllocatedBuffer, inputs); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadDiscreteInputsResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region ReadHoldingRegisters

        public class ReadHoldingRegisterRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + StartAddress + NumberOfRegisters = 5
            public static int GetSize() => _size;
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfRegisters { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private ReadHoldingRegisterRequest(SharedSubArray<byte> buffer) : base(buffer) { }
            private ReadHoldingRegisterRequest(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfRegisters) : this(zeroAllocatedBuffer) { FC = FunctionCode.ReadHoldingRegisters; StartAddress = startAddress; NumberOfRegisters = numberOfRegisters; }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfRegisters, out PDU pdu)
            {
                try { pdu = new ReadHoldingRegisterRequest(zeroAllocatedBuffer, startAddress, numberOfRegisters); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadHoldingRegisterRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class ReadHoldingRegisterResponse : PDU
        {
            private const int _baseSize = sizeof(FunctionCode) + sizeof(byte);//FC + NumberOfDataBytes = 2+
            private static int GetRegisterBytes(int numberOfRegisters) => 2 * numberOfRegisters;
            public static int GetSize(int numberOfRegisters) => _baseSize + GetRegisterBytes(numberOfRegisters);
            public byte ByteCount { get => Data[0]; private set => Data[0] = value; }
            public SharedRegisterArray HoldingRegisters { get; private set; }
            private ReadHoldingRegisterResponse(SharedSubArray<byte> buffer) : base(buffer) => HoldingRegisters = new SharedRegisterArray(new SharedSubArray<byte>(Data, 1));
            private ReadHoldingRegisterResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16[] inputs) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.ReadHoldingRegisters;
                ByteCount = (byte)GetRegisterBytes(inputs.Length);
                for (int i = 0; i < inputs.Length; ++i) { HoldingRegisters[i] = inputs[i]; }
            }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16[] inputs, out PDU pdu)
            {
                try { pdu = new ReadHoldingRegisterResponse(zeroAllocatedBuffer, inputs); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadHoldingRegisterResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region WriteSingleRegister

        /// <summary> Both the Request and response for this PDU is the exact same </summary>
        public class WriteSingleRegisterRequestResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + RegisterAddress + RegisterValue = 5
            public static int GetSize() => _size;
            public UInt16 RegisterAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 RegisterValue { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private WriteSingleRegisterRequestResponse(SharedSubArray<byte> buffer) : base(buffer) {}
            private WriteSingleRegisterRequestResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 registerAddress, UInt16 registerValue) : this(zeroAllocatedBuffer) { FC = FunctionCode.WriteSingleRegister; RegisterAddress = registerAddress; RegisterValue = registerValue; }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteSingleRegisterRequestResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 registerAddress, UInt16 registerValue, out PDU pdu)
            {
                try { pdu = new WriteSingleRegisterRequestResponse(zeroAllocatedBuffer, registerAddress, registerValue); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region Diagnostics

        /// <summary> Both the Request and response for this PDU is the exact same </summary>
        public class DiagnosticsRequestResponse : PDU
        {
            public enum SubFunctionCode : UInt16
            {
                ReturnQueryData = 0,
                RestartCommunicationsOption,
                ReturnDiagnosticRegister,
                ChangeAsciiInputDelimiter,
                ForceListenModeOnly,
                //05 to 09 RESERVED
                ClearCountersAndDiagnosticRegister = 10,
                ReturnBusMessageCount,
                ReturnBusCommunicationErrorCount,
                ReturnBusExceptionErrorCount,
                ReturnServerMesageCount,
                ReturnServerNoResponseCount,
                ReturnServerNakCount,
                ReturnServerBusyCount,
                ReturnBusCharacterOverrunCount,
                //19 : Reserved
                ClearOverrunCounterAndFlag = 20,
                //21...65535 Reserved
            }

            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + SubFunction + SubData = 5
            public static int GetSize() => _size;
            public SubFunctionCode SubFunction { get => (SubFunctionCode)Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr((UInt16)value, Data, 0); }
            public UInt16 SubData { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }

            private DiagnosticsRequestResponse(SharedSubArray<byte> buffer) : base(buffer) { }
            private DiagnosticsRequestResponse(SharedSubArray<byte> zeroAllocatedBuffer, SubFunctionCode subFunction, UInt16 subData) : this(zeroAllocatedBuffer) { FC = FunctionCode.Diagnostics; SubFunction = subFunction; SubData = subData; }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new DiagnosticsRequestResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, SubFunctionCode subFunction, UInt16 subData, out PDU pdu)
            {
                try { pdu = new DiagnosticsRequestResponse(zeroAllocatedBuffer, subFunction, subData); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region WriteMultipleRegisters

        public class WriteMultipleRegistersRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16) + sizeof(byte);//FC + StartAddress + NumberOfRegisters + ByteCount= 6+
            private static int GetRegisterBytes(int numberOfRegisters) => 2 * numberOfRegisters;
            public static int GetSize(int numberOfRegisters) => _size + GetRegisterBytes(numberOfRegisters);
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfRegisters { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            public byte ByteCount { get => Data[4]; private set => Data[4] = value; }
            public SharedRegisterArray Registers { get; private set; }
            private WriteMultipleRegistersRequest(SharedSubArray<byte> buffer) : base(buffer) => Registers = new SharedRegisterArray(new SharedSubArray<byte>(Data, 5));
            private WriteMultipleRegistersRequest(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16[] registers) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.WriteMultipleRegs;
                StartAddress = startAddress;
                NumberOfRegisters = (UInt16)registers.Length;
                ByteCount = (byte)GetRegisterBytes(registers.Length);
                for (int i = 0; i < Registers.Length; ++i) { Registers[i] = registers[i]; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteMultipleRegistersRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16[] registers, out PDU pdu)
            {
                try { pdu = new WriteMultipleRegistersRequest(zeroAllocatedBuffer, startAddress, registers); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class WriteMultipleRegistersResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + StartAddress + NumberOfRegisters = 5
            public static int GetSize() => _size;
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfRegisters { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private WriteMultipleRegistersResponse(SharedSubArray<byte> buffer) : base(buffer) { }
            private WriteMultipleRegistersResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfRegisters) : this(zeroAllocatedBuffer) { FC = FunctionCode.WriteMultipleRegs; StartAddress = startAddress; NumberOfRegisters = numberOfRegisters; }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteMultipleRegistersResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfRegisters, out PDU pdu)
            {
                try { pdu = new WriteMultipleRegistersResponse(zeroAllocatedBuffer, startAddress, numberOfRegisters); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region WriteSingleCoil

        public class WriteSingleCoilRequestResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + OutputAddress + OutputValue= 5
            public static int GetSize() => _size;
            public UInt16 OutputAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public bool OutputValue { get => Utils.GetHighByteFromWord(Data[2]) == 0xFF; private set => Data[2] = (byte)(value ? 0XFF : 0x00); }
            private WriteSingleCoilRequestResponse(SharedSubArray<byte> buffer) : base(buffer) { }
            private WriteSingleCoilRequestResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 outputAddress, bool outputValue) : this(zeroAllocatedBuffer) { FC = FunctionCode.WriteSingleCoil; OutputAddress = outputAddress; OutputValue = outputValue; }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteSingleCoilRequestResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 outputAddress, bool outputValue, out PDU pdu)
            {
                try { pdu = new WriteSingleCoilRequestResponse(zeroAllocatedBuffer, outputAddress, outputValue); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region WriteMultipleCoils

        public class WriteMultipleCoilsRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16) + sizeof(byte);//FC + StartAddress + NumberOfCoils + ByteCount= 6+
            private static int GetNumberOfBytes(int numberOfCoils) => (numberOfCoils / 8) + ((numberOfCoils % 8) != 0 ? 1 : 0);
            public static int GetSize(int numberOfCoils) => _size + GetNumberOfBytes(numberOfCoils);
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfCoils { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            public byte ByteCount { get => Data[4]; private set => Data[4] = value; }
            public SharedBitArray Coils { get; private set; }
            private WriteMultipleCoilsRequest(SharedSubArray<byte> dataBuffer) : base(dataBuffer) { Coils = new SharedBitArray(new SharedSubArray<byte>(Data, 5)); }
            private WriteMultipleCoilsRequest(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, bool[] coils) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.WriteMultipleCoils;
                StartAddress = startAddress;
                NumberOfCoils = (UInt16)coils.Length;
                ByteCount = (byte)GetNumberOfBytes(coils.Length);
                for (int i = 0; i < coils.Length; ++i) { Coils[i] = coils[i]; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteMultipleCoilsRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, bool[] coils, out PDU pdu)
            {
                try { pdu = new WriteMultipleCoilsRequest(zeroAllocatedBuffer, startAddress, coils); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class WriteMultipleCoilsResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + StartAddress + NumberOfCoils= 5
            public static int GetSize() => _size;
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfCoils { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private WriteMultipleCoilsResponse(SharedSubArray<byte> buffer) : base(buffer) { }
            private WriteMultipleCoilsResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfCoils) : this(zeroAllocatedBuffer) { FC = FunctionCode.WriteMultipleCoils; StartAddress = startAddress; NumberOfCoils = numberOfCoils; }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteMultipleCoilsResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfCoils, out PDU pdu)
            {
                try { pdu = new WriteMultipleCoilsResponse(zeroAllocatedBuffer, startAddress, numberOfCoils); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region ReadInputRegisters

        public class ReadInputRegistersRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16);//FC + StartAddress + NumberOfRegisters = 5
            public static int GetSize() => _size;
            public UInt16 StartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfRegisters { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            private ReadInputRegistersRequest(SharedSubArray<byte> buffer) : base(buffer) { }
            private ReadInputRegistersRequest(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfRegisters) : this(zeroAllocatedBuffer) { FC = FunctionCode.ReadInputRegisters; StartAddress = startAddress; NumberOfRegisters = numberOfRegisters; }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 startAddress, UInt16 numberOfRegisters, out PDU pdu)
            {
                try { pdu = new ReadInputRegistersRequest(zeroAllocatedBuffer, startAddress, numberOfRegisters); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadInputRegistersRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class ReadInputRegistersResponse : PDU
        {
            private const int _baseSize = sizeof(FunctionCode) + sizeof(byte);//FC + NumberOfDataBytes = 2+
            private static int GetRegisterBytes(int numberOfRegisters) => 2 * numberOfRegisters;
            public static int GetSize(int numberOfRegisters) => _baseSize + GetRegisterBytes(numberOfRegisters);
            public byte ByteCount { get => Data[0]; private set => Data[0] = value; }
            public SharedRegisterArray InputRegisters { get; private set; }
            private ReadInputRegistersResponse(SharedSubArray<byte> buffer) : base(buffer) => InputRegisters = new SharedRegisterArray(new SharedSubArray<byte>(Data, 1));
            private ReadInputRegistersResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16[] inputs) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.ReadInputRegisters;
                ByteCount = (byte)GetRegisterBytes(inputs.Length);
                for (int i = 0; i < inputs.Length; ++i) { InputRegisters[i] = inputs[i]; }
            }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16[] inputs, out PDU pdu)
            {
                try { pdu = new ReadInputRegistersResponse(zeroAllocatedBuffer, inputs); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new ReadInputRegistersResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #region WriteReadMultipleRegisters

        public class WriteReadMultipleRegistersRequest : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(UInt16) + sizeof(UInt16) + sizeof(UInt16) + sizeof(UInt16) + sizeof(byte);//FC + ReadStartAddress + NumberOfReadRegisters + WriteStartAddress + NumberOfWriteRegisters + WriteByteCount= 10+
            private static int GetWriteRegisterSize(int numberOfWriteRegisters) => 2 * numberOfWriteRegisters;
            public static int GetSize(int numberOfWriteRegisters) => _size + GetWriteRegisterSize(numberOfWriteRegisters);
            public UInt16 ReadStartAddress { get => Utils.GetWordFrmBigEndArr(Data, 0); private set => Utils.SetWordToBigEndArr(value, Data, 0); }
            public UInt16 NumberOfReadRegisters { get => Utils.GetWordFrmBigEndArr(Data, 2); private set => Utils.SetWordToBigEndArr(value, Data, 2); }
            public UInt16 WriteStartAddress { get => Utils.GetWordFrmBigEndArr(Data, 4); private set => Utils.SetWordToBigEndArr(value, Data, 4); }
            public UInt16 NumberOfWriteRegisters { get => Utils.GetWordFrmBigEndArr(Data, 6); private set => Utils.SetWordToBigEndArr(value, Data, 6); }
            public byte WriteByteCount { get => Data[8]; private set => Data[8] = value; }
            public SharedRegisterArray WriteRegisters { get; private set; }
            private WriteReadMultipleRegistersRequest(SharedSubArray<byte> buffer) : base(buffer) => WriteRegisters = new SharedRegisterArray(new SharedSubArray<byte>(Data, 9));
            private WriteReadMultipleRegistersRequest(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 readStartAddress, UInt16 numberOfReadRegisters, UInt16 writeStartAddress, UInt16[] registers) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.WriteReadMultipleRegisters;
                ReadStartAddress = readStartAddress;
                NumberOfReadRegisters = numberOfReadRegisters;
                WriteStartAddress = writeStartAddress;
                NumberOfWriteRegisters = (UInt16)registers.Length;
                WriteByteCount = (byte)GetWriteRegisterSize(registers.Length);
                for (int i = 0; i < registers.Length; ++i) { WriteRegisters[i] = registers[i]; }
            }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16 readStartAddress, UInt16 numberOfReadRegisters, UInt16 writeStartAddress, UInt16[] registers, out PDU pdu)
            {
                try { pdu = new WriteReadMultipleRegistersRequest(zeroAllocatedBuffer, readStartAddress, numberOfReadRegisters, writeStartAddress, registers); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteReadMultipleRegistersRequest(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        public class WriteReadMultipleRegistersResponse : PDU
        {
            private const int _size = sizeof(FunctionCode) + sizeof(byte);//FC + ReadByteCount= 2+
            private static int GetReadRegisterSize(int numberOfReadRegisters) => 2 * numberOfReadRegisters;
            public static int GetSize(int numberOfReadRegisters) => _size + GetReadRegisterSize(numberOfReadRegisters);
            public byte ReadByteCount { get => Data[0]; private set => Data[0] = value; }
            public SharedRegisterArray ReadRegisters { get; private set; }
            private WriteReadMultipleRegistersResponse(SharedSubArray<byte> buffer) : base(buffer) => ReadRegisters = new SharedRegisterArray(new SharedSubArray<byte>(Data, 1));
            private WriteReadMultipleRegistersResponse(SharedSubArray<byte> zeroAllocatedBuffer, UInt16[] registers) : this(zeroAllocatedBuffer)
            {
                FC = FunctionCode.WriteReadMultipleRegisters;
                ReadByteCount = (byte)GetReadRegisterSize(registers.Length);
                for (int i = 0; i < registers.Length; ++i) { ReadRegisters[i] = registers[i]; }
            }

            public static string Create(SharedSubArray<byte> zeroAllocatedBuffer, UInt16[] registers, out PDU pdu)
            {
                try { pdu = new WriteReadMultipleRegistersResponse(zeroAllocatedBuffer, registers); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
            public static string Create(SharedSubArray<byte> dataBuffer, out PDU pdu)
            {
                try { pdu = new WriteReadMultipleRegistersResponse(dataBuffer); return string.Empty; }
                catch (Exception ex) { pdu = null; return ex.Message; }
            }
        }

        #endregion

        #endregion

        #region Factory Delegate to create from byte array
        public static string CreateFromByteArray(SharedSubArray<byte> buffer, CommMode mode, out PDU pdu)
        {
            try
            {
                string ec = "Error";
                switch (PDU.GetFunctionCode(buffer))
                {
                    case FunctionCode.ReadCoils: { ec = (mode == CommMode.Server) ? PDU.ReadCoilsRequest.Create(buffer, out pdu) : PDU.ReadCoilsResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.ReadDiscreteInputs: { ec = (mode == CommMode.Server) ? PDU.ReadDiscreteInputsRequest.Create(buffer, out pdu) : PDU.ReadDiscreteInputsResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.ReadHoldingRegisters: { ec = (mode == CommMode.Server) ? PDU.ReadHoldingRegisterRequest.Create(buffer, out pdu) : PDU.ReadHoldingRegisterResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.ReadInputRegisters: { ec = (mode == CommMode.Server) ? PDU.ReadInputRegistersRequest.Create(buffer, out pdu) : PDU.ReadInputRegistersResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.WriteSingleCoil: { ec = PDU.WriteSingleCoilRequestResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.WriteSingleRegister: { ec = PDU.WriteSingleRegisterRequestResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.Diagnostics: { ec = PDU.DiagnosticsRequestResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.WriteMultipleCoils: { ec = (mode == CommMode.Server) ? PDU.WriteMultipleCoilsRequest.Create(buffer, out pdu) : PDU.WriteMultipleCoilsResponse.Create(buffer, out pdu); } break;
                    case FunctionCode.WriteMultipleRegs: { ec = (mode == CommMode.Server) ? PDU.WriteMultipleRegistersRequest.Create(buffer, out pdu) : PDU.WriteMultipleRegistersResponse.Create(buffer, out pdu); } break;
                    //case FunctionCode.ReadFileRecord:
                    //    break;
                    //case FunctionCode.WriteFileRecord:
                    //    break;
                    //case FunctionCode.MaskWriteRegister:
                    //    break;
                    case FunctionCode.WriteReadMultipleRegisters: { ec = (mode == CommMode.Server) ? PDU.WriteReadMultipleRegistersRequest.Create(buffer, out pdu) : PDU.WriteReadMultipleRegistersResponse.Create(buffer, out pdu); } break;
                    //case FunctionCode.ReadFifoQueue:
                    //    break;
                    //case FunctionCode.ErrorResponse:
                    //    break;
                    case var errFc when errFc.IsError(): { ec = PDU.ExceptionResponse.Create(buffer, out pdu); } break;
                    default: { pdu = null; ec = "Invalid PDU"; } break;
                }
                if (!ec.IsNullOrEmpty()) { pdu = null; return ec; }
                return string.Empty;
            }
            catch (Exception ex) { pdu = null; return ex.Message; }
        }
        #endregion
    }

    public static partial class Extensions
    {
        public static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);
        public static bool IsError(this PDU.FunctionCode fc) => (fc & PDU.FunctionCode.ErrorResponse) == PDU.FunctionCode.ErrorResponse;
    }

}
