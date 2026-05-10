using FormaCore.Engine;

var store = new CadProjectStore();

store.Apply(new CadCommandAction(CadCommandActionKind.StartSketch));
store.Apply(new CadCommandAction(CadCommandActionKind.SetSketchTool, SketchTool: CadSketchToolKind.Rectangle));
var rectPlace = store.Apply(new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: 5d, V: 7d));
var rectFinish = store.Apply(new CadCommandAction(CadCommandActionKind.FinishSketch));
var rectExtrude = store.Apply(new CadCommandAction(CadCommandActionKind.ExtrudeSelectedSketch, Amount: 10d));
var rectSolid = store.Compile().Bodies.First().Solid.GetType().Name;

Console.WriteLine($"RECT_PLACE={rectPlace.IsSuccess}");
Console.WriteLine($"RECT_FINISH={rectFinish.IsSuccess}");
Console.WriteLine($"RECT_EXTRUDE={rectExtrude.IsSuccess}");
Console.WriteLine($"RECT_SOLID={rectSolid}");

var top = store.Project.Scene.ReferencePlanes.First(x => x.Kind == CadReferencePlaneKind.Top);
store.Apply(new CadCommandAction(CadCommandActionKind.SelectPlane, EntityId: top.Id));
store.Apply(new CadCommandAction(CadCommandActionKind.StartSketch));
store.Apply(new CadCommandAction(CadCommandActionKind.SetSketchTool, SketchTool: CadSketchToolKind.Line));
store.Apply(new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: 0d, V: 0d));
store.Apply(new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: 20d, V: 0.1d));
store.Apply(new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: 20.2d, V: 10d));
store.Apply(new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: -0.1d, V: 10.2d));
var closeLoop = store.Apply(new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: 0.3d, V: 0.2d));
var lineFinish = store.Apply(new CadCommandAction(CadCommandActionKind.FinishSketch));
var lineExtrude = store.Apply(new CadCommandAction(CadCommandActionKind.ExtrudeSelectedSketch, Amount: 6d));
var solids = store.Compile().Bodies.Select(b => b.Solid.GetType().Name).ToArray();

Console.WriteLine($"LINE_CLOSE={closeLoop.Message}");
Console.WriteLine($"LINE_FINISH={lineFinish.IsSuccess}");
Console.WriteLine($"LINE_EXTRUDE={lineExtrude.IsSuccess}");
Console.WriteLine($"SOLIDS={string.Join(',', solids)}");
