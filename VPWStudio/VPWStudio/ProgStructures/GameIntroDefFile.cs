using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VPWStudio
{
    public class GameIntroDataChunk
    {
        public uint Offset;
        public byte[] Data;

        public GameIntroDataChunk()
        {
            Data = new byte[0];
        }

        public GameIntroDataChunk(uint offset, byte[] data)
        {
            Offset = offset;
            Data = data ?? new byte[0];
        }
    }

    /// <summary>
    /// Project-side storage for the fixed-size game introduction tables.
    /// Version 2 also stores camera table entries and their pointed-to data.
    /// </summary>
    public class GameIntroDefFile
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("VPWSINT2");

        public const ushort CurrentVersion = 1;

        public VPWGames BaseGame;
        public SpecificGame GameType;

        public uint AnimationOffset;
        public uint ImageOffset;
        public uint SequenceOffset;

        public byte[] AnimationData;
        public byte[] ImageData;
        public byte[] SequenceData;

        public uint CameraOffset;
        public byte[] CameraTableData;
        public List<GameIntroDataChunk> CameraDataChunks;

        public GameIntroDefFile()
        {
            BaseGame = VPWGames.Invalid;
            GameType = SpecificGame.Invalid;

            AnimationData = new byte[0];
            ImageData = new byte[0];
            SequenceData = new byte[0];

            CameraTableData = new byte[0];
            CameraDataChunks = new List<GameIntroDataChunk>();
        }

        public GameIntroDefFile(VPWGames baseGame, SpecificGame gameType)
            : this()
        {
            BaseGame = baseGame;
            GameType = gameType;
        }

        public void ReadFile(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length)
            {
                throw new EndOfStreamException(
                    "The intro definition header is incomplete.");
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (magic[i] != Magic[i])
                {
                    throw new InvalidDataException(
                        "This is not a VPWStudio intro definition file.");
                }
            }

            ushort version = reader.ReadUInt16();
            reader.ReadUInt16();

            if (version < 1 || version > CurrentVersion)
            {
                throw new InvalidDataException(
                    String.Format(
                        "Unsupported intro definition version {0}.",
                        version));
            }

            BaseGame = (VPWGames)reader.ReadInt32();
            GameType = (SpecificGame)reader.ReadInt32();

            AnimationOffset = reader.ReadUInt32();
            ImageOffset = reader.ReadUInt32();
            SequenceOffset = reader.ReadUInt32();

            AnimationData = ReadSection(reader, "animation");
            ImageData = ReadSection(reader, "image");
            SequenceData = ReadSection(reader, "sequence");

            CameraOffset = 0;
            CameraTableData = new byte[0];
            CameraDataChunks = new List<GameIntroDataChunk>();

            if (version >= 2)
            {
                reader.ReadUInt32();
                SkipSection(reader, "camera table");

                int chunkCount = reader.ReadInt32();
                if (chunkCount < 0 || chunkCount > 100000)
                {
                    throw new InvalidDataException(
                        "The camera data chunk count is invalid.");
                }

                for (int i = 0; i < chunkCount; i++)
                {
                    reader.ReadUInt32();
                    SkipSection(
                        reader,
                        String.Format(
                            "camera chunk {0}",
                            i));
                }
            }

            CameraOffset = 0;
            CameraTableData = new byte[0];
            CameraDataChunks.Clear();

            Validate();
        }

        public void WriteFile(BinaryWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            Validate();

            writer.Write(Magic);
            writer.Write(CurrentVersion);
            writer.Write((ushort)0);

            writer.Write((int)BaseGame);
            writer.Write((int)GameType);

            writer.Write(AnimationOffset);
            writer.Write(ImageOffset);
            writer.Write(SequenceOffset);

            WriteSection(writer, AnimationData);
            WriteSection(writer, ImageData);
            WriteSection(writer, SequenceData);
        }

        public void Validate()
        {
            if (AnimationData == null ||
                ImageData == null ||
                SequenceData == null ||
                CameraTableData == null ||
                CameraDataChunks == null)
            {
                throw new InvalidDataException(
                    "Intro table data cannot be null.");
            }

            if ((AnimationData.Length % 20) != 0)
            {
                throw new InvalidDataException(
                    "Animation data length is not divisible by 20.");
            }

            if ((ImageData.Length % 16) != 0)
            {
                throw new InvalidDataException(
                    "Image data length is not divisible by 16.");
            }

            if ((SequenceData.Length % 28) != 0)
            {
                throw new InvalidDataException(
                    "Sequence data length is not divisible by 28.");
            }

            if ((CameraTableData.Length % 8) != 0)
            {
                throw new InvalidDataException(
                    "Camera table data length is not divisible by 8.");
            }

            foreach (GameIntroDataChunk chunk in CameraDataChunks)
            {
                if (chunk == null || chunk.Data == null)
                {
                    throw new InvalidDataException(
                        "A camera data chunk is missing.");
                }
            }
        }

        private static byte[] ReadSection(
            BinaryReader reader,
            string sectionName)
        {
            int length = reader.ReadInt32();

            if (length < 0)
            {
                throw new InvalidDataException(
                    String.Format(
                        "The {0} section has a negative length.",
                        sectionName));
            }

            if (reader.BaseStream.CanSeek &&
                length > reader.BaseStream.Length -
                    reader.BaseStream.Position)
            {
                throw new EndOfStreamException(
                    String.Format(
                        "The {0} section is truncated.",
                        sectionName));
            }

            byte[] data = reader.ReadBytes(length);
            if (data.Length != length)
            {
                throw new EndOfStreamException(
                    String.Format(
                        "The {0} section is truncated.",
                        sectionName));
            }

            return data;
        }

        private static void SkipSection(
            BinaryReader reader,
            string sectionName)
        {
            int length = reader.ReadInt32();

            if (length < 0)
            {
                throw new InvalidDataException(
                    String.Format(
                        "The {0} section has a negative length.",
                        sectionName));
            }

            long remaining =
                reader.BaseStream.Length -
                reader.BaseStream.Position;

            if (length > remaining)
            {
                throw new EndOfStreamException(
                    String.Format(
                        "The {0} section is truncated.",
                        sectionName));
            }

            reader.BaseStream.Seek(
                length,
                SeekOrigin.Current);
        }

        private static void WriteSection(
            BinaryWriter writer,
            byte[] data)
        {
            writer.Write(data.Length);
            writer.Write(data);
        }
    }
}
