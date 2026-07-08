// Native OCCT core. Compiled without /clr (CompileAsManaged=false).
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>

#include "OcctCore.h"

#include <TopoDS_Shape.hxx>
#include <TopoDS.hxx>
#include <TopAbs_ShapeEnum.hxx>
#include <TopExp.hxx>
#include <TopTools_IndexedMapOfShape.hxx>
#include <BRepPrimAPI_MakeBox.hxx>
#include <BRepPrimAPI_MakeCylinder.hxx>
#include <BRepPrimAPI_MakePrism.hxx>
#include <BRepBuilderAPI_MakeEdge.hxx>
#include <BRepBuilderAPI_MakeWire.hxx>
#include <BRepBuilderAPI_MakeFace.hxx>
#include <GC_MakeCircle.hxx>
#include <gp_Circ.hxx>
#include <TopoDS_Compound.hxx>
#include <BRep_Builder.hxx>
#include <BRepAlgoAPI_Cut.hxx>
#include <BRepAlgoAPI_Fuse.hxx>
#include <BRepBuilderAPI_Transform.hxx>
#include <gp_Trsf.hxx>
#include <gp_Vec.hxx>
#include <Bnd_Box.hxx>
#include <BRepBndLib.hxx>
#include <STEPControl_Writer.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <Interface_Static.hxx>

#include <Aspect_DisplayConnection.hxx>
#include <OpenGl_GraphicDriver.hxx>
#include <V3d_Viewer.hxx>
#include <V3d_View.hxx>
#include <WNT_Window.hxx>
#include <AIS_InteractiveContext.hxx>
#include <AIS_Shape.hxx>
#include <StdSelect_BRepOwner.hxx>
#include <Prs3d_Drawer.hxx>
#include <Prs3d_LineAspect.hxx>
#include <Prs3d_ShadingAspect.hxx>
#include <Graphic3d_MaterialAspect.hxx>
#include <Aspect_GradientFillMethod.hxx>
#include <Quantity_Color.hxx>
#include <Standard_Failure.hxx>
#include <NCollection_DataMap.hxx>

#include <string>
#include <map>

static thread_local std::wstring g_lastError;

static void SetError(const char* msg)
{
    std::string s(msg ? msg : "unknown OCCT error");
    g_lastError.assign(s.begin(), s.end());
}

const wchar_t* OcctCore_LastError() { return g_lastError.c_str(); }

struct OcctShape
{
    TopoDS_Shape shape;
};

// ---------------- kernel ----------------

OcctShape* OcctCore_MakeBox(double dx, double dy, double dz)
{
    try
    {
        if (dx <= 0 || dy <= 0 || dz <= 0) { SetError("box dimensions must be positive"); return nullptr; }
        BRepPrimAPI_MakeBox mk(dx, dy, dz);
        mk.Build();
        if (!mk.IsDone()) { SetError("MakeBox failed"); return nullptr; }
        return new OcctShape{ mk.Shape() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakeBox: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_MakeCylinder(double radius, double height)
{
    try
    {
        if (radius <= 0 || height <= 0) { SetError("cylinder radius/height must be positive"); return nullptr; }
        BRepPrimAPI_MakeCylinder mk(radius, height);
        mk.Build();
        if (!mk.IsDone()) { SetError("MakeCylinder failed"); return nullptr; }
        return new OcctShape{ mk.Shape() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakeCylinder: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_MakeWire(const double* points2d, int ptCount, int closed)
{
    try
    {
        if (ptCount < 2) { SetError("wire needs at least 2 points"); return nullptr; }
        BRepBuilderAPI_MakeWire mkWire;
        for (int i = 0; i < ptCount - 1; i++)
        {
            gp_Pnt p1(points2d[i * 2], points2d[i * 2 + 1], 0);
            gp_Pnt p2(points2d[(i + 1) * 2], points2d[(i + 1) * 2 + 1], 0);
            BRepBuilderAPI_MakeEdge mkEdge(p1, p2);
            mkWire.Add(mkEdge.Edge());
        }
        if (closed && ptCount > 2)
        {
            gp_Pnt p1(points2d[(ptCount - 1) * 2], points2d[(ptCount - 1) * 2 + 1], 0);
            gp_Pnt p2(points2d[0], points2d[1], 0);
            if (!p1.IsEqual(p2, 1e-6))
            {
                BRepBuilderAPI_MakeEdge mkEdge(p1, p2);
                mkWire.Add(mkEdge.Edge());
            }
        }
        mkWire.Build();
        if (!mkWire.IsDone()) { SetError("MakeWire failed"); return nullptr; }
        return new OcctShape{ mkWire.Wire() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakeWire: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_MakeCircleWire(double radius)
{
    try
    {
        if (radius <= 0) { SetError("circle radius must be positive"); return nullptr; }
        gp_Ax2 axis(gp_Pnt(0, 0, 0), gp_Dir(0, 0, 1));
        gp_Circ circ(axis, radius);
        BRepBuilderAPI_MakeEdge mkEdge(circ);
        BRepBuilderAPI_MakeWire mkWire(mkEdge.Edge());
        if (!mkWire.IsDone()) { SetError("MakeCircleWire failed"); return nullptr; }
        return new OcctShape{ mkWire.Wire() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakeCircleWire: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_MakeFace(OcctShape* wire)
{
    try
    {
        if (!wire || wire->shape.IsNull()) { SetError("invalid wire"); return nullptr; }
        if (wire->shape.ShapeType() == TopAbs_COMPOUND)
        {
            TopTools_IndexedMapOfShape map;
            TopExp::MapShapes(wire->shape, TopAbs_WIRE, map);
            if (map.Extent() == 0) { SetError("MakeFace: no wires found in compound"); return nullptr; }
            
            TopoDS_Wire outerWire = TopoDS::Wire(map.FindKey(1));
            BRepBuilderAPI_MakeFace mkFace(outerWire, true);
            for (int i = 2; i <= map.Extent(); ++i)
            {
                TopoDS_Wire innerWire = TopoDS::Wire(map.FindKey(i));
                mkFace.Add(innerWire);
            }
            mkFace.Build();
            if (!mkFace.IsDone()) { SetError("MakeFace failed for compound"); return nullptr; }
            return new OcctShape{ mkFace.Face() };
        }
        else
        {
            BRepBuilderAPI_MakeFace mkFace(TopoDS::Wire(wire->shape), true);
            mkFace.Build();
            if (!mkFace.IsDone()) { SetError("MakeFace failed"); return nullptr; }
            return new OcctShape{ mkFace.Face() };
        }
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakeFace: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_MakePrism(OcctShape* baseFace, double dx, double dy, double dz)
{
    try
    {
        if (!baseFace || baseFace->shape.IsNull()) { SetError("invalid base face"); return nullptr; }
        gp_Vec vec(dx, dy, dz);
        if (vec.Magnitude() <= 1e-6) { SetError("prism vector is too short"); return nullptr; }
        BRepPrimAPI_MakePrism mkPrism(baseFace->shape, vec);
        mkPrism.Build();
        if (!mkPrism.IsDone()) { SetError("MakePrism failed"); return nullptr; }
        return new OcctShape{ mkPrism.Shape() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakePrism: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_MakeCompound(OcctShape** shapes, int shapeCount)
{
    try
    {
        TopoDS_Compound comp;
        BRep_Builder builder;
        builder.MakeCompound(comp);
        for (int i = 0; i < shapeCount; i++)
        {
            if (shapes[i] && !shapes[i]->shape.IsNull())
            {
                builder.Add(comp, shapes[i]->shape);
            }
        }
        return new OcctShape{ comp };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("MakeCompound: unknown failure"); return nullptr; }
}

static OcctShape* RunBoolean(OcctShape* a, OcctShape* b, bool cut)
{
    try
    {
        if (!a || !b) { SetError("boolean: null operand"); return nullptr; }
        if (cut)
        {
            BRepAlgoAPI_Cut op(a->shape, b->shape);
            op.Build();
            if (!op.IsDone()) { SetError("BooleanCut failed"); return nullptr; }
            return new OcctShape{ op.Shape() };
        }
        BRepAlgoAPI_Fuse op(a->shape, b->shape);
        op.Build();
        if (!op.IsDone()) { SetError("BooleanFuse failed"); return nullptr; }
        return new OcctShape{ op.Shape() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("boolean: unknown failure"); return nullptr; }
}

OcctShape* OcctCore_BooleanCut(OcctShape* target, OcctShape* tool) { return RunBoolean(target, tool, true); }
OcctShape* OcctCore_BooleanFuse(OcctShape* a, OcctShape* b) { return RunBoolean(a, b, false); }

OcctShape* OcctCore_Translate(OcctShape* shape, double dx, double dy, double dz)
{
    try
    {
        if (!OcctCore_ShapeIsValid(shape)) { SetError("translate: invalid shape"); return nullptr; }
        gp_Trsf trsf;
        trsf.SetTranslation(gp_Vec(dx, dy, dz));
        BRepBuilderAPI_Transform op(shape->shape, trsf, true);
        if (!op.IsDone()) { SetError("translate failed"); return nullptr; }
        return new OcctShape{ op.Shape() };
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("translate: unknown failure"); return nullptr; }
}

void OcctCore_FreeShape(OcctShape* shape) { delete shape; }

int OcctCore_ShapeIsValid(OcctShape* shape)
{
    return (shape != nullptr && !shape->shape.IsNull()) ? 1 : 0;
}

int OcctCore_ShapeBounds(OcctShape* shape, double* xmin, double* ymin, double* zmin,
                         double* xmax, double* ymax, double* zmax)
{
    try
    {
        if (!OcctCore_ShapeIsValid(shape)) { SetError("bounds: invalid shape"); return 0; }
        Bnd_Box box;
        BRepBndLib::Add(shape->shape, box);
        if (box.IsVoid()) { SetError("bounds: empty box"); return 0; }
        box.Get(*xmin, *ymin, *zmin, *xmax, *ymax, *zmax);
        return 1;
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return 0; }
    catch (...) { SetError("bounds: unknown failure"); return 0; }
}

// ---------------- STEP ----------------

int OcctCore_WriteStep(OcctShape* shape, const wchar_t* path)
{
    try
    {
        if (!OcctCore_ShapeIsValid(shape)) { SetError("step: invalid shape"); return 0; }
        STEPControl_Writer writer;
        Interface_Static::SetCVal("write.step.schema", "AP214");
        if (writer.Transfer(shape->shape, STEPControl_AsIs) != IFSelect_RetDone)
        {
            SetError("step: transfer failed");
            return 0;
        }
        // OCCT expects a narrow path; convert UTF-16 -> UTF-8.
        int len = WideCharToMultiByte(CP_UTF8, 0, path, -1, nullptr, 0, nullptr, nullptr);
        std::string utf8(len, '\0');
        WideCharToMultiByte(CP_UTF8, 0, path, -1, utf8.data(), len, nullptr, nullptr);
        if (writer.Write(utf8.c_str()) != IFSelect_RetDone)
        {
            SetError("step: write failed");
            return 0;
        }
        return 1;
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return 0; }
    catch (...) { SetError("step: unknown failure"); return 0; }
}

// ---------------- viewer ----------------

struct OcctViewerCore
{
    HWND hwnd = nullptr;
    Handle(V3d_Viewer) viewer;
    Handle(V3d_View) view;
    Handle(AIS_InteractiveContext) ctx;
    std::map<int, Handle(AIS_Shape)> slots;
    OcctPickCallback pickCb = nullptr;
    int displayMode = 1; // ShadedWithEdges default
    bool selBody = true, selFace = false, selEdge = false, selVertex = false;
    int lastX = 0, lastY = 0;
    bool rotating = false, panning = false;
    int downX = 0, downY = 0;
    int hoverSlot = -1, hoverKind = -1, hoverIndex = -1;
};

static const wchar_t* kWndClass = L"My3DAppOcctViewport";

static int KindToSelMode(int kind)
{
    switch (kind)
    {
    case 1: return AIS_Shape::SelectionMode(TopAbs_FACE);
    case 2: return AIS_Shape::SelectionMode(TopAbs_EDGE);
    case 3: return AIS_Shape::SelectionMode(TopAbs_VERTEX);
    default: return 0; // whole shape
    }
}

static void ApplySelectionModes(OcctViewerCore* c, const Handle(AIS_Shape)& ais)
{
    c->ctx->Deactivate(ais);
    if (c->selBody)   c->ctx->Activate(ais, 0);
    if (c->selFace)   c->ctx->Activate(ais, KindToSelMode(1));
    if (c->selEdge)   c->ctx->Activate(ais, KindToSelMode(2));
    if (c->selVertex) c->ctx->Activate(ais, KindToSelMode(3));
}

static int SlotOfInteractive(OcctViewerCore* c, const Handle(AIS_InteractiveObject)& obj)
{
    for (auto& kv : c->slots)
        if (kv.second == obj)
            return kv.first;
    return -1;
}

// Resolve a BRep owner pick to (slot, kind, subIndex).
static bool ResolvePick(OcctViewerCore* c, const Handle(SelectMgr_EntityOwner)& owner,
                        int& slot, int& kind, int& subIndex)
{
    if (owner.IsNull()) return false;
    Handle(AIS_InteractiveObject) obj = Handle(AIS_InteractiveObject)::DownCast(owner->Selectable());
    slot = SlotOfInteractive(c, obj);
    if (slot < 0) return false;

    Handle(StdSelect_BRepOwner) brepOwner = Handle(StdSelect_BRepOwner)::DownCast(owner);
    if (brepOwner.IsNull() || !brepOwner->HasShape())
    {
        kind = 0; subIndex = 0;
        return true;
    }
    const TopoDS_Shape& picked = brepOwner->Shape();
    Handle(AIS_Shape) ais = Handle(AIS_Shape)::DownCast(obj);
    if (ais.IsNull()) { kind = 0; subIndex = 0; return true; }
    const TopoDS_Shape& full = ais->Shape();

    TopAbs_ShapeEnum t = picked.ShapeType();
    switch (t)
    {
    case TopAbs_FACE:   kind = 1; break;
    case TopAbs_EDGE:   kind = 2; break;
    case TopAbs_VERTEX: kind = 3; break;
    default:            kind = 0; subIndex = 0; return true;
    }
    // TRANSIENT index: valid only for this unchanged shape instance (see TopologySelection docs).
    TopTools_IndexedMapOfShape map;
    TopExp::MapShapes(full, t, map);
    subIndex = map.FindIndex(picked); // 0 if not found
    return true;
}

static void EmitHover(OcctViewerCore* c, int slot, int kind, int subIndex)
{
    if (slot == c->hoverSlot && kind == c->hoverKind && subIndex == c->hoverIndex) return;
    c->hoverSlot = slot; c->hoverKind = kind; c->hoverIndex = subIndex;
    if (c->pickCb) c->pickCb(slot, kind, subIndex, 1);
}

static LRESULT CALLBACK ViewportWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    OcctViewerCore* c = reinterpret_cast<OcctViewerCore*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (!c || c->view.IsNull())
        return DefWindowProcW(hwnd, msg, wParam, lParam);

    const int x = GET_X_LPARAM(lParam);
    const int y = GET_Y_LPARAM(lParam);

    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        BeginPaint(hwnd, &ps);
        c->view->Redraw();
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_SIZE:
        c->view->MustBeResized();
        return 0;
    case WM_LBUTTONDOWN:
        c->downX = x; c->downY = y;
        SetFocus(hwnd);
        SetCapture(hwnd);
        return 0;
    case WM_LBUTTONUP:
    {
        ReleaseCapture();
        if (abs(x - c->downX) <= 3 && abs(y - c->downY) <= 3)
        {
            c->ctx->MoveTo(x, y, c->view, false);
            c->ctx->SelectDetected(AIS_SelectionScheme_Replace);
            c->view->Redraw();

            int slot = -1, kind = 0, subIndex = 0;
            bool got = false;
            for (c->ctx->InitSelected(); c->ctx->MoreSelected(); c->ctx->NextSelected())
            {
                if (ResolvePick(c, c->ctx->SelectedOwner(), slot, kind, subIndex)) { got = true; break; }
            }
            if (c->pickCb)
            {
                if (got) c->pickCb(slot, kind, subIndex, 0);
                else     c->pickCb(-1, 0, -1, 0); // cleared selection
            }
        }
        return 0;
    }
    case WM_RBUTTONDOWN:
        c->rotating = true;
        c->lastX = x; c->lastY = y;
        c->view->StartRotation(x, y);
        SetCapture(hwnd);
        return 0;
    case WM_RBUTTONUP:
        c->rotating = false;
        ReleaseCapture();
        return 0;
    case WM_MBUTTONDOWN:
        c->panning = true;
        c->lastX = x; c->lastY = y;
        SetCapture(hwnd);
        return 0;
    case WM_MBUTTONUP:
        c->panning = false;
        ReleaseCapture();
        return 0;
    case WM_MOUSEMOVE:
    {
        if (c->rotating)
        {
            c->view->Rotation(x, y);
        }
        else if (c->panning)
        {
            c->view->Pan(x - c->lastX, c->lastY - y);
            c->lastX = x; c->lastY = y;
        }
        else
        {
            // hover highlight
            c->ctx->MoveTo(x, y, c->view, true);
            if (c->ctx->HasDetected())
            {
                int slot = -1, kind = 0, subIndex = 0;
                if (ResolvePick(c, c->ctx->DetectedOwner(), slot, kind, subIndex))
                    EmitHover(c, slot, kind, subIndex);
            }
            else
            {
                EmitHover(c, -1, 0, -1);
            }
        }
        return 0;
    }
    case WM_MOUSEWHEEL:
    {
        const short delta = GET_WHEEL_DELTA_WPARAM(wParam);
        c->view->SetZoom(delta > 0 ? 1.12 : 1.0 / 1.12, Standard_True);
        return 0;
    }
    case WM_DESTROY:
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, 0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

static void RegisterWndClassOnce()
{
    static bool done = false;
    if (done) return;
    WNDCLASSW wc = {};
    wc.style = CS_HREDRAW | CS_VREDRAW | CS_OWNDC;
    wc.lpfnWndProc = ViewportWndProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.lpszClassName = kWndClass;
    RegisterClassW(&wc);
    done = true;
}

static void ApplyDisplayMode(OcctViewerCore* c)
{
    const bool wire = (c->displayMode == 2);
    const bool edges = (c->displayMode == 1);
    c->ctx->DefaultDrawer()->SetFaceBoundaryDraw(edges);
    if (edges)
    {
        c->ctx->DefaultDrawer()->FaceBoundaryAspect()->SetColor(Quantity_NOC_BLACK);
        c->ctx->DefaultDrawer()->FaceBoundaryAspect()->SetWidth(1.5);
    }
    for (auto& kv : c->slots)
    {
        c->ctx->SetDisplayMode(kv.second, wire ? AIS_WireFrame : AIS_Shaded, false);
        kv.second->SynchronizeAspects();
        c->ctx->Redisplay(kv.second, false);
    }
    c->view->Redraw();
}

OcctViewerCore* OcctCore_CreateViewer(void* parentHwnd, int width, int height, OcctPickCallback pickCb)
{
    try
    {
        RegisterWndClassOnce();
        auto* c = new OcctViewerCore();
        c->pickCb = pickCb;

        c->hwnd = CreateWindowExW(0, kWndClass, L"", WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS,
                                  0, 0, width > 0 ? width : 100, height > 0 ? height : 100,
                                  reinterpret_cast<HWND>(parentHwnd), nullptr,
                                  GetModuleHandleW(nullptr), nullptr);
        if (!c->hwnd) { SetError("CreateWindowEx failed"); delete c; return nullptr; }
        SetWindowLongPtrW(c->hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(c));

        Handle(Aspect_DisplayConnection) dc = new Aspect_DisplayConnection();
        Handle(OpenGl_GraphicDriver) driver = new OpenGl_GraphicDriver(dc, Standard_False);

        c->viewer = new V3d_Viewer(driver);
        c->viewer->SetDefaultLights();
        c->viewer->SetLightOn();

        c->view = c->viewer->CreateView();
        Handle(WNT_Window) wnd = new WNT_Window(c->hwnd);
        c->view->SetWindow(wnd);
        if (!wnd->IsMapped()) wnd->Map();

        // Neutral professional background: soft vertical gradient, no debug cage.
        c->view->SetBgGradientColors(
            Quantity_Color(0.94, 0.95, 0.97, Quantity_TOC_sRGB),
            Quantity_Color(0.72, 0.75, 0.80, Quantity_TOC_sRGB),
            Aspect_GradientFillMethod_Vertical, Standard_False);
        c->view->TriedronDisplay(Aspect_TOTP_LEFT_LOWER, Quantity_NOC_GRAY30, 0.08, V3d_ZBUFFER);
        c->view->SetProj(V3d_XposYnegZpos); // isometric default

        c->ctx = new AIS_InteractiveContext(c->viewer);
        c->ctx->SetDisplayMode(AIS_Shaded, false);

        // Body presentation: contrasting steel-blue matte material with dark B-Rep edges.
        Handle(Prs3d_Drawer) drawer = c->ctx->DefaultDrawer();
        drawer->SetFaceBoundaryDraw(true);
        drawer->FaceBoundaryAspect()->SetColor(Quantity_Color(0.10, 0.10, 0.12, Quantity_TOC_sRGB));
        drawer->FaceBoundaryAspect()->SetWidth(1.5);
        Graphic3d_MaterialAspect mat(Graphic3d_NameOfMaterial_Aluminum);
        mat.SetShininess(0.08f); // matte, readable curvature
        drawer->ShadingAspect()->SetMaterial(mat);
        drawer->ShadingAspect()->SetColor(Quantity_Color(0.45, 0.55, 0.68, Quantity_TOC_sRGB));

        c->view->MustBeResized();
        c->view->Redraw();
        return c;
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); return nullptr; }
    catch (...) { SetError("CreateViewer: unknown failure"); return nullptr; }
}

void* OcctCore_GetHwnd(OcctViewerCore* core) { return core ? core->hwnd : nullptr; }

void OcctCore_DestroyViewer(OcctViewerCore* core)
{
    if (!core) return;
    try
    {
        if (core->hwnd)
        {
            SetWindowLongPtrW(core->hwnd, GWLP_USERDATA, 0);
            DestroyWindow(core->hwnd);
        }
        core->slots.clear();
        if (!core->ctx.IsNull()) core->ctx->RemoveAll(false);
        if (!core->view.IsNull()) core->view->Remove();
    }
    catch (...) { /* shutdown must not throw across the boundary */ }
    delete core;
}

void OcctCore_Resize(OcctViewerCore* core, int width, int height)
{
    if (!core || !core->hwnd) return;
    SetWindowPos(core->hwnd, nullptr, 0, 0, width, height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    if (!core->view.IsNull())
    {
        core->view->MustBeResized();
        core->view->Redraw();
    }
}

void OcctCore_Display(OcctViewerCore* core, OcctShape* shape, int slot)
{
    if (!core || !OcctCore_ShapeIsValid(shape)) return;
    try
    {
        Handle(AIS_Shape) ais = new AIS_Shape(shape->shape);
        core->slots[slot] = ais;
        core->ctx->Display(ais, core->displayMode == 2 ? AIS_WireFrame : AIS_Shaded, 0, false);
        ApplySelectionModes(core, ais);
        core->view->Redraw();
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); }
}

void OcctCore_UpdateShape(OcctViewerCore* core, OcctShape* shape, int slot)
{
    if (!core || !OcctCore_ShapeIsValid(shape)) return;
    auto it = core->slots.find(slot);
    if (it == core->slots.end()) { OcctCore_Display(core, shape, slot); return; }
    try
    {
        it->second->SetShape(shape->shape);
        core->ctx->Redisplay(it->second, false);
        ApplySelectionModes(core, it->second);
        core->view->Redraw();
    }
    catch (const Standard_Failure& f) { SetError(f.GetMessageString()); }
}

void OcctCore_Erase(OcctViewerCore* core, int slot)
{
    if (!core) return;
    auto it = core->slots.find(slot);
    if (it == core->slots.end()) return;
    core->ctx->Remove(it->second, false);
    core->slots.erase(it);
    core->view->Redraw();
}

void OcctCore_SetView(OcctViewerCore* core, int view)
{
    if (!core || core->view.IsNull()) return;
    switch (view)
    {
    case 0: core->view->SetProj(V3d_Zpos); break;          // Top
    case 1: core->view->SetProj(V3d_Yneg); break;          // Front
    case 2: core->view->SetProj(V3d_Xpos); break;          // Right
    default: core->view->SetProj(V3d_XposYnegZpos); break; // Isometric
    }
    core->view->FitAll(0.10, false);
    core->view->Redraw();
}

void OcctCore_FitAll(OcctViewerCore* core)
{
    if (!core || core->view.IsNull()) return;
    core->view->FitAll(0.10, false);
    core->view->ZFitAll();
    core->view->Redraw();
}

void OcctCore_SetDisplayMode(OcctViewerCore* core, int mode)
{
    if (!core) return;
    core->displayMode = mode;
    ApplyDisplayMode(core);
}

void OcctCore_SetSelectionMode(OcctViewerCore* core, int kind, int enabled)
{
    if (!core) return;
    switch (kind)
    {
    case 0: core->selBody = enabled != 0; break;
    case 1: core->selFace = enabled != 0; break;
    case 2: core->selEdge = enabled != 0; break;
    case 3: core->selVertex = enabled != 0; break;
    }
    for (auto& kv : core->slots)
        ApplySelectionModes(core, kv.second);
    core->view->Redraw();
}

void OcctCore_Redraw(OcctViewerCore* core)
{
    if (core && !core->view.IsNull()) core->view->Redraw();
}
