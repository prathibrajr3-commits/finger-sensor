import sys
import json
import numpy as np

# Print initialization message
print("AI Engine Ready", flush=True)

# MediaPipe Hands will be lazy-loaded only when frame processing starts
mp_hands = None
hands = None

def init_mediapipe():
    global mp_hands, hands
    if hands is None:
        try:
            import mediapipe as mp
            mp_hands = mp.solutions.hands
            hands = mp_hands.Hands(
                static_image_mode=False,
                max_num_hands=1,
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5
            )
        except Exception as e:
            sys.stderr.write(f"ERROR: Failed to initialize MediaPipe: {str(e)}\n")
            sys.stderr.flush()
            raise

def process_frame(width, height, raw_bytes):
    # Ensure MediaPipe is loaded
    init_mediapipe()

    # Reshape raw bytes to image
    img = np.frombuffer(raw_bytes, dtype=np.uint8).reshape((height, width, 3))
    
    # MediaPipe expects RGB, OpenCV is BGR
    img_rgb = img[:, :, ::-1] # Quick BGR to RGB channel swap
    
    results = hands.process(img_rgb)
    
    response = {"handDetected": False}
    
    if results.multi_hand_landmarks:
        response["handDetected"] = True
        landmarks = results.multi_hand_landmarks[0].landmark
        
        # Extract Wrist (0), Thumb Tip (4), Index Tip (8)
        wrist = landmarks[0]
        thumb_tip = landmarks[4]
        index_tip = landmarks[8]
        
        # Calculate Palm Center as average of wrist (0), index base (5), pinky base (17)
        mcp_index = landmarks[5]
        mcp_pinky = landmarks[17]
        
        palm_x = (wrist.x + mcp_index.x + mcp_pinky.x) / 3.0
        palm_y = (wrist.y + mcp_index.y + mcp_pinky.y) / 3.0
        palm_z = (wrist.z + mcp_index.z + mcp_pinky.z) / 3.0
        
        response["wrist"] = {"x": wrist.x, "y": wrist.y, "z": wrist.z}
        response["palmCenter"] = {"x": palm_x, "y": palm_y, "z": palm_z}
        response["thumbTip"] = {"x": thumb_tip.x, "y": thumb_tip.y, "z": thumb_tip.z}
        response["indexTip"] = {"x": index_tip.x, "y": index_tip.y, "z": index_tip.z}
        
        # Add all 21 landmarks for full features evaluation
        response["landmarks"] = [{"x": lm.x, "y": lm.y, "z": lm.z} for lm in landmarks]
        
    return response

# Main loop reading lines from stdin
while True:
    try:
        line = sys.stdin.readline()
        if not line:
            break # EOF
            
        cmd_line = line.strip()
        if not cmd_line:
            continue
            
        if cmd_line == "PING":
            print("PONG", flush=True)
            continue
            
        elif cmd_line == "SHUTDOWN":
            break
            
        elif cmd_line.startswith("FRAME"):
            # Parse frame header e.g. "FRAME 640 480"
            parts = cmd_line.split()
            if len(parts) != 3:
                response = {"error": "Invalid FRAME command format. Expected 'FRAME width height'"}
                print(json.dumps(response), flush=True)
                continue
                
            width = int(parts[1])
            height = int(parts[2])
            expected_bytes = width * height * 3
            
            # Read binary payload from stdin buffer
            raw_bytes = sys.stdin.buffer.read(expected_bytes)
            
            if len(raw_bytes) < expected_bytes:
                response = {"error": "Unexpected EOF reading frame bytes"}
                print(json.dumps(response), flush=True)
                break
                
            # Perform tracking
            try:
                result = process_frame(width, height, raw_bytes)
                print(json.dumps(result), flush=True)
            except Exception as ex:
                response = {"handDetected": False, "error": f"Inference failed: {str(ex)}"}
                print(json.dumps(response), flush=True)
                
        else:
            response = {"error": f"Unknown command: {cmd_line}"}
            print(json.dumps(response), flush=True)
            
    except Exception as e:
        sys.stderr.write(f"ERROR: Exception in main loop: {str(e)}\n")
        sys.stderr.flush()
        break
