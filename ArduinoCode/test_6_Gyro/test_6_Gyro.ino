#include <Wire.h>

// MPU6500 I2C 주소 (보통 0x68)
const int MPU_ADDR = 0x68; 

int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ;

void setup() {
  Serial.begin(9600);
  Wire.begin();
  
  // 1. 센서 깨우기 (가장 중요)
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x6B);  // PWR_MGMT_1 레지스터
  Wire.write(0);     // 0을 쓰면 센서가 깨어납니다 (Sleep Mode 해제)
  byte error = Wire.endTransmission(true);

  if(error == 0) {
    Serial.println("센서 연결 성공! (통신 양호)");
  } else {
    Serial.print("센서 연결 실패. 에러 코드: ");
    Serial.println(error);
    Serial.println("해결책: MPU_ADDR을 0x69로 바꿔보거나 배선을 확인하세요.");
  }
}

void loop() {
  // 2. 데이터 요청
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x3B);  // 가속도 X축 상위 바이트부터 읽기 시작
  Wire.endTransmission(false);
  
  // 총 14바이트 요청 (가속도 6 + 온도 2 + 자이로 6)
  Wire.requestFrom(MPU_ADDR, 14, true); 

  if (Wire.available() >= 14) {
    // 데이터를 두 개씩 합쳐서 읽음 (High Byte + Low Byte)
    AcX = Wire.read() << 8 | Wire.read(); 
    AcY = Wire.read() << 8 | Wire.read(); 
    AcZ = Wire.read() << 8 | Wire.read(); 
    Tmp = Wire.read() << 8 | Wire.read(); 
    GyX = Wire.read() << 8 | Wire.read(); 
    GyY = Wire.read() << 8 | Wire.read(); 
    GyZ = Wire.read() << 8 | Wire.read(); 

    // 시리얼 모니터 출력
    Serial.print("가속도 X: "); Serial.print(AcX);
    Serial.print(" | Y: "); Serial.print(AcY);
    Serial.print(" | Z: "); Serial.print(AcZ);
    
    // [수정 후] 실수 나눗셈 (.0 을 붙이세요!)
    Serial.print(" | Gyro X: "); Serial.print(GyX/16384.0); // .0 추가
    Serial.print(" | Gyro Y: "); Serial.print(GyY/16384.0); // .0 추가
    Serial.print(" | Gyro Z: "); Serial.print(GyZ/16384.0); // .0 추가
    Serial.println(); 
  } else {
    Serial.println("데이터 수신 대기중...");
  }
  
  delay(300);
}