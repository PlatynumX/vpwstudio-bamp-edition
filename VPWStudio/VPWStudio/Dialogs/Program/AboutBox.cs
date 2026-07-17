using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace VPWStudio
{
    public partial class AboutBox : Form
    {
        public AboutBox()
        {
            InitializeComponent();

            Text = "About VPWStudio BAMP";

            RemoveAboutLinks();
            ConfigureVersionArea();

            StringBuilder information = new StringBuilder();

            information.AppendLine(
                "A Be A Man Productions fork of VPWStudio.");
            information.AppendLine();
            information.AppendLine(
                "BAMP Edition maintained by PlatynumX.");
            information.AppendLine(
                "Original VPWStudio created by freem.");
            information.AppendLine();
            information.AppendLine(
                "This fork contains substantial BAMP-specific code, " +
                "workflow changes, experimental editors, and behavior " +
                "that does not exist in the original VPWStudio.");
            information.AppendLine();
            information.AppendLine(
                "freem does not maintain, support, test, or troubleshoot " +
                "this fork and is not responsible for its bugs, builds, " +
                "modified ROMs, or altered behavior.");
            information.AppendLine();
            information.AppendLine(
                "DON'T FUCKING ASK FREEM FOR HELP WITH THIS VERSION.");
            information.AppendLine();
            information.AppendLine("Current BAMP additions include:");
            information.AppendLine(
                "- Game Introduction Editor and project writeback");
            information.AppendLine(
                "- Transparent PNG texture conversion");
            information.AppendLine(
                "- Bundled AKI Sound Studio workflow");
            information.AppendLine(
                "- BAMP-specific ROM editing and build changes");
            information.AppendLine();
            information.AppendLine(
                "Always keep backups of project files and base ROMs.");
            information.AppendLine(
                "This software is unfinished and may corrupt data.");
            information.AppendLine();
            information.AppendLine(
                "Thanks to the AKI hacking community and everyone whose " +
                "research helped make the original project possible.");
            information.AppendLine();
            information.AppendLine(
                "This tool remains dedicated to the memory of Maximo.");
            information.AppendLine();
            information.AppendLine(
                "Third-party components retain their respective licenses.");

            tbInformation.Text = information.ToString();
        }

        private void RemoveAboutLinks()
        {
            if (linkLabelAJWorld != null)
            {
                tlpBottomSection.Controls.Remove(
                    linkLabelAJWorld);
                linkLabelAJWorld.Visible = false;
                linkLabelAJWorld.Enabled = false;
            }

            if (linkLabelGitHub != null)
            {
                tlpBottomSection.Controls.Remove(
                    linkLabelGitHub);
                linkLabelGitHub.Visible = false;
                linkLabelGitHub.Enabled = false;
            }

            tlpBottomSection.ColumnStyles.Clear();
            tlpBottomSection.ColumnCount = 1;
            tlpBottomSection.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            tlpBottomSection.SetColumn(
                okButton,
                0);
            tlpBottomSection.SetColumnSpan(
                okButton,
                1);

            okButton.Anchor = AnchorStyles.None;
            okButton.Width = 180;
            okButton.Text = "&OK";
        }

        private void ConfigureVersionArea()
        {
            if (tlpMain.RowStyles.Count > 1)
            {
                tlpMain.RowStyles[1].SizeType =
                    SizeType.Absolute;
                tlpMain.RowStyles[1].Height = 70F;
            }

            ClientSize = new Size(640, 470);

            labelVersion.Font =
                new Font(
                    labelVersion.Font,
                    FontStyle.Bold);

            labelVersion.ForeColor =
                Color.DarkRed;

            labelVersion.Text =
                "VPWStudio BAMP Edition" +
                Environment.NewLine +
                "Version " +
                SharedStrings.BampVersion +
                Environment.NewLine +
                "DON'T FUCKING ASK FREEM FOR HELP WITH THIS VERSION.";

            string buildDate =
                ReadEmbeddedText(
                    "VPWStudio.builddate.txt");

            string gitHash =
                ReadEmbeddedText(
                    "VPWStudio.githash.txt");

            if (buildDate.Length > 19)
            {
                buildDate =
                    buildDate.Substring(0, 19);
            }

            if (!String.IsNullOrWhiteSpace(buildDate) ||
                !String.IsNullOrWhiteSpace(gitHash))
            {
                labelVersion.Text +=
                    Environment.NewLine +
                    "Built " +
                    buildDate.Trim() +
                    (
                        String.IsNullOrWhiteSpace(gitHash)
                        ? String.Empty
                        : " | Git " + gitHash.Trim()
                    );
            }
        }

        private static string ReadEmbeddedText(
            string resourceName)
        {
            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (
                    Stream stream =
                        assembly.GetManifestResourceStream(
                            "VPWStudio." +
                            resourceName))
                {
                    if (stream == null)
                    {
                        return String.Empty;
                    }

                    using (
                        StreamReader reader =
                            new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                return String.Empty;
            }
        }

        private void linkLabelAJWorld_LinkClicked(
            object sender,
            LinkLabelLinkClickedEventArgs e)
        {
        }

        private void linkLabelGitHub_LinkClicked(
            object sender,
            LinkLabelLinkClickedEventArgs e)
        {
        }
    }
}
