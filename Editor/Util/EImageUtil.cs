using System;
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
using System.Drawing.Imaging;
//using System.Linq;
using System.Xml.Linq;
//using System.Xml.XPath;
using System.IO.Compression;

using Catkeys;
using static Catkeys.NoClass;

#pragma warning disable 649

public static unsafe class EImageUtil
{
	#region API

	internal struct BITMAPINFOHEADER
	{
		public int biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	internal struct BITMAPFILEHEADER
	{
		public ushort bfType;
		public int bfSize;
		public ushort bfReserved1;
		public ushort bfReserved2;
		public int bfOffBits;
	}

	internal struct BITMAPCOREHEADER
	{
		public int bcSize;
		public ushort bcWidth;
		public ushort bcHeight;
		public ushort bcPlanes;
		public ushort bcBitCount;
	}

	internal struct BITMAPV5HEADER
	{
		public int bV5Size;
		public int bV5Width;
		public int bV5Height;
		public ushort bV5Planes;
		public ushort bV5BitCount;
		public int bV5Compression;
		public int bV5SizeImage;
		public int bV5XPelsPerMeter;
		public int bV5YPelsPerMeter;
		public int bV5ClrUsed;
		public int bV5ClrImportant;
		public uint bV5RedMask;
		public uint bV5GreenMask;
		public uint bV5BlueMask;
		public uint bV5AlphaMask;
		public uint bV5CSType;
		public CIEXYZTRIPLE bV5Endpoints;
		public uint bV5GammaRed;
		public uint bV5GammaGreen;
		public uint bV5GammaBlue;
		public uint bV5Intent;
		public uint bV5ProfileData;
		public uint bV5ProfileSize;
		public uint bV5Reserved;
	}

	internal struct CIEXYZTRIPLE
	{
		public CIEXYZ ciexyzRed;
		public CIEXYZ ciexyzGreen;
		public CIEXYZ ciexyzBlue;
	}

	internal struct CIEXYZ
	{
		public int ciexyzX;
		public int ciexyzY;
		public int ciexyzZ;
	}

	#endregion

	public struct BitmapFileInfo
	{
		/// <summary>
		/// Can be BITMAPINFOHEADER/BITMAPV5HEADER or BITMAPCOREHEADER.
		/// </summary>
		public void* biHeader;
		public int width, height, bitCount;
		public bool isCompressed;
	}

	//Checks if it is valid bitmap file header. Returns false if invalid.
	//Gets some info from BITMAPINFOHEADER or BITMAPCOREHEADER.
	public static bool GetBitmapFileInfo(byte[] mem, out BitmapFileInfo x)
	{
		x = new BitmapFileInfo();
		fixed (byte* bp = mem) {
			BITMAPFILEHEADER* f = (BITMAPFILEHEADER*)bp;
			int minHS = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPCOREHEADER);
			if(mem.Length <= minHS || f->bfType != (((byte)'M' << 8) | (byte)'B') || f->bfOffBits >= mem.Length || f->bfOffBits < minHS)
				return false;

			BITMAPINFOHEADER* h = (BITMAPINFOHEADER*)(f + 1);
			int siz = h->biSize;
			if(siz >= sizeof(BITMAPINFOHEADER) && siz <= sizeof(BITMAPV5HEADER) * 2) {
				x.width = h->biWidth;
				x.height = h->biHeight;
				x.bitCount = h->biBitCount;
				x.isCompressed = h->biCompression != 0;
			} else if(siz == sizeof(BITMAPCOREHEADER)) {
				BITMAPCOREHEADER* ch = (BITMAPCOREHEADER*)h;
				x.width = ch->bcWidth;
				x.height = ch->bcHeight;
				x.bitCount = ch->bcBitCount;
			} else return false;

			x.biHeader = h;
			return true;
			//note: don't check f->bfSize. Sometimes it is incorrect (> or < memSize). All programs open such files. Instead check other data later.
		}
	}

	/// <summary>
	/// Image type as detected by <see cref="ImageTypeFromString"/>.
	/// </summary>
	public enum ImageType
	{
		/// <summary>The string isn't image.</summary>
		None,
		/// <summary>Compressed and Base64-encoded bitmap file data with "~:" prefix. See <see cref="ImageToEmbeddedString(string)"/>.</summary>
		Embedded,
		/// <summary>"resource:resource name". An image name from managed resources of this project.</summary>
		Resource,
		/// <summary>.bmp file.</summary>
		Bmp,
		/// <summary>.png, .gif or .jpg file.</summary>
		PngGifJpg,
		/// <summary>.ico file.</summary>
		Ico,
		/// <summary>.cur or .ani file.</summary>
		Cur,
		/// <summary>Icon from a .dll or other file containing icons, like @"C:\a\b.dll,15".</summary>
		IconLib,
		/// <summary>None of other image types, when anyFile is true.</summary>
		ShellIcon
	}

	/// <summary>
	/// Gets image type from string segment start/length.
	/// </summary>
	/// <param name="anyFile">When the string is valid but not of any image type, return ShellIcon instead of None.</param>
	/// <param name="s"></param>
	/// <param name="start">Segment start index.</param>
	/// <param name="length">If -1, gets segment from start to s end.</param>
	public static ImageType ImageTypeFromString(bool anyFile, string s, int start = 0, int length = -1)
	{
		if(length < 0) length = s.Length - start;
		if(length < (anyFile ? 2 : 8)) return ImageType.None; //C:\x.bmp or .h
		char c1 = s[start], c2 = s[start + 1];

		//special strings
		switch(c1) {
		case '~': return (c2 == ':') ? ImageType.Embedded : ImageType.None;
		case 'r': if(s.EqualsPart_(start, "resource:")) return ImageType.Resource; break;
		}

		//file path
		if(length >= 8 && (c1 == '%' || (c2 == ':' && Calc.IsAlpha(c1)) || (c1 == '\\' && c2 == '\\'))) { //is image file path?
			int ext = start + length - 3;
			if(s[ext - 1] == '.') {
				if(s.EqualsPart_(ext, "bmp", true)) return ImageType.Bmp;
				if(s.EqualsPart_(ext, "png", true)) return ImageType.PngGifJpg;
				if(s.EqualsPart_(ext, "gif", true)) return ImageType.PngGifJpg;
				if(s.EqualsPart_(ext, "jpg", true)) return ImageType.PngGifJpg;
				if(s.EqualsPart_(ext, "ico", true)) return ImageType.Ico;
				if(s.EqualsPart_(ext, "cur", true)) return ImageType.Cur;
				if(s.EqualsPart_(ext, "ani", true)) return ImageType.Cur;
			} else if(Calc.IsDigit(s[ext + 2])) { //can be like C:\x.dll,10
				int i = ext + 1;
				for(; i > start + 8; i--) if(!Calc.IsDigit(s[i])) break;
				if(s[i] == '-') i--;
				if(s[i] == ',' && s[i - 4] == '.' && Calc.IsAlpha(s[i - 1])) return ImageType.IconLib;
			}
		}

		if(anyFile) return ImageType.ShellIcon; //can be other file type, URL, .ext, :: ITEMIDLIST, ::{CLSID}
		return ImageType.None;
	}

	/// <summary>
	/// Loads image and returns its data in .bmp file format.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="t"></param>
	/// <param name="searchPath">Use <see cref="Files.SearchPath"/></param>
	/// <remarks>Supports env. var. etc. If not full path, searches in Folders.ThisAppImages.</remarks>
	public static byte[] BitmapFileDataFromString(string s, ImageType t, bool searchPath = false)
	{
		//PrintList(t, s);
		try {
			switch(t) {
			case ImageType.Bmp:
			case ImageType.PngGifJpg:
			case ImageType.Cur:
				if(searchPath) {
					s = Files.SearchPath(s, Folders.ThisAppImages);
					if(s == null) return null;
				} else {
					if(!Path_.IsFullPathEEV(ref s)) return null;
					s = Path_.Normalize(s, Folders.ThisAppImages);
					if(!Files.ExistsAsFile(s)) return null;
				}
				break;
			}

			switch(t) {
			case ImageType.Embedded:
				return Convert_.Decompress(Convert.FromBase64String(s));
			case ImageType.Resource:
				return _ImageToBytes(s, true);
			case ImageType.Bmp:
				return File.ReadAllBytes(s);
			case ImageType.PngGifJpg:
				return _ImageToBytes(s, false);
			case ImageType.Ico:
			case ImageType.IconLib:
			case ImageType.ShellIcon:
			case ImageType.Cur:
				return _IconToBytes(s, t == ImageType.Cur, searchPath);
			}
		}
		catch(Exception ex) { DebugPrint(ex.Message + "    " + s); }
		return null;
	}

	static byte[] _IconToBytes(string s, bool isCursor, bool searchPath)
	{
		//info: would be much easier with Icon.ToBitmap(), but it creates bitmap that is displayed incorrectly when we draw it in Scintilla.
		//	Mask or alpha areas then are black.
		//	Another way - draw it like in the png case. Not tested. Maybe would not work with cursors. Probably slower.

		IntPtr hi; int siz;
		if(isCursor) {
			hi = Api.LoadImage(Zero, s, Api.IMAGE_CURSOR, 0, 0, Api.LR_LOADFROMFILE | Api.LR_DEFAULTSIZE);
			siz = Api.GetSystemMetrics(Api.SM_CXCURSOR);
			//note: if LR_DEFAULTSIZE, uses SM_CXCURSOR, normally 32. It may be not what Explorer displays eg in Cursors folder. But without it gets the first cursor, which often is large, eg 128.
		} else {
			hi = Icons.GetFileIconHandle(s, 16, searchPath ? Icons.IconFlags.SearchPath : 0);
			siz = 16;
		}
		if(hi == Zero) return null;
		try {
			using(var m = new Catkeys.Util.MemoryBitmap(siz, siz)) {
				var r = new RECT(0, 0, siz, siz, false);
				Api.FillRect(m.Hdc, ref r, GetStockObject(0)); //WHITE_BRUSH
				if(!DrawIconEx(m.Hdc, 0, 0, hi, siz, siz, 0, Zero, 3)) return null; //DI_NORMAL

				int headersSize = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER);
				var a = new byte[headersSize + siz * siz * 3];
				fixed (byte* p = a) {
					BITMAPFILEHEADER* f = (BITMAPFILEHEADER*)p;
					BITMAPINFOHEADER* h = (BITMAPINFOHEADER*)(f + 1);
					for(int i = 0; i < headersSize; i++) p[i] = 0;
					byte* bits = p + headersSize;
					h->biSize = sizeof(BITMAPINFOHEADER); h->biBitCount = 24; h->biWidth = siz; h->biHeight = siz; h->biPlanes = 1;
					if(0 == GetDIBits(m.Hdc, m.Hbitmap, 0, siz, bits, h, 0)) return null; //DIB_RGB_COLORS
					f->bfType = ((byte)'M' << 8) | (byte)'B'; f->bfOffBits = headersSize; f->bfSize = a.Length;
				}
				return a;
			}
		}
		finally {
			if(isCursor) DestroyCursor(hi); else Icons.DestroyIconHandle(hi);
		}
	}

	static byte[] _ImageToBytes(string s, bool isResource)
	{
		//FUTURE: support resources of script assembly.

		Image im = isResource ? EResources.GetImageNoCache(s) : Image.FromFile(s);
		if(im == null) return null;
		try {
			//workaround for the black alpha problem. Does not make slower.
			var flags = (ImageFlags)im.Flags;
			if(0 != (flags & ImageFlags.HasAlpha)) { //most png have it
				var t = new Bitmap(im.Width, im.Height);
				t.SetResolution(im.HorizontalResolution, im.VerticalResolution);

				using(var g = Graphics.FromImage(t)) {
					g.Clear(Color.White);
					g.DrawImageUnscaled(im, 0, 0);
				}

				var old = im; im = t; old.Dispose();
			}

			using(var m = new MemoryStream()) {
				im.Save(m, ImageFormat.Bmp);
				return m.ToArray();
			}
		}
		finally { im.Dispose(); }
	}

	[DllImport("user32.dll")]
	internal static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

	[DllImport("user32.dll")]
	internal static extern bool DestroyCursor(IntPtr hCursor);

	[DllImport("gdi32.dll")]
	internal static extern IntPtr GetStockObject(int i);

	[DllImport("gdi32.dll")]
	internal static extern int GetDIBits(IntPtr hdc, IntPtr hbm, int start, int cLines, byte* lpvBits, void* lpbmi, uint usage); //BITMAPINFO*

#if false //currently not used

	internal struct ICONINFO
	{
		public bool fIcon;
		public uint xHotspot;
		public uint yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}
	[DllImport("user32.dll")]
	internal static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

	internal struct BITMAP
	{
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public ushort bmPlanes;
		public ushort bmBitsPixel;
		public IntPtr bmBits;
	}

	/// <summary>
	/// Gets icon or cursor size from its native handle.
	/// Returns 0 if failed.
	/// </summary>
	static int GetIconSizeFromHandle(IntPtr hi)
	{
		if(!GetIconInfo(hi, out var ii)) return 0;
		try {
			var hb = (ii.hbmColor != Zero) ? ii.hbmColor : ii.hbmMask;
			BITMAP b;
			if(0 == Api.GetObject(hb, sizeof(BITMAP), &b)) return 0;
			PrintList(b.bmWidth, b.bmHeight, b.bmBits);
			return b.bmWidth;
		}
		finally {
			Api.DeleteObject(ii.hbmColor);
			Api.DeleteObject(ii.hbmMask);
		}
	}
#endif

	/// <summary>
	/// Compresses in-memory bitmap file data (<see cref="Calc.Compress"/>) and Base64-encodes.
	/// Returns string containing only data (no "~:" prefix).
	/// </summary>
	public static string ImageToEmbeddedString(byte[] bitmapFileData)
	{
		if(bitmapFileData == null) return null;
		return Convert.ToBase64String(Convert_.Compress(bitmapFileData));
	}

	/// <summary>
	/// Compresses image file data (<see cref="Calc.Compress"/>) and Base64-encodes.
	/// Returns string containing only data (no "~:" prefix).
	/// Supports all <see cref="ImageType"/> formats. For non-image files gets icon. Converts icons to bitmap.
	/// Returns null if path is not a valid image string or the file does not exist.
	/// </summary>
	/// <remarks>Supports env. var. etc. If not full path, searches in Folders.ThisAppImages and standard directories.</remarks>
	public static string ImageToEmbeddedString(string path)
	{
		var t = ImageTypeFromString(true, path);
		switch(t) {
		case ImageType.None: return null;
		case ImageType.Embedded: return path.Substring(2);
		case ImageType.Resource: path = path.Substring(9); break;
		}
		return ImageToEmbeddedString(BitmapFileDataFromString(path, t, true));
	}
}
