using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// M3：题型模板 + 题板（多问依赖）。
/// 演示：第 (1) 问求弦斜率 → 注入参数 → 第 (2) 问用它求面积（2022 新高考Ⅰ 21）。
/// </summary>
public class ProblemBoardTests
{
    [Fact]
    public void TwoParts_SecondUsesFirstAnswer_2022I21()
    {
        var board = new ProblemBoard();
        var hyperbola = Conic.Hyperbola(ExactScalar.FromFormula("sqrt(2)"), ExactScalar.FromInt(1));
        board.Scene.Facts.AddConic("C", hyperbola);
        board.Scene.Facts.AddPoint("A", HPoint.FromCartesian(2, 1));
        board.Scene.Facts.CurrentLevel = TransformLevel.Projective;

        // 第 (1) 问：弦族斜率条件 ⇒ m = −1
        var part1 = board.Solve(new ChordSlopeTemplate(), new ChordSlopeInput("C", "A"), "21(1)");
        Assert.NotNull(part1.Numeric);
        Assert.Equal(-1.0, part1.Numeric!.Value.Approx, 9);

        // 依赖通道：把 (1) 的结论注入为参数 m
        board.UseAnswerAsParameter("m", part1);

        // 第 (2) 问：用 m 求面积（tan = 2√2）
        var part2 = board.Solve(
            new AreaFromTangentTemplate(),
            new AreaFromTangentInput("C", "A", "m", ExactScalar.FromFormula("2*sqrt(2)")),
            "21(2)");

        Assert.NotNull(part2.Numeric);
        var expected = ExactScalar.FromFormula("16*sqrt(2)/9");
        Assert.Equal(StrictResult.StrictlyTrue, (part2.Numeric!.Value - expected).VerifyStrictZero());

        // 两问都留在题板上（可追溯）
        Assert.Equal(2, board.Scene.Answers.Count);
        Assert.Equal("21(1)", board.Scene.Answers[0].PartId);
        Assert.Equal("21(2)", board.Scene.Answers[1].PartId);
    }

    [Fact]
    public void ProveClaimTemplate_UsesFactBase()
    {
        var board = new ProblemBoard();
        board.Scene.Facts.AddLine("L1", new HLine(0, 1, 0));
        board.Scene.Facts.AddLine("L2", new HLine(0, 1, -1));
        board.Scene.Facts.AddFact(ParallelFact.Create("L1", "L2"), "已知平行");

        var answer = board.Solve(
            new ProveClaimTemplate(new TheoremEngine()),
            new ProveClaimInput(new ClaimParallel("L1", "L2")),
            "证明(1)");

        Assert.Contains("ProvedByTheoremChain", answer.Summary);
    }

    [Fact]
    public void InjectingNonNumericAnswer_IsRejected()
    {
        var board = new ProblemBoard();
        var slopeAnswer = new ProblemAnswer("21(1)", "SlopeSet", "集合答案，无数值");

        Assert.Throws<InvalidOperationException>(() =>
            board.UseAnswerAsParameter("m", slopeAnswer));
    }

    [Fact]
    public void MissingParameter_IsReportedClearly()
    {
        var board = new ProblemBoard();
        Assert.Throws<InvalidOperationException>(() => board.Scene.GetParameter("m"));
    }
}
