# Placement

Hood uses display work areas, which exclude reserved desktop space such as the taskbar. Placement calculations use logical coordinates and each display's effective scale. The first area starts near the primary work area's bottom edge; its dimensions are at least large enough for the packaged pet and supported animation footprint. A smaller display can shrink the area to that minimum.

Dragging begins only after the system drag threshold. Hood and the area move together, and the same input is not also dispatched as a click. The destination is selected from Hood's position; the pet and area are clamped together at work-area edges. A valid drag is saved on release. If a saved monitor is disconnected, Hood is placed on the primary display. Display layout changes are reconciled while Hood is running.

Choose **Edit roaming area** from the pet menu to reveal the outline. Drag its top bar to move the area; Hood stays inside and moves only when the area would otherwise leave him outside. Drag the lower-right handle to resize it. The editor is temporary, suspends ambient behavior, and saves successful changes in the same session file.

The supported manual cross-monitor, scaling, and monitor-removal checks are listed in the [Ticket 07 acceptance checklist](manual-acceptance-ticket-07.md).
