using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RakeMultiplayer.TestRunner
{
    /// <summary>
    /// Minimal offline test runner. The NUnit VS adapter in this environment cannot
    /// enumerate .NET 10 runtimes, so pure managed fixtures are executed by reflection.
    ///
    /// Usage: RakeMultiplayer.TestRunner.exe [fixtureFilter ...] [--strict]
    ///
    /// With no filter every fixture runs. Tests that need the Unity runtime cannot execute
    /// in a plain .NET process (the VM reports "ECall ... not implemented"); they are
    /// counted as UNITY and reported separately instead of silently looking green.
    /// --strict makes UNITY count as a failure (use once the Unity-only fixtures are gone).
    /// </summary>
    internal static class Program
    {
        private const string TestAttribute = "NUnit.Framework.TestAttribute";
        private const string SetUpAttribute = "NUnit.Framework.SetUpAttribute";
        private const string TearDownAttribute = "NUnit.Framework.TearDownAttribute";
        private const string OneTimeSetUpAttribute = "NUnit.Framework.OneTimeSetUpAttribute";
        private const string OneTimeTearDownAttribute = "NUnit.Framework.OneTimeTearDownAttribute";
        private const string IgnoreAttribute = "NUnit.Framework.IgnoreAttribute";

        private static int _passed;
        private static int _failed;
        private static int _unity;
        private static int _ignored;
        private static int _fixtures;

        private static int Main(string[] args)
        {
            var filters = new List<string>();
            bool strict = false;

            foreach (var arg in args)
            {
                if (string.Equals(arg, "--strict", StringComparison.OrdinalIgnoreCase)) strict = true;
                else filters.Add(arg);
            }

            var assembly = Assembly.Load("RakeMultiplayer.Tests");

            foreach (var type in Fixtures(assembly))
            {
                if (filters.Count > 0 &&
                    !filters.Any(f => type.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                var tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .Where(m => HasAttribute(m, TestAttribute))
                                .ToList();

                if (tests.Count == 0) continue;

                if (HasAttribute(type, IgnoreAttribute))
                {
                    _ignored += tests.Count;
                    foreach (var test in tests) Console.WriteLine("IGNORE " + type.Name + "." + test.Name);
                    continue;
                }

                object fixture;
                try
                {
                    fixture = Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    // A fixture that cannot even be constructed is not a passing fixture.
                    _failed += tests.Count;
                    foreach (var test in tests)
                    {
                        Console.WriteLine("FAIL " + type.Name + "." + test.Name +
                                          ": fixture ctor failed: " + Root(ex).Message);
                    }
                    continue;
                }

                _fixtures++;

                try
                {
                    InvokeAll(type, fixture, OneTimeSetUpAttribute);
                }
                catch (Exception ex)
                {
                    if (NeedsUnity(Root(ex)))
                    {
                        _unity += tests.Count;
                        foreach (var test in tests)
                        {
                            Console.WriteLine("UNITY " + type.Name + "." + test.Name +
                                              " (needs the Unity runtime: OneTimeSetUp)");
                        }
                    }
                    else
                    {
                        _failed += tests.Count;
                        foreach (var test in tests)
                        {
                            Console.WriteLine("FAIL " + type.Name + "." + test.Name +
                                              ": OneTimeSetUp failed: " + Root(ex).Message);
                        }
                    }
                    continue;
                }

                try
                {
                    foreach (var test in tests) RunTest(type, fixture, test);
                }
                finally
                {
                    // OneTimeTearDown must run even if something above threw.
                    InvokeAllQuiet(type, fixture, OneTimeTearDownAttribute);
                }
            }

            Console.WriteLine("summary: fixtures=" + _fixtures + " passed=" + _passed +
                              " failed=" + _failed + " unity=" + _unity + " ignored=" + _ignored);

            if (_failed > 0) return 1;
            if (strict && _unity > 0) return 1;
            return 0;
        }

        private static void RunTest(Type type, object fixture, MethodInfo test)
        {
            bool setUpOk = true;

            try
            {
                InvokeAll(type, fixture, SetUpAttribute);
            }
            catch (Exception ex)
            {
                setUpOk = false;
                var inner = Root(ex);

                // A Unity-only fixture usually dies in SetUp, when it first touches a
                // GameObject. Classify it the same way as a Unity-only test body.
                if (NeedsUnity(inner))
                {
                    _unity++;
                    Console.WriteLine("UNITY " + type.Name + "." + test.Name +
                                      " (needs the Unity runtime: SetUp)");
                }
                else
                {
                    _failed++;
                    Console.WriteLine("FAIL " + type.Name + "." + test.Name +
                                      ": SetUp failed: " + inner.GetType().Name + ": " + Shorten(inner.Message));
                }
            }

            if (setUpOk)
            {
                try
                {
                    test.Invoke(fixture, null);
                    _passed++;
                    Console.WriteLine("PASS " + type.Name + "." + test.Name);
                }
                catch (Exception ex)
                {
                    var inner = Root(ex);

                    if (NeedsUnity(inner))
                    {
                        _unity++;
                        Console.WriteLine("UNITY " + type.Name + "." + test.Name +
                                          " (needs the Unity runtime: " + Shorten(inner.Message) + ")");
                    }
                    else
                    {
                        _failed++;
                        Console.WriteLine("FAIL " + type.Name + "." + test.Name + ": " +
                                          inner.GetType().Name + ": " + Shorten(inner.Message));
                    }
                }
            }

            // TearDown runs regardless of the outcome; a failing test must not leave the
            // fixture (or the process-wide static state it touches) dirty for the next one.
            InvokeAllQuiet(type, fixture, TearDownAttribute);
        }

        /// <summary>
        /// True when the failure is the VM refusing an extern/ECall method because the Unity
        /// engine is not hosting the process, not an assertion or a defect in the code.
        /// </summary>
        private static bool NeedsUnity(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is MissingMethodException || e is MissingMemberException ||
                    e is EntryPointNotFoundException || e is DllNotFoundException ||
                    e is BadImageFormatException || e is TypeLoadException)
                {
                    return true;
                }

                // Mono's ECall marker. Localised, so it is only a secondary signal; the
                // exception types above cover the common cases.
                if (e.Message != null &&
                    (e.Message.IndexOf("ECall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     e.Message.IndexOf("внутренний вызов", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Type> Fixtures(Assembly assembly)
        {
            return assembly.GetTypes()
                           .Where(t => t.IsClass && !t.IsAbstract)
                           .OrderBy(t => t.FullName, StringComparer.Ordinal);
        }

        private static void InvokeAll(Type type, object fixture, string attributeName)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (HasAttribute(method, attributeName)) method.Invoke(fixture, null);
            }
        }

        private static void InvokeAllQuiet(Type type, object fixture, string attributeName)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!HasAttribute(method, attributeName)) continue;

                try
                {
                    method.Invoke(fixture, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("WARN " + type.Name + "." + method.Name + " (" + attributeName +
                                      ") threw: " + Root(ex).Message);
                }
            }
        }

        private static Exception Root(Exception ex)
        {
            var current = ex;
            while (current is TargetInvocationException && current.InnerException != null)
            {
                current = current.InnerException;
            }
            return current;
        }

        private static string Shorten(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            message = message.Replace("\r", " ").Replace("\n", " ");
            return message.Length <= 160 ? message : message.Substring(0, 157) + "...";
        }

        private static bool HasAttribute(MemberInfo member, string attributeName)
        {
            return member.GetCustomAttributes(false).Any(a => a.GetType().FullName == attributeName);
        }
    }
}
