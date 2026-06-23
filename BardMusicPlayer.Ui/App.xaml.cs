/*
 * Copyright(c) 2026 GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using System.Windows;
using BardMusicPlayer.Coffer;
using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Seer;
using BardMusicPlayer.Maestro;
using System.Diagnostics;
using BardMusicPlayer.Siren;
using BardMusicPlayer.Jamboree;
using BardMusicPlayer.Script;
using BardMusicPlayer.XIVMIDI;
using System.Globalization;
using System;

namespace BardMusicPlayer.Ui
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public sealed partial class App : Application
    {
        public static string TempPath { get; } = System.IO.Path.GetTempPath() + "LightAmp\\";

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            SplashScreen splashScreen = new SplashScreen("/Resources/Images/splash.jpg");
            splashScreen.Show(true);

            Globals.Globals.DataPath = @"data\";

            //init pigeon at first
            BmpPigeonhole.Initialize(Globals.Globals.DataPath + @"\Configuration.json");

            // LogManager.Initialize(new(view.Log));

            //Load the last used catalog
            string CatalogFile = BmpPigeonhole.Instance.LastLoadedCatalog;
            if (System.IO.File.Exists(CatalogFile))
                BmpCoffer.Initialize(CatalogFile);
            else
                BmpCoffer.Initialize(Globals.Globals.DataPath + @"\MusicCatalog.db");

            //Setup seer
            BmpSeer.Instance.SetupFirewall("BardMusicPlayer");
            //Start meastro before seer, else we'll not get all the players
            BmpMaestro.Instance.Start();
            //Start seer
            BmpSeer.Instance.Start();

            DalamudBridge.DalamudBridge.Instance.Start();

            //Start the scripting
            BmpScript.Instance.Start();

            BmpSiren.Instance.Setup();
            XIVMidiApi.Instance.Start();
            BmpJamboree.Instance.Start();
            ConfigureLanguage(System.Threading.Thread.CurrentThread.CurrentUICulture.ToString());
        }

        private static void App_DispatcherUnhandledException(
            object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            WriteStartupError(
                "DispatcherUnhandledException",
                e.Exception);

            MessageBox.Show(
                "LightAmpRedux could not finish loading.\n\n"
                + "A diagnostic log was written to:\n"
                + GetStartupLogPath()
                + "\n\n"
                + e.Exception.Message,
                "LightAmpRedux startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Allow WPF to terminate rather than continue with a partly
            // constructed interface.
            e.Handled = false;
        }

        private static void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            WriteStartupError(
                "UnhandledException",
                e.ExceptionObject as Exception);
        }

        private static string GetStartupLogPath()
        {
            return System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "LightAmpRedux-startup.log");
        }

        private static void WriteStartupError(
            string source,
            Exception exception)
        {
            string report =
                DateTime.Now.ToString("O")
                + Environment.NewLine
                + source
                + Environment.NewLine
                + (exception == null
                    ? "Unknown startup failure."
                    : exception.ToString())
                + Environment.NewLine
                + new string('-', 72)
                + Environment.NewLine;

            try
            {
                System.IO.File.AppendAllText(
                    GetStartupLogPath(),
                    report);
            }
            catch
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(),
                            "LightAmpRedux-startup.log"),
                        report);
                }
                catch
                {
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            XIVMidiApi.Instance.Stop();
            //LogManager.Shutdown();
            BmpJamboree.Instance.Stop();
            if (BmpSiren.Instance.IsReadyForPlayback)
                BmpSiren.Instance.Stop();
            BmpSiren.Instance.ShutDown();
            BmpMaestro.Instance.Stop();

            BmpScript.Instance.Stop();

            DalamudBridge.DalamudBridge.Instance.Stop();
            BmpSeer.Instance.Stop();
            BmpSeer.Instance.DestroyFirewall("BardMusicPlayer");
            BmpCoffer.Instance.Dispose();
            BmpPigeonhole.Instance.Dispose();

            //Wasabi hangs kill it with fire
            Process.GetCurrentProcess().Kill();
        }
        internal static void ConfigureLanguage(string langCode = null)
        {
            try
            {
                Locales.Language.Culture = new CultureInfo(langCode);
            }
            catch (Exception)
            {
                Locales.Language.Culture = CultureInfo.DefaultThreadCurrentUICulture;
            }
        }
    }
}
