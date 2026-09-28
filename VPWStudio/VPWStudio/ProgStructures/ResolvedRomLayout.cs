using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace VPWStudio
{
    /// <summary>
    /// One location resolved for the exact Base ROM used by a project.
    /// BAMP_RESOLVED_ROM_LAYOUT
    /// </summary>
    [Serializable]
    public class ResolvedRomLocation
    {
        public string Comment;
        public LocationType Type;
        public UInt32 Address;
        public int Length;
        public string Source;
        public bool Validated;

        public ResolvedRomLocation()
        {
            Comment = String.Empty;
            Type = LocationType.Invalid;
            Address = 0;
            Length = 0;
            Source = String.Empty;
            Validated = false;
        }

        public ResolvedRomLocation(LocationFileEntry entry, string source, bool validated)
        {
            Comment = entry.Comment;
            Type = entry.Type;
            Address = entry.Address;
            Length = entry.Length;
            Source = source;
            Validated = validated;
        }

        public ResolvedRomLocation(ResolvedRomLocation src)
        {
            Comment = src.Comment;
            Type = src.Type;
            Address = src.Address;
            Length = src.Length;
            Source = src.Source;
            Validated = src.Validated;
        }

        public LocationFileEntry ToLocationFileEntry()
        {
            LocationFileEntry result = new LocationFileEntry(Type, Address, Length, Comment);
            if (!String.IsNullOrEmpty(Comment))
            {
                if (Comment.StartsWith("$"))
                {
                    result.Handler = LocationHandlerType.DataLocation;
                }
                else if (Comment.StartsWith("%"))
                {
                    result.Handler = LocationHandlerType.CodeChange;
                }
                else if (Comment.StartsWith("@"))
                {
                    result.Handler = LocationHandlerType.CodeSegment;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// SHA-1-bound map of logical VPWStudio locations to the actual Base ROM.
    /// Serialized inside the project file so a LocationFiles TXT is a fallback, not gospel.
    /// </summary>
    [Serializable]
    public class ResolvedRomLayout
    {
        public const string ResolverVersionCurrent = "BAMP-1";

        public string RomSha1;
        public string ResolverVersion;
        public bool CoreValidated;
        public List<ResolvedRomLocation> Locations;

        public ResolvedRomLayout()
        {
            RomSha1 = String.Empty;
            ResolverVersion = ResolverVersionCurrent;
            CoreValidated = false;
            Locations = new List<ResolvedRomLocation>();
        }

        public ResolvedRomLayout(ResolvedRomLayout src)
        {
            RomSha1 = src == null ? String.Empty : src.RomSha1;
            ResolverVersion = src == null ? ResolverVersionCurrent : src.ResolverVersion;
            CoreValidated = src != null && src.CoreValidated;
            Locations = new List<ResolvedRomLocation>();
            if (src != null && src.Locations != null)
            {
                foreach (ResolvedRomLocation item in src.Locations)
                {
                    Locations.Add(new ResolvedRomLocation(item));
                }
            }
        }

        public ResolvedRomLocation Get(string comment)
        {
            if (Locations == null)
            {
                return null;
            }
            foreach (ResolvedRomLocation item in Locations)
            {
                if (item.Comment == comment)
                {
                    return item;
                }
            }
            return null;
        }

        public LocationFileEntry GetLocationFileEntry(string comment)
        {
            ResolvedRomLocation item = Get(comment);
            return item == null ? null : item.ToLocationFileEntry();
        }

        public void AddIfMissing(LocationFileEntry entry, string source, bool validated)
        {
            if (entry == null || Get(entry.Comment) != null)
            {
                return;
            }
            Locations.Add(new ResolvedRomLocation(entry, source, validated));
        }

        public void Upsert(LocationFileEntry entry, string source, bool validated)
        {
            if (entry == null)
            {
                return;
            }
            ResolvedRomLocation existing = Get(entry.Comment);
            if (existing == null)
            {
                Locations.Add(new ResolvedRomLocation(entry, source, validated));
                return;
            }
            existing.Type = entry.Type;
            existing.Address = entry.Address;
            existing.Length = entry.Length;
            existing.Source = source;
            existing.Validated = validated;
        }
    }

    /// <summary>
    /// Resolves locations for the exact loaded ROM. Saved layouts are reused only when SHA-1 matches.
    /// VPW2 additionally reads the live SetupFiletable MIPS immediates, so relocated/expanded
    /// FileTable and FirstFile values are learned from the ROM rather than NA2J.txt.
    /// </summary>
    public static class RomLayoutResolver
    {
        public static ResolvedRomLayout Resolve(
            Z64Rom rom,
            LocationFile primaryLocations,
            LocationFile stockLocations,
            bool primaryIsCustom,
            SpecificGame gameType,
            ResolvedRomLayout savedLayout,
            FileTable projectFileTable)
        {
            ResolvedRomLayout result = new ResolvedRomLayout();
            if (rom == null || rom.Data == null)
            {
                return result;
            }

            result.RomSha1 = ComputeSha1(rom.Data);

            // Tier 2: a project-saved layout is usable only for the exact same Base ROM bytes.
            bool savedMatches = savedLayout != null &&
                !String.IsNullOrEmpty(savedLayout.RomSha1) &&
                String.Equals(savedLayout.RomSha1, result.RomSha1, StringComparison.OrdinalIgnoreCase);
            if (savedMatches && savedLayout.Locations != null)
            {
                foreach (ResolvedRomLocation item in savedLayout.Locations)
                {
                    result.Locations.Add(new ResolvedRomLocation(item));
                }
            }

            // Compatibility bridge for older project files: trust the stored FileTable core only
            // when it structurally validates against the currently loaded ROM.
            if (projectFileTable != null && projectFileTable.Entries != null && projectFileTable.Entries.Count > 0)
            {
                int projectLength = projectFileTable.Entries.Count * 4;
                if (ValidateFileTable(rom.Data, projectFileTable.Location, projectLength, projectFileTable.FirstFile))
                {
                    LocationFileEntry projectFt = MakeRomEntry(
                        LocationFile.SpecialEntryStrings["FileTable"],
                        projectFileTable.Location,
                        projectLength);
                    LocationFileEntry projectFf = MakeRomEntry(
                        LocationFile.SpecialEntryStrings["FirstFile"],
                        projectFileTable.FirstFile,
                        0);
                    result.AddIfMissing(projectFt, "ProjectFileValidated", true);
                    result.AddIfMissing(projectFf, "ProjectFileValidated", true);
                }
            }

            // Tier 3/4: current custom TXT, then built-in stock TXT.
            if (primaryLocations != null)
            {
                string source = primaryIsCustom ? "CustomLocationFile" : "StockLocationFile";
                AddLocationFileFallbacks(result, primaryLocations, source);
            }
            if (stockLocations != null && !Object.ReferenceEquals(stockLocations, primaryLocations))
            {
                AddLocationFileFallbacks(result, stockLocations, "StockLocationFile");
            }

            // Tier 5: internal defaults for known logical names not supplied by a TXT.
            AddDefaultFallbacks(result, gameType);

            // Tier 1: actual ROM detection wins over everything above.
            if (gameType == SpecificGame.VPW2_NTSC_J)
            {
                UInt32 detectedFt;
                UInt32 detectedFf;
                int detectedLength;
                if (TryDetectVpw2RuntimeLayout(rom.Data, out detectedFt, out detectedLength, out detectedFf))
                {
                    result.Upsert(
                        MakeRomEntry(LocationFile.SpecialEntryStrings["FileTable"], detectedFt, detectedLength),
                        "Detected:VPW2Runtime",
                        true);
                    result.Upsert(
                        MakeRomEntry(LocationFile.SpecialEntryStrings["FirstFile"], detectedFf, 0),
                        "Detected:VPW2Runtime",
                        true);
                }
            }

            ResolvedRomLocation ft = result.Get(LocationFile.SpecialEntryStrings["FileTable"]);
            ResolvedRomLocation ff = result.Get(LocationFile.SpecialEntryStrings["FirstFile"]);
            result.CoreValidated = ft != null && ff != null &&
                ft.Type == LocationType.ROM && ff.Type == LocationType.ROM &&
                ValidateFileTable(rom.Data, ft.Address, ft.Length, ff.Address);

            if (result.CoreValidated)
            {
                ft.Validated = true;
                ff.Validated = true;
            }

            return result;
        }

        private static void AddLocationFileFallbacks(ResolvedRomLayout result, LocationFile file, string source)
        {
            if (file.Locations == null)
            {
                return;
            }
            foreach (LocationFileEntry entry in file.Locations)
            {
                result.AddIfMissing(entry, source, false);
            }
        }

        private static void AddDefaultFallbacks(ResolvedRomLayout result, SpecificGame gameType)
        {
            if (!DefaultGameData.DefaultLocations.ContainsKey(gameType))
            {
                return;
            }

            DefaultGameData.DefaultLocationData defaults = DefaultGameData.DefaultLocations[gameType];
            foreach (KeyValuePair<string, string> special in LocationFile.SpecialEntryStrings)
            {
                if (!defaults.Locations.ContainsKey(special.Key))
                {
                    continue;
                }
                DefaultGameData.DefaultLocationDataEntry d = defaults.Locations[special.Key];
                LocationFileEntry entry = MakeRomEntry(special.Value, d.Offset, (int)d.Length);
                result.AddIfMissing(entry, "DefaultGameData", false);
            }
        }

        private static LocationFileEntry MakeRomEntry(string comment, UInt32 address, int length)
        {
            LocationFileEntry entry = new LocationFileEntry(LocationType.ROM, address, length, comment);
            if (comment.StartsWith("$"))
            {
                entry.Handler = LocationHandlerType.DataLocation;
            }
            else if (comment.StartsWith("%"))
            {
                entry.Handler = LocationHandlerType.CodeChange;
            }
            else if (comment.StartsWith("@"))
            {
                entry.Handler = LocationHandlerType.CodeSegment;
            }
            return entry;
        }

        private static string ComputeSha1(byte[] data)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", String.Empty);
            }
        }

        private static UInt16 ReadBE16(byte[] data, int offset)
        {
            return (UInt16)((data[offset] << 8) | data[offset + 1]);
        }

        private static UInt32 ReadBE32(byte[] data, int offset)
        {
            return ((UInt32)data[offset] << 24) |
                   ((UInt32)data[offset + 1] << 16) |
                   ((UInt32)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        private static UInt32 DecodeLuiAddiuAddress(byte[] data, int luiOffset, int addiuOffset)
        {
            UInt16 hi = ReadBE16(data, luiOffset + 2);
            Int16 lo = unchecked((Int16)ReadBE16(data, addiuOffset + 2));
            Int64 value = ((Int64)hi << 16) + lo;
            return unchecked((UInt32)value);
        }

        /// <summary>
        /// VPW2's SetupFiletable lives in the main text segment and directly describes the active
        /// file index address, first-file base, and entry count. Reading these immediates lets BAMP
        /// follow expanded/relocated VPW2 layouts without assuming NA2J.txt is still correct.
        /// </summary>
        private static bool TryDetectVpw2RuntimeLayout(byte[] data, out UInt32 fileTable, out int fileTableLength, out UInt32 firstFile)
        {
            fileTable = 0;
            fileTableLength = 0;
            firstFile = 0;

            // Highest instruction touched below is 0x498F.
            if (data == null || data.Length <= 0x4990)
            {
                return false;
            }

            // Stock/expanded VPW2 keeps the same SetupFiletable instruction forms:
            // 48D8: lui a0, hi(file index); 48DC: addiu a0,a0,lo(file index)
            // 4920: lui s4, hi(first file); 4924: addiu s4,s4,lo(first file)
            // 498C: sltiu v0,s2,(entry count - 1)
            UInt32 ftLui = ReadBE32(data, 0x48D8);
            UInt32 ftAddiu = ReadBE32(data, 0x48DC);
            UInt32 ffLui = ReadBE32(data, 0x4920);
            UInt32 ffAddiu = ReadBE32(data, 0x4924);
            UInt32 countInsn = ReadBE32(data, 0x498C);

            if ((ftLui & 0xFFFF0000) != 0x3C040000 ||
                (ftAddiu & 0xFFFF0000) != 0x24840000 ||
                (ffLui & 0xFFFF0000) != 0x3C140000 ||
                (ffAddiu & 0xFFFF0000) != 0x26940000 ||
                (countInsn & 0xFFFF0000) != 0x2E420000)
            {
                return false;
            }

            UInt32 ft = DecodeLuiAddiuAddress(data, 0x48D8, 0x48DC);
            UInt32 ff = DecodeLuiAddiuAddress(data, 0x4920, 0x4924);
            int count = (int)(countInsn & 0xFFFF) + 1;
            int length = count * 4;

            if (count < 16 || length <= 0 || ft >= data.Length || ff >= data.Length)
            {
                return false;
            }
            if ((UInt64)ft + (UInt64)length > (UInt64)data.Length)
            {
                return false;
            }

            // The allocation/DMA size at 48B4 is another independent sanity check when it
            // remains a one-instruction LI/ORI (true for retail and BAMP's current expansions).
            UInt32 sizeInsn = ReadBE32(data, 0x48B4);
            if ((sizeInsn & 0xFFFF0000) == 0x34040000)
            {
                int encodedSize = (int)(sizeInsn & 0xFFFF);
                if (encodedSize != length)
                {
                    return false;
                }
            }

            if (!ValidateFileTable(data, ft, length, ff))
            {
                return false;
            }

            fileTable = ft;
            fileTableLength = length;
            firstFile = ff;
            return true;
        }

        private static bool ValidateFileTable(byte[] data, UInt32 fileTable, int length, UInt32 firstFile)
        {
            if (data == null || length < 64 || (length & 3) != 0)
            {
                return false;
            }
            if (fileTable >= data.Length || firstFile >= data.Length)
            {
                return false;
            }
            if ((UInt64)fileTable + (UInt64)length > (UInt64)data.Length)
            {
                return false;
            }

            int count = length / 4;
            UInt32 maxRelative = (UInt32)(data.Length - firstFile);
            UInt32 previous = 0;
            int decreases = 0;
            int zeroEntries = 0;

            for (int i = 0; i < count; i++)
            {
                UInt32 raw = ReadBE32(data, (int)fileTable + (i * 4));
                UInt32 relative = raw & 0xFFFFFFFE;
                if (relative > maxRelative)
                {
                    return false;
                }
                if (i == 0 && relative > 0x1000)
                {
                    return false;
                }
                if (i > 0 && relative < previous)
                {
                    decreases++;
                }
                if (relative == 0)
                {
                    zeroEntries++;
                }
                previous = relative;
            }

            // AKI tables are overwhelmingly monotonic. Keep room for known odd entries without
            // accepting arbitrary data as a table.
            int allowedDecreases = Math.Max(4, count / 1024);
            if (decreases > allowedDecreases)
            {
                return false;
            }
            if (zeroEntries > Math.Max(8, count / 16))
            {
                return false;
            }

            return true;
        }
    }
}
