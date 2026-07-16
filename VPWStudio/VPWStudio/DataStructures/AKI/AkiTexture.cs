using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace VPWStudio
{
    /// <summary>
    /// AkiTexture container format ("TEX\0").
    /// </summary>
    public class AkiTexture
    {
        private readonly byte[] TEX_HEADER_MAGIC = { 0x54, 0x45, 0x58, 0x00 };

        public enum AkiTextureFormat
        {
            Ci4 = 0x04,
            Ci8 = 0x08,
        }

        public UInt16 Width;
        public UInt16 Height;
        public AkiTextureFormat ImageFormat;
        public byte ColorWidth;
        public UInt16 PaletteNumColors;
        public UInt16[] Palette;
        public byte[] Data;

        public AkiTexture()
        {
            Width = 0;
            Height = 0;
            ImageFormat = 0;
            ColorWidth = 0;
            PaletteNumColors = 0;
            Palette = null;
            Data = null;
        }

        public AkiTexture(BinaryReader br)
        {
            ReadData(br);
        }

        public bool ReadData(BinaryReader br)
        {
            byte[] magic = br.ReadBytes(4);
            if (magic.Length < 4 || magic[0] != 'T' || magic[1] != 'E' || magic[2] != 'X' || magic[3] != 0)
            {
                return false;
            }

            byte[] w = br.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(w);
            Width = BitConverter.ToUInt16(w, 0);

            byte[] h = br.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(h);
            Height = BitConverter.ToUInt16(h, 0);

            ImageFormat = (AkiTextureFormat)br.ReadByte();
            ColorWidth = br.ReadByte();

            byte[] pnc = br.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(pnc);
            PaletteNumColors = BitConverter.ToUInt16(pnc, 0);

            br.BaseStream.Seek(0x10, SeekOrigin.Begin);
            Palette = new UInt16[PaletteNumColors];
            for (int i = 0; i < PaletteNumColors; i++)
            {
                byte[] cd = br.ReadBytes(2);
                if (BitConverter.IsLittleEndian) Array.Reverse(cd);
                Palette[i] = BitConverter.ToUInt16(cd, 0);
            }

            br.BaseStream.Seek((PaletteNumColors * ColorWidth) + 0x10, SeekOrigin.Begin);
            switch (ImageFormat)
            {
                case AkiTextureFormat.Ci4:
                {
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
                    break;
                }
                case AkiTextureFormat.Ci8:
                    Data = br.ReadBytes(Width * Height);
                    break;
                default:
                    Data = null;
                    break;
            }

            return true;
        }

        public void WriteData(BinaryWriter bw)
        {
            bw.Write(TEX_HEADER_MAGIC);

            byte[] w = BitConverter.GetBytes(Width);
            if (BitConverter.IsLittleEndian) Array.Reverse(w);
            bw.Write(w);

            byte[] h = BitConverter.GetBytes(Height);
            if (BitConverter.IsLittleEndian) Array.Reverse(h);
            bw.Write(h);

            bw.Write((byte)ImageFormat);
            bw.Write(ColorWidth);

            byte[] nc = BitConverter.GetBytes(PaletteNumColors);
            if (BitConverter.IsLittleEndian) Array.Reverse(nc);
            bw.Write(nc);

            bw.Seek(0x10, SeekOrigin.Begin);
            for (int i = 0; i < PaletteNumColors; i++)
            {
                byte[] cv = BitConverter.GetBytes(Palette[i]);
                if (BitConverter.IsLittleEndian) Array.Reverse(cv);
                bw.Write(cv);
            }

            switch (ImageFormat)
            {
                case AkiTextureFormat.Ci4:
                    WritePackedCi4Data(bw);
                    break;
                case AkiTextureFormat.Ci8:
                    bw.Write(Data, 0, Width * Height);
                    break;
            }
        }

        public Bitmap ToBitmap()
        {
            if (Data == null)
            {
                return null;
            }

            Bitmap bOut = new Bitmap(Width, Height);
            switch (ImageFormat)
            {
                case AkiTextureFormat.Ci4:
                case AkiTextureFormat.Ci8:
                    ToBitmap_CI(bOut);
                    break;
            }

            return bOut;
        }

        private void ToBitmap_CI(Bitmap bOut)
        {
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
                    Color c = N64Colors.Value5551ToColor(Palette[palIdx]);
                    int p = ((y * Width) + x) * 4;
                    bPixels[p] = c.B;
                    bPixels[p + 1] = c.G;
                    bPixels[p + 2] = c.R;
                    bPixels[p + 3] = c.A;
                }
            }

            Marshal.Copy(bPixels, 0, imageDataPtr, numBytes);
            bOut.UnlockBits(bData);
        }

        public bool FromBitmap(Bitmap bm)
        {
            if (bm == null)
            {
                return false;
            }

            int priorTransparentIndex = TextureConversionHelper.FindTransparentIndex(Palette);
            int targetPaletteSize;

            if (bm.PixelFormat == PixelFormat.Format4bppIndexed)
            {
                targetPaletteSize = 16;
            }
            else if (bm.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                targetPaletteSize = 256;
            }
            else if (ImageFormat == AkiTextureFormat.Ci8 || (Palette != null && Palette.Length > 16))
            {
                targetPaletteSize = 256;
            }
            else if (ImageFormat == AkiTextureFormat.Ci4 || (Palette != null && Palette.Length > 0 && Palette.Length <= 16))
            {
                targetPaletteSize = 16;
            }
            else
            {
                targetPaletteSize = (TextureConversionHelper.CountUniqueOpaqueColors(bm) <= 16) ? 16 : 256;
            }

            Color[] colors;
            byte[] pixels;
            if (!TextureConversionHelper.TryConvertBitmapToIndexed(bm, targetPaletteSize, priorTransparentIndex, out colors, out pixels))
            {
                return false;
            }

            Width = (UInt16)bm.Width;
            Height = (UInt16)bm.Height;
            ImageFormat = (targetPaletteSize == 16) ? AkiTextureFormat.Ci4 : AkiTextureFormat.Ci8;
            ColorWidth = 2;
            PaletteNumColors = (UInt16)targetPaletteSize;
            Palette = new UInt16[PaletteNumColors];
            for (int i = 0; i < PaletteNumColors; i++)
            {
                Palette[i] = N64Colors.ColorToValue5551(colors[i]);
            }

            Data = pixels;
            return true;
        }

        private void WritePackedCi4Data(BinaryWriter bw)
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
