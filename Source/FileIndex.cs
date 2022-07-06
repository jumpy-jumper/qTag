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

    public Godot.Collections.Array get_window(string id, string expr) 
    {
        Window window;
        if (windows.ContainsKey(id))
        {
            if (windows[id].Expr == expr)
            {
                window = windows[id];
                window.dirty = false;
                return new Godot.Collections.Array(
                    window.GetWindow());
            }
            windows[id].Dispose();
        }
        window = new Window(Files, expr);
        windows[id] = window;
        window.Start();
        return new Godot.Collections.Array(
            window.GetWindow());
    }

    public bool is_window_dirty(string id) 
    {
        return !windows.ContainsKey(id) || windows[id].dirty;
    }

    public bool has_file(string path)
    {
        return Files.ContainsKey(path);
    }

    public void request_thumbnail(string path)
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
        return null;
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
            foreach (Window window in windows.Values)
            {
                lock (this)
                {
                    window.window.Remove(path);
                }
            }
        }
    }

    /*
        FILE WINDOW

        A portion of the file index that meets 
        some filters and is ordered by some sort rules.

        WINDOW EXPRESSION
        
        An interface for checking a file's
        filter pass and weight based on a string expression.

        An expression a list of tokens separated by spaces, which contain subtokens separated by colon. The tokens specify filtering rules unless the first subtoken specifies a sort rule.
    */

    ConcurrentDictionary<string, Window> windows = new ConcurrentDictionary<string, Window>();

    class Window : IDisposable
    {
        readonly string UNMATCHABLE_REGEX = "a^";

        public string Expr = "";
        ConcurrentDictionary<string, HashSet<Tag>> Files;
        List<string> Filters = new List<string>();
        List<string> SortRules = new List<string>();
        public bool dirty = false;

        public enum Directive { Filter=0, Sort=1 }

        Regex directive_rx = new Regex("#(.+):(.+)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public Window(ConcurrentDictionary<string, HashSet<Tag>> files, string expr)
        {
            Files = files;
            Expr = expr;
            string[] tokens = expr.Split(' ');
            
            foreach (string token in tokens)
            {
                Directive directive = Directive.Filter;
                Match m = directive_rx.Match(token);
                string directive_args = token;
                if (m.Success)
                {
                    switch(m.Groups[1].Value.ToLower())
                    {
                        case "s":
                        case "sort":
                            directive = Directive.Sort;
                            break;
                    }
                    directive_args = m.Groups[2].Value;
                }
                if(directive_args=="")
                {
                    continue;
                }
                switch(directive)
                {
                    case Directive.Filter:
                        Filters.Add(directive_args);
                        break;
                    case Directive.Sort:
                        SortRules.Add(directive_args);
                        break;
                }
            }

            window = new SortedSet<string>(new WindowComparer(Files, SortRules));

            Console.WriteLine("[Filters] " + string.Join("|", Filters));
            Console.WriteLine("[Sorts] " + string.Join("|", SortRules));
            Console.WriteLine();
        }

        bool AcceptsFile(string path)
        {
            foreach (string filter in Filters)
            {
                if (!(bool)TagDB.Instance.EvaluateExpr(path, filter, Tag.Valuation.Boolean))
                {
                    return false;
                }
            }
            return true;
        }

        public void Start()
        {
            ct = tokenSource.Token;
            task = Task.Run(() => {WindowProc();}, tokenSource.Token);
        }

        public void Dispose()
        {
            tokenSource.Cancel();
        }

        void WindowProc()
        {
            while (true)
            {
                bool flag = true;
                foreach (var item in Files)
                {
                    if (!window_hashed.Contains(item.Key) && AcceptsFile(item.Key))
                    {
                        lock(this)
                        {
                            window.Add(item.Key);
                            dirty = true;
                            flag = false;
                        }
                    }
                    window_hashed.Add(item.Key);
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                if (ct.IsCancellationRequested)
                {
                    break;
                }
                if (flag)
                {
                    //break;
                }
            }
        }
        Task task;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken ct;
        public SortedSet<string> window;
        HashSet<string> window_hashed = new HashSet<string>();

        class WindowComparer : IComparer<string>
        {
            ConcurrentDictionary<string, HashSet<Tag>> Files;
            List<string> SortRules;

            public WindowComparer(ConcurrentDictionary<string, HashSet<Tag>> files, List<string> sortRules)
            {
                Files = files;
                SortRules = sortRules;
            }
            public int Compare(string x, string y)
            {
                var ret = 0;
                foreach(var SortRule in SortRules)
                {
                    if (x.Equals(y) || !Files.ContainsKey(x) || !Files.ContainsKey(y))
                    {
                        return 0;
                    }
                    IComparable xe = (IComparable)TagDB.Instance.EvaluateExpr(x, SortRule);
                    IComparable ye = TagDB.Instance.EvaluateExpr(y, SortRule);
                    if (ye == null)
                    {
                        return -1;
                    }
                    else if (xe == null)
                    {
                        return 1;
                    }
                    ret = xe.CompareTo(ye);
                    if (ret != 0)
                    {
                        return ret;
                    }
                }
                return 1;
            }
        }

        public List<string> GetWindow()
        {
            lock (this)
            {
                return window.ToList();
            }
        }
    }

    /*
        FILE INDEXING

        For debug purposes, will only index a few specified paths for now.
        In the future, this should save the index to disk for future retrieval,
        as well as re-indexing after application launch.
    */

    void StartIndexing()
    {
        LoadFromDisk();
        Task.Run(delegate() {IndexDirectory("C:\\Sync");});
        Task.Run(delegate() {IndexDirectory("E:\\Music");});
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
    }

    /*
        CONTENT STREAMING
    */
    ConcurrentQueue<string> requestedThumbnails = new ConcurrentQueue<string>();
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
            if (requestedThumbnails.TryDequeue(out path))
            {
                if (Thumbnails.ContainsKey(path))
                {
                    continue;
                }
                MagickImage img;
                Godot.ImageTexture imgtex = null;
                try {
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
