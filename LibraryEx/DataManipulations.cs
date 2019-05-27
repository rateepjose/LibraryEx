using LibraryEx.SharedData;
using System;
using System.Runtime.CompilerServices;

namespace LibraryEx.DataManipulations
{
    public static partial class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetHighByteFromWord(UInt16 word) => (byte)(word >> 8);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetLowByteFromWord(UInt16 word) => (byte)word;
        public static UInt16 MakeWord(byte high, byte low) => (UInt16)((high << 8) | low);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 GetWordFrmBigEndArr(byte[] arr, int offset) => MakeWord(arr[offset], arr[offset + 1]);
        public static void SetWordToBigEndArr(UInt16 value, byte[] arr, int offset) { arr[offset] = GetHighByteFromWord(value); arr[offset + 1] = GetLowByteFromWord(value); }
        public static UInt16 GetWordFrmBigEndArr(SharedSubArray<byte> subArr, int offset) => MakeWord(subArr[offset], subArr[offset + 1]);
        public static void SetWordToBigEndArr(UInt16 value, SharedSubArray<byte> subArr, int offset) { subArr[offset] = GetHighByteFromWord(value); subArr[offset + 1] = GetLowByteFromWord(value); }
        public static UInt16 GetWordFrmSmallEndArr(byte[] arr, int offset) => MakeWord(arr[offset + 1], arr[offset]);
        public static void SetWordToSmallEndArr(UInt16 value, byte[] arr, int offset) { arr[offset + 1] = GetHighByteFromWord(value); arr[offset] = GetLowByteFromWord(value); }
    }
}
