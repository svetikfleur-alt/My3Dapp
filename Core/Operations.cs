namespace FormaCore.Core;

// --- Перемещение ---
public sealed class TranslateNode : ShapeNode
{
    public ShapeNode Child { get; }
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public TranslateNode(ShapeNode child, double x, double y, double z)
    {
        Child = child;
        X = x;
        Y = y;
        Z = z;
    }
}

// --- Объединение ---
public sealed class UnionNode : ShapeNode
{
    public ShapeNode A { get; }
    public ShapeNode B { get; }

    public UnionNode(ShapeNode a, ShapeNode b)
    {
        A = a;
        B = b;
    }
}

// --- Вычитание ---
public sealed class SubtractNode : ShapeNode
{
    public ShapeNode A { get; }
    public ShapeNode B { get; }

    public SubtractNode(ShapeNode a, ShapeNode b)
    {
        A = a;
        B = b;
    }
}

// --- Пересечение ---
public sealed class IntersectNode : ShapeNode
{
    public ShapeNode A { get; }
    public ShapeNode B { get; }

    public IntersectNode(ShapeNode a, ShapeNode b)
    {
        A = a;
        B = b;
    }
}