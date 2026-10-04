// Throwaway reflection tool: prints Popup.AskNumber / AskString / PickOption signatures.
using System;
using System.Linq;
using System.Reflection;

public static class SigDump
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

        Assembly asm = Assembly.LoadFrom(System.IO.Path.Combine(path, "Assembly-CSharp.dll"));
        Type t = asm.GetType("XRL.UI.Popup");
        if (t == null) { Console.WriteLine("XRL.UI.Popup NOT FOUND"); return; }

        foreach (string name in new[] { "AskNumber", "AskString", "PickOption" })
        {
            foreach (MethodInfo m in t.GetMethods().Where(x => x.Name == name))
            {
                Console.WriteLine("=== " + m.ReturnType.Name + " " + m.Name);
                foreach (ParameterInfo p in m.GetParameters())
                {
                    Console.WriteLine(string.Format("    {0,-24} {1}{2}",
                        p.ParameterType.Name + " " + p.Name,
                        p.HasDefaultValue ? "= " + (p.RawDefaultValue ?? "null") : "",
                        p.IsOptional ? "  [optional]" : ""));
                }
            }
        }
    }
}
