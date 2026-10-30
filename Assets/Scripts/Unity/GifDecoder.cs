using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// One decoded GIF image block. Frames are stored uncompressed as palette indices so the
    /// player can compose them on a single canvas (cheap memory, correct disposal handling).
    /// </summary>
    public sealed class GifFrame
    {
        public int Left;
        public int Top;
        public int Width;
        public int Height;
        public byte[] Indices;
        public Color32[] Palette;
        public int TransparentIndex = -1;
        public int DisposalMethod;
        public float Delay = 0.1f;
    }

    public sealed class GifAnimation
    {
        public int Width;
        public int Height;
        public Color32 BackgroundColor = new Color32(0, 0, 0, 0);
        public GifFrame[] Frames;
    }

    /// <summary>
    /// Minimal GIF87a/GIF89a decoder (LZW, interlacing, transparency, disposal methods and
    /// animation delays). It only depends on managed types, so it can run on a worker thread.
    /// </summary>
    public static class GifDecoder
    {
        public static GifAnimation Decode(byte[] data)
        {
            if (data == null || data.Length < 14)
                throw new ArgumentException("GIF data is empty or too short.");
            if (data[0] != 'G' || data[1] != 'I' || data[2] != 'F')
                throw new ArgumentException("The data is not a GIF file.");

            int pos = 6;
            int width = ReadUInt16(data, ref pos);
            int height = ReadUInt16(data, ref pos);
            if (width <= 0 || height <= 0)
                throw new ArgumentException("The GIF has an invalid canvas size.");

            byte packed = data[pos++];
            int backgroundIndex = data[pos++];
            pos++; // pixel aspect ratio

            Color32[] globalPalette = null;
            if ((packed & 0x80) != 0)
            {
                int colorCount = 1 << ((packed & 0x07) + 1);
                globalPalette = ReadPalette(data, ref pos, colorCount);
            }

            var animation = new GifAnimation
            {
                Width = width,
                Height = height,
                BackgroundColor = new Color32(0, 0, 0, 0)
            };

            if (globalPalette != null && backgroundIndex < globalPalette.Length)
                animation.BackgroundColor = globalPalette[backgroundIndex];

            var frames = new List<GifFrame>();
            int disposal = 0;
            float delay = 0.1f;
            int transparentIndex = -1;

            while (pos < data.Length)
            {
                byte block = data[pos++];

                if (block == 0x3B) break; // trailer

                if (block == 0x00) continue; // padding

                if (block == 0x21) // extension
                {
                    if (pos >= data.Length) break;
                    byte label = data[pos++];

                    if (label == 0xF9) // graphic control extension
                    {
                        int size = data[pos++];
                        if (size >= 4 && pos + 4 <= data.Length)
                        {
                            byte control = data[pos];
                            disposal = (control >> 2) & 0x07;
                            bool hasTransparency = (control & 0x01) != 0;
                            int delayCentiseconds = data[pos + 1] | (data[pos + 2] << 8);
                            int index = data[pos + 3];
                            delay = Mathf.Max(delayCentiseconds * 0.01f, 0.02f);
                            transparentIndex = hasTransparency ? index : -1;
                        }
                        pos += size;
                        SkipSubBlocks(data, ref pos);
                    }
                    else
                    {
                        SkipSubBlocks(data, ref pos);
                    }

                    continue;
                }

                if (block != 0x2C) // image descriptor
                {
                    // Unknown block: stop instead of reading garbage.
                    break;
                }

                int left = ReadUInt16(data, ref pos);
                int top = ReadUInt16(data, ref pos);
                int frameWidth = ReadUInt16(data, ref pos);
                int frameHeight = ReadUInt16(data, ref pos);
                byte imagePacked = data[pos++];

                bool hasLocalPalette = (imagePacked & 0x80) != 0;
                bool interlaced = (imagePacked & 0x40) != 0;
                int localColorCount = 1 << ((imagePacked & 0x07) + 1);

                Color32[] palette = globalPalette;
                if (hasLocalPalette)
                    palette = ReadPalette(data, ref pos, localColorCount);

                if (pos >= data.Length) break;
                int minCodeSize = data[pos++];
                byte[] compressed = ReadSubBlocks(data, ref pos);

                if (palette != null && frameWidth > 0 && frameHeight > 0 && compressed.Length > 0)
                {
                    byte[] indices = DecodeLzw(compressed, minCodeSize, frameWidth * frameHeight);
                    if (interlaced) indices = Deinterlace(indices, frameWidth, frameHeight);

                    frames.Add(new GifFrame
                    {
                        Left = left,
                        Top = top,
                        Width = frameWidth,
                        Height = frameHeight,
                        Indices = indices,
                        Palette = palette,
                        TransparentIndex = transparentIndex,
                        DisposalMethod = disposal,
                        Delay = delay
                    });
                }

                // The control block applies to a single frame.
                disposal = 0;
                delay = 0.1f;
                transparentIndex = -1;
            }

            if (frames.Count == 0)
                throw new ArgumentException("The GIF does not contain any decodable frame.");

            animation.Frames = frames.ToArray();
            return animation;
        }

        private static Color32[] ReadPalette(byte[] data, ref int pos, int count)
        {
            var palette = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                if (pos + 2 >= data.Length) break;
                byte r = data[pos++];
                byte g = data[pos++];
                byte b = data[pos++];
                palette[i] = new Color32(r, g, b, 255);
            }
            return palette;
        }

        private static byte[] ReadSubBlocks(byte[] data, ref int pos)
        {
            var result = new List<byte>(256);
            while (pos < data.Length)
            {
                int size = data[pos++];
                if (size == 0) break;
                if (pos + size > data.Length) size = data.Length - pos;
                for (int i = 0; i < size; i++) result.Add(data[pos++]);
            }
            return result.ToArray();
        }

        private static void SkipSubBlocks(byte[] data, ref int pos)
        {
            while (pos < data.Length)
            {
                int size = data[pos++];
                if (size == 0) break;
                pos += size;
            }
        }

        private static int ReadUInt16(byte[] data, ref int pos)
        {
            if (pos + 1 >= data.Length) return 0;
            int value = data[pos] | (data[pos + 1] << 8);
            pos += 2;
            return value;
        }

        private static byte[] Deinterlace(byte[] indices, int width, int height)
        {
            var result = new byte[indices.Length];
            int sourceRow = 0;
            int[] starts = { 0, 4, 2, 1 };
            int[] steps = { 8, 8, 4, 2 };

            for (int pass = 0; pass < 4; pass++)
            {
                for (int y = starts[pass]; y < height; y += steps[pass])
                {
                    int source = sourceRow * width;
                    int target = y * width;
                    Array.Copy(indices, source, result, target, width);
                    sourceRow++;
                }
            }

            return result;
        }

        private static byte[] DecodeLzw(byte[] data, int minCodeSize, int pixelCount)
        {
            if (minCodeSize < 2 || minCodeSize > 8)
                throw new ArgumentException("Invalid GIF LZW code size.");

            const int MaxCodes = 4096;
            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            int codeSize = minCodeSize + 1;
            int nextCode = endCode + 1;

            var prefix = new short[MaxCodes];
            var suffix = new byte[MaxCodes];
            var stack = new byte[MaxCodes + 1];
            var output = new byte[pixelCount];

            for (int i = 0; i < clearCode; i++)
            {
                prefix[i] = -1;
                suffix[i] = (byte)i;
            }

            int bitPosition = 0;
            int outputPosition = 0;
            int previousCode = -1;

            while (outputPosition < pixelCount)
            {
                int code = ReadCode(data, ref bitPosition, codeSize);
                if (code < 0) break;

                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = endCode + 1;
                    previousCode = -1;
                    continue;
                }

                if (code == endCode) break;

                int stackSize = 0;

                if (code >= nextCode)
                {
                    if (previousCode < 0) break;
                    stack[stackSize++] = suffix[previousCode];
                    code = previousCode;
                }

                int current = code;
                while (current >= clearCode)
                {
                    if (stackSize >= MaxCodes || current >= MaxCodes) break;
                    stack[stackSize++] = suffix[current];
                    current = prefix[current];
                    if (current < 0) break;
                }

                if (current < 0) break;

                byte first = suffix[current];
                stack[stackSize++] = first;

                for (int i = stackSize - 1; i >= 0 && outputPosition < pixelCount; i--)
                    output[outputPosition++] = stack[i];

                if (previousCode >= 0 && nextCode < MaxCodes)
                {
                    prefix[nextCode] = (short)previousCode;
                    suffix[nextCode] = first;
                    nextCode++;

                    if (nextCode == (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }

                previousCode = code;
            }

            return output;
        }

        private static int ReadCode(byte[] data, ref int bitPosition, int codeSize)
        {
            if (bitPosition + codeSize > data.Length * 8) return -1;

            int value = 0;
            for (int i = 0; i < codeSize; i++)
            {
                int absolute = bitPosition + i;
                int byteIndex = absolute >> 3;
                int bitIndex = absolute & 7;
                if ((data[byteIndex] & (1 << bitIndex)) != 0) value |= 1 << i;
            }

            bitPosition += codeSize;
            return value;
        }
    }
}
