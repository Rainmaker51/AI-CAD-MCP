"""Exercise real copy/move/rollback in an isolated archive fixture, never the live warehouse."""
import contextlib, importlib.util, io, json, pathlib, tempfile, unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('archive_legacy',pathlib.Path(__file__).with_name('Archive-LegacyApp.py'))
archive=importlib.util.module_from_spec(spec);spec.loader.exec_module(archive)

class ArchiveTests(unittest.TestCase):
    def setup_fixture(self,root):
        repo=root/'repo';warehouse=root/'warehouse';data=root/'data'
        def put(p,value):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(value),encoding='utf-8')
        put(repo/'warehouse.local.json',{'path':str(warehouse)})
        put(repo/'artifacts/p0-p6/install.json',{'DataRoot':str(data)})
        put(warehouse/'warehouse.json',{'schemaVersion':1})
        for name,builder in [('SOURCE','source'),('KEEP','custom')]:
            put(warehouse/'Versions/skills'/name/'active.json',{'version':'1.0.0'})
            put(warehouse/'Versions/skills'/name/'1.0.0/content.json',{'Builder':builder})
        put(warehouse/'Catalog/addin-settings/rainmaker-axis.json',{'HeadBlockId':'head-id'})
        put(warehouse/'Sources/Catalog/source.json',{'name':'image.png'})
        put(data/'sources/analysis.json',{'draft':[1,2,3]})
        return repo,warehouse,data
    def run_archive(self,repo):
        with patch('sys.argv',['Archive-LegacyApp.py','--repo',str(repo),'--apply']),patch.object(archive.subprocess,'run',return_value=type('Result',(),{'stdout':'0'})()),patch.object(archive.subprocess,'check_output',return_value=b'fixture-commit'),contextlib.redirect_stdout(io.StringIO()):archive.main()
    def test_verified_archive_preserves_native_configuration_and_no_ghost_skill(self):
        with tempfile.TemporaryDirectory() as folder:
            repo,warehouse,data=self.setup_fixture(pathlib.Path(folder));self.run_archive(repo)
            self.assertFalse((warehouse/'Versions/skills/SOURCE').exists())
            self.assertTrue((warehouse/'Versions/skills/KEEP/active.json').exists())
            self.assertTrue((warehouse/'Catalog/addin-settings/rainmaker-axis.json').exists())
            self.assertEqual(list((data/'sources').iterdir()),[])
            out=next((warehouse/'Dọn dẹp').iterdir());manifest=archive.read(out/'manifest.json')
            for e in manifest['files']:self.assertEqual(archive.digest(out/e['archive']),e['sha256'])
            self.assertTrue((out/'Ban-goc-da-chuyen/Du-lieu-may/sources/analysis.json').exists())
    def test_move_failure_restores_previous_originals(self):
        with tempfile.TemporaryDirectory() as folder:
            repo,warehouse,data=self.setup_fixture(pathlib.Path(folder));move=archive.shutil.move;calls=0
            def fail_once(*args,**kwargs):
                nonlocal calls
                calls+=1
                if calls==2:raise OSError('simulated lock')
                return move(*args,**kwargs)
            with patch.object(archive.shutil,'move',side_effect=fail_once):
                with self.assertRaises(OSError):self.run_archive(repo)
            self.assertTrue((warehouse/'Versions/skills/SOURCE/active.json').exists())
            self.assertTrue((warehouse/'Sources/Catalog/source.json').exists())
            self.assertTrue((data/'sources/analysis.json').exists())

if __name__=='__main__':unittest.main()
