; CAD Tool Manager sample: no drawing entities are modified.
(defun c:QATOOLSHELLO (/ f root)
  (princ "\nCAD Tool Manager: AutoLISP OK.")
  (setq root (getenv "AICAD_TOOL_TEST_ROOT"))
  (if root
    (progn
      (setq f (open (strcat root "/lisp-ok.txt") "w"))
      (if f (progn (write-line "LISP_OK" f) (close f)))))
  (princ))
