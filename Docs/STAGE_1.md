# 단계 1 — 자원 및 초기 세션

## 현재 구현 범위

- 버섯 ID별로 버섯과 종균 수량을 분리한다.
- `InventoryService`가 획득, 비용 충족 확인, 소비를 처리한다.
- 0 이하 수량, 미등록 ID, 잘못된 자원 종류, 보유량 부족, `int` 오버플로 요청은 `false`를 반환한다. 실패 시 상태 변경과 변경 알림이 없다.
- 성공한 변경은 수량을 반영한 후 `Changed` 이벤트를 한 번 발생시킨다.
- `GameSession.Initialize()`는 세션당 한 번 초기화한다. 재호출이나 오브젝트 재활성화로 시작 아이템을 중복 지급하지 않는다.
- 느타리는 시작부터 발견 상태이며 버섯 0개, 종균 3개로 시작한다.
- 파일 저장은 아직 없다. 씬을 다시 로드하거나 기본 Play Mode 설정에서 플레이를 다시 시작하면 새 세션이 된다.

## 씬과 Inspector 설정

`Assets/Scenes/SampleScene.unity`에 다음 참조를 연결했다. 추가 수동 연결 없이 Play로 확인할 수 있다.

1. `Game Session`의 `GameSession`
   - Starting Mushroom: `Assets/Data/Mushrooms/OysterMushroom.asset`
   - Starting Mushroom Count: `0`
   - Starting Spawn Count: `3`
2. `Inventory Canvas / Inventory Panel`의 `InventoryPanel`
   - Session: 위 `Game Session`
   - Quantities Text: 자식 `Quantities`의 uGUI Text

느타리 데이터의 ID는 `oyster`다. ID와 표시 이름, 선택적 아이콘은 ScriptableObject에서 조정한다. 시작 수량은 씬의 `GameSession` Inspector에서 조정한다. 플레이 중 데이터 에셋의 ID를 변경하지 않는다.

UI는 기본 폰트로 표시 가능한 임시 영문이다. `Oyster Mushroom`, `Mushrooms: 0`, `Spawn: 3`을 표시한다. 읽기 전용이므로 EventSystem과 입력 모듈은 아직 추가하지 않았다. 버튼을 도입하는 단계에서 Input System UI 모듈과 월드 입력 차단을 연결한다.

## 다음 시스템에서 사용하는 API

```csharp
// GameSession 초기화 이후 사용한다.
string id = session.StartingMushroom.Id;
bool canInoculate = session.Inventory.CanAfford(id, ResourceKind.Spawn, 1);
bool spent = session.Inventory.TrySpend(id, ResourceKind.Spawn, 1);
bool gained = session.Inventory.TryAdd(id, ResourceKind.Mushroom, 3);
InventoryAmounts amounts = session.Inventory.State.GetAmounts(id);
```

`GetAmounts`는 등록된 ID를 읽는 API이며, null에는 `ArgumentNullException`, 미등록 ID에는 `KeyNotFoundException`을 발생시킨다. 거래 요청은 잘못된 ID를 `false`로 처리한다.

접종·수확·제작 UI와 동작은 후속 단계에서 구현한다. 특히 종균 제작의 비용 차감과 결과 지급은 두 호출을 UI에서 이어 붙이지 않고, 제작 단계에서 원자적인 거래로 구현해야 한다.

## 확인 방법

1. SampleScene을 열고 Play를 누른다. 좌측 위 패널의 버섯 0개·종균 3개를 확인한다.
2. `Game Session`을 비활성화했다가 활성화한다. 종균이 6개로 증가하지 않는지 확인한다.
3. Play를 종료하고 다시 실행한다. 버섯 0개·종균 3개로 시작하는지 확인한다.
4. Game 뷰 해상도를 바꾸어 두 줄의 텍스트가 패널 안에 들어오는지 확인한다.
5. `Window > General > Test Runner`에서 EditMode 및 PlayMode 테스트를 실행한다.
   - EditMode: 종류·자원 분리, 소비 경계, 비정상 입력, 오버플로, 알림, 상태 독립성.
   - PlayMode: 실제 SampleScene 초기화, UI 갱신, 재활성화, 씬 재로드 시 새 상태 생성.

씬 재로드를 끄는 Enter Play Mode 옵션 조합은 검증 범위에 포함하지 않는다. 초기 검증에는 Unity의 기본 Domain Reload / Scene Reload 설정을 사용한다.

## 실행한 검증 (2026-11-07)

- Unity `6000.3.19f1` 배치 모드 컴파일 성공.
- EditMode 테스트 16개 통과, 실패 0개.
- PlayMode 테스트 2개 통과, 실패 0개.
- 원본 프로젝트가 다른 프로세스에서 사용 중이어서 `Temp/Stage1Validation` 사본에서 실행했다. 코드·테스트·씬·버섯 데이터의 바이트 일치 및 의존 패키지 버전 일치를 확인했다.
- 결과: `Logs/Stage1-EditMode.xml`, `Logs/Stage1-PlayMode.xml`.
- 그래픽 없는 배치 실행이다. 텍스트 참조·폰트 연결·패널 높이는 자동 검사했지만, 실제 Game 뷰의 시각적 배치와 해상도별 모습은 직접 확인하지 않았다.

## 폰트 참조 수정 (2026-11-07)

- 실제 에디터에서 Text의 Font가 `Missing (Font)`인 문제를 확인했다. 잘못 지정한 내장 폰트 fileID `12800000`을 Unity 폰트 선택기로 `LegacyRuntime`에 다시 연결해 `10102`로 저장했다.
- 실제 SampleScene의 Play 모드에서 `Oyster Mushroom`, `Mushrooms: 0`, `Spawn: 3` 표시를 확인했다.
- 기존 테스트의 CLR null 검사는 Unity의 누락 참조를 놓칠 수 있다. Unity의 `!= null` 검사, 글리프 존재, 양수 텍스트 높이 검사로 보완했다.
- 보완한 PlayMode 테스트 2개를 검증용 사본에서 재실행해 모두 통과했다. 결과: `Logs/FontFix-PlayMode.xml`.
