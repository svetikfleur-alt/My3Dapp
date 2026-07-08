// C++/CLI bridge: thin managed surface over OcctCore. No OCCT types cross here.
#include "OcctCore.h"

#include <vcclr.h>
#include <vector>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;

namespace My3DApp { namespace Occt {

static Exception^ NativeError(String^ context)
{
    String^ detail = gcnew String(OcctCore_LastError());
    return gcnew InvalidOperationException(context + ": " + detail);
}

public ref class OcctBody sealed : IDisposable
{
internal:
    OcctShape* _shape;

    OcctBody(OcctShape* shape, Guid bodyId) : _shape(shape), _bodyId(bodyId) {}

public:
    property Guid BodyId { Guid get() { return _bodyId; } }
    property bool IsValid { bool get() { return _shape != nullptr && OcctCore_ShapeIsValid(_shape) != 0; } }

    /// Axis-aligned bounds in model units (mm). Throws if the shape is invalid.
    void GetBounds([Out] double% xmin, [Out] double% ymin, [Out] double% zmin,
                   [Out] double% xmax, [Out] double% ymax, [Out] double% zmax)
    {
        double a, b, c, d, e, f;
        if (!OcctCore_ShapeBounds(_shape, &a, &b, &c, &d, &e, &f))
            throw NativeError("GetBounds");
        xmin = a; ymin = b; zmin = c; xmax = d; ymax = e; zmax = f;
    }

    ~OcctBody() { this->!OcctBody(); }
    !OcctBody()
    {
        if (_shape != nullptr)
        {
            OcctCore_FreeShape(_shape);
            _shape = nullptr;
        }
    }

private:
    Guid _bodyId;
};

public ref class OcctKernel sealed : IDisposable
{
public:
    OcctKernel() : _alive(true) {}

    property bool IsAlive { bool get() { return _alive; } }

    OcctBody^ CreateBox(double dx, double dy, double dz)
    {
        Ensure();
        OcctShape* s = OcctCore_MakeBox(dx, dy, dz);
        if (s == nullptr) throw NativeError("CreateBox");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ CreateCylinder(double radius, double height)
    {
        Ensure();
        OcctShape* s = OcctCore_MakeCylinder(radius, height);
        if (s == nullptr) throw NativeError("CreateCylinder");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ CreateWire(array<double>^ points, bool closed)
    {
        Ensure();
        pin_ptr<double> p = &points[0];
        OcctShape* s = OcctCore_MakeWire(p, points->Length / 2, closed ? 1 : 0);
        if (s == nullptr) throw NativeError("CreateWire");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ CreateCircleWire(double radius)
    {
        Ensure();
        OcctShape* s = OcctCore_MakeCircleWire(radius);
        if (s == nullptr) throw NativeError("CreateCircleWire");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ CreateFace(OcctBody^ wire)
    {
        Ensure();
        if (wire == nullptr || !wire->IsValid) throw gcnew ArgumentException("wire");
        OcctShape* s = OcctCore_MakeFace(wire->_shape);
        if (s == nullptr) throw NativeError("CreateFace");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ CreatePrism(OcctBody^ face, double dx, double dy, double dz)
    {
        Ensure();
        if (face == nullptr || !face->IsValid) throw gcnew ArgumentException("face");
        OcctShape* s = OcctCore_MakePrism(face->_shape, dx, dy, dz);
        if (s == nullptr) throw NativeError("CreatePrism");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ CreateCompound(array<OcctBody^>^ bodies)
    {
        Ensure();
        std::vector<OcctShape*> shapes(bodies->Length);
        for (int i = 0; i < bodies->Length; i++) {
            if (bodies[i] != nullptr && bodies[i]->IsValid) {
                shapes[i] = bodies[i]->_shape;
            } else {
                shapes[i] = nullptr;
            }
        }
        OcctShape* s = OcctCore_MakeCompound(shapes.data(), shapes.size());
        if (s == nullptr) throw NativeError("CreateCompound");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ Translated(OcctBody^ body, double dx, double dy, double dz)
    {
        Ensure();
        CheckBody(body, "body");
        OcctShape* s = OcctCore_Translate(body->_shape, dx, dy, dz);
        if (s == nullptr) throw NativeError("Translated");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ BooleanSubtract(OcctBody^ target, OcctBody^ tool)
    {
        Ensure();
        CheckBody(target, "target"); CheckBody(tool, "tool");
        OcctShape* s = OcctCore_BooleanCut(target->_shape, tool->_shape);
        if (s == nullptr) throw NativeError("BooleanSubtract");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    OcctBody^ BooleanUnion(OcctBody^ a, OcctBody^ b)
    {
        Ensure();
        CheckBody(a, "a"); CheckBody(b, "b");
        OcctShape* s = OcctCore_BooleanFuse(a->_shape, b->_shape);
        if (s == nullptr) throw NativeError("BooleanUnion");
        return gcnew OcctBody(s, Guid::NewGuid());
    }

    void ExportStep(OcctBody^ body, String^ path)
    {
        Ensure();
        CheckBody(body, "body");
        pin_ptr<const wchar_t> p = PtrToStringChars(path);
        if (!OcctCore_WriteStep(body->_shape, p))
            throw NativeError("ExportStep");
    }

    ~OcctKernel() { this->!OcctKernel(); }
    !OcctKernel() { _alive = false; }

private:
    bool _alive;

    void Ensure()
    {
        if (!_alive) throw gcnew ObjectDisposedException("OcctKernel");
    }

    static void CheckBody(OcctBody^ body, String^ name)
    {
        if (body == nullptr) throw gcnew ArgumentNullException(name);
        if (!body->IsValid) throw gcnew ObjectDisposedException(name, "body handle is invalid or disposed");
    }
};

public enum class OcctTopoKind { Body = 0, Face = 1, Edge = 2, Vertex = 3 };
public enum class OcctDisplayMode { Shaded = 0, ShadedWithEdges = 1, Wireframe = 2 };
public enum class OcctStandardView { Top = 0, Front = 1, Right = 2, Isometric = 3 };

public ref class OcctPickEventArgs sealed : EventArgs
{
public:
    OcctPickEventArgs(Guid bodyId, OcctTopoKind kind, int subIndex, bool isHover, bool cleared)
        : _bodyId(bodyId), _kind(kind), _subIndex(subIndex), _isHover(isHover), _cleared(cleared) {}

    property Guid BodyId { Guid get() { return _bodyId; } }
    property OcctTopoKind Kind { OcctTopoKind get() { return _kind; } }
    /// 1-based transient sub-shape index; 0 = whole body. Valid only until the body changes.
    property int SubIndex { int get() { return _subIndex; } }
    property bool IsHover { bool get() { return _isHover; } }
    property bool Cleared { bool get() { return _cleared; } }

private:
    Guid _bodyId; OcctTopoKind _kind; int _subIndex; bool _isHover; bool _cleared;
};

delegate void NativePickDelegate(int slot, int kind, int subIndex, int isHover);

public ref class OcctViewer sealed : IDisposable
{
public:
    event EventHandler<OcctPickEventArgs^>^ SelectionChanged;
    event EventHandler<OcctPickEventArgs^>^ HoverChanged;

    OcctViewer()
    {
        _slotToBody = gcnew Dictionary<int, Guid>();
        _bodyToSlot = gcnew Dictionary<Guid, int>();
        _nextSlot = 1;
        _core = nullptr;
    }

    /// Creates the native child window under parentHwnd and boots AIS/V3d. Returns the child HWND.
    IntPtr Initialize(IntPtr parentHwnd, int width, int height)
    {
        if (_core != nullptr) throw gcnew InvalidOperationException("viewer already initialized");
        _pickDelegate = gcnew NativePickDelegate(this, &OcctViewer::OnNativePick);
        IntPtr fp = Marshal::GetFunctionPointerForDelegate(_pickDelegate);
        _core = OcctCore_CreateViewer(parentHwnd.ToPointer(), width, height,
                                      static_cast<OcctPickCallback>(fp.ToPointer()));
        if (_core == nullptr) throw NativeError("Initialize");
        return IntPtr(OcctCore_GetHwnd(_core));
    }

    property bool IsInitialized { bool get() { return _core != nullptr; } }

    void Resize(int width, int height) { Ensure(); OcctCore_Resize(_core, width, height); }

    void DisplayBody(OcctBody^ body)
    {
        Ensure();
        if (body == nullptr) throw gcnew ArgumentNullException("body");
        int slot;
        if (!_bodyToSlot->TryGetValue(body->BodyId, slot))
        {
            slot = _nextSlot++;
            _bodyToSlot[body->BodyId] = slot;
            _slotToBody[slot] = body->BodyId;
        }
        OcctCore_Display(_core, body->_shape, slot);
    }

    void UpdateBody(OcctBody^ body)
    {
        Ensure();
        if (body == nullptr) throw gcnew ArgumentNullException("body");
        int slot;
        if (!_bodyToSlot->TryGetValue(body->BodyId, slot)) { DisplayBody(body); return; }
        OcctCore_UpdateShape(_core, body->_shape, slot);
    }

    void HideBody(Guid bodyId)
    {
        Ensure();
        int slot;
        if (_bodyToSlot->TryGetValue(bodyId, slot))
        {
            OcctCore_Erase(_core, slot);
            _bodyToSlot->Remove(bodyId);
            _slotToBody->Remove(slot);
        }
    }

    void SetView(OcctStandardView view) { Ensure(); OcctCore_SetView(_core, (int)view); }
    void FitAll() { Ensure(); OcctCore_FitAll(_core); }
    void SetDisplayMode(OcctDisplayMode mode) { Ensure(); OcctCore_SetDisplayMode(_core, (int)mode); }
    void SetSelectionMode(OcctTopoKind kind, bool enabled) { Ensure(); OcctCore_SetSelectionMode(_core, (int)kind, enabled ? 1 : 0); }
    void Redraw() { Ensure(); OcctCore_Redraw(_core); }

    ~OcctViewer() { this->!OcctViewer(); }
    !OcctViewer()
    {
        if (_core != nullptr)
        {
            OcctCore_DestroyViewer(_core);
            _core = nullptr;
        }
    }

private:
    OcctViewerCore* _core;
    NativePickDelegate^ _pickDelegate; // keeps the native thunk alive
    Dictionary<int, Guid>^ _slotToBody;
    Dictionary<Guid, int>^ _bodyToSlot;
    int _nextSlot;

    void Ensure()
    {
        if (_core == nullptr) throw gcnew ObjectDisposedException("OcctViewer");
    }

    void OnNativePick(int slot, int kind, int subIndex, int isHover)
    {
        Guid bodyId = Guid::Empty;
        bool cleared = slot < 0;
        if (!cleared && !_slotToBody->TryGetValue(slot, bodyId))
            cleared = true;

        auto args = gcnew OcctPickEventArgs(bodyId, (OcctTopoKind)kind, subIndex, isHover != 0, cleared);
        if (isHover != 0) HoverChanged(this, args);
        else SelectionChanged(this, args);
    }
};

}} // namespace My3DApp::Occt
