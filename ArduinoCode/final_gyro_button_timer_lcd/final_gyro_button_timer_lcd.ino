//*******************************************************************************//
// Unity ↔ Arduino MPU6500 + 4Digit 7seg + LCD1602 (매출 표시)
//*******************************************************************************//

/* [필수] 하드웨어 안전 수칙
   1. 7세그먼트 핀 2~8번과 7세그먼트 사이에 반드시 220Ω~330Ω 저항을 연결해야 합니다.
   2. LCD와 MPU6500 모두 표준 I2C 핀 (SDA=A4, SCL=A5)에 연결하는 것을 권장합니다.
*/

#include <Wire.h> 
#include <LiquidCrystal_I2C.h> // LCD 제어 라이브러리 추가

///-----------LCD (16x2 I2C)-------------///
// LCD I2C 주소 정의 (가장 일반적인 0x27 사용. 모듈에 따라 0x3F일 수 있음)
// 핀 설정: I2C 통신은 하드웨어적으로 A4(SDA), A5(SCL)에 고정되어 있습니다.
LiquidCrystal_I2C lcd(0x27, 16, 2); 
long currentRevenue = 0; // 현재 매출 (Unity에서 'C' 접두사로 수신)

///-----------GYRO (MPU6500)-------------///
const int MPU_ADDR = 0x68; 
int16_t AcX, AcY, AcZ, Tmp, GyX, GyY, GyZ; 
float GyroScale = 16384.0; 

///---------7 segments & Buzzer------------///
// 사용자 정의 패턴 매핑 유지: { a, b, c, d, e, f, g }
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
const int D2 = 10; // 1분 자리
const int D3 = 12; // 10초 자리
const int D4 = 9;  // 1초 자리
const int BUZZER_PIN = A0; // 부저 핀

// 타이머 변수
int timerSeconds = 300; 
unsigned long lastUpdateTime = 0;
const long updateInterval = 1000; 

// 시리얼 변수 (Unity에서 수신)
String serialData = ""; 

// 논블로킹 부저 제어 변수 (3번 삐-삐-삐 로직)
bool timeoutBeepStarted = false; 
bool buzzerActive = false;       
bool buzzerPhaseOn = false;      
int buzzerBeepIndex = 0;         
unsigned long buzzerLastMillis = 0;

///-----------BUTTON CLICK-------------///
const int buttonPin = A3; // 버튼 핀
int lastButtonState = HIGH; 
bool buttonReleased = false; 

// 함수 선언
void gyroSetup();
void gyroLoop();
void timerSetup();
void lcdSetup(); // [NEW] LCD 초기화 함수
void updateLCDRevenue(long revenue); // [NEW] LCD 매출 업데이트 함수
void timerLoop();
void buttonLoop();
void Num_Write(int number, int dig); 
void displayTime(int totalSeconds);
void readSerialInput();
void checkTimeoutBuzzer();    
void startTimeoutBuzzer();    
void updateTimeoutBuzzer();   
void printToUnity();

/**
 * @brief 초기 설정
 */
void setup() {
    Serial.begin(9600);
    
    // 버튼 핀 설정 (내부 풀업 사용)
    pinMode(buttonPin, INPUT_PULLUP);
    
    gyroSetup();  // MPU6500 초기화 (Wire.begin() 포함)
    timerSetup(); // 7-Segment 및 Buzzer 초기화
    lcdSetup();   // [NEW] LCD 초기화 호출
    
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
    
    // 4. 시리얼 입력 확인 및 데이터(타이머/매출) 파싱
    readSerialInput();
    
    // 5. 논블로킹 부저 상태 갱신 (항상 실행)
    updateTimeoutBuzzer();
}

// --- LCD 로직 ---

void lcdSetup() {
  // Wire.begin()은 gyroSetup에서 호출되므로, 여기서는 LCD만 초기화합니다.
  lcd.init();      // LCD 초기화
  lcd.backlight(); // 백라이트 켜기
  
  // 첫 번째 줄 고정 텍스트 출력
  lcd.setCursor(0, 0);
  lcd.print("Current Revenue:");
  
  // 초기 매출 0 표시
  updateLCDRevenue(currentRevenue);
}

void updateLCDRevenue(long revenue) {
  // 2번째 줄 (0, 1) 위치에 출력
  lcd.setCursor(0, 1);
  
  // "(C)" + 매출 금액 출력 (예: C100)
  lcd.print("(C)");
  lcd.print(revenue);
  
  // 나머지 칸은 공백으로 채워서 이전 값 잔상 제거 (예: C1234 -> C10)
  lcd.print("      "); 
}

// --- MPU6500 (GYRO) 로직 ---

void gyroSetup(){
    // MPU6500과 LCD 모두 I2C 통신을 위해 Wire.begin()을 한 번만 호출합니다.
    Wire.begin(); 
    
    Wire.beginTransmission(MPU_ADDR);
    Wire.write(0x6B); 
    Wire.write(0);  
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
    if (millis() - lastGyroRead >= 300) { 
        lastGyroRead = millis();
        
        Wire.beginTransmission(MPU_ADDR);
        Wire.write(0x3B); 
        Wire.endTransmission(false);
        
        Wire.requestFrom(MPU_ADDR, 14, true); 
        if (Wire.available() >= 14) {
            AcX = Wire.read() << 8 | Wire.read();
            AcY = Wire.read() << 8 | Wire.read(); 
            AcZ = Wire.read() << 8 | Wire.read();
            Tmp = Wire.read() << 8 | Wire.read();
            GyX = Wire.read() << 8 | Wire.read(); 
            GyY = Wire.read() << 8 | Wire.read();
            GyZ = Wire.read() << 8 | Wire.read(); 
            
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

    // 7-Segment Digit 핀 설정
    pinMode(D1, OUTPUT); pinMode(D2, OUTPUT); 
    pinMode(D3, OUTPUT); pinMode(D4, OUTPUT); 
    
    // 부저 핀 설정
    pinMode(BUZZER_PIN, OUTPUT);
    digitalWrite(BUZZER_PIN, LOW); 
    
    // 초기화: 모든 디지트 끔 
    digitalWrite(D1, LOW); digitalWrite(D2, LOW);
    digitalWrite(D3, LOW); digitalWrite(D4, LOW);
}

void timerLoop(){
    // 1. 타이머 카운트다운 (1초마다)
    if (timerSeconds > 0 && millis() - lastUpdateTime >= updateInterval) {
        lastUpdateTime = millis();
        timerSeconds--; 
    }

    // 2. 시간 초과 시 논블로킹 부저 작동 시작
    checkTimeoutBuzzer(); 
    
    // 3. 7-Segment에 시간 표시 (멀티플렉싱)
    displayTime(timerSeconds);
}

void displayTime(int totalSeconds) {
    totalSeconds = constrain(totalSeconds, 0, 5999);
    
    int minutes = totalSeconds / 60;
    int seconds = totalSeconds % 60;

    int m10 = minutes / 10;
    int m1 = minutes % 10;
    int s10 = seconds / 10;
    int s1 = seconds % 10;

    Num_Write(m10, D1);
    Num_Write(m1, D2);
    Num_Write(s10, D3);
    Num_Write(s1, D4);
}

void Num_Write(int number, int dig) {
    // 1. 모든 Digit 끄기 (Active HIGH: LOW가 OFF)
    digitalWrite(D1, LOW);
    digitalWrite(D2, LOW);
    digitalWrite(D3, LOW);
    digitalWrite(D4, LOW);

    // 2. 세그먼트 패턴 설정
    int pin = SegPinStart; 
    for (int j = 0; j < 7; j++) {
        digitalWrite(pin, !num_array[number][j]);
        pin++;
    }

    // 3. 해당 Digit 켜기 (Active HIGH: HIGH가 ON)
    digitalWrite(dig, HIGH);

    // 4. 잔상 대기 (멀티플렉싱 딜레이)
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
    if (buzzerActive) return; 

    buzzerActive = true;
    buzzerPhaseOn = true; 
    buzzerBeepIndex = 0;
    buzzerLastMillis = millis();

    tone(BUZZER_PIN, 1000); 
}

void updateTimeoutBuzzer() {
    if (!buzzerActive) return; 

    unsigned long now = millis();

    if (buzzerPhaseOn) {
        // 삐~ ON (200ms)
        if (now - buzzerLastMillis >= 200) {
            noTone(BUZZER_PIN); 
            buzzerPhaseOn = false; 
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
        buttonReleased = true; 
    }
    
    lastButtonState = currentButtonState;
    delay(10); // 디바운싱
}

// --- 시리얼 입력 로직 ---

void readSerialInput() {
    while (Serial.available()) {
        char inChar = Serial.read();
        
        if (inChar == '\n' || inChar == '\r') {
            // 새 줄 문자(\n)가 입력되면 데이터 처리
            if (serialData.length() > 0) {
                // 1. [NEW] 매출 데이터 처리 (C1234 형식)
                if (serialData.startsWith("C")) {
                    // 'C'를 제외한 나머지 부분을 숫자로 변환
                    long revenue = serialData.substring(1).toInt();
                    if (revenue >= 0) {
                        currentRevenue = revenue;
                        updateLCDRevenue(currentRevenue); // LCD 업데이트
                        Serial.print("REVENUE_SET:"); Serial.println(currentRevenue);
                    }
                } 
                // 2. [EXISTING] 타이머 데이터 처리 (300 형식)
                else {
                    int newTime = serialData.toInt();
                    if (newTime >= 0) {
                        timerSeconds = constrain(newTime, 0, 9999); 
                        lastUpdateTime = millis(); 
                        
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
            }
            serialData = ""; // 시리얼 데이터 버퍼 초기화
        } 
        // 문자 버퍼링: 숫자와 'C'만 버퍼에 추가
        else if (isDigit(inChar) || inChar == 'C') {
            serialData += inChar; 
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
        buttonReleased = false; 
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