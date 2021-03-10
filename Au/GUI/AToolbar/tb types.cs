using Au.Types;
using Au.Util;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Au
{
public partial class AToolbar
{
	/// <summary>
	/// Toolbar button or separator.
	/// </summary>
	public class ToolbarItem : MTItem
	{
		internal RECT rect;
		internal SIZE textSize;
		internal System.Drawing.Image image2;
		internal TBItemType type;
		internal AMenu menu;
		
		internal bool IsSeparator_ => type == TBItemType.Separator;
		internal bool IsGroup_ => type == TBItemType.Group;
		internal bool IsMenu_ => type == TBItemType.Menu;
		internal bool IsSeparatorOrGroup_ => type is (TBItemType.Separator or TBItemType.Group);

		internal bool HasImage_ => image2 != null || imageAsync;
		
		///
		public TBItemType ItemType => type;
		
		///
		public ColorInt TextColor { get; set; }
	}
	
	class _Settings : ASettings
	{
		public static _Settings Load(string file, bool useDefault) => Load<_Settings>(file, useDefault);

		public TBAnchor anchor { get => _anchor; set => Set(ref _anchor, value); }
		TBAnchor _anchor = TBAnchor.TopLeft;

		public TBLayout layout { get => _layout; set => Set(ref _layout, value); }
		TBLayout _layout;

		public TBBorder border { get => _border; set => Set(ref _border, value); }
		TBBorder _border = TBBorder.Width2;

		public bool dispText { get => _dispText; set => Set(ref _dispText, value); }
		bool _dispText = true;

		public bool sizable { get => _sizable; set => Set(ref _sizable, value); }
		bool _sizable = true;

		public bool autoSize { get => _autoSize; set => Set(ref _autoSize, value); }
		bool _autoSize = true;

		public TBFlags miscFlags { get => _miscFlags; set => Set(ref _miscFlags, value); }
		TBFlags _miscFlags = TBFlags.HideWhenFullScreen | TBFlags.ActivateOwnerWindow;

		public System.Windows.Size size { get => _size; set => Set(ref _size, value); }
		System.Windows.Size _size = new(150, 24);

		public double wrapWidth { get => _wrapWidth; set => Set(ref _wrapWidth, value); }
		double _wrapWidth;

		public TBOffsets offsets { get => _location; set => Set(ref _location, value); }
		TBOffsets _location; // = new(150, 5, 7, 7);

		public int screen { get => _screen; set => Set(ref _screen, value); }
		int _screen;
	}
}
}

namespace Au.Types {
	/// <summary>
	/// Used with <see cref="AToolbar.ToolbarItem.ItemType"/>.
	/// </summary>
	public enum TBItemType : byte {
#pragma warning disable 1591 //doc
		Button,
		Menu,
		Separator,
		Group,
#pragma warning restore
	}
	
	/// <summary>
	/// Used with <see cref="AToolbar.MiscFlags"/>.
	/// </summary>
	[Flags]
	public enum TBFlags
	{
		/// <summary>
		/// Activate the owner window when the toolbar clicked. Default.
		/// </summary>
		ActivateOwnerWindow = 1,

		/// <summary>
		/// Hide the toolbar when a full-screen window is active. Default.
		/// </summary>
		HideWhenFullScreen = 2,
	}

	/// <summary>
	/// Used with <see cref="AToolbar.Border"/>.
	/// </summary>
	public enum TBBorder
	{
		//note: don't reorder.

		/// <summary>No border.</summary>
		None,

		/// <summary>1 pixel border.</summary>
		Width1,

		/// <summary>1 pixel border + 1 pixel padding.</summary>
		Width2,

		/// <summary>1 pixel border + 2 pixels padding.</summary>
		Width3,

		/// <summary>1 pixel border + 3 pixels padding.</summary>
		Width4,

		/// <summary>3D border.</summary>
		ThreeD,

		/// <summary>Standard window border.</summary>
		Thick,

		/// <summary>Title bar and standard window border.</summary>
		Caption,

		/// <summary>Title bar, [x] button and standard window border.</summary>
		CaptionX,
	}

	/// <summary>
	/// Used with <see cref="AToolbar.Anchor"/>.
	/// </summary>
	public enum TBAnchor
	{
		//top 1, bottom 2, left 4, right 8

		/// <summary>
		/// Anchors are top and left edges. Default.
		/// </summary>
		TopLeft = 1 | 4,

		/// <summary>
		/// Anchors are top and right edges.
		/// </summary>
		TopRight = 1 | 8,

		/// <summary>
		/// Anchors are bottom and left edges.
		/// </summary>
		BottomLeft = 2 | 4,

		/// <summary>
		/// Anchors are bottom and right edges.
		/// </summary>
		BottomRight = 2 | 8,

		/// <summary>
		/// Anchors are top, left and right edges. The toolbar is resized horizontally when resizing its owner.
		/// </summary>
		TopLR = 1 | 4 | 8,

		/// <summary>
		/// Anchors are bottom, left and right edges. The toolbar is resized horizontally when resizing its owner.
		/// </summary>
		BottomLR = 2 | 4 | 8,

		/// <summary>
		/// Anchors are left, top and bottom edges. The toolbar is resized vertically when resizing its owner.
		/// </summary>
		LeftTB = 4 | 1 | 2,

		/// <summary>
		/// Anchors are right, top and bottom edges. The toolbar is resized vertically when resizing its owner.
		/// </summary>
		RightTB = 8 | 1 | 2,

		/// <summary>
		/// Anchors are all edges. The toolbar is resized when resizing its owner.
		/// </summary>
		All = 15,

		/// <summary>
		/// Use owner's opposite left/right edge than specified. In other words, attach toolbar's left edge to owner's right edge or vice versa.
		/// This flag is for toolbars that normally are outside of the owner rectangle (at the left or right).
		/// This flag cannot be used with <b>TopLR</b>, <b>BottomLR</b>, <b>All</b>.
		/// </summary>
		OppositeEdgeX = 32,

		/// <summary>
		/// Use owner's opposite top/bottom edge than specified. In other words, attach toolbar's top edge to owner's bottom edge or vice versa.
		/// This flag is for toolbars that normally are outside of the owner rectangle (above or below).
		/// This flag cannot be used with <b>LeftTB</b>, <b>RightTB</b>, <b>All</b>.
		/// </summary>
		OppositeEdgeY = 64,
		
		/// <summary>
		/// Anchor is screen, not owner window. Don't move the toolbar together with its owner window.
		/// </summary>
		Screen = 128,
	}

	static partial class TBExt_
	{
		internal static bool HasTop(this TBAnchor a) => 0 != ((int)a & 1);
		internal static bool HasBottom(this TBAnchor a) => 0 != ((int)a & 2);
		internal static bool HasLeft(this TBAnchor a) => 0 != ((int)a & 4);
		internal static bool HasRight(this TBAnchor a) => 0 != ((int)a & 8);
		internal static bool OppositeX(this TBAnchor a) => 0 != ((int)a & 32);
		internal static bool OppositeY(this TBAnchor a) => 0 != ((int)a & 64);
		internal static bool OfScreen(this TBAnchor a) => 0 != ((int)a & 128);
		internal static TBAnchor WithoutFlags(this TBAnchor a) => a & TBAnchor.All;
	}

	//rejected. Instead use System.Windows.Size. It loads 1 assembly in 1.5 ms and does not add much process memory.
	//public struct SizeD : IEquatable<SizeD> {
	//	[JsonInclude]
	//	public double width;
	//	[JsonInclude]
	//	public double height;
	//	
	//	public SizeD(double width, double height) { this.width = width; this.height = height; }
	//
	//	public static bool operator ==(SizeD s1, SizeD s2) => s1.width == s2.width && s1.height == s2.height;
	//	public static bool operator !=(SizeD s1, SizeD s2) => !(s1 == s2);
	//
	//	public override int GetHashCode() => HashCode.Combine(width, height);
	//
	//	public bool Equals(SizeD other) => this == other; //IEquatable
	//
	//	public void Deconstruct(out double width, out double height) { width = this.width; height = this.height; }
	//
	//	public override string ToString() => $"{{cx={width.ToStringInvariant()} cy={height.ToStringInvariant()}}}";
	//}

	/// <summary>
	/// Used with <see cref="AToolbar.Offsets"/>.
	/// </summary>
	public struct TBOffsets : IEquatable<TBOffsets>
	{
		/// <summary>
		/// Horizontal distance from the owner's left edge (right if <see cref="TBAnchor.OppositeEdgeX"/>) to the toolbar's left edge.
		/// </summary>
		public double Left { get; set; }

		/// <summary>
		/// Vertical distance from the owner's top edge (bottom if <see cref="TBAnchor.OppositeEdgeY"/>) to the toolbar's top edge.
		/// </summary>
		public double Top { get; set; }

		/// <summary>
		/// Horizontal distance from the toolbar's right edge to the owner's right edge (left if <see cref="TBAnchor.OppositeEdgeX"/>).
		/// </summary>
		public double Right { get; set; }

		/// <summary>
		/// Vertical distance from the toolbar's bottom edge to the owner's bottom edge (top if <see cref="TBAnchor.OppositeEdgeY"/>).
		/// </summary>
		public double Bottom { get; set; }

		/// <summary>
		/// Sets all properties.
		/// </summary>
		public TBOffsets(double left, double top, double right, double bottom)
		{
			Left = left; Top = top; Right = right; Bottom = bottom;
		}

		///
		public bool Equals(TBOffsets other)
			=> other.Left == this.Left && other.Top == this.Top && other.Right == this.Right && other.Bottom == this.Bottom;

		///
		public override string ToString() => $"L={Left} T={Top} R={Right} B={Bottom}";
	}

	/// <summary>
	/// Reasons to hide a toolbar. Used with <see cref="AToolbar.Hide"/>.
	/// </summary>
	[Flags]
	public enum TBHide
	{
		/// <summary>Owner window is hidden, minimized, etc.</summary>
		Owner = 1,

		/// <summary>A full-screen window is active. See flag <see cref="TBFlags.HideWhenFullScreen"/>.</summary>
		FullScreen = 2,

		//Satellite = 128, //no, _SetVisible and this enum aren't used with satellites

		/// <summary>This and bigger flag values can be used by callers for any purpose. Value 0x10000.</summary>
		User = 0x10000,
	}

	/// <summary>
	/// Used with <see cref="AToolbar.Layout"/>.
	/// </summary>
	public enum TBLayout
	{
		/// <summary>Default layout. Buttons are in single row. Wrapped when exceeds maximal row width. More rows can be added with <see cref="AToolbar.Group"/>.</summary>
		HorizontalWrap,

		/// <summary>Buttons are in single column, like in a popup menu. Separators are horizontal.</summary>
		Vertical, //SHOULDDO: if some buttons don't fit, add overflow drop-down menu. Or scrollbar; or add VerticalScroll.

	//	/// <summary>Buttons are in single row. When it exceeds maximal row width, buttons are moved to a drop-down menu. More rows can be added with <see cref="AToolbar.Group"/>.</summary>
	//	Horizontal,//SHOULDDO
	}

	/// <summary>
	/// Used with <see cref="AToolbar.NoContextMenu"/>.
	/// </summary>
	[Flags]
	public enum TBNoMenu
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		Menu = 1,
		Edit = 1 << 1,
		Anchor = 1 << 2,
		Layout = 1 << 3,
		Border = 1 << 4,
		Sizable = 1 << 5,
		AutoSize = 1 << 6,
		MiscFlags = 1 << 7,
		Toolbars = 1 << 8,
		Help = 1 << 9,
		Close = 1 << 10,
		File = 1 << 11,
		Text = 1 << 12,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}

	/// <summary>
	/// Flags for <see cref="AToolbar"/> constructor.
	/// </summary>
	[Flags]
	public enum TBCtor
	{
		/// <summary>
		/// Don't load saved settings. Delete the settings file of the toolbar, if exists.
		/// </summary>
		ResetSettings = 1,

		/// <summary>
		/// Don't load and save settings. No file will be created or opened.
		/// </summary>
		DontSaveSettings = 2,
	}

	/// <summary>
	/// Used with <see cref="AToolbar.DpiScaling"/>.
	/// </summary>
	public struct TBScaling {
		///
		public TBScaling(bool? size, bool? offsets) { this.size=size; this.offsets=offsets; }
	
		/// <summary>
		/// Scale toolbar size and related properties.
		/// If default (null), scales size, except of empty toolbars created by <see cref="AToolbar.AutoHideScreenEdge"/>.
		/// </summary>
		public bool? size;
	
		/// <summary>
		/// Scale toolbar offsets. See <see cref="AToolbar.Offsets"/>.
		/// If default (null), scales offsets, except when anchor is screen (not window etc).
		/// </summary>
		public bool? offsets;
	}

	/// <summary>
	/// Used with <see cref="AToolbar.Show(AWnd, ITBOwnerObject)"/>.
	/// </summary>
	/// <remarks>
	/// Allows a toolbar to follow an object in the owner window, for example an accessible object or image. Or to hide in certain conditions.
	/// Define a class that implements this interface. Create a variable of that class and pass it to <see cref="AToolbar.Show(AWnd, ITBOwnerObject)"/>.
	/// The interface functions are called every 250 ms, sometimes more frequently. Not called when the owner window is invisible or cloaked or minimized.
	/// </remarks>
	public interface ITBOwnerObject
	{
		/// <summary>
		/// Returns false to close the toolbar.
		/// </summary>
		/// <remarks>
		/// Not called if the owner window is invisible or cloaked or minimized.
		/// The default implementation returns true.
		/// </remarks>
		bool IsAlive => true;

		/// <summary>
		/// Returns false to hide the toolbar temporarily.
		/// </summary>
		/// <remarks>
		/// Not called if the owner window is invisible or cloaked or minimized.
		/// The default implementation returns true.
		/// </remarks>
		bool IsVisible => true;

		/// <summary>
		/// Gets object rectangle.
		/// Returns false if failed.
		/// </summary>
		/// <param name="r">Rectangle in screen coordinates.</param>
		/// <remarks>
		/// Not called if the owner window is invisible or cloaked or minimized or if <see cref="IsVisible"/> returned false.
		/// </remarks>
		bool GetRect(out RECT r);
	}


}