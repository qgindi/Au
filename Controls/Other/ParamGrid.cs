using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
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
//using System.Linq;
//using System.Xml.Linq;

using Au;
using Au.Types;
using static Au.NoClass;

using SG = SourceGrid;
using Editors = SourceGrid.Cells.Editors;
using DevAge.Drawing;

namespace Au.Controls
{
	/// <summary>
	/// Property grid control.
	/// Used in many tools, for example "Find window or control".
	/// </summary>
	public class ParamGrid :SG.Grid
	{
		Editors.TextBox _editor;
		SG.Cells.Controllers.ToolTipText _controllerTooltip0, _controllerTooltip1;

		const SG.EditableMode c_editableMode = SG.EditableMode.SingleClick | SG.EditableMode.F2Key | SG.EditableMode.AnyKey; //double click -> single click. See also OnMouseDown.

		public ParamGrid()
		{
			this.ColumnsCount = 2; //let the programmer set = 1 if need only for flags
			this.MinimumHeight = 18; //height when font is SegoeUI,9. With most other fonts it's smaller.
			this.SpecialKeys = SG.GridSpecialKeys.Default & ~SG.GridSpecialKeys.Tab;

			//this.AutoStretchColumnsToFitWidth = true; //does not work well. Instead we resize in OnClientSizeChanged etc.

			_editor = new Editors.TextBox(typeof(string)) { EditableMode = c_editableMode };
			var c = _editor.Control;
			c.Multiline = true; //let all rows have the same multiline editor, even if the value cannot be multiline

			this.Controller.AddController(new _CellController());
			this.Controller.AddController(SG.Cells.Controllers.Resizable.ResizeWidth); //we resize width and height automatically, but the user may want to resize width. This is like in VS.
			_controllerTooltip0 = new SG.Cells.Controllers.ToolTipText() /*{ IsBalloon = true }*/;
			_controllerTooltip1 = new SG.Cells.Controllers.ToolTipText();

			//this.Font = new Font("Verdana", 8);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			//somehow this does not work in ctor
			var sel = this.Selection as SG.Selection.SelectionBase;
			var border = sel.Border;
			border.SetWidth(1);
			border.SetColor(Color.Blue);
			border.SetDashStyle(System.Drawing.Drawing2D.DashStyle.Dot);
			sel.Border = border;
			sel.BackColor = sel.FocusBackColor;
			sel.EnableMultiSelection = false;

			//SourceGrid.Grid always shows a selection rect after entering first time.
			//	The properties only allow to change background color when focused/nonfocused.
			//	We use OnEnter/OnLeave to show a focus rect only when the control is focused.

			VScrollBar.LocationChanged += (unu, sed) => _AutoSizeLastColumn(); //when vscrollbar added/removed (SB width changed); when grid width changed.
		}

		protected override void OnHandleDestroyed(EventArgs e)
		{
			if(this._comboPopup != null) {
				this._comboPopup.Dispose();
				this._comboPopup = null;
			}
			base.OnHandleDestroyed(e);
		}

//#if DEBUG
//		public bool ZDebug { get; set; }
//#endif

		void _ShowCellFocusRect(bool yes)
		{
			var sel = this.Selection as SG.Selection.SelectionBase;
			var border = sel.Border;
			border.SetWidth(yes ? 1 : 0);
			sel.Border = border;
		}

		protected override void OnEnter(EventArgs e)
		{
			base.OnEnter(e);
			_ShowCellFocusRect(true);
		}

		protected override void OnLeave(EventArgs e)
		{
			base.OnLeave(e);
			_ShowCellFocusRect(false);
		}

		void _AutoSizeLastColumn()
		{
			//Print(this.Name, this.RowsCount);
			if(this.RowsCount > 0) {
				int n = this.ClientSize.Width;
				if(this.VScrollBarVisible) n -= this.VScrollBar.Width;
				int col = 0;
				if(this.ColumnsCount > 1) {
					n -= this.Columns[0].Width;
					col = 1;
				}
				this.Columns[col].Width = Math.Max(n, 0);
			}

			//SHOULDDO: also call this when the user resizes column 0. Or don't allow to resize, why need it.
		}

		int _MeasureColumnWidth(int column)
		{
			//This code taken from MeasureColumnWidth and modified to skip col-spanned cells etc.

			int wid = 10;
			for(int r = 0, n = this.RowsCount; r < n; r++) {
				var cell = this[r, column];
				if(cell == null || cell.ColumnSpan != 1) continue;
				var cellContext = new SG.CellContext(this, new SG.Position(r, column), cell);
				Size cellSize = cellContext.Measure(default);
				if(cellSize.Width > wid) wid = cellSize.Width;
			}
			return wid;
		}

		/// <summary>
		/// Call this after adding all rows.
		/// </summary>
		public void ZAutoSize(bool rows = true, bool columns = true)
		{
			if(rows) {
				this.Rows.AutoSize(false);
			}
			if(columns) {
				if(this.ColumnsCount > 1 && this.RowsCount > 0) {
					//this.Columns.AutoSizeColumn(0); //no, can be too wide. There is MinimumWidth but no MaximumWidth.
					//int wid = this.Columns.MeasureColumnWidth(0, false, 0, this.RowsCount - 1); //no, it does not work well with col-spanned cells
					int wid = _MeasureColumnWidth(0);
					this.Columns.SetWidth(0, Math.Min(wid, this.ClientSize.Width / 2));
				}
				_AutoSizeLastColumn();
			}
		}

		class _CellController :SG.Cells.Controllers.ControllerBase
		{
			public override void OnValueChanged(SG.CellContext sender, EventArgs e)
			{
				base.OnValueChanged(sender, e);

				var grid = sender.Grid as ParamGrid;

				var pos = sender.Position;
				if(pos.Column == 1) {
					grid.Rows.AutoSizeRow(pos.Row);
					if(sender.IsEditing()) grid.ZCheck(pos.Row, true); //note: this alone would interfere with the user clicking the checkbox of this row. OnMouseDown prevents it.
				}

				grid.ZOnValueChanged(sender);
			}

			public override void OnEditStarted(SG.CellContext sender, EventArgs e)
			{
				//Debug_.PrintFunc();

				if(sender.Cell.Editor is Editors.ComboBox cb) {
					cb.Control.DroppedDown = true;
				} else if(sender.Cell is ComboCell cc) {
					var g = cc.Grid as ParamGrid;
					var tb = (cc.Editor as Editors.TextBox).Control;
					var bWidth = cc.MeasuredButtonWidth;
					tb.Width -= bWidth;
					if(g._clickX >= tb.Right) cc.ShowDropDown(); //clicked drop-down button
				}

				base.OnEditStarted(sender, e);

				_ShowEditInfo(sender, true);
			}

			public override void OnEditEnded(SG.CellContext sender, EventArgs e)
			{
				_ShowEditInfo(sender, false);
				base.OnEditEnded(sender, e);
			}

			void _ShowEditInfo(SG.CellContext sender, bool show)
			{
				var t = sender.Cell as EditCell;
				if(t?.Info != null) {
					var grid = sender.Grid as ParamGrid;
					grid.ZOnShowEditInfo(sender, show ? t.Info : null);
				}
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if(ZGetEditCell(out var c)) {
				if(c.Position != PositionAtPoint(e.Location)) {
					c.EndEdit(cancel: false);
				} else if(c.Cell is ComboCell cc && _comboPopup!=null) {
					//never mind: cannot toggle, because the click closed the popup before this event
					//_comboPopup.Popup.Visible = !_comboPopup.Popup.Visible; //no, then shows in wrong place if form moved while editing cell
					cc.ShowDropDown(sameItems: true);
				}
				return;
			}

			base.OnMouseDown(e);
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			switch(keyData) {
			case Keys.Down: case Keys.Down | Keys.Alt:
				if(ZGetEditCell(out var c) && c.Cell is ComboCell cc) {
					cc.ShowDropDown();
					return true;
				}
				break;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			_clickX = e.Location.X;
			base.OnMouseClick(e);
			_clickX = 0;
		}
		int _clickX;

		protected virtual void ZOnValueChanged(SG.CellContext sender)
		{
			ZValueChanged?.Invoke(sender);
		}

		/// <summary>
		/// When changed text of a value cell or state of a checkbox cell.
		/// </summary>
		public event Action<SG.CellContext> ZValueChanged;

		protected virtual void ZOnShowEditInfo(SG.CellContext sender, string info)
		{
			ZShowEditInfo?.Invoke(sender, info);
		}

		/// <summary>
		/// When started and ended editing a cell that has info.
		/// When ended, the string is null.
		/// </summary>
		public event Action<SG.CellContext, string> ZShowEditInfo;

		/// <summary>
		/// Simple editable text cell.
		/// Adds to SG.Cells.Cell: property Info.
		/// </summary>
		public class EditCell :SG.Cells.Cell
		{
			public EditCell(string value) : base(value, typeof(string)) { }

			public string Info { get; set; }
		}

		/// <summary>
		/// Editable text cell with drop-down button that shows drop-down list similar to combo box.
		/// </summary>
		public class ComboCell :EditCell
		{
			string[] _items;
			Func<string[]> _callback;

			internal ComboCell(string[] items, int iSelect) : base(iSelect >= 0 ? items[iSelect] : "")
			{
				_items = items;
			}

			internal ComboCell(Func<string[]> callback) : base("")
			{
				_callback = callback;
			}

			internal int MeasuredButtonWidth => ((this.View as SG.Cells.Views.ComboBox).ElementDropDown as _ComboButton).MeasuredWidth;

			internal void ShowDropDown(bool sameItems = false)
			{
				var g = Grid as ParamGrid;
				var p = g._comboPopup;
				if(!sameItems) {
					var items = _items;
					if(items == null) items = _callback?.Invoke();
					if(items == null || items.Length == 0) return;

					var tb = (this.Editor as Editors.TextBox).Control;
					tb.Update(); g.Update(); //paint controls before the popup animation, to avoid flickering
					if(p == null) g._comboPopup = p = new DropDownList();
					p.Items = items;
					p.OnSelected = pp =>
					{
						tb.Value = pp.ResultString;
						if(!pp.ResultWasKey) g.ZEndEdit(cancel: false);
					};
				}
				var r = g.PositionToRectangle(new SG.Position(Row.Index, Column.Index));
				p.Show(g, r);
			}
		}
		DropDownList _comboPopup;

		enum _ViewType
		{
			//column 0
			Check, Readonly, HeaderRow, HeaderRowCheck,
			//column 1
			Edit, Combo, Button
		}
		SG.Cells.Views.Cell[] _views = new SG.Cells.Views.Cell[7];
		static DevAge.Drawing.VisualElements.TextRenderer s_textRenderer = new DevAge.Drawing.VisualElements.TextRenderer(); //default GDI+ //ExpandTabs ignored. It seems it can be used only in SourceGrid.Cells.Views.Cell.PrepareVisualElementText override. Never mind.

		SG.Cells.Views.Cell _GetView(_ViewType type)
		{
			ref SG.Cells.Views.Cell view = ref _views[(int)type];
			switch(type) {
			case _ViewType.Edit:
				if(view == null) view = new SG.Cells.Views.Cell {
					ElementText = s_textRenderer,
					Padding = new DevAge.Drawing.Padding(0, 2, 0, 0) //default all 2 2 2 2
				};
				break;
			case _ViewType.Combo:
				if(view == null) view = new SG.Cells.Views.ComboBox {
					ElementText = s_textRenderer,
					Padding = new DevAge.Drawing.Padding(0, 0, 0, 0),
					ElementDropDown = new _ComboButton()
				};
				break;
			case _ViewType.Button:
				if(view == null) view = new SG.Cells.Views.Button {
					ElementText = s_textRenderer,
					Padding = new DevAge.Drawing.Padding(0, 0, 0, 0)
				};
				break;
			case _ViewType.Check:
				if(view == null) view = new SG.Cells.Views.CheckBox {
					ElementText = s_textRenderer,
					BackColor = Form.DefaultBackColor,
					CheckBoxAlignment = DevAge.Drawing.ContentAlignment.MiddleLeft,
					Padding = new DevAge.Drawing.Padding(2, 2, 0, 0)
				};
				break;
			case _ViewType.Readonly:
				if(view == null) view = new SG.Cells.Views.Cell {
					ElementText = s_textRenderer,
					BackColor = Form.DefaultBackColor,
					Padding = new DevAge.Drawing.Padding(0, 2, 0, 0)
				};
				break;
			case _ViewType.HeaderRow:
				if(view == null) view = new SG.Cells.Views.Cell {
					ElementText = s_textRenderer,
					TextAlignment = DevAge.Drawing.ContentAlignment.MiddleCenter,
					Padding = new DevAge.Drawing.Padding(2.1f),
					Background = new DevAge.Drawing.VisualElements.BackgroundLinearGradient(Color.Silver, Color.WhiteSmoke, 90)
				};
				break;
			case _ViewType.HeaderRowCheck:
				if(view == null) view = new SG.Cells.Views.CheckBox {
					ElementText = s_textRenderer,
					TextAlignment = DevAge.Drawing.ContentAlignment.MiddleCenter,
					CheckBoxAlignment = DevAge.Drawing.ContentAlignment.MiddleLeft, //default MiddleCenter draws on text
					Padding = new DevAge.Drawing.Padding(2.1f),
					Background = new DevAge.Drawing.VisualElements.BackgroundLinearGradient(Color.Silver, Color.WhiteSmoke, 90)
				};
				break;
			}
			return view;
		}

		//Combo button that does not make the cell taller.
		class _ComboButton :DevAge.Drawing.VisualElements.DropDownButtonThemed
		{
			public _ComboButton()
			{
				AnchorArea = new AnchorArea(float.NaN, 0, 0, 0, false, false);
			}

			protected override SizeF OnMeasureContent(MeasureHelper measure, SizeF maxSize)
			{
				var z = base.OnMeasureContent(measure, maxSize);
				z.Height = 16; //info: can be even 0; grid does not draw it smaller than cell height.
				MeasuredWidth = (int)z.Width;
				return z;
			}

			public int MeasuredWidth { get; private set; }
		}

		enum _RowType
		{
			/// <summary>Checkbox and editable cell. If check is null, adds lebel instead of checkbox.</summary>
			Editable,

			/// <summary>Only checkbox.</summary>
			Check,

			/// <summary>Only label. If check is not null, adds checkbox instead of label.</summary>
			Header,
		}

		/// <summary>
		/// Types of the editable cell.
		/// </summary>
		public enum EditType
		{
			/// <summary>Simple editable text.</summary>
			Text,

			/// <summary>Editable text with combobox-like drop-down.</summary>
			ComboText,

			/// <summary>Read-only combobox.</summary>
			ComboList,

			/// <summary>Editable text with button.</summary>
			TextButton,

			/// <summary>Button.</summary>
			Button,
		}

		int _AddRow(string key, string name, object value, bool? check, _RowType type, string tt, string info, int insertAt,
			EditType etype = EditType.Text, EventHandler buttonAction = null, int comboIndex = -1)
		{
			int r = insertAt < 0 ? this.RowsCount : insertAt;

			this.Rows.Insert(r);
			if(ZAddHidden) this.Rows[r].Visible = false;

			SG.Cells.Cell c; SG.Cells.Views.Cell view;
			if(check == null) {
				c = new SG.Cells.Cell(name);
				if(type == _RowType.Header) {
					view = _GetView(_ViewType.HeaderRow);
					c.AddController(SG.Cells.Controllers.Unselectable.Default);
				} else {
					view = _GetView(_ViewType.Readonly);
				}
			} else {
				c = new SG.Cells.CheckBox(name, check.GetValueOrDefault());
				if(type == _RowType.Header) {
					view = _GetView(_ViewType.HeaderRowCheck);
				} else {
					view = _GetView(_ViewType.Check);
				}
			}
			c.View = view;

			if(tt != null) {
				c.AddController(_controllerTooltip0);
				c.ToolTipText = tt;
			}

			this[r, 0] = c;

			int nc = this.ColumnsCount;
			if(nc > 1) {
				if(type == _RowType.Check || type == _RowType.Header) {
					c.ColumnSpan = nc;
				} else {
					SG.Cells.Cell t; _ViewType viewType = _ViewType.Edit;
					switch(etype) {
					case EditType.Text:
						t = new EditCell(value?.ToString()) { Editor = _editor, Info = info };
						break;
					case EditType.TextButton: {
							var ed = new Editors.TextBoxButton(typeof(string)) { EditableMode = c_editableMode };
							ed.Control.TextBox.Multiline = true;
							t = new EditCell(value?.ToString()) { Editor = ed, Info = info };
							ed.Control.DialogOpen += buttonAction;
						}
						break;
					case EditType.Button: {
							t = new SG.Cells.Button(value?.ToString());
							var ev = new SG.Cells.Controllers.Button();
							ev.Executed += buttonAction;
							t.Controller.AddController(ev);
							viewType = _ViewType.Button;
						}
						break;
					default: { //combo
							string[] a = null; Func<string[]> callback = null;
							switch(value) {
							case string s: a = s.Split_("|"); break;
							case string[] sa: a = sa; break;
							case List<string> sl: a = sl.ToArray(); break;
							case Func<string[]> callb: callback = callb; break;
							}
							if(etype == EditType.ComboList) {
								var ed = new Editors.ComboBox(typeof(string), a, false) { EditableMode = c_editableMode };
								var cb = ed.Control;
								cb.DropDownStyle = ComboBoxStyle.DropDownList;
								cb.SelectionChangeCommitted += (unu, sed) => ZEndEdit(false);
								if(buttonAction != null) cb.DropDown += buttonAction;
								t = new EditCell(comboIndex >= 0 ? a[comboIndex] : "") { Editor = ed, Info = info };
							} else {
								var ed = _editor;
								var cc = (callback != null) ? new ComboCell(callback) : new ComboCell(a, comboIndex);
								cc.Editor = ed;
								cc.Info = info;
								t = cc;
								viewType = _ViewType.Combo;
							}
						}
						break;
					}
					t.AddController(_controllerTooltip1);
					t.View = _GetView(viewType);
					this[r, 1] = t;
				}
			}

			if(key == null) key = name;
#if DEBUG
			Debug.Assert(ZFindRow(key) < 0, "Duplicate grid row key:", key);
#endif
			this.Rows[r].Tag = key;
			return r;
		}

		#region public add/get/clear functions

		/// <summary>
		/// Adds row with checkbox (or label) and editable cell.
		/// Returns row index.
		/// </summary>
		/// <param name="key">Row's Tag property. If null, uses <paramref name="name"/>. Used by <see cref="ZGetValue(string, out string, bool)"/> and other functions.</param>
		/// <param name="name">Readonly text in column 0 (checkbox or label).</param>
		/// <param name="value">
		/// string.
		/// For combo can be string like "one|two|three" or string[] List of string.
		/// For editable combo also can be Func&lt;string[]&gt; callback that returns items. Called before each dropdown.
		/// </param>
		/// <param name="check">Checked or not. If null, adds label instead of checkbox.</param>
		/// <param name="tt">Tooltip text.</param>
		/// <param name="info"><see cref="ZShowEditInfo"/> text.</param>
		/// <param name="insertAt"></param>
		/// <param name="etype">Edit cell control type.</param>
		/// <param name="buttonAction">Button click action when etype is Button or TextButton; required.</param>
		/// <param name="comboIndex">If not -1, selects this combo box item.</param>
		public int ZAdd(string key, string name, object value = null, bool? check = false, string tt = null, string info = null, int insertAt = -1,
			EditType etype = EditType.Text, EventHandler buttonAction = null, int comboIndex = -1)
		{
			return _AddRow(key, name, value, check, _RowType.Editable, tt, info, insertAt, etype, buttonAction, comboIndex);
		}

		/// <summary>
		/// Adds row with only checkbox (without an editable cell).
		/// Returns row index.
		/// </summary>
		/// <param name="key">Row's Tag property. If null, uses <paramref name="name"/>. Used by <see cref="ZGetValue(string, out string, bool)"/> and other functions.</param>
		/// <param name="name">Checkbox text.</param>
		/// <param name="check"></param>
		/// <param name="tt">Tooltip text.</param>
		/// <param name="insertAt"></param>
		public int ZAddCheck(string key, string name, bool check = false, string tt = null, int insertAt = -1)
		{
			return _AddRow(key, name, null, check, _RowType.Check, tt, null, insertAt);
		}

		/// <summary>
		/// Adds a header row that can be anywhere (and multiple). It is readonly and spans all columns. Optionally with checkbox.
		/// Returns row index.
		/// </summary>
		/// <param name="name">Read-only text.</param>
		/// <param name="check">Checked or not. If null, adds label instead of checkbox.</param>
		/// <param name="tt">Tooltip text.</param>
		/// <param name="insertAt"></param>
		/// <param name="key">Row's Tag property. If null, uses <paramref name="name"/>. Used by <see cref="ZGetValue(string, out string, bool)"/> and other functions.</param>
		public int ZAddHeaderRow(string name, bool? check = null, string tt = null, int insertAt = -1, string key=null)
		{
			return _AddRow(key, name, null, check, _RowType.Header, tt, null, insertAt);
		}

		/// <summary>
		/// If true, ZAdd and similar functions will add hidden rows.
		/// </summary>
		public bool ZAddHidden { get; set; }

		/// <summary>
		/// Returns true if the row is checked or required.
		/// </summary>
		/// <param name="row">Row index. If negative, asserts and returns false.</param>
		public bool ZIsChecked(int row)
		{
			Debug.Assert(row >= 0); if(row < 0) return false;
			if(this[row, 0] is SG.Cells.CheckBox cb) return cb.Checked.GetValueOrDefault();
			return this[row, 0].View == _views[(int)_ViewType.Readonly]; //required; else header row
		}

		/// <summary>
		/// Returns true if the row is checked or required.
		/// </summary>
		/// <param name="rowKey">Row key. If not found, asserts and returns false.</param>
		public bool ZIsChecked(string rowKey) => ZIsChecked(ZFindRow(rowKey));

		/// <summary>
		/// Checks or unchecks.
		/// Use only for flags and optionals, not for required.
		/// </summary>
		/// <param name="row">Row index. If negative, asserts and returns.</param>
		/// <param name="check"></param>
		public void ZCheck(int row, bool check)
		{
			Debug.Assert(row >= 0); if(row < 0) return;
			var cb = this[row, 0] as SG.Cells.CheckBox;
			Debug.Assert(cb != null);
			cb.Checked = check;
		}

		/// <summary>
		/// Checks or unchecks.
		/// Use only for flags and optionals, not for required.
		/// </summary>
		/// <param name="rowKey">Row key. If not found, asserts and returns.</param>
		/// <param name="check"></param>
		public void ZCheck(string rowKey, bool check) => ZCheck(ZFindRow(rowKey), check);
		public void ZCheckIfExists(string rowKey, bool check) { int i = ZFindRow(rowKey); if(i >= 0) ZCheck(i, check); }

		/// <summary>
		/// If the row is checked or required, gets its value and returns true.
		/// </summary>
		/// <param name="row">Row index. If negative, asserts and returns false.</param>
		/// <param name="value"></param>
		/// <param name="falseIfEmpty">Return false if the value is empty (null).</param>
		public bool ZGetValue(int row, out string value, bool falseIfEmpty)
		{
			value = null;
			Debug.Assert(row >= 0); if(row < 0) return false;
			if(!ZIsChecked(row)) return false;
			value = ZGetCellText(row, 1);
			if(falseIfEmpty && value == null) return false;
			return true;
		}

		/// <summary>
		/// If the row is checked or required, gets its value and returns true.
		/// </summary>
		/// <param name="rowKey">Row key. If not found, asserts returns false.</param>
		/// <param name="value"></param>
		/// <param name="falseIfEmpty">Return false if the value is empty (null).</param>
		public bool ZGetValue(string rowKey, out string value, bool falseIfEmpty) => ZGetValue(ZFindRow(rowKey), out value, falseIfEmpty);

		/// <summary>
		/// If the row exists and is checked or required, gets its value and returns true.
		/// </summary>
		/// <param name="rowKey">Row key. If not found, returns false.</param>
		/// <param name="value"></param>
		/// <param name="falseIfEmpty">Return false if the value is empty (null).</param>
		public bool ZGetValueIfExists(string rowKey, out string value, bool falseIfEmpty)
		{
			int row = ZFindRow(rowKey);
			if(row < 0) { value = null; return false; }
			return ZGetValue(row, out value, falseIfEmpty);
		}

		/// <summary>
		/// Gets cell value or checkbox label.
		/// </summary>
		/// <param name="row">Row index. If negative, asserts and returns null.</param>
		/// <param name="column">Column index.</param>
		public string ZGetCellText(int row, int column)
		{
			Debug.Assert(row >= 0); if(row < 0) return null;
			var c = this[row, column];
			if(column == 0 && c is SG.Cells.CheckBox cb) return cb.Caption;
			return c.Value as string;
		}

		/// <summary>
		/// Gets cell value or checkbox label.
		/// </summary>
		/// <param name="rowKey">Row key. If not found, asserts and returns null.</param>
		/// <param name="column">Column index.</param>
		public string ZGetCellText(string rowKey, int column) => ZGetCellText(ZFindRow(rowKey), column);

		/// <summary>
		/// Changes cell text or checkbox label.
		/// </summary>
		/// <param name="row">Row index. If negative, asserts and returns null.</param>
		/// <param name="column">Column index.</param>
		/// <param name="text"></param>
		public void ZSetCellText(int row, int column, string text)
		{
			Debug.Assert(row >= 0); if(row < 0) return;
			ZEndEdit(row, column, true);
			var c = this[row, column];
			if(c is SG.Cells.CheckBox cb) cb.Caption = text;
			else c.Value = text;
			InvalidateCell(new SG.Position(row, column));
		}

		/// <summary>
		/// Changes cell value or checkbox label.
		/// </summary>
		/// <param name="rowKey">Row key. If not found, asserts and returns null.</param>
		/// <param name="column">Column index.</param>
		/// <param name="text"></param>
		public void ZSetCellText(string rowKey, int column, string text) => ZSetCellText(ZFindRow(rowKey), column, text);

		/// <summary>
		/// Finds row by row key and returns row index.
		/// Returns -1 if not found.
		/// </summary>
		public int ZFindRow(string rowKey)
		{
			for(int r = 0, n = this.RowsCount; r < n; r++) {
				if(this.Rows[r].Tag as string == rowKey) return r;
			}
			return -1;
		}

		/// <summary>
		/// Gets row key.
		/// </summary>
		public string ZGetRowKey(int row) => this.Rows[row].Tag as string;

		/// <summary>
		/// Removes all rows.
		/// </summary>
		public void Clear()
		{
			//this.Rows.Clear(); //makes editors invalid
			ZEndEdit(true);
			this.RowsCount = 0;
		}

		#endregion public add/get/clear functions

		#region other public functions

		/// <summary>
		/// If editing any cell, gets the cell context and returns true.
		/// </summary>
		public bool ZGetEditCell(out SG.CellContext c)
		{
			//Somehow SG does not have a method to get the edit cell. This code is from SG source, eg in GridVirtual.OnMouseDown.
			if(!Selection.ActivePosition.IsEmpty()) {
				c = new SG.CellContext(this, Selection.ActivePosition);
				if(c.Cell != null && c.IsEditing()) return true;
			}
			c = default;
			return false;
		}

		/// <summary>
		/// If editing the specified cell, gets cell context and returns true.
		/// </summary>
		public bool ZIsEditing(int row, int col, out SG.CellContext c)
		{
			Debug.Assert(row >= 0);
			c = new SG.CellContext(this, new SG.Position(row, col));
			if(c.Cell != null && c.IsEditing()) return true;
			c = default;
			return false;
		}

		/// <summary>
		/// If editing any cell, ends editing and returns true.
		/// </summary>
		/// <param name="cancel">Undo changes.</param>
		public bool ZEndEdit(bool cancel)
		{
			if(!ZGetEditCell(out var c)) return false;
			c.EndEdit(cancel: false);
			return true;
		}

		/// <summary>
		/// If editing the specified cell, ends editing and returns true.
		/// </summary>
		public bool ZEndEdit(int row, int col, bool cancel)
		{
			if(!ZIsEditing(row, col, out var c)) return false;
			c.EndEdit(cancel);
			return true;
		}

		public void ZShowRows(bool visible, int from, int to)
		{
			if(to < 0) to = RowsCount;
			for(int i = from; i < to; i++) Rows.ShowRow(i, visible);
			RecalcCustomScrollBars();
		}

		/// <summary>
		/// Disables or enables cell (can be checkbox).
		/// </summary>
		public void ZEnableCell(int row, int col, bool enable)
		{
			Debug.Assert(row >= 0); if(row < 0) return;
			if(!enable) ZEndEdit(row, col, true);
			var c = this[row, col];
			var e = c.Editor; if(e == null) return;
			if(e.EnableEdit == enable) return;
			e.EnableEdit = enable;
			InvalidateCell(c);
		}

		/// <summary>
		/// Disables or enables cell (can be checkbox).
		/// </summary>
		public void ZEnableCell(string rowKey, int col, bool enable)
			=> ZEnableCell(ZFindRow(rowKey), col, enable);

		#endregion

		//rejected: if disabled, draw gray. Difficult to make all parts gray.
		//protected override void OnEnabledChanged(EventArgs e)
		//{
		//	//ForeColor = c; //does nothing

		//	var enabled = Enabled;
		//	s_textRenderer.Enabled = enabled;
		//	if(_views[(int)_ViewType.Combo] is SG.Cells.Views.ComboBox cb) cb.ElementDropDown.Style = enabled ? ButtonStyle.Normal : ButtonStyle.Disabled;
		//	//these don't work, because grid overrides (draws normal/hot)
		//	//if(_views[(int)_ViewType.Check] is SG.Cells.Views.CheckBox c1) c1.ElementCheckBox.Style = enabled ? ControlDrawStyle.Normal : ControlDrawStyle.Disabled;
		//	//if(_views[(int)_ViewType.HeaderRowCheck] is SG.Cells.Views.CheckBox c2) c2.ElementCheckBox.Style = enabled ? ControlDrawStyle.Normal : ControlDrawStyle.Disabled;

		//	base.OnEnabledChanged(e);
		//}
	}
}
