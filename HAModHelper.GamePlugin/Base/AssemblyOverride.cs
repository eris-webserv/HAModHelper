
using System.Reflection;
using System.Runtime.Loader;
using MelonLoader;

namespace HAModHelper.GamePlugin.Core.Debug;

internal static class AssemblyManager
{
    public static Assembly AssemblyResolveEventListener(object sender, ResolveEventArgs args)
    {
        if (args is null)
        {
            return null!;
        }

        string name = "HAMH.Resources." + args.Name[..args.Name.IndexOf(',')] + ".dll";
        using Stream? str = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (str is not null)
        {
            var context = new AssemblyLoadContext(name, false);
            MelonLogger.Warning($"Loaded {args.Name} from our embedded resources, saving to userlibs for next time");
            string path = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly()?.Location!)!.Parent!.FullName, "UserLibs", args.Name[..args.Name.IndexOf(',')] + ".dll");

            FileStream fstr = new(path, FileMode.Create);
            str.CopyTo(fstr);
            fstr.Close();
            str.Seek(0, SeekOrigin.Begin);
            return context.LoadFromStream(str);
        }
        return null!;
    }

    public static void SetOurResolveHandlerAtFront()
    {
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo? field = null;

        Type domainType = typeof(AssemblyLoadContext);

        while (field is null)
        {
            if (domainType is not null)
            {
                field = domainType.GetField("AssemblyResolve", flags);
            }
            else
            {
                //MelonDebug.Error("domainType got set to null for the AssemblyResolve event was null");
                return;
            }
            if (field is null)
            {
                domainType = domainType.BaseType!;
            }
        }

        var resolveDelegate = (MulticastDelegate)field.GetValue(null)!;
        Delegate[] subscribers = resolveDelegate.GetInvocationList();

        Delegate currentDelegate = resolveDelegate;
        for (int i = 0; i < subscribers.Length; i++)
        {
            currentDelegate = System.Delegate.RemoveAll(currentDelegate, subscribers[i])!;
        }

        var newSubscriptions = new Delegate[subscribers.Length + 1];
        newSubscriptions[0] = (ResolveEventHandler)AssemblyResolveEventListener!;
        System.Array.Copy(subscribers, 0, newSubscriptions, 1, subscribers.Length);

        currentDelegate = Delegate.Combine(newSubscriptions)!;

        field.SetValue(null, currentDelegate);
    }
}