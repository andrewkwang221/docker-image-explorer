using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace DockerImageExplorer;

public static class Theme
{
    public static IValueConverter AddOne { get; } = new AddOneConverter();
    public static IValueConverter OneLine { get; } = new OneLineConverter();
    public static IValueConverter Count { get; } = new CountConverter();

    public static void Apply(Window window)
    {
        var resources = Create();
        if (Application.Current != null)
            Application.Current.Resources = resources;
        else
            window.Resources = resources;

        window.FontFamily = new FontFamily("Segoe UI");
        window.FontSize = 14;
        window.Background = (Brush)resources["WindowBackground"];
        window.Foreground = (Brush)resources["Text"];
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
    }

    public static Brush Get(FrameworkElement element, string key) => (Brush)element.FindResource(key);

    static ResourceDictionary Create()
    {
        bool highContrast = SystemParameters.HighContrast;
        bool light = highContrast || UseLightTheme();
        var accent = AccentColor();
        var window = highContrast ? SystemColors.WindowColor : light ? Color.FromRgb(243, 243, 243) : Color.FromRgb(32, 32, 32);
        var pane = highContrast ? SystemColors.WindowColor : light ? Color.FromRgb(255, 255, 255) : Color.FromRgb(43, 43, 43);
        var text = highContrast ? SystemColors.WindowTextColor : light ? Color.FromRgb(26, 26, 26) : Color.FromRgb(255, 255, 255);
        var secondary = highContrast ? SystemColors.GrayTextColor : light ? Color.FromRgb(92, 92, 92) : Color.FromRgb(200, 200, 200);
        var border = highContrast ? SystemColors.WindowTextColor : light ? Color.FromRgb(229, 229, 229) : Color.FromRgb(61, 61, 61);
        var hover = highContrast ? SystemColors.ControlColor : light ? Color.FromRgb(245, 245, 245) : Color.FromRgb(58, 58, 58);
        var pressed = highContrast ? SystemColors.ControlColor : light ? Color.FromRgb(232, 232, 232) : Color.FromRgb(72, 72, 72);
        var input = highContrast ? SystemColors.WindowColor : light ? Color.FromRgb(255, 255, 255) : Color.FromRgb(32, 32, 32);
        var code = highContrast ? SystemColors.WindowColor : light ? Color.FromRgb(250, 250, 250) : Color.FromRgb(30, 30, 30);
        var preview = highContrast ? SystemColors.ControlColor : light ? Color.FromRgb(240, 240, 240) : Color.FromRgb(26, 26, 26);
        var header = highContrast ? SystemColors.ControlColor : light ? Color.FromRgb(250, 250, 250) : Color.FromRgb(37, 37, 37);
        var selection = highContrast ? SystemColors.HighlightColor : Mix(pane, accent, light ? 0.18 : 0.32);
        var selectionText = highContrast ? SystemColors.HighlightTextColor : text;
        var accentText = highContrast ? SystemColors.HighlightTextColor : IsLight(accent) ? Colors.Black : Colors.White;
        var accentHover = highContrast ? SystemColors.HighlightColor : Mix(accent, Colors.White, 0.14);
        var accentPressed = highContrast ? SystemColors.HighlightColor : Mix(accent, Colors.Black, 0.14);
        var added = highContrast ? text : light ? Color.FromRgb(11, 106, 48) : Color.FromRgb(111, 207, 151);
        var modified = highContrast ? text : light ? Color.FromRgb(138, 90, 0) : Color.FromRgb(242, 201, 76);
        var deleted = highContrast ? text : light ? Color.FromRgb(196, 49, 75) : Color.FromRgb(235, 87, 87);
        var scrollThumb = highContrast ? text : light ? Color.FromArgb(120, 0, 0, 0) : Color.FromArgb(150, 255, 255, 255);
        var scrollThumbHover = highContrast ? text : light ? Color.FromArgb(190, 0, 0, 0) : Color.FromArgb(220, 255, 255, 255);
        var separatorLine = highContrast ? border : light ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(48, 255, 255, 255);

        var brushes = new Dictionary<string, Brush>
        {
            ["WindowBackground"] = Paint(window),
            ["Pane"] = Paint(pane),
            ["Text"] = Paint(text),
            ["Secondary"] = Paint(secondary),
            ["Border"] = Paint(border),
            ["Hover"] = Paint(hover),
            ["Pressed"] = Paint(pressed),
            ["Input"] = Paint(input),
            ["Code"] = Paint(code),
            ["PreviewSurface"] = Paint(preview),
            ["Header"] = Paint(header),
            ["Selection"] = Paint(selection),
            ["SelectionText"] = Paint(selectionText),
            ["Accent"] = Paint(accent),
            ["AccentText"] = Paint(accentText),
            ["AccentHover"] = Paint(accentHover),
            ["AccentPressed"] = Paint(accentPressed),
            ["AddedBrush"] = Paint(added),
            ["ModifiedBrush"] = Paint(modified),
            ["DeletedBrush"] = Paint(deleted),
            ["ScrollThumb"] = Paint(scrollThumb),
            ["ScrollThumbHover"] = Paint(scrollThumbHover),
            ["SeparatorLine"] = Paint(separatorLine),
        };

        var dict = new ResourceDictionary();
        foreach (var pair in brushes)
            dict.Add(pair.Key, pair.Value);

        dict.Add(typeof(Button), ButtonStyle(brushes, primary: false));
        dict.Add("PrimaryButton", ButtonStyle(brushes, primary: true));
        dict.Add(typeof(ListBox), ListBoxStyle(brushes));
        dict.Add(typeof(ListBoxItem), ListItemStyle(brushes));
        dict.Add(typeof(DataGrid), Extend(typeof(DataGrid),
            Set(Control.BackgroundProperty, brushes["Pane"]),
            Set(Control.ForegroundProperty, brushes["Text"]),
            Set(Control.BorderBrushProperty, brushes["Border"]),
            Set(Control.BorderThicknessProperty, new Thickness(0)),
            Set(DataGrid.GridLinesVisibilityProperty, DataGridGridLinesVisibility.Horizontal),
            Set(DataGrid.HorizontalGridLinesBrushProperty, brushes["Border"]),
            Set(DataGrid.RowHeightProperty, 28.0),
            Set(DataGrid.ColumnHeaderHeightProperty, 32.0),
            Set(DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.Column),
            Set(DataGrid.RowBackgroundProperty, Brushes.Transparent),
            Set(DataGrid.AlternatingRowBackgroundProperty, Brushes.Transparent),
            Set(Control.FontSizeProperty, 13.0)));
        dict.Add(typeof(DataGridRow), RowStyle(brushes));
        dict.Add(typeof(DataGridCell), CellStyle(brushes));
        dict.Add(typeof(DataGridColumnHeader), Extend(typeof(DataGridColumnHeader),
            Set(Control.BackgroundProperty, brushes["Header"]),
            Set(Control.ForegroundProperty, brushes["Text"]),
            Set(Control.FontWeightProperty, FontWeights.SemiBold),
            Set(Control.FontSizeProperty, 12.0),
            Set(Control.PaddingProperty, new Thickness(8, 0, 8, 0)),
            Set(Control.BorderBrushProperty, brushes["Border"]),
            Set(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)),
            Set(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left)));
        dict.Add(typeof(TabControl), Extend(typeof(TabControl),
            Set(Control.BackgroundProperty, Brushes.Transparent),
            Set(Control.BorderThicknessProperty, new Thickness(0)),
            Set(Control.PaddingProperty, new Thickness(0))));
        dict.Add(typeof(TabItem), TabStyle(brushes));
        dict.Add(typeof(TextBox), Extend(typeof(TextBox),
            Set(Control.BackgroundProperty, brushes["Input"]),
            Set(Control.ForegroundProperty, brushes["Text"]),
            Set(Control.BorderBrushProperty, brushes["Border"]),
            Set(Control.BorderThicknessProperty, new Thickness(1)),
            Set(Control.PaddingProperty, new Thickness(8, 0, 8, 0)),
            Set(Control.MinHeightProperty, 32.0),
            Set(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            Set(TextBox.CaretBrushProperty, brushes["Text"]),
            Set(TextBox.SelectionBrushProperty, brushes["Selection"])));
        dict.Add(typeof(ComboBox), ComboStyle(brushes));
        dict.Add(typeof(ComboBoxItem), ComboItemStyle(brushes));
        dict.Add(typeof(CheckBox), CheckBoxStyle(brushes));
        dict.Add(typeof(Menu), MenuBarStyle(brushes));
        dict.Add(typeof(MenuItem), MenuItemStyle(brushes));
        dict.Add(typeof(ContextMenu), ContextMenuStyle(brushes));
        dict.Add(typeof(StatusBar), StatusBarStyle(brushes));
        dict.Add(typeof(StatusBarItem), StatusItemStyle(brushes));
        dict.Add(typeof(ToolTip), ToolTipStyle(brushes));
        var separator = SeparatorStyle(brushes);
        dict.Add(typeof(Separator), separator);
        dict.Add(MenuItem.SeparatorStyleKey, separator);
        dict.Add(typeof(ScrollBar), ScrollBarStyle());
        dict.Add(typeof(Slider), Extend(typeof(Slider),
            Set(Control.ForegroundProperty, brushes["Accent"]),
            Set(Control.BackgroundProperty, brushes["Border"])));
        return dict;
    }

    static Style ButtonStyle(Dictionary<string, Brush> brushes, bool primary)
    {
        var background = primary ? brushes["Accent"] : Brushes.Transparent;
        var foreground = primary ? brushes["AccentText"] : brushes["Text"];
        var border = primary ? brushes["Accent"] : Brushes.Transparent;
        var hover = primary ? brushes["AccentHover"] : brushes["Hover"];
        var pressed = primary ? brushes["AccentPressed"] : brushes["Pressed"];
        var focus = primary ? brushes["AccentText"] : brushes["Accent"];
        var style = new Style(typeof(Button));
        style.Setters.Add(Set(Control.BackgroundProperty, background));
        style.Setters.Add(Set(Control.ForegroundProperty, foreground));
        style.Setters.Add(Set(Control.BorderBrushProperty, border));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(Set(Control.FontFamilyProperty, new FontFamily("Segoe UI")));
        style.Setters.Add(Set(Control.FontSizeProperty, 14.0));
        style.Setters.Add(Set(Control.MinHeightProperty, 32.0));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(12, 0, 12, 0)));
        style.Setters.Add(Set(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(Set(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(Set(Control.SnapsToDevicePixelsProperty, true));
        style.Setters.Add(Set(Control.TemplateProperty, ButtonTemplate(hover, pressed, focus)));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        style.Triggers.Add(disabled);
        return style;
    }

    static ControlTemplate ButtonTemplate(Brush hover, Brush pressed, Brush focus)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chrome";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        template.Triggers.Add(ChromeTrigger(UIElement.IsMouseOverProperty, true, Border.BackgroundProperty, hover));
        template.Triggers.Add(ChromeTrigger(Button.IsPressedProperty, true, Border.BackgroundProperty, pressed));
        template.Triggers.Add(ChromeTrigger(UIElement.IsKeyboardFocusedProperty, true, Border.BorderBrushProperty, focus));
        return template;
    }

    static Style MenuBarStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var items = new FrameworkElementFactory(typeof(StackPanel));
        items.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        items.SetValue(Panel.IsItemsHostProperty, true);
        border.AppendChild(items);
        var style = new Style(typeof(Menu));
        style.Setters.Add(Set(Control.BackgroundProperty, brushes["Pane"]));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.FontFamilyProperty, new FontFamily("Segoe UI")));
        style.Setters.Add(Set(Control.FontSizeProperty, 14.0));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(Menu)) { VisualTree = border }));
        return style;
    }

    static Style MenuItemStyle(Dictionary<string, Brush> brushes)
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.FontFamilyProperty, new FontFamily("Segoe UI")));
        style.Setters.Add(Set(Control.FontSizeProperty, 14.0));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(12, 6, 12, 6)));
        style.Setters.Add(Set(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(Set(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(Set(Control.TemplateProperty, MenuItemTemplate(brushes)));
        var highlighted = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        highlighted.Setters.Add(new Setter(Control.ForegroundProperty, brushes["SelectionText"]));
        style.Triggers.Add(highlighted);
        foreach (var role in new[] { MenuItemRole.TopLevelHeader, MenuItemRole.TopLevelItem })
        {
            var top = new Trigger { Property = MenuItem.RoleProperty, Value = role };
            top.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 0, 10, 0)));
            top.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 32.0));
            style.Triggers.Add(top);
        }
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        style.Triggers.Add(disabled);
        return style;
    }

    static ControlTemplate MenuItemTemplate(Dictionary<string, Brush> brushes)
    {
        var grid = new FrameworkElementFactory(typeof(Grid));
        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.Name = "Chrome";
        chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var row = new FrameworkElementFactory(typeof(DockPanel));
        var gesture = new FrameworkElementFactory(typeof(TextBlock));
        gesture.Name = "Gesture";
        gesture.SetValue(DockPanel.DockProperty, Dock.Right);
        gesture.SetValue(TextBlock.ForegroundProperty, brushes["Secondary"]);
        gesture.SetValue(TextBlock.FontSizeProperty, 12.0);
        gesture.SetValue(FrameworkElement.MarginProperty, new Thickness(32, 0, 0, 0));
        gesture.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        gesture.SetValue(UIElement.IsHitTestVisibleProperty, false);
        gesture.SetBinding(TextBlock.TextProperty, new Binding("InputGestureText") { RelativeSource = RelativeSource.TemplatedParent });
        var header = new FrameworkElementFactory(typeof(ContentPresenter));
        header.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(gesture);
        row.AppendChild(header);
        chrome.AppendChild(row);
        grid.AppendChild(chrome);

        var popup = new FrameworkElementFactory(typeof(Popup));
        popup.Name = "SubMenu";
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsSubmenuOpen")
        {
            RelativeSource = RelativeSource.TemplatedParent,
            Mode = BindingMode.TwoWay,
        });
        popup.SetBinding(Popup.PlacementTargetProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent });
        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, brushes["Pane"]);
        popupBorder.SetValue(Border.BorderBrushProperty, brushes["Border"]);
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        popupBorder.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var items = new FrameworkElementFactory(typeof(StackPanel));
        items.SetValue(Panel.IsItemsHostProperty, true);
        popupBorder.AppendChild(items);
        popup.AppendChild(popupBorder);
        grid.AppendChild(popup);

        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = grid };
        var noGesture = new Trigger { Property = MenuItem.InputGestureTextProperty, Value = "" };
        noGesture.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, "Gesture"));
        template.Triggers.Add(noGesture);
        template.Triggers.Add(ChromeTrigger(MenuItem.IsHighlightedProperty, true, Border.BackgroundProperty, brushes["Selection"]));
        var submenu = new Trigger { Property = MenuItem.RoleProperty, Value = MenuItemRole.SubmenuHeader };
        submenu.Setters.Add(new Setter(Popup.PlacementProperty, PlacementMode.Right, "SubMenu"));
        template.Triggers.Add(submenu);
        return template;
    }

    static Style ContextMenuStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var items = new FrameworkElementFactory(typeof(StackPanel));
        items.SetValue(Panel.IsItemsHostProperty, true);
        border.AppendChild(items);
        var style = new Style(typeof(ContextMenu));
        style.Setters.Add(Set(Control.BackgroundProperty, brushes["Pane"]));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderBrushProperty, brushes["Border"]));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(Set(Control.FontFamilyProperty, new FontFamily("Segoe UI")));
        style.Setters.Add(Set(Control.FontSizeProperty, 14.0));
        style.Setters.Add(Set(Control.SnapsToDevicePixelsProperty, true));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(ContextMenu)) { VisualTree = border }));
        return style;
    }

    static Style ComboStyle(Dictionary<string, Brush> brushes)
    {
        var style = new Style(typeof(ComboBox));
        style.Setters.Add(Set(Control.BackgroundProperty, brushes["Input"]));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderBrushProperty, brushes["Border"]));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(Set(Control.MinHeightProperty, 32.0));
        style.Setters.Add(Set(Control.SnapsToDevicePixelsProperty, true));
        style.Setters.Add(Set(Control.TemplateProperty, ComboTemplate(brushes)));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        style.Triggers.Add(disabled);
        return style;
    }

    static ControlTemplate ComboTemplate(Dictionary<string, Brush> brushes)
    {
        var grid = new FrameworkElementFactory(typeof(Grid));
        var toggle = new FrameworkElementFactory(typeof(ToggleButton));
        toggle.Name = "Drop";
        toggle.SetValue(FrameworkElement.FocusableProperty, false);
        toggle.SetValue(Control.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        toggle.SetValue(Control.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        toggle.SetValue(Control.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        toggle.SetValue(Control.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
        toggle.SetValue(Control.TemplateProperty, ComboToggleTemplate());
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
        {
            RelativeSource = RelativeSource.TemplatedParent,
            Mode = BindingMode.TwoWay,
        });
        grid.AppendChild(toggle);

        var selection = new FrameworkElementFactory(typeof(ContentPresenter));
        selection.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 28, 0));
        selection.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        selection.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        selection.SetValue(UIElement.IsHitTestVisibleProperty, false);
        selection.SetBinding(ContentPresenter.ContentProperty, new Binding("SelectionBoxItem") { RelativeSource = RelativeSource.TemplatedParent });
        selection.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("SelectionBoxItemTemplate") { RelativeSource = RelativeSource.TemplatedParent });
        grid.AppendChild(selection);

        var popup = new FrameworkElementFactory(typeof(Popup));
        popup.Name = "PART_Popup";
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(Popup.StaysOpenProperty, true);
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
        {
            RelativeSource = RelativeSource.TemplatedParent,
            Mode = BindingMode.TwoWay,
        });
        popup.SetBinding(Popup.PlacementTargetProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent });
        var drop = new FrameworkElementFactory(typeof(Border));
        drop.SetValue(Border.BackgroundProperty, brushes["Pane"]);
        drop.SetValue(Border.BorderBrushProperty, brushes["Border"]);
        drop.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        drop.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        drop.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        drop.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth") { RelativeSource = RelativeSource.TemplatedParent });
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(FrameworkElement.MaxHeightProperty, 320.0);
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.SetValue(Control.TemplateProperty, OverlayScrollTemplate(revealOnHover: false));
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        drop.AppendChild(scroll);
        popup.AppendChild(drop);
        grid.AppendChild(popup);

        var template = new ControlTemplate(typeof(ComboBox)) { VisualTree = grid };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, brushes["Hover"], "Drop"));
        template.Triggers.Add(hover);
        var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focused.Setters.Add(new Setter(Control.BorderBrushProperty, brushes["Accent"], "Drop"));
        template.Triggers.Add(focused);
        return template;
    }

    static ControlTemplate ComboToggleTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var arrow = new FrameworkElementFactory(typeof(TextBlock));
        arrow.SetValue(TextBlock.TextProperty, "\uE70D");
        arrow.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        arrow.SetValue(TextBlock.FontSizeProperty, 10.0);
        arrow.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
        arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        arrow.SetValue(UIElement.IsHitTestVisibleProperty, false);
        border.AppendChild(arrow);
        return new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
    }

    static Style ComboItemStyle(Dictionary<string, Brush> brushes)
    {
        var template = ChromeTemplate(typeof(ComboBoxItem));
        template.Triggers.Add(ChromeTrigger(ComboBoxItem.IsHighlightedProperty, true, Border.BackgroundProperty, brushes["Selection"]));
        template.Triggers.Add(ChromeTrigger(ListBoxItem.IsSelectedProperty, true, Border.BackgroundProperty, brushes["Selection"]));
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(8, 4, 18, 4)));
        style.Setters.Add(Set(Control.MinHeightProperty, 28.0));
        style.Setters.Add(Set(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(Set(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(Set(Control.TemplateProperty, template));
        var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlighted.Setters.Add(new Setter(Control.ForegroundProperty, brushes["SelectionText"]));
        style.Triggers.Add(highlighted);
        return style;
    }

    static Style CheckBoxStyle(Dictionary<string, Brush> brushes)
    {
        var panel = new FrameworkElementFactory(typeof(DockPanel));
        panel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var box = new FrameworkElementFactory(typeof(Border));
        box.Name = "Box";
        box.SetValue(DockPanel.DockProperty, Dock.Left);
        box.SetValue(FrameworkElement.WidthProperty, 16.0);
        box.SetValue(FrameworkElement.HeightProperty, 16.0);
        box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        box.SetValue(Border.BorderBrushProperty, brushes["Border"]);
        box.SetValue(Border.BackgroundProperty, brushes["Input"]);
        var mark = new FrameworkElementFactory(typeof(TextBlock));
        mark.Name = "Mark";
        mark.SetValue(TextBlock.TextProperty, "\uE73E");
        mark.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        mark.SetValue(TextBlock.FontSizeProperty, 10.0);
        mark.SetValue(TextBlock.ForegroundProperty, brushes["AccentText"]);
        mark.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        mark.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        mark.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        box.AppendChild(mark);
        panel.AppendChild(box);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        panel.AppendChild(content);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = panel };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, brushes["Hover"], "Box"));
        template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, brushes["Accent"], "Box"));
        template.Triggers.Add(focus);
        var check = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        check.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Mark"));
        check.Setters.Add(new Setter(Border.BackgroundProperty, brushes["Accent"], "Box"));
        check.Setters.Add(new Setter(Border.BorderBrushProperty, brushes["Accent"], "Box"));
        template.Triggers.Add(check);
        var style = new Style(typeof(CheckBox));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(Set(Control.TemplateProperty, template));
        style.Setters.Add(Set(Control.FocusVisualStyleProperty, null!));
        return style;
    }

    static Style StatusBarStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        var items = new FrameworkElementFactory(typeof(DockPanel));
        items.SetValue(Panel.IsItemsHostProperty, true);
        items.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0));
        border.AppendChild(items);
        var style = new Style(typeof(StatusBar));
        style.Setters.Add(Set(Control.BackgroundProperty, brushes["Pane"]));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.FontSizeProperty, 12.0));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(StatusBar)) { VisualTree = border }));
        return style;
    }

    static Style StatusItemStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        var style = new Style(typeof(StatusBarItem));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(StatusBarItem)) { VisualTree = border }));
        return style;
    }

    static Style ToolTipStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        border.AppendChild(content);
        var style = new Style(typeof(ToolTip));
        style.Setters.Add(Set(Control.BackgroundProperty, brushes["Header"]));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderBrushProperty, brushes["Border"]));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        style.Setters.Add(Set(Control.FontSizeProperty, 12.0));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(ToolTip)) { VisualTree = border }));
        return style;
    }

    static Style SeparatorStyle(Dictionary<string, Brush> brushes)
    {
        var line = new FrameworkElementFactory(typeof(Border));
        line.SetValue(FrameworkElement.HeightProperty, 1.0);
        line.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        line.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        line.SetValue(Border.BackgroundProperty, brushes["SeparatorLine"]);
        line.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var style = new Style(typeof(Separator));
        style.Setters.Add(Set(FrameworkElement.HeightProperty, 1.0));
        style.Setters.Add(Set(FrameworkElement.MinHeightProperty, 1.0));
        style.Setters.Add(Set(FrameworkElement.MaxHeightProperty, 1.0));
        style.Setters.Add(Set(FrameworkElement.MarginProperty, new Thickness(0, 5, 0, 5)));
        style.Setters.Add(Set(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(Set(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(Set(Control.FocusableProperty, false));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(Separator)) { VisualTree = line }));
        return style;
    }

    static Style ListBoxStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(ScrollViewer.FocusableProperty, false);
        scroll.SetValue(Control.TemplateProperty, OverlayScrollTemplate(revealOnHover: true));
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, new TemplateBindingExtension(ScrollViewer.HorizontalScrollBarVisibilityProperty));
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, new TemplateBindingExtension(ScrollViewer.VerticalScrollBarVisibilityProperty));
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        border.AppendChild(scroll);
        var style = new Style(typeof(ListBox));
        style.Setters.Add(Set(Control.BackgroundProperty, brushes["Pane"]));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderBrushProperty, brushes["Border"]));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(4)));
        style.Setters.Add(Set(Control.SnapsToDevicePixelsProperty, true));
        style.Setters.Add(Set(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled));
        style.Setters.Add(Set(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto));
        style.Setters.Add(Set(Control.TemplateProperty, new ControlTemplate(typeof(ListBox)) { VisualTree = border }));
        return style;
    }

    static Style ListItemStyle(Dictionary<string, Brush> brushes)
    {
        var template = ChromeTemplate(typeof(ListBoxItem));
        template.Triggers.Add(ChromeTrigger(UIElement.IsMouseOverProperty, true, Border.BackgroundProperty, brushes["Hover"]));
        template.Triggers.Add(ChromeTrigger(ListBoxItem.IsSelectedProperty, true, Border.BackgroundProperty, brushes["Selection"]));
        template.Triggers.Add(InactiveSelectionTrigger(brushes));
        template.Triggers.Add(ChromeTrigger(UIElement.IsKeyboardFocusedProperty, true, Border.BorderBrushProperty, brushes["Accent"]));
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(Set(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(8, 8, 16, 8)));
        style.Setters.Add(Set(Control.MarginProperty, new Thickness(4, 2, 2, 2)));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(Set(Control.FocusVisualStyleProperty, null!));
        style.Setters.Add(Set(Control.TemplateProperty, template));
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.ForegroundProperty, brushes["SelectionText"]));
        style.Triggers.Add(selected);
        var inactive = new MultiTrigger();
        inactive.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, true));
        inactive.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
        inactive.Setters.Add(new Setter(Control.ForegroundProperty, brushes["SelectionText"]));
        inactive.Setters.Add(new Setter(Control.BackgroundProperty, brushes["Selection"]));
        style.Triggers.Add(inactive);
        return style;
    }

    static MultiTrigger InactiveSelectionTrigger(Dictionary<string, Brush> brushes)
    {
        var inactive = new MultiTrigger();
        inactive.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, true));
        inactive.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
        inactive.Setters.Add(new Setter(Border.BackgroundProperty, brushes["Selection"], "Chrome"));
        return inactive;
    }

    static Style CellStyle(Dictionary<string, Brush> brushes)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chrome";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(DataGridCell)) { VisualTree = border };
        template.Triggers.Add(ChromeTrigger(DataGridCell.IsSelectedProperty, true, Border.BackgroundProperty, brushes["Selection"]));
        var inactive = new MultiTrigger();
        inactive.Conditions.Add(new Condition(DataGridCell.IsSelectedProperty, true));
        inactive.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
        inactive.Setters.Add(new Setter(Border.BackgroundProperty, brushes["Selection"], "Chrome"));
        template.Triggers.Add(inactive);
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Text"]));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(8, 0, 8, 0)));
        style.Setters.Add(Set(Control.FocusVisualStyleProperty, null!));
        style.Setters.Add(Set(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(Set(Control.TemplateProperty, template));
        var selected = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.ForegroundProperty, brushes["SelectionText"]));
        style.Triggers.Add(selected);
        var inactiveText = new MultiTrigger();
        inactiveText.Conditions.Add(new Condition(DataGridCell.IsSelectedProperty, true));
        inactiveText.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
        inactiveText.Setters.Add(new Setter(Control.ForegroundProperty, brushes["SelectionText"]));
        style.Triggers.Add(inactiveText);
        return style;
    }

    static Style RowStyle(Dictionary<string, Brush> brushes)
    {
        var style = Extend(typeof(DataGridRow),
            Set(Control.BackgroundProperty, Brushes.Transparent),
            Set(Control.BorderThicknessProperty, new Thickness(0)),
            Set(Control.FocusVisualStyleProperty, null!));
        var hover = new MultiTrigger();
        hover.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
        hover.Conditions.Add(new Condition(DataGridRow.IsSelectedProperty, false));
        hover.Setters.Add(new Setter(Control.BackgroundProperty, brushes["Hover"]));
        style.Triggers.Add(hover);
        var inactive = new MultiTrigger();
        inactive.Conditions.Add(new Condition(DataGridRow.IsSelectedProperty, true));
        inactive.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
        inactive.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        inactive.Setters.Add(new Setter(Control.ForegroundProperty, brushes["Text"]));
        style.Triggers.Add(inactive);
        return style;
    }

    static Style TabStyle(Dictionary<string, Brush> brushes)
    {
        var template = ChromeTemplate(typeof(TabItem), header: true);
        template.Triggers.Add(ChromeTrigger(UIElement.IsMouseOverProperty, true, Border.BackgroundProperty, brushes["Hover"]));
        template.Triggers.Add(ChromeTrigger(TabItem.IsSelectedProperty, true, Border.BorderBrushProperty, brushes["Accent"]));
        var style = new Style(typeof(TabItem));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.ForegroundProperty, brushes["Secondary"]));
        style.Setters.Add(Set(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(Set(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 2)));
        style.Setters.Add(Set(Control.PaddingProperty, new Thickness(12, 8, 12, 6)));
        style.Setters.Add(Set(Control.MarginProperty, new Thickness(0, 0, 4, 0)));
        style.Setters.Add(Set(Control.FocusVisualStyleProperty, null!));
        style.Setters.Add(Set(Control.TemplateProperty, template));
        var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.ForegroundProperty, brushes["Text"]));
        selected.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Triggers.Add(selected);
        return style;
    }

    static ControlTemplate ChromeTemplate(Type type, bool header = false)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chrome";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(header ? 0 : 4));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        if (header)
            presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        border.AppendChild(presenter);
        return new ControlTemplate(type) { VisualTree = border };
    }

    static Trigger ChromeTrigger(DependencyProperty property, object value, DependencyProperty target, object targetValue)
    {
        var trigger = new Trigger { Property = property, Value = value };
        trigger.Setters.Add(new Setter(target, targetValue, "Chrome"));
        return trigger;
    }

    static Style Extend(Type type, params Setter[] setters)
    {
        var probe = (FrameworkElement)Activator.CreateInstance(type)!;
        var theme = (Style)probe.FindResource(type);
        var style = new Style(type) { BasedOn = theme };
        foreach (var setter in setters)
            style.Setters.Add(setter);
        return style;
    }

    static Style ScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(Set(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(Set(FrameworkElement.WidthProperty, 10.0));
        style.Setters.Add(Set(FrameworkElement.MinWidthProperty, 0.0));
        style.Setters.Add(Set(FrameworkElement.MinHeightProperty, 0.0));
        style.Setters.Add(Set(FrameworkElement.CursorProperty, Cursors.Arrow));
        style.Setters.Add(Set(Control.TemplateProperty, ScrollBarTemplate(vertical: true)));
        var horizontal = new Trigger { Property = ScrollBar.OrientationProperty, Value = Orientation.Horizontal };
        horizontal.Setters.Add(new Setter(FrameworkElement.WidthProperty, double.NaN));
        horizontal.Setters.Add(new Setter(FrameworkElement.HeightProperty, 10.0));
        horizontal.Setters.Add(new Setter(Control.TemplateProperty, ScrollBarTemplate(vertical: false)));
        style.Triggers.Add(horizontal);
        return style;
    }

    static ControlTemplate ScrollBarTemplate(bool vertical)
    {
        var decrease = vertical ? "ScrollBar.PageUpCommand" : "ScrollBar.PageLeftCommand";
        var increase = vertical ? "ScrollBar.PageDownCommand" : "ScrollBar.PageRightCommand";
        var reversed = vertical ? "True" : "False";
        var margin = vertical ? "2,0" : "0,2";
        return (ControlTemplate)XamlReader.Parse(
            $$"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollBar">
              <Grid Background="Transparent" SnapsToDevicePixels="True">
                <Track x:Name="PART_Track" IsDirectionReversed="{{reversed}}" Focusable="False">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Command="{{decrease}}" Focusable="False" IsTabStop="False">
                      <RepeatButton.Template>
                        <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
                      </RepeatButton.Template>
                    </RepeatButton>
                  </Track.DecreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb Margin="{{margin}}" Background="{DynamicResource ScrollThumb}" Cursor="Arrow">
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Border x:Name="Grip" Background="{TemplateBinding Background}" CornerRadius="3"/>
                          <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                              <Setter TargetName="Grip" Property="Background" Value="{DynamicResource ScrollThumbHover}"/>
                            </Trigger>
                            <Trigger Property="IsDragging" Value="True">
                              <Setter TargetName="Grip" Property="Background" Value="{DynamicResource ScrollThumbHover}"/>
                            </Trigger>
                          </ControlTemplate.Triggers>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Command="{{increase}}" Focusable="False" IsTabStop="False">
                      <RepeatButton.Template>
                        <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
                      </RepeatButton.Template>
                    </RepeatButton>
                  </Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
            """);
    }

    static ControlTemplate OverlayScrollTemplate(bool revealOnHover)
    {
        var appearance = revealOnHover
            ? "Opacity=\"0\" IsHitTestVisible=\"False\""
            : "Opacity=\"0.95\" IsHitTestVisible=\"True\"";
        var triggers = revealOnHover
            ? """
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter TargetName="PART_VerticalScrollBar" Property="Opacity" Value="1"/>
                  <Setter TargetName="PART_HorizontalScrollBar" Property="Opacity" Value="1"/>
                  <Setter TargetName="PART_VerticalScrollBar" Property="IsHitTestVisible" Value="True"/>
                  <Setter TargetName="PART_HorizontalScrollBar" Property="IsHitTestVisible" Value="True"/>
                </Trigger>
                <Trigger Property="IsMouseCaptureWithin" Value="True">
                  <Setter TargetName="PART_VerticalScrollBar" Property="Opacity" Value="1"/>
                  <Setter TargetName="PART_HorizontalScrollBar" Property="Opacity" Value="1"/>
                  <Setter TargetName="PART_VerticalScrollBar" Property="IsHitTestVisible" Value="True"/>
                  <Setter TargetName="PART_HorizontalScrollBar" Property="IsHitTestVisible" Value="True"/>
                </Trigger>
              </ControlTemplate.Triggers>
              """
            : "";
        return (ControlTemplate)XamlReader.Parse(
            $$"""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollViewer">
              <Grid>
                <ScrollContentPresenter x:Name="PART_ScrollContentPresenter"
                                        CanContentScroll="{TemplateBinding CanContentScroll}"
                                        Content="{TemplateBinding Content}"
                                        ContentTemplate="{TemplateBinding ContentTemplate}"/>
                <ScrollBar x:Name="PART_VerticalScrollBar"
                           Orientation="Vertical"
                           HorizontalAlignment="Right"
                           Margin="0,2,1,2"
                           Cursor="Arrow"
                           Minimum="0"
                           Maximum="{TemplateBinding ScrollableHeight}"
                           ViewportSize="{TemplateBinding ViewportHeight}"
                           Value="{TemplateBinding VerticalOffset}"
                           Visibility="{TemplateBinding ComputedVerticalScrollBarVisibility}"
                           {{appearance}}/>
                <ScrollBar x:Name="PART_HorizontalScrollBar"
                           Orientation="Horizontal"
                           VerticalAlignment="Bottom"
                           Margin="2,0,2,1"
                           Cursor="Arrow"
                           Minimum="0"
                           Maximum="{TemplateBinding ScrollableWidth}"
                           ViewportSize="{TemplateBinding ViewportWidth}"
                           Value="{TemplateBinding HorizontalOffset}"
                           Visibility="{TemplateBinding ComputedHorizontalScrollBarVisibility}"
                           {{appearance}}/>
              </Grid>
              {{triggers}}
            </ControlTemplate>
            """);
    }

    static Setter Set(DependencyProperty property, object value) => new(property, value);

    static bool UseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    static Color AccentColor()
    {
        var color = SystemParameters.WindowGlassColor;
        if (color.A == 0)
            return Color.FromRgb(0, 120, 212);

        return Color.FromRgb(color.R, color.G, color.B);
    }

    static bool IsLight(Color color) => color.R * 0.299 + color.G * 0.587 + color.B * 0.114 > 160;

    static Color Mix(Color from, Color to, double amount)
    {
        byte Channel(byte start, byte end) => (byte)Math.Round(start + (end - start) * amount);
        return Color.FromRgb(Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
    }

    static SolidColorBrush Paint(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    sealed class AddOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int index ? (index + 1).ToString() : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    sealed class OneLineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var line = string.Join(' ', (value as string ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
            return line.Length == 0 ? "No command recorded" : line;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    sealed class CountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var action = parameter as string ?? "";
            var count = value is System.Collections.IEnumerable items
                ? items.OfType<Change>().Count(change => change.Action == action)
                : 0;
            return count.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
