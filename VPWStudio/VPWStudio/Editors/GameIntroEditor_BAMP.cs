using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using VPWStudio.GameSpecific;

namespace VPWStudio
{
    public partial class GameIntroEditor_Later
    {
        private sealed class IntroChoice
        {
            public ushort Value;
            public string Label;

            public IntroChoice(ushort value, string label)
            {
                Value = value;
                Label = label;
            }

            public override string ToString()
            {
                return Label;
            }
        }

        private readonly Dictionary<DataGridView, object[]> BampCopiedRows =
            new Dictionary<DataGridView, object[]>();

        private readonly Dictionary<string, int> BampCameraCapacities =
            new Dictionary<string, int>();

        private List<IntroSequenceAnimation_Later> BampBaseAnimations =
            new List<IntroSequenceAnimation_Later>();

        private List<IntroSequenceGraphic_Later> BampBaseImages =
            new List<IntroSequenceGraphic_Later>();

        private List<IntroSequence_Later> BampBaseSequence =
            new List<IntroSequence_Later>();

        private List<CameraDef> BampBaseCameraDefs =
            new List<CameraDef>();

        private readonly Dictionary<ushort, string> BampWrestlerNames =
            new Dictionary<ushort, string>();

        private ComboBox BampWrestlerPicker;
        private ComboBox BampAnimationPicker;
        private ComboBox BampSequenceCameraPicker;

        private ComboBox BampCameraAxisPicker;
        private TextBox BampCameraId;
        private TextBox BampCameraUnknown;
        private Label BampCameraPointerLabel;
        private DataGridView BampCameraGrid;

        private int BampAnimationCapacity;
        private int BampImageCapacity;
        private int BampSequenceCapacity;

        private int BampCurrentCameraEntry = -1;
        private string BampCurrentCameraAxis = "X";
        private bool BampUpdatingUi;

        private ToolStripStatusLabel BampAnimationSemanticStatus;

        private void InitializeBampEditor()
        {
            Text = "Game Introduction Editor - BAMP Edition";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new Size(900, 560);
            ClientSize = new Size(1180, 720);

            tabControl1.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Bottom |
                AnchorStyles.Left |
                AnchorStyles.Right;

            tabControl1.Location = new Point(12, 12);
            tabControl1.Size = new Size(
                ClientSize.Width - 24,
                ClientSize.Height - 92);

            buttonOK.Anchor =
                AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor =
                AnchorStyles.Bottom | AnchorStyles.Right;
            btnReloadRom.Anchor =
                AnchorStyles.Bottom | AnchorStyles.Left;

            buttonOK.Location = new Point(
                ClientSize.Width - 168,
                ClientSize.Height - 64);

            buttonCancel.Location = new Point(
                ClientSize.Width - 87,
                ClientSize.Height - 64);

            btnReloadRom.Location = new Point(
                12,
                ClientSize.Height - 64);

            statusStrip1.Dock = DockStyle.Bottom;

            ConfigureFixedGrid(
                tabPage1,
                dgvAnimations,
                12,
                "Animations");

            ConfigureFixedGrid(
                tabPage2,
                dgvImages,
                8,
                "Images");

            ConfigureFixedGrid(
                tabPage3,
                dgvSequence,
                12,
                "Sequence");

            AddDerivedColumns();
            ConfigureAnimationSelectors();
            ConfigureAnimationSemanticUi();
            DisableBampCameraFeatures();

            dgvAnimations.SelectionChanged +=
                BampAnimationSelectionChanged;
            dgvAnimations.CellEndEdit +=
                BampAnimationCellEndEdit;
            dgvAnimations.CellValidating +=
                BampAnimationCellValidating;

            dgvImages.CellEndEdit +=
                BampImageCellEndEdit;

            dgvSequence.SelectionChanged +=
                BampSequenceSelectionChanged;
            dgvSequence.CellEndEdit +=
                BampSequenceCellEndEdit;
        }

        private void ConfigureFixedGrid(
            TabPage page,
            DataGridView grid,
            int rawColumnCount,
            string tableName)
        {
            grid.Dock = DockStyle.Fill;
            grid.RowHeadersVisible = true;
            grid.RowHeadersWidth = 52;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Top;
            tools.Height = 34;
            tools.Padding = new Padding(3);
            tools.WrapContents = false;

            tools.Controls.Add(
                MakeGridButton(
                    "Copy Row",
                    grid,
                    BampCopyRow));

            tools.Controls.Add(
                MakeGridButton(
                    "Paste Row",
                    grid,
                    BampPasteRow));

            tools.Controls.Add(
                MakeGridButton(
                    "Duplicate → Next",
                    grid,
                    BampDuplicateNext));

            tools.Controls.Add(
                MakeGridButton(
                    "Restore ROM Row",
                    grid,
                    BampRestoreRomRow));

            tools.Controls.Add(
                MakeGridButton(
                    "Move Up",
                    grid,
                    BampMoveRowUp));

            tools.Controls.Add(
                MakeGridButton(
                    "Move Down",
                    grid,
                    BampMoveRowDown));

            Label note = new Label();
            note.AutoSize = true;
            note.Padding = new Padding(12, 7, 0, 0);
            note.Text =
                tableName +
                " use fixed ROM slots; row tools never expand the table.";
            tools.Controls.Add(note);

            page.Controls.Add(grid);
            page.Controls.Add(tools);
            tools.BringToFront();

            grid.Tag = rawColumnCount;
        }

        private Button MakeGridButton(
            string text,
            DataGridView grid,
            EventHandler handler)
        {
            Button button = new Button();
            button.AutoSize = true;
            button.Text = text;
            button.Tag = grid;
            button.Click += handler;
            return button;
        }

        private void AddDerivedColumns()
        {
            if (!dgvAnimations.Columns.Contains(
                "BampWrestlerName"))
            {
                DataGridViewTextBoxColumn column =
                    new DataGridViewTextBoxColumn();

                column.Name = "BampWrestlerName";
                column.HeaderText = "Wrestler";
                column.ReadOnly = true;
                column.AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.AllCells;

                dgvAnimations.Columns.Add(column);
            }

            if (!dgvAnimations.Columns.Contains(
                "BampAnimationLabel"))
            {
                DataGridViewTextBoxColumn column =
                    new DataGridViewTextBoxColumn();

                column.Name = "BampAnimationLabel";
                column.HeaderText = "Animation";
                column.ReadOnly = true;
                column.AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.AllCells;

                dgvAnimations.Columns.Add(column);
            }

            if (!dgvImages.Columns.Contains("BampFileLabel"))
            {
                DataGridViewTextBoxColumn column =
                    new DataGridViewTextBoxColumn();

                column.Name = "BampFileLabel";
                column.HeaderText = "File Description";
                column.ReadOnly = true;
                column.AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.Fill;

                dgvImages.Columns.Add(column);
            }

            if (!dgvSequence.Columns.Contains(
                "BampSequenceReferences"))
            {
                DataGridViewTextBoxColumn column =
                    new DataGridViewTextBoxColumn();

                column.Name = "BampSequenceReferences";
                column.HeaderText = "Resolved References";
                column.ReadOnly = true;
                column.AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.Fill;

                dgvSequence.Columns.Add(column);
            }
        }

        private FlowLayoutPanel FindToolPanel(TabPage page)
        {
            foreach (Control control in page.Controls)
            {
                FlowLayoutPanel panel =
                    control as FlowLayoutPanel;

                if (panel != null)
                {
                    return panel;
                }
            }

            return null;
        }

        private void ConfigureAnimationSelectors()
        {
            FlowLayoutPanel tools = FindToolPanel(tabPage1);
            if (tools == null)
            {
                return;
            }

            Label wrestlerLabel = new Label();
            wrestlerLabel.AutoSize = true;
            wrestlerLabel.Padding =
                new Padding(12, 7, 0, 0);
            wrestlerLabel.Text = "Wrestler:";
            tools.Controls.Add(wrestlerLabel);

            BampWrestlerPicker = new ComboBox();
            BampWrestlerPicker.DropDownStyle =
                ComboBoxStyle.DropDownList;
            BampWrestlerPicker.Width = 280;
            BampWrestlerPicker.SelectedIndexChanged +=
                BampWrestlerPickerChanged;
            tools.Controls.Add(BampWrestlerPicker);

            Label animationLabel = new Label();
            animationLabel.AutoSize = true;
            animationLabel.Padding =
                new Padding(8, 7, 0, 0);
            animationLabel.Text = "Animation:";
            tools.Controls.Add(animationLabel);

            BampAnimationPicker = new ComboBox();
            BampAnimationPicker.DropDownStyle =
                ComboBoxStyle.DropDownList;
            BampAnimationPicker.Width = 190;
            BampAnimationPicker.SelectedIndexChanged +=
                BampAnimationPickerChanged;
            tools.Controls.Add(BampAnimationPicker);
        }

        private void DisableBampCameraFeatures()
        {
            CameraMotionDefs.Clear();

            // Camera editing is disabled, but legacy picker-refresh code
            // still expects this field to exist. Keep a detached, hidden
            // control so opening the editor cannot throw a null reference.
            if (BampSequenceCameraPicker == null)
            {
                BampSequenceCameraPicker = new ComboBox();
                BampSequenceCameraPicker.Enabled = false;
                BampSequenceCameraPicker.Visible = false;
            }

            if (tabControl1.TabPages.Contains(tabPage4))
            {
                tabControl1.TabPages.Remove(tabPage4);
            }

            if (cbCameraMotionList != null)
            {
                cbCameraMotionList.Enabled = false;
                cbCameraMotionList.Visible = false;
            }

            // Sequence column 6 is the camera-motion ID. Keep its ROM
            // value intact, but remove it from the editor completely.
            if (dgvSequence.Columns.Count > 6)
            {
                dgvSequence.Columns[6].ReadOnly = true;
                dgvSequence.Columns[6].Visible = false;
            }
        }

        private void ConfigureSequenceSelector()
        {
            FlowLayoutPanel tools = FindToolPanel(tabPage3);
            if (tools == null)
            {
                return;
            }

            Label cameraLabel = new Label();
            cameraLabel.AutoSize = true;
            cameraLabel.Padding =
                new Padding(12, 7, 0, 0);
            cameraLabel.Text = "Camera:";
            tools.Controls.Add(cameraLabel);

            BampSequenceCameraPicker = new ComboBox();
            BampSequenceCameraPicker.DropDownStyle =
                ComboBoxStyle.DropDownList;
            BampSequenceCameraPicker.Width = 180;
            BampSequenceCameraPicker.SelectedIndexChanged +=
                BampSequenceCameraPickerChanged;
            tools.Controls.Add(BampSequenceCameraPicker);
        }

        private void ConfigureCameraEditor()
        {
            tabPage4.Controls.Clear();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 3;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 38));

            FlowLayoutPanel header = new FlowLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.Padding = new Padding(6);
            header.WrapContents = true;

            Label entryLabel = new Label();
            entryLabel.Text = "Camera Entry:";
            entryLabel.AutoSize = true;
            entryLabel.Padding = new Padding(0, 7, 0, 0);
            header.Controls.Add(entryLabel);

            cbCameraMotionList.Width = 180;
            cbCameraMotionList.DropDownStyle =
                ComboBoxStyle.DropDownList;
            header.Controls.Add(cbCameraMotionList);

            Label axisLabel = new Label();
            axisLabel.Text = "Axis:";
            axisLabel.AutoSize = true;
            axisLabel.Padding = new Padding(8, 7, 0, 0);
            header.Controls.Add(axisLabel);

            BampCameraAxisPicker = new ComboBox();
            BampCameraAxisPicker.DropDownStyle =
                ComboBoxStyle.DropDownList;
            BampCameraAxisPicker.Width = 90;
            BampCameraAxisPicker.Items.AddRange(
                new object[]
                {
                    "X",
                    "Y",
                    "Z",
                    "Pitch",
                    "Pan",
                    "Roll"
                });

            BampCameraAxisPicker.SelectedIndexChanged +=
                BampCameraAxisChanged;
            header.Controls.Add(BampCameraAxisPicker);

            Label idLabel = new Label();
            idLabel.Text = "ID (hex):";
            idLabel.AutoSize = true;
            idLabel.Padding = new Padding(8, 7, 0, 0);
            header.Controls.Add(idLabel);

            BampCameraId = new TextBox();
            BampCameraId.Width = 60;
            header.Controls.Add(BampCameraId);

            Label unknownLabel = new Label();
            unknownLabel.Text = "Unknown (hex):";
            unknownLabel.AutoSize = true;
            unknownLabel.Padding =
                new Padding(8, 7, 0, 0);
            header.Controls.Add(unknownLabel);

            BampCameraUnknown = new TextBox();
            BampCameraUnknown.Width = 60;
            header.Controls.Add(BampCameraUnknown);

            BampCameraPointerLabel = new Label();
            BampCameraPointerLabel.AutoSize = true;
            BampCameraPointerLabel.Padding =
                new Padding(8, 7, 0, 0);
            header.Controls.Add(BampCameraPointerLabel);

            BampCameraGrid = new DataGridView();
            BampCameraGrid.Dock = DockStyle.Fill;
            BampCameraGrid.AllowUserToAddRows = false;
            BampCameraGrid.AllowUserToDeleteRows = false;
            BampCameraGrid.AllowUserToResizeRows = false;
            BampCameraGrid.RowHeadersVisible = true;
            BampCameraGrid.RowHeadersWidth = 52;
            BampCameraGrid.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect;
            BampCameraGrid.MultiSelect = false;
            BampCameraGrid.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            DataGridViewTextBoxColumn valueColumn =
                new DataGridViewTextBoxColumn();
            valueColumn.Name = "BampCameraValue";
            valueColumn.HeaderText = "Value (signed decimal)";

            DataGridViewTextBoxColumn frameColumn =
                new DataGridViewTextBoxColumn();
            frameColumn.Name = "BampCameraFrame";
            frameColumn.HeaderText =
                "Frame (signed decimal; 32767 terminates)";

            BampCameraGrid.Columns.Add(valueColumn);
            BampCameraGrid.Columns.Add(frameColumn);
            BampCameraGrid.CellValidating +=
                BampCameraCellValidating;

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.Padding = new Padding(4);
            tools.WrapContents = false;

            tools.Controls.Add(
                MakeCameraButton(
                    "Add Pair",
                    BampCameraAddPair));

            tools.Controls.Add(
                MakeCameraButton(
                    "Delete Pair",
                    BampCameraDeletePair));

            tools.Controls.Add(
                MakeCameraButton(
                    "Move Up",
                    BampCameraMovePairUp));

            tools.Controls.Add(
                MakeCameraButton(
                    "Move Down",
                    BampCameraMovePairDown));

            tools.Controls.Add(
                MakeCameraButton(
                    "Restore ROM Axis",
                    BampCameraRestoreAxis));

            Label safety = new Label();
            safety.AutoSize = true;
            safety.Padding = new Padding(12, 7, 0, 0);
            safety.Text =
                "Pointers remain read-only; pair additions are capped " +
                "at the original ROM allocation.";
            tools.Controls.Add(safety);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(BampCameraGrid, 0, 1);
            layout.Controls.Add(tools, 0, 2);

            tabPage4.Controls.Add(layout);
        }

        private Button MakeCameraButton(
            string text,
            EventHandler handler)
        {
            Button button = new Button();
            button.AutoSize = true;
            button.Text = text;
            button.Click += handler;
            return button;
        }

                private void ResetBampEditorUiBeforeLoad()
        {
            BampUpdatingUi = true;

            IntroAnimations.Clear();
            IntroImages.Clear();
            IntroSequenceItems.Clear();
            CameraMotionDefs.Clear();

            dgvAnimations.Rows.Clear();
            dgvImages.Rows.Clear();
            dgvSequence.Rows.Clear();

            BampUpdatingUi = false;
        }

                private void CaptureBampBaseCapacities()
        {
            BampAnimationCapacity = IntroAnimations.Count;
            BampImageCapacity = IntroImages.Count;
            BampSequenceCapacity = IntroSequenceItems.Count;

            BampBaseAnimations =
                CloneAnimations(IntroAnimations);
            BampBaseImages =
                CloneImages(IntroImages);
            BampBaseSequence =
                CloneSequence(IntroSequenceItems);

            CameraMotionDefs.Clear();
            BampBaseCameraDefs.Clear();
            BampCameraCapacities.Clear();
        }

                private void PrepareBampEditorAfterLoad()
        {
            CameraMotionDefs.Clear();

            LoadBampWrestlerNames();
            PopulateBampPickers();
            RefreshAllBampLabels();
            RefreshRowHeaders();

            SyncAnimationPickersToSelection();
            UpdateBampAnimationSemanticStatus();
        }

        private void RefreshRowHeaders()
        {
            SetRowHeaders(dgvAnimations);
            SetRowHeaders(dgvImages);
            SetRowHeaders(dgvSequence);

            if (BampCameraGrid != null)
            {
                SetRowHeaders(BampCameraGrid);
            }
        }

        private static void SetRowHeaders(DataGridView grid)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                grid.Rows[i].HeaderCell.Value =
                    i.ToString("X2");
            }
        }

                private void LoadBampWrestlerNames()
        {
            BampWrestlerNames.Clear();

            try
            {
                string relativePath =
                    Program.CurrentProject.Settings
                    .WrestlerNameFilePath;

                if (!String.IsNullOrWhiteSpace(relativePath))
                {
                    string path =
                        Program.ConvertRelativePath(relativePath);

                    if (!String.IsNullOrWhiteSpace(path) &&
                        File.Exists(path))
                    {
                        foreach (string rawLine in File.ReadAllLines(path))
                        {
                            string line =
                                rawLine == null
                                ? String.Empty
                                : rawLine.Trim();

                            if (line.Length == 0 ||
                                line.StartsWith("#"))
                            {
                                continue;
                            }

                            try
                            {
                                WrestlerNameEntry entry =
                                    new WrestlerNameEntry(line);

                                string name =
                                    !String.IsNullOrWhiteSpace(
                                        entry.LongName)
                                    ? entry.LongName.Trim()
                                    : (
                                        !String.IsNullOrWhiteSpace(
                                            entry.ShortName)
                                        ? entry.ShortName.Trim()
                                        : String.Empty
                                    );

                                if (entry.ID4 != 0 &&
                                    name.Length > 0)
                                {
                                    BampWrestlerNames[entry.ID4] =
                                        name;
                                }
                            }
                            catch
                            {
                                // Ignore one malformed name line instead of
                                // discarding every translated wrestler name.
                            }
                        }
                    }
                }
            }
            catch
            {
                // Keep valid names already loaded before the failure.
            }

            foreach (
                IntroSequenceAnimation_Later animation
                in IntroAnimations)
            {
                if (!BampWrestlerNames.ContainsKey(
                    animation.WrestlerID4))
                {
                    BampWrestlerNames.Add(
                        animation.WrestlerID4,
                        String.Format(
                            "Unknown Wrestler",
                            animation.WrestlerID4));
                }
            }
        }

        private void PopulateBampPickers()
        {
            BampUpdatingUi = true;

            BampWrestlerPicker.Items.Clear();
            List<IntroChoice> wrestlerChoices =
                new List<IntroChoice>();

            foreach (
                KeyValuePair<ushort, string> pair
                in BampWrestlerNames)
            {
                wrestlerChoices.Add(
                    new IntroChoice(
                        pair.Key,
                        String.Format("{0} /{1:X4}", pair.Value, pair.Key)));
            }

            wrestlerChoices.Sort(
                delegate(IntroChoice left, IntroChoice right)
                {
                    return String.Compare(
                        left.Label,
                        right.Label,
                        StringComparison.OrdinalIgnoreCase);
                });

            foreach (IntroChoice choice in wrestlerChoices)
            {
                BampWrestlerPicker.Items.Add(choice);
            }

            BampAnimationPicker.Items.Clear();
            Dictionary<ushort, bool> animationIds =
                new Dictionary<ushort, bool>();

            foreach (
                IntroSequenceAnimation_Later animation
                in IntroAnimations)
            {
                if (!animationIds.ContainsKey(
                    animation.AnimationID))
                {
                    animationIds.Add(
                        animation.AnimationID,
                        true);
                }
            }

            List<IntroChoice> animationChoices =
                new List<IntroChoice>();

            foreach (ushort id in animationIds.Keys)
            {
                animationChoices.Add(
                    new IntroChoice(
                        id,
                        String.Format(
                            "{0} [0x{1:X4}]",
                            GetBampAnimationLabel(id),
                            id)));
            }

            animationChoices.Sort(
                delegate(IntroChoice left, IntroChoice right)
                {
                    return left.Value.CompareTo(right.Value);
                });

            foreach (IntroChoice choice in animationChoices)
            {
                BampAnimationPicker.Items.Add(choice);
            }

            BampSequenceCameraPicker.Items.Clear();
            foreach (CameraDef camera in CameraMotionDefs)
            {
                BampSequenceCameraPicker.Items.Add(
                    new IntroChoice(
                        camera.ID,
                        String.Format(
                            "Camera 0x{0:X4}",
                            camera.ID)));
            }

            BampUpdatingUi = false;
        }

        private string GetBampWrestlerName(ushort id4)
        {
            string name;
            if (BampWrestlerNames.TryGetValue(id4, out name))
            {
                return name;
            }

            return String.Format(
                "Unknown ID4 0x{0:X4}",
                id4);
        }

        private string GetBampAnimationLabel(ushort id)
        {
            string label = GetBampFileLabel(id);

            if (label.StartsWith("File 0x"))
            {
                return String.Format(
                    "Animation 0x{0:X4}",
                    id);
            }

            return label;
        }

        private string GetBampFileLabel(ushort id)
        {
            try
            {
                if (Program.CurrentProject.ProjectFileTable
                    .Entries.ContainsKey(id))
                {
                    FileTableEntry entry =
                        Program.CurrentProject.ProjectFileTable
                            .Entries[id];

                    if (entry != null &&
                        !String.IsNullOrWhiteSpace(
                            entry.Comment))
                    {
                        return entry.Comment;
                    }
                }
            }
            catch
            {
            }

            return String.Format(
                "File 0x{0:X4}",
                id);
        }

        private void RefreshAllBampLabels()
        {
            for (int i = 0; i < dgvAnimations.Rows.Count; i++)
            {
                RefreshBampAnimationLabel(i);
            }

            for (int i = 0; i < dgvImages.Rows.Count; i++)
            {
                RefreshBampImageLabel(i);
            }

            for (int i = 0; i < dgvSequence.Rows.Count; i++)
            {
                RefreshBampSequenceLabel(i);
            }
        }

        private void RefreshBampAnimationLabel(int rowIndex)
        {
            if (rowIndex < 0 ||
                rowIndex >= dgvAnimations.Rows.Count)
            {
                return;
            }

            ushort wrestler;
            ushort animation;

            if (!TryParseHexUInt16Cell(
                dgvAnimations.Rows[rowIndex].Cells[0],
                out wrestler) ||
                !TryParseHexUInt16Cell(
                dgvAnimations.Rows[rowIndex].Cells[2],
                out animation))
            {
                return;
            }

            dgvAnimations.Rows[rowIndex]
                .Cells["BampWrestlerName"].Value =
                GetBampWrestlerName(wrestler);

            dgvAnimations.Rows[rowIndex]
                .Cells["BampAnimationLabel"].Value =
                GetBampAnimationLabel(animation);
        }

        private void RefreshBampImageLabel(int rowIndex)
        {
            if (rowIndex < 0 ||
                rowIndex >= dgvImages.Rows.Count)
            {
                return;
            }

            ushort fileId;
            if (!TryParseHexUInt16Cell(
                dgvImages.Rows[rowIndex].Cells[0],
                out fileId))
            {
                return;
            }

            dgvImages.Rows[rowIndex]
                .Cells["BampFileLabel"].Value =
                GetBampFileLabel(fileId);
        }

        private void RefreshBampSequenceLabel(int rowIndex)
        {
            if (rowIndex < 0 ||
                rowIndex >= dgvSequence.Rows.Count)
            {
                return;
            }

            DataGridViewRow row =
                dgvSequence.Rows[rowIndex];

            string[] refs = new string[4];

            for (int i = 0; i < 4; i++)
            {
                uint pointer;
                if (TryParseHexUInt32Cell(
                    row.Cells[8 + i],
                    out pointer))
                {
                    refs[i] = DescribeBampPointer(pointer);
                }
                else
                {
                    refs[i] = "invalid";
                }
            }

            row.Cells["BampSequenceReferences"].Value =
                String.Format(
                    "P1 {0}; P2 {1}; P3 {2}; P4 {3}",
                    refs[0],
                    refs[1],
                    refs[2],
                    refs[3]);
        }

        private string DescribeBampPointer(uint pointer)
        {
            if (pointer == 0)
            {
                return "null";
            }

            try
            {
                uint offset =
                    Program.PointerToRomAddr(pointer, 1);

                if (offset >= AnimStartLocation &&
                    offset <
                    AnimStartLocation +
                    (uint)(BampAnimationCapacity * 20))
                {
                    return String.Format(
                        "Anim[{0:X2}]",
                        (offset - AnimStartLocation) / 20);
                }

                if (offset >= ImgStartLocation &&
                    offset <
                    ImgStartLocation +
                    (uint)(BampImageCapacity * 16))
                {
                    return String.Format(
                        "Image[{0:X2}]",
                        (offset - ImgStartLocation) / 16);
                }

                if (offset >= SeqStartLocation &&
                    offset <
                    SeqStartLocation +
                    (uint)(BampSequenceCapacity * 28))
                {
                    return String.Format(
                        "Seq[{0:X2}]",
                        (offset - SeqStartLocation) / 28);
                }

                return String.Format(
                    "ROM 0x{0:X}",
                    offset);
            }
            catch
            {
                return "bad pointer";
            }
        }

        private static bool TryParseHexUInt16Cell(
            DataGridViewCell cell,
            out ushort value)
        {
            string text = cell.Value == null
                ? String.Empty
                : cell.Value.ToString();

            return UInt16.TryParse(
                NormalizeHex(text),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        private static bool TryParseHexUInt32Cell(
            DataGridViewCell cell,
            out uint value)
        {
            string text = cell.Value == null
                ? String.Empty
                : cell.Value.ToString();

            return UInt32.TryParse(
                NormalizeHex(text),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        private void BampAnimationSelectionChanged(
            object sender,
            EventArgs e)
        {
            SyncAnimationPickersToSelection();
            UpdateBampAnimationSemanticStatus();
        }

        private void BampAnimationCellEndEdit(
            object sender,
            DataGridViewCellEventArgs e)
        {
            NormalizeBampAnimationCell(e.RowIndex, e.ColumnIndex);
            RefreshBampAnimationLabel(e.RowIndex);
            SyncAnimationPickersToSelection();
            UpdateBampAnimationSemanticStatus();
        }

        private void BampImageCellEndEdit(
            object sender,
            DataGridViewCellEventArgs e)
        {
            RefreshBampImageLabel(e.RowIndex);
        }

        private void BampSequenceSelectionChanged(
            object sender,
            EventArgs e)
        {
            SyncSequenceCameraPicker();
        }

        private void BampSequenceCellEndEdit(
            object sender,
            DataGridViewCellEventArgs e)
        {
            RefreshBampSequenceLabel(e.RowIndex);
            SyncSequenceCameraPicker();
        }

        private void SyncAnimationPickersToSelection()
        {
            if (BampUpdatingUi ||
                dgvAnimations.CurrentRow == null)
            {
                return;
            }

            ushort wrestler;
            ushort animation;

            if (!TryParseHexUInt16Cell(
                dgvAnimations.CurrentRow.Cells[0],
                out wrestler) ||
                !TryParseHexUInt16Cell(
                dgvAnimations.CurrentRow.Cells[2],
                out animation))
            {
                return;
            }

            BampUpdatingUi = true;
            SelectChoice(BampWrestlerPicker, wrestler);
            SelectChoice(BampAnimationPicker, animation);
            BampUpdatingUi = false;
        }

        private void SyncSequenceCameraPicker()
        {
            if (BampSequenceCameraPicker == null)
            {
                return;
            }

            if (BampUpdatingUi ||
                dgvSequence.CurrentRow == null)
            {
                return;
            }

            ushort camera;
            if (!TryParseHexUInt16Cell(
                dgvSequence.CurrentRow.Cells[5],
                out camera))
            {
                return;
            }

            BampUpdatingUi = true;
            SelectChoice(
                BampSequenceCameraPicker,
                camera);
            BampUpdatingUi = false;
        }

        private static void SelectChoice(
            ComboBox combo,
            ushort value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                IntroChoice choice =
                    combo.Items[i] as IntroChoice;

                if (choice != null &&
                    choice.Value == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            combo.SelectedIndex = -1;
        }

                private void BampWrestlerPickerChanged(
            object sender,
            EventArgs e)
        {
            if (BampUpdatingUi)
            {
                return;
            }

            IntroChoice choice =
                BampWrestlerPicker.SelectedItem
                as IntroChoice;

            DataGridViewRow row =
                dgvAnimations.CurrentRow;

            if (choice == null ||
                row == null ||
                row.Index < 0)
            {
                return;
            }

            row.Cells[0].Value =
                choice.Value.ToString("X4");

            RefreshBampAnimationLabel(row.Index);
            UpdateBampAnimationSemanticStatus();

            dgvAnimations.NotifyCurrentCellDirty(true);
            dgvAnimations.EndEdit();
        }

        private void BampAnimationPickerChanged(
            object sender,
            EventArgs e)
        {
            if (BampUpdatingUi ||
                dgvAnimations.CurrentRow == null)
            {
                return;
            }

            IntroChoice choice =
                BampAnimationPicker.SelectedItem
                as IntroChoice;

            if (choice == null)
            {
                return;
            }

            dgvAnimations.CurrentRow.Cells[2].Value =
                choice.Value.ToString("X4");

            RefreshBampAnimationLabel(
                dgvAnimations.CurrentRow.Index);
        }

        private void BampSequenceCameraPickerChanged(
            object sender,
            EventArgs e)
        {
            if (BampSequenceCameraPicker == null)
            {
                return;
            }

            if (BampUpdatingUi ||
                dgvSequence.CurrentRow == null)
            {
                return;
            }

            IntroChoice choice =
                BampSequenceCameraPicker.SelectedItem
                as IntroChoice;

            if (choice == null)
            {
                return;
            }

            dgvSequence.CurrentRow.Cells[5].Value =
                choice.Value.ToString("X4");

            RefreshBampSequenceLabel(
                dgvSequence.CurrentRow.Index);
        }

        private void BampCopyRow(
            object sender,
            EventArgs e)
        {
            DataGridView grid =
                (DataGridView)((Button)sender).Tag;

            if (grid.CurrentRow == null)
            {
                return;
            }

            int rawColumns = (int)grid.Tag;
            object[] values = new object[rawColumns];

            for (int i = 0; i < rawColumns; i++)
            {
                values[i] = grid.CurrentRow.Cells[i].Value;
            }

            BampCopiedRows[grid] = values;
        }

        private void BampPasteRow(
            object sender,
            EventArgs e)
        {
            DataGridView grid =
                (DataGridView)((Button)sender).Tag;

            if (grid.CurrentRow == null ||
                !BampCopiedRows.ContainsKey(grid))
            {
                return;
            }

            ApplyRowValues(
                grid,
                grid.CurrentRow.Index,
                BampCopiedRows[grid]);

            RefreshAllBampLabels();
        }

        private void BampDuplicateNext(
            object sender,
            EventArgs e)
        {
            DataGridView grid =
                (DataGridView)((Button)sender).Tag;

            if (grid.CurrentRow == null)
            {
                return;
            }

            int source = grid.CurrentRow.Index;
            int target = source + 1;

            if (target >= grid.Rows.Count)
            {
                Program.WarningMessageBox(
                    "There is no next fixed ROM slot.");
                return;
            }

            int rawColumns = (int)grid.Tag;
            object[] values = new object[rawColumns];

            for (int i = 0; i < rawColumns; i++)
            {
                values[i] =
                    grid.Rows[source].Cells[i].Value;
            }

            ApplyRowValues(grid, target, values);
            grid.CurrentCell = grid.Rows[target].Cells[0];
            RefreshAllBampLabels();
        }

        private static void ApplyRowValues(
            DataGridView grid,
            int rowIndex,
            object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                grid.Rows[rowIndex].Cells[i].Value =
                    values[i];
            }
        }

        private void BampMoveRowUp(
            object sender,
            EventArgs e)
        {
            MoveBampGridRow(
                (DataGridView)((Button)sender).Tag,
                -1);
        }

        private void BampMoveRowDown(
            object sender,
            EventArgs e)
        {
            MoveBampGridRow(
                (DataGridView)((Button)sender).Tag,
                1);
        }

        private void MoveBampGridRow(
            DataGridView grid,
            int direction)
        {
            if (grid.CurrentRow == null)
            {
                return;
            }

            int source = grid.CurrentRow.Index;
            int target = source + direction;

            if (target < 0 || target >= grid.Rows.Count)
            {
                return;
            }

            int rawColumns = (int)grid.Tag;
            object[] sourceValues =
                new object[rawColumns];
            object[] targetValues =
                new object[rawColumns];

            for (int i = 0; i < rawColumns; i++)
            {
                sourceValues[i] =
                    grid.Rows[source].Cells[i].Value;
                targetValues[i] =
                    grid.Rows[target].Cells[i].Value;
            }

            ApplyRowValues(grid, source, targetValues);
            ApplyRowValues(grid, target, sourceValues);

            grid.CurrentCell = grid.Rows[target].Cells[0];
            RefreshAllBampLabels();
            RefreshRowHeaders();
        }

        private void BampRestoreRomRow(
            object sender,
            EventArgs e)
        {
            DataGridView grid =
                (DataGridView)((Button)sender).Tag;

            if (grid.CurrentRow == null)
            {
                return;
            }

            int index = grid.CurrentRow.Index;

            if (grid == dgvAnimations &&
                index < BampBaseAnimations.Count)
            {
                PopulateAnimationRow(
                    grid.Rows[index],
                    BampBaseAnimations[index]);
            }
            else if (grid == dgvImages &&
                index < BampBaseImages.Count)
            {
                PopulateImageRow(
                    grid.Rows[index],
                    BampBaseImages[index]);
            }
            else if (grid == dgvSequence &&
                index < BampBaseSequence.Count)
            {
                PopulateSequenceRow(
                    grid.Rows[index],
                    BampBaseSequence[index]);
            }

            RefreshAllBampLabels();
        }

        private static void PopulateAnimationRow(
            DataGridViewRow row,
            IntroSequenceAnimation_Later item)
        {
            row.Cells[0].Value =
                item.WrestlerID4.ToString("X4");
            row.Cells[1].Value = item.TimingA;
            row.Cells[2].Value =
                item.AnimationID.ToString("X4");
            row.Cells[3].Value = item.TimingB;
            row.Cells[4].Value = item.XPosition;
            row.Cells[5].Value = item.YPosition;
            row.Cells[6].Value = item.ZPosition;
            row.Cells[7].Value = item.Rotation;
            row.Cells[8].Value =
                item.AnimFlags.ToString("X2");
            row.Cells[9].Value =
                item.MoveSpeed.ToString("X2");
            row.Cells[10].Value =
                item.Unknown.ToString("X2");
            row.Cells[11].Value =
                item.CostumeNum.ToString("X2");
        }

        private static void PopulateImageRow(
            DataGridViewRow row,
            IntroSequenceGraphic_Later item)
        {
            row.Cells[0].Value =
                item.FileID.ToString("X4");
            row.Cells[1].Value = item.Width;
            row.Cells[2].Value = item.Height;
            row.Cells[3].Value =
                item.VertDisplacement;
            row.Cells[4].Value = item.HorizStretch;
            row.Cells[5].Value =
                item.Flags1.ToString("X4");
            row.Cells[6].Value = item.ScrollSpeed;
            row.Cells[7].Value =
                item.Unknown.ToString("X4");
        }

        private static void PopulateSequenceRow(
            DataGridViewRow row,
            IntroSequence_Later item)
        {
            row.Cells[0].Value = item.MainSequence;
            row.Cells[1].Value = item.SubSequence;
            row.Cells[2].Value =
                item.Flags.ToString("X2");
            row.Cells[3].Value =
                item.Transition.ToString("X2");
            row.Cells[4].Value = item.SceneTime;
            row.Cells[5].Value =
                item.CameraMotion.ToString("X4");
            row.Cells[6].Value =
                item.Unknown.ToString("X4");
            row.Cells[7].Value =
                item.StageNum.ToString("X4");
            row.Cells[8].Value =
                item.Pointer1.ToString("X8");
            row.Cells[9].Value =
                item.Pointer2.ToString("X8");
            row.Cells[10].Value =
                item.Pointer3.ToString("X8");
            row.Cells[11].Value =
                item.Pointer4.ToString("X8");
        }

        private bool TryReadBampEditorData(
            List<IntroSequenceAnimation_Later> animations,
            List<IntroSequenceGraphic_Later> images,
            List<IntroSequence_Later> sequence,
            out string errorMessage)
        {
            if (BampCameraGrid != null)
            {
                BampCameraGrid.EndEdit();
            }

            if (animations.Count != BampAnimationCapacity)
            {
                errorMessage =
                    "The animation table must retain its original " +
                    "fixed row count.";
                return false;
            }

            if (images.Count != BampImageCapacity)
            {
                errorMessage =
                    "The image table must retain its original " +
                    "fixed row count.";
                return false;
            }

            if (sequence.Count != BampSequenceCapacity)
            {
                errorMessage =
                    "The sequence table must retain its original " +
                    "fixed row count.";
                return false;
            }

            if (!CommitBampCameraEditor(out errorMessage))
            {
                return false;
            }

            for (int i = 0; i < sequence.Count; i++)
            {
                uint[] pointers =
                {
                    sequence[i].Pointer1,
                    sequence[i].Pointer2,
                    sequence[i].Pointer3,
                    sequence[i].Pointer4
                };

                for (int p = 0; p < pointers.Length; p++)
                {
                    if (pointers[p] == 0)
                    {
                        continue;
                    }

                    try
                    {
                        uint offset =
                            Program.PointerToRomAddr(
                                pointers[p],
                                1);

                        if (offset >=
                            Program.CurrentInputROM.Data.Length)
                        {
                            errorMessage = String.Format(
                                "Sequence row {0}, pointer {1} " +
                                "resolves outside the input ROM.",
                                i,
                                p + 1);
                            return false;
                        }
                    }
                    catch
                    {
                        errorMessage = String.Format(
                            "Sequence row {0}, pointer {1} " +
                            "is not a valid game pointer.",
                            i,
                            p + 1);
                        return false;
                    }
                }
            }

            errorMessage = String.Empty;
            return true;
        }

        private void BampCameraEntryChanged()
        {
            if (BampUpdatingUi)
            {
                return;
            }

            string errorMessage;
            if (!CommitBampCameraEditor(out errorMessage))
            {
                Program.ErrorMessageBox(errorMessage);
                BampUpdatingUi = true;
                cbCameraMotionList.SelectedIndex =
                    BampCurrentCameraEntry;
                BampUpdatingUi = false;
                return;
            }

            BampCurrentCameraEntry =
                cbCameraMotionList.SelectedIndex;

            LoadBampCameraEditor();
        }

        private void BampCameraAxisChanged(
            object sender,
            EventArgs e)
        {
            if (BampUpdatingUi)
            {
                return;
            }

            string errorMessage;
            if (!CommitBampCameraEditor(out errorMessage))
            {
                Program.ErrorMessageBox(errorMessage);
                BampUpdatingUi = true;
                SelectCameraAxis(
                    BampCurrentCameraAxis);
                BampUpdatingUi = false;
                return;
            }

            BampCurrentCameraAxis =
                BampCameraAxisPicker.SelectedItem == null
                ? "X"
                : BampCameraAxisPicker.SelectedItem.ToString();

            LoadBampCameraEditor();
        }

        private void SelectCameraAxis(string axis)
        {
            for (int i = 0;
                i < BampCameraAxisPicker.Items.Count;
                i++)
            {
                if (String.Equals(
                    BampCameraAxisPicker.Items[i].ToString(),
                    axis,
                    StringComparison.Ordinal))
                {
                    BampCameraAxisPicker.SelectedIndex = i;
                    return;
                }
            }
        }

        private void LoadBampCameraEditor()
        {
            BampCameraGrid.Rows.Clear();

            if (BampCurrentCameraEntry < 0 ||
                BampCurrentCameraEntry >=
                    CameraMotionDefs.Count)
            {
                BampCameraId.Text = String.Empty;
                BampCameraUnknown.Text = String.Empty;
                BampCameraPointerLabel.Text = String.Empty;
                return;
            }

            CameraDef camera =
                CameraMotionDefs[BampCurrentCameraEntry];

            BampUpdatingUi = true;

            BampCameraId.Text =
                camera.ID.ToString("X4");
            BampCameraUnknown.Text =
                camera.UnknownValue.ToString("X4");

            uint pointer =
                GetCameraAxisPointer(
                    camera,
                    BampCurrentCameraAxis);

            BampCameraPointerLabel.Text =
                String.Format(
                    "{0} pointer 0x{1:X8} → ROM 0x{2:X}",
                    BampCurrentCameraAxis,
                    pointer,
                    pointer == 0
                        ? 0
                        : Program.PointerToRomAddr(pointer, 1));

            List<CameraValuePair> pairs =
                GetCameraAxisList(
                    camera,
                    BampCurrentCameraAxis);

            foreach (CameraValuePair pair in pairs)
            {
                BampCameraGrid.Rows.Add(
                    pair.Value,
                    pair.FrameNumber);
            }

            RefreshRowHeaders();

            BampUpdatingUi = false;
        }

        private bool CommitBampCameraEditor(
            out string errorMessage)
        {
            if (BampCurrentCameraEntry < 0 ||
                BampCurrentCameraEntry >=
                    CameraMotionDefs.Count)
            {
                errorMessage = String.Empty;
                return true;
            }

            ushort id;
            ushort unknown;

            try
            {
                id = ParseHexUInt16(
                    BampCameraId.Text,
                    "Camera ID");

                unknown = ParseHexUInt16(
                    BampCameraUnknown.Text,
                    "Camera Unknown");
            }
            catch (FormatException ex)
            {
                errorMessage = ex.Message;
                return false;
            }

            List<CameraValuePair> pairs =
                new List<CameraValuePair>();

            for (int i = 0;
                i < BampCameraGrid.Rows.Count;
                i++)
            {
                DataGridViewRow row =
                    BampCameraGrid.Rows[i];

                short value;
                short frame;

                if (!Int16.TryParse(
                    CellText(row, 0),
                    out value))
                {
                    errorMessage = String.Format(
                        "Camera {0}, {1} row {2}: " +
                        "value must be a signed 16-bit number.",
                        BampCurrentCameraEntry,
                        BampCurrentCameraAxis,
                        i);
                    return false;
                }

                if (!Int16.TryParse(
                    CellText(row, 1),
                    out frame))
                {
                    errorMessage = String.Format(
                        "Camera {0}, {1} row {2}: " +
                        "frame must be a signed 16-bit number.",
                        BampCurrentCameraEntry,
                        BampCurrentCameraAxis,
                        i);
                    return false;
                }

                if (frame ==
                    CameraValuePair.CAMERA_FRAME_TERMINATOR &&
                    i != BampCameraGrid.Rows.Count - 1)
                {
                    errorMessage =
                        "The camera terminator frame 32767 " +
                        "must be the final row.";
                    return false;
                }

                pairs.Add(
                    new CameraValuePair(value, frame));
            }

            if (pairs.Count == 0 ||
                pairs[pairs.Count - 1].FrameNumber !=
                    CameraValuePair.CAMERA_FRAME_TERMINATOR)
            {
                errorMessage =
                    "Each camera axis must end with frame 32767.";
                return false;
            }

            int capacity = GetCameraCapacity(
                BampCurrentCameraEntry,
                BampCurrentCameraAxis);

            if (pairs.Count > capacity)
            {
                errorMessage = String.Format(
                    "The {0} axis contains {1} pairs, but " +
                    "the original ROM allocation only has {2} slots.",
                    BampCurrentCameraAxis,
                    pairs.Count,
                    capacity);
                return false;
            }

            CameraDef camera =
                CameraMotionDefs[BampCurrentCameraEntry];

            camera.ID = id;
            camera.UnknownValue = unknown;

            SetCameraAxisList(
                camera,
                BampCurrentCameraAxis,
                pairs);

            errorMessage = String.Empty;
            return true;
        }

        private void BampCameraCellValidating(
            object sender,
            DataGridViewCellValidatingEventArgs e)
        {
            short value;

            if (!Int16.TryParse(
                e.FormattedValue == null
                    ? String.Empty
                    : e.FormattedValue.ToString(),
                out value))
            {
                BampCameraGrid.Rows[e.RowIndex]
                    .ErrorText =
                    "Camera values and frames must be " +
                    "signed 16-bit decimal numbers.";

                e.Cancel = true;
            }
            else
            {
                BampCameraGrid.Rows[e.RowIndex]
                    .ErrorText = String.Empty;
            }
        }

        private void BampCameraAddPair(
            object sender,
            EventArgs e)
        {
            if (BampCurrentCameraEntry < 0)
            {
                return;
            }

            int capacity = GetCameraCapacity(
                BampCurrentCameraEntry,
                BampCurrentCameraAxis);

            if (BampCameraGrid.Rows.Count >= capacity)
            {
                Program.WarningMessageBox(
                    "This axis already uses every pair slot " +
                    "allocated in the original ROM.");
                return;
            }

            int insertIndex =
                Math.Max(
                    0,
                    BampCameraGrid.Rows.Count - 1);

            BampCameraGrid.Rows.Insert(
                insertIndex,
                0,
                0);

            BampCameraGrid.CurrentCell =
                BampCameraGrid.Rows[insertIndex].Cells[0];

            RefreshRowHeaders();
        }

        private void BampCameraDeletePair(
            object sender,
            EventArgs e)
        {
            if (BampCameraGrid.CurrentRow == null)
            {
                return;
            }

            int index =
                BampCameraGrid.CurrentRow.Index;

            if (index ==
                BampCameraGrid.Rows.Count - 1)
            {
                Program.WarningMessageBox(
                    "The final camera terminator row " +
                    "cannot be deleted.");
                return;
            }

            BampCameraGrid.Rows.RemoveAt(index);
            RefreshRowHeaders();
        }

        private void BampCameraMovePairUp(
            object sender,
            EventArgs e)
        {
            MoveCameraPair(-1);
        }

        private void BampCameraMovePairDown(
            object sender,
            EventArgs e)
        {
            MoveCameraPair(1);
        }

        private void MoveCameraPair(int direction)
        {
            if (BampCameraGrid.CurrentRow == null)
            {
                return;
            }

            int source =
                BampCameraGrid.CurrentRow.Index;
            int target = source + direction;
            int terminator =
                BampCameraGrid.Rows.Count - 1;

            if (source == terminator ||
                target < 0 ||
                target >= terminator)
            {
                return;
            }

            object value =
                BampCameraGrid.Rows[source]
                    .Cells[0].Value;
            object frame =
                BampCameraGrid.Rows[source]
                    .Cells[1].Value;

            object targetValue =
                BampCameraGrid.Rows[target]
                    .Cells[0].Value;
            object targetFrame =
                BampCameraGrid.Rows[target]
                    .Cells[1].Value;

            BampCameraGrid.Rows[source]
                .Cells[0].Value = targetValue;
            BampCameraGrid.Rows[source]
                .Cells[1].Value = targetFrame;

            BampCameraGrid.Rows[target]
                .Cells[0].Value = value;
            BampCameraGrid.Rows[target]
                .Cells[1].Value = frame;

            BampCameraGrid.CurrentCell =
                BampCameraGrid.Rows[target].Cells[0];

            RefreshRowHeaders();
        }

        private void BampCameraRestoreAxis(
            object sender,
            EventArgs e)
        {
            if (BampCurrentCameraEntry < 0 ||
                BampCurrentCameraEntry >=
                    BampBaseCameraDefs.Count)
            {
                return;
            }

            CameraDef baseCamera =
                BampBaseCameraDefs[
                    BampCurrentCameraEntry];

            List<CameraValuePair> pairs =
                GetCameraAxisList(
                    baseCamera,
                    BampCurrentCameraAxis);

            BampCameraGrid.Rows.Clear();

            foreach (CameraValuePair pair in pairs)
            {
                BampCameraGrid.Rows.Add(
                    pair.Value,
                    pair.FrameNumber);
            }

            BampCameraId.Text =
                baseCamera.ID.ToString("X4");
            BampCameraUnknown.Text =
                baseCamera.UnknownValue.ToString("X4");

            RefreshRowHeaders();
        }

        private void SetCameraCapacity(
            int entry,
            string axis,
            int capacity)
        {
            BampCameraCapacities[
                MakeCameraCapacityKey(entry, axis)] =
                capacity;
        }

        private int GetCameraCapacity(
            int entry,
            string axis)
        {
            int capacity;

            if (BampCameraCapacities.TryGetValue(
                MakeCameraCapacityKey(entry, axis),
                out capacity))
            {
                return capacity;
            }

            return 0;
        }

        private static string MakeCameraCapacityKey(
            int entry,
            string axis)
        {
            return String.Format(
                "{0}:{1}",
                entry,
                axis);
        }

        private static List<CameraValuePair>
            GetCameraAxisList(
                CameraDef camera,
                string axis)
        {
            switch (axis)
            {
                case "Y":
                    return camera.Y;
                case "Z":
                    return camera.Z;
                case "Pitch":
                    return camera.Pitch;
                case "Pan":
                    return camera.Pan;
                case "Roll":
                    return camera.Roll;
                default:
                    return camera.X;
            }
        }

        private static void SetCameraAxisList(
            CameraDef camera,
            string axis,
            List<CameraValuePair> pairs)
        {
            switch (axis)
            {
                case "Y":
                    camera.Y = pairs;
                    break;
                case "Z":
                    camera.Z = pairs;
                    break;
                case "Pitch":
                    camera.Pitch = pairs;
                    break;
                case "Pan":
                    camera.Pan = pairs;
                    break;
                case "Roll":
                    camera.Roll = pairs;
                    break;
                default:
                    camera.X = pairs;
                    break;
            }
        }

        private static uint GetCameraAxisPointer(
            CameraDef camera,
            string axis)
        {
            switch (axis)
            {
                case "Y":
                    return camera.ValuePointerY;
                case "Z":
                    return camera.ValuePointerZ;
                case "Pitch":
                    return camera.ValuePointerPitch;
                case "Pan":
                    return camera.ValuePointerPan;
                case "Roll":
                    return camera.ValuePointerRoll;
                default:
                    return camera.ValuePointerX;
            }
        }

        private void PopulateCameraFileData(
            GameIntroDefFile introFile)
        {
            string errorMessage;
            if (!CommitBampCameraEditor(out errorMessage))
            {
                throw new InvalidDataException(errorMessage);
            }

            introFile.CameraOffset =
                CameraMotionStartLocation;
            introFile.CameraTableData =
                BuildBampCameraTableData();
            introFile.CameraDataChunks =
                BuildBampCameraChunks();
        }

        private byte[] BuildBampCameraTableData()
        {
            using (MemoryStream stream =
                new MemoryStream())
            using (BinaryWriter writer =
                new BinaryWriter(stream))
            {
                foreach (
                    CameraDef camera
                    in CameraMotionDefs)
                {
                    camera.WriteData(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private List<GameIntroDataChunk>
            BuildBampCameraChunks()
        {
            Dictionary<uint, byte[]> chunks =
                new Dictionary<uint, byte[]>();

            foreach (
                CameraDef camera
                in CameraMotionDefs)
            {
                AddBampCameraChunk(
                    chunks,
                    camera.DataPointer,
                    BuildBampCameraPointerBlock(camera));

                AddBampCameraChunk(
                    chunks,
                    camera.ValuePointerX,
                    BuildBampCameraPairs(camera.X));

                AddBampCameraChunk(
                    chunks,
                    camera.ValuePointerY,
                    BuildBampCameraPairs(camera.Y));

                AddBampCameraChunk(
                    chunks,
                    camera.ValuePointerZ,
                    BuildBampCameraPairs(camera.Z));

                AddBampCameraChunk(
                    chunks,
                    camera.ValuePointerPitch,
                    BuildBampCameraPairs(camera.Pitch));

                AddBampCameraChunk(
                    chunks,
                    camera.ValuePointerPan,
                    BuildBampCameraPairs(camera.Pan));

                AddBampCameraChunk(
                    chunks,
                    camera.ValuePointerRoll,
                    BuildBampCameraPairs(camera.Roll));
            }

            List<GameIntroDataChunk> result =
                new List<GameIntroDataChunk>();

            foreach (
                KeyValuePair<uint, byte[]> pair
                in chunks)
            {
                result.Add(
                    new GameIntroDataChunk(
                        pair.Key,
                        pair.Value));
            }

            result.Sort(
                delegate(
                    GameIntroDataChunk left,
                    GameIntroDataChunk right)
                {
                    return left.Offset.CompareTo(
                        right.Offset);
                });

            return result;
        }

        private static void AddBampCameraChunk(
            Dictionary<uint, byte[]> chunks,
            uint pointer,
            byte[] data)
        {
            if (pointer == 0)
            {
                return;
            }

            uint offset =
                Program.PointerToRomAddr(pointer, 1);

            byte[] existing;
            if (chunks.TryGetValue(offset, out existing))
            {
                if (!ByteArraysEqual(existing, data))
                {
                    throw new InvalidDataException(
                        String.Format(
                            "Shared camera data at ROM 0x{0:X} " +
                            "was edited inconsistently.",
                            offset));
                }

                return;
            }

            chunks.Add(offset, data);
        }

        private static bool ByteArraysEqual(
            byte[] left,
            byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] BuildBampCameraPointerBlock(
            CameraDef camera)
        {
            using (MemoryStream stream =
                new MemoryStream())
            using (BinaryWriter writer =
                new BinaryWriter(stream))
            {
                WriteUInt32BigEndian(
                    writer,
                    camera.ValuePointerX);
                WriteUInt32BigEndian(
                    writer,
                    camera.ValuePointerY);
                WriteUInt32BigEndian(
                    writer,
                    camera.ValuePointerZ);
                WriteUInt32BigEndian(
                    writer,
                    camera.ValuePointerPitch);
                WriteUInt32BigEndian(
                    writer,
                    camera.ValuePointerPan);
                WriteUInt32BigEndian(
                    writer,
                    camera.ValuePointerRoll);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] BuildBampCameraPairs(
            List<CameraValuePair> pairs)
        {
            if (pairs == null ||
                pairs.Count == 0 ||
                pairs[pairs.Count - 1].FrameNumber !=
                    CameraValuePair.CAMERA_FRAME_TERMINATOR)
            {
                throw new InvalidDataException(
                    "Camera pair data is missing its terminator.");
            }

            using (MemoryStream stream =
                new MemoryStream())
            using (BinaryWriter writer =
                new BinaryWriter(stream))
            {
                foreach (CameraValuePair pair in pairs)
                {
                    pair.WriteData(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteUInt32BigEndian(
            BinaryWriter writer,
            uint value)
        {
            byte[] data =
                BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(data);
            }

            writer.Write(data);
        }

        private void LoadSavedCameraData(
            GameIntroDefFile introFile)
        {
            if (introFile.CameraTableData == null ||
                introFile.CameraTableData.Length == 0)
            {
                return;
            }

            byte[] patchedRom =
                (byte[])Program.CurrentInputROM.Data.Clone();

            ApplyBampCameraBytes(
                patchedRom,
                introFile.CameraOffset,
                introFile.CameraTableData);

            foreach (
                GameIntroDataChunk chunk
                in introFile.CameraDataChunks)
            {
                ApplyBampCameraBytes(
                    patchedRom,
                    chunk.Offset,
                    chunk.Data);
            }

            CameraMotionDefs.Clear();

            using (MemoryStream stream =
                new MemoryStream(patchedRom))
            using (BinaryReader reader =
                new BinaryReader(stream))
            {
                stream.Seek(
                    introFile.CameraOffset,
                    SeekOrigin.Begin);

                int count =
                    introFile.CameraTableData.Length / 8;

                for (int i = 0; i < count; i++)
                {
                    CameraMotionDefs.Add(
                        new CameraDef(reader));
                }
            }

            CameraMotionStartLocation =
                introFile.CameraOffset;
        }

        private static void ApplyBampCameraBytes(
            byte[] rom,
            uint offset,
            byte[] data)
        {
            ulong end =
                (ulong)offset + (ulong)data.Length;

            if (end > (ulong)rom.Length)
            {
                throw new InvalidDataException(
                    String.Format(
                        "Saved camera data at 0x{0:X} " +
                        "exceeds the input ROM.",
                        offset));
            }

            Buffer.BlockCopy(
                data,
                0,
                rom,
                (int)offset,
                data.Length);
        }

        private static List<IntroSequenceAnimation_Later>
            CloneAnimations(
                List<IntroSequenceAnimation_Later> source)
        {
            List<IntroSequenceAnimation_Later> result =
                new List<IntroSequenceAnimation_Later>();

            foreach (
                IntroSequenceAnimation_Later item
                in source)
            {
                IntroSequenceAnimation_Later clone =
                    new IntroSequenceAnimation_Later();

                clone.WrestlerID4 = item.WrestlerID4;
                clone.TimingA = item.TimingA;
                clone.AnimationID = item.AnimationID;
                clone.TimingB = item.TimingB;
                clone.XPosition = item.XPosition;
                clone.YPosition = item.YPosition;
                clone.ZPosition = item.ZPosition;
                clone.Rotation = item.Rotation;
                clone.AnimFlags = item.AnimFlags;
                clone.MoveSpeed = item.MoveSpeed;
                clone.Unknown = item.Unknown;
                clone.CostumeNum = item.CostumeNum;

                result.Add(clone);
            }

            return result;
        }

        private static List<IntroSequenceGraphic_Later>
            CloneImages(
                List<IntroSequenceGraphic_Later> source)
        {
            List<IntroSequenceGraphic_Later> result =
                new List<IntroSequenceGraphic_Later>();

            foreach (
                IntroSequenceGraphic_Later item
                in source)
            {
                IntroSequenceGraphic_Later clone =
                    new IntroSequenceGraphic_Later();

                clone.FileID = item.FileID;
                clone.Width = item.Width;
                clone.Height = item.Height;
                clone.VertDisplacement =
                    item.VertDisplacement;
                clone.HorizStretch = item.HorizStretch;
                clone.Flags1 = item.Flags1;
                clone.ScrollSpeed = item.ScrollSpeed;
                clone.Unknown = item.Unknown;

                result.Add(clone);
            }

            return result;
        }

        private static List<IntroSequence_Later>
            CloneSequence(
                List<IntroSequence_Later> source)
        {
            List<IntroSequence_Later> result =
                new List<IntroSequence_Later>();

            foreach (IntroSequence_Later item in source)
            {
                IntroSequence_Later clone =
                    new IntroSequence_Later();

                clone.MainSequence = item.MainSequence;
                clone.SubSequence = item.SubSequence;
                clone.Flags = item.Flags;
                clone.Transition = item.Transition;
                clone.SceneTime = item.SceneTime;
                clone.CameraMotion = item.CameraMotion;
                clone.Unknown = item.Unknown;
                clone.StageNum = item.StageNum;
                clone.Pointer1 = item.Pointer1;
                clone.Pointer2 = item.Pointer2;
                clone.Pointer3 = item.Pointer3;
                clone.Pointer4 = item.Pointer4;

                result.Add(clone);
            }

            return result;
        }

        private static List<CameraDef> CloneCameraDefs(
            List<CameraDef> source)
        {
            List<CameraDef> result =
                new List<CameraDef>();

            foreach (CameraDef item in source)
            {
                CameraDef clone =
                    new CameraDef(
                        item.DataPointer,
                        item.UnknownValue,
                        item.ID);

                clone.ValuePointerX =
                    item.ValuePointerX;
                clone.ValuePointerY =
                    item.ValuePointerY;
                clone.ValuePointerZ =
                    item.ValuePointerZ;
                clone.ValuePointerPitch =
                    item.ValuePointerPitch;
                clone.ValuePointerPan =
                    item.ValuePointerPan;
                clone.ValuePointerRoll =
                    item.ValuePointerRoll;

                clone.X = CloneCameraPairs(item.X);
                clone.Y = CloneCameraPairs(item.Y);
                clone.Z = CloneCameraPairs(item.Z);
                clone.Pitch =
                    CloneCameraPairs(item.Pitch);
                clone.Pan =
                    CloneCameraPairs(item.Pan);
                clone.Roll =
                    CloneCameraPairs(item.Roll);

                result.Add(clone);
            }

            return result;
        }

        private static List<CameraValuePair>
            CloneCameraPairs(
                List<CameraValuePair> source)
        {
            List<CameraValuePair> result =
                new List<CameraValuePair>();

            foreach (CameraValuePair pair in source)
            {
                result.Add(
                    new CameraValuePair(
                        pair.Value,
                        pair.FrameNumber));
            }

            return result;
        }

        private void ConfigureAnimationSemanticUi()
        {
            string[] headers =
            {
                "Wrestler ID4",
                "Start / Timing",
                "Animation ID",
                "End / Count",
                "X Position",
                "Y Position",
                "Z Position",
                "Rotation",
                "Animation Flags",
                "Move / Item Flags",
                "Extra Flags",
                "Costume"
            };

            string[] help =
            {
                "Four-digit wrestler ID4 in hexadecimal.",
                "Intro action start/timing value in hexadecimal.",
                "AKI animation File ID in hexadecimal.",
                "Intro action end/count value in hexadecimal.",
                "Signed X position stored as a 16-bit hexadecimal value.",
                "Signed Y position stored as a 16-bit hexadecimal value.",
                "Signed Z position stored as a 16-bit hexadecimal value.",
                "Facing/rotation value in hexadecimal.",
                "Animation-control flags byte in hexadecimal.",
                "Movement/item flags byte in hexadecimal.",
                "Additional flags byte in hexadecimal.",
                "Costume byte in hexadecimal."
            };

            int count = Math.Min(
                12,
                dgvAnimations.Columns.Count);

            for (int i = 0; i < count; i++)
            {
                DataGridViewColumn column =
                    dgvAnimations.Columns[i];

                column.HeaderText = headers[i];
                column.ToolTipText = help[i];
                column.SortMode =
                    DataGridViewColumnSortMode.NotSortable;
                column.AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.AllCells;
            }

            tabPage1.Text = "Animation Records";
            tabPage2.Text = "Image Records";
            tabPage3.Text = "Sequence";

            BampAnimationSemanticStatus =
                new ToolStripStatusLabel();

            BampAnimationSemanticStatus.Spring = true;
            BampAnimationSemanticStatus.TextAlign =
                ContentAlignment.MiddleLeft;
            BampAnimationSemanticStatus.Text =
                "Select an animation record.";

            statusStrip1.Items.Add(
                new ToolStripSeparator());

            statusStrip1.Items.Add(
                BampAnimationSemanticStatus);
        }

        private void BampAnimationCellValidating(
            object sender,
            DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex < 0 ||
                e.ColumnIndex >= 12)
            {
                return;
            }

            string text =
                e.FormattedValue == null
                ? String.Empty
                : e.FormattedValue.ToString();

            text = NormalizeHex(text);

            bool valid;
            if (e.ColumnIndex < 8)
            {
                ushort value;
                valid =
                    text.Length > 0 &&
                    text.Length <= 4 &&
                    UInt16.TryParse(
                        text,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value);
            }
            else
            {
                byte value;
                valid =
                    text.Length > 0 &&
                    text.Length <= 2 &&
                    Byte.TryParse(
                        text,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value);
            }

            if (!valid)
            {
                e.Cancel = true;
                dgvAnimations.Rows[e.RowIndex]
                    .Cells[e.ColumnIndex]
                    .ErrorText =
                    e.ColumnIndex < 8
                    ? "Enter a hexadecimal value from 0000 to FFFF."
                    : "Enter a hexadecimal byte from 00 to FF.";
            }
            else
            {
                dgvAnimations.Rows[e.RowIndex]
                    .Cells[e.ColumnIndex]
                    .ErrorText = String.Empty;
            }
        }

        private void NormalizeBampAnimationCell(
            int rowIndex,
            int columnIndex)
        {
            if (rowIndex < 0 ||
                rowIndex >= dgvAnimations.Rows.Count ||
                columnIndex < 0 ||
                columnIndex >= 12)
            {
                return;
            }

            DataGridViewCell cell =
                dgvAnimations.Rows[rowIndex]
                    .Cells[columnIndex];

            string text =
                cell.Value == null
                ? String.Empty
                : NormalizeHex(cell.Value.ToString());

            if (columnIndex < 8)
            {
                ushort value;
                if (UInt16.TryParse(
                    text,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value))
                {
                    cell.Value = value.ToString("X4");
                }
            }
            else
            {
                byte value;
                if (Byte.TryParse(
                    text,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value))
                {
                    cell.Value = value.ToString("X2");
                }
            }
        }

        private static bool TryParseHexByteCell(
            DataGridViewCell cell,
            out byte value)
        {
            string text =
                cell.Value == null
                ? String.Empty
                : cell.Value.ToString();

            return Byte.TryParse(
                NormalizeHex(text),
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        private void UpdateBampAnimationSemanticStatus()
        {
            if (BampAnimationSemanticStatus == null)
            {
                return;
            }

            DataGridViewRow row =
                dgvAnimations.CurrentRow;

            if (row == null || row.Index < 0)
            {
                BampAnimationSemanticStatus.Text =
                    "Select an animation record.";
                return;
            }

            ushort wrestler = 0;
            ushort start = 0;
            ushort animation = 0;
            ushort end = 0;
            ushort xRaw = 0;
            ushort yRaw = 0;
            ushort zRaw = 0;
            ushort rotation = 0;
            byte animationFlags = 0;
            byte moveFlags = 0;
            byte extraFlags = 0;
            byte costume = 0;

            bool valid =
                TryParseHexUInt16Cell(row.Cells[0], out wrestler) &&
                TryParseHexUInt16Cell(row.Cells[1], out start) &&
                TryParseHexUInt16Cell(row.Cells[2], out animation) &&
                TryParseHexUInt16Cell(row.Cells[3], out end) &&
                TryParseHexUInt16Cell(row.Cells[4], out xRaw) &&
                TryParseHexUInt16Cell(row.Cells[5], out yRaw) &&
                TryParseHexUInt16Cell(row.Cells[6], out zRaw) &&
                TryParseHexUInt16Cell(row.Cells[7], out rotation) &&
                TryParseHexByteCell(
                    row.Cells[8],
                    out animationFlags) &&
                TryParseHexByteCell(
                    row.Cells[9],
                    out moveFlags) &&
                TryParseHexByteCell(
                    row.Cells[10],
                    out extraFlags) &&
                TryParseHexByteCell(
                    row.Cells[11],
                    out costume);

            if (!valid)
            {
                BampAnimationSemanticStatus.Text =
                    String.Format(
                        "Record 0x{0:X2}: one or more fields are invalid.",
                        row.Index);
                return;
            }

            uint romOffset =
                AnimStartLocation +
                (uint)(row.Index * 20);

            short x = unchecked((short)xRaw);
            short y = unchecked((short)yRaw);
            short z = unchecked((short)zRaw);

            BampAnimationSemanticStatus.Text =
                String.Format(
                    "Record 0x{0:X2} | ROM 0x{1:X} | " +
                    "{2} [0x{3:X4}] | {4} [0x{5:X4}] | " +
                    "start 0x{6:X4}, end 0x{7:X4} | " +
                    "position {8}, {9}, {10} | rotation 0x{11:X4} | " +
                    "flags {12:X2}/{13:X2}/{14:X2} | costume {15:X2}",
                    row.Index,
                    romOffset,
                    GetBampWrestlerName(wrestler),
                    wrestler,
                    GetBampAnimationLabel(animation),
                    animation,
                    start,
                    end,
                    x,
                    y,
                    z,
                    rotation,
                    animationFlags,
                    moveFlags,
                    extraFlags,
                    costume);
        }

    }
}
