"""Pruebas unitarias para mongo_trx_summary.py."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from mongo_trx_summary import (
    EXPECTED_TESTS,
    find_latest_trx,
    main,
    matches_test_name,
    parse_trx,
    render_markdown,
    validate,
)

SAMPLE_TRX_SHORT_NAMES = """<?xml version="1.0" encoding="utf-8"?>
<TestRun id="11111111-1111-1111-1111-111111111111" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult executionId="e1" testId="t1" testName="RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal" computerName="c1" duration="00:00:03.1234567" startTime="2026-11-08T00:00:00Z" endTime="2026-11-08T00:00:03Z" testType="13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b" outcome="Passed" testListId="l1" relativeResultsDirectory="e1" />
    <UnitTestResult executionId="e2" testId="t2" testName="BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal" computerName="c1" duration="00:00:04.5678901" startTime="2026-11-08T00:00:03Z" endTime="2026-11-08T00:00:07Z" testType="13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b" outcome="Passed" testListId="l1" relativeResultsDirectory="e2" />
  </Results>
  <ResultSummary outcome="Completed">
    <Counters total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" help="0" />
  </ResultSummary>
</TestRun>
"""

SAMPLE_TRX_FULL_NAMES = """<?xml version="1.0" encoding="utf-8"?>
<TestRun id="22222222-2222-2222-2222-222222222222" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult executionId="e1" testId="t1" testName="UniHome.IntegrationTests.MongoIdentityPersistenceTests.RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal" outcome="Passed" duration="00:00:03.1234567" />
    <UnitTestResult executionId="e2" testId="t2" testName="UniHome.IntegrationTests.MongoPromotionLockPersistenceTests.BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal" outcome="Passed" duration="00:00:04.5678901" />
  </Results>
  <ResultSummary outcome="Completed">
    <Counters total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" help="0" />
  </ResultSummary>
</TestRun>
"""

SAMPLE_TRX_PARTIAL_MATCH = """<?xml version="1.0" encoding="utf-8"?>
<TestRun id="33333333-3333-3333-3333-333333333333" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult executionId="e1" testId="t1" testName="UniHome.IntegrationTests.MongoIdentityPersistenceTests.RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal" outcome="Passed" duration="00:00:02" />
    <UnitTestResult executionId="e2" testId="t2" testName="UniHome.IntegrationTests.OtraClase.MetodoParecido_BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal" outcome="Passed" duration="00:00:02" />
  </Results>
  <ResultSummary outcome="Completed">
    <Counters total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" help="0" />
  </ResultSummary>
</TestRun>
"""

SAMPLE_TRX_SKIPPED = """<?xml version="1.0" encoding="utf-8"?>
<TestRun id="44444444-4444-4444-4444-444444444444" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult executionId="e1" testId="t1" testName="RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal" outcome="NotExecuted" duration="0" />
    <UnitTestResult executionId="e2" testId="t2" testName="BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal" outcome="NotExecuted" duration="0" />
  </Results>
  <ResultSummary outcome="Completed">
    <Counters total="2" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" notExecuted="2" />
  </ResultSummary>
</TestRun>
"""

SAMPLE_TRX_FAILED = """<?xml version="1.0" encoding="utf-8"?>
<TestRun id="55555555-5555-5555-5555-555555555555" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult executionId="e1" testId="t1" testName="RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal" outcome="Passed" duration="00:00:02" />
    <UnitTestResult executionId="e2" testId="t2" testName="BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal" outcome="Failed" duration="00:00:01" />
  </Results>
  <ResultSummary outcome="Failed">
    <Counters total="2" executed="2" passed="1" failed="1" notExecuted="0" />
  </ResultSummary>
</TestRun>
"""


class MongoTrxSummaryTests(unittest.TestCase):
    def test_matches_test_name_unit(self):
        # Exact match
        self.assertTrue(matches_test_name("MiPrueba", "MiPrueba"))
        # FQCN match with dot separator
        self.assertTrue(matches_test_name("UniHome.Tests.MiClase.MiPrueba", "MiPrueba"))
        # Partial match without dot separator must fail
        self.assertFalse(matches_test_name("MetodoParecido_MiPrueba", "MiPrueba"))
        self.assertFalse(matches_test_name("UniHome.Tests.MiClase.OtraCosaMiPrueba", "MiPrueba"))
        self.assertFalse(matches_test_name("UniHome.Tests.MiClase.MiPruebaSuffix", "MiPrueba"))

    def test_parse_and_validate_short_names_success(self):
        with tempfile.NamedTemporaryFile("w", suffix=".trx", delete=False) as f:
            f.write(SAMPLE_TRX_SHORT_NAMES)
            f.flush()
            temp_path = Path(f.name)

        try:
            summary = parse_trx(temp_path)
            self.assertEqual(2, summary["counters"]["total"])
            self.assertEqual(2, summary["counters"]["passed"])
            self.assertEqual(0, summary["counters"]["failed"])
            self.assertEqual(0, summary["counters"]["notExecuted"])

            issues = validate(summary)
            self.assertEqual([], issues)

            markdown = render_markdown(summary, issues)
            self.assertIn("APROBADO (PASS)", markdown)
            self.assertIn(EXPECTED_TESTS[0], markdown)
            self.assertIn(EXPECTED_TESTS[1], markdown)
        finally:
            temp_path.unlink(missing_ok=True)

    def test_parse_and_validate_full_names_success(self):
        with tempfile.NamedTemporaryFile("w", suffix=".trx", delete=False) as f:
            f.write(SAMPLE_TRX_FULL_NAMES)
            f.flush()
            temp_path = Path(f.name)

        try:
            summary = parse_trx(temp_path)
            self.assertEqual(2, summary["counters"]["total"])
            self.assertEqual(2, summary["counters"]["passed"])
            self.assertEqual(0, summary["counters"]["failed"])
            self.assertEqual(0, summary["counters"]["notExecuted"])

            issues = validate(summary)
            self.assertEqual([], issues)

            markdown = render_markdown(summary, issues)
            self.assertIn("APROBADO (PASS)", markdown)
            self.assertIn(EXPECTED_TESTS[0], markdown)
            self.assertIn(EXPECTED_TESTS[1], markdown)
        finally:
            temp_path.unlink(missing_ok=True)

    def test_validate_partial_name_match_fails(self):
        with tempfile.NamedTemporaryFile("w", suffix=".trx", delete=False) as f:
            f.write(SAMPLE_TRX_PARTIAL_MATCH)
            f.flush()
            temp_path = Path(f.name)

        try:
            summary = parse_trx(temp_path)
            issues = validate(summary)
            self.assertTrue(any("Falta la prueba esperada: BloqueoDistribuido_" in i for i in issues))
            markdown = render_markdown(summary, issues)
            self.assertIn("FALLIDO (FAIL)", markdown)
        finally:
            temp_path.unlink(missing_ok=True)

    def test_validate_skipped_detects_issues(self):
        with tempfile.NamedTemporaryFile("w", suffix=".trx", delete=False) as f:
            f.write(SAMPLE_TRX_SKIPPED)
            f.flush()
            temp_path = Path(f.name)

        try:
            summary = parse_trx(temp_path)
            issues = validate(summary)
            self.assertTrue(any("omitidas" in i for i in issues))
            self.assertTrue(any("ejecutadas" in i for i in issues))
            markdown = render_markdown(summary, issues)
            self.assertIn("FALLIDO (FAIL)", markdown)
        finally:
            temp_path.unlink(missing_ok=True)

    def test_validate_failed_detects_issues(self):
        with tempfile.NamedTemporaryFile("w", suffix=".trx", delete=False) as f:
            f.write(SAMPLE_TRX_FAILED)
            f.flush()
            temp_path = Path(f.name)

        try:
            summary = parse_trx(temp_path)
            issues = validate(summary)
            self.assertTrue(any("fallidas" in i for i in issues))
            self.assertTrue(any("no tiene estado Passed" in i for i in issues))
        finally:
            temp_path.unlink(missing_ok=True)

    def test_find_latest_trx(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            p = Path(tmpdir)
            self.assertIsNone(find_latest_trx(p / "nonexistent"))
            self.assertIsNone(find_latest_trx(p))
            f1 = p / "test1.trx"
            f1.write_text(SAMPLE_TRX_SHORT_NAMES, encoding="utf-8")
            latest = find_latest_trx(p)
            self.assertEqual(f1, latest)

    def test_main_with_valid_trx(self):
        with tempfile.NamedTemporaryFile("w", suffix=".trx", delete=False) as f:
            f.write(SAMPLE_TRX_FULL_NAMES)
            f.flush()
            temp_path = Path(f.name)

        try:
            with patch("sys.argv", ["mongo_trx_summary.py", "--trx", str(temp_path)]):
                code = main()
            self.assertEqual(0, code)
        finally:
            temp_path.unlink(missing_ok=True)

    def test_main_with_missing_trx(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            with patch("sys.argv", ["mongo_trx_summary.py", "--dir", tmpdir]):
                code = main()
            self.assertEqual(1, code)


if __name__ == "__main__":
    unittest.main()
