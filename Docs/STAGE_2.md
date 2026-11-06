# 단계 2 — 시작 숲과 시설 선택

## 범위

- 16×16 타일의 쿼터뷰 시작 숲, 임시 선반과 종균 배양대.
- 시설 클릭 선택, 선택한 시설의 바닥 테두리, 상태 패널.
- 빈 곳을 클릭하면 선택 해제. UI 패널 위 클릭은 월드 선택으로 전달하지 않는다.
- 접종·성장·수확·종균 제작·건설은 아직 구현하지 않는다.
- `GameSession`을 합의한 이름인 `GameManager`로 변경한다. 기존 스크립트 GUID와 직렬화 필드 이름은 유지한다.

## 임시 좌표·정렬·입력 기준

- 정수 `(x, y)`는 타일의 기준 모서리이며, 타일 중심은 `(x + 0.5, y + 0.5)`다.
- 시작 숲의 유효 셀 좌표는 각 축 `0..15`. 논리 공간의 위쪽 경계 `16`은 포함하지 않는다.
- 화면 투영: `worldX = (x - y) × tileWidth / 2`, `worldY = (x + y - mapSize) × tileHeight / 2`.
- 현재 타일 크기는 `1 × 0.5`이고 맵의 전체 중심은 `(0, 0)`이다. 카메라는 Z = -10의 직교 카메라이며 지형을 표현하려고 카메라를 회전하지 않는다.
- 시설 좌표는 점유 사각형의 시작 셀이다. 배양대는 `(3, 13)`의 3×2, 선반은 `(7, 7)`의 2×2로 임시 배치한다.
- 시설 그림의 기준점은 점유 영역 중심이다. 그림의 바닥 기준점 Y가 낮을수록 높은 sortingOrder를 주어 앞에 그린다.
- 클릭은 Physics2D의 시설 BoxCollider2D로 판정한다. 겹치는 클릭 영역에서는 가장 높은 sortingOrder의 시설을 선택한다. 투명한 그림 가장자리도 일부 클릭 영역에 포함된다.
- Input System의 마우스 왼쪽 버튼 입력을 사용한다. 입력 시점의 좌표로 EventSystem UI 레이캐스트를 먼저 실행해 포인터 캐시 갱신 순서와 무관하게 UI 통과 클릭을 차단한다.

이 기준은 프로토타입 구현 기준이며 최종 아트·시설 방향별 배치 설계를 확정하는 것은 아니다.

## 코드와 데이터

| 파일 | 역할 |
|---|---|
| `ForestGrid.cs` | 좌표 변환, 맵 경계와 점유 크기 검사 |
| `FacilityData.cs` | 시설 ID·표시 이름·종류·점유 크기·지원 버섯·임시 스프라이트 |
| `FacilityState.cs` | 배치된 시설의 인스턴스 ID·정의·논리 위치 |
| `FacilitySelection.cs` | 선택 상태와 변경 알림 |
| `ForestGridView.cs` | 초기 배치 전체 검사 후 지면과 시설 오브젝트 생성 |
| `FacilityView.cs` | 그림·클릭 영역·정렬·선택 테두리 |
| `WorldInteractionController.cs` | UI 입력 차단과 월드 시설 선택 |
| `FacilityPanel.cs` | 선택한 시설의 이름·빈 선반 상태·지원 버섯 표시 |
| `ForestCamera.cs` | 화면 비율에 맞춰 맵이 보이도록 카메라 크기 조정 |

임시 그래픽은 `Assets/Art/Prototype`, 시설 정의는 `Assets/Data/Facilities`, 공용 시설 프리팹은 `Assets/Prefabs/Facility.prefab`에 있다. 초기 배치에서 중복 ID·맵 밖 점유·겹침·누락 데이터를 발견하면 오브젝트를 부분 생성하기 전에 오류를 낸다.

## Unity 연결

SampleScene에 연결한다. 지면 256개와 시설 두 개는 Play 시작 시 한 번 생성하며 중복 초기화하지 않는다.

- `Game Manager`: 기존 인벤토리와 초기 지급 유지.
- `Forest`: Ground Sprite, Facility Prefab, Initial Facilities 2개 연결.
- `Main Camera`: `ForestCamera`의 Forest 연결.
- `World Input`: Forest, Main Camera, EventSystem 연결.
- `Inventory Canvas`: GraphicRaycaster, 읽기 전용 패널 배경의 Raycast Target 활성화.
- `EventSystem`: InputSystemUIInputModule 사용.
- `Inventory Canvas / Facility Panel`: Forest와 자식 Status Text 연결. 폰트는 실제 LegacyRuntime 참조 사용.

Project Settings의 입력 방식·Sorting Layer·물리 설정은 바꾸지 않는다. 카메라 크기는 실행 중 화면 비율이 바뀔 때만 다시 계산한다.

## 직접 확인

1. SampleScene을 다시 열고 Play. 16×16 숲과 배양대·빈 선반, 기존 버섯 0개·종균 3개 표시를 확인한다.
2. 중앙 선반 클릭. 노란 바닥 테두리와 `Oyster Shelf`, `Empty | Oyster Mushroom`이 표시되는지 확인한다.
3. 왼쪽 위 가장자리 부근 배양대 클릭. 이전 테두리가 사라지고 `Spawn Workbench`, `Common facility`로 바뀌는지 확인한다.
4. 시설의 가장자리와 빈 타일 클릭. 시설은 선택 가능하고 빈 곳에서는 선택이 해제되는지 확인한다.
5. 시설을 선택한 뒤 보유량 패널과 상태 패널 클릭. 선택이 그대로인지 확인한다.
6. Game 뷰 화면 비율을 변경해 맵이 화면 안에 유지되는지 확인한다.
7. Play 종료 후 재실행해 초기 배치·수량으로 시작하는지 확인한다.

Test Runner에서는 좌표 왕복·경계·점유 크기·선택 상태를 EditMode로, 실제 씬 생성·선택·UI 차단·앞쪽 시설 판정을 PlayMode로 검사한다.
