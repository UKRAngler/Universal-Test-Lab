using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using Microsoft.Win32;

namespace UniversalTestLab
{
    internal sealed class AircraftView
    {
        public Aircraft Source { get; private set; }
        public string Name { get { return Source.Display; } }
        public string Meta { get { return Source.Nation + "  •  RANK " + Roman(Source.Rank) + "  •  " + Source.Kind.ToUpperInvariant(); } }
        public string Nation { get { return Source.Nation; } }
        public string Kind { get { return Source.Kind; } }
        public int Rank { get { return Source.Rank; } }

        public AircraftView(Aircraft source) { Source = source; }
        public override string ToString() { return Name; }

        private static string Roman(int rank)
        {
            string[] values = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return rank >= 0 && rank < values.Length ? values[rank] : rank.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class TargetView
    {
        public TargetUnit Source { get; private set; }
        public string Name { get { return Source.Display; } }
        public string Nation { get { return Source.Nation; } }
        public int Rank { get { return Source.Rank; } }
        public TargetView(TargetUnit source) { Source = source; }
        public override string ToString() { return Name; }
    }

    internal sealed class WeaponView
    {
        public DonorWeapon Source { get; private set; }
        public string Name { get { return Source.Name; } }
        public string Category { get { return Source.Category; } }
        public string Ammo { get { return Source.Bullets.ToString(CultureInfo.InvariantCulture); } }
        public string Mass { get { return Source.TotalMass.ToString("0.0", CultureInfo.InvariantCulture) + " kg"; } }
        public string Mode { get; private set; }
        public WeaponView(DonorWeapon source, bool injected) { Source = source; Mode = injected ? "INJECTED" : "NATIVE"; }
    }

    internal static class ModernPalette
    {
        public const string Window = "#29354D";
        public const string Surface = "#80505B74";
        public const string SurfaceSolid = "#3B4862";
        public const string Field = "#B81B2740";
        public const string Border = "#58759F";
        public const string Text = "#F3F6FF";
        public const string Muted = "#9EACCE";
        public const string Accent = "#6C63FF";
        public const string AccentDark = "#4A55CC";
        public const string Cyan = "#4BD5FF";
        public const string Good = "#48DEB3";
        public const string Danger = "#FF5B8B";

        public static Brush Brush(string value)
        {
            return (Brush)new BrushConverter().ConvertFromString(value);
        }
    }

    internal static class ModernComboSizing
    {
        public static void Attach(DependencyObject root)
        {
            if (root == null) return;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                ComboBox combo = child as ComboBox;
                if (combo != null)
                {
                    combo.DropDownOpened -= Fit;
                    combo.DropDownOpened += Fit;
                }
                Attach(child);
            }
        }

        private static void Fit(object sender, EventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo == null) return;
            combo.UpdateLayout();
            double contentHeight = 12;
            int measured = 0;
            for (int index = 0; index < combo.Items.Count; index++)
            {
                FrameworkElement item = combo.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                if (item == null || item.ActualHeight <= 0) continue;
                contentHeight += item.ActualHeight;
                measured++;
            }
            if (measured < combo.Items.Count) contentHeight += (combo.Items.Count - measured) * (measured > 0 ? (contentHeight - 12) / measured : 31);
            combo.MaxDropDownHeight = Math.Min(320, Math.Max(34, Math.Ceiling(contentHeight)));
        }
    }

    internal static class DwmGlass
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Margins { public int Left; public int Right; public int Top; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public NativePoint Reserved;
            public NativePoint MaxSize;
            public NativePoint MaxPosition;
            public NativePoint MinTrackSize;
            public NativePoint MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public int Flags;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        public static void EnablePerMonitorDpi()
        {
            try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); }
            catch { try { SetProcessDPIAware(); } catch { } }
        }

        public static void Apply(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                HwndSource source = HwndSource.FromHwnd(handle);
                if (source != null) source.AddHook(WindowProc);

                // Keep DWM's backdrop without extending the non-client glass over the
                // first client rows. Full-sheet glass masked roughly 22 px below the
                // custom caption on some DPI/scaling combinations.
                Margins margins = new Margins { Left = 0, Right = 0, Top = 0, Bottom = 0 };
                DwmExtendFrameIntoClientArea(handle, ref margins);
                // Windows' transient Acrylic policy recolours the entire HWND and changes
                // dramatically when a dialog deactivates its owner. Mica keeps the DWM
                // blur but leaves our neutral glass palette stable in both focus states.
                int backdrop = 2;
                DwmSetWindowAttribute(handle, 38, ref backdrop, 4);
                int corner = 2;
                DwmSetWindowAttribute(handle, 33, ref corner, 4);
                int dark = 1;
                DwmSetWindowAttribute(handle, 20, ref dark, 4);
            }
            catch { }
        }

        private static IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WmGetMinMaxInfo = 0x0024;
            const uint MonitorDefaultToNearest = 2;
            if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                if (monitor == IntPtr.Zero) return IntPtr.Zero;
                MonitorInfo monitorInfo = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (!GetMonitorInfo(monitor, ref monitorInfo)) return IntPtr.Zero;
                MinMaxInfo minMax = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
                minMax.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
                minMax.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
                minMax.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
                minMax.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
                minMax.MaxTrackSize = minMax.MaxSize;
                Marshal.StructureToPtr(minMax, lParam, true);
                handled = true;
            }
            catch { }
            return IntPtr.Zero;
        }
    }

    internal static class ModernXaml
    {
        public static object Parse(string xaml) { return XamlReader.Parse(xaml); }

        public const string Main = @"
<Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
      xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
      x:Name=""Root"" Background=""#B329354D"">
  <Grid.Resources>
    <SolidColorBrush x:Key=""TextBrush"" Color=""#F3F6FF""/>
    <SolidColorBrush x:Key=""MutedBrush"" Color=""#9EACCE""/>
    <SolidColorBrush x:Key=""AccentBrush"" Color=""#6C63FF""/>
    <SolidColorBrush x:Key=""AccentDarkBrush"" Color=""#4A55CC""/>
    <SolidColorBrush x:Key=""CyanBrush"" Color=""#4BD5FF""/>
    <SolidColorBrush x:Key=""Good"" Color=""#48DEB3""/>
    <SolidColorBrush x:Key=""Danger"" Color=""#FF5B8B""/>
    <SolidColorBrush x:Key=""FieldBrush"" Color=""#B81B2740""/>
    <SolidColorBrush x:Key=""SurfaceBrush"" Color=""#80505B74""/>
    <SolidColorBrush x:Key=""BorderBrush"" Color=""#58759F""/>

    <Style TargetType=""TextBlock"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/>
      <Setter Property=""FontFamily"" Value=""Segoe UI""/>
    </Style>
    <Style x:Key=""Caption"" TargetType=""TextBlock"">
      <Setter Property=""Foreground"" Value=""{StaticResource MutedBrush}""/>
      <Setter Property=""FontSize"" Value=""11""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
    </Style>
    <Style x:Key=""GlassCard"" TargetType=""Border"">
      <Setter Property=""Background"" Value=""{StaticResource SurfaceBrush}""/>
      <Setter Property=""BorderBrush"" Value=""{StaticResource BorderBrush}""/>
      <Setter Property=""BorderThickness"" Value=""1""/>
      <Setter Property=""CornerRadius"" Value=""20""/>
      <Setter Property=""Padding"" Value=""16""/>
    </Style>
    <Style x:Key=""ButtonStyle"" TargetType=""Button"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/>
      <Setter Property=""Background"" Value=""#24365F""/>
      <Setter Property=""BorderBrush"" Value=""{StaticResource BorderBrush}""/>
      <Setter Property=""BorderThickness"" Value=""1""/>
      <Setter Property=""Padding"" Value=""14,8""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""10"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#304A78""/></Trigger>
              <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#17294B""/></Trigger>
              <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Opacity"" Value=""0.42""/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key=""PrimaryButton"" TargetType=""Button"" BasedOn=""{StaticResource ButtonStyle}"">
      <Setter Property=""Background"" Value=""{StaticResource AccentDarkBrush}""/>
      <Setter Property=""BorderThickness"" Value=""0""/>
      <Setter Property=""FontSize"" Value=""13""/>
    </Style>
    <Style x:Key=""ChromeButton"" TargetType=""Button"" BasedOn=""{StaticResource ButtonStyle}"">
      <Setter Property=""Width"" Value=""46""/><Setter Property=""Height"" Value=""36""/>
      <Setter Property=""Padding"" Value=""0""/><Setter Property=""Background"" Value=""Transparent""/><Setter Property=""BorderThickness"" Value=""0""/>
      <Setter Property=""FontSize"" Value=""14""/>
    </Style>
    <Style TargetType=""TextBox"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/>
      <Setter Property=""Background"" Value=""{StaticResource FieldBrush}""/>
      <Setter Property=""BorderBrush"" Value=""{StaticResource BorderBrush}""/>
      <Setter Property=""BorderThickness"" Value=""1""/>
      <Setter Property=""Padding"" Value=""10,7""/>
      <Setter Property=""CaretBrush"" Value=""{StaticResource CyanBrush}""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""TextBox"">
            <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""8"">
              <ScrollViewer x:Name=""PART_ContentHost"" Margin=""{TemplateBinding Padding}""/>
            </Border>
            <ControlTemplate.Triggers><Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""bd"" Property=""BorderBrush"" Value=""{StaticResource CyanBrush}""/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key=""ComboItemStyle"" TargetType=""ComboBoxItem"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""Background"" Value=""{StaticResource FieldBrush}""/>
      <Setter Property=""Padding"" Value=""10,8""/><Setter Property=""HorizontalContentAlignment"" Value=""Stretch""/>
      <Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ComboBoxItem""><Border x:Name=""bd"" Background=""{TemplateBinding Background}"" Padding=""{TemplateBinding Padding}"" CornerRadius=""6""><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property=""IsHighlighted"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#4A55CC""/></Trigger><Trigger Property=""IsSelected"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#2D4673""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType=""ComboBox"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""Background"" Value=""{StaticResource FieldBrush}""/>
      <Setter Property=""BorderBrush"" Value=""{StaticResource BorderBrush}""/><Setter Property=""BorderThickness"" Value=""1""/>
      <Setter Property=""Padding"" Value=""10,7""/><Setter Property=""ItemContainerStyle"" Value=""{StaticResource ComboItemStyle}""/>
      <Setter Property=""MaxDropDownHeight"" Value=""360""/>
      <Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ComboBox""><Grid><ToggleButton x:Name=""toggle"" Focusable=""False"" IsChecked=""{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"" Background=""Transparent"" Foreground=""{TemplateBinding Foreground}"" BorderThickness=""0"" HorizontalContentAlignment=""Stretch"" VerticalContentAlignment=""Stretch""><ToggleButton.Template><ControlTemplate TargetType=""ToggleButton""><ContentPresenter HorizontalAlignment=""Stretch"" VerticalAlignment=""Stretch""/></ControlTemplate></ToggleButton.Template><Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""8""><Grid><ContentPresenter Margin=""10,7,34,7"" VerticalAlignment=""Center"" HorizontalAlignment=""Left"" Content=""{TemplateBinding SelectionBoxItem}"" ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}"" TextElement.Foreground=""{TemplateBinding Foreground}""/><Path Data=""M 0 0 L 5 5 L 10 0 Z"" Fill=""#9EACCE"" HorizontalAlignment=""Right"" VerticalAlignment=""Center"" Margin=""0,0,10,0""/></Grid></Border></ToggleButton><Popup x:Name=""PART_Popup"" IsOpen=""{TemplateBinding IsDropDownOpen}"" Placement=""Bottom"" AllowsTransparency=""True"" Focusable=""False"" PopupAnimation=""Fade""><Border Background=""#0B1632"" BorderBrush=""#4D6D9F"" BorderThickness=""1"" CornerRadius=""10"" Padding=""5"" MinWidth=""{Binding ActualWidth, ElementName=toggle}"" MaxHeight=""{TemplateBinding MaxDropDownHeight}""><ScrollViewer VerticalScrollBarVisibility=""Auto"" CanContentScroll=""True""><ItemsPresenter/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property=""IsKeyboardFocusWithin"" Value=""True""><Setter TargetName=""bd"" Property=""BorderBrush"" Value=""{StaticResource CyanBrush}""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType=""ListBox"">
      <Setter Property=""Background"" Value=""{StaticResource FieldBrush}""/><Setter Property=""BorderThickness"" Value=""0""/>
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""ScrollViewer.HorizontalScrollBarVisibility"" Value=""Disabled""/>
    </Style>
    <Style TargetType=""ListBoxItem"">
      <Setter Property=""Padding"" Value=""10,7""/><Setter Property=""HorizontalContentAlignment"" Value=""Stretch""/><Setter Property=""Background"" Value=""Transparent""/>
      <Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ListBoxItem""><Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"" Margin=""3,2""><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#24365F""/></Trigger><Trigger Property=""IsSelected"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#4A55CC""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType=""ListViewItem"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""Background"" Value=""Transparent""/><Setter Property=""Padding"" Value=""6,7""/><Setter Property=""HorizontalContentAlignment"" Value=""Stretch""/>
      <Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ListViewItem""><Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""6"" Padding=""{TemplateBinding Padding}""><GridViewRowPresenter Content=""{TemplateBinding Content}"" Columns=""{Binding View.Columns, RelativeSource={RelativeSource AncestorType=ListView}}""/></Border><ControlTemplate.Triggers><Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#24365F""/></Trigger><Trigger Property=""IsSelected"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#4A55CC""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType=""GridViewColumnHeader""><Setter Property=""Background"" Value=""#1D315C""/><Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""BorderBrush"" Value=""#A8C7ECFF""/><Setter Property=""Padding"" Value=""8,7""/><Setter Property=""FontWeight"" Value=""SemiBold""/><Setter Property=""HorizontalContentAlignment"" Value=""Center""/><Setter Property=""Focusable"" Value=""False""/><Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""GridViewColumnHeader""><Border x:Name=""HeaderBorder"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""0,0,1,1"" Padding=""{TemplateBinding Padding}""><ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/></Border></ControlTemplate></Setter.Value></Setter></Style>
    <Style x:Key=""LastGridHeader"" TargetType=""GridViewColumnHeader"" BasedOn=""{StaticResource {x:Type GridViewColumnHeader}}""><Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""GridViewColumnHeader""><Border Background=""{TemplateBinding Background}"" BorderBrush=""#A8C7ECFF"" BorderThickness=""0,0,1,1"" CornerRadius=""0,11,11,0"" Padding=""{TemplateBinding Padding}""><ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/></Border></ControlTemplate></Setter.Value></Setter></Style>
    <Style TargetType=""ScrollBar"">
      <Setter Property=""Background"" Value=""Transparent""/><Setter Property=""Width"" Value=""8""/><Setter Property=""Height"" Value=""8""/>
      <Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ScrollBar""><Grid Background=""Transparent""><Track x:Name=""PART_Track"" Orientation=""{TemplateBinding Orientation}"" Minimum=""{TemplateBinding Minimum}"" Maximum=""{TemplateBinding Maximum}"" Value=""{TemplateBinding Value}"" ViewportSize=""{TemplateBinding ViewportSize}"" IsDirectionReversed=""False""><Track.DecreaseRepeatButton><RepeatButton x:Name=""dec"" Command=""{x:Static ScrollBar.PageUpCommand}"" Opacity=""0""/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType=""Thumb""><Border Background=""#4D6D9F"" CornerRadius=""4"" Margin=""1""/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton x:Name=""inc"" Command=""{x:Static ScrollBar.PageDownCommand}"" Opacity=""0""/></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property=""Orientation"" Value=""Horizontal""><Setter Property=""Width"" Value=""Auto""/><Setter Property=""Height"" Value=""8""/><Setter TargetName=""PART_Track"" Property=""IsDirectionReversed"" Value=""False""/><Setter TargetName=""dec"" Property=""Command"" Value=""{x:Static ScrollBar.PageLeftCommand}""/><Setter TargetName=""inc"" Property=""Command"" Value=""{x:Static ScrollBar.PageRightCommand}""/></Trigger><Trigger Property=""Orientation"" Value=""Vertical""><Setter Property=""Width"" Value=""8""/><Setter Property=""Height"" Value=""Auto""/><Setter TargetName=""PART_Track"" Property=""IsDirectionReversed"" Value=""True""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key=""ToggleStyle"" TargetType=""ToggleButton"">
      <Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""Background"" Value=""#24365F""/><Setter Property=""BorderBrush"" Value=""{StaticResource BorderBrush}""/><Setter Property=""BorderThickness"" Value=""1""/><Setter Property=""Padding"" Value=""12,8""/><Setter Property=""FontWeight"" Value=""SemiBold""/><Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ToggleButton""><Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""10""><ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/></Border><ControlTemplate.Triggers><Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""#304A78""/></Trigger><Trigger Property=""IsChecked"" Value=""True""><Setter TargetName=""bd"" Property=""Background"" Value=""{StaticResource AccentDarkBrush}""/><Setter TargetName=""bd"" Property=""BorderBrush"" Value=""{StaticResource CyanBrush}""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key=""StatusToggleStyle"" TargetType=""ToggleButton""><Setter Property=""Foreground"" Value=""{StaticResource TextBrush}""/><Setter Property=""Background"" Value=""#24365F""/><Setter Property=""BorderBrush"" Value=""{StaticResource BorderBrush}""/><Setter Property=""BorderThickness"" Value=""1""/><Setter Property=""Padding"" Value=""12,8""/><Setter Property=""FontWeight"" Value=""SemiBold""/><Setter Property=""Cursor"" Value=""Hand""/><Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""ToggleButton""><Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""10""><ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{TemplateBinding Padding}""/></Border><ControlTemplate.Triggers><Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bd"" Property=""Opacity"" Value=""0.86""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  </Grid.Resources>

  <Grid.RowDefinitions><RowDefinition Height=""38""/><RowDefinition Height=""*""/></Grid.RowDefinitions>
  <Border x:Name=""TitleBar"" Grid.Row=""0"" Background=""#FF35415E"" BorderBrush=""#664BD5FF"" BorderThickness=""0,0,0,1"">
    <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
      <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""14,0""><Border Width=""20"" Height=""20"" CornerRadius=""6"" Background=""#4A55CC"" Margin=""0,0,9,0""><TextBlock Text=""U"" FontWeight=""Bold"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" FontSize=""11""/></Border><TextBlock Text=""Universal Test Lab"" FontWeight=""SemiBold"" VerticalAlignment=""Center""/><TextBlock Text=""  /  Mission Studio"" Foreground=""#9EACCE"" VerticalAlignment=""Center""/></StackPanel>
      <StackPanel Grid.Column=""1"" Orientation=""Horizontal""><Button x:Name=""MinimizeButton"" Style=""{StaticResource ChromeButton}"" Content=""—""/><Button x:Name=""MaximizeButton"" Style=""{StaticResource ChromeButton}"" Content=""□""/><Button x:Name=""CloseButton"" Style=""{StaticResource ChromeButton}"" Content=""×""/></StackPanel>
    </Grid>
  </Border>

  <Grid Grid.Row=""1"" Margin=""0""><Grid.RowDefinitions><RowDefinition Height=""64""/><RowDefinition Height=""*""/><RowDefinition Height=""28""/></Grid.RowDefinitions>
    <Border Style=""{StaticResource GlassCard}"" Padding=""22,4"" Margin=""0"" CornerRadius=""0"" BorderThickness=""0,0,0,1""><Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""270""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
      <StackPanel VerticalAlignment=""Center""><TextBlock Text=""U.T.L. by AstraSEP"" FontSize=""20"" FontWeight=""SemiBold""/><TextBlock Text=""AIR &amp; GROUND VEHICLE TEST WORKSPACE"" Foreground=""{StaticResource CyanBrush}"" FontSize=""10"" FontWeight=""SemiBold""/></StackPanel>
      <StackPanel Grid.Column=""1"" Margin=""10,0,12,0"" VerticalAlignment=""Center""><TextBlock Text=""GAME DIRECTORY"" Style=""{StaticResource Caption}"" Margin=""2,0,0,3""/><TextBox x:Name=""GameFolderBox"" Height=""30"" Padding=""10,3"" Margin=""0"" VerticalContentAlignment=""Center""/></StackPanel>
      <StackPanel Grid.Column=""2"" Orientation=""Horizontal"" VerticalAlignment=""Center""><Button x:Name=""BrowseButton"" Style=""{StaticResource ButtonStyle}"" Content=""BROWSE"" Margin=""4,0""/><Button x:Name=""SyncButton"" Style=""{StaticResource ButtonStyle}"" Content=""SYNC BASE"" Margin=""4,0""/><Button x:Name=""MissionsButton"" Style=""{StaticResource ButtonStyle}"" Content=""MISSIONS"" Margin=""4,0""/><Button x:Name=""PresetsButton"" Style=""{StaticResource ButtonStyle}"" Content=""PRESETS"" Margin=""4,0""/><Button x:Name=""AboutButton"" Style=""{StaticResource ButtonStyle}"" Content=""SUPPORT"" Margin=""4,0,0,0""/></StackPanel>
    </Grid></Border>

    <Grid Grid.Row=""1"" Margin=""12,10,12,10""><Grid.ColumnDefinitions><ColumnDefinition Width=""330""/><ColumnDefinition Width=""12""/><ColumnDefinition Width=""*"" MinWidth=""500""/><ColumnDefinition Width=""12""/><ColumnDefinition Width=""330""/></Grid.ColumnDefinitions>
      <Border Grid.Column=""0"" Style=""{StaticResource GlassCard}""><Grid><Grid.RowDefinitions><RowDefinition Height=""58""/><RowDefinition Height=""Auto""/><RowDefinition Height=""Auto""/><RowDefinition Height=""Auto""/><RowDefinition Height=""*""/></Grid.RowDefinitions>
        <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""48""/><ColumnDefinition Width=""*""/></Grid.ColumnDefinitions><Border Width=""44"" Height=""44"" CornerRadius=""13"" Background=""{StaticResource AccentDarkBrush}""><TextBlock Text=""01"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" FontWeight=""Bold""/></Border><StackPanel Grid.Column=""1"" Margin=""10,2,0,0""><TextBlock Text=""CHOOSE VEHICLE"" FontSize=""16"" FontWeight=""SemiBold""/><TextBlock Text=""Air and ground vehicles"" Foreground=""{StaticResource MutedBrush}"" FontSize=""11""/></StackPanel></Grid>
        <StackPanel Grid.Row=""1"" Margin=""0,8,0,10""><TextBlock Text=""SEARCH"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><TextBox x:Name=""AircraftSearch""/></StackPanel>
        <Grid Grid.Row=""2"" Margin=""0,0,0,10""><Grid.ColumnDefinitions><ColumnDefinition Width=""1.25*""/><ColumnDefinition Width=""1.2*""/><ColumnDefinition Width=""1*""/></Grid.ColumnDefinitions><StackPanel Margin=""0,0,5,0""><TextBlock Text=""NATION"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ComboBox x:Name=""NationFilter""/></StackPanel><StackPanel Grid.Column=""1"" Margin=""5,0""><TextBlock Text=""RANK"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ComboBox x:Name=""RankFilter""/></StackPanel><StackPanel Grid.Column=""2"" Margin=""5,0,0,0""><TextBlock Text=""TYPE"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ComboBox x:Name=""TypeFilter""/></StackPanel></Grid>
        <StackPanel Grid.Row=""3"" Margin=""2,0,0,8""><TextBlock Text=""AVAILABLE VEHICLES"" Style=""{StaticResource Caption}""/><TextBlock x:Name=""VehicleCountText"" Foreground=""{StaticResource CyanBrush}"" FontSize=""11"" Margin=""0,4,0,0""/></StackPanel>
        <Border Grid.Row=""4"" Background=""{StaticResource FieldBrush}"" CornerRadius=""12"" Padding=""3""><ListBox x:Name=""AircraftList""><ListBox.ItemTemplate><DataTemplate><StackPanel><TextBlock Text=""{Binding Name}"" FontWeight=""SemiBold"" TextTrimming=""CharacterEllipsis""/><TextBlock Text=""{Binding Meta}"" Foreground=""#AEB9D8"" FontSize=""10"" Margin=""0,2,0,0"" TextTrimming=""CharacterEllipsis""/></StackPanel></DataTemplate></ListBox.ItemTemplate></ListBox></Border>
      </Grid></Border>

      <Border Grid.Column=""2"" Style=""{StaticResource GlassCard}""><Grid><Grid.RowDefinitions><RowDefinition Height=""58""/><RowDefinition Height=""32""/><RowDefinition Height=""86""/><RowDefinition Height=""72""/><RowDefinition Height=""*""/><RowDefinition Height=""48""/></Grid.RowDefinitions>
        <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""48""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions><Border Width=""44"" Height=""44"" CornerRadius=""13"" Background=""{StaticResource AccentDarkBrush}""><TextBlock Text=""02"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" FontWeight=""Bold""/></Border><StackPanel Grid.Column=""1"" Margin=""10,2,0,0""><TextBlock x:Name=""BuildTitle"" Text=""BUILD LOADOUT"" FontSize=""16"" FontWeight=""SemiBold""/><TextBlock x:Name=""BuildSubtitle"" Text=""Select a station, then mount a weapon"" Foreground=""{StaticResource MutedBrush}"" FontSize=""11""/></StackPanel><TextBlock x:Name=""MassText"" Grid.Column=""2"" Foreground=""{StaticResource CyanBrush}"" FontWeight=""SemiBold"" VerticalAlignment=""Center""/></Grid>
        <TextBlock x:Name=""StationText"" Grid.Row=""1"" Foreground=""{StaticResource MutedBrush}"" VerticalAlignment=""Center"" TextTrimming=""CharacterEllipsis""/>
        <Border x:Name=""PylonCard"" Grid.Row=""2"" Background=""{StaticResource FieldBrush}"" CornerRadius=""12"" Padding=""5"" Margin=""0,2,0,8""><UniformGrid x:Name=""PylonPanel"" Rows=""1"" VerticalAlignment=""Center""/></Border>
        <Grid x:Name=""WeaponFilterPanel"" Grid.Row=""3""><Grid.ColumnDefinitions><ColumnDefinition Width=""175""/><ColumnDefinition Width=""*""/><ColumnDefinition Width=""155""/><ColumnDefinition Width=""125""/><ColumnDefinition Width=""145""/></Grid.ColumnDefinitions><StackPanel Margin=""0,0,5,0""><TextBlock Text=""WEAPON SOURCE"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ToggleButton x:Name=""InjectionToggle"" Style=""{StaticResource ToggleStyle}"" Content=""INJECT ANY WEAPON""/></StackPanel><StackPanel Grid.Column=""1"" Margin=""5,0""><TextBlock Text=""SEARCH"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><TextBox x:Name=""WeaponSearch""/></StackPanel><StackPanel Grid.Column=""2"" Margin=""5,0""><TextBlock Text=""WEAPON TYPE"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ComboBox x:Name=""CategoryFilter""/></StackPanel><StackPanel Grid.Column=""3"" Margin=""5,0""><TextBlock Text=""NATION"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ComboBox x:Name=""WeaponNationFilter""/></StackPanel><StackPanel Grid.Column=""4"" Margin=""5,0,0,0""><TextBlock Text=""SORT"" Style=""{StaticResource Caption}"" Margin=""2,0,0,5""/><ComboBox x:Name=""SortFilter""/></StackPanel></Grid>
        <Grid x:Name=""WeaponTableFrame"" Grid.Row=""4"" Margin=""0,10,0,10""><Border Background=""{StaticResource FieldBrush}"" CornerRadius=""12""/><Grid x:Name=""WeaponTableClipContent""><ListView x:Name=""WeaponList"" Background=""Transparent"" BorderThickness=""0"" Foreground=""{StaticResource TextBrush}"" ScrollViewer.HorizontalScrollBarVisibility=""Disabled"" ScrollViewer.CanContentScroll=""True"" VirtualizingStackPanel.IsVirtualizing=""True"" VirtualizingStackPanel.VirtualizationMode=""Recycling""><ListView.Resources><Style TargetType=""ScrollBar"" BasedOn=""{StaticResource {x:Type ScrollBar}}""><Style.Triggers><Trigger Property=""Orientation"" Value=""Vertical""><Setter Property=""Margin"" Value=""0,32,0,1""/></Trigger></Style.Triggers></Style></ListView.Resources><ListView.View><GridView><GridViewColumn Header=""Weapon"" Width=""330"" DisplayMemberBinding=""{Binding Name}""/><GridViewColumn Header=""Type"" Width=""185"" DisplayMemberBinding=""{Binding Category}""/><GridViewColumn Header=""Ammo"" Width=""70"" DisplayMemberBinding=""{Binding Ammo}""/><GridViewColumn Header=""Mass"" Width=""85"" DisplayMemberBinding=""{Binding Mass}""/><GridViewColumn Width=""82""><GridViewColumn.Header><GridViewColumnHeader Content=""Mode""/></GridViewColumn.Header><GridViewColumn.CellTemplate><DataTemplate><TextBlock Text=""{Binding Mode}"" HorizontalAlignment=""Center"" TextAlignment=""Center""/></DataTemplate></GridViewColumn.CellTemplate></GridViewColumn></GridView></ListView.View></ListView></Grid><Border BorderBrush=""#A8C7ECFF"" BorderThickness=""1"" CornerRadius=""12"" IsHitTestVisible=""False""/></Grid>
        <Grid Grid.Row=""5""><Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""145""/><ColumnDefinition Width=""128""/><ColumnDefinition Width=""94""/><ColumnDefinition Width=""145""/></Grid.ColumnDefinitions><TextBlock Text=""Tip: double-click a weapon to mount it"" Foreground=""{StaticResource MutedBrush}"" VerticalAlignment=""Center""/><Button x:Name=""SystemsButton"" Grid.Column=""1"" Style=""{StaticResource ButtonStyle}"" Content=""MODULES"" Margin=""4,0""/><Button x:Name=""ClearStationButton"" Grid.Column=""2"" Style=""{StaticResource ButtonStyle}"" Content=""CLEAR STATION"" Margin=""4,0""/><Button x:Name=""ClearAllButton"" Grid.Column=""3"" Style=""{StaticResource ButtonStyle}"" Content=""CLEAR ALL"" Margin=""4,0""/><Button x:Name=""MountButton"" Grid.Column=""4"" Style=""{StaticResource PrimaryButton}"" Content=""MOUNT WEAPON"" Margin=""4,0,0,0""/></Grid>
      </Grid></Border>

      <Border Grid.Column=""4"" Style=""{StaticResource GlassCard}""><Grid><Grid.RowDefinitions><RowDefinition Height=""58""/><RowDefinition Height=""150""/><RowDefinition Height=""34""/><RowDefinition Height=""48""/><RowDefinition Height=""48""/><RowDefinition Height=""*""/><RowDefinition Height=""56""/><RowDefinition Height=""26""/></Grid.RowDefinitions>
        <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""48""/><ColumnDefinition Width=""*""/></Grid.ColumnDefinitions><Border Width=""44"" Height=""44"" CornerRadius=""13"" Background=""{StaticResource AccentDarkBrush}""><TextBlock Text=""03"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" FontWeight=""Bold""/></Border><StackPanel Grid.Column=""1"" Margin=""10,2,0,0""><TextBlock Text=""CONFIGURE TEST"" FontSize=""16"" FontWeight=""SemiBold""/><TextBlock Text=""Flight, targets and launch profile"" Foreground=""{StaticResource MutedBrush}"" FontSize=""11""/></StackPanel></Grid>
        <Border x:Name=""PreviewCard"" Grid.Row=""1"" CornerRadius=""15"" BorderBrush=""#78A7DFFF"" BorderThickness=""1"" Background=""#7A1D315C""><Grid x:Name=""PreviewClipContent""><Ellipse Width=""155"" Height=""105"" Fill=""#284BD5FF"" VerticalAlignment=""Top"" Margin=""0,12,0,0""/><Grid x:Name=""PreviewAircraftVisual""><Image x:Name=""PreviewAircraftImage"" Width=""220"" Height=""112"" Stretch=""Uniform"" Opacity=""0.92"" VerticalAlignment=""Top"" Margin=""0,4,0,0""/></Grid><Grid x:Name=""PreviewHelicopterVisual"" Visibility=""Collapsed""><Image x:Name=""PreviewHelicopterImage"" Width=""270"" Height=""108"" Stretch=""Uniform"" Opacity=""0.94"" VerticalAlignment=""Top"" Margin=""0,5,0,0""/></Grid><Grid x:Name=""PreviewDroneVisual"" Visibility=""Collapsed""><Image x:Name=""PreviewDroneImage"" Width=""270"" Height=""108"" Stretch=""Uniform"" Opacity=""0.94"" VerticalAlignment=""Top"" Margin=""0,5,0,0""/></Grid><Border VerticalAlignment=""Bottom"" Background=""#900A142E"" Padding=""12,10""><StackPanel><TextBlock x:Name=""PreviewName"" FontSize=""15"" FontWeight=""SemiBold"" TextTrimming=""CharacterEllipsis""/><TextBlock x:Name=""PreviewMeta"" Foreground=""{StaticResource MutedBrush}"" FontSize=""10"" Margin=""0,3,0,0"" TextTrimming=""CharacterEllipsis""/></StackPanel></Border></Grid></Border>
        <TextBlock Grid.Row=""2"" Text=""MISSION SETUP"" FontSize=""14"" FontWeight=""SemiBold"" VerticalAlignment=""Bottom""/>
        <Button x:Name=""FlightConfigureButton"" Grid.Row=""3"" Style=""{StaticResource ButtonStyle}"" Content=""FLIGHT CONFIGURE"" Margin=""0,7,0,0""/>
        <Button x:Name=""MapButton"" Grid.Row=""4"" Style=""{StaticResource ButtonStyle}"" Content=""MAP &amp; SCENARIO"" Margin=""0,7,0,0""/>
        <StackPanel Grid.Row=""5"" Margin=""2,16,2,8""><TextBlock Text=""FLIGHT PROFILE"" Style=""{StaticResource Caption}""/><TextBlock x:Name=""FlightProfileText"" Foreground=""{StaticResource MutedBrush}"" FontSize=""11"" TextWrapping=""Wrap"" Margin=""0,3,0,0""/><TextBlock Text=""MAP PROFILE"" Style=""{StaticResource Caption}"" Margin=""0,14,0,0""/><TextBlock x:Name=""TargetSummaryText"" Foreground=""{StaticResource MutedBrush}"" FontSize=""11"" TextWrapping=""Wrap"" Margin=""0,3,0,0""/><TextBlock Text=""Aircraft/helicopters: reopen User Missions. Ground vehicle changes: restart War Thunder once."" Foreground=""{StaticResource Good}"" FontSize=""11"" TextWrapping=""Wrap"" Margin=""0,14,0,0""/></StackPanel>
        <Grid Visibility=""Collapsed""><ComboBox x:Name=""AirTargetBox""/><ComboBox x:Name=""AirCountBox""/><ComboBox x:Name=""GroundTargetBox""/><ComboBox x:Name=""GroundCountBox""/><ToggleButton x:Name=""HostileToggle""/><ComboBox x:Name=""ShipTargetBox""/><ComboBox x:Name=""ShipCountBox""/></Grid>
        <Button x:Name=""GenerateButton"" Grid.Row=""6"" Style=""{StaticResource PrimaryButton}"" Content=""GENERATE TEST MISSION""/>
        <TextBlock Grid.Row=""7"" Text=""AIR HOT LOAD  •  GROUND PROXY RELOAD"" Foreground=""{StaticResource CyanBrush}"" FontSize=""10"" HorizontalAlignment=""Center"" VerticalAlignment=""Bottom""/>
      </Grid></Border>
    </Grid>
    <Border Grid.Row=""2"" Background=""#D01A263D"" CornerRadius=""0"" Margin=""0"" Padding=""14,0"" BorderBrush=""#664BD5FF"" BorderThickness=""0,1,0,0""><TextBlock x:Name=""StatusText"" Text=""●  READY"" Foreground=""{StaticResource Good}"" VerticalAlignment=""Center""/></Border>
  </Grid>
</Grid>";
    }

    internal sealed class ModernMainWindow : Window
    {
        private readonly MainForm controller;
        private readonly Grid root;
        private readonly Grid windowHost;
        private readonly Grid overlayLayer;
        private readonly Border overlayBackdrop;
        private readonly Stack<ModernDialogWindow> overlayDialogs = new Stack<ModernDialogWindow>();
        private Border titleBar;
        private TextBox gameFolder;
        private TextBox aircraftSearch;
        private ComboBox nationFilter;
        private ComboBox rankFilter;
        private ComboBox typeFilter;
        private ListBox aircraftList;
        private TextBlock vehicleCount;
        private Border previewCard;
        private Grid previewClipContent;
        private TextBlock previewName;
        private TextBlock previewMeta;
        private Grid previewAircraftVisual;
        private Grid previewHelicopterVisual;
        private Grid previewDroneVisual;
        private Grid previewGroundVisual;
        private Image previewAircraftImage;
        private Image previewHelicopterImage;
        private Image previewDroneImage;
        private Image previewGroundImage;
        private TextBlock buildTitle;
        private TextBlock buildSubtitle;
        private Border pylonCard;
        private Grid weaponFilterPanel;
        private StackPanel groundWorkspacePanel;
        private Button systemsButton;
        private Button flightConfigureButton;
        private Button clearStationButton;
        private Button clearAllButton;
        private Button mountButton;
        private TextBlock stationText;
        private TextBlock massText;
        private UniformGrid pylonPanel;
        private ToggleButton injectionToggle;
        private System.Windows.Threading.DispatcherTimer weaponSearchTimer;
        private bool weaponColumnsPending;
        private TextBox weaponSearch;
        private ComboBox categoryFilter;
        private ComboBox weaponNationFilter;
        private ComboBox sortFilter;
        private Grid weaponTableFrame;
        private Grid weaponTableClipContent;
        private ListView weaponList;
        private ComboBox airTarget;
        private ComboBox groundTarget;
        private ComboBox shipTarget;
        private ComboBox airCount;
        private ComboBox groundCount;
        private ComboBox shipCount;
        private ToggleButton hostileToggle;
        private TextBlock flightProfileText;
        private TextBlock targetSummaryText;
        private TextBlock status;
        private Aircraft selectedAircraft;
        private PylonSlot selectedPylon;
        private List<AircraftView> aircraftViews;
        private readonly List<TargetView> configuredGroundTargets = new List<TargetView>();
        private bool passiveShip;
        private CombinedScenarioSettings combinedScenario = new CombinedScenarioSettings();
        private bool updatingWeaponColumns;

        public ModernMainWindow()
        {
            controller = new MainForm();
            Title = "Universal Test Lab — Mission Studio";
            Width = 1500;
            Height = 920;
            MinWidth = 1200;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Brushes.Transparent;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 38,
                ResizeBorderThickness = new Thickness(7),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            root = (Grid)ModernXaml.Parse(ModernXaml.Main);
            windowHost = new Grid { ClipToBounds = true };
            windowHost.Children.Add(root);
            overlayLayer = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = Brushes.Transparent,
                ClipToBounds = true
            };
            overlayBackdrop = new Border
            {
                Background = ModernPalette.Brush("#A60A142B"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            overlayLayer.Children.Add(overlayBackdrop);
            windowHost.Children.Add(overlayLayer);
            Content = windowHost;
            BindControls();
            ApplyChromeAccent();
            LoadPreviewImages();
            WireEvents();
            PopulateControls();
            controller.WorkspaceConfirmation = ConfirmWorkspaceAction;
            FitToWorkingArea();
            Loaded += delegate { ModernComboSizing.Attach(this); };
            SourceInitialized += delegate { DwmGlass.Apply(this); };
            Closed += delegate { controller.Dispose(); };
        }

        internal void ShowOverlay(ModernDialogWindow dialog)
        {
            if (dialog == null) return;
            ModernDialogWindow previous = overlayDialogs.Count > 0 ? overlayDialogs.Peek() : null;
            if (previous != null)
            {
                previous.IsHitTestVisible = false;
                previous.Effect = new BlurEffect { Radius = 6, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
                previous.Opacity = 0.52;
            }
            else
            {
                root.IsHitTestVisible = false;
                root.Effect = new BlurEffect { Radius = 12, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
                overlayLayer.Visibility = Visibility.Visible;
            }

            dialog.AttachOverlay(this);
            dialog.HorizontalAlignment = HorizontalAlignment.Center;
            dialog.VerticalAlignment = VerticalAlignment.Center;
            dialog.Margin = new Thickness(24);
            dialog.MaxWidth = Math.Max(480, ActualWidth - 48);
            dialog.MaxHeight = Math.Max(420, ActualHeight - 48);
            dialog.MinWidth = 0;
            dialog.MinHeight = 0;
            overlayDialogs.Push(dialog);
            overlayLayer.Children.Add(dialog);
            dialog.Focus();
            Keyboard.Focus(dialog);
        }

        internal void CloseOverlay(ModernDialogWindow dialog)
        {
            if (dialog == null || !overlayDialogs.Contains(dialog)) return;
            if (!ReferenceEquals(overlayDialogs.Peek(), dialog))
            {
                overlayLayer.Children.Remove(dialog);
                return;
            }

            overlayDialogs.Pop();
            overlayLayer.Children.Remove(dialog);
            dialog.DetachOverlay();
            if (overlayDialogs.Count > 0)
            {
                ModernDialogWindow previous = overlayDialogs.Peek();
                previous.IsHitTestVisible = true;
                previous.Effect = null;
                previous.Opacity = 1;
                previous.Focus();
            }
            else
            {
                overlayLayer.Visibility = Visibility.Collapsed;
                root.Effect = null;
                root.IsHitTestVisible = true;
            }
        }

        private T Find<T>(string name) where T : DependencyObject { return (T)root.FindName(name); }

        private void BindControls()
        {
            titleBar = Find<Border>("TitleBar");
            gameFolder = Find<TextBox>("GameFolderBox");
            aircraftSearch = Find<TextBox>("AircraftSearch");
            nationFilter = Find<ComboBox>("NationFilter");
            rankFilter = Find<ComboBox>("RankFilter");
            typeFilter = Find<ComboBox>("TypeFilter");
            aircraftList = Find<ListBox>("AircraftList");
            vehicleCount = Find<TextBlock>("VehicleCountText");
            previewCard = Find<Border>("PreviewCard");
            previewClipContent = Find<Grid>("PreviewClipContent");
            previewName = Find<TextBlock>("PreviewName");
            previewMeta = Find<TextBlock>("PreviewMeta");
            previewAircraftVisual = Find<Grid>("PreviewAircraftVisual");
            previewHelicopterVisual = Find<Grid>("PreviewHelicopterVisual");
            previewDroneVisual = Find<Grid>("PreviewDroneVisual");
            previewAircraftImage = Find<Image>("PreviewAircraftImage");
            previewHelicopterImage = Find<Image>("PreviewHelicopterImage");
            previewDroneImage = Find<Image>("PreviewDroneImage");
            buildTitle = Find<TextBlock>("BuildTitle");
            buildSubtitle = Find<TextBlock>("BuildSubtitle");
            stationText = Find<TextBlock>("StationText");
            massText = Find<TextBlock>("MassText");
            pylonPanel = Find<UniformGrid>("PylonPanel");
            pylonCard = Find<Border>("PylonCard");
            weaponFilterPanel = Find<Grid>("WeaponFilterPanel");
            injectionToggle = Find<ToggleButton>("InjectionToggle");
            weaponSearchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            weaponSearchTimer.Tick += delegate { weaponSearchTimer.Stop(); RefreshWeapons(); };
            weaponSearch = Find<TextBox>("WeaponSearch");
            categoryFilter = Find<ComboBox>("CategoryFilter");
            weaponNationFilter = Find<ComboBox>("WeaponNationFilter");
            sortFilter = Find<ComboBox>("SortFilter");
            weaponTableFrame = Find<Grid>("WeaponTableFrame");
            weaponTableClipContent = Find<Grid>("WeaponTableClipContent");
            weaponList = Find<ListView>("WeaponList");
            airTarget = Find<ComboBox>("AirTargetBox");
            groundTarget = Find<ComboBox>("GroundTargetBox");
            shipTarget = Find<ComboBox>("ShipTargetBox");
            airCount = Find<ComboBox>("AirCountBox");
            groundCount = Find<ComboBox>("GroundCountBox");
            shipCount = Find<ComboBox>("ShipCountBox");
            hostileToggle = Find<ToggleButton>("HostileToggle");
            flightProfileText = Find<TextBlock>("FlightProfileText");
            targetSummaryText = Find<TextBlock>("TargetSummaryText");
            status = Find<TextBlock>("StatusText");
            systemsButton = Find<Button>("SystemsButton");
            flightConfigureButton = Find<Button>("FlightConfigureButton");
            clearStationButton = Find<Button>("ClearStationButton");
            clearAllButton = Find<Button>("ClearAllButton");
            mountButton = Find<Button>("MountButton");
        }

        private void ApplyChromeAccent()
        {
            Color accent = SystemParameters.WindowGlassColor;
            if (accent.A < 64) accent = Color.FromRgb(210, 122, 242);
            accent.A = 255;
            SolidColorBrush brush = new SolidColorBrush(accent);
            brush.Freeze();
            titleBar.Background = brush;
        }

        private void LoadPreviewImages()
        {
            BitmapImage yf23 = LoadEmbeddedImage("UTL.preview-yf23.png");
            TransformedBitmap horizontalYf23 = new TransformedBitmap(yf23, new RotateTransform(90));
            horizontalYf23.Freeze();
            BitmapImage apache = LoadEmbeddedImage("UTL.preview-ah64e.png");
            previewAircraftImage.Source = horizontalYf23;
            previewHelicopterImage.Source = apache;
            // The FPV/drone preview intentionally uses the same AH-64E side asset.
            previewDroneImage.Source = apache;
            previewGroundVisual = new Grid { Visibility = Visibility.Collapsed };
            previewGroundImage = new Image { Width = 290, Height = 110, Stretch = Stretch.Uniform, Opacity = 0.96, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0), Source = LoadEmbeddedImage("UTL.preview-m1a2-sepv3.png") };
            previewGroundVisual.Children.Add(previewGroundImage);
            previewClipContent.Children.Insert(Math.Max(0, previewClipContent.Children.Count - 1), previewGroundVisual);
            groundWorkspacePanel = new StackPanel { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 620 };
            groundWorkspacePanel.Children.Add(new TextBlock { Text = "GROUND VEHICLE LAB", Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontSize = 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            groundWorkspacePanel.Children.Add(new TextBlock { Text = "Research modules are configured in MODULES. Ammunition, foreign projectile injection, reload, recoil, ballistics and mobility are configured in GROUND CONFIGURE.", Foreground = ModernPalette.Brush(ModernPalette.Muted), FontSize = 13, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 14, 0, 0) });
            weaponTableClipContent.Children.Add(groundWorkspacePanel);
        }

        private static BitmapImage LoadEmbeddedImage(string resourceName)
        {
            BitmapImage image = new BitmapImage();
            using (MemoryStream stream = new MemoryStream(Embedded.Bytes(resourceName)))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }
            return image;
        }

        private void WireEvents()
        {
            previewClipContent.SizeChanged += delegate { UpdatePreviewClip(); };
            previewClipContent.Loaded += delegate { UpdatePreviewClip(); };

            titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) ToggleMaximize();
                else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            Button minimize = Find<Button>("MinimizeButton");
            Button maximize = Find<Button>("MaximizeButton");
            Button close = Find<Button>("CloseButton");
            WindowChrome.SetIsHitTestVisibleInChrome(minimize, true);
            WindowChrome.SetIsHitTestVisibleInChrome(maximize, true);
            WindowChrome.SetIsHitTestVisibleInChrome(close, true);
            minimize.Click += delegate { SystemCommands.MinimizeWindow(this); };
            maximize.Click += delegate { ToggleMaximize(); };
            close.Click += delegate { Close(); };

            Find<Button>("BrowseButton").Click += delegate { BrowseGameFolder(); };
            Find<Button>("SyncButton").Click += delegate { SyncBaseMission(); };
            Find<Button>("MissionsButton").Click += delegate { OpenMissionFolder(); };
            Find<Button>("PresetsButton").Click += delegate { ShowPresets(); };
            Find<Button>("AboutButton").Click += delegate { ShowAbout(); };

            aircraftSearch.TextChanged += delegate { FilterAircraft(); };
            nationFilter.SelectionChanged += delegate { FilterAircraft(); };
            rankFilter.SelectionChanged += delegate { FilterAircraft(); };
            typeFilter.SelectionChanged += delegate { FilterAircraft(); };
            aircraftList.SelectionChanged += delegate { AircraftChanged(); };
            injectionToggle.Checked += delegate { RefreshWeapons(); };
            injectionToggle.Unchecked += delegate { RefreshWeapons(); };
            weaponSearch.TextChanged += delegate { weaponSearchTimer.Stop(); weaponSearchTimer.Start(); };
            categoryFilter.SelectionChanged += delegate { RefreshWeapons(); };
            weaponNationFilter.SelectionChanged += delegate { RefreshWeapons(); };
            sortFilter.SelectionChanged += delegate { RefreshWeapons(); };
            weaponList.MouseDoubleClick += delegate { MountWeapon(); };
            weaponTableClipContent.SizeChanged += delegate { UpdateWeaponTableClip(); };
            weaponTableClipContent.Loaded += delegate { UpdateWeaponTableClip(); };
            weaponList.SizeChanged += delegate { UpdateWeaponColumns(); };
            weaponList.Loaded += delegate { UpdateWeaponColumns(); };
            Find<Button>("MountButton").Click += delegate { MountWeapon(); };
            Find<Button>("ClearStationButton").Click += delegate { ClearStation(); };
            Find<Button>("ClearAllButton").Click += delegate { controller.WorkspaceClearAll(); RefreshPylons(); };
            Find<Button>("SystemsButton").Click += delegate { ShowFlightSystems(); };
            Find<Button>("FlightConfigureButton").Click += delegate { ShowFlightConfigure(); };
            Find<Button>("MapButton").Click += delegate { ShowMap(); };
            Find<Button>("GenerateButton").Click += delegate { GenerateMission(); };
        }

        private void FitToWorkingArea()
        {
            Rect work = SystemParameters.WorkArea;
            const double edge = 12;
            if (work.Width < MinWidth + edge * 2) MinWidth = Math.Max(960, work.Width - edge * 2);
            if (work.Height < MinHeight + edge * 2) MinHeight = Math.Max(560, work.Height - edge * 2);
            Width = Math.Min(1500, Math.Max(MinWidth, work.Width - edge * 2));
            Height = Math.Min(920, Math.Max(MinHeight, work.Height - edge * 2));
            Left = work.Left + Math.Max(edge, (work.Width - Width) / 2);
            Top = work.Top + Math.Max(edge, (work.Height - Height) / 2);
        }

        private void UpdatePreviewClip()
        {
            double width = Math.Max(0, previewClipContent.ActualWidth);
            double height = Math.Max(0, previewClipContent.ActualHeight);
            previewClipContent.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 14, 14);
        }

        private void UpdateWeaponTableClip()
        {
            double width = Math.Max(0, weaponTableClipContent.ActualWidth);
            double height = Math.Max(0, weaponTableClipContent.ActualHeight);
            weaponTableClipContent.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 12, 12);
        }

        private void PopulateControls()
        {
            gameFolder.Text = controller.WorkspaceGameFolder;
            aircraftViews = controller.WorkspaceAircraft.Select(x => new AircraftView(x)).ToList();
            nationFilter.Items.Add("All Nations");
            foreach (string value in controller.WorkspaceNations) nationFilter.Items.Add(value);
            rankFilter.Items.Add("Any Rank");
            for (int i = 1; i <= Math.Max(9, controller.WorkspaceAircraft.Max(x => x.Rank)); i++) rankFilter.Items.Add("Rank " + AircraftViewRoman(i));
            typeFilter.Items.Add("All Types");
            foreach (string value in controller.WorkspaceAircraft.Select(x => x.Kind).Distinct().OrderBy(x => x)) typeFilter.Items.Add(value);
            categoryFilter.Items.Add("All Weapon Types");
            foreach (string value in controller.WorkspaceWeaponCategories) categoryFilter.Items.Add(value);
            weaponNationFilter.Items.Add("All Nations");
            foreach (string value in controller.WorkspaceNations) weaponNationFilter.Items.Add(value);
            sortFilter.Items.Add("Mass: low to high");
            sortFilter.Items.Add("Mass: high to low");
            sortFilter.Items.Add("Name: A to Z");
            nationFilter.SelectedIndex = rankFilter.SelectedIndex = typeFilter.SelectedIndex = categoryFilter.SelectedIndex = weaponNationFilter.SelectedIndex = sortFilter.SelectedIndex = 0;

            List<AircraftView> targets = aircraftViews.OrderBy(x => x.Name).ToList();
            airTarget.ItemsSource = targets;
            groundTarget.ItemsSource = controller.WorkspaceGroundTargets.Select(x => new TargetView(x)).OrderBy(x => x.Name).ToList();
            shipTarget.ItemsSource = controller.WorkspaceShipTargets.Select(x => new TargetView(x)).OrderBy(x => x.Name).ToList();
            List<int> counts = Enumerable.Range(0, 21).ToList();
            airCount.ItemsSource = counts;
            groundCount.ItemsSource = counts;
            shipCount.ItemsSource = counts;
            airCount.SelectedItem = groundCount.SelectedItem = shipCount.SelectedItem = 1;
            SelectAircraftTarget("j_10c");
            SelectGroundTarget("ussr_bmpt");
            SelectShipTarget("jp_battleship_yamato");
            string[] defaultGround = { "ussr_t_34_1941_57", "us_m4_sherman_calliope", "ussr_bmpt", "us_m1a2_sep2_abrams", "ussr_t_90m_arena_m", "us_adats_bradley", "us_m901_itv" };
            IEnumerable<TargetView> availableGround = (groundTarget.ItemsSource as IEnumerable<TargetView>) ?? Enumerable.Empty<TargetView>();
            foreach (string id in defaultGround)
            {
                TargetView value = availableGround.FirstOrDefault(x => x.Source.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (value != null) configuredGroundTargets.Add(value);
            }
            UpdateConfigurationSummary();
            FilterAircraft();
            AircraftView initial = aircraftViews.FirstOrDefault(x => x.Source.Id == "ef_2000_typhoon_aesa") ?? aircraftViews.FirstOrDefault();
            if (initial != null) aircraftList.SelectedItem = aircraftList.Items.Cast<AircraftView>().FirstOrDefault(x => x.Source.Id == initial.Source.Id);
        }

        private static string AircraftViewRoman(int rank)
        {
            string[] values = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return rank >= 0 && rank < values.Length ? values[rank] : rank.ToString(CultureInfo.InvariantCulture);
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            Find<Button>("MaximizeButton").Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        private void FilterAircraft()
        {
            if (aircraftViews == null) return;
            string search = (aircraftSearch.Text ?? "").Trim();
            string nation = nationFilter.SelectedIndex > 0 ? nationFilter.SelectedItem as string : null;
            int rank = rankFilter.SelectedIndex > 0 ? rankFilter.SelectedIndex : 0;
            string kind = typeFilter.SelectedIndex > 0 ? typeFilter.SelectedItem as string : null;
            string keep = selectedAircraft == null ? null : selectedAircraft.Id;
            IEnumerable<AircraftView> query = aircraftViews;
            if (!String.IsNullOrEmpty(search)) query = query.Where(x => x.Name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.Source.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!String.IsNullOrEmpty(nation)) query = query.Where(x => x.Nation == nation);
            if (rank > 0) query = query.Where(x => x.Rank == rank);
            if (!String.IsNullOrEmpty(kind)) query = query.Where(x => x.Kind == kind);
            List<AircraftView> result = query.OrderByDescending(x => x.Rank).ThenBy(x => x.Name).ToList();
            aircraftList.ItemsSource = result;
            vehicleCount.Text = result.Count.ToString("N0", CultureInfo.InvariantCulture) + " vehicles in the current catalog";
            AircraftView previous = result.FirstOrDefault(x => x.Source.Id == keep);
            if (previous != null) aircraftList.SelectedItem = previous;
            else if (result.Count > 0 && aircraftList.SelectedItem == null) aircraftList.SelectedIndex = 0;
        }

        private void AircraftChanged()
        {
            AircraftView view = aircraftList.SelectedItem as AircraftView;
            if (view == null) return;
            selectedAircraft = view.Source;
            controller.WorkspaceSelectAircraft(selectedAircraft.Id);
            previewName.Text = selectedAircraft.Display.ToUpperInvariant();
            previewMeta.Text = selectedAircraft.Kind.ToUpperInvariant() + "  •  " + selectedAircraft.Nation.ToUpperInvariant() + "  •  RANK " + AircraftViewRoman(selectedAircraft.Rank);
            UpdatePreviewKind(selectedAircraft.Kind);
            UpdateVehicleWorkspaceMode();
            if (combinedScenario != null && combinedScenario.Enabled)
            {
                string combinedKind = GroundSelected ? "ground" : MainForm.IsHelicopter(selectedAircraft, null) ? "helicopter" : "aircraft";
                CombinedMap combinedMap = controller.WorkspaceCombinedMaps.FirstOrDefault(x => x.Id.Equals(combinedScenario.MapId ?? "", StringComparison.OrdinalIgnoreCase));
                int combinedSide = combinedScenario.Side == 2 ? 2 : 1;
                if (combinedMap != null && !combinedMap.Spawns.Any(x => x.Side == combinedSide && x.Kind.Equals(combinedKind, StringComparison.OrdinalIgnoreCase) && x.Option.Equals(combinedScenario.SpawnOption ?? "", StringComparison.OrdinalIgnoreCase)))
                {
                    CombinedSpawn fallback = combinedMap.Spawns.FirstOrDefault(x => x.Side == combinedSide && x.Kind.Equals(combinedKind, StringComparison.OrdinalIgnoreCase));
                    combinedScenario.SpawnOption = fallback == null ? null : fallback.Option;
                }
            }
            RefreshPylons();
            UpdateConfigurationSummary();
            SetStatus("VEHICLE READY — " + selectedAircraft.Display, false);
        }

        private bool GroundSelected { get { return selectedAircraft != null && String.Equals(selectedAircraft.Kind, "Ground Vehicle", StringComparison.OrdinalIgnoreCase); } }

        private void UpdateVehicleWorkspaceMode()
        {
            bool ground = GroundSelected;
            buildTitle.Text = ground ? "CONFIGURE GROUND VEHICLE" : "BUILD LOADOUT";
            buildSubtitle.Text = ground ? "Modules, ammunition, ballistics and mobility" : "Select a station, then mount a weapon";
            pylonCard.Visibility = ground ? Visibility.Collapsed : Visibility.Visible;
            weaponFilterPanel.Visibility = ground ? Visibility.Collapsed : Visibility.Visible;
            weaponList.Visibility = ground ? Visibility.Collapsed : Visibility.Visible;
            if (groundWorkspacePanel != null) groundWorkspacePanel.Visibility = ground ? Visibility.Visible : Visibility.Collapsed;
            systemsButton.Content = "MODULES";
            flightConfigureButton.Content = ground ? "GROUND CONFIGURE" : "FLIGHT CONFIGURE";
            clearStationButton.Visibility = clearAllButton.Visibility = mountButton.Visibility = ground ? Visibility.Collapsed : Visibility.Visible;
            massText.Text = ground ? "GROUND UNIT" : massText.Text;
        }

        private void UpdatePreviewKind(string kind)
        {
            bool helicopter = String.Equals(kind, "Helicopter", StringComparison.OrdinalIgnoreCase);
            bool drone = String.Equals(kind, "Drone", StringComparison.OrdinalIgnoreCase);
            bool ground = String.Equals(kind, "Ground Vehicle", StringComparison.OrdinalIgnoreCase);
            previewAircraftVisual.Visibility = !helicopter && !drone && !ground ? Visibility.Visible : Visibility.Collapsed;
            previewHelicopterVisual.Visibility = helicopter ? Visibility.Visible : Visibility.Collapsed;
            previewDroneVisual.Visibility = drone ? Visibility.Visible : Visibility.Collapsed;
            if (previewGroundVisual != null) previewGroundVisual.Visibility = ground ? Visibility.Visible : Visibility.Collapsed;
        }

        private static int DisplayStation(PylonSlot slot)
        {
            return slot != null && slot.Order > 0 ? slot.Order : (slot == null ? 0 : slot.Slot);
        }

        private void RefreshPylons()
        {
            pylonPanel.Children.Clear();
            selectedPylon = null;
            if (selectedAircraft == null) return;
            if (GroundSelected)
            {
                stationText.Text = "CUSTOM GROUND UNIT — choose research modules and create a projectile/mobility profile.";
                weaponList.ItemsSource = null;
                UpdateMass();
                return;
            }
            List<PylonSlot> slots = controller.WorkspacePylons(selectedAircraft.Id);
            Dictionary<int, PylonAssignment> mounted = controller.WorkspaceAssignments;
            foreach (PylonSlot slot in slots)
            {
                PylonAssignment assignment;
                mounted.TryGetValue(slot.Slot, out assignment);
                int stationNumber = DisplayStation(slot);
                StackPanel label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                label.Children.Add(new TextBlock
                {
                    Text = stationNumber.ToString("00", CultureInfo.InvariantCulture),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                label.Children.Add(new TextBlock
                {
                    Text = assignment == null ? "EMPTY" : ShortName(assignment.Weapon.Name, 9),
                    FontSize = 8,
                    Foreground = ModernPalette.Brush(ModernPalette.Muted),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Button button = new Button
                {
                    MinWidth = 0,
                    Height = 62,
                    Margin = new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(2),
                    Tag = slot,
                    ToolTip = "Station " + stationNumber.ToString(CultureInfo.InvariantCulture),
                    Content = label,
                    Style = (Style)root.Resources["ButtonStyle"],
                    Background = assignment == null ? ModernPalette.Brush("#24365F") : ModernPalette.Brush("#225C62")
                };
                button.Click += PylonClicked;
                pylonPanel.Children.Add(button);
            }
            if (slots.Count > 0) SelectPylon(slots[0]);
            else
            {
                stationText.Text = selectedAircraft.Id.Equals("uav_inf_fpv_strike_drone", StringComparison.OrdinalIgnoreCase)
                    ? "FPV DRONE — no external pylons. Fly into the target to detonate the built-in HEAT warhead."
                    : "This vehicle has no editable weapon stations in the current catalog.";
                RefreshWeapons();
            }
            UpdateMass();
        }

        private void PylonClicked(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SelectPylon(button == null ? null : button.Tag as PylonSlot);
        }

        private void SelectPylon(PylonSlot slot)
        {
            if (slot == null) return;
            selectedPylon = slot;
            stationText.Text = "STATION " + DisplayStation(slot).ToString(CultureInfo.InvariantCulture) + " — choose a compatible weapon, or enable Injection for the full catalog.";
            foreach (Button button in pylonPanel.Children.OfType<Button>())
            {
                PylonSlot current = button.Tag as PylonSlot;
                button.BorderBrush = current != null && current.Slot == slot.Slot ? ModernPalette.Brush(ModernPalette.Cyan) : ModernPalette.Brush(ModernPalette.Border);
                button.BorderThickness = current != null && current.Slot == slot.Slot ? new Thickness(2) : new Thickness(1);
            }
            RefreshWeapons();
        }

        private void RefreshWeapons()
        {
            if (selectedAircraft == null || selectedPylon == null)
            {
                weaponList.ItemsSource = null;
                return;
            }
            bool injected = injectionToggle.IsChecked == true;
            string category = categoryFilter.SelectedItem as string;
            string nation = weaponNationFilter.SelectedItem as string;
            int sort = Math.Max(0, sortFilter.SelectedIndex);
            List<WeaponView> weapons = controller.WorkspaceWeapons(selectedAircraft.Id, selectedPylon.Slot, injected, weaponSearch.Text, category, nation, sort)
                .Select(x => new WeaponView(x, injected)).ToList();
            // WPF grouping materializes the whole collection and disables effective
            // row virtualization. The Type column still exposes the category, so a
            // flat, recycling list remains clear and stays responsive with thousands
            // of injectable weapons.
            weaponList.ItemsSource = weapons;
            if (!weaponColumnsPending)
            {
                weaponColumnsPending = true;
                weaponList.Dispatcher.BeginInvoke(new Action(delegate
                {
                    weaponColumnsPending = false;
                    UpdateWeaponColumns();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateWeaponColumns()
        {
            if (updatingWeaponColumns || weaponList == null) return;
            GridView view = weaponList.View as GridView;
            if (view == null || view.Columns.Count < 5 || weaponList.ActualWidth <= 0) return;
            updatingWeaponColumns = true;
            try
            {
                ScrollBar vertical = FindVisibleVerticalScrollBar(weaponList);
                GridViewColumnHeader modeHeader = view.Columns[4].Header as GridViewColumnHeader;
                if (modeHeader != null)
                {
                    if (vertical == null) modeHeader.ClearValue(FrameworkElement.StyleProperty);
                    else modeHeader.Style = (Style)root.Resources["LastGridHeader"];
                }
                double gutter = vertical == null ? 0 : Math.Max(8, vertical.ActualWidth);
                double available = Math.Max(360, weaponList.ActualWidth - gutter - 2);
                view.Columns[0].Width = available * 0.43;
                view.Columns[1].Width = available * 0.24;
                view.Columns[2].Width = available * 0.09;
                view.Columns[3].Width = available * 0.12;
                view.Columns[4].Width = available * 0.12;
            }
            finally { updatingWeaponColumns = false; }
        }

        private static ScrollBar FindVisibleVerticalScrollBar(DependencyObject parent)
        {
            if (parent == null) return null;
            int children = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < children; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                ScrollBar bar = child as ScrollBar;
                if (bar != null && bar.Orientation == Orientation.Vertical && bar.Visibility == Visibility.Visible)
                    return bar;
                ScrollBar nested = FindVisibleVerticalScrollBar(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private void MountWeapon()
        {
            WeaponView weapon = weaponList.SelectedItem as WeaponView;
            if (weapon == null || selectedPylon == null) return;
            if (controller.WorkspaceAssignWeapon(selectedPylon.Slot, weapon.Source, injectionToggle.IsChecked == true))
            {
                RefreshPylonsKeeping(selectedPylon.Slot);
                SetStatus("MOUNTED — " + weapon.Name + " on station " + DisplayStation(selectedPylon).ToString(CultureInfo.InvariantCulture), false);
            }
        }

        private void ClearStation()
        {
            if (selectedPylon == null) return;
            int slot = selectedPylon.Slot;
            controller.WorkspaceClearStation(slot);
            RefreshPylonsKeeping(slot);
        }

        private void RefreshPylonsKeeping(int slot)
        {
            RefreshPylons();
            PylonSlot keep = controller.WorkspacePylons(selectedAircraft.Id).FirstOrDefault(x => x.Slot == slot);
            if (keep != null) SelectPylon(keep);
        }

        private void UpdateMass()
        {
            if (GroundSelected) { massText.Text = "GROUND UNIT"; return; }
            double total = controller.WorkspaceAssignments.Values.Sum(x => x.Weapon.TotalMass);
            string limit = selectedAircraft != null && selectedAircraft.MaxLoad > 0 ? " / " + selectedAircraft.MaxLoad.ToString("0", CultureInfo.InvariantCulture) + " kg" : "";
            massText.Text = "MASS: " + total.ToString("0.0", CultureInfo.InvariantCulture) + " kg" + limit;
        }

        private static string ShortName(string value, int length)
        {
            if (String.IsNullOrEmpty(value)) return "WEAPON";
            string clean = value.Trim();
            return clean.Length <= length ? clean.ToUpperInvariant() : clean.Substring(0, Math.Max(3, length - 1)).ToUpperInvariant() + "…";
        }

        private void GenerateMission()
        {
            try
            {
                controller.WorkspaceGameFolder = gameFolder.Text;
                AircraftView air = airTarget.SelectedItem as AircraftView;
                TargetView ship = shipTarget.SelectedItem as TargetView;
                bool generated = controller.WorkspaceGenerateMission(air == null ? null : air.Source.Id, SelectedCount(airCount), configuredGroundTargets.Select(x => x.Source.Id).ToList(),
                    hostileToggle.IsChecked == true, ship == null ? null : ship.Source.Id, SelectedCount(shipCount), passiveShip, combinedScenario);
                if (generated)
                {
                    SetStatus("MISSION GENERATED — reopen User Missions in War Thunder", false);
                    ModernMissionGeneratedWindow dialog = new ModernMissionGeneratedWindow(GroundSelected) { Owner = this };
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                ModernMessageDialog error = new ModernMessageDialog("Universal Test Lab", ex.Message, "CLOSE", null, true) { Owner = this };
                error.ShowDialog();
            }
        }

        private void BrowseGameFolder()
        {
            try
            {
                string selected = controller.WorkspaceBrowseFolder(gameFolder.Text, new WindowInteropHelper(this).Handle);
                if (String.IsNullOrWhiteSpace(selected)) return;
                gameFolder.Text = selected;
                gameFolder.CaretIndex = gameFolder.Text.Length;
                gameFolder.ScrollToEnd();
                controller.WorkspaceGameFolder = selected;
                SetStatus("GAME DIRECTORY SAVED", false);
            }
            catch (Exception ex) { ShowWorkspaceMessage("Game Directory", ex.Message, true); }
        }

        private void SyncBaseMission()
        {
            try
            {
                controller.WorkspaceGameFolder = gameFolder.Text;
                controller.WorkspaceSyncBase();
                gameFolder.Text = controller.WorkspaceGameFolder;
                SetStatus("BASE MISSION INSTALLED", false);
                ShowWorkspaceMessage("Base Mission Installed", "Base mission installed. Close the User Missions tab in War Thunder and open it again; no game restart is required.", false);
            }
            catch (Exception ex) { ShowWorkspaceMessage("Base Mission", ex.Message, true); }
        }

        private void OpenMissionFolder()
        {
            try
            {
                controller.WorkspaceGameFolder = gameFolder.Text;
                controller.WorkspaceOpenMissions();
                gameFolder.Text = controller.WorkspaceGameFolder;
            }
            catch (Exception ex) { ShowWorkspaceMessage("User Missions", ex.Message, true); }
        }

        private bool ConfirmWorkspaceAction(string title, string message)
        {
            ModernMessageDialog dialog = new ModernMessageDialog(title, message, "CONTINUE", "CANCEL", false) { Owner = this };
            return dialog.ShowDialog() == true;
        }

        private void ShowWorkspaceMessage(string title, string message, bool danger)
        {
            SetStatus(message, danger);
            ModernMessageDialog dialog = new ModernMessageDialog(title, message, "CLOSE", null, danger) { Owner = this };
            dialog.ShowDialog();
        }

        private static int SelectedCount(ComboBox box) { return box.SelectedItem is int ? (int)box.SelectedItem : 0; }

        private void SelectAircraftTarget(string id)
        {
            airTarget.SelectedItem = (airTarget.ItemsSource as IEnumerable<AircraftView>).FirstOrDefault(x => x.Source.Id == id);
        }

        private void SelectGroundTarget(string id)
        {
            groundTarget.SelectedItem = (groundTarget.ItemsSource as IEnumerable<TargetView>).FirstOrDefault(x => x.Source.Id == id);
        }

        private void SelectShipTarget(string id)
        {
            shipTarget.SelectedItem = (shipTarget.ItemsSource as IEnumerable<TargetView>).FirstOrDefault(x => x.Source.Id == id);
        }

        private void ShowFlightSystems()
        {
            if (selectedAircraft == null) return;
            ModernFlightSystemsWindow dialog = new ModernFlightSystemsWindow(selectedAircraft,
                controller.WorkspaceModifications.Where(x => x.AircraftId.Equals(selectedAircraft.Id, StringComparison.OrdinalIgnoreCase)),
                controller.WorkspaceGetSettings(selectedAircraft), MainForm.IsHelicopter(selectedAircraft, null));
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                controller.WorkspaceSetSettings(selectedAircraft, dialog.Result);
                SetStatus("MODULES UPDATED — " + selectedAircraft.Display, false);
                UpdateConfigurationSummary();
            }
        }

        private void ShowFlightConfigure()
        {
            if (selectedAircraft == null) return;
            if (GroundSelected)
            {
                ModernGroundConfigureWindow groundDialog = new ModernGroundConfigureWindow(selectedAircraft, controller.WorkspaceGetSettings(selectedAircraft), controller.WorkspaceGroundAmmo);
                groundDialog.Owner = this;
                if (groundDialog.ShowDialog() == true && groundDialog.Result != null)
                {
                    controller.WorkspaceSetSettings(selectedAircraft, groundDialog.Result);
                    SetStatus("GROUND CONFIGURATION UPDATED — " + selectedAircraft.Display, false);
                    UpdateConfigurationSummary();
                }
                return;
            }
            ModernFlightConfigureWindow dialog = new ModernFlightConfigureWindow(selectedAircraft,
                controller.WorkspaceGetSettings(selectedAircraft), controller.WorkspaceCountermeasureLaunchers(selectedAircraft),
                controller.WorkspaceModifications.Where(x => x.AircraftId.Equals(selectedAircraft.Id, StringComparison.OrdinalIgnoreCase)));
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                controller.WorkspaceSetSettings(selectedAircraft, dialog.Result);
                SetStatus("FLIGHT CONFIGURATION UPDATED — " + selectedAircraft.Display, false);
                UpdateConfigurationSummary();
            }
        }

        private void ShowMap()
        {
            string playerKind = GroundSelected ? "ground" : MainForm.IsHelicopter(selectedAircraft, null) ? "helicopter" : "aircraft";
            ModernMapWindow dialog = new ModernMapWindow(
                (airTarget.ItemsSource as IEnumerable<AircraftView>) ?? Enumerable.Empty<AircraftView>(),
                (groundTarget.ItemsSource as IEnumerable<TargetView>) ?? Enumerable.Empty<TargetView>(),
                (shipTarget.ItemsSource as IEnumerable<TargetView>) ?? Enumerable.Empty<TargetView>(),
                airTarget.SelectedItem as AircraftView, SelectedCount(airCount), configuredGroundTargets,
                hostileToggle.IsChecked == true, shipTarget.SelectedItem as TargetView, SelectedCount(shipCount), passiveShip,
                controller.WorkspaceCombinedMaps, playerKind, combinedScenario);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;
            airTarget.SelectedItem = dialog.AirTarget;
            airCount.SelectedItem = dialog.AirCount;
            configuredGroundTargets.Clear();
            configuredGroundTargets.AddRange(dialog.GroundTargets);
            if (configuredGroundTargets.Count > 0) groundTarget.SelectedItem = configuredGroundTargets[0];
            groundCount.SelectedItem = configuredGroundTargets.Count > 0 ? 1 : 0;
            hostileToggle.IsChecked = dialog.Hostile;
            shipTarget.SelectedItem = dialog.ShipTarget;
            shipCount.SelectedItem = dialog.ShipCount;
            passiveShip = dialog.PassiveShip;
            combinedScenario = dialog.Scenario == null ? new CombinedScenarioSettings() : dialog.Scenario.Copy();
            UpdateConfigurationSummary();
        }

        private void UpdateConfigurationSummary()
        {
            if (flightProfileText == null || targetSummaryText == null) return;
            AircraftSettings settings = selectedAircraft == null ? new AircraftSettings() : controller.WorkspaceGetSettings(selectedAircraft);
            if (GroundSelected)
            {
                string ammo = settings.GroundAmmoLoadouts.Count == 0 ? "native ammunition" : settings.GroundAmmoLoadouts.Count.ToString(CultureInfo.InvariantCulture) + " custom ammunition slots";
                string tuning = settings.OverrideGroundBallistics ? "custom ballistics & mobility" : "native ballistics & mobility";
                string sight = String.IsNullOrWhiteSpace(settings.UserSightPath) ? "game/default sight" : System.IO.Path.GetFileNameWithoutExtension(settings.UserSightPath) + " sight";
                flightProfileText.Text = ammo + "  •  " + tuning + "\n" + sight + "  •  rearm 1 second after depletion\n" +
                    (combinedScenario != null && combinedScenario.Enabled ? "Instant respawn at the selected combined-battles spawn" : "Instant zero-delay respawn at the range hangar");
            }
            else
            {
            string fuel = settings.FullFuel ? "Full internal fuel" : settings.FuelMinutes.ToString(CultureInfo.InvariantCulture) + " minutes of internal fuel";
            string countermeasures = !settings.OverrideCountermeasures ? "Native countermeasure load" :
                settings.CountermeasureLoadouts.Count.ToString(CultureInfo.InvariantCulture) + " configured dispenser groups";
            string belts = settings.GunBeltSelections.Count == 0 ? "default gun belts" : settings.GunBeltSelections.Count.ToString(CultureInfo.InvariantCulture) + " selected gun belt groups";
            flightProfileText.Text = fuel + "  •  " + (combinedScenario != null && combinedScenario.Enabled ? "selected map spawn profile" : "adaptive air-start speed") + "\n" + countermeasures + "  •  " + belts + "\nAmmunition restored 1 second after depletion";
            }
            if (combinedScenario != null && combinedScenario.Enabled)
            {
                CombinedMap map = controller.WorkspaceCombinedMaps.FirstOrDefault(x => x.Id.Equals(combinedScenario.MapId ?? "", StringComparison.OrdinalIgnoreCase));
                string playerKind = GroundSelected ? "ground" : MainForm.IsHelicopter(selectedAircraft, null) ? "helicopter" : "aircraft";
                CombinedSpawn spawn = map == null ? null : map.Spawns.FirstOrDefault(x => x.Side == (combinedScenario.Side == 2 ? 2 : 1) && x.Kind.Equals(playerKind, StringComparison.OrdinalIgnoreCase) && x.Option.Equals(combinedScenario.SpawnOption ?? "", StringComparison.OrdinalIgnoreCase));
                targetSummaryText.Text = "Combined Battles — Domination\n" + (map == null ? "Select a map" : map.Display) + "  •  Side " + (combinedScenario.Side == 2 ? "2" : "1") + "\n" + (spawn == null ? "Select a compatible spawn" : spawn.Label) + "  •  no AI units";
                return;
            }
            AircraftView air = airTarget == null ? null : airTarget.SelectedItem as AircraftView;
            TargetView ground = groundTarget == null ? null : groundTarget.SelectedItem as TargetView;
            TargetView ship = shipTarget == null ? null : shipTarget.SelectedItem as TargetView;
            targetSummaryText.Text = "Air: " + SelectedCount(airCount).ToString(CultureInfo.InvariantCulture) + " × " + (air == null ? "none" : air.Name) +
                "\nGround: " + configuredGroundTargets.Count.ToString(CultureInfo.InvariantCulture) + " positions  •  " + (hostileToggle.IsChecked == true ? "ATTACKING" : "PASSIVE") +
                "\nNaval: " + SelectedCount(shipCount).ToString(CultureInfo.InvariantCulture) + " × " + (ship == null ? "none" : ship.Name) + "  •  " + (passiveShip ? "PASSIVE" : "RETURNS FIRE");
        }

        private void ShowPresets()
        {
            ModernPresetWindow dialog = new ModernPresetWindow(controller, this);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true) RefreshFromController();
        }

        private void ShowAbout()
        {
            ModernAboutWindow dialog = new ModernAboutWindow(controller.WorkspaceAircraft.Count, controller.WorkspaceWeaponCount);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        internal void RefreshFromController()
        {
            Aircraft current = controller.WorkspaceSelectedAircraft;
            if (current == null) return;
            aircraftSearch.Text = "";
            nationFilter.SelectedIndex = rankFilter.SelectedIndex = typeFilter.SelectedIndex = 0;
            FilterAircraft();
            aircraftList.SelectedItem = aircraftList.Items.Cast<AircraftView>().FirstOrDefault(x => x.Source.Id == current.Id);
            selectedAircraft = current;
            RefreshPylons();
            UpdateConfigurationSummary();
        }

        internal void ExerciseDropdownForSelfTest()
        {
            rankFilter.IsDropDownOpen = true;
            UpdateLayout();
            rankFilter.IsDropDownOpen = false;
        }

        internal void SelectVehicleKindForScreenshot(string kind)
        {
            AircraftView target = aircraftList.Items.Cast<AircraftView>().FirstOrDefault(x => String.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                aircraftList.SelectedItem = target;
                aircraftList.ScrollIntoView(target);
            }
        }

        internal void EnableInjectionForScreenshot()
        {
            injectionToggle.IsChecked = true;
            UpdateLayout();
            UpdateWeaponColumns();
        }

        internal void ShowGroundPresetForScreenshot()
        {
            SelectVehicleKindForScreenshot("Ground Vehicle");
            ModernPresetWindow dialog = new ModernPresetWindow(controller, this) { Owner = this };
            ShowOverlay(dialog);
            dialog.SelectFirstCustomSightForScreenshot();
        }

        internal void ShowMessageForScreenshot(bool danger)
        {
            ModernMessageDialog dialog = new ModernMessageDialog(
                danger ? "Game Resource" : "Base Mission Installed",
                danger ? "Extracted game resource was not found." : "Base mission installed. Close the User Missions tab in War Thunder and open it again; no game restart is required.",
                "CLOSE", null, danger) { Owner = this };
            ShowOverlay(dialog);
        }

        internal bool ExerciseOverlayForSelfTest()
        {
            int windowCountBefore = System.Windows.Application.Current.Windows.Count;
            ModernMissionGeneratedWindow dialog = new ModernMissionGeneratedWindow { Owner = this };
            ShowOverlay(dialog);
            UpdateLayout();
            bool firstShown = overlayLayer.Visibility == Visibility.Visible &&
                overlayDialogs.Count == 1 &&
                overlayLayer.Children.Contains(dialog) &&
                dialog.OverlayChromeReadyForSelfTest() &&
                root.Effect is BlurEffect &&
                !root.IsHitTestVisible &&
                System.Windows.Application.Current.Windows.Count == windowCountBefore;

            ModernMessageDialog nested = new ModernMessageDialog("Overlay Test", "Nested confirmations stay inside the same application window.", "OK", "CANCEL", false) { Owner = this };
            ShowOverlay(nested);
            UpdateLayout();
            bool nestedShown = overlayDialogs.Count == 2 &&
                overlayLayer.Children.Contains(nested) &&
                nested.OverlayChromeReadyForSelfTest() &&
                dialog.Effect is BlurEffect &&
                !dialog.IsHitTestVisible &&
                System.Windows.Application.Current.Windows.Count == windowCountBefore;
            nested.Close();
            UpdateLayout();
            bool nestedClosed = overlayDialogs.Count == 1 &&
                dialog.Effect == null && dialog.IsHitTestVisible && dialog.Opacity == 1;
            dialog.Close();
            UpdateLayout();
            bool closed = overlayLayer.Visibility == Visibility.Collapsed &&
                overlayDialogs.Count == 0 &&
                root.Effect == null &&
                root.IsHitTestVisible;
            return firstShown && nestedShown && nestedClosed && closed;
        }

        internal bool LayoutFixesReadyForSelfTest()
        {
            RectangleGeometry clip = previewClipContent.Clip as RectangleGeometry;
            Rect work = SystemParameters.WorkArea;
            bool insideWorkArea = Left >= work.Left - 1 && Top >= work.Top - 1 &&
                Left + ActualWidth <= work.Right + 1 && Top + ActualHeight <= work.Bottom + 1;
            ScrollBar testBar = new ScrollBar
            {
                Orientation = Orientation.Vertical,
                Style = (Style)root.Resources[typeof(ScrollBar)]
            };
            testBar.ApplyTemplate();
            Track track = testBar.Template.FindName("PART_Track", testBar) as Track;
            RectangleGeometry weaponClip = weaponTableClipContent.Clip as RectangleGeometry;
            injectionToggle.IsChecked = false;
            UpdateLayout();
            UpdateWeaponColumns();
            GridView normalView = weaponList.View as GridView;
            GridViewColumnHeader normalMode = normalView == null ? null : normalView.Columns[4].Header as GridViewColumnHeader;
            bool unroundedModeWithoutScroll = FindVisibleVerticalScrollBar(weaponList) == null &&
                normalMode != null && !Object.ReferenceEquals(normalMode.Style, root.Resources["LastGridHeader"]);
            if (normalMode != null) normalMode.ApplyTemplate();
            bool staticHeaderTemplate = normalMode != null && normalMode.Template != null &&
                normalMode.Template.FindName("HeaderBorder", normalMode) is Border &&
                normalMode.Template.Triggers.Count == 0;
            injectionToggle.IsChecked = true;
            UpdateLayout();
            UpdateWeaponColumns();
            GridView weaponView = weaponList.View as GridView;
            GridViewColumnHeader scrollingMode = weaponView == null ? null : weaponView.Columns[4].Header as GridViewColumnHeader;
            bool virtualizedWeapons = weaponList.ItemsSource is List<WeaponView> &&
                ScrollViewer.GetCanContentScroll(weaponList) &&
                VirtualizingStackPanel.GetIsVirtualizing(weaponList) &&
                VirtualizingStackPanel.GetVirtualizationMode(weaponList) == VirtualizationMode.Recycling;
            double columnWidth = weaponView == null ? 0 : weaponView.Columns.Sum(x => x.Width);
            ScrollBar weaponScroll = FindVisibleVerticalScrollBar(weaponList);
            double weaponGutter = weaponScroll == null ? 0 : Math.Max(8, weaponScroll.ActualWidth);
            double expectedColumnWidth = Math.Max(360, weaponList.ActualWidth - weaponGutter - 2);
            List<PylonSlot> stationSlots = pylonPanel.Children.OfType<Button>().Select(x => x.Tag as PylonSlot).Where(x => x != null).ToList();
            bool stationOrder = stationSlots.Count > 0 && stationSlots.Select(DisplayStation).SequenceEqual(stationSlots.Select(DisplayStation).OrderBy(x => x));
            bool stationFit = pylonPanel.Children.OfType<Button>().All(x => x.ActualWidth <= 100 && x.ActualHeight <= 64);
            SolidColorBrush rootBrush = root.Background as SolidColorBrush;
            Border titleBar = Find<Border>("TitleBar");
            SolidColorBrush titleBrush = titleBar.Background as SolidColorBrush;
            bool gameFolderVisible = gameFolder.ActualHeight >= 29 && gameFolder.Padding.Top <= 3 &&
                gameFolder.VerticalContentAlignment == VerticalAlignment.Center && !String.IsNullOrWhiteSpace(gameFolder.Text);
            UpdatePreviewKind("Aircraft");
            bool aircraftPreview = previewAircraftVisual.Visibility == Visibility.Visible && previewHelicopterVisual.Visibility == Visibility.Collapsed && previewDroneVisual.Visibility == Visibility.Collapsed && previewAircraftImage.Source != null;
            UpdatePreviewKind("Helicopter");
            bool helicopterPreview = previewAircraftVisual.Visibility == Visibility.Collapsed && previewHelicopterVisual.Visibility == Visibility.Visible && previewDroneVisual.Visibility == Visibility.Collapsed && previewHelicopterImage.Source != null;
            UpdatePreviewKind("Drone");
            bool dronePreview = previewAircraftVisual.Visibility == Visibility.Collapsed && previewHelicopterVisual.Visibility == Visibility.Collapsed && previewDroneVisual.Visibility == Visibility.Visible && Object.ReferenceEquals(previewHelicopterImage.Source, previewDroneImage.Source);
            UpdatePreviewKind(selectedAircraft == null ? "Aircraft" : selectedAircraft.Kind);
            return clip != null && clip.RadiusX == 14 && clip.RadiusY == 14 && previewCard.Clip == null &&
                previewClipContent.ActualWidth > 0 && previewClipContent.ActualHeight > 0 && insideWorkArea &&
                weaponClip != null && weaponClip.RadiusX == 12 && weaponTableFrame.Clip == null &&
                weaponScroll != null && unroundedModeWithoutScroll && staticHeaderTemplate && scrollingMode != null &&
                Object.ReferenceEquals(scrollingMode.Style, root.Resources["LastGridHeader"]) &&
                virtualizedWeapons &&
                Math.Abs(columnWidth - expectedColumnWidth) < 2 && stationOrder && stationFit &&
                rootBrush != null && rootBrush.Color.A < 255 && titleBrush != null && titleBrush.Color.A == 255 && gameFolderVisible &&
                aircraftPreview && helicopterPreview && dronePreview &&
                track != null && track.IsDirectionReversed &&
                !ModernXaml.Main.Contains("ChromeFill") &&
                ModernXaml.Main.Contains("Margin=\"10,7,34,7\"") &&
                ModernXaml.Main.Contains("Grid Grid.Row=\"1\" Margin=\"12,10,12,10\"");
        }

        internal bool CombinedCatalogReadyForSelfTest()
        {
            return controller.WorkspaceCombinedMaps.Count >= 40 && controller.WorkspaceCombinedMaps.All(map =>
                !String.IsNullOrWhiteSpace(map.Level) && map.Spawns.Count == 12 && new[] { 1, 2 }.All(side =>
                    new[] { "ground_1", "ground_2", "airfield", "air", "heli_near", "heli_far" }.All(option =>
                        map.Spawns.Count(spawn => spawn.Side == side && spawn.Option.Equals(option, StringComparison.OrdinalIgnoreCase)) == 1)));
        }

        private void SetStatus(string message, bool error)
        {
            status.Text = error ? "●  ERROR — " + message : "●  " + message;
            status.Foreground = ModernPalette.Brush(error ? ModernPalette.Danger : ModernPalette.Good);
        }
    }

    internal abstract class ModernDialogWindow : ContentControl
    {
        protected readonly Grid DialogRoot;
        protected readonly Border ContentCard;
        private ModernMainWindow overlayOwner;
        private Window standaloneHost;
        private System.Windows.Threading.DispatcherFrame dialogFrame;
        private bool isOpen;

        public string Title { get; set; }
        public Window Owner { get; set; }
        public ResizeMode ResizeMode { get; set; }
        public WindowStartupLocation WindowStartupLocation { get; set; }
        public WindowState WindowState { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool? DialogResult { get; set; }

        protected ModernDialogWindow(string title, double width, double height)
        {
            Title = title;
            Width = width;
            Height = height;
            MinWidth = Math.Min(width, 720);
            MinHeight = Math.Min(height, 520);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Brushes.Transparent;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Focusable = true;

            DialogRoot = new Grid { Background = Brushes.Transparent };
            Grid styleSource = (Grid)ModernXaml.Parse(ModernXaml.Main);
            foreach (object key in styleSource.Resources.Keys) DialogRoot.Resources[key] = styleSource.Resources[key];

            ContentCard = new Border
            {
                Margin = new Thickness(8),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(18),
                Background = ModernPalette.Brush("#EE34415B"),
                BorderBrush = ModernPalette.Brush(ModernPalette.Border),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };
            DialogRoot.Children.Add(ContentCard);

            Border closeCloud = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(13),
                Background = ModernPalette.Brush(ModernPalette.Danger),
                BorderBrush = ModernPalette.Brush("#FFFFA2BC"),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 14, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Close",
                Tag = "OverlayCloseCloud"
            };
            closeCloud.Child = new TextBlock
            {
                Text = "×",
                Foreground = ModernPalette.Brush("#FFFFE8EF"),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            };
            closeCloud.MouseLeftButtonUp += delegate { Close(); };
            Panel.SetZIndex(closeCloud, 20);
            DialogRoot.Children.Add(closeCloud);
            Content = DialogRoot;
            Loaded += delegate { ModernComboSizing.Attach(this); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            };
        }

        internal bool OverlayChromeReadyForSelfTest()
        {
            Border closeCloud = DialogRoot.Children.OfType<Border>().FirstOrDefault(x => String.Equals(x.Tag as string, "OverlayCloseCloud", StringComparison.Ordinal));
            SolidColorBrush closeFill = closeCloud == null ? null : closeCloud.Background as SolidColorBrush;
            SolidColorBrush dangerFill = ModernPalette.Brush(ModernPalette.Danger) as SolidColorBrush;
            return DialogRoot.RowDefinitions.Count == 0 && ContentCard.CornerRadius.TopLeft >= 18 && ContentCard.BorderThickness.Left > 0 &&
                closeCloud != null && closeCloud.CornerRadius.TopLeft >= 12 && closeCloud.BorderBrush != null && closeFill != null && dangerFill != null &&
                closeFill.Color == dangerFill.Color && closeFill.Color.A == 255;
        }

        internal void AttachOverlay(ModernMainWindow owner)
        {
            overlayOwner = owner;
            Owner = owner;
            isOpen = true;
        }

        internal void DetachOverlay()
        {
            overlayOwner = null;
        }

        public bool? ShowDialog()
        {
            ModernMainWindow main = Owner as ModernMainWindow ?? System.Windows.Application.Current.MainWindow as ModernMainWindow;
            DialogResult = null;
            isOpen = true;
            if (main != null)
            {
                main.ShowOverlay(this);
                dialogFrame = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.PushFrame(dialogFrame);
                dialogFrame = null;
                return DialogResult;
            }

            Window host = CreateStandaloneHost();
            host.ShowDialog();
            return DialogResult;
        }

        public void Show()
        {
            if (isOpen) return;
            DialogResult = null;
            isOpen = true;
            CreateStandaloneHost().Show();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            ModernMainWindow main = overlayOwner;
            if (main != null) main.CloseOverlay(this);
            Window host = standaloneHost;
            standaloneHost = null;
            if (host != null) host.Close();
            if (dialogFrame != null) dialogFrame.Continue = false;
        }

        public void DragMove()
        {
            if (standaloneHost != null)
            {
                try { standaloneHost.DragMove(); }
                catch (InvalidOperationException) { }
            }
        }

        private Window CreateStandaloneHost()
        {
            if (standaloneHost != null) return standaloneHost;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Margin = new Thickness(0);
            MaxWidth = Double.PositiveInfinity;
            MaxHeight = Double.PositiveInfinity;
            Window host = new Window
            {
                Title = Title,
                Width = Width,
                Height = Height,
                MinWidth = MinWidth,
                MinHeight = MinHeight,
                WindowStartupLocation = WindowStartupLocation,
                ResizeMode = ResizeMode,
                WindowStyle = WindowStyle.None,
                Background = Brushes.Transparent,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                Content = this
            };
            if (WindowStartupLocation == WindowStartupLocation.Manual)
            {
                host.Left = Left;
                host.Top = Top;
            }
            if (Owner != null && Owner != host) host.Owner = Owner;
            WindowChrome.SetWindowChrome(host, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(7),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });
            host.SourceInitialized += delegate { DwmGlass.Apply(host); };
            host.Closed += delegate
            {
                if (standaloneHost == host) standaloneHost = null;
                isOpen = false;
                if (dialogFrame != null) dialogFrame.Continue = false;
            };
            standaloneHost = host;
            return host;
        }

        protected Button DialogButton(string text, bool primary)
        {
            return new Button { Content = text, Style = (Style)DialogRoot.Resources[primary ? "PrimaryButton" : "ButtonStyle"], Margin = new Thickness(4, 0, 0, 0) };
        }

        protected TextBlock Heading(string text, double size)
        {
            return new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = ModernPalette.Brush(ModernPalette.Text) };
        }

        protected TextBlock Caption(string text)
        {
            return new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = ModernPalette.Brush(ModernPalette.Muted) };
        }
    }

    internal sealed class ModernMissionGeneratedWindow : ModernDialogWindow
    {
        public ModernMissionGeneratedWindow(bool ground = false) : base("Mission Generated", 590, 455)
        {
            ResizeMode = ResizeMode.NoResize;
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            ContentCard.Child = layout;
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            Border badge = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(16), Background = ModernPalette.Brush(ModernPalette.Good), VerticalAlignment = VerticalAlignment.Top };
            badge.Child = new TextBlock { Text = "✓", FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(badge);
            StackPanel heading = new StackPanel { Margin = new Thickness(10, 3, 0, 0) };
            heading.Children.Add(Heading("MISSION GENERATED", 21));
            heading.Children.Add(new TextBlock { Text = ground ? "The ground proxy and mission are ready." : "The hot-load mission is ready in War Thunder.", Foreground = ModernPalette.Brush(ModernPalette.Cyan), Margin = new Thickness(0, 4, 0, 0) });
            Grid.SetColumn(heading, 1); header.Children.Add(heading); layout.Children.Add(header);
            Border instructions = new Border { CornerRadius = new CornerRadius(14), Background = ModernPalette.Brush(ModernPalette.Field), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Padding = new Thickness(18), Margin = new Thickness(0, 8, 0, 10) };
            StackPanel steps = new StackPanel();
            steps.Children.Add(Heading(ground ? "RELOAD THE GROUND PROXY" : "REFRESH USER MISSIONS", 14));
            steps.Children.Add(new TextBlock { Text = ground ? "1   Exit War Thunder completely.\n\n2   Start War Thunder again.\n\n3   Open User Missions and launch the current HOT UTL mission." : "1   Close the User Missions tab.\n\n2   Open User Missions again to refresh the list.\n\n3   Launch the current HOT UTL mission.", Foreground = ModernPalette.Brush(ModernPalette.Text), FontSize = 13, Margin = new Thickness(0, 14, 0, 0) });
            steps.Children.Add(new TextBlock { Text = ground ? "A restart is required only because War Thunder caches the reserve-tank proxy." : "No game restart is required.", Foreground = ModernPalette.Brush(ModernPalette.Good), Margin = new Thickness(0, 16, 0, 0), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            steps.Children.Add(new TextBlock { Text = "Custom ground sight selected? Press Alt + F9 once in the mission to reload UserSights.", Foreground = ModernPalette.Brush(ModernPalette.Cyan), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), FontSize = 11 });
            instructions.Child = steps; Grid.SetRow(instructions, 1); layout.Children.Add(instructions);
            Button ok = DialogButton("GOT IT", true); ok.Width = 150; ok.HorizontalAlignment = HorizontalAlignment.Right; ok.Click += delegate { Close(); }; Grid.SetRow(ok, 2); layout.Children.Add(ok);
        }
    }

    internal sealed class ModernMessageDialog : ModernDialogWindow
    {
        public ModernMessageDialog(string title, string message, string primaryText, string secondaryText, bool danger)
            : base(title, 620, 390)
        {
            ResizeMode = ResizeMode.NoResize;
            bool confirmation = !String.IsNullOrEmpty(secondaryText);
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            ContentCard.Child = layout;

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            Border badge = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(15),
                Background = ModernPalette.Brush(danger ? ModernPalette.Danger : confirmation ? ModernPalette.AccentDark : ModernPalette.Good),
                VerticalAlignment = VerticalAlignment.Top
            };
            badge.Child = new TextBlock
            {
                Text = danger ? "!" : confirmation ? "?" : "✓",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(badge);
            StackPanel heading = new StackPanel { Margin = new Thickness(10, 2, 0, 0) };
            heading.Children.Add(Heading(title.ToUpperInvariant(), 20));
            heading.Children.Add(new TextBlock
            {
                Text = danger ? "The requested action could not be completed." : confirmation ? "Please confirm this action." : "The action completed successfully.",
                Foreground = ModernPalette.Brush(danger ? ModernPalette.Danger : confirmation ? ModernPalette.Cyan : ModernPalette.Good),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(heading, 1);
            header.Children.Add(heading);
            layout.Children.Add(header);

            Border messageCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = ModernPalette.Brush(ModernPalette.Field),
                BorderBrush = ModernPalette.Brush(ModernPalette.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 6, 0, 10)
            };
            messageCard.Child = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ModernPalette.Brush(ModernPalette.Text),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(messageCard, 1);
            layout.Children.Add(messageCard);

            Grid footer = new Grid { HorizontalAlignment = HorizontalAlignment.Right };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            if (!String.IsNullOrEmpty(secondaryText)) footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            if (!String.IsNullOrEmpty(secondaryText))
            {
                Button secondary = DialogButton(secondaryText, false);
                secondary.Click += delegate { DialogResult = false; Close(); };
                footer.Children.Add(secondary);
            }
            Button primary = DialogButton(primaryText, true);
            primary.Click += delegate { DialogResult = true; Close(); };
            if (!String.IsNullOrEmpty(secondaryText)) Grid.SetColumn(primary, 1);
            footer.Children.Add(primary);
            Grid.SetRow(footer, 2);
            layout.Children.Add(footer);
        }
    }

    internal sealed class ModernMapWindow : ModernDialogWindow
    {
        private readonly List<TargetView> allGround;
        private readonly List<TargetView> allShips;
        private readonly List<CombinedMap> allCombinedMaps;
        private readonly string playerKind;
        private readonly ComboBox modeBox;
        private readonly ComboBox mapBox;
        private readonly ComboBox sideBox;
        private readonly ComboBox spawnBox;
        private readonly Border combinedCard;
        private readonly StackPanel targetCards;
        private readonly TextBlock footerHint;
        private readonly ComboBox airBox;
        private readonly ComboBox airCountBox;
        private readonly List<ComboBox> groundBoxes = new List<ComboBox>();
        private readonly ComboBox groundNation;
        private readonly ComboBox groundRank;
        private readonly ToggleButton hostileBox;
        private readonly ComboBox shipBox;
        private readonly ComboBox shipCountBox;
        private readonly ComboBox shipNation;
        private readonly ComboBox shipRank;
        private readonly ToggleButton passiveShipBox;

        public AircraftView AirTarget { get; private set; }
        public int AirCount { get; private set; }
        public IList<TargetView> GroundTargets { get; private set; }
        public bool Hostile { get; private set; }
        public TargetView ShipTarget { get; private set; }
        public int ShipCount { get; private set; }
        public bool PassiveShip { get; private set; }
        public CombinedScenarioSettings Scenario { get; private set; }

        public ModernMapWindow(IEnumerable<AircraftView> aircraft, IEnumerable<TargetView> ground, IEnumerable<TargetView> ships,
            AircraftView currentAir, int currentAirCount, IEnumerable<TargetView> currentGround, bool hostile,
            TargetView currentShip, int currentShipCount, bool passiveShip, IEnumerable<CombinedMap> combinedMaps,
            string currentPlayerKind, CombinedScenarioSettings currentScenario) : base("Map & Scenario", 1000, 820)
        {
            allGround = ground.OrderBy(x => x.Name).ToList();
            allShips = ships.OrderBy(x => x.Name).ToList();
            allCombinedMaps = (combinedMaps ?? Enumerable.Empty<CombinedMap>()).OrderBy(x => x.Display).ToList();
            playerKind = String.IsNullOrWhiteSpace(currentPlayerKind) ? "aircraft" : currentPlayerKind;
            currentScenario = currentScenario == null ? new CombinedScenarioSettings() : currentScenario.Copy();
            List<TargetView> selectedGround = (currentGround ?? Enumerable.Empty<TargetView>()).Take(7).ToList();
            while (selectedGround.Count < 7 && allGround.Count > 0) selectedGround.Add(allGround[Math.Min(selectedGround.Count, allGround.Count - 1)]);

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            ContentCard.Child = layout;
            StackPanel header = new StackPanel();
            header.Children.Add(Heading("MAP & SCENARIO", 22));
            header.Children.Add(new TextBlock { Text = "Use the clean test range, or a solo combined-battles Domination map with native spawn coordinates.", Foreground = ModernPalette.Brush(ModernPalette.Cyan), Margin = new Thickness(0, 4, 0, 0) });
            Grid modeLine = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            modeLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            modeLine.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock modeLabel = Caption("SCENARIO MODE"); modeLabel.VerticalAlignment = VerticalAlignment.Center; modeLine.Children.Add(modeLabel);
            modeBox = new ComboBox { Margin = new Thickness(8, 0, 0, 0) };
            modeBox.Items.Add("Clean Test Range");
            modeBox.Items.Add("Combined Battles — Domination");
            modeBox.SelectedIndex = currentScenario.Enabled ? 1 : 0;
            Grid.SetColumn(modeBox, 1); modeLine.Children.Add(modeBox);
            header.Children.Add(modeLine);
            layout.Children.Add(header);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 6, 0, 8) };
            StackPanel content = new StackPanel();
            scroll.Content = content;
            Grid.SetRow(scroll, 1);
            layout.Children.Add(scroll);

            combinedCard = SectionCard();
            StackPanel combinedPanel = new StackPanel();
            combinedPanel.Children.Add(Heading("SOLO COMBINED-BATTLES SPAWN", 15));
            combinedPanel.Children.Add(new TextBlock
            {
                Text = "Uses extracted native Domination spawn coordinates. Only your configured vehicle is created; AI units are not added.",
                Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 12)
            });
            Grid combinedFields = new Grid();
            combinedFields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            combinedFields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            combinedFields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            StackPanel mapStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; mapStack.Children.Add(Caption("MAP"));
            mapBox = new ComboBox { ItemsSource = allCombinedMaps, Margin = new Thickness(0, 6, 0, 0) };
            mapBox.SelectedItem = allCombinedMaps.FirstOrDefault(x => x.Id.Equals(currentScenario.MapId ?? "", StringComparison.OrdinalIgnoreCase)) ?? allCombinedMaps.FirstOrDefault();
            mapStack.Children.Add(mapBox); combinedFields.Children.Add(mapStack);
            StackPanel sideStack = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; sideStack.Children.Add(Caption("SIDE"));
            sideBox = new ComboBox { ItemsSource = new[] { "Side 1", "Side 2" }, SelectedIndex = currentScenario.Side == 2 ? 1 : 0, Margin = new Thickness(0, 6, 0, 0) };
            sideStack.Children.Add(sideBox); Grid.SetColumn(sideStack, 1); combinedFields.Children.Add(sideStack);
            StackPanel spawnStack = new StackPanel(); spawnStack.Children.Add(Caption("SPAWN"));
            spawnBox = new ComboBox { Margin = new Thickness(0, 6, 0, 0), Tag = currentScenario.SpawnOption };
            spawnStack.Children.Add(spawnBox); Grid.SetColumn(spawnStack, 2); combinedFields.Children.Add(spawnStack);
            combinedPanel.Children.Add(combinedFields);
            combinedCard.Child = combinedPanel;
            content.Children.Add(combinedCard);

            targetCards = new StackPanel();
            content.Children.Add(targetCards);

            Border airCard = SectionCard();
            Grid airLine = new Grid();
            airLine.ColumnDefinitions.Add(new ColumnDefinition());
            airLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            StackPanel airStack = new StackPanel(); airStack.Children.Add(Caption("AIR TARGET")); airBox = new ComboBox { ItemsSource = aircraft.OrderBy(x => x.Name).ToList(), SelectedItem = currentAir, Margin = new Thickness(0, 6, 8, 0) }; airStack.Children.Add(airBox); airLine.Children.Add(airStack);
            StackPanel airCountStack = new StackPanel(); airCountStack.Children.Add(Caption("COUNT")); airCountBox = CountBox(currentAirCount); airCountBox.Margin = new Thickness(0, 6, 0, 0); airCountStack.Children.Add(airCountBox); Grid.SetColumn(airCountStack, 1); airLine.Children.Add(airCountStack);
            airCard.Child = airLine; targetCards.Children.Add(airCard);

            Border groundCard = SectionCard();
            StackPanel groundPanel = new StackPanel();
            Grid groundHeader = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            groundHeader.ColumnDefinitions.Add(new ColumnDefinition());
            groundHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            groundHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            groundHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            TextBlock groundTitle = Heading("GROUND RANGE POSITIONS", 15); groundTitle.VerticalAlignment = VerticalAlignment.Center; groundHeader.Children.Add(groundTitle);
            groundNation = FilterBox(allGround.Select(x => x.Nation), "All Nations"); Grid.SetColumn(groundNation, 1); groundHeader.Children.Add(groundNation);
            groundRank = RankBox(allGround); groundRank.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(groundRank, 2); groundHeader.Children.Add(groundRank);
            hostileBox = new ToggleButton { IsChecked = hostile, Style = (Style)DialogRoot.Resources["StatusToggleStyle"], Margin = new Thickness(8, 0, 0, 0), ToolTip = "Controls whether all seven selected ground targets actively aim at and fire on the player." }; Grid.SetColumn(hostileBox, 3); groundHeader.Children.Add(hostileBox);
            groundPanel.Children.Add(groundHeader);

            Grid groundGrid = new Grid();
            groundGrid.ColumnDefinitions.Add(new ColumnDefinition());
            groundGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int row = 0; row < 4; row++) groundGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });
            for (int index = 0; index < 7; index++)
            {
                StackPanel slot = new StackPanel { Margin = new Thickness(index % 2 == 0 ? 0 : 8, 0, index % 2 == 0 ? 8 : 0, 8) };
                slot.Children.Add(Caption("POSITION " + (index + 1).ToString("00", CultureInfo.InvariantCulture)));
                ComboBox box = new ComboBox { ItemsSource = allGround, SelectedItem = selectedGround.Count > index ? selectedGround[index] : null, Margin = new Thickness(0, 5, 0, 0) };
                groundBoxes.Add(box); slot.Children.Add(box);
                Grid.SetColumn(slot, index % 2); Grid.SetRow(slot, index / 2); groundGrid.Children.Add(slot);
            }
            groundPanel.Children.Add(groundGrid);
            groundCard.Child = groundPanel; targetCards.Children.Add(groundCard);

            Border shipCard = SectionCard();
            StackPanel shipPanel = new StackPanel();
            Grid shipFilters = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            shipFilters.ColumnDefinitions.Add(new ColumnDefinition());
            shipFilters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            shipFilters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            TextBlock shipTitle = Heading("NAVAL TARGET", 15); shipTitle.VerticalAlignment = VerticalAlignment.Center; shipFilters.Children.Add(shipTitle);
            shipNation = FilterBox(allShips.Select(x => x.Nation), "All Nations"); Grid.SetColumn(shipNation, 1); shipFilters.Children.Add(shipNation);
            shipRank = RankBox(allShips); shipRank.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(shipRank, 2); shipFilters.Children.Add(shipRank);
            shipPanel.Children.Add(shipFilters);
            Grid shipLine = new Grid(); shipLine.ColumnDefinitions.Add(new ColumnDefinition()); shipLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); shipLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            shipBox = new ComboBox { ItemsSource = allShips, SelectedItem = currentShip, Margin = new Thickness(0, 0, 8, 0) }; shipLine.Children.Add(shipBox);
            shipCountBox = CountBox(currentShipCount); Grid.SetColumn(shipCountBox, 1); shipLine.Children.Add(shipCountBox);
            passiveShipBox = new ToggleButton { IsChecked = passiveShip, Style = (Style)DialogRoot.Resources["StatusToggleStyle"], Margin = new Thickness(8, 0, 0, 0), ToolTip = "Controls whether the naval target stays passive or returns fire after the player attacks it." }; Grid.SetColumn(passiveShipBox, 2); shipLine.Children.Add(passiveShipBox);
            shipPanel.Children.Add(shipLine); shipCard.Child = shipPanel; targetCards.Children.Add(shipCard);

            groundNation.SelectionChanged += delegate { RefreshGround(); };
            groundRank.SelectionChanged += delegate { RefreshGround(); };
            shipNation.SelectionChanged += delegate { RefreshShips(); };
            shipRank.SelectionChanged += delegate { RefreshShips(); };
            hostileBox.Checked += delegate { UpdateReactionButtons(); };
            hostileBox.Unchecked += delegate { UpdateReactionButtons(); };
            passiveShipBox.Checked += delegate { UpdateReactionButtons(); };
            passiveShipBox.Unchecked += delegate { UpdateReactionButtons(); };
            modeBox.SelectionChanged += delegate { UpdateScenarioMode(); };
            mapBox.SelectionChanged += delegate { RefreshCombinedSpawns(); };
            sideBox.SelectionChanged += delegate { RefreshCombinedSpawns(); };
            UpdateReactionButtons();

            Grid footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            footerHint = new TextBlock { Foreground = ModernPalette.Brush(ModernPalette.Muted), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            footer.Children.Add(footerHint);
            Button cancel = DialogButton("CANCEL", false); cancel.Click += delegate { DialogResult = false; Close(); }; Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
            Button apply = DialogButton("APPLY MAP", true); apply.Click += delegate { Save(); }; Grid.SetColumn(apply, 2); footer.Children.Add(apply); Grid.SetRow(footer, 2); layout.Children.Add(footer);
            RefreshCombinedSpawns();
            UpdateScenarioMode();
        }

        public ModernMapWindow(IEnumerable<AircraftView> aircraft, IEnumerable<TargetView> ground, IEnumerable<TargetView> ships,
            AircraftView currentAir, int currentAirCount, TargetView currentGround, int currentGroundCount, bool hostile,
            TargetView currentShip, int currentShipCount)
            : this(aircraft, ground, ships, currentAir, currentAirCount, new[] { currentGround }, hostile, currentShip, currentShipCount, false,
                Enumerable.Empty<CombinedMap>(), "aircraft", new CombinedScenarioSettings()) { }

        private void UpdateScenarioMode()
        {
            bool combined = modeBox.SelectedIndex == 1;
            Height = combined ? 520 : 820;
            combinedCard.Visibility = combined ? Visibility.Visible : Visibility.Collapsed;
            targetCards.Visibility = combined ? Visibility.Collapsed : Visibility.Visible;
            footerHint.Text = combined
                ? "The mission contains only your vehicle, the selected spawn base and instant player respawn."
                : "Destroyed targets recover rapidly; player ammunition rearms after depletion.";
        }

        private void RefreshCombinedSpawns()
        {
            CombinedMap map = mapBox.SelectedItem as CombinedMap;
            int side = sideBox.SelectedIndex == 1 ? 2 : 1;
            string preferred = spawnBox.SelectedItem is CombinedSpawn ? ((CombinedSpawn)spawnBox.SelectedItem).Option : spawnBox.Tag as string;
            List<CombinedSpawn> values = map == null ? new List<CombinedSpawn>() : map.Spawns
                .Where(x => x.Side == side && x.Kind.Equals(playerKind, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Option.Equals("airfield", StringComparison.OrdinalIgnoreCase) || x.Option.Equals("ground_1", StringComparison.OrdinalIgnoreCase) || x.Option.Equals("heli_near", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x.Option, StringComparer.OrdinalIgnoreCase).ToList();
            spawnBox.ItemsSource = values;
            spawnBox.SelectedItem = values.FirstOrDefault(x => x.Option.Equals(preferred ?? "", StringComparison.OrdinalIgnoreCase)) ?? values.FirstOrDefault();
            spawnBox.Tag = null;
        }

        private Border SectionCard()
        {
            return new Border { CornerRadius = new CornerRadius(14), Background = ModernPalette.Brush(ModernPalette.Field), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 10) };
        }

        private ComboBox CountBox(int value)
        {
            return new ComboBox { ItemsSource = Enumerable.Range(0, 21).ToList(), SelectedItem = Math.Max(0, Math.Min(20, value)) };
        }

        private ComboBox FilterBox(IEnumerable<string> values, string all)
        {
            ComboBox box = new ComboBox();
            box.Items.Add(all);
            foreach (string value in values.Where(x => !String.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)) box.Items.Add(value);
            box.SelectedIndex = 0;
            return box;
        }

        private ComboBox RankBox(IEnumerable<TargetView> values)
        {
            ComboBox box = new ComboBox();
            box.Items.Add("Any Rank");
            foreach (int rank in values.Select(x => x.Rank).Where(x => x > 0).Distinct().OrderBy(x => x)) box.Items.Add(rank);
            box.SelectedIndex = 0;
            return box;
        }

        private IEnumerable<TargetView> ApplyFilter(IEnumerable<TargetView> source, ComboBox nation, ComboBox rank)
        {
            string selectedNation = nation.SelectedIndex > 0 ? nation.SelectedItem as string : null;
            int selectedRank = rank.SelectedItem is int ? (int)rank.SelectedItem : 0;
            if (!String.IsNullOrEmpty(selectedNation)) source = source.Where(x => x.Nation == selectedNation);
            if (selectedRank > 0) source = source.Where(x => x.Rank == selectedRank);
            return source.OrderBy(x => x.Name).ToList();
        }

        private void RefreshGround()
        {
            List<TargetView> values = ApplyFilter(allGround, groundNation, groundRank).ToList();
            foreach (ComboBox box in groundBoxes)
            {
                TargetView keep = box.SelectedItem as TargetView;
                box.ItemsSource = values;
                box.SelectedItem = keep != null && values.Any(x => x.Source.Id == keep.Source.Id) ? values.First(x => x.Source.Id == keep.Source.Id) : values.FirstOrDefault();
            }
        }

        private void RefreshShips()
        {
            TargetView keep = shipBox.SelectedItem as TargetView;
            List<TargetView> values = ApplyFilter(allShips, shipNation, shipRank).ToList();
            shipBox.ItemsSource = values;
            shipBox.SelectedItem = keep != null && values.Any(x => x.Source.Id == keep.Source.Id) ? values.First(x => x.Source.Id == keep.Source.Id) : values.FirstOrDefault();
        }

        private void UpdateReactionButtons()
        {
            bool groundAttacks = hostileBox.IsChecked == true;
            hostileBox.Content = groundAttacks ? "GROUND TARGETS — ATTACKING" : "GROUND TARGETS — PASSIVE";
            hostileBox.Background = ModernPalette.Brush(groundAttacks ? "#A34B1733" : "#8A1D5148");
            hostileBox.BorderBrush = ModernPalette.Brush(groundAttacks ? ModernPalette.Danger : ModernPalette.Good);
            hostileBox.Foreground = ModernPalette.Brush(groundAttacks ? "#FFFFE7EF" : "#FFE7FFF7");

            bool shipPassive = passiveShipBox.IsChecked == true;
            passiveShipBox.Content = shipPassive ? "SHIP — STAYS PASSIVE" : "SHIP — RETURNS FIRE";
            passiveShipBox.Background = ModernPalette.Brush(shipPassive ? "#8A1D5148" : "#A34B1733");
            passiveShipBox.BorderBrush = ModernPalette.Brush(shipPassive ? ModernPalette.Good : ModernPalette.Danger);
            passiveShipBox.Foreground = ModernPalette.Brush(shipPassive ? "#FFE7FFF7" : "#FFFFE7EF");
        }

        private void Save()
        {
            AirTarget = airBox.SelectedItem as AircraftView;
            AirCount = (int)(airCountBox.SelectedItem ?? 0);
            GroundTargets = groundBoxes.Select(x => x.SelectedItem as TargetView).Where(x => x != null).ToList();
            Hostile = hostileBox.IsChecked == true;
            ShipTarget = shipBox.SelectedItem as TargetView;
            ShipCount = (int)(shipCountBox.SelectedItem ?? 0);
            PassiveShip = passiveShipBox.IsChecked == true;
            CombinedMap map = mapBox.SelectedItem as CombinedMap;
            CombinedSpawn spawn = spawnBox.SelectedItem as CombinedSpawn;
            Scenario = new CombinedScenarioSettings
            {
                Enabled = modeBox.SelectedIndex == 1,
                MapId = map == null ? null : map.Id,
                Side = sideBox.SelectedIndex == 1 ? 2 : 1,
                SpawnOption = spawn == null ? null : spawn.Option
            };
            if (Scenario.Enabled && (map == null || spawn == null))
            {
                ModernMessageDialog error = new ModernMessageDialog("Map & Scenario", "Select a map, side and compatible spawn.", "CLOSE", null, true) { Owner = Owner };
                error.ShowDialog();
                return;
            }
            DialogResult = true;
            Close();
        }
    }

    internal sealed class GroundAmmoSlotEditor
    {
        public int Slot;
        public Border Card;
        public Button Select;
        public TextBlock Name;
        public Slider Count;
        public TextBlock Value;
    }

    internal sealed class ModernGroundConfigureWindow : ModernDialogWindow
    {
        private readonly Aircraft vehicle;
        private readonly AircraftSettings original;
        private readonly List<GroundAmmo> catalog;
        private readonly Dictionary<int, GroundAmmoLoadout> loadouts = new Dictionary<int, GroundAmmoLoadout>();
        private readonly List<GroundAmmoSlotEditor> slotEditors = new List<GroundAmmoSlotEditor>();
        private readonly Dictionary<string, TextBox> tuning = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, double> tuningStock = new Dictionary<string, double>();
        private readonly TextBox searchBox;
        private readonly ComboBox typeBox;
        private readonly ToggleButton injectionToggle;
        private readonly ListBox ammoList;
        private readonly TextBlock totalAmmoText;
        private readonly CheckBox overrideBallistics;
        private GroundAmmo projectileReference;
        private int selectedSlot;
        private bool updatingSlots;
        public AircraftSettings Result { get; private set; }

        private int AmmoCapacity { get { return vehicle.MaxAmmo > 0 ? vehicle.MaxAmmo : 200; } }

        public ModernGroundConfigureWindow(Aircraft item, AircraftSettings current, IEnumerable<GroundAmmo> ammo) : base("Ground Configure — " + item.Display, 1180, 780)
        {
            vehicle = item;
            original = (current ?? new AircraftSettings()).Copy();
            catalog = (ammo ?? Enumerable.Empty<GroundAmmo>()).ToList();
            foreach (GroundAmmoLoadout entry in original.GroundAmmoLoadouts.Where(x => x.Slot >= 0 && x.Slot < 4)) loadouts[entry.Slot] = entry.Copy();

            Grid layout = new Grid { ClipToBounds = true };
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            ContentCard.Child = layout;
            StackPanel header = new StackPanel();
            header.Children.Add(Heading("GROUND CONFIGURE", 22));
            header.Children.Add(new TextBlock { Text = item.Display + "  •  ammunition, projectile, cannon and mobility setup", Foreground = ModernPalette.Brush(ModernPalette.Cyan), Margin = new Thickness(0, 4, 0, 0) });
            layout.Children.Add(header);

            Grid body = new Grid { Margin = new Thickness(0, 6, 0, 10), ClipToBounds = true };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1); layout.Children.Add(body);

            Border ammoCard = Card(); Grid ammoGrid = new Grid { ClipToBounds = true };
            ammoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            ammoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            ammoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            ammoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(174) });
            ammoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            Grid ammoHeader = new Grid(); ammoHeader.ColumnDefinitions.Add(new ColumnDefinition()); ammoHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ammoHeader.Children.Add(Heading("AMMUNITION & PROJECTILE INJECTION", 15));
            totalAmmoText = new TextBlock { Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(totalAmmoText, 1); ammoHeader.Children.Add(totalAmmoText); ammoGrid.Children.Add(ammoHeader);
            Grid filters = new Grid(); filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); filters.ColumnDefinitions.Add(new ColumnDefinition()); filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            injectionToggle = new ToggleButton { Content = "INJECT ANY SHELL", Style = (Style)DialogRoot.Resources["ToggleStyle"], Margin = new Thickness(0, 3, 8, 3) };
            searchBox = new TextBox { Margin = new Thickness(0, 3, 8, 3) }; Grid.SetColumn(searchBox, 1);
            typeBox = new ComboBox { Margin = new Thickness(0, 3, 0, 3) }; Grid.SetColumn(typeBox, 2);
            filters.Children.Add(injectionToggle); filters.Children.Add(searchBox); filters.Children.Add(typeBox); Grid.SetRow(filters, 1); ammoGrid.Children.Add(filters);
            ammoList = new ListBox { Background = ModernPalette.Brush(ModernPalette.Field), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 0, 7) };
            Grid.SetRow(ammoList, 2); ammoGrid.Children.Add(ammoList);

            UniformGrid slots = new UniformGrid { Rows = 2, Columns = 2, Margin = new Thickness(0, 0, 0, 6) };
            for (int slot = 0; slot < 4; slot++) slots.Children.Add(CreateAmmoSlot(slot));
            Grid.SetRow(slots, 3); ammoGrid.Children.Add(slots);
            Grid mountRow = new Grid(); mountRow.ColumnDefinitions.Add(new ColumnDefinition()); mountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            mountRow.Children.Add(new TextBlock { Text = "Choose a slot, select a round above, then mount it.", Foreground = ModernPalette.Brush(ModernPalette.Muted), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            Button mount = DialogButton("MOUNT ROUND", true); mount.Click += delegate { MountSelectedAmmo(); }; Grid.SetColumn(mount, 1); mountRow.Children.Add(mount); Grid.SetRow(mountRow, 4); ammoGrid.Children.Add(mountRow);
            ammoCard.Child = ammoGrid; body.Children.Add(ammoCard);

            Border tuningCard = Card(); ScrollViewer tuningScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, ClipToBounds = true };
            StackPanel tuningPanel = new StackPanel(); tuningPanel.Children.Add(Heading("REAL VEHICLE VALUES", 15));
            overrideBallistics = new CheckBox { Content = "Override native values", IsChecked = original.OverrideGroundBallistics, Foreground = ModernPalette.Brush(ModernPalette.Cyan), Margin = new Thickness(0, 12, 0, 7) }; tuningPanel.Children.Add(overrideBallistics);
            tuningPanel.Children.Add(new TextBlock { Text = "Projectile values follow the selected ammunition slot. Every field can be typed directly.", Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
            projectileReference = ResolveProjectileReference();
            AddValue(tuningPanel, "PROJECTILE MASS", "projectileMass", projectileReference == null ? 0 : projectileReference.Mass, original.ProjectileMassMultiplier, "kg");
            AddValue(tuningPanel, "MUZZLE VELOCITY", "velocity", projectileReference == null ? 0 : projectileReference.Speed, original.MuzzleVelocityMultiplier, "m/s");
            AddValue(tuningPanel, "EXPLOSIVE FILLER", "explosive", projectileReference == null ? 0 : projectileReference.ExplosiveMass, original.ExplosiveMassMultiplier, "kg");
            AddValue(tuningPanel, "REFERENCE PENETRATION", "penetration", projectileReference == null ? 0 : projectileReference.Penetration, original.PenetrationMultiplier, "mm");
            AddValue(tuningPanel, "RELOAD TIME", "reload", vehicle.NativeReloadSeconds, original.ReloadSeconds > 0 && vehicle.NativeReloadSeconds > 0 ? original.ReloadSeconds / vehicle.NativeReloadSeconds : 1, "s");
            AddValue(tuningPanel, "RECOIL TRAVEL", "recoil", vehicle.NativeRecoil, original.RecoilMultiplier, "m");
            tuningPanel.Children.Add(new Border { Height = 1, Background = ModernPalette.Brush(ModernPalette.Border), Margin = new Thickness(0, 8, 0, 7) });
            AddValue(tuningPanel, "ENGINE POWER", "engine", vehicle.NativeEnginePower, original.EnginePowerMultiplier, "hp");
            AddValue(tuningPanel, "VEHICLE MASS", "mass", vehicle.NativeMass, original.VehicleMassMultiplier, "kg");
            AddValue(tuningPanel, "FORWARD SPEED LIMIT", "forward", vehicle.NativeForwardSpeed, original.ForwardSpeedMultiplier, "km/h");
            AddValue(tuningPanel, "REVERSE SPEED LIMIT", "reverse", vehicle.NativeReverseSpeed, original.ReverseSpeedMultiplier, "km/h");
            Button resetAll = DialogButton("RESET ALL TO CURRENT STOCK", false); resetAll.Margin = new Thickness(0, 10, 0, 4); resetAll.Click += delegate { ResetAllValues(); }; tuningPanel.Children.Add(resetAll);
            tuningPanel.Children.Add(new TextBlock { Text = "Stock reset uses this vehicle's current game definition; selected research modules remain configured separately in Modules.", Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) });
            tuningScroll.Content = tuningPanel; tuningCard.Child = tuningScroll; Grid.SetColumn(tuningCard, 2); body.Children.Add(tuningCard);

            Grid footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
            footer.Children.Add(new TextBlock { Text = "Player ammunition is restored one second after complete depletion.", Foreground = ModernPalette.Brush(ModernPalette.Muted), VerticalAlignment = VerticalAlignment.Center });
            Button cancel = DialogButton("CANCEL", false); cancel.Click += delegate { DialogResult = false; Close(); }; Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
            Button apply = DialogButton("APPLY CONFIG", true); apply.Click += delegate { Save(); }; Grid.SetColumn(apply, 2); footer.Children.Add(apply); Grid.SetRow(footer, 2); layout.Children.Add(footer);

            typeBox.Items.Add("All Projectile Types"); foreach (string kind in catalog.Select(x => x.Type).Distinct().OrderBy(x => x)) typeBox.Items.Add(kind); typeBox.SelectedIndex = 0;
            injectionToggle.IsChecked = false; injectionToggle.Checked += delegate { RefreshAmmo(); }; injectionToggle.Unchecked += delegate { RefreshAmmo(); }; searchBox.TextChanged += delegate { RefreshAmmo(); }; typeBox.SelectionChanged += delegate { RefreshAmmo(); };
            overrideBallistics.Checked += delegate { UpdateTuningState(); }; overrideBallistics.Unchecked += delegate { UpdateTuningState(); };
            SelectSlot(0); RefreshAmmo(); RefreshSlotEditors();
            UpdateTuningState();
        }

        private Border Card() { return new Border { CornerRadius = new CornerRadius(14), Background = ModernPalette.Brush(ModernPalette.Field), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Padding = new Thickness(12), ClipToBounds = true }; }

        private Border CreateAmmoSlot(int slot)
        {
            GroundAmmoSlotEditor editor = new GroundAmmoSlotEditor { Slot = slot };
            editor.Card = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = ModernPalette.Brush(ModernPalette.Border), Background = ModernPalette.Brush("#8A24324D"), Padding = new Thickness(8), Margin = new Thickness(3) };
            Grid grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) }); grid.RowDefinitions.Add(new RowDefinition());
            editor.Select = new Button { Content = "AMMO " + (slot + 1).ToString(CultureInfo.InvariantCulture), Tag = slot, Style = (Style)DialogRoot.Resources["ButtonStyle"], Padding = new Thickness(5, 1, 5, 1) };
            editor.Select.Click += delegate { SelectSlot(editor.Slot); }; grid.Children.Add(editor.Select);
            editor.Name = new TextBlock { Text = "EMPTY", Foreground = ModernPalette.Brush(ModernPalette.Muted), FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(editor.Name, 1); grid.Children.Add(editor.Name);
            Grid count = new Grid(); count.ColumnDefinitions.Add(new ColumnDefinition()); count.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            editor.Count = new Slider { Minimum = 0, Maximum = AmmoCapacity, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
            editor.Value = new TextBlock { Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            count.Children.Add(editor.Count); Grid.SetColumn(editor.Value, 1); count.Children.Add(editor.Value); Grid.SetRow(count, 2); grid.Children.Add(count);
            editor.Count.ValueChanged += delegate { if (!updatingSlots) UpdateSlotCount(editor.Slot, (int)editor.Count.Value); };
            editor.Card.Child = grid; slotEditors.Add(editor); return editor.Card;
        }

        private void AddValue(StackPanel panel, string label, string key, double stock, double multiplier, string unit)
        {
            double initial = stock > 0 ? stock * multiplier : 0;
            tuningStock[key] = stock;
            Grid row = new Grid { Margin = new Thickness(0, 3, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            StackPanel labelStack = new StackPanel(); labelStack.Children.Add(Caption(label)); labelStack.Children.Add(new TextBlock { Text = "Stock: " + FormatValue(stock) + " " + unit, Foreground = ModernPalette.Brush(ModernPalette.Muted), FontSize = 9, Margin = new Thickness(0, 2, 4, 0) }); row.Children.Add(labelStack);
            TextBox box = new TextBox { Text = FormatValue(initial), Height = 34, Padding = new Thickness(8, 3, 8, 3), Tag = unit }; Grid.SetColumn(box, 1); row.Children.Add(box);
            Button reset = new Button { Content = "RESET", Style = (Style)DialogRoot.Resources["ButtonStyle"], FontSize = 9, Padding = new Thickness(2), Margin = new Thickness(5, 0, 0, 0), Tag = key };
            reset.Click += delegate { ResetValue((string)reset.Tag); }; Grid.SetColumn(reset, 2); row.Children.Add(reset);
            panel.Children.Add(row); tuning[key] = box;
        }

        private static string FormatValue(double value) { return value.ToString(value >= 100 ? "0.##" : "0.####", CultureInfo.InvariantCulture); }

        private double ReadValue(string key)
        {
            double value;
            string text = (tuning[key].Text ?? "").Trim();
            if (Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return Math.Max(0, value);
            return tuningStock[key];
        }

        private double Ratio(string key)
        {
            double stock = tuningStock[key];
            return stock > 0 ? ReadValue(key) / stock : 1.0;
        }

        private void ResetValue(string key) { tuning[key].Text = FormatValue(tuningStock[key]); }
        private void ResetAllValues() { foreach (string key in tuning.Keys.ToList()) ResetValue(key); }
        private void UpdateTuningState() { bool enabled = overrideBallistics.IsChecked == true; foreach (TextBox value in tuning.Values) value.IsEnabled = enabled; }

        private GroundAmmo ResolveProjectileReference()
        {
            GroundAmmoLoadout entry;
            if (loadouts.TryGetValue(selectedSlot, out entry))
            {
                GroundAmmo selected = catalog.FirstOrDefault(x => x.SourceBlk.Equals(entry.SourceBlk ?? "", StringComparison.OrdinalIgnoreCase) && x.BulletName.Equals(entry.BulletName ?? "", StringComparison.OrdinalIgnoreCase));
                if (selected != null) return selected;
            }
            return catalog.FirstOrDefault(x => x.SourceBlk.Equals(vehicle.MainWeaponBlk ?? "", StringComparison.OrdinalIgnoreCase)) ?? catalog.FirstOrDefault();
        }

        private void SetProjectileReference(GroundAmmo ammo)
        {
            if (ammo == null || tuning.Count == 0) return;
            projectileReference = ammo;
            SetProjectileStock("projectileMass", ammo.Mass, original.ProjectileMassMultiplier);
            SetProjectileStock("velocity", ammo.Speed, original.MuzzleVelocityMultiplier);
            SetProjectileStock("explosive", ammo.ExplosiveMass, original.ExplosiveMassMultiplier);
            SetProjectileStock("penetration", ammo.Penetration, original.PenetrationMultiplier);
        }

        private void SetProjectileStock(string key, double stock, double multiplier)
        {
            tuningStock[key] = stock;
            tuning[key].Text = FormatValue(stock * multiplier);
        }

        private void RefreshAmmo()
        {
            IEnumerable<GroundAmmo> query = catalog;
            if (injectionToggle.IsChecked != true && !String.IsNullOrWhiteSpace(vehicle.MainWeaponBlk)) query = query.Where(x => x.SourceBlk.Equals(vehicle.MainWeaponBlk, StringComparison.OrdinalIgnoreCase));
            string search = (searchBox.Text ?? "").Trim(); if (search.Length > 0) query = query.Where(x => x.Display.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.BulletName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || x.Type.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            string type = typeBox.SelectedIndex > 0 ? typeBox.SelectedItem as string : null; if (!String.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);
            ammoList.ItemsSource = query.OrderBy(x => x.Caliber).ThenBy(x => x.Type).ThenBy(x => x.Display).ToList();
        }

        private void SelectSlot(int slot)
        {
            selectedSlot = slot;
            foreach (GroundAmmoSlotEditor editor in slotEditors) editor.Card.BorderBrush = editor.Slot == slot ? ModernPalette.Brush(ModernPalette.Cyan) : ModernPalette.Brush(ModernPalette.Border);
            SetProjectileReference(ResolveProjectileReference());
        }

        private void MountSelectedAmmo()
        {
            GroundAmmo ammo = ammoList.SelectedItem as GroundAmmo; if (ammo == null) return;
            GroundAmmoLoadout existing; loadouts.TryGetValue(selectedSlot, out existing);
            int others = loadouts.Values.Where(x => x.Slot != selectedSlot).Sum(x => Math.Max(0, x.Count));
            int available = Math.Max(0, AmmoCapacity - others);
            int count = existing == null ? Math.Min(1, available) : Math.Min(Math.Max(1, existing.Count), available);
            loadouts[selectedSlot] = new GroundAmmoLoadout { Slot = selectedSlot, Count = count, SourceBlk = ammo.SourceBlk, BulletName = ammo.BulletName };
            SetProjectileReference(ammo); RefreshSlotEditors(); SelectSlot(selectedSlot);
        }

        private void UpdateSlotCount(int slot, int count)
        {
            GroundAmmoLoadout entry;
            if (loadouts.TryGetValue(slot, out entry))
            {
                int others = loadouts.Values.Where(x => x.Slot != slot).Sum(x => Math.Max(0, x.Count));
                int allowedMaximum = Math.Max(0, AmmoCapacity - others);
                entry.Count = Math.Max(0, Math.Min(count, allowedMaximum));
            }
            RefreshSlotEditors();
        }

        private void RefreshSlotEditors()
        {
            updatingSlots = true;
            try
            {
                int remaining = AmmoCapacity;
                foreach (GroundAmmoLoadout entry in loadouts.Values.OrderBy(x => x.Slot))
                {
                    entry.Count = Math.Max(0, Math.Min(entry.Count, remaining));
                    remaining -= entry.Count;
                }
                foreach (GroundAmmoSlotEditor editor in slotEditors)
                {
                    GroundAmmoLoadout entry; loadouts.TryGetValue(editor.Slot, out entry);
                    int others = loadouts.Values.Where(x => x.Slot != editor.Slot).Sum(x => Math.Max(0, x.Count));
                    int allowedMaximum = Math.Max(0, AmmoCapacity - others);
                    int current = entry == null ? 0 : Math.Max(0, entry.Count);
                    editor.Count.Maximum = AmmoCapacity;
                    editor.Count.Value = current;
                    editor.Value.Text = current.ToString(CultureInfo.InvariantCulture) + " / " + allowedMaximum.ToString(CultureInfo.InvariantCulture);
                    editor.Count.ToolTip = "Loaded: " + current.ToString(CultureInfo.InvariantCulture) + "  •  Maximum currently available: " + allowedMaximum.ToString(CultureInfo.InvariantCulture);
                    GroundAmmo ammo = entry == null ? null : catalog.FirstOrDefault(x => x.SourceBlk.Equals(entry.SourceBlk ?? "", StringComparison.OrdinalIgnoreCase) && x.BulletName.Equals(entry.BulletName ?? "", StringComparison.OrdinalIgnoreCase));
                    editor.Name.Text = ammo == null ? "EMPTY" : ammo.Display + "  •  " + ammo.Type;
                }
                int used = loadouts.Values.Sum(x => Math.Max(0, x.Count));
                totalAmmoText.Text = "TOTAL  " + used.ToString(CultureInfo.InvariantCulture) + " / " + AmmoCapacity.ToString(CultureInfo.InvariantCulture);
            }
            finally { updatingSlots = false; }
        }

        internal bool AmmoSlidersStableForSelfTest()
        {
            if (slotEditors.Count < 2 || !loadouts.ContainsKey(0) || !loadouts.ContainsKey(1)) return false;
            int firstBefore = loadouts[0].Count;
            int secondBefore = loadouts[1].Count;
            int allowed = Math.Max(0, AmmoCapacity - loadouts.Values.Where(x => x.Slot != 0).Sum(x => Math.Max(0, x.Count)));
            int requested = Math.Min(allowed, firstBefore + 1);
            UpdateSlotCount(0, requested);
            bool stable = loadouts[0].Count == requested && loadouts[1].Count == secondBefore &&
                slotEditors.All(x => Math.Abs(x.Count.Maximum - AmmoCapacity) < 0.01) &&
                slotEditors[0].Value.Text.StartsWith(requested.ToString(CultureInfo.InvariantCulture) + " / ", StringComparison.Ordinal) &&
                slotEditors[1].Value.Text.StartsWith(secondBefore.ToString(CultureInfo.InvariantCulture) + " / ", StringComparison.Ordinal);
            UpdateSlotCount(0, firstBefore);
            return stable;
        }

        private void Save()
        {
            AircraftSettings result = original.Copy(); result.GroundAmmoLoadouts.Clear(); foreach (GroundAmmoLoadout entry in loadouts.Values.Where(x => x.Count > 0).OrderBy(x => x.Slot)) result.GroundAmmoLoadouts.Add(entry.Copy());
            result.OverrideGroundBallistics = overrideBallistics.IsChecked == true;
            result.ProjectileMassMultiplier = result.OverrideGroundBallistics ? Ratio("projectileMass") : 1; result.MuzzleVelocityMultiplier = result.OverrideGroundBallistics ? Ratio("velocity") : 1; result.ExplosiveMassMultiplier = result.OverrideGroundBallistics ? Ratio("explosive") : 1; result.PenetrationMultiplier = result.OverrideGroundBallistics ? Ratio("penetration") : 1;
            result.ReloadSeconds = result.OverrideGroundBallistics ? ReadValue("reload") : 0; result.RecoilMultiplier = result.OverrideGroundBallistics ? Ratio("recoil") : 1; result.EnginePowerMultiplier = result.OverrideGroundBallistics ? Ratio("engine") : 1; result.VehicleMassMultiplier = result.OverrideGroundBallistics ? Ratio("mass") : 1; result.ForwardSpeedMultiplier = result.OverrideGroundBallistics ? Ratio("forward") : 1; result.ReverseSpeedMultiplier = result.OverrideGroundBallistics ? Ratio("reverse") : 1;
            Result = result; DialogResult = true; Close();
        }
    }

    internal sealed class CountermeasureEditor
    {
        public CountermeasureLauncher Launcher;
        public Slider FlareSlider;
        public Slider ChaffSlider;
        public TextBlock FlareValue;
        public TextBlock ChaffValue;
        public Border Card;
    }

    internal sealed class GunBeltChoice
    {
        public string Id;
        public string Display;
        public override string ToString() { return Display; }
    }

    internal sealed class GunBeltEditor
    {
        public int GroupIndex;
        public ComboBox Selection;
    }

    internal sealed class ModernFlightConfigureWindow : ModernDialogWindow
    {
        private readonly AircraftSettings original;
        private readonly CheckBox fullFuel;
        private readonly Slider fuelSlider;
        private readonly TextBlock fuelValue;
        private readonly CheckBox customizeCountermeasures;
        private readonly ScrollViewer contentScroll;
        private readonly List<CountermeasureEditor> editors = new List<CountermeasureEditor>();
        private readonly List<GunBeltEditor> gunBeltEditors = new List<GunBeltEditor>();
        public AircraftSettings Result { get; private set; }

        public ModernFlightConfigureWindow(Aircraft aircraft, AircraftSettings current, IEnumerable<CountermeasureLauncher> launchers, IEnumerable<AircraftModification> modifications)
            : base("Flight Configure — " + aircraft.Display, 940, 780)
        {
            original = (current ?? new AircraftSettings()).Copy();
            Grid layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) }); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) }); ContentCard.Child = layout;
            StackPanel header = new StackPanel(); header.Children.Add(Heading("FLIGHT CONFIGURE", 22)); header.Children.Add(new TextBlock { Text = aircraft.Display + "  •  fuel, gun belts and countermeasure stations", Foreground = ModernPalette.Brush(ModernPalette.Cyan), Margin = new Thickness(0, 4, 0, 0) }); layout.Children.Add(header);
            StackPanel content = new StackPanel();
            Border fuelCard = Card("STARTING FUEL"); StackPanel fuelContent = fuelCard.Child as StackPanel;
            fullFuel = new CheckBox { Content = "Full internal fuel", IsChecked = original.FullFuel, Foreground = ModernPalette.Brush(ModernPalette.Text), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 8) }; fuelContent.Children.Add(fullFuel);
            Grid fuelRow = new Grid(); fuelRow.ColumnDefinitions.Add(new ColumnDefinition()); fuelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            fuelSlider = new Slider { Minimum = 5, Maximum = 60, TickFrequency = 5, IsSnapToTickEnabled = true, Value = Math.Max(5, Math.Min(60, original.FuelMinutes)), AutoToolTipPlacement = AutoToolTipPlacement.TopLeft, VerticalAlignment = VerticalAlignment.Center };
            fuelValue = ValueText(); fuelRow.Children.Add(fuelSlider); Grid.SetColumn(fuelValue, 1); fuelRow.Children.Add(fuelValue); fuelContent.Children.Add(fuelRow);
            fuelContent.Children.Add(new TextBlock { Text = "Minutes are mapped to the aircraft's internal-fuel percentage used by User Missions. External tanks are never added automatically.", Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }); content.Children.Add(fuelCard);

            AddGunBeltCard(content, modifications == null ? Enumerable.Empty<AircraftModification>() : modifications);

            Border cmCard = Card("COUNTERMEASURE STATIONS"); StackPanel cmContent = cmCard.Child as StackPanel;
            customizeCountermeasures = new CheckBox { Content = "Customize installed countermeasure stations", IsChecked = original.OverrideCountermeasures, Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 8) }; cmContent.Children.Add(customizeCountermeasures);
            foreach (CountermeasureLauncher launcher in launchers) AddLauncher(cmContent, launcher);
            cmContent.Children.Add(new TextBlock { Text = "Each emitter is configured separately. Flare-only or chaff-only dispensers expose only the supported slider; BOL, BKO and MAW modules still decide which stations exist. Ammunition is restored only after it is exhausted so active optics and seekers are not reset in flight.", Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }); content.Children.Add(cmCard);
            contentScroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 6, 0, 14), Padding = new Thickness(0, 0, 8, 30) }; Grid.SetRow(contentScroll, 1); layout.Children.Add(contentScroll);
            Grid footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) }); footer.Children.Add(new TextBlock { Text = "Settings are saved with this aircraft and custom presets.", Foreground = ModernPalette.Brush(ModernPalette.Muted), VerticalAlignment = VerticalAlignment.Center });
            Button cancel = DialogButton("CANCEL", false); cancel.Click += delegate { DialogResult = false; Close(); }; Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
            Button apply = DialogButton("APPLY CONFIG", true); apply.Click += delegate { Save(); }; Grid.SetColumn(apply, 2); footer.Children.Add(apply); Grid.SetRow(footer, 2); layout.Children.Add(footer);
            fuelSlider.ValueChanged += delegate { UpdateState(); }; fullFuel.Checked += delegate { UpdateState(); }; fullFuel.Unchecked += delegate { UpdateState(); };
            customizeCountermeasures.Checked += delegate { UpdateState(); }; customizeCountermeasures.Unchecked += delegate { UpdateState(); }; UpdateState();
        }

        internal void ScrollToEndForScreenshot()
        {
            contentScroll.ScrollToEnd();
            UpdateLayout();
        }

        private Border Card(string title)
        {
            StackPanel stack = new StackPanel(); stack.Children.Add(Heading(title, 15));
            return new Border { CornerRadius = new CornerRadius(15), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Background = ModernPalette.Brush("#A024324D"), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12), Child = stack };
        }

        private TextBlock ValueText()
        {
            return new TextBlock { Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        }

        private void AddGunBeltCard(StackPanel host, IEnumerable<AircraftModification> source)
        {
            List<AircraftModification> all = source.ToList();
            List<IGrouping<string, AircraftModification>> families = all.Where(IsGunBeltChoice)
                .GroupBy(x => GunBeltFamily(x.Id), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Take(4).ToList();
            if (families.Count == 0) return;
            List<AircraftModification> beltPacks = all.Where(x => x.Id.IndexOf("belt_pack", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Border card = Card("CANNON AMMUNITION BELTS");
            StackPanel stack = card.Child as StackPanel;
            stack.Children.Add(new TextBlock { Text = "Available belts follow the current Modules configuration.", Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 8) });
            int groupIndex = 0;
            foreach (IGrouping<string, AircraftModification> family in families)
            {
                List<AircraftModification> relatedPacks = beltPacks.Where(x => RelatedBeltPack(family.Key, x.Id)).ToList();
                if (relatedPacks.Count == 0 && families.Count == 1) relatedPacks = beltPacks;
                bool unlocked = original.UseAllModifications || relatedPacks.Count == 0 || relatedPacks.Any(x => original.EnabledModifications.Contains(x.Id));
                List<GunBeltChoice> options = new List<GunBeltChoice> { new GunBeltChoice { Id = "", Display = "Default belt (stock)" } };
                if (unlocked)
                    options.AddRange(family.OrderBy(x => x.Display).Select(x => new GunBeltChoice { Id = x.Id, Display = x.Display }));
                Grid row = new Grid { Margin = new Thickness(0, 4, 0, 7) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                string familyName = family.Key.Replace('_', ' ').ToUpperInvariant();
                StackPanel label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                label.Children.Add(new TextBlock { Text = "GUN GROUP " + (groupIndex + 1).ToString(CultureInfo.InvariantCulture) + "  •  " + familyName, Foreground = ModernPalette.Brush(ModernPalette.Text), FontWeight = FontWeights.SemiBold, FontSize = 11 });
                if (!unlocked) label.Children.Add(new TextBlock { Text = "Enable its Belt Pack in Modules", Foreground = ModernPalette.Brush(ModernPalette.Danger), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
                row.Children.Add(label);
                ComboBox selector = new ComboBox { ItemsSource = options, Margin = new Thickness(8, 0, 0, 0) };
                string saved;
                original.GunBeltSelections.TryGetValue(groupIndex, out saved);
                selector.SelectedItem = options.FirstOrDefault(x => !String.IsNullOrEmpty(saved) && x.Id.Equals(saved, StringComparison.OrdinalIgnoreCase)) ?? options[0];
                Grid.SetColumn(selector, 1); row.Children.Add(selector); stack.Children.Add(row);
                gunBeltEditors.Add(new GunBeltEditor { GroupIndex = groupIndex, Selection = selector });
                groupIndex++;
            }
            host.Children.Add(card);
        }

        private static bool IsGunBeltChoice(AircraftModification modification)
        {
            if (modification == null || modification.Tier != 0 || String.IsNullOrWhiteSpace(modification.Id)) return false;
            string id = modification.Id.ToLowerInvariant();
            return Regex.IsMatch(id, @"_(?:air_targets?|ground_targets?|armor_targets?|stealth|tracers?|all_tracers|universal|turret_ap(?:_t)?|turret_api)$") ||
                (modification.Display ?? "").IndexOf("ammunition belt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string GunBeltFamily(string id)
        {
            return Regex.Replace((id ?? "").ToLowerInvariant(), @"_(?:air_targets?|ground_targets?|armor_targets?|stealth|tracers?|all_tracers|universal|turret_ap(?:_t)?|turret_api)$", "");
        }

        internal static bool RelatedBeltPack(string family, string packId)
        {
            string left = Regex.Replace(family ?? "", @"[^a-z0-9]", "");
            string right = Regex.Replace((packId ?? "").Replace("belt_pack", ""), @"[^a-z0-9]", "");
            return left.Length > 0 && right.Length > 0 && (left.StartsWith(right, StringComparison.OrdinalIgnoreCase) || right.StartsWith(left, StringComparison.OrdinalIgnoreCase));
        }

        private void AddLauncher(StackPanel host, CountermeasureLauncher launcher)
        {
            CountermeasureLoadout saved = original.CountermeasureLoadouts.FirstOrDefault(x => x.Key.Equals(launcher.Key, StringComparison.OrdinalIgnoreCase));
            int flares = saved == null ? (launcher.AllowsFlares ? (launcher.AllowsChaff ? launcher.NativeRounds / 2 : launcher.NativeRounds) : 0) : saved.Flares;
            int chaff = saved == null ? (launcher.AllowsChaff ? (launcher.AllowsFlares ? launcher.NativeRounds - flares : launcher.NativeRounds) : 0) : saved.Chaff;
            Border card = new Border { CornerRadius = new CornerRadius(12), Background = ModernPalette.Brush(ModernPalette.Field), BorderBrush = ModernPalette.Brush("#526F99"), BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 8) };
            StackPanel stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = launcher.Display, FontWeight = FontWeights.SemiBold, Foreground = ModernPalette.Brush(ModernPalette.Text) });
            stack.Children.Add(new TextBlock { Text = "Native capacity: " + launcher.NativeRounds.ToString(CultureInfo.InvariantCulture), Foreground = ModernPalette.Brush(ModernPalette.Muted), FontSize = 10, Margin = new Thickness(0, 2, 0, 8) });
            CountermeasureEditor editor = new CountermeasureEditor { Launcher = launcher, Card = card };
            if (launcher.AllowsFlares) AddCountermeasureSlider(stack, "FLARES", flares, out editor.FlareSlider, out editor.FlareValue);
            if (launcher.AllowsChaff) AddCountermeasureSlider(stack, "CHAFF", chaff, out editor.ChaffSlider, out editor.ChaffValue);
            card.Child = stack; host.Children.Add(card); editors.Add(editor);
        }

        private void AddCountermeasureSlider(StackPanel host, string name, int initial, out Slider slider, out TextBlock value)
        {
            Grid row = new Grid { Margin = new Thickness(0, 3, 0, 3) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            row.Children.Add(new TextBlock { Text = name, Foreground = ModernPalette.Brush(ModernPalette.Muted), VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            slider = new Slider { Minimum = 0, Maximum = 512, TickFrequency = 1, IsSnapToTickEnabled = true, Value = Math.Max(0, Math.Min(512, initial)), AutoToolTipPlacement = AutoToolTipPlacement.TopLeft, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(slider, 1); row.Children.Add(slider); value = ValueText(); value.Text = ((int)slider.Value).ToString(CultureInfo.InvariantCulture); Grid.SetColumn(value, 2); row.Children.Add(value); host.Children.Add(row);
            Slider sliderControl = slider; TextBlock valueControl = value; slider.ValueChanged += delegate { valueControl.Text = ((int)sliderControl.Value).ToString(CultureInfo.InvariantCulture); };
        }

        private void UpdateState()
        {
            fuelSlider.IsEnabled = fullFuel.IsChecked != true;
            int fuelPercent = (int)Math.Round(fuelSlider.Value * 100.0 / 60.0);
            fuelValue.Text = fullFuel.IsChecked == true ? "FULL  •  100%" : ((int)fuelSlider.Value).ToString(CultureInfo.InvariantCulture) + " MIN  •  " + fuelPercent.ToString(CultureInfo.InvariantCulture) + "%";
            bool enabled = customizeCountermeasures.IsChecked == true;
            foreach (CountermeasureEditor editor in editors) { if (editor.FlareSlider != null) editor.FlareSlider.IsEnabled = enabled; if (editor.ChaffSlider != null) editor.ChaffSlider.IsEnabled = enabled; editor.Card.Opacity = customizeCountermeasures.IsChecked == true ? 1.0 : 0.62; }
        }

        private void Save()
        {
            AircraftSettings result = original.Copy(); result.FullFuel = fullFuel.IsChecked == true; result.FuelMinutes = (int)fuelSlider.Value; result.OverrideCountermeasures = customizeCountermeasures.IsChecked == true; result.UnlimitedCountermeasures = false; result.CountermeasureLoadouts.Clear(); result.GunBeltSelections.Clear();
            foreach (GunBeltEditor editor in gunBeltEditors)
            {
                GunBeltChoice selected = editor.Selection.SelectedItem as GunBeltChoice;
                if (selected != null && !String.IsNullOrWhiteSpace(selected.Id)) result.GunBeltSelections[editor.GroupIndex] = selected.Id;
            }
            int totalFlares = 0, totalChaff = 0;
            foreach (CountermeasureEditor editor in editors)
            {
                int flares = editor.FlareSlider == null ? 0 : (int)editor.FlareSlider.Value; int chaff = editor.ChaffSlider == null ? 0 : (int)editor.ChaffSlider.Value;
                result.CountermeasureLoadouts.Add(new CountermeasureLoadout { Key = editor.Launcher.Key, Flares = flares, Chaff = chaff }); totalFlares += flares; totalChaff += chaff;
            }
            result.FlareRounds = totalFlares; result.ChaffRounds = totalChaff; Result = result; DialogResult = true; Close();
        }
    }

    internal sealed class ModificationChoice
    {
        public AircraftModification Definition;
        public CheckBox Check;
    }

    internal sealed class ModernFlightSystemsWindow : ModernDialogWindow
    {
        private readonly Aircraft aircraft;
        private readonly AircraftSettings originalSettings;
        private readonly List<AircraftModification> definitions;
        private readonly List<ModificationChoice> choices = new List<ModificationChoice>();
        private readonly Grid pageHost;
        private readonly CheckBox allMods;
        private readonly StackPanel modificationList;
        private readonly FrameworkElement modulesPage;
        public AircraftSettings Result { get; private set; }

        public ModernFlightSystemsWindow(Aircraft item, IEnumerable<AircraftModification> modifications, AircraftSettings current, bool isHelicopter)
            : base("Modules — " + item.Display, 1080, 740)
        {
            aircraft = item;
            definitions = modifications.OrderBy(x => x.Tier).ThenBy(x => x.Display).ToList();
            AircraftSettings settings = (current ?? new AircraftSettings()).Copy();
            originalSettings = settings.Copy();

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            ContentCard.Child = layout;

            bool ground = String.Equals(item.Kind, "Ground Vehicle", StringComparison.OrdinalIgnoreCase);
            Grid header = StepHeader("MD", "MODULES", item.Display + "  •  research modules");
            layout.Children.Add(header);

            pageHost = new Grid { ClipToBounds = true, Margin = new Thickness(0, 0, 0, 2) };
            Grid.SetRow(pageHost, 1); layout.Children.Add(pageHost);
            modulesPage = BuildModulesPage(settings, out allMods, out modificationList);
            pageHost.Children.Add(modulesPage);

            Grid footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            footer.Children.Add(new TextBlock { Text = "Settings stay with this vehicle and are saved in presets.", Foreground = ModernPalette.Brush(ModernPalette.Muted), VerticalAlignment = VerticalAlignment.Center });
            Button cancel = DialogButton("CANCEL", false); Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
            Button apply = DialogButton("APPLY SETTINGS", true); Grid.SetColumn(apply, 2); footer.Children.Add(apply);
            cancel.Click += delegate { DialogResult = false; Close(); };
            apply.Click += delegate { Save(); };
            Grid.SetRow(footer, 2); layout.Children.Add(footer);
        }

        private Grid StepHeader(string badge, string title, string subtitle)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Border icon = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(13), Background = ModernPalette.Brush(ModernPalette.AccentDark), VerticalAlignment = VerticalAlignment.Top };
            icon.Child = new TextBlock { Text = badge, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(icon);
            StackPanel text = new StackPanel { Margin = new Thickness(10, 2, 0, 0) };
            text.Children.Add(Heading(title, 17));
            text.Children.Add(new TextBlock { Text = subtitle, Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetColumn(text, 1); grid.Children.Add(text);
            return grid;
        }

        private FrameworkElement BuildModulesPage(AircraftSettings settings, out CheckBox all, out StackPanel list)
        {
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = ModernPalette.Brush("#B51B2944"),
                BorderBrush = ModernPalette.Brush(ModernPalette.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(2, 4, 2, 4),
                ClipToBounds = true
            };
            Grid page = new Grid { Background = Brushes.Transparent };
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            shell.Child = page;

            Border selectionCard = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = ModernPalette.Brush("#9A24324D"),
                BorderBrush = ModernPalette.Brush(ModernPalette.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            StackPanel selectionContent = new StackPanel();
            all = new CheckBox { Content = "Enable all research modifications (current default)", IsChecked = settings.UseAllModifications, Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontWeight = FontWeights.SemiBold };
            selectionContent.Children.Add(all);
            selectionContent.Children.Add(new TextBlock { Text = "Turn this off to build a stock or selective vehicle. Alternative weapon groups remain mutually exclusive.", Foreground = ModernPalette.Brush(ModernPalette.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0) });
            selectionCard.Child = selectionContent;
            page.Children.Add(selectionCard);

            list = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
            ScrollViewer scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(6), ClipToBounds = true };
            Border moduleFrame = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = ModernPalette.Brush(ModernPalette.Field),
                BorderBrush = ModernPalette.Brush(ModernPalette.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                ClipToBounds = true,
                Child = scroll
            };
            Grid.SetRow(moduleFrame, 1); page.Children.Add(moduleFrame);
            foreach (IGrouping<int, AircraftModification> tier in definitions.GroupBy(x => x.Tier).OrderBy(x => x.Key))
            {
                Grid column = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
                column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                List<ModificationChoice> tierChoices = new List<ModificationChoice>();
                string rank = tier.Key <= 0 ? "BASE" : "RANK " + RomanTier(tier.Key);
                TextBlock rankTitle = new TextBlock { Text = rank, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = ModernPalette.Brush(ModernPalette.Cyan), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 9) };
                Grid.SetRow(rankTitle, 0);
                column.Children.Add(rankTitle);
                StackPanel moduleChoices = new StackPanel();
                foreach (AircraftModification definition in tier)
                {
                    CheckBox check = new CheckBox
                    {
                        Content = new TextBlock { Text = definition.Display, TextWrapping = TextWrapping.Wrap, FontSize = 11 },
                        Foreground = ModernPalette.Brush(ModernPalette.Text),
                        Margin = new Thickness(2, 5, 2, 5),
                        IsChecked = !settings.UseAllModifications && settings.EnabledModifications.Contains(definition.Id),
                        Tag = definition,
                        ToolTip = definition.Id
                    };
                    check.Checked += AlternativeChecked;
                    ModificationChoice choice = new ModificationChoice { Definition = definition, Check = check };
                    choices.Add(choice); tierChoices.Add(choice);
                    moduleChoices.Children.Add(check);
                }
                Grid.SetRow(moduleChoices, 1);
                column.Children.Add(moduleChoices);
                Button rankToggle = DialogButton("SELECT RANK", false);
                rankToggle.Height = 34; rankToggle.FontSize = 10; rankToggle.Margin = new Thickness(0, 10, 0, 0); rankToggle.Tag = false; rankToggle.VerticalAlignment = VerticalAlignment.Bottom;
                CheckBox allRanks = all;
                rankToggle.Click += delegate
                {
                    allRanks.IsChecked = false;
                    bool select = !(rankToggle.Tag is bool && (bool)rankToggle.Tag);
                    foreach (ModificationChoice choice in tierChoices) choice.Check.IsChecked = select;
                    rankToggle.Tag = select;
                    rankToggle.Content = select ? "CLEAR RANK" : "SELECT RANK";
                };
                Grid.SetRow(rankToggle, 2);
                column.Children.Add(rankToggle);
                Border tierCard = new Border { Width = 188, Margin = new Thickness(3, 2, 3, 4), Padding = new Thickness(10), CornerRadius = new CornerRadius(12), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Background = ModernPalette.Brush("#8A24324D"), Child = column, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Stretch };
                list.Children.Add(tierCard);
            }
            StackPanel listControl = list;
            CheckBox allControl = all;
            all.Checked += delegate { listControl.IsEnabled = false; };
            all.Unchecked += delegate { listControl.IsEnabled = true; };
            list.IsEnabled = all.IsChecked != true;

            Grid controls = new Grid { Margin = new Thickness(0, 9, 0, 0), ClipToBounds = true };
            controls.ColumnDefinitions.Add(new ColumnDefinition());
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            controls.Children.Add(new TextBlock { Text = definitions.Count.ToString(CultureInfo.InvariantCulture) + " modules found", Foreground = ModernPalette.Brush(ModernPalette.Cyan), VerticalAlignment = VerticalAlignment.Center });
            Button top = DialogButton("SELECT TOP SET", false); Grid.SetColumn(top, 1); controls.Children.Add(top);
            Button clear = DialogButton("CLEAR", false); Grid.SetColumn(clear, 2); controls.Children.Add(clear);
            top.Click += delegate { SelectTopSet(); };
            clear.Click += delegate { allControl.IsChecked = false; foreach (ModificationChoice choice in choices) choice.Check.IsChecked = false; };
            Grid.SetRow(controls, 2); page.Children.Add(controls);
            return shell;
        }

        internal bool ModulesCardReadyForSelfTest()
        {
            Border shell = modulesPage as Border;
            Grid page = shell == null ? null : shell.Child as Grid;
            Border selection = page == null ? null : page.Children.OfType<Border>().FirstOrDefault();
            List<Border> tierCards = modificationList == null ? new List<Border>() : modificationList.Children.OfType<Border>().ToList();
            bool rankButtonsAnchored = tierCards.Count > 0 && tierCards.All(delegate(Border card)
            {
                Grid layout = card.Child as Grid;
                Button button = layout == null ? null : layout.Children.OfType<Button>().FirstOrDefault();
                return layout != null && layout.RowDefinitions.Count == 3 && layout.RowDefinitions[1].Height.IsStar &&
                    button != null && Grid.GetRow(button) == 2 && button.VerticalAlignment == VerticalAlignment.Bottom;
            });
            return shell != null && shell.CornerRadius.TopLeft >= 16 && shell.Padding.Left >= 14 && shell.Margin.Left > 0 &&
                shell.BorderThickness.Left > 0 && selection != null && selection.CornerRadius.TopLeft >= 12 && selection.Margin.Bottom >= 10 && rankButtonsAnchored;
        }

        private static string RomanTier(int rank)
        {
            string[] values = { "—", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return rank >= 0 && rank < values.Length ? values[rank] : rank.ToString(CultureInfo.InvariantCulture);
        }

        private FrameworkElement BuildCountermeasuresPage(AircraftSettings settings, out CheckBox enable, out TextBox flares, out TextBox chaff, out CheckBox unlimited)
        {
            Grid page = CardPage();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            enable = new CheckBox { Content = "Override countermeasure settings", IsChecked = settings.OverrideCountermeasures, Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) };
            page.Children.Add(enable);
            flares = LabeledTextBox(page, "FLARES PER INSTALLED LAUNCHER", settings.FlareRounds.ToString(CultureInfo.InvariantCulture), 1);
            chaff = LabeledTextBox(page, "CHAFF PER INSTALLED LAUNCHER", settings.ChaffRounds.ToString(CultureInfo.InvariantCulture), 2);
            unlimited = new CheckBox { IsChecked = false, Visibility = Visibility.Collapsed };
            TextBlock hint = new TextBlock { Text = "BOL, BKO and external dispenser modules still decide which launchers exist. A mixed belt is generated with the requested flare/chaff ratio for every installed launcher.", TextWrapping = TextWrapping.Wrap, Foreground = ModernPalette.Brush(ModernPalette.Muted), Margin = new Thickness(0, 14, 0, 0) };
            Grid.SetRow(hint, 4); page.Children.Add(hint);
            TextBox flareControl = flares;
            TextBox chaffControl = chaff;
            CheckBox enableControl = enable;
            Action update = delegate { flareControl.IsEnabled = chaffControl.IsEnabled = enableControl.IsChecked == true; };
            enable.Checked += delegate { update(); }; enable.Unchecked += delegate { update(); };
            update();
            return page;
        }

        private Grid CardPage()
        {
            return new Grid { Background = Brushes.Transparent, Margin = new Thickness(14), ClipToBounds = true };
        }

        private TextBox LabeledTextBox(Grid page, string label, string value, int row)
        {
            StackPanel stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            stack.Children.Add(Caption(label));
            TextBox box = new TextBox { Text = value, Margin = new Thickness(0, 6, 0, 0), Height = 38 };
            stack.Children.Add(box); Grid.SetRow(stack, row); page.Children.Add(stack); return box;
        }

        private void AlternativeChecked(object sender, RoutedEventArgs e)
        {
            CheckBox selected = sender as CheckBox;
            AircraftModification definition = selected == null ? null : selected.Tag as AircraftModification;
            if (definition == null || String.IsNullOrWhiteSpace(definition.Group)) return;
            foreach (ModificationChoice choice in choices)
                if (!Object.ReferenceEquals(choice.Check, selected) && String.Equals(choice.Definition.Group, definition.Group, StringComparison.OrdinalIgnoreCase)) choice.Check.IsChecked = false;
        }

        private void SelectTopSet()
        {
            allMods.IsChecked = false;
            foreach (ModificationChoice choice in choices) choice.Check.IsChecked = false;
            HashSet<string> groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModificationChoice choice in choices.OrderByDescending(x => x.Definition.Tier))
                if (String.IsNullOrWhiteSpace(choice.Definition.Group) || groups.Add(choice.Definition.Group)) choice.Check.IsChecked = true;
        }

        private void Save()
        {
            AircraftSettings result = originalSettings.Copy();
            result.UseAllModifications = allMods.IsChecked == true;
            result.EnabledModifications.Clear();
            if (!result.UseAllModifications)
                foreach (ModificationChoice choice in choices.Where(x => x.Check.IsChecked == true)) result.EnabledModifications.Add(choice.Definition.Id);
            if (!result.UseAllModifications && result.GunBeltSelections.Count > 0)
            {
                foreach (KeyValuePair<int, string> belt in result.GunBeltSelections.ToList())
                {
                    string family = ModernFlightConfigureWindow.GunBeltFamily(belt.Value);
                    List<AircraftModification> packs = definitions.Where(x => x.Id.IndexOf("belt_pack", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        ModernFlightConfigureWindow.RelatedBeltPack(family, x.Id)).ToList();
                    if (packs.Count > 0 && !packs.Any(x => result.EnabledModifications.Contains(x.Id))) result.GunBeltSelections.Remove(belt.Key);
                }
            }
            Result = result;
            DialogResult = true;
            Close();
        }
    }

    internal sealed class ModernPresetWindow : ModernDialogWindow
    {
        private readonly MainForm controller;
        private readonly ModernMainWindow main;
        private readonly Aircraft presetVehicle;
        private readonly TextBox nameBox;
        private readonly ComboBox sightBox;
        private readonly ListBox list;
        private readonly List<SavedPreset> presets;

        public ModernPresetWindow(MainForm source, ModernMainWindow owner) : base("Custom Presets", 760, 560)
        {
            controller = source;
            main = owner;
            presetVehicle = controller.WorkspaceSelectedAircraft;
            presets = PresetStore.Load();
            bool groundPreset = presetVehicle != null && String.Equals(presetVehicle.Kind, "Ground Vehicle", StringComparison.OrdinalIgnoreCase);
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(groundPreset ? 74 : 0) });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            ContentCard.Child = layout;
            StackPanel heading = new StackPanel();
            heading.Children.Add(Heading("CUSTOM LOADOUT PRESETS", 18));
            heading.Children.Add(new TextBlock { Text = "Save or restore the vehicle, pylons, Modules and configuration settings.", Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
            layout.Children.Add(heading);
            StackPanel name = new StackPanel(); name.Children.Add(Caption("PRESET NAME"));
            nameBox = new TextBox { Text = controller.WorkspaceSelectedAircraft == null ? "" : controller.WorkspaceSelectedAircraft.Display, Margin = new Thickness(0, 6, 0, 0) }; name.Children.Add(nameBox);
            Grid.SetRow(name, 1); layout.Children.Add(name);
            if (groundPreset)
            {
                StackPanel sight = new StackPanel();
                List<UserSightEntry> sights = UserSightStore.Discover(controller.WorkspaceGameFolder);
                sight.Children.Add(Caption("GROUND USER SIGHT — " + Math.Max(0, sights.Count - 1).ToString(CultureInfo.InvariantCulture) + " FOUND • SAVED WITH THIS PRESET"));
                sightBox = new ComboBox { ItemsSource = sights, Margin = new Thickness(0, 6, 0, 0) };
                AircraftSettings settings = controller.WorkspaceGetSettings(presetVehicle);
                sightBox.SelectedItem = sights.FirstOrDefault(x => String.Equals(x.FilePath ?? "", settings.UserSightPath ?? "", StringComparison.OrdinalIgnoreCase)) ?? sights.First();
                sight.Children.Add(sightBox);
                Grid.SetRow(sight, 2); layout.Children.Add(sight);
            }
            list = new ListBox { ItemsSource = presets.OrderBy(x => x.Name).ToList(), Margin = new Thickness(0, 10, 0, 10) };
            Grid.SetRow(list, 3); layout.Children.Add(list);
            list.MouseDoubleClick += delegate { LoadSelected(); };
            Grid buttons = new Grid();
            for (int i = 0; i < 4; i++) buttons.ColumnDefinitions.Add(new ColumnDefinition());
            Button save = DialogButton("SAVE CURRENT", true); buttons.Children.Add(save);
            Button load = DialogButton("LOAD SELECTED", false); Grid.SetColumn(load, 1); buttons.Children.Add(load);
            Button delete = DialogButton("DELETE", false); Grid.SetColumn(delete, 2); buttons.Children.Add(delete);
            Button close = DialogButton("CLOSE", false); Grid.SetColumn(close, 3); buttons.Children.Add(close);
            save.Click += delegate { SaveCurrent(); }; load.Click += delegate { LoadSelected(); }; delete.Click += delegate { DeleteSelected(); }; close.Click += delegate { DialogResult = false; Close(); };
            Grid.SetRow(buttons, 4); layout.Children.Add(buttons);
        }

        internal void SelectFirstCustomSightForScreenshot()
        {
            if (sightBox != null && sightBox.Items.Count > 1) sightBox.SelectedIndex = 1;
        }

        private SavedPreset Selected { get { return list.SelectedItem as SavedPreset; } }

        private void RefreshList()
        {
            list.ItemsSource = null;
            list.ItemsSource = presets.OrderBy(x => x.Name).ToList();
        }

        private void SaveCurrent()
        {
            string name = (nameBox.Text ?? "").Trim();
            if (String.IsNullOrEmpty(name))
            {
                ModernMessageDialog warning = new ModernMessageDialog("Presets", "Enter a preset name.", "OK", null, true) { Owner = Owner };
                warning.ShowDialog();
                return;
            }
            SavedPreset existing = presets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null)
            {
                ModernMessageDialog confirm = new ModernMessageDialog("Replace Preset", "Replace the existing preset '" + existing.Name + "'?", "REPLACE", "CANCEL", false) { Owner = Owner };
                if (confirm.ShowDialog() != true) return;
            }
            if (existing != null) presets.Remove(existing);
            ApplyUserSightSelection();
            presets.Add(controller.CaptureCurrentPreset(name));
            PresetStore.Save(presets);
            RefreshList();
            main.RefreshFromController();
        }

        private void ApplyUserSightSelection()
        {
            if (presetVehicle == null || sightBox == null) return;
            UserSightEntry sight = sightBox.SelectedItem as UserSightEntry;
            AircraftSettings settings = controller.WorkspaceGetSettings(presetVehicle);
            settings.UserSightPath = sight == null || sight.IsDefault ? "" : sight.FilePath;
            controller.WorkspaceSetSettings(presetVehicle, settings);
        }

        private void LoadSelected()
        {
            if (Selected == null) return;
            controller.LoadSavedPreset(Selected);
            DialogResult = true;
            Close();
        }

        private void DeleteSelected()
        {
            SavedPreset selected = Selected;
            if (selected == null) return;
            ModernMessageDialog confirm = new ModernMessageDialog("Delete Preset", "Delete preset '" + selected.Name + "'?", "DELETE", "CANCEL", true) { Owner = Owner };
            if (confirm.ShowDialog() != true) return;
            presets.Remove(selected); PresetStore.Save(presets); RefreshList();
        }
    }

    internal sealed class ModernAboutWindow : ModernDialogWindow
    {
        private const string SupportUrl = "https://buy.stripe.com/bJe00bbHB0GH0qI655fQI00";
        private const string ProjectUrl = "https://github.com/UKRAngler/Universal-Test-Lab";
        private const string AstraYoutubeUrl = "https://youtube.com/@astra-sep?si=TiMO8--EXG2zXapG";
        private const string AstraTiktokUrl = "https://www.tiktok.com/@astro.sep?_r=1&_t=ZS-997wx6cJtcm";

        public ModernAboutWindow(int aircraftCount, int weaponCount) : base("Support Universal Test Lab", 900, 670)
        {
            ResizeMode = ResizeMode.NoResize;
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            ContentCard.Child = layout;
            StackPanel header = new StackPanel();
            header.Children.Add(Heading("UNIVERSAL TEST LAB", 24));
            header.Children.Add(new TextBlock { Text = "v0.12.0  •  community-inspired mission and vehicle test workspace for War Thunder", Foreground = ModernPalette.Brush(ModernPalette.Cyan), FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
            layout.Children.Add(header);
            Grid content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            StackPanel info = new StackPanel { Margin = new Thickness(0, 6, 20, 0) };
            info.Children.Add(Heading("PROJECT", 14));
            info.Children.Add(new TextBlock { Text = "Build experimental vehicles, modules, ammunition, loadouts and reusable test missions from one workspace.", TextWrapping = TextWrapping.Wrap, Foreground = ModernPalette.Brush(ModernPalette.Muted), Margin = new Thickness(0, 4, 0, 5) });
            info.Children.Add(new TextBlock { Text = aircraftCount.ToString("N0", CultureInfo.InvariantCulture) + " playable vehicle entries  •  " + weaponCount.ToString("N0", CultureInfo.InvariantCulture) + " air-weapon entries", Foreground = ModernPalette.Brush(ModernPalette.Cyan), Margin = new Thickness(0, 0, 0, 15) });
            info.Children.Add(Heading("COMMUNITY INSPIRATION", 14));
            info.Children.Add(new TextBlock { Text = "Independent work by AstraSEP, inspired by GUI and custom-mission concepts shared by community creators and YouTube channels, for example Ask3lad. They are not project contributors.", TextWrapping = TextWrapping.Wrap, Foreground = ModernPalette.Brush(ModernPalette.Muted), Margin = new Thickness(0, 4, 0, 15) });
            info.Children.Add(Heading("CREATED BY ASTRASEP", 14));
            info.Children.Add(new TextBlock { Text = "Independent fan-made software shaped by community testing and feedback.", TextWrapping = TextWrapping.Wrap, Foreground = ModernPalette.Brush(ModernPalette.Muted), Margin = new Thickness(0, 4, 0, 8) });
            Button youtube = DialogButton("ASTRASEP ON YOUTUBE", false); youtube.Margin = new Thickness(0, 0, 0, 4); youtube.Click += delegate { OpenUrl(AstraYoutubeUrl); }; info.Children.Add(youtube);
            Button tiktok = DialogButton("ASTRASEP ON TIKTOK", false); tiktok.Margin = new Thickness(0, 0, 0, 12); tiktok.Click += delegate { OpenUrl(AstraTiktokUrl); }; info.Children.Add(tiktok);
            info.Children.Add(Heading("OPEN SOURCE", 14));
            info.Children.Add(new TextBlock { Text = "Source, issue tracking and contribution information are available on GitHub. The bundled wt_ext_cli component retains its Apache 2.0 license.", TextWrapping = TextWrapping.Wrap, Foreground = ModernPalette.Brush(ModernPalette.Muted), Margin = new Thickness(0, 4, 0, 10) });
            Button github = DialogButton("OPEN PROJECT ON GITHUB", false); github.Margin = new Thickness(0, 0, 0, 4); github.Click += delegate { OpenUrl(ProjectUrl); }; info.Children.Add(github);
            content.Children.Add(info);
            Border support = new Border { CornerRadius = new CornerRadius(16), BorderBrush = ModernPalette.Brush(ModernPalette.Border), BorderThickness = new Thickness(1), Background = ModernPalette.Brush("#E80D1835"), Padding = new Thickness(16), Margin = new Thickness(0, 6, 0, 10) };
            StackPanel supportContent = new StackPanel();
            supportContent.Children.Add(new TextBlock { Text = "SUPPORT THE PROJECT", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = ModernPalette.Brush(ModernPalette.Cyan), HorizontalAlignment = HorizontalAlignment.Center });
            Image qr = new Image { Source = LoadImage(Embedded.Bytes("UTL.support-qr.png")), Height = 240, Stretch = Stretch.Uniform, Margin = new Thickness(8, 12, 8, 10), Cursor = Cursors.Hand };
            qr.MouseLeftButtonUp += delegate { OpenUrl(SupportUrl); }; supportContent.Children.Add(qr);
            supportContent.Children.Add(new TextBlock { Text = "Support is optional. Scan the QR code or open the secure Stripe payment page.", TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Foreground = ModernPalette.Brush(ModernPalette.Muted), Margin = new Thickness(4, 0, 4, 12) });
            Button stripe = DialogButton("SUPPORT VIA STRIPE", true); stripe.Click += delegate { OpenUrl(SupportUrl); }; supportContent.Children.Add(stripe);
            support.Child = supportContent; Grid.SetColumn(support, 1); content.Children.Add(support);
            Grid.SetRow(content, 1); layout.Children.Add(content);
            Button close = DialogButton("CLOSE", false); close.Width = 150; close.HorizontalAlignment = HorizontalAlignment.Right; close.Margin = new Thickness(0, 10, 0, 0); close.Click += delegate { Close(); }; Grid.SetRow(close, 2); layout.Children.Add(close);
        }

        private static BitmapImage LoadImage(byte[] bytes)
        {
            BitmapImage image = new BitmapImage();
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
            }
            return image;
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch (Exception ex)
            {
                ModernMessageDialog error = new ModernMessageDialog("Universal Test Lab", "Could not open the link.\n\n" + url + "\n\n" + ex.Message, "CLOSE", null, true) { Owner = Owner };
                error.ShowDialog();
            }
        }
    }

    internal static class ModernUi
    {
        public static void Run()
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new ModernMainWindow());
        }

        public static void RenderMain(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderMainMaximized(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow { Width = 1920, Height = 1040, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderMainKind(string path, string kind)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.SelectVehicleKindForScreenshot(kind);
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderGroundPreset(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.ShowGroundPresetForScreenshot();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderMessage(string path, bool danger)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.ShowMessageForScreenshot(danger);
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderWeaponScrollbar(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.EnableInjectionForScreenshot();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderSettings(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            Aircraft sample = new Aircraft { Id = "ef_2000_aesa", Display = "EF-2000 Typhoon (AESA)", Kind = "Aircraft", Nation = "Great Britain", Rank = 9 };
            List<AircraftModification> sampleMods = new List<AircraftModification>();
            foreach (string line in Embedded.Text("UTL.modifications.tsv").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = line.Split('\t'); int tier;
                if (p.Length >= 7 && p[0].Equals(sample.Id, StringComparison.OrdinalIgnoreCase) && Int32.TryParse(p[3], out tier))
                    sampleMods.Add(new AircraftModification { AircraftId = p[0], Id = p[1], Display = p[2], Tier = tier, ModClass = p[4], Group = p[5], Requires = p[6] });
            }
            ModernFlightSystemsWindow window = new ModernFlightSystemsWindow(sample, sampleMods, new AircraftSettings(), false);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = 0;
            window.Top = 0;
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            window.Close();
            app.Shutdown();
        }

        public static void RenderGroundConfigure(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            Aircraft sample = new Aircraft
            {
                Id = "us_m1a2_sep2_abrams", Display = "M1A2 SEP V2", Kind = "Ground Vehicle", Nation = "USA", Rank = 8,
                MainWeaponBlk = "gameData/Weapons/groundModels_weapons/120mm_M256_M1A3_user_cannon.blk", MaxAmmo = 42,
                NativeMass = 54000, NativeEnginePower = 1519, NativeForwardSpeed = 75, NativeReverseSpeed = 10, NativeReloadSeconds = 5, NativeRecoil = 0.5
            };
            List<GroundAmmo> ammo = new List<GroundAmmo>();
            foreach (string line in Embedded.Text("UTL.ground_ammo.tsv").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = line.Split('\t');
                if (p.Length < 8 || !p[0].Equals(sample.MainWeaponBlk, StringComparison.OrdinalIgnoreCase)) continue;
                ammo.Add(new GroundAmmo { SourceBlk = p[0], BulletName = p[1], Display = p[2], Type = p[3], Mass = MainForm.ParseNumber(p[4]), Speed = MainForm.ParseNumber(p[5]), ExplosiveMass = MainForm.ParseNumber(p[6]), Caliber = MainForm.ParseNumber(p[7]), Penetration = p.Length > 8 ? MainForm.ParseNumber(p[8]) : 0 });
            }
            AircraftSettings settings = new AircraftSettings();
            if (ammo.Count > 0) settings.GroundAmmoLoadouts.Add(new GroundAmmoLoadout { Slot = 0, Count = 31, SourceBlk = ammo[0].SourceBlk, BulletName = ammo[0].BulletName });
            if (ammo.Count > 1) settings.GroundAmmoLoadouts.Add(new GroundAmmoLoadout { Slot = 1, Count = 9, SourceBlk = ammo[1].SourceBlk, BulletName = ammo[1].BulletName });
            ModernGroundConfigureWindow window = new ModernGroundConfigureWindow(sample, settings, ammo);
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = 0; window.Top = 0; window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path); window.Close(); app.Shutdown();
        }

        public static void RenderFlightConfigure(string path) { RenderFlightConfigure(path, false); }

        public static void RenderFlightConfigureBottom(string path) { RenderFlightConfigure(path, true); }

        private static void RenderFlightConfigure(string path, bool bottom)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            Aircraft sample = new Aircraft { Id = "ef_2000_aesa", Display = "EF-2000 Typhoon (AESA)", Kind = "Aircraft", Nation = "Great Britain", Rank = 9 };
            List<CountermeasureLauncher> launchers = new List<CountermeasureLauncher>
            {
                new CountermeasureLauncher { Key = "emtr_flare1", Display = "INTERNAL COUNTERMEASURE DISPENSER", NativeRounds = 32, AllowsFlares = true, AllowsChaff = true },
                new CountermeasureLauncher { Key = "emtr_flare3", Display = "BOL COUNTERMEASURE DISPENSER", NativeRounds = 160, AllowsFlares = true, AllowsChaff = false }
            };
            List<AircraftModification> beltMods = new List<AircraftModification>
            {
                new AircraftModification { AircraftId = sample.Id, Id = "bk_27_air_targets", Display = "Air targets", Tier = 0 },
                new AircraftModification { AircraftId = sample.Id, Id = "bk_27_ground_targets", Display = "Ground targets", Tier = 0 },
                new AircraftModification { AircraftId = sample.Id, Id = "bk_27_stealth", Display = "Stealth", Tier = 0 },
                new AircraftModification { AircraftId = sample.Id, Id = "bk_27_belt_pack", Display = "BK 27 Belt Pack", Tier = 1 }
            };
            ModernFlightConfigureWindow window = new ModernFlightConfigureWindow(sample, new AircraftSettings { FullFuel = false, FuelMinutes = 30, OverrideCountermeasures = true }, launchers, beltMods);
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = 0; window.Top = 0; window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            if (bottom) { window.ScrollToEndForScreenshot(); window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
            RenderWindow(window, path); window.Close(); app.Shutdown();
        }

        public static void RenderMap(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            AircraftView air = new AircraftView(new Aircraft { Id = "j_10c", Display = "J-10C", Nation = "China", Kind = "Aircraft", Rank = 9 });
            TargetView ground = new TargetView(new TargetUnit { Id = "ussr_bmpt", Display = "BMPT" });
            TargetView ship = new TargetView(new TargetUnit { Id = "jp_battleship_yamato", Display = "Yamato-class, IJN Yamato, 1945" });
            CombinedMap map = new CombinedMap { Id = "western_europe", Display = "Western Europe", Level = "levels/avg_western_europe.bin" };
            map.Spawns.Add(new CombinedSpawn { Kind = "aircraft", Side = 1, Option = "airfield", Label = "Airfield" });
            map.Spawns.Add(new CombinedSpawn { Kind = "aircraft", Side = 1, Option = "air", Label = "Air spawn" });
            ModernMapWindow window = new ModernMapWindow(new[] { air }, new[] { ground }, new[] { ship }, air, 1, new[] { ground }, true, ship, 1, false,
                new[] { map }, "aircraft", new CombinedScenarioSettings { Enabled = true, MapId = map.Id, Side = 1, SpawnOption = "airfield" });
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = 0; window.Top = 0; window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path); window.Close(); app.Shutdown();
        }

        public static void RenderGenerated(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMissionGeneratedWindow window = new ModernMissionGeneratedWindow(); window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = 0; window.Top = 0; window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path); window.Close(); app.Shutdown();
        }

        public static void RenderAbout(string path)
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            ModernAboutWindow about = new ModernAboutWindow(2817, 1838) { Owner = window };
            window.ShowOverlay(about);
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            RenderWindow(window, path);
            about.Close();
            window.Close();
            app.Shutdown();
        }

        public static void SelfTest()
        {
            DwmGlass.EnablePerMonitorDpi();
            System.Windows.Application app = new System.Windows.Application();
            ModernMainWindow window = new ModernMainWindow();
            window.Show();
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.ExerciseDropdownForSelfTest();
            if (!window.LayoutFixesReadyForSelfTest())
                throw new InvalidOperationException("WPF clipping/dropdown/work-area self-test failed.");
            if (!window.CombinedCatalogReadyForSelfTest())
                throw new InvalidOperationException("WPF combined-battles map catalog self-test failed.");
            if (!window.ExerciseOverlayForSelfTest())
                throw new InvalidOperationException("WPF single-window overlay self-test failed.");

            Aircraft modulesVehicle = new Aircraft { Id = "selftest_modules", Display = "Self-test Vehicle", Kind = "Aircraft", Nation = "USA", Rank = 1 };
            ModernFlightSystemsWindow modules = new ModernFlightSystemsWindow(modulesVehicle,
                new[] { new AircraftModification { AircraftId = modulesVehicle.Id, Id = "engine", Display = "Engine", Tier = 1 } },
                new AircraftSettings(), false);
            if (!modules.ModulesCardReadyForSelfTest())
                throw new InvalidOperationException("WPF Modules glass-card self-test failed.");

            Aircraft groundVehicle = new Aircraft { Id = "selftest_ground", Display = "M1A2 SEP V3", Kind = "Ground Vehicle", Nation = "USA", Rank = 8, MaxAmmo = 42, MainWeaponBlk = "selftest.blk", NativeReloadSeconds = 5 };
            GroundAmmo groundRound = new GroundAmmo { SourceBlk = "selftest.blk", BulletName = "round", Display = "Test Round", Type = "APFSDS", Mass = 5, Speed = 1500 };
            AircraftSettings groundSettings = new AircraftSettings();
            groundSettings.GroundAmmoLoadouts.Add(new GroundAmmoLoadout { Slot = 0, Count = 31, SourceBlk = groundRound.SourceBlk, BulletName = groundRound.BulletName });
            groundSettings.GroundAmmoLoadouts.Add(new GroundAmmoLoadout { Slot = 1, Count = 9, SourceBlk = groundRound.SourceBlk, BulletName = groundRound.BulletName });
            ModernGroundConfigureWindow groundConfigure = new ModernGroundConfigureWindow(groundVehicle, groundSettings, new[] { groundRound });
            if (!groundConfigure.AmmoSlidersStableForSelfTest())
                throw new InvalidOperationException("WPF ground-ammunition slider self-test failed.");

            window.WindowState = WindowState.Maximized;
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Rect selfTestWorkArea = SystemParameters.WorkArea;
            double requiredMaxWidth = Math.Min(1100, selfTestWorkArea.Width);
            double requiredMaxHeight = Math.Min(600, selfTestWorkArea.Height);
            if (window.WindowState != WindowState.Maximized || window.ActualWidth + 1 < requiredMaxWidth || window.ActualHeight + 1 < requiredMaxHeight)
                throw new InvalidOperationException("WPF maximize/layout self-test failed.");
            window.WindowState = WindowState.Normal;
            window.Width = 1500;
            window.Height = 920;
            window.Dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            double requiredRestoreWidth = Math.Min(1200, Math.Max(960, selfTestWorkArea.Width - 24));
            double requiredRestoreHeight = Math.Min(640, Math.Max(560, selfTestWorkArea.Height - 24));
            if (window.WindowState != WindowState.Normal || window.ActualWidth + 1 < requiredRestoreWidth || window.ActualHeight + 1 < requiredRestoreHeight)
                throw new InvalidOperationException("WPF restore/layout self-test failed.");
            ModernAboutWindow about = new ModernAboutWindow(2817, 1838);
            about.Close();
            window.Close();
            app.Shutdown();
            Console.WriteLine("UISELFTEST OK wpf=yes custom-chrome=yes no-client-gap=yes dark-glass=yes dark-dropdowns=yes virtualized-weapons=yes rounded-preview=yes vehicle-kind-previews=yes border-retention=yes weapon-table-fit=yes station-order=yes stations-one-row=yes vertical-scroll=yes single-window-overlays=yes styled-messages=yes solid-close=yes visible-game-path=yes blurred-background=yes work-area-fit=yes maximize-restore=yes dpi-aware=yes");
        }

        internal static void RenderWindow(Window window, string path)
        {
            int width = Math.Max(1, (int)Math.Round(window.ActualWidth));
            int height = Math.Max(1, (int)Math.Round(window.ActualHeight));
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        internal static void RenderWindow(ModernDialogWindow dialog, string path)
        {
            int width = Math.Max(1, (int)Math.Round(dialog.ActualWidth));
            int height = Math.Max(1, (int)Math.Round(dialog.ActualHeight));
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(dialog);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
