﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

using Au;
using Au.Types;
using Au.Controls;

namespace Au.Tools
{
	class DWinapi : KDialogWindow
	{
		ASqlite _db;
		TextBox tName;
		KSciCodeBox code;

		public DWinapi(string name = null) {
			Title = "Find Windows API";
			var doc = Panels.Editor.ZActiveDoc;
			Owner = GetWindow(doc);

			if (name == null) {
				name = doc.zSelectedText();
				if (!name.NE()) {
					name = name.RegexReplace(@"\W+", " ");
				}
			}

			var b = new AWpfBuilder(this).WinSize(700, 500);
			b.WinProperties(WindowStartupLocation.CenterOwner, showInTaskbar: false);
			b.R.Add("Name", out tName, name).Tooltip(
@"Case-sensitive name of a function, struct, constant, interface, callback.
Use wildcard to specify partial name. Examples: Start*, *End, *AnyPart*
Or text containing multiple full names. Example: Name1 Name2 Name3."
);
			b.Row(-1).Add(out code); code.ZInitBorder = true;
			b.R.AddButton("...", _ => _Menu());
			b.AddOkCancel("OK, copy to clipboard");
			b.End();

			b.OkApply += o => {
				string s = code.zText;
				if (s.NE()) return;
				Clipboard.SetText(s);
				//InsertCode.UsingDirective("System.Runtime.InteropServices"); //no, we don't know where will paste. If the namespace is in favorites (default), will add the using directive automatically when pasted.
			};

			tName.TextChanged += (_, _) => _TextChanged();

			void _Menu() {
				int i = AMenu.ShowSimple("1 Class for Windows API");
				if (i == 1) {
					var s = @"/// <summary>
/// Paste this class in scripts (at the end) and projects (in any file) where you want to use Windows API. Change the class name (api) if want.
/// Then, whenever you need a Windows API function etc, type the class name and dot (api.). The completion list will contain undeclared API too. Select one, and it will add the declaration to this class, possibly with more code required for it.
/// </summary>
unsafe class api : WinAPI {}
";
					code.ZSetText(s);
				}
			}
		}

		protected override void OnSourceInitialized(EventArgs e) {
			_db = EdDatabases.OpenWinapi();
			if (!tName.Text.NE()) _TextChanged();

			base.OnSourceInitialized(e);
		}

		protected override void OnClosed(EventArgs e) {
			_db?.Dispose();
			_db = null;
			base.OnClosed(e);
		}

		void _TextChanged() {
			string name = tName.Text;
			var a = new List<(string name, string code)>();

			int nWild = 0; for (int i = 0; i < name.Length; i++) { switch (name[i]) { case '*': case '?': nWild++; break; } }
			if (name.Length > 0 && (nWild == 0 || name.Length - nWild >= 2)) {
				string sql;
				if (name.Contains(' ')) sql = $"in ('{string.Join("', '", name.RegexFindAll(@"\b[A-Za-z_]\w\w+", 0))}')";
				else if (name.FindAny("*?") >= 0) sql = $"GLOB '{name}'";
				else sql = $"= '{name}'";
				try {
					using var stat = _db.Statement("SELECT name, code FROM api WHERE name " + sql);
					//APerf.First();
					while (stat.Step()) a.Add((stat.GetText(0), stat.GetText(1)));
					//APerf.NW(); //30 ms cold, 10 ms warm. Without index.
				}
				catch (SLException ex) { ADebug.Print(ex.Message); }
			}

			string s = "";
			if (a.Count != 0) {
				s = a[0].code;
				if (a.Count > 1) s = string.Join(s.Starts("internal const") ? "\r\n" : "\r\n\r\n", a.Select(o => o.code));
				s += "\r\n";
			}
			code.ZSetText(s);
		}
	}
}
