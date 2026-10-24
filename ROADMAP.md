# Roadmap: coverage of the requested feature list

All 42 requested functions are mapped below to a tool in this add-in. Names on the ribbon are my own;
the behaviour follows the description you gave. Two extra tools (Circuit Report, Select by Circuit)
bring the total to 44. Status: [x] built, [ ] planned.

## Drafting (10)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 1 | Convert Lines: detail/model lines into conduits | Lines to Conduit | [x] |
| 2 | Connect: join selected elements with conduit runs | Connect Runs | [x] |
| 3 | Overkill: remove duplicate/overlapping curves | Clean Lines | [x] (detail lines) |
| 4 | CAD to Detail: imported CAD lines into detail lines | CAD to Detail Lines | [x] |
| 5 | Wire to Detail: wires into detail lines | Wires to Detail Lines | [x] |
| 6 | Detail to Wire: detail lines into chamfered wires | Detail Lines to Wires | [x] |
| 7 | Fillet Lines: fillet at corners | Fillet Corners | [x] |
| 8 | Fillet Radius: default radius for fillet tools | Fillet Radius Setting | [x] |
| 9 | Fillet Pick: fillet two picked lines | Fillet Two Lines | [x] |
| 10 | Line Cutter: cut/split lines at intersections | Split Lines | [x] |

## Wiring (5)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 11 | Home Run: home-run detail lines, devices to panels | Home Run Lines | [x] |
| 12 | Wiring Pro: conduits from a boundary of detail lines | Boundary to Conduits | [x] |
| 13 | Circuit Number: circuit number text on fixtures/equipment | Circuit Number Text | [x] |
| 14 | Write Wire Info: nearby circuit info into conduit comments | Wire Info to Comments | [x] |
| 15 | Edit Catalog: review/edit the cable catalog | Cable Catalog Editor | [x] |

## Annotation (4)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 16 | Switch Label: label text on lighting devices | Switch Labels | [ ] |
| 17 | Tag & Leader: independent tags with leaders | Tags with Leaders | [ ] |
| 18 | Dim Pro: aligned dimension between two elements | Aligned Dimension | [ ] |
| 19 | Distribute: even spacing along a path | Distribute Along Path | [ ] |

## Model (7)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 20 | Assign Workset: by type | Worksets by Type | [ ] |
| 21 | Show Hosts: highlight host elements | Select Hosts | [x] |
| 22 | Junction Box: place at a picked point | Place Junction Box | [ ] |
| 23 | Align Family: location and rotation to a reference | Align to Reference | [ ] |
| 24 | Section: aligned to a linear element | Section Along Element | [ ] |
| 25 | Match Type: select all instances of a picked type | Select Same Type | [x] |
| 26 | 3D View: cropped to selection bounding box | 3D from Selection | [x] |

## Analysis (4)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 27 | Clash Detect: between categories/elements | Clash Check | [ ] |
| 28 | Qty Takeoff: selected elements/categories | Quantity Takeoff | [ ] |
| 29 | Exporter: all schedules to CSV files | Export All Schedules | [x] |
| 30 | Smart Filter: filter/isolate by category, type, parameter | Smart Filter | [ ] |

## Schedules (4)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 31 | Export Schedules: Excel / CSV | Export Schedules to Excel | [ ] (CSV covered by #29) |
| 32 | Dynamic Export: active schedule to one CSV | Export Active Schedule | [x] |
| 33 | Import Back: CSV values onto elements, matched by Mark | Import Values by Mark | [ ] |
| 34 | Import Schedules: CSV as formatted text note | CSV to Text Note | [ ] |

## Panels and SLD (3)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 35 | View Manager: list printable views, activate one | View Manager | [ ] |
| 36 | Sheet Creator: new sheet with active view | Sheet Creator | [ ] |
| 37 | Panel SLD: single-line report for a panel | Panel Single-Line Report | [ ] |

## Cable and Tray (3)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 38 | Cable & CB: conduit sizes, lengths, comments | Conduit Run Report | [ ] |
| 39 | Cable Tray Section: summary in a drafting view | Tray Summary View | [ ] |
| 40 | Draw Cable Tray: between two picked points | Draw Cable Tray | [ ] |

## Data (2)
| # | Requested function | Tool here | Status |
|---|---|---|---|
| 41 | Shared Params: create and bind electrical parameters | Shared Parameters | [ ] |
| 42 | Family Manager: list types, switch type of an instance | Family Type Manager | [ ] |

## Extras (2)
| # | Tool | Status |
|---|---|---|
| 43 | Circuit Report (CSV of all circuits) | [x] |
| 44 | Select by Circuit | [x] |

Not included: the server-based "Update" and "Contact Us" commands. This add-in has no update server and no licensing.

## Suggested build order
1. Drafting: complete (v3 and v4)
2. Model and annotation: Align to Reference, Place Junction Box, Distribute, Aligned Dimension, Tags with Leaders
3. Wiring: complete (v5)
4. Schedules, panels, tray, data, analysis: the rest
