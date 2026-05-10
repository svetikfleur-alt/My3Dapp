namespace FormaCore.Core;

public struct Vertex
{
    public double X;
    public double Y;
    public double Z;

    public Vertex(double x, double y, double z)
    {
        X = x; Y = y; Z = z;
    }
}

public struct Triangle
{
    public int A;
    public int B;
    public int C;

    public Triangle(int a, int b, int c)
    {
        A = a; B = b; C = c;
    }
}

public class Mesh
{
    public List<Vertex> Vertices { get; } = new();
    public List<Triangle> Triangles { get; } = new();
}