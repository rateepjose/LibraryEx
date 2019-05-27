using LibraryEx.DataManipulations;
using System;
using System.Linq;

namespace LibraryEx.SharedData
{
    public class SharedSubArray<T>
    {
        public T[] Array { get; private set; }
        public int Offset { get; private set; }
        /// <summary> Length of the subarray </summary>
        public int Length { get; private set; }
        public T this[int index] { get => Array[Offset + index]; set => Array[Offset + index] = value; }

        public SharedSubArray(T[] buffer, int offset) { Array = buffer; Offset = offset; Length = Array.Length - Offset; }
        public SharedSubArray(T[] buffer, int offset, int count) { Array = buffer; Offset = offset; Length = count; if (Length > (Array.Length - Offset)) { throw (new Exception("Array count set is greater than its length")); } }
        public SharedSubArray(SharedSubArray<T> subBuffer, int offset) : this(subBuffer.Array, subBuffer.Offset + offset, subBuffer.Length - offset) { }
        public SharedSubArray(SharedSubArray<T> subBuffer, int offset, int count) : this(subBuffer.Array, subBuffer.Offset + offset, subBuffer.Length + count) { }

        public T[] ToArray() => Array.TakeWhile((_, y) => (y >= Offset && y < Offset + Length)).ToArray();
    }

    public class SharedBitArray
    {
        public uint MaxBits => (uint)(Source.Length * 8);
        public SharedSubArray<byte> Source { get; private set; }
        public SharedBitArray(SharedSubArray<byte> source) { Source = source; }
        public SharedBitArray(byte[] source) : this(new SharedSubArray<byte>(source, 0)) { }
        public bool this[int index] { get => (Source[index / 8] & (0x01 << index % 8)) != 0; set => Source[index / 8] = (byte)(value ? (Source[index / 8] | (0x01 << (index % 8))) : (Source[index / 8] & (byte)(~(0x01 << (index % 8))))); }
        /// <summary> Calls ToArray() internally. This property need to be used sparingly since each call generates a new array. Value on this array does not reflect to the actual bit array. Use for read only/debugging. </summary>
        public bool[] ArrayReadOnly => ToArray();
        private bool[] ToArray()
        {
            var arr = new bool[MaxBits];
            for (int i = 0; i < arr.Length; ++i) { arr[i] = this[i]; }
            return arr;
        }
    }

    public class SharedRegisterArray
    {
        public uint Length => (uint)(Source.Length / 2);
        public SharedSubArray<byte> Source { get; private set; }
        public SharedRegisterArray(SharedSubArray<byte> source) => Source = source;
        public SharedRegisterArray(byte[] source) : this(new SharedSubArray<byte>(source, 0)) { }
        public UInt16 this[int index] { get => Utils.GetWordFrmBigEndArr(Source, index * 2); set => Utils.SetWordToBigEndArr(value, Source, index * 2); }
        public UInt16[] ArrayReadOnly => ToArray();
        private UInt16[] ToArray()
        {
            var arr = new UInt16[Length];
            for (int i = 0; i < arr.Length; ++i) { arr[i] = this[i]; }
            return arr;
        }
    }

}
