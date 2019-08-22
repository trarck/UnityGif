using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Gif.Decode
{
    public class LZWDecoder
    {
        private const int StackSize = 4096;
        private const int NullCode = -1;

        private Stream m_Stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="LZWDecoder"/> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="stream">The stream. where to read from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null
        /// (Nothing in Visual Basic).</exception>
        public LZWDecoder(Stream stream)
        {
            m_Stream = stream;
        }

        /// <summary>
        /// Decodes and uncompresses all pixel indices from the stream.
        /// </summary>
        /// <param name="width">The width of the pixel index array.</param>
        /// <param name="height">The height of the pixel index array.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <returns>The decoded and uncompressed array.</returns>
        public byte[] DecodePixels(int width, int height, int dataSize)
        {
            byte[] pixels = new byte[width * height];
            int clearCode = 1 << dataSize;
            if (dataSize == Int32.MaxValue)
            {
                throw new ArgumentOutOfRangeException("dataSize", "Must be less than Int32.MaxValue");
            }
            int codeSize = dataSize + 1;
            int endCode = clearCode + 1;
            int availableCode = clearCode + 2;
            #region Jillzhangs Code (Not From Me) see: http://giflib.codeplex.com/
            int code = NullCode;
            int old_code = NullCode;
            int code_mask = (1 << codeSize) - 1;
            int bits = 0;
            int[] prefix = new int[StackSize];
            int[] suffix = new int[StackSize];
            int[] pixelStatck = new int[StackSize + 1];
            int top = 0;
            int count = 0;
            int bi = 0;
            int xyz = 0;
            int data = 0;
            int first = 0;
            int inCode = NullCode;
            for (code = 0; code < clearCode; code++)
            {
                prefix[code] = 0;
                suffix[code] = (byte)code;
            }

            byte[] buffer = null;
            while (xyz < pixels.Length)
            {
                if (top == 0)
                {
                    if (bits < codeSize)
                    {
                        if (count == 0)
                        {
                            buffer = ReadBlock();
                            count = buffer.Length;
                            if (count == 0)
                            {
                                break;
                            }
                            bi = 0;
                        }
                        data += buffer[bi] << bits;
                        bits += 8;
                        bi++;
                        count--;
                        continue;
                    }
                    code = data & code_mask;
                    data >>= codeSize;
                    bits -= codeSize;
                    if (code > availableCode || code == endCode)
                    {
                        break;
                    }
                    if (code == clearCode)
                    {
                        codeSize = dataSize + 1;
                        code_mask = (1 << codeSize) - 1;
                        availableCode = clearCode + 2;
                        old_code = NullCode;
                        continue;
                    }
                    if (old_code == NullCode)
                    {
                        pixelStatck[top++] = suffix[code];
                        old_code = code;
                        first = code;
                        continue;
                    }
                    inCode = code;
                    if (code == availableCode)
                    {
                        pixelStatck[top++] = (byte)first;
                        code = old_code;
                    }
                    while (code > clearCode)
                    {
                        pixelStatck[top++] = suffix[code];
                        code = prefix[code];
                    }
                    first = suffix[code];
                    if (availableCode > StackSize)
                    {
                        break;
                    }
                    pixelStatck[top++] = suffix[code];
                    prefix[availableCode] = old_code;
                    suffix[availableCode] = first;
                    availableCode++;
                    if (availableCode == code_mask + 1 && availableCode < StackSize)
                    {
                        codeSize++;
                        code_mask = (1 << codeSize) - 1;
                    }
                    old_code = inCode;
                }
                top--;
                pixels[xyz++] = (byte)pixelStatck[top];
            }

            #endregion

            return pixels;
        }

        private byte[] ReadBlock()
        {
            int blockSize = m_Stream.ReadByte();
            return ReadBytes(blockSize);
        }

        private byte[] ReadBytes(int length)
        {
            byte[] buffer = new byte[length];
            m_Stream.Read(buffer, 0, length);
            return buffer;
        }
    }
}