# UI 구성과 프리팹

UI의 모양과 기본 배치는 프리팹에서 정의하고, C#은 데이터 갱신, 표시 조건, 클릭 동작을 담당한다. 런타임에 버튼·이미지·텍스트를 직접 조립하거나 다른 부모로 옮기지 않는다. 레시피처럼 개수가 달라지는 항목은 전용 프리팹에서 생성한다.

## 폴더

- `Assets/Prefabs/UI/HUD/ContextActions.prefab`: 하단 행동 메뉴. 모든 행동 버튼과 FacilityPanel을 포함한다.
- `Assets/Prefabs/UI/HUD/InventoryPanel.prefab`: 발견한 버섯의 보유량 표시.
- `Assets/Prefabs/UI/Popups/InformationPopup.prefab`: 시설·구역·탐험 정보, 도감, 복원 확인, 탐험 종료 확인에 재사용하는 팝업.
- `Assets/Prefabs/UI/Popups/ProductionPopup.prefab`: 종균 배양 전용 팝업. 제작 콘텐츠를 Popup Window 안에 고정한다.
- `Assets/Prefabs/UI/Elements/RecipeButton.prefab`: 제작 가능한 레시피마다 복제하는 버튼.
- `Assets/Prefabs/UI/Tutorial/TutorialPanel.prefab`: 안내 메시지와 스포트라이트 가림막.
- `Assets/Prefabs/Facilities`: 시설 프리팹.
- `Assets/Prefabs/Exploration`: 탐험 맵 프리팹.
- `Assets/Prefabs/Environment`: 복원 전후 환경 프리팹.

기존 프리팹은 `.meta`와 함께 이동하여 GUID를 유지한다.

## 씬과 런타임

SampleScene의 Gameplay Canvas에 UI 프리팹 인스턴스를 배치한다. GameManager, Forest, 카메라 등 씬 오브젝트 참조는 인스턴스의 Inspector 오버라이드로 연결한다. 프리팹 에셋의 해당 참조가 비어 있는 것은 정상이다.

World Input의 GameplayActionMenu는 하단 메뉴와 두 팝업을 참조한다. OnEnable에서 클릭 및 상태 변경 이벤트를 연결하고 OnDisable에서 해제한다. UI를 다시 만들거나 버튼의 부모를 변경하지 않는다.

FacilityPanel은 Context Actions에 붙어 종균 심기·물 주기 버튼과 시설 정보 문자열을 관리한다. 숨겨진 Text를 정보 저장용으로 사용하지 않는다.

GameplayPopup은 프리팹에 지정된 창을 표시한다. 팝업 루트는 활성 상태를 유지하고 자식 Popup Backdrop을 켜고 끈다. 제작 화면도 같은 방식이라 팝업을 닫아도 SpawnProductionPanel의 상태 구독을 유지한다.

SpawnProductionPanel은 배양대 선택 시 레시피 버튼 프리팹을 복제한다. 목록 간격과 항목 크기는 LayoutGroup과 LayoutElement에서 편집한다. 팝업 높이는 레시피 개수에 맞춰 조정한다.

TutorialPanel은 안내 대상에 맞춰 가림막 크기를 계산하고 가장 위에 표시한다. 팝업을 맨 앞으로 표시하는 형제 순서 변경은 유지한다.

## 편집과 확인

1. 버튼 색상·크기·아이콘·배치는 해당 프리팹의 Image, RectTransform, LayoutElement, LayoutGroup에서 편집한다.
2. 팝업 제목·본문 스타일과 기본 너비는 팝업 프리팹에서 편집한다. GameplayPopup의 Text Height와 여백 설정도 Inspector에서 변경할 수 있다.
3. 기본 화면에서는 보유량과 건설·도감 버튼을 확인한다. 다른 행동 버튼은 선택과 진행 상태에 따라 표시된다.
4. 튜토리얼의 종균 심기 → 수확 → 종균 배양 → 물 주기 → 탐험 진입 → 복귀 확인 흐름을 확인한다.
5. 배양 팝업을 반복해서 열고 닫아도 Content의 부모가 Popup Window로 유지되는지 확인한다.

Unity 배치 모드의 PlayMode 테스트는 실제 씬과 에셋을 로드한다. 배치 모드 검증과 사람이 Game View에서 수행하는 화면·조작 검증은 구분해서 보고한다.
