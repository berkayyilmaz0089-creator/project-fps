using Godot;
using System.Collections.Generic;

public partial class SaveDirectoryHandler : Node
{
    private const string SaveDir = "user://GameSaveData";
    private const int MaxSaveFiles = 200;

    public void EnsureDirectoryExists()
    {
        if (!DirAccess.DirExistsAbsolute(SaveDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        }
    }

    public void ManageSaves()
    {
        EnsureDirectoryExists();
        var files = GetSaveFiles();

        while (files.Count >= MaxSaveFiles)
        {
            var oldest = files[files.Count - 1];
            string pathToRemove = (string)oldest["path"];
            DirAccess.RemoveAbsolute(pathToRemove);
            files.RemoveAt(files.Count - 1);
        }
    }

    public List<Godot.Collections.Dictionary> GetSaveFiles()
    {
        EnsureDirectoryExists();
        var fileList = new List<Godot.Collections.Dictionary>();
        using var dir = DirAccess.Open(SaveDir);

        if (dir != null)
        {
            dir.ListDirBegin();
            string fileName = dir.GetNext();

            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".sav"))
                {
                    string fullPath = SaveDir + "/" + fileName;
                    ulong modifiedTime = FileAccess.GetModifiedTime(fullPath);

                    var fileInfo = new Godot.Collections.Dictionary
                    {
                        ["name"] = fileName,
                        ["path"] = fullPath,
                        ["time"] = modifiedTime
                    };
                    fileList.Add(fileInfo);
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        fileList.Sort((a, b) => ((ulong)b["time"]).CompareTo((ulong)a["time"]));
        return fileList;
    }

    public string GetLatestSaveFile()
    {
        var files = GetSaveFiles();
        return files.Count > 0 ? (string)files[0]["name"] : string.Empty;
    }

    public string GetSavePath(string fileName)
    {
        if (!fileName.EndsWith(".sav"))
        {
            fileName += ".sav";
        }
        return SaveDir + "/" + fileName;
    }
}