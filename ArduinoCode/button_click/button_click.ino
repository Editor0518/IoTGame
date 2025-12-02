const int buttonPin = 13; // 버튼을 연결한 디지털 핀
int lastButtonState = HIGH; // 이전 버튼 상태 (초기 상태는 HIGH: 떼어짐)

void setup() {
  // 시리얼 통신 시작 (결과 확인용)
  Serial.begin(9600); 
  
  // 버튼 핀에 내부 풀업 저항 활성화
  // 버튼이 떼어졌을 때 HIGH, 눌렸을 때 LOW가 됨
  pinMode(buttonPin, INPUT_PULLUP);
  Serial.println("Ready. Press the button and then release it.");
}

void loop() {
  // 1. 현재 버튼 상태 읽기
  int currentButtonState = digitalRead(buttonPin);
  
  // 2. 상태 변화 감지 (버튼 릴리즈)
  // 버튼을 떼는 순간 (LOW:눌림 -> HIGH:떼어짐)을 감지
  if (currentButtonState == HIGH && lastButtonState == LOW) {
    // 버튼이 떼어졌을 때 실행할 동작
    Serial.println("Button was released! - Print action performed.");
    // 여기에 Mouse.click(MOUSE_LEFT); 와 같은 마우스 동작을 넣을 수 있습니다.
  }
  
  // 3. 다음 루프를 위해 현재 상태를 이전 상태로 저장
  lastButtonState = currentButtonState;

  // 4. 디바운싱을 위한 짧은 딜레이
  // 짧은 시간 동안 여러 번 눌림이 감지되는 것을 방지
  delay(10); 
}