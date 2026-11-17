using System;
using System.Collections.Generic;

namespace AssetStudio;
public static class TypeFlags
{
    private static Dictionary<ClassIDType, (bool, bool)> Types;

    public static void SetTypes(Dictionary<ClassIDType, (bool, bool)> types)
    {
        Types = types;
    }

    public static void SetType(ClassIDType type, bool parse, bool export)
    {
        Types ??= new Dictionary<ClassIDType, (bool, bool)>();
        Types[type] = (parse, export);
    }

    private static bool TryGetParam(ClassIDType type, out (bool, bool) param)
    {
        if (Types.TryGetValue(type, out param))
        {
            return true;
        }
        // Settings saved before cubemap support have no Cubemap entry: follow the Texture2D switches instead
        if (type == ClassIDType.Cubemap)
        {
            return Types.TryGetValue(ClassIDType.Texture2D, out param);
        }
        return false;
    }

    public static bool CanParse(this ClassIDType type)
    {
        if (Types == null)
        {
            return true;
        }
        else if (TryGetParam(type, out var param))
        {
            return param.Item1;
        }

        return false;
    }

    public static bool CanExport(this ClassIDType type)
    {
        if (Types == null)
        {
            return true;
        }
        else if (TryGetParam(type, out var param))
        {
            return param.Item2;
        }

        return false;
    }
}

[Flags]
public enum TypeFlag
{
    None,
    Parse,
    Export,
    Both = Parse | Export,
}