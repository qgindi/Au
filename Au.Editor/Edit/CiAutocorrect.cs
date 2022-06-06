using System.Linq;
using System.Windows.Input;
using Au.Controls;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;

//CONSIDER: menu command "Exit statement on Enter" and toolbar check-button [;].
//	Would complete statement from anywhere in statement.
//	Tab would complete without new line.
//	But problem with @"string". Maybe on Enter show menu "New line|Exit statement".

class CiAutocorrect {
	public class BeforeCharContext {
		public int oldPosUtf8, newPosUtf8;
		public bool dontSuppress;
	}

	/// <summary>
	/// Call when added text with { } etc and want it behave like when the user types { etc.
	/// </summary>
	public void BracketsAdded(SciCode doc, int innerFrom, int innerTo, EBrackets operation) {
		var r = doc.ZTempRanges_Add(this, innerFrom, innerTo);
		if (operation == EBrackets.NewExpression) r.OwnerData = "new";
		else r.OwnerData = "ac";
	}

	public enum EBrackets {
		/// <summary>
		/// The same as when the user types '(' etc and is auto-added ')' etc. The user can overtype the ')' with the same character or delete '()' with Backspace.
		/// </summary>
		Regular,

		/// <summary>
		/// Like Regular, but also the user can overtype entire empty '()' with '[' or '{'. Like always, is auto-added ']' or '}' and the final result is '[]' or '{}'.
		/// </summary>
		NewExpression,
	}

	/// <summary>
	/// Called on Enter, Shift+Enter, Ctrl+Enter, Ctrl+;, Tab, Backspace and Delete, before passing it to Scintilla. Won't pass if returns true.
	/// Enter: If before ')' or ']' and not after ',': leaves the argument list etc and adds newline, maybe semicolon, braces, indentation, and returns true.
	/// Shift+Enter or Ctrl+Enter: The same as above, but anywhere.
	/// Ctrl+;: Like SciBeforeCharAdded(';'), but anywhere; and inserts semicolon now.
	/// Tab: calls/returns SciBeforeCharAdded, which skips auto-added ')' etc.
	/// Backspace: If inside an empty temp range, selects the '()' etc to erase and returns false.
	/// Delete, Backspace: If after deleting newline would be tabs after caret, deletes newline with tabs and returns true.
	/// </summary>
	public bool SciBeforeKey(SciCode doc, KKey key, ModifierKeys mod) {
		switch ((key, mod)) {
		case (KKey.Enter, 0):
			return _OnEnterOrSemicolon(anywhere: false, onSemicolon: false, out _);
		case (KKey.Enter, ModifierKeys.Shift):
		case (KKey.Enter, ModifierKeys.Control):
			_OnEnterOrSemicolon(anywhere: true, onSemicolon: false, out _);
			return true;
		case (KKey.OemSemicolon, ModifierKeys.Control):
			_OnEnterOrSemicolon(anywhere: true, onSemicolon: true, out _);
			return true;
		case (KKey.Back, 0):
			return _OnBackspaceOrDelete(doc, true) || SciBeforeCharAdded(doc, (char)key, out _);
		case (KKey.Delete, 0):
			return _OnBackspaceOrDelete(doc, false);
		case (KKey.Tab, 0):
			return SciBeforeCharAdded(doc, (char)key, out _);
		default:
			Debug.Assert(false);
			return false;
		}
	}

	/// <summary>
	/// Called on WM_CHAR, before passing it to Scintilla. Won't pass if returns true, unless ch is ';'. Not called for chars below space.
	/// If ch is ')' etc, and at current position is ')' etc previously added on '(' etc, clears the temp range, sets the out vars and returns true.
	/// If ch is ';' inside '(...)' and the terminating ';' is missing, sets newPosUtf8 = where ';' should be and returns true.
	/// Also called by SciBeforeKey on Backspace and Tab.
	/// </summary>
	public bool SciBeforeCharAdded(SciCode doc, char ch, out BeforeCharContext c) {
		c = null;
		bool isBackspace = false, isOpenBrac = false;

		int pos = doc.zCurrentPos8;
		if (pos == doc.zLen8 && ch != (char)KKey.Back && !doc.zHasSelection) { //if pos is at the end of text, add newline
			doc.zInsertText(false, pos, "\r\n");
		}

		switch (ch) {
		case ';': return _OnEnterOrSemicolon(anywhere: false, onSemicolon: true, out c);
		case '\"': case '\'': case ')': case ']': case '}': case '>': case (char)KKey.Tab: break; //skip auto-added char
		case (char)KKey.Back: isBackspace = true; break; //delete auto-added char too
		case '[': case '{': case '(': case '<': isOpenBrac = true; break; //replace auto-added '()' when completing 'new Type' with '[]' or '{}'. Also ignore user-typed '(' or '<' after auto-added '()' or '<>' by autocompletion.
		default: return false;
		}

		var r = doc.ZTempRanges_Enum(pos, this, endPosition: (ch == '\"' || ch == '\''), utf8: true).FirstOrDefault();
		if (r == null) return false;
		if (isOpenBrac && !(r.OwnerData == (object)"ac" || r.OwnerData == (object)"new")) return false;
		r.GetCurrentFromTo(out int from, out int to, utf8: true);

		if (isBackspace || isOpenBrac) {
			if (pos != from) return false;
		} else {
			if (ch != (char)KKey.Tab && ch != (char)doc.Call(Sci.SCI_GETCHARAT, to)) return false; //info: '\0' if posUtf8 invalid
			if (ch == (char)KKey.Tab && doc.Call(Sci.SCI_GETCHARAT, pos - 1) < 32) return false; //don't exit temp range if pos is after tab or newline
		}
		for (int i = pos; i < to; i++) switch ((char)doc.Call(Sci.SCI_GETCHARAT, i)) { case ' ': case '\r': case '\n': case '\t': break; default: return false; } //eg space before '}'

		//rejected: ignore user-typed '(' or '<' after auto-added '()' or '<>' by autocompletion. Probably more annoying than useful, because than may want to type (cast) or ()=>lambda or (tup, le).
		//if(isOpenBrac && (ch == '(' || ch == '<') && ch == (char)doc.Call(Sci.SCI_GETCHARAT, pos - 1)) {
		//	r.OwnerData = null;
		//	return true;
		//}
		if (isOpenBrac && r.OwnerData != (object)"new") return false;

		r.Remove();

		if (isBackspace || isOpenBrac) {
			doc.Call(Sci.SCI_SETSEL, pos - 1, to + 1); //select and pass to Scintilla, let it delete or overtype
			return false;
		}

		to++;
		if (ch == (char)KKey.Tab) doc.zCurrentPos8 = to;
		else c = new BeforeCharContext { oldPosUtf8 = pos, newPosUtf8 = to };
		return true;
	}

	/// <summary>
	/// Called on SCN_CHARADDED.
	/// If ch is '(' etc, adds ')' etc. Similar with """ and /*.
	/// At the start of line corrects indentation of '}'. Removes of '#'.
	/// Replaces code like 5s with 5.s(); etc.
	/// </summary>
	public void SciCharAdded(CodeInfo.CharContext c) {
		char ch = c.ch;
		string replaceText = ch switch { '\"' => "\"", '\'' => "'", '(' => ")", '[' => "]", '{' => "}", '<' => ">", '*' => "*/", 's' or 't' or '}' or '#' => "", _ => null };
		if (replaceText == null) return;

		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		string code = cd.code;
		int pos = cd.pos - 1; if (pos < 0) return;

		Debug.Assert(code[pos] == ch);
		if (code[pos] != ch) return;

		bool isBeforeWord = ch != '}' && cd.pos < code.Length && char.IsLetterOrDigit(code[cd.pos]); //usually user wants to enclose the word manually, unless typed '{' in interpolated string
		if (isBeforeWord && ch != '{') return;

		var root = cd.syntaxRoot;
		//if(!root.ContainsDiagnostics) return; //no. Don't use errors. It can do more bad than good. Tested.

		if (ch == 's') { //when typed like 5s or 500ms, replace with 5.s(); or 500.ms();
			if (pos > 0 && code[pos - 1] == 'm') { pos--; replaceText = ".ms();"; } else replaceText = ".s();";
			if (_IsNumericLiteralStatement(out _)) {
				//never mind: should ignore if not int s/ms or double s. Error if eg long or double ms.
				c.doc.zReplaceRange(true, pos, cd.pos, replaceText, moveCurrentPos: true);
				c.ignoreChar = true;
			}
			return;
		}
		if (ch == 't') { //when typed like 5t, replace with for (int i = 0; i < 5; i++) {  }
			if (_IsNumericLiteralStatement(out int spanStart)) {
				var br = code.Eq(cd.pos, '{') ? null : "{  }";
				replaceText = $"for (int i = 0; i < {code[spanStart..pos]}; i++) {br}";
				c.doc.zReplaceRange(true, spanStart, cd.pos, replaceText);
				c.doc.zCurrentPos16 = spanStart + replaceText.Length - (br == null ? 0 : 2);
				c.ignoreChar = true;
			}
			return;
		}
		bool _IsNumericLiteralStatement(out int spanStart) {
			if (pos > 0 && code[pos - 1].IsAsciiDigit()) {
				var node = root.FindToken(pos - 1).Parent;
				if (node.IsKind(SyntaxKind.NumericLiteralExpression) && node.Parent is ExpressionStatementSyntax) {
					var span = node.Span;
					spanStart = span.Start;
					return span.Contains(pos - 1);
				}
			}
			spanStart = 0;
			return false;
		}

		int replaceLength = 0, tempRangeFrom = cd.pos, tempRangeTo = cd.pos, newPos = 0;

		if (ch == '#') { //#directive
			int i = pos; while (i > 0 && code[i - 1] is '\t' or ' ') i--;
			if (i < pos && (i == 0 || code[i - 1] == '\n')) {
				if (root.FindTrivia(pos).IsDirective) {
					c.doc.zDeleteRange(true, i, pos);
				}
			}
			return;
		} else if (ch == '*') { /**/
			var trivia = root.FindTrivia(pos);
			if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) return;
			if (trivia.SpanStart != --pos) return;
			if (pos > 0 && code[pos - 1] == '\n') replaceText = "\r\n*/";
			tempRangeFrom = 0;
		} else {
			var token = root.FindToken(pos);
			var node = token.Parent;

			if (ch == '}') { //decrease indentation
				int i = pos;
				while (i > 0 && code[i - 1] == '\t') i--; //and ignore spaces
				if (i < 2 || code[i - 1] != '\n') return;
				//CiUtil.PrintNode(node);
				if (token.Kind() != SyntaxKind.CloseBraceToken || pos != token.SpanStart) return;
				if (node is not (BlockSyntax or MemberDeclarationSyntax or AccessorListSyntax or InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax or SwitchExpressionSyntax or PropertyPatternClauseSyntax)) return;
				int ip = code.LastIndexOf('\n', i - 2) + 1;
				var (ind1, _) = _GetLineInd(ip);
				var (ind2, end2) = _GetLineInd(i);
				if (ind1 == 0 || ind2 == --ind1) return;
				c.doc.zReplaceRange(true, i, end2, new string('\t', ind1));
				c.ignoreChar = true;
				return;

				(int ind, int end) _GetLineInd(int i) {
					int r = 0;
					for (; i < code.Length && code[i] == '\t'; i++) r++;
					return (r, i);
				}
			}

			var kind = node.Kind();
			if (kind == SyntaxKind.InterpolatedStringText) {
				node = node.Parent;
				kind = node.Kind();
			}

			if (isBeforeWord && kind != SyntaxKind.Interpolation) return;

			var span = node.Span;
			if (span.Start > pos) return; // > if pos is in node's leading trivia, eg comments or #if-disabled block

			//CiUtil.PrintNode(node);
			//CiUtil.PrintNode(token);
			//print.it("ALL");
			//CiUtil.PrintNode(root, false);

			if (ch == '\'') {
				if (kind != SyntaxKind.CharacterLiteralExpression || span.Start != pos) return;
			} else if (ch == '\"') {
				bool isVerbatim, isInterpolated = false;
				switch (kind) {
				case SyntaxKind.StringLiteralExpression:
					isVerbatim = code[span.Start] == '@';
					break;
				case SyntaxKind.InterpolatedStringExpression:
					isInterpolated = true;
					isVerbatim = code[span.Start] == '@' || code[span.Start + 1] == '@';
					break;
				default: return;
				}

				if (span.Start != pos - (isVerbatim ? 1 : 0) - (isInterpolated ? 1 : 0)) {
					if (isVerbatim) {
						//rejected: inside verbatim string replace " with "". Often more annoying than useful.
						return;
						//cd.pos--; //insert " before ", and let caret be after ""
						//tempRangeFrom = 0;
					} else if (token.Kind() is SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken && token.Parent.NoClosingQuote()) {
						int i = span.Start;
						while (code[i] == '$') i++;
						int iq = i;
						while (i < pos) if (code[i++] != '\"') return;
						if (!code.Eq(cd.pos, '\"')) replaceText = code[iq..cd.pos]; //add eg """ at """|, else add " at """"|"""
						if (pos - iq >= 2) tempRangeFrom = 0; //don't add temp range, else next typed " would just skip one " instead of inserting ""
					} else return;
				}
			} else {
				//print.it(kind);
				if (ch == '<' && !_IsGenericLessThan()) return; //can be operators
				switch (kind) {
				case SyntaxKind.CompilationUnit:
				case SyntaxKind.CharacterLiteralExpression:
				case SyntaxKind.StringLiteralExpression:
					return;
				case SyntaxKind.InterpolatedStringExpression:
					//after next typed { in interpolated string remove } added after first {
					if (ch == '{' && code.Eq(pos - 1, "{{}") && c.doc.ZTempRanges_Enum(cd.pos, this, endPosition: true).Any()) {
						replaceLength = 1;
						replaceText = null;
						tempRangeFrom = 0;
						break;
					}
					return;
				default:
					if (_IsInNonblankTrivia(node, pos)) return;
					if (ch == '{') {
						if (kind == SyntaxKind.Interpolation) { //add } or }} etc if need
							int n = 0;
							for (int i = pos; code[i] == '{'; i--) n++;
							if (n > 1) tempRangeFrom = 0; //raw string like $$"""...{{
							for (int i = cd.pos; code.Eq(i, '}'); i++) n--;
							if (n <= 0) return;
							if (n > 1) replaceText = new('}', n);
						} else {
							replaceText = "  }";
							if (pos > 0 && !char.IsWhiteSpace(code[pos - 1])) {
								replaceText = " {  }";
								tempRangeFrom++;
								cd.pos--; replaceLength = 1; //replace the '{' too
							}
							tempRangeTo = tempRangeFrom + 2;
							newPos = tempRangeFrom + 1;
						}
					}
					break;
				}

				bool _IsGenericLessThan() {
					if (kind is SyntaxKind.TypeParameterList or SyntaxKind.TypeArgumentList) return true;
					if (kind != SyntaxKind.LessThanExpression) return false;
					var tok2 = token.GetPreviousToken(); if (!tok2.IsKind(SyntaxKind.IdentifierToken)) return false;
					var semo = cd.semanticModel;
					var sa = semo.GetSymbolInfo(tok2).GetAllSymbols();
					if (!sa.IsEmpty) {
						foreach (var v in sa) {
							//print.it(v);
							if (v is INamedTypeSymbol { IsGenericType: true } or IMethodSymbol { IsGenericMethod: true }) return true;
							//bad: if eg IList and IList<T> are available, GetSymbolInfo gets only IList. Then no '>' completion. The same in VS.
							//	OK if only IList<T> available. Methods OK.
						}
					}
					return false;
				}
			}
		}

		c.doc.zReplaceRange(true, cd.pos, cd.pos + replaceLength, replaceText, moveCurrentPos: ch == ';');
		if (newPos > 0) c.doc.zCurrentPos16 = newPos;

		if (tempRangeFrom > 0) c.doc.ZTempRanges_Add(this, tempRangeFrom, tempRangeTo);
		else c.ignoreChar = true;
	}

	//anywhere true when Ctrl+Enter or Shift+Enter or Ctrl+;.
	static bool _OnEnterOrSemicolon(bool anywhere, bool onSemicolon, out BeforeCharContext bcc) {
		bcc = null; //need to return it only if onSemicolon==true and anywhere==false and returns true
		bool onEnterWithoutMod = !(onSemicolon | anywhere);

		if (!CodeInfo.GetContextWithoutDocument(out var cd)) return false;
		var doc = cd.sci;
		var code = cd.code;
		int pos = cd.pos;
		if (pos < 1) return false;
		if (pos == code.Length) return false;

		bool canCorrect = true, canAutoindent = onEnterWithoutMod, isSelection = doc.zHasSelection; //note: complResult is never Complex here
		if (!anywhere) {
			if (!isSelection) {
				char ch = code[pos];
				canCorrect = (ch == ')' || ch == ']') && code[pos - 1] != ',';
				if (!(canCorrect | canAutoindent)) return false;
				//shoulddo?: don't correct after inner ']' etc, eg A(1, [In] 2)
				//SHOULDDO: don't move ';' outside of lambda expression when user wants to enclose it. Example: timer.after(1, _=>{print.it(1)); (user cannot ype ';' after "print.it(1)".
			} else if (onSemicolon) return false;
		}

		if (!cd.GetDocument()) return false;

		var root = cd.syntaxRoot;
		var tok1 = root.FindToken(pos);

		//CiUtil.PrintNode(tok1, printErrors: true);
		if (!anywhere) {
			if (canCorrect && !isSelection) {
				var tokKind = tok1.Kind();
				canCorrect = (tokKind == SyntaxKind.CloseParenToken || tokKind == SyntaxKind.CloseBracketToken) && tok1.SpanStart == pos;
				if (!(canCorrect | canAutoindent)) return false;
			}

			int r = _InNonblankTriviaOrStringOrChar(cd, tok1, isSelection);
			//print.it(r);
			if (r == 1) return true; //yes and corrected
			if (r == 2) return false; //yes and not corrected
			if (isSelection) return false;
			if (r == 3) { //string or char. Let's complete statement.
				anywhere = canCorrect = true;
				canAutoindent = onEnterWithoutMod = false;
			}
		}

		SyntaxNode nodeFromPos = tok1.Parent;
		//CiUtil.PrintNode(nodeFromPos, printErrors: true);

		SyntaxNode node = null, indentNode = null;
		bool needSemicolon = false, needBlock = false, canExitBlock = false, dontIndent = false, isCase = false;
		SyntaxToken token = default; _Block block = null;
		foreach (var v in nodeFromPos.AncestorsAndSelf()) {
			//print.it(v.GetType().Name);
			if (v is StatementSyntax ss) {
				node = v;
				switch (ss) {
				case IfStatementSyntax k:
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case ForStatementSyntax k:
					if (onSemicolon && !anywhere) return false;
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case CommonForEachStatementSyntax k:
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case WhileStatementSyntax k:
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case FixedStatementSyntax k:
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case LockStatementSyntax k:
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case UsingStatementSyntax k:
					token = k.CloseParenToken;
					block = k.Statement as BlockSyntax;
					break;
				case SwitchStatementSyntax k:
					token = k.CloseParenToken;
					if (canExitBlock = block != null) {
						block = k;
					} else if (token.IsMissing && k.OpenParenToken.IsMissing && k.Expression is CastExpressionSyntax ce) {
						//switch (...) without block is interpreted as switch castexpression
						token = ce.CloseParenToken;
					}
					dontIndent = true;
					break;
				case LocalFunctionStatementSyntax k:
					block = k.Body;
					needSemicolon = block == null && k.ExpressionBody != null;
					break;
				case DoStatementSyntax k:
					needSemicolon = true;
					break;
				case BlockSyntax k:
					if (k.Parent is ExpressionSyntax && anywhere) continue; //eg lambda
					canExitBlock = true;
					break;
				default: //method invocation, assignment expression, return, throw etc. Many cannot have parentheses or children, eg break, goto, empty (;).
					needSemicolon = true;
					break;
				}

				//if eg 'if(...) ThisNodeNotInBraces', next line must have indent of 'if'
				if (needSemicolon && node.Parent is StatementSyntax pa && pa is not BlockSyntax) indentNode = pa;

			} else if (v is MemberDeclarationSyntax mds) {
				node = v;
				switch (mds) {
				case NamespaceDeclarationSyntax k:
					if (!k.OpenBraceToken.IsMissing) block = k;
					else if (!(k.Name?.IsMissing ?? true)) token = k.Name.GetLastToken();
					else return false;
					canExitBlock = block != null;
					dontIndent = true;
					break;
				case BaseTypeDeclarationSyntax k: //class, struct, interface, enum
					block = k;
					canExitBlock = block != null;
					break;
				case EnumMemberDeclarationSyntax:
					canCorrect = false;
					break;
				case BaseMethodDeclarationSyntax k: //method, operator, constructor, destructor
					block = k.Body;
					if (block != null) break;
					if (k.ExpressionBody != null || k.Parent is InterfaceDeclarationSyntax || k.ChildTokens().Any(o => _IsKind(o, SyntaxKind.ExternKeyword, SyntaxKind.AbstractKeyword))) {
						needSemicolon = true;
						//also may need semicolon if partial, but we don't know which part this is.
					}
					break;
				case PropertyDeclarationSyntax k:
					needSemicolon = k.ExpressionBody != null || k.Initializer != null;
					if (!needSemicolon) block = k.AccessorList;
					break;
				case IndexerDeclarationSyntax k:
					needSemicolon = k.ExpressionBody != null;
					if (!needSemicolon) block = k.AccessorList;
					break;
				case EventDeclarationSyntax k:
					block = k.AccessorList;
					break;
				default: //field, event field, delegate, GlobalStatementSyntax
					needSemicolon = true;
					break;
				}
			} else if (v is AttributeListSyntax als) {
				if (v.Parent is ParameterSyntax) continue;
				node = v;
				token = als.CloseBracketToken;
				if (token.IsMissing) canCorrect = false;
				break;
			} else {
				bool canCorrect2 = false;
				switch (v) {
				case ElseClauseSyntax k:
					token = k.ElseKeyword;
					block = k.Statement as BlockSyntax;
					canCorrect2 = block == null && k.Parent is IfStatementSyntax pa && pa.Statement is BlockSyntax;
					break;
				case FinallyClauseSyntax k:
					block = k.Block;
					token = k.FinallyKeyword;
					canCorrect2 = true;
					break;
				case CatchClauseSyntax k:
					block = k.Block;
					if (block == null) {
						token = k.Filter?.CloseParenToken ?? k.Declaration?.CloseParenToken ?? k.CatchKeyword;
					} else {
						//workaround for: if '{ }' is missing but in that place is eg 'if(...) { }', says that the 'if()' is filter and the '{ }' is block of this 'catch'
						var cfilter = k.Filter;
						if (cfilter != null && cfilter.WhenKeyword.IsMissing) {
							block = null;
							token = k.Declaration?.CloseParenToken ?? k.CatchKeyword;
						}
					}
					canCorrect2 = true;
					break;
				case SwitchSectionSyntax sss:
					isCase = true;
					if (canCorrect) {
						token = sss.Labels[^1].ColonToken;
						if (token.IsMissing) needSemicolon = true;
					}
					if (!onSemicolon) canAutoindent = true;
					break;
				case AccessorListSyntax or InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax or SwitchExpressionSyntax or PropertyPatternClauseSyntax:
					canExitBlock = true;
					break;
				case AccessorDeclarationSyntax k:
					if (k.ExpressionBody != null) needSemicolon = true;
					else block = k.Body;
					break;
				case UsingDirectiveSyntax:
				case ExternAliasDirectiveSyntax:
					needSemicolon = true;
					break;
				default: continue;
				}
				node = v;
				if (canCorrect2 && !(canCorrect | onSemicolon) && block == null && pos > v.SpanStart) canCorrect = true;
			}

			if (onSemicolon) canExitBlock = false;
			if (canExitBlock) {
				canCorrect = false;
				canAutoindent = true;
				canExitBlock = anywhere && node == nodeFromPos && tok1.IsKind(SyntaxKind.CloseBraceToken) && pos <= tok1.SpanStart;
			}
			//print.it(canCorrect, canAutoindent, canExitBlock, needSemicolon);

			if (canCorrect) {
				if (needSemicolon) {
					token = node.GetLastToken();
					if (!isCase) needSemicolon = !token.IsKind(SyntaxKind.SemicolonToken);
				} else if (block != null) {
					token = block.OpenBraceToken;
				} else if (!isCase) {
					needBlock = true;
					if (token == default || token.IsMissing) token = node.GetLastToken();
				}
			}
			break;
		}

		//print.it("----");
		//CiUtil.PrintNode(nodeFromPos);
		//CiUtil.PrintNode(node);

		if (node == null) {
#if DEBUG
			switch (nodeFromPos) {
			case CompilationUnitSyntax:
			case UsingDirectiveSyntax:
			case ExternAliasDirectiveSyntax:
				break;
			default:
				Debug_.Print($"{nodeFromPos.Kind()}, '{nodeFromPos}'");
				break;
			}
#endif
			return false;
		}

		if (canCorrect && keys.gui.isPressed(KKey.Escape)) canCorrect = false;
		if (!(canCorrect | canAutoindent)) return false;

		if (canCorrect) {
			//print.it($"Span={ node.Span}, Span.End={node.Span.End}, FullSpan={ node.FullSpan}, SpanStart={ node.SpanStart}, EndPosition={ node.EndPosition}, FullWidth={ node.FullWidth}, HasTrailingTrivia={ node.HasTrailingTrivia}, Position={ node.Position}, Width={ node.Width}");
			//return true;

			int endOfSpan = token.Span.End, endOfFullSpan = token.FullSpan.End;
			//print.it(endOfSpan, endOfFullSpan);

			if (onSemicolon) {
				if (anywhere) {
					doc.zGoToPos(true, (needSemicolon || endOfSpan > pos) ? endOfSpan : endOfFullSpan);
					if (needSemicolon) doc.zReplaceSel(isCase ? ":" : ";");
				} else {
					bcc = new BeforeCharContext { oldPosUtf8 = doc.zPos8(pos), newPosUtf8 = doc.zPos8(endOfSpan), dontSuppress = needSemicolon };
				}
			} else {
				int indent = doc.zLineIndentationFromPos(true, (indentNode ?? node).SpanStart);
				if (needBlock || block != null || isCase) indent++;
				bool indentNext = indent > 0 && code[endOfFullSpan - 1] != '\n' && endOfFullSpan < code.Length; //indent next statement (or whatever) that was in the same line

				var b = new StringBuilder();
				if (needBlock) b.Append(" {"); else if (needSemicolon) b.Append(isCase ? ':' : ';');

				int replaceLen = endOfFullSpan - endOfSpan;
				int endOfFullTrimmed = endOfFullSpan; while (code[endOfFullTrimmed - 1] <= ' ') endOfFullTrimmed--; //remove newline and spaces
				if (endOfFullTrimmed > endOfSpan) b.Append(code, endOfSpan, endOfFullTrimmed - endOfSpan);
				b.AppendLine();

				if (indent > 0) b.Append('\t', dontIndent ? indent - 1 : indent);
				b.AppendLine();

				int finalPos = endOfSpan + b.Length - 2;
				if (needBlock) {
					if (--indent > 0) b.Append('\t', indent);
					b.AppendLine("}");
				}

				int endOfBlock = block?.CloseBraceToken.SpanStart ?? 0;
				if (indentNext) {
					if (endOfFullSpan == endOfBlock) indent--;
					if (indent > 0) b.Append('\t', indent);
				}

				if (endOfBlock > endOfFullSpan) { //if block contains statements, move the closing '}' down
					bool hasNewline = false;
					while (code[endOfBlock - 1] <= ' ') if (code[--endOfBlock] == '\n') hasNewline = true;
					if (endOfBlock > endOfFullSpan) {
						replaceLen += endOfBlock - endOfFullSpan;
						b.Append(code, endOfFullSpan, endOfBlock - endOfFullSpan);
						if (!hasNewline) {
							b.AppendLine();
							if (--indent > 0) b.Append('\t', indent);
						}
					}
				}

				var s = b.ToString();
				//print.it($"'{s}'");
				doc.zReplaceRange(true, endOfSpan, endOfSpan + replaceLen, s);
				doc.zGoToPos(true, finalPos);
			}
		} else { //autoindent
			int indent = 0;

			//print.it(pos);

			//remove spaces and tabs around the line break
			static bool _IsSpace(char c) => c == ' ' || c == '\t';
			int from = pos, to = pos;
			while (from > 0 && _IsSpace(code[from - 1])) from--;
			while (to < code.Length && _IsSpace(code[to])) to++;
			int replaceFrom = from, replaceTo = to;

			if (canExitBlock && (canExitBlock = code.RxMatch(@"(?m)^[\t \r\n]*\}", 0, out RXGroup g, RXFlags.ANCHORED, from..))) replaceTo = g.End;

			if (!canExitBlock) {
				if (node is AttributeListSyntax) { indent--; node = node.Parent; } //don't indent after [Attribute]

				//if we are not inside node span, find the first ancestor node where we are inside
				TextSpan spanN = node.Span;
				if (!(from >= spanN.End && tok1.IsKind(SyntaxKind.CloseParenToken))) { //if after ')', we are after eg if(...) or inside an expression
					for (; !(from > spanN.Start && from < spanN.End); spanN = node.Span) {
						if (node is SwitchSectionSyntax && from >= spanN.End && nodeFromPos is not BreakStatementSyntax) break; //indent switch section statements and 'break'
						node = node.Parent;
						if (node == null) return false;
					}
				}

				//don't indent if we are after 'do ...' or 'try ...' or 'try ... catch ...' or before 'else'
				SyntaxNode doTryChild = null;
				switch (node) {
				case DoStatementSyntax k1: doTryChild = k1.Statement; break;
				case TryStatementSyntax k2: doTryChild = k2.Block; break;
				case IfStatementSyntax k3:
					var e = k3.Else;
					if (e != null && e.SpanStart == to) indent--;
					break;
				}
				if (doTryChild != null && spanN.End >= doTryChild.Span.End) node = node.Parent;
			}

			//get indentation
			for (var v = node; v != null; v = v.Parent) {
				//if(v is not CompilationUnitSyntax) CiUtil.PrintNode(v);
				//if(v is not (CompilationUnitSyntax or TypeDeclarationSyntax or MethodDeclarationSyntax)) CiUtil.PrintNode(v);
				switch (v) {
				case BlockSyntax when v.Parent is not (BlockSyntax or GlobalStatementSyntax or SwitchSectionSyntax): //don't indent block that is child of function, 'if' etc which adds indentation
				case SwitchStatementSyntax: //don't indent 'case' in 'switch'. If node is a switch section, it will indent its child statements and 'break.
				case ElseClauseSyntax or CatchClauseSyntax or FinallyClauseSyntax:
				case PropertyPatternClauseSyntax or RecursivePatternSyntax or BinaryPatternSyntax: //PropertyPatternClauseSyntax and its ancestors
				case LabeledStatementSyntax:
				case AttributeListSyntax:
				case AccessorListSyntax:
				//case NamespaceDeclarationSyntax: //rejected: don't indent namespaces. Now can use file-scoped namespaces.
				case FileScopedNamespaceDeclarationSyntax:
				case IfStatementSyntax k3 when k3.Parent is ElseClauseSyntax:
				case UsingStatementSyntax k4 when k4.Parent is UsingStatementSyntax: //don't indent multiple using()
				case FixedStatementSyntax k5 when k5.Parent is FixedStatementSyntax: //don't indent multiple fixed()
				case BaseArgumentListSyntax or ArgumentSyntax:
				case ExpressionSyntax:
				case EqualsValueClauseSyntax:
				case VariableDeclaratorSyntax or VariableDeclarationSyntax:
				case SelectClauseSyntax:
					//print.it("--" + v.GetType().Name, v.Span, pos);
					continue;
				case GlobalStatementSyntax or CompilationUnitSyntax: //don't indent top-level statements
																	 //case ClassDeclarationSyntax k1 when k1.Identifier.Text is "Program" or "Script": //rejected: don't indent script class content. Now can use top-level statements.
																	 //case ConstructorDeclarationSyntax k2 when k2.Identifier.Text is "Program" or "Script": //rejected: don't indent script constructor content
					goto endLoop1;
				}
				//print.it(v.GetType().Name, v.Span, pos);
				indent++;
			}
		endLoop1:

			//maybe need to add 1 line when breaking line inside '{  }', add tabs in current line, decrement indent in '}' line, etc
			int iOB = 0, iCB = 0;
			switch (node) {
			case BlockSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case SwitchStatementSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case BaseTypeDeclarationSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case NamespaceDeclarationSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case AccessorListSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case InitializerExpressionSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case AnonymousObjectCreationExpressionSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case SwitchExpressionSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			case PropertyPatternClauseSyntax k: iOB = k.OpenBraceToken.Span.End; iCB = k.CloseBraceToken.SpanStart; break;
			}
			bool isBraceLine = to == iCB, expandBraces = isBraceLine && from == iOB;

			//indent if we are directly in switch statement below breakless section. If Ctrl+Enter, instead add 'break;' line.
			bool addBreak = false;
			if (!expandBraces) {
				if (node == nodeFromPos && node is SwitchStatementSyntax ss) {
					var sectionAbove = ss.Sections.LastOrDefault(o => o.FullSpan.End <= pos);
					if (sectionAbove != null && _IsBreaklessSection(sectionAbove)) {
						if (!anywhere) indent++; else addBreak = true;
					}
				} else if (anywhere && nodeFromPos is SwitchLabelSyntax && from >= nodeFromPos.Span.End && node is SwitchSectionSyntax ses && _IsBreaklessSection(ses)) {
					addBreak = true;
				}
				static bool _IsBreaklessSection(SwitchSectionSyntax ss) => !ss.Statements.Any(o => o is BreakStatementSyntax);
			}

			//print.it($"from={from}, to={to}, nodeFromPos={nodeFromPos.GetType().Name}, node={node.GetType().Name}");
			//print.it($"indent={indent}, isBraceLine={isBraceLine}, expandBraces={expandBraces}");

			if (indent < 1 && !(expandBraces | addBreak | canExitBlock)) return false;

			var b = new StringBuilder();

			if (canExitBlock) if (addBreak || expandBraces) { canExitBlock = false; replaceTo = to; }

			//correct 'case' if indented too much. It happens when it is not the first 'case' in section.
			if (!expandBraces && indent > 0 && node is SwitchSectionSyntax && nodeFromPos is SwitchLabelSyntax && from >= nodeFromPos.Span.End) {
				int i = nodeFromPos.SpanStart, j = i;
				if (cd.sci.zLineIndentationFromPos(true, i) != indent - 1) {
					while (_IsSpace(code[i - 1])) i--;
					if (code[i - 1] == '\n') {
						replaceFrom = i;
						b.Append('\t', indent - 1);
						b.Append(code, j, from - j);
					}
				}
			}

			//append newlines and tabs
			if (canExitBlock) {
				if (!dontIndent && indent > 0) indent--;
				b.Append('\t', indent).AppendLine("}");
			} else {
				if (addBreak) {
					if (code[from - 1] == '\n') b.Append('\t', indent + 1); else b.Append(' ');
					b.Append("break;");
					if (node is SwitchSectionSyntax) indent--;
				} else if (expandBraces || from == 0 || code[from - 1] == '\n') {
					if (expandBraces) b.AppendLine();
					b.Append('\t', indent);
				}
				b.AppendLine();
				if (indent > 0 && isBraceLine && !dontIndent) indent--;
			}
			if (indent > 0) b.Append('\t', indent);

			//replace text and set caret position
			var s = b.ToString();
			//print.it($"'{s}'");
			doc.zReplaceRange(true, replaceFrom, replaceTo, s);
			pos = replaceFrom + s.Length;
			if (expandBraces) pos -= indent + 2;
			doc.zGoToPos(true, pos);
		}
		return true;
	}

	/// <returns>0 no, 1 yes and corrected, 2 yes and not corrected, 3 string or char. Returns 0 if isSelection and string/char.</returns>
	static int _InNonblankTriviaOrStringOrChar(CodeInfo.Context cd, SyntaxToken token, bool isSelection) {
		string /*prefix = null,*/ suffix = null; bool newlineLast = false;
		int indent = 0;
		int pos = cd.pos, posStart = pos, posEnd = pos;
		var span = token.Span;
		if (pos < span.Start || pos > span.End) {
			if (isSelection) {
				posStart = cd.sci.zSelectionStart16;
				posEnd = cd.sci.zSelectionEnd16;
			}
			var trivia = token.Parent.FindTrivia(pos);
			//CiUtil.PrintNode(trivia, pos);
			span = trivia.Span;
			if (posStart < span.Start || posEnd > span.End) return 0;
			var kind = trivia.Kind();
			if (posStart == span.Start && kind != SyntaxKind.MultiLineDocumentationCommentTrivia) return 0; //info: /** span starts after /**
			switch (kind) {
			case SyntaxKind.MultiLineCommentTrivia:
			case SyntaxKind.MultiLineDocumentationCommentTrivia:
				break;
			case SyntaxKind.SingleLineCommentTrivia:
				suffix = "//";
				break;
			case SyntaxKind.SingleLineDocumentationCommentTrivia:
				suffix = "/// ";
				newlineLast = !isSelection && cd.code.RxIsMatch(@"[ \t]*///", RXFlags.ANCHORED, pos..);
				break;
			default: return 0;
			}
			if (suffix != null && !isSelection) { //trim spaces
				while (posStart > span.Start && cd.code[posStart - 1] == ' ') posStart--;
				while (posEnd < span.End && cd.code[posEnd] == ' ') posEnd++;
			}
		} else {
			if (isSelection) return 0;
			if (span.ContainsInside(pos) && token.IsKind(SyntaxKind.CharacterLiteralToken)) return 3;
			bool? isString = token.IsInString(pos, cd.code, out var si);
			if (isString == false) return 0;
			if (si.isRawPrefixCenter) cd.sci.zInsertText(true, pos, "\r\n"); //let Enter in raw string prefix like """|""" add extra newline
			if (isString == null || !si.isClassic) return 2;
			return 3;
			//rejected: split string into "abc" + "" or "abc\r\n" + "". Rarely used. Better complete statement.
		}

		var doc = cd.sci;
		indent += doc.zLineIndentationFromPos(true, posStart);
		if (indent < 1 /*&& prefix == null*/ && suffix == null) return 2;

		var b = new StringBuilder();
		//b.Append(prefix);
		if (!newlineLast) b.AppendLine();
		b.Append('\t', indent).Append(suffix);
		if (newlineLast) b.AppendLine();

		var s = b.ToString();
		doc.zReplaceRange(true, posStart, posEnd, s, moveCurrentPos: true);

		return 1;
	}

	static bool _OnBackspaceOrDelete(SciCode doc, bool back) {
		//when joining 2 non-empty lines with Delete or Backspace, remove indentation from the second line
		if (doc.zHasSelection) return false;
		int i = doc.zCurrentPos16;
		var code = doc.zText;
		RXGroup g;
		if (back) {
			int i0 = i;
			if (code.Eq(i - 1, '\n')) i--;
			if (code.Eq(i - 1, '\r')) i--;
			if (i == i0) return false;
		} else {
			//if at the start of a line containing just tabs/spaces, let Delete delete entire line
			if (code.RxMatch(@"(?m)^\h+\R", 0, out g, RXFlags.ANCHORED, i..)) goto g1;
		}
		if (!code.RxMatch(@"(?m)(?<!^)\R\h+", 0, out g, RXFlags.ANCHORED, i..)) return false;
		g1: doc.zDeleteRange(true, g.Start, g.End);
		return true;
	}

	#region util

	class _Block {
		SyntaxNode _b;

		public static implicit operator _Block(BlockSyntax n) => _New(n);
		public static implicit operator _Block(SwitchStatementSyntax n) => _New(n);
		public static implicit operator _Block(AccessorListSyntax n) => _New(n);
		public static implicit operator _Block(BaseTypeDeclarationSyntax n) => _New(n);
		public static implicit operator _Block(NamespaceDeclarationSyntax n) => _New(n);

		static _Block _New(SyntaxNode n) {
			if (n == null || _BraceToken(n, false).IsMissing) return null;
			return new _Block { _b = n };
		}

		static SyntaxToken _BraceToken(SyntaxNode n, bool right) {
			switch (n) {
			case BlockSyntax k: return right ? k.CloseBraceToken : k.OpenBraceToken;
			case SwitchStatementSyntax k: return right ? k.CloseBraceToken : k.OpenBraceToken;
			case AccessorListSyntax k: return right ? k.CloseBraceToken : k.OpenBraceToken;
			case BaseTypeDeclarationSyntax k: return right ? k.CloseBraceToken : k.OpenBraceToken;
			case NamespaceDeclarationSyntax k: return right ? k.CloseBraceToken : k.OpenBraceToken;
			}
			return default;
		}

		public SyntaxToken OpenBraceToken => _BraceToken(_b, false);

		public SyntaxToken CloseBraceToken => _BraceToken(_b, true);
	}

	//currently unused
	//static bool _GetNodeIfNotInNonblankTriviaOrStringOrChar(out SyntaxNode node, CodeInfo.Context cd)
	//{
	//	int pos = cd.position;

	//	var root = cd.document.GetSyntaxRootAsync().Result;
	//	node = root.FindToken(pos).Parent;
	//	if(node.Parent is InterpolatedStringExpressionSyntax iss) node = iss;
	//	//CiUtil.PrintNode(node, true, true);

	//	var span = node.Span;
	//	if(pos > span.Start && pos < span.End) {
	//		switch(node.Kind()) {
	//		case SyntaxKind.CharacterLiteralExpression:
	//		case SyntaxKind.StringLiteralExpression:
	//		case SyntaxKind.InterpolatedStringExpression:
	//		case SyntaxKind.CompilationUnit:
	//		case SyntaxKind.Block:
	//		case SyntaxKind.ClassDeclaration:
	//		case SyntaxKind.StructDeclaration:
	//		case SyntaxKind.InterfaceDeclaration:
	//			return false;
	//		}
	//	} else {
	//		if(pos == span.End) {
	//			switch(node.Kind()) {
	//			case SyntaxKind.CharacterLiteralExpression:
	//			case SyntaxKind.StringLiteralExpression:
	//			case SyntaxKind.InterpolatedStringExpression:
	//				if(node.ContainsDiagnostics && node.GetDiagnostics().Any(o => o.Id == "CS1010")) return false; //newline in constant
	//				break;
	//			}
	//		}
	//		if(_IsInNonblankTrivia(node, pos)) return false;
	//	}

	//	return true;
	//}

	static bool _IsInNonblankTrivia(SyntaxNode node, int pos) {
		var trivia = node.FindTrivia(pos);
		if (trivia.RawKind != 0) {
			//print.it($"{trivia.Kind()}, {pos}, {trivia.FullSpan}, '{trivia}'");
			var ts = trivia.Span;
			if (!(pos > ts.Start && pos < ts.End)) { //pos is not inside trivia; possibly at start or end.
				bool lookBefore = pos == ts.Start && trivia.IsKind(SyntaxKind.EndOfLineTrivia) && node.FullSpan.Start < pos;
				if (!lookBefore) return false;
				trivia = node.FindTrivia(pos - 1); //can be eg single-line comment
				switch (trivia.Kind()) {
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.MultiLineDocumentationCommentTrivia:
					return false;
				}
				//CiUtil.PrintNode(trivia);
			}
			switch (trivia.Kind()) {
			case SyntaxKind.None:
			case SyntaxKind.WhitespaceTrivia:
			case SyntaxKind.EndOfLineTrivia:
				break;
			default:
				return true; //mostly comments, directives and #if-disabled text
			}
		}
		return false;
	}

	static bool _IsKind(in SyntaxToken t, SyntaxKind k1, SyntaxKind k2) { var k = t.RawKind; return k == (int)k1 || k == (int)k2; }

	#endregion
}
