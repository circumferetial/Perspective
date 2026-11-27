using System.Diagnostics;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>Subspace 全部演示的统一入口：`dotnet run --project Perspective.Experiment -- --subspace`。</summary>
public static class SubspaceDemos
{
    public static void RunAll()
    {
        var demos = new (string Name, Action Run)[]
        {
            ("SubspaceExperiments", SubspaceExperiments.RunAll),
            ("ExamSpike2020Shanghai20", ExamSpike2020Shanghai20.Run),
            ("UnifiedAlgebraExperiment", UnifiedAlgebraExperiment.Run),
            ("DiscoveryPrototypeExperiment", DiscoveryPrototypeExperiment.Run),
            ("ComplexGeometryExperiment", ComplexGeometryExperiment.Run),
            ("ParabolicGeometryDemo", ParabolicGeometryDemo.Run),
            ("HyperbolicGeometryDemo", HyperbolicGeometryDemo.Run),
            ("ProblemP1Demo", ProblemP1Demo.Run),
            ("ProblemP1SymbolicDemo", ProblemP1SymbolicDemo.Run),
            ("ProblemP1SymbolicControlDemo", ProblemP1SymbolicControlDemo.Run),
            ("ProblemP1NaiveDemo", ProblemP1NaiveDemo.Run),
            ("ProblemP2Demo", ProblemP2Demo.Run),
            ("ProblemP3Demo", ProblemP3Demo.Run),
            ("RationalRangeDemo", RationalRangeDemo.Run),
        };

        foreach (var (name, run) in demos)
        {
            Console.WriteLine($"\n########## {name} ##########");
            Console.Out.Flush();
            var sw = Stopwatch.StartNew();
            run();
            Console.WriteLine($"########## {name} done in {sw.ElapsedMilliseconds} ms ##########");
            Console.Out.Flush();
        }
    }
}
