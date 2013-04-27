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
    public partial class ParameterBox : Form
    {
        Input ip = new Input();
        bool library;

        public ParameterBox()
        {
            InitializeComponent();
        }

        private void show(string parm, string type, string def, MainAssemblyMgrForm.Parameter p)
        {
            ListViewItem li = new ListViewItem(new string[] { parm, type, def });
            li.Tag = p;
            lvParameters.Items.Add(li);
        }

        private string parmConvert(string parm)
        {
            return parm == "Object" ? "sql_variant" : parm;
        }

        private void ParameterBox_Activated(object sender, EventArgs e)
        {
            lvParameters.Items.Clear();
            object o = this.Tag;
            if (o != null)
            {
                switch (o.GetType().Name)
                {
                    case "TreeNode":
                        TreeNode tn = (TreeNode)o;
                        library = (tn.TreeView.Name == "tvAssemblies");
                        MainAssemblyMgrForm.Function f = (MainAssemblyMgrForm.Function)tn.Tag;
                        this.Text = "Parameters for " + f.name;
                        foreach (MainAssemblyMgrForm.Parameter p in f.parameters)
                        {
                            if(p.name != "(output)") 
                            {
                                show(p.name, parmConvert(p.type), p.default_value, p);
                            }
                        }
                        break;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void getParameterDefault()
        {
            if (lvParameters.SelectedItems.Count > 0)
            {
                string[] s = new string[2];
                int i = lvParameters.SelectedIndices[0];
                ListViewItem lvi = lvParameters.Items[i];
                s[0] = lvi.SubItems[1].Text;
                s[1] = lvi.SubItems[2].Text;
                ip.Tag = s;
                DialogResult dr = ip.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    lvParameters.Items[i].SubItems[2].Text = (string)ip.Tag;
                    MainAssemblyMgrForm.Parameter p = (MainAssemblyMgrForm.Parameter)lvParameters.Items[i].Tag;
                    MainAssemblyMgrForm frm = (MainAssemblyMgrForm)this.Owner;
                    if (library) frm.ChangeLibraryParameterDefault(p, null, (string)ip.Tag);
                    else frm.ChangeParameterDefault(p, null, (string)ip.Tag);
                    lvParameters.Refresh();
                }
            }
        }

        private void getParameterDefault(object sender, EventArgs e)
        {
            getParameterDefault();
        }

        private void addEditDefaultValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            getParameterDefault();
        }

        private void lvParameters_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvParameters.SelectedItems.Count > 0)
            {
                lvParameters.ContextMenuStrip = cmParameter;
                if (lvParameters.SelectedItems[0].SubItems[2].Text == "")
                    addEditDefaultValueToolStripMenuItem.Text = "Add Default Value...";
                else
                    addEditDefaultValueToolStripMenuItem.Text = "Edit Default Value...";
            }
            else lvParameters.ContextMenuStrip = null;
        }
    }
}
