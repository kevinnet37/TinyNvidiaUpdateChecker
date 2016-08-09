﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using HtmlAgilityPack;

namespace TinyNvidiaUpdateChecker
{

    /*
    TinyNvidiaUpdateChecker - Check for NVIDIA GPU drivers, GeForce Experience replacer
    Copyright (C) 2016 Hawaii_Beach

    This program Is free software: you can redistribute it And/Or modify
    it under the terms Of the GNU General Public License As published by
    the Free Software Foundation, either version 3 Of the License, Or
    (at your option) any later version.

    This program Is distributed In the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty Of
    MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License For more details.

    You should have received a copy Of the GNU General Public License
    along with this program.  If Not, see <http://www.gnu.org/licenses/>.
    */

    class MainConsole
    {

        /// <summary>
        /// Server adress
        /// </summary>
        private readonly static string serverURL = "https://elpumpo.github.io/TinyNvidiaUpdateChecker/";

        /// <summary>
        /// Current client version
        /// </summary>
        private static string offlineVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        
        /// <summary>
        /// Remote client version
        /// </summary>
        private static int onlineVer;

        /// <summary>
        /// Current GPU driver version
        /// </summary>
        private static int offlineGPUDriverVersion;

        /// <summary>
        /// Remote GPU driver version
        /// </summary>
        private static int onlineGPUDriverVersion;

        /// <summary>
        /// Langauge ID for GPU driver download
        /// </summary>
        private static int langID;

        private static string downloadURL;
        private static string savePath;

        /// <summary>
        /// Local Windows version
        /// </summary>
        private static string winVer;

        /// <summary>
        /// OS ID for GPU driver download
        /// </summary>
        private static int osID;

        /// <summary>
        /// Show UI or go quiet mode
        /// </summary>
        public static bool showUI = true;

        /// <summary>
        /// Enable extended information
        /// </summary>
        private static bool debug = false;

        /// <summary>
        /// Direction for configuration folder, blueprint: <local-appdata><author><project-name>
        /// </summary>
        private static string dirToConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).CompanyName, FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductName);
        
        public static string fullConfig = Path.Combine(dirToConfig, "app.config");


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeConsole();

        [STAThread]
        private static void Main(string[] args)
        {
            string message = "TinyNvidiaUpdateChecker v" + offlineVer;
            LogManager.log(message, 1);
            Console.Title = message;

            /// The command line argument handler does its work here,
            /// for a list of available arguments, use the '--help' argument.
            
            string[] parms = Environment.GetCommandLineArgs();
            int isSet = 0;

            if (parms.Length > 1) {
                
                // go quiet mode
                if (Array.IndexOf(parms, "--quiet") != -1) {
                    FreeConsole();
                    showUI = false;
                    isSet = 1;
                }

                // erase config
                if (Array.IndexOf(parms, "--eraseConfig") != -1) {
                    isSet = 1;
                    if (File.Exists(fullConfig)) {
                        File.Delete(fullConfig);
                    }
                }

                // enable debug
                if (Array.IndexOf(parms, "--debug") != -1) {
                    isSet = 1;
                    debug = true;
                }

                // help menu
                if (Array.IndexOf(parms, "--help") != -1) {
                    isSet = 1;
                    introMessage();
                    Console.WriteLine("Usage: " + Path.GetFileName(Assembly.GetEntryAssembly().Location) + " [--quiet] [--eraseConfig] [--debug] [--help]");
                    Console.WriteLine();
                    Console.WriteLine("--quiet        Run application quiet.");
                    Console.WriteLine("--eraseConfig  Erase local configuration file.");
                    Console.WriteLine("--debug        Enable debugging for extended information.");
                    Console.WriteLine("--help         Displays this message.");
                    Environment.Exit(0);
                }

                if (isSet == 0) {
                    introMessage();
                    Console.WriteLine("Unknown command, type --help for help.");
                    Environment.Exit(1);
                }

            }
            if (showUI == true) AllocConsole();

            introMessage();

            checkDll();

            configInit();

            checkWinVer();

            getLanguage();
            
            bool set = false;
            string key = "Check for Updates";

            while (set == false) {
                string val = SettingManager.readSetting(key); // refresh value each time

                if (val == "true") {
                    searchForUpdates();
                    set = true;
                } else if (val == "false") {
                    set = true; // leave loophole
                } else {
                    // invalid value
                    SettingManager.setupSetting(key);
                }   
            }

            gpuInfo();

            if (onlineGPUDriverVersion == offlineGPUDriverVersion) {
                Console.WriteLine("Your GPU drivers are up-to-date!");
            } else {
                if (offlineGPUDriverVersion > onlineGPUDriverVersion) {
                    Console.WriteLine("Your current GPU driver is newer than remote!");}
                if (onlineGPUDriverVersion < offlineGPUDriverVersion) {
                    Console.WriteLine("Your GPU drivers are up-to-date!");
                } else {
                    Console.WriteLine("There are new drivers available to download!");
                    DialogResult dialog = MessageBox.Show("There is a new update available to download, do you want to download the update?", "TinyNvidiaUpdateChecker", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (dialog == DialogResult.Yes) {
                        
                        Console.WriteLine();

                        // @todo error handling could be better:
                        // isolate saveFileDialog errors with accually downloading GPU driver

                        // @todo add status bar for download progress

                        bool error = false;
                        try {
                            WebClient downloadClient = new WebClient();

                            string driverName = downloadURL.Split('/').Last();

                            // set attributes
                            SaveFileDialog saveFileDialog = new SaveFileDialog();
                            saveFileDialog.Filter = "Executable|*.exe";
                            saveFileDialog.Title = "Choose save file for GPU driver";
                            saveFileDialog.FileName = driverName;

                            DialogResult result = saveFileDialog.ShowDialog(); // show dialog and get status (will wait for input)

                            switch (result) {
                                case DialogResult.OK:
                                    savePath = saveFileDialog.FileName.ToString();
                                    break;

                                default:
                                    // if something went wrong, fall back to temp folder
                                    savePath = Path.GetTempPath() + driverName;
                                    break;
                            }

                            if (debug == true) {
                                Console.WriteLine("savePath: " + savePath);
                                Console.WriteLine("result: " + result);
                            }

                            Console.Write("Downloading driver file . . . ");
                            
                            downloadClient.DownloadFile(downloadURL, savePath);

                        } catch (Exception ex) {
                            error = true;
                            Console.Write("ERROR!");
                            LogManager.log(ex.Message, 2);
                            Console.WriteLine();
                            Console.WriteLine(ex.Message);
                            Console.WriteLine();
                        }

                        if (error == false)
                        {
                            Console.Write("OK!");
                        }

                        Console.WriteLine();
                        Console.WriteLine("The downloaded file has been saved at: " + savePath);

                        DialogResult dialog2 = MessageBox.Show("Do you wish to run the driver installer?", "TinyNvidiaUpdateChecker", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (dialog2 == DialogResult.Yes) {
                            Process.Start(savePath);
                        }
                    }
                }
            }

            Console.WriteLine();
            
            Console.WriteLine("Job done! Press any key to exit.");
            if (showUI == true) Console.ReadKey();
            LogManager.log("BYE!", 1);
            Environment.Exit(0);
        }

        /// <summary>
        /// Initialize configuration manager
        /// </summary>
        public static void configInit()
        {
            // powered by the .NET framework "Settings" function

            // set config dir
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", fullConfig);

            if (debug == true) {
                Console.WriteLine("Current configuration file is located at: " + AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
                Console.WriteLine();
            }
            LogManager.log("ConfigDir: " + fullConfig, 1);

            // create config file
            if (!File.Exists(fullConfig)) {
                Console.WriteLine("Generating configuration file, this only happenes once.");
                Console.WriteLine("The configuration file is located at: " + dirToConfig);

                SettingManager.setupSetting("Check for Updates");
                SettingManager.setupSetting("GPU Type");

                Console.WriteLine();
            }

        }

        /// <summary>
        /// Search for client updates
        /// </summary>
        private static void searchForUpdates()
        {
            Console.Write("Searching for Updates . . . ");
            int error = 0;
            try
            {
                HtmlWeb webClient = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument htmlDocument = webClient.Load(serverURL);

                // get version
                HtmlNode tdVer = htmlDocument.DocumentNode.Descendants().SingleOrDefault(x => x.Id == "currentVersion");
                onlineVer = Convert.ToInt32(tdVer.InnerText.Replace(".", string.Empty));

            } catch (Exception ex) {
                error = 1;
                Console.Write("ERROR!");
                LogManager.log(ex.Message, 2);
                Console.WriteLine();
                Console.WriteLine(ex.StackTrace);
            }
            if (error == 0) {
                Console.Write("OK!");
                Console.WriteLine();
            }
            int iOfflineVer = Convert.ToInt32(offlineVer.Replace(".", string.Empty));

            if (onlineVer > iOfflineVer) {
                Console.WriteLine("There is a update available for TinyNvidiaUpdateChecker!");
                DialogResult dialog = MessageBox.Show("There is a new client update available to download, do you want to be navigate to the official GitHub download section?", "TinyNvidiaUpdateChecker", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (dialog == DialogResult.Yes) {
                    Process.Start("https://github.com/ElPumpo/TinyNvidiaUpdateChecker/releases");
                }
            }

            if (debug == true)
            {
                Console.WriteLine("iOfflineVer: " + iOfflineVer);
                Console.WriteLine("onlineVer:   " + onlineVer);
            }
            Console.WriteLine();
        } // checks for application updates

        /// <summary>
        /// Gets the current Windows version and sets important value 'osID'.
        /// </summary>
        /// <seealso cref="gpuInfo"> Used here, decides OS and OS architecture.</seealso>
        private static void checkWinVer()
        {
            string verOrg = Environment.OSVersion.Version.ToString();

            // Windows 10
            if (verOrg.Contains("10.0")) {
                winVer = "10";
                if (Environment.Is64BitOperatingSystem == true) {
                    osID = 57;
                } else {
                    osID = 56;
                }
            }
            // Windows 8.1
            else if (verOrg.Contains("6.3")) {
                winVer = "8.1";
                if (Environment.Is64BitOperatingSystem == true) {
                    osID = 41;
                } else {
                    osID = 40;
                }
            }
            // Windows 8
            else if (verOrg.Contains("6.2")) {
                winVer = "8";
                if (Environment.Is64BitOperatingSystem == true) {
                    osID = 41;
                } else {
                    osID = 40;
                }
            }
            // Windows 7
            else if (verOrg.Contains("6.1")) {
                winVer = "7";
                if (Environment.Is64BitOperatingSystem == true) {
                    osID = 41;
                } else {
                    osID = 40;
                }

            } else {
                winVer = "Unknown";
                string message = "You're running a non-supported version of Windows; the application will determine itself.";

                Console.WriteLine(message);
                Console.WriteLine("verOrg: " + verOrg);
                LogManager.log(message, 2);
                if (showUI == true) Console.ReadKey();
                Environment.Exit(1);
            }

            if (debug == true) {
                Console.WriteLine("winVer: " + winVer);
                Console.WriteLine("osID: " + osID.ToString());
                Console.WriteLine("verOrg: " + verOrg);
                Console.WriteLine();
            }

            LogManager.log("winVer: " + winVer, 1);
            

        }

        /// <summary>
        /// Gets the local langauge used by operator and sets value 'langID'.
        /// </summary>
        /// <seealso cref="gpuInfo"> Used here, decides driver download language and possibly download server.</seealso>
        private static void getLanguage()
        {
            string cultName = CultureInfo.CurrentCulture.ToString(); // https://msdn.microsoft.com/en-us/library/ee825488(v=cs.20).aspx - http://www.lingoes.net/en/translator/langcode.htm

            switch (cultName)
            {
                case "en-US":
                    langID = 1;
                    break;
                case "en-GB":
                    langID = 2;
                    break;
                case "zh-CHS":
                    langID = 5;
                    break;
                case "zh-CHT":
                    langID = 6;
                    break;
                case "ja-JP":
                    langID = 7;
                    break;
                case "ko-KR":
                    langID = 8;
                    break;
                case "de-DE":
                    langID = 9;
                    break;
                case "es-ES":
                    langID = 10;
                    break;
                case "fr-FR":
                    langID = 12;
                    break;
                case "it-IT":
                    langID = 13;
                    break;
                case "pl-PL":
                    langID = 14;
                    break;
                case "pt-BR":
                    langID = 15;
                    break;
                case "ru-RU":
                    langID = 16;
                    break;
                case "tr-TR":
                    langID = 19;
                    break;
                default:
                    // intl
                    langID = 17;
                    break;
            }

            if (debug == true) {
                Console.WriteLine("langID: " + langID);
                Console.WriteLine("cultName: " + cultName);
                Console.WriteLine();
            }
            LogManager.log("langID: " + langID, 1);
        }

        /// <summary>
        /// A lot of things going on inside: gets current gpu driver, fetches latest gpu driver from NVIDIA server and fetches download link for latest drivers.
        /// </summary>
        private static void gpuInfo()
        {
            Console.Write("Looking up GPU information . . . ");
            int error = 0;
            string processURL = null;
            string confirmURL = null;
            string gpuURL = null;

            // query local driver version
            try
            {
                FileVersionInfo nvvsvcExe = FileVersionInfo.GetVersionInfo(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\System32\nvvsvc.exe"); // Sysnative?
                offlineGPUDriverVersion = Convert.ToInt32(nvvsvcExe.FileDescription.Substring(38).Trim().Replace(".", string.Empty));
            } catch (FileNotFoundException ex) {
                error = 1;
                Console.Write("ERROR!");
                LogManager.log(ex.Message, 2);
                Console.WriteLine();
                Console.WriteLine("The required executable is not there! Are you sure you've at least installed NVIDIA GPU drivers once?");

            } catch (Exception ex) {
                error = 1;
                Console.Write("ERROR!");
                LogManager.log(ex.Message, 2);
                Console.WriteLine();
                Console.WriteLine(ex.StackTrace);
            }

            /// In order to proceed, we must input what GPU we have.
            /// Looking at the supported products on NVIDIA website for desktop and mobile GeForce series,
            /// we can see that they're sharing drivers with other GPU families, the only thing we have to do is tell the website
            /// if we're running a mobile or desktop GPU.
            
            int psID = 0;
            int pfID = 0;

            // loop until value is selected by user
            string key = "GPU Type";

            while (psID == 0 && pfID == 0) {
                string val = SettingManager.readSetting(key); // refresh value each time

                /// Get correct gpu drivers:
                /// you do not have to choose the exact GPU,
                /// looking at supported products, we see that the same driver package includes
                /// drivers for the majority GPU family.
                if (val == "desktop") {
                    psID = 98;  // GeForce 900-series
                    pfID = 756; // GTX 970
                } else if (val == "mobile") {
                    psID = 99;  // GeForce 900M-series (M for Mobile)
                    pfID = 758; // GTX 970M
                } else {
                    // invalid value
                    SettingManager.setupSetting(key);
                }
            }

            // finish request
            try
            {
                gpuURL = "http://www.nvidia.com/Download/processDriver.aspx?psid=" + psID.ToString() + "&pfid=" + pfID.ToString() + "&rpf=1&osid=" + osID.ToString() + "&lid=" + langID.ToString() + "&ctk=0";

                WebClient client = new WebClient();
                Stream stream = client.OpenRead(gpuURL);
                StreamReader reader = new StreamReader(stream);
                processURL = reader.ReadToEnd();
                reader.Close();
                stream.Close();
            } catch (Exception ex) {
                if (error == 0) {
                    Console.Write("ERROR!");
                    Console.WriteLine();
                    error = 1;
                }
                Console.WriteLine(ex.StackTrace);
            }

            try
            {
                // HTMLAgilityPack
                // thanks to http://www.codeproject.com/Articles/691119/Html-Agility-Pack-Massive-information-extraction-f for a great article

                HtmlWeb webClient = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument htmlDocument = webClient.Load(processURL);

                // get version
                HtmlNode tdVer = htmlDocument.DocumentNode.Descendants().SingleOrDefault(x => x.Id == "tdVersion");
                onlineGPUDriverVersion = Convert.ToInt32(tdVer.InnerHtml.Trim().Substring(0, 6).Replace(".", string.Empty));

                // get driver URL
                IEnumerable<HtmlNode> links = htmlDocument.DocumentNode.Descendants("a").Where(x => x.Attributes.Contains("href"));
                foreach (var link in links) {
                    if (link.Attributes["href"].Value.Contains("/content/DriverDownload-March2009/")) {
                        confirmURL = "http://www.nvidia.com" + link.Attributes["href"].Value;
                    }
                }

                // get download link
                htmlDocument = webClient.Load(confirmURL);
                links = htmlDocument.DocumentNode.Descendants("a").Where(x => x.Attributes.Contains("href"));
                foreach (var link in links) {
                    if (link.Attributes["href"].Value.Contains("download.nvidia")) {

                        downloadURL = link.Attributes["href"].Value;
                    }
                }

            } catch (Exception ex) {
                LogManager.log(ex.Message, 2);
                if (error == 0) {
                    Console.Write("ERROR!");
                    Console.WriteLine();
                    error = 1;
                }
                Console.WriteLine(ex.StackTrace);
            }

            if (error == 0) {
                Console.Write("OK!");
                Console.WriteLine();
            }

            if (debug == true) {
                Console.WriteLine("psID: " + psID);
                Console.WriteLine("pfID: " + pfID);
                Console.WriteLine("processURL: " + processURL);
                Console.WriteLine("confirmURL: " + confirmURL);
                Console.WriteLine("gpuURL: " + gpuURL);
                Console.WriteLine("downloadURL: " + downloadURL);
                
                Console.WriteLine("offlineGPUDriverVersion: " + offlineGPUDriverVersion);
                Console.WriteLine("onlineGPUDriverVersion:  " + onlineGPUDriverVersion);
            }

        }

        /// <summary>
        /// Nothing important, just a check if the required dll is placed correctly.
        /// </summary>
        private static void checkDll()
        {
            if (!File.Exists("HtmlAgilityPack.dll")) {
                string message = "The required binary cannot be found and the application will determinate itself. It must be put in the same folder as this executable.";

                Console.WriteLine(message);
                LogManager.log(message, 2);
                if (showUI == true) Console.ReadKey();
                Environment.Exit(2);
            }
        }

        /// <summary>
        /// Intro with legal message for cleanup at the top.
        /// </summary>
        private static void introMessage()
        {
            Console.WriteLine("TinyNvidiaUpdateChecker v" + offlineVer + " dev build");
            Console.WriteLine();
            Console.WriteLine("Copyright (C) 2016 Hawaii_Beach");
            Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY");
            Console.WriteLine("This is free software, and you are welcome to redistribute it");
            Console.WriteLine("under certain conditions. Licensed under GPLv3.");
            Console.WriteLine();
        }
    }
}