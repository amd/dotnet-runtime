// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if TARGET_AMD64 || TARGET_ARM64 || (TARGET_32BIT && !TARGET_ARM)
#define HAS_CUSTOM_BLOCKS
#endif

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime.CompilerServices;

namespace System
{
    public static partial class Buffer
    {
        // Copies from one primitive array to another primitive array without
        // respecting types.  This calls memmove internally.  The count and
        // offset parameters here are in bytes.  If you want to use traditional
        // array element indices and counts, use Array.Copy.
        public static unsafe void BlockCopy(Array src, int srcOffset, Array dst, int dstOffset, int count)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            nuint uSrcLen = src.NativeLength;
            if (src.GetType() != typeof(byte[]))
            {
                if (!src.GetCorElementTypeOfElementType().IsPrimitiveType())
                    throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(src));
                uSrcLen *= (nuint)src.GetElementSize();
            }

            nuint uDstLen = uSrcLen;
            if (src != dst)
            {
                uDstLen = dst.NativeLength;
                if (dst.GetType() != typeof(byte[]))
                {
                    if (!dst.GetCorElementTypeOfElementType().IsPrimitiveType())
                        throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(dst));
                    uDstLen *= (nuint)dst.GetElementSize();
                }
            }

            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset), SR.ArgumentOutOfRange_MustBeNonNegInt32);
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(dstOffset), SR.ArgumentOutOfRange_MustBeNonNegInt32);
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), SR.ArgumentOutOfRange_MustBeNonNegInt32);

            nuint uCount = (nuint)count;
            nuint uSrcOffset = (nuint)srcOffset;
            nuint uDstOffset = (nuint)dstOffset;

            if ((uSrcLen < uSrcOffset + uCount) || (uDstLen < uDstOffset + uCount))
                throw new ArgumentException(SR.Argument_InvalidOffLen);

            Memmove(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(dst), uDstOffset), ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(src), uSrcOffset), uCount);
        }

        public static int ByteLength(Array array)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!array.GetCorElementTypeOfElementType().IsPrimitiveType())
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            nuint byteLength = array.NativeLength * (nuint)array.GetElementSize();

            // This API is explosed both as Buffer.ByteLength and also used indirectly in argument
            // checks for Buffer.GetByte/SetByte.
            //
            // If somebody called Get/SetByte on 2GB+ arrays, there is a decent chance that
            // the computation of the index has overflowed. Thus we intentionally always
            // throw on 2GB+ arrays in Get/SetByte argument checks (even for indicies <2GB)
            // to prevent people from running into a trap silently.

            return checked((int)byteLength);
        }

        public static byte GetByte(Array array, int index)
        {
            // array argument validation done via ByteLength
            if ((uint)index >= (uint)ByteLength(array))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            return Unsafe.Add<byte>(ref MemoryMarshal.GetArrayDataReference(array), index);
        }

        public static void SetByte(Array array, int index, byte value)
        {
            // array argument validation done via ByteLength
            if ((uint)index >= (uint)ByteLength(array))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
            }

            Unsafe.Add<byte>(ref MemoryMarshal.GetArrayDataReference(array), index) = value;
        }

        internal static unsafe void ZeroMemory(byte* dest, nuint len)
        {
            SpanHelpers.ClearWithoutReferences(ref *dest, len);
        }

        // The attributes on this method are chosen for best JIT performance.
        // Please do not edit unless intentional.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sourceBytesToCopy);
            }

            Memmove(ref *(byte*)destination, ref *(byte*)source, checked((nuint)sourceBytesToCopy));
        }

        // The attributes on this method are chosen for best JIT performance.
        // Please do not edit unless intentional.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, ulong destinationSizeInBytes, ulong sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sourceBytesToCopy);
            }

            Memmove(ref *(byte*)destination, ref *(byte*)source, checked((nuint)sourceBytesToCopy));
        }

        internal static void Memmove(ref byte dest, ref byte src, nuint len)
        {
            // P/Invoke into the native version when the buffers are overlapping.
            if (((nuint)(nint)Unsafe.ByteOffset(ref src, ref dest) < len) || ((nuint)(nint)Unsafe.ByteOffset(ref dest, ref src) < len))
            {
                goto BuffersOverlap;
            }

            nint vectorSize = Vector<byte>.Count;
            nint i = 0;

            if ((nint)len < vectorSize)
            {
                goto RemainingBytes;
            }

            if (len > MemmoveNativeThreshold)
            {
                goto PInvoke;
            }

            nint vectorMoveBound = (nint)len - vectorSize;
            for (; i < vectorMoveBound; i += vectorSize)
            {
                Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref dest, i)) = Unsafe.As<byte, Vector<byte>>(ref Unsafe.Add(ref src, i));
            }

        RemainingBytes:
            for (; i < (nint)len; i++)
            {
                Unsafe.Add(ref dest, i) = Unsafe.Add(ref src, i);
            }

            return;

        BuffersOverlap:
            // If the buffers overlap perfectly, there's no point to copying the data.
            if (Unsafe.AreSame(ref dest, ref src))
            {
                return;
            }

        PInvoke:
            _Memmove(ref dest, ref src, len);
        }

        // Non-inlinable wrapper around the QCall that avoids polluting the fast path
        // with P/Invoke prolog/epilog.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe void _Memmove(ref byte dest, ref byte src, nuint len)
        {
            fixed (byte* pDest = &dest)
            fixed (byte* pSrc = &src)
                __Memmove(pDest, pSrc, len);
        }

#if HAS_CUSTOM_BLOCKS
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct Block16 { }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct Block64 { }

        [StructLayout(LayoutKind.Sequential, Size = 128)]
        private struct Block128 { }
#endif // HAS_CUSTOM_BLOCKS
    }
}
