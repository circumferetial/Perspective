namespace Perspective.Experiment.Subspace;

/// <summary>实仿射二次曲线的粗略分类（圆是椭圆的特例，单独列出）。</summary>
public enum ConicKind
{
    Circle,
    Ellipse,
    Parabola,
    Hyperbola,
    Point,
    ImaginaryEllipse,
    IntersectingLines,
    ParallelLines,
    CoincidentLines,
    Degenerate,
    Unknown
}
