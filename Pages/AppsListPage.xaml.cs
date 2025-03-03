﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinDurango.UI.Controls;
using WinDurango.UI.Dialogs;
using WinDurango.UI.Settings;
using WinDurango.UI.Utils;
using static WinDurango.UI.Localization.Locale;

namespace WinDurango.UI.Pages
{
    public sealed partial class AppsListPage : Page
    {
        public async Task InitAppListAsync()
        {
            appList.Children.Clear();
            SwitchScrollDirection(App.Settings.Settings.AppViewIsHorizontalScrolling);

            List<installedPackage> installedPackages = await Task.Run(() => App.InstalledPackages.GetPackages());
            PackageManager pm = new();

            foreach (installedPackage installedPackage in installedPackages)
            {
                if (this.SearchBox.Text.Length > 0)
                {
                    // Maybe we should at some point save the package Name/DisplayName to installedPackage model too? to skip this step
                    Package pk = Packages.GetPackageByFamilyName(installedPackage.FamilyName);
                    if (pk != null)
                    {
                        string searchMatch = "";
                        try
                        {
                            searchMatch = pk.DisplayName ?? pk.Id.Name;
                        }
                        catch
                        {
                            searchMatch = pk.Id.Name;
                        }
                        if (searchMatch.Contains(this.SearchBox.Text, StringComparison.InvariantCultureIgnoreCase) == false) continue;
                    }
                }

                // TODO: add handling for that annoying invalid logo stuff
                if (pm.FindPackageForUser(WindowsIdentity.GetCurrent().User?.Value, installedPackage.FullName) != null)
                {
                    try
                    {
                        AppTile gameContainer = new(installedPackage.FamilyName);

                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            appList.Children.Add(gameContainer);
                        });

                        Logger.WriteDebug($"Added {installedPackage.FamilyName} to the app list");
                    }
                    catch (Exception ex)
                    {
                        // maybe should have infobar on app list?
                        Logger.WriteError($"Failed to add {installedPackage.FamilyName} to the app list: {ex.Message}");
                        Logger.WriteException(ex);
                    }
                }
                else
                {
                    Logger.WriteError($"Couldn't find package {installedPackage.FullName} in installed UWP packages list");
                }
            }
        }

        private async void ShowAppListView(object sender, RoutedEventArgs e)
        {
            List<Windows.ApplicationModel.Package> uwpApps = Packages.GetInstalledPackages().ToList();
            if (uwpApps.Count <= 0)
            {
                NoticeDialog dialog = new NoticeDialog("No UWP Apps have been found.");
                await dialog.ShowAsync();
                return;
            }
            AppListDialog dl = new(uwpApps, true);
            dl.Title = "Installed UWP apps";
            dl.XamlRoot = this.Content.XamlRoot;
            await dl.ShowAsync();
        }

        private async void ShowInstalledEraApps(object sender, RoutedEventArgs e)
        {
            List<Windows.ApplicationModel.Package> eraApps = XHandler.GetXPackages(Packages.GetInstalledPackages().ToList());
            if (eraApps.Count <= 0)
            {
                NoticeDialog dialog = new NoticeDialog("No Era/XUWP Apps have been found.");
                await dialog.ShowAsync();
                return;
            }
            AppListDialog dl = new(eraApps, true);
            dl.Title = "Installed Era/XUWP apps";
            dl.XamlRoot = this.Content.XamlRoot;
            await dl.ShowAsync();
        }

        public void SwitchScrollDirection(bool horizontal)
        {
            if (horizontal)
            {
                scrollView.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                appList.Orientation = Orientation.Vertical;
                appList.VerticalAlignment = VerticalAlignment.Center;
            } else
            {
                scrollView.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                appList.Orientation = Orientation.Horizontal;
                appList.VerticalAlignment = VerticalAlignment.Top;
            }
        }

        private void UpdateCheckboxes(object sender, RoutedEventArgs e)
        {
            if (autoSymlinkCheckBox == null || addToAppListCheckBox == null)
                return;

            autoSymlinkCheckBox.IsEnabled = (bool)addToAppListCheckBox.IsChecked;
        }

        public AppsListPage()
        {
            InitializeComponent();
            _ = InitAppListAsync();

            // All this is useless now basically... because InitAppListAsync now runs asynchronously (not blocking the ui thread)
            /*
            Stopwatch PlatinumWatch = new Stopwatch();
            Logger.WriteDebug("Initializing AppsListPage...");
            PlatinumWatch.Start();
            PlatinumWatch.Stop();
            Logger.WriteDebug("Initialized AppsListPage in {0:D2}:{1:D2}:{2:D2}.{3:D3}", (int)PlatinumWatch.Elapsed.TotalHours, (int)PlatinumWatch.Elapsed.TotalMinutes, (int)PlatinumWatch.Elapsed.TotalSeconds, (int)PlatinumWatch.Elapsed.TotalMilliseconds);
            */
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            _ = InitAppListAsync();
        }

        // needs to be cleaned
        private async void InstallButton_Tapped(SplitButton sender, SplitButtonClickEventArgs args)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder != null)
            {
                string manifest = Path.Combine(folder.Path + "\\AppxManifest.xml");
                string mountFolder = Path.Combine(folder.Path + "\\Mount");

                if (File.Exists(manifest))
                {
                    var dialog = new InstallConfirmationDialog(manifest);
                    dialog.PrimaryButtonClick += async (sender, e) =>
                    {
                        dialog.Hide();
                        var controller = new ProgressDialog("Starting installation...", $"Installing {Packages.GetPropertiesFromManifest(manifest).DisplayName}", isIndeterminate: false).GetController();
                        _ = controller.CreateAsync(async () =>
                        {
                            await Packages.InstallPackageAsync(new Uri(manifest, UriKind.Absolute), controller,
                                (bool)addToAppListCheckBox.IsChecked);
                        });
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    // AppxManifest does not exist in that folder
                    if (Directory.Exists(mountFolder))
                    {
                        // there IS a mount folder
                        if (File.Exists(Path.Combine(mountFolder + "\\AppxManifest.xml")))
                        {
                            var dialog = new InstallConfirmationDialog(Path.Combine(mountFolder + "\\AppxManifest.xml"));
                            dialog.PrimaryButtonClick += async (sender, e) =>
                            {
                                dialog.Hide();
                                var controller = new ProgressDialog("Starting installation...", "Installing", isIndeterminate: false).GetController();
                                _ = controller.CreateAsync(async () =>
                                {
                                    await Packages.InstallXPackageAsync(folder.Path.ToString(), controller,
                                        (bool)addToAppListCheckBox.IsChecked);
                                });
                            };
                            await dialog.ShowAsync();
                        }
                        else
                        {
                            // there is no AppxManifest inside.
                            Logger.WriteError($"Could not find AppxManifest.xml in {folder.Path} and {mountFolder}");
                            await new NoticeDialog(GetLocalizedText("/Packages/ManifestNotFoundMulti", folder.Path, mountFolder), "Error").ShowAsync();
                        }
                    }
                    else
                    {
                        Logger.WriteError($"Could not find AppxManifest.xml in {folder.Path} and no Mount folder exists");
                        await new NoticeDialog(GetLocalizedText("/Packages/ManifestNotFoundNoMount", folder.Path), "Error").ShowAsync();
                    }

                    return;
                }
            }
        }
    }
}
