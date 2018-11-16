using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AssetStudio
{
    public partial class ExportOptions : Form
    {
        public ExportOptions()
        {
            this.InitializeComponent();
            this.converttexture.Checked = (bool) Properties.Settings.Default["convertTexture"];
            this.convertAudio.Checked = (bool) Properties.Settings.Default["convertAudio"];
            var str = (string) Properties.Settings.Default["convertType"];
            foreach (Control c in this.panel1.Controls)
            {
                if (c.Text == str)
                {
                    ((RadioButton) c).Checked = true;
                    break;
                }
            }
            this.eulerFilter.Checked = (bool) Properties.Settings.Default["EulerFilter"];
            this.filterPrecision.Value = (decimal) Properties.Settings.Default["filterPrecision"];
            this.allFrames.Checked = (bool) Properties.Settings.Default["allFrames"];
            this.allBones.Checked = (bool) Properties.Settings.Default["allBones"];
            this.skins.Checked = (bool) Properties.Settings.Default["skins"];
            this.boneSize.Value = (decimal) Properties.Settings.Default["boneSize"];
            this.scaleFactor.Value = (decimal) Properties.Settings.Default["scaleFactor"];
            this.flatInbetween.Checked = (bool) Properties.Settings.Default["flatInbetween"];
            this.fbxVersion.SelectedIndex = (int) Properties.Settings.Default["fbxVersion"];
            this.fbxFormat.SelectedIndex = (int) Properties.Settings.Default["fbxFormat"];
        }

        private void exportOpnions_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default[((CheckBox) sender).Name] = ((CheckBox) sender).Checked;
            Properties.Settings.Default.Save();
        }

        private void fbxOKbutton_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default["convertTexture"] = this.converttexture.Checked;
            Properties.Settings.Default["convertAudio"] = this.convertAudio.Checked;
            foreach (Control c in this.panel1.Controls)
            {
                if (((RadioButton) c).Checked)
                {
                    Properties.Settings.Default["convertType"] = c.Text;
                    break;
                }
            }
            Properties.Settings.Default["eulerFilter"] = this.eulerFilter.Checked;
            Properties.Settings.Default["filterPrecision"] = this.filterPrecision.Value;
            Properties.Settings.Default["allFrames"] = this.allFrames.Checked;
            Properties.Settings.Default["allBones"] = this.allBones.Checked;
            Properties.Settings.Default["skins"] = this.skins.Checked;
            Properties.Settings.Default["boneSize"] = this.boneSize.Value;
            Properties.Settings.Default["scaleFactor"] = this.scaleFactor.Value;
            Properties.Settings.Default["flatInbetween"] = this.flatInbetween.Checked;
            Properties.Settings.Default["fbxVersion"] = this.fbxVersion.SelectedIndex;
            Properties.Settings.Default["fbxFormat"] = this.fbxFormat.SelectedIndex;
            Properties.Settings.Default.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}