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
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using System.Linq;

using Au.Types;
using static Au.NoClass;

namespace Au.Util
{
	/// <summary>
	/// Temporarily disables file system redirection, to allow this 32-bit process access the 64-bit System32 directory.
	/// </summary>
	public struct DisableFsRedirection : IDisposable
	{
		bool _redirected;
		IntPtr _redirValue;

		/// <summary>
		/// If <see cref="Ver.Is32BitProcessAnd64BitOS"/>, calls API <msdn>Wow64DisableWow64FsRedirection</msdn>, which disables file system redirection.
		/// The caller can call this without checking OS and process bitness. This function checks it and it is fast.
		/// Always call <see cref="Revert"/> or <b>Dispose</b>, for example use <b>finally</b> or <b>using</b> statement. Not calling it is more dangerous than a memory leak. It is not called by GC.
		/// </summary>
		public void Disable()
		{
			if(Ver.Is32BitProcessAnd64BitOS)
				_redirected = Api.Wow64DisableWow64FsRedirection(out _redirValue);
		}

		/// <summary>
		/// If redirected, calls API <msdn>Wow64RevertWow64FsRedirection</msdn>.
		/// </summary>
		public void Revert()
		{
			if(_redirected)
				_redirected = !Api.Wow64RevertWow64FsRedirection(_redirValue);
		}

		/// <summary>
		/// Returns true if <see cref="Ver.Is32BitProcessAnd64BitOS"/> is true and path starts with <see cref="Folders.System"/>.
		/// Most such paths are redirected, therefore you may want to disable redirection with this class.
		/// </summary>
		/// <param name="path">Normalized path. This function does not normalize. Also it is unaware of <c>@"\\?\"</c>.</param>
		public static bool IsSystem64PathIn32BitProcess(string path)
		{
			return 0 != _IsSystem64PathIn32BitProcess(path);
		}

		static int _IsSystem64PathIn32BitProcess(string path)
		{
			if(!Ver.Is32BitProcessAnd64BitOS) return 0;
			string sysDir = Folders.System;
			if(!path.Starts(sysDir, true)) return 0;
			int len = sysDir.Length;
			if(path.Length > len && !APath.LibIsSepChar(path[len])) return 0;
			return len;
		}

		/// <summary>
		/// If <see cref="Ver.Is32BitProcessAnd64BitOS"/> is true and path starts with <see cref="Folders.System"/>, replaces that path part with <see cref="Folders.SystemX64"/>.
		/// It disables redirection to <see cref="Folders.SystemX86"/> for that path.
		/// </summary>
		/// <param name="path">Normalized path. This function does not normalize. Also it is unaware of <c>@"\\?\"</c>.</param>
		/// <param name="ifExistsOnlyThere">Don't replace path if the file or directory exists in the redirected folder or does not exist in the non-redirected folder.</param>
		public static string GetNonRedirectedSystemPath(string path, bool ifExistsOnlyThere = false)
		{
			int i = _IsSystem64PathIn32BitProcess(path);
			if(i == 0) return path;
			if(ifExistsOnlyThere && AFile.ExistsAsAny(path)) return path;
			var s = path.ReplaceAt(0, i, Folders.SystemX64);
			if(ifExistsOnlyThere && !AFile.ExistsAsAny(s)) return path;
			return s;
		}

		/// <summary>
		/// Calls <see cref="Revert"/>.
		/// </summary>
		public void Dispose()
		{
			Revert();
		}
	}
}
