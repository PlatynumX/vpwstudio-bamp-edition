using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace VPWStudio
{
    /// <summary>
    /// CI4 (Color Indexed 4bpp) texture.
    /// Data is stored internally as one byte per pixel; write methods repack it.
    /// </summary>
    public class Ci4Texture
    {
        public int Width;
        public int Height;
        public UInt16 NumPalEntries;
        public byte HorizMirror;
        public byte VertMirror;
        public byte WidthBitLength;
        public byte HeightBitLength;
        public byte[] Data;

        public Ci4Texture()
        {
            Width = 0;
            Height = 0;
            NumPalEntries = 16;
            HorizMirror = 0;
            VertMirror = 0;
            WidthBitLength = 0;
            HeightBitLength = 0;
            Data = null;
        }

        public Ci4Texture(BinaryReader br)
        {
            ReadData(br);
        }

        public void ReadData(BinaryReader br)
        {
            Width = (br.ReadByte() + 1);
            Height = (br.ReadByte() + 1);
            byte[] npe = br.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(npe);
            NumPalEntries = BitConverter.ToUInt16(npe, 0);
            HorizMirror = br.ReadByte();
            VertMirror = br.ReadByte();
            WidthBitLength = br.ReadByte();
            HeightBitLength = br.ReadByte();

            int numPixels = Width * Height;
            Data = new byte[numPixels];
            int i = 0;
            while (i < numPixels)
            {
                byte b = br.ReadByte();
                Data[i] = (byte)((b & 0xF0) >> 4);
                if ((i + 1) < numPixels)
                {
                    Data[i + 1] = (byte)(b & 0x0F);
                }
                i += 2;
            }
        }

        public void ReadRawData(int width, int height, BinaryReader br)
        {
            Width = width;
            Height = height;
            int numPixels = width * height;
            Data = new byte[numPixels];
            int i = 0;
            while (i < numPixels)
            {
                byte b = br.ReadByte();
                Data[i] = (byte)((b & 0xF0) >> 4);
                if ((i + 1) < numPixels)
                {
                    Data[i + 1] = (byte)(b & 0x0F);
                }
                i += 2;
            }
        }

        public void WriteData(BinaryWriter bw)
        {
            bw.Write((byte)(Width - 1));
            bw.Write((byte)(Height - 1));
            byte[] npe = BitConverter.GetBytes(NumPalEntries);
            if (BitConverter.IsLittleEndian) Array.Reverse(npe);
            bw.Write(npe);
            bw.Write(HorizMirror);
            bw.Write(VertMirror);
            bw.Write(WidthBitLength);
            bw.Write(HeightBitLength);
            WritePackedPixelData(bw);
        }

        public void WriteCi4BackgroundData(BinaryWriter bw)
        {
            bw.Write(new byte[] { 0x3F, 0xEF, 0x00, 0x20, 0x00, 0x00, 0x08, 0x07 });
            WritePackedPixelData(bw);
        }

        public Bitmap ToBitmap(Ci4Palette pal, int subPalette = 0)
        {
            if (Data == null || pal == null)
            {
                return null;
            }

            if (subPalette > 0 && pal.SubPalettes.Count <= 0)
            {
                subPalette = 0;
            }

            Bitmap bOut = new Bitmap(Width, Height);
            BitmapData bData = bOut.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            IntPtr imageDataPtr = bData.Scan0;
            int numBytes = Math.Abs(bData.Stride) * Height;
            byte[] bPixels = new byte[numBytes];
            Marshal.Copy(imageDataPtr, bPixels, 0, numBytes);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    byte palIdx = Data[(y * Width) + x];
                    Color c = (subPalette != 0)
                        ? N64Colors.Value5551ToColor(pal.SubPalettes[subPalette - 1].Entries[palIdx])
                        : N64Colors.Value5551ToColor(pal.Entries[palIdx]);

                    int p = ((y * Width) + x) * 4;
                    bPixels[p] = c.B;
                    bPixels[p + 1] = c.G;
                    bPixels[p + 2] = c.R;
                    bPixels[p + 3] = c.A;
                }
            }

            Marshal.Copy(bPixels, 0, imageDataPtr, numBytes);
            bOut.UnlockBits(bData);
            return bOut;
        }

        public bool FromBitmap(Bitmap inBmp)
        {
            if (inBmp == null)
            {
                return false;
            }

            Color[] colors;
            byte[] pixels;
            if (!TextureConversionHelper.TryConvertBitmapToIndexed(inBmp, 16, -1, out colors, out pixels))
            {
                return false;
            }

            Width = inBmp.Width;
            Height = inBmp.Height;
            NumPalEntries = 16;
            Data = pixels;
            CalculateBitLengths();
            return true;
        }

        private void CalculateBitLengths()
        {
            int temp = Width - 1;
            int l = 0;
            do { l++; } while ((temp >>= 1) != 0);
            WidthBitLength = (byte)l;

            temp = Height - 1;
            l = 0;
            do { l++; } while ((temp >>= 1) != 0);
            HeightBitLength = (byte)l;
        }

        private void WritePackedPixelData(BinaryWriter bw)
        {
            int numPixels = Width * Height;
            for (int i = 0; i < numPixels; i += 2)
            {
                byte hi = (byte)(Data[i] & 0x0F);
                byte lo = (byte)(((i + 1) < numPixels) ? (Data[i + 1] & 0x0F) : 0);
                bw.Write((byte)((hi << 4) | lo));
            }
        }
    }
}
