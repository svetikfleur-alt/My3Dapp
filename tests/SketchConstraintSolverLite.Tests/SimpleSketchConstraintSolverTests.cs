using FormaCore.Engine;
using Xunit;

namespace SketchConstraintSolverLite.Tests;

public sealed class SimpleSketchConstraintSolverTests
{
    private readonly SimpleSketchConstraintSolver _solver = new();

    [Fact]
    public void HorizontalConstraint_MakesLineHorizontal()
    {
        var line = new CadSketchLine
        {
            StartX = 1,
            StartY = 2,
            EndX = 5,
            EndY = 7
        };

        var result = _solver.Solve(new SolverInput
        {
            Entities = [line],
            Constraints =
            [
                new CadSketchConstraint(CadSketchConstraintKind.Horizontal, "Keep line horizontal.", [line.Id])
            ]
        });

        var updatedLine = Assert.IsType<CadSketchLine>(Assert.Single(result.UpdatedEntities));
        Assert.Equal(updatedLine.StartY, updatedLine.EndY, 6);
        Assert.Equal(CadSketchSolveStatus.Underdefined, result.Status);
        Assert.Equal(CadSketchConstraintStatus.Active, Assert.Single(result.Constraints).Status);
    }

    [Fact]
    public void VerticalConstraint_MakesLineVertical()
    {
        var line = new CadSketchLine
        {
            StartX = -2,
            StartY = 1,
            EndX = 3,
            EndY = 9
        };

        var result = _solver.Solve(new SolverInput
        {
            Entities = [line],
            Constraints =
            [
                new CadSketchConstraint(CadSketchConstraintKind.Vertical, "Keep line vertical.", [line.Id])
            ]
        });

        var updatedLine = Assert.IsType<CadSketchLine>(Assert.Single(result.UpdatedEntities));
        Assert.Equal(updatedLine.StartX, updatedLine.EndX, 6);
        Assert.Equal(CadSketchConstraintStatus.Active, Assert.Single(result.Constraints).Status);
    }

    [Theory]
    [InlineData(CadSketchDimensionKind.Radius, 7.5, 7.5)]
    [InlineData(CadSketchDimensionKind.Diameter, 15.0, 7.5)]
    public void CircleDimension_DrivesCircleRadius(CadSketchDimensionKind kind, double value, double expectedRadius)
    {
        var circle = new CadSketchCircle
        {
            CenterX = 0,
            CenterY = 0,
            Radius = 2
        };

        var result = _solver.Solve(new SolverInput
        {
            Entities = [circle],
            Dimensions =
            [
                new CadSketchDimension(kind, kind.ToString(), value, [circle.Id], kind == CadSketchDimensionKind.Radius ? "Radius" : "Diameter")
            ]
        });

        var updatedCircle = Assert.IsType<CadSketchCircle>(Assert.Single(result.UpdatedEntities));
        Assert.Equal(expectedRadius, updatedCircle.Radius, 6);
        Assert.Equal(CadSketchDimensionStatus.Active, Assert.Single(result.Dimensions).Status);
    }

    [Fact]
    public void InvalidConstraintReference_FailsHonestly()
    {
        var line = new CadSketchLine
        {
            StartX = 0,
            StartY = 0,
            EndX = 1,
            EndY = 1
        };

        var result = _solver.Solve(new SolverInput
        {
            Entities = [line],
            Constraints =
            [
                new CadSketchConstraint(CadSketchConstraintKind.Horizontal, "Broken reference.", [Guid.NewGuid()])
            ]
        });

        var constraint = Assert.Single(result.Constraints);
        Assert.Equal(CadSketchSolveStatus.Failed, result.Status);
        Assert.Equal(CadSketchConstraintStatus.Failed, constraint.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void UnsupportedConstraintType_IsMarkedUnsupported()
    {
        var lineA = new CadSketchLine { StartX = 0, StartY = 0, EndX = 10, EndY = 0 };
        var lineB = new CadSketchLine { StartX = 0, StartY = 2, EndX = 10, EndY = 3 };

        var result = _solver.Solve(new SolverInput
        {
            Entities = [lineA, lineB],
            Constraints =
            [
                new CadSketchConstraint(CadSketchConstraintKind.Tangent, "Tangent is outside the lite subset.", [lineA.Id, lineB.Id])
            ]
        });

        var constraint = Assert.Single(result.Constraints);
        Assert.Equal(CadSketchSolveStatus.Unsupported, result.Status);
        Assert.Equal(CadSketchConstraintStatus.Unsupported, constraint.Status);
        Assert.NotEmpty(result.Warnings);
    }
}
