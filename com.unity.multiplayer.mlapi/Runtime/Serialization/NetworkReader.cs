#define ARRAY_WRITE_PERMISSIVE // Allow attempt to write "packed" byte array (calls WriteByteArray())
#define ARRAY_RESOLVE_IMPLICIT // Include WriteArray() method with automatic type resolution
#define ARRAY_WRITE_PREMAP // Create a prefixed array diff mapping
#define ARRAY_DIFF_ALLOW_RESIZE // Whether or not to permit writing diffs of differently sized arrays

using System;
using System.IO;
using System.Text;
using MLAPI.Reflection;
using MLAPI.Logging;
using MLAPI.Spawning;
using UnityEngine;

namespace MLAPI.Serialization
{
    /// <summary>
    /// A BinaryReader that can do bit wise manipulation when backed by a NetworkBuffer
    /// </summary>
    public class NetworkReader
    {
        private Stream m_Source;
        private NetworkBuffer m_NetworkSource;

        /// <summary>
        /// Creates a new NetworkReader backed by a given stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        public NetworkReader(Stream stream)
        {
            m_Source = stream;
            m_NetworkSource = stream as NetworkBuffer;
        }

        /// <summary>
        /// Changes the underlying stream the reader is reading from
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        public void SetStream(Stream stream)
        {
            m_Source = stream;
            m_NetworkSource = stream as NetworkBuffer;
        }

        /// <summary>
        /// Retrieves the underlying stream the reader is reading from
        /// </summary>
        /// <returns></returns>
        public Stream GetStream()
        {
            return m_Source;
        }

        /// <summary>
        /// Reads a single byte
        /// </summary>
        /// <returns>The byte read as an integer</returns>
        public int ReadByte() => m_Source.ReadByte();

        /// <summary>
        /// Reads a byte
        /// </summary>
        /// <returns>The byte read</returns>
        public byte ReadByteDirect() => (byte)m_Source.ReadByte();

        /// <summary>
        /// Reads a single bit
        /// </summary>
        /// <returns>The bit read</returns>
        public bool ReadBit()
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            return m_NetworkSource.ReadBit();
        }

        /// <summary>
        /// Reads a single bit
        /// </summary>
        /// <returns>The bit read</returns>
        public bool ReadBool()
        {
            if (m_NetworkSource == null)
            {
                return m_Source.ReadByte() != 0;
            }

            // return ReadBit(); // old (buggy)
            return ReadByte() != 0; // new (hotfix)
        }

        /// <summary>
        /// Skips pad bits and aligns the position to the next byte
        /// </summary>
        public void SkipPadBits()
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            while (!m_NetworkSource.BitAligned)
            {
                ReadBit();
            }
        }

        /// <summary>
        /// Reads a single boxed object of a given type in a packed format
        /// </summary>
        /// <param name="type">The type to read</param>
        /// <returns>Returns the boxed read object</returns>
        public object ReadObjectPacked(Type type)
        {
            if (type.IsNullable())
            {
                bool isNull = ReadBool();

                if (isNull)
                {
                    return null;
                }
            }

            if (SerializationManager.TryDeserialize(m_Source, type, out object obj))
            {
                return obj;
            }

            if (type.IsArray && type.HasElementType)
            {
                int size = ReadInt32Packed();

                var array = Array.CreateInstance(type.GetElementType(), size);

                for (int i = 0; i < size; i++)
                {
                    array.SetValue(ReadObjectPacked(type.GetElementType()), i);
                }

                return array;
            }

            if (type == typeof(byte))
            {
                return ReadByteDirect();
            }

            if (type == typeof(sbyte))
            {
                return ReadSByte();
            }

            if (type == typeof(ushort))
            {
                return ReadUInt16Packed();
            }

            if (type == typeof(short))
            {
                return ReadInt16Packed();
            }

            if (type == typeof(int))
            {
                return ReadInt32Packed();
            }

            if (type == typeof(uint))
            {
                return ReadUInt32Packed();
            }

            if (type == typeof(long))
            {
                return ReadInt64Packed();
            }

            if (type == typeof(ulong))
            {
                return ReadUInt64Packed();
            }

            if (type == typeof(float))
            {
                return ReadSinglePacked();
            }

            if (type == typeof(double))
            {
                return ReadDoublePacked();
            }

            if (type == typeof(string))
            {
                return ReadStringPacked();
            }

            if (type == typeof(bool))
            {
                return ReadBool();
            }

            if (type == typeof(Vector2))
            {
                return ReadVector2Packed();
            }

            if (type == typeof(Vector3))
            {
                return ReadVector3Packed();
            }

            if (type == typeof(Vector4))
            {
                return ReadVector4Packed();
            }

            if (type == typeof(Color))
            {
                return ReadColorPacked();
            }

            if (type == typeof(Color32))
            {
                return ReadColor32();
            }

            if (type == typeof(Ray))
            {
                return ReadRayPacked();
            }

            if (type == typeof(Quaternion))
            {
                return ReadRotationPacked();
            }

            if (type == typeof(char))
            {
                return ReadCharPacked();
            }

            if (type.IsEnum)
            {
                return ReadInt32Packed();
            }

            if (type == typeof(GameObject))
            {
                ulong networkObjectId = ReadUInt64Packed();

                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
                {
                    return networkObject.gameObject;
                }

                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkReader)} cannot find the {nameof(GameObject)} sent in the {nameof(NetworkSpawnManager.SpawnedObjects)} list, it may have been destroyed. {nameof(networkObjectId)}: {networkObjectId}");
                }

                return null;
            }

            if (type == typeof(NetworkObject))
            {
                ulong networkObjectId = ReadUInt64Packed();

                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
                {
                    return networkObject;
                }

                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkReader)} cannot find the {nameof(NetworkObject)} sent in the {nameof(NetworkSpawnManager.SpawnedObjects)} list, it may have been destroyed. {nameof(networkObjectId)}: {networkObjectId}");
                }

                return null;
            }

            if (typeof(NetworkBehaviour).IsAssignableFrom(type))
            {
                ulong networkObjectId = ReadUInt64Packed();
                ushort behaviourId = ReadUInt16Packed();
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
                {
                    return networkObject.GetNetworkBehaviourAtOrderIndex(behaviourId);
                }

                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkReader)} cannot find the {nameof(NetworkBehaviour)} sent in the {nameof(NetworkSpawnManager.SpawnedObjects)} list, it may have been destroyed. {nameof(networkObjectId)}: {networkObjectId}");
                }

                return null;
            }

            if (typeof(INetworkSerializable).IsAssignableFrom(type))
            {
                object instance = Activator.CreateInstance(type);
                ((INetworkSerializable)instance).NetworkSerialize(new NetworkSerializer(this));
                return instance;
            }

            Type nullableUnderlyingType = Nullable.GetUnderlyingType(type);

            if (nullableUnderlyingType != null && SerializationManager.IsTypeSupported(nullableUnderlyingType))
            {
                return ReadObjectPacked(nullableUnderlyingType);
            }

            throw new ArgumentException($"{nameof(NetworkReader)} cannot read type {type.Name}");
        }

        /// <summary>
        /// Read a single-precision floating point value from the stream.
        /// </summary>
        /// <returns>The read value</returns>
        public float ReadSingle() => new UIntFloat { UIntValue = ReadUInt32() }.FloatValue;

        /// <summary>
        /// Read a double-precision floating point value from the stream.
        /// </summary>
        /// <returns>The read value</returns>
        public double ReadDouble() => new UIntFloat { ULongValue = ReadUInt64() }.DoubleValue;

        /// <summary>
        /// Read a single-precision floating point value from the stream from a varint
        /// </summary>
        /// <returns>The read value</returns>
        public float ReadSinglePacked() => new UIntFloat { UIntValue = ReadUInt32Packed() }.FloatValue;

        /// <summary>
        /// Read a double-precision floating point value from the stream as a varint
        /// </summary>
        /// <returns>The read value</returns>
        public double ReadDoublePacked() => new UIntFloat { ULongValue = ReadUInt64Packed() }.DoubleValue;

        /// <summary>
        /// Read a Vector2 from the stream.
        /// </summary>
        /// <returns>The Vector2 read from the stream.</returns>
        public Vector2 ReadVector2() => new Vector2(ReadSingle(), ReadSingle());

        /// <summary>
        /// Read a Vector2 from the stream.
        /// </summary>
        /// <returns>The Vector2 read from the stream.</returns>
        public Vector2 ReadVector2Packed() => new Vector2(ReadSinglePacked(), ReadSinglePacked());

        /// <summary>
        /// Read a Vector3 from the stream.
        /// </summary>
        /// <returns>The Vector3 read from the stream.</returns>
        public Vector3 ReadVector3() => new Vector3(ReadSingle(), ReadSingle(), ReadSingle());

        /// <summary>
        /// Read a Vector3 from the stream.
        /// </summary>
        /// <returns>The Vector3 read from the stream.</returns>
        public Vector3 ReadVector3Packed() => new Vector3(ReadSinglePacked(), ReadSinglePacked(), ReadSinglePacked());

        /// <summary>
        /// Read a Vector4 from the stream.
        /// </summary>
        /// <returns>The Vector4 read from the stream.</returns>
        public Vector4 ReadVector4() => new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

        /// <summary>
        /// Read a Vector4 from the stream.
        /// </summary>
        /// <returns>The Vector4 read from the stream.</returns>
        public Vector4 ReadVector4Packed() => new Vector4(ReadSinglePacked(), ReadSinglePacked(), ReadSinglePacked(), ReadSinglePacked());

        /// <summary>
        /// Read a Color from the stream.
        /// </summary>
        /// <returns>The Color read from the stream.</returns>
        public Color ReadColor() => new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

        /// <summary>
        /// Read a Color from the stream.
        /// </summary>
        /// <returns>The Color read from the stream.</returns>
        public Color ReadColorPacked() => new Color(ReadSinglePacked(), ReadSinglePacked(), ReadSinglePacked(), ReadSinglePacked());

        /// <summary>
        /// Read a Color32 from the stream.
        /// </summary>
        /// <returns>The Color32 read from the stream.</returns>
        public Color32 ReadColor32() => new Color32((byte)ReadByte(), (byte)ReadByte(), (byte)ReadByte(), (byte)ReadByte());

        /// <summary>
        /// Read a Ray from the stream.
        /// </summary>
        /// <returns>The Ray read from the stream.</returns>
        public Ray ReadRay() => new Ray(ReadVector3(), ReadVector3());

        /// <summary>
        /// Read a Ray from the stream.
        /// </summary>
        /// <returns>The Ray read from the stream.</returns>
        public Ray ReadRayPacked() => new Ray(ReadVector3Packed(), ReadVector3Packed());

        /// <summary>
        /// Read a Ray2D from the stream.
        /// </summary>
        /// <returns>The Ray2D read from the stream.</returns>
        public Ray2D ReadRay2D() => new Ray2D(ReadVector2(), ReadVector2());

        /// <summary>
        /// Read a Ray2D from the stream.
        /// </summary>
        /// <returns>The Ray2D read from the stream.</returns>
        public Ray2D ReadRay2DPacked() => new Ray2D(ReadVector2Packed(), ReadVector2Packed());

        /// <summary>
        /// Read a single-precision floating point value from the stream. The value is between (inclusive) the minValue and maxValue.
        /// </summary>
        /// <param name="minValue">Minimum value that this value could be</param>
        /// <param name="maxValue">Maximum possible value that this could be</param>
        /// <param name="bytes">How many bytes the compressed value occupies. Must be between 1 and 4 (inclusive)</param>
        /// <returns>The read value</returns>
        public float ReadRangedSingle(float minValue, float maxValue, int bytes)
        {
            if (bytes < 1 || bytes > 4)
            {
                throw new ArgumentOutOfRangeException("Result must occupy between 1 and 4 bytes!");
            }

            uint read = 0;
            for (int i = 0; i < bytes; ++i)
            {
                read |= (uint)ReadByte() << (i << 3);
            }

            return ((float)read / ((0x100 * bytes) - 1) * (maxValue - minValue)) + minValue;
        }

        /// <summary>
        /// read a double-precision floating point value from the stream. The value is between (inclusive) the minValue and maxValue.
        /// </summary>
        /// <param name="minValue">Minimum value that this value could be</param>
        /// <param name="maxValue">Maximum possible value that this could be</param>
        /// <param name="bytes">How many bytes the compressed value occupies. Must be between 1 and 8 (inclusive)</param>
        /// <returns>The read value</returns>
        public double ReadRangedDouble(double minValue, double maxValue, int bytes)
        {
            if (bytes < 1 || bytes > 8)
            {
                throw new ArgumentOutOfRangeException("Result must occupy between 1 and 8 bytes!");
            }

            ulong read = 0;
            for (int i = 0; i < bytes; ++i)
            {
                read |= (ulong)ReadByte() << (i << 3);
            }

            return ((double)read / ((0x100 * bytes) - 1) * (maxValue - minValue)) + minValue;
        }

        /// <summary>
        /// Reads the rotation from the stream
        /// </summary>
        /// <returns>The rotation read from the stream</returns>
        public Quaternion ReadRotationPacked()
        {
            float x = ReadSinglePacked();
            float y = ReadSinglePacked();
            float z = ReadSinglePacked();

            // numerical precision issues can make the remainder very slightly negative.
            // In this case, use 0 for w as, otherwise, w would be NaN.
            float remainder = 1f - Mathf.Pow(x, 2) - Mathf.Pow(y, 2) - Mathf.Pow(z, 2);
            float w = (remainder > 0f) ? Mathf.Sqrt(remainder) : 0.0f;

            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Reads the rotation from the stream
        /// </summary>
        /// <returns>The rotation read from the stream</returns>
        public Quaternion ReadRotation()
        {
            float x = ReadSingle();
            float y = ReadSingle();
            float z = ReadSingle();
            float w = ReadSingle();

            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Read a certain amount of bits from the stream.
        /// </summary>
        /// <param name="bitCount">How many bits to read. Minimum 0, maximum 8.</param>
        /// <returns>The bits that were read</returns>
        public ulong ReadBits(int bitCount)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (bitCount > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot read more than 64 bits into a 64-bit value!");
            }

            if (bitCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot read fewer than 0 bits!");
            }

            ulong read = 0;
            for (int i = 0; i + 8 < bitCount; i += 8)
            {
                read |= (ulong)ReadByte() << i;
            }

            read |= (ulong)ReadByteBits(bitCount & 7) << (bitCount & ~7);
            return read;
        }

        /// <summary>
        /// Read a certain amount of bits from the stream.
        /// </summary>
        /// <param name="bitCount">How many bits to read. Minimum 0, maximum 64.</param>
        /// <returns>The bits that were read</returns>
        public byte ReadByteBits(int bitCount)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (bitCount > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot read more than 8 bits into an 8-bit value!");
            }

            if (bitCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot read fewer than 0 bits!");
            }

            int result = 0;
            var convert = new ByteBool();
            for (int i = 0; i < bitCount; ++i)
            {
                result |= convert.Collapse(ReadBit()) << i;
            }

            return (byte)result;
        }

        /// <summary>
        /// Read a nibble (4 bits) from the stream.
        /// </summary>
        /// <param name="asUpper">Whether or not the nibble should be left-shifted by 4 bits</param>
        /// <returns>The nibble that was read</returns>
        public byte ReadNibble(bool asUpper)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            var convert = new ByteBool();

            byte result = (byte)
            (
                convert.Collapse(ReadBit()) |
                (convert.Collapse(ReadBit()) << 1) |
                (convert.Collapse(ReadBit()) << 2) |
                (convert.Collapse(ReadBit()) << 3)
            );
            if (asUpper)
            {
                result <<= 4;
            }

            return result;
        }

        // Marginally faster than the one that accepts a bool
        /// <summary>
        /// Read a nibble (4 bits) from the stream.
        /// </summary>
        /// <returns>The nibble that was read</returns>
        public byte ReadNibble()
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            var convert = new ByteBool();
            return (byte)
            (
                convert.Collapse(ReadBit()) |
                (convert.Collapse(ReadBit()) << 1) |
                (convert.Collapse(ReadBit()) << 2) |
                (convert.Collapse(ReadBit()) << 3)
            );
        }

        /// <summary>
        /// Reads a signed byte
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public sbyte ReadSByte() => (sbyte)ReadByte();

        /// <summary>
        /// Read an unsigned short (UInt16) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public ushort ReadUInt16() => (ushort)(ReadByte() | (ReadByte() << 8));

        /// <summary>
        /// Read a signed short (Int16) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public short ReadInt16() => (short)ReadUInt16();

        /// <summary>
        /// Read a single character from the stream
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public char ReadChar() => (char)ReadUInt16();

        /// <summary>
        /// Read an unsigned int (UInt32) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public uint ReadUInt32() => (uint)(ReadByte() | (ReadByte() << 8) | (ReadByte() << 16) | (ReadByte() << 24));

        /// <summary>
        /// Read a signed int (Int32) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public int ReadInt32() => (int)ReadUInt32();

        /// <summary>
        /// Read an unsigned long (UInt64) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public ulong ReadUInt64() =>
        (
            ((uint)ReadByte()) |
            ((ulong)ReadByte() << 8) |
            ((ulong)ReadByte() << 16) |
            ((ulong)ReadByte() << 24) |
            ((ulong)ReadByte() << 32) |
            ((ulong)ReadByte() << 40) |
            ((ulong)ReadByte() << 48) |
            ((ulong)ReadByte() << 56)
        );

        /// <summary>
        /// Read a signed long (Int64) from the stream.
        /// </summary>
        /// <returns>Value read from stream.</returns>
        public long ReadInt64() => (long)ReadUInt64();

        /// <summary>
        /// Read a ZigZag encoded varint signed short (Int16) from the stream.
        /// </summary>
        /// <returns>Decoded un-varinted value.</returns>
        public short ReadInt16Packed() => (short)Arithmetic.ZigZagDecode(ReadUInt64Packed());

        /// <summary>
        /// Read a varint unsigned short (UInt16) from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public ushort ReadUInt16Packed() => (ushort)ReadUInt64Packed();

        /// <summary>
        /// Read a varint two-byte character from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public char ReadCharPacked() => (char)ReadUInt16Packed();

        /// <summary>
        /// Read a ZigZag encoded varint signed int (Int32) from the stream.
        /// </summary>
        /// <returns>Decoded un-varinted value.</returns>
        public int ReadInt32Packed() => (int)Arithmetic.ZigZagDecode(ReadUInt64Packed());

        /// <summary>
        /// Read a varint unsigned int (UInt32) from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public uint ReadUInt32Packed() => (uint)ReadUInt64Packed();

        /// <summary>
        /// Read a ZigZag encoded varint signed long(Int64) from the stream.
        /// </summary>
        /// <returns>Decoded un-varinted value.</returns>
        public long ReadInt64Packed() => Arithmetic.ZigZagDecode(ReadUInt64Packed());

        /// <summary>
        /// Read a varint unsigned long (UInt64) from the stream.
        /// </summary>
        /// <returns>Un-varinted value.</returns>
        public ulong ReadUInt64Packed()
        {
            ulong header = ReadByteDirect();
            if (header <= 240)
            {
                return header;
            }

            if (header <= 248)
            {
                return 240 + ((header - 241) << 8) + ReadByteDirect();
            }

            if (header == 249)
            {
                return 2288UL + (ulong)(ReadByte() << 8) + ReadByteDirect();
            }

            ulong res = ReadByteDirect() | ((ulong)ReadByteDirect() << 8) | ((ulong)ReadByte() << 16);
            int cmp = 2;
            int hdr = (int)(header - 247);
            while (hdr > ++cmp)
            {
                res |= (ulong)ReadByte() << (cmp << 3);
            }

            return res;
        }

        // Read arrays

        /// <summary>
        /// Read a string from the stream.
        /// </summary>
        /// <returns>The string that was read.</returns>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadString(bool oneByteChars) => ReadString(null, oneByteChars);

        /// <summary>
        /// Read a string from the stream.
        /// </summary>
        /// <returns>The string that was read.</returns>
        /// <param name="builder">The builder to read the values into or null to use a new builder.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadString(StringBuilder builder = null, bool oneByteChars = false)
        {
            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null)
            {
                builder = new StringBuilder(expectedLength);
            }
            else if (builder.Capacity + builder.Length < expectedLength)
            {
                builder.Capacity = expectedLength + builder.Length;
            }

            for (int i = 0; i < expectedLength; ++i)
            {
                builder.Insert(i, oneByteChars ? (char)ReadByte() : ReadChar());
            }

            return builder;
        }

        /// <summary>
        /// Read string encoded as a varint from the stream.
        /// </summary>
        /// <returns>The string that was read.</returns>
        /// <param name="builder">The builder to read the string into or null to use a new builder</param>
        public string ReadStringPacked(StringBuilder builder = null)
        {
            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null)
            {
                builder = new StringBuilder(expectedLength);
            }
            else if (builder.Capacity + builder.Length < expectedLength)
            {
                builder.Capacity = expectedLength + builder.Length;
            }

            for (int i = 0; i < expectedLength; ++i)
            {
                builder.Insert(i, ReadCharPacked());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Read string diff from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compare">The version to compare the diff to.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadStringDiff(string compare, bool oneByteChars = false) => ReadStringDiff(null, compare, oneByteChars);

        /// <summary>
        /// Read string diff from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version</returns>
        /// <param name="builder">The builder to read the string into or null to use a new builder.</param>
        /// <param name="compare">The version to compare the diff to.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars are used and only ASCII is supported.</param>
        public StringBuilder ReadStringDiff(StringBuilder builder, string compare, bool oneByteChars = false)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null)
            {
                builder = new StringBuilder(expectedLength);
            }
            else if (builder.Capacity < expectedLength)
            {
                builder.Capacity = expectedLength;
            }

            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(compare == null ? 0 : Math.Min(expectedLength, compare.Length));
            ulong mapStart;
            int compareLength = compare?.Length ?? 0;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    builder.Insert(i, oneByteChars ? (char)ReadByte() : ReadChar());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < compareLength)
                {
                    builder.Insert(i, compare[i]);
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return builder;
        }

        /// <summary>
        /// Read string diff from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compareAndBuffer">The builder containing the current version and that will also be used as the output buffer.</param>
        /// <param name="oneByteChars">If set to <c>true</c> one byte chars will be used and only ASCII will be supported.</param>
        public StringBuilder ReadStringDiff(StringBuilder compareAndBuffer, bool oneByteChars = false)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            int expectedLength = (int)ReadUInt32Packed();
            if (compareAndBuffer == null)
            {
                throw new ArgumentNullException(nameof(compareAndBuffer), "Buffer cannot be null");
            }

            if (compareAndBuffer.Capacity < expectedLength)
            {
                compareAndBuffer.Capacity = expectedLength;
            }

            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)Math.Min(expectedLength, compareAndBuffer.Length);
            ulong mapStart;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareAndBuffer.Length || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    compareAndBuffer.Remove(i, 1);
                    compareAndBuffer.Insert(i, oneByteChars ? (char)ReadByte() : ReadChar());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return compareAndBuffer;
        }

        /// <summary>
        /// Read string diff encoded as varints from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compare">The version to compare the diff to.</param>
        public StringBuilder ReadStringPackedDiff(string compare) => ReadStringPackedDiff(null, compare);

        /// <summary>
        /// Read string diff encoded as varints from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version</returns>
        /// <param name="builder">The builder to read the string into or null to use a new builder.</param>
        /// <param name="compare">The version to compare the diff to.</param>
        public StringBuilder ReadStringPackedDiff(StringBuilder builder, string compare)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            int expectedLength = (int)ReadUInt32Packed();
            if (builder == null)
            {
                builder = new StringBuilder(expectedLength);
            }
            else if (builder.Capacity < expectedLength)
            {
                builder.Capacity = expectedLength;
            }

            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(compare == null ? 0 : Math.Min(expectedLength, compare.Length));
            ulong mapStart;
            int compareLength = compare?.Length ?? 0;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    builder.Insert(i, ReadCharPacked());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < compareLength)
                {
                    builder.Insert(i, compare[i]);
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return builder;
        }

        /// <summary>
        /// Read string diff encoded as varints from the stream.
        /// </summary>
        /// <returns>The string based on the diff and the old version.</returns>
        /// <param name="compareAndBuffer">The builder containing the current version and that will also be used as the output buffer.</param>
        public StringBuilder ReadStringPackedDiff(StringBuilder compareAndBuffer)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            int expectedLength = (int)ReadUInt32Packed();
            if (compareAndBuffer == null)
            {
                throw new ArgumentNullException(nameof(compareAndBuffer), "Buffer cannot be null");
            }

            if (compareAndBuffer.Capacity < expectedLength)
            {
                compareAndBuffer.Capacity = expectedLength;
            }

            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)Math.Min(expectedLength, compareAndBuffer.Length);
            ulong mapStart;
            for (int i = 0; i < expectedLength; ++i)
            {
                if (i >= compareAndBuffer.Length || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    compareAndBuffer.Remove(i, 1);
                    compareAndBuffer.Insert(i, ReadCharPacked());
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return compareAndBuffer;
        }

        /// <summary>
        /// Read byte array into an optional buffer from the stream.
        /// </summary>
        /// <returns>The byte array that has been read.</returns>
        /// <param name="readTo">The array to read into. If the array is not large enough or if it's null. A new array is created.</param>
        /// <param name="knownLength">The length of the array if it's known. Otherwise -1</param>
        public byte[] ReadByteArray(byte[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new byte[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadByteDirect();
            }

            return readTo;
        }

        /// <summary>
        /// CreateArraySegment
        /// Creates an array segment from the size and offset values passed in.
        /// If none are passed in, then it creates an array segment of the entire buffer.
        /// </summary>
        /// <param name="sizeToCopy">size to copy</param>
        /// <param name="offset">offset within the stream buffer to start copying</param>
        /// <returns>ArraySegment&lt;byte&gt;</returns>
        public ArraySegment<byte> CreateArraySegment(int sizeToCopy = -1, int offset = -1)
        {
            if (m_NetworkSource != null)
            {
                //If no offset was passed, used the current position
                bool noOffset = offset == -1;
                bool noSizeToCopy = sizeToCopy == -1;

                offset = noOffset ? (int)m_NetworkSource.Position : offset;
                sizeToCopy = noSizeToCopy && noOffset ? (int)m_NetworkSource.Length : sizeToCopy;
                if (sizeToCopy > 0)
                {
                    //Check to make sure we won't be copying beyond our bounds
                    if ((m_NetworkSource.Length - offset) >= sizeToCopy)
                    {
                        return new ArraySegment<byte>(m_NetworkSource.GetBuffer(), offset, sizeToCopy);
                    }

                    //If we didn't pass anything in or passed the length of the buffer
                    if (sizeToCopy == m_NetworkSource.Length)
                    {
                        offset = 0;
                    }
                    else
                    {
                        Debug.LogError($"{nameof(sizeToCopy)} ({sizeToCopy}) exceeds bounds with an {nameof(offset)} of ({offset})! <returning empty array segment>");
                        return new ArraySegment<byte>();
                    }

                    //Return the request array segment
                    return new ArraySegment<byte>(m_NetworkSource.GetBuffer(), offset, sizeToCopy);
                }

                Debug.LogError($"{nameof(sizeToCopy)} ({sizeToCopy}) is zero or less! <returning empty array segment>");
            }
            else
            {
                Debug.LogError("Reader has no stream assigned to it! <returning empty array segment>");
            }

            return new ArraySegment<byte>();
        }

        /// <summary>
        /// Read byte array diff into an optional buffer from the stream.
        /// </summary>
        /// <returns>The byte array created from the diff and original.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The length of the array if it's known. Otherwise -1</param>
        public byte[] ReadByteArrayDiff(byte[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            byte[] writeTo = readTo == null || readTo.LongLength != knownLength ? new byte[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadByteDirect();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read short array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArray(short[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new short[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadInt16();
            }

            return readTo;
        }

        /// <summary>
        /// Read short array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArrayPacked(short[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new short[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadInt16Packed();
            }

            return readTo;
        }

        /// <summary>
        /// Read short array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArrayDiff(short[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            short[] writeTo = readTo == null || readTo.LongLength != knownLength ? new short[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadInt16();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read short array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public short[] ReadShortArrayPackedDiff(short[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            short[] writeTo = readTo == null || readTo.LongLength != knownLength ? new short[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadInt16Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }

        /// <summary>
        /// Read ushort array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArray(ushort[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new ushort[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadUInt16();
            }

            return readTo;
        }

        /// <summary>
        /// Read ushort array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArrayPacked(ushort[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new ushort[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadUInt16Packed();
            }

            return readTo;
        }

        /// <summary>
        /// Read ushort array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArrayDiff(ushort[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            ushort[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ushort[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt16();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read ushort array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ushort[] ReadUShortArrayPackedDiff(ushort[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            ushort[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ushort[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt16Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }

        /// <summary>
        /// Read int array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArray(int[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new int[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadInt32();
            }

            return readTo;
        }

        /// <summary>
        /// Read int array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArrayPacked(int[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new int[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadInt32Packed();
            }

            return readTo;
        }

        /// <summary>
        /// Read int array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArrayDiff(int[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            int[] writeTo = readTo == null || readTo.LongLength != knownLength ? new int[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadInt32();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read int array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public int[] ReadIntArrayPackedDiff(int[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            int[] writeTo = readTo == null || readTo.LongLength != knownLength ? new int[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadInt32Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }

        /// <summary>
        /// Read uint array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public uint[] ReadUIntArray(uint[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new uint[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadUInt32();
            }

            return readTo;
        }

        /// <summary>
        /// Read uint array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public uint[] ReadUIntArrayPacked(uint[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new uint[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadUInt32Packed();
            }

            return readTo;
        }

        /// <summary>
        /// Read uint array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public uint[] ReadUIntArrayDiff(uint[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            uint[] writeTo = readTo == null || readTo.LongLength != knownLength ? new uint[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt32();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read long array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArray(long[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new long[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadInt64();
            }

            return readTo;
        }

        /// <summary>
        /// Read long array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArrayPacked(long[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new long[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadInt64Packed();
            }

            return readTo;
        }

        /// <summary>
        /// Read long array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArrayDiff(long[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            long[] writeTo = readTo == null || readTo.LongLength != knownLength ? new long[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadInt64();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read long array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public long[] ReadLongArrayPackedDiff(long[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            long[] writeTo = readTo == null || readTo.LongLength != knownLength ? new long[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadInt64Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }

        /// <summary>
        /// Read ulong array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArray(ulong[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new ulong[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadUInt64();
            }

            return readTo;
        }

        /// <summary>
        /// Read ulong array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArrayPacked(ulong[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new ulong[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadUInt64Packed();
            }

            return readTo;
        }

        /// <summary>
        /// Read ulong array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArrayDiff(ulong[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            ulong[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ulong[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt64();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read ulong array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public ulong[] ReadULongArrayPackedDiff(ulong[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            ulong[] writeTo = readTo == null || readTo.LongLength != knownLength ? new ulong[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadUInt64Packed();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }

        /// <summary>
        /// Read float array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArray(float[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new float[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadSingle();
            }

            return readTo;
        }

        /// <summary>
        /// Read float array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArrayPacked(float[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new float[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadSinglePacked();
            }

            return readTo;
        }

        /// <summary>
        /// Read float array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArrayDiff(float[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            float[] writeTo = readTo == null || readTo.LongLength != knownLength ? new float[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadSingle();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read float array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public float[] ReadFloatArrayPackedDiff(float[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            float[] writeTo = readTo == null || readTo.LongLength != knownLength ? new float[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    readTo[i] = ReadSinglePacked();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }

        /// <summary>
        /// Read double array from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArray(double[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new double[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadDouble();
            }

            return readTo;
        }

        /// <summary>
        /// Read double array in a packed format from the stream.
        /// </summary>
        /// <returns>The array read from the stream.</returns>
        /// <param name="readTo">The buffer to read into or null to create a new array</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArrayPacked(double[] readTo = null, long knownLength = -1)
        {
            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            if (readTo == null || readTo.LongLength != knownLength)
            {
                readTo = new double[knownLength];
            }

            for (long i = 0; i < knownLength; ++i)
            {
                readTo[i] = ReadDoublePacked();
            }

            return readTo;
        }

        /// <summary>
        /// Read double array diff from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArrayDiff(double[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            double[] writeTo = readTo == null || readTo.LongLength != knownLength ? new double[knownLength] : readTo;
            ulong dBlockStart = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong mapStart;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    mapStart = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = dBlockStart;
#endif
                    // Read datum
                    writeTo[i] = ReadDouble();
#if ARRAY_WRITE_PREMAP
                    dBlockStart = m_NetworkSource.BitPosition;
                    // Return to mapping section
                    m_NetworkSource.BitPosition = mapStart;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = dBlockStart;
            return writeTo;
        }

        /// <summary>
        /// Read double array diff in a packed format from the stream.
        /// </summary>
        /// <returns>The array created from the diff and the current version.</returns>
        /// <param name="readTo">The buffer containing the old version or null.</param>
        /// <param name="knownLength">The known length or -1 if unknown</param>
        public double[] ReadDoubleArrayPackedDiff(double[] readTo = null, long knownLength = -1)
        {
            if (m_NetworkSource == null)
            {
                throw new InvalidOperationException($"Cannot read bits on a non-{nameof(NetworkBuffer)} stream");
            }

            if (knownLength < 0)
            {
                knownLength = (long)ReadUInt64Packed();
            }

            double[] writeTo = readTo == null || readTo.LongLength != knownLength ? new double[knownLength] : readTo;
            ulong data = m_NetworkSource.BitPosition + (ulong)(readTo == null ? 0 : Math.Min(knownLength, readTo.LongLength));
            ulong rset;
            long readToLength = readTo?.LongLength ?? 0;
            for (long i = 0; i < knownLength; ++i)
            {
                if (i >= readToLength || ReadBit())
                {
#if ARRAY_WRITE_PREMAP
                    // Move to data section
                    rset = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = data;
#endif
                    // Read datum
                    writeTo[i] = ReadDoublePacked();
#if ARRAY_WRITE_PREMAP
                    // Return to mapping section
                    data = m_NetworkSource.BitPosition;
                    m_NetworkSource.BitPosition = rset;
#endif
                }
                else if (i < readTo.LongLength)
                {
                    writeTo[i] = readTo[i];
                }
            }

            m_NetworkSource.BitPosition = data;
            return writeTo;
        }
    }
}
