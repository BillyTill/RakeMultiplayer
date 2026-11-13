using System;
using System.Linq;
using System.Reflection;

namespace RakeMultiplayer.TestRunner
{
    /// <summary>
    /// Minimal offline test runner. The NUnit VS adapter in this environment cannot
    /// enumerate .NET 10 runtimes, so pure managed fixtures are executed by reflection.
    /// Unity-dependent fixtures (GameObject, Camera) must be run inside the game instead.
    /// Usage: RakeMultiplayer.TestRunner.exe [fixtureNameFilter]
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : "PacketSerializationTests";

            var assembly = Assembly.Load("RakeMultiplayer.Tests");
            int passed = 0;
            int failed = 0;

            foreach (var type in assembly.GetTypes())
            {
                if (!type.Name.Contains(filter)) continue;
                if (type.IsAbstract) continue;

                object fixture;
                try { fixture = Activator.CreateInstance(type); }
                catch (Exception ex)
                {
                    Console.WriteLine("SKIP " + type.Name + " (cannot construct: " + ex.Message + ")");
                    continue;
                }

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!HasAttribute(method, "NUnit.Framework.TestAttribute")) continue;

                    try
                    {
                        InvokeSetUp(type, fixture);
                        method.Invoke(fixture, null);
                        passed++;
                        Console.WriteLine("PASS " + type.Name + "." + method.Name);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        var inner = ex is TargetInvocationException && ex.InnerException != null
                            ? ex.InnerException
                            : ex;
                        Console.WriteLine("FAIL " + type.Name + "." + method.Name + ": " + inner.Message);
                    }
                }
            }

            Console.WriteLine("passed=" + passed + " failed=" + failed);
            return failed > 0 ? 1 : 0;
        }

        private static void InvokeSetUp(Type type, object fixture)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (HasAttribute(method, "NUnit.Framework.SetUpAttribute"))
                {
                    method.Invoke(fixture, null);
                }
            }
        }

        private static bool HasAttribute(MemberInfo member, string attributeName)
        {
            return member.GetCustomAttributes(false)
                         .Any(a => a.GetType().FullName == attributeName);
        }
    }
}
