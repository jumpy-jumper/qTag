using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

public class TagDB : Godot.Node
{
    // Godot Interface

    public static TagDB Instance;

    public override void _Ready()
    {
        Instance = this;
        BuildDefaultTags();
    }

    public Godot.Collections.Dictionary get_tag_template(string tag)
    {
        var ret = new Godot.Collections.Dictionary();
        var t = Tags[tag];
        ret.Add("id", tag);
        ret.Add("name", t.Name);
        ret.Add("exposed", t.Exposed);
        ret.Add("quantities", new Godot.Collections.Array(t.Quantities));
        ret.Add("colors", new Godot.Collections.Array());
        ret.Add("value", 0);
        return ret;
    }

    public Godot.Collections.Dictionary get_file_tag(string path, string tag)
    {
        if (!Tags.ContainsKey(tag))
        {
            return new Godot.Collections.Dictionary();
        }
        if (!Tags[tag].Has(path))
        {
            return new Godot.Collections.Dictionary();
        }
        var value = Tags[tag].Evaluate(path);
        var ret = get_tag_template(tag);
        ret["value"] = value;
        return ret;
    }

    public Godot.Collections.Array get_tag_list()
    {
        return new Godot.Collections.Array(Tags.Keys);
    }

    public Godot.Collections.Array get_user_tag_list()
    {
        return new Godot.Collections.Array(from t in Tags where t.Value.Exposed orderby t.Key select t.Key);
    }

    public void set_tag(string file, string tag, object value)
    {
        SetTagValue(file, tag, value);
        //Console.WriteLine(Tags[tag].Evaluate(file, Tag.Valuation.String));
    }

    public void remove_tag(string file, string tag)
    {
        RemoveTag(file, tag);
    }

    public Godot.Collections.Array get_file_exposed_tags(string file)
    {
        return new Godot.Collections.Array(from t in FileIndex.Instance.Files[file] orderby t.ID where t.Exposed select t.ID);
    }

    // Default Tags

    public ConcurrentDictionary<string, Tag> Tags = new ConcurrentDictionary<string, Tag>();

    public void BuildDefaultTags()
    {
        Tags.TryAdd("path", new Tag("Path", Tag.ValueType.Text));
        Tags.TryAdd("name", new Tag("Name", Tag.ValueType.Text));
        Tags.TryAdd("extension", new Tag("Extension", Tag.ValueType.Text));
        Tags.TryAdd("random", new Tag("Random", Tag.ValueType.Number));
        Tags.TryAdd("last_accessed", new Tag("Last Accessed", Tag.ValueType.Number));
        Tags.TryAdd("created", new Tag("Creation Date", Tag.ValueType.Number));
        Tags.TryAdd("last_modified", new Tag("Last Modified", Tag.ValueType.Number));
        Tags.TryAdd("size", new Tag("Size", Tag.ValueType.Number));
        Tags.TryAdd("format", new Tag("Format", false, "Image", "Audio", "Video"));
        Tags.TryAdd("duration", new Tag("Duration", Tag.ValueType.Number));

        Tags.TryAdd("NSFW", new Tag("NSFW", true, "Suggestive", "Explicit"));
        Tags.TryAdd("Character/Clothing/Amount", new Tag("Nude", true, "Full Clothing", "Light Clothing", "Swimsuit", "Underwear", "Lingerie", "Nude"));
        Tags.TryAdd("Character/GenderIdentity", new Tag("Gender Identity", true, "Female", "NB", "Male"));
        Tags.TryAdd("Character/GenderPresentaton", new Tag("Gender Presentation", true, "Feminine", "Androgynous", "Masculine"));
        Tags.TryAdd("Character/Body/Breasts/Size", new Tag("Breast Size", true, "Small Breasts", "Average Breasts", "Big Breasts", "Huge Breasts", "Massive Breasts", "Monster Breasts"));
        Tags.TryAdd("Character/Body/Waist/Size", new Tag("Waist Size", true, "Tiny Waist", "Skinny Waist", "Average Waist", "Chubby Waist", "Fat Waist", "Obese Waist"));
        Tags.TryAdd("Character/Body/Arm/Size", new Tag("Arm Size", true, "Tiny Arms", "Skinny Arms",  "Average Arms", "Chubby Arms", "Massive Arms", "Obese Arms"));
        Tags.TryAdd("Character/Body/Ass/Size", new Tag("Ass Size", true, "Tiny Ass", "Fat Ass", "Huge Ass", "Massive Ass", "Ridiculous Ass"));
        Tags.TryAdd("Character/Body/Thigh/Size", new Tag("Thigh Size", true, "Skinny Thighs", "Average Thighs", "Fat Thighs", "Huge Thighs", "Obese Thighs", "Monster Thighs"));
        Tags.TryAdd("Character/Body/Cock/Size", new Tag("Cock Size", true, "Tiny Dick", "Average Dick", "Big Cock", "Huge Cock", "Massive Cock", "Ridiculous Cock"));
        Tags.TryAdd("Character/Body/Cock/Erect", new Tag("Erect", true, "Flaccid", "Semi-Hard", "Erect"));
        Tags.TryAdd("Character/Body/Muscle", new Tag("Muscle", true, "Light Muscle", "Defined Muscles", "Ridiculous Muscles"));
        Tags.TryAdd("Character/Body/Hair/Length", new Tag("Hair Length", true, "Bald", "Very Short Hair", "Short Hair", "Medium Hair", "Long Hair", "Very Long Hair"));
        Tags.TryAdd("Character/Age", new Tag("Age", true, "Child", "Teen", "Young", "Middle Aged", "Old"));

        foreach(var t in Tags)
        {
            t.Value.ID = t.Key;
        }
    }

    public object GetTagValue(string file_path, string tag)
    {
        return Tags[tag].Evaluate(file_path);
    }

    public string GetTagValueAsString(string file_path, string tag)
    {
        return (string)Tags[tag].Evaluate(file_path, Tag.Valuation.String);
    }

    public Tag SetTagValue(string file_path, string tag, object value, bool updateOnIndex=true)
    {
        if (value is string && Tags[tag].Quantities.Count > 0)
        {
            value = Tags[tag].Quantities.FindIndex((string s) => {return s == (string)value;}).ToString();
        }
        Tags[tag].Values[file_path] = (IComparable)Convert.ChangeType(value, Tags[tag]._Type);
        if (updateOnIndex)
        {
            FileIndex.Instance.Files[file_path].Add(Tags[tag]);

            if (Tags[tag].Exposed)
            {
                if (!FileIndex.Instance.ExposedTags.ContainsKey(file_path))
                {
                    FileIndex.Instance.ExposedTags[file_path] = new Dictionary<string, object>();
                }
                FileIndex.Instance.ExposedTags[file_path][Tags[tag].ID] = Tags[tag].Evaluate(file_path);
                FileIndex.Instance.SaveToDisk();
            }
        }

        return Tags[tag];
    }

    public void RemoveTag(string file_path, string tag, bool updateOnIndex=true)
    {
        Tags[tag].Values.Remove(file_path);
        if (updateOnIndex)
        {
            FileIndex.Instance.Files[file_path].Remove(Tags[tag]);

            if (Tags[tag].Exposed)
            {
                FileIndex.Instance.ExposedTags[file_path].Remove(Tags[tag].ID);
                if (FileIndex.Instance.ExposedTags[file_path].Count == 0)
                {
                    FileIndex.Instance.ExposedTags.TryRemove(file_path, out _);
                }
                FileIndex.Instance.SaveToDisk();
            }
        }
    }

    Regex regex_rx = new Regex("(^(.+)(::?)(.+)$)|(\\((.+)(::?)(.+)\\))", RegexOptions.Compiled);

    public IComparable EvaluateExpr(string file, string expr, Tag.Valuation valuation = Tag.Valuation.Direct)
    {

        try {
        var tag = new Tag("foo", Tag.ValueType.None);
        var negative = false;
        if(expr.Length > 0 && expr[0] == '-')
        {
            expr = expr.Remove(0,1);
            negative = true;
        }
        if (Tags.ContainsKey(expr))
        {
            // Trivial case - the expression exactly matches a tag
            tag = Tags[expr];
            var value = (IComparable)tag.Evaluate(file, valuation);
            if (value is bool)
            {
                return (bool)value ^ negative;
            }
            else if (!(value is string))
            {
                return (long)value * (negative ? -1 : 1);
            }
            return (IComparable)tag.Evaluate(file, valuation);
        }

        Match m = regex_rx.Match(expr);
        while (m.Success)
        {
            Tag this_tag;
            if (!Tags.ContainsKey(m.Groups[2].Value))
            {
                Console.WriteLine("Tag " + m.Groups[2].Value + " not found.");
                m = m.NextMatch();
                continue;
            }
            this_tag = Tags[m.Groups[2].Value];
            Regex value_rx = new Regex(m.Groups[4].Value, m.Groups[3].Value == ":" ? RegexOptions.IgnoreCase : RegexOptions.None);
            var matches = value_rx.IsMatch((string)this_tag.Evaluate(file, Tag.Valuation.String));
            expr.Replace(m.Groups[1].Value, matches ? "(1)" : "(0)");

            m = m.NextMatch();
        }

        // Cleanup binary operations

        while(expr != "(0)" && expr != "(1)")
        {
            var old = expr;
            expr.Replace("(0)(0)", "(0)");
            expr.Replace("(0)(1)", "(0)");
            expr.Replace("(1)(0)", "(0)");
            expr.Replace("(1)(1)", "(1)");
            expr.Replace("(0)||(0)", "(0)");
            expr.Replace("(0)||(1)", "(1)");
            expr.Replace("(1)||(0)", "(1)");
            expr.Replace("(1)||(1)", "(1)");
            if (old == expr)
            {
                return false;
            }
            Console.WriteLine(expr);
        }

        return expr == "(1)";} catch (Exception e) {Console.WriteLine("Error analyzing file " + file + " expr " + expr + "\n\n" + e + "\n"); return null;}
    }
}
