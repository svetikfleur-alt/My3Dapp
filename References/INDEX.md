# CAD Studio — Visual References

Target: gather real UI screenshots from modern CAD programs as raw reference material ("Lego bricks") for the CAD Studio you're building.

---

## Priority (user-confirmed)

**Zoo + Onshape are the primary references.** Fusion / SolidWorks / Rhino / CATIA / NX are deprioritized — pull from them only if a specific pattern is needed (e.g. Fusion **Timeline** for the action-log analog, SolidWorks **PropertyManager** for the structured tool-panel analog).

## Orientation note for user-captured photos

All three user-captured photos (`user_v0_prototype/v0_main.jpg`, `zoo/zoo_full_interface.jpg`, `onshape/onshape_part_studio.jpg`) are phone photos of a laptop. They were shot with the laptop lying on its left side, so the image is **rotated 90° counterclockwise** relative to normal viewing. **Rotate them 90° clockwise** in an image viewer to read them correctly. Minor glare + off-axis angle but content is readable.

## 0. User's baseline (`/user_v0_prototype/`)

Her existing direction — a v0.app prototype titled "parametric-cad-interface". This is the shape she's already built toward; everything else in this folder is reference material against which this concept should be measured.

- `v0_main.jpg` — full screen: left sidebar (Sketch mode + Features tree: Origin / Top / Front / Right), viewport with coordinate gizmo, right-side **AI Assistant** panel with mode switcher **Auto / Do / Think / Assist**, "Apply suggestion" button, and quick prompts ("Create a bracket", "Ask about modeling", "Explain parametric modeling").

---

## ⚠️ Status

The agent could not download the images directly — the sandbox blocks egress to every CAD vendor domain (autodesk.com, onshape.com, zoo.dev, rhino3d.com, solidworks.com) and every image host (wikimedia, imgur, github, youtube thumbnails). `curl`/`wget` are blocked too.

What this file is: a **curated source list** organized by program and UI element. Each link has been selected from web search as likely containing good, clean UI screenshots (docs pages, tutorial blogs, help centers). Open each link in your browser and save the screenshots you want into the corresponding subfolder.

Suggested naming: `onshape_feature_tree.png`, `fusion_ribbon_toolbar.jpg`, `zoo_code_panel.png`, etc.

---

## 1. Onshape — primary reference (`/onshape/`)

Priority elements: full studio overview, feature tree, top toolbar, tab bar, sketch mode, viewport with gizmo, property/parameter panel, right-click menus.

### Captured (user)
- `onshape_part_studio.jpg` — in-browser Part Studio, doc "reference ui / expirement – Main Part Studio 2". Left panel: Default geometry (Origin, Top, Front, Right), Features (4), Parts (0), Sketch entry, timeline control at bottom. Left tool strip with sketch/feature icons. Viewport: two intersecting reference planes (Front + another) on clean white. Top bar: Search tools, Share, Explore Onshape. URL `cad.onshape.com`. (Photo rotated 90° CCW.)

### Source links to mine for more

**Official help docs (cleanest UI shots, usually annotated):**
- User Interface Basics (overview, toolbar, tabs): https://cad.onshape.com/help/Content/Home/user_interface_basics.htm
- Toolbar / UI basics: https://cad.onshape.com/help/Content/ui-basics.htm
- Sketch basics (sketch mode, sketch toolbar): https://cad.onshape.com/help/Content/sketch_basics.htm
- Sketch tools reference: https://cad.onshape.com/help/Content/sketch-tools.htm
- Feature basics (feature tree, rollback bar): https://cad.onshape.com/help/Content/feature-basics.htm
- Camera + viewport/gizmo: https://cad.onshape.com/help/Content/moving.htm
- Context menus (right-click): https://cad.onshape.com/help/Content/contextmenus.htm
- Structure View (feature tree variant): https://cad.onshape.com/help/Content/Document/structure_view.htm
- Onshape Primer (end-to-end overview): https://cad.onshape.com/help/Content/Primer/onshape_primer.htm

**Tutorial / blog:**
- Robotics Knowledgebase Onshape tutorial (annotated full UI): https://roboticsknowledgebase.com/wiki/fabrication/onshape-tutorial/
- What's New: right-click context menu (modern look): https://www.onshape.com/en/resource-center/what-is-new/context-menu-on-right-click-tangent-edge-display-in-assemblies-action-items-dashboard
- Tech Tip — keyboard shortcuts (usually paired with UI shots): https://www.onshape.com/en/resource-center/tech-tips/tech-tip-keyboard-shortcuts

**Forum threads (sometimes contain user screenshots showing real use):**
- Sketch toolbar discussion: https://forum.onshape.com/discussion/1013/sketch-toolbar
- Icons in features list (with screenshot): https://forum.onshape.com/discussion/1231/suggestion-with-screenshot-icons-in-features-list

Target: 5–10 images.

---

## 2. Fusion 360 (`/fusion/`)

Priority elements: overview, ribbon toolbar, browser tree, timeline, sketch mode, properties panel.

**Autodesk official blog / help (cleanest, most current UI):**
- Navigate the Fusion Interface — beginner overview: https://www.autodesk.com/products/fusion-360/blog/autodesk-fusion-interface/
- Master the Timeline, Browser & Preferences: https://www.autodesk.com/products/fusion-360/blog/master-the-timeline-browser-preferences/
- Fusion 360 Sketch Environment: https://www.autodesk.com/products/fusion-360/blog/fusion-360-sketch-environment/
- Sketches in Fusion (help): https://help.autodesk.com/view/fusion360/ENU/?guid=SKT-3D-SKETCH
- Sketch tools reference: https://help.autodesk.com/view/fusion360/ENU/?guid=GD-SKETCH-TOOLS
- Sketching basics on-demand tutorial: https://www.autodesk.com/learn/ondemand/tutorial/sketching-basics-overview
- API UI customization (ribbon anatomy): https://help.autodesk.com/cloudhelp/ENU/Fusion-360-API/files/UserInterface_UM.htm

**Third-party tutorials with good screenshots:**
- Product Design Online — full UI walkthrough (2024 layout): https://productdesignonline.com/fusion-360-tutorials/learn-the-fusion-360-user-interface/
- Product Design Online — new UI preview: https://productdesignonline.com/tips-and-tricks/how-to-preview-fusion-360s-new-user-interface-design/
- Product Design Online — 8 timeline tips: https://productdesignonline.com/tips-and-tricks/8-fusion-360-timeline-tips-you-must-know/
- Noble Desktop — browser & timeline organization: https://www.nobledesktop.com/learn/cad/exploring-browser-and-timeline-organization-in-fusion-360

**Wikimedia (single clean shot):**
- File:Fusion_360_Screenshot.png: https://commons.wikimedia.org/wiki/File:Fusion_360_Screenshot.png
- Category: https://commons.wikimedia.org/wiki/Category:Fusion_360

Target: 5–10 images.

---

## 3. SolidWorks (`/solidworks/`)

Priority elements: command manager, feature tree, property manager panel, sketch mode, confirmation corner.

**Tutorial blogs (usually full UI screenshots, clearly annotated):**
- TriMech — Anatomy of the SolidWorks UI: https://store.trimech.com/blog/anatomy-of-the-solidworks-ui
- Hawk Ridge Systems — UI Basics: https://hawkridgesys.com/blog/user-interface-basics-in-solidworks
- Pressbooks — Major UI Components (free textbook, clean diagrams): https://pressbooks.pub/solidworks1/chapter/major-user-interface-components/
- OpenWA Pressbooks alt: https://openwa.pressbooks.pub/testmhrtc/chapter/major-user-interface-components/
- Mechanitec — What is what in SolidWorks Interface: https://mechanitec.ca/what-is-what-in-solidworks-interface/
- Mechanitec — Property Manager walkthrough: https://mechanitec.ca/how-to-use-property-manager-in-solidworks/
- CADimensions — manipulating the interface: https://knowledge.cadimensions.com/knowledge/how-do-i-manipulate-the-solidworks-interface
- SolidWorks tutorials for beginners — UI basics: https://solidworkstutorialsforbeginners.com/solidworks-user-interface/
- Engineers Rule — modeling tips (UI deep-dive): https://www.engineersrule.com/solidworks-modeling-tips-techniques-user-interface/
- GrabCAD — understanding the UI: https://grabcad.com/questions/how-to-understanding-the-solidworks-user-interface

**Confirmation corner specifically:**
- SolidSmack article: https://www.solidsmack.com/cad/solidworks-confirmation-corner-use-it-loose-it-three-other-options/
- Javelin — D-key confirmation: https://www.javelin-tech.com/blog/2015/11/solidworks-confirmation-corner/
- Help — accepting features: https://help.solidworks.com/2018/English/SolidWorks/sldworks/c_accepting_features.htm
- What's New 2016 — confirmation moving: https://help.solidworks.com/2016/english/whatsnew/c_command_confirmation.htm

**FeatureManager specifically:**
- GoEngineer — hidden FeatureManager commands: https://www.goengineer.com/blog/solidworks-featuremanager-hidden-forgotten-commands

**Sketch basics:**
- Cadasio beginner's guide part 1: https://www.cadasio.com/post/getting-started-with-sketching-in-solidworks-a-beginners-guide-part-1

Target: 5–10 images.

---

## 4. Rhino (`/rhino/`)

Priority elements: command line area, viewport layout, side panels.

**Official:**
- Rhino features — Window Layouts (viewport/tab layouts): https://www.rhino3d.com/features/user-interface/window-layouts/

**Tutorial:**
- AFW Design — basic toolbars and menus: https://www.afwdesign.info/post/basic-toolbars-and-menus-in-rhino-3d
- Materiability basics course: https://materiability.com/portfolio/basics/
- Hopific — how to screenshot viewport (often contains UI): https://hopific.com/how-to-take-a-screenshot-in-rhino-using-viewcapture/

**Command reference (sometimes has UI):**
- ViewCapture docs: http://docs.mcneel.com/rhino/5/help/en-us/commands/viewcapture.htm
- ViewCaptureToFile docs v8: http://docs.mcneel.com/rhino/8/help/en-us/commands/viewcapturetofile.htm

Target: 5–10 images.

---

## 5. Zoo Design Studio / KittyCAD (`/zoo/`)

**Priority panels (from user):**
1. **Code panel** — KCL code editor shown next to the 3D viewport (proportions + layout critical). 3–5 solid shots.
2. **Chat / assistant panel** — text-to-CAD chat UI: input bar, message bubbles, inline action buttons. 3–5 solid shots.

General overview is secondary but still welcome.

### Captured (user)
- `zoo_full_interface.jpg` — **full Design Studio interface, code panel + chat panel visible simultaneously** (exactly the shot she wanted). Project: `kung-fu-panda / main.kcl`.
  - **Left rail:** Feature Tree (Front plane YZ, Top plane XY, Side plane YZ), Code Editor with KCL (`chain_segment` repair — imports, strict concentric correction, `insertCircle` hinted), Project Files (`main.kcl`, `chain_segment.fixed-AP214.step`, `scenic-panda.obj`). Version `v1.2.3`.
  - **Center viewport:** Star of David / hexagram (two intersecting triangles), modeling gizmo top-right, **Start Sketch** button.
  - **Right rail:** **Zookeeper** assistant panel — account/billing state visible (payment method prompt + link), prompt chip "Create a gear with 10 teeth". Non-chat, structured layout.
  - **Top bar:** Commands (Ctrl+K), Share, Publish.
  - **Bottom status bar:** Fast, No selection, "Zookeeper can make mistakes. Always verify information."
  - (Photo rotated 90° CCW.)

> ⚠️ **Zookeeper panel caveat — paywall state, not normal state.** The free trial on this account has expired, so the Zookeeper panel content visible in this screenshot is a **billing/upsell state** ("month and do not have a payment method… Add a payment method… https://zoo.dev/account") with a residual "Create a gear with 10 teeth" prompt chip. This is **not** the representative Zookeeper welcome — the typical state shows something like "ready to help" / "how can I help today". When mining this shot, infer **structure only**: input bar position, message area layout, action-chip pattern, panel width, relationship to viewport + code editor. **Do not copy the message content as representative of normal behavior.**

### Source links to mine for more

**Code panel (KCL editor):**
- Zoo Design Studio live app (opens right into code pane): https://app.zoo.dev/file/%2Fdocuments%2Fzoo-design-studio-projects%2Ftutorial-project%2Ffan-housing.kcl/onboarding/desktop/code-pane
- KCL book — installation + screenshots: https://zoo.dev/docs/kcl-book/installation.html
- KCL language reference: https://zoo.dev/docs/kcl-lang
- Design Studio docs (panel layout): https://docs.zoo.dev/design-studio
- Blog — Zoo Design Studio v1 launch (screenshots of the new stack): https://zoo.dev/blog/zoo-design-studio-v1
- Blog — What's new (September): https://zoo.dev/blog/whats-new-september
- GitHub repo (README often has hero shots + GIFs): https://github.com/KittyCAD/modeling-app
- GitHub kcl-samples (often shows editor next to rendered result): https://github.com/KittyCAD/kcl-samples

**Chat / text-to-CAD panel:**
- Zoo text-to-CAD standalone UI (live — take screenshots): https://text-to-cad.zoo.dev/
- Text-to-CAD marketing page: https://zoo.dev/text-to-cad
- Text-to-CAD in Design Studio (docs, shows Zookeeper chat panel): https://zoo.dev/docs/zoo-design-studio/text-to-cad
- Intro blog (first reveal shots): https://zoo.dev/blog/introducing-text-to-cad
- Text-to-CAD tutorial (step-through UI): https://zoo.dev/docs/developer-tools/tutorials/text-to-cad
- Research — editable parametric B-rep: https://zoo.dev/research/introducing-text-to-cad
- GitHub — text-to-cad-ui open source repo (README likely has screenshots): https://github.com/KittyCAD/text-to-cad-ui

**General overview / marketing:**
- Homepage: https://zoo.dev/
- Design Studio product page: https://zoo.dev/design-studio
- Design API page: https://zoo.dev/design-api
- Intro KittyCAD blog: https://zoo.dev/blog/introducing-kittycad

Target: 6–10 images (3–5 code panel, 3–5 chat panel, 1–2 overview).

---

## 6. CATIA (`/catia/`) — optional, dense-enterprise feel

- How to capture screenshots in CATIA (often has UI shots): https://catiav5v6tutorials.com/how-to/capture-screenshots-catia-v5/
- LearnVern CATIA V5 user interface guide: https://learnvern.com/catia-course/user-interface
- Software Informer screenshots page: https://catia-v5-r19-interface.software.informer.com/screenshot/203419/
- GrabCAD basic tutorial: https://grabcad.com/tutorials/catia-v5-basic-tutorial--1
- CATIA V5 CAD blogspot — UI overview: http://catia-v5-cad.blogspot.com/2013/04/User-Interface-catia-CAD.html
- Tree manipulation (specification tree shots): https://catiav5v6tutorials.com/catia-v5-tutorials/catia-tree-manipulation-resizes-moves-hide-and-scrolls/
- Display product tree: https://grabcad.com/tutorials/display-the-product-tree-in-catia-v5

Target: 3–5 images (just enough for enterprise-dense feel).

---

## 7. Siemens NX (`/nx/`) — optional, dense-enterprise feel

- Siemens community — design feature snapshots: https://community.sw.siemens.com/s/article/design-feature-snapshots
- Manualzz — NX Interface Manual (UI diagrams): https://manualzz.com/doc/26898314/nx-interface
- Swoosh Tech — highlight commands on ribbon: https://www.swooshtech.com/2020/03/05/highlight-commands-on-ribbon-bar/
- FEAC Engineering — ribbon customization: https://feacomp.com/customizing-siemens-nx-ribbons-enhancing-your-workflow-with-custom-tabs-and-tools/
- Machine Ribbon Tab: https://community.sw.siemens.com/s/article/Machine-Ribbon-Tab-in-NX-Manufacturing
- What's New in NX11 (PDF with clear screenshots): https://gmsystem.pl/wp-content/uploads/Whatsnew_NX11.pdf
- Wikimedia NX screenshots category: https://commons.wikimedia.org/wiki/Category:NX_(Unigraphics)_screenshots

Target: 3–5 images.

---

## Fit notes — how each program's UI relates to your Assistant concept

Concept baseline (from `Blueprint/Assistant/06` + `08`): engineering co-pilot, **not** a chatbot. Structured modes **Auto / Do / Think / Assist / Think & Do**. Output is concise, structured, actionable, non-chatty. Panel is a tool panel with dialog capability — no avatars, no chat bubbles, no filler. Reasoning is context-fed from an action log (selection, active tool, recent actions, model tree).

### Onshape — aligns well
- Feature tree + dedicated right-side property/parameter panel is a clean template for a structured, non-chat tool panel.
- Right-click context menus are already "next-step, here, now" — good analog for the **Assist** mode semantics.
- No assistant concept exists natively, so you're adding a net-new panel rather than retrofitting a chat UI — low risk of pattern collision.

### Fusion 360 — mixed
- Timeline (bottom bar) is a direct match for your Event/Context action log — strongest UI reuse in the whole reference set.
- Contextual ribbon tabs that appear in Sketch mode match the "tool panel with dialog capability" idea well (mode-aware surface).
- Ribbon density clashes with your low-noise / non-chatty output principle — take the *pattern* (context-switching surface), not the *weight*.

### SolidWorks — closest structural analog
- **PropertyManager** is the canonical "tool panel with dialog capability": context-sensitive, structured, no chat. Closest existing UI in industry to what you're building.
- Confirmation corner (accept/cancel in viewport) is a clean micro-pattern for **Do** mode — a small, explicit commit step after an agent action.
- CommandManager is visually heavy and clashes with low-noise principle; don't copy the ribbon, only the PropertyManager behavior.

### Zoo Design Studio — useful contrast case
- The chat panel aesthetic (input bar, inline action buttons, message styling) is close to what you have on the right side of `v0_main` — worth mining for typography / spacing / command chips.
- But it's **freeform chat** — conflicts with your explicit Mode switcher (Auto/Do/Think/Assist). You want mode-gated, not open-ended; strip bubbles + avatars, keep the action-button pattern.
- KCL code editor sitting next to the viewport is a good reference for dual-pane structured layout if you ever surface the action log or plan block alongside the model.

---

## Tips while saving

- **Prefer docs/help pages** first — they usually have clean, annotated UI shots without marketing clutter.
- **Skip hero/marketing images** with rendered product beauty shots and tiny UI.
- **Rename on save** using the convention `program_element.ext` (e.g. `onshape_feature_tree.png`, `zoo_code_panel_01.png`).
- **40–60 images total across all programs is the target.** Don't over-collect.
- If a link is 404 or paywalled, skip — there are redundant sources listed per element.
