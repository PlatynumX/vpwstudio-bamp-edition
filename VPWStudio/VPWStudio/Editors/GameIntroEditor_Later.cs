using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPWStudio.GameSpecific;

namespace VPWStudio
{
	/// <summary>
	/// Game introduction sequence editor for WCW/nWo Revenge and later.
	/// </summary>
	/// todo: in the future, this may handle the ending sequence for WM2K, VPW2, and No Mercy?
	public partial class GameIntroEditor_Later : Form
	{
		/// <summary>
		/// Introduction animation entries
		/// </summary>
		public List<IntroSequenceAnimation_Later> IntroAnimations;

		/// <summary>
		/// Introduction image entries
		/// </summary>
		public List<IntroSequenceGraphic_Later> IntroImages;

		/// <summary>
		/// Introduction sequence entries
		/// </summary>
		public List<IntroSequence_Later> IntroSequenceItems;

		/// <summary>
		/// Camera motion entries
		/// </summary>
		public List<CameraDef> CameraMotionDefs;

		public bool AnyChangesSubmitted = false;

		/// <summary>
		/// Starting ROM address of the animation entries.
		/// </summary>
		private uint AnimStartLocation;

		/// <summary>
		/// Starting ROM address of the image entries.
		/// </summary>
		private uint ImgStartLocation;

		/// <summary>
		/// Starting ROM address of the sequence entries.
		/// </summary>
		private uint SeqStartLocation;

		/// <summary>
		/// Starting ROM address of the camera motion data.
		/// </summary>
		private uint CameraMotionStartLocation;

		private StringBuilder StrBuilder;

		public GameIntroEditor_Later()
		{
			InitializeComponent();

			IntroAnimations = new List<IntroSequenceAnimation_Later>();
			IntroImages = new List<IntroSequenceGraphic_Later>();
			IntroSequenceItems = new List<IntroSequence_Later>();
			CameraMotionDefs = new List<CameraDef>();
			StrBuilder = new StringBuilder();

			LoadIntroData();
		}

		private void LoadIntroData()
		{
			Program.ReloadBaseRom();
			MemoryStream ms = new MemoryStream(Program.CurrentInputROM.Data);
			BinaryReader br = new BinaryReader(ms);

			bool hasAnimLocation = false;
			bool hasImageLocation = false;
			bool hasSeqLocation = false;
			bool hasCameraMotionLocation = false;

			AnimStartLocation = 0;
			int numAnims = 0;

			ImgStartLocation = 0;
			int numImages = 0;

			SeqStartLocation = 0;
			int numSeqEntries = 0;

			CameraMotionStartLocation = 0;
			int numCameraMotionDefs = 0;

			// xxx: non-image values don't take the credits sequence into account
			if (Program.CurLocationFile != null)
			{
				LocationFileEntry animEntry = Program.CurLocationFile.GetEntryFromComment(LocationFile.SpecialEntryStrings["IntroDefs_Later_Anims"]);
				if (animEntry != null)
				{
					AnimStartLocation = animEntry.Address;
					numAnims = animEntry.Length / 20;
					hasAnimLocation = true;
				}

				LocationFileEntry imgEntry = Program.CurLocationFile.GetEntryFromComment(LocationFile.SpecialEntryStrings["IntroDefs_Later_Images"]);
				if (imgEntry != null)
				{
					ImgStartLocation = imgEntry.Address;
					numImages = imgEntry.Length / 16;
					hasImageLocation = true;
				}

				LocationFileEntry seqEntry = Program.CurLocationFile.GetEntryFromComment(LocationFile.SpecialEntryStrings["IntroDefs_Later_Sequence"]);
				if (seqEntry != null)
				{
					SeqStartLocation = seqEntry.Address;
					numSeqEntries = seqEntry.Length / 28;
					hasSeqLocation = true;
				}

				LocationFileEntry camEntry = Program.CurLocationFile.GetEntryFromComment(LocationFile.SpecialEntryStrings["IntroDefs_Later_CameraMotion"]);
				if (camEntry != null)
				{
					CameraMotionStartLocation = camEntry.Address;
					numCameraMotionDefs = camEntry.Length / 8;
					hasCameraMotionLocation = true;
				}
			}

			// if no values were found in the location file, use hardcoded values
			if (!hasAnimLocation)
			{
				DefaultGameData.DefaultLocationDataEntry anims = DefaultGameData.GetEntry(Program.CurrentProject.Settings.GameType, "IntroDefs_Later_Anims");
				if (anims != null)
				{
					AnimStartLocation = anims.Offset; // 0x7C710
					numAnims = (int)(anims.Length / 20); // 218;
					hasAnimLocation = true;
				}
			}

			if (!hasImageLocation)
			{
				DefaultGameData.DefaultLocationDataEntry imgs = DefaultGameData.GetEntry(Program.CurrentProject.Settings.GameType, "IntroDefs_Later_Images");
				if (imgs != null)
				{
					ImgStartLocation = imgs.Offset; // 0x7DEA8;
					numImages = (int)(imgs.Length / 16); // 12;
					hasImageLocation = true;
				}
			}

			if (!hasSeqLocation)
			{
				DefaultGameData.DefaultLocationDataEntry seqs = DefaultGameData.GetEntry(Program.CurrentProject.Settings.GameType, "IntroDefs_Later_Sequence");
				if (seqs != null)
				{
					SeqStartLocation = seqs.Offset; // 0x7E098;
					numSeqEntries = (int)(seqs.Length / 28); // 81;
					hasSeqLocation = true;
				}
			}

			if (!hasCameraMotionLocation)
			{
                DefaultGameData.DefaultLocationDataEntry cams = DefaultGameData.GetEntry(Program.CurrentProject.Settings.GameType, "IntroDefs_Later_CameraMotion");
				if (cams != null)
				{
					CameraMotionStartLocation = cams.Offset;
					numCameraMotionDefs = (int)(cams.Length / 8);
					hasCameraMotionLocation = true;
				}
            }

			// FINALLY get to reading the damned data
			if (hasAnimLocation)
			{
				ms.Seek(AnimStartLocation, SeekOrigin.Begin);
				for (int i = 0; i < numAnims; i++)
				{
					IntroAnimations.Add(new IntroSequenceAnimation_Later(br));
				}
			}

			if(hasImageLocation)
			{
				ms.Seek(ImgStartLocation, SeekOrigin.Begin);
				for (int i = 0; i < numImages; i++)
				{
					IntroImages.Add(new IntroSequenceGraphic_Later(br));
				}
			}

			if (hasSeqLocation)
			{
				ms.Seek(SeqStartLocation, SeekOrigin.Begin);
				for (int i = 0; i < numSeqEntries; i++)
				{
					IntroSequenceItems.Add(new IntroSequence_Later(br));
				}
			}

			if (hasCameraMotionLocation)
			{
                ms.Seek(CameraMotionStartLocation, SeekOrigin.Begin);
				for (int i = 0; i < numCameraMotionDefs; i++)
				{
					CameraMotionDefs.Add(new CameraDef(br));
					cbCameraMotionList.Items.Add(string.Format("Entry {0}",i));
				}
            }


			br.Close();

			// BAMP_INTRO_EDITOR_PERSISTENCE
			if (TryLoadSavedIntroData())
			{
			    hasAnimLocation = IntroAnimations.Count > 0;
			    hasImageLocation = IntroImages.Count > 0;
			    hasSeqLocation = IntroSequenceItems.Count > 0;
			}

			PopulateRows(hasAnimLocation, hasImageLocation, hasSeqLocation);
		}

		private void PopulateRows(bool _anim, bool _img, bool _seq)
		{
			if (_anim)
			{
				dgvAnimations.Rows.Add(IntroAnimations.Count);
				for (int i = 0; i < IntroAnimations.Count; i++)
				{
					IntroSequenceAnimation_Later curAnim = IntroAnimations[i];
					dgvAnimations.Rows[i].Cells[0].Value = string.Format("{0:X4}", curAnim.WrestlerID4);
					dgvAnimations.Rows[i].Cells[1].Value = curAnim.TimingA;
					dgvAnimations.Rows[i].Cells[2].Value = string.Format("{0:X4}", curAnim.AnimationID);
					dgvAnimations.Rows[i].Cells[3].Value = curAnim.TimingB;
					dgvAnimations.Rows[i].Cells[4].Value = curAnim.XPosition;
					dgvAnimations.Rows[i].Cells[5].Value = curAnim.YPosition;
					dgvAnimations.Rows[i].Cells[6].Value = curAnim.ZPosition;
					dgvAnimations.Rows[i].Cells[7].Value = curAnim.Rotation;
					dgvAnimations.Rows[i].Cells[8].Value = string.Format("{0:X2}", curAnim.AnimFlags);
					dgvAnimations.Rows[i].Cells[9].Value = string.Format("{0:X2}", curAnim.MoveSpeed);
					dgvAnimations.Rows[i].Cells[10].Value = string.Format("{0:X2}", curAnim.Unknown);
					dgvAnimations.Rows[i].Cells[11].Value = string.Format("{0:X2}", curAnim.CostumeNum);
				}
			}

			if (_img)
			{
				dgvImages.Rows.Add(IntroImages.Count);
				for (int i = 0; i < IntroImages.Count; i++)
				{
					IntroSequenceGraphic_Later curImage = IntroImages[i];
					dgvImages.Rows[i].Cells[0].Value = string.Format("{0:X4}", curImage.FileID);
					dgvImages.Rows[i].Cells[1].Value = curImage.Width;
					dgvImages.Rows[i].Cells[2].Value = curImage.Height;
					dgvImages.Rows[i].Cells[3].Value = curImage.VertDisplacement;
					dgvImages.Rows[i].Cells[4].Value = curImage.HorizStretch;
					dgvImages.Rows[i].Cells[5].Value = string.Format("{0:X2}", curImage.Flags1);
					dgvImages.Rows[i].Cells[6].Value = curImage.ScrollSpeed;
					dgvImages.Rows[i].Cells[7].Value = string.Format("{0:X2}", curImage.Unknown);
				}
			}

			if (_seq)
			{
				dgvSequence.Rows.Add(IntroSequenceItems.Count);
				for (int i = 0; i < IntroSequenceItems.Count; i++)
				{
					IntroSequence_Later curSeq = IntroSequenceItems[i];
					dgvSequence.Rows[i].Cells[0].Value = curSeq.MainSequence;
					dgvSequence.Rows[i].Cells[1].Value = curSeq.SubSequence;
					dgvSequence.Rows[i].Cells[2].Value = string.Format("{0:X2}", curSeq.Flags);
					dgvSequence.Rows[i].Cells[3].Value = string.Format("{0:X2}", curSeq.Transition);
					dgvSequence.Rows[i].Cells[4].Value = curSeq.SceneTime;
					dgvSequence.Rows[i].Cells[5].Value = string.Format("{0:X2}", curSeq.CameraMotion);
					dgvSequence.Rows[i].Cells[6].Value = string.Format("{0:X4}", curSeq.Unknown);
					dgvSequence.Rows[i].Cells[7].Value = string.Format("{0:X4}", curSeq.StageNum);
					dgvSequence.Rows[i].Cells[8].Value = string.Format("{0:X8}", curSeq.Pointer1);
					dgvSequence.Rows[i].Cells[9].Value = string.Format("{0:X8}", curSeq.Pointer2);
					dgvSequence.Rows[i].Cells[10].Value = string.Format("{0:X8}", curSeq.Pointer3);
					dgvSequence.Rows[i].Cells[11].Value = string.Format("{0:X8}", curSeq.Pointer4);
				}
			}
		}


        private enum IntroCellValueType
        {
            DecimalByte,
            DecimalInt16,
            DecimalUInt16,
            HexByte,
            HexUInt16,
            HexUInt32
        }

        private bool TryLoadSavedIntroData()
        {
            string relativePath =
                Program.CurrentProject.Settings.GameIntroDefinitionFilePath;

            if (String.IsNullOrEmpty(relativePath))
            {
                return false;
            }

            string absolutePath = Program.ConvertRelativePath(relativePath);
            if (String.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return false;
            }

            try
            {
                GameIntroDefFile introFile = new GameIntroDefFile();

                using (FileStream stream = new FileStream(
                    absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    introFile.ReadFile(reader);
                }

                if (introFile.BaseGame != Program.CurrentProject.Settings.BaseGame ||
                    introFile.GameType != Program.CurrentProject.Settings.GameType)
                {
                    Program.WarningMessageBox(
                        "The saved intro data belongs to a different game or ROM version. " +
                        "The base ROM data will be shown instead.");
                    return false;
                }

                IntroAnimations.Clear();
                IntroImages.Clear();
                IntroSequenceItems.Clear();

                AnimStartLocation = introFile.AnimationOffset;
                ImgStartLocation = introFile.ImageOffset;
                SeqStartLocation = introFile.SequenceOffset;

                using (MemoryStream stream = new MemoryStream(introFile.AnimationData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (stream.Position < stream.Length)
                    {
                        IntroAnimations.Add(new IntroSequenceAnimation_Later(reader));
                    }
                }

                using (MemoryStream stream = new MemoryStream(introFile.ImageData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (stream.Position < stream.Length)
                    {
                        IntroImages.Add(new IntroSequenceGraphic_Later(reader));
                    }
                }

                using (MemoryStream stream = new MemoryStream(introFile.SequenceData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (stream.Position < stream.Length)
                    {
                        IntroSequenceItems.Add(new IntroSequence_Later(reader));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Program.WarningMessageBox(
                    "The saved intro data could not be loaded.\n\n" + ex.Message);
                return false;
            }
        }

        private bool TryReadEditorRows(out string errorMessage)
        {
            List<IntroSequenceAnimation_Later> animations =
                new List<IntroSequenceAnimation_Later>();
            List<IntroSequenceGraphic_Later> images =
                new List<IntroSequenceGraphic_Later>();
            List<IntroSequence_Later> sequence =
                new List<IntroSequence_Later>();

            for (int i = 0; i < dgvAnimations.Rows.Count; i++)
            {
                DataGridViewRow row = dgvAnimations.Rows[i];
                if (row.IsNewRow)
                {
                    continue;
                }

                try
                {
                    IntroSequenceAnimation_Later item =
                        new IntroSequenceAnimation_Later();

                    item.WrestlerID4 = ParseHexUInt16(CellText(row, 0), "Wrestler ID4");
                    item.TimingA = ParseDecimalInt16(CellText(row, 1), "Timing A");
                    item.AnimationID = ParseHexUInt16(CellText(row, 2), "Animation ID");
                    item.TimingB = ParseDecimalInt16(CellText(row, 3), "Timing B");
                    item.XPosition = ParseDecimalInt16(CellText(row, 4), "X Position");
                    item.YPosition = ParseDecimalInt16(CellText(row, 5), "Y Position");
                    item.ZPosition = ParseDecimalInt16(CellText(row, 6), "Z Position");
                    item.Rotation = ParseDecimalInt16(CellText(row, 7), "Rotation");
                    item.AnimFlags = ParseHexByte(CellText(row, 8), "Animation Flags");
                    item.MoveSpeed = ParseHexByte(CellText(row, 9), "Move Speed");
                    item.Unknown = ParseHexByte(CellText(row, 10), "Unknown");
                    item.CostumeNum = ParseHexByte(CellText(row, 11), "Costume Number");
                    animations.Add(item);
                }
                catch (FormatException ex)
                {
                    errorMessage = String.Format(
                        "Animation row {0}: {1}", i, ex.Message);
                    return false;
                }
            }

            for (int i = 0; i < dgvImages.Rows.Count; i++)
            {
                DataGridViewRow row = dgvImages.Rows[i];
                if (row.IsNewRow)
                {
                    continue;
                }

                try
                {
                    IntroSequenceGraphic_Later item =
                        new IntroSequenceGraphic_Later();

                    item.FileID = ParseHexUInt16(CellText(row, 0), "File ID");
                    item.Width = ParseDecimalUInt16(CellText(row, 1), "Width");
                    item.Height = ParseDecimalUInt16(CellText(row, 2), "Height");
                    item.VertDisplacement =
                        ParseDecimalUInt16(CellText(row, 3), "Vertical Displacement");
                    item.HorizStretch =
                        ParseDecimalUInt16(CellText(row, 4), "Horizontal Stretch");
                    item.Flags1 = ParseHexUInt16(CellText(row, 5), "Image Flags");
                    item.ScrollSpeed =
                        ParseDecimalInt16(CellText(row, 6), "Scroll Speed");
                    item.Unknown = ParseHexUInt16(CellText(row, 7), "Unknown");
                    images.Add(item);
                }
                catch (FormatException ex)
                {
                    errorMessage = String.Format(
                        "Image row {0}: {1}", i, ex.Message);
                    return false;
                }
            }

            for (int i = 0; i < dgvSequence.Rows.Count; i++)
            {
                DataGridViewRow row = dgvSequence.Rows[i];
                if (row.IsNewRow)
                {
                    continue;
                }

                try
                {
                    IntroSequence_Later item = new IntroSequence_Later();

                    item.MainSequence =
                        ParseDecimalByte(CellText(row, 0), "Main Sequence");
                    item.SubSequence =
                        ParseDecimalByte(CellText(row, 1), "Sub Sequence");
                    item.Flags = ParseHexByte(CellText(row, 2), "Flags");
                    item.Transition = ParseHexByte(CellText(row, 3), "Transition");
                    item.SceneTime =
                        ParseDecimalUInt16(CellText(row, 4), "Scene Time");
                    item.CameraMotion =
                        ParseHexUInt16(CellText(row, 5), "Camera Motion");
                    item.Unknown = ParseHexUInt16(CellText(row, 6), "Unknown");
                    item.StageNum = ParseHexUInt16(CellText(row, 7), "Stage Number");
                    item.Pointer1 = ParseHexUInt32(CellText(row, 8), "Pointer 1");
                    item.Pointer2 = ParseHexUInt32(CellText(row, 9), "Pointer 2");
                    item.Pointer3 = ParseHexUInt32(CellText(row, 10), "Pointer 3");
                    item.Pointer4 = ParseHexUInt32(CellText(row, 11), "Pointer 4");
                    sequence.Add(item);
                }
                catch (FormatException ex)
                {
                    errorMessage = String.Format(
                        "Sequence row {0}: {1}", i, ex.Message);
                    return false;
                }
            }

            IntroAnimations.Clear();
            IntroAnimations.AddRange(animations);
            IntroImages.Clear();
            IntroImages.AddRange(images);
            IntroSequenceItems.Clear();
            IntroSequenceItems.AddRange(sequence);

            errorMessage = String.Empty;
            return true;
        }

        private bool TrySaveIntroData(out string errorMessage)
        {
            if (String.IsNullOrEmpty(Program.CurProjectPath))
            {
                errorMessage =
                    "Save the VPWStudio project before saving intro changes.";
                return false;
            }

            try
            {
                if (String.IsNullOrEmpty(
                    Program.CurrentProject.Settings.ProjectFilesPath))
                {
                    Program.CurrentProject.Settings.ProjectFilesPath =
                        "ProjectFiles";
                }

                string relativePath = Path.Combine(
                    Program.CurrentProject.Settings.ProjectFilesPath,
                    "GameIntroDefinitions.vpwsintro");

                string absolutePath = Program.ConvertRelativePath(relativePath);
                if (String.IsNullOrEmpty(absolutePath))
                {
                    errorMessage =
                        "VPWStudio could not resolve the project files directory.";
                    return false;
                }

                string directory = Path.GetDirectoryName(absolutePath);
                if (!String.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                GameIntroDefFile introFile = new GameIntroDefFile(
                    Program.CurrentProject.Settings.BaseGame,
                    Program.CurrentProject.Settings.GameType);

                introFile.AnimationOffset = AnimStartLocation;
                introFile.ImageOffset = ImgStartLocation;
                introFile.SequenceOffset = SeqStartLocation;
                introFile.AnimationData = BuildAnimationData();
                introFile.ImageData = BuildImageData();
                introFile.SequenceData = BuildSequenceData();

                using (FileStream stream = new FileStream(
                    absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    introFile.WriteFile(writer);
                }

                Program.CurrentProject.Settings.GameIntroDefinitionFilePath =
                    relativePath;
                Program.UnsavedChanges = true;

                errorMessage = String.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private byte[] BuildAnimationData()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                foreach (IntroSequenceAnimation_Later item in IntroAnimations)
                {
                    item.WriteData(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private byte[] BuildImageData()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                foreach (IntroSequenceGraphic_Later item in IntroImages)
                {
                    item.WriteData(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private byte[] BuildSequenceData()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                foreach (IntroSequence_Later item in IntroSequenceItems)
                {
                    item.WriteData(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static string CellText(DataGridViewRow row, int columnIndex)
        {
            object value = row.Cells[columnIndex].Value;
            return value == null ? String.Empty : value.ToString().Trim();
        }

        private static string NormalizeHex(string text)
        {
            string value = text == null ? String.Empty : text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }
            return value;
        }

        private static byte ParseDecimalByte(string text, string fieldName)
        {
            byte value;
            if (!Byte.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value))
            {
                throw InvalidValue(fieldName, "decimal byte (0 to 255)");
            }
            return value;
        }

        private static short ParseDecimalInt16(string text, string fieldName)
        {
            short value;
            if (!Int16.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value))
            {
                throw InvalidValue(fieldName, "decimal signed 16-bit value");
            }
            return value;
        }

        private static ushort ParseDecimalUInt16(string text, string fieldName)
        {
            ushort value;
            if (!UInt16.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value))
            {
                throw InvalidValue(fieldName, "decimal value from 0 to 65535");
            }
            return value;
        }

        private static byte ParseHexByte(string text, string fieldName)
        {
            byte value;
            if (!Byte.TryParse(NormalizeHex(text), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value))
            {
                throw InvalidValue(fieldName, "hex byte (00 to FF)");
            }
            return value;
        }

        private static ushort ParseHexUInt16(string text, string fieldName)
        {
            ushort value;
            if (!UInt16.TryParse(NormalizeHex(text), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value))
            {
                throw InvalidValue(fieldName, "hex 16-bit value (0000 to FFFF)");
            }
            return value;
        }

        private static uint ParseHexUInt32(string text, string fieldName)
        {
            uint value;
            if (!UInt32.TryParse(NormalizeHex(text), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value))
            {
                throw InvalidValue(
                    fieldName, "hex 32-bit value (00000000 to FFFFFFFF)");
            }
            return value;
        }

        private static FormatException InvalidValue(
            string fieldName, string expectedFormat)
        {
            return new FormatException(
                String.Format("{0} must be a {1}.", fieldName, expectedFormat));
        }

        private void ValidateCell(
            DataGridView grid,
            DataGridViewCellValidatingEventArgs e,
            IntroCellValueType valueType)
        {
            string fieldName = grid.Columns[e.ColumnIndex].HeaderText;
            string value = e.FormattedValue == null
                ? String.Empty
                : e.FormattedValue.ToString();

            try
            {
                switch (valueType)
                {
                    case IntroCellValueType.DecimalByte:
                        ParseDecimalByte(value, fieldName);
                        break;
                    case IntroCellValueType.DecimalInt16:
                        ParseDecimalInt16(value, fieldName);
                        break;
                    case IntroCellValueType.DecimalUInt16:
                        ParseDecimalUInt16(value, fieldName);
                        break;
                    case IntroCellValueType.HexByte:
                        ParseHexByte(value, fieldName);
                        break;
                    case IntroCellValueType.HexUInt16:
                        ParseHexUInt16(value, fieldName);
                        break;
                    case IntroCellValueType.HexUInt32:
                        ParseHexUInt32(value, fieldName);
                        break;
                }

                grid.Rows[e.RowIndex].ErrorText = String.Empty;
            }
            catch (FormatException ex)
            {
                grid.Rows[e.RowIndex].ErrorText = ex.Message;
                e.Cancel = true;
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            dgvAnimations.EndEdit();
            dgvImages.EndEdit();
            dgvSequence.EndEdit();

            string errorMessage;
            if (!TryReadEditorRows(out errorMessage))
            {
                Program.ErrorMessageBox(errorMessage);
                return;
            }

            if (!TrySaveIntroData(out errorMessage))
            {
                Program.ErrorMessageBox(
                    "The intro changes could not be saved.\n\n" + errorMessage);
                return;
            }

            AnyChangesSubmitted = true;
            DialogResult = DialogResult.OK;
            Close();
        }


		private void buttonCancel_Click(object sender, EventArgs e)
		{
			AnyChangesSubmitted = false; // shouldn't need this but just in case
			DialogResult = DialogResult.Cancel;
			Close();
		}

        private void dgvAnimations_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            IntroCellValueType valueType;

            if (e.ColumnIndex == 0 || e.ColumnIndex == 2)
            {
                valueType = IntroCellValueType.HexUInt16;
            }
            else if (e.ColumnIndex >= 8)
            {
                valueType = IntroCellValueType.HexByte;
            }
            else
            {
                valueType = IntroCellValueType.DecimalInt16;
            }

            ValidateCell(dgvAnimations, e, valueType);
        }

        private void dgvImages_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            IntroCellValueType valueType;

            if (e.ColumnIndex == 0 ||
                e.ColumnIndex == 5 ||
                e.ColumnIndex == 7)
            {
                valueType = IntroCellValueType.HexUInt16;
            }
            else if (e.ColumnIndex == 6)
            {
                valueType = IntroCellValueType.DecimalInt16;
            }
            else
            {
                valueType = IntroCellValueType.DecimalUInt16;
            }

            ValidateCell(dgvImages, e, valueType);
        }

        private void dgvSequence_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            IntroCellValueType valueType;

            if (e.ColumnIndex <= 1)
            {
                valueType = IntroCellValueType.DecimalByte;
            }
            else if (e.ColumnIndex <= 3)
            {
                valueType = IntroCellValueType.HexByte;
            }
            else if (e.ColumnIndex == 4)
            {
                valueType = IntroCellValueType.DecimalUInt16;
            }
            else if (e.ColumnIndex <= 7)
            {
                valueType = IntroCellValueType.HexUInt16;
            }
            else
            {
                valueType = IntroCellValueType.HexUInt32;
            }

            ValidateCell(dgvSequence, e, valueType);
        }

		private void btnReloadRom_Click(object sender, EventArgs e)
		{
			IntroAnimations.Clear();
			IntroImages.Clear();
			IntroSequenceItems.Clear();

			dgvAnimations.Rows.Clear();
			dgvImages.Rows.Clear();
			dgvSequence.Rows.Clear();
			LoadIntroData();
		}

		// todo: properly calculate the offsets

		private void dgvAnimations_SelectionChanged(object sender, EventArgs e)
		{
			if (dgvAnimations.SelectedCells.Count <= 0)
			{
				tsslblCurAddressAnim.Text = "No Anim. cell selected";
			}
			else
			{
				// each row is 20 bytes
				// most entries are 2 bytes, except for the 4 bytes at the end (cols 8-11)
				int offset = dgvAnimations.SelectedCells[0].RowIndex * 20;
				if (dgvAnimations.SelectedCells[0].ColumnIndex >= 8)
				{
					// fixerator
					offset += 16 + (dgvAnimations.SelectedCells[0].ColumnIndex-8);
				}
				else
				{
					// 2 bytes
					offset += (dgvAnimations.SelectedCells[0].ColumnIndex*2);
				}

				tsslblCurAddressAnim.Text = String.Format("Anim. ROM Address: 0x{0:X}", AnimStartLocation+offset);
			}
		}

		private void dgvImages_SelectionChanged(object sender, EventArgs e)
		{
			if (dgvImages.SelectedCells.Count <= 0)
			{
				tsslblCurAddressImg.Text = "No Image cell selected";
			}
			else
			{
				// each row is 16 bytes, each entry is 2 bytes
				int offset = (dgvImages.SelectedCells[0].RowIndex * 16) + (dgvImages.SelectedCells[0].ColumnIndex * 2);
				tsslblCurAddressImg.Text = String.Format("Image ROM Address: 0x{0:X}", ImgStartLocation+offset);
			}
		}

		private void dgvSequence_SelectionChanged(object sender, EventArgs e)
		{
			if (dgvSequence.SelectedCells.Count <= 0)
			{
				tsslblCurAddressSeq.Text = "No Seq. cell selected";
			}
			else
			{
				// each row is 28 bytes
				int offset = dgvSequence.SelectedCells[0].RowIndex * 28;

				if (dgvSequence.SelectedCells[0].ColumnIndex <= 3)
				{
					// 4 bytes (cols 0-3)
					offset += dgvSequence.SelectedCells[0].ColumnIndex;
				}
				else if (dgvSequence.SelectedCells[0].ColumnIndex <= 7)
				{
					// add 4 bytes; 4*2 bytes (cols 4-7)
					offset += 4 + ((dgvSequence.SelectedCells[0].ColumnIndex-4)*2);
				}
				else
				{
					// add 12 bytes; 4*4 bytes (cols 8-11)
					offset += 12 + ((dgvSequence.SelectedCells[0].ColumnIndex - 8) * 4);
				}

				tsslblCurAddressSeq.Text = String.Format("Seq. ROM Address: 0x{0:X}", SeqStartLocation+offset);
			}
		}

        private void cbCameraMotionList_SelectedIndexChanged(object sender, EventArgs e)
        {
			if (cbCameraMotionList.SelectedIndex < 0)
			{
				return;
			}

			int index = cbCameraMotionList.SelectedIndex;
			StrBuilder.Clear();
			StrBuilder.AppendLine(string.Format("Camera Motion Entry #{0} (Z64 ROM addr 0x{1:X})", index, CameraMotionStartLocation + (8*index)));

			StrBuilder.AppendLine(string.Format("Data Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].DataPointer, Program.PointerToRomAddr(CameraMotionDefs[index].DataPointer, 1)));
            StrBuilder.AppendLine(string.Format("Unknown Value: 0x{0:X4}", CameraMotionDefs[index].UnknownValue));
            StrBuilder.AppendLine(string.Format("Camera Motion ID: 0x{0:X4}", CameraMotionDefs[index].ID));
            StrBuilder.AppendLine();

            StrBuilder.AppendLine(string.Format("X Values Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].ValuePointerX, Program.PointerToRomAddr(CameraMotionDefs[index].ValuePointerX, 1)));
			foreach (CameraValuePair cvp in CameraMotionDefs[index].X)
			{
				StrBuilder.AppendLine(string.Format("value 0x{0:X2} ({0}) at frame 0x{1:X2} ({1})", cvp.Value, cvp.FrameNumber));
			}
            StrBuilder.AppendLine();

            StrBuilder.AppendLine(string.Format("Y Values Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].ValuePointerY, Program.PointerToRomAddr(CameraMotionDefs[index].ValuePointerY, 1)));
            foreach (CameraValuePair cvp in CameraMotionDefs[index].Y)
            {
                StrBuilder.AppendLine(string.Format("value 0x{0:X2} ({0}) at frame 0x{1:X2} ({1})", cvp.Value, cvp.FrameNumber));
            }
            StrBuilder.AppendLine();

            StrBuilder.AppendLine(string.Format("Z Values Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].ValuePointerZ, Program.PointerToRomAddr(CameraMotionDefs[index].ValuePointerZ, 1)));
            foreach (CameraValuePair cvp in CameraMotionDefs[index].Z)
            {
                StrBuilder.AppendLine(string.Format("value 0x{0:X2} ({0}) at frame 0x{1:X2} ({1})", cvp.Value, cvp.FrameNumber));
            }
            StrBuilder.AppendLine();

            StrBuilder.AppendLine(string.Format("Pitch Values Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].ValuePointerPitch, Program.PointerToRomAddr(CameraMotionDefs[index].ValuePointerPitch, 1)));
            foreach (CameraValuePair cvp in CameraMotionDefs[index].Pitch)
            {
                StrBuilder.AppendLine(string.Format("value 0x{0:X2} ({0}) at frame 0x{1:X2} ({1})", cvp.Value, cvp.FrameNumber));
            }
            StrBuilder.AppendLine();

            StrBuilder.AppendLine(string.Format("Pan Values Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].ValuePointerPan, Program.PointerToRomAddr(CameraMotionDefs[index].ValuePointerPan, 1)));
            foreach (CameraValuePair cvp in CameraMotionDefs[index].Pan)
            {
                StrBuilder.AppendLine(string.Format("value 0x{0:X2} ({0}) at frame 0x{1:X2} ({1})", cvp.Value, cvp.FrameNumber));
            }
            StrBuilder.AppendLine();

            StrBuilder.AppendLine(string.Format("Roll Values Pointer: 0x{0:X} (Z64 ROM addr 0x{1:X})", CameraMotionDefs[index].ValuePointerRoll, Program.PointerToRomAddr(CameraMotionDefs[index].ValuePointerRoll, 1)));
            foreach (CameraValuePair cvp in CameraMotionDefs[index].Roll)
            {
                StrBuilder.AppendLine(string.Format("value 0x{0:X2} ({0}) at frame 0x{1:X2} ({1})", cvp.Value, cvp.FrameNumber));
            }

            tbCameraMotion.Text = StrBuilder.ToString();
        }
    }
}
