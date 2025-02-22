using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinDurango.UI.Settings;
using WinDurango.UI.Utils;


namespace WinDurango.UI.Pages.Settings
{
    public sealed partial class UiSettings : Page
    {
        public UiSettings()
        {
            this.InitializeComponent();
                        
            ComboBoxItem psbSelected = PatchSourceButton.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == App.Settings.Settings.DownloadSource.ToString());
            if (psbSelected != null && (ComboBoxItem)PatchSourceButton.SelectedItem != psbSelected)
            {
                PatchSourceButton.SelectedItem = psbSelected;
            }
            
            ComboBoxItem themeSelected = themeButton.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == App.Settings.Settings.Theme.ToString());
            if (themeSelected != null && (ComboBoxItem)themeButton.SelectedItem != themeSelected)
            {
                themeButton.SelectedItem = themeSelected;
            }
        }
        
        private void OnThemeSelected(object sender, RoutedEventArgs e)
        {
            if (themeButton.SelectedItem is not ComboBoxItem sel)
            {
                return;
            }

            if (!Enum.TryParse(sel.Tag.ToString(), out UiConfigData.ThemeSetting theme))
            {
                return;
            }
            
            if (App.Settings.Settings.Theme == theme)
            {
                return;
            }

            App.Settings.Set("Theme", theme);
        }
        
        private async void OnSourceSelected(object sender, RoutedEventArgs e)
        {
            if (PatchSourceButton.SelectedItem is not ComboBoxItem sel)
            {
                return;
            }
            
            if (!Enum.TryParse(sel.Tag.ToString(), out UiConfigData.PatchSource source))
            {
                return;
            }
                
            if (App.Settings.Settings.DownloadSource == source)
            {
                return;
            }
            
            PatchSourceButton.SelectedItem = sel.Content;
            App.Settings.Set("DownloadSource", source);
        }

        private async void OnDebugLogToggled(object sender, RoutedEventArgs e)
        {
            App.Settings.Set("DebugLoggingEnabled", ((ToggleSwitch)sender).IsOn);
        }

        private void OpenAppData(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(App.DataDir) { UseShellExecute = true });
        }

        public void OnToggleSetting(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && toggleSwitch.Tag is string settingName)
            {
                App.Settings.Set(settingName, toggleSwitch.IsOn);
            }
        }
        
        private void OnDebugLogToggleLoaded(object sender, RoutedEventArgs e)
        {
            if (!Debugger.IsAttached || ((ToggleSwitch)sender).IsEnabled)
            {
                return;
            }

            ((ToggleSwitch)sender).IsEnabled = false;
            ((ToggleSwitch)sender).IsOn = true;
            ((ToggleSwitch)sender).OnContent = "Enable debug logging (currently debugging)";
        }
    }
}
