using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Photo_Organiser
{
    class Program
    {
        private static DateTime MinDate = new DateTime(2000, 1, 1);
        private static string FilePattern = "\\.jpg|\\.png|\\.mov|\\.mp4";
        private static string FileDatePattern = "(?<dt>[\\d]{4}-[\\d]{2}-[\\d]{2})(?:\\s|_)(?<tm>[\\d]{2}(?:-|\\.)[\\d]{2}(?:-|\\.)[\\d]{2}).*";
        private static int FileIncrement = 1;

        static void Main(string[] args)
        {
            if (args.Length != 2) throw new Exception("Invalid arguments");

            var rootPath = GetPath(args);
            var action = GetAction(args);

            if (rootPath == null) throw new Exception("Root path not specified");
            if (!Directory.Exists(rootPath)) throw new Exception("Root path does not exist");

            if (!action.HasValue) throw new Exception("Action not specified");

            Console.WriteLine($"Action: {action}, Root dirtectory: {rootPath}");

            switch (action.Value)
            {
                case Action.CreateFolders: CreateFolders(rootPath, rootPath); break;
                case Action.RenameFilesInPlace: RenameFilesInPlace(rootPath, rootPath); break;
                case Action.RenameAndMoveFiles: RenameAndMoveFiles(rootPath, rootPath); break;
            }

            Console.WriteLine();
            Console.WriteLine("Finished.");
            Console.ReadKey();
        }

        private static string GetPath(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("-p=") || arg.StartsWith("-path="))
                {
                    return arg.Split('=')[1].Trim(new char[] { '\"' });
                }
            }

            return null;
        }

        private static Action? GetAction(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg == "-c") return Action.CreateFolders;
                if (arg == "-r") return Action.RenameFilesInPlace;
                if (arg == "-m") return Action.RenameAndMoveFiles;
            }

            return null;
        }


        private static void CreateFolders(string rootFolder, string folder)
        {
            var files = GetFiles(folder);

            foreach (var file in files)
            {
                var dt = GetFileDate(file);
                if (!dt.HasValue) continue;

                var dir = $"{rootFolder}/{dt.Value.Year}/{dt:yyy-MM-dd}";
                Directory.CreateDirectory($"{rootFolder}/{dt.Value.Year}/{dt:yyy-MM-dd}");
                Console.WriteLine(dir);
            }

            var subDirectories = Directory.GetDirectories(folder);
            foreach (var subDirectory in subDirectories)
            {
                CreateFolders(rootFolder, subDirectory);
            }
        }

        private static void RenameFilesInPlace(string rootFolder, string folder)
        {

        }

        private static void RenameAndMoveFiles(string rootFolder, string folder)
        {
            var files = GetFiles(folder);
            
            foreach (var file in files)
            {
                string srcFilename = Path.GetFileNameWithoutExtension(file);
                string ext = Path.GetExtension(file);

                string destFilename;
                DateTime fileDate;

                var dateMatch = Regex.Match(file, FileDatePattern, RegexOptions.IgnoreCase);

                if (dateMatch.Success)
                {
                    var dt = dateMatch.Groups["dt"].ToString();
                    var tm = dateMatch.Groups["tm"].ToString();

                    fileDate = DateTime.Parse($"{dt} {tm.Replace(".", ":").Replace("-", ":").Replace("_", ":")}");

                    destFilename = $"{srcFilename}";
                }
                else
                {
                    var dt = GetFileDate(file);
                    if (!dt.HasValue) continue;

                    fileDate = dt.Value;
                    destFilename = $"{dt:yyyy-MM-dd_HH-mm-ss}";
                }

                var destPath = $"{rootFolder}\\{fileDate.Year}\\{fileDate:yyyy-MM-dd}";
                var destFilePath = $"{destPath}\\{destFilename}{ext}";
                
                try
                {
                    if (destFilePath == file) continue;

                    var moved = false;
                    int folderIncrement = 1;
                    while (!moved && folderIncrement < 1000) {

                        if (!File.Exists(destFilePath)) {
                            moved = true;

                            if (!Directory.Exists($"{destPath}"))
                                Directory.CreateDirectory($"{destPath}");
                            
                            Console.WriteLine($"{srcFilename}{ext} > {destFilePath}");
                            File.Move(file, destFilePath);
                        } else {
                            destPath = $"{rootFolder}\\{fileDate.Year}\\_duplicates_{folderIncrement}\\{fileDate:yyyy-MM-dd}";
                            destFilePath = $"{destPath}\\{destFilename}{ext}";
                            //destFilePath = $"{destPath}\\{destFilename}_{folderIncrement}{ext}";
                            folderIncrement++;
                        }
                    }
                }
                catch (System.IO.IOException ex)
                {
                    Console.WriteLine($"IO Excetion {ex.Message}");
                }
            }

            var subDirectories = Directory.GetDirectories(folder);
            foreach (var subDirectory in subDirectories)
            {
                RenameAndMoveFiles(rootFolder, subDirectory);
            }

            try
            {
                // Cleanup empty Directories
                var dirSysEntries = Directory.GetFileSystemEntries(folder);
                if (dirSysEntries.Length == 0)
                {
                    Directory.Delete(folder);
                }
                else if (dirSysEntries.Length == 1 && dirSysEntries[0].EndsWith("Thumbs.db"))
                {
                    File.Delete(dirSysEntries[0]);
                    Directory.Delete(folder);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete directory error: {ex.Message}");
                //ignore
            }
        }

        private static List<string> GetFiles(string folder)
        {
            return Directory.GetFiles(folder).Where(file => Regex.IsMatch(file, FilePattern, RegexOptions.IgnoreCase)).ToList();
        }

        private static DateTime? GetFileDate(string file)
        {
            DateTime? dt = GetDateTakenFromImage(file);

            if (dt.HasValue) return dt;

            dt = File.GetCreationTimeUtc(file);
            dt = File.GetLastWriteTimeUtc(file) < dt ? File.GetLastWriteTimeUtc(file) : dt;

            return dt < MinDate ? null : dt;
        }

        private static DateTime? GetDateTakenFromImage(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (Image myImage = Image.FromStream(fs, false, false))
                {
                    PropertyItem propItem = myImage.GetPropertyItem(36867);
                    Regex r = new Regex(":");
                    string dateTaken = r.Replace(Encoding.UTF8.GetString(propItem.Value), "-", 2);
                    return DateTime.Parse(dateTaken);
                }
            }
            catch { return null; }
        }
    }

    public enum Action
    {
        CreateFolders,
        RenameFilesInPlace,
        RenameAndMoveFiles
    }
}
