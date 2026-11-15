# Preview approval fix (AutoCAD 2021, plugin 0.4.2)

Native dimensions in the staging preview used AutoCAD's current WorkingDatabase. A one-dimension regression reproduced a target revision change from 0 to 1 although no target entity was intentionally created. Prepare captured the newer live revision, but Apply still checked the original plan revision and failed with STALE_REVISION. The old command error handler printed the error without updating the palette.

NativePreviewScene now uses its staging database as WorkingDatabase and restores the previous database in finally. View navigation records do not invalidate geometry revisions. AICADAPPLY has NoUndoMarker so validation precedes Undo metadata; its writes use an explicit Undo BEGIN/END group. Apply failures clear stale previews or display execution failure details. Geometry edits, source hashes, resources, session identity and local approval checks remain enforced.

Validation: 47 managed tests; 5 baseline native tests; 9 phase native tests; 5 review regression tests. The latter check the command flag, viewport navigation, dimension preview isolation, one-Undo behavior and real entity revision invalidation. All passed. The live image plan 6632ac456fd34ee1b2c4c4870e8744dd also kept revision 0 before and after preview. Local human approval completed. The user confirmed HOÀN TẤT. Native receipt inspection returned PRESENT, 99 handles confirmed, no QA errors. The two native dimension measurements are 16000 mm and 5000 mm. The drawing still needs saving in AutoCAD to persist the result.

Native test evidence: tests/fixtures/review-fix-4dfd0e56adc04c3d9680972c828bc48a/review-flow-results.json and console3.log. Summary: tests/results/preview-approval-fix.json.

