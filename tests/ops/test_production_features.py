import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('activation', ROOT / 'scripts/phase18-enable-production-features.py')
activation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(activation)

class ProductionFeatures(unittest.TestCase):
    def test_activation_replaces_disabled_duplicates_and_preserves_credentials(self):
        original = 'Ghasedak__ApiKey=private-value\nGhasedak__EnableLiveSending=false\nGhasedak__EnableLiveSending=false\nConnectionStrings__ArginTaxPayerDb=database-value\n'
        result = activation.updated(original, activation.API_FLAGS)
        self.assertEqual(result.count('Ghasedak__EnableLiveSending='), 1)
        self.assertIn('Ghasedak__EnableLiveSending=true\n', result)
        self.assertIn('Ghasedak__ApiKey=private-value\n', result)
        self.assertIn('ConnectionStrings__ArginTaxPayerDb=database-value\n', result)
        self.assertEqual(activation.updated(result, activation.API_FLAGS), result)
