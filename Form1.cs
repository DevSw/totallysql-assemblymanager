using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Configuration;
using System.Windows.Forms;
using System.Reflection;
using System.Globalization;
using Microsoft.SqlServer.Server;
using Microsoft.Win32;
using System.Xml;
using Infragistics.Win.UltraWinTabControl;


namespace AssemblyManager
{

    public partial class MainAssemblyMgrForm : Form
    {

        SQLConnect connectform = new SQLConnect();
        ParameterBox parmbox = new ParameterBox();
        PropertyBox propbox = new PropertyBox();
        KeyName keynamebox = new KeyName();
        public UserSettings us = new UserSettings();

        public List<SqlConnection> connections = new List<SqlConnection>();
        public List<string> externalassemblies = new List<string>();
        private string[] PermissionSets = { "SAFE", "EXTERNAL ACCESS", "UNSAFE" };
        private int ActionSequence = 0;
        private bool changingChecks = false;
        bool resolving = false;
        private bool Stop = false;
        private bool SuppressEvents = false;
        private int scriptTab = 1;
        private Font fontRegular;
        private Font fontItalic;
        private Font fontStrikeout;
        private Font fontCourier;

        #region Helper Functions

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        public class HourGlass : IDisposable
        {
            public HourGlass(string message)
            {
                MainAssemblyMgrForm mf = null;

                Enabled = true;
                Form f = Application.OpenForms[0];
                if (f != null) mf = (MainAssemblyMgrForm)f;
                mf.menuStrip1.Enabled =
                mf.tsActions.Enabled =
                mf.tsHistory.Enabled =
                mf.tsLibrary.Enabled =
                mf.tsScript.Enabled =
                mf.tsServers.Enabled =
                mf.tvActions.Enabled =
                mf.tvServers.Enabled =
                mf.tvHistory.Enabled =
                mf.tvAssemblies.Enabled = false;               

                mf.statusLabel.Text = message;
                //                mf.statusLabel.Refresh();
                mf.statusStrip1.Refresh();
            }
            public static void Update(string message)
            {
                MainAssemblyMgrForm mf = null;
                Form f = Application.OpenForms[0];
                if (f != null && f.GetType().Name == "MainAssemblyMgrForm")
                {
                    mf = (MainAssemblyMgrForm)f;
                    mf.statusLabel.Text = message;
                    mf.statusStrip1.Refresh();
                }
            }
            public void Dispose()
            {
                MainAssemblyMgrForm mf = null;
                Enabled = false;
                Form f = Application.OpenForms["MainAssemblyMgrForm"];
                if (f != null) mf = (MainAssemblyMgrForm)f;
                mf.statusLabel.Text = "Ready";
                mf.pBar.Visible = false;
                mf.DisableStop();
                mf.menuStrip1.Enabled =
                mf.tsActions.Enabled =
                mf.tsHistory.Enabled =
                mf.tsLibrary.Enabled =
                mf.tsScript.Enabled =
                mf.tsServers.Enabled =
                mf.tvActions.Enabled =
                mf.tvServers.Enabled =
                mf.tvHistory.Enabled =
                mf.tvAssemblies.Enabled = true;               

            }
            public static bool Enabled
            {
                get { return Application.UseWaitCursor; }
                set
                {
                    if (value == Application.UseWaitCursor) return;
                    Application.UseWaitCursor = value;
                    Form f = Form.ActiveForm;
                    if (f != null && f.Handle != null)   // Send WM_SETCURSOR
                        SendMessage(f.Handle, 0x20, f.Handle, (IntPtr)1);
                }
            }
        }

        public class NodeSorter : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                TreeNode tx = (TreeNode)x;
                TreeNode ty = (TreeNode)y;
                if (tx.Tag != null && tx.Tag.GetType().Name == "Function" && ty.Tag != null && ty.Tag.GetType().Name == "InstalledAssembly") return -1;
                if (ty.Tag != null && ty.Tag.GetType().Name == "Function" && tx.Tag != null && tx.Tag.GetType().Name == "InstalledAssembly") return 1;
                return string.Compare(tx.Text, ty.Text);
            }
        }

        public class ActionReverser : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                TreeNode tx = (TreeNode)x;
                TreeNode ty = (TreeNode)y;
                Action ax = (Action)tx.Tag;
                Action ay = (Action)ty.Tag;
                if (ax.sequence < ay.sequence) return 1;
                if (ax.sequence > ay.sequence) return -1;
                return 0;
            }
        }

        public class ActionSorter : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                TreeNode tx = (TreeNode)x;
                TreeNode ty = (TreeNode)y;
                Action ax = (Action)tx.Tag;
                Action ay = (Action)ty.Tag;
                if (ax.sequence < ay.sequence) return -1;
                if (ax.sequence > ay.sequence) return 1;
                return 0;
            }
        }

        private string ActionText(string action, string server, string database, string assembly, string function, string oldvalue, string newvalue)
        {
            string result = action;
            result = result.Replace("%SERVER%", server);
            result = result.Replace("%DATABASE%", database);
            result = result.Replace("%ASSEMBLY%", assembly);
            result = result.Replace("%FUNCTION%", function);
            result = result.Replace("%OLDVALUE%", oldvalue);
            result = result.Replace("%NEWVALUE%", newvalue);
            return result;
        }

        private Action AddAllObjects(TreeNode amNode, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            Action act = null;

            act = am.actions.SingleOrDefault(p => p.action == ActionType.DropAllObjects);
            if (act != null) ReverseAction(act);

            if (am.functions.Count(p => p.status == installstatus.not_installed || p.status == installstatus.pending_remove) > 0)
            {
                act = new Action();
                act.action = ActionType.AddAllObjects;
                act.target = am;
                act.targetnode = amNode;
                RegisterAction(act, parent);
            }
            foreach (TreeNode fnNode in amNode.Nodes)
            {
                if (fnNode.Tag.GetType().Name == "Function")
                {
                    Function fn = (Function)fnNode.Tag;
                    if (fn.status == installstatus.not_installed) AddFunction(fnNode, act, true);
                }
            }
            toggleAssemblyControls(am);
            return act;
        }

        private void AddAssembliesToDatabase(TreeNode dbNode)
        {
            LoadDialog.Title = "Add Assemblies To Database " + dbNode.Text;
            LoadDialog.FileName = "*.dll";
            LoadDialog.Filter = "Assemblies|*.dll|All Files|*.*";
            DialogResult result = LoadDialog.ShowDialog();
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.OK)
            {
                string[] files = LoadDialog.FileNames;
                foreach (string file in files) AddAssemblyFromFileToDatabase(dbNode, file, files);
                dbNode.Expand();
            }
        }

        private void AddAssembliesToLibrary(TreeNode lbNode)
        {
            LoadDialog.Title = "Add Assemblies To Library " + lbNode.Text;
            LoadDialog.FileName = "*.dll";
            LoadDialog.Filter = "Assemblies|*.dll|All Files|*.*";
            DialogResult result = LoadDialog.ShowDialog();
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.OK)
            {
                string[] files = LoadDialog.FileNames;
                foreach (string file in files) AddAssemblyFromFileToLibrary(lbNode, file, files);
                lbNode.Expand();
            }

        }

        private Action AddAssembly(InstalledAssembly am, TreeNode dbNode, Action parent)
        {
            Database db = (Database)dbNode.Tag;
            am.database = db;
            am.status = installstatus.pending_add;
            db.assemblies.Add(am);
            RelinkAssemblyReferences(am);
            TreeNode amNode = AddAssemblyNodeToDatabaseNode(am, dbNode);
            amNode.Tag = am;
            Action act = new Action();
            act.action = ActionType.AddAssembly;
            act.target = am;
            act.newvalue = am;
            act.targetnode = amNode;
            RegisterAction(act, parent);
            if (am.permission_set > 1 && !am.database.trustworthy && (am.login == null
                                                         || am.login.status == installstatus.pending_remove
                                                         || am.login.permission == PermissionSet.SAFE
                                                         || am.permission_set == 3 && am.login.permission == PermissionSet.EXTERNAL_ACCESS))
            {
                Action a = AuthoriseNonsafeAssembly(amNode, am.permission_set, act);
                if (a == null)
                {
                    DeregisterAction(act);
                    amNode.Remove();
                    db.assemblies.Remove(am);
                    act = null;
                }
            }
            return act;
        }

        private TreeNode AddAssemblyFromFileToDatabase(TreeNode dbNode, string file, string[] files)
        {
            AssemblyName a;
            TreeNode amNode = null;

            Database db = (Database)dbNode.Tag;
            string name = Path.GetFileNameWithoutExtension(file);
            InstalledAssembly am = new InstalledAssembly();
            am.bytes = File.ReadAllBytes(file);
            AssemblyParser Parser = (AssemblyParser)db.domain.CreateInstanceAndUnwrap
            (
                Assembly.GetExecutingAssembly().FullName,
                "AssemblyManager.AssemblyParser"
            );
            AssemblyName[] ra = Parser.GetAssemblies();
            try
            {
                a = Parser.Load(am.bytes);
            }
            catch (BadImageFormatException)
            {
                MessageBox.Show(Resource.errSelectedFileNotAValidAssembly.Replace("%FILE%", file), "File Is Not A Valid Assembly");
                return null;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return null;
            }
            externalassemblies.Add(a.FullName);
            if (db.assemblies.Exists(p => p.fullname == a.FullName)) return null;
            am.is_assembly = true;
            am.database = db;
            am.fullname = a.FullName;
            am.name = a.Name;
            am.version = a.Version;
            am.culture = a.CultureInfo;
            am.platform = a.ProcessorArchitecture;
            am.hashcode = a.GetHashCode();
            am.publicKeyToken = a.GetPublicKeyToken();
            AssemblyName[] ana = Parser.GetReferencedAssemblies(a);
            foreach (AssemblyName an in ana)
            {
                if (am.references == null) am.references = new List<InstalledAssembly>();
                if (!db.assemblies.Exists(p => p.name == an.Name))
                {
                    try
                    {
                        Parser.ReflectionOnlyLoad(an);
                        if (!externalassemblies.Exists(p => p == an.FullName)) continue;
                    }
                    catch (Exception) { }
                    TreeNode refNode = null;
                    if (files == null || files.Count(p => Path.GetFileName(p) == an.Name + ".dll") == 0 || (refNode = AddAssemblyFromFileToDatabase(dbNode, files.First(p => Path.GetFileName(p) == an.Name + ".dll"), files)) == null)
                    {
                        if (File.Exists(Path.GetDirectoryName(file) + "\\" + an.Name + ".dll"))
                        {
                            string root = Resource.mbxLoadReferencedAssembly.Replace("%ASSEMBLY1%", am.name + ".dll").Replace("%ASSEMBLY2%", an.Name + ".dll");
                            DialogResult dr = MessageBox.Show(root, "Referenced Assembly Must Be Loaded", MessageBoxButtons.OKCancel);
                            if (dr == DialogResult.OK)
                            {
                                refNode = AddAssemblyFromFileToDatabase(dbNode, Path.GetDirectoryName(file) + "\\" + an.Name + ".dll", files);
                                InstalledAssembly refa = (InstalledAssembly)refNode.Tag;
                                if (refa.dependents == null) refa.dependents = new List<InstalledAssembly>();
                                refa.dependents.Add(am);
                                am.references.Add(refa);
                            }
                            else return null;
                        }
                        else
                        {
                            string root = Resource.errReferencedAssemblyMustBeLoadedFirst.Replace("%ASSEMBLY1%", am.name + ".dll").Replace("%ASSEMBLY2%", an.Name + ".dll");
                            MessageBox.Show(root, "Referenced Assembly Must Be Loaded");
                            return null;
                        }
                    }
                    else
                    {
                        InstalledAssembly refa = (InstalledAssembly)refNode.Tag;
                        if (refa.dependents == null) refa.dependents = new List<InstalledAssembly>();
                        refa.dependents.Add(am);
                        am.references.Add(refa);
                    }
                }
                else
                {
                    InstalledAssembly refa = db.assemblies.Single(p => p.name == an.Name);
                    if (refa.dependents == null) refa.dependents = new List<InstalledAssembly>();
                    if (!refa.dependents.Contains(am)) refa.dependents.Add(am);
                    am.references.Add(refa);
                }
            }
            am.functions = Parser.parseAssembly(a);
            foreach (Function f in am.functions)
            {
                if (f.actions == null) f.actions = new List<Action>();
                f.assembly = am;
                f.schema = am.database.default_schema;
            }
            am.create_date = File.GetCreationTime(file);
            am.modify_date = File.GetLastWriteTime(file);
            am.permission_set = 1;
            Action act = AddOrUpdateAssembly(am, dbNode, null, true);
            if (act != null) amNode = act.targetnode;
            else
            {
                am = db.assemblies.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                if (am != null) amNode = GetNodeFor(am);
            }
            return amNode;
        }

        private TreeNode AddAssemblyFromFileToLibrary(TreeNode lbNode, string file, string[] files)
        {
            AssemblyName a;

            Library ly = (Library)lbNode.Tag;
            string name = Path.GetFileNameWithoutExtension(file);
            TreeNode amNode = lbNode.Nodes.Add(name, name, 2, 2);
            amNode.ContextMenuStrip = cmLibraryAssembly;
            InstalledAssembly la = new InstalledAssembly();
            la.bytes = File.ReadAllBytes(file);
            if (ly.domain == null) ly.domain = AppDomain.CreateDomain("LIB_" + ly.name);
            AssemblyParser Parser = (AssemblyParser)ly.domain.CreateInstanceAndUnwrap
            (
                Assembly.GetExecutingAssembly().FullName,
                "AssemblyManager.AssemblyParser"
            );
            AssemblyName[] ra = Parser.GetAssemblies();
            try
            {
                a = Parser.Load(la.bytes);
            }
            catch (BadImageFormatException)
            {
                MessageBox.Show(Resource.errSelectedFileNotAValidAssembly.Replace("%FILE%", file), "File Is Not A Valid Assembly");
                amNode.Remove();
                return null;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                amNode.Remove();
                return null;
            }
            if (ly.assemblies.Exists(p => p.fullname == a.FullName))
            {
                amNode.Remove();
                return null;
            }
            if (!externalassemblies.Contains(a.FullName)) externalassemblies.Add(a.FullName);
            la.is_assembly = true;
            la.name = a.Name;
            la.fullname = a.FullName;
            la.version = a.Version;
            la.culture = a.CultureInfo;
            la.platform = a.ProcessorArchitecture;
            la.hashcode = a.GetHashCode();
            la.publicKeyToken = a.GetPublicKeyToken();
            AssemblyName[] ana = Parser.GetReferencedAssemblies(a);
            foreach (AssemblyName an in ana)
            {
                if (!ly.assemblies.Exists(p => p.name == an.Name))
                {
                    try
                    {
                        Parser.ReflectionOnlyLoad(an);
                        if (!externalassemblies.Exists(p => p == an.FullName)) continue;
                    }
                    catch (Exception) { }
                    TreeNode refNode = null;
                    if (files == null || files.Count(p => Path.GetFileName(p) == an.Name + ".dll") == 0 || (refNode = AddAssemblyFromFileToLibrary(lbNode, files.First(p => Path.GetFileName(p) == an.Name + ".dll"), files)) == null)
                    {
                        if (File.Exists(Path.GetDirectoryName(file) + "\\" + an.Name + ".dll"))
                        {
                            string root = Resource.mbxLoadReferencedAssembly.Replace("%ASSEMBLY1%", la.name + ".dll").Replace("%ASSEMBLY2%", an.Name + ".dll");
                            DialogResult dr = MessageBox.Show(root, "Referenced Assembly Must Be Loaded", MessageBoxButtons.OKCancel);
                            if (dr == DialogResult.OK)
                            {
                                refNode = AddAssemblyFromFileToLibrary(lbNode, Path.GetDirectoryName(file) + "\\" + an.Name + ".dll", files);
                                InstalledAssembly refa = (InstalledAssembly)refNode.Tag;
                                if (refa.dependents == null) refa.dependents = new List<InstalledAssembly>();
                                refa.dependents.Add(la);
                                if (la.references == null) la.references = new List<InstalledAssembly>();
                                la.references.Add(refa);
                            }
                        }
                        else
                        {
                            string root = Resource.errReferencedAssemblyMustBeLoadedFirst.Replace("%ASSEMBLY1%", la.name + ".dll").Replace("%ASSEMBLY2%", an.Name + ".dll");
                            MessageBox.Show(root, "Referenced Assembly Must Be Loaded");
                            amNode.Remove();
                            return null;
                        }
                    }
                    else
                    {
                        InstalledAssembly refa = (InstalledAssembly)refNode.Tag;
                        if (ra.Count(p => p.Name == refa.name) == 0) Parser.Load(refa.bytes);
                        if (refa.dependents == null) refa.dependents = new List<InstalledAssembly>();
                        refa.dependents.Add(la);
                        if (la.references == null) la.references = new List<InstalledAssembly>();
                        la.references.Add(refa);
                    }
                }
                else
                {
                    InstalledAssembly refa = ly.assemblies.Single(p => p.name == an.Name);
                    if (ra.Count(p => p.Name == refa.name) == 0) Parser.Load(refa.bytes);
                    if (la.references == null) la.references = new List<InstalledAssembly>();
                    if (refa.dependents == null) refa.dependents = new List<InstalledAssembly>();
                    if (!la.references.Contains(refa)) la.references.Add(refa);
                    if (!refa.dependents.Contains(la)) refa.dependents.Add(la);
                }
            }
            la.functions = Parser.parseAssembly(a);
            la.create_date = File.GetCreationTime(file);
            la.modify_date = File.GetLastWriteTime(file);
            la.permission_set = 1;
            la.changes_pending = true;
            foreach (Function f in la.functions)
            {
                if (f.actions == null) f.actions = new List<Action>();
                f.status = installstatus.in_place;
                f.assembly = la;
            }
            populateFunctionTree(amNode, la.functions, cmLibraryFunction);
            ly.assemblies.Add(la);
            amNode.Tag = la;
            amNode.ImageIndex = la.permission_set + 1;
            amNode.SelectedImageIndex = la.permission_set + 1;
            amNode.ToolTipText = "Assembly: " + DetailedAssemblyName(la);
            amNode.Collapse();
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            ShowStatus(amNode, installstatus.in_place, false, true);
            return amNode;
        }

        private TreeNode AddAssemblyNodeToDatabaseNode(InstalledAssembly am, TreeNode dbNode)
        {
            int index = am.permission_set + 1;
            TreeNode AssemblyNode = dbNode.Nodes.Add(am.name, am.name, index, index);
            AssemblyNode.ContextMenuStrip = cmAssembly;
            AssemblyNode.Tag = am;
            AssemblyNode.ToolTipText = "Assembly: " + DetailedAssemblyName(am);
            ShowStatus(AssemblyNode, am.status, false, am.changes_pending);
            tvServers.Sort();
            return AssemblyNode;
        }

        private TreeNode AddAssemblyToLibrary(TreeNode lbNode, InstalledAssembly am)
        {
            Library ly = (Library)lbNode.Tag;

            if (am.references != null && am.references.Count > 0)
            {
                foreach (InstalledAssembly ra in am.references)
                {
                    if (!ly.assemblies.Exists(p => p.name.Equals(ra.name, StringComparison.OrdinalIgnoreCase)))
                    {
                        string s = Resource.mbxLoadReferencedAssembly;
                        s = s.Replace("%ASSEMBLY1%", am.name);
                        s = s.Replace("%ASSEMBLY2%", ra.name);
                        DialogResult dr = MessageBox.Show(s, "Referenced Assembly Must Be Added", MessageBoxButtons.OKCancel);
                        if (dr == DialogResult.Cancel) return null;
                        AddAssemblyToLibrary(lbNode, new InstalledAssembly(ra));
                    }
                }
                RelinkLibraryAssemblyReferences(am, ly);
            }
            int index = am.permission_set + 1;
            TreeNode amNode = lbNode.Nodes.Add(am.name, am.name, index, index);
            amNode.ContextMenuStrip = cmLibraryAssembly;
            populateFunctionTree(amNode, am.functions, cmLibraryFunction);
            populateFileTree(amNode, am.subfiles, cmLibraryFile);
            ly.assemblies.Add(am);
            am.changes_pending = true;
            am.key = null;
            am.login = null;
            amNode.Tag = am;
            amNode.ToolTipText = "Assembly: " + DetailedAssemblyName(am);
            amNode.Collapse();
            lbNode.Expand();
            ShowStatus(amNode, installstatus.in_place, false, true);
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            return amNode;
        }

        private Action AddAsymmetricKeyAndLogin(TreeNode amNode, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (am.publicKeyToken.Count() == 0)
            {
                MessageBox.Show("Error: you cannot create an asymmetric key for an assembly that is not strong-named", "Assembly not signed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            List<InstalledAssembly> peers = new List<InstalledAssembly>();
            foreach (Database db in am.database.server.databases)
                foreach (InstalledAssembly a in db.assemblies.Where(p => p.publicKeyToken.SequenceEqual(am.publicKeyToken)))
                    peers.Add(a);

            Action act = null;
            foreach (InstalledAssembly a in peers)
            {
                act = a.actions.FirstOrDefault(p => p.action == ActionType.DropKeyAndLogin);
                if (act != null) break;
            }

            if (act == null)
            {
                string keyname = am.key == null ? am.name + "_AsymmetricKey" : am.key.Name;
                string loginname = am.login == null ? am.name + "_Login" : am.login.Name;
                AsymmetricKey key = new AsymmetricKey();
                Login login = new Login();
                key = new AsymmetricKey();
                key.status = installstatus.pending_add;
                key.Name = keyname;
                key.Thumbprint = am.publicKeyToken;
                key.assembly = am;

                login = new Login();
                login.status = installstatus.pending_add;
                login.Name = loginname;
                login.key = key;
                login.permission = am.permission_set == 3 ? PermissionSet.UNSAFE : PermissionSet.EXTERNAL_ACCESS;

                keynamebox.Tag = login;
                DialogResult dr = keynamebox.ShowDialog();
                if (dr == System.Windows.Forms.DialogResult.Cancel) return null;

                foreach (InstalledAssembly a in peers)
                {
                    a.key = key;
                    a.login = login;
                }

                act = new Action();
                act.action = ActionType.AddKeyAndLogin;
                act.target = am;
                act.targetnode = amNode;
                act.newvalue = am.login;
                RegisterAction(act, parent);

            }
            else
            {
                am.key.status = installstatus.in_place;
                am.login.status = installstatus.in_place;
                DeregisterAction(act);
                act = null;


            }

            toggleAssemblyControls(am);
            ShowStatus(amNode, am.status, false, am.changes_pending);
            return act;
        }
        
        private void AddDatabaseToTree(TreeNode ServerNode, Database db)
        {
            TreeNode DBNode;
            TreeNode AssemblyNode;

            if (db.show)
            {
                int i = db.ImageIndex();
                DBNode = ServerNode.Nodes.Add(db.name, db.name, i, i);
                DBNode.Tag = db;
                DBNode.ContextMenuStrip = cmDatabase;

                foreach (InstalledAssembly am in db.assemblies)
                {
                    AssemblyNode = AddAssemblyNodeToDatabaseNode(am, DBNode);
                    populateFunctionTree(AssemblyNode, am.functions, cmFunction);
                    populateFileTree(AssemblyNode, am.subfiles, cmFile);
                }
                if (DBNode.Nodes.Count > 0) DBNode.Expand();
                DBNode.ToolTipText = db.ToolTipText();
            }
        }

        private void AddFileToAssemblyInDatabase(TreeNode amNode, string file)
        {
            string name;
            name = Path.GetFileName(file);
            InstalledAssembly la = new InstalledAssembly();
            la.bytes = File.ReadAllBytes(file);
            la.is_assembly = false;
            la.name = name;
            la.create_date = File.GetCreationTime(file);
            la.modify_date = File.GetLastWriteTime(file);
            la.permission_set = 1;
            AddFileToAssemblyInDatabase(amNode, la, null, true);
        }

        private Action AddFileToAssemblyInDatabase(TreeNode amNode, InstalledAssembly la, Action parent, bool ReverseIfAdded)
        {
            Action act = null;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (ReverseIfAdded && la.actions.Exists(p => p.action == ActionType.DropFile)) ReverseAction(la.actions.Single(p => p.action == ActionType.DropFile));
            else
            {
                la.database = am.database;
                la.parent = am;
                la.status = installstatus.pending_add;
                if (am.subfiles.Exists(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase)))
                {
                    string root = Resource.mbxAssociatedFileAlreadyExists.Replace("%FILE%", la.name).Replace("%ASSEMBLY%", am.name);
                    MessageBox.Show(root, "File already exists");
                    return null;
                }
                TreeNode fNode = amNode.Nodes.Add(la.name, la.name, 13, 13);
                fNode.ContextMenuStrip = cmFile;
                act = new Action();
                act.target = la;
                act.newvalue = la;
                act.targetnode = fNode;
                act.action = ActionType.AddFile;
                RegisterAction(act, parent);
                am.subfiles.Add(la);
                la.parent = am;
                fNode.ToolTipText = "Associated File: " + la.name + ", size: " + la.bytes.Length.ToString("#,###") + " bytes";
                fNode.Tag = la;
                ShowStatus(fNode, la.status, false, false);
            }
            return act;
        }

        private void AddFileToAssemblyInLibrary(TreeNode amNode, string file)
        {
            string name;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            name = Path.GetFileName(file);
            if (am.subfiles.Exists(p => p.name == name))
            {
                string root = Resource.mbxAssociatedFileAlreadyExists.Replace("%FILE%", name).Replace("%ASSEMBLY%", am.name);
                MessageBox.Show(root, "File already exists");
                return;
            }
            TreeNode fNode = amNode.Nodes.Add(name, name, 13, 13);
            fNode.ContextMenuStrip = cmLibraryFile;
            InstalledAssembly la = new InstalledAssembly();
            la.bytes = File.ReadAllBytes(file);
            la.is_assembly = false;
            la.name = name;
            la.create_date = File.GetCreationTime(file);
            la.modify_date = File.GetLastWriteTime(file);
            la.permission_set = 1;
            am.subfiles.Add(la);
            la.parent = am;
            am.changes_pending = true;
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            fNode.ToolTipText = "Associated File: " + la.name + ", size: " + la.bytes.Length.ToString("#,###") + " bytes";
            fNode.Tag = la;
            fNode.Collapse();
        }

        private void AddFileToAssemblyInLibrary(TreeNode amNode, InstalledAssembly la)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (am.subfiles.Exists(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase) && p != la))
            {
                string root = Resource.mbxAssociatedFileAlreadyExists.Replace("%FILE%", la.name).Replace("%ASSEMBLY%", am.name);
                MessageBox.Show(root, "File already exists");
                return;
            }
            TreeNode fNode = amNode.Nodes.Add(la.name, la.name, 13, 13);
            fNode.ContextMenuStrip = cmLibraryFile;
            if (!am.subfiles.Contains(la))
            {
                am.subfiles.Add(la);
                am.changes_pending = true;
            }
            la.parent = am;
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            fNode.ToolTipText = "Associated File: " + la.name + ", size: " + la.bytes.Length.ToString("#,###") + " bytes";
            fNode.Tag = la;
            fNode.Collapse();
        }

        private void AddFilesToAssemblyInLibrary(TreeNode amNode)
        {
            LoadDialog.Title = "Import Associated Files for " + ((InstalledAssembly)amNode.Tag).name;
            LoadDialog.FileName = "*.*";
            LoadDialog.Filter = "All Files|*.*";
            DialogResult result = LoadDialog.ShowDialog();
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.OK)
            {
                string[] files = LoadDialog.FileNames;

                foreach (string file in files) AddFileToAssemblyInLibrary(amNode, file);
                amNode.Expand();
            }
        }

        private void AddFilesToAssemblyInDatabase(TreeNode amNode)
        {
            LoadDialog.Title = "Import Associated Files for " + ((InstalledAssembly)amNode.Tag).name;
            LoadDialog.FileName = "*.*";
            LoadDialog.Filter = "All Files|*.*";
            DialogResult result = LoadDialog.ShowDialog();
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.OK)
            {
                string[] files = LoadDialog.FileNames;

                foreach (string file in files) AddFileToAssemblyInDatabase(amNode, file);
                amNode.Expand();
            }
        }

        private Action AddFunction(TreeNode fnNode, Action parent, bool reverseifdropped)
        {
            Action act = null;
            Function fn = (Function)fnNode.Tag;
            InstalledAssembly am = fn.assembly;

            if (fn.status == installstatus.pending_remove && reverseifdropped)
            {
                act = fn.actions.Last(a => a.action == ActionType.DropFunction);
                DeregisterAction(act);
                fn.status = (installstatus)Enum.Parse(typeof(installstatus), (string)act.oldvalue);
                act = null;
            }
            else if (fn.status == installstatus.pending_add || fn.status == installstatus.in_place) return null;
            else
            {
                act = new Action();
                act.action = ActionType.AddFunction;
                act.target = fn;
                act.newvalue = fn;
                act.targetnode = fnNode;
                act.oldvalue = fn.status.ToString();
                fn.status = installstatus.pending_add;
                RegisterAction(act, parent);
            }
            toggleFunctionControls(fn);
            ShowStatus(fnNode, fn.status, true, fn.changes_pending);
            return act;
        }

        private TreeNode AddLibraryToTree(Library lib, int index)
        {
            TreeNode lNode;
            if (index == -1) lNode = tvAssemblies.Nodes.Add(lib.name, lib.name, 12, 12);
            else lNode = tvAssemblies.Nodes.Insert(index, lib.name, lib.name, 12, 12);
            lNode.ContextMenuStrip = cmLibrary;
            lNode.Tag = lib;
            lNode.ToolTipText = "Library: " + lib.name + " (" + lib.file + ")";
            foreach (InstalledAssembly a in lib.assemblies)
            {
                TreeNode amNode = lNode.Nodes.Add(a.name, a.name, 2, 2);
                amNode.ContextMenuStrip = cmLibraryAssembly;
                foreach (Function f in a.functions)
                {
                    TreeNode fnNode = amNode.Nodes.Add(f.name, f.name, f.FunctionTypeIconIndex(), f.FunctionTypeIconIndex());
                    fnNode.ContextMenuStrip = cmLibraryFunction;
                    fnNode.ToolTipText = f.tooltiptext();
                    fnNode.Tag = f;
                    ShowStatus(fnNode, f.status, true, false);
                }
                foreach (InstalledAssembly s in a.subfiles)
                {
                    TreeNode fnNode = amNode.Nodes.Add(s.name, s.name, 13, 13);
                    fnNode.ContextMenuStrip = cmLibraryFile;
                    fnNode.ToolTipText = "File: " + s.name;
                    fnNode.Tag = s;
                }
                amNode.ImageIndex = a.permission_set + 1;
                amNode.SelectedImageIndex = a.permission_set + 1;
                amNode.Tag = a;
                amNode.ToolTipText = "Assembly: " + DetailedAssemblyName(a);
                amNode.Collapse();
            }
            lNode.Expand();
            return lNode;
        }

        private void AddOrRemoveLibraryFunction(TreeNode fnNode)
        {
            Function fn = (Function)fnNode.Tag;
            if (fn.status == installstatus.not_installed) fn.status = installstatus.in_place;
            else fn.status = installstatus.not_installed;
            fn.changes_pending = true;
            toggleLibraryFunctionControls(fn);
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            ShowStatus(fnNode, fn.status, true, fn.changes_pending);
        }

        private Action AddOrUpdateAssembly(InstalledAssembly la, TreeNode dbNode, Action parent, bool addFunctions)
        {
            InstalledAssembly am = null;
            Database db = (Database)dbNode.Tag;
            Action act = null;
            DialogResult dr;
            List<Action> subactions = new List<Action>();

            am = db.assemblies.SingleOrDefault(ay => ay.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
            if (am != null)
            {
                if (la.version != am.version)
                {
                    string msg = Resource.mbxNonIdenticalAssemblyAlreadyInstalledPrompt.Replace("%ASSEMBLY%", am.name);
                    msg = msg.Replace("%DATABASE%", am.database.name);
                    msg = msg.Replace("%OLDVERSION%", am.version.ToString(4));
                    msg = msg.Replace("%OLDDATE%", am.create_date.ToString(Resource.dateFormat));
                    msg = msg.Replace("%NEWVERSION%", la.version.ToString(4));
                    msg = msg.Replace("%NEWDATE%", la.create_date.ToString(Resource.dateFormat));
                    dr = MessageBox.Show(msg, Resource.mbxNonIdenticalAssemblyAlreadyInstalledTitle, MessageBoxButtons.YesNo);
                    if (dr == DialogResult.No) return null;
                    TreeNode amNode = (from TreeNode t in dbNode.Nodes where t.Tag == am select t).Single();
                    act = ReplaceAssembly(amNode, la, null);
                }
            }
            else
            {
                InstalledAssembly amNew = la; // new InstalledAssembly(la);
                if (amNew.references != null)
                {
                    for (int i = 0; i < amNew.references.Count; i++)
                    {
                        InstalledAssembly refa = amNew.references[i];
                        if (db.assemblies.Exists(p => p.fullname == refa.fullname))
                        {
                            InstalledAssembly newrefa = db.assemblies.Single(p => p.fullname == refa.fullname);
                            amNew.references[amNew.references.IndexOf(refa)] = newrefa;
                            if (newrefa.dependents == null) newrefa.dependents = new List<InstalledAssembly>();
                            newrefa.dependents.Add(amNew);
                        }
                        else if (db.assemblies.Exists(p => p.name.Equals(refa.name, StringComparison.OrdinalIgnoreCase)))
                        {
                            InstalledAssembly localdepa = db.assemblies.First(p => p.name.Equals(refa.name, StringComparison.OrdinalIgnoreCase));
                            string root = Resource.mbxReferencedAssemblyIsDifferentVersionInDB;
                            root = root.Replace("%DATABASE%", db.name);
                            root = root.Replace("%ASSEMBLY1%", amNew.name);
                            root = root.Replace("%ASSEMBLY2%", refa.name);
                            root = root.Replace("%VERSION1%", refa.version.ToString(4));
                            root = root.Replace("%VERSION2%", localdepa.version.ToString(4));
                            dr = MessageBox.Show(root, "Referenced Assembly Has Different Version", MessageBoxButtons.YesNo);
                            if (dr == DialogResult.Yes)
                            {
                                TreeNode amNode = (from TreeNode t in dbNode.Nodes where t.Tag == localdepa select t).Single();
                                subactions.Add(ReplaceAssembly(amNode, refa, null));
                                localdepa = db.assemblies.First(p => p.name.Equals(refa.name, StringComparison.OrdinalIgnoreCase));
                                amNew.references[amNew.references.IndexOf(refa)] = localdepa;
                                if (localdepa.dependents == null) localdepa.dependents = new List<InstalledAssembly>();
                                localdepa.dependents.Add(amNew);
                            }
                        }
                        else
                        {
                            string root = Resource.mbxLoadReferencedAssembly;
                            root = root.Replace("%ASSEMBLY1%", amNew.name);
                            root = root.Replace("%ASSEMBLY2%", refa.name);
                            dr = MessageBox.Show(root, "Assembly Has Dependencies", MessageBoxButtons.OKCancel);
                            if (dr == DialogResult.OK)
                            {
                                subactions.Add(AddOrUpdateAssembly(new InstalledAssembly(refa), dbNode, parent, addFunctions));
                                InstalledAssembly newrefa = db.assemblies.Single(p => p.fullname == refa.fullname);
                                amNew.references[i] = newrefa;
                                if (newrefa.dependents == null) newrefa.dependents = new List<InstalledAssembly>();
                                newrefa.dependents.Add(amNew);
                            }
                            else return null;
                        }
                    }
                }

                foreach (Function fn in amNew.functions)
                {
                    if (fn.type == "TA") fn.schema = fn.trigger.target_schema;
                    else fn.schema = db.default_schema;
                }
                act = AddAssembly(amNew, dbNode, parent);
                if (act != null)
                {
                    populateFunctionTree(act.targetnode, amNew.functions, cmFunction);
                    foreach (TreeNode fNode in act.targetnode.Nodes)
                    {
                        Function f = (Function)fNode.Tag;
                        if (addFunctions && f.status != installstatus.not_installed)
                        {
                            f.status = installstatus.not_installed;
                            AddFunction(fNode, act, true);
                        }
                        else if (!addFunctions) f.status = installstatus.not_installed;
                    }
                    List<InstalledAssembly> subfiles = amNew.subfiles;
                    amNew.subfiles = new List<InstalledAssembly>();
                    foreach (InstalledAssembly af in subfiles) AddFileToAssemblyInDatabase(act.targetnode, af, act, false);
                }
            }

            return act;
        }

        private Action AddPermissionToLogin(TreeNode amNode, int set, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (am.login == null) return null;
            Action act;

            act = am.actions.FirstOrDefault(p => p.action == ActionType.DropPermission);
            if (act != null)
            {
                ReverseAction(act);
                if ((int)am.login.permission == set) return act;
            }
            act = am.actions.FirstOrDefault(p => p.action == ActionType.AddPermission);
            if (act != null) DeregisterAction(act);
            else act = new Action();
            act.action = ActionType.AddPermission;
            act.target = am;
            act.targetnode = amNode;
            act.oldvalue = (int)am.login.permission;
            act.newvalue = set;
            am.login.permission = set < 3 ? PermissionSet.EXTERNAL_ACCESS : PermissionSet.UNSAFE;
            RegisterAction(act, parent);
            return act;
        }

        private void AddServerToTree(SqlConnection conn, SqlConnectionStringBuilder builder)
        {
            Server server;
            TreeNode ServerNode;

            us.UnObscure(builder);
            string u = builder.UserID;
            us.Obscure(builder);
            server = new Server(conn, builder);
            server.name = builder.DataSource;
            server.GetVersion();
            server.GetKeysAndLogins();
            server.GetDatabases(externalassemblies);
            if (!pBar.Visible) SetPBar(TotalFunctionCount(server));
            server.GetPermissions();
            int index = server.clr_enabled ? 0 : 17;
            ServerNode = tvServers.Nodes.Add(builder.DataSource, builder.DataSource, index, index);
            ServerNode.ContextMenuStrip = cmServer;
            foreach (Database db in server.databases) AddDatabaseToTree(ServerNode, db);
            if (ServerNode.Nodes.Count > 0) ServerNode.Expand();
            ServerNode.Tag = server;
            ServerNode.ToolTipText = server.ToolTipText();
            tvServers.Sort();
            tvServers_NodeAdded();
            tvServers.SelectedNode = ServerNode;
        }

        private void Audit(TreeNodeCollection tnc)
        {
            foreach (TreeNode tn in tnc)
            {
                if (!(tn.Tag != null)) throw new Exception();
                switch (tn.Level)
                {
                    case 0:
                        if (!(tn.Tag.GetType().Name == "Server")) throw new Exception();
                        Server sv = (Server)tn.Tag;
                        if (!(tn.ToolTipText == sv.ToolTipText())) throw new Exception();
                        if (!(tn.ImageIndex == 0 && tn.SelectedImageIndex == 0)) throw new Exception();
                        if (!(sv.databases.Count(p => p.show) == tn.Nodes.Count)) throw new Exception();
                        foreach (Database adb in sv.databases.Where(p => p.show)) if (!(tn.Nodes.Contains(GetNodeFor(adb)))) throw new Exception();
                        foreach (Database adb in sv.databases.Where(p => !p.show)) if (!(GetNodeFor(adb) == null)) throw new Exception();
                        foreach (Action a in sv.actions)
                        {
                            if (!(a.actionnode != null)) throw new Exception();
                            if (!(a.actionnode.TreeView == tvActions)) throw new Exception();
                            if (!(a.target == sv)) throw new Exception();
                            if (!(a.targetnode == tn)) throw new Exception();
                        }
                        break;
                    case 1:
                        if (!(tn.Tag.GetType().Name == "Database")) throw new Exception();
                        sv = (Server)tn.Parent.Tag;
                        Database db = (Database)tn.Tag;
                        if (!(sv.databases.Contains(db))) throw new Exception();
                        if (!(db.server == sv)) throw new Exception();
                        if (!(tn.Text == db.name)) throw new Exception();
                        if (!(tn.ToolTipText == db.ToolTipText())) throw new Exception();
                        int ix = db.trustworthy ? 15 : 14;
                        if (!(tn.ImageIndex == ix && tn.SelectedImageIndex == ix)) throw new Exception();
                        if (!(db.assemblies.Count == tn.Nodes.Count)) throw new Exception();
                        foreach (InstalledAssembly aam in db.assemblies) if (!(tn.Nodes.Contains(GetNodeFor(aam)))) throw new Exception();
                        foreach (Action a in db.actions)
                        {
                            if (!(a.actionnode != null)) throw new Exception();
                            if (!(a.actionnode.TreeView == tvActions)) throw new Exception();
                            if (!(a.target == db)) throw new Exception();
                            if (!(a.targetnode == tn)) throw new Exception();
                        }
                        break;
                    case 2:
                        if (!(tn.Tag.GetType().Name == "InstalledAssembly")) throw new Exception();
                        db = (Database)tn.Parent.Tag;
                        InstalledAssembly am = (InstalledAssembly)tn.Tag;
                        if (!(db.assemblies.Contains(am))) throw new Exception();
                        if (!(am.database == db)) throw new Exception();
                        if (!(am.is_assembly)) throw new Exception();
                        if (!(tn.Text == am.name)) throw new Exception();
                        if (!(tn.ToolTipText == "Assembly: " + DetailedAssemblyName(am))) throw new Exception();
                        if (!(tn.ImageIndex == am.permission_set + 1)) throw new Exception();
                        if (!(am.functions.Count + am.subfiles.Count <= tn.Nodes.Count)) throw new Exception();
                        foreach (Function afn in am.functions) if (!(tn.Nodes.Contains(GetNodeFor(afn)))) throw new Exception();
                        foreach (InstalledAssembly aaf in am.subfiles) if (!(tn.Nodes.Contains(GetNodeFor(aaf)))) throw new Exception();
                        if (am.dependents != null)
                        {
                            foreach (InstalledAssembly da in am.dependents)
                            {
                                if (!(db.assemblies.Contains(da))) throw new Exception();
                                if (!(da.references.Contains(am))) throw new Exception();
                            }
                        }
                        if (am.references != null)
                        {
                            foreach (InstalledAssembly ra in am.references)
                            {
                                if (!(db.assemblies.Contains(ra))) throw new Exception();
                                if (!(ra.dependents.Contains(am))) throw new Exception();
                            }
                        }
                        foreach (Action a in am.actions)
                        {
                            if (!(a.actionnode != null)) throw new Exception();
                            if (!(a.actionnode.TreeView == tvActions)) throw new Exception();
                            if (!(a.target == am)) throw new Exception();
                            if (!(a.targetnode == tn)) throw new Exception();
                        }
                        break;
                    case 3:
                        if (!(tn.Tag.GetType().Name == "InstalledAssembly" || tn.Tag.GetType().Name == "Function")) throw new Exception();
                        if (tn.Tag.GetType().Name == "Function")
                        {
                            am = (InstalledAssembly)tn.Parent.Tag;
                            Function fn = (Function)tn.Tag;
                            if (!(am.functions.Contains(fn))) throw new Exception();
                            if (!(fn.assembly == am)) throw new Exception();
                            if (!(tn.Text == fn.ShortName(false))) throw new Exception();
                            if (!(tn.ToolTipText.ToLower() == fn.tooltiptext().ToLower())) throw new Exception();
                            foreach (Action a in fn.actions)
                            {
                                if (!(a.actionnode != null)) throw new Exception();
                                if (!(a.actionnode.TreeView == tvActions)) throw new Exception();
                                if (!(a.target == fn)) throw new Exception();
                                if (!(a.targetnode == tn)) throw new Exception();
                            }
                        }
                        else
                        {
                            am = (InstalledAssembly)tn.Parent.Tag;
                            InstalledAssembly la = (InstalledAssembly)tn.Tag;
                            if (la.status == installstatus.pending_remove) continue;
                            if (!(am.subfiles.Contains(la))) throw new Exception();
                            if (!(la.parent == am)) throw new Exception();
                            if (!(!la.is_assembly)) throw new Exception();
                            if (!(la.dependents == null)) throw new Exception();
                            if (!(la.references == null)) throw new Exception();
                            if (!(la.functions.Count == 0)) throw new Exception();
                            Debug.Assert(tn.Text == la.name);
                            foreach (Action a in la.actions)
                            {
                                if (!(a.actionnode != null)) throw new Exception();
                                if (!(a.actionnode.TreeView == tvActions)) throw new Exception();
                                if (!(a.target == la)) throw new Exception();
                                if (!(a.targetnode == tn)) throw new Exception();
                            }
                        }
                        break;
                }
                Audit(tn.Nodes);
            }
        }

        private void CancelAllActions(bool prompt)
        {
            List<Action> klist = new List<Action>();
            DialogResult dr;

            if (prompt)
            {
                dr = MessageBox.Show(Resource.mbxCancelAllActionsPrompt, "Cancel All Pending Actions", MessageBoxButtons.YesNo);
                if (dr == DialogResult.No) return;
            }

            int n = TotalActionCount();
            if (n > 0)
            {
                using (new HourGlass("Cancelling Actions..."))
                {
                    SetPBar(n);
                    SuppressEvents = true;
                    foreach (TreeNode tn in tvActions.Nodes)
                    {
                        Action a = (Action)tn.Tag;
                        klist.Insert(0, a);
                    }
                    foreach (Action a in klist)
                    {
                        ReverseAction(a);
                        tvActions.Refresh();
                    }
                    SuppressEvents = false;
                    toggleActionControls();
                }
            }
        }

        private Action ChangeDatabaseDefaultSchema(TreeNode dbNode, Action parent, string schema, bool recursive)
        {
            int n = 0;
            Action act = null;
            Database db = (Database)dbNode.Tag;
            string oldschema = db.default_schema;
            DialogResult resp = DialogResult.No;

            if (!Database.defaultschemas.ContainsKey(db.FQN)) Database.defaultschemas.Add(db.FQN, schema);
            else Database.defaultschemas[db.FQN] = schema;

            if (db.default_schema != schema)
            {
                foreach (InstalledAssembly assembly in db.assemblies) n += assembly.functions.Count(f => (f.status == installstatus.in_place || f.status == installstatus.pending_add) && f.schema == oldschema);
                if (n > 0 && recursive)
                {
                    resp = MessageBox.Show(ActionText(Resource.mbxMoveAllObjectsToNewSchemaPrompt, "", "", "", "", oldschema, schema), Resource.mbxMoveAllObjectsToNewSchemaTitle, MessageBoxButtons.YesNoCancel);
                    if (resp == DialogResult.Cancel) return act;
                }

                act = db.actions.SingleOrDefault(a => a.action == ActionType.ChangeDatabaseDefaultSchema);
                if (act != null)
                {
                    if (resp == DialogResult.No)
                    {
                        while (act.subactions.Count > 0)
                        {
                            Action a = act.subactions[0];
                            a.actionnode.Remove();
                            tvActions.Nodes.Add(a.actionnode);
                            a.parent = null;
                            act.subactions.Remove(a);
                        }
                    }
                    DeregisterAction(act);
                    if ((string)act.oldvalue == schema)
                    {
                        act = null;
                    }
                    else
                    {
                        db.default_schema = (string)act.oldvalue;
                        act.newvalue = schema;
                        RegisterAction(act, parent);
                    }
                }
                else
                {
                    act = new Action();
                    act.action = ActionType.ChangeDatabaseDefaultSchema;
                    act.target = db;
                    act.targetnode = dbNode;
                    act.oldvalue = db.default_schema;
                    act.newvalue = schema;
                    RegisterAction(act, parent);
                }
                db.default_schema = schema;
                if (resp == DialogResult.Yes)
                    using (new HourGlass("Changing schema of child objects..."))
                    {
                        tvServers.SuspendLayout();
                        SetPBar(TotalFunctionCount(db));
                        foreach (InstalledAssembly am in db.assemblies)
                            foreach (Function fn in am.functions)
                            {
                                pBar.PerformStep();
                                pBar.ProgressBar.Refresh();
                                if (fn.type != "TA" && fn.schema == oldschema) ChangeFunctionSchema(GetNodeFor(fn), act, schema);
                            }
                        tvServers.ResumeLayout();
                    }
            }
            return act;
        }

        private Action ChangeFunctionSchema(TreeNode fnNode, Action parent, string schema)
        {
            Function fn = (Function)fnNode.Tag;
            Action act = null;

            if (fn.schema != schema)
            {
                if (fn.status == installstatus.not_installed)
                {
                    fn.schema = schema;
                }
                else
                {
                    act = fn.actions.SingleOrDefault(a => a.action == ActionType.ChangeFunctionSchema);
                    if (act != null)
                    {
                        if (fn.type == "UDT")
                        {
                            foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                            {
                                foreach (Function f in am.functions)
                                {
                                    foreach (Parameter p in f.parameters)
                                    {
                                        TreeNode n = null;
                                        if (p.type == fn.name || p.type == fn.ShortName(false))
                                        {
                                            if (n == null) n = GetNodeFor(f);
                                            if (p.type.Contains(".")) p.type = p.type.Substring(p.type.IndexOf(".") + 1);
                                            p.type = schema + "." + p.type;
                                            n.ToolTipText = f.tooltiptext();
                                        }
                                    }
                                }
                            }
                        }

                        DeregisterAction(act);
                        if ((string)act.oldvalue == schema)
                        {
                            fn.schema = schema;
                            if (fn.actions.Count == 0) fn.changes_pending = false;
                            act = null;
                        }
                        else
                        {
                            fn.schema = (string)act.oldvalue;
                            act.newvalue = schema;
                            RegisterAction(act, parent);
                            fn.schema = schema;
                        }
                    }
                    else
                    {
                        act = new Action();
                        act.action = ActionType.ChangeFunctionSchema;
                        act.target = fn;
                        act.targetnode = fnNode;
                        act.oldvalue = fn.schema;
                        act.newvalue = schema;
                        RegisterAction(act, parent);

                        if (fn.type == "UDT")
                        {
                            foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                            {
                                foreach (Function f in am.functions)
                                {
                                    foreach (Parameter p in f.parameters)
                                    {
                                        TreeNode n = null;
                                        if (p.type == fn.name || p.type == fn.ShortName(false))
                                        {
                                            if (n == null) n = GetNodeFor(f);
                                            if (f.status == installstatus.in_place || f.status == installstatus.pending_add) DropFunction(n, act, false);
                                            if (p.type.Contains(".")) p.type = p.type.Substring(p.type.IndexOf(".") + 1);
                                            p.type = schema + "." + p.type;
                                            if (f.status == installstatus.pending_remove) AddFunction(n, act, false);
                                            n.ToolTipText = f.tooltiptext();
                                        }
                                    }
                                }
                            }
                        }

                        fn.schema = schema;
                        fn.changes_pending = true;
                        ShowStatus(fnNode, fn.status, true, fn.changes_pending);
                    }
                }
                int i = fnNode.Index;
                TreeNode amNode = fnNode.Parent;
                fnNode.Remove();
                TreeNode f2Node = amNode.Nodes.Insert(i, fn.ShortName(false), fn.ShortName(false), fn.FunctionTypeIconIndex(), fn.FunctionTypeIconIndex());
                substituteNodeRecursive(tvActions.Nodes, fnNode, f2Node);
                f2Node.ToolTipText = fn.tooltiptext();
                f2Node.ContextMenuStrip = cmFunction;
                f2Node.Tag = fn;
                cmFunction.Tag = f2Node;
                if (!SuppressEvents) tvServers.SelectedNode = f2Node;
                ShowStatus(f2Node, fn.status, true, fn.changes_pending);
                GC.Collect();
            }
            return act;
        }

        public Action ChangeLibraryParameterDefault(Parameter p, Action parent, string noo)
        {
            Action a = null;
            Function f = p.function;
            TreeNode node = GetLibraryNodeFor(f);
            f.changes_pending = true;
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            p.default_value = noo;
            ShowStatus(node, f.status, true, f.changes_pending);
            return a;
        }

        private Action ChangeLibraryPermissionSet(TreeNode amNode, int set)
        {
            Action act = null;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (am.permission_set != set)
            {
                am.permission_set = set;
                am.changes_pending = true;
                amNode.ImageIndex = set + 1;
                amNode.SelectedImageIndex = set + 1;
                tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
                toggleLibraryAssemblyControls(am);
                ShowStatus(amNode, am.status, false, am.changes_pending);
            }
            return act;
        }

        public Action ChangeParameterDefault(Parameter p, Action parent, string noo)
        {
            Action a = null;
            string old = p.default_value;
            if (old == null) old = "";
            Function f = p.function;
            TreeNode node = GetNodeFor(f);
            a = f.actions.SingleOrDefault(ap => ap.action == ActionType.SetParameterDefault && ap.target == p);
            if (a == null)
            {
                a = new Action();
                a.action = ActionType.SetParameterDefault;
                a.target = p;
                a.newvalue = noo;
                a.oldvalue = old;
                a.targetnode = node;
                RegisterAction(a, parent);
                f.changes_pending = true;
            }
            else if ((string)a.oldvalue == noo) DeregisterAction(a);
            else
            {
                DeregisterAction(a);
                a.newvalue = noo;
                RegisterAction(a, a.parent);
            }
            if (f.actions.Count == 0) f.changes_pending = false;
            p.default_value = noo;
            ShowStatus(node, f.status, true, f.changes_pending);
            return a;
        }

        private Action AuthoriseNonsafeAssembly(TreeNode amNode, int set, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            bool SetTrustworthy = false;
            bool AddLogin = false;
            bool AddPermission = false;
            Action act = null;

            string msg;
            if (am.login != null && (am.login.permission == PermissionSet.SAFE || set == 3 && am.login.permission == PermissionSet.EXTERNAL_ACCESS))
            {
                msg = Resource.mbxNeedToAddPermission.Replace("%ASSEMBLY%", am.name);
                msg = msg.Replace("%DATABASE%", am.database.name);
                msg = msg.Replace("%PERMISSION%", PermissionSets[set - 1]);
                DialogResult dr = MessageBox.Show(msg, "Add permission to login", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (dr == System.Windows.Forms.DialogResult.Yes) AddPermission = true;
                else if (dr == System.Windows.Forms.DialogResult.No)
                {
                    msg = Resource.mbxThenNeedToSetTrustworthy.Replace("%ASSEMBLY%", am.name);
                    msg = msg.Replace("%DATABASE%", am.database.name);
                    msg = msg.Replace("%PERMISSION%", PermissionSets[set - 1]);
                    dr = MessageBox.Show(msg, "Set database to TRUSTWORTHY", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == System.Windows.Forms.DialogResult.Yes) SetTrustworthy = true;
                }
            }
            else if (am.publicKeyToken.Count() == 0)
            {
                msg = Resource.mbxNeedToSetTrustworthy.Replace("%ASSEMBLY%", am.name);
                msg = msg.Replace("%DATABASE%", am.database.name);
                msg = msg.Replace("%PERMISSION%", PermissionSets[set - 1]);
                DialogResult dr = MessageBox.Show(msg, "Set database to TRUSTWORTHY", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (dr == System.Windows.Forms.DialogResult.OK) SetTrustworthy = true;
            }
            else
            {
                msg = Resource.mbxNeedToAddLogin.Replace("%ASSEMBLY%", am.name);
                msg = msg.Replace("%DATABASE%", am.database.name);
                msg = msg.Replace("%PERMISSION%", PermissionSets[set - 1]);
                DialogResult dr = MessageBox.Show(msg, "Add asymmetric key and login", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (dr == System.Windows.Forms.DialogResult.Yes) AddLogin = true;
                else if (dr == System.Windows.Forms.DialogResult.No)
                {
                    msg = Resource.mbxThenNeedToSetTrustworthy.Replace("%ASSEMBLY%", am.name);
                    msg = msg.Replace("%DATABASE%", am.database.name);
                    msg = msg.Replace("%PERMISSION%", PermissionSets[set - 1]);
                    dr = MessageBox.Show(msg, "Set database to TRUSTWORTHY", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == System.Windows.Forms.DialogResult.Yes) SetTrustworthy = true;
                }
            }

            if (SetTrustworthy) act = ToggleTrustworthy(amNode.Parent, parent);
            if (AddPermission) act = AddPermissionToLogin(amNode, set, parent);
            if (AddLogin) act = AddAsymmetricKeyAndLogin(amNode, parent);
            return act;
        }

        private Action ChangePermissionSet(TreeNode amNode, Action parent, int set)
        {
            Action act = null;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (am.permission_set != set)
            {
                act = am.actions.SingleOrDefault(a => a.action == ActionType.ChangePermissionSet);
                if (act != null)
                {
                    DeregisterAction(act);
                    if (set == (int)act.oldvalue)
                    {
                        am.permission_set = set;
                        if (am.actions.Count == 0) am.changes_pending = false;
                        act = null;
                    }
                    else
                    {
                        am.permission_set = (int)act.oldvalue;
                        act.newvalue = set;
                        RegisterAction(act, parent);
                        am.permission_set = set;
                    }
                }
                else
                {
                    act = am.actions.SingleOrDefault(a => a.action == ActionType.AddAssembly);
                    if (act != null)
                    {
                        int i = act.actionnode.Index;
                        DeregisterAction(act);
                        am.permission_set = set;
                        RegisterAction(act, null);
                        act.actionnode.Remove();
                        tvActions.Nodes.Insert(i, act.actionnode);
                        act.actionnode.SelectedImageIndex = act.actionnode.ImageIndex = set + 1;
                    }
                    else
                    {
                        act = new Action();
                        act.action = ActionType.ChangePermissionSet;
                        act.target = am;
                        act.targetnode = amNode;
                        act.oldvalue = am.permission_set;
                        act.newvalue = set;
                        RegisterAction(act, parent);
                        am.permission_set = set;
                        am.changes_pending = true;
                    }
                }

                if (set > 1 && !am.database.trustworthy && (    am.login == null 
                                                             || am.login.status == installstatus.pending_remove
                                                             || am.login.permission == PermissionSet.SAFE
                                                             || set == 3 && am.login.permission == PermissionSet.EXTERNAL_ACCESS))
                {
                    Action a = AuthoriseNonsafeAssembly(amNode, set, act);
                    if (a == null)
                    {
                        am.permission_set = (int)act.oldvalue;
                        DeregisterAction(act);
                    }
                }


                amNode.ImageIndex = am.permission_set + 1;
                amNode.SelectedImageIndex = am.permission_set + 1;
                tvServers.SelectedNode = amNode;
                toggleServerPaneControls(amNode);
                ShowStatus(amNode, am.status, false, am.changes_pending);
            }
            return act;
        }

        private Action changeTriggerEvents(TreeNode fNode, string eventname, string verb)
        {
            Function f = (Function)fNode.Tag;
            Action act = f.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerEvents);
            Action a = f.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerTarget || p.action == ActionType.AddFunction);
            if (act == null)
            {
                act = new Action();
                act.action = ActionType.ChangeTriggerEvents;
                Trigger oldTrigger = new Trigger(f.trigger);
                if (verb == "SET TIMING")
                {
                    if (eventname == "AFTER") oldTrigger.insteadof = true;
                    else oldTrigger.insteadof = false;
                }
                act.oldvalue = oldTrigger;
                if (verb == "Remove") f.trigger.events.Remove(eventname);
                else if (verb == "Add") f.trigger.events.Add(eventname);
                act.newvalue = f.trigger;
                act.target = f;
                act.targetnode = fNode;
                RegisterAction(act, a);
                f.changes_pending = true;
            }
            else
            {
                Trigger oldTrigger = (Trigger)act.oldvalue;
                Trigger newTrigger = f.trigger;
                List<string> eventlist = new List<string>(f.trigger.events);
                DeregisterAction(act);
                f.trigger = newTrigger;
                if (verb == "Remove") eventlist.Remove(eventname);
                else if (verb == "Add") eventlist.Add(eventname);
                f.trigger.events = eventlist;
                eventlist.Sort();
                oldTrigger.events.Sort();
                if (!oldTrigger.events.SequenceEqual(eventlist) || (oldTrigger.insteadof != f.trigger.insteadof))
                {
                    act.newvalue = f.trigger;
                    RegisterAction(act, a);
                }
                else act = null;
                f.changes_pending = f.actions.Count > 0;
            }
            toggleFunctionControls(f);
            ShowStatus(fNode, f.status, true, f.changes_pending);
            return act;
        }

        private Action changeTriggerTarget(TreeNode fNode, string target)
        {
            Function f = (Function)fNode.Tag;
            Action act = null;

            if (f.status == installstatus.pending_add)
            {
                act = f.actions.Single(p => p.action == ActionType.AddFunction);
                DeregisterAction(act);
                parseTriggerTarget(target, f.trigger, f);
                if (!f.trigger.isdatabase) f.schema = f.trigger.target_schema;
            }
            else
            {
                act = f.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerTarget);
                if (act == null)
                {
                    act = new Action();
                    act.action = ActionType.ChangeTriggerTarget;
                    act.oldvalue = new Trigger(f.trigger);
                    act.newvalue = f.trigger;
                    act.target = f;
                    act.targetnode = fNode;
                    parseTriggerTarget(target, f.trigger, f);
                    if (f.trigger.isdatabase != ((Trigger)act.oldvalue).isdatabase)
                    {
                        Action a = f.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerEvents);
                        if (a != null)
                        {
                            ReverseAction(a);
                            parseTriggerTarget(target, f.trigger, f); //need to do it again because ReverseAction restores an old copy
                        }
                    }
                }
                else
                {
                    ReverseAction(act);
                    Trigger newvalue = new Trigger(f.trigger);
                    parseTriggerTarget(target, newvalue, f);
                    if (f.trigger.isdatabase != ((Trigger)act.newvalue).isdatabase)
                    {
                        Action a = f.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerEvents);
                        if (a != null)
                        {
                            ReverseAction(a);
                            parseTriggerTarget(target, f.trigger, f);//need to do it again because ReverseAction restores an old copy
                        }
                    }
                    if (newvalue.target_type == f.trigger.target_type && newvalue.target_schema == f.trigger.target_schema && newvalue.target == f.trigger.target)
                    {
                        f.changes_pending = f.actions.Count > 0;
                        ShowStatus(fNode, f.status, true, f.changes_pending);
                        return null;
                    }
                    act.newvalue = newvalue;
                    f.trigger = newvalue;
                }
                f.changes_pending = true;
            }
            RegisterAction(act, null);
            toggleFunctionControls(f);
            int i = fNode.Index;
            TreeNode amNode = fNode.Parent;
            fNode.Remove();
            TreeNode f2Node = amNode.Nodes.Insert(i, f.ShortName(false), f.ShortName(false), f.FunctionTypeIconIndex(), f.FunctionTypeIconIndex());
            substituteNodeRecursive(tvActions.Nodes, fNode, f2Node);
            f2Node.ToolTipText = f.tooltiptext();
            f2Node.ContextMenuStrip = cmFunction;
            f2Node.Tag = f;
            cmFunction.Tag = f2Node;
            tvServers.SelectedNode = f2Node;
            ShowStatus(f2Node, f.status, true, f.changes_pending);
            return act;
        }

        private void ClearLibraryChanges(TreeNode lNode)
        {
            Library l = (Library)lNode.Tag;
            l.changes_pending = false;
            l.actions.Clear();
            ShowStatus(lNode, installstatus.in_place, false, false);
            foreach (TreeNode amNode in lNode.Nodes)
            {
                InstalledAssembly am = (InstalledAssembly)amNode.Tag;
                am.changes_pending = false;
                ShowStatus(amNode, installstatus.in_place, false, false);
                foreach (TreeNode fnNode in amNode.Nodes)
                {
                    if (fnNode.Tag.GetType().Name == "Function")
                    {
                        Function fn = (Function)fnNode.Tag;
                        fn.changes_pending = false;
                        ShowStatus(fnNode, fn.status, true, false);
                    }
                }
            }
        }

        private void cloneTrigger(TreeNode fNode)
        {
            Function fn = (Function)fNode.Tag;
            Function f2 = new Function(fn);
            f2.name = "Copy of " + fn.name;
            int i = 2;
            while (fn.assembly.functions.Exists(p => p.name.Equals(f2.name, StringComparison.OrdinalIgnoreCase) && p.type == f2.type && p.schema == f2.schema)) f2.name = "Copy " + i.ToString() + " of " + fn.name;
            f2.status = installstatus.not_installed;
            fn.assembly.functions.Add(f2);

            int index = f2.FunctionTypeIconIndex();
            string nodetext = f2.ShortName(false);
            TreeNode f2Node = fNode.Parent.Nodes.Add(nodetext, nodetext, index, index);
            f2Node.ToolTipText = f2.tooltiptext();
            f2Node.Tag = f2;
            f2Node.ContextMenuStrip = cmFunction;
            Action a = AddFunction(f2Node, null, true);
            ShowStatus(f2Node, f2.status, true, f2.changes_pending);
            tvServers.SelectedNode = f2Node;
            f2Node.BeginEdit();
        }

        private void closeLibrary(TreeNode lNode)
        {
            Library lib = (Library)lNode.Tag;
            if (lib.changes_pending)
            {
                DialogResult dr = MessageBox.Show(Resource.mbxSaveLibraryChangesPrompt, "Close Library", MessageBoxButtons.YesNoCancel);
                if (dr == DialogResult.Cancel) return;
                if (dr == DialogResult.Yes)
                {
                    if (lib.file == null || lib.file == "") SaveLibraryAs(lNode);
                    else
                    {
                        if (WriteLibraryTo(lNode, lib.file)) ClearLibraryChanges(lNode);
                    }
                    if (!lib.changes_pending) lNode.Remove();
                }
                else lNode.Remove();
            }
            else lNode.Remove();
            if (tvAssemblies.SelectedNode != null) toggleLibraryControls((Library)tvAssemblies.SelectedNode.Tag);
            else
            {
                for (int i = 2; i < tsLibrary.Items.Count; i++) tsLibrary.Items[i].Enabled = false;
                lbNoLibrariesLoaded.Visible = true;
            }
        }

        private void Connect()
        {
            connectform.ShowDialog(this);
            using (new HourGlass("Connect to server..."))
            {
                if (connectform.Tag != null)
                {
                    object[] o = (object[])connectform.Tag;
                    SqlConnection sc = (SqlConnection)o[0];
                    SqlConnectionStringBuilder sb = (SqlConnectionStringBuilder)o[1];
                    if (!connections.Exists(p => p.DataSource == sb.DataSource))
                    {
                        connections.Add(sc);
                        if (!sb.PersistSecurityInfo) sb.Password = "";
                        us.Obscure(sb);
//                        sb.PersistSecurityInfo = false;
                        if (!us.Connections.Exists(p => p.DataSource == sb.DataSource)) us.Connections.Add(sb);
                        else
                        {
                            int i = us.Connections.FindIndex(p => p.DataSource == sb.DataSource);
                            us.Connections[i].Clear();
                            us.Connections[i] = sb;
                        }
                        us.LastServer = sb.DataSource;
                        HourGlass.Update("Retrieving data from server...");
                        AddServerToTree(sc, sb);
                    }
                    else
                    {
                        string root = Resource.mbxServerAlreadyConnected.Replace("%SERVER%", sb.DataSource);
                        MessageBox.Show(root, "Server Already Connected");
                    }
                }
            }
        }

        private int CountActionsInNode(XmlNode xn)
        {
            int i = 0;
            if (xn.Name == "Action") i++;
            foreach (XmlNode xc in xn.ChildNodes) i += CountActionsInNode(xc);
            return i;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly[] aa = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in aa) if (a.GetName().Name == args.Name.Split(',')[0]) return a;
            return null;
        }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly asm = null;

            if (resolving) return null;
            resolving = true;
            Assembly[] ra = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies();
            Assembly[] xa = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in ra) if (a.FullName == args.Name) { asm = a; break; }
            if (asm == null) asm = Assembly.ReflectionOnlyLoad(args.Name);
            resolving = false;
            return asm;
        }

        public static string decryptStringFromBytes_AES(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("Key");

            // TDeclare the streams used
            // to decrypt to an in memory
            // array of bytes.
            MemoryStream msDecrypt = null;
            CryptoStream csDecrypt = null;
            StreamReader srDecrypt = null;

            // Declare the RijndaelManaged object
            // used to decrypt the data.
            RijndaelManaged aesAlg = null;

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            try
            {
                // Create a RijndaelManaged object
                // with the specified key and IV.
                aesAlg = new RijndaelManaged();
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                msDecrypt = new MemoryStream(cipherText);
                csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                srDecrypt = new StreamReader(csDecrypt);

                // Read the decrypted bytes from the decrypting stream
                // and place them in a string.
                plaintext = srDecrypt.ReadToEnd();
            }
            finally
            {
                // Clean things up.

                // Close the streams.
                if (srDecrypt != null)
                    srDecrypt.Close();
                if (csDecrypt != null)
                    csDecrypt.Close();
                if (msDecrypt != null)
                    msDecrypt.Close();

                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            return plaintext;

        }

        public void DeregisterAction(Action act)
        {
            act.actionnode.Remove();
            switch (act.action)
            {
                case ActionType.ChangeDatabaseDefaultSchema:
                case ActionType.ToggleTrustworthy:
                    Database db = (Database)act.target;
                    db.actions.Remove(act);
                    break;
                case ActionType.ToggleCLR:
                    Server svr = (Server)act.target;
                    svr.actions.Remove(act);
                    break;
                case ActionType.AddAllObjects:
                case ActionType.AddAssembly:
                case ActionType.ChangePermissionSet:
                case ActionType.DropAllObjects:
                case ActionType.DropAssembly:
                case ActionType.DropFile:
                case ActionType.AddFile:
                case ActionType.AddKeyAndLogin:
                case ActionType.AddPermission:
                case ActionType.DropKeyAndLogin:
                case ActionType.DropPermission:
                    InstalledAssembly am = (InstalledAssembly)act.target;
                    am.actions.Remove(act);
                    break;
                case ActionType.SwapAssembly:
                    InstalledAssembly am1 = (InstalledAssembly)act.oldvalue;
                    InstalledAssembly am2 = (InstalledAssembly)act.newvalue;
                    am1.actions.Remove(act);
                    am2.actions.Remove(act);
                    break;
                case ActionType.AddFunction:
                case ActionType.ChangeFunctionSchema:
                case ActionType.DropFunction:
                case ActionType.RenameFunction:
                    Function fn = (Function)act.target;
                    fn.actions.Remove(act);
                    break;
                case ActionType.SetParameterDefault:
                    fn = ((Parameter)act.target).function;
                    fn.actions.Remove(act);
                    break;
                case ActionType.ChangeTriggerEvents:
                case ActionType.ChangeTriggerTarget:
                    fn = (Function)act.target;
                    //fn.trigger = (Trigger)act.oldvalue;
                    fn.actions.Remove(act);
                    fn.changes_pending = fn.actions.Count > 0;
                    break;
            }
            if (act.parent != null)
            {
                act.parent.subactions.Remove(act);
                if (act.parent.subactions.Count == 0 && (act.parent.action == ActionType.DropAllObjects || act.parent.action == ActionType.AddAllObjects || act.parent.action == ActionType.DropAllAssemblies))
                    DeregisterAction(act.parent);
            }
            toggleActionControls();
        }

        private string DetailedAssemblyName(InstalledAssembly am)
        {
            string sn = am.publicKeyToken.Count() > 0 ? "true" : "false";
            string culture = am.culture.Name == "" ? "neutral" : am.culture.Name;
            string name = am.name + ", permission=" + PermissionSets[am.permission_set - 1] + ", version=" + am.version.ToString(4) + ", created=" + am.create_date.ToString("dd-MMM-yyyy HH:mm:ss") + ", strong name=" + sn + ", culture=" + culture + ", platform=" + am.platform.ToString();
            return name;
        }

        public void DisableStop()
        {
            Stop = false;
            tbStop.Visible = false;
            tbStopLine.Visible = false;
        }

        private Action DropAllAssemblies(TreeNode dbNode, Action parent)
        {
            Database db = (Database)dbNode.Tag;
            Action act = new Action();
            act.action = ActionType.DropAllAssemblies;
            act.target = db;
            act.targetnode = dbNode;
            RegisterAction(act, parent);
            return act;
        }

        private Action DropAllObjects(TreeNode amNode, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            Action act = null;

            act = am.actions.SingleOrDefault(p => p.action == ActionType.AddAllObjects);
            if (act != null) ReverseAction(act);
            if (am.functions.Count(p => p.status == installstatus.in_place || p.status == installstatus.pending_add) > 0)
            {
                act = new Action();
                act.action = ActionType.DropAllObjects;
                act.target = am;
                act.targetnode = amNode;
                RegisterAction(act, parent);

                foreach (TreeNode fnNode in amNode.Nodes)
                {
                    if (fnNode.Tag.GetType().Name == "Function")
                    {
                        Function fn = (Function)fnNode.Tag;
                        if (fn.status == installstatus.in_place) DropFunction(fnNode, act, true);
                    }
                }
            }
            toggleAssemblyControls(am);
            return act;
        }

        private Action DropAssembly(TreeNode amNode, Action parent, bool recursive)
        {
            Action act, a;

            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            if (am.dependents != null)
            {
                while (am.dependents.Count(p => p.status == installstatus.in_place || p.status == installstatus.pending_add) > 0)
                {
                    InstalledAssembly depa = am.dependents.First(p => p.status == installstatus.in_place || p.status == installstatus.pending_add);
                    string root = Resource.mbxRemoveDependentAssemblyPrompt.Replace("%ASSEMBLY1%", am.name).Replace("%ASSEMBLY2%", depa.name);
                    DialogResult dr = MessageBox.Show(root, "Assembly has dependents", MessageBoxButtons.YesNo);
                    if (dr == DialogResult.Yes)
                    {
                        DropAssembly(GetNodeFor(depa), null, recursive);
                        if (depa.status == installstatus.in_place || depa.status == installstatus.pending_add) return null;
                    }
                    else return null;
                }
            }

            if (am.status == installstatus.pending_add)
            {
                act = am.actions.Single(p => p.action == ActionType.AddAssembly);
                DeregisterAction(act);
                amNode.Remove();
                am.database.assemblies.Remove(am);
                act = null;
            }
            else
            {
                act = new Action();

                am.status = installstatus.pending_remove;
                ShowStatus(amNode, am.status, false, false);

                a = am.actions.SingleOrDefault(p => p.action == ActionType.DropAllObjects);
                if (a != null) DeregisterAction(a);

                a = am.actions.SingleOrDefault(p => p.action == ActionType.AddAllObjects);
                if (a != null) DeregisterAction(a);

                act.action = ActionType.DropAssembly;
                act.target = am;
                act.targetnode = amNode;
                RegisterAction(act, parent);

                if (recursive)
                {
                    foreach (TreeNode tn in amNode.Nodes)
                    {
                        if (tn.Tag.GetType().Name == "Function")
                        {
                            Function fn = (Function)tn.Tag;
                            if (fn.status == installstatus.in_place) DropFunction(tn, act, true);
                        }
                        else if (tn.Tag.GetType().Name == "InstalledAssembly")
                        {
                            RemoveFileFromAssembly(tn, act, true);
                        }
                    }
                }
            }

            if (am.references != null) foreach (InstalledAssembly ra in am.references) ra.dependents.Remove(am);
            am.status = installstatus.pending_remove;
            return act;
        }

        private void DropAssemblyFromLibrary(TreeNode amNode)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            TreeNode lNode = amNode.Parent;
            Library lib = (Library)lNode.Tag;

            if (am.dependents != null)
            {
                while (am.dependents.Count() > 0)
                {
                    InstalledAssembly depa = am.dependents.First();
                    TreeNode depNode = GetLibraryNodeFor(depa);
                    if (depNode == null) continue;
                    string root = Resource.mbxRemoveDependentAssemblyPrompt.Replace("%ASSEMBLY1%", am.name).Replace("%ASSEMBLY2%", depa.name);
                    DialogResult dr = MessageBox.Show(root, "Assembly has dependents", MessageBoxButtons.YesNo);
                    if (dr == DialogResult.Yes)
                    {
                        DropAssemblyFromLibrary(depNode);
                        if (depNode.TreeView != null) return;
                    }
                    else return;
                }
            }
            lib.changes_pending = true;
            amNode.Remove();
            lib.assemblies.Remove(am);
            var l = from InstalledAssembly da in lib.assemblies where da.dependents != null && da.dependents.Contains(am) select da;
            while (l.Count() > 0) l.First().dependents.Remove(am);
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            ShowStatus(lNode, installstatus.in_place, false, true);
        }

        private Action DropAsymmetricKeyAndLogin(TreeNode amNode, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            string msg = Resource.mbxDropKeyAndLogin;
            int affected_assemblies = 0;
            msg = msg.Replace("%SERVER%", am.database.server.name);
            msg = msg.Replace("%THUMBPRINT%", "0x" + BitConverter.ToString(am.publicKeyToken).Replace("-", ""));

            List<InstalledAssembly> peers = new List<InstalledAssembly>();
            foreach (Database db in am.database.server.databases)
                foreach (InstalledAssembly a in db.assemblies.Where(p => p.publicKeyToken.SequenceEqual(am.publicKeyToken)))
                {
                    if (a.permission_set > 1 && !db.trustworthy)
                    {
                        msg += "\nDatabase: " + a.database.name + ",\tAssembly: " + a.name;
                        affected_assemblies++;
                    }
                    peers.Add(a);
                }

            if (affected_assemblies > 0)
            {
                DialogResult dr = MessageBox.Show(msg, "Drop asymmetric key and login", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (dr == DialogResult.Cancel) return null;
            }

            Action act = null;
            foreach (InstalledAssembly a in peers)
            {
                act = a.actions.FirstOrDefault(p => p.action == ActionType.AddKeyAndLogin);
                if (act != null) break;
            }

            if (act == null)
            {
                act = new Action();
                act.action = ActionType.DropKeyAndLogin;
                act.target = am;
                act.targetnode = amNode;
                act.oldvalue = am.login;
                am.key.status = installstatus.pending_remove;
                am.login.status = installstatus.pending_remove;
                RegisterAction(act, parent);

            }
            else
            {
                am.key = null;
                am.login = null;
                DeregisterAction(act);
                act = null;
                foreach (InstalledAssembly a in peers.Where(p => p != am))
                {
                    a.key = null;
                    a.login = null;
                }

            }

            foreach (InstalledAssembly a in peers.Where(p => p.actions.Exists(q => q.action == ActionType.AddPermission || q.action == ActionType.DropPermission)))
            {
                Action b = a.actions.FirstOrDefault(p => p.action == ActionType.DropPermission || p.action == ActionType.AddPermission);
                if(b != null) DeregisterAction(b);
            }
            
            toggleAssemblyControls(am);
            ShowStatus(amNode, am.status, false, am.changes_pending);
            return act;
        }

        private Action DropFunction(TreeNode fnNode, Action parent, bool reverseifadded)
        {
            Action act = null;
            Function fn = (Function)fnNode.Tag;
            TreeNode amNode = fnNode.Parent;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            if (fn.status == installstatus.pending_add && reverseifadded)
            {
                act = fn.actions.Last(a => a.action == ActionType.AddFunction);
                DeregisterAction(act);
                fn.status = (installstatus)Enum.Parse(typeof(installstatus), (string)act.oldvalue);
                act = null;
                if (fn.status == installstatus.not_installed && fn.type == "TA" && am.functions.Exists(p => p.assembly_method == fn.assembly_method && p != fn))
                {
                    tvServers.SelectedNode = fnNode.PrevVisibleNode;
                    fnNode.Remove();
                    am.functions.Remove(fn);
                    return null;
                }
            }
            else
            {
                act = new Action();
                act.action = ActionType.DropFunction;
                act.target = fn;
                act.targetnode = fnNode;
                act.oldvalue = fn.status.ToString();
                fn.status = installstatus.pending_remove;
                RegisterAction(act, parent);
            }
            toggleFunctionControls(fn);
            ShowStatus(fnNode, fn.status, true, fn.changes_pending);
            return act;
        }

        private void DropLibraryFunction(TreeNode fnNode)
        {
            Function fn = (Function)fnNode.Tag;
            fn.status = installstatus.not_installed;
            fn.changes_pending = true;
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            ShowStatus(fnNode, fn.status, true, fn.changes_pending);
        }

        private Action DropPermissionFromLogin(TreeNode amNode, int set, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            if (am.login == null) return null;
            Action act;
            act = am.actions.FirstOrDefault(p => p.action == ActionType.AddPermission);
            if (act != null)
            {
                DeregisterAction(act);
                act = null;
            }
            else
            {
                act = new Action();
                act.action = ActionType.DropPermission;
                act.target = am;
                act.targetnode = amNode;
                act.oldvalue = (int)am.login.permission;
                act.newvalue = set;
                am.login.permission = set < 3 ? PermissionSet.EXTERNAL_ACCESS : PermissionSet.UNSAFE;
                RegisterAction(act, parent);
            }
            return act;
        }

        private void EnableStop()
        {
            Stop = false;
            tbStop.Visible = true;
            tbStopLine.Visible = true;
            statusStrip1.Refresh();
        }

        public static byte[] encryptStringToBytes_AES(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("Key");

            // Declare the streams used
            // to encrypt to an in memory
            // array of bytes.
            MemoryStream msEncrypt = null;
            CryptoStream csEncrypt = null;
            StreamWriter swEncrypt = null;

            // Declare the RijndaelManaged object
            // used to encrypt the data.
            RijndaelManaged aesAlg = null;

            // Declare the bytes used to hold the
            // encrypted data.
            byte[] encrypted = null;

            try
            {
                // Create a RijndaelManaged object
                // with the specified key and IV.
                aesAlg = new RijndaelManaged();
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                msEncrypt = new MemoryStream();
                csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                swEncrypt = new StreamWriter(csEncrypt);

                //Write all data to the stream.
                swEncrypt.Write(plainText);

            }
            finally
            {
                // Clean things up.

                // Close the streams.
                if (swEncrypt != null)
                    swEncrypt.Close();
                if (csEncrypt != null)
                    csEncrypt.Close();
                if (msEncrypt != null)
                    msEncrypt.Close();

                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            // Return the encrypted bytes from the memory stream.
            return msEncrypt.ToArray();

        }

        private void ExecuteAction(Action act, bool migrate)
        {
            if (Stop) return;

            Server sv = null;
            Database db = null;
            InstalledAssembly am = null;
            Function fn = null;
            Parameter pm = null;
            bool result = true;
            List<Action> actlist = new List<Action>();

            act.actionnode.EnsureVisible();
            //IntPtr h = tvServers.Handle;
            //SendMessage(h, 0x114, (IntPtr)6, (IntPtr)null);
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.DropFunction && ((Function)p.target).type != "UDT")) actlist.Add(a);
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.DropFunction && ((Function)p.target).type == "UDT")) actlist.Add(a);
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.DropFile 
                                                        || p.action == ActionType.AddKeyAndLogin 
                                                        || p.action == ActionType.AddPermission
                                                        || p.action == ActionType.ToggleTrustworthy)) actlist.Add(a);
            foreach (Action a in actlist) ExecuteAction(a, migrate);
            actlist.Clear();
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.AddFunction && ((Function)p.target).type == "UDT")) actlist.Add(a);
            foreach (Action a in act.subactions.Where(p => !(p.action == ActionType.AddFunction && ((Function)p.target).type == "UDT") 
                                                          && p.action != ActionType.DropFunction 
                                                          && p.action != ActionType.DropFile
                                                          && p.action != ActionType.AddKeyAndLogin
                                                          && p.action != ActionType.AddPermission
                                                          && p.action != ActionType.ToggleTrustworthy)) actlist.Add(a);
            foreach (Action a in actlist)
            {
                a.actionnode.Remove();
                a.parent = null;
                act.subactions.Remove(a);
                tvActions.Nodes.Add(a.actionnode);
            }

            if (    act.action == ActionType.AddKeyAndLogin 
                 || act.action == ActionType.DropKeyAndLogin 
                 || act.action == ActionType.AddPermission
                 || act.action == ActionType.DropPermission)
            {
                am = (InstalledAssembly)act.target;
                db = am.database.server.databases.FirstOrDefault(p => p.name == "master");
                if (db == null)
                {
                    db = new Database();
                    db.server = am.database.server;
                    db.name = "master";
                }
                sv = am.database.server;
            }
            else
            {

                switch (act.target.GetType().Name)
                {
                    case "Server":
                        sv = (Server)act.target;
                        break;
                    case "Database":
                        db = (Database)act.target;
                        sv = db.server;
                        break;
                    case "InstalledAssembly":
                        am = (InstalledAssembly)act.target;
                        db = am.database;
                        sv = db.server;
                        break;
                    case "Function":
                        fn = (Function)act.target;
                        db = fn.assembly.database;
                        sv = db.server;
                        break;
                    case "Parameter":
                        pm = (Parameter)act.target;
                        db = pm.function.assembly.database;
                        sv = db.server;
                        break;
                }
            }
            if (act.targetnode == null || act.targetnode.TreeView == null) act.targetnode = GetNodeFor(act.target);
            if (act.targetnode != null) act.path = act.targetnode.FullPath;

            while (act.sqlcommand != "")
            {
                pBar.PerformStep();
                pBar.ProgressBar.Refresh();
                string[] ca = act.sqlcommand.Replace("\r", "").Split(new string[] { "\nGO\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string cs in ca)
                {
                    result = sv.ExecuteCommand(cs, db);
                    if (!result) break;
                }
                if (!result)
                {
                    string message = act.sqlcommand;
                    Match m = Regex.Match(act.sqlcommand, "0x[\\dA-F]+", RegexOptions.None);
                    if (m.Success) message = message.Replace(m.Value, "{bytes}");
                    DialogResult dr = MessageBox.Show(sv.LastError + "\n" + "Command:\n" + message, "Action Failed", MessageBoxButtons.AbortRetryIgnore);
                    if (dr == DialogResult.Abort)
                    {
                        Stop = true;
                        return;
                    }
                    if (dr == DialogResult.Ignore) break;
                }
                else
                {
                    if (migrate) MigrateExecutedAction(act);
                    else DeregisterAction(act);
                    ShowEffectOfAction(act);
                    if((string)ultraTabControl1.SelectedTab.Tag == "Actions") tvActions.Refresh();
                    else if ((string)ultraTabControl1.SelectedTab.Tag == "History") tvHistory.Refresh();
                    //Application.DoEvents();
                    break;
                }
            }
            if (act.sqlcommand == "") DeregisterAction(act);
            foreach (Action a in actlist) ExecuteAction(a, migrate);
            toggleActionControls();
        }

        private void ExecuteAllActions()
        {
            List<Action> xlist = new List<Action>();

            DialogResult dr = MessageBox.Show(Resource.mbxExecuteAllActionsPrompt, "Execute All Pending Actions", MessageBoxButtons.OKCancel);
            if (dr == DialogResult.OK)
            {
                using (new HourGlass("Executing Actions..."))
                {
                    SuppressEvents = true;
                    SetPBar(TotalActionCount());
                    EnableStop();
                    tvActions.ExpandAll();
                    foreach (TreeNode tn in tvActions.Nodes)
                    {
                        Action a = (Action)tn.Tag;
                        xlist.Add(a);
                    }
                    foreach (Action a in xlist)
                    {
                        if (Stop) break;
                        ExecuteAction(a, true);
                        //tvActions.Refresh();
                        Application.DoEvents();
                    }
                    SuppressEvents = false;
                    toggleActionControls();
                }
            }
        }

        private void ExportAssembly(TreeNode amNode)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveDialog.InitialDirectory == "") saveDialog.InitialDirectory = path;
            saveDialog.Title = "Export Assembly " + amNode.Text + " To Dll File";
            saveDialog.DefaultExt = "dll";
            saveDialog.CheckPathExists = true;
            saveDialog.FileName = am.name + ".dll";
            saveDialog.OverwritePrompt = true;
            saveDialog.ValidateNames = true;
            saveDialog.Filter = "Assembly Files|*.dll";
            DialogResult dr = saveDialog.ShowDialog();
            if (dr == DialogResult.Cancel) return;
            File.WriteAllBytes(saveDialog.FileName, am.bytes);
        }

        private TreeNode FindTreeNode(TreeNodeCollection tnc, int x, int y)
        {
            TreeNode tn2;

            foreach (TreeNode tn in tnc)
            {
                if (tn.Bounds.Left <= x && tn.Bounds.Right >= x && tn.Bounds.Top <= y && tn.Bounds.Bottom >= y)
                    return tn;
                tn2 = FindTreeNode(tn.Nodes, x, y);
                if (tn2 != null) return tn2;
            }
            return null;
        }

        private string GetActionScript(Action act)
        {
            Database db = null;

            string script = "";

            if (act.target != null)
            {
                if (act.action == ActionType.AddKeyAndLogin || act.action == ActionType.DropKeyAndLogin)
                {
                    InstalledAssembly am = (InstalledAssembly)act.target;
                    db = am.database.server.databases.FirstOrDefault(p => p.name == "master");
                    if (db == null)
                    {
                        db = new Database();
                        db.server = am.database.server;
                        db.name = "master";
                    }
                }
                else
                {
                    switch (act.target.GetType().Name)
                    {
                        case "Database":
                            db = (Database)act.target;
                            break;
                        case "InstalledAssembly":
                            InstalledAssembly am = (InstalledAssembly)act.target;
                            db = am.database;
                            break;
                        case "Function":
                            Function fn = (Function)act.target;
                            db = fn.assembly.database;
                            break;
                        case "Parameter":
                            Parameter pm = (Parameter)act.target;
                            db = pm.function.assembly.database;
                            break;
                    }
                }
            }

            if (db != null)
            {
                script = Resource.sqlSetServer.Replace("%SERVER%", db.server.name);
                script += Resource.sqlUseDatabase.Replace("%DATABASE%", db.name) + "\nGO\n\n";
            }
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.DropFile)) script += a.sqlcommand + "\nGO\n\n";
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.DropFunction && ((Function)p.target).type != "UDT")) script += a.sqlcommand + "\nGO\n\n";
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.DropFunction && ((Function)p.target).type == "UDT")) script += a.sqlcommand + "\nGO\n\n";
            if (act.sqlcommand != "" && act.sqlcommand != null) script += act.sqlcommand + "\nGO\n\n";
            foreach (Action a in act.subactions.Where(p => p.action == ActionType.AddFunction && ((Function)p.target).type == "UDT")) script += a.sqlcommand + "\nGO\n\n";
            foreach (Action a in act.subactions.Where(p => !(p.action == ActionType.AddFunction && ((Function)p.target).type == "UDT") && p.action != ActionType.DropFunction && p.action != ActionType.DropFile)) script += a.sqlcommand + "\nGO\n\n";
            script = script.Replace("--GO--", "\nGO\n\n");
            return script;
        }

        private List<Action> GetActionsFor(Server svr)
        {
            List<Action> al = new List<Action>();
            Action a;
            foreach (TreeNode tn in tvActions.Nodes)
            {
                a = (Action)tn.Tag;
                if (GetServerOf(a.target) == svr) al.Add(a);
            }
            return al;
        }

        private TreeNode GetNodeFor(object o)
        {
            if (o == null) return null;
            foreach (TreeNode svNode in tvServers.Nodes)
            {
                if (o.GetType().Name == "Server")
                {
                    Server s1 = (Server)o;
                    Server s2 = (Server)svNode.Tag;
                    if (s1.name.Equals(s2.name, StringComparison.OrdinalIgnoreCase)) return svNode;
                }
                else
                {
                    foreach (TreeNode dbNode in svNode.Nodes)
                    {
                        if (o.GetType().Name == "Database")
                        {
                            Database db1 = (Database)o;
                            Database db2 = (Database)dbNode.Tag;
                            if (db1.FQN == db2.FQN) return dbNode;
                        }
                        else
                        {
                            foreach (TreeNode amNode in dbNode.Nodes)
                            {
                                if (o.GetType().Name == "InstalledAssembly" && ((InstalledAssembly)o).is_assembly)
                                {
                                    InstalledAssembly am1 = (InstalledAssembly)o;
                                    InstalledAssembly am2 = (InstalledAssembly)amNode.Tag;
                                    if (am1.FQN == am2.FQN) return amNode;
                                }
                                else
                                {
                                    foreach (TreeNode fnNode in amNode.Nodes)
                                    {
                                        if (o.GetType().Name == "Function" && fnNode.Tag.GetType().Name == "Function")
                                        {
                                            Function f1 = (Function)o;
                                            Function f2 = (Function)fnNode.Tag;
                                            if (f1.FQN == f2.FQN && f1.type == f2.type && (IsIn(f1.type, "TA", "AF") ? f1.assembly_class == f2.assembly_class : f1.assembly_method == f2.assembly_method)) return fnNode;
                                        }
                                        else if (o.GetType().Name == "InstalledAssembly" && fnNode.Tag.GetType().Name == "InstalledAssembly")
                                        {
                                            InstalledAssembly af1 = (InstalledAssembly)o;
                                            InstalledAssembly af2 = (InstalledAssembly)fnNode.Tag;
                                            if (af1.FQN == af2.FQN) return fnNode;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private TreeNode GetLibraryNodeFor(object o)
        {
            foreach (TreeNode lNode in tvAssemblies.Nodes)
            {
                if (o.GetType().Name == "Library")
                {
                    if (o == lNode.Tag) return lNode;
                }
                else
                {
                    foreach (TreeNode amNode in lNode.Nodes)
                    {
                        if (o.GetType().Name == "InstalledAssembly")
                        {
                            if (o == amNode.Tag) return amNode;
                        }
                        else
                        {
                            foreach (TreeNode fnNode in amNode.Nodes)
                                if (o == fnNode.Tag) return fnNode;
                        }
                    }
                }
            }
            return null;
        }

        private string GetScriptFor(Function function, string action)
        {
            string sqlRoot = "",
                    parmlist = "",
                    returntype = "",
                    triggerevents = "",
                    triggertarget = "",
                    lbrace = "",
                    rbrace = "";

            if (action == "Select" || action == "Execute")
            {
                lbrace = "<";
                rbrace = ">";
            }

            string ps, dv;
            foreach (Parameter p in function.parameters)
            {
                if (p.name == "(output)")
                {
                    switch (p.type)
                    {
                        case "Object":
                            returntype = "sql_variant";
                            break;
                        case "SqlBytes":
                            returntype = "varbinary(max)";
                            break;
                        default:
                            returntype = p.type;
                            break;
                    }
                }
                else
                {
                    ps = lbrace + p.name + " " + Database.SqlNameFor(p.type, p.max_length) + rbrace;
                    dv = p.default_value;
                    if (dv != "" && dv != null)
                    {
                        string s;
                        Match m = Regex.Match(ps, "\\w+");
                        s = m.Value;

                        switch (s)
                        {
                            case "nvarchar":
                            case "varchar":
                            case "char":
                            case "nchar":
                            case "datetime":
                            case "datetimeoffset":
                            case "smalldatetime":
                            case "datetime2":
                            case "date":
                            case "time":
                                dv = "'" + dv + "'";
                                break;
                        }
                        ps += " = " + dv;
                    }

                    if (parmlist == "") parmlist = ps;
                    else parmlist += ", " + ps;
                }
            }


            switch (function.type)
            {
                case "AF":
                    switch (action)
                    {
                        case "Create": sqlRoot = Resource.sqlCreateAggregate; break;
                        case "Drop": sqlRoot = Resource.sqlDropAggregate; break;
                        case "Drop and Create": sqlRoot = Resource.sqlDropAggregate + "\nGO\n\n" + Resource.sqlCreateAggregate; break;
                    }
                    break;
                case "FS":
                case "FT":
                    switch (action)
                    {
                        case "Create": sqlRoot = Resource.sqlCreateFunction; break;
                        case "Drop": sqlRoot = Resource.sqlDropFunction; break;
                        case "Alter": sqlRoot = Resource.sqlAlterFunction; break;
                        case "Drop and Create": sqlRoot = Resource.sqlDropFunction + "\nGO\n\n" + Resource.sqlCreateFunction; break;
                        case "Select": sqlRoot = Resource.sqlSelectFunction; break;
                    }
                    break;
                case "PC":
                    switch (action)
                    {
                        case "Create": sqlRoot = Resource.sqlCreateProcedure; break;
                        case "Drop": sqlRoot = Resource.sqlDropProcedure; break;
                        case "Alter": sqlRoot = Resource.sqlAlterProcedure; break;
                        case "Drop and Create": sqlRoot = Resource.sqlDropProcedure + "\nGO\n\n" + Resource.sqlCreateProcedure; break;
                        case "Execute": sqlRoot = Resource.sqlExecuteProcedure; break;
                    }
                    break;
                case "UDT":
                    switch (action)
                    {
                        case "Create": sqlRoot = Resource.sqlCreateType; break;
                        case "Drop": sqlRoot = Resource.sqlDropType; break;
                        case "Drop and Create": sqlRoot = Resource.sqlDropType + "\nGO\n\n" + Resource.sqlCreateType; break;
                    }
                    break;
                case "TA":

                    if (function.trigger.isdatabase) triggertarget = "DATABASE";
                    else triggertarget = "[" + function.trigger.target_schema + "].[" + function.trigger.target + "]";

                    foreach (string te in function.trigger.events)
                        if (triggerevents == "")
                            if (function.trigger.insteadof) triggerevents = "INSTEAD OF " + te;
                            else triggerevents = "AFTER " + te;
                        else triggerevents += ", " + te;

                    switch (action)
                    {
                        case "Create":
                            if (function.trigger.isdatabase) sqlRoot = Resource.sqlCreateDatabaseTrigger;
                            else sqlRoot = Resource.sqlCreateTrigger;
                            break;
                        case "Drop":
                            if (function.trigger.isdatabase) sqlRoot = Resource.sqlDropDatabaseTrigger;
                            else sqlRoot = Resource.sqlDropTrigger;
                            break;
                        case "Drop and Create":
                            if (function.trigger.isdatabase) sqlRoot = Resource.sqlDropDatabaseTrigger + "\nGO\n\n" + Resource.sqlCreateTrigger;
                            else sqlRoot = Resource.sqlDropTrigger + "\nGO\n\n" + Resource.sqlCreateTrigger;
                            break;
                        case "Change Events":
                            sqlRoot = Resource.sqlAlterTrigger;
                            break;
                        case "Change Target":
                            Action a = function.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerTarget);
                            if (a != null)
                            {
                                Trigger t1 = (Trigger)a.oldvalue;
                                Trigger t2 = (Trigger)a.newvalue;
                                if (t1.isdatabase && !t2.isdatabase)
                                {
                                    sqlRoot = Resource.sqlDropDatabaseTrigger.Replace("%FUNCTION%", function.name);
                                    sqlRoot += "\nGO\n\n" + Resource.sqlCreateTrigger;
                                }
                                else
                                {
                                    sqlRoot = Resource.sqlDropTrigger.Replace("%FUNCTION%", "[" + t1.target_schema + "].[" + function.name + "]");
                                    sqlRoot += "\nGO\n\n" + Resource.sqlCreateTrigger;
                                }
                            }
                            break;
                    }
                    break;
                default:
                    break;
            }

            sqlRoot = sqlRoot.Replace("%DATABASE%", function.assembly.database.name);
            sqlRoot = sqlRoot.Replace("%FUNCTION%", function.ShortName(true));
            sqlRoot = sqlRoot.Replace("%RAWFUNCTION%", function.name);
            sqlRoot = sqlRoot.Replace("%SCHEMA%", function.schema == null || function.schema == "" ? "dbo" : function.schema);
            sqlRoot = sqlRoot.Replace("%PARMLIST%", parmlist);
            sqlRoot = sqlRoot.Replace("%RETURNTYPE%", returntype);
            sqlRoot = sqlRoot.Replace("%ASSEMBLY%", function.assembly.name);
            sqlRoot = sqlRoot.Replace("%CLASS%", function.assembly_class);
            sqlRoot = sqlRoot.Replace("%FUNCTIONENTRYPOINT%", function.assembly_method);
            sqlRoot = sqlRoot.Replace("%EVENT%", triggerevents);
            sqlRoot = sqlRoot.Replace("%TARGET%", triggertarget);
            sqlRoot = sqlRoot.Replace("%DATE%", DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"));

            return sqlRoot;
        }

        private string GetScriptFor(InstalledAssembly assembly, string action)
        {
            string sqlRoot = "";
            string bytestring = "";

            switch (action)
            {
                case "ChangePermissionSet":
                    sqlRoot = Resource.sqlChangePermissionSet;
                    break;
                case "AddKeyAndLogin":
                    sqlRoot = Resource.sqlAddKeyAndLogin;
                    sqlRoot = sqlRoot.Replace("%THUMBPRINT%", "0x" + BitConverter.ToString(assembly.key.Thumbprint).Replace("-", ""));
                    sqlRoot = sqlRoot.Replace("%KEY%", assembly.key.Name);
                    sqlRoot = sqlRoot.Replace("%LOGIN%", assembly.login.Name);
                    sqlRoot = sqlRoot.Replace("%PERMISSION%", assembly.login.permission == PermissionSet.UNSAFE ? "UNSAFE" : "EXTERNAL ACCESS");
                    bytestring = "0x" + String.Concat(Array.ConvertAll(assembly.bytes, x => x.ToString("X2")));
                    break;
                case "AddPermission":
                    sqlRoot = Resource.sqlAddPermission;
                    sqlRoot = sqlRoot.Replace("%LOGIN%", assembly.login.Name);
                    sqlRoot = sqlRoot.Replace("%PERMISSION%", assembly.login.permission == PermissionSet.UNSAFE ? "UNSAFE" : "EXTERNAL ACCESS");
                    break;
                case "DropKeyAndLogin":
                    sqlRoot = Resource.sqlDropKeyAndLogin;
                    sqlRoot = sqlRoot.Replace("%KEY%", assembly.key.Name);
                    sqlRoot = sqlRoot.Replace("%LOGIN%", assembly.login.Name);
                    break;
                case "DropPermission":
                    sqlRoot = Resource.sqlDropPermission;
                    sqlRoot = sqlRoot.Replace("%LOGIN%", assembly.login.Name);
                    sqlRoot = sqlRoot.Replace("%PERMISSION%", assembly.login.permission == PermissionSet.UNSAFE ? "UNSAFE" : "EXTERNAL ACCESS");
                    break;
                case "Add":     //Applies only to associated files
                    sqlRoot = Resource.sqlAddFile.Replace("%FILE%", assembly.name);
                    InstalledAssembly am = assembly.parent;
                    if (am != null) sqlRoot = sqlRoot.Replace("%ASSEMBLY%", am.name);
                    else sqlRoot = sqlRoot.Replace("%ASSEMBLY%", "(ASSEMBLY NAME)");
                    bytestring = "0x" + String.Concat(Array.ConvertAll(assembly.bytes, x => x.ToString("X2")));
                    break;
                case "Create":
                    bytestring = "0x" + String.Concat(Array.ConvertAll(assembly.bytes, x => x.ToString("X2")));
                    sqlRoot = Resource.sqlCreateAssembly;
                    break;
                case "Swap":
                    bytestring = "0x" + String.Concat(Array.ConvertAll(assembly.bytes, x => x.ToString("X2")));
                    sqlRoot = Resource.sqlSwapAssembly;
                    break;
                case "Drop":
                    if (assembly.is_assembly) sqlRoot = Resource.sqlDropAssembly;
                    else
                    {
                        sqlRoot = Resource.sqlDropFile.Replace("%FILE%", assembly.name);
                        am = assembly.parent;
                        if (am != null) sqlRoot = sqlRoot.Replace("%ASSEMBLY%", am.name);
                        else sqlRoot = sqlRoot.Replace("%ASSEMBLY%", "(ASSEMBLY NAME)");
                    }
                    break;
            }

            if (assembly.database != null)
            {
                sqlRoot = sqlRoot.Replace("%DATABASE%", assembly.database.name);
                sqlRoot = sqlRoot.Replace("%OWNER%", assembly.database.default_schema);
            }
            sqlRoot = sqlRoot.Replace("%ASSEMBLY%", assembly.name);
            sqlRoot = sqlRoot.Replace("%ASSEMBLYBYTES%", bytestring);
            sqlRoot = sqlRoot.Replace("%PERMISSIONSET%", PermissionSets[assembly.permission_set - 1].Replace(" ", "_"));
            sqlRoot = sqlRoot.Replace("%DATE%", DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"));

            return sqlRoot;
        }

        private Server GetServerOf(Object o)
        {
            switch (o.GetType().Name)
            {
                case "Server": return (Server)o;
                case "Database": return ((Database)o).server;
                case "InstalledAssembly": return ((InstalledAssembly)o).database == null ? null : ((InstalledAssembly)o).database.server;
                case "Function": return ((Function)o).assembly.database == null ? null : ((Function)o).assembly.database.server;
                case "Parameter": return ((Parameter)o).function.assembly.database == null ? null : ((Parameter)o).function.assembly.database.server;
            }
            return null;
        }

        private string GetUseDBScript(string database)
        {
            string sqlRoot = Resource.sqlUseDatabase.Replace("%DATABASE%", database);
            return sqlRoot;
        }

        private static void HideDatabase(TreeNode node)
        {
            Database database = (Database)node.Tag;
            if (database.changes_pending)
            {
                MessageBox.Show(Resource.errCantHideDatabaseWithPendingActions, "Hide Database");
                return;
            }
            database.show = false;
            Server server = (Server)node.Parent.Tag;
            for (int i = 0; i < server.databases.Count; i++)
            {
                if (server.databases[i].name == database.name)
                {
                    server.databases.RemoveAt(i);
                    server.databases.Insert(i, database);
                    break;
                }
            }
            node.Remove();
        }

        public Action InverseOfAction(Action act, Action parent)
        {
            string root = "";
            string cmd = "";
            Server sv = null;
            Database db = null;
            InstalledAssembly am = null;
            InstalledAssembly af = null;
            Function fn = null;
            Parameter p = null;
            string oldvalue = "";
            string newvalue = "";
            int index = 0;
            if (act.targetnode != null) index = act.targetnode.ImageIndex;
            Action react = new Action();
            react.target = act.target;
            react.oldvalue = act.newvalue;
            react.newvalue = act.oldvalue;
            react.parent = parent;
            react.targetnode = GetNodeFor(act.target);
            if (react.targetnode == null) react.path = act.path;
            else react.path = react.targetnode.FullPath;
            if (act.action != ActionType.ChangeTriggerTarget) foreach (Action a in act.subactions) react.subactions.Add(InverseOfAction(a, react));
            List<Action> relatedactions = new List<Action>();

            switch (act.action)
            {
                case ActionType.AddAssembly:
                    react.action = ActionType.DropAssembly;
                    react.newvalue = null;
                    react.oldvalue = null;
                    root = Resource.actnDropAssembly;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "Drop");
                    break;
                case ActionType.SwapAssembly:
                    react.action = ActionType.SwapAssembly;
                    root = Resource.actnSwapAssembly;
                    am = (InstalledAssembly)act.oldvalue;
                    cmd = GetScriptFor(am, "Swap");
                    InstalledAssembly amOld = (InstalledAssembly)act.newvalue;
                    newvalue = am.version.ToString(4);
                    oldvalue = amOld.version.ToString(4);
                    db = amOld.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.AddAllObjects:
                    react.action = ActionType.DropAllObjects;
                    root = Resource.actnDropAssemblyObjects;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.AddFunction:
                    react.action = ActionType.DropFunction;
                    root = Resource.actnDropFunction;
                    fn = (Function)act.target;
                    am = fn.assembly;
                    cmd = GetScriptFor(fn, "Drop");
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropAllObjects:
                    react.action = ActionType.AddAllObjects;
                    root = Resource.actnAddAssemblyObjects;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropAssembly:
                    react.action = ActionType.AddAssembly;
                    react.newvalue = react.target;
                    root = Resource.actnAddAssembly;
                    am = (InstalledAssembly)act.target;
                    cmd = GetScriptFor(am, "Create");
                    db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropFunction:
                    react.action = ActionType.AddFunction;
                    react.newvalue = act.target;
                    root = Resource.actnAddFunction;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Create");
                    am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropFile:
                    react.action = ActionType.AddFile;
                    react.newvalue = react.target;
                    root = Resource.actnAddFile;
                    af = (InstalledAssembly)act.target;
                    am = af.parent;
                    cmd = Resource.sqlAddFile.Replace("%ASSEMBLY%", am.name);
                    cmd = cmd.Replace("%FILE%", af.name);
                    string bytestring = "0x" + String.Concat(Array.ConvertAll(af.bytes, x => x.ToString("X2")));
                    cmd = cmd.Replace("%ASSEMBLYBYTES%", bytestring);
                    newvalue = af.name;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.AddFile:
                    react.action = ActionType.DropFile;
                    react.newvalue = null;
                    react.oldvalue = null;
                    root = Resource.actnDropFile;
                    af = (InstalledAssembly)act.target;
                    //am = (InstalledAssembly)act.targetnode.Parent.Tag;
                    am = (InstalledAssembly)(GetNodeFor(af.parent).Tag);
                    cmd = Resource.sqlDropFile.Replace("%ASSEMBLY%", am.name);
                    cmd = cmd.Replace("%FILE%", af.name);
                    oldvalue = af.name;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.ChangePermissionSet:
                    react.action = ActionType.ChangePermissionSet;
                    root = Resource.actnChangePermissionSet;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    newvalue = PermissionSets[(int)act.oldvalue - 1];
                    oldvalue = PermissionSets[(int)act.newvalue - 1];
                    am.permission_set = (int)act.oldvalue;
                    cmd = GetScriptFor(am, "ChangePermissionSet");
                    am.permission_set = (int)act.newvalue;
                    break;
                case ActionType.AddKeyAndLogin:
                    react.action = ActionType.DropKeyAndLogin;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    react.oldvalue = am.login;
                    root = Resource.actnDropKeyAndLogin;
                    root = root.Replace("%KEY%", am.key.Name);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x"+BitConverter.ToString(am.publicKeyToken).Replace("-",""));
                    cmd = GetScriptFor(am, "DropKeyAndLogin");
                    am.key.status = installstatus.pending_remove;
                    am.login.status = installstatus.pending_remove;
                    break;
                case ActionType.AddPermission:
                    react.action = ActionType.DropPermission;
                    react.newvalue = act.newvalue;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    root = Resource.actnDropPermission;
                    root = root.Replace("%PERMISSION%", am.login.permission == PermissionSet.UNSAFE ? "UNSAFE" : "EXTERNAL ACCESS");
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x" + BitConverter.ToString(am.publicKeyToken).Replace("-", ""));
                    cmd = GetScriptFor(am, "DropPermission");
                    break;
                case ActionType.DropPermission:
                    react.action = ActionType.AddPermission;
                    react.newvalue = act.newvalue;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    root = Resource.actnDropPermission;
                    root = root.Replace("%PERMISSION%", am.login.permission == PermissionSet.UNSAFE ? "UNSAFE" : "EXTERNAL ACCESS");
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x" + BitConverter.ToString(am.publicKeyToken).Replace("-", ""));
                    cmd = GetScriptFor(am, "AddPermission");
                    break;
                case ActionType.DropKeyAndLogin:
                    react.action = ActionType.AddKeyAndLogin;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    am.login = (Login)act.oldvalue;
                    am.key = am.login.key;
                    foreach(Database d in am.database.server.databases)
                        foreach (InstalledAssembly a in d.assemblies.Where(q => q.publicKeyToken.SequenceEqual(am.publicKeyToken) && q != am))
                        {
                            a.login = am.login;
                            a.key = am.key;
                        }
                    am.key.status = installstatus.pending_add;
                    am.login.status = installstatus.pending_add;
                    root = Resource.actnAddKeyAndLogin;
                    root = root.Replace("%KEY%", am.key.Name);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x"+BitConverter.ToString(am.publicKeyToken).Replace("-",""));
                    cmd = GetScriptFor(am, "AddKeyAndLogin");
                    break;
                case ActionType.ChangeDatabaseDefaultSchema:
                    react.action = ActionType.ChangeDatabaseDefaultSchema;
                    root = Resource.actnChangeDatabaseDefaultSchema;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    newvalue = (string)act.oldvalue;
                    oldvalue = (string)act.newvalue;
                    break;
                case ActionType.ChangeFunctionSchema:
                    react.action = ActionType.ChangeFunctionSchema;
                    root = Resource.actnChangeFunctionSchema;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop") + "--GO--";
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    newvalue = (string)act.oldvalue;
                    oldvalue = (string)act.newvalue;
                    fn.schema = newvalue;
                    cmd += GetScriptFor(fn, "Create");
                    fn.schema = oldvalue;
                    break;
                case ActionType.RenameFunction:
                    react.action = ActionType.RenameFunction;
                    root = Resource.actnRenameFunction;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop") + "--GO--";
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    newvalue = (string)act.oldvalue;
                    oldvalue = (string)act.newvalue;
                    fn.name = newvalue;
                    cmd += GetScriptFor(fn, "Create");
                    fn.name = oldvalue;
                    break;
                case ActionType.SetParameterDefault:
                    react.action = ActionType.SetParameterDefault;
                    root = Resource.actnChangeParameterDefault;
                    p = (Parameter)act.target;
                    newvalue = (string)act.oldvalue;
                    oldvalue = (string)act.newvalue;
                    fn = p.function;
                    p.default_value = newvalue;
                    cmd = GetScriptFor(fn, "Alter");
                    p.default_value = oldvalue;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.ToggleTrustworthy:
                    react.action = ActionType.ToggleTrustworthy;
                    root = Resource.actnChangeTrustworthySetting;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    oldvalue = (bool)act.newvalue ? "ON" : "OFF";
                    newvalue = (bool)act.oldvalue ? "ON" : "OFF";
                    cmd = Resource.sqlSetTrustworthy;
                    cmd = cmd.Replace("%DATABASE%", db.name);
                    cmd = cmd.Replace("%NEWVALUE%", newvalue);
                    break;
                case ActionType.ToggleCLR:
                    react.action = ActionType.ToggleCLR;
                    root = Resource.actnChangeCLREnabledSetting;
                    sv = (Server)act.target;
                    oldvalue = (bool)act.newvalue ? "1" : "0";
                    newvalue = (bool)act.oldvalue ? "1" : "0";
                    cmd = Resource.sqlSetClrEnabled.Replace("%NEWVALUE%", newvalue);
                    break;
                case ActionType.ChangeTriggerEvents:
                    react.action = ActionType.ChangeTriggerEvents;
                    root = Resource.actnChangeTriggerEvents;
                    fn = (Function)act.target;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    Trigger newtrigger = (Trigger)act.oldvalue;
                    newvalue = newtrigger.insteadof ? "INSTEAD OF " : "AFTER ";
                    newvalue += String.Join(", ", newtrigger.events.ToArray());
                    string target;
                    if (newtrigger.isdatabase) target = "DATABASE";
                    else if (newtrigger.target_type == "V ") target = "view " + fn.ShortName(true);
                    else target = "table " + fn.ShortName(true);
                    root = root.Replace("%TARGET%", target);
                    fn.trigger = newtrigger;
                    cmd = GetScriptFor(fn, "Change Events");
                    fn.trigger = (Trigger)act.newvalue;
                    break;
                case ActionType.ChangeTriggerTarget:
                    react.action = ActionType.ChangeTriggerTarget;
                    root = Resource.actnChangeTriggerTarget;
                    fn = (Function)act.target;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    newtrigger = (Trigger)act.oldvalue;
                    if (newtrigger.isdatabase) target = "DATABASE";
                    else if (newtrigger.target_type == "V ") target = "view [" + newtrigger.target_schema + "].[" + newtrigger.target + "]";
                    else target = "table [" + newtrigger.target_schema + "].[" + newtrigger.target + "]";
                    root = root.Replace("%NEWVALUE%", target);
                    fn.trigger = newtrigger;
                    act.newvalue = react.newvalue;
                    act.oldvalue = react.oldvalue;
                    cmd = GetScriptFor(fn, "Change Target");
                    fn.trigger = (Trigger)act.oldvalue;
                    act.newvalue = react.oldvalue;
                    act.oldvalue = react.newvalue;
                    break;
            }
            if (p != null) root = root.Replace("%PARAMETER%", p.name);
            if (sv != null) root = root.Replace("%SERVER%", sv.name);
            if (db != null) root = root.Replace("%DATABASE%", db.name);
            if (am != null) root = root.Replace("%ASSEMBLY%", am.name);
            if (fn != null) root = root.Replace("%FUNCTION%", fn.ShortName(true));
            if (fn != null) root = root.Replace("%TYPE%", fn.ShortFunctionTypeName());
            root = root.Replace("%OLDVALUE%", oldvalue);
            root = root.Replace("%NEWVALUE%", newvalue);
            react.sqlcommand = cmd;
            react.displaytext = root;
            return react;
        }

        private void LoadActions()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (LoadDialog.InitialDirectory == "") LoadDialog.InitialDirectory = path;
            LoadDialog.Title = "Open Actions File";
            LoadDialog.DefaultExt = "act";
            LoadDialog.FileName = "*.act";
            LoadDialog.Filter = "Action Files|*.act";
            LoadDialog.CheckFileExists = true;
            LoadDialog.Multiselect = false;
            DialogResult dr = LoadDialog.ShowDialog();
            LoadDialog.Multiselect = true;
            if (dr == DialogResult.OK)
            {
                if (tvActions.Nodes.Count > 0)
                {
                    dr = MessageBox.Show(Resource.mbxLoadActionsOverOthersPrompt, "Replace Existing Actions?", MessageBoxButtons.YesNoCancel);
                    if (dr == DialogResult.Cancel) return;
                    if (dr == DialogResult.Yes)
                        foreach (TreeNode tn in tvActions.Nodes)
                        {
                            Action act = (Action)tn.Tag;
                            ReverseAction(act);
                        }
                }
                using (new HourGlass("Loading And Applying Actions..."))
                {
                    Application.DoEvents();
                    SuppressEvents = true;
                    ReadActionsFromFile(LoadDialog.FileName);
                    RelinkActionAssemblies(tvActions.Nodes);
                    SuppressEvents = false;
                    toggleActionControls();
                }
            }
        }

        private void LoadConnectionSettings()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (LoadDialog.InitialDirectory == "") LoadDialog.InitialDirectory = path;
            LoadDialog.Title = "Open Connection Settings File";
            LoadDialog.DefaultExt = "cxs";
            LoadDialog.FileName = "*.cxs";
            LoadDialog.Filter = "Connection Settings Files|*.cxs";
            LoadDialog.CheckFileExists = true;
            LoadDialog.Multiselect = false;
            DialogResult dr = LoadDialog.ShowDialog();
            LoadDialog.Multiselect = true;
            if (dr != DialogResult.Cancel) ReadConnectionsFromFile(LoadDialog.FileName);
        }

        private void MergeLibrary(TreeNode lNode)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (LoadDialog.InitialDirectory == "") LoadDialog.InitialDirectory = path;
            LoadDialog.Title = "Open Assembly Library For Merging With Library " + lNode.Text;
            LoadDialog.DefaultExt = "alb";
            LoadDialog.FileName = "*.alb";
            LoadDialog.Filter = "Assembly Libraries|*.alb";
            LoadDialog.CheckFileExists = true;
            DialogResult dr = LoadDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                Library lib = (Library)lNode.Tag;
                List<InstalledAssembly> amlist = new List<InstalledAssembly>();
                foreach (string file in LoadDialog.FileNames)
                {
                    Library sublib = ReadLibraryFrom(file);
                    foreach (InstalledAssembly am in sublib.assemblies)
                    {
                        if (!lib.assemblies.Exists(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase))) amlist.Add(am);
                        else
                        {
                            int c = lib.assemblies.Single(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase)).version.CompareTo(am.version);
                            if (c != 0)
                            {
                                string root = Resource.mbxMergeLibraryNonIdenticalAssemblyExistsPrompt;
                                root = root.Replace("%LIBRARY%", sublib.name);
                                root = root.Replace("%COMPARE%", c > 0 ? "a newer" : "an older");
                                root = root.Replace("%ASSEMBLY%", am.name);
                                dr = MessageBox.Show(root, "Replace Assembly with Different Version?", MessageBoxButtons.YesNoCancel);
                                if (dr == DialogResult.Cancel) return;
                                if (dr == DialogResult.Yes) amlist.Add(am);
                            }
                        }
                    }
                }
                foreach (InstalledAssembly am in amlist)
                {
                    InstalledAssembly amOld = lib.assemblies.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                    if (amOld != null) lib.assemblies.Remove(amOld);
                    lib.assemblies.Add(am);
                    am.changes_pending = true;
                }
                int i = lNode.Index;
                lNode.Remove();
                lib.changes_pending = true;
                lNode = AddLibraryToTree(lib, i);
                tvAssemblies.SelectedNode = lNode;
            }
        }

        private void MigrateExecutedAction(Action a)
        {
            TreeNode node = null;
            TreeNode anode = a.actionnode;
            a.sequence = ActionSequence++;
            if ((string)tvHistory.Tag == "ROLLBACK")
            {
                node = new TreeNode(InverseOfAction(a, null).displaytext, anode.ImageIndex, anode.SelectedImageIndex);
                node.Tag = a;
                tvHistory.Nodes.Insert(0, node);
                node.StateImageIndex = 5;
            }
            else
            {
                node = new TreeNode(anode.Text, anode.ImageIndex, anode.SelectedImageIndex);
                node.Tag = a;
                tvHistory.Nodes.Add(node);
                node.StateImageIndex = 6;
            }
            node.ContextMenuStrip = cmHistoryAction;
            tvHistory_NodeAdded();
            DeregisterAction(a);
            a.actionnode = node;
        }

        private void OpenLibrary()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (LoadDialog.InitialDirectory == "") LoadDialog.InitialDirectory = path;
            LoadDialog.Title = "Open Assembly Library";
            LoadDialog.DefaultExt = "alb";
            LoadDialog.FileName = "*.alb";
            LoadDialog.Filter = "Assembly Libraries|*.alb";
            LoadDialog.CheckFileExists = true;
            DialogResult dr = LoadDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                string msg = LoadDialog.FileNames.Count() > 1 ? "Loading Libaries..." : "Loading Library";
                using (new HourGlass(msg))
                {
                    Application.DoEvents();
                    foreach (string file in LoadDialog.FileNames)
                    {
                        bool proceed = true;
                        foreach (TreeNode lNode in tvAssemblies.Nodes)
                        {
                            Library lb = (Library)lNode.Tag;
                            if (lb.file == file)
                            {
                                dr = MessageBox.Show(Resource.mbxReloadLibraryPrompt.Replace("%FILE%", Path.GetFileName(file)), "Library Already Loaded", MessageBoxButtons.YesNo);
                                if (dr == DialogResult.No) proceed = false;
                                else lNode.Remove();
                                break;
                            }
                        }
                        if (proceed)
                        {
                            Library lib = ReadLibraryFrom(file);
                            tvAssemblies.SelectedNode = AddLibraryToTree(lib, -1);
                        }
                    }
                }
            }
        }

        private void parseTriggerTarget(string target, Trigger t, Function f)
        {
            if (target == "DATABASE")
            {
                if (!t.isdatabase)
                {
                    t.events.Clear();
                    t.events.Add("CREATE_TABLE");
                }
                t.isdatabase = true;
                t.target = "";
                t.target_schema = "";
                t.target_type = "DB";
                return;
            }
            else if (t.isdatabase)
            {
                t.isdatabase = false;
                t.events.Clear();
                t.events.Add("UPDATE");
            }
            if (target.Substring(0, 6) == "(view)")
            {
                target = target.Substring(7);
                t.target_type = "V ";
                t.insteadof = true;
            }
            else
            {
                target = target.Substring(8);
                t.target_type = "U ";
            }
            t.target_schema = (target.Split('.'))[0];
            t.target = (target.Split('.'))[1];
        }

        private void PasteIntoLibrary(DataObject o, ref TreeNode tn, int KeyState)
        {
            DialogResult dr;
            TreeNode stn = null;
            if (tn == null) return;

            if (o.GetDataPresent("Assembly"))
            {
                bool OneFunction = false;
                Function function = null;

                if (o.GetDataPresent("Function")) OneFunction = true;

                InstalledAssembly la = (InstalledAssembly)o.GetData("Assembly");
                if (o.GetDataPresent("LibraryNode"))
                {
                    stn = (TreeNode)o.GetData("LibraryNode");
                    if (tn == stn || (tn.Level == 1 && tn.Parent == stn) || (tn.Level == 2 && tn.Parent.Parent == stn)) return;
                }
                else if (o.GetDataPresent("DatabaseNode")) stn = (TreeNode)o.GetData("DatabaseNode");
                if (tn.Level == 1) tn = tn.Parent;
                else if (tn.Level == 2) tn = tn.Parent.Parent;
                Library lb = (Library)tn.Tag;
                InstalledAssembly am = lb.assemblies.SingleOrDefault(ay => ay.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
                if (am != null)
                {
                    TreeNode amNode = (from TreeNode t in tn.Nodes where t.Tag == am select t).First();
                    if (la.version != am.version)
                    {
                        string msg = Resource.mbxNonIdenticalAssemblyAlreadyInstalledPrompt.Replace("%ASSEMBLY%", am.name);
                        msg = msg.Replace("%DATABASE%", lb.name);
                        msg = msg.Replace("database", "library");
                        msg = msg.Replace("%OLDVERSION%", am.version.ToString(4));
                        msg = msg.Replace("%OLDDATE%", am.create_date.ToString(Resource.dateFormat));
                        msg = msg.Replace("%NEWVERSION%", la.version.ToString(4));
                        msg = msg.Replace("%NEWDATE%", la.create_date.ToString(Resource.dateFormat));
                        dr = MessageBox.Show(msg, Resource.mbxNonIdenticalAssemblyAlreadyInstalledTitle, MessageBoxButtons.YesNo);
                        if (dr == DialogResult.No) return;
                        SwapLibraryAssembly(amNode, la, lb);
                    }
                    else if (!OneFunction)
                    {
                        string msg = Resource.mbxIdenticalAssemblyAlreadyInstalledPrompt.Replace("%ASSEMBLY%", am.name);
                        msg = msg.Replace("%DATABASE%", lb.name);
                        msg = msg.Replace("database", "library");
                        msg = msg.Replace("%VERSION%", am.version.ToString(4));
                        msg = msg.Replace("%DATE%", am.create_date.ToString(Resource.dateFormat));
                        MessageBox.Show(msg, Resource.mbxIdenticalAssemblyAlreadyInstalledTitle, MessageBoxButtons.OK);
                        return;
                    }
                    if (OneFunction)
                    {
                        function = (Function)o.GetData("Function");
                        Function fn = am.functions.FirstOrDefault(f => f.name.Equals(function.name, StringComparison.OrdinalIgnoreCase) && f.type == function.type);
                        TreeNode fnNode = (from TreeNode t in amNode.Nodes where t.Tag == fn select t).First();
                        switch (fn.status)
                        {
                            case installstatus.not_installed:
                                AddOrRemoveLibraryFunction(fnNode);
                                return;
                            case installstatus.in_place:
                                MessageBox.Show(ActionText(Resource.mbxDragFunctionIntoExistingAssemblyWhereAlreadyInstalledPrompt, "", "", am.name, fn.ShortName(true), "", ""), Resource.mbxDragFunctionIntoExistingAssemblyTitle);
                                return;
                        }
                    }
                }
                else
                {
                    List<string> lbfunctions = new List<string>();
                    foreach (InstalledAssembly dba in lb.assemblies)
                        foreach (Function f in dba.functions)
                            lbfunctions.Add(f.name);
                    if (OneFunction)
                    {
                        function = (Function)o.GetData("Function");
                        foreach (Function fn in la.functions) if (fn.name != function.name) fn.status = installstatus.not_installed;
                        function = la.functions.Single(p => p.ShortName(false) == function.ShortName(false));
                        if (lbfunctions.Contains(function.name))
                        {
                            string root = Resource.mbxFunctionWithSameNameAlreadyExistsPrompt;
                            root = root.Replace("%FUNCTION%", function.name);
                            root = root.Replace("%DATABASE%", lb.name);
                            root = root.Replace("database", "library");
                            dr = MessageBox.Show(root, Resource.mbxFunctionWithSameNameAlreadyExistsTitle, MessageBoxButtons.OKCancel);
                            if (dr == DialogResult.Cancel) return;
                            function.status = installstatus.not_installed;
                        }
                        else function.status = installstatus.in_place;
                    }
                    else
                    {
                        foreach (Function f in la.functions)
                        {
                            if (f.status == installstatus.pending_add) f.status = installstatus.in_place;
                            else if (f.status == installstatus.pending_remove) f.status = installstatus.not_installed;
                        }
                    }
                    TreeNode anode = AddAssemblyToLibrary(tn, la);
                }
                tn.Expand();
                if (stn != null && (KeyState & 4) > 0)
                {
                    if (stn.Tag.GetType().Name == "Library")
                    {
                        Library l = (Library)stn.Tag;
                        Function f = null;
                        InstalledAssembly a = null;
                        a = l.assemblies.SingleOrDefault(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
                        if (a != null && OneFunction) f = a.functions.SingleOrDefault(p => p.name.Equals(function.name, StringComparison.OrdinalIgnoreCase) && p.type == function.type);
                        if (f != null) DropLibraryFunction(GetLibraryNodeFor(f));
                        else if (a != null)
                        {
                            TreeNode amNode = GetLibraryNodeFor(a);
                            DropAssemblyFromLibrary(amNode);
                        }
                    }
                    else if (stn.Tag.GetType().Name == "Database")
                    {
                        Database db = (Database)stn.Tag;
                        Function f = null;
                        InstalledAssembly a = null;
                        a = db.assemblies.SingleOrDefault(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
                        if (a != null && OneFunction) f = a.functions.SingleOrDefault(p => p.name.Equals(function.name, StringComparison.OrdinalIgnoreCase) && p.type == function.type && p.schema == function.schema);
                        if (f != null) DropFunction(GetNodeFor(f), null, true);
                        else if (a != null)
                        {
                            TreeNode amNode = GetNodeFor(a);
                            DropAssembly(amNode, null, true);
                        }
                    }
                }
            }
            else if (o.GetDataPresent("File"))
            {
                InstalledAssembly la = (InstalledAssembly)o.GetData("File");
                if (tn.Level == 2) tn = tn.Parent;
                AddFileToAssemblyInLibrary(tn, la);
                if (o.GetDataPresent("AssemblyNode")) stn = (TreeNode)o.GetData("AssemblyNode");
                if (stn != null && (KeyState & 4) > 0)
                {
                    InstalledAssembly a = (InstalledAssembly)stn.Tag;
                    if (a.database != null)
                    {
                        TreeNode fNode = (from TreeNode xn in stn.Nodes where xn.Tag.GetType().Name == "InstalledAssembly" && xn.Text == la.name select xn).FirstOrDefault();
                        RemoveFileFromAssembly(fNode, null, true);
                    }
                    else
                    {
                        TreeNode fNode = (from TreeNode xn in stn.Nodes where xn.Tag.GetType().Name == "InstalledAssembly" && xn.Text == la.name select xn).FirstOrDefault();
                        RemoveFileFromAssemblyInLibrary(fNode);
                    }
                }
            }
            else if (o.ContainsFileDropList())
            {
                string[] sa = new string[o.GetFileDropList().Count];
                o.GetFileDropList().CopyTo(sa, 0);
                if (tn.Level == 0) foreach (string s in sa) AddAssemblyFromFileToLibrary(tn, s, sa);
                else if (tn.Level == 1) foreach (string s in sa) AddFileToAssemblyInLibrary(tn, s);
                else foreach (string s in o.GetFileDropList()) AddFileToAssemblyInLibrary(tn.Parent, s);
                if (tn.Level < 2) tn.Expand();
                else tn.Parent.Expand();
            }
        }

        private void PasteIntoDatabase(DataObject o, TreeNode tn, int KeyState)
        {
            Database db = null;
            InstalledAssembly am = null;
            TreeNode dbNode = null;
            TreeNode amNode = null;
            TreeNode stn = null;
            Action act = null;
            Function fn = null;

            if (tn != null && tn.Level > 0)
            {
                int level = tn.Level;
                if (level == 1)
                {
                    dbNode = tn;
                }
                else if (level == 2)
                {
                    amNode = tn;
                    dbNode = tn.Parent;
                }
                else if (level == 3)
                {
                    amNode = tn.Parent;
                    dbNode = tn.Parent.Parent;
                }
                else return;
                db = (Database)dbNode.Tag;

                if (o.GetDataPresent("Assembly"))
                {
                    if (o.GetDataPresent("LibraryNode"))
                    {
                        stn = (TreeNode)o.GetData("LibraryNode");
                    }
                    else if (o.GetDataPresent("DatabaseNode"))
                    {
                        stn = (TreeNode)o.GetData("DatabaseNode");
                        if (tn == stn || (tn.Level == 2 && tn.Parent == stn) || (tn.Level == 3 && tn.Parent.Parent == stn)) return;
                    }

                    InstalledAssembly la = (InstalledAssembly)o.GetData("Assembly");
                    la.key = null;
                    la.login = null;
                    if (o.GetDataPresent("Function"))
                    {
                        fn = (Function)o.GetData("Function");
                        foreach (Function f2 in la.functions) f2.status = installstatus.not_installed;
                        act = AddOrUpdateAssembly(la, dbNode, null, false);
                        if (act != null) am = (InstalledAssembly)act.target;
                        else am = db.assemblies.SingleOrDefault(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
                        Function f = (am.functions.SingleOrDefault(p => p.name.Equals(fn.name, StringComparison.OrdinalIgnoreCase) && p.type == fn.type));
                        if (f != null)
                        {
                            TreeNode fNode = GetNodeFor(f);
                            if (fNode != null) AddFunction(fNode, act, true);
                        }
                    }
                    else act = AddOrUpdateAssembly(la, dbNode, null, true);
                    if (stn != null && (KeyState & 4) > 0)
                    {
                        if (stn.Tag.GetType().Name == "Library")
                        {
                            Library l = (Library)stn.Tag;
                            Function f = null;
                            InstalledAssembly a = null;
                            a = l.assemblies.SingleOrDefault(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
                            if (a != null && fn != null) f = a.functions.SingleOrDefault(p => p.name.Equals(fn.name, StringComparison.OrdinalIgnoreCase) && p.type == fn.type);
                            if (f != null) DropLibraryFunction(GetLibraryNodeFor(f));
                            else if (a != null)
                            {
                                GetLibraryNodeFor(a).Remove();
                                l.assemblies.Remove(a);
                                l.changes_pending = true;
                            }
                        }
                        else if (stn.Tag.GetType().Name == "Database")
                        {
                            db = (Database)stn.Tag;
                            Function f = null;
                            InstalledAssembly a = null;
                            a = db.assemblies.SingleOrDefault(p => p.name.Equals(la.name, StringComparison.OrdinalIgnoreCase));
                            if (a != null && fn != null) f = a.functions.SingleOrDefault(p => p.name.Equals(fn.name, StringComparison.OrdinalIgnoreCase) && p.type == fn.type && p.schema == fn.schema);
                            if (f != null) DropFunction(GetNodeFor(f), null, true);
                            else if (a != null)
                            {
                                amNode = GetNodeFor(a);
                                DropAssembly(amNode, null, true);
                            }
                        }
                    }

                }
                else if (o.GetDataPresent("File") && amNode != null)
                {
                    InstalledAssembly la = (InstalledAssembly)o.GetData("File");
                    AddFileToAssemblyInDatabase(amNode, la, null, true);
                    if (o.GetDataPresent("AssemblyNode")) stn = (TreeNode)o.GetData("AssemblyNode");
                    if (stn != null && (KeyState & 4) > 0)
                    {
                        InstalledAssembly a = (InstalledAssembly)stn.Tag;
                        if (a.database != null)
                        {
                            TreeNode fNode = (from TreeNode xn in stn.Nodes where xn.Tag.GetType().Name == "InstalledAssembly" && xn.Text == la.name select xn).FirstOrDefault();
                            RemoveFileFromAssembly(fNode, null, true);
                        }
                        else
                        {
                            TreeNode fNode = (from TreeNode xn in stn.Nodes where xn.Tag.GetType().Name == "InstalledAssembly" && xn.Text == la.name select xn).FirstOrDefault();
                            RemoveFileFromAssemblyInLibrary(fNode);
                        }
                    }
                }
                else if (o.ContainsFileDropList())
                {
                    if (amNode != null) am = (InstalledAssembly)amNode.Tag;
                    string[] sa = new string[o.GetFileDropList().Count];
                    o.GetFileDropList().CopyTo(sa, 0);
                    foreach (string s in sa)
                    {
                        if (level == 1 || (am != null && Path.GetFileNameWithoutExtension(s) == am.name && Path.GetExtension(s) == ".dll")) AddAssemblyFromFileToDatabase(dbNode, s, sa);
                        else if (level == 2) AddFileToAssemblyInDatabase(amNode, s);
                    }
                }
            }
        }

        private void populateDdlTriggerToolStripMenuItems(ToolStripMenuItem me)
        {
            string[] items;
            ToolStripMenuItem mi;
            ToolStripItem hold1 = null;
            ToolStripItem hold2 = null;

            Server svr = (Server)me.OwnerItem.Tag;
            int sv = int.Parse(svr.version.Split('.')[0]);

            if (sv >= 10) items = Resource.ddlCreateEvents2008.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            else items = Resource.ddlCreateEvents2005.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            ToolStripMenuItem pi = (ToolStripMenuItem)me.DropDownItems[0];
            hold1 = pi.DropDownItems[0];
            hold2 = pi.DropDownItems[1];
            pi.DropDownItems.Clear();
            pi.DropDownItems.Add(hold1);
            pi.DropDownItems.Add(hold2);
            foreach (string s in items)
            {
                mi = (ToolStripMenuItem)pi.DropDownItems.Add(s);
                mi.Tag = "CREATE_" + s;
                mi.Click += new EventHandler(triggerEvent_Click);
            }

            if (sv >= 10) items = Resource.ddlAlterEvents2008.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            else items = Resource.ddlAlterEvents2005.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            pi = (ToolStripMenuItem)me.DropDownItems[2];
            hold1 = pi.DropDownItems[0];
            hold2 = pi.DropDownItems[1];
            pi.DropDownItems.Clear();
            pi.DropDownItems.Add(hold1);
            pi.DropDownItems.Add(hold2);
            foreach (string s in items)
            {
                mi = (ToolStripMenuItem)pi.DropDownItems.Add(s);
                mi.Tag = "ALTER_" + s;
                mi.Click += new EventHandler(triggerEvent_Click);
            }

            if (sv >= 10) items = Resource.ddlDropEvents2008.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            else items = Resource.ddlDropEvents2005.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            pi = (ToolStripMenuItem)me.DropDownItems[3];
            hold1 = pi.DropDownItems[0];
            hold2 = pi.DropDownItems[1];
            pi.DropDownItems.Clear();
            pi.DropDownItems.Add(hold1);
            pi.DropDownItems.Add(hold2);
            foreach (string s in items)
            {
                mi = (ToolStripMenuItem)pi.DropDownItems.Add(s);
                mi.Tag = "DROP_" + s;
                mi.Click += new EventHandler(triggerEvent_Click);
            }

            if (sv >= 10) { foreach (ToolStripItem ti in me.DropDownItems) if (ti.Text == "ADD" || ti.Text == "RULE") ti.Visible = true; }
            else { foreach (ToolStripItem ti in me.DropDownItems) if (ti.Text == "ADD" || ti.Text == "RULE") ti.Visible = false; }
        }

        private void populateFileTree(TreeNode root, List<InstalledAssembly> flist, ContextMenuStrip cMenu)
        {
            TreeNode fileNode;
            foreach (InstalledAssembly fm in flist)
            {
                string nodetext = fm.name;
                fileNode = root.Nodes.Add(nodetext, nodetext, 13, 13);
                fileNode.ToolTipText = "Associated File: " + fm.name + ", size: " + fm.bytes.Length.ToString("#,###") + " bytes";
                fileNode.Tag = fm;
                fileNode.ContextMenuStrip = cMenu;
            }
        }

        private void populateFunctionTree(TreeNode root, List<Function> flist, ContextMenuStrip cMenu)
        {
            int index = 0;
            TreeNode FunctionNode;

            foreach (Function fn in flist)
            {
                pBar.PerformStep();
                pBar.ProgressBar.Refresh();
                index = fn.FunctionTypeIconIndex();
                string nodetext = fn.ShortName(false);
                FunctionNode = root.Nodes.Add(nodetext, nodetext, index, index);
                FunctionNode.ToolTipText = fn.tooltiptext();
                FunctionNode.Tag = fn;
                FunctionNode.ContextMenuStrip = cMenu;
                ShowStatus(FunctionNode, fn.status, true, fn.changes_pending);
            }
        }

        private void purgeSQLFiles()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (path == "") return;
            path += @"\TotallySql\AssemblyManager";
            string[] files = Directory.GetFiles(path, "*.sql", SearchOption.TopDirectoryOnly);
            foreach (string file in files) File.Delete(file);
        }

        private Action ReadActionRecursive(XmlNode xn, Action parent)
        {
            if (xn.Name != "Action") return null;
            pBar.PerformStep();
            pBar.ProgressBar.Refresh();
            XmlNode childactions = null;
            Action act = new Action();
            act.action = (ActionType)Enum.Parse(typeof(ActionType), xn.Attributes["ActionType"].Value);
            string TargetType = xn.Attributes["TargetType"].Value;
            foreach (XmlNode sn in xn.ChildNodes)
            {
                switch (sn.Name)
                {
                    case "Oldvalue":
                        switch (sn.Attributes["Type"].Value)
                        {
                            case "Int32": act.oldvalue = int.Parse(sn.InnerText); break;
                            case "String": act.oldvalue = sn.InnerText; break;
                            case "Bool": act.oldvalue = bool.Parse(sn.InnerText); break;
                            case "InstalledAssembly": act.oldvalue = ReadAssembly(sn.FirstChild); break;
                            case "Trigger":
                                act.oldvalue = ReadTrigger(sn.FirstChild);
                                break;
                            case "File": act.oldvalue = ReadFile(sn.FirstChild); break;
                            case "Login": act.oldvalue = ReadLogin(sn.FirstChild); break;
                            default: act.oldvalue = sn.InnerText; break;
                        }
                        break;
                    case "Newvalue":
                        switch (sn.Attributes["Type"].Value)
                        {
                            case "Int32": act.newvalue = int.Parse(sn.InnerText); break;
                            case "String": act.newvalue = sn.InnerText; break;
                            case "Bool": act.newvalue = bool.Parse(sn.InnerText); break;
                            case "InstalledAssembly": act.newvalue = ReadAssembly(sn.FirstChild); break;
                            case "File": act.newvalue = ReadFile(sn.FirstChild); break;
                            case "Function": act.newvalue = ReadFunction(sn.FirstChild); break;
                            case "Trigger":
                                act.newvalue = ReadTrigger(sn.FirstChild);
                                break;
                            case "Login": act.newvalue = ReadLogin(sn.FirstChild); break;
                            default: act.newvalue = sn.InnerText; break;
                        }
                        break;
                    case "Subactions":
                        childactions = sn;
                        break;
                }
            }
            string path = xn.Attributes["Path"].Value;
            TreeNode tm, tn = null;
            string[] keys = path.Split('/');
            TreeNodeCollection tnc = tvServers.Nodes;
            for (int i = 0; i < keys.Length; i++)
            {
                tm = tnc.Find(keys[i], false).Where(p => p.Tag.GetType().Name != "Function" || ((Function)p.Tag).type == TargetType).FirstOrDefault();
                if (tm == null || (tm.Tag.GetType().Name == "InstalledAssembly" && !((InstalledAssembly)tm.Tag).is_assembly && ((InstalledAssembly)tm.Tag).status == installstatus.pending_remove))
                {
                    if (act.action == ActionType.AddAssembly)
                    {
                        if (i < 2) return null;
                        Database db = (Database)tn.Tag;
                        InstalledAssembly am = (InstalledAssembly)act.newvalue;
                        am.database = db;
                        am.status = installstatus.pending_add;
                        act.target = am;
                        break;
                    }
                    if (act.action == ActionType.AddFile)
                    {
                        if (i < 3) return null;
                        InstalledAssembly am = (InstalledAssembly)tn.Tag;
                        Database db = am.database;
                        InstalledAssembly af = (InstalledAssembly)act.newvalue;
                        af.database = db;
                        af.parent = am;
                        af.status = installstatus.pending_add;
                        act.target = af;
                        if (am.subfiles.Exists(p => p.name.Equals(af.name, StringComparison.OrdinalIgnoreCase))) am.subfiles.Remove(am.subfiles.Single(p => p.name.Equals(af.name, StringComparison.OrdinalIgnoreCase)));
                        break;
                    }
                    if (act.action == ActionType.RenameFunction || act.action == ActionType.ChangeFunctionSchema)
                    {
                        if (i < 3) return null;
                        keys[i] = keys[i].Replace((string)act.newvalue, (string)act.oldvalue);
                        tn = tnc.Find(keys[i], false).Where(p => p.Tag.GetType().Name != "Function" || ((Function)p.Tag).type == TargetType).FirstOrDefault();
                        if (tn != null) break;
                        else return null;
                    }
                    if (act.action == ActionType.AddFunction)
                    {
                        if (i < 3) return null;
                        Function fn = (Function)act.newvalue;
                        fn.status = installstatus.not_installed;
                        InstalledAssembly am = (InstalledAssembly)tn.Tag;
                        fn.assembly = am;
                        if (!am.functions.Contains(fn)) am.functions.Add(fn);
                        TreeNode fnNode = GetNodeFor(fn);
                        if (fnNode == null)
                        {
                            fnNode = tn.Nodes.Add(fn.ShortName(false), fn.ShortName(false), fn.FunctionTypeIconIndex(), fn.FunctionTypeIconIndex());
                            fnNode.ToolTipText = fn.tooltiptext();
                            fnNode.ContextMenuStrip = cmFunction;
                            fnNode.Tag = fn;
                        }
                        cmFunction.Tag = fnNode;
                        ShowStatus(fnNode, fn.status, true, fn.changes_pending);
                        tn = fnNode;
                        break;
                    }
                    if (act.action == ActionType.ChangeTriggerTarget)
                    {
                        if (i < 3) return null;
                        Trigger ot = (Trigger)act.oldvalue;
                        Trigger nt = (Trigger)act.newvalue;
                        if (ot.isdatabase && !nt.isdatabase) keys[i] = keys[i].Replace(nt.target_schema + ".", "");
                        else if (!ot.isdatabase && nt.isdatabase) keys[i] = ot.target_schema + "." + keys[i];
                        tm = tnc.Find(keys[i], false).Where(p => p.Tag.GetType().Name != "Function" || ((Function)p.Tag).type == TargetType).FirstOrDefault();
                        if (tm == null) return null;
                        tnc = tm.Nodes;
                        tn = tm;
                        break;
                    }
                    return null;
                }
                else
                {
                    tnc = tm.Nodes;
                    tn = tm;
                }
            }
            act.targetnode = tn;
            if (act.action == ActionType.SetParameterDefault)
            {
                string pname = xn.Attributes["Parameter"].Value;
                act.target = ((Function)tn.Tag).parameters.SingleOrDefault(p => p.name == pname);
            }
            else if (act.action != ActionType.AddAssembly && act.action != ActionType.AddFile) act.target = tn.Tag;
            else if (act.target == null) return null;
            if (act != null) act = ReRegisterAction(act, parent);

            if (childactions != null)
            {
                foreach (XmlNode an in childactions.ChildNodes)
                {
                    Action a = ReadActionRecursive(an, act);
                }
            }
            return act;
        }

        private object ReadLogin(XmlNode xn)
        {
            Login l = new Login();
            l.Name = xn.Attributes["Name"].Value;
            l.key = new AsymmetricKey();
            l.key.Name = xn.Attributes["Key"].Value;
            return l;
        }

        private void ReadActionsFromFile(string file)
        {
            XmlDocument doc = new XmlDocument();
            //            try
            //           {
            lbNoActionsPending.Visible = false;
            doc.Load(file);
            SetPBar(CountActionsInNode(doc.ChildNodes[1]));
            foreach (XmlNode xn in doc.ChildNodes[1].ChildNodes)
            {
                Action act = ReadActionRecursive(xn, null);
                tvActions.Refresh();
            }
            //            }
            //            catch (Exception e)
            //            {
            //                MessageBox.Show(e.Message);
            //            }
        }

        private List<Action> ReadActionsFromString(string input)
        {
            List<Action> alist = new List<Action>();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(input);
            foreach (XmlNode xn in doc.ChildNodes[1].ChildNodes)
            {
                alist.Add(ReadActionRecursive(xn, null));
            }
            return alist;
        }

        private InstalledAssembly ReadAssembly(XmlNode xn)
        {
            InstalledAssembly am;
            Function fn;

            am = new InstalledAssembly();
            am.name = xn.Attributes["Name"].Value;
            am.fullname = xn.Attributes["Fullname"].Value;
            am.version = new Version(xn.Attributes["Version"].Value);
            am.culture = new CultureInfo(xn.Attributes["Culture"].Value);
            am.platform = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), xn.Attributes["Platform"].Value);
            am.create_date = DateTime.Parse(xn.Attributes["Created"].Value);
            am.modify_date = DateTime.Parse(xn.Attributes["Modified"].Value);
            am.is_assembly = true;
            am.permission_set = int.Parse(xn.Attributes["Permission"].Value);
            string pkt = xn.Attributes["Token"].Value;
            am.publicKeyToken = new byte[pkt.Length / 2];
            for (int i = 0; i < pkt.Length; i += 2)
            {
                am.publicKeyToken[i / 2] = byte.Parse(pkt.Substring(i, 2), NumberStyles.HexNumber);
            }
            foreach (XmlNode nf in xn.ChildNodes)
            {
                switch (nf.Name)
                {
                    case "Bytes":
                        int i = int.Parse(nf.Attributes["Size"].Value);
                        XmlNodeReader nr = new XmlNodeReader(nf);
                        nr.IsStartElement();
                        am.bytes = new byte[i];
                        nr.ReadElementContentAsBase64(am.bytes, 0, i);
                        break;
                    case "Functions":
                        foreach (XmlNode nfn in nf.ChildNodes)
                        {
                            fn = ReadFunction(nfn);
                            fn.assembly = am;
                            am.functions.Add(fn);
                        }
                        break;
                    case "Dependents":
                        if (am.dependents == null) am.dependents = new List<InstalledAssembly>();
                        foreach (XmlNode nfn in nf.ChildNodes)
                        {
                            InstalledAssembly da = ReadAssembly(nfn);
                            am.dependents.Add(da);
                        }
                        break;
                    case "References":
                        if (am.references == null) am.references = new List<InstalledAssembly>();
                        foreach (XmlNode nfn in nf.ChildNodes)
                        {
                            InstalledAssembly da = ReadAssembly(nfn);
                            am.references.Add(da);
                        }
                        break;
                    case "Files":
                        foreach (XmlNode nfn in nf.ChildNodes)
                        {
                            InstalledAssembly sf = new InstalledAssembly();
                            sf.name = nfn.Attributes["Name"].Value;
                            sf.is_assembly = false;
                            i = int.Parse(nfn.FirstChild.Attributes["Size"].Value);
                            nr = new XmlNodeReader(nfn.FirstChild);
                            nr.IsStartElement();
                            sf.bytes = new byte[i];
                            i = nr.ReadElementContentAsBase64(sf.bytes, 0, i);
                            am.subfiles.Add(sf);
                            sf.parent = am;
                        }
                        break;
                }
            }
            return am;
        }

        private void ReadConnectionsFromFile(string file)
        {
            using (new HourGlass("Loading connections from " + file))
            {
                XmlDocument doc = new XmlDocument();
                SqlConnection sc = null;

                doc.Load(file);
                foreach (XmlNode xn in doc.ChildNodes[1].ChildNodes)
                {
                    SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder();
                    sb.DataSource = xn.Attributes["Server"].Value;
                    HourGlass.Update("Connecting to server " + sb.DataSource + "...");
                    sb.IntegratedSecurity = bool.Parse(xn.Attributes["IntegratedSecurity"].Value);
                    sb.ConnectTimeout = int.Parse(xn.Attributes["ConnectTimeout"].Value);
                    sb.UserID = xn.Attributes["User"].Value;
                    sb.Password = sb.UserID;
                    if (!connections.Exists(p => p.DataSource == sb.DataSource))
                    {
                        if (!sb.IntegratedSecurity)
                        {
                            if (us.Connections.Exists(p => p.DataSource == sb.DataSource && p.Password != "" && p.Password != null))
                            {
                                sb = us.Connections.First(p => p.DataSource == sb.DataSource);
                                us.UnObscure(sb);
                                sc = new SqlConnection(sb.ToString());
                                us.Obscure(sb);
                            }
                            else
                            {
                                connectform.cbServerName.SelectedValue = sb.DataSource;
                                connectform.cbServerName.Enabled = false;
                                connectform.cbAuthentication.SelectedIndex = 1;
                                connectform.cbAuthentication.Enabled = false;
                                us.UnObscure(sb);
                                connectform.cbUserName.Text = sb.UserID;
                                us.Obscure(sb);
                                connectform.Text = "Please enter password for server " + sb.DataSource;
                                connectform.ActiveControl = connectform.tbPassword;
                                HourGlass.Enabled = false;
                                connectform.ShowDialog(this);
                                HourGlass.Enabled = true;
                                connectform.cbServerName.Enabled = true;
                                connectform.cbAuthentication.Enabled = true;
                                connectform.Text = "Connect To SQL Server";
                                if (connectform.Tag != null)
                                {
                                    object[] o = (object[])connectform.Tag;
                                    sb = (SqlConnectionStringBuilder)o[1];
                                    sc = new SqlConnection(sb.ToString());
                                    if (!sb.PersistSecurityInfo) sb.Password = "";
                                    us.Obscure(sb);
                                    if (!us.Connections.Exists(p => p.DataSource == sb.DataSource)) us.Connections.Add(sb);
                                    else
                                    {
                                        int i = us.Connections.FindIndex(p => p.DataSource == sb.DataSource);
                                        us.Connections[i].Clear();
                                        us.Connections[i] = sb;
                                    }
                                }
                                else continue;
                            }
                        }
                        else sc = new SqlConnection(sb.ToString());
                        connections.Add(sc);
                        AddServerToTree(sc, sb);
                        tvServers.Refresh();
                    }
                    else
                    {
                        string root = Resource.mbxServerAlreadyConnected.Replace("%SERVER%", sb.DataSource);
                        MessageBox.Show(root, "Server Already Connected");
                    }
                }
            }
        }

        private void ReadDefaultSchemas()
        {
            Database.defaultschemas.Clear();
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (path == "") return;
            path += @"\TotallySql\AssemblyManager\Settings.xml";
            if (!File.Exists(path)) return;

            XmlDocument xd = new XmlDocument();
            xd.Load(path);
            foreach (XmlNode xn in xd["Schemas"].ChildNodes)
            {
                Database.defaultschemas.Add(xn.Attributes["FQN"].Value, xn.Attributes["schema"].Value);
            }
        }

        private InstalledAssembly ReadFile(XmlNode xn)
        {
            InstalledAssembly am;

            am = new InstalledAssembly();
            am.name = xn.Attributes["Name"].Value;
            am.is_assembly = false;
            foreach (XmlNode nf in xn.ChildNodes)
            {
                switch (nf.Name)
                {
                    case "Bytes":
                        int i = int.Parse(nf.Attributes["Size"].Value);
                        XmlNodeReader nr = new XmlNodeReader(nf);
                        nr.IsStartElement();
                        am.bytes = new byte[i];
                        nr.ReadElementContentAsBase64(am.bytes, 0, i);
                        break;
                }
            }
            return am;
        }

        private Function ReadFunction(XmlNode nfn)
        {
            Function fn = new Function();
            fn.name = nfn.Attributes["Name"].Value;
            fn.schema = nfn.Attributes["Schema"].Value;
            fn.assembly_class = nfn.Attributes["Class"].Value;
            fn.assembly_method = nfn.Attributes["Method"].Value;
            fn.status = (installstatus)Enum.Parse(typeof(installstatus), nfn.Attributes["Status"].Value);
            fn.type = nfn.Attributes["Type"].Value;
            if (fn.type != "TA" && fn.type != "UDT")
            {
                foreach (XmlNode np in nfn.FirstChild.ChildNodes)
                {
                    Parameter p = new Parameter();
                    p.name = np.Attributes["Name"].Value;
                    p.type = np.Attributes["Type"].Value;
                    p.max_length = int.Parse(np.Attributes["Size"].Value);
                    p.position = int.Parse(np.Attributes["Position"].Value);
                    p.default_value = np.Attributes["Default"].Value;
                    p.function = fn;
                    fn.parameters.Add(p);
                }
            }
            if (fn.type == "TA") fn.trigger = ReadTrigger(nfn.FirstChild);
            return fn;
        }

        private Library ReadLibraryFrom(string file)
        {
            Library lib = new Library();

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(file);
                XmlElement root = doc.DocumentElement;
                lib.name = root.GetAttribute("Name");
                lib.file = file;
                foreach (XmlNode xn in root.FirstChild.ChildNodes)
                {
                    InstalledAssembly am = ReadAssembly(xn);
                    lib.assemblies.Add(am);
                }
                foreach (InstalledAssembly am in lib.assemblies)
                {
                    if (am.dependents != null && am.dependents.Count > 0)
                    {
                        for (int i = 0; i < am.dependents.Count; i++)
                        {
                            InstalledAssembly la = lib.assemblies.Single(p => p.name == am.dependents[i].name);
                            am.dependents[i] = la;
                            if (la.references == null) la.references = new List<InstalledAssembly>();
                            if (!la.references.Contains(am))
                            {
                                InstalledAssembly ra = la.references.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                                if (ra != null) la.references.Remove(ra);
                                la.references.Add(am);
                            }
                        }
                    }
                    if (am.references != null && am.references.Count > 0)
                    {
                        for (int i = 0; i < am.references.Count; i++)
                        {
                            InstalledAssembly ra = lib.assemblies.Single(p => p.name == am.references[i].name);
                            am.references[i] = ra;
                            if (ra.dependents == null) ra.dependents = new List<InstalledAssembly>();
                            if (!ra.dependents.Contains(am))
                            {
                                InstalledAssembly da = ra.dependents.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                                if (da != null) ra.dependents.Remove(da);
                                da.dependents.Add(am);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            return lib;
        }

        private Trigger ReadTrigger(XmlNode tn)
        {
            Trigger t = new Trigger();
            t.disabled = bool.Parse(tn.Attributes["Disabled"].Value);
            t.insteadof = bool.Parse(tn.Attributes["InsteadOf"].Value);
            t.isdatabase = bool.Parse(tn.Attributes["Database"].Value);
            t.target = tn.Attributes["Target"].Value;
            t.target_schema = tn.Attributes["Schema"].Value;
            t.target_type = tn.Attributes["Type"].Value;
            foreach (XmlNode en in tn.FirstChild.ChildNodes) t.events.Add(en.InnerText);
            return t;
        }

        private void RefreshAllServers()
        {
            //if (tvActions.Nodes.Count > 0)
            //{
            //    DialogResult dr = MessageBox.Show("Cancel all pending actions and re-load databases from server(s) ?", "Refresh Server Explorer", MessageBoxButtons.YesNo);
            //    if (dr == DialogResult.No) return;
            //}
            using (new HourGlass("Refreshing Explorer Objects From Server(s)..."))
            {
                SetPBar(TotalFunctionCount((Server)null));

                string actionXML = SaveActionsToString();
                tvActions.Nodes.Clear();
                List<Server> servlist = new List<Server>();
                foreach (TreeNode sn in tvServers.Nodes) servlist.Add((Server)sn.Tag);
                tvServers.Nodes.Clear();
                foreach (Server sv in servlist)
                {
                    SqlConnection conn = sv.connection;
                    SqlConnectionStringBuilder builder = sv.connector;
                    AddServerToTree(conn, builder);
                }
                servlist.Clear();
                servlist = null;
                ReadActionsFromString(actionXML);
            }
        }

        private void RefreshDatabase(TreeNode dbNode)
        {
            Database db = (Database)dbNode.Tag;
            using (new HourGlass("Refreshing Database " + db.name + "..."))
            {
                string actionXML = SaveActionsToString();
                CancelAllActions(false);
                TreeNode sNode = dbNode.Parent;
                dbNode.Remove();
                db.assemblies.Clear();
                db.actions.Clear();
                db.changes_pending = false;
                db.permissions.Clear();
                db.GetPermissions();
                db.ReLoad(externalassemblies);
                AddDatabaseToTree(sNode, db);
                ReadActionsFromString(actionXML);
            }
        }

        private void RefreshServer(TreeNode sNode)
        {
            Server sv = (Server)sNode.Tag;
            using (new HourGlass("Refreshing Server " + sv.name + "..."))
            {
                string actionXML = SaveActionsToString();
                CancelAllActions(false);
                sNode.Remove();
                SqlConnection conn = sv.connection;
                SqlConnectionStringBuilder builder = sv.connector;
                AddServerToTree(conn, builder);
                ReadActionsFromString(actionXML);
                pBar.Visible = false;
            }
        }

        public void RegisterAction(Action act, Action parent)
        {
            string root = "";
            string cmd = "";
            Server sv = null;
            Database db = null;
            InstalledAssembly am = null;
            InstalledAssembly af = null;
            Function fn = null;
            Parameter p = null;
            string oldvalue = "";
            string newvalue = "";
            int index = 0;
            List<Action> relatedactions = new List<Action>();
            if (act.targetnode != null) index = act.targetnode.ImageIndex;

            switch (act.action)
            {
                case ActionType.AddAssembly:
                    root = Resource.actnAddAssembly;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "Create");
                    am.actions.Add(act);
                    break;
                case ActionType.AddKeyAndLogin:
                    root = Resource.actnAddKeyAndLogin;
                    am = (InstalledAssembly)act.target;
                    root = root.Replace("%KEY%", am.key.Name);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x"+BitConverter.ToString(am.publicKeyToken).Replace("-",""));
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "AddKeyAndLogin");
                    am.actions.Add(act);
                    index = 18;
                    break;
                case ActionType.AddPermission:
                    root = Resource.actnAddPermission;
                    am = (InstalledAssembly)act.target;
                    root = root.Replace("%PERMISSION%", PermissionSets[(int)act.newvalue-1]);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x" + BitConverter.ToString(am.publicKeyToken).Replace("-", ""));
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "AddPermission");
                    am.actions.Add(act);
                    index = 18;
                    break;
                case ActionType.DropPermission:
                    root = Resource.actnDropPermission;
                    am = (InstalledAssembly)act.target;
                    root = root.Replace("%PERMISSION%", PermissionSets[(int)act.newvalue-1]);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x" + BitConverter.ToString(am.publicKeyToken).Replace("-", ""));
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "DropPermission");
                    am.actions.Add(act);
                    index = 19;
                    break;
                case ActionType.DropKeyAndLogin:
                    root = Resource.actnDropKeyAndLogin;
                    am = (InstalledAssembly)act.target;
                    root = root.Replace("%KEY%", am.key.Name);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x"+BitConverter.ToString(am.publicKeyToken).Replace("-",""));
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "DropKeyAndLogin");
                    am.actions.Add(act);
                    index = 19;
                    break;
                case ActionType.SwapAssembly:
                    root = Resource.actnSwapAssembly;
                    am = (InstalledAssembly)act.newvalue;
                    cmd = GetScriptFor(am, "Swap");
                    InstalledAssembly amOld = (InstalledAssembly)act.oldvalue;
                    oldvalue = amOld.version.ToString(4);
                    newvalue = am.version.ToString(4);
                    db = amOld.database;
                    if (db != null) sv = db.server;
                    am.actions.Add(act);
                    amOld.actions.Add(act);
                    break;
                case ActionType.AddAllObjects:
                    root = Resource.actnAddAssemblyObjects;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    am.actions.Add(act);
                    foreach (Function f in am.functions)
                    {
                        Action a = f.actions.SingleOrDefault(r => r.action == ActionType.AddFunction);
                        if (a != null)
                        {
                            a.parent = act;
                            act.subactions.Add(a);
                        }
                        a = f.actions.SingleOrDefault(r => r.action == ActionType.DropFunction);
                        if (a != null) ReverseAction(a);
                        relatedactions.AddRange(f.actions.Where(r => r.action != ActionType.AddFunction));
                    }
                    break;
                case ActionType.AddFunction:
                    root = Resource.actnAddFunction;
                    fn = (Function)act.target;
                    am = fn.assembly;
                    cmd = GetScriptFor(fn, "Create");
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    fn.actions.Add(act);
                    break;
                case ActionType.DropAllObjects:
                    root = Resource.actnDropAssemblyObjects;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    am.actions.Add(act);
                    foreach (Function f in am.functions)
                    {
                        Action a = f.actions.SingleOrDefault(r => r.action == ActionType.DropFunction);
                        if (a != null)
                        {
                            a.parent = act;
                            act.subactions.Add(a);
                        }
                        a = f.actions.SingleOrDefault(r => r.action == ActionType.AddFunction);
                        if (a != null) ReverseAction(a);
                        relatedactions.AddRange(f.actions.Where(r => r.action != ActionType.DropFunction));
                    }
                    break;
                case ActionType.DropAssembly:
                    root = Resource.actnDropAssembly;
                    am = (InstalledAssembly)act.target;
                    cmd = GetScriptFor(am, "Drop");
                    db = am.database;
                    if (db != null) sv = db.server;
                    am.actions.Add(act);
                    relatedactions.AddRange(am.actions.Where(a => a.action != ActionType.DropAssembly));
                    foreach (Function f in am.functions)
                    {
                        Action a = f.actions.SingleOrDefault(r => r.action == ActionType.DropFunction);
                        if (a != null)
                        {
                            a.parent = act;
                            act.subactions.Add(a);
                        }
                        a = f.actions.SingleOrDefault(r => r.action == ActionType.AddFunction);
                        if (a != null) ReverseAction(a);
                        relatedactions.AddRange(f.actions.Where(r => r.action != ActionType.DropFunction));
                    }
                    List<InstalledAssembly> sfl = new List<InstalledAssembly>(am.subfiles);
                    foreach (InstalledAssembly sf in sfl)
                    {
                        Action a = sf.actions.SingleOrDefault(r => r.action == ActionType.AddFile);
                        if (a != null) ReverseAction(a);
                    }
                    break;
                case ActionType.DropAllAssemblies:
                    root = Resource.actnDropAllAssemblies;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    db.actions.Add(act);
                    foreach (InstalledAssembly iam in db.assemblies)
                    {
                        relatedactions.AddRange(iam.actions.Where(a => a.action != ActionType.DropAssembly));
                        foreach (Function f in am.functions)
                        {
                            Action a = f.actions.SingleOrDefault(r => r.action == ActionType.DropFunction);
                            if (a != null)
                            {
                                a.parent = act;
                                act.subactions.Add(a);
                            }
                            a = f.actions.SingleOrDefault(r => r.action == ActionType.AddFunction);
                            if (a != null) ReverseAction(a);
                            relatedactions.AddRange(f.actions.Where(r => r.action != ActionType.DropFunction));
                        }
                    }
                    break;
                case ActionType.DropFunction:
                    root = Resource.actnDropFunction;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop");
                    am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    fn.actions.Add(act);
                    if (!fn.actions.Exists(x => x.action == ActionType.AddFunction)) relatedactions.AddRange(fn.actions.Where(a => a.action != ActionType.DropFunction));
                    break;
                case ActionType.DropFile:
                    root = Resource.actnDropFile;
                    af = (InstalledAssembly)act.target;
                    am = (InstalledAssembly)act.targetnode.Parent.Tag;
                    cmd = Resource.sqlDropFile.Replace("%ASSEMBLY%", am.name);
                    cmd = cmd.Replace("%FILE%", af.name);
                    oldvalue = af.name;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    af.actions.Add(act);
                    break;
                case ActionType.AddFile:
                    root = Resource.actnAddFile;
                    af = (InstalledAssembly)act.target;
                    am = (InstalledAssembly)act.targetnode.Parent.Tag;
                    cmd = Resource.sqlAddFile.Replace("%ASSEMBLY%", am.name);
                    cmd = cmd.Replace("%FILE%", af.name);
                    string bytestring = "0x" + String.Concat(Array.ConvertAll(af.bytes, x => x.ToString("X2")));
                    cmd = cmd.Replace("%ASSEMBLYBYTES%", bytestring);
                    newvalue = af.name;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    af.actions.Add(act);
                    break;
                case ActionType.ChangePermissionSet:
                    root = Resource.actnChangePermissionSet;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    oldvalue = PermissionSets[am.permission_set - 1];
                    newvalue = PermissionSets[(int)act.newvalue - 1];
                    am.permission_set = (int)act.newvalue;
                    cmd = GetScriptFor(am, "ChangePermissionSet");
                    am.permission_set = (int)act.oldvalue;
                    am.actions.Add(act);
                    break;
                case ActionType.ChangeDatabaseDefaultSchema:
                    root = Resource.actnChangeDatabaseDefaultSchema;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    oldvalue = db.default_schema;
                    newvalue = (string)act.newvalue;
                    db.actions.Add(act);
                    break;
                case ActionType.ChangeFunctionSchema:
                    root = Resource.actnChangeFunctionSchema;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop") + "--GO--";
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    oldvalue = fn.schema;
                    newvalue = (string)act.newvalue;
                    fn.schema = newvalue;
                    cmd += GetScriptFor(fn, "Create");
                    fn.schema = oldvalue;
                    fn.actions.Add(act);
                    break;
                case ActionType.RenameFunction:
                    root = Resource.actnRenameFunction;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop") + "--GO--";
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    oldvalue = fn.name;
                    newvalue = (string)act.newvalue;
                    fn.name = newvalue;
                    cmd += GetScriptFor(fn, "Create");
                    fn.name = oldvalue;
                    fn.actions.Add(act);
                    break;
                case ActionType.SetParameterDefault:
                    root = Resource.actnChangeParameterDefault;
                    p = (Parameter)act.target;
                    oldvalue = (string)act.oldvalue;
                    newvalue = (string)act.newvalue;
                    fn = p.function;
                    p.default_value = newvalue;
                    cmd = GetScriptFor(fn, "Alter");
                    p.default_value = oldvalue;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    fn.actions.Add(act);
                    index = 6;
                    break;
                case ActionType.ToggleTrustworthy:
                    root = Resource.actnChangeTrustworthySetting;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    oldvalue = (bool)act.oldvalue ? "ON" : "OFF";
                    newvalue = (bool)act.newvalue ? "ON" : "OFF";
                    cmd = Resource.sqlSetTrustworthy;
                    cmd = cmd.Replace("%DATABASE%", db.name);
                    cmd = cmd.Replace("%NEWVALUE%", newvalue);
                    db.actions.Add(act);
                    break;
                case ActionType.ToggleCLR:
                    root = Resource.actnChangeCLREnabledSetting;
                    sv = (Server)act.target;
                    oldvalue = (bool)act.oldvalue ? "1" : "0";
                    newvalue = (bool)act.newvalue ? "1" : "0";
                    cmd = Resource.sqlSetClrEnabled.Replace("%NEWVALUE%", newvalue);
                    sv.actions.Add(act);
                    break;
                case ActionType.ChangeTriggerEvents:
                    root = Resource.actnChangeTriggerEvents;
                    fn = (Function)act.target;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    newvalue = fn.trigger.insteadof ? "INSTEAD OF " : "AFTER ";
                    newvalue += String.Join(", ", fn.trigger.events.ToArray());
                    string target;
                    if (fn.trigger.isdatabase) target = "DATABASE";
                    else if (fn.trigger.target_type == "V ") target = "view " + fn.ShortName(true);
                    else target = "table " + fn.ShortName(true);
                    root = root.Replace("%TARGET%", target);
                    fn.actions.Add(act);
                    cmd = GetScriptFor(fn, "Change Events");
                    break;
                case ActionType.ChangeTriggerTarget:
                    root = Resource.actnChangeTriggerTarget;
                    fn = (Function)act.target;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    if (fn.trigger.isdatabase) target = "DATABASE";
                    else if (fn.trigger.target_type == "V ") target = "view [" + fn.trigger.target_schema + "].[" + fn.trigger.target + "]";
                    else target = "table [" + fn.trigger.target_schema + "].[" + fn.trigger.target + "]";
                    Trigger t1 = (Trigger)act.oldvalue;
                    Trigger t2 = (Trigger)act.newvalue;
                    fn.schema = t1.target_schema;
                    root = root.Replace("%FUNCTION%", fn.ShortName(true));
                    fn.schema = t2.target_schema;
                    root = root.Replace("%NEWVALUE%", target);
                    fn.actions.Add(act);
                    cmd = GetScriptFor(fn, "Change Target");
                    break;
            }
            act.path = act.targetnode.FullPath;
            if (p != null) root = root.Replace("%PARAMETER%", p.name);
            if (sv != null) root = root.Replace("%SERVER%", sv.name);
            if (db != null) root = root.Replace("%DATABASE%", db.name);
            if (am != null) root = root.Replace("%ASSEMBLY%", am.name);
            if (fn != null) root = root.Replace("%FUNCTION%", fn.ShortName(true));
            if (fn != null) root = root.Replace("%TYPE%", fn.ShortFunctionTypeName());
            root = root.Replace("%OLDVALUE%", oldvalue);
            root = root.Replace("%NEWVALUE%", newvalue);
            act.sqlcommand = cmd;
            if (parent == null)
            {
                act.actionnode = tvActions.Nodes.Add(root, root, index, index);
                //                act.actionnode.StateImageIndex = stateindex;
                act.parent = null;
            }
            else
            {
                act.actionnode = parent.actionnode.Nodes.Add(root, root, index, index);
                //                act.actionnode.StateImageIndex = stateindex;
                act.parent = parent;
                parent.subactions.Add(act);
            }
            act.displaytext = root;
            act.actionnode.Tag = act;
            act.actionnode.ContextMenuStrip = cmAction;
            foreach (Action a in act.subactions)
                if (a.actionnode.Parent != act.actionnode)
                {
                    a.actionnode.Remove();
                    act.actionnode.Nodes.Add(a.actionnode);
                }

            //if (relatedactions.Count > 0)
            //{
            //    DialogResult dr = MessageBox.Show("Cancel related actions?", "Related Actions Found", MessageBoxButtons.YesNo);
            //    if (dr == DialogResult.No) return;
            //}
            //foreach (Action a in relatedactions) ReverseAction(a);
            toggleActionControls();
        }

        public bool RelinkAssemblyReferences(InstalledAssembly am)
        {
            bool success = true;

            if (am.dependents != null && am.dependents.Count > 0)
            {
                for (int i = 0; i < am.dependents.Count; i++)
                {
                    InstalledAssembly la = am.database.assemblies.SingleOrDefault(p => p.name == am.dependents[i].name);
                    am.dependents[i] = la;      //Note order of this statement vs. next one compared with references
                    if (la == null) continue;
                    if (la.references == null) la.references = new List<InstalledAssembly>();
                    if (!la.references.Contains(am))
                    {
                        InstalledAssembly ra = la.references.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                        if (ra != null) la.references.Remove(ra);
                        la.references.Add(am);
                    }
                }
                am.dependents.Remove(null);     // Different treatment from references: missing dependents can just be ignored.
            }
            if (am.references != null && am.references.Count > 0)
            {
                for (int i = 0; i < am.references.Count; i++)
                {
                    InstalledAssembly ra = am.database.assemblies.SingleOrDefault(p => p.name == am.references[i].name && p.status != installstatus.pending_remove);
                    if (ra == null)
                    {
                        success = false;
                        continue;   // Referenced assembly is missing - will pick it up in calling function
                    }
                    am.references[i] = ra;
                    if (ra.dependents == null) ra.dependents = new List<InstalledAssembly>();
                    if (!ra.dependents.Contains(am))
                    {
                        InstalledAssembly da = ra.dependents.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                        if (da != null) ra.dependents.Remove(da);
                        ra.dependents.Add(am);
                    }
                }
            }
            return success;
        }

        public bool RelinkLibraryAssemblyReferences(InstalledAssembly am, Library lib)
        {
            bool success = true;

            if (am.dependents != null && am.dependents.Count > 0)
            {
                for (int i = 0; i < am.dependents.Count; i++)
                {
                    InstalledAssembly la = lib.assemblies.SingleOrDefault(p => p.name == am.dependents[i].name);
                    am.dependents[i] = la;      //Note order of this statement vs. next one compared with references
                    if (la == null) continue;
                    if (la.references == null) la.references = new List<InstalledAssembly>();
                    if (!la.references.Contains(am))
                    {
                        InstalledAssembly ra = la.references.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                        if (ra != null) la.references.Remove(ra);
                        la.references.Add(am);
                    }
                }
                am.dependents.Remove(null);     // Different treatment from references: missing dependents can just be ignored.
            }
            if (am.references != null && am.references.Count > 0)
            {
                for (int i = 0; i < am.references.Count; i++)
                {
                    InstalledAssembly ra = lib.assemblies.SingleOrDefault(p => p.name == am.references[i].name && p.status != installstatus.pending_remove);
                    if (ra == null)
                    {
                        success = false;
                        continue;   // Referenced assembly is missing - will pick it up in calling function
                    }
                    am.references[i] = ra;
                    if (ra.dependents == null) ra.dependents = new List<InstalledAssembly>();
                    if (!ra.dependents.Contains(am))
                    {
                        InstalledAssembly da = ra.dependents.SingleOrDefault(p => p.name.Equals(am.name, StringComparison.OrdinalIgnoreCase));
                        if (da != null) ra.dependents.Remove(da);
                        ra.dependents.Add(am);
                    }
                }
            }
            return success;
        }

        public void RelinkActionAssemblies(TreeNodeCollection tnc)
        {
            foreach (TreeNode tn in tnc)
            {
                Action a = (Action)tn.Tag;
                if (a.target.GetType().Name == "InstalledAssembly") RelinkAssemblyReferences((InstalledAssembly)a.target);
                if (a.newvalue != null && a.newvalue.GetType().Name == "InstalledAssembly") RelinkAssemblyReferences((InstalledAssembly)a.newvalue);
                RelinkActionAssemblies(tn.Nodes);
            }
        }

        private Action RemoveFileFromAssembly(TreeNode fNode, Action parent, bool ReverseIfAdded)
        {
            Action act = null;
            InstalledAssembly af = (InstalledAssembly)fNode.Tag;
            InstalledAssembly am = (InstalledAssembly)fNode.Parent.Tag;
            if (ReverseIfAdded && af.actions.Exists(p => p.action == ActionType.AddFile)) ReverseAction(af.actions.Single(p => p.action == ActionType.AddFile));
            else
            {
                act = new Action();
                act.action = ActionType.DropFile;
                act.target = af;
                act.targetnode = fNode;
                RegisterAction(act, parent);
                af.status = installstatus.pending_remove;
                ShowStatus(fNode, af.status, false, false);
            }
            return act;
        }

        private static void RemoveFileFromAssemblyInLibrary(TreeNode fnode)
        {
            InstalledAssembly file = (InstalledAssembly)fnode.Tag;
            TreeNode amNode = fnode.Parent;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            am.subfiles.Remove(file);
            //file.parent = null;
            fnode.Remove();
        }

        private Action RenameFunction(TreeNode fnNode, string name)
        {
            Action a = null;
            Function fn = (Function)fnNode.Tag;
            Action parent = null;

            if (fn.status == installstatus.pending_add)
            {
                parent = fn.actions.SingleOrDefault(p => p.action == ActionType.AddFunction);
                if (parent != null)
                {
                    DeregisterAction(parent);
                    fn.name = name;
                    RegisterAction(parent, parent.parent);
                }
            }
            else if (fn.actions.Exists(p => p.action == ActionType.RenameFunction))
            {
                a = fn.actions.Single(p => p.action == ActionType.RenameFunction);
                if (fn.type == "UDT")
                {
                    foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                    {
                        foreach (Function f in am.functions)
                        {
                            foreach (Parameter p in f.parameters)
                            {
                                TreeNode n = null;
                                if (p.type == fn.name || p.type == fn.ShortName(false))
                                {
                                    if (n == null) n = GetNodeFor(f);
                                    if (f.schema != fn.schema) p.type = fn.schema + "." + name;
                                    else p.type = name;
                                    n.ToolTipText = f.tooltiptext();
                                }
                            }
                        }
                    }
                }
                DeregisterAction(a);
                if ((string)a.oldvalue != name)
                {
                    a.newvalue = name;
                    fn.name = (string)a.oldvalue;
                    RegisterAction(a, null);
                }
                else a = null;
                fn.name = name;
                if (fn.actions.Count == 0) fn.changes_pending = false;
            }
            else
            {
                a = new Action();
                a.action = ActionType.RenameFunction;
                a.target = fn;
                a.targetnode = fnNode;
                a.oldvalue = fn.name;
                a.newvalue = name;
                RegisterAction(a, null);

                if (fn.type == "UDT")
                {
                    foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                    {
                        foreach (Function f in am.functions)
                        {
                            foreach (Parameter p in f.parameters)
                            {
                                TreeNode n = null;
                                if (p.type == fn.name || p.type == fn.ShortName(false))
                                {
                                    if (n == null) n = GetNodeFor(f);
                                    if (f.status == installstatus.in_place || f.status == installstatus.pending_add) DropFunction(n, a, false);
                                    if (f.schema != fn.schema) p.type = fn.schema + "." + name;
                                    else p.type = name;
                                    if (f.status == installstatus.pending_remove) AddFunction(n, a, false);
                                    n.ToolTipText = f.tooltiptext();
                                }
                            }
                        }
                    }
                }
                fn.name = (string)a.newvalue;
                fn.changes_pending = true;
            }
            int i = fnNode.Index;
            TreeNode amNode = fnNode.Parent;
            fnNode.Remove();
            TreeNode f2Node = amNode.Nodes.Insert(i, fn.ShortName(false), fn.ShortName(false), fn.FunctionTypeIconIndex(), fn.FunctionTypeIconIndex());
            substituteNodeRecursive(tvActions.Nodes, fnNode, f2Node);
            f2Node.ToolTipText = fn.tooltiptext();
            f2Node.ContextMenuStrip = cmFunction;
            f2Node.Tag = fn;
            cmFunction.Tag = f2Node;
            tvServers.SelectedNode = f2Node;
            ShowStatus(f2Node, fn.status, true, fn.changes_pending);
            return a;
        }

        private void RenameLibrary(TreeNode lyNode, Action parent, string name)
        {
            Library ly = (Library)lyNode.Tag;

            ly.name = name;
            lyNode.Text = ly.name;
            lyNode.ToolTipText = "Library: " + ly.name + " (" + (ly.file == null ? "not saved" : ly.file) + ")";
            ly.changes_pending = true;
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            ShowStatus(lyNode, installstatus.in_place, false, ly.changes_pending);
        }

        private void RenameLibraryFunction(TreeNode fnNode, Action parent, string name)
        {
            Function fn = (Function)fnNode.Tag;

            fn.name = name;
            fnNode.Text = fn.name;
            fnNode.ToolTipText = fn.tooltiptext();
            fn.changes_pending = true;
            tbSaveAllLibraries.Enabled = tbSaveLibrary.Enabled = true;
            ShowStatus(fnNode, fn.status, true, fn.changes_pending);
        }

        private Action ReplaceAssembly(TreeNode amNode, InstalledAssembly amNew, Action parent)
        {
            Action act = null;

            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            act = am.actions.SingleOrDefault(p => p.action == ActionType.SwapAssembly);
            if (act != null)
            {
                ReverseAction(act);
                am = (InstalledAssembly)amNode.Tag;
            }

            amNew.permission_set = am.permission_set;
            act = SwapAssembly(amNode, amNew, parent);

            while (am.functions.Exists(p => p.not_defined && (p.status == installstatus.in_place || p.status == installstatus.pending_add)))
            {
                Function f = am.functions.First(p => p.not_defined && (p.status == installstatus.in_place || p.status == installstatus.pending_add));
                string root = Resource.mbxFunctionNotInNewAssemblyVersionPrompt;
                root = root.Replace("%TYPE%", f.FullFunctionTypeName());
                root = root.Replace("%FUNCTION%", f.name);
                root = root.Replace("%ASSEMBLY%", am.name);
                root = root.Replace("%OLDVALUE%", am.version.ToString(4));
                root = root.Replace("%NEWVALUE%", amNew.version.ToString(4));
                DialogResult dr = MessageBox.Show(root, Resource.mbxFunctionNotInNewAssemblyVersionTitle, MessageBoxButtons.OKCancel);
                if (dr == DialogResult.Cancel)
                {
                    if (act != null) DeregisterAction(act);
                    act = null;
                    break;
                }
                TreeNode fNode = GetNodeFor(f);
                DropFunction(fNode, act, true);
                f.assembly = amNew;
                f.not_defined = false;
            }

            while (amNew.functions.Exists(p => p.signature_changed))
            {
                Function f = amNew.functions.First(p => p.signature_changed);
                string root = Resource.mbxFunctionDiffersInNewAssemblyVersionPrompt;
                root = root.Replace("%TYPE%", f.FullFunctionTypeName());
                root = root.Replace("%FUNCTION%", f.name);
                root = root.Replace("%ASSEMBLY%", am.name);
                root = root.Replace("%OLDVALUE%", am.version.ToString(4));
                root = root.Replace("%NEWVALUE%", amNew.version.ToString(4));
                DialogResult dr = MessageBox.Show(root, Resource.mbxFunctionDiffersInNewAssemblyVersionTitle, MessageBoxButtons.OKCancel);
                if (dr == DialogResult.Cancel)
                {
                    if (act != null) DeregisterAction(act);
                    act = null;
                    break;
                }
                TreeNode fNode = GetNodeFor(f);
                DropFunction(fNode, act, false);
                AddFunction(fNode, act, false);
                f.signature_changed = false;
            }

            TreeNode FunctionNode;
            foreach (Function f in amNew.functions.Where(p => p.needs_installing))
            {
                FunctionNode = GetNodeFor(f);
                if (f.status == installstatus.in_place || f.status == installstatus.pending_add)
                {
                    f.status = installstatus.not_installed;
                    f.actions.Add(AddFunction(FunctionNode, act, true));
                }
                else f.status = installstatus.not_installed;
                f.needs_installing = false;
            }

            List<InstalledAssembly> subfiles = am.subfiles;
            foreach (InstalledAssembly af in subfiles) RemoveFileFromAssembly(GetNodeFor(af), act, false);
            subfiles = amNew.subfiles;
            amNew.subfiles = new List<InstalledAssembly>();
            foreach (InstalledAssembly af in subfiles) AddFileToAssemblyInDatabase(amNode, af, act, false);
            subfiles = am.subfiles.Where(p => !amNew.subfiles.Exists(x => x.name.Equals(p.name, StringComparison.OrdinalIgnoreCase))).ToList<InstalledAssembly>();
            foreach (InstalledAssembly af in subfiles)
            {
                AddFileToAssemblyInDatabase(amNode, af, act, false);
            }

            if (amNew.references != null)
            {
                for (int i = 0; i < amNew.references.Count; i++)
                {
                    InstalledAssembly oldref = am.database.assemblies.FirstOrDefault(p => p.name == amNew.references[i].name);
                    if (oldref == null)
                    {
                        string root = Resource.mbxLoadReferencedAssembly;
                        root = root.Replace("%ASSEMBLY1%", amNew.name);
                        root = root.Replace("%ASSEMBLY2%", amNew.references[i].name);
                        DialogResult dr = MessageBox.Show(root, "Assembly Has New Dependencies", MessageBoxButtons.OKCancel);
                        if (dr == DialogResult.OK)
                        {
                            TreeNode dbNode = GetNodeFor(am.database);
                            AddOrUpdateAssembly(new InstalledAssembly(amNew.references[i]), dbNode, parent, true);
                            InstalledAssembly newrefa = am.database.assemblies.Single(p => p.fullname == amNew.references[i].fullname);
                            amNew.references[i] = newrefa;
                            if (newrefa.dependents == null) newrefa.dependents = new List<InstalledAssembly>();
                            newrefa.dependents.Add(amNew);
                        }
                        else
                        {
                            ReverseAction(act);
                            return null;
                        }
                    }
                }
            }

            tvServers.SelectedNode = amNode;
            return act;
        }

        private Action ReRegisterAction(Action act, Action parent)
        {
            InstalledAssembly am = null;
            Database db = null;
            Server sv = null;
            Function fn = null;
            Parameter pm = null;
            Action newact = null;

            switch (act.action)
            {
                case ActionType.AddAllObjects:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.AddAllObjects);
                    if (newact == null) newact = AddAllObjects(act.targetnode, parent);
                    break;
                case ActionType.AddAssembly:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.AddAssembly);
                    if (newact == null)
                    {
                        newact = AddAssembly((InstalledAssembly)act.newvalue, act.targetnode, parent);
                        foreach (Function f in am.functions) f.status = installstatus.not_installed;
                        populateFunctionTree(newact.targetnode, am.functions, cmFunction);
                    }
                    break;
                case ActionType.AddFile:
                    bool reverseifdropped = !(parent != null && (parent.action == ActionType.SwapAssembly));
                    InstalledAssembly af = (InstalledAssembly)act.target;
                    am = af.parent;
                    newact = af.actions.SingleOrDefault(p => p.action == ActionType.AddFile);
                    if (newact == null) newact = AddFileToAssemblyInDatabase(GetNodeFor(am), af, parent, reverseifdropped);
                    break;
                case ActionType.AddFunction:
                    reverseifdropped = !(parent != null && (parent.action == ActionType.SwapAssembly || parent.action == ActionType.ChangeFunctionSchema || parent.action == ActionType.RenameFunction));
                    fn = (Function)act.target;
                    newact = AddFunction(act.targetnode, parent, reverseifdropped);
                    break;
                case ActionType.ChangeDatabaseDefaultSchema:
                    newact = ChangeDatabaseDefaultSchema(act.targetnode, parent, (string)act.newvalue, false);
                    break;
                case ActionType.ChangeFunctionSchema:
                    newact = ChangeFunctionSchema(act.targetnode, parent, (string)act.newvalue);
                    break;
                case ActionType.ChangePermissionSet:
                    newact = ChangePermissionSet(act.targetnode, parent, (int)act.newvalue);
                    break;
                case ActionType.AddKeyAndLogin:
                    //am = (InstalledAssembly)act.target;
                    //newact = am.actions.SingleOrDefault(p => p.action == ActionType.AddKeyAndLogin);
                    //if (newact == null)
                    //{
                    //    am.login = (Login)act.newvalue;
                    //    am.key = am.login.key;
                    //    newact = AddAsymmetricKeyAndLogin(act.targetnode, parent);
                    //}
                    break;
                case ActionType.DropKeyAndLogin:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.DropKeyAndLogin);
                    if(newact == null && am.login != null) newact = DropAsymmetricKeyAndLogin(act.targetnode, parent);
                    break;
                case ActionType.AddPermission:
                    //am = (InstalledAssembly)act.target;
                    //newact = am.actions.SingleOrDefault(p => p.action == ActionType.AddPermission);
                    //if (newact == null) newact = AddPermissionToLogin(act.targetnode, (int)act.newvalue, parent);
                    break;
                case ActionType.DropPermission:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.DropPermission);
                    if (newact == null && am.login != null && (int)am.login.permission >= (int)act.newvalue) newact = DropPermissionFromLogin(act.targetnode, (int)act.newvalue, parent);
                    break;
                case ActionType.DropAllAssemblies:
                    db = (Database)act.target;
                    newact = db.actions.SingleOrDefault(p => p.action == ActionType.DropAllAssemblies);
                    if (newact == null) newact = DropAllAssemblies(act.targetnode, parent);
                    break;
                case ActionType.DropAllObjects:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.DropAllObjects);
                    if (newact == null) newact = DropAllObjects(act.targetnode, parent);
                    break;
                case ActionType.DropAssembly:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.DropAssembly);
                    if (newact == null) newact = DropAssembly(act.targetnode, parent, true);
                    break;
                case ActionType.DropFile:
                    reverseifdropped = !(parent != null && (parent.action == ActionType.SwapAssembly));
                    af = (InstalledAssembly)act.target;
                    newact = af.actions.SingleOrDefault(p => p.action == ActionType.AddFile);
                    if (newact == null) newact = RemoveFileFromAssembly(act.targetnode, parent, reverseifdropped);
                    break;
                case ActionType.DropFunction:
                    bool reverseifadded = !(parent != null && (parent.action == ActionType.SwapAssembly || parent.action == ActionType.ChangeFunctionSchema || parent.action == ActionType.RenameFunction));
                    fn = (Function)act.target;
                    newact = DropFunction(act.targetnode, parent, reverseifadded);
                    break;
                case ActionType.RenameFunction:
                    newact = RenameFunction(act.targetnode, (string)act.newvalue);
                    break;
                case ActionType.SetParameterDefault:
                    pm = (Parameter)act.target;
                    newact = ChangeParameterDefault(pm, parent, (string)act.newvalue);
                    break;
                case ActionType.SwapAssembly:
                    am = (InstalledAssembly)act.target;
                    newact = am.actions.SingleOrDefault(p => p.action == ActionType.SwapAssembly);
                    if (newact == null) newact = SwapAssembly(act.targetnode, (InstalledAssembly)act.newvalue, parent);
                    break;
                case ActionType.ToggleCLR:
                    sv = (Server)act.target;
                    newact = sv.actions.SingleOrDefault(p => p.action == ActionType.ToggleCLR);
                    if (newact == null) newact = toggleCLREnabled(act.targetnode, parent);
                    break;
                case ActionType.ToggleTrustworthy:
                    db = (Database)act.target;
                    newact = db.actions.SingleOrDefault(p => p.action == ActionType.ToggleTrustworthy);
                    if (newact == null) newact = ToggleTrustworthy(act.targetnode, parent);
                    break;
                case ActionType.ChangeTriggerEvents:
                    newact = act;
                    fn = (Function)act.target;
                    TreeNode fnNode = GetNodeFor(fn);
                    act.targetnode = fnNode;
                    Action a = fn.actions.SingleOrDefault(p => p.action == ActionType.ChangeTriggerEvents);
                    if (a == null)
                    {
                        fn.trigger = (Trigger)act.newvalue;
                        RegisterAction(act, parent);
                    }
                    else
                    {
                        ReverseAction(a);
                        fn.trigger = (Trigger)act.newvalue;
                        Trigger oldTrigger = (Trigger)a.oldvalue;
                        oldTrigger.events.Sort();
                        fn.trigger.events.Sort();
                        if (!fn.trigger.events.SequenceEqual(oldTrigger.events)) RegisterAction(act, parent);
                    }
                    fn.changes_pending = fn.actions.Count > 0;
                    ShowStatus(fnNode, fn.status, true, fn.changes_pending);
                    break;
                case ActionType.ChangeTriggerTarget:
                    fn = (Function)act.target;
                    fnNode = GetNodeFor(fn);
                    Trigger t = (Trigger)act.newvalue;
                    if (t.isdatabase) newact = changeTriggerTarget(fnNode, "DATABASE");
                    else
                    {
                        string target = t.target_type == "V " ? "(view) " : "(table) ";
                        target += t.target_schema + "." + t.target;
                        newact = changeTriggerTarget(fnNode, target);
                    }
                    break;
            }
            toggleActionControls();
            return newact;
        }

        private Action ReRegisterActionRecursive(Action act, Action parent)
        {
            Action a = ReRegisterAction(act, parent);
            foreach (Action b in act.subactions) ReRegisterActionRecursive(b, a);
            return a;
        }

        public void ReScript(Action act)
        {
            string root = "";
            string cmd = "";
            Server sv = null;
            Database db = null;
            InstalledAssembly am = null;
            InstalledAssembly af = null;
            Function fn = null;
            Parameter p = null;
            string oldvalue = "";
            string newvalue = "";

            switch (act.action)
            {
                case ActionType.AddAssembly:
                    root = Resource.actnAddAssembly;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "Create");
                    break;
                case ActionType.SwapAssembly:
                    root = Resource.actnSwapAssembly;
                    am = (InstalledAssembly)act.newvalue;
                    cmd = GetScriptFor(am, "Swap");
                    InstalledAssembly amOld = (InstalledAssembly)act.oldvalue;
                    oldvalue = amOld.version.ToString(4);
                    newvalue = am.version.ToString(4);
                    db = amOld.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.AddFunction:
                    root = Resource.actnAddFunction;
                    fn = (Function)act.target;
                    am = fn.assembly;
                    cmd = GetScriptFor(fn, "Create");
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropAssembly:
                    root = Resource.actnDropAssembly;
                    am = (InstalledAssembly)act.target;
                    cmd = GetScriptFor(am, "Drop");
                    db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropFunction:
                    root = Resource.actnDropFunction;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop");
                    am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.DropFile:
                    root = Resource.actnDropFile;
                    af = (InstalledAssembly)act.target;
                    am = (InstalledAssembly)act.targetnode.Parent.Tag;
                    cmd = Resource.sqlDropFile.Replace("%ASSEMBLY%", am.name);
                    cmd = cmd.Replace("%FILE%", af.name);
                    oldvalue = af.name;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.AddFile:
                    root = Resource.actnAddFile;
                    af = (InstalledAssembly)act.target;
                    am = (InstalledAssembly)act.targetnode.Parent.Tag;
                    cmd = Resource.sqlAddFile.Replace("%ASSEMBLY%", am.name);
                    cmd = cmd.Replace("%FILE%", af.name);
                    string bytestring = "0x" + String.Concat(Array.ConvertAll(af.bytes, x => x.ToString("X2")));
                    cmd = cmd.Replace("%ASSEMBLYBYTES%", bytestring);
                    newvalue = af.name;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.ChangePermissionSet:
                    root = Resource.actnChangePermissionSet;
                    am = (InstalledAssembly)act.target;
                    db = am.database;
                    if (db != null) sv = db.server;
                    oldvalue = PermissionSets[am.permission_set - 1];
                    newvalue = PermissionSets[(int)act.newvalue - 1];
                    am.permission_set = (int)act.newvalue;
                    cmd = GetScriptFor(am, "ChangePermissionSet");
                    break;
                case ActionType.AddKeyAndLogin:
                    root = Resource.actnAddKeyAndLogin;
                    am = (InstalledAssembly)act.target;
                    root = root.Replace("%KEY%", am.key.Name);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x"+BitConverter.ToString(am.publicKeyToken).Replace("-",""));
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "AddKeyAndLogin");
                    break;
                case ActionType.DropKeyAndLogin:
                    root = Resource.actnDropKeyAndLogin;
                    am = (InstalledAssembly)act.target;
                    root = root.Replace("%KEY%", am.key.Name);
                    root = root.Replace("%LOGIN%", am.login.Name);
                    root = root.Replace("%THUMBPRINT%", "0x"+BitConverter.ToString(am.publicKeyToken).Replace("-",""));
                    db = am.database;
                    if (db != null) sv = db.server;
                    cmd = GetScriptFor(am, "AddKeyAndLogin");
                    break;
                case ActionType.ChangeDatabaseDefaultSchema:
                    root = Resource.actnChangeDatabaseDefaultSchema;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    oldvalue = db.default_schema;
                    newvalue = (string)act.newvalue;
                    break;
                case ActionType.ChangeFunctionSchema:
                    root = Resource.actnChangeFunctionSchema;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop") + "--GO--";
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    oldvalue = fn.schema;
                    newvalue = (string)act.newvalue;
                    fn.schema = newvalue;
                    cmd += GetScriptFor(fn, "Create");
                    fn.schema = oldvalue;
                    break;
                case ActionType.RenameFunction:
                    root = Resource.actnRenameFunction;
                    fn = (Function)act.target;
                    cmd = GetScriptFor(fn, "Drop") + "--GO--";
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    oldvalue = fn.name;
                    newvalue = (string)act.newvalue;
                    fn.name = newvalue;
                    cmd += GetScriptFor(fn, "Create");
                    fn.name = oldvalue;
                    break;
                case ActionType.SetParameterDefault:
                    root = Resource.actnChangeParameterDefault;
                    p = (Parameter)act.target;
                    oldvalue = (string)act.oldvalue;
                    newvalue = (string)act.newvalue;
                    fn = p.function;
                    p.default_value = newvalue;
                    cmd = GetScriptFor(fn, "Alter");
                    p.default_value = oldvalue;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case ActionType.ToggleTrustworthy:
                    root = Resource.actnChangeTrustworthySetting;
                    db = (Database)act.target;
                    if (db != null) sv = db.server;
                    oldvalue = (bool)act.oldvalue ? "ON" : "OFF";
                    newvalue = (bool)act.newvalue ? "ON" : "OFF";
                    cmd = Resource.sqlSetTrustworthy;
                    cmd = cmd.Replace("%DATABASE%", db.name);
                    cmd = cmd.Replace("%NEWVALUE%", newvalue);
                    break;
                case ActionType.ToggleCLR:
                    root = Resource.actnChangeCLREnabledSetting;
                    sv = (Server)act.target;
                    oldvalue = (bool)act.oldvalue ? "1" : "0";
                    newvalue = (bool)act.newvalue ? "1" : "0";
                    cmd = Resource.sqlSetClrEnabled.Replace("%NEWVALUE%", newvalue);
                    break;
                case ActionType.ChangeTriggerEvents:
                    root = Resource.actnChangeTriggerEvents;
                    fn = (Function)act.target;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    newvalue = fn.trigger.insteadof ? "INSTEAD OF " : "AFTER ";
                    newvalue += String.Join(", ", fn.trigger.events.ToArray());
                    string target;
                    if (fn.trigger.isdatabase) target = "DATABASE";
                    else if (fn.trigger.target_type == "V ") target = "view " + fn.ShortName(true);
                    else target = "table " + fn.ShortName(true);
                    root = root.Replace("%TARGET%", target);
                    cmd = GetScriptFor(fn, "Change Events");
                    break;
                case ActionType.ChangeTriggerTarget:
                    root = Resource.actnChangeTriggerTarget;
                    fn = (Function)act.target;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    if (fn.trigger.isdatabase) target = "DATABASE";
                    else if (fn.trigger.target_type == "V ") target = "view [" + fn.trigger.target_schema + "].[" + fn.trigger.target + "]";
                    else target = "table [" + fn.trigger.target_schema + "].[" + fn.trigger.target + "]";
                    root = root.Replace("%NEWVALUE%", target);
                    cmd = GetScriptFor(fn, "Change Target");
                    break;
            }
            if (p != null) root = root.Replace("%PARAMETER%", p.name);
            if (sv != null) root = root.Replace("%SERVER%", sv.name);
            if (db != null) root = root.Replace("%DATABASE%", db.name);
            if (am != null) root = root.Replace("%ASSEMBLY%", am.name);
            if (fn != null) root = root.Replace("%FUNCTION%", fn.ShortName(true));
            if (fn != null) root = root.Replace("%TYPE%", fn.ShortFunctionTypeName());
            root = root.Replace("%OLDVALUE%", oldvalue);
            root = root.Replace("%NEWVALUE%", newvalue);
            act.sqlcommand = cmd;
            act.displaytext = root;
            act.actionnode.Text = act.displaytext;
            foreach (Action a in act.subactions) ReScript(a);
        }

        public void ReverseAction(Action act)
        {
            Database db = null;
            InstalledAssembly am = null;
            Function fn = null;
            Parameter p = null;

            pBar.PerformStep();
            pBar.ProgressBar.Refresh();
            List<Action> sublist = new List<Action>(act.subactions);
            for (int i = sublist.Count - 1; i >= 0; i--) ReverseAction(sublist[i]);
            switch (act.action)
            {
                case ActionType.ChangeDatabaseDefaultSchema:
                    db = (Database)act.target;
                    TreeNode node = GetNodeFor(db);
                    ChangeDatabaseDefaultSchema(node, act.parent, (string)act.oldvalue, false);
                    break;
                case ActionType.ToggleTrustworthy:
                    db = (Database)act.target;
                    node = GetNodeFor(db);
                    ToggleTrustworthy(node, act.parent);
                    break;
                case ActionType.ToggleCLR:
                    Server svr = (Server)act.target;
                    node = GetNodeFor(svr);
                    toggleCLREnabled(node, act.parent);
                    break;
                case ActionType.AddAllObjects:
                case ActionType.DropAllObjects:
                    DeregisterAction(act);
                    break;
                case ActionType.AddAssembly:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    DropAssembly(node, act.parent, false);
                    break;
                case ActionType.ChangePermissionSet:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    ChangePermissionSet(node, act.parent, (int)act.oldvalue);
                    break;
                case ActionType.AddKeyAndLogin:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    DropAsymmetricKeyAndLogin(node, act.parent);
                    break;
                case ActionType.DropKeyAndLogin:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    AddAsymmetricKeyAndLogin(node, act.parent);
                    break;
                case ActionType.AddPermission:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    DropPermissionFromLogin(node, (int)act.newvalue, act.parent);
                    break;
                case ActionType.DropPermission:
                    DeregisterAction(act);
                    break;
                case ActionType.DropAssembly:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    UndropAssembly(node, act.parent);
                    break;
                case ActionType.SwapAssembly:
                    InstalledAssembly am1 = (InstalledAssembly)act.oldvalue;
                    InstalledAssembly am2 = (InstalledAssembly)act.newvalue;
                    node = GetNodeFor(am2);
                    SwapAssembly(node, am1, act.parent);
                    if (node != null)
                    {
                        List<TreeNode> nl = (from TreeNode n in node.Nodes where n.Tag.GetType().Name == "Function" && !am1.functions.Contains((Function)n.Tag) select n).ToList<TreeNode>();
                        foreach (TreeNode n in nl) n.Remove();
                        ShowStatus(node, am1.status, false, am1.changes_pending);
                    }
                    break;
                case ActionType.AddFunction:
                    fn = (Function)act.target;
                    node = GetNodeFor(fn);
                    DropFunction(node, act.parent, true);
                    break;
                case ActionType.ChangeFunctionSchema:
                    fn = (Function)act.target;
                    node = GetNodeFor(fn);
                    ChangeFunctionSchema(node, act.parent, (string)act.oldvalue);
                    break;
                case ActionType.DropFunction:
                    fn = (Function)act.target;
                    node = GetNodeFor(fn);
                    AddFunction(node, act.parent, true);
                    break;
                case ActionType.RenameFunction:
                    fn = (Function)act.target;
                    node = GetNodeFor(fn);
                    RenameFunction(node, (string)act.oldvalue);
                    break;
                case ActionType.SetParameterDefault:
                    p = (Parameter)act.target;
                    ChangeParameterDefault(p, act.parent, (string)act.oldvalue);
                    break;
                case ActionType.AddFile:
                    InstalledAssembly af = (InstalledAssembly)act.target;
                    TreeNode afNode = act.targetnode;
                    TreeNode amNode = afNode.Parent;
                    am = (InstalledAssembly)amNode.Tag;
                    am.subfiles.Remove(af);
                    //af.parent = null;
                    afNode.Remove();
                    act.actionnode.Remove();
                    af.actions.Remove(act);
                    break;
                case ActionType.DropFile:
                    af = (InstalledAssembly)act.target;
                    afNode = act.targetnode;
                    amNode = afNode.Parent;
                    am = (InstalledAssembly)amNode.Tag;
                    //                    am.subfiles.Add(af);
                    af.status = installstatus.in_place;
                    ShowStatus(afNode, af.status, false, false);
                    act.actionnode.Remove();
                    af.actions.Remove(act);
                    break;
                case ActionType.ChangeTriggerEvents:
                case ActionType.ChangeTriggerTarget:
                    fn = (Function)act.target;
                    node = act.targetnode;
                    if (fn.trigger.isdatabase && !((Trigger)act.oldvalue).isdatabase) fn.schema = fn.assembly.database.default_schema;
                    else if (!fn.trigger.isdatabase && ((Trigger)act.oldvalue).isdatabase) fn.schema = "";
                    fn.trigger = (Trigger)act.oldvalue;
                    fn.actions.Remove(act);
                    fn.changes_pending = fn.actions.Count > 0;
                    act.actionnode.Remove();
                    node.Text = fn.ShortName(false);
                    node.ToolTipText = fn.tooltiptext();
                    ShowStatus(node, fn.status, true, fn.changes_pending);
                    break;
            }
            if (act.parent != null && act.parent.subactions.Contains(act)) act.parent.subactions.Remove(act);
            toggleActionControls();
        }

        private void RollBack(Action a)
        {
            a.subactions.Clear();
            Action act = InverseOfAction(a, null);
            if (act.action == ActionType.AddAssembly)
            {
                InstalledAssembly am = (InstalledAssembly)act.target;
                TreeNode dbNode = GetNodeFor(am.database);
                act = AddAssembly(am, dbNode, null);
                //                act.targetnode = AddAssemblyNodeToDatabaseNode(am, dbNode);
                RelinkAssemblyReferences(am);
                populateFunctionTree(act.targetnode, am.functions, cmFunction);
                populateFileTree(act.targetnode, am.subfiles, cmFile);
                ExecuteAction(act, false);
            }
            else if (act.action == ActionType.AddFile)
            {
                Database db = null;
                InstalledAssembly af = (InstalledAssembly)act.target;
                TreeNode dbNode = GetNodeFor(af.database);
                if (dbNode != null) db = (Database)dbNode.Tag;
                InstalledAssembly am = db.assemblies.SingleOrDefault(p => p.name == af.parent.name);
                TreeNode amNode = GetNodeFor(am);
                if (amNode != null) act = AddFileToAssemblyInDatabase(amNode, af, null, true);
                if (act != null) ExecuteAction(act, false);
            }
            else if (act.action == ActionType.ChangeFunctionSchema && ((Function)act.target).type == "UDT")
            {
                act.targetnode = GetNodeFor(act.target);
                act.target = act.targetnode.Tag;
                string s = SaveActionToString(act);
                List<Action> alist = ReadActionsFromString(s);

                Function fn = (Function)act.target;
                foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                {
                    foreach (Function f in am.functions)
                    {
                        foreach (Parameter p in f.parameters)
                        {
                            TreeNode n = null;
                            if (p.type == (string)a.newvalue || p.type == fn.schema + "." + (string)a.newvalue)
                            {
                                if (n == null) n = GetNodeFor(f);
                                if (p.type.Contains(".")) p.type = p.type.Substring(p.type.IndexOf(".") + 1);
                                if ((string)a.oldvalue != f.schema) p.type = (string)a.oldvalue + "." + p.type;
                                n.ToolTipText = f.tooltiptext();
                            }
                        }
                    }
                }
                if (alist[0] != null) ExecuteAction(alist[0], false);
            }
            else if (act.action == ActionType.RenameFunction && ((Function)act.target).type == "UDT")
            {
                act.targetnode = GetNodeFor(act.target);
                act.target = act.targetnode.Tag;
                string s = SaveActionToString(act);
                List<Action> alist = ReadActionsFromString(s);

                Function fn = (Function)act.target;
                foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                {
                    foreach (Function f in am.functions)
                    {
                        foreach (Parameter p in f.parameters)
                        {
                            TreeNode n = null;
                            if (p.type == (string)a.newvalue || p.type == fn.schema + "." + (string)a.newvalue)
                            {
                                if (n == null) n = GetNodeFor(f);
                                if (f.schema != fn.schema) p.type = fn.schema + "." + (string)a.oldvalue;
                                p.type = (string)a.oldvalue;
                                n.ToolTipText = f.tooltiptext();
                            }
                        }
                    }
                }
                if (alist[0] != null) ExecuteAction(alist[0], false);
            }
            else
            {
                if (act.action == ActionType.SetParameterDefault) act.targetnode = GetNodeFor(((Parameter)act.target).function);
                else act.targetnode = GetNodeFor(act.target);
                if (act.targetnode == null && act.target is Function && act.action == ActionType.AddFunction)
                {
                    Function fn = act.target as Function;
                    if (fn.assembly.functions.Exists(p => p.name.Equals(fn.name, StringComparison.OrdinalIgnoreCase) && p.type == fn.type && p.schema == fn.schema))
                    {
                        MessageBox.Show(Resource.mbxFunctionWithSameNameAlreadyExistsPrompt, "Cannot complete roll-back");
                        return;
                    }
                    fn.status = installstatus.not_installed;
                    fn.assembly.functions.Add(fn);

                    int index = fn.FunctionTypeIconIndex();
                    string nodetext = fn.ShortName(false);
                    TreeNode amNode = GetNodeFor(fn.assembly);
                    TreeNode f2Node = amNode.Nodes.Add(nodetext, nodetext, index, index);
                    f2Node.ToolTipText = fn.tooltiptext();
                    f2Node.Tag = fn;
                    f2Node.ContextMenuStrip = cmFunction;
                    act.targetnode = f2Node;
                }
                else act.target = act.targetnode.Tag;
                string s = SaveActionToString(act);
                List<Action> alist = ReadActionsFromString(s);
                RelinkActionAssemblies(tvActions.Nodes);
                foreach (Action b in alist) if (b != null) ExecuteAction(b, false);
            }
            if (Stop) return;
            TreeNode anode = a.actionnode;
            anode.Remove();
            TreeNode tn = tvServers.SelectedNode;
            if (tn != null && !SuppressEvents)
            {
                Application.DoEvents();
                tvServers.SelectedNode = null;
                tvServers.SelectedNode = tn;
            }
        }

        private void RollBackAll()
        {
            using (new HourGlass("Rolling Back Actions..."))
            {
                SuppressEvents = true;
                SetPBar(TotalRollBacksCount());
                EnableStop();

                int j = tvHistory.Nodes.Count;

                if ((string)tvHistory.Tag == "EXECUTE")
                {
                    for (int i = j - 1; i >= 0; i--)
                    {
                        if (Stop) break;
                        Action a = (Action)tvHistory.Nodes[i].Tag;
                        RollBack(a);
                    }
                }
                else
                {
                    for (int i = 0; i < j; i++)
                    {
                        if (Stop) break;
                        Action a = (Action)tvHistory.Nodes[0].Tag;
                        RollBack(a);
                    }
                }
                SuppressEvents = false;
                toggleActionControls();
            }
        }

        private void SaveActionsToFile(string file)
        {
            XmlTextWriter tw = null;
            Action act = null;

            tw = new XmlTextWriter(file, null);
            tw.WriteStartDocument();
            tw.WriteStartElement("Actions");
            foreach (TreeNode tn in tvActions.Nodes)
            {
                act = (Action)tn.Tag;
                WriteActionRecursive(tw, act);
            }
            tw.WriteEndElement();
            tw.WriteEndDocument();
            tw.Close();
        }

        private void SaveDefaultSchemas()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (path == "") return;
            path += @"\TotallySql\AssemblyManager";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            path += @"\Settings.xml";

            XmlTextWriter tw = null;

            tw = new XmlTextWriter(path, null);
            tw.WriteStartDocument();
            tw.WriteStartElement("Schemas");
            int i = 0;
            foreach (string key in Database.defaultschemas.Keys)
            {
                tw.WriteStartElement("DB" + i++.ToString());
                tw.WriteAttributeString("FQN", key);
                tw.WriteAttributeString("schema", Database.defaultschemas[key]);
                tw.WriteEndElement();
            }
            tw.WriteEndElement();
            tw.WriteEndDocument();
            tw.Close();
        }

        private void SaveRollBacksToFile(string file)
        {
            XmlTextWriter tw = null;
            Action act = null;

            tw = new XmlTextWriter(file, null);
            tw.WriteStartDocument();
            tw.WriteStartElement("Actions");
            if ((string)tvHistory.Tag == "EXECUTE")
            {
                for (int i = tvHistory.Nodes.Count - 1; i >= 0; i--)
                {
                    act = (Action)tvHistory.Nodes[i].Tag;
                    WriteActionRecursive(tw, InverseOfAction(act, null));
                }
            }
            else
            {
                for (int i = 0; i < tvHistory.Nodes.Count; i++)
                {
                    act = (Action)tvHistory.Nodes[i].Tag;
                    WriteActionRecursive(tw, InverseOfAction(act, null));
                }
            }
            tw.WriteEndElement();
            tw.WriteEndDocument();
            tw.Close();
        }

        private string SaveActionToString(Action act)
        {
            XmlTextWriter tw = null;

            StringWriter sw = new StringWriter();
            tw = new XmlTextWriter(sw);
            tw.WriteStartDocument();
            tw.WriteStartElement("Actions");
            WriteActionRecursive(tw, act);
            tw.WriteEndElement();
            tw.WriteEndDocument();
            tw.Close();
            return sw.ToString();
        }

        private string SaveActionsToString()
        {
            XmlTextWriter tw = null;
            Action act = null;

            StringWriter sw = new StringWriter();
            tw = new XmlTextWriter(sw);
            tw.WriteStartDocument();
            tw.WriteStartElement("Actions");
            foreach (TreeNode tn in tvActions.Nodes)
            {
                act = (Action)tn.Tag;
                WriteActionRecursive(tw, act);
            }
            tw.WriteEndElement();
            tw.WriteEndDocument();
            tw.Close();
            return sw.ToString();
        }

        private void SaveAllActions()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveDialog.InitialDirectory == "") saveDialog.InitialDirectory = path;
            saveDialog.Title = "Save All Actions To File";
            saveDialog.FileName = "*.act";
            saveDialog.Filter = "Action Lists|*.act";
            saveDialog.DefaultExt = "act";
            saveDialog.CheckPathExists = true;
            saveDialog.OverwritePrompt = true;
            saveDialog.ValidateNames = true;
            DialogResult dr = saveDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                SaveActionsToFile(saveDialog.FileName);
            }
        }

        private void SaveAllRollbackActions()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveDialog.InitialDirectory == "") saveDialog.InitialDirectory = path;
            saveDialog.Title = "Save Roll Back For All Completed Actions To File";
            saveDialog.FileName = "*.act";
            saveDialog.Filter = "Action Lists|*.act";
            saveDialog.DefaultExt = "act";
            saveDialog.CheckPathExists = true;
            saveDialog.OverwritePrompt = true;
            saveDialog.ValidateNames = true;
            DialogResult dr = saveDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                SaveRollBacksToFile(saveDialog.FileName);
            }
        }

        private void SaveConnectionSettings()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveDialog.InitialDirectory == "") saveDialog.InitialDirectory = path;
            saveDialog.Title = "Save Connection Settings To File";
            saveDialog.FileName = "*.cxs";
            saveDialog.Filter = "Connection Settings|*.cxs";
            saveDialog.DefaultExt = "cxs";
            saveDialog.CheckPathExists = true;
            saveDialog.OverwritePrompt = true;
            saveDialog.ValidateNames = true;
            DialogResult dr = saveDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                SaveConnectionSettingsToFile(saveDialog.FileName);
            }
        }

        private void SaveConnectionSettingsToFile(string file)
        {
            XmlTextWriter tw = new XmlTextWriter(file, null);
            tw.WriteStartDocument();
            tw.WriteStartElement("Connections");
            foreach (TreeNode tn in tvServers.Nodes)
            {
                Server s = (Server)tn.Tag;
                tw.WriteStartElement("Connection");
                WriteXmlAttribute(tw, "Server", s.connector.DataSource);
                WriteXmlAttribute(tw, "IntegratedSecurity", s.connector.IntegratedSecurity.ToString());
                WriteXmlAttribute(tw, "ConnectTimeout", s.connector.ConnectTimeout.ToString());
                WriteXmlAttribute(tw, "User", s.connector.UserID);
                tw.WriteEndElement();
            }
            tw.WriteEndElement();
            tw.WriteEndDocument();
            tw.Close();
        }

        private void SaveLibrary(TreeNode lNode)
        {
            Library lib = (Library)lNode.Tag;
            if (lib.file == null || lib.file == "") SaveLibraryAs(lNode);
            else
            {
                if (WriteLibraryTo(lNode, lib.file)) ClearLibraryChanges(lNode);
            }
            toggleLibraryControls(lib);
        }

        private void SaveLibraryAs(TreeNode lNode)
        {
            Library lib = (Library)lNode.Tag;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveDialog.InitialDirectory == "") saveDialog.InitialDirectory = path;
            saveDialog.Title = "Save Library \"" + lNode.Text + "\" As File...";
            saveDialog.FileName = "*.alb";
            saveDialog.Filter = "Assembly Libraries|*.alb";
            saveDialog.DefaultExt = "alb";
            saveDialog.CheckPathExists = true;
            saveDialog.OverwritePrompt = true;
            saveDialog.ValidateNames = true;
            DialogResult dr = saveDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                if (WriteLibraryTo(lNode, saveDialog.FileName))
                {
                    ClearLibraryChanges(lNode);
                    lib.file = saveDialog.FileName;
                    lNode.ToolTipText = "Library: " + lib.name + " (" + lib.file + ")";
                }
            }
            toggleLibraryControls(lib);
        }

        private void ScriptAction(TreeNode actionNode, string destination, string direction)
        {
            string script = "";
            Action act = (Action)actionNode.Tag;
            if (direction == "ROLLBACK") act = InverseOfAction(act, null);
            script = GetActionScript(act);
            if (script == "")
            {
                MessageBox.Show(Resource.errNothingToDoOnSql, "No SQL Actions Needed");
                return;
            }
            SendScriptTo(script, destination);
        }

        private void ScriptAllActionsTo(string destination, string direction, TreeNodeCollection tnc)
        {
            string script = "";

            SetPBar(tnc.Count);
            if (direction == "EXECUTE")
            {
                foreach (TreeNode tn in tnc)
                {
                    Action a = (Action)tn.Tag;
                    script += GetActionScript(a);
                    pBar.PerformStep();
                    pBar.ProgressBar.Refresh();
                }
            }
            else
            {
                for (int i = tnc.Count - 1; i >= 0; i--)
                {
                    Action a = (Action)tnc[i].Tag;
                    script += GetActionScript(InverseOfAction(a, null));
                    pBar.PerformStep();
                    pBar.ProgressBar.Refresh();
                }
            }

            SendScriptTo(script, destination);
        }

        private string ScriptAssemblyAsTo(string action, string destination, bool library)
        {
            string script;
            TreeNode assemblyNode;
            if (library) assemblyNode = (TreeNode)cmLibraryAssembly.Tag;
            else assemblyNode = (TreeNode)cmAssembly.Tag;
            InstalledAssembly assembly = (InstalledAssembly)assemblyNode.Tag;

            string server = "";
            if (assembly.database != null) server = assembly.database.server.name;
            else server = "<SERVER NAME>";

            if (assembly.database == null)
            {
                Database db = new Database();
                db.name = "DATABASE_NAME";
                assembly.database = db;
            }

            script = Resource.sqlSetServer.Replace("%SERVER%", server);
            script += GetUseDBScript(assembly.database.name) + "\nGO\n\n";

            if (action == "Create")
            {
                script += GetScriptFor(assembly, action) + "\nGO\n\n";      //Assembly first for create
                foreach (Function fn in assembly.functions.Where(f => f.type == "UDT")) script += GetScriptFor(fn, action) + "\nGO\n\n"; //Types next
                foreach (Function fn in assembly.functions.Where(f => f.type != "UDT")) script += GetScriptFor(fn, action) + "\nGO\n\n"; //Then functions
                foreach (InstalledAssembly af in assembly.subfiles) script += GetScriptFor(af, "Add") + "\nGO\n\n"; //Then finally associated files
            }
            else
            {
                foreach (Function fn in assembly.functions.Where(f => f.type != "UDT")) script += GetScriptFor(fn, action) + "\nGO\n\n"; //Functions first
                foreach (Function fn in assembly.functions.Where(f => f.type == "UDT")) script += GetScriptFor(fn, action) + "\nGO\n\n"; //Then types
                foreach (InstalledAssembly af in assembly.subfiles) script += GetScriptFor(af, "Drop") + "\nGO\n\n"; //Then associated files
                script += GetScriptFor(assembly, action) + "\nGO\n\n";        //Assembly last for drop
            }

            SendScriptTo(script, destination);
            if (assembly.database.server == null) assembly.database = null;
            return script;
        }

        private string ScriptFileAsTo(string action, string destination, bool library)
        {
            string script = "";
            string server = "";
            TreeNode afNode;
            if (library) afNode = (TreeNode)cmLibraryFile.Tag;
            else afNode = (TreeNode)cmFile.Tag;
            InstalledAssembly af = (InstalledAssembly)afNode.Tag;
            if (af.database == null)
            {
                Database db = new Database();
                db.name = "DATABASE_NAME";
                af.database = db;
            }
            else server = af.database.server.name;
            if (server != "") script = Resource.sqlSetServer.Replace("%SERVER%", server);
            script += GetUseDBScript(af.database.name) + "\nGO\n\n" + GetScriptFor(af, action) + "\nGO\n";

            SendScriptTo(script, destination);
            if (af.database.server == null) af.database = null;
            return script;
        }

        private string ScriptFunctionAsTo(string action, string destination, bool library)
        {
            string script = "";
            string server = "";

            TreeNode functionNode;
            if (library) functionNode = (TreeNode)cmLibraryFunction.Tag;
            else functionNode = (TreeNode)cmFunction.Tag;
            Function function = (Function)functionNode.Tag;
            if (function.assembly.database == null)
            {
                Database db = new Database();
                db.name = "DATABASE_NAME";
                function.assembly.database = db;
            }
            else server = function.assembly.database.server.name;

            if (server != "") script = Resource.sqlSetServer.Replace("%SERVER%", server);
            script += GetUseDBScript(function.assembly.database.name) + "\nGO\n\n" + GetScriptFor(function, action) + "\nGO\n";

            SendScriptTo(script, destination);
            if (function.assembly.database.server == null) function.assembly.database = null;
            if (destination == "TAB") ultraTabControl1.SelectedTab.Tag = function;

            return script;
        }

        private void SendScriptTo(string script, string destination)
        {
            switch (destination)
            {
                case "TAB":
                    sendToNewTab(script);
                    break;
                case "SSMS":
                    sendToNewSSMSWindow(script);
                    break;
                case "File":
                    sendToFile(script);
                    break;
                case "Clipboard":
                    sendToClipboard(script);
                    break;
            }
        }

        private void sendToClipboard(string script)
        {
            string[] parts = ServerSort(script).Split(new string[] { ">>>>>> SET SERVER AS " }, StringSplitOptions.RemoveEmptyEntries);
            string sql = "";
            foreach (string part in parts)
            {
                string server = "";
                Match m = Regex.Match(part, @"\[(.*?)\] <<<<<<");
                if (m.Success && m.Captures.Count > 0)
                {
                    server = m.Groups[1].Value;
                    if (parts.Count() > 1) sql += Resource.sqlSetServerWarning.Replace("%SERVER%", server);
                    else sql += Resource.sqlSetServerReminder.Replace("%SERVER%", server);
                    sql += part.Substring(m.Value.Length);
                }
                else sql += part;
            }
            Clipboard.SetText(sql, TextDataFormat.Text);
        }

        struct sortlet : IComparable<sortlet>
        {
            int IComparable<sortlet>.CompareTo(sortlet b)
            {
                if (server == b.server) return order.CompareTo(b.order);
                else return server.CompareTo(b.server);
            }
            public string server;
            public int order;
            public string sql;
        }

        private string ServerSort(string script)
        {
            string[] parts = script.Split(new string[] { ">>>>>> SET SERVER AS " }, StringSplitOptions.RemoveEmptyEntries);
            List<sortlet> sortlist = new List<sortlet>();
            int i = 0;
            string server = "";

            foreach (string part in parts)
            {
                sortlet s = new sortlet();

                Match m = Regex.Match(part, @"\[(.*?)\] <<<<<<");
                if (m.Success && m.Captures.Count > 0)
                {
                    s.server = m.Groups[1].Value;
                    s.sql = part.Substring(m.Value.Length);
                    if (s.server == server) s.order = ++i;
                    else s.order = i = 0;
                    server = s.server;
                }
                else
                {
                    s.server = "none";
                    s.sql = part;
                    if (s.server == server) s.order = ++i;
                    else s.order = i = 0;
                    server = s.server;
                }
                sortlist.Add(s);
            }
            sortlist.Sort();

            string sql = "";
            server = "";
            foreach (sortlet s in sortlist)
            {
                if (server != s.server) sql += Resource.sqlSetServer.Replace("%SERVER%", s.server);
                sql += s.sql;
                server = s.server;
            }
            return sql;
        }

        private void sendToNewSSMSWindow(string script)
        {
            string[] parts = ServerSort(script).Split(new string[] { ">>>>>> SET SERVER AS " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string sql = part;
                string server = "";
                Server svr = null;
                Match m = Regex.Match(part, @"\[(.*?)\] <<<<<<");
                if (m.Success && m.Captures.Count > 0)
                {
                    server = m.Groups[1].Value;
                    sql = part.Substring(m.Value.Length);
                }
                if (server != "" && server != "<SERVER NAME>") foreach (TreeNode tn in tvServers.Nodes) if (((Server)tn.Tag).name == server) svr = (Server)tn.Tag;

                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\TotallySQL\AssemblyManager";
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                path += "\\SQLQuery";
                int i = 0;
                while (File.Exists(path + (++i).ToString() + ".sql")) ;
                path += i.ToString() + ".sql";

                string cmd = "";

                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\sqlwb.sql.9.0");
                if (key != null)
                {
                    cmd = (string)key.OpenSubKey(@"Shell\Open\Command").GetValue("");
                    cmd = cmd.Replace(" /dde", "");
                    cmd += " \"" + path + "\"";
                    if (server != "" && server != "<SERVER NAME>") cmd += " -S \"" + server + "\"";
                    if (svr != null)
                    {
                        if (svr.connector.IntegratedSecurity) cmd += " -E";
                        else
                        {
                            us.UnObscure(svr.connector);
                            cmd += " -U " + svr.connector.UserID;
                            if (svr.connector.Password != "") cmd += " -P " + svr.connector.Password;
                            us.Obscure(svr.connector);
                        }
                    }
                    cmd += " -nosplash";
                    File.WriteAllText(path, sql);
                    Microsoft.VisualBasic.Interaction.Shell(cmd, Microsoft.VisualBasic.AppWinStyle.NormalFocus, false, 0);
                }
                else
                {
                    cmd = "cmd.exe /C " + path;
                    sql = Resource.sqlSetServerReminder.Replace("%SERVER%", server == "" ? "<SERVER NAME>" : server) + sql;
                    File.WriteAllText(path, sql);
                    Microsoft.VisualBasic.Interaction.Shell(cmd, Microsoft.VisualBasic.AppWinStyle.Hide, false, 0);
                }

            }
        }

        private void sendToNewTab(string script)
        {
            string[] parts = ServerSort(script).Split(new string[] { ">>>>>> SET SERVER AS " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                Server svr = null;

                string sql = part;
                string server = "";
                Match m = Regex.Match(part, @"\[(.*?)\] <<<<<<");
                if (m.Success && m.Captures.Count > 0)
                {
                    server = m.Groups[1].Value;
                    sql = part.Substring(m.Value.Length);
                }
                if (server != "" && server != "<SERVER NAME>") foreach (TreeNode tn in tvServers.Nodes) if (((Server)tn.Tag).name == server) svr = (Server)tn.Tag;

                string tabname = "Generated Script (" + scriptTab++.ToString() + ")";
                if (server != "") tabname += " - " + server;
                Infragistics.Win.UltraWinTabControl.UltraTab tp = ultraTabControl1.Tabs.Add(tabname, tabname);
                tp.Appearance.Image = Resource.script;
                RichTextBox tb = new RichTextBox();
                tb.MouseClick += new MouseEventHandler(scriptTBEnter);
                tb.SelectionChanged += new EventHandler(ScriptSelectionChanged);
                tb.KeyDown += new KeyEventHandler(ScriptKeyDown);
                tb.AcceptsTab = true;
                Panel p = new Panel();
                p.BackColor = Color.FromKnownColor(KnownColor.Window);
                tp.TabPage.Controls.Add(p);
                p.Location = new Point(20, 0);
                p.Width = tp.TabPage.Width - 20;
                p.Height = tp.TabPage.Height;
                p.Anchor = (AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom);
                p.Controls.Add(tb);
                p.Padding = new Padding(5, 5, 0, 0);
                tp.Tag = svr;
                tb.Dock = DockStyle.Fill;
                tb.Multiline = true;
                tb.WordWrap = false;
                tb.MaxLength = 0x7FFFFFFF;
                tb.ScrollBars = RichTextBoxScrollBars.ForcedBoth;
                tb.BorderStyle = BorderStyle.None;
                tb.Text = sql.Replace("\r", "");
                tb.Font = fontCourier;
                tb.ContextMenuStrip = cmScript;
                SqlColour(tb);
                ultraTabControl1.SelectedTab = tp;
            }
        }

        void ScriptKeyDown(object sender, KeyEventArgs e)
        {
            RichTextBox rtb = (RichTextBox)sender;
            switch (e.KeyData)
            {
                case (Keys.Control | Keys.A): rtb.SelectAll(); break;
                case Keys.F5: if (tbExecuteScript.Enabled) executeToolStripMenuItem_Click(tbExecuteScript, e); break;
                case (Keys.Control | Keys.S): if (tbSaveScript.Enabled) saveToFileToolStripMenuItem_Click(tbSaveScript, e); break;
                case (Keys.Shift | Keys.F4): if (tbCloseScript.Enabled) closeToolStripMenuItem_Click(tbCloseScript, e); break;
            }
        }

        void ScriptSelectionChanged(object sender, EventArgs e)
        {
            RichTextBox tb = (RichTextBox)sender;
            tbScriptCut.Enabled = tbScriptCopy.Enabled = cutToolStripMenuItem.Enabled = copyToolStripMenuItem.Enabled = tb.SelectionLength > 0;
        }

        private void SqlColour(RichTextBox tb)
        {
            tb.SelectAll();
            tb.SelectionColor = Color.Blue;
            MatchCollection mc = Regex.Matches(tb.Text, @"(\[.*?\])|(\.)|(@\S+)|(0x[0-9A-F]+)|(\([0-9]+\))");
            foreach (Match m in mc)
            {
                tb.Select(m.Index, m.Length);
                tb.SelectionColor = Color.Black;
            }
            mc = Regex.Matches(tb.Text, "(N?'[^']*?')");
            foreach (Match m in mc)
            {
                tb.Select(m.Index, m.Length);
                tb.SelectionColor = Color.Red;
            }
            mc = Regex.Matches(tb.Text, @"(/\*.*?\*/)|(//.*$)", RegexOptions.Multiline);
            foreach (Match m in mc)
            {
                tb.Select(m.Index, m.Length);
                tb.SelectionColor = Color.DarkGreen;
            }
        }

        private void sendToFile(string script)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (saveDialog.InitialDirectory == "") saveDialog.InitialDirectory = path;
            saveDialog.Title = "Save Script As File...";
            saveDialog.FileName = "*.sql";
            saveDialog.Filter = "Sql Files|*.sql";
            saveDialog.DefaultExt = "sql";
            saveDialog.CheckPathExists = true;
            saveDialog.OverwritePrompt = true;
            saveDialog.ValidateNames = true;
            DialogResult dr = saveDialog.ShowDialog();
            if (dr == DialogResult.Cancel) return;
            path = saveDialog.FileName;

            string[] parts = ServerSort(script).Split(new string[] { ">>>>>> SET SERVER AS " }, StringSplitOptions.RemoveEmptyEntries);
            string sql = "";
            foreach (string part in parts)
            {
                string server = "";
                Match m = Regex.Match(part, @"\[(.*?)\] <<<<<<");
                if (m.Success && m.Captures.Count > 0)
                {
                    server = m.Groups[1].Value;
                    if (parts.Count() > 1) sql += Resource.sqlSetServerWarning.Replace("%SERVER%", server);
                    else sql += Resource.sqlSetServerReminder.Replace("%SERVER%", server);
                    sql += part.Substring(m.Value.Length);
                }
                else sql += part;
            }

            File.WriteAllText(path, sql);
        }

        private void SetCheck(ToolStripItemCollection ic, int index)
        {
            changingChecks = true;
            for (int i = 0; i < ic.Count; i++)
            {
                if (ic[i].GetType().Name == "ToolStripMenuItem")
                {
                    ToolStripMenuItem tmi = (ToolStripMenuItem)ic[i];
                    if (i == index) tmi.Checked = true;
                    else tmi.Checked = false;
                }
            }
            changingChecks = false;
        }

        private void SetPBar(int n)
        {
            pBar.Maximum = n;
            pBar.Value = 0;
            pBar.Visible = true;
            pBar.Step = 1;
        }

        public void ShowEffectOfAction(Action act)
        {
            Database db = null;
            InstalledAssembly am = null;
            Function fn = null;
            Parameter p = null;
            TreeNode node;

            switch (act.action)
            {
                case ActionType.ChangeDatabaseDefaultSchema:
                case ActionType.ToggleTrustworthy:
                    db = (Database)act.target;
                    if (!db.actions.Exists(a => a != act)) db.changes_pending = false;
                    node = GetNodeFor(db);
                    if (node != null) ShowStatus(node, installstatus.in_place, false, db.changes_pending);
                    break;
                case ActionType.ToggleCLR:
                    Server svr = (Server)act.target;
                    if (!svr.actions.Exists(a => a != act)) svr.changes_pending = false;
                    node = GetNodeFor(svr);
                    if (node != null) ShowStatus(node, installstatus.in_place, false, svr.changes_pending);
                    break;
                case ActionType.AddAssembly:
                    am = (InstalledAssembly)act.target;
                    am.status = installstatus.in_place;
                    node = GetNodeFor(am);
                    if (node != null) ShowStatus(node, am.status, false, am.changes_pending);
                    break;
                case ActionType.SwapAssembly:
                    am = (InstalledAssembly)act.target;
                    if (!am.actions.Exists(a => a != act)) am.changes_pending = false;
                    node = GetNodeFor(am);
                    if (node != null)
                    {
                        List<TreeNode> nl = (from TreeNode n in node.Nodes where n.Tag.GetType().Name == "Function" && !am.functions.Contains((Function)n.Tag) select n).ToList<TreeNode>();
                        foreach (TreeNode n in nl) n.Remove();
                        ShowStatus(node, am.status, false, am.changes_pending);
                    }
                    am.subfiles.Clear();
                    break;
                case ActionType.ChangePermissionSet:
                    am = (InstalledAssembly)act.target;
                    if (!am.actions.Exists(a => a != act)) am.changes_pending = false;
                    node = GetNodeFor(am);
                    if (node != null) ShowStatus(node, am.status, false, am.changes_pending);
                    break;
                case ActionType.AddKeyAndLogin:
                    am = (InstalledAssembly)act.target;
                    am.database.server.GetKeysAndLogins();
                    am.key = am.database.server.keys.FirstOrDefault(q => q.Thumbprint.SequenceEqual(am.publicKeyToken));
                    am.login = am.database.server.logins.FirstOrDefault(q => q.SID.SequenceEqual(am.key.SID));
                    foreach (Database d in am.database.server.databases)
                        foreach (InstalledAssembly a in d.assemblies.Where(q => q.publicKeyToken.SequenceEqual(am.publicKeyToken) && q != am))
                        {
                            a.key = am.key;
                            a.login = am.login;
                        }
                    if (!am.actions.Exists(a => a != act)) am.changes_pending = false;
                    node = GetNodeFor(am);
                    if (node != null) ShowStatus(node, am.status, false, am.changes_pending);
                    break;
                case ActionType.DropKeyAndLogin:
                    am = (InstalledAssembly)act.target;
                    am.database.server.GetKeysAndLogins();
                    foreach (Database d in am.database.server.databases)
                        foreach (InstalledAssembly a in d.assemblies.Where(q => q.publicKeyToken.SequenceEqual(am.publicKeyToken)))
                        {
                            a.key = null;
                            a.login = null;
                        }
                    if (!am.actions.Exists(a => a != act)) am.changes_pending = false;
                    node = GetNodeFor(am);
                    if (node != null) ShowStatus(node, am.status, false, am.changes_pending);
                    break;
                case ActionType.DropPermission:
                case ActionType.AddPermission:
                    am = (InstalledAssembly)act.target;
                    am.database.server.GetKeysAndLogins();
                    am.login = am.database.server.logins.FirstOrDefault(q => q.PID == am.login.PID);
                    am.key = am.login.key;
                    break;
                case ActionType.DropAssembly:
                    am = (InstalledAssembly)act.target;
                    node = GetNodeFor(am);
                    if (node != null) node.Remove();
                    db = am.database;
                    if (db != null) db.assemblies.Remove(am);
                    break;
                case ActionType.AddFunction:
                    fn = (Function)act.target;
                    fn.status = installstatus.in_place;
                    node = GetNodeFor(fn);
                    if (node != null) ShowStatus(node, fn.status, true, fn.changes_pending);
                    break;
                case ActionType.RenameFunction:
                case ActionType.ChangeFunctionSchema:
                    fn = (Function)act.target;
                    if (!fn.actions.Exists(a => a != act)) fn.changes_pending = false;
                    node = GetNodeFor(fn);
                    if (node != null)
                    {
                        node.Text = fn.ShortName(false);
                        ShowStatus(node, fn.status, false, fn.changes_pending);
                    }
                    break;
                case ActionType.DropFunction:
                    fn = (Function)act.target;
                    fn.status = installstatus.not_installed;
                    node = GetNodeFor(fn);
                    if (node != null)
                    {
                        if ((act.parent != null && act.parent.action == ActionType.SwapAssembly)
                        || (fn.type == "TA" && fn.assembly.functions.Count(pr => pr.assembly_method == fn.assembly_method) > 1))
                        {
                            fn.assembly.functions.Remove(fn);
                            node.Remove();
                        }
                        else ShowStatus(node, fn.status, true, fn.changes_pending);
                    }
                    break;
                case ActionType.SetParameterDefault:
                    p = (Parameter)act.target;
                    fn = p.function;
                    if (!fn.actions.Exists(a => a != act)) fn.changes_pending = false;
                    node = GetNodeFor(fn);
                    if (node != null) ShowStatus(node, fn.status, false, fn.changes_pending);
                    break;
                case ActionType.DropFile:
                    InstalledAssembly af = (InstalledAssembly)act.target;
                    TreeNode afNode = act.targetnode;
                    TreeNode amNode = afNode.Parent;
                    am = (InstalledAssembly)amNode.Tag;
                    am.subfiles.Remove(af);
                    //af.parent = null;
                    afNode.Remove();
                    //                    act.actionnode.Remove();
                    //                    af.actions.Remove(act);
                    break;
                case ActionType.AddFile:
                    af = (InstalledAssembly)act.target;
                    afNode = act.targetnode;
                    amNode = afNode.Parent;
                    am = (InstalledAssembly)amNode.Tag;
                    if (!am.subfiles.Contains(af)) am.subfiles.Add(af);
                    af.status = installstatus.in_place;
                    ShowStatus(afNode, af.status, false, false);
                    //                    act.actionnode.Remove();
                    //                    af.actions.Remove(act);
                    break;
                case ActionType.ChangeTriggerEvents:
                case ActionType.ChangeTriggerTarget:
                    fn = (Function)act.target;
                    node = act.targetnode;
                    fn.trigger = (Trigger)act.newvalue;
                    node.ToolTipText = fn.tooltiptext();
                    node.Text = fn.ShortName(false);
                    ShowStatus(node, fn.status, true, fn.changes_pending);
                    break;
            }
        }

        public void ShowEffectOfActionRecursive(Action act)
        {
            ShowEffectOfAction(act);
            foreach (Action a in act.subactions) ShowEffectOfActionRecursive(a);
        }

        private void ShowStatus(TreeNode tn, installstatus status, bool showticks, bool changes_pending)
        {
            switch (status)
            {
                case installstatus.in_place:
                    if (showticks) tn.StateImageIndex = 1;
                    tn.ForeColor = Color.Black;
                    tn.NodeFont = fontRegular;
                    break;
                case installstatus.not_installed:
                    if (showticks) tn.StateImageIndex = 0;
                    tn.ForeColor = Color.SlateGray;
                    tn.NodeFont = fontRegular;
                    break;
                case installstatus.pending_add:
                    if (showticks) tn.StateImageIndex = 1;
                    tn.ForeColor = Color.Black;
                    tn.NodeFont = fontItalic;
                    break;
                case installstatus.pending_remove:
                    if (showticks) tn.StateImageIndex = 0;
                    tn.ForeColor = Color.SlateGray;
                    tn.NodeFont = fontStrikeout;
                    break;
            }
            if (changes_pending && tn.ForeColor == Color.Black) tn.ForeColor = Color.Blue;
        }

        private bool IsIn(string subject, params string[] list)
        {
            return list.Contains(subject);
        }
        private bool Similar(Function f1, Function f2)
        {
            if (f1 == null || f2 == null) return false;

            if (IsIn(f1.type, "FS", "FT", "PC", "TA") && f1.assembly_method != f2.assembly_method) return false;
            if (IsIn(f1.type, "AF", "UDT") && f1.assembly_class != f2.assembly_class) return false;

            var pl = from Parameter p1 in f1.parameters
                     join Parameter p2 in f2.parameters on new { p1.position, p1.type } equals new { p2.position, p2.type }
                     select p1;
            if (pl.Count() != f1.parameters.Count || pl.Count() != f2.parameters.Count) return false;

            return true;
        }

        private void substituteNodeRecursive(TreeNodeCollection tnc, TreeNode old, TreeNode nyoo)
        {
            foreach (TreeNode tn in tnc)
            {
                Action act = (Action)tn.Tag;
                if (act.targetnode == old) act.targetnode = nyoo;
                substituteNodeRecursive(tn.Nodes, old, nyoo);
            }
        }

        private Action SwapAssembly(TreeNode amNode, InstalledAssembly amNew, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            amNew.assembly_id = am.assembly_id;
            amNew.database = am.database;
            amNode.Tag = amNew;
            am.database.assemblies.Remove(am);
            am.database.assemblies.Add(amNew);

            Action act = am.actions.SingleOrDefault(p => p.action == ActionType.SwapAssembly);
            if (act != null)
            {
                while (act.subactions.Count(p => p.action == ActionType.AddFunction) > 0) DeregisterAction(act.subactions.First(p => p.action == ActionType.AddFunction));
                while (act.subactions.Count(p => p.action == ActionType.DropFunction) > 0)
                {
                    Action a = act.subactions.First(p => p.action == ActionType.DropFunction);
                    Function f = (Function)a.target;
                    TreeNode fn = a.targetnode;
                    DeregisterAction(a);
                    if (f.actions.Exists(p => p.action == ActionType.AddFunction)) f.status = installstatus.pending_add;
                    else f.status = installstatus.in_place;
                    ShowStatus(fn, f.status, true, f.changes_pending);
                }
                DeregisterAction(act);
                InstalledAssembly old = (InstalledAssembly)act.oldvalue;
                if (old.fullname != amNew.fullname)
                {
                    act.newvalue = amNew;
                    act.target = amNew;
                    RegisterAction(act, parent);
                    amNew.changes_pending = true;
                }
                else
                {
                    amNew.changes_pending = false;
                    act = null;
                }
            }
            else
            {
                act = new Action();
                act.oldvalue = am;
                act.newvalue = amNew;
                act.target = amNew;
                act.targetnode = amNode;
                act.action = ActionType.SwapAssembly;
                amNode.Tag = amNew;
                RegisterAction(act, parent);
                amNew.changes_pending = true;
            }

            RelinkAssemblyReferences(amNew);

            SwapAssemblyFunctions(amNode, amNew, am);

            foreach (Action a in am.actions.Where(p => p != act))
            {
                if (a.target == am) a.target = amNew;
                if (a.oldvalue == am) a.oldvalue = amNew;
                if (a.newvalue == am) a.newvalue = amNew;
                amNew.actions.Add(a);
            }
            am.actions.Clear();

            ShowStatus(amNode, amNew.status, false, amNew.changes_pending);
            amNode.ToolTipText = "Assembly: " + DetailedAssemblyName(amNew);
            return act;
        }

        private void SwapAssemblyFunctions(TreeNode amNode, InstalledAssembly amNew, InstalledAssembly am)
        {
            bool islibrary = amNode.TreeView == tvAssemblies;

            List<Function> matchedNewFunctions = new List<Function>();
            Function newFunc;
            foreach (Function f in am.functions)
            {
                newFunc = null;
                int m1 = amNew.functions.Count(p => p.name.Equals(f.name, StringComparison.OrdinalIgnoreCase) && p.type == f.type);
                if (m1 == 0)
                {
                    if (IsIn(f.type, "FS", "FT", "PC", "TA"))
                        newFunc = amNew.functions.SingleOrDefault(p => p.type == f.type && p.assembly_method == f.assembly_method);
                    else
                        newFunc = amNew.functions.SingleOrDefault(p => p.type == f.type && p.assembly_class == f.assembly_class);
                    if (newFunc != null) m1 = 1;
                }
                else if (m1 > 1)
                {
                    if (f.type == "FS" || f.type == "FT" || f.type == "PC" || f.type == "TA")
                        newFunc = amNew.functions.SingleOrDefault(p => p.name.Equals(f.name, StringComparison.OrdinalIgnoreCase) && p.type == f.type && p.assembly_method == f.assembly_method);
                    else
                        newFunc = amNew.functions.SingleOrDefault(p => p.name.Equals(f.name, StringComparison.OrdinalIgnoreCase) && p.type == f.type && p.assembly_class == f.assembly_class);
                    if (newFunc != null) m1 = 1;
                    else m1 = 0;
                }
                else newFunc = amNew.functions.Single(p => p.name.Equals(f.name, StringComparison.OrdinalIgnoreCase) && p.type == f.type);
                if (newFunc != null)
                {
                    if (matchedNewFunctions.Contains(newFunc)) newFunc = new Function(newFunc);
                    matchedNewFunctions.Add(newFunc);
                    newFunc.name = f.name;
                    newFunc.schema = f.schema;
                    newFunc.status = f.status;
                    newFunc.signature_changed = islibrary && !Similar(f, newFunc);
                    if (f.type == "TA") newFunc.trigger = new Trigger(f.trigger);
                    foreach (Action a in f.actions)
                    {
                        if (a.target == f) a.target = newFunc;
                        if (a.oldvalue == f) a.oldvalue = newFunc;
                        if (a.newvalue == f) a.newvalue = newFunc;
                        newFunc.actions.Add(a);
                    }
                    f.actions.Clear();
                }
                else if (!islibrary) f.not_defined = true;
            }
            foreach (Function f in amNew.functions.Where(p => !matchedNewFunctions.Exists(x => x.name.Equals(p.name, StringComparison.OrdinalIgnoreCase) && x.type == p.type)))
            {
                matchedNewFunctions.Add(f);
                if (f.type == "TA" && !f.trigger.isdatabase) f.schema = f.trigger.target_schema;
                else if (!islibrary) f.schema = am.database.default_schema;
                if (!islibrary) f.needs_installing = true;
            }
            amNew.functions.Clear();
            amNew.functions = matchedNewFunctions;

            TreeNode FunctionNode;
            foreach (Function f in amNew.functions)
            {
                if (islibrary) FunctionNode = GetLibraryNodeFor(f);
                else FunctionNode = GetNodeFor(f);
                if (FunctionNode != null)
                {
                    FunctionNode.Tag = f;
                    continue;
                }
                f.assembly = amNew;
                string tooltip = f.FullFunctionTypeName();
                int index = f.FunctionTypeIconIndex();
                if (f.type == "TA")
                {
                    if (f.trigger.isdatabase)
                    {
                        index = 11;
                        tooltip = "Database Trigger: ";
                    }
                }
                string nodetext = f.ShortName(false);
                FunctionNode = amNode.Nodes.Add(nodetext, nodetext, index, index);
                FunctionNode.ToolTipText = f.tooltiptext();
                FunctionNode.Tag = f;
                FunctionNode.ContextMenuStrip = islibrary ? cmLibraryFunction : cmFunction;
                ShowStatus(FunctionNode, f.status, true, false);
            }
        }

        private void SwapLibraryAssembly(TreeNode amNode, InstalledAssembly amNew, Library lib)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;

            amNew.assembly_id = am.assembly_id;
            amNew.database = null;
            amNode.Tag = amNew;
            lib.assemblies.Remove(am);
            lib.assemblies.Add(amNew);

            RelinkLibraryAssemblyReferences(amNew, lib);

            amNode.Nodes.Clear();
            SwapAssemblyFunctions(amNode, amNew, am);

            foreach (InstalledAssembly af in amNew.subfiles) AddFileToAssemblyInLibrary(amNode, af);
            foreach (InstalledAssembly af in am.subfiles.Where(p => !amNew.subfiles.Exists(x => x.name.Equals(p.name, StringComparison.OrdinalIgnoreCase)))) AddFileToAssemblyInLibrary(amNode, af);

        }

        private void toggleActionControls()
        {
            if (SuppressEvents) return;
            mnuExecuteAllActions.Enabled =
            mnuCancelAllActions.Enabled =
            mnuScriptAllActions.Enabled =
            mnuSaveActions.Enabled =
            tbExecuteAllActions.Enabled =
            tbScriptAllActions.Enabled =
            tbCancelAllActions.Enabled =
            tbSaveActions.Enabled =
            tvActions.Nodes.Count > 0;
            lbNoActionsPending.Visible = tvActions.Nodes.Count == 0;

            tbExecuteActionsToHere.Enabled = mnuExecuteDownToAction.Enabled = executeToHereToolStripMenuItem.Enabled =
                tvActions.Nodes.Count > 1 && tvActions.SelectedNode != null && tvActions.SelectedNode.Level == 0 && tvActions.SelectedNode.Index > 0;

            if (!tbSaveActions.Enabled)
                mnuExecuteAction.Enabled = mnuScriptAction.Enabled = mnuCancelAction.Enabled =
                tbExecuteAction.Enabled = tbScriptAction.Enabled = tbCancelAction.Enabled = false;
        }

        private void toggleAssemblyControls(InstalledAssembly assembly)
        {
            toggleDatabaseControls(assembly.database);
            tbScript.Tag = "Assembly";
            tbScriptAsAdd.Visible = false;
            tbScriptAsCreate.Visible = true;
            tbScriptAsAlter.Visible = false;
            tbScriptAsDropAndCreate.Visible = false;
            tbScriptAsExecute.Visible = false;
            tbScriptAsExecuteDivider.Visible = false;
            tbScriptAsSelect.Visible = false;
            tbScriptAsSelectDivider.Visible = false;

            tbScriptAsCreate.Text = "Script Assembly As CREATE To";
            tbScriptAsDrop.Text = "Script Assembly As DROP To";

            switch (assembly.status)
            {
                case installstatus.in_place:
                case installstatus.pending_add:
                    tbDropAssembly.DefaultItem = tbDropAssemblyAndAll;
                    tbDropAssembly.Image = tbDropAssemblyAndAll.Image;
                    tbDropAssembly.ToolTipText = tbDropAssemblyAndAll.Text;
                    tbPermissionSet.Enabled = mnuPermission.Enabled = assemblyPermissionSetToolStripMenuItem.Enabled = true;
                    mnuDropAssembly.Available = dropAssemblyToolStripMenuItem.Available = tbDropAssemblyAndAll.Available = true;
                    mnuUndoDropAssembly.Available = reinstateAssemblyToolStripMenuItem.Available = tbUndoDropAssembly.Available = false;
                    mnuDropChildren.Enabled = tbDropChildren.Enabled = uninstallAllObjectsStripMenuItem.Enabled
                        = (assembly.functions.Count(f => (f.status == installstatus.in_place || f.status == installstatus.pending_add)) > 0);
                    mnuInstallChildren.Enabled = tbInstallChildren.Enabled = installAllObjectsToolStripMenuItem.Enabled
                        = (assembly.functions.Count(f => (f.status == installstatus.not_installed || f.status == installstatus.pending_remove)) > 0);
                    mnuImportFile.Enabled = importAssociatedFilesToolStripMenuItem.Enabled = true;
                    mnuAssemblyPaste.Enabled = pasteAssemblyToolStripMenuItem.Enabled = tbPaste.Enabled;
                    mnuAssemblyPaste.Text = pasteAssemblyToolStripMenuItem.Text = tbPaste.ToolTipText;
                    mnuCutAssembly.Enabled = cutAssemblyToolStripMenuItem.Enabled = true;
                    break;
                case installstatus.pending_remove:
                    tbDropAssembly.DefaultItem = tbUndoDropAssembly;
                    tbDropAssembly.Image = tbUndoDropAssembly.Image;
                    tbDropAssembly.ToolTipText = tbUndoDropAssembly.Text;
                    tbPermissionSet.Enabled = mnuPermission.Enabled = assemblyPermissionSetToolStripMenuItem.Enabled = false;
                    mnuDropAssembly.Available = dropAssemblyToolStripMenuItem.Available = tbDropAssemblyAndAll.Available = false;
                    mnuUndoDropAssembly.Available = reinstateAssemblyToolStripMenuItem.Available = tbUndoDropAssembly.Available = true;
                    mnuDropChildren.Enabled = uninstallAllObjectsStripMenuItem.Enabled = tbDropChildren.Enabled = false;
                    mnuInstallChildren.Enabled = installAllObjectsToolStripMenuItem.Enabled = tbInstallChildren.Enabled = false;
                    mnuImportFile.Enabled = importAssociatedFilesToolStripMenuItem.Enabled = false;
                    tbPaste.Enabled = mnuAssemblyPaste.Enabled = pasteAssemblyToolStripMenuItem.Enabled = false;
                    tbCut.Enabled = mnuCutAssembly.Enabled = cutAssemblyToolStripMenuItem.Enabled = false;
                    break;
            }
            tbCut.ToolTipText = "Cut Assembly";
            tbCopy.ToolTipText = "Copy Assembly";
            tbCut.Tag = tbPaste.Tag = tbCopy.Tag = "Database Assembly";

            if (assembly.actions.Exists(a => a.action == ActionType.SwapAssembly))
                undoReplaceAssemblyToolStripMenuItem.Available = true;
            else
                undoReplaceAssemblyToolStripMenuItem.Available = false;
            mnuUndoReplace.Available = tbUndoSwapAssembly.Available = undoReplaceAssemblyToolStripMenuItem.Available;

            switch (assembly.permission_set)
            {
                case 1:
                    tbPermissionSet.Image = Resource.SafeAssembly2;
                    mnuPermission.Image = assemblyPermissionSetToolStripMenuItem.Image = Resource.SafeAssembly2;
                    SetCheck(assemblyPermissionSetToolStripMenuItem.DropDownItems, 0);
                    SetCheck(tbPermissionSet.DropDownItems, 0);
                    SetCheck(mnuPermission.DropDownItems, 0);
                    break;
                case 2:
                    tbPermissionSet.Image = Resource.ExternalAssembly;
                    mnuPermission.Image = assemblyPermissionSetToolStripMenuItem.Image = Resource.ExternalAssembly;
                    SetCheck(assemblyPermissionSetToolStripMenuItem.DropDownItems, 1);
                    SetCheck(tbPermissionSet.DropDownItems, 1);
                    SetCheck(mnuPermission.DropDownItems, 1);
                    break;
                case 3:
                    tbPermissionSet.Image = Resource.UnsafeAssembly;
                    mnuPermission.Image = assemblyPermissionSetToolStripMenuItem.Image = Resource.UnsafeAssembly;
                    SetCheck(assemblyPermissionSetToolStripMenuItem.DropDownItems, 2);
                    SetCheck(tbPermissionSet.DropDownItems, 2);
                    SetCheck(mnuPermission.DropDownItems, 2);
                    break;
            }

            if (assembly.key == null)
            {
                tbKey.ToolTipText = tbKey.Text = mnuKey.Text = keyToolStripMenuItem.Text = "Create Asymmetric Key and Login...";
                tbKey.Image = mnuKey.Image = keyToolStripMenuItem.Image = Resource.Key;
                tbKey.Tag = "CREATE";
            }
            else if (assembly.key.status == installstatus.pending_remove)
            {
                tbKey.ToolTipText = tbKey.Text = mnuKey.Text = keyToolStripMenuItem.Text = "Don't Remove Asymmetric Key and Login...";
                tbKey.Image = mnuKey.Image = keyToolStripMenuItem.Image = Resource.Key;
                tbKey.Tag = "CREATE";
            }
            else if (assembly.key.status == installstatus.pending_add)
            {
                tbKey.ToolTipText = tbKey.Text = mnuKey.Text = keyToolStripMenuItem.Text = "Don't Create Asymmetric Key and Login...";
                tbKey.Image = mnuKey.Image = keyToolStripMenuItem.Image = Resource.DropKey;
                tbKey.Tag = "DROP";
            }            
            else
            {
                tbKey.ToolTipText = tbKey.Text = mnuKey.Text = keyToolStripMenuItem.Text = "Remove Asymmetric Key and Login...";
                tbKey.Image = mnuKey.Image = keyToolStripMenuItem.Image = Resource.DropKey;
                tbKey.Tag = "DROP";
            }
            
            tbKey.Enabled = mnuKey.Enabled = keyToolStripMenuItem.Enabled = (assembly.publicKeyToken.Count() > 0);

            mnuAssembly.Tag = "ASSEMBLY";
            mnuFunction.Tag = "FUNCTION";
            mnuAssociatedFile.Tag = "FILE";
            mnuScriptAssembly.Tag = "Assembly";
        }

        private void toggleDatabaseControls(Database database)
        {
            toggleServerControls(database.server);
            if (database.trustworthy)
            {
                mnuSetTrustworthy.Text = tbSetTrustworthy.ToolTipText = setTrustworthyToolStripMenuItem.Text = "Set Trustworthy Off";
                mnuSetTrustworthy.Image = tbSetTrustworthy.Image = setTrustworthyToolStripMenuItem.Image = AssemblyManager.Resource.Untrustworthy;
            }
            else
            {
                mnuSetTrustworthy.Text = tbSetTrustworthy.ToolTipText = setTrustworthyToolStripMenuItem.Text = "Set Trustworthy On";
                mnuSetTrustworthy.Image = tbSetTrustworthy.Image = setTrustworthyToolStripMenuItem.Image = AssemblyManager.Resource.Trustworthy;
            }
            mnuAssembly.Tag = "ASSEMBLY";
            mnuFunction.Tag = "FUNCTION";
            mnuAssociatedFile.Tag = "FILE";
            tbSchema.ToolTipText = "Change Database Default Schema";
            tbRefreshAll.ToolTipText = "Refresh Database " + database.name;
            mnuDatabasePaste.Enabled = databasePasteToolStripMenuItem.Enabled = tbPaste.Enabled;
            mnuDatabasePaste.Text = databasePasteToolStripMenuItem.Text = tbPaste.ToolTipText;
            tbPaste.Tag = "Database";
        }

        private void toggleFileControls(InstalledAssembly af)
        {
            tbScript.Tag = "File";
            tbScriptAsAdd.Visible = true;
            tbScriptAsAlter.Visible = false;
            tbScriptAsCreate.Visible = false;
            tbScriptAsDropAndCreate.Visible = false;
            tbScriptAsExecute.Visible = false;
            tbScriptAsExecuteDivider.Visible = false;
            tbScriptAsSelect.Visible = false;
            tbScriptAsSelectDivider.Visible = false;

            tbScriptAsAdd.Text = "Script File As ADD To";
            tbScriptAsDrop.Text = "Script File As DROP To";

            tbDrop.Tag = "FILE";

            switch (af.status)
            {
                case installstatus.in_place:
                    tbDrop.ToolTipText = removeFileInDatabaseToolStripMenuItem.Text = "Remove File";
                    tbDrop.Image = Resource.Drop;
                    mnuRemoveFile.ShortcutKeyDisplayString = removeFileInDatabaseToolStripMenuItem.ShortcutKeyDisplayString = "Del";
                    break;
                case installstatus.pending_add:
                    tbDrop.ToolTipText = removeFileInDatabaseToolStripMenuItem.Text = "Cancel File Import";
                    tbDrop.Image = Resource.Drop;
                    mnuRemoveFile.ShortcutKeyDisplayString = removeFileInDatabaseToolStripMenuItem.ShortcutKeyDisplayString = "Del";
                    break;
                case installstatus.pending_remove:
                    tbDrop.ToolTipText = removeFileInDatabaseToolStripMenuItem.Text = "Cancel Remove File";
                    tbDrop.Image = Resource.Checked_box;
                    mnuRemoveFile.ShortcutKeyDisplayString = removeFileInDatabaseToolStripMenuItem.ShortcutKeyDisplayString = "Ins";
                    break;
            }
            mnuRemoveFile.Text = tbDrop.ToolTipText;
            mnuRemoveFile.Image = tbDrop.Image;
            mnuAssembly.Tag = "ASSEMBLY";
            mnuFunction.Tag = "FUNCTION";
            mnuAssociatedFile.Tag = "FILE";
            mnuScriptFile.Tag = "File";
            tbCut.ToolTipText = "Cut File";
            tbCopy.ToolTipText = "Copy File";
            tbCut.Tag = tbCopy.Tag = "Database File";
            mnuCutFile.Enabled = cutFileToolStripMenuItem.Enabled = tbCut.Enabled = af.status == installstatus.in_place || af.status == installstatus.pending_add;
        }

        private void toggleFunctionControls(Function function)
        {
            toggleAssemblyControls(function.assembly);
            tbScript.Tag = "Function";
            tbScriptAsAdd.Visible = false;
            tbScriptAsAlter.Visible = true;
            tbScriptAsCreate.Visible = true;
            tbScriptAsDropAndCreate.Visible = true;
            tbScriptAsExecute.Visible = true;
            tbScriptAsExecuteDivider.Visible = true;
            tbScriptAsSelect.Visible = true;
            tbScriptAsSelectDivider.Visible = true;

            functionInstalledToolStripMenuItem.Enabled = !(function.assembly.status == installstatus.pending_remove);
            if (function.ShowAsInstalled())
            {
                functionInstalledToolStripMenuItem.Image = Resource.Drop;
                functionInstalledToolStripMenuItem.Text = "Drop " + function.ShortFunctionTypeName();
                functionInstalledToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            }
            else
            {
                functionInstalledToolStripMenuItem.Image = Resource.Checked_box;
                functionInstalledToolStripMenuItem.Text = "Install " + function.ShortFunctionTypeName();
                functionInstalledToolStripMenuItem.ShortcutKeyDisplayString = "Ins";
            }
            tbDrop.Tag = "FUNCTION";
            mnuInstallFunction.Enabled = tbDrop.Enabled = functionInstalledToolStripMenuItem.Enabled;
            mnuInstallFunction.Image = tbDrop.Image = functionInstalledToolStripMenuItem.Image;
            mnuInstallFunction.Text = tbDrop.ToolTipText = functionInstalledToolStripMenuItem.Text;

            tbRename.Enabled = mnuRename.Enabled = renameFunctionToolStripMenuItem.Enabled = (function.status == installstatus.in_place || function.status == installstatus.pending_add);

            mnuScriptFunctionAsAlter.Enabled = tbScriptAsAlter.Enabled = functionScriptAsAlterToToolStripMenuItem.Enabled = function.AllowScriptAsAlter();
            mnuScriptFunctionAsSelect.Enabled = tbScriptAsSelect.Enabled = functionScriptAsSelectToToolStripMenuItem.Enabled = function.AllowScriptAsSelect();
            mnuScriptFunctionAsExecute.Enabled = tbScriptAsExecute.Enabled = functionScriptAsExecuteToToolStripMenuItem.Enabled = function.AllowScriptAsExecute();
            mnuSchema.Enabled = tbSchema.Enabled = functionSchemaToolStripMenuItem.Enabled = function.AllowChangeSchema();
            tbSchema.ToolTipText = "Change Schema";
            mnuParameters.Visible = tbParameters.Enabled = functionParameterDefaultsToolStripMenuItem.Visible = function.AllowSetParameterDefaults();
            mnuScriptFunction.Text = scriptFunctionAsToolStripMenuItem.Text = Resource.mnuScriptAs.Replace("%TYPE%", function.ShortFunctionTypeName());
            switch (function.type)
            {
                case "FS":
                case "FT":
                    mnuFunction.Text = "F&unction";
                    break;
                case "PC":
                    mnuFunction.Text = "Stored &Procedure";
                    break;
                case "AF":
                    mnuFunction.Text = "A&ggregate";
                    break;
                case "UDT":
                    mnuFunction.Text = "&Type";
                    break;
                case "TA":
                    mnuFunction.Text = "&Trigger";
                    break;
            }
            tbScriptAsCreate.Text = scriptFunctionAsToolStripMenuItem.Text + " CREATE To";
            tbScriptAsAlter.Text = scriptFunctionAsToolStripMenuItem.Text + " ALTER To";
            tbScriptAsDrop.Text = scriptFunctionAsToolStripMenuItem.Text + " DROP To";
            tbScriptAsDropAndCreate.Text = scriptFunctionAsToolStripMenuItem.Text + " DROP And CREATE To";
            tbScriptAsExecute.Text = scriptFunctionAsToolStripMenuItem.Text + " EXECUTE To";
            tbScriptAsSelect.Text = scriptFunctionAsToolStripMenuItem.Text + " SELECT To";

            if (function.type == "TA")
            {
                mnuSchema.Visible = functionSchemaToolStripMenuItem.Visible = false;
                mnuTrigger.Visible = tbTrigger.Enabled = triggerToolStripMenuItem.Visible = (function.status == installstatus.in_place || function.status == installstatus.pending_add);
                mnuCloneTrigger.Visible = cloneTriggerToolStripMenuItem.Visible = !(function.assembly.status == installstatus.pending_remove);
                mnuTriggerAfterDatabase.Visible = tbAfterDatabase.Visible = afterDatabaseToolStripMenuItem.Visible = function.trigger.isdatabase;
                mnuTriggerAfterTable.Visible = tbAfterTable.Visible = afterTableToolStripMenuItem.Visible = function.trigger.target_type == "U ";
                mnuTriggerInsteadOf.Visible = tbInsteadOfTable.Visible = insteadOfToolStripMenuItem.Visible = !function.trigger.isdatabase;
                mnuTriggerAfterDatabase.Checked = tbAfterDatabase.Checked = afterDatabaseToolStripMenuItem.Checked = function.trigger.isdatabase;
                mnuTriggerInsteadOf.Checked = tbInsteadOfTable.Checked = insteadOfToolStripMenuItem.Checked = function.trigger.insteadof;
                mnuTriggerAfterTable.Checked = tbAfterTable.Checked = afterTableToolStripMenuItem.Checked = !function.trigger.insteadof && !function.trigger.isdatabase;
            }
            else
            {
                mnuSchema.Visible = functionSchemaToolStripMenuItem.Visible = true;
                mnuTrigger.Visible = tbTrigger.Enabled = triggerToolStripMenuItem.Visible = false;
                mnuCloneTrigger.Visible = cloneTriggerToolStripMenuItem.Visible = false;
            }
            mnuAssembly.Tag = "ASSEMBLY";
            mnuFunction.Tag = "FUNCTION";
            mnuAssociatedFile.Tag = "FILE";
            mnuScriptFunction.Tag = "Function";
            mnuCutFunction.Text = cutFunctionToolStripMenuItem.Text = tbCut.ToolTipText = "Cut " + function.ShortFunctionTypeName();
            mnuCopyFunction.Text = copyFunctionToolStripMenuItem.Text = tbCopy.ToolTipText = "Copy " + function.ShortFunctionTypeName();
            tbCut.Tag = tbCopy.Tag = "Database Function";
            tbCut.Enabled = mnuCutFunction.Enabled = cutFunctionToolStripMenuItem.Enabled = function.status == installstatus.in_place || function.status == installstatus.pending_add;
        }

        private Action toggleCLREnabled(TreeNode sNode, Action parent)
        {
            Action a = null;
            Server svr = (Server)sNode.Tag;
            if (svr.actions.Exists(p => p.action == ActionType.ToggleCLR))
            {
                a = svr.actions.Single(p => p.action == ActionType.ToggleCLR);
                DeregisterAction(a);
                svr.clr_enabled = !svr.clr_enabled;
                if (svr.actions.Count == 0) svr.changes_pending = false;
            }
            else
            {
                a = new Action();
                a.action = ActionType.ToggleCLR;
                a.target = svr;
                a.targetnode = sNode;
                a.oldvalue = svr.clr_enabled;
                a.newvalue = !svr.clr_enabled;
                RegisterAction(a, null);
                svr.clr_enabled = !svr.clr_enabled;
                svr.changes_pending = true;
            }

            toggleServerControls(svr);

            sNode.ToolTipText = svr.ToolTipText();
            sNode.ImageIndex = sNode.SelectedImageIndex = svr.clr_enabled ? 0 : 17;
            ShowStatus(sNode, installstatus.in_place, false, svr.changes_pending);
            return a;
        }

        private void toggleLibraryControls(Library library)
        {
            mnuSaveLibraryAs.Enabled = mnuRenameLibrary.Enabled = mnuCloseLibrary.Enabled = true;
            saveLibraryToolStripMenuItem.Enabled = mnuSaveLibrary.Enabled = tbSaveLibrary.Enabled = library.changes_pending;
            mnuSaveAllLibraries.Enabled = tbSaveAllLibraries.Enabled = (from TreeNode tn in tvAssemblies.Nodes select (Library)tn.Tag).Count(p => p.changes_pending) > 0;
            mnuMergeLibrary.Enabled = tbMergeLibraries.Enabled = true;
            tbLibraryRename.Tag = "Library";
            tbLibraryRename.ToolTipText = "Rename Library";
            tbImportAssemblyToLibrary.Enabled = true;
            mnuAssembly.Tag = "LIBRARY ASSEMBLY";
            mnuFunction.Tag = "LIBRARY FUNCTION";
            mnuAssociatedFile.Tag = "LIBRARY FILE";
            mnuLibraryPaste.Enabled = libraryPasteToolStripMenuItem.Enabled = tbLibraryPaste.Enabled;
            mnuLibraryPaste.Text = libraryPasteToolStripMenuItem.Text = tbLibraryPaste.ToolTipText;
            tbLibraryPaste.Tag = "Library";
        }

        private void toggleLibraryAssemblyControls(InstalledAssembly assembly)
        {
            tbLibraryScript.Tag = "LibraryAssembly";
            tbLibraryScriptAsAdd.Visible = false;
            tbLibraryScriptAsCreate.Visible = true;
            tbLibraryScriptAsAlter.Visible = false;
            tbLibraryScriptAsDropAndCreate.Visible = false;
            tbLibraryScriptAsExecute.Visible = false;
            tbLibraryScriptAsExecuteDivider.Visible = false;
            tbLibraryScriptAsSelect.Visible = false;
            tbLibraryScriptAsSelectDivider.Visible = false;

            tbLibraryScriptAsCreate.Text = "Script Assembly As CREATE To";
            tbLibraryScriptAsDrop.Text = "Script Assembly As DROP To";

            setAllObjectsToNotInstallToolStripMenuItem.Enabled = (assembly.functions.Count(f => (f.status == installstatus.in_place || f.status == installstatus.pending_add)) > 0);
            mnuDropChildren.Enabled = tbLibraryDropAll.Enabled = setAllObjectsToNotInstallToolStripMenuItem.Enabled;
            setAllObjectsToInstallToolStripMenuItem.Enabled = (assembly.functions.Count(f => (f.status == installstatus.not_installed || f.status == installstatus.pending_remove)) > 0);
            mnuInstallChildren.Enabled = tbLibraryInstallAll.Enabled = setAllObjectsToInstallToolStripMenuItem.Enabled;
            mnuDropAssembly.Visible = true;
            mnuUndoDropAssembly.Visible = false;
            mnuUndoReplace.Visible = false;
            mnuAssemblyPaste.Enabled = libraryAssemblyPasteToolStripMenuItem.Enabled = tbLibraryPaste.Enabled;
            mnuAssemblyPaste.Text = libraryAssemblyPasteToolStripMenuItem.Text = tbLibraryPaste.ToolTipText;

            switch (assembly.permission_set)
            {
                case 1:
                    mnuPermission.Image = libraryAssemblyPermissionSetToolStripMenuItem.Image = tbLibraryAssemblyPermissionSet.Image = Resource.SafeAssembly2;
                    SetCheck(libraryAssemblyPermissionSetToolStripMenuItem.DropDownItems, 0);
                    SetCheck(tbLibraryAssemblyPermissionSet.DropDownItems, 0);
                    SetCheck(mnuPermission.DropDownItems, 0);
                    break;
                case 2:
                    mnuPermission.Image = libraryAssemblyPermissionSetToolStripMenuItem.Image = tbLibraryAssemblyPermissionSet.Image = Resource.ExternalAssembly;
                    SetCheck(libraryAssemblyPermissionSetToolStripMenuItem.DropDownItems, 1);
                    SetCheck(tbLibraryAssemblyPermissionSet.DropDownItems, 1);
                    SetCheck(mnuPermission.DropDownItems, 1);
                    break;
                case 3:
                    mnuPermission.Image = libraryAssemblyPermissionSetToolStripMenuItem.Image = tbLibraryAssemblyPermissionSet.Image = Resource.UnsafeAssembly;
                    SetCheck(libraryAssemblyPermissionSetToolStripMenuItem.DropDownItems, 2);
                    SetCheck(tbLibraryAssemblyPermissionSet.DropDownItems, 2);
                    SetCheck(mnuPermission.DropDownItems, 2);
                    break;
            }
            tbKey.Enabled = mnuKey.Enabled = keyToolStripMenuItem.Enabled = false;
            mnuAssembly.Tag = "LIBRARY ASSEMBLY";
            mnuFunction.Tag = "LIBRARY FUNCTION";
            mnuAssociatedFile.Tag = "LIBRARY FILE";
            mnuScriptAssembly.Tag = "LibraryAssembly";
            tbLibraryCut.ToolTipText = "Cut Assembly";
            tbLibraryCopy.ToolTipText = "Copy Assembly";
            tbLibraryCut.Tag = tbLibraryPaste.Tag = tbLibraryCopy.Tag = "Library Assembly";

        }

        private void toggleLibraryFileControls(InstalledAssembly af)
        {
            tbLibraryScript.Tag = "LibraryFile";
            tbLibraryScriptAsAdd.Visible = true;
            tbLibraryScriptAsAlter.Visible = false;
            tbLibraryScriptAsCreate.Visible = false;
            tbLibraryScriptAsDropAndCreate.Visible = false;
            tbLibraryScriptAsExecute.Visible = false;
            tbLibraryScriptAsExecuteDivider.Visible = false;
            tbLibraryScriptAsSelect.Visible = false;
            tbLibraryScriptAsSelectDivider.Visible = false;
            tbLibraryParameters.Enabled = false;
            //tbLibraryProperties

            tbLibraryScriptAsAdd.Text = scriptLibraryFileAsToolStripMenuItem.Text + " ADD To";
            tbLibraryScriptAsDrop.Text = scriptLibraryFileAsToolStripMenuItem.Text + " DROP To";

            mnuAssembly.Tag = "LIBRARY ASSEMBLY";
            mnuFunction.Tag = "LIBRARY FUNCTION";
            mnuAssociatedFile.Tag = "LIBRARY FILE";
            mnuScriptFunction.Tag = "LibraryFunction";
            mnuScriptFile.Tag = "LibraryFile";

            tbLibraryDropFunction.Image = Resource.Drop;
            tbLibraryDropFunction.ToolTipText = "Drop File";
            tbLibraryDropFunction.Tag = "LibraryFile";

            tbLibraryCut.ToolTipText = "Cut File";
            tbLibraryCopy.ToolTipText = "Copy File";
            tbLibraryCut.Tag = tbLibraryCopy.Tag = "Library File";
        }

        private void toggleLibraryFunctionControls(Function function)
        {

            tbLibraryScript.Tag = "LibraryFunction";
            tbLibraryDropFunction.Tag = "LibraryFunction";
            tbLibraryScriptAsAdd.Visible = false;
            tbLibraryScriptAsAlter.Visible = true;
            tbLibraryScriptAsCreate.Visible = true;
            tbLibraryScriptAsDropAndCreate.Visible = true;
            tbLibraryScriptAsExecute.Visible = true;
            tbLibraryScriptAsExecuteDivider.Visible = true;
            tbLibraryScriptAsSelect.Visible = true;
            tbLibraryScriptAsSelectDivider.Visible = true;

            if (function.ShowAsInstalled())
            {
                installLibraryFunctionByDefaultToolStripMenuItem.Image = Resource.Drop;
                installLibraryFunctionByDefaultToolStripMenuItem.Text = "Drop " + function.ShortFunctionTypeName();
                installLibraryFunctionByDefaultToolStripMenuItem.ShortcutKeyDisplayString = "Del";
            }
            else
            {
                installLibraryFunctionByDefaultToolStripMenuItem.Image = Resource.Checked_box;
                installLibraryFunctionByDefaultToolStripMenuItem.Text = "Install " + function.ShortFunctionTypeName();
                installLibraryFunctionByDefaultToolStripMenuItem.ShortcutKeyDisplayString = "Ins";
            }
            mnuInstallFunction.Image = tbLibraryDropFunction.Image = installLibraryFunctionByDefaultToolStripMenuItem.Image;
            mnuInstallFunction.Text = tbLibraryDropFunction.ToolTipText = installLibraryFunctionByDefaultToolStripMenuItem.Text;

            mnuScriptFunctionAsAlter.Enabled = tbLibraryScriptAsAlter.Enabled = libraryFunctionScriptAsAlterToToolStripMenuItem.Enabled = function.AllowScriptAsAlter();
            mnuScriptFunctionAsSelect.Enabled = tbLibraryScriptAsSelect.Enabled = libraryFunctionScriptAsSelectToToolStripMenuItem.Enabled = function.AllowScriptAsSelect();
            mnuScriptFunctionAsExecute.Enabled = tbLibraryScriptAsExecute.Enabled = libraryFunctionScriptAsExecuteToToolStripMenuItem.Enabled = function.AllowScriptAsExecute();

            mnuParameters.Enabled = tbLibraryParameters.Enabled = libraryFunctionParameterDefaultsToolStripMenuItem.Enabled = function.AllowSetParameterDefaults();

            mnuScriptFunction.Text = scriptLibraryFunctionAsToolStripMenuItem.Text = Resource.mnuScriptAs.Replace("%TYPE%", function.ShortFunctionTypeName());
            tbLibraryScriptAsCreate.Text = scriptLibraryFunctionAsToolStripMenuItem.Text + " CREATE To";
            tbLibraryScriptAsAlter.Text = scriptLibraryFunctionAsToolStripMenuItem.Text + " ALTER To";
            tbLibraryScriptAsDrop.Text = scriptLibraryFunctionAsToolStripMenuItem.Text + " DROP To";
            tbLibraryScriptAsDropAndCreate.Text = scriptLibraryFunctionAsToolStripMenuItem.Text + " DROP And CREATE To";
            tbLibraryScriptAsExecute.Text = scriptLibraryFunctionAsToolStripMenuItem.Text + " EXECUTE To";
            tbLibraryScriptAsSelect.Text = scriptLibraryFunctionAsToolStripMenuItem.Text + " SELECT To";

            tbLibraryRename.Tag = "Function";
            tbLibraryRename.ToolTipText = "Rename " + function.ShortFunctionTypeName();
            mnuAssembly.Tag = "LIBRARY ASSEMBLY";
            mnuFunction.Tag = "LIBRARY FUNCTION";
            mnuAssociatedFile.Tag = "LIBRARY FILE";
            mnuScriptFunction.Tag = "LibraryFunction";
            mnuScriptFile.Tag = "LibraryFile";
            mnuSchema.Enabled = false;
            mnuTrigger.Enabled = false;
            mnuCloneTrigger.Enabled = false;

            mnuCutFunction.Text = cutLibraryFunctionToolStripMenuItem.Text = tbLibraryCut.ToolTipText = "Cut " + function.ShortFunctionTypeName();
            mnuCopyFunction.Text = copyLibraryFunctionToolStripMenuItem.Text = tbLibraryCopy.ToolTipText = "Copy " + function.ShortFunctionTypeName();
            tbLibraryCut.Tag = tbLibraryCopy.Tag = "Library Function";
            tbLibraryCut.Enabled = mnuCutFunction.Enabled = cutLibraryFunctionToolStripMenuItem.Enabled = function.status == installstatus.in_place;
        }

        private void toggleLibraryPaneControls(TreeNode node)
        {
            mnuAssembly.Enabled = node.Level == 1;
            mnuFunction.Enabled = node.Level == 2 && node.Tag.GetType().Name == "Function";
            mnuAssociatedFile.Enabled = node.Level == 2 && node.Tag.GetType().Name == "InstalledAssembly";



            tbLibraryAssemblyPermissionSet.Enabled = node.Level >= 1;
            tbLibraryDropAssembly.Enabled = node.Level >= 1;
            tbLibraryScript.Enabled = node.Level >= 1;
            tbLibraryDropFunction.Enabled = node.Level == 2;
            tbLibraryProperties.Enabled = node.Level == 1;
            tbExportAssemblyFromLibrary.Enabled = node.Level >= 1;
            tbLibraryRename.Enabled = node.Level == 0 || node.Level == 2 && node.Tag.GetType().Name == "Function";
            lbNoLibrariesLoaded.Visible = tvAssemblies.Nodes.Count == 0;
            tbLibraryCopy.Enabled = tbLibraryCut.Enabled = node.Level >= 1;
            string s = EnablePaste(node);
            tbLibraryPaste.Enabled = (tbLibraryPaste.ToolTipText = s == null ? "Paste" : "Paste " + s) != "Paste";

            switch (node.Level)
            {
                case 0: //library
                    cmLibrary.Tag = node;
                    Library lb = (Library)node.Tag;
                    toggleLibraryControls(lb);
                    break;
                case 1: //assembly
                    cmLibrary.Tag = node.Parent;
                    cmLibraryAssembly.Tag = node;
                    InstalledAssembly am = (InstalledAssembly)node.Tag;
                    Library l = (Library)node.Parent.Tag;
                    toggleLibraryControls(l);
                    toggleLibraryAssemblyControls(am);
                    break;
                case 2: //function or file
                    cmLibrary.Tag = node.Parent.Parent;
                    cmLibraryAssembly.Tag = node.Parent;
                    l = (Library)node.Parent.Parent.Tag;
                    toggleLibraryControls(l);
                    if (node.Tag.GetType().Name == "Function")
                    {
                        cmLibraryFunction.Tag = node;
                        Function function = (Function)node.Tag;
                        toggleLibraryFunctionControls(function);
                    }
                    else
                    {
                        cmLibraryFile.Tag = node;
                        InstalledAssembly af = (InstalledAssembly)node.Tag;
                        toggleLibraryFileControls(af);
                    }
                    break;
            }
        }

        private void toggleServerControls(Server server)
        {
            if (server.clr_enabled)
            {
                mnuEnableCLR.Image = tbEnableClr.Image = enableClrToolStripMenuItem.Image = AssemblyManager.Resource.ClrDisabled;
                mnuEnableCLR.Text = tbEnableClr.ToolTipText = enableClrToolStripMenuItem.Text = "Disable CLR";
            }
            else
            {
                mnuEnableCLR.Image = enableClrToolStripMenuItem.Image = tbEnableClr.Image = AssemblyManager.Resource.ClrEnabled;
                mnuEnableCLR.Text = enableClrToolStripMenuItem.Text = tbEnableClr.ToolTipText = "Enable CLR";
            }
            mnuAssembly.Tag = "ASSEMBLY";
            mnuFunction.Tag = "FUNCTION";
            mnuAssociatedFile.Tag = "FILE";
            tbRefreshAll.ToolTipText = "Refresh Server " + server.name;
        }

        private string EnablePaste(TreeNode node)
        {
            int amLevel = node.TreeView == tvServers ? 2 : 1;
            if (Clipboard.ContainsFileDropList())
            {
                if (node.Level == amLevel) return Clipboard.GetFileDropList().Count > 1 ? "Files" : "File";
                string[] files = new string[Clipboard.GetFileDropList().Count];
                Clipboard.GetFileDropList().CopyTo(files, 0);
                if (node.Level == amLevel - 1 && files.All(p => Path.GetExtension(p).ToLower() == ".dll")) return files.Count() > 1 ? "Assemblies" : "Assembly";
                return null;
            }
            DataObject o = (DataObject)Clipboard.GetDataObject();
            if (o.GetDataPresent("Function"))
            {
                Function f = (Function)o.GetData("Function");
                if (node.Level == amLevel) return f.ShortFunctionTypeName();
                else if (node.Level == amLevel - 1) return "Assembly with " + f.ShortFunctionTypeName();
            }
            if (o.GetDataPresent("Assembly") && node.Level == amLevel - 1) return "Assembly";
            if (o.GetDataPresent("File") && node.Level == amLevel) return "File";
            return null;
        }

        private void toggleServerPaneControls(TreeNode node)
        {
            if (node == null)
            {
                cmServerPane.Tag = null;
                mnuDatabase.Enabled = false;
                mnuAssembly.Enabled = false;
                mnuFunction.Enabled = false;
                mnuAssociatedFile.Enabled = false;
                mnuDisconnect.Enabled = tbDisconnectServer.Enabled = false;
                tbLoadActions.Enabled = loadActionsFromFileToolStripMenuItem.Enabled = mnuActions.Enabled =
                mnuDisconnectAll.Enabled = tbDisconnect.Enabled = tvServers.Nodes.Count > 0;
                mnuEnableCLR.Enabled = tbEnableClr.Enabled = false;
                mnuRefreshServer.Enabled = false;
                mnuRefreshAllServers.Enabled = tbRefreshAll.Enabled = tvServers.Nodes.Count > 0;
                if (tbDisconnect.Enabled)
                {
                    tbDisconnect.Image = tbDisconnectAll.Image;
                    tbDisconnect.DefaultItem = tbDisconnectAll;
                    tbDisconnect.ToolTipText = tbDisconnectAll.Text;
                }
                mnuSaveConnections.Enabled = tbSaveConnectionFile.Enabled = tvServers.Nodes.Count > 0;
                mnuServerProperties.Enabled = false;
                mnuShowHide.Enabled = tbShowHideDatabases.Enabled = false;
                for (int i = 7; i < tsServers.Items.Count; i++) tsServers.Items[i].Enabled = false;
                lbNoServersConnected.Visible = tvServers.Nodes.Count == 0;
                return;
            }
            mnuRefreshAllServers.Enabled = tbRefreshAll.Enabled = true;
            mnuSaveConnections.Enabled = tbSaveConnectionFile.Enabled = true;
            tbDisconnect.Enabled = true;
            tbDisconnect.Image = tbDisconnectServer.Image;
            tbDisconnect.DefaultItem = tbDisconnectServer;
            tbDisconnect.ToolTipText = tbDisconnectServer.Text;
            mnuDisconnect.Enabled = tbDisconnectServer.Enabled = true; // node.Level == 0;
            mnuShowHide.Enabled = tbShowHideDatabases.Enabled = true; // node.Level == 0;
            mnuEnableCLR.Enabled = tbEnableClr.Enabled = true; // node.Level == 0;
            mnuServerProperties.Enabled = mnuRefreshServer.Enabled = true; //node.Level == 0;
            mnuDatabase.Enabled = node.Level >= 1;
            mnuAssembly.Enabled = node.Level >= 2;
            mnuFunction.Enabled = node.Level == 3 && node.Tag.GetType().Name == "Function";
            mnuAssociatedFile.Enabled = node.Level == 3 && node.Tag.GetType().Name == "InstalledAssembly";
            tbImportAssemblies.Enabled = node.Level >= 1;
            tbSetTrustworthy.Enabled = node.Level >= 1;
            tbSchema.Enabled = node.Level == 1 || node.Level == 2 || node.Tag.GetType().Name == "Function";
            tbDropAssembly.Enabled = node.Level >= 2;
            tbPermissionSet.Enabled = node.Level >= 2;
            tbScript.Enabled = node.Level >= 2;
            tbDrop.Enabled = node.Level == 3;
            tbProperties.Enabled = true;
            tbRename.Enabled = node.Tag.GetType().Name == "Function";
            tbTrigger.Enabled = node.Tag.GetType().Name == "Function" && ((Function)node.Tag).type == "TA";
            tbCopy.Enabled = tbCut.Enabled = node.Level >= 2;
            string s = EnablePaste(node);
            tbPaste.Enabled = (tbPaste.ToolTipText = s == null ? "Paste" : "Paste " + s) != "Paste";

            switch (node.Level)
            {
                case 0: //Server
                    cmServer.Tag = node;
                    cmServerPane.Tag = "Server";
                    Server server = (Server)node.Tag;
                    toggleServerControls(server);
                    break;
                case 1: //Database
                    cmDatabase.Tag = node;
                    cmServer.Tag = node.Parent;
                    cmServerPane.Tag = "Database";
                    Database database = (Database)node.Tag;
                    toggleDatabaseControls(database);
                    break;
                case 2: //Assembly
                    cmAssembly.Tag = node;
                    cmDatabase.Tag = node.Parent;
                    cmServer.Tag = node.Parent.Parent;
                    cmServerPane.Tag = "Assembly";
                    InstalledAssembly assembly = (InstalledAssembly)node.Tag;
                    toggleAssemblyControls(assembly);
                    break;
                case 3: //Function or Procedure or Aggregate or Type or Associated File
                    cmAssembly.Tag = node.Parent;
                    cmDatabase.Tag = node.Parent.Parent;
                    cmServer.Tag = node.Parent.Parent.Parent;
                    if (node.Tag.GetType().Name == "Function")
                    {
                        cmFunction.Tag = node;
                        cmServerPane.Tag = "Function";
                        Function function = (Function)node.Tag;
                        toggleFunctionControls(function);
                    }
                    else
                    {
                        cmFile.Tag = node;
                        cmServerPane.Tag = "File";
                        InstalledAssembly af = (InstalledAssembly)node.Tag;
                        toggleFileControls(af);
                    }
                    break;
            }
        }

        private Action ToggleTrustworthy(TreeNode dbNode, Action parent)
        {
            Action a = null;
            Database db = (Database)dbNode.Tag;
            if (db.actions.Exists(p => p.action == ActionType.ToggleTrustworthy))
            {
                a = db.actions.Single(p => p.action == ActionType.ToggleTrustworthy);
                DeregisterAction(a);
                db.trustworthy = !db.trustworthy;
                if (db.actions.Count == 0) db.changes_pending = false;
            }
            else
            {
                a = new Action();
                a.action = ActionType.ToggleTrustworthy;
                a.target = db;
                a.targetnode = dbNode;
                a.oldvalue = db.trustworthy;
                a.newvalue = !db.trustworthy;
                RegisterAction(a, parent);
                db.trustworthy = !db.trustworthy;
                db.changes_pending = true;
            }
            dbNode.ToolTipText = db.ToolTipText();
            dbNode.ImageIndex = db.ImageIndex();
            dbNode.SelectedImageIndex = db.ImageIndex();
            ShowStatus(dbNode, installstatus.in_place, false, db.changes_pending);
            toggleDatabaseControls(db);
            return a;
        }

        private int TotalActionCount()
        {
            int n = 0;
            foreach (TreeNode tn in tvActions.Nodes) n += tn.Nodes.Count + 1;
            return n;
        }

        private int TotalAssemblyCount(Server server)
        {
            int n = 0;

            if (server == null)
            {
                foreach (TreeNode tn in tvServers.Nodes)
                {
                    Server sv = (Server)tn.Tag;
                    foreach (Database db in sv.databases) n += db.assemblies.Count;
                }
            }
            else foreach (Database db in server.databases) n += db.assemblies.Count;
            return n;
        }

        private int TotalFunctionCount(Database db)
        {
            int n = 0;

            foreach (InstalledAssembly am in db.assemblies) n += am.functions.Count + am.subfiles.Count;
            return n;
        }

        private int TotalFunctionCount(Server server)
        {
            int n = 0;

            if (server == null)
            {
                foreach (TreeNode tn in tvServers.Nodes)
                {
                    Server sv = (Server)tn.Tag;
                    foreach (Database db in sv.databases) foreach (InstalledAssembly am in db.assemblies) n += am.functions.Count + am.subfiles.Count;
                }
            }
            else foreach (Database db in server.databases) foreach (InstalledAssembly am in db.assemblies) n += am.functions.Count + am.subfiles.Count;
            return n;
        }

        private int TotalRollBacksCount()
        {
            int i = 0;
            foreach (TreeNode n in tvHistory.Nodes)
            {
                i++;
                Action a = (Action)n.Tag;
                if (a.action == ActionType.DropAssembly)
                {
                    InstalledAssembly am = (InstalledAssembly)a.target;
                    i += am.functions.Count * 2;
                }
            }
            return i;
        }

        private string Translate(string root, object o, string oldvalue, string newvalue)
        {
            Server sv = null;
            Database db = null;
            InstalledAssembly am = null;
            Function fn = null;
            Parameter pm = null;
            Schema sc = null;

            switch (o.GetType().Name)
            {
                case "Server":
                    sv = (Server)o;
                    break;
                case "Database":
                    db = (Database)o;
                    sv = db.server;
                    break;
                case "InstalledAssembly":
                    am = (InstalledAssembly)o;
                    db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case "Function":
                    fn = (Function)o;
                    am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case "Parameter":
                    pm = (Parameter)o;
                    fn = pm.function;
                    if (fn != null) am = fn.assembly;
                    if (am != null) db = am.database;
                    if (db != null) sv = db.server;
                    break;
                case "Schema":
                    sc = (Schema)o;
                    break;
            }
            if (sc != null) root = root.Replace("%SCHEMA%", sc.name);
            if (pm != null) root = root.Replace("%PARAMETER%", pm.name);
            if (sv != null) root = root.Replace("%SERVER%", sv.name);
            if (db != null) root = root.Replace("%DATABASE%", db.name);
            if (am != null) root = root.Replace("%ASSEMBLY%", am.name);
            if (fn != null) root = root.Replace("%FUNCTION%", fn.ShortName(true));
            if (fn != null) root = root.Replace("%TYPE%", fn.ShortFunctionTypeName());
            root = root.Replace("%OLDVALUE%", oldvalue);
            root = root.Replace("%NEWVALUE%", newvalue);
            return root;
        }

        public static byte[] Trip(string pt, byte[] k, byte[] v)
        {
            MemoryStream ms = null;
            CryptoStream cs = null;
            StreamWriter sw = null;

            TripleDESCryptoServiceProvider td = null;

            byte[] ec = null;

            try
            {
                td = new TripleDESCryptoServiceProvider();
                td.Key = k;
                td.IV = v;

                ICryptoTransform cr = td.CreateEncryptor(td.Key, td.IV);

                ms = new MemoryStream();
                cs = new CryptoStream(ms, cr, CryptoStreamMode.Write);
                sw = new StreamWriter(cs);

                sw.Write(pt);
            }
            finally
            {
                if (sw != null)
                    sw.Close();
                if (cs != null)
                    cs.Close();
                if (ms != null)
                    ms.Close();
                if (td != null)
                    td.Clear();
            }
            return ms.ToArray();
        }

        private Action UndropAssembly(TreeNode amNode, Action parent)
        {
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            Action act = am.actions.SingleOrDefault(a => a.action == ActionType.DropAssembly);
            if (act != null)
            {
                if (am.status == installstatus.pending_remove)
                {
                    if (!RelinkAssemblyReferences(am))
                    {
                        var ral = am.references.Where(p => !am.database.assemblies.Contains(p) || p.status == installstatus.pending_remove);
                        foreach (InstalledAssembly ra in ral)
                        {
                            Action a = ra.actions.FirstOrDefault(p => p.action == ActionType.DropAssembly);
                            if (a != null)
                            {
                                string root = Resource.mbxReferencedAssemblyMustBeRecovered.Replace("%ASSEMBLY1%", am.name).Replace("%ASSEMBLY2%", ra.name);
                                MessageBox.Show(root, "Referenced Assembly Must Be Recovered Too");
                                ReverseAction(a);
                            }
                            else
                            {
                                string root = Resource.mbxReferenceAssemblyCannotBeRecovered.Replace("%ASSEMBLY1%", am.name).Replace("%ASSEMBLY2%", ra.name);
                                MessageBox.Show(root, "Referenced Assembly Cannot Be Found");
                                return null;
                            }
                        }
                        RelinkAssemblyReferences(am);
                    }
                    List<Action> alist = new List<Action>(act.subactions);
                    foreach (Action a in alist) ReverseAction(a);
                    DeregisterAction(act);
                    am.status = installstatus.in_place;
                    ShowStatus(amNode, am.status, false, am.changes_pending);
                }
                else
                    amNode.Remove();
            }
            tvServers.SelectedNode = amNode;
            toggleServerPaneControls(amNode);
            return act;
        }

        public static string UnTrip(byte[] ct, byte[] k, byte[] v)
        {
            MemoryStream ms = null;
            CryptoStream cs = null;
            StreamReader sr = null;

            TripleDESCryptoServiceProvider td = null;

            string pt = null;

            try
            {
                td = new TripleDESCryptoServiceProvider();
                td.Key = k;
                td.IV = v;

                ICryptoTransform dc = td.CreateDecryptor(td.Key, td.IV);

                ms = new MemoryStream(ct);
                cs = new CryptoStream(ms, dc, CryptoStreamMode.Read);
                sr = new StreamReader(cs);

                pt = sr.ReadToEnd();
            }
            finally
            {
                if (sr != null)
                    sr.Close();
                if (cs != null)
                    cs.Close();
                if (ms != null)
                    ms.Close();
                if (td != null)
                    td.Clear();
            }
            return pt;
        }

        private void WriteActionRecursive(XmlTextWriter tw, Action act)
        {
            tw.WriteStartElement("Action");
            WriteXmlAttribute(tw, "ActionType", act.action.ToString());
            if (act.path != "") WriteXmlAttribute(tw, "Path", act.path);
            else WriteXmlAttribute(tw, "Path", act.targetnode.FullPath);
            string TargetType;
            if (act.target.GetType().Name == "Function")
                TargetType = ((Function)act.target).type;
            else
                TargetType = act.target.GetType().Name;
            WriteXmlAttribute(tw, "TargetType", TargetType);
            switch (act.target.GetType().Name)
            {
                case "Parameter": WriteXmlAttribute(tw, "Parameter", ((Parameter)act.target).name); break;
            }
            tw.WriteValue(act.displaytext);
            if (act.oldvalue != null)
            {
                tw.WriteStartElement("Oldvalue");
                switch (act.oldvalue.GetType().Name)
                {
                    case "Trigger":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        Trigger t = (Trigger)act.oldvalue;
                        WriteTrigger(tw, t);
                        break;
                    case "InstalledAssembly":
                        if (((InstalledAssembly)act.oldvalue).is_assembly)
                        {
                            WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                            WriteAssembly(tw, (InstalledAssembly)act.oldvalue, true);
                        }
                        else
                        {
                            WriteXmlAttribute(tw, "Type", "File");
                            WriteFile(tw, (InstalledAssembly)act.oldvalue);
                        }
                        break;
                    case "Int32":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        tw.WriteValue((int)act.oldvalue); break;
                    case "String":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        tw.WriteValue((string)act.oldvalue); break;
                    case "Bool":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        tw.WriteValue((bool)act.oldvalue); break;
                    case "Function":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        WriteFunction(tw, (Function)act.oldvalue); break;
                    case "Login":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        WriteLogin(tw, (Login)act.oldvalue); break;
                    default:
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        tw.WriteValue(act.oldvalue); break;
                }
                tw.WriteEndElement();
            }
            if (act.newvalue != null)
            {
                tw.WriteStartElement("Newvalue");
                switch (act.newvalue.GetType().Name)
                {
                    case "Trigger":
                        WriteXmlAttribute(tw, "Type", act.oldvalue.GetType().Name);
                        Trigger t = (Trigger)act.newvalue;
                        WriteTrigger(tw, t);
                        break;
                    case "InstalledAssembly":
                        InstalledAssembly am = (InstalledAssembly)act.newvalue;
                        if (act.action == ActionType.AddFile)
                        {
                            WriteXmlAttribute(tw, "Type", "File");
                            WriteFile(tw, am);
                        }
                        else
                        {
                            WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                            WriteAssembly(tw, am, true);
                        }
                        break;
                    case "Function":
                        WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                        WriteFunction(tw, (Function)act.newvalue);
                        break;
                    case "Login":
                        WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                        WriteLogin(tw, (Login)act.newvalue); break;
                    case "Int32":
                        WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                        tw.WriteValue((int)act.newvalue); break;
                    case "String":
                        WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                        tw.WriteValue((string)act.newvalue); break;
                    case "Bool":
                        WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                        tw.WriteValue((bool)act.newvalue); break;
                    default:
                        WriteXmlAttribute(tw, "Type", act.newvalue.GetType().Name);
                        tw.WriteValue(act.newvalue); break;
                }
                tw.WriteEndElement();
            }
            if (act.subactions.Count > 0 && act.action != ActionType.RenameFunction && act.action != ActionType.ChangeFunctionSchema)
            {
                tw.WriteStartElement("Subactions");
                foreach (Action a in act.subactions) WriteActionRecursive(tw, a);
                tw.WriteEndElement();
            }
            tw.WriteEndElement();
        }

        private void WriteLogin(XmlTextWriter tw, Login login)
        {
            tw.WriteStartElement("Login");
            WriteXmlAttribute(tw, "Name", login.Name);
            WriteXmlAttribute(tw, "Key", login.key.Name);
            tw.WriteEndElement();
        }

        private void WriteAssembly(XmlTextWriter tw, InstalledAssembly am, bool infull)
        {
            tw.WriteStartElement("Assembly");
            WriteXmlAttribute(tw, "Name", am.name);
            WriteXmlAttribute(tw, "Fullname", am.fullname);
            WriteXmlAttribute(tw, "Permission", am.permission_set.ToString());
            WriteXmlAttribute(tw, "Created", am.create_date.ToString("yyyy-MMM-dd HH:mm:ss"));
            WriteXmlAttribute(tw, "Modified", am.modify_date.ToString("yyyy-MMM-dd HH:mm:ss"));
            WriteXmlAttribute(tw, "Culture", am.culture.Name);
            WriteXmlAttribute(tw, "Platform", am.platform.ToString());
            WriteXmlAttribute(tw, "Version", am.version.ToString(4));
            tw.WriteStartAttribute("Token");
            tw.WriteBinHex(am.publicKeyToken, 0, am.publicKeyToken.Length);
            tw.WriteEndAttribute();

            //If writing out dependents and references you only need the stub
            if (infull)
            {
                //Functions
                if (am.functions.Count > 0)
                {
                    tw.WriteStartElement("Functions");
                    foreach (Function fn in am.functions) WriteFunction(tw, fn);
                    tw.WriteEndElement(); //Functions
                }

                //Files
                tw.WriteStartElement("Files");
                foreach (InstalledAssembly subfile in am.subfiles) WriteFile(tw, subfile);
                tw.WriteEndElement(); //Files

                //Bytes
                tw.WriteStartElement("Bytes");
                WriteXmlAttribute(tw, "Size", am.bytes.Length.ToString());
                tw.WriteBase64(am.bytes, 0, am.bytes.Length);
                tw.WriteEndElement();

                //Dependents
                if (am.dependents != null && am.dependents.Count > 0)
                {
                    tw.WriteStartElement("Dependents");
                    foreach (InstalledAssembly da in am.dependents) WriteAssembly(tw, da, false);
                    tw.WriteEndElement();
                }

                //References
                if (am.references != null && am.references.Count > 0)
                {
                    tw.WriteStartElement("References");
                    foreach (InstalledAssembly da in am.references) WriteAssembly(tw, da, false);
                    tw.WriteEndElement();
                }
            }


            tw.WriteEndElement(); //Assembly
        }

        private void WriteFile(XmlTextWriter tw, InstalledAssembly f)
        {
            tw.WriteStartElement("File");
            WriteXmlAttribute(tw, "Name", f.name);

            //Bytes
            tw.WriteStartElement("Bytes");
            WriteXmlAttribute(tw, "Size", f.bytes.Length.ToString());
            tw.WriteBase64(f.bytes, 0, f.bytes.Length);
            tw.WriteEndElement();   //Bytes

            tw.WriteEndElement();   //File
        }

        private void WriteFunction(XmlTextWriter tw, Function fn)
        {
            tw.WriteStartElement("Function");
            WriteXmlAttribute(tw, "Name", fn.name);
            WriteXmlAttribute(tw, "Schema", fn.schema);
            WriteXmlAttribute(tw, "Class", fn.assembly_class);
            WriteXmlAttribute(tw, "Method", fn.assembly_method);
            WriteXmlAttribute(tw, "Type", fn.type);
            WriteXmlAttribute(tw, "Status", fn.status.ToString());

            //Trigger stuff
            if (fn.type == "TA") WriteTrigger(tw, fn.trigger);

            //Parameters
            if (fn.type != "TA" && fn.type != "UDT")
            {
                tw.WriteStartElement("Parameters");
                foreach (Parameter p in fn.parameters)
                {
                    tw.WriteStartElement("Parameter");
                    WriteXmlAttribute(tw, "Name", p.name);
                    WriteXmlAttribute(tw, "Type", p.type);
                    WriteXmlAttribute(tw, "Size", p.max_length.ToString());
                    WriteXmlAttribute(tw, "Position", p.position.ToString());
                    WriteXmlAttribute(tw, "Default", p.default_value);
                    tw.WriteEndElement(); //Parameter
                }
                tw.WriteEndElement(); //Parameters
            }

            tw.WriteEndElement(); //Function
        }

        private bool WriteLibraryTo(TreeNode lNode, string file)
        {
            bool result = true;
            XmlTextWriter tw = null;

            try
            {
                Library lib = (Library)lNode.Tag;
                tw = new XmlTextWriter(file, null);
                tw.WriteStartDocument();
                tw.WriteStartElement("Library");
                WriteXmlAttribute(tw, "Name", lib.name);

                //Assemblies
                tw.WriteStartElement("Assemblies");
                foreach (InstalledAssembly am in lib.assemblies) WriteAssembly(tw, am, true);
                tw.WriteEndElement();   //Assemblies

                tw.WriteEndElement();   //Library
                tw.WriteEndDocument(); //
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Problem Encountered While Saving");
                result = false;
            }
            if (tw != null) tw.Close();
            return result;
        }

        private void WriteTrigger(XmlTextWriter tw, Trigger t)
        {
            tw.WriteStartElement("Trigger");
            WriteXmlAttribute(tw, "Disabled", t.disabled.ToString());
            WriteXmlAttribute(tw, "InsteadOf", t.insteadof.ToString());
            WriteXmlAttribute(tw, "Database", t.isdatabase.ToString());
            WriteXmlAttribute(tw, "Target", t.target);
            WriteXmlAttribute(tw, "Schema", t.target_schema);
            WriteXmlAttribute(tw, "Type", t.target_type);
            tw.WriteStartElement("Events");
            foreach (string s in t.events) tw.WriteElementString("Event", s);
            tw.WriteEndElement();
            tw.WriteEndElement();
        }

        private void WriteXmlAttribute(XmlTextWriter tw, string key, string value)
        {
            tw.WriteStartAttribute(key);
            tw.WriteString(value);
            tw.WriteEndAttribute();
        }

        #endregion Helper Functions

        #region Event Handlers

        public MainAssemblyMgrForm()
        {
            InitializeComponent();
        }

        private void AboutClick(object sender, EventArgs e)
        {
            AboutBox ab = new AboutBox();
            ab.ShowDialog();
        }

        private void actionHistoryViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            if (!mi.Checked)
            {
                if ((string)mi.Tag == "EXECUTE")
                {
                    viewAsRollBackActionsToolStripMenuItem.Checked = rollBackActionsToolStripMenuItem.Checked = false;
                    actionHistoryViewToolStripMenuItem.Checked = executedActionsToolStripMenuItem.Checked = true;
                    tvHistory.Tag = "EXECUTE";
                    foreach (TreeNode aNode in tvHistory.Nodes)
                    {
                        Action a = (Action)aNode.Tag;
                        aNode.Text = a.displaytext;
                        aNode.StateImageIndex = 6;
                    }
                    rollbackToHereToolStripMenuItem.Image = tbRollBackActionsToHere.Image = Resource.RollbackToHere;
                    tbRollBackActionsToHere.ToolTipText = "Roll Back Up To Selected Action";
                    rollbackToHereToolStripMenuItem.Text = "Roll Back Up To This Action";
                    tvHistory.TreeViewNodeSorter = new ActionSorter();
                }
                else
                {
                    viewAsRollBackActionsToolStripMenuItem.Checked = rollBackActionsToolStripMenuItem.Checked = true;
                    actionHistoryViewToolStripMenuItem.Checked = executedActionsToolStripMenuItem.Checked = false;
                    tvHistory.Tag = "ROLLBACK";
                    foreach (TreeNode aNode in tvHistory.Nodes)
                    {
                        Action a = (Action)aNode.Tag;
                        Action ra = InverseOfAction(a, null);
                        aNode.Text = ra.displaytext;
                        aNode.StateImageIndex = 5;
                    }
                    rollbackToHereToolStripMenuItem.Image = tbRollBackActionsToHere.Image = Resource.RollbackDownToHere;
                    tbRollBackActionsToHere.ToolTipText = "Roll Back Down To Selected Action";
                    rollbackToHereToolStripMenuItem.Text = "Roll Back Down To This Action";
                    tvHistory.TreeViewNodeSorter = new ActionReverser();
                }
                tvHistory.Sort();
            }
        }

        private void afterDatabaseToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            TreeNode fNode = (TreeNode)cmFunction.Tag;
            Function f = (Function)fNode.Tag;
            Server s = f.assembly.database.server;

            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem.Tag != s)
            {
                me.OwnerItem.Tag = s;
                populateDdlTriggerToolStripMenuItems(me);
            }

            bool checkt, allcheckt = true;

            changingChecks = true;
            foreach (ToolStripItem tsi in me.DropDownItems)
            {
                if (tsi.GetType().Name == "ToolStripMenuItem")
                {
                    checkt = false;
                    ToolStripMenuItem tsmi = (ToolStripMenuItem)tsi;
                    if (me.Checked && f.trigger.events.Contains((string)tsmi.Tag)) tsmi.Checked = true;
                    else
                    {
                        foreach (ToolStripItem smi in tsmi.DropDownItems)
                        {
                            if (smi.GetType().Name == "ToolStripMenuItem")
                            {
                                ToolStripMenuItem stsmi = (ToolStripMenuItem)smi;
                                if (me.Checked && f.trigger.events.Contains((string)stsmi.Tag))
                                {
                                    ((ToolStripMenuItem)stsmi).Checked = true;
                                    checkt = true;
                                }
                                else
                                {
                                    stsmi.Checked = false;
                                    if (!((string)stsmi.Tag).Contains('*')) allcheckt = false;
                                }
                            }
                        }
                        tsmi.Checked = checkt;
                    }
                    switch (tsmi.Name)
                    {
                        case "tbOnCreate": tbCreateAll.Checked = allcheckt; break;
                        case "tbOnAlter": tbAlterAll.Checked = allcheckt; break;
                        case "tbOnDrop": tbDropAll.Checked = allcheckt; break;
                        case "createToolStripMenuItem": allItemsInCreateListToolStripMenuItem.Checked = allcheckt; break;
                        case "alterToolStripMenuItem": allItemsInAlterListToolStripMenuItem.Checked = allcheckt; break;
                        case "dropToolStripMenuItem": allItemsInDropListToolStripMenuItem.Checked = allcheckt; break;
                        case "triggerOnCreateMenuItem": allItemsInTriggerOnCreateMenu.Checked = allcheckt; break;
                        case "triggerOnAlterMenuItem": allItemsInTriggerOnAlterMenu.Checked = allcheckt; break;
                        case "triggerOnDropMenuItem": allItemsInTriggerOnDropMenu.Checked = allcheckt; break;
                    }
                }
            }
            changingChecks = false;
        }

        private void afterTableToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            changingChecks = true;
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            TreeNode tn = (TreeNode)cmFunction.Tag;
            Function f = (Function)tn.Tag;
            if (((string)mi.Tag == "INSTEAD OF" && f.trigger.insteadof)
                || ((string)mi.Tag == "AFTER" && !f.trigger.insteadof))
            {
                ((ToolStripMenuItem)mi.DropDownItems[0]).Checked = f.trigger.events.Contains("INSERT");
                ((ToolStripMenuItem)mi.DropDownItems[1]).Checked = f.trigger.events.Contains("UPDATE");
                ((ToolStripMenuItem)mi.DropDownItems[2]).Checked = f.trigger.events.Contains("DELETE");
            }
            else
            {
                ((ToolStripMenuItem)mi.DropDownItems[0]).Checked =
                ((ToolStripMenuItem)mi.DropDownItems[1]).Checked =
                ((ToolStripMenuItem)mi.DropDownItems[2]).Checked = false;
            }
            changingChecks = false;
        }

        private void assemblyPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode;

            ToolStripItem me = (ToolStripItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY ASSEMBLY") amNode = (TreeNode)cmLibraryAssembly.Tag;
            else amNode = (TreeNode)cmAssembly.Tag;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            propbox.Tag = am;
            propbox.ShowDialog(this);
        }

        private void bAddAssemblies_Click(object sender, EventArgs e)
        {
            TreeNode lbNode;

            if (tvAssemblies.Nodes.Count == 1) lbNode = tvAssemblies.Nodes[0];
            else
            {
                if (tvAssemblies.SelectedNode == null)
                {
                    MessageBox.Show(Resource.errSelectAssemblyBeforeAddingFile, "Select Assembly First");
                    return;
                }
                lbNode = tvAssemblies.SelectedNode;
                while (lbNode.Level > 0) lbNode = lbNode.Parent;
            }
            AddAssembliesToLibrary(lbNode);
        }

        private void bAdd_Click(object sender, EventArgs e)
        {
            TreeNode amNode;

            if (tvAssemblies.SelectedNode == null || tvAssemblies.SelectedNode.Level == 0)
            {
                MessageBox.Show(Resource.errSelectAssemblyBeforeAddingFile, "Select Assembly First");
                return;
            }
            amNode = tvAssemblies.SelectedNode;
            while (amNode.Level > 1) amNode = amNode.Parent;
            AddFilesToAssemblyInLibrary(amNode);
        }

        private void cancelAllActionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CancelAllActions(true);
        }

        private void cancelActionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Cancelling Action..."))
            {
                TreeNode node = (TreeNode)cmAction.Tag;
                Action a = (Action)node.Tag;
                SetPBar(a.subactions.Count + 1);
                ReverseAction(a);
            }
        }

        private void cloneTriggerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fNode = (TreeNode)cmFunction.Tag;
            cloneTrigger(fNode);
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox rtb = (RichTextBox)cmScript.Tag;
            UltraTabPageControl page = (UltraTabPageControl)rtb.Parent.Parent;
            UltraTab tab = (UltraTab)page.Tab;
            int i = tab.Index;
            if (i == 3) i = 1;
            ultraTabControl1.Tabs.Remove(tab);
            ultraTabControl1.SelectedTab = ultraTabControl1.Tabs[i - 1];
        }

        private void closeToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TreeNode lNode = (TreeNode)cmLibrary.Tag;
            closeLibrary(lNode);
        }

        private void cmActionPane_Opening(object sender, CancelEventArgs e)
        {
            saveAllActionsToFileToolStripMenuItem.Enabled = (tvActions.Nodes.Count > 0);
            executeAllActionsToolStripMenuItem.Enabled = (tvActions.Nodes.Count > 0);
            cancelAllActionsToolStripMenuItem.Enabled = (tvActions.Nodes.Count > 0);
            scriptAllActionsToolStripMenuItem.Enabled = (tvActions.Nodes.Count > 0);
            loadActionsFromFileToolStripMenuItem.Enabled = (tvServers.Nodes.Count > 0);
        }

        private void cmLibraryPane_Opening(object sender, CancelEventArgs e)
        {
            saveAllLibrariesToolStripMenuItem.Enabled = tvAssemblies.Nodes.Count > 0;
        }

        private void cmServerPane_Opening(object sender, CancelEventArgs e)
        {
            refreshAllToolStripMenuItem.Enabled = tvServers.Nodes.Count > 0;
            disconnectAllToolStripMenuItem.Enabled = tvServers.Nodes.Count > 0;
            saveConnectionSettingsToolStripMenuItem.Enabled = tvServers.Nodes.Count > 0;
        }

        private void connectToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private void contentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string cmd = Path.GetDirectoryName(Application.ExecutablePath) + @"\AssemblyMgr.chm";
            Help.ShowHelp(this, cmd, HelpNavigator.TableOfContents);
        }

        private void copyAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Copy"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                if ((string)mi.Tag == "Database Assembly")
                    item = (TreeNode)cmAssembly.Tag;
                else if ((string)mi.Tag == "Library Assembly")
                    item = (TreeNode)cmLibraryAssembly.Tag;
                else return;
                InstalledAssembly am = (InstalledAssembly)item.Tag;
                DataObject o = new DataObject();
                o.SetData("Assembly", new InstalledAssembly(am));
                Clipboard.SetDataObject(o);
            }
        }

        private void copyFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Copy"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                if ((string)mi.Tag == "Database File")
                    item = (TreeNode)cmFile.Tag;
                else if ((string)mi.Tag == "Library File")
                    item = (TreeNode)cmLibraryFile.Tag;
                else return;
                InstalledAssembly file = (InstalledAssembly)item.Tag;
                DataObject o = new DataObject();
                o.SetData("File", new InstalledAssembly(file));
                Clipboard.SetDataObject(o);
            }
        }

        private void copyFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Copy"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                if ((string)mi.Tag == "Database Function")
                    item = (TreeNode)cmFunction.Tag;
                else if ((string)mi.Tag == "Library Function")
                    item = (TreeNode)cmLibraryFunction.Tag;
                else return;
                Function fn = (Function)item.Tag;
                DataObject o = new DataObject();
                o.SetData("Assembly", new InstalledAssembly(fn.assembly));
                o.SetData("Function", fn);
                Clipboard.SetDataObject(o);
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Copy"))
            {
                RichTextBox rtb = (RichTextBox)cmScript.Tag;
                rtb.Copy();
            }
        }

        private void cutAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Cut"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                if ((string)mi.Tag == "Database Assembly")
                {
                    item = (TreeNode)cmAssembly.Tag;
                    copyAssemblyToolStripMenuItem_Click(sender, e);
                    DropAssembly(item, null, true);
                }
                else if ((string)mi.Tag == "Library Assembly")
                {
                    item = (TreeNode)cmLibraryAssembly.Tag;
                    copyAssemblyToolStripMenuItem_Click(sender, e);
                    DropAssemblyFromLibrary(item);
                }
            }
        }

        private void cutFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Cut"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                if ((string)mi.Tag == "Database File")
                {
                    item = (TreeNode)cmFile.Tag;
                    copyFileToolStripMenuItem_Click(sender, e);
                    RemoveFileFromAssembly(item, null, true);
                }
                else if ((string)mi.Tag == "Library File")
                {
                    item = (TreeNode)cmLibraryFile.Tag;
                    copyFileToolStripMenuItem_Click(sender, e);
                    RemoveFileFromAssemblyInLibrary(item);
                }
            }
        }

        private void cutFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Cut"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                if ((string)mi.Tag == "Database Function")
                {
                    item = (TreeNode)cmFunction.Tag;
                    copyFunctionToolStripMenuItem_Click(sender, e);
                    DropFunction(item, null, true);
                }
                else if ((string)mi.Tag == "Library Function")
                {
                    item = (TreeNode)cmLibraryFunction.Tag;
                    copyFunctionToolStripMenuItem_Click(sender, e);
                    DropLibraryFunction(item);
                }
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Cut"))
            {
                RichTextBox rtb = (RichTextBox)cmScript.Tag;
                rtb.Cut();
            }
        }

        private void databasePasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Paster"))
            {
                TreeNode item = (TreeNode)cmDatabase.Tag;
                DataObject o = (DataObject)Clipboard.GetDataObject();
                if (o != null && o.GetType().Name == "DataObject") PasteIntoDatabase((DataObject)o, item, 0);
            }
        }

        private void disconnectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tvActions.Nodes.Count > 0)
            {
                DialogResult dr = MessageBox.Show(Resource.mbxDisconnectAllWithPendingActions, "Disconnect All Servers", MessageBoxButtons.YesNo);
                if (dr == DialogResult.No) return;
            }
            tvServers.Nodes.Clear();
            tvActions.Nodes.Clear();
            tvHistory.Nodes.Clear();
            connections.Clear();
            tvServers_NodeRemoved();
        }

        private void disconnectToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TreeNode servernode = (TreeNode)cmServer.Tag;
            Server svr = (Server)servernode.Tag;
            List<Action> al = GetActionsFor(svr);
            if (al.Count > 0)
            {
                DialogResult dr = MessageBox.Show(Resource.mbxCancelSvrActionsPrompt, "Pending Actions Outstanding", MessageBoxButtons.YesNo);
                if (dr == DialogResult.No) return;
            }
            foreach (Action a in al) ReverseAction(a);
            servernode.Remove();
            connections.Remove(svr.connection);
            tvServers_NodeRemoved();
        }

        private void databaseDefaultSchemaToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem schemaitem, menu;

            TreeNode dbNode = (TreeNode)cmDatabase.Tag;
            Database database = (Database)dbNode.Tag;
            menu = (ToolStripMenuItem)sender;
            menu.DropDownItems.Clear();
            foreach (Schema schema in database.schemas)
            {
                schemaitem = (ToolStripMenuItem)menu.DropDownItems.Add(schema.name);
                if (schema.name == database.default_schema) schemaitem.Checked = true;
                schemaitem.Click += new EventHandler(schemaitem_Click);
            }
        }

        private void databasePropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode dbNode = (TreeNode)cmDatabase.Tag;
            Database db = (Database)dbNode.Tag;
            propbox.Tag = db;
            propbox.ShowDialog(this);
        }

        private void dropAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem me = (ToolStripItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY ASSEMBLY")
            {
                removeAssemblyFromLibraryToolStripMenuItem_Click(sender, e);
                return;
            }

            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            Action act = DropAssembly(amNode, null, true);
            toggleServerPaneControls(tvServers.SelectedNode);
        }

        private void enableClrToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode sNode = (TreeNode)cmServer.Tag;
            toggleCLREnabled(sNode, null);
        }

        private void executeAllActionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExecuteAllActions();
        }

        private void executeNowToolStripMenuItem_Click(object sender, EventArgs e)
        {

            using (new HourGlass("Executing Selected Action..."))
            {
                EnableStop();
                TreeNode actionNode = (TreeNode)cmAction.Tag;
                Action act = (Action)actionNode.Tag;
                SetPBar(act.subactions.Count + 1);
                ExecuteAction(act, true);
            }
        }

        private void executeToHereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode hNode = (TreeNode)cmAction.Tag;
            if (hNode.Level > 0) return;
            int j = hNode.Index;

            EnableStop();
            using (new HourGlass("Executing Actions..."))
            {
                SetPBar(tvActions.Nodes.Count - j);
                for (int i = 0; i < j; i++)
                {
                    if (Stop) break;
                    Action a = (Action)tvActions.Nodes[i].Tag;
                    ExecuteAction(a, true);
                }
            }
            DisableStop();
        }

        private void executeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox rtb = (RichTextBox)cmScript.Tag;
            string tabtext = ((UltraTabPageControl)rtb.Parent.Parent).Tab.Text;
            Match m = Regex.Match(tabtext, @"Generated Script \(\d+\) - (.*)$");
            string servername = m.Groups[1].Value;
            Server svr = null;
            foreach (TreeNode tn in tvServers.Nodes)
            {
                if (((Server)tn.Tag).name == servername)
                {
                    svr = (Server)tn.Tag;
                    break;
                }
            }
            if (svr == null) MessageBox.Show(Resource.errNoServerToExecuteAgainst.Replace("%SERVER%", servername), "Server Not Connected");
            else
            {
                DialogResult dr = MessageBox.Show(Resource.mbxNoRollbackWithScriptExecution, "No Roll-Back From Executed Scripts", MessageBoxButtons.YesNo);
                if (dr == DialogResult.No) return;
                using (new HourGlass("Executing Script..."))
                {
                    string[] ca = rtb.Text.Split(new string[] { "\nGO\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                    SetPBar(ca.Count());
                    bool result = true;
                    Database db = null;
                    foreach (string cs in ca)
                    {
                        while (true)
                        {
                            result = true;
                            if (cs.StartsWith("USE ")) db = svr.databases.FirstOrDefault(p => p.name == cs.Substring(5).Replace("]", ""));
                            else result = svr.ExecuteCommand(cs, db);
                            if (!result)
                            {
                                string message = cs;
                                m = Regex.Match(cs, "0x[\\dA-F]+", RegexOptions.None);
                                if (m.Success) message = message.Replace(m.Value, "{bytes}");
                                dr = MessageBox.Show(svr.LastError + "\n" + "Command:\n" + message, "Action Failed", MessageBoxButtons.AbortRetryIgnore);
                                if (dr == DialogResult.Abort) return;
                                if (dr == DialogResult.Ignore) break;
                            }
                            else
                            {
                                pBar.PerformStep();
                                break;
                            }
                        }
                    }
                    RefreshServer(GetNodeFor(svr));
                }
            }
        }

        private void exportLibraryAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            ExportAssembly(amNode);
        }

        private void exportToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY ASSEMBLY")
            {
                exportLibraryAssemblyToolStripMenuItem_Click(sender, e);
                return;
            }

            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            ExportAssembly(amNode);
        }

        private void filePropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fNode = (TreeNode)cmFile.Tag;
            InstalledAssembly af = (InstalledAssembly)fNode.Tag;
            propbox.Tag = af;
            propbox.ShowDialog(this);
        }

        private void functionParameterDefaultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem ti = (ToolStripItem)sender;
            if (ti.OwnerItem != null && ti.OwnerItem.Tag != null && (string)ti.OwnerItem.Tag == "LIBRARY FUNCTION")
            {
                libraryFunctionParameterDefaultsToolStripMenuItem_Click(sender, e);
                return;
            }

            TreeNode fnNode = (TreeNode)cmFunction.Tag;
            parmbox.Tag = fnNode;
            parmbox.ShowDialog(this);
        }

        private void functionPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fNode = (TreeNode)cmFunction.Tag;
            Function f = (Function)fNode.Tag;
            propbox.Tag = f;
            propbox.ShowDialog(this);
        }

        private void functionSchemaToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem schemaitem, menu;

            TreeNode fNode = (TreeNode)cmFunction.Tag;
            Function function = (Function)fNode.Tag;
            menu = (ToolStripMenuItem)sender;

            menu.DropDownItems.Clear();
            foreach (Schema schema in function.assembly.database.schemas)
            {
                schemaitem = (ToolStripMenuItem)menu.DropDownItems.Add(schema.name);
                if (schema.name == function.schema) schemaitem.Checked = true;
                schemaitem.Click += new EventHandler(schemaitem_Click);
            }

        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = (TreeNode)cmDatabase.Tag;
            HideDatabase(node);
        }

        private void importAssemblyFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode dbNode = (TreeNode)cmDatabase.Tag;
            AddAssembliesToDatabase(dbNode);
        }

        private void importAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode lNode = (TreeNode)cmLibrary.Tag;
            AddAssembliesToLibrary(lNode);
        }

        private void importAssociatedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY ASSEMBLY")
            {
                bAdd_Click(sender, e);
                return;
            }

            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            AddFilesToAssemblyInDatabase(amNode);
        }

        private void installAllObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY ASSEMBLY")
            {
                setAllObjectsToInstallToolStripMenuItem_Click(sender, e);
                return;
            }

            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            InstalledAssembly assembly = (InstalledAssembly)amNode.Tag;
            Action act = AddAllObjects(amNode, null);
        }

        private void installLibraryFunctionByDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string type = (string)((ToolStripItem)sender).Tag;
            if (type == "LibraryFunction")
            {
                TreeNode fnNode = (TreeNode)cmLibraryFunction.Tag;
                AddOrRemoveLibraryFunction(fnNode);
            }
            else
            {
                TreeNode afNode = (TreeNode)cmLibraryFile.Tag;
                RemoveFileFromAssemblyInLibrary(afNode);
            }
        }

        private void libraryAssemblyPermissionSetToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            foreach (ToolStripMenuItem i in libraryAssemblyPermissionSetToolStripMenuItem.DropDownItems)
            {
                if (int.Parse((string)i.Tag) == am.permission_set) i.Checked = true;
                else i.Checked = false;
            }
        }

        private void libraryAssemblyPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {

            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            propbox.Tag = am;
            propbox.ShowDialog(this);
        }

        private void libraryAssemblySafeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            ToolStripMenuItem i = (ToolStripMenuItem)sender;
            ChangeLibraryPermissionSet(amNode, int.Parse((string)i.Tag));
        }

        private void libraryFunctionParameterDefaultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fnNode = (TreeNode)cmLibraryFunction.Tag;
            parmbox.Tag = fnNode;
            parmbox.ShowDialog(this);
        }

        private void libraryPasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Paste"))
            {
                TreeNode item = (TreeNode)cmLibrary.Tag;
                DataObject o = (DataObject)Clipboard.GetDataObject();
                if (o != null && o.GetType().Name == "DataObject") PasteIntoLibrary((DataObject)o, ref item, 0);
            }
        }

        private void loadActionsFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadActions();
        }

        private void loadConnectionSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadConnectionSettings();
        }

        private void MainAssemblyMgrForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            us.Save();
            SaveDefaultSchemas();
            purgeSQLFiles();
        }

        private void MainAssemblyMgrForm_Load(object sender, EventArgs e)
        {
            Properties.Settings.Default.Upgrade();
            tvServers.TreeViewNodeSorter = (System.Collections.IComparer)new NodeSorter();
            tvHistory.TreeViewNodeSorter = (System.Collections.IComparer)new ActionReverser();
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(CurrentDomain_ReflectionOnlyAssemblyResolve);
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            //Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            us.Load();
            ReadDefaultSchemas();
            fontRegular = new Font(tvServers.Font, FontStyle.Regular);
            fontItalic = new Font(tvServers.Font, FontStyle.Italic);
            fontStrikeout = new Font(tvServers.Font, FontStyle.Strikeout);
            fontCourier = new Font("Courier New", 10);

            DateTime max = new DateTime(2100, 12, 31);
            if (us.LicenseFound()) us.LicenseState = us.CheckLicense(us.License);
            if (!us.LicenseFound() || !us.LicenseState.LicenseValid || us.LicenseState.LicenseExpires < max)
            //string id = System.Environment.MachineName;
            //bool needsActivation = false;
            //string returnMsg = "";
            //bool ret = lv.ValidateLicenseAtStartup(id, ref needsActivation, ref returnMsg);
            //if (!ret || needsActivation)
            {
                LicenseForm frm = new LicenseForm();
                DialogResult dr = frm.ShowDialog(this);
                if (dr == System.Windows.Forms.DialogResult.Cancel) Application.Exit();
            }
        }

        private void mergeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode lNode = (TreeNode)cmLibrary.Tag;
            MergeLibrary(lNode);
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = 1;
            while (tvAssemblies.Nodes.ContainsKey("New Library" + i.ToString())) i++;
            string name = "New Library" + i.ToString();
            TreeNode lNode = tvAssemblies.Nodes.Add(name, name, 12, 12);
            Library l = new Library();
            l.name = name;
            lNode.Tag = l;
            lNode.ContextMenuStrip = cmLibrary;
            lNode.ToolTipText = "Library: " + l.name + " (" + (l.file == null ? "not saved" : l.file) + ")";
            tvAssemblies.SelectedNode = lNode;
            lNode.BeginEdit();
        }

        private void onToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            TreeNode fnNode = (TreeNode)cmFunction.Tag;
            Function fn = (Function)fnNode.Tag;
            Database db = fn.assembly.database;
            ((ToolStripMenuItem)me.DropDownItems[0]).Checked = fn.trigger.isdatabase;
            if (db.tables == null) db.GetTables();
            int j = me.DropDownItems.Count;
            for (int i = 2; i < j; i++) me.DropDownItems.RemoveAt(2);
            foreach (string s in db.tables)
            {
                ToolStripMenuItem ni = new ToolStripMenuItem(s);
                if (s.Substring(0, 6) == "(view)") ni.Image = Resource.View;
                else ni.Image = Resource.Table;
                me.DropDownItems.Add(ni);
                if (fn.trigger.target_schema + "." + fn.trigger.target == s.Substring(s.IndexOf(' ') + 1)) ni.Checked = true;
                ni.Click += new EventHandler(triggerTarget_Click);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenLibrary();
        }

        private void pasteAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Paste"))
            {
                TreeNode item;
                ToolStripItem mi = (ToolStripItem)sender;
                object o = Clipboard.GetDataObject();
                if ((string)mi.Tag == "Database Assembly")
                {
                    item = (TreeNode)cmAssembly.Tag;
                    PasteIntoDatabase((DataObject)o, item, 0);
                }
                else if ((string)mi.Tag == "Library Assembly")
                {
                    item = (TreeNode)cmLibraryAssembly.Tag;
                    PasteIntoLibrary((DataObject)o, ref item, 0);
                }
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Paste"))
            {
                RichTextBox rtb = (RichTextBox)cmScript.Tag;
                rtb.Paste();
            }
        }

        private void refreshAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshAllServers();
            toggleServerPaneControls(tvServers.SelectedNode);
        }

        private void refreshDatabase_Click(object sender, EventArgs e)
        {
            TreeNode dbNode = (TreeNode)cmDatabase.Tag;
            RefreshDatabase(dbNode);
            toggleServerPaneControls(tvServers.SelectedNode);
        }

        private void refreshServer_Click(object sender, EventArgs e)
        {
            TreeNode sNode = (TreeNode)cmServer.Tag;
            RefreshServer(sNode);
            toggleServerPaneControls(tvServers.SelectedNode);
        }

        private void reinstateAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            UndropAssembly(amNode, null);
        }

        private void removeAssemblyFromLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            DropAssemblyFromLibrary(amNode);
        }

        private void removeFileFromLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fnode = (TreeNode)cmLibraryFile.Tag;
            RemoveFileFromAssemblyInLibrary(fnode);
        }

        private void removeFileFromDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fNode;

            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem != null && (string)me.OwnerItem.Tag == "LIBRARY FILE")
            {
                removeFileFromLibraryToolStripMenuItem_Click(sender, e);
                return;
            }

            fNode = (TreeNode)cmFile.Tag;
            InstalledAssembly af = (InstalledAssembly)fNode.Tag;
            switch (af.status)
            {
                case installstatus.in_place:
                    RemoveFileFromAssembly(fNode, null, true);
                    break;
                case installstatus.pending_add:
                case installstatus.pending_remove:
                    Action act = af.actions.Single();
                    ReverseAction(act);
                    break;
            }
            toggleFileControls(af);
        }

        private void renameFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode fnNode;
            ToolStripItem me = (ToolStripItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY FUNCTION") fnNode = (TreeNode)cmLibraryFunction.Tag;
            else fnNode = (TreeNode)cmFunction.Tag;
            fnNode.BeginEdit();
        }

        private void renameLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender.GetType().Name == "ToolStripButton" && (string)((ToolStripButton)sender).Tag == "Function"
                || sender.GetType().Name == "ToolStripMenuItem" && (string)((ToolStripMenuItem)sender).Tag == "Function")
            {
                TreeNode fnNode = (TreeNode)cmLibraryFunction.Tag;
                fnNode.BeginEdit();
            }
            else
            {
                TreeNode lNode = (TreeNode)cmLibrary.Tag;
                lNode.BeginEdit();
            }
        }

        private void safeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (changingChecks) return;
            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem.OwnerItem != null && me.OwnerItem.OwnerItem.Tag != null && (string)me.OwnerItem.OwnerItem.Tag == "LIBRARY ASSEMBLY")
            {
                libraryAssemblySafeToolStripMenuItem_Click(sender, e);
                return;
            }

            if (me.Checked)
            {
                foreach (ToolStripMenuItem ti in me.Owner.Items)
                {
                    if (ti != me) ti.Checked = false;
                }
                TreeNode node = (TreeNode)cmAssembly.Tag;
                InstalledAssembly assembly = (InstalledAssembly)node.Tag;
                int selectedset = int.Parse((string)me.Tag);

                if (selectedset != assembly.permission_set)
                {
                    ChangePermissionSet(node, null, selectedset);
                }
            }
        }

        private void saveAllActionsToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAllActions();
        }

        private void saveAllLibrariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (TreeNode tn in tvAssemblies.Nodes) SaveLibrary(tn);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode lNode = (TreeNode)cmLibrary.Tag;
            SaveLibraryAs(lNode);
        }

        private void saveConnectionSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveConnectionSettings();
        }

        private void saveToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RichTextBox rtb = (RichTextBox)cmScript.Tag;
            SendScriptTo(rtb.Text, "File");
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode lNode = (TreeNode)cmLibrary.Tag;
            SaveLibrary(lNode);
        }

        private void schemaitem_Click(object sender, EventArgs e)
        {
            TreeNode node;

            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            foreach (ToolStripMenuItem tsmi in item.Owner.Items) if (tsmi != item && tsmi.Checked) tsmi.Checked = false;
            item.Checked = true;
            if ((string)cmServerPane.Tag == "Function" && (string)item.OwnerItem.Tag != "DATABASE")
            {
                node = (TreeNode)cmFunction.Tag;
                ChangeFunctionSchema(node, null, item.Text);
            }
            else
            {
                node = (TreeNode)cmDatabase.Tag;
                Action act = ChangeDatabaseDefaultSchema(node, null, item.Text, true);
            }
        }

        private void scriptAllActionsToXXXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            string destination = (string)mi.Tag;
            string direction = (string)mi.OwnerItem.Tag;
            string source = (string)mi.OwnerItem.OwnerItem.Tag;

            TreeNodeCollection tnc = null;
            if (source == "HISTORY") tnc = tvHistory.Nodes;
            else tnc = tvActions.Nodes;

            using (new HourGlass("Generating Scripts...")) ScriptAllActionsTo(destination, direction, tnc);
        }

        private void scriptActionToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode actionNode = null;
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            string destination = (string)mi.Tag;
            string direction = (string)mi.OwnerItem.Tag;
            string source = (string)mi.OwnerItem.OwnerItem.Tag;
            if (source == "ACTION") actionNode = (TreeNode)cmAction.Tag;
            else actionNode = (TreeNode)cmHistoryAction.Tag;
            ScriptAction(actionNode, destination, direction);
        }

        private void scriptTBEnter(object sender, MouseEventArgs e)
        {
            RichTextBox tb = (RichTextBox)sender;
            cmScript.Tag = tb;
            tbScriptCut.Enabled = tbScriptCopy.Enabled = cutToolStripMenuItem.Enabled = copyToolStripMenuItem.Enabled = tb.SelectionLength > 0;
            tbScriptPaste.Enabled = pasteToolStripMenuItem.Enabled = Clipboard.ContainsText() || Clipboard.ContainsData("Rtf");
        }

        private void scriptXXXAsYYYToZZZToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            string type = (string)mi.OwnerItem.OwnerItem.Tag;
            string action = (string)mi.OwnerItem.Tag;
            string destination = (string)mi.Tag;

            using (new HourGlass("Generating Scripts..."))
            {
                switch (type)
                {
                    case "Function": ScriptFunctionAsTo(action, destination, false); break;
                    case "Assembly": ScriptAssemblyAsTo(action, destination, false); break;
                    case "File": ScriptFileAsTo(action, destination, false); break;
                    case "LibraryFunction": ScriptFunctionAsTo(action, destination, true); break;
                    case "LibraryAssembly": ScriptAssemblyAsTo(action, destination, true); break;
                    case "LibraryFile": ScriptFileAsTo(action, destination, true); break;
                }
            }
        }

        private void serverPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode sNode = (TreeNode)cmServer.Tag;
            Server server = (Server)sNode.Tag;
            propbox.Tag = server;
            propbox.ShowDialog(this);
        }

        private void setAllObjectsToInstallToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            foreach (TreeNode tn in amNode.Nodes)
            {
                if (tn.Tag.GetType().Name == "Function") AddOrRemoveLibraryFunction(tn);
            }
            toggleLibraryAssemblyControls(am);
        }

        private void setAllObjectsToNotInstallToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmLibraryAssembly.Tag;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            foreach (TreeNode tn in amNode.Nodes)
            {
                if (tn.Tag.GetType().Name == "Function") DropLibraryFunction(tn);
            }
            toggleLibraryAssemblyControls(am);
        }

        private void setTrustworthyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode dbNode = (TreeNode)cmDatabase.Tag;
            ToggleTrustworthy(dbNode, null);
        }

        private void showHideDatabaseClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            Database database = (Database)item.Tag;
            TreeNode servernode = (TreeNode)cmServer.Tag;
            Server server = (Server)servernode.Tag;

            if (item.Checked)
            {
                if (database.changes_pending)
                {
                    MessageBox.Show(Resource.errCantHideDatabaseWithPendingActions, "Hide Database");
                    return;
                }
                database.show = false;
                servernode.Nodes.RemoveByKey(database.name);
                item.Checked = false;
            }
            else
            {
                database.show = true;
                AddDatabaseToTree(servernode, database);
                item.Checked = false;
            }

            for (int i = 0; i < server.databases.Count; i++)
            {
                if (server.databases[i].name == database.name)
                {
                    server.databases.RemoveAt(i);
                    server.databases.Insert(i, database);
                    break;
                }
            }
            tvServers.Sort();
        }

        private void showHideDatabasesToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            TreeNode sNode = (TreeNode)cmServer.Tag;
            Server server = (Server)sNode.Tag;
            ToolStripMenuItem menu = (ToolStripMenuItem)sender;

            menu.DropDownItems.Clear();
            foreach (Database db in server.databases)
            {
                ToolStripMenuItem mi = new ToolStripMenuItem(db.name);
                mi.Checked = db.show;
                if (db.show) mi.Image = Resource.Database;
                else mi.Image = Resource.HideDatabase1;
                mi.Click += new EventHandler(showHideDatabaseClick);
                mi.Tag = db;
                menu.DropDownItems.Add(mi);
            }

        }

        private void splitContainer2_SplitterMoved(object sender, SplitterEventArgs e)
        {
            int x1 = splitContainer2.SplitterDistance;
            int x2 = tsServers.Width;
            int x = x1 > x2 ? x1 : x2;
            tsActions.Left = tsHistory.Left = tsLibrary.Left = tsScript.Left = x;
        }

        private void tbAudit_Click(object sender, EventArgs e)
        {
            using (new HourGlass("Auditing..."))
            {
                Audit(tvServers.Nodes);
            }
        }

        private void tbConnect_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private void tbCopy_Click(object sender, EventArgs e)
        {
            ToolStripItem mi = (ToolStripItem)sender;
            switch ((string)mi.Tag)
            {
                case "Database Assembly": copyAssemblyToolStripMenuItem_Click(sender, e); break;
                case "Database Function": copyFunctionToolStripMenuItem_Click(sender, e); break;
                case "Database File": copyFileToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbCut_Click(object sender, EventArgs e)
        {
            ToolStripItem mi = (ToolStripItem)sender;
            switch ((string)mi.Tag)
            {
                case "Database Assembly": cutAssemblyToolStripMenuItem_Click(sender, e); break;
                case "Database Function": cutFunctionToolStripMenuItem_Click(sender, e); break;
                case "Database File": cutFileToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbDeleteAll_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show(Resource.mbxCancelAllActionsPrompt, "Delete All Actions", MessageBoxButtons.YesNo);
            if (dr == DialogResult.Yes) tvHistory.Nodes.Clear();
            tvHistory_NodeAdded();
        }

        private void tbDeleteHistoryAction_Click(object sender, EventArgs e)
        {
            TreeNode hNode = (TreeNode)cmHistoryAction.Tag;
            hNode.Remove();
            tvHistory_NodeAdded();
        }

        private void tbDropFunction_Click(object sender, EventArgs e)
        {
            TreeNode node;

            ToolStripItem ti = (ToolStripItem)sender;
            if (ti.OwnerItem != null && ti.OwnerItem.Tag != null && (string)ti.OwnerItem.Tag == "LIBRARY FUNCTION")
            {
                installLibraryFunctionByDefaultToolStripMenuItem_Click(sender, e);
                return;
            }

            if ((string)ti.Tag == "FUNCTION")
            {
                node = (TreeNode)cmFunction.Tag;
                Function function = (Function)node.Tag;
                switch (function.status)
                {
                    case installstatus.in_place:
                    case installstatus.pending_add:
                        DropFunction(node, null, true);
                        break;
                    case installstatus.pending_remove:
                    case installstatus.not_installed:
                        AddFunction(node, null, true);
                        break;
                }
            }
            else
            {
                node = (TreeNode)cmFile.Tag;
                InstalledAssembly la = (InstalledAssembly)node.Tag;
                switch (la.status)
                {
                    case installstatus.in_place:
                    case installstatus.pending_add:
                        RemoveFileFromAssembly(node, null, true);
                        break;
                    case installstatus.pending_remove:
                        Action act = la.actions.Single();
                        ReverseAction(act);
                        break;
                }
                toggleFileControls(la);
            }
        }

        private void tbKey_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            string s = (string)tbKey.Tag;
            switch (s)
            {
                case "CREATE":
                    AddAsymmetricKeyAndLogin(amNode, null);
                    break;
                case "DROP":
                    DropAsymmetricKeyAndLogin(amNode, null);
                    break;
            }
        }

        private void tbLibraryCut_Click(object sender, EventArgs e)
        {
            ToolStripItem mi = (ToolStripItem)sender;
            switch ((string)mi.Tag)
            {
                case "Library Assembly": cutAssemblyToolStripMenuItem_Click(sender, e); break;
                case "Library Function": cutFunctionToolStripMenuItem_Click(sender, e); break;
                case "Library File": cutFileToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbLibraryCopy_Click(object sender, EventArgs e)
        {
            ToolStripItem mi = (ToolStripItem)sender;
            switch ((string)mi.Tag)
            {
                case "Library Assembly": copyAssemblyToolStripMenuItem_Click(sender, e); break;
                case "Library Function": copyFunctionToolStripMenuItem_Click(sender, e); break;
                case "Library File": copyFileToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbLibraryPaste_Click(object sender, EventArgs e)
        {
            ToolStripItem mi = (ToolStripItem)sender;
            switch ((string)mi.Tag)
            {
                case "Library": libraryPasteToolStripMenuItem_Click(sender, e); break;
                case "Library Assembly": pasteAssemblyToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbPaste_Click(object sender, EventArgs e)
        {
            ToolStripItem mi = (ToolStripItem)sender;
            switch ((string)mi.Tag)
            {
                case "Database": databasePasteToolStripMenuItem_Click(sender, e); break;
                case "Database Assembly": pasteAssemblyToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbProperties_Click(object sender, EventArgs e)
        {
            switch ((string)cmServerPane.Tag)
            {
                case "Server": serverPropertiesToolStripMenuItem_Click(sender, e); break;
                case "Database": databasePropertiesToolStripMenuItem_Click(sender, e); break;
                case "Assembly": assemblyPropertiesToolStripMenuItem_Click(sender, e); break;
                case "Function": functionPropertiesToolStripMenuItem_Click(sender, e); break;
                case "File": filePropertiesToolStripMenuItem_Click(sender, e); break;
            }
        }

        private void tbRefresh_Click(object sender, EventArgs e)
        {
            if (cmServerPane.Tag == null)
            {
                refreshAllToolStripMenuItem_Click(sender, e);
                return;
            }
            switch ((string)cmServerPane.Tag)
            {
                case "Server":
                    refreshServer_Click(sender, e);
                    break;
                case "Database":
                case "Assembly":
                case "Function":
                case "File":
                    refreshDatabase_Click(sender, e);
                    break;
            }
        }

        private void tbRollBackActionsToHere_Click(object sender, EventArgs e)
        {
            TreeNode hNode = (TreeNode)cmHistoryAction.Tag;
            int j = hNode.Index;

            EnableStop();
            using (new HourGlass("Rolling Back Actions..."))
            {
                if ((string)tvHistory.Tag == "EXECUTE")
                {
                    SetPBar(tvHistory.Nodes.Count - j);
                    for (int i = tvHistory.Nodes.Count - 1; i > j; i--)
                    {
                        if (Stop) break;
                        Action a = (Action)tvHistory.Nodes[i].Tag;
                        RollBack(a);
                    }
                }
                else
                {
                    SetPBar(j);
                    for (int i = 0; i < j; i++)
                    {
                        if (Stop) break;
                        Action a = (Action)tvHistory.Nodes[0].Tag;
                        RollBack(a);
                    }
                }
            }
            DisableStop();
            tvHistory_NodeAdded();
        }

        private void tbRollbackAll_Click(object sender, EventArgs e)
        {
            RollBackAll();
            tvHistory_NodeAdded();
        }

        private void tbRollback_Click(object sender, EventArgs e)
        {
            TreeNode aNode = (TreeNode)cmHistoryAction.Tag;
            Action a = (Action)aNode.Tag;
            RollBack(a);
            tvHistory_NodeAdded();
        }

        private void tbSaveHistoryActions_Click(object sender, EventArgs e)
        {
            SaveAllRollbackActions();
        }

        private void tbSchema_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem schemaitem;
            TreeNode node;
            Database db;
            string current;

            if ((string)cmServerPane.Tag == "Database")
            {
                node = (TreeNode)cmDatabase.Tag;
                db = (Database)node.Tag;
                current = db.default_schema;
            }
            else
            {
                node = (TreeNode)cmFunction.Tag;
                Function fn = (Function)node.Tag;
                db = fn.assembly.database;
                current = fn.schema;
            }

            tbSchema.DropDownItems.Clear();
            foreach (Schema schema in db.schemas)
            {
                schemaitem = (ToolStripMenuItem)tbSchema.DropDownItems.Add(schema.name);
                if (schema.name == current) schemaitem.Checked = true;
                schemaitem.Click += new EventHandler(schemaitem_Click);
            }

        }

        private void tbShowHideDatabases_DropDownOpening(object sender, EventArgs e)
        {
            TreeNode sNode = (TreeNode)cmServer.Tag;
            Server server = (Server)sNode.Tag;

            tbShowHideDatabases.DropDownItems.Clear();
            foreach (Database db in server.databases)
            {
                ToolStripMenuItem mi = new ToolStripMenuItem(db.name);
                mi.Checked = db.show;
                if (db.show) mi.Image = Resource.Database;
                else mi.Image = Resource.HideDatabase1;
                mi.Click += new EventHandler(showHideDatabaseClick);
                mi.Tag = db;
                tbShowHideDatabases.DropDownItems.Add(mi);
            }
        }

        private void tbStop_Click(object sender, EventArgs e)
        {
            Stop = true;
        }

        private void tbStop_MouseEnter(object sender, EventArgs e)
        {
            HourGlass.Enabled = false;
        }

        private void tbStop_MouseLeave(object sender, EventArgs e)
        {
            HourGlass.Enabled = true;
        }

        private void triggerEvent_Click(object sender, EventArgs e)
        {
            //if (changingChecks) return;
            TreeNode fNode = (TreeNode)cmFunction.Tag;
            Function f = (Function)fNode.Tag;

            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            if (((string)mi.Tag).Contains('*'))
            {
                mi.Checked = !mi.Checked;
                foreach (ToolStripItem tsi in ((ToolStripMenuItem)mi.OwnerItem).DropDownItems)
                {
                    if (tsi != mi && tsi.GetType().Name == "ToolStripMenuItem")
                    {
                        if (((ToolStripMenuItem)tsi).Checked != mi.Checked) triggerEvent_Click(tsi, e);
                    }
                }
            }
            else
            {
                if (mi.Checked && f.trigger.events.Count == 1)
                {
                    System.Media.SystemSounds.Exclamation.Play();
                    return;
                }
                ToolStripMenuItem pi = (ToolStripMenuItem)mi.OwnerItem;
                if (!mi.Checked && !pi.Checked)
                {
                    if ((string)pi.Tag == "AFTER" && f.trigger.insteadof)
                    {
                        f.trigger.insteadof = false;
                        pi.Checked = true;
                        insteadOfToolStripMenuItem.Checked = false;
                        changeTriggerEvents(fNode, "AFTER", "SET TIMING");
                    }
                    else if ((string)pi.Tag == "INSTEAD OF" && !f.trigger.insteadof)
                    {
                        f.trigger.insteadof = true;
                        pi.Checked = true;
                        afterTableToolStripMenuItem.Checked = false;
                        changeTriggerEvents(fNode, "INSTEAD OF", "SET TIMING");
                    }
                }
                if (mi.Checked && f.trigger.events.Contains((string)mi.Tag)) changeTriggerEvents(fNode, (string)mi.Tag, "Remove");
                else if (!mi.Checked && !f.trigger.events.Contains((string)mi.Tag)) changeTriggerEvents(fNode, (string)mi.Tag, "Add");
            }
        }

        private void triggerTarget_Click(object sender, EventArgs e)
        {
            TreeNode fNode = (TreeNode)cmFunction.Tag;
            Function f = (Function)fNode.Tag;

            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            if (!mi.Checked) changeTriggerTarget(fNode, mi.Text);
        }

        private void tvActions_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (SuppressEvents) return;
            cmAction.Tag = e.Node;
            mnuCancelAction.Enabled = mnuScriptAction.Enabled = mnuExecuteAction.Enabled =
            tbCancelAction.Enabled = tbScriptAction.Enabled = tbExecuteAction.Enabled = true;
            mnuExecuteDownToAction.Enabled = tbExecuteActionsToHere.Enabled = e.Node.Index > 0 || e.Node.Level > 0;
        }

        private void tvActions_Enter(object sender, EventArgs e)
        {
            TreeNode node = tvActions.SelectedNode;
            if (node != null)
            {
                tbCancelAction.Enabled = tbScriptAction.Enabled = tbExecuteAction.Enabled = true;
                tbExecuteActionsToHere.Enabled = node.Index > 0 || node.Level > 0;
            }
        }

        private void tvActions_KeyDown(object sender, KeyEventArgs e)
        {
            TreeNode node = tvActions.SelectedNode;
            switch (e.KeyData)
            {
                case Keys.Delete:
                    if (tbCancelAction.Enabled) cancelActionToolStripMenuItem_Click(tbCancelAction, e);
                    break;
                case (Keys.Shift | Keys.Delete):
                    if (tbCancelAllActions.Enabled) cancelAllActionsToolStripMenuItem_Click(tbCancelAllActions, e);
                    break;
                case Keys.F5:
                    if (tbExecuteAction.Enabled) executeNowToolStripMenuItem_Click(tbExecuteAction, e);
                    break;
                case (Keys.Shift | Keys.F5):
                    if (tbExecuteAllActions.Enabled) executeAllActionsToolStripMenuItem_Click(tbExecuteAllActions, e);
                    break;
                case (Keys.Control | Keys.F5):
                    if (tbExecuteActionsToHere.Enabled) executeToHereToolStripMenuItem_Click(tbExecuteActionsToHere, e);
                    break;
                case (Keys.Control | Keys.O):
                    if (tbLoadActions.Enabled) loadActionsFromFileToolStripMenuItem_Click(tbLoadActions, e);
                    break;
                case (Keys.Control | Keys.S):
                    if (tbSaveActions.Enabled) saveAllActionsToFileToolStripMenuItem_Click(tbSaveActions, e);
                    break;
            }
        }

        private void tvActions_Leave(object sender, EventArgs e)
        {
            tbCancelAction.Enabled = tbScriptAction.Enabled = tbExecuteAction.Enabled = tbExecuteActionsToHere.Enabled = false;
        }

        private void tvActions_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            tvActions.SelectedNode = e.Node;
        }

        private void tvAssemblies_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Label == null) return;
            switch (e.Node.Level)
            {
                case 0:
                    Library ly = (Library)e.Node.Tag;
                    RenameLibrary(e.Node, null, e.Label);
                    break;
                case 2:
                    Function f = (Function)e.Node.Tag;
                    if (f.assembly.functions.Exists(p => p.name == e.Label))
                        MessageBox.Show(Translate(Resource.errNameAlreadyExistsInLibraryAfterLabelEdit, f, "", e.Label), "Name Already In Use");
                    else
                    {
                        RenameLibraryFunction(e.Node, null, e.Label);
                        TreeNode lyNode = e.Node.Parent.Parent;
                        ly = (Library)lyNode.Tag;
                        ly.changes_pending = true;
                        ShowStatus(lyNode, installstatus.in_place, false, ly.changes_pending);
                    }
                    break;
            }
        }

        private void tvAssemblies_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (SuppressEvents) return;
            TreeNode node = e.Node;

            toggleLibraryPaneControls(node);
        }

        private void tvAssemblies_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            switch (e.Node.Level)
            {
                case 1:
                    e.CancelEdit = true;
                    break;
                case 2:
                    if (e.Node.Tag.GetType().Name == "InstalledAssembly") e.CancelEdit = true;
                    break;
            }
        }

        private void tvAssemblies_DragDrop(object sender, DragEventArgs e)
        {
            DataObject o = (DataObject)e.Data;
            Point pt = new Point(e.X, e.Y);
            Point pp = tvAssemblies.PointToClient(pt);
            TreeNode tn = FindTreeNode(tvAssemblies.Nodes, pp.X, pp.Y);
            int KeyState = e.KeyState;

            PasteIntoLibrary(o, ref tn, KeyState);
        }

        private void tvAssemblies_DragEnter(object sender, DragEventArgs e)
        {
            DataObject o = (DataObject)e.Data;

            if (o.GetDataPresent("Assembly") || o.GetDataPresent("File") || o.ContainsFileDropList())
                e.Effect = e.AllowedEffect;
        }

        private void tvAssemblies_DragOver(object sender, DragEventArgs e)
        {
            DataObject o = (DataObject)e.Data;
            Point pt = new Point(e.X, e.Y);
            Point pp = tvAssemblies.PointToClient(pt);
            TreeNode tn = FindTreeNode(tvAssemblies.Nodes, pp.X, pp.Y);

            if (tn != null)
            {
                if (o.GetDataPresent("Assembly") || o.GetDataPresent("File"))
                {
                    if (tn.PrevVisibleNode != null) tn.PrevVisibleNode.EnsureVisible();
                    if (tn.NextVisibleNode != null) tn.NextVisibleNode.EnsureVisible();
                    tvAssemblies.SelectedNode = tn;
                    if (o.GetDataPresent("LibraryNode"))
                    {
                        TreeNode stn = (TreeNode)o.GetData("LibraryNode");
                        if (tn == stn || (tn.Level == 1 && tn.Parent == stn) || (tn.Level == 2 && tn.Parent.Parent == stn))
                        {
                            e.Effect = DragDropEffects.None;
                            return;
                        }
                    }
                    if ((e.KeyState & 4) > 0)
                        e.Effect = DragDropEffects.Move;
                    else
                        e.Effect = DragDropEffects.Copy;
                }
                else if (o.ContainsFileDropList()) e.Effect = DragDropEffects.Copy;
            }
            else
            {
                tvAssemblies.SelectedNode = null;
                e.Effect = DragDropEffects.None;
            }
        }

        private void tvAssemblies_Enter(object sender, EventArgs e)
        {
            if (tvAssemblies.SelectedNode != null) toggleLibraryPaneControls(tvAssemblies.SelectedNode);
        }

        private void tvAssemblies_ItemDrag(object sender, ItemDragEventArgs e)
        {
            TreeNode item;

            if (e.Item.GetType().Name == "TreeNode")
            {
                item = (TreeNode)e.Item;
                if (item.Level == 0) return;

                DataObject o = new DataObject();
                if (item.Level == 1)
                {
                    o.SetData("Assembly", new InstalledAssembly((InstalledAssembly)item.Tag));
                    o.SetData("LibraryNode", item.Parent);
                }
                else if (item.Tag.GetType().Name == "Function")
                {
                    o.SetData("Assembly", new InstalledAssembly((InstalledAssembly)item.Parent.Tag));
                    o.SetData("Function", item.Tag);
                    o.SetData("LibraryNode", item.Parent.Parent);
                }
                else if (item.Tag.GetType().Name == "InstalledAssembly")
                {
                    o.SetData("File", new InstalledAssembly((InstalledAssembly)item.Tag));
                    o.SetData("AssemblyNode", item.Parent);
                }
                DoDragDrop(o, DragDropEffects.All);
            }
        }

        private void tvAssemblies_KeyDown(object sender, KeyEventArgs e)
        {
            TreeNode node = tvAssemblies.SelectedNode;
            if (node == null) return;
            string item = node.Tag.GetType().Name;
            switch (item)
            {
                case "Function":
                    Function fn = (Function)node.Tag;
                    switch (e.KeyData)
                    {
                        case Keys.Back:
                            if (fn.status == installstatus.in_place) DropLibraryFunction(node);
                            if (node.PrevNode != null && node.PrevNode.Level == node.Level) tvAssemblies.SelectedNode = node.PrevNode;
                            e.SuppressKeyPress = true;
                            break;
                        case Keys.Delete:
                            if (fn.status == installstatus.in_place) DropLibraryFunction(node);
                            if (node.NextNode != null && node.NextNode.Level == node.Level) tvAssemblies.SelectedNode = node.NextNode;
                            break;
                        case Keys.Insert:
                            if (fn.status == installstatus.not_installed) AddOrRemoveLibraryFunction(node);
                            if (node.NextNode != null && node.NextNode.Level == node.Level) tvAssemblies.SelectedNode = node.NextNode;
                            break;
                        case Keys.F2:
                            node.BeginEdit();
                            break;
                        case Keys.F4:
                            libraryAssemblyPropertiesToolStripMenuItem_Click(tbLibraryProperties, e);
                            break;
                        case (Keys.Control | Keys.P):
                            if (fn.AllowSetParameterDefaults())
                            {
                                parmbox.Tag = node;
                                parmbox.ShowDialog(this);
                            }
                            break;
                        case (Keys.Control | Keys.C):
                        case (Keys.Control | Keys.Insert):
                            if (tbLibraryCopy.Enabled) copyFunctionToolStripMenuItem_Click(tbLibraryCopy, e); break;
                        case (Keys.Control | Keys.X):
                        case (Keys.Shift | Keys.Delete):
                            if (tbLibraryCut.Enabled) cutFunctionToolStripMenuItem_Click(tbLibraryCut, e); break;
                    }
                    break;
                case "InstalledAssembly":
                    InstalledAssembly am = (InstalledAssembly)node.Tag;
                    switch (e.KeyData)
                    {
                        case Keys.Delete:
                            if (am.is_assembly) DropAssemblyFromLibrary(node);
                            else RemoveFileFromAssemblyInLibrary(node);
                            break;
                        case Keys.Add:
                        case (Keys.Shift | Keys.Oemplus):
                            AddFilesToAssemblyInLibrary(node);
                            break;
                        case Keys.F4:
                            libraryAssemblyPropertiesToolStripMenuItem_Click(tbLibraryProperties, e);
                            break;
                        case (Keys.Control | Keys.C):
                        case (Keys.Control | Keys.Insert):
                            if (tbLibraryCopy.Enabled) copyAssemblyToolStripMenuItem_Click(tbLibraryCopy, e); break;
                        case (Keys.Control | Keys.X):
                        case (Keys.Shift | Keys.Delete):
                            if (tbLibraryCut.Enabled) cutAssemblyToolStripMenuItem_Click(tbLibraryCut, e); break;
                        case (Keys.Control | Keys.V):
                        case (Keys.Shift | Keys.Insert):
                            if (tbLibraryPaste.Enabled) pasteAssemblyToolStripMenuItem_Click(tbLibraryPaste, e); break;
                    }
                    break;
                case "Library":
                    switch (e.KeyData)
                    {
                        case (Keys.Control | Keys.N): newToolStripMenuItem_Click(tbNewLibrary, e); break;
                        case (Keys.Control | Keys.O): openToolStripMenuItem_Click(tbOpenLibrary, e); break;
                        case (Keys.Control | Keys.M): if (tbMergeLibraries.Enabled) mergeToolStripMenuItem_Click(tbMergeLibraries, e); break;
                        case (Keys.Control | Keys.S): if (tbSaveLibrary.Enabled) saveToolStripMenuItem_Click(tbSaveLibrary, e); break;
                        case (Keys.Control | Keys.Shift | Keys.S):
                            if (saveLibraryAsToolStripMenuItem.Enabled) saveAsToolStripMenuItem_Click(saveLibraryAsToolStripMenuItem, e);
                            break;
                        case (Keys.F2): if (tbLibraryRename.Enabled) renameLibraryToolStripMenuItem_Click(tbLibraryRename, e); break;
                        case Keys.Delete: closeLibrary(node); break;
                        case Keys.Add:
                        case (Keys.Shift | Keys.Oemplus):
                            AddAssembliesToLibrary(node);
                            break;
                        case (Keys.Control | Keys.V):
                        case (Keys.Shift | Keys.Insert):
                            if (tbLibraryPaste.Enabled) libraryPasteToolStripMenuItem_Click(tbLibraryPaste, e); break;
                    }
                    break;
            }
        }

        private void tvAssemblies_Leave(object sender, EventArgs e)
        {
            mnuRenameLibrary.Enabled =
            mnuSaveLibrary.Enabled =
            mnuSaveLibraryAs.Enabled =
            mnuMergeLibrary.Enabled =
            mnuCloseLibrary.Enabled = false;
        }

        private void tvAssemblies_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            tvAssemblies.SelectedNode = e.Node;
        }

        private void tvHistory_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (SuppressEvents) return;
            cmHistoryAction.Tag = e.Node;
            mnuDeleteRollback.Enabled = mnuRollBackSelected.Enabled = mnuScriptRollback.Enabled =
            tbDeleteHistoryAction.Enabled = tbRollback.Enabled = tbScriptHistoryAction.Enabled = true;
            if (tvHistory.Tag as string == "EXECUTE")
                mnuRollBackToSelected.Enabled = rollbackToHereToolStripMenuItem.Enabled = tbRollBackActionsToHere.Enabled = e.Node.Index < tvHistory.Nodes.Count - 1;
            else
                mnuRollBackToSelected.Enabled = rollbackToHereToolStripMenuItem.Enabled = tbRollBackActionsToHere.Enabled = e.Node.Index > 0;
        }

        private void tvHistory_Enter(object sender, EventArgs e)
        {
            TreeNode node = tvHistory.SelectedNode;
            if (node != null)
            {
                mnuDeleteRollback.Enabled = mnuRollBackSelected.Enabled = mnuScriptRollback.Enabled =
                tbDeleteHistoryAction.Enabled = tbRollback.Enabled = tbScriptHistoryAction.Enabled = true;
                mnuRollBackToSelected.Enabled = rollbackToHereToolStripMenuItem.Enabled = tbRollBackActionsToHere.Enabled = node.Index < tvHistory.Nodes.Count - 1;
            }
        }

        private void tvHistory_KeyDown(object sender, KeyEventArgs e)
        {
            TreeNode node = tvHistory.SelectedNode;
            switch (e.KeyData)
            {
                case Keys.Delete:
                    if (tbDeleteHistoryAction.Enabled) tbDeleteHistoryAction_Click(tbDeleteHistoryAction, e);
                    break;
                case (Keys.Shift | Keys.Delete):
                    if (tbDeleteAll.Enabled) tbDeleteAll_Click(tbDeleteAll, e);
                    break;
                case Keys.F5:
                    if (tbRollback.Enabled) tbRollback_Click(tbRollback, e);
                    break;
                case (Keys.Shift | Keys.F5):
                    if (tbRollbackAll.Enabled) tbRollbackAll_Click(tbRollbackAll, e);
                    break;
                case (Keys.Control | Keys.F5):
                    if (tbRollBackActionsToHere.Enabled) tbRollBackActionsToHere_Click(tbRollBackActionsToHere, e);
                    break;
                case (Keys.Control | Keys.S):
                    if (tbSaveHistoryActions.Enabled) tbSaveHistoryActions_Click(tbSaveHistoryActions, e);
                    break;
            }

        }

        private void tvHistory_Leave(object sender, EventArgs e)
        {
            mnuDeleteRollback.Enabled = mnuRollBackSelected.Enabled = mnuScriptRollback.Enabled = mnuRollBackToSelected.Enabled =
            tbDeleteHistoryAction.Enabled
            = tbRollback.Enabled
            = tbRollBackActionsToHere.Enabled
            = tbScriptHistoryAction.Enabled = false;
        }

        private void tvHistory_NodeAdded()
        {
            mnuHistory.Enabled =
            tbDeleteAll.Enabled
            = tbRollbackAll.Enabled
            = tbScriptHistoryActions.Enabled
            = tbSaveHistoryActions.Enabled
            = tbViewHistory.Enabled
            = DeleteHistoryActionsToolStripMenuItem.Enabled
            = executeHistoryActionsToolStripMenuItem.Enabled
            = scriptHistoryActionsToolStripMenuItem.Enabled
            = saveHistoryActionsToolStripMenuItem.Enabled
            = viewCompletedActionsAsToolStripMenuItem.Enabled = tvHistory.Nodes.Count > 0;
            lbNoCompletedActions.Visible = tvHistory.Nodes.Count == 0;

            if (!tbDeleteAll.Enabled) tbDeleteHistoryAction.Enabled
                                        = tbRollback.Enabled
                                        = tbRollBackActionsToHere.Enabled
                                        = tbScriptHistoryAction.Enabled = false;
        }

        private void tvHistory_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            tvHistory.SelectedNode = e.Node;
        }

        private void tvServers_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Label == null) return;
            e.CancelEdit = true;
            int i = 0;
            TreeNode fnNode = e.Node;
            Function fn = (Function)fnNode.Tag;
            string[] s = e.Label.Split('.');
            string sma = fn.schema;
            if (s.Count() > 1)
            {
                sma = s[0];
                if (sma != fn.schema)
                {
                    if (fn.type == "TA")
                        MessageBox.Show(Resource.errSchemaCannotBeSetForTriggers, "Schema Specified for Trigger");
                    else if (fn.assembly.database.schemas.Exists(p => p.name == sma))
                        ChangeFunctionSchema(fnNode, null, sma);
                    else
                        MessageBox.Show(Translate(Resource.errSchemaNotFoundAfterLabelEdit, fn, "", sma), "Schema Not Found");
                }
                i = 1;
            }
            if (s[i] != fn.name)
            {
                foreach (InstalledAssembly am in fn.assembly.database.assemblies)
                {
                    foreach (Function f in am.functions)
                    {
                        if (f.name == s[i] && f.schema == sma && (f.status == installstatus.in_place || f.status == installstatus.pending_add) && (f.type == fn.type || (f.type != "UDT" && fn.type != "UDT")))
                        {
                            MessageBox.Show(Translate(Resource.errNameAlreadyExistsAfterLabelEdit, fn, "", sma + "].[" + s[i]), "Name Already In Use");
                            return;
                        }
                    }
                }
                fnNode = GetNodeFor(fn);
                RenameFunction(fnNode, s[i]);
            }
        }

        private void tvServers_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (SuppressEvents) return;
            TreeNode node = e.Node;
            toggleServerPaneControls(node);
        }

        private void tvServers_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node.Level < 3 || e.Node.Tag.GetType().Name != "Function") e.CancelEdit = true;
            else
            {
                Function fn = (Function)e.Node.Tag;
                if (!fn.ShowAsInstalled()) e.CancelEdit = true;
            }
        }

        private void tvServers_DragDrop(object sender, DragEventArgs e)
        {
            DataObject o = (DataObject)e.Data;
            Point pt = new Point(e.X, e.Y);
            Point pp = tvServers.PointToClient(pt);
            TreeNode tn = FindTreeNode(tvServers.Nodes, pp.X, pp.Y);

            PasteIntoDatabase(o, tn, e.KeyState);
        }

        private void tvServers_DragEnter(object sender, DragEventArgs e)
        {
            DataObject o = (DataObject)e.Data;

            if (o.GetDataPresent("Assembly") || o.GetDataPresent("File") || o.ContainsFileDropList())
                e.Effect = e.AllowedEffect;
        }

        private void tvServers_DragOver(object sender, DragEventArgs e)
        {
            DataObject o = (DataObject)e.Data;
            Point pt = new Point(e.X, e.Y);
            Point pp = tvServers.PointToClient(pt);
            TreeNode tn = FindTreeNode(tvServers.Nodes, pp.X, pp.Y);

            if (o.GetDataPresent("Assembly") || o.GetDataPresent("File"))
            {
                if (tn != null && (tn.Level == 1 || tn.Level == 2 || (tn.Level == 3 && tn.Tag.GetType().Name == "Function" && o.GetDataPresent("Function"))))
                {
                    if (tn.Level == 3)
                    {
                        Function f1 = (Function)o.GetData("Function");
                        Function f2 = (Function)tn.Tag;
                        if (f1.name != f2.name || f2.status == installstatus.in_place || f2.status == installstatus.pending_add)
                        {
                            tvServers.SelectedNode = null;
                            e.Effect = DragDropEffects.None;
                            return;
                        }
                    }
                    if (o.GetDataPresent("DatabaseNode"))
                    {
                        TreeNode dbNode = (TreeNode)o.GetData("DatabaseNode");
                        if (tn == dbNode || tn.Parent == dbNode || tn.Level == 3 && tn.Parent.Parent == dbNode)
                        {
                            e.Effect = DragDropEffects.None;
                            return;
                        }
                    }
                    if (tn.PrevVisibleNode != null) tn.PrevVisibleNode.EnsureVisible();
                    if (tn.NextVisibleNode != null) tn.NextVisibleNode.EnsureVisible();
                    tvServers.SelectedNode = tn;
                    if ((e.KeyState & 4) > 0)
                        e.Effect = DragDropEffects.Move;
                    else
                        e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    tvServers.SelectedNode = null;
                    e.Effect = DragDropEffects.None;
                }
            }
            else if (o.ContainsFileDropList() && tn != null && (tn.Level == 1 || tn.Level == 2)) e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        private void tvServers_Enter(object sender, EventArgs e)
        {
            if (tvServers.SelectedNode != null) toggleServerPaneControls(tvServers.SelectedNode);
        }

        private void tvServers_ItemDrag(object sender, ItemDragEventArgs e)
        {
            TreeNode item;

            if (e.Item.GetType().Name == "TreeNode")
            {
                item = (TreeNode)e.Item;
                if (item.Level < 2) return;
                DataObject o = new DataObject();

                if (item.Level == 2)
                {
                    o.SetData("Assembly", new InstalledAssembly((InstalledAssembly)item.Tag));
                    o.SetData("DatabaseNode", item.Parent);
                }
                else if (item.Tag.GetType().Name == "Function")
                {
                    o.SetData("Assembly", new InstalledAssembly((InstalledAssembly)item.Parent.Tag));
                    o.SetData("Function", item.Tag);
                    o.SetData("DatabaseNode", item.Parent.Parent);
                }
                else if (item.Tag.GetType().Name == "InstalledAssembly")
                {
                    o.SetData("File", new InstalledAssembly((InstalledAssembly)item.Tag));
                    o.SetData("AssemblyNode", item.Parent);
                }
                DoDragDrop(o, DragDropEffects.All);
            }
        }

        private void tvServers_KeyDown(object sender, KeyEventArgs e)
        {
            Function fn = null;
            InstalledAssembly am = null;

            if (e.KeyData == (Keys.Control | Keys.S))
            {
                saveConnectionSettingsToolStripMenuItem_Click(tbSaveConnectionFile, e);
                return;
            }

            if (tvServers.SelectedNode == null)
            {
                switch (e.KeyData)
                {
                    case Keys.Add:
                    case (Keys.Shift | Keys.Oemplus):
                        tbConnect_Click(tbConnect, e);
                        break;
                    case (Keys.Control | Keys.O):
                        loadConnectionSettingsToolStripMenuItem_Click(tbOpenConnectionFile, e);
                        break;
                }
                return;
            }
            string item = tvServers.SelectedNode.Tag.GetType().Name;
            switch (item)
            {
                case "Function":
                    fn = (Function)tvServers.SelectedNode.Tag;
                    switch (e.KeyData)
                    {
                        case Keys.Back:
                            if (fn.status == installstatus.in_place || fn.status == installstatus.pending_add) DropFunction(tvServers.SelectedNode, null, true);
                            if (tvServers.SelectedNode.PrevNode != null && tvServers.SelectedNode.PrevNode.Level == tvServers.SelectedNode.Level) tvServers.SelectedNode = tvServers.SelectedNode.PrevNode;
                            e.SuppressKeyPress = true;
                            break;
                        case Keys.Delete:
                            if (fn.status == installstatus.in_place || fn.status == installstatus.pending_add) DropFunction(tvServers.SelectedNode, null, true);
                            if (tvServers.SelectedNode.NextNode != null && tvServers.SelectedNode.NextNode.Level == tvServers.SelectedNode.Level) tvServers.SelectedNode = tvServers.SelectedNode.NextNode;
                            break;
                        case Keys.Insert:
                            if (fn.status == installstatus.not_installed || fn.status == installstatus.pending_remove) AddFunction(tvServers.SelectedNode, null, true);
                            if (tvServers.SelectedNode.NextNode != null && tvServers.SelectedNode.NextNode.Level == tvServers.SelectedNode.Level) tvServers.SelectedNode = tvServers.SelectedNode.NextNode;
                            break;
                        case Keys.F2:
                            tvServers.SelectedNode.BeginEdit();
                            break;
                        case Keys.F4:
                            tbProperties_Click(tbProperties, e);
                            break;
                        case (Keys.Control | Keys.P):
                            if (fn.AllowSetParameterDefaults())
                            {
                                parmbox.Tag = tvServers.SelectedNode;
                                parmbox.ShowDialog(this);
                            }
                            break;
                        case (Keys.Control | Keys.C):
                        case (Keys.Control | Keys.Insert):
                            if (tbCopy.Enabled) tbCopy_Click(tbCopy, e); break;
                        case (Keys.Control | Keys.X):
                        case (Keys.Shift | Keys.Delete):
                            if (tbCut.Enabled) tbCut_Click(tbCut, e); break;
                        case (Keys.Control | Keys.V):
                        case (Keys.Shift | Keys.Insert):
                            if (tbPaste.Enabled) tbPaste_Click(tbPaste, e); break;
                    }
                    break;
                case "InstalledAssembly":
                    am = (InstalledAssembly)tvServers.SelectedNode.Tag;
                    switch (e.KeyData)
                    {
                        case Keys.Delete:
                            if (am.status == installstatus.in_place || am.status == installstatus.pending_add)
                            {
                                if (am.is_assembly) DropAssembly(tvServers.SelectedNode, null, true);
                                else RemoveFileFromAssembly(tvServers.SelectedNode, null, false);
                            }
                            if (tvServers.SelectedNode.NextNode != null && tvServers.SelectedNode.NextNode.Level == tvServers.SelectedNode.Level) tvServers.SelectedNode = tvServers.SelectedNode.NextNode;
                            break;
                        case Keys.Insert:
                            if (am.is_assembly && am.status == installstatus.pending_remove) UndropAssembly(tvServers.SelectedNode, null);
                            break;
                        case Keys.F4:
                            tbProperties_Click(tbProperties, e);
                            break;
                        case Keys.Add:
                        case (Keys.Shift | Keys.Oemplus):
                            if (am.is_assembly) AddFilesToAssemblyInDatabase(tvServers.SelectedNode);
                            break;
                        case (Keys.Control | Keys.C):
                        case (Keys.Control | Keys.Insert):
                            if (tbCopy.Enabled) tbCopy_Click(tbCopy, e); break;
                        case (Keys.Control | Keys.X):
                        case (Keys.Shift | Keys.Delete):
                            if (tbCut.Enabled) tbCut_Click(tbCut, e); break;
                        case (Keys.Control | Keys.V):
                        case (Keys.Shift | Keys.Insert):
                            if (tbPaste.Enabled) tbPaste_Click(tbPaste, e); break;
                    }
                    break;
                case "Database":
                    switch (e.KeyData)
                    {
                        case Keys.Delete:
                            HideDatabase(tvServers.SelectedNode);
                            break;
                        case Keys.F4:
                            tbProperties_Click(tbProperties, e);
                            break;
                        case Keys.F5:
                            RefreshDatabase(tvServers.SelectedNode);
                            break;
                        case Keys.Add:
                        case (Keys.Shift | Keys.Oemplus):
                            AddAssembliesToDatabase(tvServers.SelectedNode);
                            break;
                        case (Keys.Control | Keys.V):
                        case (Keys.Shift | Keys.Insert):
                            if (tbPaste.Enabled) databasePasteToolStripMenuItem_Click(tbPaste, e); break;
                    }
                    break;
                case "Server":
                    switch (e.KeyData)
                    {
                        case Keys.Add:
                        case (Keys.Shift | Keys.Oemplus): tbConnect_Click(tbConnect, e); break;
                        case Keys.Delete: disconnectToolStripMenuItem1_Click(tbDisconnect, e); break;
                        case (Keys.Shift | Keys.Delete): disconnectAllToolStripMenuItem_Click(tbDisconnectAll, e); break;
                        case Keys.F5: RefreshServer(tvServers.SelectedNode); break;
                        case (Keys.Shift | Keys.F5): if (tbRefreshAll.Enabled) refreshAllToolStripMenuItem_Click(tbRefreshAll, e); break;
                        case Keys.F4: tbProperties_Click(tbProperties, e); break;
                    }
                    break;
            }
        }

        private void tvServers_Leave(object sender, EventArgs e)
        {
            mnuDatabase.Enabled =
            mnuAssembly.Enabled =
            mnuFunction.Enabled =
            mnuAssociatedFile.Enabled = false;
        }

        private void tvServers_NodeAdded()
        {
            //mnuActions.Enabled =
            //mnuSaveConnections.Enabled =
            //mnuDisconnectAll.Enabled =
            //mnuRefreshAllServers.Enabled =
            //tbDisconnect.Enabled = 
            //tbRefreshAll.Enabled = 
            //tbLoadActions.Enabled =
            //tbSaveConnectionFile.Enabled = true;
            lbNoServersConnected.Visible = false;
        }

        private void tvServers_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            tvServers.SelectedNode = e.Node;
        }

        private void tvServers_NodeRemoved()            //Not really an event handler but used like one
        {
            toggleServerPaneControls(tvServers.SelectedNode);
        }

        private void undoReplaceAssemblyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            InstalledAssembly am = (InstalledAssembly)amNode.Tag;
            Action act = am.actions.SingleOrDefault(a => a.action == ActionType.SwapAssembly);
            if (act != null) ReverseAction(act);
        }

        private void uninstallAllObjectsStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem me = (ToolStripMenuItem)sender;
            if (me.OwnerItem != null && me.OwnerItem.Tag != null && (string)me.OwnerItem.Tag == "LIBRARY ASSEMBLY")
            {
                setAllObjectsToNotInstallToolStripMenuItem_Click(sender, e);
                return;
            }

            TreeNode amNode = (TreeNode)cmAssembly.Tag;
            InstalledAssembly assembly = (InstalledAssembly)amNode.Tag;
            Action act = DropAllObjects(amNode, null);
        }

        private void ultraTabControl1_SelectedTabChanged(object sender, SelectedTabChangedEventArgs e)
        {
            if (e.Tab == null) return;
            UltraTab tab = e.Tab;
            if (tab.Text.StartsWith("Generated Script"))
            {
                RichTextBox rtb = tab.TabPage.Controls.OfType<Panel>().Single().Controls.OfType<RichTextBox>().Single();
                cmScript.Tag = rtb;
                tsScript.Visible = true;
                tsHistory.Visible = tsLibrary.Visible = tsActions.Visible = false;
            }
            else
            {
                cmScript.Tag = null;
                switch ((string)e.Tab.Tag)
                {
                    case "Actions":
                        mnuActions.Enabled = tvServers.Nodes.Count > 0;
                        tsActions.Visible = true;
                        mnuHistory.Enabled = mnuLibrary.Enabled = tsScript.Visible = tsHistory.Visible = tsLibrary.Visible = false;
                        if (!lbNoActionsPending.Visible) tvActions.Focus();
                        break;
                    case "History":
                        mnuHistory.Enabled = tvHistory.Nodes.Count > 0;
                        tsHistory.Visible = true;
                        mnuActions.Enabled = mnuLibrary.Enabled = tsScript.Visible = tsActions.Visible = tsLibrary.Visible = false;
                        if (!lbNoCompletedActions.Visible) tvHistory.Focus();
                        break;
                    case "Libraries":
                        mnuLibrary.Enabled = tsLibrary.Visible = true;
                        mnuActions.Enabled = mnuHistory.Enabled = tsScript.Visible = tsHistory.Visible = tsActions.Visible = false;
                        if (!lbNoLibrariesLoaded.Visible) tvAssemblies.Focus();
                        break;
                }
            }
        }

        #endregion Event Handlers

        #region Internal Classes

        public class Schema
        {
            public int schema_id;
            public string name;
        }

        public class Server
        {
            private string error = "";
            public Server(SqlConnection conn, SqlConnectionStringBuilder builder)
            {
                databases = new List<Database>();
                permissions = new List<string>();
                actions = new List<Action>();
                keys = new List<AsymmetricKey>();
                logins = new List<Login>();
                name = "";
                clr_enabled = false;
                changes_pending = false;
                lightweight_pooling = false;
                connector = builder;
                connection = conn;
            }
            public Server()
            {
                //Returns an empty server object for script-generation purposes
            }
            public bool ExecuteCommand(string cmd, Database db)
            {
                bool success = true;
                SqlCommand command = new SqlCommand(cmd);
                command.Connection = connection;
                connection.Open();
                try
                {
                    if (db != null) connection.ChangeDatabase(db.name);
                    error = "";
                    if (cmd.Contains("--GO--"))
                    {
                        string[] da = new string[] { "--GO--" };
                        string[] sa = cmd.Split(da, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string s in sa)
                        {
                            command.CommandText = s;
                            command.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    success = false;
                    error = e.Message;
                }
                finally
                {
                    connection.Close();
                }
                return success;
            }
            public string LastError
            {
                get { return error; }
            }
            public string name;
            public string version;
            public string level;
            public string edition;
            public string versionname
            {
                get
                {
                    string[] sa = version.Split('.');
                    int[] ia = new int[3];
                    for (int i = 0; i < 3; i++) ia[i] = int.Parse(sa[i]);
                    switch (ia[0])
                    {
                        case 9:
                            if (ia[2] < 1399) return "2005 Beta";
                            if (ia[2] < 2000) return "2005 RTM";
                            if (ia[2] < 2047) return "2005 SP1 Beta";
                            if (ia[2] < 3027) return "2005 SP1";
                            if (ia[2] < 4035) return "2005 SP2";
                            return "2005 SP3";
                        case 10:
                            if (ia[1] >= 5) return "2008 R2";
                            if (ia[2] < 1600) return "2008 Beta";
                            if (ia[2] < 2000) return "2008 RTM";
                            return "2008 SP1";
                        default:
                            if (ia[0] > 10) return "Version > 2008";
                            return "Unsupported version";
                    }
                }
            }
            public SqlConnectionStringBuilder connector;
            public SqlConnection connection;
            public List<Database> databases;
            public List<string> permissions;
            public List<Action> actions;
            public List<AsymmetricKey> keys = null;
            public List<Login> logins = null;
            public bool clr_enabled;
            protected bool _changes_pending;
            public bool lightweight_pooling;
            public bool changes_pending
            {
                get
                {
                    if (_changes_pending) return true;
                    foreach (Database db in databases) if (db.changes_pending) return true;
                    return false;
                }
                set { _changes_pending = value; }
            }
            public string ToolTipText()
            {
                string tip;
                tip = "Server: " + name + "; Version " + version + " " + level + " " + edition + "; CLR Enabled = " + clr_enabled.ToString();
                return tip;
            }
            public void GetPermissions()
            {
                permissions.Clear();
                SqlDataReader dr;
                SqlCommand comm;

                comm = new SqlCommand(Resource.sqlGetServerPermissions, connection);
                connection.Open();
                try
                {
                    dr = comm.ExecuteReader();
                    while (dr.Read()) permissions.Add((string)dr[0]);
                    dr.Close();
                    comm.CommandText = Resource.sqlGetClrEnabled;
                    int i = (int)comm.ExecuteScalar();
                    clr_enabled = i == 1;
                }
                finally
                {
                    connection.Close();
                }
            }
            public void GetKeysAndLogins()
            {
                Login login;
                AsymmetricKey key;
                SqlCommand comm;
                SqlDataAdapter da;
                DataTable dt;

                if (keys == null) keys = new List<AsymmetricKey>();
                else keys.Clear();
                comm = new SqlCommand(Resource.sqlGetAsymmetricKeys, connection);
                da = new SqlDataAdapter(comm);
                dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    key = new AsymmetricKey();
                    key.Name = (string)dr["name"];
                    key.Thumbprint = (byte[])dr["thumbprint"];
                    key.SID = (byte[])dr["sid"];
                    key.status = installstatus.in_place;
                    keys.Add(key);
                }

                if (logins == null) logins = new List<Login>();
                else logins.Clear();
                comm.CommandText = Resource.sqlGetLogins;
                dt.Clear();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    login = new Login();
                    login.Name = (string)dr["name"];
                    login.SID = (byte[])dr["sid"];
                    login.PID = (int)dr["principal_id"];
                    string level = dr.IsNull("level") ? "Null" : (string)dr["level"];
                    switch (level.Trim())
                    {
                        case "XU": login.permission = PermissionSet.UNSAFE; break;
                        case "XA": login.permission = PermissionSet.EXTERNAL_ACCESS; break;
                        default: login.permission = PermissionSet.SAFE; break;
                    }
                    login.status = installstatus.in_place;
                    login.key = keys.FirstOrDefault(p => p.SID.SequenceEqual(login.SID));
                    logins.Add(login);
                }

            }
            public void GetDatabases(List<string> externalassemblies)
            {
                SqlCommand comm;
                SqlDataReader dr;
                Database database;

                comm = new SqlCommand(AssemblyManager.Resource.sqlGetDatabases, connection);
                connection.Open();
                try
                {
                    dr = comm.ExecuteReader();
                    while (dr.Read())
                    {
                        database = new Database(this);
                        database.name = (string)dr["name"];
                        database.trustworthy = (bool)dr["is_trustworthy_on"];
                        if (database.name != "master" && database.name != "tempdb" && database.name != "msdb" && database.name != "model") database.show = true;
                        else database.show = false;
                        database.schemas = new List<Schema>();
                        database.assemblies = new List<InstalledAssembly>();
                        database.actions = new List<Action>();
                        database.server = this;
                        databases.Add(database);
                    }
                    dr.Close();

                    foreach (Database db in databases)
                    {
                        db.GetPermissions();
                        db.ReLoad(externalassemblies);
                    }

                }
                finally
                {
                    connection.Close();
                }
            }
            public void GetVersion()
            {
                SqlCommand comm;

                comm = new SqlCommand(Resource.sqlGetVersion, connection);
                connection.Open();
                try
                {
                    version = (string)comm.ExecuteScalar();
                    comm.CommandText = Resource.sqlGetEdition;
                    edition = (string)comm.ExecuteScalar();
                    comm.CommandText = Resource.sqlGetProductLevel;
                    level = (string)comm.ExecuteScalar();
                }
                finally
                {
                    connection.Close();
                }

            }
        }

        public class Database
        {
            static public Dictionary<string, string> defaultschemas = new Dictionary<string, string>();
            private List<Assembly> dlls = new List<Assembly>();
            public Database(Server svr)
            {
                name = "";
                schemas = new List<Schema>();
                assemblies = new List<InstalledAssembly>();
                actions = new List<Action>();
                permissions = new List<string>();
                trustworthy = false;
                changes_pending = false;
                server = svr;
                show = true;
            }
            public Database()
            {
                changes_pending = false;
            }
            public static int LengthOfType(string type)
            {
                string root;
                Match m;
                m = Regex.Match(type, "\\w+");
                root = m.Value;

                switch (root)
                {
                    case "nvarchar":
                    case "nchar":
                        m = Regex.Match(type, "\\((\\w+)\\)");
                        if (m.Groups.Count < 2 || m.Groups[1].Value == "max")
                            return -1;
                        else
                            return int.Parse(m.Groups[1].Value) * 2;
                    case "varchar":
                    case "varbinary":
                    case "binary":
                        m = Regex.Match(type, "\\((\\w+)\\)");
                        if (m.Groups.Count < 2 || m.Groups[1].Value == "max")
                            return -1;
                        else
                            return int.Parse(m.Groups[1].Value);
                    case "decimal":
                    case "numeric":
                        return 17;
                    case "datetimeoffset":
                        return 10;
                    case "bigint":
                    case "float":
                    case "datetime":
                    case "datetime2":
                    case "money":
                    case "uniqueidentifier":
                        return 8;
                    case "time":
                        return 5;
                    case "int":
                    case "real":
                    case "smalldatetime":
                    case "smallmoney":
                        return 4;
                    case "date":
                        return 3;
                    case "smallint":
                        return 2;
                    case "tinyint":
                    case "bit":
                        return 1;
                    case "xml":
                        return -1;
                }
                return -1;
            }
            public static string SqlNameFor(string type, int length)
            {
                string s;

                if (type.Contains("(")) return type;
                switch (type)
                {
                    case "Object":
                        return "sql_variant";
                    case "SqlBinary":
                        return ("varbinary(8000)");
                    case "SqlBytes":
                        return ("varbinary(max)");
                    case "varbinary":
                        s = "varbinary(max)";
                        if (length != -1) s = s.Replace("max", length.ToString());
                        return s;
                    case "bool":
                    case "Bool":
                    case "SqlBoolean":
                        return "bit";
                    case "Byte":
                    case "byte":
                    case "SqlByte":
                        return "tinyint";
                    case "String":
                    case "string":
                    case "SqlString":
                        return "nvarchar(4000)";
                    case "SqlChars":
                    case "nvarchar":
                        s = "nvarchar(max)";
                        if (length != -1) s = s.Replace("max", (length / 2).ToString());
                        return s;
                    case "DateTime":
                    case "datetime":
                    case "SqlDateTime":
                        return "datetime";
                    case "decimal":
                    case "Decimal":
                    case "SqlDecimal":
                        return "decimal";
                    case "Double":
                    case "double":
                    case "SqlDouble":
                        return "float";
                    case "Guid":
                    case "SqlGuid":
                        return "uniqueidentifier";
                    case "short":
                    case "ushort":
                    case "Int16":
                    case "SqlInt16": return "smallint";
                    case "int":
                    case "uint":
                    case "Int32":
                    case "SqlInt32": return "int";
                    case "long":
                    case "ulong":
                    case "Int64":
                    case "SqlInt64": return "bigint";
                    case "SqlMoney": return "money";
                    case "Single":
                    case "SqlSingle": return "real";
                    case "SqlXml": return "xml";
                    default: return type;
                }
            }
            public static bool Parses(string type, string input)
            {
                string s;
                Match m = Regex.Match(type, "\\w+");
                Match n = Regex.Match(type, "\\(([^\\)]*)\\)");
                s = m.Value;

                switch (s)
                {
                    case "nvarchar":
                    case "varchar":
                    case "char":
                    case "nchar":
                        //                    if (!Regex.IsMatch(input, "^\\s*'[^']+'\\s*$")) return false;
                        if (Regex.IsMatch(input, "'")) return false;
                        break;
                    case "datetime":
                    case "datetimeoffset":
                    case "smalldatetime":
                    case "datetime2":
                    case "date":
                    case "time":
                        DateTime dt;
                        //                    if (!Regex.IsMatch(input, "^\\s*'[^']+'\\s*$")) return false;
                        if (!DateTime.TryParse(input.Replace("'", ""), out dt)) return false;
                        break;
                    case "money":
                    case "decimal":
                        decimal d;
                        if (!decimal.TryParse(input, out d)) return false;
                        break;
                    case "uniqueidentifier":
                        return false;
                    case "tinyint":
                        byte b;
                        if (!byte.TryParse(input, out b)) return false;
                        break;
                    case "smallint":
                        short sh;
                        if (!short.TryParse(input, out sh)) return false;
                        break;
                    case "bit":
                        if (input != "0" && input != "1") return false;
                        break;
                    case "int":
                        int i;
                        if (!int.TryParse(input, out i)) return false;
                        break;
                    case "bigint":
                        long l;
                        if (!long.TryParse(input, out l)) return false;
                        break;
                    case "float":
                        double f;
                        if (!double.TryParse(input, out f)) return false;
                        break;
                    case "real":
                        float r;
                        if (!float.TryParse(input, out r)) return false;
                        break;
                }
                return true;
            }
            public string FQN
            {
                get
                {
                    if (server == null) return "[].[" + name + "]";
                    else return "[" + server.name + "].[" + name + "]";
                }
            }
            public bool changes_pending
            {
                get
                {
                    if (_changes_pending) return true;
                    else foreach (InstalledAssembly am in assemblies) if (am.changes_pending) return true;
                    return false;
                }
                set
                {
                    _changes_pending = value;
                }
            }
            public int ImageIndex()
            {
                if (trustworthy) return 15;
                else return 14;
            }
            public string name;
            public Server server;
            public List<Schema> schemas;
            public List<InstalledAssembly> assemblies;
            public List<Action> actions;
            public List<string> permissions;
            public List<string> tables;
            public string default_schema;
            public bool show;
            protected bool _changes_pending;
            public bool trustworthy;
            public void ToggleShow()
            {
                show = !show;
            }
            public void GetPermissions()
            {
                permissions.Clear();
                SqlDataReader dr;
                SqlCommand comm;
                bool open;

                string root = Resource.sqlGetDBPermissions.Replace("%DATABASE%", name);
                comm = new SqlCommand(root, server.connection);
                open = (server.connection.State == ConnectionState.Open);
                if (!open) server.connection.Open();
                try
                {
                    dr = comm.ExecuteReader();
                    while (dr.Read()) permissions.Add((string)dr[0]);
                    dr.Close();
                }
                catch (Exception) { }
                finally
                {
                    if (!open) server.connection.Close();
                }
            }
            public void GetTables()
            {
                if (tables == null) tables = new List<string>();
                else tables.Clear();
                SqlDataReader dr;
                SqlCommand comm;
                bool open;

                string root = Resource.sqlGetTablesAndViews.Replace("%DATABASE%", name);
                comm = new SqlCommand(root, server.connection);
                open = (server.connection.State == ConnectionState.Open);
                if (!open) server.connection.Open();
                try
                {
                    dr = comm.ExecuteReader();
                    while (dr.Read()) tables.Add(((string)dr["type"] == "U " ? "(table) " : "(view) ") + (string)dr["schema"] + "." + (string)dr["name"]);
                    dr.Close();
                }
                finally
                {
                    if (!open) server.connection.Close();
                }

            }
            public string ToolTipText()
            {
                string tip;
                tip = "Database: " + name + "; Trustworthy = " + (trustworthy ? "ON" : "OFF");
                return tip;
            }
            public AppDomain domain = null;
            public void ReLoad(List<string> externalassemblies)
            {
                if (permissions.Count == 0) return;

                List<Trigger> tlist;
                List<Function> flist = new List<Function>();
                Trigger trigger;
                int tid = -1;
                SqlCommand comm;
                Schema schema;
                InstalledAssembly assembly = new InstalledAssembly();
                Function function;
                string AssemblyName = "";

                schemas.Clear();
                comm = new SqlCommand(AssemblyManager.Resource.sqlGetDatabases, server.connection);
                comm.CommandText = AssemblyManager.Resource.sqlGetSchemas.Replace("%DATABASE%", name);
                SqlDataAdapter da = new SqlDataAdapter(comm);
                DataTable dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    schema = new Schema();
                    schema.name = (string)dr["name"];
                    schema.schema_id = (int)dr["schema_id"];
                    schemas.Add(schema);
                }
                string ds = "dbo";
                if (defaultschemas.ContainsKey(FQN)) ds = defaultschemas[FQN];
                if (schemas.Exists(s => s.name == ds)) default_schema = ds;
                else default_schema = schemas[0].name;

                //Get parameters
                List<Parameter> parmlist = new List<Parameter>();
                comm.CommandText = AssemblyManager.Resource.sqlGetParameters.Replace("%DATABASE%", name);
                dt.Clear();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    Parameter p = new Parameter();
                    p.name = (string)dr["parameter"];
                    p.object_id = (int)dr["object_id"];
                    p.position = (int)dr["parameter_id"];
                    p.max_length = (int)(short)dr["max_length"];
                    p.type = Database.SqlNameFor((string)dr["type"], p.max_length);
                    if ((bool)dr["has_default_value"]) p.default_value = dr["default_value"].ToString();
                    parmlist.Add(p);
                }

                //Get columns for table-valued function return parameters
                List<Parameter> columnlist = new List<Parameter>();
                comm.CommandText = AssemblyManager.Resource.sqlGetColumns.Replace("%DATABASE%", name);
                dt.Clear();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    Parameter p = new Parameter();
                    p.name = (string)dr["table_name"] + "].[" + (string)dr["column_name"];
                    p.position = (int)dr["ordinal_position"];
                    if (!dr.IsNull("character_maximum_length")) p.max_length = (int)dr["character_maximum_length"];
                    p.type = Database.SqlNameFor((string)dr["data_type"], p.max_length);
                    columnlist.Add(p);
                }

                //Get trigger definitions
                tlist = new List<Trigger>();
                trigger = new Trigger();
                comm.CommandText = Resource.sqlGetTriggers.Replace("%DATABASE%", name);
                dt.Clear();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    int id = (int)dr["object_id"];
                    if (id != tid)
                    {
                        tid = id;
                        trigger = new Trigger();
                        tlist.Add(trigger);
                        trigger.object_id = id;
                        trigger.target_schema = (string)dr["target_schema"];
                        trigger.target = (string)dr["target_object"];
                        trigger.target_type = (string)dr["target_type"];
                        trigger.insteadof = (bool)dr["is_instead_of_trigger"];
                        trigger.disabled = (bool)dr["is_disabled"];
                        trigger.events = new List<string>();
                        trigger.events.Add((string)dr["type_desc"]);
                        trigger.isdatabase = (trigger.target_schema == null || trigger.target_schema == "");
                    }
                    else
                        if (trigger != null) trigger.events.Add((string)dr["type_desc"]);
                }

                if (domain != null) AppDomain.Unload(domain);
                AppDomainSetup setup = new AppDomainSetup();
                setup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                setup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                setup.DisallowBindingRedirects = false;
                Evidence evidence = new Evidence(AppDomain.CurrentDomain.Evidence);
                domain = AppDomain.CreateDomain(this.FQN, evidence, AppDomain.CurrentDomain.SetupInformation);
                AssemblyParser Parser = (AssemblyParser)domain.CreateInstanceAndUnwrap
//                AssemblyParser Parser = (AssemblyParser)AppDomain.CurrentDomain.CreateInstanceAndUnwrap
                (   
                    Assembly.GetExecutingAssembly().FullName, 
                    "AssemblyManager.AssemblyParser" 
                );
                comm.CommandText = AssemblyManager.Resource.sqlGetAssembliesAndFunctions.Replace("%DATABASE%", name);
                dt.Clear();
                da.Fill(dt);
                bool asmok = true;
                foreach (DataRow dr in dt.Rows)
                {

                    if ((string)dr["assembly_name"] != AssemblyName)
                    {
                        asmok = true;
                        AssemblyName = (string)dr["assembly_name"];
                        assembly = new InstalledAssembly();
                        assembly.name = AssemblyName;
                        assembly.database = this;
                        assembly.clr_name = (string)dr["clr_name"];
                        assembly.create_date = (DateTime)dr["create_date"];
                        assembly.modify_date = (DateTime)dr["modify_date"];
                        assembly.is_visible = (bool)dr["is_visible"];
                        assembly.is_assembly = true;
                        assembly.permission_set = (int)(byte)dr["permission_set"];
                        assembly.assembly_id = (int)dr["assembly_id"];
                        assemblies.Add(assembly);
                        assembly.LoadBytesFromSql();
                        AssemblyName[] aa = Parser.GetAssemblies();
                        AssemblyName a = Parser.Load(assembly.bytes);
                        AssemblyName[] ana = Parser.GetReferencedAssemblies(a);
                        foreach (AssemblyName an in ana)
                        {
                            if (aa.Count(p => p.Name.Equals(an.Name, StringComparison.OrdinalIgnoreCase)) > 0) continue;
                            try
                            {
                                Parser.ReflectionOnlyLoad(an);
                                continue;
                            }
                            catch (FileNotFoundException)
                            {
                                var drl = from DataRow r in dt.Rows where !r.IsNull("assembly_name") && (string)r["assembly_name"] == an.Name select r;
                                if (drl.Count() == 0)
                                {
                                    string root = Resource.errReferencedAssemblyNotFoundInDatabase.Replace("%ASSEMBLY1%", assembly.fullname).Replace("%ASSEMBLY2%", an.FullName);
                                    MessageBox.Show(root, "Assembly Not Found");
                                    asmok = false;
                                }
                                else
                                {
                                    DataRow r = drl.First();
                                    InstalledAssembly am = new InstalledAssembly();
                                    am.name = (string)r["assembly_name"];
                                    am.database = this;
                                    am.assembly_id = (int)r["assembly_id"];
                                    am.LoadBytesFromSql();
                                    AssemblyName n = Parser.Load(am.bytes);
                                    //if (n.FullName != an.FullName)
                                    //    MessageBox.Show(string.Format("Warning: installed version {0:G} of referenced assembly {1:G} does not match version {2:G} as referenced in assembly {3:G}", n.Version.ToString(4), n.Name, an.Version.ToString(4), a.Name));
                                }
                            }
                        }
                        if (!asmok) continue;
                        flist = Parser.parseAssembly(a);
                        externalassemblies.Add(a.FullName);
                        assembly.fullname = a.FullName;
                        assembly.version = a.Version;
                        assembly.culture = a.CultureInfo;
                        assembly.platform = a.ProcessorArchitecture;
                        assembly.hashcode = a.GetHashCode();
                        assembly.publicKeyToken = a.GetPublicKeyToken();
                        assembly.key = server.keys.FirstOrDefault(p => p.Thumbprint.SequenceEqual(assembly.publicKeyToken));
                        if (assembly.key != null) assembly.login = server.logins.FirstOrDefault(p => p.key == assembly.key);
                        assembly.status = installstatus.in_place;
                        assembly.changes_pending = false;
                        foreach (Function f in flist)
                        {
                            if (f.actions == null) f.actions = new List<Action>();
                            f.status = installstatus.not_installed;
                            f.assembly = assembly;
                            f.schema = assembly.database.default_schema;
                            if (f.type == "TA") f.trigger.target_schema = f.schema;
                            assembly.functions.Add(f);
                        }
                    }
                    if (!asmok) continue;
                    if (dr["function_name"].GetType().Name != "DBNull")
                    {
                        function = new Function();
                        function.assembly = assembly;
                        function.schema = (string)dr["schema"];
                        function.name = (string)dr["function_name"];
                        function.type = (string)dr["type"];
                        function.object_id = (int)dr["object_id"];
                        function.assembly_class = (string)dr["assembly_class"];
                        if (function.type != "UDT" && !dr.IsNull("assembly_method"))
                            function.assembly_method = (string)dr["assembly_method"];
                        if (function.type == "TA")
                            function.trigger = tlist.FirstOrDefault<Trigger>(t => t.object_id == function.object_id);
                        if (function.type == "FT")
                        {
                            string td = "";
                            foreach (Parameter p in columnlist)
                            {
                                string[] a = new string[] { "].[" };
                                string[] s = p.name.Split(a, StringSplitOptions.RemoveEmptyEntries);
                                if (s[0] == function.name)
                                {
                                    if (td == "") td = "TABLE ([" + s[1] + "] " + p.type;
                                    else td += ", [" + s[1] + "] " + p.type;
                                }
                            }
                            td += ")";
                            Parameter pout = new Parameter();
                            pout.object_id = function.object_id;
                            pout.function = function;
                            pout.name = "(output)";
                            pout.position = 0;
                            pout.type = td;
                            function.parameters.Add(pout);
                        }
                        function.status = installstatus.in_place;
                        function.changes_pending = false;
                        var pl = from parm in parmlist where parm.object_id == function.object_id orderby parm.position select parm;
                        function.parameters.AddRange(pl.ToList<Parameter>());
                        foreach (Parameter p in function.parameters) p.function = function;
                        function.actions = new List<Action>();
                        assembly.functions.Add(function);
                        Function f = flist.FirstOrDefault(fn =>
                            fn.type == function.type
                            && (
                                    fn.assembly_method == function.assembly_method && (fn.type == "FS" || fn.type == "FT" || fn.type == "PC" || fn.type == "TA")
                                    ||
                                    fn.assembly_class == function.assembly_class && (fn.type == "UDT" || fn.type == "AF")
                                ));
                        if (f != null)
                        {
                            flist.Remove(f);
                            assembly.functions.Remove(f);
                        }
                    }
                }

                //Assocated files
                comm.CommandText = AssemblyManager.Resource.sqlGetAssociatedFiles.Replace("%DATABASE%", name);
                dt.Clear();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    InstalledAssembly am = new InstalledAssembly();
                    am.is_assembly = false;
                    am.database = this;
                    am.assembly_id = (int)dr["assembly_id"];
                    am.bytes = (byte[])dr["content"];
                    am.name = (string)dr["name"];
                    assembly = assemblies.SingleOrDefault(p => p.assembly_id == am.assembly_id);
                    if (assembly != null)
                    {
                        assembly.subfiles.Add(am);
                        am.parent = assembly;
                    }
                }

                //References
                comm.CommandText = AssemblyManager.Resource.sqlGetReferences.Replace("%DATABASE%", name);
                dt.Clear();
                da.Fill(dt);
                foreach (DataRow dr in dt.Rows)
                {
                    int fromid = (int)dr["assembly_id"];
                    int toid = (int)dr["referenced_assembly_id"];
                    InstalledAssembly fromasm = assemblies.SingleOrDefault(p => p.assembly_id == fromid);
                    InstalledAssembly toasm = assemblies.SingleOrDefault(p => p.assembly_id == toid);
                    if (fromasm == null || toasm == null) continue;
                    if (toasm.dependents == null) toasm.dependents = new List<InstalledAssembly>();
                    toasm.dependents.Add(fromasm);
                    if (fromasm.references == null) fromasm.references = new List<InstalledAssembly>();
                    fromasm.references.Add(toasm);
                }

                if (tables != null) GetTables();

            }

        }

        [Serializable()]
        public class InstalledAssembly
        {
            public InstalledAssembly()
            {
                subfiles = new List<InstalledAssembly>();
                actions = new List<Action>();
                functions = new List<Function>();
                changes_pending = false;
                permission_set = 1;
            }
            public InstalledAssembly(InstalledAssembly am)
            {
                Function f2;

                assembly_id = am.assembly_id;
                clr_name = am.clr_name;
                create_date = am.create_date;
                culture = am.culture;
                changes_pending = am._changes_pending;
                database = am.database;
                subfiles = new List<InstalledAssembly>();
                actions = new List<Action>();
                functions = new List<Function>();
                if (am.references != null)
                {
                    references = new List<InstalledAssembly>();
                    foreach (InstalledAssembly refa in am.references)
                    {
                        references.Add(refa);
                    }
                }
                foreach (Function f in am.functions)
                {
                    f2 = new Function(f);
                    f2.assembly = this;
                    functions.Add(f2);
                }
                foreach (InstalledAssembly af in am.subfiles)
                {
                    InstalledAssembly af2 = new InstalledAssembly(af);
                    af2.parent = this;
                    af2.database = database;
                    subfiles.Add(af2);
                }
                hashcode = am.hashcode;
                modify_date = am.modify_date;
                name = am.name;
                fullname = am.fullname;
                permission_set = am.permission_set;
                platform = am.platform;
                publicKeyToken = am.publicKeyToken;
                status = am.status;
                version = am.version;
                bytes = am.bytes;
                is_assembly = am.is_assembly;
                parent = am.parent;
                key = am.key;
                login = am.login;
            }
            public void LoadBytesFromSql()
            {
                SqlConnection conn = database.server.connection;
                bool open = conn.State == ConnectionState.Open;
                string commandText = Resource.sqlGetAssemblyBytes;
                commandText = commandText.Replace("%DATABASE%", database.name);
                commandText = commandText.Replace("%ASSEMBLY_ID%", assembly_id.ToString());
                commandText = commandText.Replace("%ASSEMBLY%", name);
                SqlCommand comm = new SqlCommand(commandText, conn);
                if (!open) conn.Open();
                SqlDataReader dr = comm.ExecuteReader();
                dr.Read();
                bytes = (byte[])dr["content"];
                dr.Close();
                if (!open) conn.Close();
            }

            public string FQN
            {
                get
                {
                    string fqn = ".[" + name + "]";
                    if (!is_assembly) fqn = parent.FQN + fqn;
                    else if (database != null) fqn = database.FQN + fqn;
                    else fqn = "[].[]" + fqn;
                    return fqn;
                }
            }
            public bool changes_pending
            {
                get
                {
                    if (_changes_pending) return true;
                    foreach (Function f in functions) if (f.changes_pending) return true;
                    foreach (InstalledAssembly af in subfiles) if (af.changes_pending) return true;
                    return false;
                }
                set { _changes_pending = value; }
            }
            public int assembly_id;
            [NonSerialized()]
            public Database database;
            public string clr_name;
            public string fullname;
            public int permission_set;
            public bool is_visible;
            public bool is_assembly;
            public installstatus status;
            protected bool _changes_pending;
            public byte[] bytes;
            public string name;
            public DateTime create_date;
            public DateTime modify_date;
            public List<Function> functions = null;
            public List<InstalledAssembly> subfiles = null;
            public List<InstalledAssembly> dependents = null;
            public List<InstalledAssembly> references = null;
            [NonSerialized()]
            public List<Action> actions;
            public Version version;
            public CultureInfo culture;
            public ProcessorArchitecture platform;
            [NonSerialized()]
            public InstalledAssembly parent = null;
            public AsymmetricKey key = null;
            public Login login = null;
            public byte[] publicKeyToken;
            public int hashcode;
            [OnDeserialized()]
            internal void RelinkFunctionsAndFiles(StreamingContext context)
            {
                actions = new List<Action>();
                foreach (Function f in functions)
                {
                    f.actions = new List<Action>();
                    f.assembly = this;
                }
                foreach (InstalledAssembly af in subfiles) af.parent = this;
            }
        }

        [Serializable()]
        public class Parameter : IComparable
        {
            public Parameter()
            {
            }
            public Parameter(Parameter p)
            {
                position = p.position;
                object_id = p.object_id;
                name = p.name;
                type = p.type;
                default_value = p.default_value;
            }
            public int CompareTo(object o)
            {
                Parameter y = (Parameter)o;
                return position.CompareTo(y.position);
            }
            public string FQN
            {
                get
                {
                    string fqn = ".[" + name + "]";
                    if (function != null) fqn = function.FQN + fqn;
                    else fqn = "[].[].[].[]" + fqn;
                    return fqn;
                }
            }
            public int position;
            public int object_id;
            public string name;
            public string type;
            public string default_value;
            public int max_length;
            [NonSerialized()]
            public Function function;
        }

        [Serializable()]
        public class Function
        {
            public Function()
            {
                parameters = new List<Parameter>();
                actions = new List<Action>();
                signature_changed = false;
                not_defined = false;
                needs_installing = false;
            }
            public Function(Function fn)
            {
                assembly = fn.assembly;
                object_id = fn.object_id;
                schema = fn.schema;
                type = fn.type;
                name = fn.name;
                assembly_class = fn.assembly_class;
                assembly_method = fn.assembly_method;
                if (fn.trigger != null) trigger = new Trigger(fn.trigger);
                status = fn.status;
                changes_pending = fn.changes_pending;
                parameters = new List<Parameter>();
                foreach (Parameter p in fn.parameters)
                {
                    Parameter p2 = new Parameter(p);
                    p2.function = this;
                    parameters.Add(p2);
                }
                actions = new List<Action>();
                signature_changed = fn.signature_changed;
                not_defined = fn.not_defined;
                needs_installing = fn.needs_installing;
            }
            public string tooltiptext()
            {
                string tooltip = "";
                tooltip = FullFunctionTypeName();
                if (type == "TA" && trigger.isdatabase)
                {
                    tooltip = "Database Trigger: ";
                }
                tooltip += DetailedName();
                return tooltip;
            }
            public string ShortName(bool brackets)
            {
                if (brackets)
                {
                    if (type == "TA" && trigger.isdatabase) return "[" + name + "]";
                    if (schema == null || schema == "") return "[" + name + "]";
                    else return "[" + schema + "].[" + name + "]";
                }
                else
                {
                    if (schema == null || schema == "" || type == "TA" && trigger.isdatabase) return name;
                    else return schema + "." + name;
                }
            }
            public string DetailedName()
            {
                string s = name;
                if (type == "UDT" || type == "TA") return s;
                int pcount = type == "AF" || type == "FS" ? parameters.Count - 1 : parameters.Count;
                foreach (Parameter p in parameters)
                {
                    string n = p.name.Replace("@", "");
                    if (n == "(output)") s = Database.SqlNameFor(p.type, p.max_length) + " " + s;
                    else if (p.position == 1) s += "(" + n + " " + Database.SqlNameFor(p.type, p.max_length);
                    else s += ", " + n + " " + Database.SqlNameFor(p.type, p.max_length);
                    if (p.position == pcount) s += ")";
                }
                return s;
            }
            public string FullFunctionTypeName()
            {
                string name;
                switch (type)
                {
                    case "AF": name = "Aggregate Function: "; break;
                    case "FS": name = "Scalar Function: "; break;
                    case "FT": name = "Table-Valued Function: "; break;
                    case "PC": name = "Stored Procedure: "; break;
                    case "UDT": name = "User-Defined Type: "; break;
                    case "TA": name = "Trigger: "; break;
                    default: name = "Unknown Object: "; break;
                }
                return name;
            }
            public string ShortFunctionTypeName()
            {
                string name;
                switch (type)
                {
                    case "AF":
                    case "FS":
                    case "FT": name = "Function"; break;
                    case "PC": name = "Stored Procedure"; break;
                    case "UDT": name = "User-Defined Type"; break;
                    case "TA": name = "Trigger"; break;
                    default: name = "Unknown Object"; break;
                }
                return name;
            }
            public int FunctionTypeIconIndex()
            {
                int idx;
                switch (type)
                {
                    case "AF": idx = 6; break;
                    case "FS": idx = 6; break;
                    case "FT": idx = 7; break;
                    case "PC": idx = 8; break;
                    case "UDT": idx = 9; break;
                    case "TA": idx = 10; break;
                    default: idx = 12; break;
                }
                return idx;
            }
            public bool AllowScriptAsAlter()
            {
                if (type == "FS" || type == "FT" || type == "PC") return true;
                if (type == "TA" && !trigger.isdatabase) return true;
                return false;
            }
            public bool AllowScriptAsSelect()
            {
                if (type == "FS" || type == "FT") return true;
                return false;
            }
            public bool AllowScriptAsExecute()
            {
                if (type == "PC") return true;
                return false;
            }
            public bool AllowChangeSchema()
            {
                if (type == "TA") return false;
                if (status == installstatus.not_installed || status == installstatus.pending_remove) return false;
                return true;
            }
            public bool AllowSetParameterDefaults()
            {
                if ((type == "FS" || type == "FT" || type == "PC") && parameters.Count > 0) return true;
                return false;
            }
            public bool ShowAsInstalled()
            {
                if (status == installstatus.in_place || status == installstatus.pending_add) return true;
                return false;
            }
            public string FQN
            {
                get
                {
                    string fqn = ".[" + name + "]";
                    if (schema != null && schema != "") fqn = ".[" + schema + "]" + fqn;
                    if (assembly != null) fqn = assembly.FQN + fqn;
                    else fqn = "[].[].[]" + fqn;
                    return fqn;
                }
            }
            [NonSerialized()]
            public InstalledAssembly assembly;
            public int object_id;
            public string schema;
            public string type;
            public string name;
            public List<Parameter> parameters;
            [NonSerialized()]
            public List<Action> actions;
            public string assembly_class;
            public string assembly_method;
            public Trigger trigger;
            public installstatus status;
            public bool changes_pending;
            public bool signature_changed;
            public bool not_defined;
            public bool needs_installing;
        }

        [Serializable()]
        public class Trigger
        {
            public Trigger()
            {
                events = new List<string>();
            }
            public Trigger(Trigger t)
            {
                object_id = t.object_id;
                target = t.target;
                target_schema = t.target_schema;
                target_type = t.target_type;
                disabled = t.disabled;
                insteadof = t.insteadof;
                isdatabase = t.isdatabase;
                events = t.events.ToList();
            }
            public int object_id;
            public List<string> events;
            public string target;
            public string target_schema;
            public string target_type;
            public bool disabled;
            public bool insteadof;
            public bool isdatabase;
        }

        public class AsymmetricKey
        {
            public string Name;
            public byte[] Thumbprint;
            public byte[] SID;
            public InstalledAssembly assembly;
            public installstatus status;
        }

        public enum PermissionSet {SAFE=1, EXTERNAL_ACCESS=2, UNSAFE=3}

        public class Login
        {
            public string Name;
            public int PID;
            public byte[] SID;
            public AsymmetricKey key;
            public installstatus status;
            public PermissionSet permission;
        }

        public enum ActionType
        {
            AddFunction, AddAssembly, AddFile, AddKeyAndLogin, AddPermission, DropFunction, DropAssembly, DropFile, DropKeyAndLogin, SwapAssembly, AddAllObjects,
            DropAllObjects, DropAllAssemblies, DropPermission, ChangeFunctionSchema, ChangeDatabaseDefaultSchema, ChangePermissionSet,
            SetParameterDefault, ToggleTrustworthy, ToggleCLR, RenameFunction, RenameLibrary, ChangeTriggerEvents, ChangeTriggerTarget
        };

        public enum installstatus { in_place, pending_remove, pending_add, not_installed };

        public class Action
        {
            public Action()
            {
                subactions = new List<Action>();
            }
            public ActionType action;
            public int sequence;
            public object target;
            public TreeNode targetnode;
            public TreeNode actionnode;
            public object newvalue;
            public object oldvalue;
            public string displaytext;
            public string sqlcommand;
            public List<Action> subactions;
            public Action parent;
            public string path = "";
        }

        public class Library
        {
            bool cp;
            public Library()
            {
                actions = new List<Action>();
                assemblies = new List<InstalledAssembly>();
                cp = false;
            }
            public List<InstalledAssembly> assemblies;
            public List<Action> actions;
            public string name;
            public string file;
            public AppDomain domain = null;
            public bool changes_pending
            {
                get
                {
                    if (cp) return true;
                    foreach (InstalledAssembly am in assemblies)
                    {
                        if (am.changes_pending || am.actions.Count > 0) return true;
                        foreach (Function f in am.functions)
                        {
                            if (f.changes_pending || f.actions.Count > 0) return true;
                        }
                    }
                    return false;
                }
                set
                {
                    cp = value;
                }
            }
        }

        public class LicenseResult
        {
            public DateTime LicenseExpires = DateTime.MinValue;
            public string LicensedProduct = "";
            public bool LicenseValid = false;
        }

        public class UserSettings
        {
            RijndaelManaged rj = new RijndaelManaged();
            TripleDESCryptoServiceProvider td = new TripleDESCryptoServiceProvider();

            public List<SqlConnectionStringBuilder> Connections = new List<SqlConnectionStringBuilder>();
            public string LastServer = null;
            public byte[] License = new byte[8] {0, 0, 0, 0, 0, 0, 0, 0};
            public LicenseResult LicenseState = new LicenseResult();
            
            public bool LicenseFound()
            {
                for (int i = 0; i < 8; i++) if (License[i] != 0) return true;
                return false;
            }

            public LicenseResult CheckLicense(byte[] key)
            {
                LicenseResult result = new LicenseResult();
                ushort a, ticks = 0, prodcust = 0, cust2 = 0;
                for (int j = 3; j >= 0; j--)
                {
                    a = BitConverter.ToUInt16(key, j * 2);
                    for (int i = 0; i < 4; i++)
                    {
                        a = (ushort)(a >> 1);
                        cust2 = (ushort)((cust2 << 1) + (a & 1));
                        a = (ushort)(a >> 1);
                        prodcust = (ushort)((prodcust << 1) + (a & 1));
                        a = (ushort)(a >> 1);
                        ticks = (ushort)((ticks << 1) + (a & 1));
                        a = (ushort)(a >> 1);
                    }
                }
                ulong ticktotal = (ulong)new DateTime(2010, 1, 1).Ticks / 864000000000 + (ulong)ticks;
                DateTime d = new DateTime((long)ticktotal * 864000000000);
                string s;
                byte product = (byte)(prodcust & 0xFF);
                uint customer = cust2;
                customer = (uint)((customer << 8) + (prodcust >> 8));

                switch (product)
                {
                    case 0xEF: s = "AssemblyManager"; break;
                    case 0xBB: s = "SQLStatistics"; break;
                    case 0x48: s = "SQLMath"; break;
                    case 0x6D: s = "SQLUtilities"; break;
                    case 0xB1: s = "SQLEngineering"; break;
                    case 0x9E: s = "SQLDistributions"; break;
                    case 0x95: s = "SQLRollingStats"; break;
                    case 0xE4: s = "SQLFinancials"; break;
                    default: s = "Error"; break;
                }
                
                result.LicensedProduct = s;
                if (s == "AssemblyManager")
                {
                    result.LicenseExpires = d;
                    result.LicenseValid = d >= DateTime.UtcNow;
                }
                else
                {
                    result.LicenseExpires = DateTime.MinValue;
                    result.LicenseValid = false;
                }
                return result;
            }

            public void MakeTrialLicense()
            {
                Random rand = new Random();
                ulong tickbase = (ulong)new DateTime(2010, 1, 1).Ticks / 864000000000;
                DateTime expiry = DateTime.UtcNow.AddDays(30);
                ushort ticks = (ushort)((ulong)expiry.Ticks / 864000000000 - tickbase);
                ushort seed = (ushort)rand.Next();
                byte product = 0xEF;
                uint customer = 0xABCDEF;
                ushort prodcust = (ushort)((customer << 8) + product);
                ushort cust2 = (ushort)(customer >> 8);
                ushort a = 0;
                for (int j = 0; j < 4; j++)
                {
                    a = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        a = (ushort)((a << 1) + (ticks & 1)); ticks = (ushort)(ticks >> 1);
                        a = (ushort)((a << 1) + (prodcust & 1)); prodcust = (ushort)(prodcust >> 1);
                        a = (ushort)((a << 1) + (cust2 & 1)); cust2 = (ushort)(cust2 >> 1);
                        a = (ushort)((a << 1) + (seed & 1)); seed = (ushort)(seed >> 1);
                    }
                    BitConverter.GetBytes(a).CopyTo(License, j * 2);
                }
                Save();
                LicenseState = CheckLicense(License);
            }

            public void Obscure(SqlConnectionStringBuilder sb)
            {
                string uid = sb.UserID;
                uid = Convert.ToBase64String(encryptStringToBytes_AES(uid, rj.Key, rj.IV));
                sb.UserID = Convert.ToBase64String(Trip(uid, td.Key, td.IV));

                if (sb.Password != null && sb.Password != "")
                {
                    string pwd = sb.Password;
                    pwd = Convert.ToBase64String(encryptStringToBytes_AES(pwd, rj.Key, rj.IV));
                    sb.Password = Convert.ToBase64String(Trip(pwd, td.Key, td.IV));
                    pwd = null;
                }

                uid = null;
            }

            public void UnObscure(SqlConnectionStringBuilder sb)
            {
                string uid = sb.UserID;
                uid = UnTrip(Convert.FromBase64String(uid), td.Key, td.IV);
                sb.UserID = decryptStringFromBytes_AES(Convert.FromBase64String(uid), rj.Key, rj.IV);
                if (sb.Password != null && sb.Password != "")
                {
                    string pwd = sb.Password;
                    pwd = UnTrip(Convert.FromBase64String(pwd), td.Key, td.IV);
                    sb.Password = decryptStringFromBytes_AES(Convert.FromBase64String(pwd), rj.Key, rj.IV);
                    pwd = null;
                }
            }

            public void Save()
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (path == "") throw new DirectoryNotFoundException("No application data path found for the current user");
                path += "\\TotallySql\\AssemblyManager\\100\\bin";
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                path += "\\Studio.bin";
                FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write);
                fs.Write(License, 0, 8);
                if (LastServer == null) fs.WriteByte(0);
                else
                {
                    byte[] ls = Encoding.UTF8.GetBytes(LastServer);
                    fs.WriteByte((byte)ls.Length);
                    fs.Write(ls, 0, ls.Length);
                }
                fs.Write(rj.Key, 0, rj.Key.Length);
                fs.Write(td.IV, 0, td.IV.Length);
                foreach (SqlConnectionStringBuilder sb in Connections)
                {
                    byte[] ba = Trip(Convert.ToBase64String(encryptStringToBytes_AES(sb.ConnectionString, rj.Key, rj.IV)), td.Key, td.IV);
                    byte[] bal = BitConverter.GetBytes(ba.Length);
                    fs.WriteByte(bal[3]);
                    fs.WriteByte(bal[1]);
                    fs.WriteByte(bal[2]);
                    fs.WriteByte(bal[0]);
                    fs.Write(ba, 0, ba.Length);
                    if (!sb.IntegratedSecurity)
                    {
                        string p = (sb.Password == null || sb.Password == "") ? "(not persisted)" : sb.Password;
                        ba = Trip(Convert.ToBase64String(encryptStringToBytes_AES(p, rj.Key, rj.IV)), td.Key, td.IV);
                        bal = BitConverter.GetBytes(ba.Length);
                        fs.WriteByte(bal[1]);
                        fs.WriteByte(bal[3]);
                        fs.WriteByte(bal[0]);
                        fs.WriteByte(bal[2]);
                        fs.Write(ba, 0, ba.Length);
                    }
                }
                fs.Write(td.Key, 0, td.Key.Length);
                fs.Write(rj.IV, 0, rj.IV.Length);
                fs.Flush();
                fs.Close();
            }

            public void Load()
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (path == "") path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                path += "\\TotallySql\\AssemblyManager\\100\\bin";
                if (!Directory.Exists(path)) return;
                path += "\\Studio.bin";
                if (!File.Exists(path)) return;
                FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read);
                fs.Read(License, 0, 8);
                int n = fs.ReadByte();
                if (n != 0)
                {
                    byte[] ls = new byte[n];
                    fs.Read(ls, 0, n);
                    this.LastServer = Encoding.UTF8.GetString(ls, 0, n);
                }
                byte[] ba = new byte[rj.Key.Length];
                fs.Read(ba, 0, rj.Key.Length);
                rj.Key = ba;
                ba = new byte[td.IV.Length];
                fs.Read(ba, 0, td.IV.Length);
                td.IV = ba;
                fs.Position = fs.Length - (td.Key.Length + rj.IV.Length);
                ba = new byte[td.Key.Length];
                fs.Read(ba, 0, td.Key.Length);
                td.Key = ba;
                ba = new byte[rj.IV.Length];
                fs.Read(ba, 0, rj.IV.Length);
                rj.IV = ba;
                fs.Position = (rj.Key.Length + td.IV.Length) + n + 9;
                while (fs.Position < fs.Length - (td.Key.Length + rj.IV.Length))
                {
                    byte[] bal = new byte[4];
                    bal[3] = (byte)fs.ReadByte();
                    bal[1] = (byte)fs.ReadByte();
                    bal[2] = (byte)fs.ReadByte();
                    bal[0] = (byte)fs.ReadByte();
                    int i = BitConverter.ToInt32(bal, 0);
                    ba = new byte[i];
                    fs.Read(ba, 0, i);
                    string s = decryptStringFromBytes_AES(Convert.FromBase64String(UnTrip(ba, td.Key, td.IV)), rj.Key, rj.IV);
                    SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(s);

                    if (!sb.IntegratedSecurity)
                    {
                        bal[1] = (byte)fs.ReadByte();
                        bal[3] = (byte)fs.ReadByte();
                        bal[0] = (byte)fs.ReadByte();
                        bal[2] = (byte)fs.ReadByte();
                        i = BitConverter.ToInt32(bal, 0);
                        ba = new byte[i];
                        fs.Read(ba, 0, i);
                        string p = decryptStringFromBytes_AES(Convert.FromBase64String(UnTrip(ba, td.Key, td.IV)), rj.Key, rj.IV);
                        sb.Password = p == "(not persisted)" ? "" : p;
                    }
                    Connections.Add(sb);
                }
                fs.Close();
            }
        }

        #endregion Internal Classes

    }

    public class AssemblyParser : MarshalByRefObject
    {
        private static Dictionary<string, Assembly> Assemblies = new Dictionary<string, Assembly>();

        public AssemblyParser()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string name = args.Name.Split(',')[0];
            string key = Assemblies.Keys.FirstOrDefault(p => p.Split(',')[0] == name);
            if (key != null) return Assemblies[key];
            throw new FileNotFoundException();
        }

        public List<MainAssemblyMgrForm.Function> parseAssembly(byte[] abytes)
        {
            Assembly a = Assembly.Load(abytes);
            return parseAssembly(a);
        }

        public List<MainAssemblyMgrForm.Function> parseAssembly(AssemblyName n)
        {
            Assembly a = Assemblies[n.FullName];
            return parseAssembly(a);
        }

        public List<MainAssemblyMgrForm.Function> parseAssembly(Assembly a)
        {
            List<MainAssemblyMgrForm.Function> flist = new List<MainAssemblyMgrForm.Function>();
            MainAssemblyMgrForm.Function f;
            MainAssemblyMgrForm.Parameter p;

            foreach (Type t in a.GetExportedTypes())
            {
                List<CustomAttributeData> attlist = new List<CustomAttributeData>(t.GetCustomAttributesData());
                foreach (CustomAttributeData o in t.GetCustomAttributesData())
                {
                    string thing = o.ToString();
                    if (!thing.Contains("SqlUserDefinedTypeAttribute") && !thing.Contains("SqlUserDefinedAggregateAttribute")) continue;
                    f = new MainAssemblyMgrForm.Function();
                    if (t.Namespace != null && t.Namespace != "") f.assembly_class = t.Namespace + "." + t.Name;
                    else f.assembly_class = t.Name;
                    f.status = MainAssemblyMgrForm.installstatus.in_place;
                    f.changes_pending = false;
                    //f.actions = new List<MainAssemblyMgrForm.Action>();
                    f.schema = "";
                    //if (o.GetType().Name == "SqlUserDefinedTypeAttribute")
                    if (thing.Contains("SqlUserDefinedTypeAttribute"))
                    {
                        //                            SqlUserDefinedTypeAttribute udt = (SqlUserDefinedTypeAttribute)o;
                        f.name = t.Name;
                        f.type = "UDT";
                        flist.Add(f);
                    }
                    else if (thing.Contains("SqlUserDefinedAggregateAttribute"))
                    {
                        //                            SqlUserDefinedAggregateAttribute aa = (SqlUserDefinedAggregateAttribute)o;
                        f.name = t.Name;
                        f.type = "AF";
                        //f.parameters = new List<MainAssemblyMgrForm.Parameter>();

                        string rtntype = "ERROR", parmtype = "ERROR", parmname = "ERROR";
                        foreach (MemberInfo member in t.GetMembers())
                        {
                            if (member.Name == "Accumulate")
                            {
                                MethodInfo method = (MethodInfo)member;
                                int n = method.GetParameters().Count();
                                for (int i = 0; i < n; i++)
                                {
                                    parmtype = method.GetParameters()[i].ParameterType.Name;
                                    parmname = "@" + method.GetParameters()[i].Name;
                                    p = new MainAssemblyMgrForm.Parameter();
                                    p.name = parmname;
                                    p.position = i + 1;
                                    p.type = MainAssemblyMgrForm.Database.SqlNameFor(parmtype, -1);
                                    p.max_length = MainAssemblyMgrForm.Database.LengthOfType(p.type);
                                    f.parameters.Add(p);
                                    p.function = f;
                                }
                            }
                            else if (member.Name == "Terminate")
                            {
                                MethodInfo method = (MethodInfo)member;
                                rtntype = method.ReturnType.Name;
                                p = new MainAssemblyMgrForm.Parameter();
                                p.name = "(output)";
                                p.position = 0;
                                p.type = MainAssemblyMgrForm.Database.SqlNameFor(rtntype, -1);
                                f.parameters.Add(p);
                                p.function = f;
                            }
                        }
                        flist.Add(f);
                    }
                }

                foreach (MemberInfo mi in t.GetMembers())
                {
                    foreach (object o in mi.GetCustomAttributes(false))
                    {
                        string attrtype = o.GetType().Name;
                        if (attrtype != "SqlFunctionAttribute" && attrtype != "SqlTriggerAttribute" && attrtype != "SqlProcedureAttribute") break;
                        f = new MainAssemblyMgrForm.Function();
                        if (t.Namespace != null && t.Namespace != "") f.assembly_class = t.Namespace + "." + t.Name;
                        else f.assembly_class = t.Name;
                        f.assembly_method = mi.Name;
                        f.status = MainAssemblyMgrForm.installstatus.in_place;
                        f.changes_pending = false;
                        f.schema = "";
                        //f.parameters = new List<MainAssemblyMgrForm.Parameter>();
                        //f.actions = new List<MainAssemblyMgrForm.Action>();
                        if (attrtype == "SqlFunctionAttribute")
                        {
                            SqlFunctionAttribute sfa = (SqlFunctionAttribute)o;
                            f.name = sfa.Name == null ? mi.Name : sfa.Name;
                            p = new MainAssemblyMgrForm.Parameter();
                            p.name = "(output)";
                            p.position = 0;
                            if (sfa.TableDefinition == null)
                            {
                                f.type = "FS";
                                p.type = ((MethodInfo)mi).ReturnType.Name;
                                p.max_length = MainAssemblyMgrForm.Database.LengthOfType(p.type);
                                p.type = MainAssemblyMgrForm.Database.SqlNameFor(p.type, p.max_length);
                            }
                            else
                            {
                                f.type = "FT";
                                p.type = "TABLE (" + sfa.TableDefinition + ")";
                                p.max_length = MainAssemblyMgrForm.Database.LengthOfType(p.type);
                            }
                            f.parameters.Add(p);
                            p.function = f;
                        }
                        else if (attrtype == "SqlTriggerAttribute")
                        {
                            f.type = "TA";
                            SqlTriggerAttribute ta = (SqlTriggerAttribute)o;
                            f.name = ta.Name == null ? mi.Name : ta.Name;
                            f.trigger = new MainAssemblyMgrForm.Trigger();
                            string[] target = ta.Target.Split('.');
                            if (target.Length > 1)
                            {
                                f.trigger.target_schema = target[0].Replace("[", "").Replace("]", "");
                                f.trigger.target = target[1].Replace("[", "").Replace("]", "");
                            }
                            else
                            {
                                f.trigger.target_schema = "";
                                f.trigger.target = target[0].Replace("[", "").Replace("]", "");
                            }
                            f.trigger.isdatabase = (f.trigger.target == "DATABASE");
                            f.trigger.events = new List<string>();
                            if (ta.Event != null)
                            {
                                string[] events = ta.Event.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                if (events[0] == "FOR" || events[0] == "AFTER") f.trigger.insteadof = false;
                                else f.trigger.insteadof = true;
                                for (int i = 1; i < events.Length; i++) f.trigger.events.Add(events[i]);
                            }
                        }
                        else if (attrtype == "SqlProcedureAttribute")
                        {
                            SqlProcedureAttribute sp = (SqlProcedureAttribute)o;
                            f.name = sp.Name == null ? mi.Name : sp.Name;
                            f.type = "PC";
                        }
                        else break;
                        foreach (ParameterInfo pi in ((MethodInfo)mi).GetParameters())
                        {
                            p = new MainAssemblyMgrForm.Parameter();
                            p.name = "@" + pi.Name;
                            p.position = pi.Position + 1;
                            p.type = MainAssemblyMgrForm.Database.SqlNameFor(pi.ParameterType.Name, -1);
                            p.max_length = MainAssemblyMgrForm.Database.LengthOfType(p.type);
                            f.parameters.Add(p);
                            p.function = f;
                        }
                        f.parameters.Sort();
                        flist.Add(f);
                    }
                }
            }
            return flist;
        }

        public AssemblyName Load(byte[] abytes)
        {
            //Assembly a = Assembly.Load(abytes);
            Assembly a = AppDomain.CurrentDomain.Load(abytes);
            AssemblyName n = a.GetName();
            if (!Assemblies.Keys.Contains(n.FullName)) Assemblies.Add(n.FullName, a);
            return n;
        }

        public AssemblyName Load(AssemblyName Aname)
        {
            if (Assemblies.Keys.Contains(Aname.FullName)) return Aname;
            Assembly a = Assembly.Load(Aname);
            AssemblyName n = a.GetName();
            Assemblies.Add(n.FullName, a);
            return n;
        }

        public AssemblyName ReflectionOnlyLoad(AssemblyName Aname)
        {
            if (Assemblies.Keys.Contains(Aname.FullName)) return Aname;
            Assembly a = Assembly.ReflectionOnlyLoad(Aname.FullName);
            AssemblyName n = a.GetName();
            return n;
        }

        public AssemblyName Load(string Fname)
        {
            if (Assemblies.Keys.Contains(Fname)) return Assemblies[Fname].GetName();
            Assembly a = Assembly.Load(Fname);
            AssemblyName n = a.GetName();
            Assemblies.Add(Fname, a);
            return n;
        }
        public AssemblyName[] GetReferencedAssemblies(AssemblyName n)
        {
            Assembly a = Assemblies[n.FullName];
            return a.GetReferencedAssemblies();
        }
        public AssemblyName[] GetAssemblies()
        {
            Assembly[] aa = AppDomain.CurrentDomain.GetAssemblies();
            AssemblyName[] ana = new AssemblyName[aa.Length];
            for (int i = 0; i < aa.Length; i++) ana[i] = aa[i].GetName();
            return ana;
        }
    }
             

}
