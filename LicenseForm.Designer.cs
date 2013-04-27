namespace AssemblyManager
{
    partial class LicenseForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LicenseForm));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.Prompt = new System.Windows.Forms.Label();
            this.rbLicensed = new System.Windows.Forms.RadioButton();
            this.tbKey = new System.Windows.Forms.TextBox();
            this.rbTrial = new System.Windows.Forms.RadioButton();
            this.btOK = new System.Windows.Forms.Button();
            this.btQuit = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Left;
            this.pictureBox1.Image = global::AssemblyManager.Resource.Assembly_Manager_Blue_400_x_300;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(300, 257);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            // 
            // label1
            // 
            this.label1.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.label1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(300, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(426, 66);
            this.label1.TabIndex = 2;
            this.label1.Text = "Assembly Manager License Control";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btQuit);
            this.groupBox1.Controls.Add(this.btOK);
            this.groupBox1.Controls.Add(this.rbTrial);
            this.groupBox1.Controls.Add(this.tbKey);
            this.groupBox1.Controls.Add(this.rbLicensed);
            this.groupBox1.Controls.Add(this.Prompt);
            this.groupBox1.Location = new System.Drawing.Point(306, 69);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(408, 176);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            // 
            // Prompt
            // 
            this.Prompt.AutoSize = true;
            this.Prompt.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Prompt.Location = new System.Drawing.Point(7, 20);
            this.Prompt.Name = "Prompt";
            this.Prompt.Size = new System.Drawing.Size(144, 13);
            this.Prompt.TabIndex = 0;
            this.Prompt.Text = "Please select an option:";
            // 
            // rbLicensed
            // 
            this.rbLicensed.AutoSize = true;
            this.rbLicensed.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rbLicensed.Location = new System.Drawing.Point(10, 53);
            this.rbLicensed.Name = "rbLicensed";
            this.rbLicensed.Size = new System.Drawing.Size(147, 17);
            this.rbLicensed.TabIndex = 1;
            this.rbLicensed.TabStop = true;
            this.rbLicensed.Text = "I have a product key:";
            this.rbLicensed.UseVisualStyleBackColor = true;
            // 
            // tbKey
            // 
            this.tbKey.Location = new System.Drawing.Point(164, 53);
            this.tbKey.Name = "tbKey";
            this.tbKey.Size = new System.Drawing.Size(238, 20);
            this.tbKey.TabIndex = 2;
            this.tbKey.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // rbTrial
            // 
            this.rbTrial.AutoSize = true;
            this.rbTrial.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rbTrial.Location = new System.Drawing.Point(10, 96);
            this.rbTrial.Name = "rbTrial";
            this.rbTrial.Size = new System.Drawing.Size(335, 17);
            this.rbTrial.TabIndex = 3;
            this.rbTrial.TabStop = true;
            this.rbTrial.Text = "I would like to try Assembly Manager FREE for 30 days";
            this.rbTrial.UseVisualStyleBackColor = true;
            this.rbTrial.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // btOK
            // 
            this.btOK.Enabled = false;
            this.btOK.Location = new System.Drawing.Point(327, 147);
            this.btOK.Name = "btOK";
            this.btOK.Size = new System.Drawing.Size(75, 23);
            this.btOK.TabIndex = 4;
            this.btOK.Text = "OK";
            this.btOK.UseVisualStyleBackColor = true;
            this.btOK.Click += new System.EventHandler(this.btOK_Click);
            // 
            // btQuit
            // 
            this.btQuit.Location = new System.Drawing.Point(235, 147);
            this.btQuit.Name = "btQuit";
            this.btQuit.Size = new System.Drawing.Size(75, 23);
            this.btQuit.TabIndex = 5;
            this.btQuit.Text = "Quit";
            this.btQuit.UseVisualStyleBackColor = true;
            this.btQuit.Click += new System.EventHandler(this.btQuit_Click);
            // 
            // LicenseForm
            // 
            this.AcceptButton = this.btOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(726, 257);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pictureBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "LicenseForm";
            this.Text = "Assembly Manager License Control";
            this.Activated += new System.EventHandler(this.LicenseForm_Activated);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label Prompt;
        private System.Windows.Forms.Button btQuit;
        private System.Windows.Forms.Button btOK;
        public System.Windows.Forms.RadioButton rbTrial;
        public System.Windows.Forms.TextBox tbKey;
        public System.Windows.Forms.RadioButton rbLicensed;



    }
}