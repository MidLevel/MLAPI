﻿using System;
using System.Collections.Generic;
using System.IO;

namespace MLAPI.Serialization
{
    /// <summary>
    /// Static class containing PooledBitStreams
    /// </summary>
    public static class BitStreamPool
    {
        private static readonly Queue<PooledBitStream> streams = new Queue<PooledBitStream>();

        /// <summary>
        /// Retrieves an expandable PooledBitStream from the pool
        /// </summary>
        /// <returns>An expandable PooledBitStream</returns>
        public static PooledBitStream GetStream()
        {
            if (streams.Count == 0) return new PooledBitStream();

            PooledBitStream stream = streams.Dequeue();
            stream.SetLength(0);
            stream.Position = 0;

            return stream;
        }

        /// <summary>
        /// Puts a PooledBitStream back into the pool
        /// </summary>
        /// <param name="stream">The stream to put in the pool</param>
        public static void PutBackInPool(PooledBitStream stream)
        {
            streams.Enqueue(stream);
        }
    }

    /// <summary>
    /// Static class containing PooledBitWriters
    /// </summary>
    public static class BitWriterPool
    {
        private static readonly Queue<PooledBitWriter> writers = new Queue<PooledBitWriter>();

        /// <summary>
        /// Retrieves a PooledBitWriter
        /// </summary>
        /// <param name="stream">The stream the writer should write to</param>
        /// <returns>A PooledBitWriter</returns>
        public static PooledBitWriter GetWriter(Stream stream)
        {
            if (writers.Count == 0) return new PooledBitWriter(stream);

            PooledBitWriter writer = writers.Dequeue();
            writer.SetStream(stream);

            return writer;
        }

        /// <summary>
        /// Puts a PooledBitWriter back into the pool
        /// </summary>
        /// <param name="stream">The writer to put in the pool</param>
        public static void PutBackInPool(PooledBitWriter writer)
        {
            writers.Enqueue(writer);
        }
    }

    /// <summary>
    /// Static class containing PooledBitReaders
    /// </summary>
    public static class BitReaderPool
    {
        private static readonly Queue<PooledBitReader> readers = new Queue<PooledBitReader>();

        /// <summary>
        /// Retrieves a PooledBitReader
        /// </summary>
        /// <param name="stream">The stream the reader should read from</param>
        /// <returns>A PooledBitReader</returns>
        public static PooledBitReader GetReader(Stream stream)
        {
            if (readers.Count == 0) return new PooledBitReader(stream);

            PooledBitReader reader = readers.Dequeue();
            reader.SetStream(stream);

            return reader;
        }

        /// <summary>
        /// Puts a PooledBitReader back into the pool
        /// </summary>
        /// <param name="stream">The reader to put in the pool</param>
        public static void PutBackInPool(PooledBitReader reader)
        {
            readers.Enqueue(reader);
        }
    }

    /// <summary>
    /// Disposable BitStream that returns the Stream to the BitStreamPool when disposed
    /// </summary>
    public sealed class PooledBitStream : BitStream, IDisposable
    {
        /// <summary>
        /// Gets a PooledBitStream from the static BitStreamPool
        /// </summary>
        /// <returns>PooledBitStream</returns>
        public static PooledBitStream Get()
        {
            return BitStreamPool.GetStream();
        }

        /// <summary>
        /// Returns the PooledBitStream into the static BitStreamPool
        /// </summary>
        public new void Dispose()
        {
            BitStreamPool.PutBackInPool(this);
        }
    }

    /// <summary>
    /// Disposable BitWriter that returns the Writer to the BitWriterPool when disposed
    /// </summary>
    public sealed class PooledBitWriter : BitWriter, IDisposable
    {
        public PooledBitWriter(Stream stream) : base(stream)
        {

        }

        /// <summary>
        /// Gets a PooledBitWriter from the static BitWriterPool
        /// </summary>
        /// <returns>PooledBitWriter</returns>
        public static PooledBitWriter Get(Stream stream)
        {
            return BitWriterPool.GetWriter(stream);
        }

        /// <summary>
        /// Returns the PooledBitWriter into the static BitWriterPool
        /// </summary>
        public void Dispose()
        {
            BitWriterPool.PutBackInPool(this);
        }
    }

    /// <summary>
    /// Disposable BitReader that returns the Reader to the BitReaderPool when disposed
    /// </summary>
    public sealed class PooledBitReader : BitReader, IDisposable
    {
        public PooledBitReader(Stream stream) : base(stream)
        {
        }

        /// <summary>
        /// Gets a PooledBitReader from the static BitReaderPool
        /// </summary>
        /// <returns>PooledBitReader</returns>
        public static PooledBitReader Get(Stream stream)
        {
            return BitReaderPool.GetReader(stream);
        }

        /// <summary>
        /// Returns the PooledBitReader into the static BitReaderPool
        /// </summary>
        public void Dispose()
        {
            BitReaderPool.PutBackInPool(this);
        }
    }
}
