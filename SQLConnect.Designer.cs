namespace AssemblyManager
{
    partial class SQLConnect
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SQLConnect));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cbServerName = new System.Windows.Forms.ComboBox();
            this.cbAuthentication = new System.Windows.Forms.ComboBox();
            this.lUserName = new System.Windows.Forms.Label();
            this.lPassword = new System.Windows.Forms.Label();
            this.cbUserName = new System.Windows.Forms.ComboBox();
            this.tbPassword = new System.Windows.Forms.TextBox();
            this.xbRememberPassword = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.bConnect = new System.Windows.Forms.Button();
            this.bCancel = new System.Windows.Forms.Button();
            this.bHelp = new System.Windows.Forms.Button();
            this.bOptions = new System.Windows.Forms.Button();
            this.gbNetwork = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.udPacketSize = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.cbProtocol = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.gbConnection = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.xbEncrypt = new System.Windows.Forms.CheckBox();
            this.udExecutionTimeout = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.udConnectionTimeout = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.bReset = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.gbNetwork.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udPacketSize)).BeginInit();
            this.gbConnection.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udExecutionTimeout)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udConnectionTimeout)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "&Server name:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(78, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "&Authentication:";
            // 
            // cbServerName
            // 
            this.cbServerName.FormattingEnabled = true;
            this.cbServerName.Items.AddRange(new object[] {
            "(local)",
            "<Browse for more...>"});
            this.cbServerName.Location = new System.Drawing.Point(123, 10);
            this.cbServerName.Name = "cbServerName";
            this.cbServerName.Size = new System.Drawing.Size(278, 21);
            this.cbServerName.TabIndex = 2;
            this.cbServerName.SelectedIndexChanged += new System.EventHandler(this.cbServerName_SelectedIndexChanged);
            // 
            // cbAuthentication
            // 
            this.cbAuthentication.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbAuthentication.Items.AddRange(new object[] {
            "Windows Authentication",
            "SQL Server Authentication"});
            this.cbAuthentication.Location = new System.Drawing.Point(123, 37);
            this.cbAuthentication.Name = "cbAuthentication";
            this.cbAuthentication.Size = new System.Drawing.Size(278, 21);
            this.cbAuthentication.TabIndex = 3;
            this.cbAuthentication.SelectedIndexChanged += new System.EventHandler(this.cbAuthentication_SelectedIndexChanged);
            // 
            // lUserName
            // 
            this.lUserName.AutoSize = true;
            this.lUserName.Enabled = false;
            this.lUserName.Location = new System.Drawing.Point(39, 67);
            this.lUserName.Name = "lUserName";
            this.lUserName.Size = new System.Drawing.Size(63, 13);
            this.lUserName.TabIndex = 4;
            this.lUserName.Text = "&User Name:";
            // 
            // lPassword
            // 
            this.lPassword.AutoSize = true;
            this.lPassword.Enabled = false;
            this.lPassword.Location = new System.Drawing.Point(46, 94);
            this.lPassword.Name = "lPassword";
            this.lPassword.Size = new System.Drawing.Size(56, 13);
            this.lPassword.TabIndex = 5;
            this.lPassword.Text = "&Password:";
            // 
            // cbUserName
            // 
            this.cbUserName.Enabled = false;
            this.cbUserName.FormattingEnabled = true;
            this.cbUserName.Location = new System.Drawing.Point(150, 64);
            this.cbUserName.Name = "cbUserName";
            this.cbUserName.Size = new System.Drawing.Size(251, 21);
            this.cbUserName.TabIndex = 6;
            this.cbUserName.Text = "Charles-Laptop\\Charles";
            // 
            // tbPassword
            // 
            this.tbPassword.Enabled = false;
            this.tbPassword.Location = new System.Drawing.Point(150, 91);
            this.tbPassword.Name = "tbPassword";
            this.tbPassword.PasswordChar = '*';
            this.tbPassword.Size = new System.Drawing.Size(251, 20);
            this.tbPassword.TabIndex = 7;
            this.tbPassword.UseSystemPasswordChar = true;
            // 
            // xbRememberPassword
            // 
            this.xbRememberPassword.AutoSize = true;
            this.xbRememberPassword.Enabled = false;
            this.xbRememberPassword.Location = new System.Drawing.Point(150, 117);
            this.xbRememberPassword.Name = "xbRememberPassword";
            this.xbRememberPassword.Size = new System.Drawing.Size(125, 17);
            this.xbRememberPassword.TabIndex = 8;
            this.xbRememberPassword.Text = "Re&member password";
            this.xbRememberPassword.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.bConnect);
            this.panel1.Controls.Add(this.bCancel);
            this.panel1.Controls.Add(this.bHelp);
            this.panel1.Controls.Add(this.bOptions);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 147);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(413, 34);
            this.panel1.TabIndex = 9;
            // 
            // bConnect
            // 
            this.bConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bConnect.Location = new System.Drawing.Point(82, 3);
            this.bConnect.Name = "bConnect";
            this.bConnect.Size = new System.Drawing.Size(75, 23);
            this.bConnect.TabIndex = 3;
            this.bConnect.Text = "&Connect";
            this.bConnect.UseVisualStyleBackColor = true;
            this.bConnect.Click += new System.EventHandler(this.bConnect_Click);
            // 
            // bCancel
            // 
            this.bCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bCancel.Location = new System.Drawing.Point(163, 3);
            this.bCancel.Name = "bCancel";
            this.bCancel.Size = new System.Drawing.Size(75, 23);
            this.bCancel.TabIndex = 2;
            this.bCancel.Text = "Cancel";
            this.bCancel.UseVisualStyleBackColor = true;
            this.bCancel.Click += new System.EventHandler(this.bCancel_Click);
            // 
            // bHelp
            // 
            this.bHelp.Location = new System.Drawing.Point(244, 3);
            this.bHelp.Name = "bHelp";
            this.bHelp.Size = new System.Drawing.Size(75, 23);
            this.bHelp.TabIndex = 1;
            this.bHelp.Text = "Help";
            this.bHelp.UseVisualStyleBackColor = true;
            // 
            // bOptions
            // 
            this.bOptions.Location = new System.Drawing.Point(325, 3);
            this.bOptions.Name = "bOptions";
            this.bOptions.Size = new System.Drawing.Size(75, 23);
            this.bOptions.TabIndex = 0;
            this.bOptions.Text = "&Options >>";
            this.bOptions.UseVisualStyleBackColor = true;
            this.bOptions.Click += new System.EventHandler(this.bOptions_Click);
            // 
            // gbNetwork
            // 
            this.gbNetwork.Controls.Add(this.label7);
            this.gbNetwork.Controls.Add(this.udPacketSize);
            this.gbNetwork.Controls.Add(this.label4);
            this.gbNetwork.Controls.Add(this.cbProtocol);
            this.gbNetwork.Controls.Add(this.label3);
            this.gbNetwork.Location = new System.Drawing.Point(12, 149);
            this.gbNetwork.Name = "gbNetwork";
            this.gbNetwork.Size = new System.Drawing.Size(388, 84);
            this.gbNetwork.TabIndex = 10;
            this.gbNetwork.TabStop = false;
            this.gbNetwork.Text = "Network";
            this.gbNetwork.Visible = false;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(253, 53);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(32, 13);
            this.label7.TabIndex = 8;
            this.label7.Text = "bytes";
            // 
            // udPacketSize
            // 
            this.udPacketSize.Location = new System.Drawing.Point(162, 51);
            this.udPacketSize.Maximum = new decimal(new int[] {
            32767,
            0,
            0,
            0});
            this.udPacketSize.Minimum = new decimal(new int[] {
            512,
            0,
            0,
            0});
            this.udPacketSize.Name = "udPacketSize";
            this.udPacketSize.Size = new System.Drawing.Size(84, 20);
            this.udPacketSize.TabIndex = 7;
            this.udPacketSize.Value = new decimal(new int[] {
            4096,
            0,
            0,
            0});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 53);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(107, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Network packet size:";
            // 
            // cbProtocol
            // 
            this.cbProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbProtocol.Items.AddRange(new object[] {
            "<default>",
            "Shared Memory",
            "TCP/IP",
            "Named Pipes"});
            this.cbProtocol.Location = new System.Drawing.Point(162, 24);
            this.cbProtocol.Name = "cbProtocol";
            this.cbProtocol.Size = new System.Drawing.Size(220, 21);
            this.cbProtocol.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 27);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(91, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Network protocol:";
            // 
            // gbConnection
            // 
            this.gbConnection.Controls.Add(this.label9);
            this.gbConnection.Controls.Add(this.label8);
            this.gbConnection.Controls.Add(this.xbEncrypt);
            this.gbConnection.Controls.Add(this.udExecutionTimeout);
            this.gbConnection.Controls.Add(this.label6);
            this.gbConnection.Controls.Add(this.udConnectionTimeout);
            this.gbConnection.Controls.Add(this.label5);
            this.gbConnection.Location = new System.Drawing.Point(13, 248);
            this.gbConnection.Name = "gbConnection";
            this.gbConnection.Size = new System.Drawing.Size(388, 114);
            this.gbConnection.TabIndex = 11;
            this.gbConnection.TabStop = false;
            this.gbConnection.Text = "Connection";
            this.gbConnection.Visible = false;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(251, 54);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 13);
            this.label9.TabIndex = 14;
            this.label9.Text = "seconds";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(252, 28);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 13);
            this.label8.TabIndex = 13;
            this.label8.Text = "seconds";
            // 
            // xbEncrypt
            // 
            this.xbEncrypt.AutoSize = true;
            this.xbEncrypt.Location = new System.Drawing.Point(9, 91);
            this.xbEncrypt.Name = "xbEncrypt";
            this.xbEncrypt.Size = new System.Drawing.Size(118, 17);
            this.xbEncrypt.TabIndex = 12;
            this.xbEncrypt.Text = "Encrypt connection";
            this.xbEncrypt.UseVisualStyleBackColor = true;
            // 
            // udExecutionTimeout
            // 
            this.udExecutionTimeout.Location = new System.Drawing.Point(161, 52);
            this.udExecutionTimeout.Maximum = new decimal(new int[] {
            30000,
            0,
            0,
            0});
            this.udExecutionTimeout.Name = "udExecutionTimeout";
            this.udExecutionTimeout.Size = new System.Drawing.Size(84, 20);
            this.udExecutionTimeout.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 54);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(97, 13);
            this.label6.TabIndex = 10;
            this.label6.Text = "Execution time-out:";
            // 
            // udConnectionTimeout
            // 
            this.udConnectionTimeout.Location = new System.Drawing.Point(162, 26);
            this.udConnectionTimeout.Maximum = new decimal(new int[] {
            30000,
            0,
            0,
            0});
            this.udConnectionTimeout.Name = "udConnectionTimeout";
            this.udConnectionTimeout.Size = new System.Drawing.Size(84, 20);
            this.udConnectionTimeout.TabIndex = 9;
            this.udConnectionTimeout.Value = new decimal(new int[] {
            15,
            0,
            0,
            0});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 28);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(104, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "Connection time-out:";
            // 
            // bReset
            // 
            this.bReset.Location = new System.Drawing.Point(286, 368);
            this.bReset.Name = "bReset";
            this.bReset.Size = new System.Drawing.Size(108, 24);
            this.bReset.TabIndex = 15;
            this.bReset.Text = "Reset All";
            this.bReset.UseVisualStyleBackColor = true;
            this.bReset.Visible = false;
            this.bReset.Click += new System.EventHandler(this.bReset_Click);
            // 
            // SQLConnect
            // 
            this.AcceptButton = this.bConnect;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.bCancel;
            this.ClientSize = new System.Drawing.Size(413, 181);
            this.Controls.Add(this.bReset);
            this.Controls.Add(this.gbConnection);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.xbRememberPassword);
            this.Controls.Add(this.tbPassword);
            this.Controls.Add(this.cbUserName);
            this.Controls.Add(this.lPassword);
            this.Controls.Add(this.lUserName);
            this.Controls.Add(this.cbAuthentication);
            this.Controls.Add(this.cbServerName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.gbNetwork);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SQLConnect";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Connect to SQL Server";
            this.Load += new System.EventHandler(this.SQLConnect_Load);
            this.panel1.ResumeLayout(false);
            this.gbNetwork.ResumeLayout(false);
            this.gbNetwork.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udPacketSize)).EndInit();
            this.gbConnection.ResumeLayout(false);
            this.gbConnection.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udExecutionTimeout)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udConnectionTimeout)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lUserName;
        private System.Windows.Forms.Label lPassword;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button bConnect;
        private System.Windows.Forms.Button bCancel;
        private System.Windows.Forms.Button bHelp;
        public System.Windows.Forms.ComboBox cbServerName;
        public System.Windows.Forms.ComboBox cbAuthentication;
        public System.Windows.Forms.ComboBox cbUserName;
        public System.Windows.Forms.TextBox tbPassword;
        public System.Windows.Forms.CheckBox xbRememberPassword;
        public System.Windows.Forms.Button bOptions;
        private System.Windows.Forms.GroupBox gbNetwork;
        public System.Windows.Forms.ComboBox cbProtocol;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox gbConnection;
        private System.Windows.Forms.NumericUpDown udPacketSize;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown udConnectionTimeout;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown udExecutionTimeout;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox xbEncrypt;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button bReset;
    }
}