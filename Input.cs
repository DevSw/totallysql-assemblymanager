using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AssemblyManager
{
    public partial class Input : Form
    {
        private string type = "";

        public Input()
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
            s = tbDefault.Text;

            if (s == "" || MainAssemblyMgrForm.Database.Parses(type, s))
            {
                this.Tag = s;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                string root = Resource.mbxParameterDefaultValueDoesntParsePrompt;
                root = root.Replace("%VALUE%", s);
                root = root.Replace("%TYPE%", type);
                MessageBox.Show(root, "Parameter default not in valid format");
            }
        }

        private void Input_Load(object sender, EventArgs e)
        {
            string[] s = (string[])this.Tag;
            type = s[0];
            tbDefault.Text = s[1];
            tbDefault.Select();
        }
    }
}
