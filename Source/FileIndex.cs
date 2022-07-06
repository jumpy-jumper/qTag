using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;   
using System.IO;
using System.Threading;
using System.Threading.Tasks; 
using System.Diagnostics;
using ImageMagick;
using Newtonsoft.Json;

public class FileIndex : Godot.Node
{
    /*
        Asynchronous indexing of all files on disk.

        Stores and serves file information that obeys
        filtering and sorting rules.

        Loads file content via streaming aiming to stay
        under some amount of RAM.
    */

    public static FileIndex Instance;

    public ConcurrentDictionary<string, HashSet<Tag>> Files = new 
    ConcurrentDictionary<string, HashSet<Tag>>();

    /*
        GDScript Interface.
    */

    public override void _Ready()
    {
        Instance = this;
        tagdb_path = Godot.ProjectSettings.GlobalizePath("user://") + "tags.qtdb";
        StartIndexing();
        StartStreaming();
    }

    public bool has_window(string expr)
    {
        if (windows.ContainsKey(expr))
        {
            return true;
        }
        else
        {
            foreach (KeyValuePair<string, FileWindow> window in windows)
            {
                if (window.Value.SatisfiesExpression(expr))
                {
                    windows[expr] = window.Value;
                    return true;
                }
            }
        }
        return false;
    }

    public List<string> get_window(string expr) 
    {
        if (windows.ContainsKey(expr))
        {
            windows[expr].dirty = false;
        }
        else
        {
            foreach (KeyValuePair<string, FileWindow> window in windows)
            {
                if (window.Value.SatisfiesExpression(expr))
                {
                    windows[expr] = window.Value;
                    return windows[expr].ToList();
                }
            }
            windows[expr] = new FileWindow(expr);
            windows[expr].AddFiles(Files.Keys);
        }
        return windows[expr].ToList();
    }

    public bool is_window_dirty(string id) 
    {
        return !windows.ContainsKey(id) || windows[id].dirty;
    }

    public bool has_file(string path)
    {
        return Files.ContainsKey(path);
    }

    public void request_thumbnail(string path, bool urgent)
    {
        if (!requestedThumbnailsHash.ContainsKey(path))
        {
            requestedThumbnails.Enqueue(path);
            requestedThumbnailsHash[path] = true;
        }
    }

    public void cancel_requests()
    {
        requestedThumbnails = new ConcurrentQueue<string>();
        requestedThumbnailsHash.Clear();
    }

    public Godot.Texture get_thumbnail(string path)
    {
        if (Thumbnails.ContainsKey(path))
        {
            return Thumbnails[path];
        }
        return null;
    }

    public object get_content(string path)
    {
        if (!Content.ContainsKey(path))
        {
            MagickImage img;
            Godot.ImageTexture imgtex = null;
            try {
                img = new MagickImage(path);
            } catch (Exception e) {
                img = null;
            }

            if (img != null)
            {
                var gdimg = new Godot.Image();
                gdimg.CreateFromData(img.Width, img.Height, false, Godot.Image.Format.Rgb8, img.ToByteArray(MagickFormat.Rgb));
                imgtex = new Godot.ImageTexture();
                imgtex.CreateFromImage(gdimg);
            }
            Content[path] = imgtex;
        }
        return (Godot.Texture)Content[path];
    }

    public void execute(string path)
    {
        try {Process.Start(path);} catch (Exception e) {Console.WriteLine(e);}
    }

    public void execute_properties(string path)
    {
        var i = new ProcessStartInfo(path);
        i.Verb = "properties";
        try {Process.Start(i);} catch (Exception e) {Console.WriteLine(e);}
    }

    public void delete(string path)
    {
        HashSet<Tag> v;
        var b = false;
        try {b = Files.TryRemove(path, out v);} catch {} 
        if (b)
        {
            foreach (FileWindow window in windows.Values)
            {
                lock (this)
                {
                    window.window.Remove(path);
                }
            }
        }
    }
    
    /*
        FILE INDEXING

        For debug purposes, will only index a few specified paths for now.
        In the future, this should save the index to disk for future retrieval,
        as well as re-indexing after application launch.
    */

    ConcurrentDictionary<string, FileWindow> windows = new ConcurrentDictionary<string, FileWindow>();

    void StartIndexing()
    {
        LoadFromDisk();
        Task.Run(delegate() {IndexDirectory("C:\\Sync");});
        Task.Run(delegate() {IndexDirectory("E:\\Music");});
        Task.Run(delegate() {IndexDirectory("E:\\Manga");});
        Task.Run(delegate() {IndexDirectory("E:\\Doujinshi");});
        Task.Run(delegate() {IndexDirectory("C:\\Users\\jcsar\\Videos\\SSD Captures");});
    }

    // Recursively spawns a new task to index every directory inside the given path.
    void IndexDirectory(string path)
    {
        foreach (string dir in System.IO.Directory.EnumerateDirectories(path))
        {
            Task.Run(delegate() {IndexDirectory(dir);});
        }
        foreach (string file in System.IO.Directory.EnumerateFiles(path))
        {
            IndexFile(file);
        }
    }

    Random rnd = new Random();    
    readonly Dictionary<string, string> fileFormats = 
        new Dictionary<string, string>()
    {
        {"JPG", "Image"},
        {"JPEG", "Image"},
        {"PNG", "Image"},
        {"BMP", "Image"},
        {"GIF", "Image"},
        {"TIFF", "Image"},
        {"MP4", "Video"},
        {"AVI", "Video"},
        {"WEBM", "Video"},
        {"MKV", "Video"},
        {"MP3", "Audio"},
        {"OGG", "Audio"},
        {"WAV", "Audio"},
        {"FLAC", "Audio"},
    };

    void IndexFile(string path)
    {
        var data = new HashSet<Tag>();
        data.Add(TagDB.Instance.SetTagValue(path, "path", path, false));
        data.Add(TagDB.Instance.SetTagValue(path, "name", System.IO.Path.GetFileName(path), false));
        var extension = System.IO.Path.GetExtension(path).ToUpper().Replace(".", "");
        data.Add(TagDB.Instance.SetTagValue(path, "extension", extension, false));
        data.Add(TagDB.Instance.SetTagValue(path, "random", rnd.Next().ToString(), false));
        System.IO.FileInfo info = new System.IO.FileInfo(path);
        data.Add(TagDB.Instance.SetTagValue(path, "last_accessed", info.LastAccessTime.Ticks, false));
        data.Add(TagDB.Instance.SetTagValue(path, "created", info.CreationTime.Ticks, false));
        data.Add(TagDB.Instance.SetTagValue(path, "last_modified", info.LastWriteTime.Ticks, false));
        data.Add(TagDB.Instance.SetTagValue(path, "size", info.Length.ToString(), false));
        var format = fileFormats.ContainsKey(extension) ? fileFormats[extension] : "Unknown";
        data.Add(TagDB.Instance.SetTagValue(path, "format", format, false));
        if (format == "Video")
        {
            data.Add(TagDB.Instance.SetTagValue(path, "duration", (rnd.Next() % (74*60)).ToString(), false));
        }
        if (ExposedTags.ContainsKey(path))
        {
            foreach(var tag in ExposedTags[path])
            {
                if (TagDB.Instance.Tags.ContainsKey(tag.Key))
                {
                    data.Add(TagDB.Instance.SetTagValue(path, tag.Key, tag.Value, false));
                }
            }
        }
        Files[path] = data;
        foreach (FileWindow window in windows.Values)
        {
            Task.Run(()=>{window.AddFile(path);});
        }
    }

    public void UpdateFile(string path)
    {
        foreach (FileWindow window in windows.Values)
        {
            window.UpdateFile(path);
        }
    }

    /*
        CONTENT STREAMING
    */
    ConcurrentQueue<string> requestedThumbnails = new ConcurrentQueue<string>();
    ConcurrentQueue<string> requestedThumbnailsLowPrio = new ConcurrentQueue<string>();
    ConcurrentDictionary<string, bool> requestedThumbnailsHash = new ConcurrentDictionary<string, bool>();
    ConcurrentDictionary<string, Godot.Texture> Thumbnails = new 
    ConcurrentDictionary<string, Godot.Texture>();
    ConcurrentDictionary<string, object> Content = new 
    ConcurrentDictionary<string, object>();

    void StartStreaming()
    {
        for (int i = 0; i < 8; i++)
        {
            Task.Run(() => ThumbnailProc());
        }
    }

    void ThumbnailProc()
    {
        while (true)
        {
            string path;
            if (requestedThumbnails.TryDequeue(out path) || requestedThumbnailsLowPrio.TryDequeue(out path))
            {
                if (Thumbnails.ContainsKey(path))
                {
                    continue;
                }
                MagickImage img;
                Godot.ImageTexture imgtex = null;
                try {
                    var s = new MagickReadSettings();
                    img = new MagickImage(path);
                } catch (Exception e) {
                    img = null;
                }

                if (img != null)
                {
                    var geo = new MagickGeometry(256, 256);
                    img.Thumbnail(geo);
                    var gdimg = new Godot.Image();
                    gdimg.CreateFromData(img.Width, img.Height, false, Godot.Image.Format.Rgb8, img.ToByteArray(MagickFormat.Rgb));
                    imgtex = new Godot.ImageTexture();
                    imgtex.CreateFromImage(gdimg);
                }
                Thumbnails[path] = imgtex;
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    /*
        SAVING
    */

    public ConcurrentDictionary<string, Dictionary<string, object>> ExposedTags = new ConcurrentDictionary<string, Dictionary<string, object>>();

    string tagdb_path;

    public void SaveToDisk()
    {
        var json = JsonConvert.SerializeObject(ExposedTags);
        File.WriteAllText(tagdb_path, json);
    }

    public void LoadFromDisk()
    {
        try{
        ExposedTags = JsonConvert.DeserializeObject<ConcurrentDictionary<string, Dictionary<string, object>>>(
        File.ReadAllText(tagdb_path));} catch{};
    }
}
