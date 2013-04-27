using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AssemblyManager
{
    public partial class SQLConnect : Form  //Hello
    {
        private SQLBrowse browser = null;

        public SQLConnect()
        {
            InitializeComponent();
        }

        private void SQLConnect_Load(object sender, EventArgs e)
        {

            MainAssemblyMgrForm frm = (MainAssemblyMgrForm)this.Owner;

            foreach (SqlConnectionStringBuilder s in frm.us.Connections) if (!cbServerName.Items.Contains(s.DataSource)) cbServerName.Items.Insert(0, s.DataSource);
            string server = frm.us.LastServer;
            DisplayServer(frm, server);
        }

        private void DisplayServer(MainAssemblyMgrForm frm, string server)
        {
            string username, password, protocol;
            SqlConnectionStringBuilder connection;
            int packetsize = 4096, contimeout = 15, extimeout = 0;
            bool remember, encrypt = false;

            connection = frm.us.Connections.FirstOrDefault(p => p.DataSource == server);
            if (server != null) cbServerName.Text = server;
            if (connection != null)
            {
                cbAuthentication.SelectedIndex = connection.IntegratedSecurity ? 0 : 1;
                if (!connection.IntegratedSecurity)
                {
                    remember = connection.PersistSecurityInfo;
                    xbRememberPassword.Checked = remember;
                    password = null;
                    frm.us.UnObscure(connection);
                    if(remember) password = connection.Password;
                    username = connection.UserID;
                    frm.us.Obscure(connection);
                    if (password != null) tbPassword.Text = password;
                    if (username != null) cbUserName.Text = username;
                }
                protocol = connection.NetworkLibrary;
                switch (protocol)
                {
                    case "dbmslpcn":
                        cbProtocol.SelectedItem = "Shared Memory";
                        break;
                    case "dbmssocn":
                        cbProtocol.SelectedItem = "TCP/IP";
                        break;
                    case "dbnmpntw":
                        cbProtocol.SelectedItem = "Named Pipes";
                        break;
                    default:
                        cbProtocol.SelectedIndex = 0;
                        break;
                }
                packetsize = connection.PacketSize;
                contimeout = connection.ConnectTimeout;
                encrypt = connection.Encrypt;
                udPacketSize.Value = packetsize;
                udConnectionTimeout.Value = contimeout;
                udExecutionTimeout.Value = extimeout;
                xbEncrypt.Checked = encrypt;
            }
            else cbAuthentication.SelectedIndex = 0;
        }

        private void cbAuthentication_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbAuthentication.SelectedIndex == 0)
            {
                cbUserName.Text = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                tbPassword.Text = "";
                cbUserName.Enabled = false;
                lUserName.Enabled = false;
                tbPassword.Enabled = false;
                lPassword.Enabled = false;
                xbRememberPassword.Enabled = false;
                xbRememberPassword.Checked = false;
            }
            else
            {
                cbUserName.Text = "";
                tbPassword.Text = "";
                cbUserName.Enabled = true;
                lUserName.Enabled = true;
                tbPassword.Enabled = true;
                lPassword.Enabled = true;
                xbRememberPassword.Enabled = true;
                xbRememberPassword.Checked = false;
            }
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            this.Tag = null;
            this.Close();
        }

        private void bConnect_Click(object sender, EventArgs e)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            string version = "";
            int v = 0;

            string server = cbServerName.Text;
            string username = cbUserName.Text;
            string password = tbPassword.Text;
            int authentication = cbAuthentication.SelectedIndex;

            if (server == "" || server == "." || server == "(local)") server = Environment.MachineName;

            builder.DataSource = server;
            builder.ConnectTimeout = 10;
            builder.UserID = username;
            if (authentication > 0)
            {
                builder.Password = password;
                builder.IntegratedSecurity = false;
            }
            else builder.IntegratedSecurity = true;
            builder.PacketSize = (int)udPacketSize.Value;
            builder.ConnectTimeout = (int)udConnectionTimeout.Value;
            builder.Encrypt = xbEncrypt.Checked;
            builder.PersistSecurityInfo = xbRememberPassword.Checked;
            switch((string)cbProtocol.SelectedItem)
            {
                case "<default>":
                    break;
                case "Shared Memory":
                    builder.NetworkLibrary = "dbmslpcn";
                    break;
                case "TCP/IP":
                    builder.NetworkLibrary = "dbmssocn";
                    break;
                case "Named Pipes":
                    builder.NetworkLibrary = "dbnmpntw";
                    break;
            }

            SqlConnection sc = new SqlConnection(builder.ConnectionString);
            try
            {
                using (new MainAssemblyMgrForm.HourGlass("Attempting To Connect..."))
                {
                    SqlCommand comm = new SqlCommand(Resource.sqlGetVersion, sc);
                    sc.Open();
                    version = (string)comm.ExecuteScalar();
                    sc.Close();
                    v = int.Parse(version.Split('.')[0]);
                    if (v < 9)
                    {
                        string root = Resource.errSqlVersionNotSupported;
                        if (v == 8) root = root.Replace("%VERSION%", "SQL Server 2000");
                        else root = root.Replace("%VERSION%", "SQL Server Version " + v.ToString());
                        MessageBox.Show(root);
                        return;
                    }
                }
            }
            catch(Exception ee)
            {
                MessageBox.Show(ee.Message);
                return;
            }

            object[] o = new object[2];
            o[0] = sc;
            o[1] = builder;
            this.Tag = o;
            this.Close();
        }

        private void cbServerName_SelectedIndexChanged(object sender, EventArgs e)
        {
            string server;
            if (cbServerName.Text == "<Browse for more...>")
            {
                if (browser == null) browser = new SQLBrowse();
                browser.Tag = null;
                DialogResult dr = browser.ShowDialog();
                if (dr == DialogResult.Cancel)
                {
                    cbServerName.SelectedIndex = 0;
                    return;
                }
                server = (string)browser.Tag;
                if (!cbServerName.Items.Contains(server)) cbServerName.Items.Insert(0, server);
                cbServerName.SelectedItem = server;
//                return;
            }

            server = cbServerName.Text;
            MainAssemblyMgrForm frm = (MainAssemblyMgrForm)this.Owner;
            DisplayServer(frm, server);
        }

        private void bOptions_Click(object sender, EventArgs e)
        {
            if (gbNetwork.Visible)
            {
                gbNetwork.Visible = false;
                gbConnection.Visible = false;
                bReset.Visible = false;
                this.Height = 209;
                bOptions.Text = "&Options >>";
            }
            else
            {
                gbNetwork.Visible = true;
                gbConnection.Visible = true;
                bReset.Visible = true;
                this.Height = 475;
                bOptions.Text = "&Options <<";
            }
        }

        private void bReset_Click(object sender, EventArgs e)
        {
            cbProtocol.SelectedIndex = 0;
            udPacketSize.Value = 4096;
            udConnectionTimeout.Value = 15;
            udExecutionTimeout.Value = 0;
            xbEncrypt.Checked = false;
        }
    }
}
