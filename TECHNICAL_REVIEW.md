# Blokfit — Teknik Kod İnceleme Dökümanı

> **Hazırlandığı tarih:** 2026-04-04  
> **Engine:** Unity (URP) · **Dil:** C# · **Test:** NUnit (EditMode)

---

## İçindekiler

1. [Projeye Genel Bakış](#1-projeye-genel-bakış)
2. [Dizin ve Namespace Yapısı](#2-dizin-ve-namespace-yapısı)
3. [Veri Modeli](#3-veri-modeli)
4. [Tasarım Desenleri](#4-tasarım-desenleri)
5. [Sistem Sistemi — Çalışma Akışı](#5-sistem-akışı-bir-level-nasıl-başlar)
6. [Detaylı Sistem Analizleri](#6-detaylı-sistem-analizleri)
   - 6.1 [GameManager — Orkestratör](#61-gamemanager--orkestratör)
   - 6.2 [BoardController — Grid Durumu](#62-boardcontroller--grid-durumu)
   - 6.3 [SnapSystem — Snap Mantığı](#63-snapsystem--snap-mantığı)
   - 6.4 [PieceBehaviour — Parça Davranışı](#64-piecebehaviour--parça-davranışı)
   - 6.5 [Command Pattern — Geri Alma](#65-command-pattern--geri-alma)
   - 6.6 [Prosedürel Üretim Sistemi](#66-prosedürel-üretim-sistemi)
   - 6.7 [Input Sistemi](#67-input-sistemi)
   - 6.8 [Factory + Spawner](#68-factory--spawner)
   - 6.9 [Level Serializasyonu](#69-level-serializasyonu)
   - 6.10 [DifficultyConfig — ScriptableObject](#610-difficultyconfig--scriptableobject)
   - 6.11 [UIOverlay](#611-uioverlay)
   - 6.12 [Level Editor (Editor-Only)](#612-level-editor-editor-only)
7. [Performans Kararları](#7-performans-kararları)
8. [Test Kapsamı](#8-test-kapsamı)
9. [Bağımlılık Grafiği](#9-bağımlılık-grafiği)

---

## 1. Projeye Genel Bakış

Blokfit, **N×N üçgensel izgara** (triangular grid) üzerine kurulu bir bulmaca oyunudur. Her kare iki üçgene bölünür (alt-sağ = type 0, üst-sol = type 1). Oyuncu, tepsideki parçaları sürükleyip ızgaraya yapıştırarak tüm hücreleri doldurur.

### Temel Kavramlar

| Kavram | Açıklama |
|---|---|
| **Triangle flat index** | `(row * N + col) * 2 + type` — tüm üçgenlerin tek sayıyla adreslenmesi |
| **SnapPoint** | Izgaranın her köşe noktasına karşılık gelen nesne; parçalar buraya "snap" edilir |
| **Anchor** | Bir parçanın referans noktası — yerel koordinatların sıfır orijini |
| **TriOffset** | Parça içindeki her üçgenin anchor'dan `(dcol, drow, type)` farkı |

---

## 2. Dizin ve Namespace Yapısı

```
Assets/Scripts/
├── Board/               → Blokfit.Board
│   ├── BoardController.cs   # grid durumu + tamamlanma tespiti
│   ├── SnapPoint.cs         # köşe nokta veri + görsel
│   └── SnapSystem.cs        # snap hesabı + command üretimi
├── Commands/            → Blokfit.Commands
│   ├── ICommand.cs          # geri alma arayüzü
│   └── PlacePieceCommand.cs # tek yerleştirme eylemi
├── Core/                → Blokfit.Core
│   ├── GameManager.cs       # orkestratör MonoBehaviour
│   ├── LevelData.cs         # JSON veri sınıfları
│   └── LevelSerializer.cs   # JsonUtility sarmalayıcı
├── Generation/          → Blokfit.Generation
│   ├── LevelGenerator.cs    # prosedürel seviye üreticisi
│   └── TrianglePartitioner.cs # BFS flood-fill bölümleyici
├── Input/               → Blokfit.Input
│   ├── DragState.cs         # aktif sürüklemenin anlık görüntüsü
│   └── InputHandler.cs      # tek-parça sürükleme kapı bekçisi
├── Pieces/              → Blokfit.Pieces
│   ├── PieceBehaviour.cs    # MonoBehaviour: drag/snap/görsel
│   ├── PieceData.cs         # runtime immutable parça tanımı
│   ├── PieceFactory.cs      # JSON → PieceData dönüşümü
│   ├── PieceSpawner.cs      # parçaların sahneye yerleştirilmesi
│   └── PieceSortOrder.cs    # global sıralama sayacı
├── ScriptableObjects/   → Blokfit.ScriptableObjects
│   └── DifficultyConfig.cs  # zorluk parametreleri (tasarımcı verileri)
├── UI/                  → Blokfit.UI
│   └── UIOverlay.cs         # buton bağlantıları + tamamlanma paneli
└── Editor/              → Blokfit.LevelEditor  (yalnız Editor build)
    └── LevelEditorWindow.cs # Unity Editor penceresi

Assets/Tests/EditMode/
└── FloodFillPartitionerTests.cs  → Blokfit.Tests.EditMode
```

**Assembly Definition'lar:**
- `Blokfit.Scripts.asmdef` — çalışma zamanı kodu
- `Blokfit.Editor.asmdef` — yalnız Editor (`UNITY_EDITOR` guard yerine ayrı asmdef; derleme hataları runtime build'e sızmaz)
- `Blokfit.Tests.EditMode.asmdef` — NUnit testleri

---

## 3. Veri Modeli

### 3.1 JSON Seviye Formatı (`LevelData.cs`)

```
LevelData
 ├── GridData grid
 │    └── int size              # kaç hücre × kaç hücre (örn. 4 → 4×4 = 32 üçgen)
 └── PieceJson[] pieces
      ├── int[]  cells          # üçgen flat index listesi
      ├── string color          # "#FF6600" gibi HTML rengi
      ├── int[]  anchors        # [0] = primary anchor flat index
      ├── float  spawnX/spawnY  # dünya-uzayı başlangıç konumu
```

**Neden flat index?**  
Hem satır+sütun+tip üçlüsünü tek bir `int` ile ifade eder, hem de JSON boyutunu küçük tutar. Her yerde `flat / 2` → hücre, `flat % 2` → tip hesabı yapılır.

### 3.2 Runtime Parça Tanımı (`PieceData.cs` + `TriOffset`)

```csharp
public readonly struct TriOffset
{
    public readonly int dcol;  // anchor'dan sütun farkı
    public readonly int drow;  // anchor'dan satır farkı
    public readonly int type;  // 0=alt, 1=üst
}
```

`PieceFactory.FromJson()` absolute flat index'leri anchor'a göre **relative offset'lere** dönüştürür. Bunun avantajı: aynı şekilli bir parça, farklı konumlarda yerleştirildiğinde `SnapPoint` offset'lerini toplayarak hedef üçgeni hesaplamak O(k) kalır (k = üçgen sayısı).

---

## 4. Tasarım Desenleri

### 4.1 Command Pattern

**Neden:** Geri alma (undo) işlemi. Her başarılı snap bir `PlacePieceCommand` üretir ve `GameManager`'daki `Stack<ICommand>` üstüne itilir.

```
ICommand
 └── PlacePieceCommand
      ├── Execute()  → BoardController.Place()  + piece.SetPlaced()
      └── Undo()     → BoardController.Lift()   + piece.ReturnToTray()
```

`SnapSystem.OnMoveExecuted` eventi command'ı `GameManager`'a iletir; `GameManager` stack'i sahiplenir. Bu sayede `SnapSystem` ve `BoardController`, `GameManager`'dan bağımsızdır.

### 4.2 Observer Pattern (C# Events)

| Event | Yayıncı | Abone |
|---|---|---|
| `BoardController.OnBoardCompleted` | `BoardController.CheckCompletion()` | `GameManager.HandleBoardCompleted` |
| `SnapSystem.OnMoveExecuted(ICommand)` | `SnapSystem.TrySnap()` | `GameManager.HandleMoveExecuted` |

`OnDestroy`'da abonelik kaldırılır (`-=`). Memory leak'e karşı doğru pratik.

### 4.3 Factory Pattern

`PieceFactory` static bir sınıftır (MonoBehaviour değil). JSON'dan runtime veri üretmek için tek noktadan sorumluluk: flat index → relative offset dönüşümü ve renk parse'ı burada yapılır. `PieceSpawner` bu factory'yi kullanır ama iç detayını bilmez.

### 4.4 ScriptableObject (Veri Nesneleri)

`DifficultyConfig` tasarımcı parametrelerini kod değişikliği gerektirmeden Inspector'dan ayarlanmasına olanak tanır. Sahneye prefab olarak değil, asset olarak seralize edilir — yani farklı sahnelerde referans paylaşımı mümkündür.

---

## 5. Sistem Akışı — Bir Level Nasıl Başlar?

```
GameManager.Start()
  │
  ├─ BeginLevel(easyConfig)
  │    ├─ pieceSpawner.DestroyAll()          # önceki parçaları temizle
  │    ├─ boardController.ResetBoard()        # occupancy sıfırla
  │    │
  │    ├─ [serverBaseUrl boşsa]
  │    │    └─ levelGenerator.Generate(config)  ──► LevelData
  │    └─ [serverBaseUrl doluysa]
  │         └─ Coroutine: LoadFromServer()
  │              ├─ UnityWebRequest.Get(url)
  │              ├─ başarılı → LevelSerializer.FromJson(json)
  │              └─ başarısız → levelGenerator.Generate(config)  (fallback)
  │
  └─ ApplyLevel(levelData)
       ├─ boardController.Initialize(gridSize)   # snap point grid'i kur
       ├─ snapSystem.SetSnapThreshold(cellSize)  # eşiği güncelle
       └─ pieceSpawner.SpawnAll(pieces, gridSize)
            └─ foreach piece:
                 ├─ PieceFactory.FromJson()      → PieceData
                 ├─ Instantiate(piecePrefab)
                 ├─ piece.Initialize(...)         # sprite, collider, anchor
                 └─ piece.AnimateIn(index)        # DOTween bounce
```

---

```
Oyuncu bir parçayı sürükler:
  PieceBehaviour.OnPointerDown()
    ├─ InputHandler.BeginDrag()   # zaten sürükleme var mı? engellendi mi?
    ├─ board.Lift(this)           # occupancy temizle (undo hazırlığı)
    └─ SetDragging(true)          # scale ↑, sort order ↑, snap dots görünür

  PieceBehaviour.OnDrag()
    └─ transform.position = mouseWorldPos + dragOffset

  PieceBehaviour.OnPointerUp()
    └─ snapSystem.TrySnap(this)
         ├─ board.GetNearestFreeSnapPoint()   # O(1) round & clamp
         ├─ distSq < thresholdSq?
         │    └─ board.Place(piece, snapPoint)  # tüm üçgenler boş mu?
         │         ├─ başarılı → piece.SetPlaced(pos)   [DOTween]
         │         │             PlacePieceCommand oluştur
         │         │             OnMoveExecuted → GameManager → stack.Push(cmd)
         │         └─ başarısız → piece.ReturnToTray()  [DOTween]
         └─ eşik dışı → piece.ReturnToTray()
```

---

```
Tüm üçgenler dolu olduğunda:
  BoardController.CheckCompletion()
    └─ filledTriangles >= totalTriangles
         └─ OnBoardCompleted?.Invoke()
              └─ GameManager.HandleBoardCompleted()
                   ├─ inputHandler.IsBlocked = true   # input dondur
                   └─ uiOverlay.ShowCompletion()       # panel DOFade
```

---

## 6. Detaylı Sistem Analizleri

### 6.1 GameManager — Orkestratör

**Dosya:** [Assets/Scripts/Core/GameManager.cs](Assets/Scripts/Core/GameManager.cs)  
**Namespace:** `Blokfit.Core`  
**MonoBehaviour lifecycle:** `Awake`, `Start`, `OnDestroy`

GameManager sahne içindeki tüm sistemlerin **wiring** (bağlantı) noktasıdır. Kendisi iş mantığı yürütmez; diğer sistemleri koordine eder.

#### Sorumluluklar

| Sorumluluk | Nasıl |
|---|---|
| Level başlatma | `BeginLevel(DifficultyConfig)` |
| Seviye yükleme fallback zinciri | `_serverBaseUrl` → `LevelGenerator` |
| Undo stack | `Stack<ICommand> _commandHistory` |
| Input engelleme | `_inputHandler.IsBlocked = true/false` |
| Handmade level döngüsü | `_handmadeIndex % _handmadeLevels.Length` |

#### Dikkat Çeken Nokta — `ScatterSpawnPositions()`

Level Editor'dan export edilen JSON'larda `spawnX/Y = 0` gelir. Bu metod, bunları tray merkezi etrafında daire üzerinde dağıtır:

```csharp
float angle = (float)i / n * Mathf.PI * 2f;
p.spawnX = center.x + Mathf.Cos(angle) * _traySpread + jitter;
p.spawnY = center.y + Mathf.Sin(angle) * _traySpread * 0.5f + jitter;
```

Y ekseni 0.5x scale ile sıkıştırılmıştır — ekran aspect ratio'suna göre elips görünümü sağlar.

#### GameState Enum

```csharp
private enum GameState { Idle, LevelComplete }
```

Yalnızca input bloğunu yönetmek için minimal state machine. Karmaşık durum geçişleri olmadığından enum basit tutulmuş; bu iyi bir karar.

---

### 6.2 BoardController — Grid Durumu

**Dosya:** [Assets/Scripts/Board/BoardController.cs](Assets/Scripts/Board/BoardController.cs)  
**Namespace:** `Blokfit.Board`

Izgaranın **authoritative state kaynağı**. Parçalar "gerçekte yerleştirildi mi?" sorusuna buradaki diziler cevap verir.

#### İç Veri Yapıları

```csharp
SnapPoint[,]           _snapPoints;          // [vRow, vCol] — (N+1)² köşe
bool[,,]               _occupiedTriangles;   // [row, col, type] — üçgen doluluk
PieceBehaviour[,,]     _occupants;           // hangi parça bu üçgeni tutuyor
Dictionary<PieceBehaviour, List<(int r, int c, int t)>> _pieceOccupancy;
```

`_pieceOccupancy` ters yönlü bir lookup sağlar: parça → üçgen listesi. `Lift()` O(k) yapar (k = parçanın üçgen sayısı), tüm tahtayı taramadan.

#### O(1) Snap Point Bulma

```csharp
public SnapPoint GetNearestFreeSnapPoint(Vector2 worldPos)
{
    int vCol = Mathf.Clamp(Mathf.RoundToInt((worldPos.x - _boardOrigin.x) / _cellSize), 0, _gridSize);
    int vRow = Mathf.Clamp(Mathf.RoundToInt((worldPos.y - _boardOrigin.y) / _cellSize), 0, _gridSize);
    return _snapPoints[vRow, vCol];
}
```

Dünya pozisyonunu grid koordinatına çevirmek sadece böl-yuvarlala-sınırla işlemi. `_boardOrigin` `Initialize`'da önbelleğe alınır, her frame hesaplanmaz.

#### Shader ile Grid Çizgisi

```csharp
_boardMpb.SetFloat(GridSizeProp, gridSize);
_boardRenderer.SetPropertyBlock(_boardMpb);
```

Izgara çizgileri Unity Physics çizgisi değil, shader ile çizilir. `MaterialPropertyBlock` kullanımı, her BoardController örneğinin shared materyali kirletmeden kendi grid boyutunu shader'a iletmesini sağlar.

---

### 6.3 SnapSystem — Snap Mantığı

**Dosya:** [Assets/Scripts/Board/SnapSystem.cs](Assets/Scripts/Board/SnapSystem.cs)

Parça bırakıldığında tek bir karar verir: snap başarılı mı, değil mi?

#### Kritik Optimizasyon — Squared Distance

```csharp
_snapThresholdSq = _snapThreshold * _snapThreshold;
// ...
float distSq = Vector2.SqrMagnitude(primaryAnchorWorldPos - candidate.WorldPosition);
if (distSq >= _snapThresholdSq) return false;
```

`Vector2.Magnitude` içinde `Mathf.Sqrt` çağrısı vardır. Karşılaştırma için kesin mesafeye gerek olmadığından kare mesafe kullanılır — her drag frame'de değil, bırakma anında hesaplanır.

#### Snap Eşiği

```csharp
_snapThreshold = cellSize * 0.5f;
```

Hücre boyutunun yarısı. Dinamik: `Initialize` sırasında `GameManager` tarafından set edilir, sabit kodlanmamış.

---

### 6.4 PieceBehaviour — Parça Davranışı

**Dosya:** [Assets/Scripts/Pieces/PieceBehaviour.cs](Assets/Scripts/Pieces/PieceBehaviour.cs)

En büyük script. Şu sorumlulukları üstlenir:

#### a) Dinamik Sprite Üretimi

Her üçgen tipi için **paylaşılan statik sprite** üretilir (sadece bir kez):

```csharp
private static Sprite _lowerTriSprite;   // type 0
private static Sprite _upperTriSprite;   // type 1
```

`static` kullanımı kritik: 10 parça varsa 10 ayrı texture yerine 2 texture oluşturulur. Renk, `MaterialPropertyBlock` ile her parçada ayrı uygulanır:

```csharp
mpb.SetColor(ColorProp, (Color)Data.Color);
sr.SetPropertyBlock(mpb);
```

`MaterialPropertyBlock` draw call birleştirmeyi (batching) bozmaz; renk farklılığı için materyal klonu oluşturmaktan çok daha verimlidir.

#### b) Dinamik PolygonCollider2D

Her üçgen için ayrı bir path tanımlanır:

```csharp
// type 0 → alt üçgen (BL, BR, TR)
new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1) }
// type 1 → üst üçgen (BL, TR, TL)
new[] { new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(x0, y1) }
```

Çoklu path içeren tek collider, ayrı collider component'leri yerine tercih edilmiştir. Bu Unity'nin multi-shape çokgeni işlemesine izin verir.

#### c) Snap Dot Sistemi

Sürükleme sırasında görünen beyaz noktalar, parçanın "iç" köşe noktalarına yerleştirilir. Köşe noktası seçimi belirleyici bir algoritmaya dayanır:

```
Her köşe için onu çevreleyen parça üçgeni sayısı (max 6) hesaplanır.
Eşik sırası: 6 → 4 → 2 → 0 (en iç noktalar tercih edilir)
```

Bu, noktaların parçanın dışına sarkmamasını sağlar. Sayı ise ağırlıklı rastgele:

```csharp
// ~80% → 1 dot, ~12% → 2, ~5% → 3, ~3% → 4
```

#### d) Sürükleme Optimizasyonu

```csharp
private Vector3 _screenToWorldBuffer;   // her frame heap tahsisi yapılmaz

private Vector2 ScreenToWorld(Vector2 screenPos)
{
    _screenToWorldBuffer.x = screenPos.x;
    _screenToWorldBuffer.y = screenPos.y;
    _screenToWorldBuffer.z = _cameraZ;   // önbelleğe alınmış, her frame erişilmez
    return _camera.ScreenToWorldPoint(_screenToWorldBuffer);
}
```

#### e) Animasyonlar (DOTween)

| Animasyon | Metod | Açıklama |
|---|---|---|
| Giriş | `AnimateIn(index)` | `OutBounce`, `index * 0.12s` stagger |
| Snap | `SetPlaced()` | `DOMove 0.15s OutQuad` |
| Geri dön | `ReturnToTray()` | `DOMove 0.2s OutQuad` |
| Snap dots | `SetDragging()` | `DOFade` görünür/görünmez |

`DOKill()` her animasyon öncesi çağrılır — önceki tween'in yarıda kesilmesi sağlanır.

---

### 6.5 Command Pattern — Geri Alma

**Dosya:** [Assets/Scripts/Commands/](Assets/Scripts/Commands/)

```csharp
public interface ICommand
{
    void Execute();
    void Undo();
}
```

`PlacePieceCommand` tüm gerekli state'i constructor'da alır:

```csharp
new PlacePieceCommand(piece, _board, fromPos, targetPiecePos, candidate)
```

- `fromPos` — drag başlamadan önceki pozisyon (undo için geri dönüş noktası)  
- `targetPiecePos` — snap sonrası dünya pozisyonu  
- `candidate` — hangi snap point'e yerleştirildi (undo sırasında kullanılmaz ama execute için lazım)

`Undo()`:
1. `_board.Lift(piece)` — occupancy temizlenir
2. `piece.ReturnToTray(fromPos)` — DOTween animasyonuyla geri döner

Stack `GameManager`'da tutulur; level değişince temizlenir.

---

### 6.6 Prosedürel Üretim Sistemi

**Dosyalar:** [Assets/Scripts/Generation/](Assets/Scripts/Generation/)

#### TrianglePartitioner — BFS Flood-Fill

Temel algoritma: her adımda seed üçgenden başlayarak komşu üçgenlere BFS ile yayıl, `targetSize` kadar üçgen topla.

**Adjacency kuralları (üçgen tiplerine göre):**

```
lower (type 0) komşuları → hepsi upper (type 1):
  ├─ aynı hücre            → (col,   row,   1)
  ├─ alt hücre             → (col,   row-1, 1)
  └─ sağ hücre             → (col+1, row,   1)

upper (type 1) komşuları → hepsi lower (type 0):
  ├─ aynı hücre            → (col,   row,   0)
  ├─ üst hücre             → (col,   row+1, 0)
  └─ sol hücre             → (col-1, row,   0)
```

Her üçgenin en fazla 3 komşusu var. Alt-üst değişimi nedeniyle köşegen sınır geçişleri doğal olarak engellenir.

**Bellek optimizasyonu:**

```csharp
private readonly List<int>  _neighborBuffer = new List<int>(3);   // tekrar kullanılır
private readonly List<int>  _carveResult    = new List<int>();    // tekrar kullanılır
private readonly Queue<int> _carveQueue     = new Queue<int>();   // tekrar kullanılır
```

Her `CarveRegion` çağrısında yeni nesne oluşturulmaz, aynı koleksiyonlar temizlenerek kullanılır.

`HasUnassigned` ileri yönlü cursor tutar (`_nextSeedCursor`) — atanmış olanları her seferinde baştan taramaz.

#### LevelGenerator — Parça Boyutu ve Sayısı Kontrolü

`TrianglePartitioner` bölgeleri üretir, `LevelGenerator` kısıtları uygular:

1. Her bölge `[minTriSize, maxTriSize]` aralığında hedeflenir
2. Küçük çıkan bölgeler (`< minTriSize`) en yakın komşuyla birleştirilir
3. Parça sayısı `maxPieces`'i aşıyorsa en küçük olanlar birleştirilir

**Centroid cache:**

```csharp
private readonly Dictionary<int[], Vector2> _centroidCache = new();
```

Birleştirme sırasında centroid defalarca sorgulanabilir. Cache, referans eşitliği (array referansı) üzerinden çalışır. Birleştirme sonrası eski diziler cache'den temizlenir.

**Renk paleti:**

16 renk, hue wheel'de ~22° aralıklarla seçilmiştir. Her level için Fisher-Yates shuffle yapılır; böylece aynı renk iki farklı parçada kullanılmaz (mod ile cycle).

---

### 6.7 Input Sistemi

**Dosyalar:** [Assets/Scripts/Input/](Assets/Scripts/Input/)

#### InputHandler — Tek Sürükleme Kapı Bekçisi

```csharp
public bool BeginDrag(PieceBehaviour piece, PointerEventData eventData)
{
    if (IsBlocked) return false;   // level-complete durumu
    if (IsDragging) return false;  // başka parça sürükleniyor
    CurrentDrag = new DragState(piece, eventData.pointerId);
    return true;
}
```

`IsBlocked` — `GameManager` tarafından level tamamlandığında `true` yapılır, yeni level başlayınca `false`.

`IsDragging` — aynı anda iki parmak (multi-touch) farklı parçaları sürüklemeyi engeller.

#### DragState — Immutable Anlık Görüntü

```csharp
public class DragState
{
    public PieceBehaviour Piece     { get; }
    public int            PointerId { get; }
}
```

Hangi pointer'ın hangi parçayı sürüklediğini saklar. Setter yok — immutable tasarım hataları önler.

---

### 6.8 Factory + Spawner

**PieceFactory** (static):  
JSON flat index'lerini anchor'a göre relative `TriOffset`'lere dönüştürür:

```csharp
int anchorCellFlat = primaryAnchorFlat / 2;
int anchorCol      = anchorCellFlat % gridSize;
int anchorRow      = anchorCellFlat / gridSize;

// Her üçgen için:
offsets[i] = new TriOffset(col - anchorCol, row - anchorRow, type);
```

Bu dönüşüm sayesinde aynı şekilli parça farklı konumlara yerleştirildiğinde sadece anchor snap point'i değişir, offset listesi aynı kalır.

**PieceSpawner** (MonoBehaviour):  
Inspector'da tuttuğu `_piecePrefab`'ı instantiate eder. Spawn sonrası `PieceSortOrder.Reset()` çağrılır — level değişiminde sıralama sayacı sıfırlanır.

---

### 6.9 Level Serializasyonu

**LevelSerializer:**
```csharp
public static string    ToJson(LevelData data) => JsonUtility.ToJson(data, prettyPrint: true);
public static LevelData FromJson(string json)  => JsonUtility.FromJson<LevelData>(json);
```

`JsonUtility` Unity'nin yerleşik serializer'ı. `Newtonsoft.Json` gibi harici paket gerekmez; `[Serializable]` attribute yeterli. Kısıtı: `Dictionary` ve `interface` desteklenmez — bu yüzden `LevelData` sade POCO sınıflardan oluşur.

**Sunucudan yükleme:**
```csharp
using var req = UnityWebRequest.Get(url);
yield return req.SendWebRequest();
if (req.result == UnityWebRequest.Result.Success)
    ApplyLevel(LevelSerializer.FromJson(req.downloadHandler.text));
else
    ApplyLevel(_levelGenerator.Generate(config));  // graceful fallback
```

`using var` — `UnityWebRequest` `IDisposable` implement eder; `using` ile bellek serbest bırakılır. Başarısız istek sessizce prosedürel üretimle devam eder.

---

### 6.10 DifficultyConfig — ScriptableObject

**Dosya:** [Assets/Scripts/ScriptableObjects/DifficultyConfig.cs](Assets/Scripts/ScriptableObjects/DifficultyConfig.cs)

```csharp
[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Blokfit/Difficulty Config")]
public class DifficultyConfig : ScriptableObject
{
    public string difficultyName;
    public int    gridSize      = 4;
    public int    minPieces     = 5;
    public int    maxPieces     = 6;
    public int    minPieceSize  = 3;   // kare cinsinden (dahili olarak ×2 → üçgen)
    public int    maxPieceSize  = 4;
}
```

**Neden ScriptableObject?**  
- Tasarımcı kod değişikliği yapmadan parametreleri ayarlayabilir  
- Birden fazla sahne/level aynı asset'i referans gösterebilir  
- `MonoBehaviour` gerektirmez, sahne bağımsız

`GameManager` üç ayrı config tutar: `_easyConfig`, `_mediumConfig`, `_hardConfig`. `BeginLevel` hangisi geçilirse onu `_currentConfig` olarak saklar.

---

### 6.11 UIOverlay

**Dosya:** [Assets/Scripts/UI/UIOverlay.cs](Assets/Scripts/UI/UIOverlay.cs)

Callback tabanlı tasarım: `GameManager` `Awake`'de callback'leri atar, `UIOverlay` butona basılınca bu callback'leri çağırır.

```csharp
_uiOverlay.SetDifficultyCallbacks(
    easy:   () => BeginLevel(_easyConfig),
    medium: () => BeginLevel(_mediumConfig),
    hard:   () => BeginLevel(_hardConfig));
```

Bu yaklaşım `UIOverlay`'i `GameManager`'dan bağımsız tutar; `UIOverlay`, `GameManager`'ı import etmez.

Tamamlanma paneli animasyonu için DOTween fade kullanılır; başlatmadan önce `DOKill()` çağrılır.

---

### 6.12 Level Editor (Editor-Only)

**Dosya:** [Assets/Scripts/Editor/LevelEditorWindow.cs](Assets/Scripts/Editor/LevelEditorWindow.cs)  
**Açılış:** `Tools > Blokfit > Level Editor` (kısayol: `Ctrl+Alt+L`)

IMGUI tabanlı editor penceresi:
- Sol panel: ızgara boyutu, parça listesi (renk seçici, isim, sil)
- Sağ panel: N×N üçgen ızgarası, tıkla-boyayan sistem
- Araç çubuğu: Clear + Export JSON

Her üçgen için `_owner` dizisi tutulur (`-1 = atanmamış, ≥0 = parça index`). Export sırasında bu diziden `PieceJson` nesneleri üretilir ve `LevelSerializer.ToJson()` ile dosyaya yazılır. `spawnX/Y = 0` bırakılır — `GameManager.ScatterSpawnPositions()` çalışma zamanında dağıtır.

State `[SerializeField]` ile işaretlenmiştir: Unity domain reload'dan (script değişikliği derleme) sonra bile pencere içeriği korunur.

---

## 7. Performans Kararları

| Karar | Konum | Neden |
|---|---|---|
| Statik paylaşılan üçgen sprite'ları | `PieceBehaviour` | N parça için 2 texture |
| `MaterialPropertyBlock` ile renk | `PieceBehaviour.BuildTriangleSprites()` | Batching korunur |
| Squared distance karşılaştırması | `SnapSystem.TrySnap()` | `sqrt` çağrısından kaçınılır |
| `_boardOrigin` önbelleği | `BoardController.Initialize()` | Drag frame'lerinde aritmetik yok |
| `_cameraZ` önbelleği | `PieceBehaviour.Initialize()` | Her `OnDrag`'de Transform erişimi yok |
| Reused collection buffers | `TrianglePartitioner` | BFS'de GC baskısı azaltılır |
| `_nextSeedCursor` ilerleyen cursor | `TrianglePartitioner.HasUnassigned()` | O(1) yerine O(n) tarama |
| `_screenToWorldBuffer` reuse | `PieceBehaviour` | OnDrag'de heap tahsisi yok |

---

## 8. Test Kapsamı

**Dosya:** [Assets/Tests/EditMode/FloodFillPartitionerTests.cs](Assets/Tests/EditMode/FloodFillPartitionerTests.cs)

| Test | Ne test ediyor |
|---|---|
| `CarveRegion_ReturnsExactTargetSize` | Tam hedef büyüklüğünde bölge döner |
| `CarveRegion_ReturnedCellsAreContiguous` | Tüm üçgenler bitişik (adjacency ile bağlı) |
| `FullPartition_CoversAllCells_4x4` | 4×4 = 32 üçgen, hepsi kapsanıyor |
| `FullPartition_CoversAllCells_6x6` | 6×6 = 72 üçgen, hepsi kapsanıyor |
| `CarveRegion_LargerThanRemaining_ReturnsAllRemaining` | Kalan azsa mevcut olanları döndürür |
| `HasUnassigned_ReturnsFalseAfterFullCoverage` | Tüm üçgenler atandıktan sonra false |

`AreContiguous` yardımcı metodu, test içinde `TrianglePartitioner.GetFreeNeighbors`'ın aynı adjacency kurallarını DFS ile doğrular. Bu, production ve test kodunun aynı kural kümesini kullandığını kanıtlar.

**Eksik test kapsamı** (code review'da potansiyel soru):
- `PieceFactory.FromJson()` unit testleri yok
- `BoardController.Place/Lift` unit testleri yok
- `SnapSystem.TrySnap()` integration test yok

---

## 9. Bağımlılık Grafiği

```
GameManager
 ├─── BoardController ◄─── SnapSystem ◄─── PieceBehaviour
 │         │                                     │
 │    SnapPoint[,]                          PieceData
 │                                          PieceSortOrder
 ├─── PieceSpawner ──► PieceFactory ──► PieceData
 │         │
 │    PieceBehaviour (Instantiate)
 │
 ├─── LevelGenerator ──► TrianglePartitioner
 │         │
 │    DifficultyConfig (ScriptableObject)
 │
 ├─── InputHandler ──► DragState
 │
 ├─── UIOverlay  (callback tersine bağımlılık: GameManager → UIOverlay)
 │
 └─── LevelSerializer (static, bağımsız)

Commands:
 SnapSystem → PlacePieceCommand → ICommand → GameManager (via event)
```

**Dikkat çeken bağımsızlıklar:**
- `UIOverlay`, `GameManager`'ı import etmez (callback tersine enjeksiyon)
- `PieceFactory` static; herhangi bir MonoBehaviour'a bağımlı değil
- `LevelSerializer` ve `LevelData` saf POCO; Unity'ye minimum bağımlılık
- `TrianglePartitioner` saf C#; `UnityEngine` import etmez (test edilebilirlik için kritik)
- `DragState` immutable; setter yok

---

*Bu döküman, Blokfit projesinin tüm çalışma zamanı scriptlerinin kod incelemesi için hazırlanmıştır. Her servis, neden bu şekilde tasarlandığını ve hangi alternatiflerin neden seçilmediğini açıklamaktadır.*
