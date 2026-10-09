using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace AbProbe;

// ---------------------------------------------------------------------------------------------------------------
// Faux objets du SDK : un DispatchProxy par interface (IGame, IEmulator, IDataManager...). Les proprietes lisent et
// ecrivent un dictionnaire ; les methodes passent par M ; le reste rend la valeur par defaut.
// ---------------------------------------------------------------------------------------------------------------
public class Fake : DispatchProxy
{
    public string Label { get; set; }

    public Dictionary<string, object> P { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Func<object[], object>> M { get; } = new(StringComparer.Ordinal);

    public static T Make<T>(string label)
    {
        T proxy = Create<T, Fake>();
        ((Fake)(object)proxy).Label = label;
        return proxy;
    }

    public static Fake Of(object proxy) => (Fake)proxy;

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        string name = targetMethod.Name;
        if (M.TryGetValue(name, out var method))
        {
            return method(args);
        }

        if (name.StartsWith("get_", StringComparison.Ordinal) && targetMethod.GetParameters().Length == 0)
        {
            return P.TryGetValue(name.Substring(4), out var value) ? value : Default(targetMethod.ReturnType);
        }

        if (name.StartsWith("set_", StringComparison.Ordinal) && args.Length == 1)
        {
            P[name.Substring(4)] = args[0];
            return null;
        }

        return Default(targetMethod.ReturnType);
    }

    private static object Default(Type type)
        => type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
}

// Exception capturee, rendue comme une valeur.
public sealed class Caught
{
    public Caught(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: not null } tie)
        {
            ex = tie.InnerException;
        }

        var sb = new StringBuilder();
        sb.Append('!').Append(ex.GetType().FullName).Append(": ").Append(Dumper.Q(ex.Message));
        if (ex is ArgumentException argument)
        {
            sb.Append(" ParamName=").Append(Dumper.Q(argument.ParamName));
        }
        for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            sb.Append(" <- ").Append(inner.GetType().FullName).Append(": ").Append(Dumper.Q(inner.Message));
        }

        Text = sb.ToString();
    }

    public string Text { get; }

    public static object Try(Func<object> f)
    {
        try
        {
            return f();
        }
        catch (Exception ex)
        {
            return new Caught(ex);
        }
    }
}

// Enregistrement nomme (plusieurs valeurs rendues par un cas).
public sealed class Rec : IEnumerable<KeyValuePair<string, object>>
{
    private readonly List<KeyValuePair<string, object>> items = new();

    public void Add(string key, object value) => items.Add(new(key, value));

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// ---------------------------------------------------------------------------------------------------------------
// Rendu canonique en texte, en profondeur : type d'execution, proprietes et champs publics, ordre des collections,
// partage d'objets (ref#n). Les espaces de noms des deux greffons sont ramenes a {ns}.
// ---------------------------------------------------------------------------------------------------------------
public static class Dumper
{
    public static readonly DateTime Fixed = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    public static string Dump(object value)
    {
        var sb = new StringBuilder();
        var seen = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        Write(value, sb, 0, "", seen, 0);
        return sb.ToString();
    }

    public static string Q(string s)
    {
        if (s == null)
        {
            return "null";
        }

        var sb = new StringBuilder("\"");
        foreach (char ch in s)
        {
            if (ch == '"' || ch == '\\')
            {
                sb.Append('\\').Append(ch);
            }
            else if (ch < 0x20 || ch > 0x7E)
            {
                sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.Append('"').ToString();
    }

    public static string TypeName(Type t) => NormalizeNs(t.ToString());

    public static string NormalizeNs(string s)
        => s.Replace("Unbroken.LaunchBox.Windows.RetroArch", "{ns}", StringComparison.Ordinal)
            .Replace("Nixx.RetroArch", "{ns}", StringComparison.Ordinal);

    private static void Line(StringBuilder sb, int indent, string text)
        => sb.Append(' ', indent * 2).Append(text).Append('\n');

    private static string Date(DateTime dt)
    {
        string kind = dt.Kind.ToString();
        if (dt == Fixed || dt == Fixed.ToLocalTime()
            || (dt.Kind != DateTimeKind.Unspecified && dt.ToUniversalTime() == Fixed))
        {
            return "DateTime(FIXE, " + kind + ")";
        }

        DateTime utc = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt;
        if (Math.Abs((utc - DateTime.UtcNow).TotalHours) < 24)
        {
            return "DateTime(RECENT, " + kind + ")";
        }

        return "DateTime(" + dt.ToString("o", CultureInfo.InvariantCulture) + ", " + kind + ")";
    }

    private static void Write(object o, StringBuilder sb, int indent, string head, Dictionary<object, int> seen, int depth)
    {
        if (depth > 12)
        {
            Line(sb, indent, head + "<trop profond>");
            return;
        }

        if (o == null)
        {
            Line(sb, indent, head + "null");
            return;
        }

        switch (o)
        {
            case Caught c:
                Line(sb, indent, head + c.Text);
                return;
            case string s:
                Line(sb, indent, head + Q(s));
                return;
            case bool b:
                Line(sb, indent, head + (b ? "true" : "false"));
                return;
            case DateTime dt:
                Line(sb, indent, head + Date(dt));
                return;
            case Fake f:
                Line(sb, indent, head + "Fake(" + f.Label + ")");
                return;
            case IEmulator emulator:
                // Emulateur cree par le greffon (installation neuve) : seules les proprietes d'IEmulator.
                Line(sb, indent, head + "IEmulator " + TypeName(o.GetType()));
                foreach (PropertyInfo prop in typeof(IEmulator).GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    Write(Caught.Try(() => prop.GetValue(emulator)), sb, indent + 1, prop.Name + " = ", seen, depth + 1);
                }

                Write(Caught.Try(() => emulator.GetAllEmulatorPlatforms()), sb, indent + 1, "GetAllEmulatorPlatforms() = ", seen, depth + 1);
                return;
            case Rec rec:
                Line(sb, indent, head + "{");
                foreach (var kv in rec)
                {
                    Write(kv.Value, sb, indent + 1, kv.Key + ": ", seen, depth + 1);
                }

                Line(sb, indent, "}");
                return;
            case Delegate d:
                Line(sb, indent, head + "delegate " + TypeName(d.GetType()));
                return;
            case Type ty:
                Line(sb, indent, head + "Type " + TypeName(ty));
                return;
        }

        Type t = o.GetType();
        if (t.IsPrimitive || t.IsEnum || o is decimal)
        {
            Line(sb, indent, head + TypeName(t) + " " + Convert.ToString(o, CultureInfo.InvariantCulture));
            return;
        }

        if (seen.TryGetValue(o, out int refId))
        {
            Line(sb, indent, head + TypeName(t) + " =ref#" + refId);
            return;
        }

        bool isValue = t.IsValueType;
        if (!isValue)
        {
            refId = seen.Count + 1;
            seen[o] = refId;
        }

        string id = isValue ? "" : " #" + refId;

        if (o is IDictionary dict)
        {
            Line(sb, indent, head + TypeName(t) + id + " (" + dict.Count + " entrees)");
            foreach (DictionaryEntry entry in dict)
            {
                Write(entry.Value, sb, indent + 1, "[" + Convert.ToString(entry.Key, CultureInfo.InvariantCulture) + "] => ", seen, depth + 1);
            }

            return;
        }

        if (o is IEnumerable enumerable)
        {
            var items = new List<object>();
            object error = null;
            try
            {
                foreach (object item in enumerable)
                {
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                error = new Caught(ex);
            }

            Line(sb, indent, head + TypeName(t) + id + " (" + items.Count + " elements)");
            for (int i = 0; i < items.Count; i++)
            {
                Write(items[i], sb, indent + 1, "[" + i + "] ", seen, depth + 1);
            }

            if (error != null)
            {
                Write(error, sb, indent + 1, "enumeration: ", seen, depth + 1);
            }

            return;
        }

        Line(sb, indent, head + TypeName(t) + id);
        var members = new List<(string Name, Func<object> Get)>();
        foreach (PropertyInfo prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length == 0 && prop.GetMethod != null)
            {
                members.Add((prop.Name, () => prop.GetValue(o)));
            }
        }

        foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            members.Add((field.Name, () => field.GetValue(o)));
        }

        foreach (var member in members.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            Write(Caught.Try(member.Get), sb, indent + 1, member.Name + " = ", seen, depth + 1);
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------
// Un greffon (original ou clone) charge par reflexion.
// ---------------------------------------------------------------------------------------------------------------
public sealed class Side
{
    private readonly ConstructorInfo pluginCtor;
    private readonly ConstructorInfo biosCtor;

    public Side(string name, string path, string pluginTypeName)
    {
        Name = name;
        Path = path;
        Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        PluginType = Assembly.GetType(pluginTypeName, throwOnError: true);
        BiosType = Assembly.GetExportedTypes().Single(t => t.Name == "BiosFile");
        SystemFolder = BiosType.GetProperty("SystemFolder", BindingFlags.Public | BindingFlags.Static);
        pluginCtor = PluginType.GetConstructor(new[] { typeof(IPluginPaths) });
        biosCtor = BiosType.GetConstructors().Single(c => c.GetParameters().Length == 5);
    }

    public string Name { get; }

    public string Path { get; }

    public Assembly Assembly { get; }

    public Type PluginType { get; }

    public Type BiosType { get; }

    public PropertyInfo SystemFolder { get; }

    public EmulatorPlugin Create(IPluginPaths paths) => (EmulatorPlugin)Invoke(() => pluginCtor.Invoke(new object[] { paths }));

    public object NewBios(string fileName, bool required, string description = null, string md5 = null, EmulatorBiosGroup group = null)
        => Invoke(() => biosCtor.Invoke(new object[] { fileName, required, description, md5, group }));

    public string GetSystemFolder() => (string)SystemFolder.GetValue(null);

    public void SetSystemFolder(string value) => SystemFolder.SetValue(null, value);

    private static object Invoke(Func<object> f)
    {
        try
        {
            return f();
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}

public sealed class Case
{
    public Case(string id, string section, string input, Action<Env> setup, Func<Env, EmulatorPlugin, Side, object> act)
    {
        Id = id;
        Section = section;
        Input = input;
        Setup = setup;
        Act = act;
    }

    public string Id { get; }

    public string Section { get; }

    public string Input { get; }

    public Action<Env> Setup { get; }

    public Func<Env, EmulatorPlugin, Side, object> Act { get; }

    // Le greffon n'est pas construit par la sonde (S01 le construit lui-meme).
    public bool NoPlugin { get; init; }

    // Le cas peut ouvrir une fenetre (boite modale de LaunchBox) : joue seulement avec --interactive.
    public bool Interactive { get; init; }

    // Cas de la section 13 (option --network) : faux serveur local ou vrai reseau.
    public NetMode? Net { get; init; }

    // Le cas peut emettre une requete HTTP : joue seulement si la garde reseau est armee (NetGuard).
    public bool NeedsNetGuard { get; init; }

    // Egalite non exigee par la matrice (V12) : un ecart est JAUNE, pas ROUGE.
    public bool Informative { get; init; }
}

public sealed class Outcome
{
    public string Value { get; init; }

    public string SystemFolder { get; init; }

    public string Tree { get; init; }

    public string ProbeError { get; init; }

    // Empreinte des reponses du vrai reseau (statut, taille, ETag, Last-Modified) : sert a reconnaitre l'instabilite.
    public string NetPrint { get; init; } = "";
}
