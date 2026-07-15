using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace VPWStudio
{
    /// <summary>
    /// CI8 (Color Indexed 8bpp) texture.
    /// Data is stored internally as one byte per pixel.
    /// </summary>
    public class Ci8Texture
    {
        public int Width;
        public int Height;
        public UInt16 NumPalEntries;
        public byte HorizMirror;
        public byte VertMirror;
        public byte WidthBitLength;
        public byte HeightBitLength;
        public byte[] Data;

        public Ci8Texture()
        {
            Width = 0;
            Height = 0;
            NumPalEntries = 256;
            HorizMirror = 0;
            VertMirror = 0;
            WidthBitLength = 0;
            HeightBitLength = 0;
            Data = null;
        }

        public Ci8Texture(BinaryReader br)
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
            for (int i = 0; i < numPixels; i++)
            {
                Data[i] = br.ReadByte();
            }
        }

        public void ReadRawData(int width, int height, BinaryReader br)
        {
            Width = width;
            Height = height;
            int numPixels = Width * Height;
            Data = new byte[numPixels];
            for (int i = 0; i < numPixels; i++)
            {
                Data[i] = br.ReadByte();
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
            bw.Write(Data, 0, Width * Height);
        }

        public void WriteRawData(BinaryWriter bw)
        {
            bw.Write(Data, 0, Width * Height);
        }

        public Bitmap ToBitmap(Ci8Palette pal)
        {
            if (Data == null || pal == null)
            {
                return null;
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
                    Color c = N64Colors.Value5551ToColor(pal.Entries[palIdx]);
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
            if (!TextureConversionHelper.TryConvertBitmapToIndexed(inBmp, 256, -1, out colors, out pixels))
            {
                return false;
            }

            Width = inBmp.Width;
            Height = inBmp.Height;
            NumPalEntries = 256;
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
    }
}
