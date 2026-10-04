using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

// Prints exact member signatures for a list of type names, resolving dependencies
// from the game's Managed folder so that nothing is lost to ReflectionTypeLoadException.
public static class DumpMembers
{
    static string LibPath = @"D:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed\";
    static Dictionary<string, Assembly> Cache = new Dictionary<string, Assembly>();
    static List<Assembly> Loaded = new List<Assembly>();
    static Assembly Root;

    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string simple = new AssemblyName(args.Name).Name;
        Assembly found;
        if (Cache.TryGetValue(simple, out found)) return found;
        string p = Path.Combine(LibPath, simple + ".dll");
        if (File.Exists(p))
        {
            found = Assembly.LoadFrom(p);
            Cache[simple] = found;
            Loaded.Add(found);
            return found;
        }
        return null;
    }

    public static void Main(string[] argv)
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Root = Assembly.LoadFrom(Path.Combine(LibPath, "Assembly-CSharp.dll"));
        Cache["Assembly-CSharp"] = Root;
        Loaded.Add(Root);

        string[] wanted = argv.Length > 0 ? argv : new[] { "XRL.World.Parts.Mutation.ElectricalGeneration" };

        // Force-load every sibling so nested/dependent types resolve.
        foreach (string f in Directory.GetFiles(LibPath, "*.dll"))
        {
            string simple = Path.GetFileNameWithoutExtension(f);
            if (Cache.ContainsKey(simple)) continue;
            try
            {
                Assembly a = Assembly.LoadFrom(f);
                Cache[simple] = a;
                Loaded.Add(a);
            }
            catch { }
        }

        Type[] all;
        try { all = Root.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            all = ex.Types.Where(t => t != null).ToArray();
            Console.Error.WriteLine("ReflectionTypeLoadException: " + ex.LoaderExceptions.Length +
                                    " loader exceptions, " + all.Length + " types recovered");
            foreach (Exception le in ex.LoaderExceptions.Take(4))
                Console.Error.WriteLine("   " + le.Message);
        }
        Console.Error.WriteLine("total types: " + all.Length);

        foreach (string w in wanted)
        {
            Type[] matches = all.Where(t => t != null && FullNameSafe(t) == w).ToArray();
            if (matches.Length == 0)
                matches = all.Where(t => t != null && FullNameSafe(t).EndsWith("." + w)).ToArray();
            if (matches.Length == 0)
            {
                Console.WriteLine("### NOT FOUND: " + w);
                continue;
            }
            foreach (Type t in matches)
            {
                Console.WriteLine("### " + FullNameSafe(t) + " : " + FullNameSafe(t.BaseType));
                foreach (Type i in t.GetInterfaces()) Console.WriteLine("    implements " + FullNameSafe(i));
                const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (FieldInfo f in t.GetFields(BF).OrderBy(x => x.Name))
                    Console.WriteLine(string.Format("    F {0} {1} {2}",
                        f.IsPublic ? "public" : (f.IsPrivate ? "private" : "prot"),
                        Nice(f.FieldType), f.Name));
                foreach (PropertyInfo p in t.GetProperties(BF).OrderBy(x => x.Name))
                    Console.WriteLine(string.Format("    P {0} {1} {{ {2} }}",
                        Nice(p.PropertyType), p.Name,
                        (p.CanRead ? "get; " : "") + (p.CanWrite ? "set; " : "")));
                foreach (MethodInfo m in t.GetMethods(BF).OrderBy(x => x.Name))
                {
                    if (m.IsSpecialName) continue;
                    string ps = string.Join(", ", m.GetParameters().Select(Param));
                    Console.WriteLine(string.Format("    M {0} {1} {2}({3})",
                        m.IsPublic ? "public" : (m.IsPrivate ? "private" : (m.IsFamily ? "protected" : "prot-int")),
                        Nice(m.ReturnType), m.Name, ps));
                }
                foreach (ConstructorInfo c in t.GetConstructors(BF))
                    Console.WriteLine("    C .ctor(" + string.Join(", ", c.GetParameters().Select(Param)) + ")");
            }
        }
    }

    static string Param(ParameterInfo p)
    {
        string s = Nice(p.ParameterType) + " " + p.Name;
        if (p.HasDefaultValue)
        {
            object d = p.RawDefaultValue;
            s += " = " + (d == null ? "null" : (d is string ? "\"" + d + "\"" : d.ToString()));
        }
        return s;
    }

    static string FullNameSafe(Type t)
    {
        try { return t == null ? "<null>" : t.FullName ?? t.Name; }
        catch { return "<unloadable>"; }
    }

    static string Nice(Type t)
    {
        if (t == null) return "?";
        if (t.IsGenericType)
        {
            string n = t.Name;
            int i = n.IndexOf('`');
            if (i > 0) n = n.Substring(0, i);
            return n + "<" + string.Join(", ", t.GetGenericArguments().Select(Nice)) + ">";
        }
        switch (t.FullName)
        {
            case "System.Int32": return "int";
            case "System.Boolean": return "bool";
            case "System.String": return "string";
            case "System.Single": return "float";
            case "System.Object": return "object";
            case "System.Void": return "void";
            case "System.Char": return "char";
            case "System.Int64": return "long";
        }
        return t.Name;
    }
}
