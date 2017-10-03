﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using System.Xml;

using Catkeys;
using Catkeys.Types;
using static Catkeys.NoClass;

namespace G.Controls
{
	//[DebuggerStepThrough]
	//public sealed partial class GDockPanel :ContainerControl
	//public sealed partial class GDockPanel :Panel
	public sealed partial class GDockPanel :Control
	{
		List<GSplit> _aSplit;
		List<GTab> _aTab;
		List<GPanel> _aPanel;
		GSplit _firstSplit; //matches the first <split> tag in XML
		_PainTools _paintTools; //cache for brushes etc
		ToolTip _toolTip; //tooltip for panel captions and tab buttons
		string _xmlFile; //used to save panel layout later
		Dictionary<string, Control> _initControls; //used to find controls specified in XML. Temporary, just for GPanel ctors called by Create().
		const int _splitterWidth = 4; //default splitter width. Also used for toolbar caption width.

		protected override void Dispose(bool disposing)
		{
			if(_xmlFile != null) {
				_SaveLayout();
				_xmlFile = null;
			}

			//PrintList(disposing, IsHandleCreated);
			base.Dispose(disposing);
			_paintTools?.Dispose(); _paintTools = null;
			_toolTip?.Dispose(); _toolTip = null;

			//tested: all GFloat are automatically closed before destroying this window
		}

		/// <summary>
		/// Loads UI layout from XML file and adds controls (panels, toolbars) to this control.
		/// Adjusts control properties and positions everything according to the XML.
		/// </summary>
		/// <param name="xmlFileDefault">XML file containing default panel/toolbar layout.</param>
		/// <param name="xmlFileCustomized">XML file containing customized panel/toolbar layout. It will be created or updated when saving customizations.</param>
		/// <param name="controls">Controls. Control Name must match the XML element (panel) name attribute in the XML.</param>
		public void Create(string xmlFileDefault, string xmlFileCustomized, params Control[] controls)
		{
			_initControls = new Dictionary<string, Control>();
			foreach(var c in controls) {
				c.Dock = DockStyle.None;
				c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
				_initControls.Add(c.Name, c);
			}

			_aSplit = new List<GSplit>();
			_aTab = new List<GTab>();
			_aPanel = new List<GPanel>();
			_paintTools = new _PainTools(this);
			_toolTip = new ToolTip();

			_xmlFile = xmlFileCustomized;
			_LoadXmlAndCreateLayout(xmlFileDefault, xmlFileCustomized, Assembly.GetCallingAssembly().GetName().Version.ToString());

			SuspendLayout();
			this.SetStyle(ControlStyles.ContainerControl | ControlStyles.ResizeRedraw | ControlStyles.Opaque | ControlStyles.OptimizedDoubleBuffer, true); //default: UserPaint, AllPaintingInWmPaint; not OptimizedDoubleBuffer, DoubleBuffer, Opaque. Opaque prevents erasing background, which prevents flickering when moving a splitter.
			this.Dock = DockStyle.Fill;
			foreach(var c in controls) this.Controls.Add(c);
			ResumeLayout();

			_initControls = null;
		}

		//Loads and parses XML. Creates the _aX lists, _firstSplit and the tree structure.
		void _LoadXmlAndCreateLayout(string xmlFileDefault, string xmlFileCustomized, string asmVersion)
		{
			//We have 1 or 2 XML files containing panel/toolbar layout.
			//xmlFileDefault contains default XML. It eg can be in Folders.ThisApp.
			//xmlFileCustomized contains previously saved XML (user-modified layout).
			//At first try to load xmlFileCustomized. If it does not exist or is invalid, load xmlFileDefault; or get missing data from xmlFileDefault, if possible.
			//Also loads xmlFileDefault when xmlFileCustomized XML does not match panels of new app version and cannot resolve it (eg some panels removed).
			bool usesDefaultXML = false;
			string xmlFile = xmlFileCustomized, xmlVersion = null, outInfo = null;
			for(int i = 0; i < 2; i++) {
				if(i == 0) {
					if(!Files.ExistsAsFile(xmlFile)) continue;
				} else {
					usesDefaultXML = true;
					xmlFile = xmlFileDefault;
				}

				try {
					var x = XElement.Load(xmlFile);
					if(!usesDefaultXML) xmlVersion = x.Attribute_("version");
					x = x.Element("split");

					//THIS DOES THE MAIN JOB
					_firstSplit = new GSplit(this, null, x);

					if(_aPanel.Count < _initControls.Count) { //more panels added in this app version
						if(usesDefaultXML) throw new Exception("debug1");
						_GetPanelXmlFromDefaultFile(xmlFileDefault);
					}

					break;

					//speed: xml.Load takes 170 microseconds.
					//tested: XML can be added to Application Settings, but var xml=Properties.Settings.Default.PanelsXML takes 61 MILLIseconds.
				}
				catch(Exception e) {
					var sErr = $"Failed to load file:\r\n\t{xmlFile}\r\n\tError: {e.Message} ({e.GetType()})";
					if(usesDefaultXML) {
						_xmlFile = null;
						TaskDialog.ShowError("Cannot load panel/toolbar layout.", $"{sErr}\r\n\r\nReinstall the application.");
						Environment.Exit(1);
					} else {
						//probably in this version there are less panels, most likely when downgraded. Or the file is corrupt.
						if(xmlVersion != asmVersion) outInfo = "Info: this application version resets the panel/toolbar layout, sorry.";
						else Output.Warning(sErr);
					}
					_aSplit.Clear(); _aTab.Clear(); _aPanel.Clear();
				}
			}

			//if(usesDefaultXML || xmlVersion == asmVersion) return;
			if(outInfo != null) Print(outInfo);
		}

		void _GetPanelXmlFromDefaultFile(string defFile)
		{
			var xml = XElement.Load(defFile);

			foreach(var c in _initControls.Values) {
				if(_aPanel.Exists(v => v.Content == c)) continue;
				var x = xml.Descendant_("panel", "name", c.Name);
				var gp = new GPanel(this, null, x) {
					DockState = GDockState.Hidden,
					SavedVisibleDockState = GDockState.Floating
				};
				c.Visible = false;
				Print($"Info: new {(gp.HasToolbar ? "toolbar" : "panel")} '{gp.Text}' added in this aplication version. Currently it is hidden.");
			}
		}

		void _SaveLayout()
		{
			try {
				if(ResetLayoutAfterRestart) {
					Files.Delete(_xmlFile);
					return;
				}
				Files.CreateDirectoryFor(_xmlFile);
				var sett = new XmlWriterSettings() {
					OmitXmlDeclaration = true,
					Indent = true,
					IndentChars = "\t"
				};
				using(var x = XmlWriter.Create(_xmlFile, sett)) {
					x.WriteStartDocument();
					x.WriteStartElement("panels");
					_firstSplit.Save(x);
					x.WriteEndDocument();
					x.Close();
				}
				//#if DEBUG
				//			Print(File.ReadAllText(_xmlFile));
				//			//File.Delete(_xmlFile);
				//			File.Delete(_xmlFile + ".xml");
				//			File.Move(_xmlFile, _xmlFile + ".xml");
				//#endif
			}
			catch {
				//Print(e);
				//these don't work, maybe because now is closing app. Never mind, unlikely to fail, and not very important.
				//TaskDialog.ShowError("Failed to save panel/toolbar layout", _xmlFile, TDFlags.Wider, expandedText: e.ToString());
				//MessageBox.Show("aaaa");
			}
		}

		int _CaptionHeight
		{
			get
			{
				if(__captionHeight == 0) {
					var fh = this.Font.Height; //not FontHeight, it caches the value and it is not auto updated on font change
					__captionHeight = Math.Max(fh, Catkeys.Util.Dpi.ScaleInt(16)) + 2; //16 for icon, 2 for padding
				}
				return __captionHeight;
			}
		}
		int __captionHeight;

		protected override void OnFontChanged(EventArgs e)
		{
			__captionHeight = 0;
			base.OnFontChanged(e);
		}

		protected override void WndProc(ref Message m)
		{
			if(_WndProcBefore_Common(this, ref m)) return;

			base.WndProc(ref m);

			_WndProcAfter_Common(this, ref m);
		}

		//The X_Common functions are called by GDockPanel and by GFloat.
		//Would be better to use a common base class for them, but it is difficult etc because GDockPanel is a control and GFloat is a top-level form.
		bool _WndProcBefore_Common(Control c, ref Message m)
		{
			switch((uint)m.Msg) {
			case Api.WM_SETCURSOR:
				if(m.WParam == c.Handle && _OnSetCursor(c, m.LParam)) {
					m.Result = (IntPtr)1;
					return true;
				}
				break;
			}
			return false;
		}

		void _WndProcAfter_Common(Control c, ref Message m)
		{
		}

		protected override void OnClientSizeChanged(EventArgs e)
		{
			_UpdateLayout();

			base.OnClientSizeChanged(e);

			//info: this is always called at startup, because default client rectangle is empty and then resized because of Dock Fill.
		}

		void _UpdateLayout(bool invalidate = false)
		{
			var r = this.ClientRectangle;
			if(r.Width <= 0 || r.Height <= 0) return; //eg minimized.  //not r.IsEmpty, because can be negative Width/Height
			_firstSplit.UpdateLayout(r);
			if(invalidate) this.Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			_OnPaint_Common(e);
			_firstSplit.Paint(e.Graphics);

			base.OnPaint(e);

			//note: don't use e.ClipRectangle. Now it always is whole client area, because of ResizeRedraw=true (cannot work without it).
		}

		void _OnPaint_Common(PaintEventArgs e)
		{
			//foreach(var gp in _aPanel) e.Graphics.ExcludeClip(gp.Content.Bounds); //makes erasing 2 times faster when window maximized (2 -> 1 ms)

			e.Graphics.Clear(Catkeys.Util.Color_.ColorFromBGR(0xAAAAAA)); //draw borders

			//speed: this is the slowest part of painting this control. Using API does not help.
		}

		#region mouse

		bool _OnSetCursor(Control c, LPARAM lParam)
		{
			bool R = false, hilite = false, isTooltip = false;
			if((((uint)lParam) & 0xFFFF) == Api.HTCLIENT && !c.Capture) {
				var p = c.MouseClientXY_();
				switch(_HitTest(c, p.X, p.Y, out var ht)) {
				case _HitTestResult.Splitter:
					//info: works better than Cursor=x in OnMouseMove.
					var cursor = ht.gs.IsVerticalSplit ? Cursors.VSplit : Cursors.HSplit;
					Api.SetCursor(cursor.Handle);
					R = true;
					break;
				case _HitTestResult.Caption:
					var gt = ht.ParentTab;
					if(gt != null && ht.gp != gt.ActiveItem) hilite = true; //highlight inactive tab button
					else if(ht.gp != null && ht.gp.HasToolbar) hilite = true; //change toolbar caption color from form background color to panel caption color
					if(hilite) {
						if(_hilitedTabButton != ht.gp) {
							_UnhiliteTabButton();
							_hilitedTabButton = ht.gp;
							_hilitedTabButton.InvalidateCaption();
						}
					}
					//tooltip
					var tt = ht.gp?.ToolTipText;
					if(!Empty(tt)) {
						isTooltip = true;
						if(_toolTipTabButton != ht.gp) {
							int delay = _toolTipTabButton == null ? _toolTip.InitialDelay : _toolTip.ReshowDelay;
							_HideTooltip();
							_toolTipTabButton = ht.gp;
							Timer_.After(delay, t =>
							{
								var gp = _toolTipTabButton; if(gp == null) return;
								var p2 = gp.ParentControl.MouseClientXY_();
								_toolTip.Show(gp.ToolTipText, gp.ParentControl, p2.X, p2.Y + 20, _toolTip.AutoPopDelay);
							});
						}
					}
					break;
				}
			}
			if(!hilite) _UnhiliteTabButton();
			if(!isTooltip) _HideTooltip();
			return R;
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			_OnMouseLeave_Common(this);
			base.OnMouseLeave(e);
		}

		void _OnMouseLeave_Common(Control c)
		{
			_UnhiliteTabButton();
			_HideTooltip();
		}

		GPanel _hilitedTabButton, _toolTipTabButton;

		void _UnhiliteTabButton()
		{
			if(_hilitedTabButton == null) return;
			_hilitedTabButton.InvalidateCaption();
			_hilitedTabButton = null;
		}

		void _HideTooltip()
		{
			if(_toolTipTabButton == null) return;
			_toolTip.Hide(_toolTipTabButton.ParentControl);
			_toolTipTabButton = null;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			_OnMouseDown_Common(this, e);
			base.OnMouseDown(e);

			//info: no OnMouseDoubleClick when we use DragDetect
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			_OnMouseUp_Common(this, e);
			base.OnMouseUp(e);
		}

		void _OnMouseDown_Common(Control c, MouseEventArgs e)
		{
			switch(_HitTest(c, e.X, e.Y, out var ht)) {
			case _HitTestResult.Splitter:
				if(e.Clicks == 1 && e.Button == MouseButtons.Left) ht.gs.DragSplitter();
				break;

			case _HitTestResult.Caption:
				switch(e.Button) {
				case MouseButtons.Left:
					switch(e.Clicks) {
					case 1:
						ht.ParentTab?.OnMouseDownTabButton(ht.gp, e.Button); //if clicked an inactive tab button, change active tab
						ht.ContentNode.OnMouseLeftDown(); //if drag detected, make floating if docked, and drag the floating window
						break;
					case 2:
						ht.ContentNode.ToggleDockedFloating();
						break;
					}
					break;
				case MouseButtons.Middle:
					if(e.Clicks == 1) ht.ContentNode.Hide();
					break;
				}
				break;
			}
		}

		void _OnMouseUp_Common(Control c, MouseEventArgs e)
		{
			if(e.Clicks != 1) return;
			if(e.Button == MouseButtons.Right) {
				switch(_HitTest(c, e.X, e.Y, out var ht)) {
				case _HitTestResult.Caption: ht.ContentNode.ShowContextMenu(e.Location); break;
				case _HitTestResult.Splitter: ht.gs.ShowContextMenu(e.Location); break;
				}
			}
		}

		enum _HitTestResult
		{
			None, //a control or border or outside
			Splitter, //splitter
			Caption, //panel caption, tab caption or tab button
		};
		struct _HitTestData
		{
			internal GSplit gs; //not null when _HitTestResult.Splitter
			internal GTab gt; //not null when _HitTestResult.Caption and cursor is on tab caption and not button
			internal GPanel gp; //not null when _HitTestResult.Caption and cursor is on panel caption or tab button

			/// <summary>
			/// If hit test on a tabbed GPanel, returns its parent GTab, else null.
			/// </summary>
			internal GTab ParentTab { get => gp?.ParentTab; }

			/// <summary>
			/// If hit test on a GContentNode, returns it, else null.
			/// </summary>
			internal GContentNode ContentNode { get => gt ?? gp as GContentNode; }
		}

		/// <summary>
		/// If hit-test on a splitter, sets ht.gs and returns _HitTestResult.Splitter.
		/// Else if on a GPanel caption or GPanel tab button, sets gp and returns _HitTestResult.Caption.
		/// Else (if on a GTab caption but not a  GPanel tab button) sets gt and returns _HitTestResult.Caption.
		/// </summary>
		_HitTestResult _HitTest(Control c, int x, int y, out _HitTestData ht)
		{
			ht = new _HitTestData();
			if(c == this && (ht.gs = _aSplit.Find(v => v.HitTestSplitter(x, y))) != null) return _HitTestResult.Splitter;
			if((ht.gp = _aPanel.Find(v => v.HitTestCaption(c, x, y))) != null) return _HitTestResult.Caption;
			if((ht.gt = _aTab.Find(v => v.HitTestCaption(c, x, y))) != null) return _HitTestResult.Caption;
			return _HitTestResult.None;
		}

		#endregion mouse

		[DebuggerStepThrough]
		GPanel _FindPanel(Control control)
		{
			return _aPanel.Find(gp => gp.Content == control);
		}

		[DebuggerStepThrough]
		GPanel _FindPanel(string name)
		{
			return _aPanel.Find(gp => gp.Name == name);
		}

		#region public

		/// <summary>
		/// Gets control's host panel interface.
		/// Returns null if not found.
		/// </summary>
		public IPanel GetPanel(Control c)
		{
			return _FindPanel(c);
		}

		/// <summary>
		/// Gets panel interface by name.
		/// Returns null if not found.
		/// </summary>
		/// <param name="name">Panel name, which is its control's Name property.</param>
		public IPanel GetPanel(string name)
		{
			return _FindPanel(name);
		}

		/// <summary>
		/// Adds menu items for all panels or toolbars, except the doc panel, to a menu.
		/// On menu item click will show that panel.
		/// </summary>
		public void AddShowPanelsToMenu(ToolStripDropDown m, bool toolbars, bool clear = false)
		{
			m.SuspendLayout();
			if(clear) m.Items.Clear();

			var a = _aPanel.FindAll(v => !v.HasDocument && (toolbars == v.HasToolbar));
			a.Sort((v1, v2) => v1.Name.CompareTo(v2.Name));
			foreach(var v in a) {
				var s = v.Text;
				if(!v.Visible) s += "  (hidden)";
				m.Items.Add(s, v.Image, (unu, sed) => { v.Show(true); });
			}
			//add Reset...
			m.Items.Add(new ToolStripSeparator());
			(m.Items.Add("Reset...", null, (unu, sed) =>
			{
				if(ResetLayoutAfterRestart) ResetLayoutAfterRestart = false;
				else ResetLayoutAfterRestart = TaskDialog.ShowOKCancel("Reset panel/toolbar layout", "After restarting this application.");
			}) as ToolStripMenuItem).Checked = ResetLayoutAfterRestart;

			m.ResumeLayout();
		}

		/// <summary>
		/// When disposing this, delete the user's saved layout file. Then next time will use the default file.
		/// </summary>
		public bool ResetLayoutAfterRestart { get; set; }

#if false
		//This worked, but better don't use.
		//Instead, if panels added/removed/changed in new version, now automatically uses data from the default XML file.
		/// <summary>
		/// Creates new GPanel and adds it by the panel of control cBy, to an existing or new tab group or split.
		/// Does nothing if c is already added.
		/// The new panel settings will be saved to the XML file.
		/// Call this ater Create() when the new version of your app wants to add more panels without replacing the old XML (user-modified).
		/// You can simply always call this function for all panels added in new versions (it does nothing if called not first time). Or call it once, eg if FindPanel() returns false for that control.
		/// Also in new versions always pass the control to Create() too; it just ignores it if there is still no XML element for it.
		/// </summary>
		/// <param name="c">Control of the new GPanel.</param>
		/// <param name="cBy">Add the new panel by the panel of this control.</param>
		/// <param name="side">Specifies whether to add in a tab group or split, and at which side of cBy.</param>
		/// <param name="xml">XML containing single element that stores panel settings, eg "&lt;panel text='Results' tooltip='Find results' image='15' hide='' /&gt;".</param>
		public void AddPanel(Control c, Control cBy, DockSide side, string xml)
		{
			if(_FindPanel(c) != null) return;

			this.Controls.Add(c);

			var xdoc = XElement.Load(xml);
			var gp = new GPanel(c, this, null, xdoc);
			bool hide = gp.IsHidden; gp.DockState = GDockState.Hidden;
			var gpBy = _FindPanel(cBy);
			gp.DockBy(gpBy, side, true);
			if(hide) gp.Hide();

			//not tested when cBy panel is hidden or floating
		}
#endif

		/// <summary>
		/// Call this in main window's WndProc override on WM_ENABLE.
		/// </summary>
		/// <param name="enable">Enable or disable.</param>
		public void EnableDisableAllFloatingWindows(bool enable)
		{
			foreach(var v in _aPanel) if(v.IsFloating) ((Wnd)v.ParentControl).Enable(enable);
			foreach(var v in _aTab) if(v.IsFloating) ((Wnd)v.ParentControl).Enable(enable);
		}

		/// <summary>
		/// Used with many GDockPanel events and othe callbacks.
		/// </summary>
		public class GDockPanelEventArgs :EventArgs
		{
			public GDockPanel dp { get; private set; }
			public Control control { get; set; }
			public string panelName { get; private set; }

			/// <summary>ctor.</summary>
			/// <param name="gPanel">Must be GPanel. We use object because C# compiler does not allow to use GPanel (less accessible).</param>
			internal GDockPanelEventArgs(object gPanel)
			{
				var gp = gPanel as GPanel;
				dp = gp.Manager;
				control = gp.Content;
				panelName = gp.Name;
			}
		}

		/// <summary>
		/// Used with many GDockPanel events and other callbacks.
		/// </summary>
		public delegate void GDockPanelEventHandler<T>(T e);

		//public event GDockPanelEventHandler<GDockPanelEventArgs> DockStateChanged;

		public class GDockPanelContextMenuEventArgs :GDockPanelEventArgs
		{
			public CatMenu menu { get; internal set; }

			internal GDockPanelContextMenuEventArgs(object gPanel, CatMenu cm) : base(gPanel)
			{
				menu = cm;
			}
		}

		/// <summary>
		/// Before showing the context menu when the user right-clicks a panel tab button or caption.
		/// The event handler can add/remove/etc menu items.
		/// </summary>
		public event GDockPanelEventHandler<GDockPanelContextMenuEventArgs> PanelContextMenu;

		/// <summary>
		/// Removes rounded edges of the ToolStrip to which is assigned.
		/// </summary>
		public class DockedToolStripRenderer :ToolStripProfessionalRenderer
		{
			public DockedToolStripRenderer()
			{
				this.RoundedEdges = false;
			}
		}

		#endregion public

		//public void Test()
		//{

		//}
	}
}
