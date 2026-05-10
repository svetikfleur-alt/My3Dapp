using FormaCore.Core;

namespace FormaCore.Engine;

public interface IGeometryBackend
{
    object CreateBox(double width, double depth, double height);
    object CreateCylinder(double radius, double height);
    object CreateSphere(double radius);

    object CreateCone(double rTop, double rBottom, double height);
    object CreatePipe(double outer, double inner, double height);
    object CreateTorus(double major, double minor);
    object CreatePyramid(double baseWidth, double baseDepth, double height);
    object CreateWedge(double width, double depth, double height);
    object CreateEllipsoid(double radiusX, double radiusY, double radiusZ);
    object CreateCapsule(double radius, double height);
    object CreateHemisphere(double radius);
    object CreatePrism(double radius, double height, int sides);
    object CreateDisk(double outerRadius, double innerRadius);
    object CreateArrow(double shaftRadius, double shaftHeight, double headRadius, double headHeight);
    object CreateIcosphere(double radius, int subdivisions);
    object CreateTetrahedron(double radius);
    object CreateOctahedron(double radius);
    object CreateIcosahedron(double radius);
    object CreateSpring(double coilRadius, double tubeRadius, int coils, double height);

    object Translate(object shape, double x, double y, double z);

    object Union(object a, object b);
    object Subtract(object a, object b);
    object Intersect(object a, object b);
}