using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace VPWStudio
{
    public class N64Colors
    {
        #region Macros from Texture64
        private static int SCALE_5_8(int v) { return (v * 0xFF) / 0x1F; }
        private static byte SCALE_8_5(byte v) { return (byte)((((v) + 4) * 0x1F) / 0xFF); }
        private static byte SCALE_8_3(byte v) { return (byte)(v / 0x24); }
        private static byte SCALE_8_4(byte v) { return (byte)(v / 0x11); }
        #endregion

        /// <summary>
        /// Convert a UInt16 RGBA 5551 value to a Color.
        /// </summary>
        public static Color Value5551ToColor(UInt16 cv)
        {
            return Color.FromArgb(
                ((cv & 0x0001) > 0) ? 0xFF : 0,
                SCALE_5_8((cv & 0xF800) >> 11),
                SCALE_5_8((cv & 0x07C0) >> 6),
                SCALE_5_8((cv & 0x003E) >> 1)
            );
        }

        /// <summary>
        /// Convert a Color to UInt16 RGBA 5551 value.
        /// N64 5551 only supports on/off alpha, so any alpha > 0 becomes opaque.
        /// </summary>
        public static UInt16 ColorToValue5551(Color c)
        {
            UInt16 result = (UInt16)((c.A > 0) ? 1 : 0);
            result |= (UInt16)((SCALE_8_5(c.R)) << 11);
            result |= (UInt16)((SCALE_8_5(c.G)) << 6);
            result |= (UInt16)((SCALE_8_5(c.B)) << 1);
            return result;
        }
    }

    internal static class TextureConversionHelper
    {
        private const int AlphaThreshold = 1;

        public static int CountUniqueOpaqueColors(Bitmap source)
        {
            if (source == null)
            {
                return 0;
            }

            HashSet<int> unique = new HashSet<int>();
            using (Bitmap working = CloneTo32bpp(source))
            {
                for (int y = 0; y < working.Height; y++)
                {
                    for (int x = 0; x < working.Width; x++)
                    {
                        Color c = NormalizeColor(working.GetPixel(x, y));
                        if (c.A == 0)
                        {
                            continue;
                        }

                        unique.Add(c.ToArgb());
                    }
                }
            }

            return unique.Count;
        }

        public static int FindTransparentIndex(UInt16[] paletteValues)
        {
            if (paletteValues == null)
            {
                return -1;
            }

            for (int i = 0; i < paletteValues.Length; i++)
            {
                if (N64Colors.Value5551ToColor(paletteValues[i]).A == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool TryConvertBitmapToIndexed(Bitmap source, int maxColors, int forcedTransparentIndex, out Color[] palette, out byte[] indices)
        {
            palette = null;
            indices = null;

            if (source == null || maxColors <= 0)
            {
                return false;
            }

            using (Bitmap working = CloneTo32bpp(source))
            {
                Dictionary<int, int> colorFrequency = new Dictionary<int, int>();
                bool hasTransparency = false;

                for (int y = 0; y < working.Height; y++)
                {
                    for (int x = 0; x < working.Width; x++)
                    {
                        Color c = NormalizeColor(working.GetPixel(x, y));
                        if (c.A == 0)
                        {
                            hasTransparency = true;
                            continue;
                        }

                        int key = c.ToArgb();
                        if (colorFrequency.ContainsKey(key))
                        {
                            colorFrequency[key]++;
                        }
                        else
                        {
                            colorFrequency[key] = 1;
                        }
                    }
                }

                int transparentIndex = -1;
                if (hasTransparency)
                {
                    transparentIndex = (forcedTransparentIndex >= 0 && forcedTransparentIndex < maxColors)
                        ? forcedTransparentIndex
                        : 0;
                }

                int availableOpaqueSlots = maxColors - (hasTransparency ? 1 : 0);
                if (availableOpaqueSlots <= 0)
                {
                    return false;
                }

                List<KeyValuePair<int, int>> sortedColors = new List<KeyValuePair<int, int>>(colorFrequency);
                sortedColors.Sort((a, b) =>
                {
                    int cmp = b.Value.CompareTo(a.Value);
                    if (cmp != 0)
                    {
                        return cmp;
                    }

                    return a.Key.CompareTo(b.Key);
                });

                List<Color> selectedOpaqueColors = new List<Color>();
                int takeCount = Math.Min(sortedColors.Count, availableOpaqueSlots);
                for (int i = 0; i < takeCount; i++)
                {
                    selectedOpaqueColors.Add(Color.FromArgb(sortedColors[i].Key));
                }

                List<Color> finalPalette = new List<Color>(maxColors);
                for (int i = 0; i < maxColors; i++)
                {
                    finalPalette.Add(Color.Transparent);
                }

                if (transparentIndex >= 0)
                {
                    finalPalette[transparentIndex] = Color.FromArgb(0, 0, 0, 0);
                }

                int opaqueCursor = 0;
                for (int i = 0; i < maxColors; i++)
                {
                    if (i == transparentIndex)
                    {
                        continue;
                    }

                    if (opaqueCursor < selectedOpaqueColors.Count)
                    {
                        Color c = selectedOpaqueColors[opaqueCursor++];
                        finalPalette[i] = Color.FromArgb(255, c.R, c.G, c.B);
                    }
                    else if (selectedOpaqueColors.Count > 0)
                    {
                        Color c = selectedOpaqueColors[selectedOpaqueColors.Count - 1];
                        finalPalette[i] = Color.FromArgb(255, c.R, c.G, c.B);
                    }
                    else
                    {
                        finalPalette[i] = Color.FromArgb(0, 0, 0, 0);
                    }
                }

                indices = new byte[working.Width * working.Height];
                for (int y = 0; y < working.Height; y++)
                {
                    for (int x = 0; x < working.Width; x++)
                    {
                        Color c = NormalizeColor(working.GetPixel(x, y));
                        int outIndex;

                        if (c.A == 0 && transparentIndex >= 0)
                        {
                            outIndex = transparentIndex;
                        }
                        else
                        {
                            outIndex = FindClosestColorIndex(c, finalPalette, transparentIndex);
                        }

                        indices[(y * working.Width) + x] = (byte)outIndex;
                    }
                }

                palette = finalPalette.ToArray();
                return true;
            }
        }

        private static Bitmap CloneTo32bpp(Bitmap source)
        {
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
            }

            return result;
        }

        private static Color NormalizeColor(Color c)
        {
            return (c.A < AlphaThreshold)
                ? Color.FromArgb(0, 0, 0, 0)
                : Color.FromArgb(255, c.R, c.G, c.B);
        }

        private static int FindClosestColorIndex(Color target, List<Color> palette, int transparentIndex)
        {
            int bestIndex = 0;
            long bestDistance = long.MaxValue;

            for (int i = 0; i < palette.Count; i++)
            {
                if (i == transparentIndex)
                {
                    continue;
                }

                Color candidate = palette[i];
                if (candidate.A == 0)
                {
                    continue;
                }

                int dr = target.R - candidate.R;
                int dg = target.G - candidate.G;
                int db = target.B - candidate.B;
                long distance = (dr * dr) + (dg * dg) + (db * db);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    if (distance == 0)
                    {
                        break;
                    }
                }
            }

            return bestIndex;
        }
    }
}
