﻿namespace Au.Tools
{
	partial class Form_WinImage
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
			if(disposing && (components != null)) {
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
			this._info = new Au.Controls.AuInfoBox();
			this._lSpeed = new System.Windows.Forms.Label();
			this._bTest = new System.Windows.Forms.Button();
			this._bOK = new Au.Controls.ButtonOK();
			this._bCancel = new Au.Controls.ButtonCancel();
			this._bCapture = new System.Windows.Forms.Button();
			this._grid = new Au.Controls.ParamGrid();
			this._pict = new System.Windows.Forms.PictureBox();
			this._toolTip = new System.Windows.Forms.ToolTip(this.components);
			this._bEtc = new System.Windows.Forms.Button();
			this.panel1 = new System.Windows.Forms.Panel();
			this._code = new Au.Tools.CodeBox();
			this._errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
			((System.ComponentModel.ISupportInitialize)(this._pict)).BeginInit();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this._errorProvider)).BeginInit();
			this.SuspendLayout();
			// 
			// _info
			// 
			this._info.AccessibleName = "_info";
			this._info.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._info.DisableModifiedNotifications = false;
			this._info.Location = new System.Drawing.Point(8, 8);
			this._info.Name = "_info";
			this._info.Size = new System.Drawing.Size(600, 56);
			this._info.TabIndex = 9;
			// 
			// _lSpeed
			// 
			this._lSpeed.Location = new System.Drawing.Point(87, 109);
			this._lSpeed.Name = "_lSpeed";
			this._lSpeed.Size = new System.Drawing.Size(89, 15);
			this._lSpeed.TabIndex = 2;
			this._toolTip.SetToolTip(this._lSpeed, "Shows the Test execution time. Red if not found.");
			// 
			// _bTest
			// 
			this._bTest.Enabled = false;
			this._bTest.Location = new System.Drawing.Point(8, 104);
			this._bTest.Name = "_bTest";
			this._bTest.Size = new System.Drawing.Size(72, 24);
			this._bTest.TabIndex = 2;
			this._bTest.Text = "&Test";
			this._toolTip.SetToolTip(this._bTest, "Executes the \'find\' code (without wait, mouse, etc). If image found, shows its re" +
        "ctangle.");
			this._bTest.UseVisualStyleBackColor = true;
			this._bTest.Click += new System.EventHandler(this._bTest_Click);
			// 
			// _bOK
			// 
			this._bOK.Enabled = false;
			this._bOK.Location = new System.Drawing.Point(8, 136);
			this._bOK.Name = "_bOK";
			this._bOK.Size = new System.Drawing.Size(72, 24);
			this._bOK.TabIndex = 3;
			this._bOK.UseVisualStyleBackColor = true;
			this._bOK.Click += new System.EventHandler(this._bOK_Click);
			// 
			// _bCancel
			// 
			this._bCancel.Location = new System.Drawing.Point(88, 136);
			this._bCancel.Name = "_bCancel";
			this._bCancel.Size = new System.Drawing.Size(72, 24);
			this._bCancel.TabIndex = 4;
			this._bCancel.UseVisualStyleBackColor = true;
			// 
			// _bCapture
			// 
			this._bCapture.Location = new System.Drawing.Point(8, 72);
			this._bCapture.Name = "_bCapture";
			this._bCapture.Size = new System.Drawing.Size(72, 24);
			this._bCapture.TabIndex = 0;
			this._bCapture.Text = "&Capture";
			this._bCapture.UseVisualStyleBackColor = true;
			this._bCapture.Click += new System.EventHandler(this._bCapture_Click);
			// 
			// _grid
			// 
			this._grid.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._grid.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this._grid.ClipboardMode = SourceGrid.ClipboardMode.Copy;
			this._grid.ColumnsCount = 2;
			this._grid.EnableSort = false;
			this._grid.Location = new System.Drawing.Point(176, 72);
			this._grid.MinimumHeight = 18;
			this._grid.Name = "_grid";
			this._grid.OptimizeMode = SourceGrid.CellOptimizeMode.ForRows;
			this._grid.SelectionMode = SourceGrid.GridSelectionMode.Cell;
			this._grid.Size = new System.Drawing.Size(432, 192);
			this._grid.SpecialKeys = ((SourceGrid.GridSpecialKeys)((((((SourceGrid.GridSpecialKeys.Arrows | SourceGrid.GridSpecialKeys.PageDownUp) 
            | SourceGrid.GridSpecialKeys.Enter) 
            | SourceGrid.GridSpecialKeys.Escape) 
            | SourceGrid.GridSpecialKeys.Control) 
            | SourceGrid.GridSpecialKeys.Shift)));
			this._grid.TabIndex = 5;
			this._grid.TabStop = true;
			this._grid.ToolTipText = "";
			this._grid.ZAddHidden = false;
			// 
			// _pict
			// 
			this._pict.BackColor = System.Drawing.SystemColors.ControlDark;
			this._pict.Location = new System.Drawing.Point(-1, -1);
			this._pict.Name = "_pict";
			this._pict.Size = new System.Drawing.Size(16, 16);
			this._pict.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this._pict.TabIndex = 0;
			this._pict.TabStop = false;
			// 
			// _bEtc
			// 
			this._bEtc.Location = new System.Drawing.Point(88, 72);
			this._bEtc.Name = "_bEtc";
			this._bEtc.Size = new System.Drawing.Size(32, 24);
			this._bEtc.TabIndex = 1;
			this._bEtc.Text = "&...";
			this._bEtc.UseVisualStyleBackColor = true;
			this._bEtc.Click += new System.EventHandler(this._bEtc_Click);
			// 
			// panel1
			// 
			this.panel1.AutoScroll = true;
			this.panel1.BackColor = System.Drawing.SystemColors.ControlDark;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this._pict);
			this.panel1.Location = new System.Drawing.Point(8, 168);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(152, 96);
			this.panel1.TabIndex = 13;
			// 
			// _code
			// 
			this._code.AccessibleName = "_code";
			this._code.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this._code.DisableModifiedNotifications = false;
			this._code.Location = new System.Drawing.Point(8, 272);
			this._code.Name = "_code";
			this._code.Size = new System.Drawing.Size(600, 104);
			this._code.TabIndex = 6;
			// 
			// _errorProvider
			// 
			this._errorProvider.ContainerControl = this;
			// 
			// Form_WinImage
			// 
			this.AcceptButton = this._bOK;
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this._bCancel;
			this.ClientSize = new System.Drawing.Size(616, 383);
			this.Controls.Add(this._bCancel);
			this.Controls.Add(this.panel1);
			this.Controls.Add(this._grid);
			this.Controls.Add(this._code);
			this.Controls.Add(this._bEtc);
			this.Controls.Add(this._info);
			this.Controls.Add(this._lSpeed);
			this.Controls.Add(this._bCapture);
			this.Controls.Add(this._bTest);
			this.Controls.Add(this._bOK);
			this.IsPopup = true;
			this.MinimumSize = new System.Drawing.Size(500, 400);
			this.Name = "Form_WinImage";
			this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			this.Text = "Find image or color in window";
			((System.ComponentModel.ISupportInitialize)(this._pict)).EndInit();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this._errorProvider)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private Controls.AuInfoBox _info;
		private System.Windows.Forms.Label _lSpeed;
		private System.Windows.Forms.Button _bTest;
		private Au.Controls.ButtonOK _bOK;
		private Au.Controls.ButtonCancel _bCancel;
		private System.Windows.Forms.Button _bCapture;
		private Controls.ParamGrid _grid;
		private System.Windows.Forms.PictureBox _pict;
		private System.Windows.Forms.ToolTip _toolTip;
		private System.Windows.Forms.Button _bEtc;
		private CodeBox _code;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.ErrorProvider _errorProvider;
	}
}