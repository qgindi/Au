//Show/hide code info tool windows such as Regex and Keys.

using Au.Tools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Windows.Controls;

class CiTools {
	//#if DEBUG
	//	public static void RegexTest(int position)
	//	{
	//		var node = NodeAt(position);
	//		print.it(IsInString(ref node, position));
	//	}
	//#endif

	public bool HideTempWindows() {
		bool v1 = _regexWindow?.IsVisible ?? false, v2 = _keysWindow?.IsVisible ?? false;
		if (v1) _regexWindow.Close();
		if (v2) _keysWindow.Close();
		return v1 || v2;
	}

	#region regex

	RegexWindow _regexWindow;
	string _regexTopic;

	void _RegexWindowShow(SciCode doc, string code, int pos16, in CiStringInfo si, bool replace, wnd dontCover = default) {
		int j = si.textSpan.Start;

		_regexWindow ??= new RegexWindow();
		_ShowWindow(_regexWindow, doc, pos16, dontCover);

		if (!replace && si.isClassic) {
			doc.zInsertText(true, si.stringNode.SpanStart, "@");
		}

		var s = _regexWindow.CurrentTopic;
		if (s == "replace") {
			if (!replace) _regexWindow.CurrentTopic = _regexTopic;
		} else if (replace) {
			_regexTopic = s;
			_regexWindow.CurrentTopic = "replace";
		}
		doc.ZTempRanges_Add(this, si.textSpan.Start, si.textSpan.End, onLeave: () => _regexWindow.Close());
	}

	//public bool RegexWindowIsVisible => _regexWindow?.Window.Visible ?? false;

	#endregion

	#region keys

	KeysWindow _keysWindow;

	void _KeysWindowShow(SciCode doc, string code, int pos16, in CiStringInfo si, PSFormat format, wnd dontCover = default) {
		_keysWindow ??= new KeysWindow();
		_ShowWindow(_keysWindow, doc, pos16, dontCover);
		_keysWindow.SetFormat(format);
		doc.ZTempRanges_Add(this, si.textSpan.Start, si.textSpan.End, onLeave: () => _keysWindow.Close());
	}

	#endregion

	static void _ShowWindow(InfoWindow w, SciCode doc, int position, wnd dontCover) {
		if (w.IsVisible) w.Hwnd.ZorderTop();
		var r = CiUtil.GetCaretRectFromPos(doc, position, inScreen: true);
		r.left -= Dpi.Scale(80, doc);
		bool above = !dontCover.Is0;
		if (above) r = RECT.Union(r, dontCover.Rect);
		w.ShowByRect(doc, above ? Dock.Top : Dock.Bottom, r, exactSize: true);
		w.InsertInControl = doc;
	}

	public static void CmdShowRegexWindow() => _ShowRegexOrKeysWindow(true);
	public static void CmdShowKeysWindow() => _ShowRegexOrKeysWindow(false);

	static void _ShowRegexOrKeysWindow(bool isRegex) {
		bool retry = false;
	g1:
		if (!CodeInfo.GetDocumentAndFindToken(out var cd, out var token)) return;
		var pos16 = cd.pos16;
		bool? inString = token.IsInString(pos16, cd.code, out var stri);
		if (inString == null) return;
		if (inString != true) {
			if (isRegex || retry) {
				var s2 = isRegex ? null : "The fastest way to insert 'send keys' code: type kk and press Enter (or Tab, Space, double-click). It shows completion list and selects kkKeysSendSnippet.";
				dialog.showInfo("The text cursor must be in a string.", s2);
				return;
			}

			//is in keys.send argument list?
			var node = token.Parent;
			if (node is not ArgumentListSyntax && !node.Span.ContainsInside(pos16)) {
				node = node.Parent;
				if (node is ArgumentSyntax) node = node.Parent;
			}
			if (node is ArgumentListSyntax && node.Parent is InvocationExpressionSyntax ie && ie.Expression.ToString() == "keys.send") {
				SyntaxToken t1, t2;
				if (pos16 <= token.SpanStart) { t1 = token.GetPreviousToken(); t2 = token; } else { t1 = token; t2 = token.GetNextToken(); }
				//CiUtil.PrintNode(t1);
				//CiUtil.PrintNode(t2);
				SyntaxKind k1 = t1.Kind(), k2 = t2.Kind();
				bool good1 = k1 is SyntaxKind.OpenParenToken or SyntaxKind.CommaToken, good2 = k2 is SyntaxKind.CloseParenToken or SyntaxKind.CommaToken;
				string s;
				if (good1 && good2) s = "\"%\""; else if (good1) s = "\"%\", "; else if (good2) s = ", \"%\""; else s = ", \"%\", ";
				InsertCode.TextSimply(s);
			} else {
				InsertCode.Statements("keys.send(\"%\");", goToPercent: true); //rejected. Eg could be keys.send("", here).
			}
			retry = true;
			goto g1;
		}

		var t = CodeInfo._tools;
		if (isRegex) {
			t._RegexWindowShow(cd.sciDoc, cd.code, pos16, stri, replace: false);
		} else {
			PSFormat format = PSFormat.Keys;
			if (inString == true) {
				var semo = cd.document.GetSemanticModelAsync().Result;
				format = CiUtil.GetParameterStringFormat(token.Parent, semo, true);
			}
			t._KeysWindowShow(cd.sciDoc, cd.code, pos16, stri, format);
		}
	}

	public void ShowForStringParameter(PSFormat stringFormat, CodeInfo.Context cd, in CiStringInfo si, wnd dontCover = default) {
		switch (stringFormat) {
		case PSFormat.Regexp:
		case PSFormat.RegexpReplacement:
			_RegexWindowShow(cd.sciDoc, cd.code, cd.pos16, si, replace: stringFormat == PSFormat.RegexpReplacement, dontCover);
			break;
		case PSFormat.Keys or PSFormat.Hotkey or PSFormat.HotkeyTrigger or PSFormat.TriggerMod:
			_KeysWindowShow(cd.sciDoc, cd.code, cd.pos16, si, stringFormat, dontCover);
			break;
		}
	}
}
