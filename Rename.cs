using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CLRAssemblyInstaller
{
    public partial class Rename : Form
    {

        public Rename()
        {
            InitializeComponent();
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            this.Tag = null;
            this.DialogResult = DialogResult.Cancel;
        }

        private void bOK_Click(object sender, EventArgs e)
        {
            string s;
            s = tbName.Text;

            if (s != "")
            {
                this.Tag = s;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                string root = Resource.errEmptyStringNotAValidName;
                MessageBox.Show(root, "Invalid Identifier");
            }
        }

        private void Input_Load(object sender, EventArgs e)
        {
            string s = (string)this.Tag;
            tbName.Text = s;
            tbName.Select();
        }
    }
}
