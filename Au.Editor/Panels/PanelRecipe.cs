using System.Linq;
using System.Windows.Controls;
using Au.Controls;
using static Au.Controls.Sci;
using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Classification;
//using Microsoft.CodeAnalysis.Shared.Extensions;
//using Microsoft.CodeAnalysis.CSharp.Extensions;
using EToken = CiStyling.EToken;

class PanelRecipe : DockPanel {
	KScintilla _c;

	//public KScintilla ZControl => _c;

	public PanelRecipe() {
		//this.UiaSetName("Recipe panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_c = new KScintilla {
			Name = "Recipe_text",
			ZInitReadOnlyAlways = true,
			ZInitTagsStyle = KScintilla.ZTagsStyle.User
		};
		_c.ZHandleCreated += _c_ZHandleCreated;

		this.Children.Add(_c);
	}

	private void _c_ZHandleCreated() {
		_c.Call(SCI_SETWRAPMODE, SC_WRAP_WORD);

		_c.zSetMarginWidth(1, 8);
		_c.Call(SCI_MARKERDEFINE, 0, SC_MARK_FULLRECT);
		_c.Call(SCI_MARKERSETBACK, 0, 0xA0E0B0);

		var styles = new CiStyling.TStyles { FontSize = 8 };
		styles.ToScintilla(_c, multiFont: true);

		_c.ZTags.AddLinkTag("+see", _SeeLinkClicked);
		//_c.ZTags.AddLinkTag("+lang", s => run.itSafe("https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/" + s)); //unreliable, the URLs may change
		_c.ZTags.AddLinkTag("+lang", s => run.itSafe("https://www.google.com/search?q=" + Uri.EscapeDataString(s + ", C# reference")));
		//_c.ZTags.AddLinkTag("+guide", s => run.itSafe("https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/" + s)); //rejected. Use <google>.
		_c.ZTags.AddStyleTag(".k", new SciTags.UserDefinedStyle { textColor = 0xFF, bold = true }); //keyword

		_AutoRenderCurrentRecipeScript();
	}

	public void SetText(string code) {
		Panels.PanelManager[this].Visible = true;
		_SetText(code);
	}

	void _SetText(string code) {
		_c.zClearText();

		//rejected:
		//	1. Ignore code before the first ///. Not really useful, just forces to always start with ///.
		//	2. Use {  } for scopes of variables. Its' better to use unique names.
		//	3. Use if(...) {  } to enclose code examples to select which code to test.
		//		Can accidentally damage real 'if' code. I didn't use it; it's better to test codes in other script.

		var ac = new List<(string code, int offset8)>();
		int iCode = 0;
		foreach (var m in code.RxFindAll(@"(?ms)^(?:///(?!=/)\N*\R*)+|^/\*\*.+?\*/\R*")) {
			//print.it(m);
			_Code(iCode, m.Start);
			iCode = m.End;
			_Text(m.Start, m.End);
		}
		_Code(iCode, code.Length);

		void _Text(int start, int end) {
			while (code[end - 1] <= ' ') end--;
			bool ml = code[start + 1] == '*';
			if (ml) {
				start += 3; while (code[start] <= ' ') start++;
				end -= 2; while (end > start && code[end - 1] <= ' ') end--;
			}
			var s = code[start..end];
			if (!ml) s = s.RxReplace(@"(?m)^/// ?", "");
			s = s.RxReplace(@"<see cref=['""](.+?)['""]/>", "<+see '$1'>$1<>");
			//print.it("TEXT"); print.it(s);
			_c.ZTags.AddText(s, true, false, false);
		}

		void _Code(int start, int end) {
			while (end > start && code[end - 1] <= ' ') end--;
			if (end == start) return;
			var s = code[start..end];
			//print.it("CODE"); print.it(s);

			int n1 = _c.zLineCount, offset8 = _c.zLen8 + 2;
			_c.zAppendText("\r\n" + s + "\r\n", andRN: true, scroll: false, ignoreTags: true);
			int n2 = _c.zLineCount - 2;
			for (int i = n1; i < n2; i++) _c.Call(SCI_MARKERADD, i, 0);
			ac.Add((s, offset8));
		}

		//code styling
		if (ac != null) {
			code = string.Join("\r\n", ac.Select(o => o.code));
			Debug.Assert(code.IsAscii()); //never mind: does not support non-ASCII
			var b = new byte[code.Length];
			var document = CiUtil.CreateRoslynDocument(code, needSemantic: true);
			var semo = document.GetSemanticModelAsync().Result;
			var a = Classifier.GetClassifiedSpans(semo, TextSpan.FromBounds(0, code.Length), document.Project.Solution.Workspace);
			foreach (var v in a) {
				//print.it(v.ClassificationType, code[v.TextSpan.Start..v.TextSpan.End]);
				EToken style = CiStyling.StyleFromClassifiedSpan(v, semo);
				if (style == EToken.None) continue;
				for (int i = v.TextSpan.Start; i < v.TextSpan.End; i++) b[i] = (byte)style;
			}
			unsafe {
				fixed (byte* bp = b) {
					int bOffset = 0;
					foreach (var v in ac) {
						_c.Call(SCI_STARTSTYLING, v.offset8);
						_c.Call(SCI_SETSTYLINGEX, v.code.Length, bp + bOffset);
						bOffset += v.code.Length + 2; //+2 for string.Join("\r\n"
					}
				}
			}
			_c.zSetStyled();
		}
	}

	void _SeeLinkClicked(string s) {
		//add same namespaces as in default global.cs. Don't include global.cs because it may be modified.
		string code = $"///<see cref='{s}'/>";
		var document = CiUtil.CreateRoslynDocument(code, needSemantic: true);
		var syn = document.GetSyntaxRootAsync().Result;
		var node = syn.FindToken(code.Length - 3 - s.Length, true).Parent.FirstAncestorOrSelf<CrefSyntax>();
		if (node == null) return;
		var semo = document.GetSemanticModelAsync().Result;
		var si = semo.GetSymbolInfo(node);
		var sym = si.Symbol;
		//print.it(sym, si.CandidateSymbols);
		if (sym == null) {
			if (si.CandidateSymbols.IsDefaultOrEmpty) return;
			sym = si.CandidateSymbols[0];
		}
		var url = CiUtil.GetSymbolHelpUrl(sym);
		if (url != null) run.itSafe(url);
	}

	//class _KScintilla : KScintilla
	//{
	//	//protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
	//	//	switch (msg) {
	//	//	}
	//	//	return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
	//	//}
	//}

	[Conditional("DEBUG")]
	unsafe void _AutoRenderCurrentRecipeScript() {
		string prevText = null;
		SciCode prevDoc = null;
		App.Timer1sWhenVisible += () => {
			if (App.Model.WorkspaceName != "Cookbook") return;
			if (!this.IsVisible) return;
			var doc = Panels.Editor.ZActiveDoc;
			if (doc == null || !doc.ZFile.IsScript) return;
			string text = doc.zText;
			if (text == prevText) return;
			prevText = text;
			//print.it("update");

			int n1 = doc == prevDoc ? _c.Call(SCI_GETFIRSTVISIBLELINE) : 0;
			if (n1 > 0) _c.Hwnd.Send(Api.WM_SETREDRAW);
			_SetText(text);
			if (doc == prevDoc) {
				if (n1 > 0)
					//_c.Call(SCI_SETFIRSTVISIBLELINE, n1);
					timer.after(1, _ => {
						_c.Call(SCI_SETFIRSTVISIBLELINE, n1);
						_c.Hwnd.Send(Api.WM_SETREDRAW, 1);
						Api.RedrawWindow(_c.Hwnd, flags: Api.RDW_ERASE | Api.RDW_FRAME | Api.RDW_INVALIDATE);
					});
			} else prevDoc = doc;
			//rejected: autoscroll. Even if works perfectly, often it is more annoying than useful.
		};
	}
}
