// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using GifBolt.Internal;

namespace GifBolt.SampleApp
{
    /// <summary>
    /// Sample WPF application demonstrating GifBolt usage.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Gets the GIF file path provided as a command-line argument, if any.
        /// </summary>
        public static string CommandLineGifPath { get; private set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Sets up the DLL search path before any dependencies are loaded.
        /// </summary>
        public App()
        {
            // Extract GIF path from command-line arguments if provided
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var potentialPath = args[1];
                if (File.Exists(potentialPath))
                {
                    CommandLineGifPath = potentialPath;
                    LogToFile($"Command-line GIF path: {potentialPath}");
                }
                else
                {
                    LogToFile($"Command-line argument provided but file not found: {potentialPath}");
                }
            }

            // Hook global exception handlers for debugging
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogToFile($"DispatcherUnhandledException: {e.Exception}");
                MessageBox.Show($"Dispatcher Exception:\n{e.Exception.Message}\n\n{e.Exception.GetType().FullName}\n\nSee gifbolt_load.log", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogToFile($"UnhandledException: {e.ExceptionObject}");
                if (e.ExceptionObject is Exception ex)
                {
                    MessageBox.Show($"Fatal Exception:\n{ex.Message}\n\n{ex.GetType().FullName}\n\nSee gifbolt_load.log", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
            };

            // Add the application directory to PATH so P/Invoke can find GifBolt.Native.dll
            // This MUST happen before any assemblies that use P/Invoke are loaded
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            // Ensure the native DLL directory is part of the Windows loader search path
            // This is more reliable than modifying PATH at runtime for .NET Framework
            try
            {
                SetDllDirectory(appDir);
            }
            catch
            {
                // ignore; we'll still update PATH as fallback
            }
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!currentPath.Contains(appDir))
            {
                Environment.SetEnvironmentVariable("PATH", appDir + ";" + currentPath);
            }
        }

        private static void LogToFile(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gifbolt_load.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}
