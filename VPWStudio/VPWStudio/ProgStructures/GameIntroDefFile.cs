using System;
using System.IO;
using System.Text;

namespace VPWStudio
{
    /// <summary>
    /// Project-side storage for fixed-size game intro tables.
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

        public GameIntroDefFile()
        {
            BaseGame = VPWGames.Invalid;
            GameType = SpecificGame.Invalid;
            AnimationData = new byte[0];
            ImageData = new byte[0];
            SequenceData = new byte[0];
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
                throw new EndOfStreamException("The intro definition header is incomplete.");
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

            if (version != CurrentVersion)
            {
                throw new InvalidDataException(
                    String.Format("Unsupported intro definition version {0}.", version));
            }

            BaseGame = (VPWGames)reader.ReadInt32();
            GameType = (SpecificGame)reader.ReadInt32();
            AnimationOffset = reader.ReadUInt32();
            ImageOffset = reader.ReadUInt32();
            SequenceOffset = reader.ReadUInt32();
            AnimationData = ReadSection(reader, "animation");
            ImageData = ReadSection(reader, "image");
            SequenceData = ReadSection(reader, "sequence");
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
            if (AnimationData == null || ImageData == null || SequenceData == null)
            {
                throw new InvalidDataException("Intro table data cannot be null.");
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
        }

        private static byte[] ReadSection(BinaryReader reader, string sectionName)
        {
            int length = reader.ReadInt32();
            if (length < 0)
            {
                throw new InvalidDataException(
                    String.Format("The {0} section has a negative length.", sectionName));
            }

            if (reader.BaseStream.CanSeek &&
                length > (reader.BaseStream.Length - reader.BaseStream.Position))
            {
                throw new EndOfStreamException(
                    String.Format("The {0} section is truncated.", sectionName));
            }

            byte[] data = reader.ReadBytes(length);
            if (data.Length != length)
            {
                throw new EndOfStreamException(
                    String.Format("The {0} section is truncated.", sectionName));
            }

            return data;
        }

        private static void WriteSection(BinaryWriter writer, byte[] data)
        {
            writer.Write(data.Length);
            writer.Write(data);
        }
    }
}
