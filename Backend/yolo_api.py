from flask import Flask, jsonify
from ultralytics import YOLO
from datetime import datetime
import cv2
import threading

# Inisialisasi
app = Flask(__name__)

jumlah_orang_sekarang = 0

def jalankan_yolo(url_kamera):
    global jumlah_orang_sekarang
    
    # Load model 
    model = YOLO('yolov8n.pt') 
    
    cap = cv2.VideoCapture(url_kamera)
    
    frame_count = 0
    
    while cap.isOpened():
        success, frame = cap.read()
        if not success:
            continue
            
        frame_count += 1
        
        # Cuma ambil 1 frame aja dari 10
        if frame_count % 10 != 0:
            continue
            
        # classes 0 cuma ambil yang kedeteksi manusia aja
        results = model(frame, classes=[0], conf=0.5, imgsz=320, verbose=False)
        
        for r in results:
            jumlah_orang_sekarang = len(r.boxes)
            print(f"[YOLO] Update: {jumlah_orang_sekarang}")

# API
@app.route('/api/human', methods=['GET'])
def get_crowd_data():
    waktu_sekarang = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    is_crowded = True if jumlah_orang_sekarang > 10 else False
    
    return jsonify({
        "status": "success",
        "rute_1_human": jumlah_orang_sekarang,
        "crowded": is_crowded,  
        "timestamp": waktu_sekarang
    })

# buat setup kamera (pake ip)
if __name__ == '__main__':
    print("Setup Kamera")
    print("format IP Webcam: http://xxx.xxx.x.xx:xxxx/video")
    print("format Webcam Laptop: 0")
    
    input_user = input("Input URL Kamera: ")
    
    if input_user.isdigit():
        input_user = int(input_user) 

    yolo_thread = threading.Thread(target=jalankan_yolo, args=(input_user,))
    yolo_thread.daemon = True
    yolo_thread.start()
    
    print("Buka di http://localhost:5000/api/human")
    app.run(host='0.0.0.0', port=5000)