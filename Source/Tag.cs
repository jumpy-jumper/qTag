using System;
using System.Collections.Generic;
using System.Linq;

public class Tag
{
    public string ID = "";
    public string Name = "";
    public bool Exposed = false;
    public enum ValueDerivationType {Prime=0,Inferential=1}
    ValueDerivationType valueDerivationType;
    public enum ValueType {None=0,Number=1,Text=2,Folder=3,Range=4}
    ValueType valueType;
    public Type _Type;
    public List<string> Quantities;
    public List<string> Colors;
    public Dictionary<string, IComparable> Values = new Dictionary<string, IComparable>();
    public IComparable DefaultValue = 0;

    public Tag(string name, ValueType type, bool exposed = false)
    {
        this.Name = name;
        this.Exposed = exposed;
        this.valueType = type;
        this._Type = valueType == ValueType.Number ? typeof(long) : (valueType == ValueType.Range ? typeof(float) : typeof(string));
        this.valueDerivationType = ValueDerivationType.Prime;
        this.Colors = new List<string>();  
        this.Quantities = new List<string>();
    }

    public Tag(string name, bool exposed, params string[] steps)
    {
        this.Name = name;
        this.Exposed = exposed;
        this.valueType = ValueType.Number;
        this._Type = typeof(int);
        this.valueDerivationType = ValueDerivationType.Prime;
        this.Colors = new List<string>();  
        this.Quantities = new List<string>(steps);
    }

    public Tag(string name, bool exposed, params long[] steps)
    {
        this.Name = name;
        this.Exposed = exposed;
        this.valueType = ValueType.Number;
        this._Type = typeof(int);
        this.valueDerivationType = ValueDerivationType.Prime;
        this.Colors = new List<string>();  
        this.Quantities = (from s in steps select s.ToString()).ToList();
    }
    
    public enum Valuation { Direct=0, Boolean=1, Absolute=2, Relative=3, String=5 }

    public bool Has(string path)
    {
       return Values.ContainsKey(path);
    }

    public IComparable Evaluate(string path, Valuation valuation = Valuation.Direct)
    {
        var has = Has(path);
        IComparable value = has ? Values[path] : DefaultValue;
        switch(valuation)
        {
            case Valuation.Direct:
                return has ? value : null;
            case Valuation.Boolean:
                return has;
            case Valuation.Absolute:
                return has ? value : null;
            case Valuation.Relative:
                return has ? value : null;
            case Valuation.String:
                return has ? value.ToString() : "";
        }
        return null;
    }
}
