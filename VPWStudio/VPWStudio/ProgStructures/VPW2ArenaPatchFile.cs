using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VPWStudio
{
    /// <summary>
    /// Small project-side patch file for the native VPW2 Arena Editor.
    /// Only validated 16-bit arena table edits are stored.
    /// </summary>
    public static class VPW2ArenaPatchFile
    {
        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("VPWSARE1");

        private const UInt16 Version = 1;
        public const string FileName = "ArenaDefinitions.vpwsarena";

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

        public static SortedDictionary<UInt32, UInt16>
            LoadForCurrentProject(byte[] baseRom)
        {
            SortedDictionary<UInt32, UInt16> result =
                new SortedDictionary<UInt32, UInt16>();

            string path = GetProjectPath(false);

            if (String.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
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
                        "ArenaDefinitions.vpwsarena has an invalid signature.");
                }

                UInt16 version = reader.ReadUInt16();

                if (version != Version)
                {
                    throw new InvalidDataException(
                        "Unsupported arena definition file version " +
                        version + ".");
                }

                VPWGames baseGame =
                    (VPWGames)reader.ReadInt32();
                SpecificGame gameType =
                    (SpecificGame)reader.ReadInt32();
                UInt32 romSize = reader.ReadUInt32();
                int count = reader.ReadInt32();

                if (Program.CurrentProject == null ||
                    baseGame !=
                        Program.CurrentProject.Settings.BaseGame ||
                    gameType !=
                        Program.CurrentProject.Settings.GameType)
                {
                    throw new InvalidDataException(
                        "The saved arena edits belong to a different game " +
                        "or ROM revision.");
                }

                if (baseRom == null ||
                    romSize != (UInt32)baseRom.Length)
                {
                    throw new InvalidDataException(
                        "The saved arena edits were created for a different " +
                        "base ROM size.");
                }

                if (count < 0 || count > 2048)
                {
                    throw new InvalidDataException(
                        "The arena patch count is invalid.");
                }

                for (int i = 0; i < count; i++)
                {
                    UInt32 offset = reader.ReadUInt32();
                    UInt16 value = reader.ReadUInt16();

                    if (!IsAllowedArenaOffset(baseRom, offset))
                    {
                        throw new InvalidDataException(
                            String.Format(
                                "Arena patch offset 0x{0:X8} is outside " +
                                "the supported VPW2 arena tables.",
                                offset));
                    }

                    result[offset] = value;
                }
            }

            return result;
        }

        public static void SaveForCurrentProject(
            byte[] baseRom,
            IDictionary<UInt32, UInt16> edits)
        {
            if (Program.CurrentProject == null ||
                Program.CurrentProject.Settings.BaseGame != VPWGames.VPW2)
            {
                throw new InvalidOperationException(
                    "VPW2 Arena Editor requires a VPW2 project.");
            }

            if (String.IsNullOrWhiteSpace(Program.CurProjectPath))
            {
                throw new InvalidOperationException(
                    "Save the VPWStudio project before saving arena edits.");
            }

            string path = GetProjectPath(true);

            if (edits == null || edits.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            foreach (KeyValuePair<UInt32, UInt16> edit in edits)
            {
                if (!IsAllowedArenaOffset(baseRom, edit.Key))
                {
                    throw new InvalidDataException(
                        String.Format(
                            "Refusing to save unsafe arena offset 0x{0:X8}.",
                            edit.Key));
                }
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
                writer.Write(
                    (Int32)Program.CurrentProject.Settings.BaseGame);
                writer.Write(
                    (Int32)Program.CurrentProject.Settings.GameType);
                writer.Write((UInt32)baseRom.Length);
                writer.Write(edits.Count);

                SortedDictionary<UInt32, UInt16> sorted =
                    new SortedDictionary<UInt32, UInt16>();

                foreach (KeyValuePair<UInt32, UInt16> edit in edits)
                {
                    sorted[edit.Key] = edit.Value;
                }

                foreach (
                    KeyValuePair<UInt32, UInt16> edit
                    in sorted)
                {
                    writer.Write(edit.Key);
                    writer.Write(edit.Value);
                }

                writer.Flush();
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }

        public static int ApplyProjectPatches(List<byte> outputRom)
        {
            if (Program.CurrentProject == null ||
                Program.CurrentProject.Settings.BaseGame != VPWGames.VPW2)
            {
                return 0;
            }

            string path = GetProjectPath(false);

            if (String.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
            {
                return 0;
            }

            byte[] baseRom = Program.CurrentInputROM.Data;
            SortedDictionary<UInt32, UInt16> edits =
                LoadForCurrentProject(baseRom);

            foreach (KeyValuePair<UInt32, UInt16> edit in edits)
            {
                int offset = checked((int)edit.Key);

                if (offset < 0 || offset + 1 >= outputRom.Count)
                {
                    throw new InvalidDataException(
                        String.Format(
                            "Arena patch offset 0x{0:X8} exceeds the ROM.",
                            edit.Key));
                }

                outputRom[offset] =
                    (byte)(edit.Value >> 8);
                outputRom[offset + 1] =
                    (byte)(edit.Value & 0xFF);
            }

            return edits.Count;
        }

        /// <summary>
        /// Writable in the first native pass:
        /// - each arena's private LQ model IDs
        /// - each arena's private in-match material IDs
        /// - ring canvas/apron materials
        /// Shared venue/floor/guardrail data remains read-only.
        /// </summary>
                public static bool IsAllowedArenaOffset(
            byte[] rom,
            UInt32 candidate)
        {
            if (rom == null ||
                rom.Length <= 0x486D0)
            {
                return false;
            }

            for (int arena = 0; arena < 6; arena++)
            {
                int ringModels = PtrToRom(
                    ReadU32(
                        rom,
                        0x485BC + arena * 4));
                int ringMaterials = PtrToRom(
                    ReadU32(
                        rom,
                        0x485D4 + arena * 4));

                if (ringModels >= 0 &&
                    candidate == (UInt32)ringModels)
                {
                    return true;
                }

                if (ringMaterials >= 0 &&
                    candidate == (UInt32)ringMaterials)
                {
                    return true;
                }

                int ringBlockModels = PtrToRom(
                    ReadU32(
                        rom,
                        0x485F8 + arena * 4));
                int ringBlockMaterials = PtrToRom(
                    ReadU32(
                        rom,
                        0x48610 + arena * 4));

                if (ringBlockModels >= 0 &&
                    ringBlockMaterials > ringBlockModels)
                {
                    int count =
                        (ringBlockMaterials - ringBlockModels) / 2;

                    if (count > 0 && count <= 32)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (candidate ==
                                    (UInt32)(ringBlockModels + i * 2) ||
                                candidate ==
                                    (UInt32)(ringBlockMaterials + i * 2))
                            {
                                return true;
                            }
                        }
                    }
                }

                int mainCount =
                    ReadU16(
                        rom,
                        0x48628 + arena * 2);
                int mainModels = PtrToRom(
                    ReadU32(
                        rom,
                        0x48634 + arena * 4));
                int mainMaterials = PtrToRom(
                    ReadU32(
                        rom,
                        0x4864C + arena * 4));

                if (mainCount > 0 &&
                    mainCount <= 64 &&
                    mainModels >= 0 &&
                    mainMaterials >= 0)
                {
                    for (int i = 0; i < mainCount; i++)
                    {
                        if (candidate ==
                                (UInt32)(mainModels + i * 2) ||
                            candidate ==
                                (UInt32)(mainMaterials + i * 2))
                        {
                            return true;
                        }
                    }
                }

                int sectionCounts = PtrToRom(
                    ReadU32(
                        rom,
                        0x48670 + arena * 4));
                int modelPointerList = PtrToRom(
                    ReadU32(
                        rom,
                        0x48688 + arena * 4));
                int mat1PointerList = PtrToRom(
                    ReadU32(
                        rom,
                        0x486A0 + arena * 4));
                int mat2PointerList = PtrToRom(
                    ReadU32(
                        rom,
                        0x486B8 + arena * 4));

                if (sectionCounts < 0 ||
                    modelPointerList < 0 ||
                    mat1PointerList < 0 ||
                    mat2PointerList < 0)
                {
                    continue;
                }

                for (int section = 0; section < 4; section++)
                {
                    int count =
                        rom[sectionCounts + section];

                    if (count <= 0 || count > 64)
                    {
                        continue;
                    }

                    int models = PtrToRom(
                        ReadU32(
                            rom,
                            modelPointerList + section * 4));
                    int mats1 = PtrToRom(
                        ReadU32(
                            rom,
                            mat1PointerList + section * 4));
                    int mats2 = PtrToRom(
                        ReadU32(
                            rom,
                            mat2PointerList + section * 4));

                    if (models < 0 ||
                        mats1 < 0 ||
                        mats2 < 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        if (candidate ==
                                (UInt32)(models + i * 2) ||
                            candidate ==
                                (UInt32)(mats1 + i * 2) ||
                            candidate ==
                                (UInt32)(mats2 + i * 2))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static UInt16 ReadU16(
            byte[] data,
            int offset)
        {
            if (offset < 0 ||
                offset + 1 >= data.Length)
            {
                return 0;
            }

            return (UInt16)(
                (data[offset] << 8) |
                data[offset + 1]);
        }

        private static UInt32 ReadU32(byte[] data, int offset)
        {
            return
                ((UInt32)data[offset] << 24) |
                ((UInt32)data[offset + 1] << 16) |
                ((UInt32)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static int PtrToRom(UInt32 pointer)
        {
            if (pointer < 0x80000000 ||
                pointer >= 0x80800000)
            {
                return -1;
            }

            return checked((int)(pointer - 0x7FFFF400));
        }
    }
}
