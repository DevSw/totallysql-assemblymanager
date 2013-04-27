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
    public partial class PropertyBox : Form
    {
        private string[] PermissionSets = { "SAFE", "EXTERNAL", "UNRESTRICTED" };

        public PropertyBox()
        {
            InitializeComponent();
        }

        private void show(string prop, string val)
        {
            //ListViewItem li = new ListViewItem(new string[] { prop, val });
            //lvProperties.Items.Add(li);
            int i = dgProperties.Rows.Add(prop, val);
            dgProperties.AutoResizeRow(i, DataGridViewAutoSizeRowMode.AllCells);
        }

        private string parmConvert(string parm)
        {
            return parm == "Object" ? "sql_variant" : parm;
        }

        private void PropertyBox_Activated(object sender, EventArgs e)
        {
            dgProperties.Rows.Clear();
            //lvProperties.Items.Clear();
            object o = this.Tag;
            if (o != null)
            {
                switch (o.GetType().Name)
                {
                    case "Server":
                        MainAssemblyMgrForm.Server s = (MainAssemblyMgrForm.Server)o;
                        this.Text = "Properties for server " + s.name;
                        show("Name", s.name);
                        show("SQL Server Version", s.versionname);
                        show("Version Number", s.version);
                        show("Revision Level", s.level);
                        show("Edition", s.edition);
                        show("CLR Enabled", s.clr_enabled.ToString());
                        if (s.keys != null && s.keys.Count > 0)
                        {
                            string keys = "";
                            foreach (MainAssemblyMgrForm.AsymmetricKey key in s.keys) keys += keys == "" ? key.Name : ", " + key.Name;
                            show("Asymmetric Keys", keys);
                        }
                        if (s.logins != null && s.logins.Count > 0)
                        {
                            string logins = "";
                            foreach (MainAssemblyMgrForm.Login login in s.logins) logins += logins == "" ? login.Name : ", " + login.Name;
                            show("Associated Logins", logins);
                        }
                        break;
                    case "Database":
                        MainAssemblyMgrForm.Database db = (MainAssemblyMgrForm.Database)o;
                        this.Text = "Properties for database " + db.name;
                        show("Name", db.name);
                        show("Default schema", db.default_schema);
                        show("Trustworthy", db.trustworthy ? "On" : "Off");
                        break;
                    case "InstalledAssembly":
                        MainAssemblyMgrForm.InstalledAssembly a = (MainAssemblyMgrForm.InstalledAssembly)o;
                        if (a.is_assembly)
                        {
                            this.Text = "Properties for assembly " + a.name;
                            show("Name", a.name);
                            show("Version", a.version.ToString(4));
                            show("Culture", a.culture.EnglishName);
                            show("Platform", a.platform.ToString());
                            show("Created", a.create_date.ToString("dd-MMM-yyyy hh:mm:ss"));
                            show("Modified", a.modify_date.ToString("dd-MMM-yyyy hh:mm:ss"));
                            show("Permission set", PermissionSets[a.permission_set - 1]);
                            show("Public key token", string.Concat(Array.ConvertAll(a.publicKeyToken, x => x.ToString("x2"))));
                            if (a.key != null) show("Asymmetric Key", a.key.Name);
                            if (a.login != null) show("Associated Login", a.login.Name);
                            if (a.references != null && a.references.Count > 0)
                            {
                                string refs = "";
                                foreach (MainAssemblyMgrForm.InstalledAssembly da in a.references) refs += refs == "" ? da.name : ", " + da.name;
                                show("References", refs);
                            }
                            if (a.dependents != null && a.dependents.Count > 0)
                            {
                                string deps = "";
                                foreach (MainAssemblyMgrForm.InstalledAssembly da in a.dependents) deps += deps == "" ? da.name : ", " + da.name;
                                show("Referenced By", deps);
                            }
                        }
                        else
                        {
                            this.Text = "Properties for associated file " + a.name;
                            show("Name", a.name);
                            //show("Created", a.create_date.ToString("dd-MMM-yyyy hh:mm:ss"));
                            //show("Modified", a.modify_date.ToString("dd-MMM-yyyy hh:mm:ss"));
                            show("Size", a.bytes.Length.ToString("#,###") + " bytes");
                        }
                        break;
                    case "Function":
                        MainAssemblyMgrForm.Function f = (MainAssemblyMgrForm.Function)o;
                        this.Text = "Properties for " + f.ShortFunctionTypeName() + " " + f.name;
                        show("Name", f.name);
                        string inparms = "";
                        foreach (MainAssemblyMgrForm.Parameter pm in f.parameters.Where(p => p.name != "(output)"))
                            inparms += inparms == "" ? pm.name + " " + parmConvert(pm.type) : ";  " + pm.name + " " + parmConvert(pm.type);
                        switch (f.type)
                        {
                            case "PC":
                                show(f.parameters.Count > 2 ? "Input Parameters" : "Input Parameter", inparms);
                                show("Assembly Class", f.assembly_class);
                                show("Assembly Method", f.assembly_method);
                                break;
                            case "FS":
                                show(f.parameters.Count > 2 ? "Input Parameters" : "Input Parameter", inparms);
                                MainAssemblyMgrForm.Parameter opm = f.parameters.Single(p => p.name == "(output)");
                                show("Return Type", parmConvert(opm.type));
                                show("Assembly Class", f.assembly_class);
                                show("Assembly Method", f.assembly_method);
                                break;
                            case "AF":
                                show("Input Parameter", inparms);
                                opm = f.parameters.Single(p => p.name == "(output)");
                                show("Return Type", parmConvert(opm.type));
                                show("Assembly Class", f.assembly_class);
                                break;
                            case "FT":
                                show(f.parameters.Count > 2 ? "Input Parameters" : "Input Parameter", inparms);
                                opm = f.parameters.Single(p => p.name == "(output)");
                                show("Return Type", opm.type);
                                show("Assembly Class", f.assembly_class);
                                show("Assembly Method", f.assembly_method);
                                break;
                            case "TA":
                                show("Target", f.trigger.isdatabase ? "DATABASE" : f.trigger.target_schema + "." + f.trigger.target);
                                show("Events", (f.trigger.insteadof ? "INSTEAD OF " : "AFTER " + String.Join(";  ", f.trigger.events.ToArray())));
                                show("Assembly Class", f.assembly_class);
                                show("Assembly Method", f.assembly_method);
                                break;
                            case "UDT":
                                show("Assembly Class", f.assembly_class);
                                break;
                        }

                        break;
                }
                int h = 0;
                foreach (DataGridViewRow r in dgProperties.Rows) h += r.Height;
                //lvProperties.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                //this.Width = lvProperties.Columns[0].Width + lvProperties.Columns[1].Width + 25;
                //if (this.Width > Screen.GetBounds(this).Width) this.Width = Screen.GetBounds(this).Width;
                //this.Left = Screen.GetBounds(this).Width / 2 - (this.Width / 2);
                this.Height = h + 107;
                this.Top = Screen.GetBounds(this).Height / 2 - (this.Height / 2);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}
