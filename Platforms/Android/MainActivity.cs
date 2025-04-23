using System;
using System.Linq;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Extensions.DependencyInjection;  // для GetService<>
using Microsoft.Maui.Controls;                  // для Application.Current
using Microsoft.Maui.Platform;                  // для Handler.Extensions.MauiContext
using Plugin.BLE;
using MauiApp1;                                  // ваш неймспейс

namespace MauiApp1
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize
                             | ConfigChanges.Orientation
                             | ConfigChanges.UiMode
                             | ConfigChanges.ScreenLayout
                             | ConfigChanges.SmallestScreenSize
                             | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        internal const int REQUEST_BLE_PERMS = 1001;

        public override void OnRequestPermissionsResult(
            int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            
        }
    }
}
