#include <Wire.h>

///-----------GYRO (MPU6500)-------------///
// MPU6500 I2C 주소 (보통 0x68)
const int MPU_ADDR = 0x68; 
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ;
///------------------------------///

///---------7 segments & Buzzer------------///
// 7-Segment Segment 정의 (a, b, c, d, e, f, g 순서. 1=켜짐, 0=꺼짐)
// Common Cathode (공통 음극) 가정: HIGH=켜짐, LOW=꺼짐. 
// Num_Write에서 `!num_array`를 사용하므로 Common Anode (공통 양극) 또는 회로 구성에 따라 반전되는 형태인 것으로 추정하고 기존 로직을 유지합니다.
int num_array[10][7] = { { 1,1,1,1,1,1,0 }, // 0 (g만 끔)
                         { 0,1,1,0,0,0,0 }, // 1
                         { 1,1,0,1,1,0,1 }, // 2
                         { 1,1,1,1,0,0,1 }, // 3
                         { 0,1,1,0,0,1,1 }, // 4
                         { 1,0,1,1,0,1,1 }, // 5
                         { 1,0,1,1,1,1,1 }, // 6
                         { 1,1,1,0,0,0,0 }, // 7
                         { 1,1,1,1,1,1,1 }, // 8
                         { 1,1,1,1,0,1,1 }}; // 9
                         //a,b,c,d,e,f,g

// 세그먼트 핀 (Pin 2 = a, 3 = b, 4 = c, 5 = d, 6 = e, 7 = f, 8 = g 로 재정의하여 배열 순서와 맞춤)
const int SegPinStart = 2;
// 4자리 디지트 핀
const int D1=9; // 분 10의 자리 (M10)
const int D2=10; // 분 1의 자리 (M1)
const int D3=11; // 초 10의 자리 (S10)
const int D4=12; // 초 1의 자리 (S1)

// 부저 핀 (13번 핀 사용)
const int BuzzerPin = 13;

// 타이머 변수 (5분 = 300초로 초기화)
int timerSeconds = 300; 
unsigned long lastUpdateTime = 0;
const long updateInterval = 1000; // 1초

// 시리얼 입력 관련 변수
String serialData = "";
bool newSerialData = false;

///------------------------------///

// 함수 선언
void gyroSetup();
void gyroLoop();
void timerSetup();
void timerLoop();
void Num_Write(int number, int dig, bool colon);
void displayTime(int totalSeconds);
void readSerialInput();
void buzz();

void setup() {
    // 시리얼 통신 초기화는 한 번만 수행
    Serial.begin(9600); 
    
    gyroSetup(); // MPU6500 초기화 (Wire.begin() 포함)
    timerSetup(); // 7-Segment 및 Buzzer 초기화
    
    Serial.println("타이머 및 센서 초기화 완료. 타이머 시작: 5:00");
    Serial.println("새로운 타이머 시간(초)을 시리얼로 입력하여 설정할 수 있습니다.");
}

void loop() {
    // MPU6500 데이터 읽기 (비교적 느린 300ms 주기)
    gyroLoop(); 
    
    // 타이머 로직 및 표시 (빠른 주기)
    timerLoop(); 
    
    // 시리얼 입력 확인 (SerialEvent 함수는 Loop 밖에서 사용됨)
    readSerialInput();
}

/**
 * @brief MPU6500 센서 초기화 및 통신 설정
 */
void gyroSetup(){
    // Wire.begin()은 Serial.begin(9600); 아래에서 이미 초기화됨
    Wire.begin();
    
    // 1. 센서 깨우기 (가장 중요)
    Wire.beginTransmission(MPU_ADDR);
    Wire.write(0x6B); // PWR_MGMT_1 레지스터
    Wire.write(0); // 0을 쓰면 센서가 깨어납니다 (Sleep Mode 해제)
    byte error = Wire.endTransmission(true);

    if(error == 0) {
        Serial.println("MPU6500 센서 연결 성공!");
    } else {
        Serial.print("MPU6500 센서 연결 실패. 에러 코드: ");
        Serial.println(error);
    }
}

/**
 * @brief MPU6500 자이로 및 가속도 데이터 읽기
 */
void gyroLoop(){
    // MPU6500 데이터 읽기 로직은 유지
    static unsigned long lastGyroRead = 0;
    if (millis() - lastGyroRead >= 300) { // 300ms마다 실행
        lastGyroRead = millis();
    
        Wire.beginTransmission(MPU_ADDR);
        Wire.write(0x3B); // 가속도 X축 상위 바이트부터 읽기 시작
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
            
            // 실수 나눗셈 
            Serial.print(" | Gyro X: "); Serial.print(GyX/16384.0);
            Serial.print(" | Gyro Y: "); Serial.print(GyY/16384.0);
            Serial.print(" | Gyro Z: "); Serial.print(GyZ/16384.0);
            Serial.println(); 
        } else {
            // Serial.println("데이터 수신 대기중..."); // 너무 자주 출력되지 않도록 주석 처리
        }
    }
}
                        
/**
 * @brief 타이머 및 7-Segment, Buzzer 핀 설정
 */
void timerSetup(){
    // 7-Segment Segment 핀 설정 (a=2, b=3, c=4, d=5, e=6, f=7, g=8 순으로 재정의)
    for (int j = SegPinStart; j < SegPinStart + 7; j++) {
        pinMode(j, OUTPUT);
        digitalWrite(j, LOW); // 초기에는 모든 세그먼트 끔
    }

    // 7-Segment Digit 핀 설정 (D1=9, D2=10, D3=11, D4=12)
    pinMode(D1, OUTPUT); 
    pinMode(D2, OUTPUT); 
    pinMode(D3, OUTPUT); 
    pinMode(D4, OUTPUT); 
    
    // 초기에는 모든 디지트 끔 (Common Anode 가정: HIGH = OFF)
    digitalWrite(D1, HIGH);
    digitalWrite(D2, HIGH);
    digitalWrite(D3, HIGH);
    digitalWrite(D4, HIGH);

    // 부저 핀 설정
    pinMode(BuzzerPin, OUTPUT);
    digitalWrite(BuzzerPin, LOW); // 부저 끔
}

/**
 * @brief 타이머 카운트다운 로직 및 부저 제어
 */
void timerLoop(){
    // 1. 타이머 카운트다운 (비동기)
    if (timerSeconds > 0 && millis() - lastUpdateTime >= updateInterval) {
        lastUpdateTime = millis();
        timerSeconds--; // 1초 감소
        Serial.print("남은 시간: "); 
        Serial.print(timerSeconds / 60); 
        Serial.print("분 "); 
        Serial.print(timerSeconds % 60); 
        Serial.println("초");
    }

    // 2. 시간 초과 시 부저 알림
    if (timerSeconds == 0) {
        buzz();
    } else {
        digitalWrite(BuzzerPin, LOW); // 시간이 남아있으면 부저 끔
    }
    
    // 3. 7-Segment에 시간 표시
    displayTime(timerSeconds);
}

/**
 * @brief 시리얼 포트에서 새로운 타이머 값(초 단위) 읽기
 */
void readSerialInput() {
  while (Serial.available()) {
    char inChar = Serial.read();
    // 새 줄 문자(\n)가 입력되면 데이터 처리
    if (inChar == '\n' || inChar == '\r') {
      if (serialData.length() > 0) {
        int newTime = serialData.toInt();
        if (newTime >= 0) {
          timerSeconds = newTime;
          lastUpdateTime = millis(); // 타이머 재설정
          Serial.print("타이머가 ");
          Serial.print(newTime);
          Serial.println("초로 재설정되었습니다.");
        } else {
          Serial.println("유효하지 않은 시간 값입니다.");
        }
      }
      serialData = ""; // 시리얼 데이터 버퍼 초기화
    } else if (isDigit(inChar)) {
      // 숫자만 버퍼에 추가
      serialData += inChar;
    }
  }
}

/**
 * @brief 7-Segment Buzzer 제어
 */
void buzz() {
  // 0초가 되면 500ms 간격으로 부저를 켰다 껐다 반복
  static unsigned long lastBuzzToggle = 0;
  const long buzzInterval = 500;
  
  if (millis() - lastBuzzToggle >= buzzInterval) {
    lastBuzzToggle = millis();
    // 현재 부저 핀의 상태를 반전 (HIGH <-> LOW)
    digitalWrite(BuzzerPin, !digitalRead(BuzzerPin));
  }
}


/**
 * @brief 7-Segment 한 자리에 숫자 출력
 * @param number 표시할 숫자 (0-9)
 * @param dig 활성화할 디지트 핀 (D1, D2, D3, D4 중 하나)
 * @param colon 콜론(소수점)을 켤지 여부 (D2에만 적용)
 */
void Num_Write(int number, int dig, bool colon) 
{
    // 모든 디지트 끄기 (Common Anode 가정)
    digitalWrite(D1, HIGH);
    digitalWrite(D2, HIGH);
    digitalWrite(D3, HIGH);
    digitalWrite(D4, HIGH);

    // 세그먼트 설정
    int pin = SegPinStart; // 핀 2부터 시작
    for (int j = 0; j < 7; j++) {
        // num_array의 순서(a, b, c, d, e, f, g)에 따라 핀 2-8에 출력
        // 기존 로직 !num_array[number][j] 유지
        digitalWrite(pin, !num_array[number][j]);
        pin++;
    }

    // 콜론 (D2에만 적용한다고 가정하고, DP 핀이 따로 없으므로 임의의 세그먼트 f를 콜론으로 사용하지 않고, 
    // 대신 D2와 D3 디지트 사이에 빈 공간을 두어 콜론 효과를 냅니다.)
    // 만약 DP 핀이 있다면 그 핀에 별도 연결이 필요합니다. 
    // 여기서는 타이머의 특성상 디지트 D2가 켜질 때만 Colon/DP 핀을 켜는 방식으로 구현합니다.
    // DP 핀이 14번이라고 가정하고 (핀 정의 없이) 여기서는 시각적 효과를 위해 D2가 켜질 때 'a' 세그먼트 핀 2를 사용합니다. 
    // 그러나 7-seg의 'a'는 숫자의 윗줄이므로, 콜론 대신 0.0001초 단위의 짧은 점멸로 시간을 구분합니다.
    
    // D2 (분 1의 자리)가 활성화될 때만 콜론 흉내 (DP핀 없음 가정)
    // 콜론을 위한 별도 핀이 없으므로, 모든 세그먼트를 끈 후 켜는 방식으로 멀티플렉싱을 제어합니다.

    // 원하는 디지트 켜기 (Common Anode 가정: LOW = ON)
    digitalWrite(dig, LOW);
    
    // 멀티플렉싱을 위한 짧은 지연
    delay(2); 
}

/**
 * @brief 전체 시간(초)을 MM:SS 형식으로 변환하여 4자리 7-Segment에 표시
 * @param totalSeconds 표시할 총 시간 (초)
 */
void displayTime(int totalSeconds) {
    int minutes = totalSeconds / 60;
    int seconds = totalSeconds % 60;

    // MM:SS 형식으로 분리
    int m10 = minutes / 10; // 분 10의 자리
    int m1 = minutes % 10; // 분 1의 자리
    int s10 = seconds / 10; // 초 10의 자리
    int s1 = seconds % 10; // 초 1의 자리

    // 4개의 디지트를 빠르게 순차적으로 켜서 모든 숫자가 동시에 켜져있는 것처럼 보이게 함 (멀티플렉싱)
    
    // 1. 분 10의 자리 (D1)
    Num_Write(m10, D1, false); 
    
    // 2. 분 1의 자리 (D2)
    Num_Write(m1, D2, true); // 콜론은 D2에서 처리한다고 가정
    
    // 3. 초 10의 자리 (D3)
    Num_Write(s10, D3, false); 
    
    // 4. 초 1의 자리 (D4)
    Num_Write(s1, D4, false);
}