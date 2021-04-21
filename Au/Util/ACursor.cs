﻿using Au.Types;
using System;
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
//using System.Linq;

namespace Au.Util
{
	/// <summary>
	/// Helps to load cursors, etc.
	/// </summary>
	/// <remarks>
	/// To load cursors for winforms can be used <see cref="System.Windows.Forms.Cursor"/> constructors, but they don't support colors, ani cursors and custom size.
	/// Don't use this class to load cursors for WPF. Its <b>Cursor</b> class loads cursors correctly.
	/// </remarks>
	public class ACursor
	{
		IntPtr _handle;

		//rejected: GC.AddMemoryPressure like with AIcon. Unlikely somebody will use many cursors or even find this class.

		/// <summary>
		/// Sets native cursor handle.
		/// The cursor will be destroyed when disposing this variable or when converting to object of other type.
		/// </summary>
		public ACursor(IntPtr hcursor) { _handle = hcursor; }

		/// <summary>
		/// Destroys native cursor handle.
		/// </summary>
		public void Dispose() {
			if (_handle != default) { Api.DestroyIcon(_handle); _handle = default; }
		}

		///
		~ACursor() => Dispose();

		/// <summary>
		/// Gets native cursor handle.
		/// </summary>
		public IntPtr Handle => _handle;

		///// <summary>
		///// Gets native cursor handle.
		///// </summary>
		//public static implicit operator IntPtr(ACursor cursor) => cursor._handle;

		/// <summary>
		/// Loads cursor from file.
		/// </summary>
		/// <returns>Returns default(ACursor) if failed.</returns>
		/// <param name="file">.cur or .ani file. If not full path, uses <see cref="AFolders.ThisAppImages"/>.</param>
		/// <param name="size">Width and height. If 0, uses system default size, which depends on DPI.</param>
		public static ACursor Load(string file, int size = 0) {
			file = APath.Normalize(file, AFolders.ThisAppImages);
			if (file == null) return null;
			uint fl = Api.LR_LOADFROMFILE; if (size == 0) fl |= Api.LR_DEFAULTSIZE;
			return new ACursor(Api.LoadImage(default, file, Api.IMAGE_CURSOR, size, size, fl));
		}

		/// <summary>
		/// Creates cursor from cursor file data in memory, for example from a managed resource.
		/// </summary>
		/// <returns>Returns default(ACursor) if failed.</returns>
		/// <param name="cursorData">Data of .cur or .ani file.</param>
		/// <param name="size">Width and height. If 0, uses system default size, which depends on DPI.</param>
		/// <remarks>
		/// This function creates/deletes a temporary file, because there is no good API to load cursor from memory.
		/// </remarks>
		public static ACursor Load(byte[] cursorData, int size = 0) {
			var s = AFolders.Temp + Guid.NewGuid().ToString();
			File.WriteAllBytes(s, cursorData);
			try { return Load(s, size); }
			finally { AFile.Delete(s); }

			//If want to avoid temp file, can use:
			//	1. CreateIconFromResourceEx.
			//		But quite much unsafe code (at first need to find cursor offset (of size) and set hotspot), less reliable (may fail for some .ani files), in some cases works not as well (may get wrong-size cursor).
			//		The code moved to the Unused project.
			//	2. CreateIconIndirect. Not tested. No ani. Need to find cursor offset (of size).
			//WPF Cursor ctor uses temp file too.
		}

		/// <summary>
		/// Creates <see cref="System.Windows.Forms.Cursor"/> object that shares native cursor handle with this object.
		/// Returns null if <i>Handle</i> is default(IntPtr).
		/// </summary>
		public System.Windows.Forms.Cursor ToGdipCursor() {
			if (_handle == default) return null;
			var R = new System.Windows.Forms.Cursor(_handle);
			s_cwt.Add(R, this);
			return R;
		}
		static readonly ConditionalWeakTable<System.Windows.Forms.Cursor, ACursor> s_cwt = new();

		//rejected. Don't need. WPF can load from file or stream. Loads correctly.
		///// <summary>
		///// Creates WPF <b>Cursor</b> object from this native cursor.
		///// Returns null if <i>Handle</i> is default(IntPtr).
		///// </summary>
		///// <param name="destroyCursor">If true (default), the returned variable owns the unmanaged cursor and destroys it when disposing. If false, the returned variable just uses the cursor handle and will not destroy; later will need to dispose this variable.</param>
		//public System.Windows.Input.Cursor ToWpfCursor(bool destroyCursor = true) {
		//	if (_handle == default) return null;
		//	var R = System.Windows.Interop.CursorInteropHelper.Create(new _CursorHandle(_handle, destroyCursor));
		//	if (destroyCursor) _handle = default;
		//	return R;
		//}

		//class _CursorHandle : SafeHandleZeroOrMinusOneIsInvalid
		//{
		//	public _CursorHandle(IntPtr handle, bool ownsHandle) : base(ownsHandle) { base.handle = handle; }

		//	protected override bool ReleaseHandle() => Api.DestroyCursor(handle);
		//}

		/// <summary>
		/// Calculates 64-bit FNV1 hash of cursor's mask bitmap.
		/// Returns 0 if fails.
		/// </summary>
		public static unsafe long Hash(IntPtr hCursor) {
			using Api.ICONINFO ii = new(hCursor);
			var hb = Api.CopyImage(ii.hbmMask, Api.IMAGE_BITMAP, 0, 0, Api.LR_COPYDELETEORG | Api.LR_CREATEDIBSECTION);
			long R = 0; Api.BITMAP b;
			if (0 != Api.GetObject(hb, sizeof(Api.BITMAP), &b) && b.bmBits != default)
				R = AHash.Fnv1Long((byte*)b.bmBits, b.bmHeight * b.bmWidthBytes);
			return R;
		}

		///// <summary>
		///// Calculates 64-bit FNV1 hash of cursor's mask bitmap.
		///// Returns 0 if fails.
		///// </summary>
		//public unsafe long Hash() => Hash(_handle);

		/// <summary>
		/// Gets current global mouse cursor.
		/// Returns false if cursor is hidden.
		/// </summary>
		/// <param name="cursor">Receives native handle. Don't destroy.</param>
		public static bool GetCurrentVisibleCursor(out IntPtr cursor) {
			Api.CURSORINFO ci = default; ci.cbSize = Api.SizeOf(ci);
			if (Api.GetCursorInfo(ref ci) && ci.hCursor != default && 0 != (ci.flags & Api.CURSOR_SHOWING)) { cursor = ci.hCursor; return true; }
			cursor = default; return false;
		}

		/// <summary>
		/// Workaround for brief 'wait' cursor when mouse enters a window of current thread first time.
		/// Reason: default thread's cursor is 'wait'. OS shows it before the first WM_SETCURSOR sets correct cursor.
		/// Call eg on WM_CREATE.
		/// </summary>
		internal static void SetArrowCursor_() {
			var h = Api.LoadCursor(default, MCursor.Arrow);
			if (Api.GetCursor() != h) Api.SetCursor(h);
		}
	}
}
