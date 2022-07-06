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

/*
    FILE WINDOW

    A portion of the file index that meets 
    some filters and is ordered by some sort rules.

    WINDOW EXPRESSION
    
    An interface for checking a file's
    filter pass and weight based on a string expression.

    An expression a list of tokens separated by spaces, which contain subtokens separated by colon. The tokens specify filtering rules unless the first subtoken specifies a sort rule.
*/

class FileWindow : IDisposable
{
    readonly string UNMATCHABLE_REGEX = "a^";

    public string Expr = "";
    List<string> Filters = new List<string>();
    List<string> Sorts = new List<string>();
    public bool dirty = false;

    public enum Directive { None=0, Filter=1, Sort=2 }

    Regex directive_rx = new Regex("#(.+):(.+)", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    bool empty_window = false;
    public FileWindow(string expr)
    {
        Expr = expr;
        Console.WriteLine($"Expression: {expr}");
        if (expr.Trim() == "")
        {
            empty_window = true;
            Console.WriteLine($"Dead window.");
        }

        var r = GetFiltersAndSortsFromExpression(expr);

        Filters = r.Item1;
        Sorts = r.Item2;
        window = new SortedSet<string>(new WindowComparer(Sorts));
        Console.WriteLine($"[Filters] {string.Join("|", Filters)}");
        Console.WriteLine($"[Sorts] {string.Join("|", Sorts)}");
        Console.WriteLine();

        Task.Run(()=>ThumbnailRequestProc());
    }
    public Tuple<List<string>, List<string>> GetFiltersAndSortsFromExpression(string expr)
    {
        List<string> filters = new List<string>();
        List<string> sorts = new List<string>();
        string[] tokens = expr.Split(' ');
        
        foreach (string token in tokens)
        {
            Directive directive = Directive.Filter;
            Match m = directive_rx.Match(token);
            string directive_args = token;
            if (m.Success)
            {
                directive = Directive.None;
                switch(m.Groups[1].Value.ToLower())
                {
                    case "s":
                    case "sort":
                        directive = Directive.Sort;
                        break;
                    case "f":
                    case "filter":
                        directive = Directive.Filter;
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
                    filters.Add(directive_args);
                    break;
                case Directive.Sort:
                    sorts.Add(directive_args);
                    filters.Add(directive_args.Replace("-", ""));
                    break;
            }
        }

        sorts.Reverse();

        return new Tuple<List<string>, List<string>>(filters, sorts);
    }

    bool cancel = false;
    public void Dispose()
    {
        lock(this){cancel = true;};
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
    
    public void AddFiles(ICollection<string> paths)
    {
        if (empty_window)
        {
            return;
        }
        foreach(string path in paths)
        {
            Task.Run(()=>AddFile(path));
        }
    }

    public void AddFile(string path)
    {
        if (empty_window)
        {
            return;
        }
        lock(this){dirty |= !window.Remove(path);}
        if (AcceptsFile(path))
        {
            lock(this){window.Add(path);}
            dirty = true;
        }
    }

    public void UpdateFile(string path)
    {
        if (empty_window)
        {
            return;
        }
        AddFile(path);
    }

    public void RemoveFile(string path)
    {
        if (empty_window)
        {
            return;
        }
        dirty |= !window.Remove(path);
    }

    void ThumbnailRequestProc()
    {
        while(true)
        {
            lock(this)
            {
                if (cancel) return;
                foreach(var path in window.Take(100))
                {
                    FileIndex.Instance.request_thumbnail(path, false);
                }
            };
            Thread.Sleep(1000);
        }
    }

    public bool SatisfiesExpression(string expr)
    {
        if (empty_window)
        {
            return false;
        }
        var r = GetFiltersAndSortsFromExpression(expr);
        if (r.Item1.Except(Filters).ToList().Count > 0) {return false;}
        if (Filters.Except(r.Item1).ToList().Count > 0) {return false;}
        if (r.Item2.Except(Sorts).ToList().Count > 0) {return false;}
        if (Sorts.Except(r.Item2).ToList().Count > 0) {return false;}
        return true;
    }

    public SortedSet<string> window;

    class WindowComparer : IComparer<string>
    {
        List<string> Sorts;

        public WindowComparer(List<string> sortRules)
        {
            Sorts = sortRules;
        }
        public int Compare(string x, string y)
        {
            try{
            var ret = 0;
            foreach(var SortRule in Sorts)
            {
                if (x.Equals(y))
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
            return 1;}catch(Exception e){Console.WriteLine($"Could not compare {x} vs {y}.\n{e}"); return 1;}
        }
    }

    public List<string> ToList()
    {
        lock (this)
        {
            return window.ToList();
        }
    }
}

