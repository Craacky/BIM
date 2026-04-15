import hmac
import hashlib
import base64
import json
import time
from datetime import datetime, timedelta

# Configuration matched with C# implementation
SECRET_KEY = "BIM_PROJECT_MASTER_SECRET_KEY_2026_SECURE_V1"
ABSOLUTE_LICENSE_KEY = "ABSOLUTE_LICENSE_PERMANENT_ACCESS"  # Special key for permanent access

def base64url_encode(data):
    if isinstance(data, str):
        data = data.encode('utf-8')
    encoded = base64.urlsafe_b64encode(data).decode('utf-8')
    return encoded.rstrip('=') # Remove padding

def generate_license_key(days_valid=365):
    # 1. Header
    header = {
        "alg": "HS256",
        "typ": "JWT"
    }
    header_json = json.dumps(header, separators=(',', ':'))
    header_b64 = base64url_encode(header_json)

    # 2. Payload
    now = int(time.time())
    exp = now + (days_valid * 24 * 60 * 60)

    payload = {
        "nbf": now,      # Not Before
        "exp": exp,      # Expiration
        "iat": now       # Issued At
    }
    payload_json = json.dumps(payload, separators=(',', ':'))
    payload_b64 = base64url_encode(payload_json)

    # 3. Signature
    target_data = f"{header_b64}.{payload_b64}"
    signature = hmac.new(
        SECRET_KEY.encode('utf-8'),
        target_data.encode('utf-8'),
        hashlib.sha256
    ).digest()
    signature_b64 = base64url_encode(signature)

    # 4. Result
    token = f"{header_b64}.{payload_b64}.{signature_b64}"

    print(f"\n--- LICENSE GENERATED ---")
    print(f"Valid for: {days_valid} days")
    print(f"Expires: {datetime.fromtimestamp(exp)}")
    print(f"Key:\n{token}\n")
    return token

def generate_absolute_license():
    """Generate the absolute/permanent license key"""
    print(f"\n--- ABSOLUTE LICENSE GENERATED ---")
    print(f"This is a permanent license key that bypasses all time checks.")
    print(f"Key:\n{ABSOLUTE_LICENSE_KEY}\n")
    return ABSOLUTE_LICENSE_KEY

if __name__ == "__main__":
    print("BIM Project License Generator")
    print("\nOptions:")
    print("1. Generate time-limited license")
    print("2. Generate absolute/permanent license")
    
    try:
        option = input("\nSelect option (1 or 2, default 1): ").strip()
        
        if option == "2":
            generate_absolute_license()
        else:
            user_input = input("Enter number of days for validity (default 30): ").strip()
            if not user_input:
                days = 30
            else:
                days = int(user_input)

            generate_license_key(days)

        input("Press Enter to exit...")
    except ValueError:
        print("Invalid number entered.")
    except Exception as e:
        print(f"Error: {e}")
