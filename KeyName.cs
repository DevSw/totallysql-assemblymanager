using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AssemblyManager;

namespace AssemblyManager
{
    public partial class KeyName : Form
    {
        MainAssemblyMgrForm.Login login;
        MainAssemblyMgrForm.AsymmetricKey key;

        public KeyName()
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
            login.Name = tbLoginName.Text;
            key.Name = tbKeyName.Text;
            login.permission = rbUnsafe.Checked ? MainAssemblyMgrForm.PermissionSet.UNSAFE : MainAssemblyMgrForm.PermissionSet.EXTERNAL_ACCESS;
            DialogResult = DialogResult.OK;
        }

        private void Input_Load(object sender, EventArgs e)
        {
            if (this.Tag == null) this.DialogResult = DialogResult.Cancel;
            else
            {
                login = (MainAssemblyMgrForm.Login)this.Tag;
                key = login.key;
                tbKeyName.Text = key.Name;
                tbLoginName.Text = login.Name;
                rbExternalAccess.Checked = login.permission == MainAssemblyMgrForm.PermissionSet.EXTERNAL_ACCESS;
                rbUnsafe.Checked = login.permission == MainAssemblyMgrForm.PermissionSet.UNSAFE;
                rbExternalAccess.Enabled = rbUnsafe.Enabled = rbExternalAccess.Checked;
            }
        }
    }
}
