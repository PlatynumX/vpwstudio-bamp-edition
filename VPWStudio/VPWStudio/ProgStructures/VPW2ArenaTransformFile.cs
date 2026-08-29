using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VPWStudio
{
    public sealed class VPW2ArenaTranslation
    {
        public UInt32 ModelListRomOffset;
        public Int16 X;
        public Int16 Y;
        public Int16 Z;

        public VPW2ArenaTranslation()
        {
        }

        public VPW2ArenaTranslation(
            UInt32 modelListRomOffset,
            Int16 x,
            Int16 y,
            Int16 z)
        {
            ModelListRomOffset = modelListRomOffset;
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Project-side per-instance VPW2 arena translations plus the tiny
    /// runtime hook/table writer that makes them real on N64 hardware.
    ///
    /// The source compact model is never modified.  VPW2 decodes each
    /// arena-list occurrence into its own runtime vertex buffer, so the
    /// hook translates only the decoded copy for the matching list entry.
    /// Shared FileTable models therefore remain shared and untouched.
    /// </summary>
    public static class VPW2ArenaTransformFile
    {
        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("VPWSMOV1");

        private static readonly byte[] RuntimeMarker =
            Encoding.ASCII.GetBytes("VPWSMOV1-RUNTIME");

        private const UInt16 Version = 1;
        public const string FileName = "ArenaTransforms.vpwsmove";

        // Clean VPW2 main/global code locations.  Only the arena object's
        // call to the normal model loader is redirected.  The trampoline
        // calls the original loader first, then the per-instance translation
        // helper while the arena loader's s0/s1 identity is still intact.
        private const int HookRomOffset = 0x00010E00;
        private const UInt32 OriginalHookWord = 0x0C002FCF;
        private const UInt32 PatchedHookWord = 0x0C00010F;
        private const int TrampolineRomOffset = 0x0000103C;
        private const int HelperRomOffset = 0x00037D34;

        // Exactly 9 words / 36 bytes.  Clean VPW2 has zero padding at
        // 0x103C-0x105F.  This is intentionally arena-only; other model loads
        // continue calling 0x8000BF3C directly.
        private static readonly UInt32[] TrampolineWords =
        {
            0x27BDFFF8, // addiu sp,sp,-8
            0xAFBF0004, // sw    ra,4(sp)
            0x0C002FCF, // jal   0x8000BF3C ; original model loader
            0x00000000, //  nop
            0x0C00DC4D, // jal   0x80037134 ; per-instance helper
            0x00000000, //  nop
            0x8FBF0004, // lw    ra,4(sp)
            0x03E00008, // jr    ra
            0x27BD0008  //  addiu sp,sp,8
        };

        // Last 64 KiB of the clean 32 MiB VPW2 ROM.  The clean retail ROM
        // is 0xFF from 0x01F445B0 through 0x01FFFFFF.  Build ROM refuses to
        // use this reservation if another project modification has occupied it.
        private const int RuntimeTableRomOffset = 0x01FF0000;
        private const int RuntimeTableEnd = 0x02000000;
        private const int RuntimeMarkerRomOffset = 0x01FFFFE0;
        private const UInt32 GlobalRomToRamBias = 0x7FFFF400;

        // Exactly 27 words / 108 bytes: the verified zero padding at
        // 0x37D34-0x37D9F.  The arena-only trampoline calls this after the
        // normal model loader returns.  At that point the arena loader still
        // has s0 = runtime object and s1 = this occurrence's model-list entry.
        private static readonly UInt32[] HelperWords =
        {
            0x3C08B1FF, // lui   t0,0xB1FF
            0x8D090000, // lw    t1,0(t0)
            0x11200016, // beq   t1,zero,done
            0x2508000C, //  addiu t0,t0,12
            0x11310003, // beq   t1,s1,found
            0x850BFFF8, //  lh   t3,-8(t0)   ; X
            0x1000FFFA, // b     scan
            0x00000000, //  nop
            0x850CFFFA, // found: lh t4,-6(t0) ; Y
            0x850DFFFC, //        lh t5,-4(t0) ; Z
            0x920E0002, // lbu   t6,2(s0)     ; vertex count
            0x11C0000D, // beq   t6,zero,done
            0x8E0F0014, //  lw   t7,0x14(s0)  ; decoded Vtx buffer
            0x85F80000, // loop: lh t8,0(t7)
            0x030BC021, // addu  t8,t8,t3
            0xA5F80000, // sh    t8,0(t7)
            0x85F80002, // lh    t8,2(t7)
            0x030CC021, // addu  t8,t8,t4
            0xA5F80002, // sh    t8,2(t7)
            0x85F80004, // lh    t8,4(t7)
            0x030DC021, // addu  t8,t8,t5
            0xA5F80004, // sh    t8,4(t7)
            0x25CEFFFF, // addiu t6,t6,-1
            0x1DC0FFF5, // bgtz  t6,loop
            0x25EF0010, //  addiu t7,t7,16
            0x03E00008, // done: jr ra
            0x00000000  //  nop
        };

        public static string GetProjectPath(bool createDirectory)
        {
            if (String.IsNullOrWhiteSpace(Program.CurProjectPath))
            {
                return null;
            }

            string root = Path.GetDirectoryName(Program.CurProjectPath);
            string projectFiles = "ProjectFiles";

            if (Program.CurrentProject != null &&
                !String.IsNullOrWhiteSpace(
                    Program.CurrentProject.Settings.ProjectFilesPath))
            {
                projectFiles =
                    Program.CurrentProject.Settings.ProjectFilesPath;
            }

            string directory = Path.Combine(root, projectFiles);

            if (createDirectory)
            {
                Directory.CreateDirectory(directory);
            }

            return Path.Combine(directory, FileName);
        }

        public static SortedDictionary<UInt32, VPW2ArenaTranslation>
            LoadForCurrentProject(byte[] baseRom)
        {
            SortedDictionary<UInt32, VPW2ArenaTranslation> result =
                new SortedDictionary<UInt32, VPW2ArenaTranslation>();

            string path = GetProjectPath(false);

            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return result;
            }

            using (FileStream stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte[] magic = reader.ReadBytes(Magic.Length);

                if (magic.Length != Magic.Length ||
                    Encoding.ASCII.GetString(magic) !=
                    Encoding.ASCII.GetString(Magic))
                {
                    throw new InvalidDataException(
                        "ArenaTransforms.vpwsmove has an invalid signature.");
                }

                UInt16 version = reader.ReadUInt16();
                if (version != Version)
                {
                    throw new InvalidDataException(
                        "Unsupported arena transform version " + version + ".");
                }

                VPWGames baseGame = (VPWGames)reader.ReadInt32();
                SpecificGame gameType = (SpecificGame)reader.ReadInt32();
                UInt32 romSize = reader.ReadUInt32();
                int count = reader.ReadInt32();

                if (Program.CurrentProject == null ||
                    baseGame != VPWGames.VPW2 ||
                    baseGame != Program.CurrentProject.Settings.BaseGame ||
                    gameType != Program.CurrentProject.Settings.GameType)
                {
                    throw new InvalidDataException(
                        "The saved arena transforms belong to a different game " +
                        "or ROM revision.");
                }

                if (baseRom == null || romSize != (UInt32)baseRom.Length)
                {
                    throw new InvalidDataException(
                        "The saved arena transforms were created for a different " +
                        "base ROM size.");
                }

                if (count < 0 || count > 4096)
                {
                    throw new InvalidDataException(
                        "The arena transform count is invalid.");
                }

                for (int i = 0; i < count; i++)
                {
                    UInt32 offset = reader.ReadUInt32();
                    Int16 x = reader.ReadInt16();
                    Int16 y = reader.ReadInt16();
                    Int16 z = reader.ReadInt16();

                    if (!IsAllowedModelEntryOffset(baseRom, offset))
                    {
                        throw new InvalidDataException(
                            String.Format(
                                "Arena transform offset 0x{0:X8} is not a " +
                                "supported per-arena model-list entry.",
                                offset));
                    }

                    if (x == 0 && y == 0 && z == 0)
                    {
                        continue;
                    }

                    result[offset] =
                        new VPW2ArenaTranslation(offset, x, y, z);
                }
            }

            return result;
        }

        public static void SaveForCurrentProject(
            byte[] baseRom,
            IDictionary<UInt32, VPW2ArenaTranslation> transforms)
        {
            if (Program.CurrentProject == null ||
                Program.CurrentProject.Settings.BaseGame != VPWGames.VPW2)
            {
                throw new InvalidOperationException(
                    "VPW2 arena transforms require a VPW2 project.");
            }

            if (String.IsNullOrWhiteSpace(Program.CurProjectPath))
            {
                throw new InvalidOperationException(
                    "Save the VPWStudio project before saving arena transforms.");
            }

            string path = GetProjectPath(true);
            SortedDictionary<UInt32, VPW2ArenaTranslation> clean =
                new SortedDictionary<UInt32, VPW2ArenaTranslation>();

            if (transforms != null)
            {
                foreach (
                    KeyValuePair<UInt32, VPW2ArenaTranslation> pair
                    in transforms)
                {
                    VPW2ArenaTranslation move = pair.Value;
                    if (move == null ||
                        (move.X == 0 && move.Y == 0 && move.Z == 0))
                    {
                        continue;
                    }

                    if (!IsAllowedModelEntryOffset(baseRom, pair.Key))
                    {
                        throw new InvalidDataException(
                            String.Format(
                                "Refusing unsafe arena transform at 0x{0:X8}.",
                                pair.Key));
                    }

                    clean[pair.Key] =
                        new VPW2ArenaTranslation(
                            pair.Key,
                            move.X,
                            move.Y,
                            move.Z);
                }
            }

            if (clean.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }

            string tempPath = path + ".tmp";

            using (FileStream stream =
                new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write((Int32)VPWGames.VPW2);
                writer.Write((Int32)Program.CurrentProject.Settings.GameType);
                writer.Write((UInt32)baseRom.Length);
                writer.Write(clean.Count);

                foreach (
                    KeyValuePair<UInt32, VPW2ArenaTranslation> pair
                    in clean)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value.X);
                    writer.Write(pair.Value.Y);
                    writer.Write(pair.Value.Z);
                }

                writer.Flush();
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }

        /// <summary>
        /// Apply or remove the runtime translation hook at the very end of
        /// Build ROM, after FileTable/audio relocation has established final
        /// ROM occupancy.  Returns the number of moved per-arena instances.
        /// </summary>
        public static int ApplyRuntimePatch(List<byte> outputRom)
        {
            if (Program.CurrentProject == null ||
                Program.CurrentProject.Settings.BaseGame != VPWGames.VPW2)
            {
                return 0;
            }

            byte[] baseRom = Program.CurrentInputROM.Data;
            SortedDictionary<UInt32, VPW2ArenaTranslation> transforms =
                LoadForCurrentProject(baseRom);

            bool alreadyOurs = HasRuntimeMarker(outputRom);

            if (transforms.Count == 0)
            {
                if (alreadyOurs)
                {
                    RestoreRuntimePatch(outputRom);
                }
                return 0;
            }

            EnsureRuntimeReservation(outputRom, alreadyOurs);
            InstallHelper(outputRom);

            int cursor = RuntimeTableRomOffset;

            foreach (
                KeyValuePair<UInt32, VPW2ArenaTranslation> pair
                in transforms)
            {
                if (cursor + 12 + 4 > RuntimeMarkerRomOffset)
                {
                    throw new InvalidDataException(
                        "Too many VPW2 arena transforms for the reserved " +
                        "runtime table.");
                }

                UInt32 runtimeKey =
                    GlobalRomToRamBias + pair.Key;

                WriteU32(outputRom, cursor, runtimeKey);
                WriteS16(outputRom, cursor + 4, pair.Value.X);
                WriteS16(outputRom, cursor + 6, pair.Value.Y);
                WriteS16(outputRom, cursor + 8, pair.Value.Z);
                WriteU16(outputRom, cursor + 10, 0);
                cursor += 12;
            }

            // Zero key terminates the helper's linear scan.
            WriteU32(outputRom, cursor, 0);

            for (int i = 0; i < RuntimeMarker.Length; i++)
            {
                outputRom[RuntimeMarkerRomOffset + i] =
                    RuntimeMarker[i];
            }

            return transforms.Count;
        }

        public static bool IsAllowedModelEntryOffset(
            byte[] rom,
            UInt32 candidate)
        {
            if (rom == null || rom.Length <= 0x486D0)
            {
                return false;
            }

            for (int arena = 0; arena < 6; arena++)
            {
                int ringMatCount =
                    ReadU16(rom, 0x485EC + arena * 2);
                int ringMatModels =
                    PtrToRom(ReadU32(rom, 0x485BC + arena * 4));

                if (ContainsHalfword(
                    candidate,
                    ringMatModels,
                    ringMatCount,
                    8))
                {
                    return true;
                }

                int ringAreaCount =
                    ReadU16(rom, 0x48628 + arena * 2);
                int ringAreaModels =
                    PtrToRom(ReadU32(rom, 0x485F8 + arena * 4));

                if (ContainsHalfword(
                    candidate,
                    ringAreaModels,
                    ringAreaCount,
                    64))
                {
                    return true;
                }

                int outsideCount =
                    ReadU16(rom, 0x48664 + arena * 2);
                int outsideModels =
                    PtrToRom(ReadU32(rom, 0x48634 + arena * 4));

                if (ContainsHalfword(
                    candidate,
                    outsideModels,
                    outsideCount,
                    64))
                {
                    return true;
                }

                int sectionCounts =
                    PtrToRom(ReadU32(rom, 0x48670 + arena * 4));
                int modelPointerList =
                    PtrToRom(ReadU32(rom, 0x48688 + arena * 4));

                if (sectionCounts < 0 || modelPointerList < 0)
                {
                    continue;
                }

                for (int section = 0; section < 4; section++)
                {
                    int count = rom[sectionCounts + section];
                    int models = PtrToRom(
                        ReadU32(
                            rom,
                            modelPointerList + section * 4));

                    if (ContainsHalfword(
                        candidate,
                        models,
                        count,
                        64))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsHalfword(
            UInt32 candidate,
            int start,
            int count,
            int maximum)
        {
            if (start < 0 || count <= 0 || count > maximum)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (candidate == (UInt32)(start + i * 2))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureRuntimeReservation(
            List<byte> outputRom,
            bool alreadyOurs)
        {
            while (outputRom.Count < RuntimeTableEnd)
            {
                outputRom.Add(0xFF);
            }

            if (alreadyOurs)
            {
                for (int i = RuntimeTableRomOffset;
                    i < RuntimeTableEnd;
                    i++)
                {
                    outputRom[i] = 0xFF;
                }
                return;
            }

            for (int i = RuntimeTableRomOffset;
                i < RuntimeTableEnd;
                i++)
            {
                if (outputRom[i] != 0xFF)
                {
                    throw new InvalidDataException(
                        String.Format(
                            "VPW2 Arena Editor needs the final 64 KiB " +
                            "ROM reservation at 0x{0:X8}, but another " +
                            "project change already occupies it. " +
                            "No runtime arena movement patch was written.",
                            RuntimeTableRomOffset));
                }
            }
        }

        private static void InstallHelper(List<byte> outputRom)
        {
            UInt32 hook = ReadU32(outputRom, HookRomOffset);

            if (hook != OriginalHookWord &&
                hook != PatchedHookWord)
            {
                throw new InvalidDataException(
                    String.Format(
                        "Unexpected VPW2 arena model-loader call at 0x{0:X6}: " +
                        "0x{1:X8}.",
                        HookRomOffset,
                        hook));
            }

            ValidateCodeCave(
                outputRom,
                TrampolineRomOffset,
                TrampolineWords,
                "arena trampoline",
                "0x103C");

            ValidateCodeCave(
                outputRom,
                HelperRomOffset,
                HelperWords,
                "translation helper",
                "0x37D34");

            WriteWords(
                outputRom,
                TrampolineRomOffset,
                TrampolineWords);

            WriteWords(
                outputRom,
                HelperRomOffset,
                HelperWords);

            WriteU32(
                outputRom,
                HookRomOffset,
                PatchedHookWord);
        }

        private static void ValidateCodeCave(
            List<byte> outputRom,
            int offset,
            UInt32[] expected,
            string description,
            string displayOffset)
        {
            bool blank = true;
            bool ours = true;

            for (int i = 0; i < expected.Length; i++)
            {
                UInt32 word =
                    ReadU32(
                        outputRom,
                        offset + i * 4);

                if (word != 0)
                {
                    blank = false;
                }

                if (word != expected[i])
                {
                    ours = false;
                }
            }

            if (!blank && !ours)
            {
                throw new InvalidDataException(
                    "The verified VPW2 " + description +
                    " code cave at " + displayOffset +
                    " is no longer empty and does not contain the BAMP patch.");
            }
        }

        private static void WriteWords(
            List<byte> outputRom,
            int offset,
            UInt32[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                WriteU32(
                    outputRom,
                    offset + i * 4,
                    words[i]);
            }
        }

        private static void RestoreRuntimePatch(List<byte> outputRom)
        {
            if (outputRom.Count < RuntimeTableEnd)
            {
                return;
            }

            UInt32 hook = ReadU32(outputRom, HookRomOffset);
            if (hook == PatchedHookWord)
            {
                WriteU32(outputRom, HookRomOffset, OriginalHookWord);
            }

            ClearCodeCaveIfOurs(
                outputRom,
                TrampolineRomOffset,
                TrampolineWords);

            ClearCodeCaveIfOurs(
                outputRom,
                HelperRomOffset,
                HelperWords);

            for (int i = RuntimeTableRomOffset;
                i < RuntimeTableEnd;
                i++)
            {
                outputRom[i] = 0xFF;
            }
        }

        private static void ClearCodeCaveIfOurs(
            List<byte> outputRom,
            int offset,
            UInt32[] words)
        {
            bool ours = true;

            for (int i = 0; i < words.Length; i++)
            {
                if (ReadU32(
                        outputRom,
                        offset + i * 4) != words[i])
                {
                    ours = false;
                    break;
                }
            }

            if (!ours)
            {
                return;
            }

            for (int i = 0; i < words.Length * 4; i++)
            {
                outputRom[offset + i] = 0;
            }
        }

        private static bool HasRuntimeMarker(List<byte> rom)
        {
            if (rom == null ||
                rom.Count < RuntimeMarkerRomOffset + RuntimeMarker.Length)
            {
                return false;
            }

            for (int i = 0; i < RuntimeMarker.Length; i++)
            {
                if (rom[RuntimeMarkerRomOffset + i] != RuntimeMarker[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static UInt16 ReadU16(byte[] data, int offset)
        {
            if (offset < 0 || offset + 1 >= data.Length)
            {
                return 0;
            }

            return (UInt16)(
                (data[offset] << 8) |
                data[offset + 1]);
        }

        private static UInt32 ReadU32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 3 >= data.Length)
            {
                return 0;
            }

            return
                ((UInt32)data[offset] << 24) |
                ((UInt32)data[offset + 1] << 16) |
                ((UInt32)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static UInt32 ReadU32(List<byte> data, int offset)
        {
            if (offset < 0 || offset + 3 >= data.Count)
            {
                return 0;
            }

            return
                ((UInt32)data[offset] << 24) |
                ((UInt32)data[offset + 1] << 16) |
                ((UInt32)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static void WriteU16(
            List<byte> data,
            int offset,
            UInt16 value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        private static void WriteS16(
            List<byte> data,
            int offset,
            Int16 value)
        {
            WriteU16(data, offset, unchecked((UInt16)value));
        }

        private static void WriteU32(
            List<byte> data,
            int offset,
            UInt32 value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        private static int PtrToRom(UInt32 pointer)
        {
            if (pointer < 0x80000000 || pointer >= 0x80800000)
            {
                return -1;
            }

            return checked((int)(pointer - GlobalRomToRamBias));
        }
    }
}
