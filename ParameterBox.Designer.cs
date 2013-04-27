namespace AssemblyManager
{
    partial class ParameterBox
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParameterBox));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lvParameters = new System.Windows.Forms.ListView();
            this.colParameter = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDefault = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.button1 = new System.Windows.Forms.Button();
            this.cmParameter = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addEditDefaultValueToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.cmParameter.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lvParameters);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.button1);
            this.splitContainer1.Size = new System.Drawing.Size(524, 281);
            this.splitContainer1.SplitterDistance = 230;
            this.splitContainer1.TabIndex = 1;
            // 
            // lvParameters
            // 
            this.lvParameters.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colParameter,
            this.colType,
            this.colDefault});
            this.lvParameters.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvParameters.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lvParameters.FullRowSelect = true;
            this.lvParameters.GridLines = true;
            this.lvParameters.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lvParameters.HoverSelection = true;
            this.lvParameters.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.lvParameters.LabelWrap = false;
            this.lvParameters.Location = new System.Drawing.Point(0, 0);
            this.lvParameters.MultiSelect = false;
            this.lvParameters.Name = "lvParameters";
            this.lvParameters.Scrollable = false;
            this.lvParameters.ShowGroups = false;
            this.lvParameters.Size = new System.Drawing.Size(524, 230);
            this.lvParameters.TabIndex = 0;
            this.lvParameters.UseCompatibleStateImageBehavior = false;
            this.lvParameters.View = System.Windows.Forms.View.Details;
            this.lvParameters.ItemActivate += new System.EventHandler(this.getParameterDefault);
            this.lvParameters.SelectedIndexChanged += new System.EventHandler(this.lvParameters_SelectedIndexChanged);
            // 
            // colParameter
            // 
            this.colParameter.Text = "Parameter";
            this.colParameter.Width = 157;
            // 
            // colType
            // 
            this.colType.Text = "Type";
            this.colType.Width = 141;
            // 
            // colDefault
            // 
            this.colDefault.Text = "Default Value";
            this.colDefault.Width = 223;
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button1.Location = new System.Drawing.Point(421, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(91, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // cmParameter
            // 
            this.cmParameter.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addEditDefaultValueToolStripMenuItem});
            this.cmParameter.Name = "cmParameter";
            this.cmParameter.Size = new System.Drawing.Size(204, 26);
            // 
            // addEditDefaultValueToolStripMenuItem
            // 
            this.addEditDefaultValueToolStripMenuItem.Name = "addEditDefaultValueToolStripMenuItem";
            this.addEditDefaultValueToolStripMenuItem.Size = new System.Drawing.Size(203, 22);
            this.addEditDefaultValueToolStripMenuItem.Text = "Add/Edit Default Value...";
            this.addEditDefaultValueToolStripMenuItem.Click += new System.EventHandler(this.addEditDefaultValueToolStripMenuItem_Click);
            // 
            // ParameterBox
            // 
            this.AcceptButton = this.button1;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.button1;
            this.ClientSize = new System.Drawing.Size(524, 281);
            this.Controls.Add(this.splitContainer1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParameterBox";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PropertyBox";
            this.Load += new System.EventHandler(this.ParameterBox_Activated);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.cmParameter.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView lvParameters;
        private System.Windows.Forms.ColumnHeader colParameter;
        private System.Windows.Forms.ColumnHeader colType;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ColumnHeader colDefault;
        private System.Windows.Forms.ContextMenuStrip cmParameter;
        private System.Windows.Forms.ToolStripMenuItem addEditDefaultValueToolStripMenuItem;
    }
}