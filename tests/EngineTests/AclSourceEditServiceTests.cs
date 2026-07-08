using FormaCore.Engine.Acl;

namespace FormaCore.Tests.EngineTests;

public sealed class AclSourceEditServiceTests
{
    private readonly IAclSourceEditService _editService = new AclSourceEditService();

    [Fact]
    public void AppendFeatureToPart_SingleLine_InsertsCorrectly()
    {
        var source = "part plate { box(); }";
        var result = _editService.AppendFeatureToPart(source, "plate", "let h = hole();");

        var expected = "part plate { box(); \n    let h = hole();\n}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFeatureToPart_MultiLine_InsertsCorrectly()
    {
        var source = @"part plate {
    box();
}";
        var result = _editService.AppendFeatureToPart(source, "plate", "let h = hole();");

        var expected = @"part plate {
    box();
    let h = hole();
}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFeatureToPart_EmptyPart_InsertsCorrectly()
    {
        var source = "part plate {}";
        var result = _editService.AppendFeatureToPart(source, "plate", "box();");

        var expected = "part plate {\n    box();\n}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFeatureToPart_NoMatch_AppendsAtEnd()
    {
        var source = "part plate { }";
        var result = _editService.AppendFeatureToPart(source, "other", "box();");

        var expected = "part plate { }\nbox();\n";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFeatureToSketch_MultiLine_InsertsCorrectly()
    {
        var source = @"sketch mySketch on Top {
    line(p1, p2);
}";
        var result = _editService.AppendFeatureToSketch(source, "mySketch", "circle();");

        var expected = @"sketch mySketch on Top {
    line(p1, p2);
    circle();
}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendBlock_AddsNewlineIfNeeded()
    {
        var source = "part p { }";
        var result = _editService.AppendBlock(source, "part p2 { }");

        var expected = "part p { }\npart p2 { }\n";
        Assert.Equal(expected, result);
    }
}
