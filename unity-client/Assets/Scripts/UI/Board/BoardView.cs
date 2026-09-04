using System;
using System.Collections.Generic;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using BlastScale.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Board
{
    /// <summary>
    /// Renders a <see cref="BoardState"/> as a grid of tappable blocks and animates the difference
    /// between two snapshots: popped blocks scale up and vanish in a burst, survivors slide down
    /// into their new cells and fresh blocks drop in from above. The grid layout is computed by
    /// hand (no GridLayoutGroup) so every block can be tweened freely. The engine stays the only
    /// source of truth: the view never decides where a block goes, it only animates what the
    /// before/after snapshots say.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float Spacing = 8f;
        private const float MaxCellSize = 122f;
        private const float PopSeconds = 0.18f;
        private const float FallDelay = 0.12f;
        private const float FallSeconds = 0.22f;
        private const float DropSeconds = 0.36f;
        private const float ColumnStagger = 0.02f;

        private RectTransform _area;
        private RectTransform _grid;
        private int _rows;
        private int _cols;
        private Action<int, int> _onTap;
        private BlockView[,] _blocks;
        private Image[,] _slots;
        private readonly Stack<BlockView> _pool = new Stack<BlockView>();
        private readonly List<BlockView> _falling = new List<BlockView>();
        private bool _wiggle;
        private bool _wiggleDirty;

        /// <summary>Set when the grid was re-laid out while blocks were animating; they snap into place afterwards.</summary>
        private bool _layoutDirty;

        /// <summary>True while a pop/gravity/shuffle animation runs; taps are ignored meanwhile.</summary>
        public bool IsAnimating { get; private set; }

        /// <summary>Side of one cell in canvas units (0 until the first layout pass).</summary>
        public float CellSize { get; private set; }

        /// <summary>Creates the view inside <paramref name="area"/>; the grid centres itself and clips falling blocks.</summary>
        public static BoardView Create(RectTransform area, int rows, int cols, Action<int, int> onTap)
        {
            var view = area.gameObject.AddComponent<BoardView>();
            view._area = area;
            view._rows = rows;
            view._cols = cols;
            view._onTap = onTap;
            area.gameObject.AddComponent<RectMask2D>();

            view._grid = UiFactory.CreateRect(area, "Grid");
            view._grid.anchorMin = view._grid.anchorMax = new Vector2(0.5f, 0.5f);
            view._grid.pivot = new Vector2(0.5f, 0.5f);

            view._slots = new Image[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Image slot = UiFactory.CreateImage(view._grid, "Slot", BlockSprites.Slot(), UiTheme.SlotFill);
                    slot.rectTransform.anchorMin = slot.rectTransform.anchorMax = new Vector2(0f, 1f);
                    view._slots[r, c] = slot;
                }
            }
            view._blocks = new BlockView[rows, cols];
            view.Layout();
            return view;
        }

        // ------------------------------------------------------------------ layout

        private void OnRectTransformDimensionsChange()
        {
            if (_grid != null)
            {
                Layout();
            }
        }

        /// <summary>Largest square cell such that rows x cols cells (plus spacing) fit into the area.</summary>
        private void Layout()
        {
            Rect rect = _area.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            float byWidth = (rect.width - Spacing * (_cols - 1)) / _cols;
            float byHeight = (rect.height - Spacing * (_rows - 1)) / _rows;
            float cell = Mathf.Floor(Mathf.Min(byWidth, byHeight, MaxCellSize));
            if (cell < 8f)
            {
                return;
            }
            CellSize = cell;
            _grid.sizeDelta = new Vector2(_cols * cell + (_cols - 1) * Spacing, _rows * cell + (_rows - 1) * Spacing);
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    RectTransform slot = _slots[r, c].rectTransform;
                    slot.sizeDelta = new Vector2(cell, cell);
                    slot.anchoredPosition = CellPosition(r, c);
                    BlockView block = _blocks[r, c];
                    if (block != null && !IsAnimating)
                    {
                        Place(block, r, c);
                    }
                }
            }
            if (IsAnimating)
            {
                _layoutDirty = true;
            }
        }

        /// <summary>Ends an animation; if the grid changed size meanwhile every block snaps to its cell.</summary>
        private void FinishAnimation(Action onDone)
        {
            IsAnimating = false;
            if (_layoutDirty)
            {
                _layoutDirty = false;
                Layout();
            }
            onDone?.Invoke();
        }

        /// <summary>Anchored position (top-left anchored, centre pivot) of a cell; rows may be negative (above the board).</summary>
        private Vector2 CellPosition(int row, int col)
        {
            float step = CellSize + Spacing;
            return new Vector2(col * step + CellSize * 0.5f, -(row * step + CellSize * 0.5f));
        }

        private void Place(BlockView block, int row, int col)
        {
            block.Row = row;
            block.Col = col;
            float size = CellSize * BlockSprites.ImageScale;
            block.Rect.sizeDelta = new Vector2(size, size);
            block.Rect.anchoredPosition = CellPosition(row, col);
        }

        // ------------------------------------------------------------------ queries

        /// <summary>World position of a cell's centre (for particles and popups).</summary>
        public Vector3 WorldPosition(int row, int col)
        {
            BlockView block = _blocks[row, col];
            if (block != null)
            {
                return block.Rect.position;
            }
            return _grid.TransformPoint(CellPosition(row, col) - new Vector2(_grid.rect.width * 0.5f, -_grid.rect.height * 0.5f));
        }

        /// <summary>World position of the centre of a group of cells.</summary>
        public Vector3 GroupCentre(List<CellPos> group)
        {
            if (group == null || group.Count == 0)
            {
                return _grid.position;
            }
            Vector3 sum = Vector3.zero;
            foreach (CellPos cell in group)
            {
                sum += WorldPosition(cell.Row, cell.Col);
            }
            return sum / group.Count;
        }

        // ------------------------------------------------------------------ building

        /// <summary>Shows a snapshot immediately (initial board) with a small staggered pop-in.</summary>
        public void SetSnapshot(int[][] snapshot, bool animateIn)
        {
            ClearAll();
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    BlockView block = Spawn(snapshot[r][c]);
                    Place(block, r, c);
                    _blocks[r, c] = block;
                    if (animateIn)
                    {
                        Tween.ScaleFrom(block.Rect, 0f, 0.3f, Ease.OutBack, 0.012f * (r * _cols + c) + 0.05f);
                    }
                }
            }
            if (animateIn)
            {
                IsAnimating = true;
                Tween.Delay(0.012f * _rows * _cols + 0.4f, () => FinishAnimation(null), this);
            }
        }

        private BlockView Spawn(int color)
        {
            BlockView block;
            if (_pool.Count > 0)
            {
                block = _pool.Pop();
                block.Rect.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Block", typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(_grid, false);
                block = new BlockView
                {
                    Rect = (RectTransform)go.transform,
                    Group = go.GetComponent<CanvasGroup>(),
                    Image = go.AddComponent<Image>()
                };
                block.Rect.anchorMin = block.Rect.anchorMax = new Vector2(0f, 1f);
                block.Rect.pivot = new Vector2(0.5f, 0.5f);
                block.Image.raycastTarget = true;
                block.Button = go.AddComponent<Button>();
                block.Button.transition = Selectable.Transition.None;
                BlockView captured = block;
                block.Button.onClick.AddListener(() => _onTap?.Invoke(captured.Row, captured.Col));
            }
            block.Color = color;
            block.Image.sprite = BlockSprites.Get(color);
            block.Image.color = Color.white;
            block.Group.alpha = 1f;
            block.Rect.localScale = Vector3.one;
            block.Rect.localEulerAngles = Vector3.zero;
            block.Rect.SetAsLastSibling();
            return block;
        }

        private void Recycle(BlockView block)
        {
            if (block == null || block.Rect == null) return;
            Tween.Kill(block.Rect);
            block.Rect.gameObject.SetActive(false);
            _pool.Push(block);
        }

        private void ClearAll()
        {
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    Recycle(_blocks[r, c]);
                    _blocks[r, c] = null;
                }
            }
            foreach (BlockView block in _falling)
            {
                Recycle(block);
            }
            _falling.Clear();
        }

        // ------------------------------------------------------------------ animations

        /// <summary>
        /// Animates a tap or hammer: <paramref name="group"/> pops, then gravity and refill move the
        /// board from <paramref name="before"/> to <paramref name="after"/>. If the engine regenerated
        /// the whole board (no group left), the difference cannot be explained by gravity and a
        /// shuffle animation is used instead. <paramref name="onDone"/> runs when everything settled.
        /// </summary>
        public void AnimatePop(List<CellPos> group, int[][] before, int[][] after, Action onDone)
        {
            IsAnimating = true;
            var removed = new HashSet<int>();
            foreach (CellPos cell in group)
            {
                removed.Add(cell.Row * _cols + cell.Col);
                BlockView block = _blocks[cell.Row, cell.Col];
                _blocks[cell.Row, cell.Col] = null;
                if (block == null) continue;
                _falling.Add(block);
                BlockView captured = block;
                Vector3 position = block.Rect.position;
                Color color = UiTheme.BlockColor(block.Color);
                if (UiParticles.Instance != null)
                {
                    UiParticles.Instance.Burst(position, color, UnityEngine.Random.Range(6, 11), 620f + group.Count * 12f, 22f);
                }
                Tween.PopOut(block.Rect, block.Group, 1.15f, PopSeconds, 0f, () =>
                {
                    _falling.Remove(captured);
                    Recycle(captured);
                });
            }

            // Work out where every survivor lands; gravity keeps the column order, so the k
            // survivors of a column occupy its k bottom rows in the same order.
            bool explained = true;
            var plan = new List<(BlockView block, int fromRow, int toRow, int col)>();
            for (int c = 0; c < _cols && explained; c++)
            {
                var survivors = new List<int>();
                for (int r = 0; r < _rows; r++)
                {
                    if (!removed.Contains(r * _cols + c)) survivors.Add(r);
                }
                int k = survivors.Count;
                for (int i = 0; i < k; i++)
                {
                    int fromRow = survivors[i];
                    int toRow = _rows - k + i;
                    if (after[toRow][c] != before[fromRow][c])
                    {
                        explained = false;
                        break;
                    }
                    plan.Add((_blocks[fromRow, c], fromRow, toRow, c));
                }
            }

            if (!explained)
            {
                Tween.Delay(PopSeconds, () => AnimateShuffle(after, onDone), this);
                return;
            }

            // Detach survivors first so a block moving into a cell never overwrites one leaving it.
            foreach (var move in plan)
            {
                _blocks[move.fromRow, move.col] = null;
            }
            float longest = 0f;
            foreach (var move in plan)
            {
                BlockView block = move.block;
                _blocks[move.toRow, move.col] = block;
                if (block == null) continue;
                block.Row = move.toRow;
                block.Col = move.col;
                if (move.fromRow != move.toRow)
                {
                    float delay = FallDelay + move.col * ColumnStagger;
                    Tween.Move(block.Rect, CellPosition(move.toRow, move.col), FallSeconds, Ease.OutCubic, delay);
                    longest = Mathf.Max(longest, delay + FallSeconds);
                }
            }
            for (int c = 0; c < _cols; c++)
            {
                int missing = 0;
                for (int r = 0; r < _rows; r++)
                {
                    if (_blocks[r, c] == null) missing++;
                }
                for (int r = 0; r < missing; r++)
                {
                    BlockView block = Spawn(after[r][c]);
                    Place(block, r - missing, c); // start stacked above the board
                    block.Row = r;
                    block.Col = c;
                    _blocks[r, c] = block;
                    float delay = FallDelay + 0.04f + c * ColumnStagger + r * 0.015f;
                    Tween.Move(block.Rect, CellPosition(r, c), DropSeconds, Ease.OutBack, delay);
                    longest = Mathf.Max(longest, delay + DropSeconds);
                }
            }
            Tween.Delay(Mathf.Max(longest, PopSeconds) + 0.02f, () => FinishAnimation(onDone), this);
        }

        /// <summary>All blocks pop out in a wave, then the new board pops in (shuffle booster / regeneration).</summary>
        public void AnimateShuffle(int[][] after, Action onDone)
        {
            IsAnimating = true;
            int index = 0;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    BlockView block = _blocks[r, c];
                    _blocks[r, c] = null;
                    if (block == null) continue;
                    _falling.Add(block);
                    BlockView captured = block;
                    Tween.PopOut(block.Rect, block.Group, 1.1f, 0.16f, index * 0.006f, () =>
                    {
                        _falling.Remove(captured);
                        Recycle(captured);
                    });
                    index++;
                }
            }
            float outSeconds = index * 0.006f + 0.18f;
            Tween.Delay(outSeconds, () =>
            {
                int i = 0;
                for (int r = 0; r < _rows; r++)
                {
                    for (int c = 0; c < _cols; c++)
                    {
                        BlockView block = Spawn(after[r][c]);
                        Place(block, r, c);
                        _blocks[r, c] = block;
                        Tween.ScaleFrom(block.Rect, 0f, 0.28f, Ease.OutBack, i * 0.007f);
                        i++;
                    }
                }
                Tween.Delay(i * 0.007f + 0.3f, () => FinishAnimation(onDone), this);
            }, this);
        }

        /// <summary>Quick shake of one block (an illegal single-block tap).</summary>
        public void ShakeBlock(int row, int col)
        {
            BlockView block = _blocks[row, col];
            if (block != null)
            {
                Tween.Shake(block.Rect, 9f, 0.28f);
            }
        }

        /// <summary>Hammer mode: every block wiggles a little so the player knows to pick one.</summary>
        public void SetWiggle(bool on)
        {
            if (_wiggle == on) return;
            _wiggle = on;
            _wiggleDirty = true;
        }

        private void Update()
        {
            if (!_wiggle && !_wiggleDirty) return;
            float time = Time.unscaledTime;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    BlockView block = _blocks[r, c];
                    if (block == null) continue;
                    float angle = _wiggle ? Mathf.Sin(time * 9f + (r * _cols + c) * 0.7f) * 4f : 0f;
                    block.Rect.localEulerAngles = new Vector3(0f, 0f, angle);
                }
            }
            _wiggleDirty = false;
        }

        private void OnDestroy()
        {
            Tween.Kill(this);
        }
    }
}
