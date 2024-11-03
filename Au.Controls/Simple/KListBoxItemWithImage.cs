using System.Windows;
using System.Windows.Controls;

namespace Au.Controls;

public class KListBoxItemWithImage : ListBoxItem {
	readonly TextBlock _tb;
	
	/// <param name="image">String for <see cref="ImageUtil.LoadWpfImageElement"/> or <see cref="FrameworkElement"/> or null.</param>
	/// <param name="text"></param>
	/// <param name="imageMarginTB">Image's top and bottom margins.</param>
	public KListBoxItemWithImage(object image, string text, int imageMarginTB = 0) {
		(var p, _, _tb) = CreateContent(image, text, imageMarginTB);
		Content = p;
	}
	
	public void SetText(string text) {
		_tb.Text = text;
	}
	
	public void SetText(string text, string tooltip) {
		_tb.Text = text;
		ToolTip = tooltip;
	}
	
	public override string ToString() => _tb.Text;
	
	public static (StackPanel panel, FrameworkElement image, TextBlock text) CreateContent(object image, string text, int imageMarginTB) {
		var p = new StackPanel { Orientation = Orientation.Horizontal };
		
		var im = image as FrameworkElement;
		if (im == null && image is string s) try { im = ImageUtil.LoadWpfImageElement(s); } catch {  }
		if (im != null) {
			im.Margin = new(0, imageMarginTB, 4, imageMarginTB);
			p.Children.Add(im);
		}
		
		var tb = new TextBlock { Text = text };
		p.Children.Add(tb);
		return (p, im, tb);
	}
}

//not used
//public class KComboBoxItemWithImage : ComboBoxItem {
//	readonly TextBlock _tb;

//	/// <param name="image">String for <see cref="ImageUtil.LoadWpfImageElement"/> or <see cref="ImageSource"/> or null.</param>
//	/// <param name="text"></param>
//	public KComboBoxItemWithImage(object image, string text) {
//		(var p, _, _tb) = KListBoxItemWithImage.CreateContent(image, text);
//		Content = p;
//	}

//	public override string ToString() => _tb.Text;
//}
