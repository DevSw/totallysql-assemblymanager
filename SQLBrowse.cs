using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.Management;
using Microsoft.SqlServer.Management.Smo.Wmi;

namespace AssemblyManager
{
    public partial class SQLBrowse : Form
    {
        private bool network_scanned = false;

        public SQLBrowse()
        {
            InitializeComponent();
        }

        private void SQLBrowse_Activated(object sender, EventArgs e)
        {
            using (new MainAssemblyMgrForm.HourGlass("Browsing for local SQL Servers..."))
            {
                tvLocal.Nodes[0].ImageIndex = tvLocal.Nodes[0].SelectedImageIndex = 2;
                tvLocal.Nodes[0].Text = "Retrieving Data...";
                tabControl1.SelectedIndex = 0;
                network_scanned = false;
                tvLocal.Nodes[0].Nodes.Clear();
                this.Refresh();
                Application.DoEvents();

                ManagedComputer mc = new ManagedComputer();
                foreach (ServerInstance si in mc.ServerInstances)
                {
                    string name = si.Name == "MSSQLSERVER" ? mc.Name : mc.Name + "\\" + si.Name;
                    TreeNode tn = tvLocal.Nodes[0].Nodes.Add(name, name, 1, 1);
                }
                tvLocal.Nodes[0].ImageIndex = tvLocal.Nodes[0].SelectedImageIndex = 0;
                tvLocal.Nodes[0].Text = "Database Engine";
                tvLocal.Nodes[0].Expand();
                Application.DoEvents();
            }
        }

        private void tvLocal_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Level > 0)
            {
                bConnect.Enabled = true;
                this.Tag = e.Node.Text;
            }
            else
            {
                bConnect.Enabled = false;
                this.Tag = null;
            }
        }

        private void tvLocal_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            tvLocal.SelectedNode = e.Node;
        }

        private void tvLocal_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Level > 0)
            {
                this.Tag = e.Node.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void bConnect_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void tvNetwork_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if(e.Node.Level > 0) tvNetwork.SelectedNode = e.Node;
        }

        private void tvNetwork_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Level > 0)
            {
                bConnect.Enabled = true;
                this.Tag = e.Node.Text;
            }
            else
            {
                bConnect.Enabled = false;
                this.Tag = null;
            }
        }

        private void tvNetwork_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Level > 0)
            {
                this.Tag = e.Node.Text;
                bConnect_Click(sender, e);
            }
            else this.Tag = null;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0) return;
            if (network_scanned) return;
            using (new MainAssemblyMgrForm.HourGlass("Browsing for SQL Servers on network..."))
            {
                tvNetwork.Nodes[0].Nodes.Clear();
                tvNetwork.Nodes[0].ImageIndex = tvNetwork.Nodes[0].SelectedImageIndex = 2;
                tvNetwork.Nodes[0].Text = "Retrieving Data...";
                tabControl1.SelectedTab.Refresh();
                tvNetwork.Refresh();
                Application.DoEvents();
                SqlDataSourceEnumerator sdse = SqlDataSourceEnumerator.Instance;
                DataTable dt = sdse.GetDataSources();
                foreach (DataRow dr in dt.Rows)
                {
                    string server = dr.IsNull("ServerName") ? "" : (string)dr["ServerName"];
                    string instance = dr.IsNull("InstanceName") ? "" : (string)dr["InstanceName"];
                    string clustered = dr.IsNull("IsClustered") ? "" : (string)dr["IsClustered"];
                    string version = dr.IsNull("Version") ? "" : ((string)dr["Version"]).Split('.')[0];
                    if (version == "" || int.Parse(version) >= 9)
                    {
                        string name = instance == "" ? server : server + "\\" + instance;
                        TreeNode tn = tvNetwork.Nodes[0].Nodes.Add(name, name, 1, 1);
                    }
                    tvNetwork.Nodes[0].Expand();
                    Application.DoEvents();
                }
                tvNetwork.Nodes[0].ImageIndex = tvNetwork.Nodes[0].SelectedImageIndex = 0;
                tvNetwork.Nodes[0].Text = "Database Engine";
                network_scanned = true;
            }
 
        }
    }
}
