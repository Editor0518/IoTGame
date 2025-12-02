#include <Wire.h> // MPU6500 사용을 위한 I2C 라이브러리 [cite: 9]

///-----------GYRO (MPU6500)-------------///
const int MPU_ADDR = 0x68; // MPU6500 I2C 주소 [cite: 9]
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ; // 센서 데이터 변수 [cite: 9, 10]
float GyroScale = 16384.0; // Gyro 데이터 스케일 (MPU6500 기본 설정 기준)

///---------7 segments & Buzzer------------///
// Common Anode 또는 반전된 회로 구성 가정 [cite: 11]
int num_array[10][7] = { { 1,1,1,1,1,1,0 }, // 0 [cite: 12]
                         { 0,1,1,0,0,0,0 }, // 1 [cite: 12]
                         { 1,1,0,1,1,0,1 }, // 2 [cite: 12]
                         { 1,1,1,1,0,0,1 }, // 3 [cite: 12]
                         { 0,1,1,0,0,1,1 }, // 4 [cite: 13]
                         { 1,0,1,1,0,1,1 }, // 5 [cite: 13]
                         { 1,0,1,1,1,1,1 }, // 6 [cite: 13]
                         { 1,1,1,0,0,0,0 }, // 7 [cite: 14]
                         { 1,1,1,1,1,1,1 }, // 8 [cite: 14]
                         { 1,1,1,1,0,1,1 }}; // 9 [cite: 15]

const int SegPinStart = 2; // 세그먼트 a 핀 (a=2, g=8) [cite: 15]
const int D1=9; // 분 10의 자리 (M10) [cite: 16]
const int D2=10; // 분 1의 자리 (M1) [cite: 16]
const int D3=11; // 초 10의 자리 (S10) [cite: 17]
const int D4=12; // 초 1의 자리 (S1) [cite: 17]
const int BuzzerPin = 3; // 부저 핀 [cite: 17]

int timerSeconds = 300; // 타이머 변수 (5분 = 300초로 초기화) [cite: 18]
unsigned long lastUpdateTime = 0;
const long updateInterval = 1000; // 1초 [cite: 19]

String serialData = ""; // 시리얼 입력 버퍼

///-----------BUTTON CLICK-------------///
const int buttonPin = 13; // 버튼을 14번 핀으로 변경 (13번은 부저가 사용) [cite: 1]
int lastButtonState = HIGH; // 이전 버튼 상태 (초기 상태는 HIGH: 떼어짐) [cite: 2]
bool buttonReleased = false; // Unity에 전송할 버튼 릴리즈 상태 플래그

///------------------------------///

// 함수 선언
void gyroSetup();
void gyroLoop();
void timerSetup();
void timerLoop();
void buttonLoop();
void Num_Write(int number, int dig, bool colon);
void displayTime(int totalSeconds);
void readSerialInput();
void buzz();
void printToUnity();

/**
 * @brief 초기 설정
 */
void setup() {
    Serial.begin(9600); // 시리얼 통신 초기화 [cite: 21]
    
    // 버튼 핀 설정 [cite: 3]
    pinMode(buttonPin, INPUT_PULLUP);
    
    gyroSetup(); // MPU6500 초기화 (Wire.begin() 포함) [cite: 22]
    timerSetup(); // 7-Segment 및 Buzzer 초기화 [cite: 22]
    
    Serial.println("SETUP_COMPLETE");
    Serial.println("Timer and Sensor initialized. Default Time: 5:00.");
    Serial.println("Input new time (seconds) to reset timer.");
}

/**
 * @brief 메인 루프
 */
void loop() {
    // 1. 버튼 상태 확인 (매우 빠른 주기)
    buttonLoop();
    
    // 2. 타이머 로직 및 7-Segment 표시 (빠른 주기) [cite: 24]
    timerLoop();
    
    // 3. MPU6500 데이터 읽기 및 Unity 출력 (300ms 주기)
    gyroLoop(); 
    
    // 4. 시리얼 입력 확인 [cite: 25]
    readSerialInput();
}

// --- MPU6500 (GYRO) 로직 ---

void gyroSetup(){
    Wire.begin(); // I2C 통신 시작 [cite: 27]
    
    Wire.beginTransmission(MPU_ADDR);
    Wire.write(0x6B); // PWR_MGMT_1 레지스터 [cite: 28]
    Wire.write(0); // 센서 깨우기 (Sleep Mode 해제) [cite: 28]
    byte error = Wire.endTransmission(true);
    if(error == 0) {
        Serial.println("MPU6500_CONNECTED");// [cite: 29]
    } 
    else {
        Serial.print("MPU6500_ERROR:"); 
        Serial.println(error); //[//cite: 30]
    }
}

void gyroLoop(){
    static unsigned long lastGyroRead = 0;
    if (millis() - lastGyroRead >= 300) { // 300ms마다 실행 [cite: 32]
        lastGyroRead = millis();
        
        Wire.beginTransmission(MPU_ADDR);
        Wire.write(0x3B); // 가속도 X축 상위 바이트부터 읽기 시작 [cite: 33]
        Wire.endTransmission(false);
        
        Wire.requestFrom(MPU_ADDR, 14, true); // 총 14바이트 요청 [cite: 34]
        if (Wire.available() >= 14) {
            AcX = Wire.read() << 8 | Wire.read(); //[cite: 36]
            AcY = Wire.read() << 8 | Wire.read(); 
            AcZ = Wire.read() << 8 | Wire.read();
            Tmp = Wire.read() << 8 | Wire.read(); //[cite: 37]
            GyX = Wire.read() << 8 | Wire.read(); 
            GyY = Wire.read() << 8 | Wire.read(); //[cite: 38]
            GyZ = Wire.read() << 8 | Wire.read(); 
            
            // 데이터 수신 후 Unity 형식으로 출력
            printToUnity();
        } 
    }
}

// --- TIMER & 7-SEGMENT 로직 ---

void timerSetup(){
    // 7-Segment Segment 핀 설정 [cite: 44]
    for (int j = SegPinStart; j < SegPinStart + 7; j++) {
        pinMode(j, OUTPUT);
        digitalWrite(j, LOW); // 모든 세그먼트 끔 [cite: 44]
    }

    // 7-Segment Digit 핀 설정 [cite: 45]
    pinMode(D1, OUTPUT); pinMode(D2, OUTPUT); 
    pinMode(D3, OUTPUT); pinMode(D4, OUTPUT); 
    
    // 초기에는 모든 디지트 끔 (Common Anode 가정: HIGH = OFF) [cite: 46]
    digitalWrite(D1, HIGH); digitalWrite(D2, HIGH);
    digitalWrite(D3, HIGH); digitalWrite(D4, HIGH);

    // 부저 핀 설정 [cite: 47]
    pinMode(BuzzerPin, OUTPUT);
    digitalWrite(BuzzerPin, LOW); // 부저 끔
}

void timerLoop(){
    // 1. 타이머 카운트다운 (1초마다)
    if (timerSeconds > 0 && millis() - lastUpdateTime >= updateInterval) {
        lastUpdateTime = millis(); //[cite: 48]
        timerSeconds--; // 1초 감소 [cite: 48]
    }

    // 2. 시간 초과 시 부저 알림
    if (timerSeconds == 0) {
        buzz(); //[cite: 50]
    } else {
        digitalWrite(BuzzerPin, LOW); // 시간이 남아있으면 부저 끔 [cite: 51]
    }
    
    // 3. 7-Segment에 시간 표시 [cite: 52]
    displayTime(timerSeconds);
}

void buzz() {
    static unsigned long lastBuzzToggle = 0;
    const long buzzInterval = 500;
    
    if (millis() - lastBuzzToggle >= buzzInterval) {
        lastBuzzToggle = millis(); //[cite: 61]
        // 현재 부저 핀의 상태를 반전 (HIGH <-> LOW) [cite: 61]
        digitalWrite(BuzzerPin, !digitalRead(BuzzerPin));
    }
}

void displayTime(int totalSeconds) {
    int minutes = totalSeconds / 60;// [cite: 72]
    int seconds = totalSeconds % 60;

    int m10 = minutes / 10; //[cite: 73]
    int m1 = minutes % 10; //[cite: 74]
    int s10 = seconds / 10;// [cite: 75]
    int s1 = seconds % 10; //[cite: 76]

    // 멀티플렉싱: 4개의 디지트를 빠르게 순차적으로 켜서 동시에 켜져있는 것처럼 보이게 함
    Num_Write(m10, D1, false); //[cite: 77]
    Num_Write(m1, D2, true); // 콜론은 D2에서 처리한다고 가정 [cite: 78]
    Num_Write(s10, D3, false); //[cite: 79]
    Num_Write(s1, D4, false);
}

void Num_Write(int number, int dig, bool colon) {
    // 모든 디지트 끄기 (Common Anode 가정) [cite: 63]
    digitalWrite(D1, HIGH); digitalWrite(D2, HIGH);
    digitalWrite(D3, HIGH); digitalWrite(D4, HIGH);

    // 세그먼트 설정 [cite: 64]
    int pin = SegPinStart; 
    for (int j = 0; j < 7; j++) {
        // 기존 로직 !num_array[number][j] 유지 [cite: 65]
        digitalWrite(pin, !num_array[number][j]);
        pin++;
    }

    // 원하는 디지트 켜기 (Common Anode 가정: LOW = ON) [cite: 70]
    digitalWrite(dig, LOW);
    delay(2); // 멀티플렉싱을 위한 짧은 지연 [cite: 71]
}

// --- BUTTON CLICK 로직 ---

void buttonLoop() {
  int currentButtonState = digitalRead(buttonPin); //[cite: 5]
  
  // 버튼을 떼는 순간 (LOW:눌림 -> HIGH:떼어짐)을 감지 
  if (currentButtonState == HIGH && lastButtonState == LOW) {
    buttonReleased = true; // 릴리즈 플래그 설정
  }
  
  lastButtonState = currentButtonState; //[cite: 7]
  delay(10); // 디바운싱 [cite: 8]
}

// --- 시리얼 입력 로직 ---

void readSerialInput() {
  while (Serial.available()) {
    char inChar = Serial.read(); //[cite: 53]
    if (inChar == '\n' || inChar == '\r') { // 새 줄 문자(\n)가 입력되면 데이터 처리 [cite: 53]
      if (serialData.length() > 0) {
        int newTime = serialData.toInt(); //[cite: 54]
        if (newTime >= 0) {
          timerSeconds = newTime;
          lastUpdateTime = millis(); // 타이머 재설정 [cite: 55]
          Serial.print("TIMER_SET:"); Serial.println(newTime);
        } else {
          Serial.println("INPUT_INVALID"); //[cite: 57]
        }
      }
      serialData = ""; // 시리얼 데이터 버퍼 초기화 [cite: 58]
    } else if (isDigit(inChar)) {
      serialData += inChar; // 숫자만 버퍼에 추가 [cite: 59]
    }
  }
}

// --- UNITY 통신 최적화 로직 ---

/**
 * @brief Unity에서 파싱하기 쉬운 단일 문자열로 시리얼 출력
 * 형식: GYRO_X,GYRO_Y,GYRO_Z,ACCEL_X,ACCEL_Y,ACCEL_Z|BUTTON:STATUS|TIME:MM:SS
 * STATUS는 RELEASED 또는 NONE
 */
void printToUnity() {
    // 1. 자이로 및 가속도 데이터 (구분자: 쉼표 ,)
    Serial.print(GyX/GyroScale, 2); Serial.print(","); // Gyro X (2자리 소수점)
    Serial.print(GyY/GyroScale, 2); Serial.print(","); // Gyro Y
    Serial.print(GyZ/GyroScale, 2); Serial.print(","); // Gyro Z
    Serial.print(AcX); Serial.print(","); // Accel X
    Serial.print(AcY); Serial.print(","); // Accel Y
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