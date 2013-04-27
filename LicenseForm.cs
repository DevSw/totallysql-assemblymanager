using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace AssemblyManager
{
    public partial class LicenseForm : Form
    {
        public LicenseForm()
        {
            InitializeComponent();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTrial.Checked) btOK.Enabled = true;
            else if (Regex.IsMatch(tbKey.Text.Replace("-",""), "[0-9A-Fa-f]{16}")) btOK.Enabled = true;
            else btOK.Enabled = false;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            rbLicensed.Checked = true;
            string s = tbKey.Text.Replace("-", "");
            if (Regex.IsMatch(tbKey.Text.Replace("-",""), "[0-9A-Fa-f]{16}")) btOK.Enabled = true;
            else btOK.Enabled = false;
        }

        private void btOK_Click(object sender, EventArgs e)
        {
            MainAssemblyMgrForm frm = (MainAssemblyMgrForm)this.Owner;
            if (rbTrial.Checked)
            {
                if (!frm.us.LicenseState.LicenseValid) frm.us.MakeTrialLicense();
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                string s = tbKey.Text.Replace("-", "");
                byte[] key = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    key[i] = Byte.Parse(s.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
                }
                MainAssemblyMgrForm.LicenseResult result = frm.us.CheckLicense(key);
                if (result.LicenseValid)
                {
                    frm.us.License = key;
                    frm.us.LicenseState = result;
                    frm.us.Save();
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("Sorry, that doesn't appear to be a valid license key. Please try again or contact support@totallysql.com for assistance.", "License key not valid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }         
            
        }

        private void LicenseForm_Activated(object sender, EventArgs e)
        {
            MainAssemblyMgrForm frm = (MainAssemblyMgrForm)this.Owner;
            if (frm.us.LicenseState.LicenseValid)
            {
                int DaysRemaining = frm.us.LicenseState.LicenseExpires.Subtract(DateTime.UtcNow).Days + 1;
                rbTrial.Text = string.Format("I would like to continue my free trial ({0:G} days remaining)", DaysRemaining);
                rbTrial.Checked = true;
                btOK.Enabled = true;
            }
            else if (frm.us.LicenseState.LicenseExpires > DateTime.MinValue)
            {
                Prompt.Text = "Your trial license has expired. \nPlease enter a product key to activate the product";
                rbLicensed.Checked = true;
                rbTrial.Enabled = false;
                btOK.Enabled = false;
                tbKey.Focus();
            }
        }

        private void btQuit_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
