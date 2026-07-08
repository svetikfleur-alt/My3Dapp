namespace FormaCore.Engine.Acl;

public interface IAclSourceEditService
{
    string AppendFeatureToPart(string source, string partName, string featureCode);
    string AppendFeatureToSketch(string source, string sketchName, string featureCode);
    string AppendBlock(string source, string blockCode);
}

public sealed class AclSourceEditService : IAclSourceEditService
{
    public string AppendFeatureToPart(string source, string partName, string featureCode)
    {
        var lexer = new AclLexer(source);
        var parser = new AclParser(lexer);
        var doc = parser.ParseDocument();

        var part = doc.Nodes.OfType<AclPartDeclaration>().FirstOrDefault(p => p.Name == partName);
        if (part != null)
        {
            int insertPos = GetOffset(source, part.Span.EndLine, part.Span.EndColumn - 1);
            if (insertPos >= 0)
            {
                return InsertWithFormatting(source, insertPos, featureCode);
            }
        }
        
        return source + "\n" + featureCode + "\n";
    }

    public string AppendFeatureToSketch(string source, string sketchName, string featureCode)
    {
        var lexer = new AclLexer(source);
        var parser = new AclParser(lexer);
        var doc = parser.ParseDocument();

        var sketch = doc.Nodes.OfType<AclSketchDeclaration>().FirstOrDefault(s => s.Name == sketchName);
        if (sketch != null)
        {
            int insertPos = GetOffset(source, sketch.Span.EndLine, sketch.Span.EndColumn - 1);
            if (insertPos >= 0)
            {
                return InsertWithFormatting(source, insertPos, featureCode);
            }
        }

        return source + "\n" + featureCode + "\n";
    }

    public string AppendBlock(string source, string blockCode)
    {
        if (!source.EndsWith("\n")) source += "\n";
        return source + blockCode + "\n";
    }

    private string InsertWithFormatting(string source, int insertPos, string featureCode)
    {
        int i = insertPos - 1;
        bool hasNewlineBefore = false;
        
        while (i >= 0)
        {
            if (source[i] == '\n')
            {
                hasNewlineBefore = true;
                break;
            }
            if (source[i] != ' ' && source[i] != '\t' && source[i] != '\r')
            {
                break;
            }
            i--;
        }
        
        if (!hasNewlineBefore)
        {
            return source.Insert(insertPos, "\n    " + featureCode + "\n");
        }
        else
        {
            return source.Insert(insertPos, "    " + featureCode + "\n");
        }
    }

    private int GetOffset(string source, int line, int column)
    {
        int currentLine = 1;
        int currentColumn = 1;

        for (int i = 0; i < source.Length; i++)
        {
            if (currentLine == line && currentColumn == column)
            {
                return i;
            }

            if (source[i] == '\n')
            {
                currentLine++;
                currentColumn = 1;
            }
            else
            {
                currentColumn++;
            }
        }

        if (currentLine == line && currentColumn == column)
        {
            return source.Length;
        }

        return -1;
    }
}