import importlib.util
import pathlib
import unittest
import numpy as np

spec = importlib.util.spec_from_file_location('worker', pathlib.Path(__file__).resolve().parents[1] / 'src/Drawing.Intelligence/worker.py')
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)

class HoughCompatibility(unittest.TestCase):
    def test_wheel_shapes_preserve_coordinates(self):
        lines = np.array([[1, 2, 30, 40], [50, 60, 70, 80]], dtype=np.int32)
        for shape in ((2, 4), (2, 1, 4)):
            self.assertEqual([tuple(map(float, row)) for row in worker.hough_segments(lines.reshape(shape))],
                             [(1., 2., 30., 40.), (50., 60., 70., 80.)])

    def test_missing_and_empty_lines(self):
        self.assertEqual(len(worker.hough_segments(None)), 0)
        self.assertEqual(len(worker.hough_segments(np.empty((0, 1, 4)))), 0)

    def test_limit(self):
        self.assertEqual(len(worker.hough_segments(np.zeros((400, 1, 4)))), 300)

if __name__ == '__main__':
    unittest.main()
