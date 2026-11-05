// Unity 테스트 DLL을 에디터 없이 돌린다: [OneTimeSetUp] → [Test] → [OneTimeTearDown]을 리플렉션으로 부른다.
using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Program
{
    static int Main(string[] args)
    {
        string proj = args[0], bin = Path.Combine(proj, "Temp", "bin", "Debug");
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (var dir in new[] { bin }.Concat(args.Skip(1)))
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };
        Directory.SetCurrentDirectory(proj);
        var asm = Assembly.LoadFrom(Path.Combine(bin, "Colosseum.Tests.dll"));
        int pass = 0, fail = 0;
        bool Has(MemberInfo m, string attr) => m.GetCustomAttributes(true).Any(a => a.GetType().Name == attr);
        // TEST_FILTER=이름 일부 이면 그 테스트만 (480팀 세계라 전체는 15분쯤 걸린다)
        string filter = Environment.GetEnvironmentVariable("TEST_FILTER");
        bool Pick(Type t, MethodInfo m) => string.IsNullOrEmpty(filter) || $"{t.Name}.{m.Name}".Contains(filter);
        foreach (var t in asm.GetTypes().Where(t => t.GetMethods().Any(m => Has(m, "TestAttribute") && Pick(t, m))))
        {
            var inst = Activator.CreateInstance(t);
            foreach (var m in t.GetMethods().Where(m => Has(m, "OneTimeSetUpAttribute"))) m.Invoke(inst, null);
            foreach (var m in t.GetMethods().Where(m => Has(m, "TestAttribute") && Pick(t, m)))
            {
                try { m.Invoke(inst, null); pass++; Console.WriteLine($"PASS {t.Name}.{m.Name}"); }
                catch (TargetInvocationException ex) { fail++; Console.WriteLine($"FAIL {t.Name}.{m.Name}: {ex.InnerException?.Message?.Split('\n').Take(6).Aggregate((a, b) => a + "\n   " + b)}"); }
            }
            foreach (var m in t.GetMethods().Where(m => Has(m, "OneTimeTearDownAttribute"))) m.Invoke(inst, null);
        }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail;
    }
}
