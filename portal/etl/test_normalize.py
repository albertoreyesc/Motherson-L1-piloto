import unittest
from normalize import candidate,positive,CODES
def raw(**values):return {k:{'value':v} for k,v in values.items()}
class NormalizationTests(unittest.TestCase):
 def test_pfep_columns_and_units(self):
  r=candidate('PFEP','NumerosDeParte',raw(B='PART 1',C='SZ01\nPART 1',D='SZ01',L=2400,W=24))
  self.assertEqual(r['parte'],'PART 1');self.assertEqual(r['puesto'],'SZ01');self.assertEqual(r['piezasPorCaja'],24)
 def test_no_pallet_fallback(self):self.assertIsNone(candidate('PFEP','NumerosDeParte',raw(B='PART',D='SZ01',L=2400))['piezasPorCaja'])
 def test_unknown_code_not_inferred(self):self.assertNotIn(candidate('PFEP','NumerosDeParte',raw(B='PART',C='SZ01 PART',D='1J0'))['puesto'],CODES)
 def test_jit_not_consumption(self):self.assertIsNone(candidate('UPAEP','Consumo',raw(B='PART',E=10)))
 def test_integer_factor(self):self.assertIsNone(positive(1.5));self.assertIsNone(positive(True));self.assertIsNone(positive(0));self.assertEqual(positive(24),24)
if __name__=='__main__':unittest.main()
