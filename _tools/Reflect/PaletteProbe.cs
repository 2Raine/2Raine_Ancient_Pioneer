// Throwaway probe: print the game's CP437 palette entries for the codes we care about.
using System;
using System.Reflection;

public static class PaletteProbe
{
    public static void Main()
    {
        string dir = @"D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\";
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string n = new AssemblyName(e.Name).Name;
            string p = System.IO.Path.Combine(dir, n + ".dll");
            return System.IO.File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };

        var asm = Assembly.LoadFrom(System.IO.Path.Combine(dir, "Assembly-CSharp.dll"));
        Type t = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = a.GetType("ConsoleLib.Console.Palette");
            if (t != null) break;
        }
        if (t == null) { Console.WriteLine("ConsoleLib.Console.Palette not found"); return; }

        Console.WriteLine("=== static members of " + t.FullName);
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            Console.WriteLine("  field " + f.FieldType.Name + " " + f.Name);
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            Console.WriteLine("  prop  " + p.PropertyType.Name + " " + p.Name);
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            if (!m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                Console.WriteLine("  meth  " + m.ReturnType.Name + " " + m.Name + "(" +
                    string.Join(", ", Array.ConvertAll(m.GetParameters(), x => x.ParameterType.Name)) + ")");
    }
}
