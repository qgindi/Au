﻿using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Completion;
//using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
//using Microsoft.CodeAnalysis.Tags;
//using Microsoft.CodeAnalysis.DocumentationComments;
//using Microsoft.CodeAnalysis.FindSymbols;
//using Roslyn.Utilities;

/// <summary>
/// Code info util extension methods.
/// </summary>
static class CiUtilExt {
	/// <summary>
	/// Detects whether <i>position</i> is inside a string (literal or interpolated, except when in an interpolation code).
	/// </summary>
	/// <returns>
	/// - false - not in a string; or is in a hole of an interpolated string.
	/// - true - in a string; or in a text part of an interpolated string.
	/// - null - inside of a prefix or suffix of a string or interpolation, eg @", $", @$", """, $$""", {{, }}.
	/// </returns>
	/// <param name="t">This token. It's ref; if <b>EndOfFileToken</b>, the function sets it = previous token.</param>
	/// <param name="position">Where this token has been found using code like <c>var token = root.FindToken(position);</c>.</param>
	/// <param name="code">All code. The function uses it to compare substrings easier and faster.</param>
	/// <param name="x">If the function returns true, receives span etc.</param>
	public static bool? IsInString(this ref SyntaxToken t, int position, string code, out CiStringInfo x) {
		x = default;
		var k = t.Kind();
		if (k == SyntaxKind.EndOfFileToken) {
			t = t.GetPreviousToken(includeSkipped: true, includeDirectives: true);
			k = t.Kind();
		}
		//CiUtil.PrintNode(t);

		var span = t.Span;
		int start = span.Start, end = span.End;
		if (position < start || position > end) return false;
		bool isInterpolated = false, isVerbatim = false, isRaw = false, isRawPrefixCenter = false;
		var node = t.Parent;

		if (k is SyntaxKind.StringLiteralToken or SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken) {
			if (position == start) return false;
			if (k != SyntaxKind.StringLiteralToken) isRaw = true;
			if (!isRaw) {
				if (isVerbatim = code[start++] == '@') if (position == start++) return null; //inside @"
				if (position < end) { end--; goto gTrue; }
				if (code[end - 1] != '\"' || end == start || node.NoClosingQuote()) goto gTrue; //no closing "
				return false;
			} else {
				while (start < end && code[start] == '\"') start++; //skip """
				int nq = start - span.Start;
				bool ml = k == SyntaxKind.MultiLineRawStringLiteralToken;
				if (position < start) { //inside """
					if (_IsRawPrefixCenter(span.Start, nq)) goto gTrue;
					return null;
				}
				if (ml) { //skip newline
					while (start < end && code[start] is ' ' or '\t') start++;
					if (code[start] == '\r') start++;
					if (code[start++] != '\n') return null;
				}
				if (position < start) return null; //before newline

				if (position == end) {
					if (node.NoClosingQuote()) goto gTrue;
					return false;
				}

				while (nq > 0 && end > start && code[--end] == '\"') nq--;
				if (nq > 0) goto gTrue; //unterminated
				if (position > end) return null; //inside """, or """ not in its own line (error)
				if (ml) {
					while (end > start && code[end - 1] is ' ' or '\t') end--;
					if (code[--end] != '\n') return null;
					if (code[end - 1] == '\r') end--;
					if (position > end) return null;
					//never mind indentation
				}
				goto gTrue;
			}
		}
		isInterpolated = true;

		bool _IsRawPrefixCenter(int iq, int nq) {
			//most likely the user will write a raw string at """|""" and not at """"""|
			if (nq < 6 || 0 != (nq & 1)) return false;
			iq += nq / 2; if (iq != position) return false;
			if (isRawPrefixCenter = node.NoClosingQuote()) start = end = iq;
			return isRawPrefixCenter;
		}

		if (k == SyntaxKind.InterpolatedMultiLineRawStringStartToken) {
			int iq = start, nq = 0; while (code[iq] == '$') iq++;
			while (code.Eq(iq + nq, '\"')) nq++;
			if (_IsRawPrefixCenter(iq, nq)) goto gTrue;
		}

		switch (k) {
		case SyntaxKind.InterpolatedStringTextToken:
			goto gTrue;
		case SyntaxKind.InterpolatedStringEndToken:
		case SyntaxKind.InterpolatedRawStringEndToken when position == start:
			_BackToTextToken(ref t);
			goto gTrue;
		case SyntaxKind.InterpolatedRawStringEndToken:
			return position == end ? false : null;
		case SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedVerbatimStringStartToken:
		case SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken:
			return position > start ? null : false;
		case SyntaxKind.OpenBraceToken when t.Parent is InterpolationSyntax:
			if (position == start) {
				if (!_BackToTextToken(ref t)) return null;
				goto gTrue;
			}
			if (code.Eq(position - 1, "{{")) return null;
			break;
		case SyntaxKind.CloseBraceToken when t.Parent is InterpolationSyntax:
			if (code.Eq(position - 1, "}}")) return null;
			break;
		}

		bool _BackToTextToken(ref SyntaxToken t) {
			var tt = t.GetPreviousToken();
			var kk = tt.Kind();
			if (kk == SyntaxKind.OpenBraceToken) return false;
			if (kk == SyntaxKind.InterpolatedStringTextToken) (start, end) = tt.Span;
			else end = start;
			return true;
		}

		return false;
	gTrue:
		x.textSpan = TextSpan.FromBounds(start, end);
		x.stringNode = t.Parent;
		if (isInterpolated && x.stringNode is not InterpolatedStringExpressionSyntax) x.stringNode = x.stringNode.Parent;
		Debug.Assert(x.stringNode is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax);
		if (x.isInterpolated = isInterpolated) {
			var ns = x.stringNode.SpanStart;
			if (code[ns] == '@' || code[ns + 1] == '@') isVerbatim = true;
			else if (code[ns + 1] != '\"' || code.Eq(ns + 1, "\"\"\"")) isRaw = true;
		}
		x.isVerbatim = isVerbatim;
		x.isRaw = isRaw;
		x.isClassic = !(isVerbatim | isRaw);
		x.isRawPrefixCenter = isRawPrefixCenter;
		return true;
	}

	/// <summary>
	/// Returns true if this token is a string, and the closing quote(s) is missing (contains diagnostics error).
	/// </summary>
	public static bool NoClosingQuote(this SyntaxNode t) //fast
		=> t.ContainsDiagnostics && t.GetDiagnostics().Any(o => o.Id is "CS1010" or "CS1039" or "CS8997");
	//Newline in constant, Unterminated string literal, Unterminated raw string literal.
	//note: SyntaxToken may contain the same diagnostics too, but not always, eg no if $$"""""". Same speed.

	public static void Deconstruct(this TextSpan t, out int Start, out int End) { Start = t.Start; End = t.End; }

	public static Range ToRange(this TextSpan t) => t.Start..t.End;

	/// <summary>
	/// <c>position &gt; t.Start &amp;&amp; position &lt; t.End;</c>
	/// </summary>
	public static bool ContainsInside(this TextSpan t, int position) => position > t.Start && position < t.End;

	/// <summary>
	/// <c>position &gt;= t.Start &amp;&amp; position &lt;= t.End;</c>
	/// </summary>
	public static bool ContainsOrTouches(this TextSpan t, int position) => position >= t.Start && position <= t.End;

	/// <summary>
	/// Finds child trivia of this token. Returns default if <i>position</i> is not in child trivia of this token. Does not descend into structured trivia.
	/// The code is from Roslyn source function FindTriviaByOffset. Roslyn has function to find trivia in SyntaxNode (recursive), but not in SyntaxToken.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="position">Position in whole code.</param>
	public static SyntaxTrivia FindTrivia(in this SyntaxToken t, int position) {
		int textOffset = position - t.Position;
		if (textOffset >= 0) {
			var leading = t.LeadingWidth;
			if (textOffset < leading) {
				foreach (var trivia in t.LeadingTrivia) {
					if (textOffset < trivia.FullWidth) return trivia;
					textOffset -= trivia.FullWidth;
				}
			} else if (textOffset >= leading + t.Width) {
				textOffset -= leading + t.Width;
				foreach (var trivia in t.TrailingTrivia) {
					if (textOffset < trivia.FullWidth) return trivia;
					textOffset -= trivia.FullWidth;
				}
			}
		}
		return default;
	}

	/// <summary>
	/// Finds token at <i>position</i>. Returns true if its span contains or touches <i>position</i>.
	/// May return previous token in some cases, eg if the found token is EndOfFileToken.
	/// </summary>
	public static bool FindTouchingToken(this SyntaxNode t, out SyntaxToken token, int position, bool findInsideTrivia = false) {
		token = t.FindToken(position, findInsideTrivia);
		if (!token.IsKind(SyntaxKind.EndOfFileToken) && token.Span.ContainsOrTouches(position)) return true;
		token = token.GetPreviousToken();
		if (token.Span.End == position) return true;
		token = default;
		return false;
	}

	public static bool Eq(this string t, TextSpan span, string s, bool ignoreCase = false)
		=> t.Eq(span.Start..span.End, s, ignoreCase);

	[Conditional("DEBUG")]
	public static void DebugPrint(this CompletionItem t, string color = "blue") {
		print.it($"<><c {color}>{t.DisplayText},    {string.Join("|", t.Tags)},    prefix={t.DisplayTextPrefix},    suffix={t.DisplayTextSuffix},    filter={t.FilterText},    sort={t.SortText},    inline={t.InlineDescription},    automation={t.AutomationText},    provider={t.ProviderName}<>");
		print.it(string.Join("\n", t.Properties));
	}

	[Conditional("DEBUG")]
	public static void DebugPrintIf(this CompletionItem t, bool condition, string color = "blue") {
		if (condition) DebugPrint(t, color);
	}

	public static string QualifiedName(this ISymbol t, bool onlyNamespace = false, bool noDirectName = false) {
		var g = t_qnStack ??= new Stack<string>();
		g.Clear();
		if (noDirectName) t = t.ContainingType ?? t.ContainingNamespace as ISymbol;
		if (!onlyNamespace) for (var k = t; k != null; k = k.ContainingType) g.Push(k.Name);
		for (var n = t.ContainingNamespace; n != null && !n.IsGlobalNamespace; n = n.ContainingNamespace) g.Push(n.Name);
		return string.Join(".", g);
	}
	[ThreadStatic] static Stack<string> t_qnStack;

	/// <summary>
	/// Cached, thread-safe.
	/// </summary>
	public static string QualifiedNameCached(this INamespaceSymbol t) { //only 2 times faster than QualifiedName, but no garbage. Same speed with Dictionary.
																		//if (t.IsGlobalNamespace || t.ContainingNamespace.IsGlobalNamespace) return t.Name; //same speed. Few such namespaces.
		if (!_namespaceNames.TryGetValue(t, out var s)) {
			s = t.QualifiedName();
			_namespaceNames.AddOrUpdate(t, s); //OrUpdate for thread safety
											   //print.it(s);
		}
		return s;
	}
	static ConditionalWeakTable<INamespaceSymbol, string> _namespaceNames = new();
}

struct CiStringInfo {
	/// <summary>
	/// The text part of string literal or a text part of interpolated string. Without enclosing and prefix.
	/// </summary>
	public TextSpan textSpan;

	/// <summary>
	/// Entire string node. Can be either <b>LiteralExpressionSyntax</b> or <b>InterpolatedStringExpressionSyntax</b>.
	/// </summary>
	public SyntaxNode stringNode;

	public bool isInterpolated, isVerbatim, isRaw, isClassic, isRawPrefixCenter;
}