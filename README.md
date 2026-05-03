<img width="348" height="528" alt="ProMatch" src="https://github.com/user-attachments/assets/2afdf928-7632-42a9-bbed-6e24f7278e2a" />


# ▦ Blokfit

**A procedural puzzle game built in Unity 6.**  
Players drag and drop organically-shaped pieces onto a triangular half-cell grid.  
Every level is generated at runtime — no hand-authored content.

![Unity](https://img.shields.io/badge/Unity%206-000000?style=flat&logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white)

---

## Why This Project Exists

Most procedural puzzle generators target square or hex grids because adjacency is trivial.  
I chose a **triangular half-cell grid** — each square cell split diagonally into two triangles — specifically because it is *not* trivial. That one decision forced every system in the project to be designed from scratch.

---

## Procedural Generation

> This is the main technical focus of the project.

### 1 · Flat Index Encoding

Every triangle in an N×N grid is addressed by a single integer:

```
flat = (row × N + col) × 2 + type
        ─────────────────   ──────
              cell             0 = lower-right
                               1 = upper-left
```

Recovering components: `cell = flat / 2`, `type = flat % 2`.  
One `int` encodes position and orientation. JSON stays small, adjacency math stays uniform, offset arithmetic never needs a helper struct.

---

### 2 · BFS Flood-Fill Partitioner

`TrianglePartitioner` expands from a seed triangle via BFS until it collects exactly `targetSize` triangles into one region.

Triangular adjacency is asymmetric by type:

```
lower (type 0)  →  three upper neighbours:  same cell · cell below · cell right
upper (type 1)  →  three lower neighbours:  same cell · cell above · cell left
```

Because lower and upper triangles only ever neighbour the opposite type, diagonal boundary crossings are impossible without a single line of clipping logic — the type alternation enforces it structurally.

**Zero-allocation BFS.** All internal collections are pre-allocated fields, cleared between calls:

```csharp
private readonly List<int>  _neighborBuffer = new(3);
private readonly Queue<int> _carveQueue     = new();
private readonly List<int>  _carveResult    = new();
```

A forward-advancing `_nextSeedCursor` finds the next unassigned triangle in O(1) amortised time — no full-grid scans between regions.

---

### 3 · Constraint Pass

Raw BFS output produces valid partitions but unplayable levels.  
`LevelGenerator` enforces three constraints after partitioning:

| Step | Rule |
|---|---|
| Size floor | Regions smaller than `minTriSize` are merged into their nearest neighbour |
| Size ceiling | Regions larger than `maxTriSize` are re-partitioned |
| Piece cap | If piece count exceeds `maxPieces`, smallest pieces are merged first |

All parameters live in a `DifficultyConfig` ScriptableObject — designers tune difficulty without touching code.

Merge distance uses a **centroid cache** (`Dictionary<int[], Vector2>`) keyed by array reference, so repeated lookups during constraint resolution don't recompute from scratch. Stale entries are evicted after each merge.

---

### 4 · Colour Assignment

16 colours are spaced ~22° apart on the HSL hue wheel.  
A **Fisher-Yates shuffle** runs before each level so adjacent pieces are visually distinct by index cycling — no explicit adjacency colouring needed.

---

## Architecture

The generation layer is **pure C# — no `UnityEngine` dependency**. It runs and is tested entirely outside Play Mode.

```
GameManager  (orchestrator — wires systems, owns undo stack)
│
├── LevelGenerator ──► TrianglePartitioner   [pure C#, unit-tested]
│        └── DifficultyConfig  (ScriptableObject)
│
├── BoardController   [grid state, O(1) snap lookup]
│        └── SnapSystem  ──► PlacePieceCommand ──► ICommand
│
├── PieceSpawner ──► PieceFactory  [pure C#, JSON → TriOffset]
│        └── PieceBehaviour  (drag · snap · VFX · DOTween)
│
├── InputHandler  (single-drag gate, multi-touch safe)
├── UIOverlay     (callback injection — no reference to GameManager)
└── LevelSerializer  (static, zero dependencies)
```

### Key Design Decisions

**Relative offset representation.**  
`PieceFactory` converts absolute flat indices to anchor-relative `TriOffset` structs at load time. Snapping a piece to any grid point at runtime is O(k) — add offset to anchor, done.

```csharp
public readonly struct TriOffset
{
    public readonly int dcol, drow, type;
}
```

**O(1) snap point lookup.**  
No spatial hash. No distance loop. Just divide → round → clamp:

```csharp
int vCol = Mathf.Clamp(Mathf.RoundToInt((worldPos.x - _boardOrigin.x) / _cellSize), 0, _gridSize);
int vRow = Mathf.Clamp(Mathf.RoundToInt((worldPos.y - _boardOrigin.y) / _cellSize), 0, _gridSize);
return _snapPoints[vRow, vCol];
```

**Command pattern for undo.**  
Every snap produces a `PlacePieceCommand` pushed to a `Stack<ICommand>`. Undo requires no special-casing — it is just `stack.Pop().Undo()`.

**Zero-allocation hot paths.**

| Frame path | Technique |
|---|---|
| `OnDrag` world pos | Pre-allocated `Vector3` buffer, reused every frame |
| Snap dot candidates | Persistent `List<>` cleared in-place |
| Camera Z | Cached at init, never accessed via `Transform` per frame |
| Snap distance check | Squared magnitude — `Mathf.Sqrt` never called |

**Rendering without overhead.**  
Two shared static `Sprite` instances cover both triangle types. Colour is applied per piece via `MaterialPropertyBlock` — GPU batching is never broken. Grid lines are rendered by a custom shader, not debug utilities.

---

## Custom Level Editor

`LevelEditorWindow` (`Tools > Blokfit > Level Editor`, shortcut `Ctrl+Alt+L`) is a full IMGUI `EditorWindow` built on top of the same pure-C# data model the runtime uses.

- **Left panel** — grid size (2–10), piece list with colour picker and name.
- **Right panel** — N×N triangular grid; left-click assigns, right-click unassigns.
- **Export** — writes `LevelData` JSON to `Assets/Resources/Levels/` and calls `AssetDatabase.Refresh()`.
- **Domain-reload safe** — all state is `[SerializeField]`; script recompilation does not erase work-in-progress.

The editor shares `LevelSerializer.ToJson()` and `LevelData` with the runtime. The JSON the editor exports is byte-for-byte identical to what the generator produces.

---

## Game Feel

Functional is not enough — interactions have to *feel* right.

**VFX** — During drag, snap-dot markers appear at the piece's interior vertices. Interior-ness is computed by counting how many of the piece's own triangles surround each vertex (threshold ladder: 6 → 4 → 2 → 0). Count is weighted-random: ~80% = 1 dot, ~12% = 2, ~5% = 3. Dots fade via DOTween.

**Animation constants are named, not magic numbers.** A wrong easing curve on a 150 ms snap animation makes the whole game feel cheap. Timing and curve values are treated with the same rigour as gameplay tuning parameters.

---

## Testing

Six NUnit EditMode tests cover `TrianglePartitioner` — the most complex pure-C# system:

| Test | What it proves |
|---|---|
| `CarveRegion_ReturnsExactTargetSize` | BFS stops at exactly the requested count |
| `CarveRegion_ReturnedCellsAreContiguous` | All carved triangles form a connected region |
| `FullPartition_CoversAllCells_4x4` | Every triangle in a 4×4 grid is assigned |
| `FullPartition_CoversAllCells_6x6` | Scales correctly to 72 triangles |
| `CarveRegion_LargerThanRemaining` | Gracefully returns all remaining triangles |
| `HasUnassigned_ReturnsFalseAfterFull` | Cursor correctly signals completion |

The `AreContiguous` helper re-runs the same adjacency rules as production code, proving the tests are not testing a different model.

---

## What I Learned

The most important insight was that **the data representation determines system complexity**.  
Encoding a triangle as a single `int` removed an entire class of bugs and made every downstream system — serialization, adjacency, offset math, undo — simpler than if I had used a struct.

The second insight: **procedural generation without constraints is easy; the interesting engineering is in the filter**.  
Getting the BFS to produce valid partitions took one afternoon. Getting those partitions to produce *playable* levels — correctly sized pieces, correct counts, zero-allocation constraint resolution — took the rest of the week.

---

## Tech Stack

`Unity 6` · `C#` · `DOTween` · `ScriptableObjects` · `Custom EditorWindow (IMGUI)` · `NUnit`
