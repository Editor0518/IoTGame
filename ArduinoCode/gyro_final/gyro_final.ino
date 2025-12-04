//*******************************************************************************//
// Unity ↔ Arduino MPU6500 + 4Digit 7seg 논블로킹 타이머
//*******************************************************************************//

/* [필수] 하드웨어 안전 수칙
   1. 아두이노 핀 2~8번과 7세그먼트 핀 사이에 반드시 220Ω~330Ω 저항을 연결해야 합니다.
   2. 저항이 없으면 아두이노 핀이 과부하로 손상됩니다 (D3 핀의 문제 발생 원인).
*/

#include <Wire.h> // MPU6500 사용을 위한 I2C 라이브러리

///-----------GYRO (MPU6500)-------------///
const int MPU_ADDR = 0x68; // MPU6500 I2C 주소
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ; // 센서 데이터 변수
float GyroScale = 16384.0; // Gyro 데이터 스케일 (MPU6500 기본 설정 기준)

///---------7 segments & Buzzer------------///
// 사용자 정의 패턴 매핑 유지: { a, b, c, d, e, f, g }
// Common Anode (공통 양극) 방식 가정: 1=OFF(HIGH), 0=ON(LOW)
int num_array[10][7] = { 
  { 1, 1, 1, 0, 1, 1, 1 },    // 0
  { 0, 1, 1, 0, 0, 0, 0 },    // 1
  { 1, 1, 0, 1, 1, 0, 1 },    // 2
  { 1, 1, 1, 1, 0, 0, 1 },    // 3
  { 0, 1, 1, 1, 0, 1, 0 },    // 4
  { 1, 0, 1, 1, 0, 1, 1 },    // 5
  { 1, 0, 1, 1, 1, 1, 1 },    // 6
  { 0, 1, 1, 0, 0, 0, 1 },    // 7
  { 1, 1, 1, 1, 1, 1, 1 },    // 8
  { 1, 1, 1, 1, 0, 1, 1 }     // 9
};

// 핀 정의 (사용자 설정 유지)
const int SegPinStart = 2; // 세그먼트 a 핀 (a=2, b=3, ..., g=8)
const int D1 = 11; // 10분 자리
const int D2 = 10; // 1분 자리 (콜론은 D2에서 처리된다고 가정)
const int D3 = 12; // 10초 자리
const int D4 = 9;  // 1초 자리
const int BUZZER_PIN = A0; // 부저 핀

// 타이머 변수
int timerSeconds = 300; // 타이머 변수 (5분 = 300초로 초기화)
unsigned long lastUpdateTime = 0;
const long updateInterval = 1000; // 1초

// 시리얼 변수
String serialData = ""; // 시리얼 입력 버퍼

// 논블로킹 부저 제어 변수 (3번 삐-삐-삐 로직)
bool timeoutBeepStarted = false; // 타임오버가 시작되었는지 확인
bool buzzerActive = false;       // 현재 부저가 작동 중인지 확인
bool buzzerPhaseOn = false;      // 부저 ON/OFF 페이즈
int buzzerBeepIndex = 0;         // 삐 횟수 카운터 (최대 3)
unsigned long buzzerLastMillis = 0;

///-----------BUTTON CLICK-------------///
const int buttonPin = A3; // 버튼 핀
int lastButtonState = HIGH; // 이전 버튼 상태 (INPUT_PULLUP 기준: 떼어짐)
bool buttonReleased = false; // Unity에 전송할 버튼 릴리즈 상태 플래그

// 함수 선언
void gyroSetup();
void gyroLoop();
void timerSetup();
void timerLoop();
void buttonLoop();
void Num_Write(int number, int dig); // 콜론 인자 제거
void displayTime(int totalSeconds);
void readSerialInput();
void checkTimeoutBuzzer();    // 논블로킹 부저 로직 1: 타임오버 감지
void startTimeoutBuzzer();    // 논블로킹 부저 로직 2: 부저 시작
void updateTimeoutBuzzer();   // 논블로킹 부저 로직 3: 부저 상태 업데이트
void printToUnity();

/**
 * @brief 초기 설정
 */
void setup() {
    Serial.begin(9600);
    
    // 버튼 핀 설정 (내부 풀업 사용)
    pinMode(buttonPin, INPUT_PULLUP);
    
    gyroSetup();  // MPU6500 초기화
    timerSetup(); // 7-Segment 및 Buzzer 초기화
    
    Serial.println("SETUP_COMPLETE");
}

/**
 * @brief 메인 루프
 */
void loop() {
    // 1. 버튼 상태 확인 (매우 빠른 주기)
    buttonLoop();
    
    // 2. 타이머 로직 및 7-Segment 표시 (논블로킹)
    timerLoop();
    
    // 3. MPU6500 데이터 읽기 및 Unity 출력 (300ms 주기)
    gyroLoop(); 
    
    // 4. 시리얼 입력 확인
    readSerialInput();
    
    // 5. 논블로킹 부저 상태 갱신 (항상 실행)
    updateTimeoutBuzzer();
}

// --- MPU6500 (GYRO) 로직 ---

void gyroSetup(){
    Wire.begin(); // I2C 통신 시작
    
    Wire.beginTransmission(MPU_ADDR);
    Wire.write(0x6B); // PWR_MGMT_1 레지스터
    Wire.write(0);  // 센서 깨우기
    byte error = Wire.endTransmission(true);
    if(error == 0) {
        Serial.println("MPU6500_CONNECTED");
    } 
    else {
        Serial.print("MPU6500_ERROR:"); 
        Serial.println(error);
    }
}

void gyroLoop(){
    static unsigned long lastGyroRead = 0;
    if (millis() - lastGyroRead >= 300) { // 300ms마다 실행
        lastGyroRead = millis();
        
        Wire.beginTransmission(MPU_ADDR);
        Wire.write(0x3B); // 가속도 X축 상위 바이트부터 읽기 시작
        Wire.endTransmission(false);
        
        Wire.requestFrom(MPU_ADDR, 14, true); // 총 14바이트 요청
        if (Wire.available() >= 14) {
            AcX = Wire.read() << 8 | Wire.read();
            AcY = Wire.read() << 8 | Wire.read(); 
            AcZ = Wire.read() << 8 | Wire.read();
            Tmp = Wire.read() << 8 | Wire.read();
            GyX = Wire.read() << 8 | Wire.read(); 
            GyY = Wire.read() << 8 | Wire.read();
            GyZ = Wire.read() << 8 | Wire.read(); 
            
            // 데이터 수신 후 Unity 형식으로 출력
            printToUnity();
        } 
    }
}

// --- TIMER & 7-SEGMENT 로직 ---

void timerSetup(){
    // 7-Segment Segment 핀 설정 (a=2 to g=8)
    for (int j = SegPinStart; j < SegPinStart + 7; j++) {
        pinMode(j, OUTPUT);
    }

    // 7-Segment Digit 핀 설정 (D1=A2, D2=10, D3=12, D4=9)
    pinMode(D1, OUTPUT); pinMode(D2, OUTPUT); 
    pinMode(D3, OUTPUT); pinMode(D4, OUTPUT); 
    
    // 부저 핀 설정
    pinMode(BUZZER_PIN, OUTPUT);
    digitalWrite(BUZZER_PIN, LOW); // 부저 끔
    
    // 초기화: 모든 디지트 끔 (Active HIGH 핀 제어 로직에 따라 LOW로 초기화)
    digitalWrite(D1, LOW); digitalWrite(D2, LOW);
    digitalWrite(D3, LOW); digitalWrite(D4, LOW);
}

void timerLoop(){
    // 1. 타이머 카운트다운 (1초마다)
    if (timerSeconds > 0 && millis() - lastUpdateTime >= updateInterval) {
        lastUpdateTime = millis();
        timerSeconds--; // 1초 감소
    }

    // 2. 시간 초과 시 논블로킹 부저 작동 시작
    checkTimeoutBuzzer(); 
    
    // 3. 7-Segment에 시간 표시 (멀티플렉싱)
    displayTime(timerSeconds);
}

void displayTime(int totalSeconds) {
    // 99분 59초 (5999초) 초과 시 표시 제한
    totalSeconds = constrain(totalSeconds, 0, 5999);
    
    int minutes = totalSeconds / 60;
    int seconds = totalSeconds % 60;

    int m10 = minutes / 10;
    int m1 = minutes % 10;
    int s10 = seconds / 10;
    int s1 = seconds % 10;

    // 멀티플렉싱: 4개의 디지트를 빠르게 순차적으로 켜서 동시에 켜져있는 것처럼 보이게 함
    Num_Write(m10, D1);
    Num_Write(m1, D2);
    Num_Write(s10, D3);
    Num_Write(s1, D4);
}

// 한 자리 숫자를 특정 디지트에 표시
void Num_Write(int number, int dig) {
    // 1. 모든 Digit 끄기 (Active HIGH: LOW가 OFF)
    digitalWrite(D1, LOW);
    digitalWrite(D2, LOW);
    digitalWrite(D3, LOW);
    digitalWrite(D4, LOW);

    // 2. 세그먼트 패턴 설정
    // Common Anode 방식: 핀이 LOW일 때 켜짐 (!num_array[number][j] 사용하므로 1->LOW)
    int pin = SegPinStart; 
    for (int j = 0; j < 7; j++) {
        digitalWrite(pin, !num_array[number][j]);
        pin++;
    }

    // 3. 해당 Digit 켜기 (Active HIGH: HIGH가 ON)
    digitalWrite(dig, HIGH);

    // 4. 잔상 대기 (멀티플렉싱 딜레이, 2~4ms 추천)
    delay(3); 
}

// --- 논블로킹 부저 로직 (3번 삐-삐-삐) ---

void checkTimeoutBuzzer() {
    if (timerSeconds <= 0 && !timeoutBeepStarted) {
        timerSeconds = 0;
        timeoutBeepStarted = true;
        startTimeoutBuzzer();
    }
}

void startTimeoutBuzzer() {
    if (buzzerActive) return; // 이미 작동 중이면 무시

    buzzerActive = true;
    buzzerPhaseOn = true; // 첫 삐 (ON) 시작
    buzzerBeepIndex = 0;
    buzzerLastMillis = millis();

    tone(BUZZER_PIN, 1000); // 첫 삐 시작
}

void updateTimeoutBuzzer() {
    if (!buzzerActive) return; // 부저 시퀀스가 시작되지 않았으면 종료

    unsigned long now = millis();

    if (buzzerPhaseOn) {
        // 삐~ ON (200ms)
        if (now - buzzerLastMillis >= 200) {
            noTone(BUZZER_PIN); // 부저 끄기
            buzzerPhaseOn = false; // OFF 페이즈로 전환
            buzzerLastMillis = now;
        }
    } else {
        // 쉼 (150ms)
        if (now - buzzerLastMillis >= 150) {
            buzzerBeepIndex++;
            if (buzzerBeepIndex >= 3) {
                // 총 3번 삐 끝
                buzzerActive = false;
                noTone(BUZZER_PIN);
            } else {
                // 다음 삐 시작
                tone(BUZZER_PIN, 1000);
                buzzerPhaseOn = true;
                buzzerLastMillis = now;
            }
        }
    }
}


// --- BUTTON CLICK 로직 ---

void buttonLoop() {
    int currentButtonState = digitalRead(buttonPin);
    
    // 버튼을 떼는 순간 (LOW:눌림 -> HIGH:떼어짐)을 감지 
    if (currentButtonState == HIGH && lastButtonState == LOW) {
        buttonReleased = true; // 릴리즈 플래그 설정
    }
    
    lastButtonState = currentButtonState;
    delay(10); // 디바운싱
}

// --- 시리얼 입력 로직 ---

void readSerialInput() {
    while (Serial.available()) {
        char inChar = Serial.read();
        if (inChar == '\n' || inChar == '\r') {
            if (serialData.length() > 0) {
                int newTime = serialData.toInt();
                if (newTime >= 0) {
                    timerSeconds = constrain(newTime, 0, 9999); // 4자리 제한
                    lastUpdateTime = millis(); // 타이머 재설정
                    
                    // 새 타이머 시작 시 부저 로직 초기화
                    if (timerSeconds > 0) {
                        timeoutBeepStarted = false;
                        buzzerActive = false;
                        noTone(BUZZER_PIN);
                    }
                    
                    Serial.print("TIMER_SET:"); Serial.println(timerSeconds);
                } else {
                    Serial.println("INPUT_INVALID");
                }
            }
            serialData = ""; // 시리얼 데이터 버퍼 초기화
        } else if (isDigit(inChar)) {
            serialData += inChar; // 숫자만 버퍼에 추가
        }
    }
}

// --- UNITY 통신 최적화 로직 ---

/**
 * @brief Unity에서 파싱하기 쉬운 단일 문자열로 시리얼 출력
 * 형식: GYRO_X,GYRO_Y,GYRO_Z,ACCEL_X,ACCEL_Y,ACCEL_Z|BUTTON:STATUS|TIME:MM:SS
 */
void printToUnity() {
    // 1. 자이로 및 가속도 데이터 (구분자: 쉼표 ,)
    Serial.print(GyX/GyroScale, 2); Serial.print(",");
    Serial.print(GyY/GyroScale, 2); Serial.print(",");
    Serial.print(GyZ/GyroScale, 2); Serial.print(",");
    Serial.print(AcX); Serial.print(",");
    Serial.print(AcY); Serial.print(",");
    Serial.print(AcZ); 
    
    // 2. 버튼 상태 (구분자: 파이프 |)
    Serial.print("|BUTTON:");
    if (buttonReleased) {
        Serial.print("RELEASED");
        buttonReleased = false; // 플래그를 바로 초기화
    } else {
        Serial.print("NONE");
    }
    
    // 3. 타이머 남은 시간 (구분자: 파이프 |)
    int minutes = timerSeconds / 60;
    int seconds = timerSeconds % 60;
    
    Serial.print("|TIME:");
    if (minutes < 10) Serial.print("0");
    Serial.print(minutes);
    Serial.print(":");
    if (seconds < 10) Serial.print("0");
    Serial.print(seconds);
    
    Serial.println(); // 줄바꿈으로 패킷 끝 표시
}