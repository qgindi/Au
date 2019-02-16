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
//using System.Windows.Forms;
//using System.Drawing;
//using System.Linq;
//using System.Xml.Linq;

using Au;
using Au.Types;
using static Au.NoClass;

using Microsoft.CodeAnalysis;

namespace Au.Compiler
{
	/// <summary>Code errors text builder.</summary>
	class ErrBuilder
	{
		StringBuilder _b;

		public bool IsEmpty => _b == null;
		public int ErrorCount { get; private set; }
		public int WarningCount { get; private set; }

		public void Clear()
		{
			ErrorCount = 0; WarningCount = 0;
			_b = null;
		}

		/// <summary>
		/// Adds compiler error or warning message with a link that opens the C# file and goes to that position.
		/// </summary>
		/// <param name="d">Error or warning.</param>
		/// <param name="fMain">Main file of the compilation. Used only for failures that are not C# errors, eg when fails to open a metadata reference file.</param>
		/// <remarks>
		/// This and other AddX functions add line like "[C:\path\file.cs(line,column)]: message" or "[C:\path\file.cs]: message".
		/// Then our OutputServer.SetNotifications callback will convert the "[...]" part to a link.
		/// </remarks>
		public void AddErrorOrWarning(Diagnostic d, IWorkspaceFile fMain)
		{
			_StartAdd(isWarning: d.Severity != DiagnosticSeverity.Error);
			var s = d.ToString();
			int i = d.Location.IsInSource ? s.IndexOf_("): ") + 1 : 0;
			if(i > 0) {
				_b.AppendFormat("[{0}]{1}", s.Remove(i), s.Substring(i));
			} else {
				_b.AppendFormat("[{0}]: {1}", fMain.FilePath, s);
			}
		}

		/// <summary>
		/// Adds error message with a link that opens the C# file but does not go to a position.
		/// </summary>
		public void AddError(IWorkspaceFile f, string message)
		{
			_StartAdd();
			_b.AppendFormat("[{0}]: {1}", f.FilePath, message);
		}

		/// <summary>
		/// Adds error message with a link that opens the C# file and goes to the specified position.
		/// </summary>
		/// <param name="f">C# file.</param>
		/// <param name="code">f code.</param>
		/// <param name="pos">Position in code.</param>
		/// <param name="message">Text to append. Example: "error: message".</param>
		/// <param name="formatArgs">If not null/empty, calls StringBuilder.AppendFormat(message, formatArgs).</param>
		public void AddError(IWorkspaceFile f, string code, int pos, string message, params object[] formatArgs)
		{
			_StartAdd();
			int line = code.LineIndex_(pos, out var col);
			_Append(f, line, col, message, formatArgs);
		}

		/// <summary>
		/// Adds error message with a link that opens the C# file and goes to the specified position.
		/// </summary>
		/// <param name="pos">Position in code.</param>
		/// <param name="message">Text to append. Example: "error: message".</param>
		/// <param name="formatArgs">If not null/empty, calls StringBuilder.AppendFormat(message, formatArgs).</param>
		public void AddError(IWorkspaceFile f, Microsoft.CodeAnalysis.Text.LinePosition pos, string message, params object[] formatArgs)
		{
			_StartAdd();
			_Append(f, pos.Line, pos.Character, message, formatArgs);
		}

		void _StartAdd(bool isWarning = false)
		{
			if(_b == null) _b = new StringBuilder();
			else _b.AppendLine();

			if(isWarning) WarningCount++; else ErrorCount++;
		}

		void _Append(IWorkspaceFile f, int line, int col, string message, params object[] formatArgs)
		{
			_Append(f.FilePath, line, col, message, formatArgs);
		}

		void _Append(string file, int line, int col, string message, params object[] formatArgs)
		{
			_b.AppendFormat("[{0}({1},{2})]: ", file, ++line, ++col);
			if((formatArgs?.Length ?? 0) != 0) _b.AppendFormat(message, formatArgs); else _b.Append(message);
		}

		public override string ToString() => _b?.ToString();

		/// <summary>
		/// Prints all errors and warnings.
		/// Calls <see cref="Clear"/>.
		/// </summary>
		public void PrintAll()
		{
			if(IsEmpty) return;

			var s = ToString();
			_b.Clear();

			//header line
			_b.AppendFormat("<><Z #{0}>Compilation: ", ErrorCount != 0 ? "F0E080" : "A0E0A0");
			if(ErrorCount != 0) _b.Append(ErrorCount).Append(" errors").Append(WarningCount != 0 ? ", " : "");
			if(WarningCount != 0) _b.Append(WarningCount).Append(
@" warnings <fold>	Example of disabling some warnings in several lines of code:
<code>		#pragma warning disable 168, 649
		code
		code
		#pragma warning restore
</code>	Example of disabling some warnings in whole compilation (at the very start of code):
<code>		/*/ noWarnings 168, 649; */
</code>	Also look in program options, maybe you can disable some warnings in all scripts.</fold>");
			_b.AppendLine("<>");
			//FUTURE: this should be <help>, not <fold>.

			//errors and warnings
			_b.Append(s);

			Print(_b.ToString());
			Clear();
		}

		//currently not used.
		///// <summary>
		///// If the SyntaxTree contains errors, prints them (<see cref="PrintAll"/>) and returns true.
		///// In any case, prints errors and warnings.
		///// </summary>
		///// <param name="tree"></param>
		///// <param name="f"></param>
		///// <param name="printWarnings">Add warnings too (but print only if error).</param>
		//public bool AddAllAndPrint(SyntaxTree tree, IWorkspaceFile f, bool printWarnings = false)
		//{
		//	foreach(var v in tree.GetDiagnostics()) {
		//		if(v.Severity == DiagnosticSeverity.Error || printWarnings) AddErrorOrWarning(v, f);
		//	}
		//	if(ErrorCount == 0) return false;
		//	PrintAll();
		//	return true;
		//}
	}
}
