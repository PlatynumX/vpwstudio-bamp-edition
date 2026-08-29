using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace VPWStudio.Editors.VPW2
{
    /// <summary>
    /// Native WinForms/OpenTK port of VPW2 Arena Studio Proof 10.
    /// There is no browser, WebView, Three.js or CDN dependency.
    /// </summary>
    public class ArenaEditor_VPW2 : Form
    {
        private static readonly string[] ArenaNames =
        {
            "AKI Hall",
            "Nippon Budokan",
            "Kawasaki Stadium",
            "Tokyo Dome",
            "Tokyo Dome Road",
            "Arena Zero"
        };

        private const int RingModelPtrTable = 0x485BC;
        private const int RingMaterialPtrTable = 0x485D4;
        private const int RingBlockModelPtrTable = 0x485F8;
        private const int RingBlockMaterialPtrTable = 0x48610;
        private const int MainCountTable = 0x48628;
        private const int MainModelPtrTable = 0x48634;
        private const int MainMaterialPtrTable = 0x4864C;
        private const int SectionCountPtrTable = 0x48670;
        private const int SectionModelPtrTable = 0x48688;
        private const int SectionMaterial1PtrTable = 0x486A0;
        private const int SectionMaterial2PtrTable = 0x486B8;

        private readonly ComboBox arenaSelect = new ComboBox();
        private readonly OpenTK.GLControl glControl = new OpenTK.GLControl();
        private readonly DataGridView objectGrid = new DataGridView();
        private readonly CheckBox wireframe = new CheckBox();
        private readonly CheckBox doubleSided = new CheckBox();
        private readonly CheckBox showGrid = new CheckBox();
        private readonly CheckBox debugAttrs = new CheckBox();
        private readonly Label status = new Label();

        private byte[] baseRom;
        private byte[] workingRom;
        private ArenaProfile profile;
        private readonly SortedDictionary<UInt32, UInt16> edits =
            new SortedDictionary<UInt32, UInt16>();
        private readonly List<ArenaPart> parts = new List<ArenaPart>();
        private readonly Dictionary<UInt16, ArenaTexture> textureCache =
            new Dictionary<UInt16, ArenaTexture>();

        private bool validGl;
        private bool updatingGrid;
        private int currentArena;
        private Vector3 cameraTarget = new Vector3(0f, 420f, -500f);
        private float cameraYaw = 40f;
        private float cameraPitch = 18f;
        private float cameraDistance = 4300f;
        private Point mouseLast;
        private MouseButtons dragButton = MouseButtons.None;

                public ArenaEditor_VPW2()
        {
            Text = "VPW2 Arena Editor";
            Width = 1280;
            Height = 780;
            MinimumSize = new Size(960, 620);
            InitializeUi();

            if (Program.CurrentProject == null ||
                Program.CurrentProject.Settings.BaseGame != VPWGames.VPW2 ||
                Program.CurrentInputROM == null)
            {
                throw new InvalidOperationException(
                    "VPW2 Arena Editor requires an open VPW2 project.");
            }

            LoadProjectArenaEdits();
            arenaSelect.SelectedIndex = 0;
        }

        private void InitializeUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.AutoScroll = true;
            left.Padding = new Padding(8);
            root.Controls.Add(left, 0, 0);

            glControl.Dock = DockStyle.Fill;
            glControl.BackColor = Color.Black;
            root.Controls.Add(glControl, 1, 0);

            int y = 8;
            Label title = new Label();
            title.Text = "VPW2 Arena Editor";
            title.Font = new Font(Font.FontFamily, 13f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(8, y);
            left.Controls.Add(title);
            y += 32;

            arenaSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            arenaSelect.Items.AddRange(ArenaNames);
            arenaSelect.Width = 356;
            arenaSelect.Location = new Point(8, y);
            arenaSelect.SelectedIndexChanged += delegate
            {
                if (arenaSelect.SelectedIndex >= 0)
                {
                    currentArena = arenaSelect.SelectedIndex;
                    BuildScene();
                }
            };
            left.Controls.Add(arenaSelect);
            y += 34;

            FlowLayoutPanel viewButtons = new FlowLayoutPanel();
            viewButtons.Location = new Point(8, y);
            viewButtons.Width = 356;
            viewButtons.Height = 62;
            viewButtons.WrapContents = true;
            AddButton(viewButtons, "Frame Whole Arena", delegate { FrameParts(null); });
            AddButton(viewButtons, "Game Hard Cam", delegate { SetHardCamera(); });
            AddButton(viewButtons, "Venue Focus", delegate { FrameParts("Venue"); });
            AddButton(viewButtons, "Ring Focus", delegate { FrameParts("Ring"); });
            left.Controls.Add(viewButtons);
            y += 66;

            FlowLayoutPanel flags = new FlowLayoutPanel();
            flags.Location = new Point(8, y);
            flags.Width = 356;
            flags.Height = 48;
            flags.WrapContents = true;
            wireframe.Text = "Wireframe";
            doubleSided.Text = "Double-sided";
            doubleSided.Checked = true;
            showGrid.Text = "Grid";
            debugAttrs.Text = "Debug vertex attrs";
            wireframe.AutoSize = true;
            doubleSided.AutoSize = true;
            showGrid.AutoSize = true;
            debugAttrs.AutoSize = true;
            wireframe.CheckedChanged += delegate { glControl.Invalidate(); };
            doubleSided.CheckedChanged += delegate { glControl.Invalidate(); };
            showGrid.CheckedChanged += delegate { glControl.Invalidate(); };
            debugAttrs.CheckedChanged += delegate { glControl.Invalidate(); };
            flags.Controls.Add(wireframe);
            flags.Controls.Add(doubleSided);
            flags.Controls.Add(showGrid);
            flags.Controls.Add(debugAttrs);
            left.Controls.Add(flags);
            y += 52;

            Label hint = new Label();
            hint.Text =
                "Edit VPW2 ring, main-object and venue-section Model/Material IDs. " +
                "Edits are saved to the project and applied by Build ROM.";
            hint.Width = 356;
            hint.Height = 48;
            hint.Location = new Point(8, y);
            left.Controls.Add(hint);
            y += 52;

            objectGrid.Location = new Point(8, y);
            objectGrid.Width = 356;
            objectGrid.Height = 330;
            objectGrid.AllowUserToAddRows = false;
            objectGrid.AllowUserToDeleteRows = false;
            objectGrid.AllowUserToResizeRows = false;
            objectGrid.RowHeadersVisible = false;
            objectGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            objectGrid.MultiSelect = false;

            DataGridViewCheckBoxColumn visibleColumn =
                new DataGridViewCheckBoxColumn();
            visibleColumn.Name = "Visible";
            visibleColumn.HeaderText = "On";
            visibleColumn.Width = 34;
            objectGrid.Columns.Add(visibleColumn);
            objectGrid.Columns.Add(MakeTextColumn("Group", "Group", 58, true));
            objectGrid.Columns.Add(MakeTextColumn("Name", "Object", 114, true));
            objectGrid.Columns.Add(MakeTextColumn("Model", "Model", 54, false));
            objectGrid.Columns.Add(MakeTextColumn("Material", "Mat", 54, false));
            objectGrid.Columns.Add(MakeTextColumn("Triangles", "Tri", 42, true));
            objectGrid.CellBeginEdit += ObjectGrid_CellBeginEdit;
            objectGrid.CellEndEdit += ObjectGrid_CellEndEdit;
            objectGrid.CellValueChanged += ObjectGrid_CellValueChanged;
            objectGrid.CurrentCellDirtyStateChanged += delegate
            {
                if (objectGrid.IsCurrentCellDirty)
                {
                    objectGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            objectGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            left.Controls.Add(objectGrid);
            y += 338;

            status.Location = new Point(8, y);
            status.Width = 356;
            status.Height = 55;
            status.BorderStyle = BorderStyle.FixedSingle;
            status.Padding = new Padding(4);
            left.Controls.Add(status);
            y += 61;

            FlowLayoutPanel saveRow = new FlowLayoutPanel();
            saveRow.Location = new Point(8, y);
            saveRow.Width = 356;
            saveRow.Height = 36;
            AddButton(saveRow, "Reload Saved", delegate
            {
                LoadProjectArenaEdits();
                BuildScene();
            });
            AddButton(saveRow, "Save Arena Edits", delegate { SaveArenaEdits(); });
            AddButton(saveRow, "Close", delegate { Close(); });
            left.Controls.Add(saveRow);

            glControl.Load += GlControl_Load;
            glControl.Paint += GlControl_Paint;
            glControl.Resize += delegate { if (validGl) glControl.Invalidate(); };
            glControl.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                glControl.Focus();
                dragButton = e.Button;
                mouseLast = e.Location;
            };
            glControl.MouseUp += delegate { dragButton = MouseButtons.None; };
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseWheel += GlControl_MouseWheel;
            FormClosed += delegate { DestroyGlTextures(); };
        }

        private static void AddButton(Control parent, string text, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 27;
            button.Click += handler;
            parent.Controls.Add(button);
        }

        private static DataGridViewTextBoxColumn MakeTextColumn(
            string name, string header, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            column.ReadOnly = readOnly;
            return column;
        }

        private void LoadProjectArenaEdits()
        {
            baseRom = (byte[])Program.CurrentInputROM.Data.Clone();
            workingRom = (byte[])baseRom.Clone();
            edits.Clear();

            SortedDictionary<UInt32, UInt16> loaded =
                VPW2ArenaPatchFile.LoadForCurrentProject(baseRom);

            foreach (KeyValuePair<UInt32, UInt16> edit in loaded)
            {
                edits[edit.Key] = edit.Value;
                WriteU16(workingRom, checked((int)edit.Key), edit.Value);
            }

            profile = DetectProfile(workingRom);
            status.Text = String.Format(
                "{0}\r\n{1} saved arena edits loaded.",
                profile.Name,
                edits.Count);
        }

                private ArenaProfile DetectProfile(byte[] rom)
        {
            if (Program.CurrentProject.ProjectFileTable != null &&
                Program.CurrentProject.ProjectFileTable.Entries.Count > 0)
            {
                int first = checked(
                    (int)Program.CurrentProject.ProjectFileTable.FirstFile);
                int table = checked(
                    (int)Program.CurrentProject.ProjectFileTable.Location);
                int count =
                    Program.CurrentProject.ProjectFileTable.Entries.Count;

                if (first >= 0 &&
                    table > first &&
                    count > 0 &&
                    table + count * 4 + 4 <= rom.Length)
                {
                    return new ArenaProfile(
                        "Project FileTable",
                        first,
                        table,
                        count);
                }
            }

            if (rom.Length >= 0x02000000 &&
                0x01310F40 + (0x3322 + 1) * 4 <= rom.Length)
            {
                return new ArenaProfile(
                    "VPW2 retail layout",
                    0x00152DF0,
                    0x01310F40,
                    0x3322);
            }

            throw new InvalidDataException(
                "Unsupported VPW2 FileTable/profile.");
        }

        private void SaveArenaEdits()
        {
            try
            {
                VPW2ArenaPatchFile.SaveForCurrentProject(baseRom, edits);
                Program.UnsavedChanges = true;
                status.Text = String.Format(
                    "Saved {0} arena edits to\r\n{1}",
                    edits.Count,
                    VPW2ArenaPatchFile.GetProjectPath(false));
                Program.InfoMessageBox(String.Format(
                    "Saved {0} VPW2 arena edits.\n\nBuild ROM will apply them to the output ROM.",
                    edits.Count));
            }
            catch (Exception exception)
            {
                Program.ErrorMessageBox(exception.Message);
            }
        }

                private void BuildScene()
        {
            if (workingRom == null ||
                currentArena < 0 ||
                currentArena >= ArenaNames.Length)
            {
                return;
            }

            DestroyGlTextures();
            textureCache.Clear();
            parts.Clear();

            try
            {
                int arena = currentArena;

                int ringModels = PtrToRom(
                    ReadU32(
                        workingRom,
                        RingModelPtrTable + arena * 4));
                int ringMaterials = PtrToRom(
                    ReadU32(
                        workingRom,
                        RingMaterialPtrTable + arena * 4));

                AddVpw2Part(
                    "Ring",
                    "Ring mat",
                    ringModels,
                    ringMaterials,
                    -1,
                    true,
                    true);

                int ringBlockModels = PtrToRom(
                    ReadU32(
                        workingRom,
                        RingBlockModelPtrTable + arena * 4));
                int ringBlockMaterials = PtrToRom(
                    ReadU32(
                        workingRom,
                        RingBlockMaterialPtrTable + arena * 4));

                if (ringBlockModels >= 0 &&
                    ringBlockMaterials > ringBlockModels)
                {
                    int count =
                        (ringBlockMaterials - ringBlockModels) / 2;

                    if (count < 1 || count > 32)
                    {
                        throw new InvalidDataException(
                            "VPW2 ring/apron block size is invalid.");
                    }

                    for (int i = 0; i < count; i++)
                    {
                        AddVpw2Part(
                            "Ring",
                            String.Format(
                                "Ring/apron {0:00}",
                                i),
                            ringBlockModels + i * 2,
                            ringBlockMaterials + i * 2,
                            -1,
                            true,
                            true);
                    }
                }

                int mainCount =
                    ReadU16(
                        workingRom,
                        MainCountTable + arena * 2);
                int mainModels = PtrToRom(
                    ReadU32(
                        workingRom,
                        MainModelPtrTable + arena * 4));
                int mainMaterials = PtrToRom(
                    ReadU32(
                        workingRom,
                        MainMaterialPtrTable + arena * 4));

                if (mainCount < 0 ||
                    mainCount > 64 ||
                    mainModels < 0 ||
                    mainMaterials < 0)
                {
                    throw new InvalidDataException(
                        "VPW2 main arena object table is invalid.");
                }

                for (int i = 0; i < mainCount; i++)
                {
                    AddVpw2Part(
                        "Main",
                        String.Format(
                            "Main object {0:00}",
                            i),
                        mainModels + i * 2,
                        mainMaterials + i * 2,
                        -1,
                        true,
                        true);
                }

                int sectionCounts = PtrToRom(
                    ReadU32(
                        workingRom,
                        SectionCountPtrTable + arena * 4));
                int sectionModelPointers = PtrToRom(
                    ReadU32(
                        workingRom,
                        SectionModelPtrTable + arena * 4));
                int sectionMat1Pointers = PtrToRom(
                    ReadU32(
                        workingRom,
                        SectionMaterial1PtrTable + arena * 4));
                int sectionMat2Pointers = PtrToRom(
                    ReadU32(
                        workingRom,
                        SectionMaterial2PtrTable + arena * 4));

                if (sectionCounts < 0 ||
                    sectionModelPointers < 0 ||
                    sectionMat1Pointers < 0 ||
                    sectionMat2Pointers < 0)
                {
                    throw new InvalidDataException(
                        "VPW2 venue section pointers are invalid.");
                }

                string[] sectionNames =
                {
                    "East",
                    "Right",
                    "West",
                    "Left"
                };

                for (int section = 0; section < 4; section++)
                {
                    int count =
                        workingRom[sectionCounts + section];

                    if (count < 0 || count > 64)
                    {
                        throw new InvalidDataException(
                            "VPW2 venue section count is invalid.");
                    }

                    int models = PtrToRom(
                        ReadU32(
                            workingRom,
                            sectionModelPointers + section * 4));
                    int mats1 = PtrToRom(
                        ReadU32(
                            workingRom,
                            sectionMat1Pointers + section * 4));
                    int mats2 = PtrToRom(
                        ReadU32(
                            workingRom,
                            sectionMat2Pointers + section * 4));

                    if (models < 0 ||
                        mats1 < 0 ||
                        mats2 < 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        AddVpw2Part(
                            "Venue",
                            String.Format(
                                "{0} {1:00}",
                                sectionNames[section],
                                i),
                            models + i * 2,
                            mats1 + i * 2,
                            mats2 + i * 2,
                            true,
                            true);
                    }
                }

                PopulateObjectGrid();

                status.Text = String.Format(
                    "{0}\r\n{1}: {2} draws, {3} project edits.",
                    profile.Name,
                    ArenaNames[currentArena],
                    parts.Count,
                    edits.Count);

                glControl.Invalidate();
            }
            catch (Exception exception)
            {
                status.Text =
                    "VPW2 arena parse error:\r\n" +
                    exception.Message;
            }
        }

        private void AddVpw2Part(
            string group,
            string name,
            int modelOffset,
            int material1Offset,
            int material2Offset,
            bool modelEditable,
            bool materialEditable)
        {
            if (modelOffset < 0 ||
                material1Offset < 0)
            {
                return;
            }

            UInt16 modelId =
                ReadU16(
                    workingRom,
                    modelOffset);
            UInt16 materialId =
                ReadU16(
                    workingRom,
                    material1Offset);
            int materialOffset =
                material1Offset;

            if (materialId == 0 &&
                material2Offset >= 0)
            {
                UInt16 alternate =
                    ReadU16(
                        workingRom,
                        material2Offset);

                if (alternate != 0)
                {
                    materialId = alternate;
                    materialOffset =
                        material2Offset;
                }
            }

            AddPart(
                group,
                name,
                modelId,
                materialId,
                modelOffset,
                materialOffset,
                modelEditable,
                materialEditable,
                0f);
        }

        private void AddPart(
            string group,
            string name,
            UInt16 modelId,
            UInt16 materialId,
            int modelOffset,
            int materialOffset,
            bool modelEditable,
            bool materialEditable,
            float rotationY)
        {
            if (modelId == 0 || materialId == 0)
            {
                return;
            }

            try
            {
                ArenaPart part = new ArenaPart();
                part.Group = group;
                part.Name = name;
                part.ModelId = modelId;
                part.MaterialId = materialId;
                part.ModelOffset = modelOffset;
                part.MaterialOffset = materialOffset;
                part.ModelEditable = modelEditable;
                part.MaterialEditable = materialEditable;
                part.Model = ParseCompact(modelId);
                part.Texture = GetTexture(materialId);
                part.RotationY = rotationY;
                part.Visible = true;
                parts.Add(part);
            }
            catch (Exception)
            {
                // A bad optional draw does not block the rest of the arena.
            }
        }

        private void PopulateObjectGrid()
        {
            updatingGrid = true;
            try
            {
                objectGrid.Rows.Clear();
                foreach (ArenaPart part in parts)
                {
                    int index = objectGrid.Rows.Add(
                        part.Visible,
                        part.Group,
                        part.Name,
                        part.ModelId.ToString("X4"),
                        part.MaterialId.ToString("X4"),
                        part.Model.Indices.Count / 3);
                    DataGridViewRow row = objectGrid.Rows[index];
                    row.Tag = part;
                    if (!part.ModelEditable)
                    {
                        row.Cells["Model"].ReadOnly = true;
                        row.Cells["Model"].Style.ForeColor = SystemColors.GrayText;
                    }
                    if (!part.MaterialEditable)
                    {
                        row.Cells["Material"].ReadOnly = true;
                        row.Cells["Material"].Style.ForeColor = SystemColors.GrayText;
                    }
                }
            }
            finally
            {
                updatingGrid = false;
            }
        }

        private void ObjectGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0) return;
            ArenaPart part = objectGrid.Rows[e.RowIndex].Tag as ArenaPart;
            if (part == null)
            {
                e.Cancel = true;
                return;
            }
            string column = objectGrid.Columns[e.ColumnIndex].Name;
            if (column == "Model" && !part.ModelEditable) e.Cancel = true;
            if (column == "Material" && !part.MaterialEditable) e.Cancel = true;
        }

        private void ObjectGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingGrid || e.RowIndex < 0) return;
            ArenaPart part = objectGrid.Rows[e.RowIndex].Tag as ArenaPart;
            if (part == null) return;
            string column = objectGrid.Columns[e.ColumnIndex].Name;

            try
            {
                if (column == "Model" && part.ModelEditable)
                {
                    UInt16 value = ParseHex16(objectGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                    if (value == 0 || value > profile.Count)
                        throw new InvalidDataException("Model File ID is outside the project FileTable.");
                    ApplyHalfwordEdit(part.ModelOffset, value);
                    BuildScene();
                }
                else if (column == "Material" && part.MaterialEditable)
                {
                    UInt16 value = ParseHex16(objectGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                    if (value == 0 || value + 1 > profile.Count)
                        throw new InvalidDataException("Material File ID is outside the project FileTable.");
                    ApplyHalfwordEdit(part.MaterialOffset, value);
                    BuildScene();
                }
            }
            catch (Exception exception)
            {
                Program.ErrorMessageBox(exception.Message);
                BuildScene();
            }
        }

        private void ObjectGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingGrid || e.RowIndex < 0 ||
                e.ColumnIndex != objectGrid.Columns["Visible"].Index) return;
            ArenaPart part = objectGrid.Rows[e.RowIndex].Tag as ArenaPart;
            if (part == null) return;
            object value = objectGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            part.Visible = value is bool && (bool)value;
            glControl.Invalidate();
        }

        private void ApplyHalfwordEdit(int offset, UInt16 value)
        {
            if (offset < 0)
                throw new InvalidOperationException("That object is preview-only.");
            if (!VPW2ArenaPatchFile.IsAllowedArenaOffset(baseRom, (UInt32)offset))
                throw new InvalidOperationException(String.Format(
                    "Refusing unsafe arena edit at 0x{0:X8}.", offset));
            WriteU16(workingRom, offset, value);
            edits[(UInt32)offset] = value;
        }

        private static UInt16 ParseHex16(object value)
        {
            string text = Convert.ToString(value).Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            UInt16 result;
            if (!UInt16.TryParse(
                text,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out result))
            {
                throw new FormatException("Enter a 16-bit hexadecimal File ID.");
            }
            return result;
        }

                private ArenaInfo GetArenaInfo(int index)
        {
            throw new NotSupportedException(
                "VPW2 uses its own arena table traversal.");
        }

        private byte[] ReadFile(UInt16 fileId)
        {
            if (fileId < 1 || fileId > profile.Count)
                throw new InvalidDataException("Bad File ID " + fileId.ToString("X4"));

            int entry = profile.Table + (fileId - 1) * 4;
            int next = profile.Table + fileId * 4;
            UInt32 a = ReadU32(workingRom, entry);
            UInt32 b = ReadU32(workingRom, next);
            int start = profile.First + (int)(a & 0xFFFFFFFE);
            int end = profile.First + (int)(b & 0xFFFFFFFE);
            if (start < 0 || end < start || end > workingRom.Length)
                throw new InvalidDataException("Invalid FileTable range for " + fileId.ToString("X4"));

            byte[] stored = new byte[end - start];
            System.Buffer.BlockCopy(workingRom, start, stored, 0, stored.Length);
            return (a & 1) != 0 ? LzssDecode(stored) : stored;
        }

        private static byte[] LzssDecode(byte[] packed)
        {
            if (packed.Length < 4) throw new InvalidDataException("Short LZSS stream.");
            int target = checked((int)ReadU32(packed, 0));
            byte[] output = new byte[target];
            byte[] ring = new byte[4096];
            int ringPos = 4096 - 18;
            int flags = 0;
            int source = 4;
            int destination = 0;

            while (source < packed.Length && destination < target)
            {
                flags >>= 1;
                if ((flags & 0x100) == 0) flags = packed[source++] | 0xFF00;
                if ((flags & 1) != 0)
                {
                    if (source >= packed.Length) break;
                    byte value = packed[source++];
                    output[destination++] = value;
                    ring[ringPos] = value;
                    ringPos = (ringPos + 1) & 0xFFF;
                }
                else
                {
                    if (source + 1 >= packed.Length) break;
                    int first = packed[source++];
                    int second = packed[source++];
                    first |= (second & 0xF0) << 4;
                    int length = (second & 0x0F) + 2;
                    for (int i = 0; i < length + 1 && destination < target; i++)
                    {
                        byte value = ring[(first + i) & 0xFFF];
                        output[destination++] = value;
                        ring[ringPos] = value;
                        ringPos = (ringPos + 1) & 0xFFF;
                    }
                }
            }
            if (destination != target)
                throw new InvalidDataException(String.Format("LZSS decoded {0}/{1} bytes.", destination, target));
            return output;
        }

        private CompactModel ParseCompact(UInt16 fileId)
        {
            byte[] data = ReadFile(fileId);
            if (data.Length < 8)
                throw new InvalidDataException(fileId.ToString("X4") + " is a short compact model.");

            int vertexCount = data[1] & 0x7F;
            int faceCount = data[2];
            int need = 8 + vertexCount * 8 + faceCount * 3;
            if (vertexCount == 0 || data.Length < need)
                throw new InvalidDataException(fileId.ToString("X4") + " is not a valid compact arena model.");

            int unit = (data[0] & 0x7F) + 1;
            int originX = Signed8(data[4]);
            int originY = Signed8(data[5]);
            int originZ = Signed8(data[6]);
            CompactModel model = new CompactModel();

            for (int i = 0; i < vertexCount; i++)
            {
                int offset = 8 + i * 8;
                ArenaVertex vertex = new ArenaVertex();
                vertex.X = (Signed8(data[offset]) + originX * 16) * unit;
                vertex.Y = (Signed8(data[offset + 1]) + originY * 16) * unit;
                vertex.Z = (Signed8(data[offset + 2]) + originZ * 16) * unit;
                vertex.R = data[offset + 3];
                vertex.G = data[offset + 4];
                vertex.U = data[offset + 5];
                vertex.B = data[offset + 6];
                vertex.V = data[offset + 7];
                model.Vertices.Add(vertex);
            }

            int faceBase = 8 + vertexCount * 8;
            for (int i = 0; i < faceCount; i++)
            {
                int offset = faceBase + i * 3;
                int a = data[offset];
                int b = data[offset + 1];
                int c = data[offset + 2];
                if (a >= vertexCount || b >= vertexCount || c >= vertexCount)
                    throw new InvalidDataException(fileId.ToString("X4") + " contains an out-of-range face.");
                model.Indices.Add(a);
                model.Indices.Add(b);
                model.Indices.Add(c);
            }
            return model;
        }

        private ArenaTexture GetTexture(UInt16 materialId)
        {
            ArenaTexture cached;
            if (textureCache.TryGetValue(materialId, out cached)) return cached;

            byte[] palette = ReadFile(materialId);
            byte[] texture = ReadFile((UInt16)(materialId + 1));
            if (texture.Length < 8)
                throw new InvalidDataException(materialId.ToString("X4") + " has a short texture.");

            int width = texture[0] + 1;
            int height = texture[1] + 1;
            int pixels = width * height;
            if (width < 1 || height < 1 || width > 512 || height > 512)
                throw new InvalidDataException("Invalid arena texture dimensions.");

            bool ci8 = texture.Length >= 8 + pixels && palette.Length >= 512;
            int paletteCount = ci8 ? 256 : 16;
            int bodyNeed = ci8 ? pixels : (pixels + 1) / 2;
            if (texture.Length < 8 + bodyNeed || palette.Length < paletteCount * 2)
                throw new InvalidDataException("Incomplete arena texture/material.");

            byte[] rgba = new byte[pixels * 4];
            for (int i = 0; i < pixels; i++)
            {
                int paletteIndex;
                if (ci8)
                    paletteIndex = texture[8 + i];
                else
                {
                    byte packed = texture[8 + (i >> 1)];
                    paletteIndex = (i & 1) != 0 ? packed & 0x0F : packed >> 4;
                }
                int paletteOffset = paletteIndex * 2;
                UInt16 color = (UInt16)((palette[paletteOffset] << 8) | palette[paletteOffset + 1]);
                int output = i * 4;
                rgba[output] = (byte)((((color >> 11) & 31) * 255) / 31);
                rgba[output + 1] = (byte)((((color >> 6) & 31) * 255) / 31);
                rgba[output + 2] = (byte)((((color >> 1) & 31) * 255) / 31);
                rgba[output + 3] = (byte)((color & 1) != 0 ? 255 : 0);
            }

            ArenaTexture result = new ArenaTexture();
            result.Width = width;
            result.Height = height;
            result.Ci8 = ci8;
            result.Rgba = rgba;
            result.ClampS = texture[4] == 1;
            result.ClampT = texture[5] == 1;
            textureCache[materialId] = result;
            return result;
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            glControl.MakeCurrent();
            validGl = true;
            GL.ClearColor(0.035f, 0.047f, 0.067f, 1f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Greater, 0.01f);
            glControl.Invalidate();
        }

        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            if (!validGl || glControl.ClientSize.Width <= 0 || glControl.ClientSize.Height <= 0) return;
            glControl.MakeCurrent();
            GL.Viewport(0, 0, glControl.ClientSize.Width, glControl.ClientSize.Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float aspect = glControl.ClientSize.Width / (float)glControl.ClientSize.Height;
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(42f), aspect, 1f, 50000f);
            Vector3 eye = GetCameraPosition();
            Matrix4 view = Matrix4.LookAt(eye, cameraTarget, Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref projection);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref view);

            if (showGrid.Checked) DrawGrid();
            if (doubleSided.Checked)
                GL.Disable(EnableCap.CullFace);
            else
            {
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Back);
            }
            GL.PolygonMode(
                MaterialFace.FrontAndBack,
                wireframe.Checked ? PolygonMode.Line : PolygonMode.Fill);

            foreach (ArenaPart part in parts)
            {
                if (part.Visible) DrawPart(part);
            }

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            glControl.SwapBuffers();
        }

        private void DrawPart(ArenaPart part)
        {
            EnsureGlTexture(part.Texture);
            GL.PushMatrix();
            if (part.RotationY != 0f) GL.Rotate(part.RotationY, 0f, 1f, 0f);
            GL.BindTexture(TextureTarget.Texture2D, part.Texture.GlId);
            GL.Begin(PrimitiveType.Triangles);
            for (int i = 0; i < part.Model.Indices.Count; i++)
            {
                ArenaVertex vertex = part.Model.Vertices[part.Model.Indices[i]];
                if (debugAttrs.Checked)
                    GL.Color4(vertex.R / 255f, vertex.G / 255f, vertex.B / 255f, 1f);
                else
                    GL.Color4(1f, 1f, 1f, 1f);

                // Proof 10 vertical fix: native T is used directly.
                GL.TexCoord2(
                    (vertex.U + 0.5f) / part.Texture.Width,
                    (vertex.V + 0.5f) / part.Texture.Height);
                GL.Vertex3(vertex.X, vertex.Y, vertex.Z);
            }
            GL.End();
            GL.PopMatrix();
        }

        private void EnsureGlTexture(ArenaTexture texture)
        {
            if (texture.GlId != 0) return;
            texture.GlId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture.GlId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)(texture.ClampS ? TextureWrapMode.ClampToEdge : TextureWrapMode.Repeat));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)(texture.ClampT ? TextureWrapMode.ClampToEdge : TextureWrapMode.Repeat));

            GCHandle handle = GCHandle.Alloc(texture.Rgba, GCHandleType.Pinned);
            try
            {
                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    texture.Width,
                    texture.Height,
                    0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private void DestroyGlTextures()
        {
            if (!validGl || glControl.IsDisposed) return;
            try
            {
                glControl.MakeCurrent();
                foreach (ArenaTexture texture in textureCache.Values)
                {
                    if (texture.GlId != 0)
                    {
                        GL.DeleteTexture(texture.GlId);
                        texture.GlId = 0;
                    }
                }
            }
            catch
            {
            }
        }

        private void DrawGrid()
        {
            GL.Disable(EnableCap.Texture2D);
            GL.Color4(0.20f, 0.26f, 0.35f, 1f);
            GL.Begin(PrimitiveType.Lines);
            for (int i = -6000; i <= 6000; i += 400)
            {
                GL.Vertex3(i, 0, -6000);
                GL.Vertex3(i, 0, 6000);
                GL.Vertex3(-6000, 0, i);
                GL.Vertex3(6000, 0, i);
            }
            GL.End();
            GL.Enable(EnableCap.Texture2D);
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragButton == MouseButtons.None) return;
            int dx = e.X - mouseLast.X;
            int dy = e.Y - mouseLast.Y;
            mouseLast = e.Location;

            if (dragButton == MouseButtons.Left)
            {
                cameraYaw += dx * 0.45f;
                cameraPitch -= dy * 0.35f;
                cameraPitch = Math.Max(-85f, Math.Min(85f, cameraPitch));
            }
            else if (dragButton == MouseButtons.Right)
            {
                float scale = Math.Max(1f, cameraDistance) / 900f;
                Vector3 eye = GetCameraPosition();
                Vector3 forward = Vector3.Normalize(cameraTarget - eye);
                Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
                Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
                cameraTarget -= right * (dx * scale);
                cameraTarget += up * (dy * scale);
            }
            glControl.Invalidate();
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            cameraDistance *= e.Delta > 0 ? 0.88f : 1.14f;
            cameraDistance = Math.Max(80f, Math.Min(30000f, cameraDistance));
            glControl.Invalidate();
        }

        private Vector3 GetCameraPosition()
        {
            float yaw = MathHelper.DegreesToRadians(cameraYaw);
            float pitch = MathHelper.DegreesToRadians(cameraPitch);
            float horizontal = (float)Math.Cos(pitch) * cameraDistance;
            return cameraTarget + new Vector3(
                (float)Math.Sin(yaw) * horizontal,
                (float)Math.Sin(pitch) * cameraDistance,
                (float)Math.Cos(yaw) * horizontal);
        }

        private void SetHardCamera()
        {
            cameraTarget = new Vector3(0f, 420f, -500f);
            SetCameraFromPosition(new Vector3(2600f, 1650f, 3100f));
            glControl.Invalidate();
        }

        private void SetCameraFromPosition(Vector3 position)
        {
            Vector3 delta = position - cameraTarget;
            cameraDistance = Math.Max(1f, delta.Length);
            cameraPitch = MathHelper.RadiansToDegrees(
                (float)Math.Asin(delta.Y / cameraDistance));
            cameraYaw = MathHelper.RadiansToDegrees(
                (float)Math.Atan2(delta.X, delta.Z));
        }

        private void FrameParts(string group)
        {
            bool found = false;
            Vector3 min = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
            Vector3 max = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);

            foreach (ArenaPart part in parts)
            {
                if (!part.Visible ||
                    (group != null && !String.Equals(part.Group, group, StringComparison.OrdinalIgnoreCase)))
                    continue;

                float radians = MathHelper.DegreesToRadians(part.RotationY);
                float cos = (float)Math.Cos(radians);
                float sin = (float)Math.Sin(radians);
                foreach (ArenaVertex vertex in part.Model.Vertices)
                {
                    float x = vertex.X * cos + vertex.Z * sin;
                    float z = -vertex.X * sin + vertex.Z * cos;
                    min.X = Math.Min(min.X, x);
                    min.Y = Math.Min(min.Y, vertex.Y);
                    min.Z = Math.Min(min.Z, z);
                    max.X = Math.Max(max.X, x);
                    max.Y = Math.Max(max.Y, vertex.Y);
                    max.Z = Math.Max(max.Z, z);
                    found = true;
                }
            }

            if (!found) return;
            cameraTarget = (min + max) * 0.5f;
            Vector3 size = max - min;
            float span = Math.Max(size.X, Math.Max(size.Y, size.Z));
            cameraDistance = Math.Max(300f, span * 1.35f);
            cameraYaw = 38f;
            cameraPitch = 20f;
            glControl.Invalidate();
        }

        private static int Signed8(byte value)
        {
            return value > 127 ? value - 256 : value;
        }

        private static UInt16 ReadU16(byte[] data, int offset)
        {
            if (offset < 0 || offset + 1 >= data.Length)
                throw new EndOfStreamException("Arena halfword offset is outside the ROM.");
            return (UInt16)((data[offset] << 8) | data[offset + 1]);
        }

        private static UInt32 ReadU32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 3 >= data.Length)
                throw new EndOfStreamException("Arena word offset is outside the ROM.");
            return ((UInt32)data[offset] << 24) |
                ((UInt32)data[offset + 1] << 16) |
                ((UInt32)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static void WriteU16(byte[] data, int offset, UInt16 value)
        {
            if (offset < 0 || offset + 1 >= data.Length)
                throw new EndOfStreamException("Arena write offset is outside the ROM.");
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        private static int PtrToRom(UInt32 pointer)
        {
            if (pointer < 0x80000000 || pointer >= 0x80800000) return -1;
            return checked((int)(pointer - 0x7FFFF400));
        }

        private sealed class ArenaProfile
        {
            public readonly string Name;
            public readonly int First;
            public readonly int Table;
            public readonly int Count;
            public ArenaProfile(string name, int first, int table, int count)
            {
                Name = name;
                First = first;
                Table = table;
                Count = count;
            }
        }

        private sealed class ArenaInfo
        {
            public int Header;
            public int ModelList;
            public int MaterialList;
            public int Count;
            public int FloorModels;
            public int FloorMaterials;
        }

        private sealed class CompactModel
        {
            public readonly List<ArenaVertex> Vertices = new List<ArenaVertex>();
            public readonly List<int> Indices = new List<int>();
        }

        private sealed class ArenaVertex
        {
            public float X;
            public float Y;
            public float Z;
            public byte U;
            public byte V;
            public byte R;
            public byte G;
            public byte B;
        }

        private sealed class ArenaTexture
        {
            public int Width;
            public int Height;
            public bool Ci8;
            public bool ClampS;
            public bool ClampT;
            public byte[] Rgba;
            public int GlId;
        }

        private sealed class ArenaPart
        {
            public string Group;
            public string Name;
            public UInt16 ModelId;
            public UInt16 MaterialId;
            public int ModelOffset;
            public int MaterialOffset;
            public bool ModelEditable;
            public bool MaterialEditable;
            public bool Visible;
            public float RotationY;
            public CompactModel Model;
            public ArenaTexture Texture;
        }
    }
}
