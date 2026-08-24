import sys
import os
import unittest
from unittest.mock import patch, MagicMock

# Add the Backend folder to sys.path so we can import yolo_api
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

# Mock YOLO and cv2 before importing yolo_api
sys.modules['ultralytics'] = MagicMock()
sys.modules['cv2'] = MagicMock()

import yolo_api

class YoloApiTestCase(unittest.TestCase):
    def setUp(self):
        self.app = yolo_api.app.test_client()
        self.app.testing = True

    def test_get_human_status(self):
        # Reset the global counter for testing
        yolo_api.jumlah_orang_sekarang = 5
        
        response = self.app.get('/api/human')
        self.assertEqual(response.status_code, 200)
        
        data = response.get_json()
        self.assertIn('status', data)
        self.assertIn('rute_1_human', data)
        self.assertIn('crowded', data)
        self.assertIn('timestamp', data)
        
        self.assertEqual(data['status'], 'success')
        self.assertEqual(data['rute_1_human'], 5)
        self.assertFalse(data['crowded'])

    def test_get_human_crowded(self):
        yolo_api.jumlah_orang_sekarang = 15
        
        response = self.app.get('/api/human')
        self.assertEqual(response.status_code, 200)
        
        data = response.get_json()
        self.assertTrue(data['crowded'])

if __name__ == '__main__':
    unittest.main()
