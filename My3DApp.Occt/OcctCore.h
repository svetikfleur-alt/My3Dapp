// Narrow native API over OCCT. Only opaque pointers and PODs cross this line.
// Compiled WITHOUT /clr; the C++/CLI bridge (Bridge.cpp) sees only this header.
#pragma once

#ifdef _WIN32
#define OCCTCORE_CALL __stdcall
#else
#define OCCTCORE_CALL
#endif

struct OcctShape;   // opaque: owns a TopoDS_Shape
struct OcctViewerCore; // opaque: owns V3d_Viewer/View, AIS context, child HWND

// (slot, kind, subIndex, isHover) — kind: 0 body, 1 face, 2 edge, 3 vertex.
// subIndex: 1-based index in the shape's sub-shape map of that kind, 0 = whole body,
// -1 with isHover=1 means "hover cleared".
typedef void(OCCTCORE_CALL* OcctPickCallback)(int slot, int kind, int subIndex, int isHover);

// ---- kernel (exact solids) ----
OcctShape* OcctCore_MakeBox(double dx, double dy, double dz);
OcctShape* OcctCore_MakeCylinder(double radius, double height);
OcctShape* OcctCore_BooleanCut(OcctShape* target, OcctShape* tool);
OcctShape* OcctCore_BooleanFuse(OcctShape* a, OcctShape* b);
void       OcctCore_FreeShape(OcctShape* shape);
// Basic validity probe (non-null, non-empty shape).
int        OcctCore_ShapeIsValid(OcctShape* shape);
// Axis-aligned bounding box for verification/tests. Returns 0 on failure.
int        OcctCore_ShapeBounds(OcctShape* shape, double* xmin, double* ymin, double* zmin,
                                double* xmax, double* ymax, double* zmax);

// ---- STEP export (exact, never triangulated) ----
// Returns 1 on success.
int OcctCore_WriteStep(OcctShape* shape, const wchar_t* path);

// ---- viewer ----
OcctViewerCore* OcctCore_CreateViewer(void* parentHwnd, int width, int height, OcctPickCallback pickCb);
void* OcctCore_GetHwnd(OcctViewerCore* core);
void  OcctCore_DestroyViewer(OcctViewerCore* core);
void  OcctCore_Resize(OcctViewerCore* core, int width, int height);

// Display a shape under an integer slot id (managed side maps slot -> Guid).
void OcctCore_Display(OcctViewerCore* core, OcctShape* shape, int slot);
void OcctCore_UpdateShape(OcctViewerCore* core, OcctShape* shape, int slot);
void OcctCore_Erase(OcctViewerCore* core, int slot);

// view: 0 Top, 1 Front, 2 Right, 3 Isometric
void OcctCore_SetView(OcctViewerCore* core, int view);
void OcctCore_FitAll(OcctViewerCore* core);
// mode: 0 Shaded, 1 ShadedWithEdges, 2 Wireframe
void OcctCore_SetDisplayMode(OcctViewerCore* core, int mode);
// kind: 0 body, 1 face, 2 edge, 3 vertex
void OcctCore_SetSelectionMode(OcctViewerCore* core, int kind, int enabled);
void OcctCore_Redraw(OcctViewerCore* core);

// Last-error message for managed exception translation (thread-local, UTF-16).
const wchar_t* OcctCore_LastError();
