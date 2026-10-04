// Throwaway reflection tool: prints ElectricalGeneration's discharge-scaling signatures and the
// constant values that drive them.
using System;
using System.Linq;
using System.Reflection;

public static class DischargeDump
{
    public static void Main(string[] args)
    {
        string path = @"D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\";
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string n = new AssemblyName(e.Name).Name;
            string p = System.IO.Path.Combine(path, n + ".dll");
            return System.IO.File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };

        Console.SetOut(new System.IO.StreamWriter(@"C:\Users\16064\AppData\Local\Temp\qud_api\discharge_dump.txt") { AutoFlush = true });

        Assembly asm = Assembly.LoadFrom(System.IO.Path.Combine(path, "Assembly-CSharp.dll"));
        Type t = null;
        try
        {
            foreach (Type x in asm.GetTypes())
            {
                if (x != null && x.Name == "ElectricalGeneration" && x.Namespace != null && x.Namespace.Contains("Mutation"))
                {
                    t = x;
                    break;
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (Type x in ex.Types)
            {
                if (x != null && x.Name == "ElectricalGeneration" && x.Namespace != null && x.Namespace.Contains("Mutation"))
                {
                    t = x;
                    break;
                }
            }
        }
        if (t == null) { Console.WriteLine("NOT FOUND"); return; }

        Console.WriteLine("=== static readonly constants");
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            object v = null;
            try { v = f.GetRawConstantValue(); } catch { }
            if (v == null) { try { v = f.GetValue(null); } catch { } }
            Console.WriteLine("    " + f.Name + " = " + (v ?? "<unreadable>"));
        }

        Console.WriteLine("=== discharge-scaling methods");
        foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (!m.Name.Contains("Discharge")) continue;
            Console.WriteLine("    " + m.ReturnType.Name + " " + m.Name + "(" +
                string.Join(", ", m.GetParameters()
                    .Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        }
    }
}
