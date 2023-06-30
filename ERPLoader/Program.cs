using ERPLoader.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ERPLoader
{
    class Program
    {
        private static readonly List<ModModel> ModsList = new();
        public static readonly HashSet<string> FilesModified = new();
        private static readonly string FileHashesName = "modified_files_hashes.json";

        static void Main(string[] args)
        {
            Logger.FileWrite("===========ERPLoader START===========");

            bool isOnlyCleanup = false;
            bool skipRunUpdate = false;
            bool skipHashCheck = false;

            foreach (string arg in args)
            {
                switch (arg)
                {
                    case "/cleanOnly":
                        isOnlyCleanup = true;
                        break;
                    case "/skipUpdate":
                        skipRunUpdate = true;
                        break;
                    case "/cleanSkipHashCheck":
                        skipHashCheck = true;
                        break;
                    default:
                        break;
                }
            }

            // Last resort in bug catch
            try
            {
                if (!skipRunUpdate && !isOnlyCleanup)
                {
                    if (File.Exists("EasyUpdater.exe"))
                    {
                        Logger.Log("Checking for update...");

                        // Updater will kill ERPLoader if there is an update so we just wait
                        ProcessStartInfo psi = new()
                        {
                            FileName = "EasyUpdater.exe",
                            Arguments = "/autoUpdate",
                            WorkingDirectory = Directory.GetCurrentDirectory()
                        };
                        Process.Start(psi).WaitForExit();

                        // Updater exited means no updates found
                        Logger.Log("No new update found.");
                    }
                    else
                    {
                        Logger.Warning("EasyUpdater.exe not found! Failed to run update check");
                    }
                }

                Settings.InitSettings();

                if (Settings.Instance.Verify())
                {
                    PrintIntro();
                    Cleanup(skipHashCheck);

                    if (!isOnlyCleanup)
                    {
                        LoadMods();
                        StartMods();
                        StoreFilesModifiedHash();

                        if (Settings.Instance.LaunchGame)
                        {
                            var gameProcess = StartGame();

                            if (gameProcess != null)
                            {
                                Logger.Log("Waiting for game exit...");
                                Logger.Warning("Do not close this window if you want me to cleanup after you finish playing!");
                                Logger.Log("It's fine if you want to close this window now :) Just run Cleanup.bat file if you want to restore files for multiplayer.");

                                gameProcess.WaitForExit();

                                Logger.Log("Game exited! Start restoring files...");
                                Cleanup();
                            }
                        }

                        Logger.Log("Done! Thanks for using EasyERPMod :D");
                        System.Threading.Thread.Sleep(3000);
                    }
                }
                else
                {
                    Logger.Warning("Found errors in your settings.json file. Please fix it and restart the app");
                    Logger.NewLine();
                    Logger.Log("Press any key to exit...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("An unknown error has occured! Please report with .log files in \"Logs\" folder");
                Logger.FileWrite(ex.ToString(), Logger.MessageType.Error);
                Logger.NewLine();
                Logger.Log("Press any key to exit...");
                Console.ReadKey();
            }

            Logger.FileWrite("===========ERPLoader EXIT===========");
            Logger.NewLine();
        }

        private static void PrintIntro()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(3);
            var versionStrLen = $"v{version} ".Length;

            Console.Title = "EasyERPMod v" + version;

            Console.WriteLine("[ ]----------------------" + "-".Multiply(versionStrLen) + "[ ]");
            Console.WriteLine($"[ ] EasyERPMod by Maxhyt v{version} [ ]");
            Console.WriteLine("[ ]----------------------" + "-".Multiply(versionStrLen) + "[ ]");

            Console.WriteLine(@"
                              -_______----.._pBQ;
      ,^~|?|v^`          .!|JWRd9NRNND00R0dJ1lllt
rDDDW%9qyDMd8#@]    `~xtdDDDDD9qpDNGNNdRf6MduLn!!.=.=<rx]xv|v7>
 J007yvn|nYM8Q#Q^_:~vN00000ggdSdd6OgQgMZ9RDRDN0QQ##Q:.        :Qs|~,,,_     |    _
 `|88888gBBBBBQQ#@#BBBQQ8MM6%MMd0BBBBBQBBB############@@@@##QBQ#BQQQQ@@@@@#@@###QQBBKL.
   IBQQ8B98#QQQB#NB@@BBBB#gN0DBB9BQBB#####B#QB#@@#QQ@#QR#@@@@@@@@@@####@@@@@@BD#BQQQ#DN:
   O@@#@QQ#D####8@M@@###########B#8Q#QB#Q#Q#QBBQ#BBQ@#HyB###@@#####888B@@@@@@d@8###BD#MQVY*,==`
    ,::!gD#DQ#BQB#d@@@@@@@@@@@@@@@#@###@###@##@##@@#@@@@@#####BB#BdqPZMQ#@@@@RBBBBB8Q#RVqMdKQDNsDMQ~
        ,DQQQBB@QQ@@@@869RRRRD0gg88888QQQBBBBB######@##@@@@@@@@@@@@@@@8=`|hdg@BQQBBB#D}  ,Me0qd%QgB!
          :{fMR0MV??~`                                              ```      .*uIzVv:" + "\n");
        }

        private static void LoadMods()
        {
            Directory.CreateDirectory(Settings.Instance.ModsFolderName);

            string[] modsPaths = Directory.GetDirectories(Settings.Instance.ModsFolderName);

            if (modsPaths.Length == 0)
            {
                Logger.Log($"No mods found in \"{Settings.Instance.ModsFolderName}\"");
            }

            foreach (string modPath in modsPaths)
            {
                string modName = new DirectoryInfo(modPath).Name;

                if (!modName.EndsWith(Settings.Instance.DisabledModsEndsWith))
                {
                    ModsList.Add(new ModModel(modPath));
                }
            }
        }

        private static void StartMods()
        {
            ModsList.Sort((mod1, mod2) => mod1.Name.CompareTo(mod2.Name));
            ModsList.ForEach(mod => mod.Process());
        }

        private static Process StartGame()
        {
            Regex F1GameNameRegex = new(@"^f1_.+\.exe$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex F1GameTitleRegex = new(@"^F1 \d+", RegexOptions.Compiled);

            foreach (string file in Directory.GetFiles(Settings.Instance.F1GameDirectory))
            {
                if (F1GameNameRegex.IsMatch(Path.GetFileName(file)))
                {
                    Logger.Log($"Starting game process \"{Path.GetFileName(file)}\"");
                    Process.Start(file);

                    // Game process will exit and start a new one, try to find based on window's name
                    for (short i = 0; i < 60; i++)
                    {
                        System.Threading.Thread.Sleep(1000);

                        Logger.FileWrite($"Wait for game window try {i + 1}");

                        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(file)))
                        {
                            if (!string.IsNullOrEmpty(process.MainWindowTitle))
                            {
                                Logger.FileWrite("Found window: " + process.MainWindowTitle);
                            }

                            if (F1GameTitleRegex.IsMatch(process.MainWindowTitle))
                            {
                                Logger.FileWrite("Found F1 game window!");
                                return process;
                            }
                        }
                    }

                    Logger.Warning("Cannot find game's window");
                    break;
                }
            }

            Logger.Warning("Failed to start game");

            return null;
        }

        private static void Cleanup(bool skipCheckHashes = false)
        {
            Logger.Log("Start recovering original files...");

            var originalFiles = Directory.EnumerateFiles(Settings.Instance.F1GameDirectory, "*" + Settings.Instance.BackupFileExtension, SearchOption.AllDirectories);

            var filesHashes = GetFilesModifiedHash();

            Parallel.ForEach(originalFiles, file =>
            {
                try
                {
                    string moddedFilePath = file.Substring(0, file.Length - Settings.Instance.BackupFileExtension.Length);

                    if (File.Exists(moddedFilePath))
                    {
                        if (skipCheckHashes || !filesHashes.ContainsKey(moddedFilePath) || filesHashes[moddedFilePath] == GetFileHash(moddedFilePath))
                        {
                            File.Delete(moddedFilePath);
                            File.Move(file, moddedFilePath);
                        }
                        else
                        {
                            Logger.Warning("File " + moddedFilePath + " has been modified by another process. Game might be updated, this file will not be restored.");
                            Logger.Warning("If the game has just been updated, please delete this file: " + file);
                        }
                    }
                    else
                    {
                        File.Move(file, moddedFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to recover file at \"{file}\"");
                    Logger.FileWrite(ex.ToString(), Logger.MessageType.Error);
                }
            });

            File.Delete(FileHashesName);

            Logger.Log("Finished restoring original files");
        }

        private static void StoreFilesModifiedHash()
        {
            Dictionary<string, string> FilesHashes = new();

            Logger.Log("Start storing modified files hash...");

            foreach (var filePath in FilesModified)
            {
                FilesHashes.Add(filePath, GetFileHash(filePath));
            }

            File.WriteAllText(FileHashesName, JsonSerializer.Serialize(FilesHashes, new JsonSerializerOptions { WriteIndented = true }));

            Logger.Log("Hashes are stored");
        }

        private static string GetFileHash(string filePath)
        {
            if (File.Exists(filePath))
            {
                using (var hasher = MD5.Create())
                {
                    return Encoding.UTF8.GetString(hasher.ComputeHash(File.ReadAllBytes(filePath)));
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, string> GetFilesModifiedHash()
        {
            Dictionary<string, string> FilesHashes = new();

            if (File.Exists(FileHashesName))
            {
                FilesHashes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FileHashesName));
            }

            return FilesHashes;
        }
    }
}
