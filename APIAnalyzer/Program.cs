using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace APIAnalyzer
{
    class Program
    {
        static readonly string GameBinPath = @"/home/malafein/.steam/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/";

        // Core assemblies always loaded. More are loaded dynamically when using --search.
        static readonly string[] DefaultAssemblies = {
            "TaleWorlds.CampaignSystem",
            "TaleWorlds.Core",
            "TaleWorlds.Library",
            "TaleWorlds.MountAndBlade",
        };

        // Default types to inspect when no --type is specified.
        static readonly string[] DefaultTargetTypes = {
            "TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterDevelopmentModel",
        };

        // Keyword filters for members. Override with --keywords or --all.
        static readonly string[] DefaultKeywords = {
            "Point", "Learning", "Calculate", "Level", "Skill", "Attribute", "Focus", "Xp", "Experience"
        };

        static void Main(string[] args)
        {
            bool verbose    = HasFlag(args, "--verbose", "-v");
            bool listAll    = HasFlag(args, "--all",     "-a");
            bool searchMode = HasFlag(args, "--search",  "-s");
            string typeArg  = GetArg(args, "--type");
            string searchArg = GetArg(args, "--search") ?? (searchMode ? GetPositionalArg(args) : null);

            if (HasFlag(args, "--help", "-h"))
            {
                PrintHelp();
                return;
            }

            Console.WriteLine($"Game bin path: {GameBinPath}");
            Console.WriteLine();

            // Load game assemblies.
            bool loadAll = searchArg != null; // search needs all DLLs
            var assemblies = LoadAssemblies(loadAll, verbose);
            Console.WriteLine();

            if (searchArg != null)
            {
                SearchTypes(assemblies, searchArg, verbose);
                return;
            }

            var typesToInspect = typeArg != null ? new[] { typeArg } : DefaultTargetTypes;
            foreach (var typeName in typesToInspect)
            {
                var type = FindType(assemblies, typeName);
                if (type == null)
                {
                    Console.WriteLine($"[NOT FOUND] {typeName}");
                    Console.WriteLine("  Tip: use --search <partial-name> to locate it across all assemblies.");
                    Console.WriteLine();
                }
                else
                {
                    PrintType(type, listAll, verbose);
                }
            }
        }

        // -------------------------------------------------------------------------

        static Dictionary<string, Assembly> LoadAssemblies(bool all, bool verbose)
        {
            var loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> paths = all
                ? Directory.GetFiles(GameBinPath, "*.dll")
                : DefaultAssemblies.Select(n => Path.Combine(GameBinPath, n + ".dll"));

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    if (!all) Console.WriteLine($"[WARN] Not found: {path}");
                    continue;
                }
                try
                {
                    var asm = Assembly.LoadFrom(path);
                    loaded[Path.GetFileNameWithoutExtension(path)] = asm;
                    if (verbose) Console.WriteLine($"Loaded: {Path.GetFileName(path)}");
                }
                catch
                {
                    // Skip unloadable assemblies silently when bulk-loading
                    if (verbose) Console.WriteLine($"[SKIP] {Path.GetFileName(path)}");
                }
            }
            return loaded;
        }

        static Type FindType(Dictionary<string, Assembly> assemblies, string typeName)
        {
            foreach (var asm in assemblies.Values)
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        static void SearchTypes(Dictionary<string, Assembly> assemblies, string query, bool verbose)
        {
            Console.WriteLine($"Searching for types matching: \"{query}\"");
            Console.WriteLine();
            var results = new List<(string assembly, string fullName)>();
            foreach (var kv in assemblies)
            {
                try
                {
                    foreach (var t in kv.Value.GetExportedTypes())
                        if (t.FullName != null && t.FullName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            results.Add((kv.Key, t.FullName));
                }
                catch { }
            }

            if (results.Count == 0)
            {
                Console.WriteLine("No matches found.");
                return;
            }

            foreach (var (asm, fullName) in results.OrderBy(r => r.fullName))
                Console.WriteLine($"  [{asm}]  {fullName}");

            Console.WriteLine($"\n{results.Count} type(s) found.");
        }

        static void PrintType(Type type, bool listAll, bool verbose)
        {
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"Type: {type.FullName}");
            if (type.BaseType != null)
                Console.WriteLine($"Base: {type.BaseType.FullName}");
            Console.WriteLine(new string('=', 70));

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

            // Properties
            var props = type.GetProperties(flags)
                .Where(p => listAll || MatchesKeyword(p.Name))
                .OrderBy(p => p.Name)
                .ToList();

            if (props.Count > 0)
            {
                Console.WriteLine("  [Properties]");
                foreach (var p in props)
                {
                    string inherited = p.DeclaringType != type ? $"  (from {p.DeclaringType?.Name})" : "";
                    string access = (p.CanRead ? "get; " : "") + (p.CanWrite ? "set; " : "");
                    Console.WriteLine($"    {p.PropertyType.FriendlyName(),-30} {p.Name} {{ {access.Trim()} }}{inherited}");
                }
                Console.WriteLine();
            }

            // Methods (no property accessors, no System.Object noise)
            var methods = type.GetMethods(flags)
                .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                .Where(m => listAll || MatchesKeyword(m.Name))
                .OrderBy(m => m.Name)
                .ToList();

            if (methods.Count > 0)
            {
                Console.WriteLine("  [Methods]");
                foreach (var m in methods)
                {
                    string inherited = m.DeclaringType != type ? $"  (from {m.DeclaringType?.Name})" : "";
                    string virt = m.IsVirtual && !m.IsFinal ? "virtual " : "";
                    var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.FriendlyName()} {p.Name}"));
                    Console.WriteLine($"    {m.ReturnType.FriendlyName(),-20} {virt}{m.Name}({parms}){inherited}");
                }
                Console.WriteLine();
            }

            if (props.Count == 0 && methods.Count == 0)
                Console.WriteLine("  (no matching members — try --all to see everything)\n");
        }

        static bool MatchesKeyword(string name)
        {
            foreach (var kw in DefaultKeywords)
                if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        // -------------------------------------------------------------------------
        // Argument helpers

        static bool HasFlag(string[] args, params string[] flags)
            => args.Any(a => flags.Contains(a, StringComparer.OrdinalIgnoreCase));

        static string GetArg(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        static string GetPositionalArg(string[] args)
            => args.FirstOrDefault(a => !a.StartsWith("-"));

        static void PrintHelp()
        {
            Console.WriteLine("APIAnalyzer — inspect Bannerlord game model APIs");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project APIAnalyzer/APIAnalyzer.csproj [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  (no args)                  Inspect default target types with keyword filter");
            Console.WriteLine("  --all, -a                  Show all members (no keyword filter)");
            Console.WriteLine("  --type <FullTypeName>      Inspect a specific type by full name");
            Console.WriteLine("  --search <partial-name>    Search all assemblies for types matching the name");
            Console.WriteLine("  --verbose, -v              Show assembly loading details");
            Console.WriteLine("  --help, -h                 Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run ...                                   # default inspection");
            Console.WriteLine("  dotnet run ... -- --search SkillLeveling         # find a type by partial name");
            Console.WriteLine("  dotnet run ... -- --type TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel --all");
        }
    }

    static class Extensions
    {
        public static string FriendlyName(this Type t)
        {
            if (t == null) return "void";
            if (!t.IsGenericType) return t.Name;
            var baseName = t.Name[..t.Name.IndexOf('`')];
            var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyName));
            return $"{baseName}<{args}>";
        }
    }
}
