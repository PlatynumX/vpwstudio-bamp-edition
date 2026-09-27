using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using VPWStudio.GameSpecific.VPW2;

namespace VPWStudio.Editors.VPW2
{
    public partial class WrestlerMain_VPW2
    {
        // BAMP_VPW2_EXPANDED_THEME_MENU
        //
        // The Ospreay/Omega 32-theme VPW2 build exposes selectors 00-1F,
        // but those visible selectors are not the same thing as the song ID
        // byte stored in WrestlerDefinition.ThemeSong.
        private const UInt16 BAMP_THEME_TEXT_FTID = 0x0084;

        private bool _bampExpandedThemeMenuActive = false;
        private bool _bampThemeMenuLoading = false;

        private readonly List<BampThemeMusicChoice> _bampThemeMusicChoices =
            new List<BampThemeMusicChoice>();

        private sealed class BampThemeMusicChoice
        {
            public readonly byte Selector;
            public readonly byte TrackId;
            public readonly string Name;

            public BampThemeMusicChoice(
                byte selector,
                byte trackId,
                string name)
            {
                Selector = selector;
                TrackId = trackId;
                Name = name ?? String.Empty;
            }

            public override string ToString()
            {
                return String.Format(
                    "{0:X2}  [Track 0x{1:X2}]  {2}",
                    Selector,
                    TrackId,
                    Name);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            // Install before normal Load handlers/user interaction.
            TryEnableBampExpandedThemeMenu();
            base.OnLoad(e);
        }

        private void TryEnableBampExpandedThemeMenu()
        {
            if (_bampExpandedThemeMenuActive)
            {
                return;
            }

            AkiText themeText;

            try
            {
                themeText = LoadBampThemeText();
            }
            catch
            {
                // Keep freem's original behavior on stock/unrecognized ROMs.
                return;
            }

            // This mapping belongs to the 32-choice expanded VPW2 layout.
            // If FTID 0084 is not exactly that shape, leave stock behavior.
            if (themeText == null || themeText.Entries.Count != 32)
            {
                return;
            }

            _bampThemeMusicChoices.Clear();

            for (int selector = 0; selector < 32; selector++)
            {
                string name = themeText.Entries[selector].Text;
                byte trackId = GetBampExpandedTrackId(selector);

                _bampThemeMusicChoices.Add(
                    new BampThemeMusicChoice(
                        (byte)selector,
                        trackId,
                        name));
            }

            // Disable freem's stock handlers.  The stock theme handler writes
            // SelectedIndex directly into ThemeSong, which is incorrect for
            // expanded selectors.
            cbThemeMusic.SelectedIndexChanged -=
                cbThemeMusic_SelectedIndexChanged;

            lbWrestlers.SelectedIndexChanged -=
                lbWrestlers_SelectedIndexChanged;

            _bampThemeMenuLoading = true;

            cbThemeMusic.BeginUpdate();
            cbThemeMusic.Items.Clear();

            foreach (BampThemeMusicChoice choice in _bampThemeMusicChoices)
            {
                cbThemeMusic.Items.Add(choice);
            }

            cbThemeMusic.EndUpdate();

            cbThemeMusic.DropDownWidth = 360;
            cbThemeMusic.SelectedIndex = -1;

            _bampThemeMenuLoading = false;

            cbThemeMusic.SelectedIndexChanged +=
                BampThemeMusic_SelectedIndexChanged;

            lbWrestlers.SelectedIndexChanged +=
                BampWrestlers_SelectedIndexChanged;

            _bampExpandedThemeMenuActive = true;
        }

        private AkiText LoadBampThemeText()
        {
            if (Program.CurrentProject == null ||
                Program.CurrentInputROM == null ||
                Program.CurrentInputROM.Data == null ||
                Program.CurrentProject.ProjectFileTable == null ||
                Program.CurrentProject.ProjectFileTable.Entries == null ||
                !Program.CurrentProject.ProjectFileTable.Entries.ContainsKey(
                    BAMP_THEME_TEXT_FTID))
            {
                return null;
            }

            FileTableEntry entry =
                Program.CurrentProject.ProjectFileTable.Entries[
                    BAMP_THEME_TEXT_FTID];

            if (entry.HasReplacementFile())
            {
                string replacementPath =
                    Program.ConvertRelativePath(entry.ReplaceFilePath);

                if (!File.Exists(replacementPath))
                {
                    return null;
                }

                if (String.Equals(
                    Path.GetExtension(replacementPath),
                    ".lzss",
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (FileStream input =
                        new FileStream(
                            replacementPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read))
                    using (BinaryReader reader = new BinaryReader(input))
                    using (MemoryStream decoded = new MemoryStream())
                    using (BinaryWriter writer =
                        new BinaryWriter(
                            decoded,
                            System.Text.Encoding.Default,
                            true))
                    {
                        AsmikLzss.Decode(reader, writer);
                        writer.Flush();
                        decoded.Position = 0;

                        using (BinaryReader decodedReader =
                            new BinaryReader(
                                decoded,
                                System.Text.Encoding.Default,
                                true))
                        {
                            return new AkiText(decodedReader);
                        }
                    }
                }

                using (FileStream input =
                    new FileStream(
                        replacementPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                using (BinaryReader reader = new BinaryReader(input))
                {
                    return new AkiText(reader);
                }
            }

            using (MemoryStream romStream =
                new MemoryStream(
                    Program.CurrentInputROM.Data,
                    false))
            using (BinaryReader romReader = new BinaryReader(romStream))
            using (MemoryStream extracted = new MemoryStream())
            using (BinaryWriter extractedWriter =
                new BinaryWriter(
                    extracted,
                    System.Text.Encoding.Default,
                    true))
            {
                // Follow the project's real FileTable. No hardcoded ROM offset
                // is used for FTID 0084.
                Program.CurrentProject.ProjectFileTable.ExtractFile(
                    romReader,
                    extractedWriter,
                    BAMP_THEME_TEXT_FTID);

                extractedWriter.Flush();
                extracted.Position = 0;

                using (BinaryReader textReader =
                    new BinaryReader(
                        extracted,
                        System.Text.Encoding.Default,
                        true))
                {
                    return new AkiText(textReader);
                }
            }
        }

        private static byte GetBampExpandedTrackId(int selector)
        {
            // Selector 00: None.
            if (selector == 0x00)
            {
                return 0x00;
            }

            // Selectors 01-08 -> new high-level IDs 20-27.
            if (selector >= 0x01 && selector <= 0x08)
            {
                return (byte)(0x20 + (selector - 0x01));
            }

            // Preserved VPW2 special themes.
            switch (selector)
            {
                case 0x09:
                    return 0x1E; // NTV Sports Theme
                case 0x0A:
                    return 0x1F; // Triple Crown Champion Theme
                case 0x0B:
                    return 0x14; // Original Song A
                case 0x0C:
                    return 0x15; // Original Song B
            }

            // Selectors 0D-1F -> new high-level IDs 28-3A.
            if (selector >= 0x0D && selector <= 0x1F)
            {
                return (byte)(0x28 + (selector - 0x0D));
            }

            return (byte)selector;
        }

        private void BampWrestlers_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (lbWrestlers.SelectedIndex < 0)
            {
                return;
            }

            WrestlerDefinition wdef =
                WrestlerDefs[lbWrestlers.SelectedIndex];

            byte actualTrackId = wdef.ThemeSong;

            _bampThemeMenuLoading = true;

            try
            {
                // LoadEntryData() is stock code and assigns ThemeSong directly
                // to ComboBox.SelectedIndex. Expanded IDs can be > 0x1F, so
                // temporarily give it a safe index, then restore the raw byte.
                wdef.ThemeSong = 0;
                LoadEntryData(wdef);
            }
            finally
            {
                wdef.ThemeSong = actualTrackId;
            }

            SelectBampThemeByTrackId(actualTrackId);

            _bampThemeMenuLoading = false;
        }

        private void SelectBampThemeByTrackId(byte trackId)
        {
            int found = -1;

            for (int i = 0; i < _bampThemeMusicChoices.Count; i++)
            {
                if (_bampThemeMusicChoices[i].TrackId == trackId)
                {
                    found = i;
                    break;
                }
            }

            cbThemeMusic.SelectedIndex = found;
        }

        private void BampThemeMusic_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (_bampThemeMenuLoading ||
                lbWrestlers.SelectedIndex < 0 ||
                cbThemeMusic.SelectedIndex < 0 ||
                cbThemeMusic.SelectedIndex >=
                    _bampThemeMusicChoices.Count)
            {
                return;
            }

            BampThemeMusicChoice choice =
                _bampThemeMusicChoices[
                    cbThemeMusic.SelectedIndex];

            // Save the actual expanded song/track byte, not the UI selector.
            WrestlerDefs[
                lbWrestlers.SelectedIndex].ThemeSong =
                    choice.TrackId;
        }
    }
}
