using System;
using System.Collections;
using System.Collections.Generic;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// The board. Every tap is validated and applied by the local engine copy and recorded in the
    /// <see cref="LevelSession"/>; the server later replays the same moves to compute the score.
    /// Rules enforced here for a good experience (the server enforces them again):
    /// <list type="bullet">
    ///   <item>a TAP needs a group of 2+ and a remaining move;</item>
    ///   <item>boosters can only be used while the player owns enough of them;</item>
    ///   <item>EXTRA_MOVES works once per attempt and adds 5 moves;</item>
    ///   <item>"Finish" unlocks once the target is reached; when moves run out the level is
    ///         submitted as won (target reached) or lost (after offering EXTRA_MOVES).</item>
    /// </list>
    /// </summary>
    public sealed class GameplayScreen : UiScreen
    {
        private const float CellSpacing = 8f;

        private LevelSession _session;
        private Text _scoreLabel;
        private Text _movesLabel;
        private Text _starsLabel;
        private Text _statusLabel;
        private Button _finishButton;
        private Button _hammerButton;
        private Button _shuffleButton;
        private Button _extraButton;
        private Image[,] _cells;
        private bool _hammerArmed;
        private bool _ended;

        protected override void Build(RectTransform root)
        {
            _session = App.State.Session;
            RectTransform column = CreateContentColumn(root, 16f, 24);
            if (_session == null)
            {
                UiFactory.CreateLabel(column, "No active level", UiTheme.HeadingSize, UiTheme.Text);
                UiFactory.CreateButton(column, "Home", () => App.Flow.GoHome(), UiTheme.Accent);
                return;
            }

            BuildHud(column);
            BuildBoard(column);
            BuildBoosters(column);
            _finishButton = UiFactory.CreateButton(column, "Finish level", OnFinish, UiTheme.Success, UiTheme.HeadingSize, 120f);
            Refresh();
        }

        private void BuildHud(RectTransform column)
        {
            Image hud = UiFactory.CreatePanel(column, "Hud", UiTheme.Panel);
            UiFactory.SetLayout(hud.gameObject, preferredHeight: 250f, minHeight: 250f);
            UiFactory.AddVerticalLayout(hud.rectTransform, 6f, 20);

            RectTransform top = UiFactory.CreateRow(hud.transform, "TopRow", 80f);
            Text level = UiFactory.CreateLabel(top, "Level " + _session.Level, UiTheme.HeadingSize, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(level.gameObject, flexibleWidth: 1f);
            UiFactory.CreateButton(top, "Quit", OnQuit, UiTheme.Secondary, UiTheme.SmallSize, 70f, 160f);

            RectTransform mid = UiFactory.CreateRow(hud.transform, "MidRow", 60f);
            _scoreLabel = UiFactory.CreateLabel(mid, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(_scoreLabel.gameObject, flexibleWidth: 1f);
            _movesLabel = UiFactory.CreateLabel(mid, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleRight);
            UiFactory.SetLayout(_movesLabel.gameObject, flexibleWidth: 1f);

            RectTransform bottom = UiFactory.CreateRow(hud.transform, "BottomRow", 60f);
            _starsLabel = UiFactory.CreateLabel(bottom, "", UiTheme.HeadingSize, UiTheme.Warning, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(_starsLabel.gameObject, preferredWidth: 220f);
            _statusLabel = UiFactory.CreateLabel(bottom, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleRight);
            UiFactory.SetLayout(_statusLabel.gameObject, flexibleWidth: 1f);
        }

        /// <summary>A centred grid of coloured buttons; the cell size follows the free area (BoardGridFitter).</summary>
        private void BuildBoard(RectTransform column)
        {
            int rows = _session.Config.Rows;
            int cols = _session.Config.Cols;

            RectTransform area = UiFactory.CreateRect(column, "BoardArea");
            UiFactory.SetLayout(area.gameObject, flexibleHeight: 1f, flexibleWidth: 1f);

            RectTransform gridRect = UiFactory.CreateRect(area, "Grid");
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            GridLayoutGroup grid = UiFactory.AddGrid(gridRect, cols, 100f, CellSpacing);
            var fitter = gridRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sizer = area.gameObject.AddComponent<BoardGridFitter>();
            sizer.Grid = grid;
            sizer.Rows = rows;
            sizer.Cols = cols;
            sizer.Spacing = CellSpacing;

            _cells = new Image[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    _cells[r, c] = CreateCell(gridRect, r, c);
                }
            }
        }

        private Image CreateCell(Transform parent, int row, int col)
        {
            RectTransform rt = UiFactory.CreateRect(parent, "Cell " + row + "," + col);
            var image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;
            int r = row;
            int c = col;
            button.onClick.AddListener(() => OnCellTapped(r, c));
            return image;
        }

        private void BuildBoosters(RectTransform column)
        {
            RectTransform row = UiFactory.CreateRow(column, "Boosters", 110f, 12f);
            _hammerButton = UiFactory.CreateButton(row, "Hammer", OnHammer, UiTheme.Secondary, UiTheme.SmallSize);
            _shuffleButton = UiFactory.CreateButton(row, "Shuffle", OnShuffle, UiTheme.Secondary, UiTheme.SmallSize);
            _extraButton = UiFactory.CreateButton(row, "+5 Moves", OnExtraMoves, UiTheme.Secondary, UiTheme.SmallSize);
        }

        // ------------------------------------------------------------------ rendering

        private void Refresh()
        {
            BoardState board = _session.Board;
            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Cols; c++)
                {
                    _cells[r, c].color = UiTheme.BlockColor(board.Cell(r, c));
                }
            }
            _scoreLabel.text = "Score " + TimeFormat.Number(_session.Score) + " / " + TimeFormat.Number(_session.TargetScore);
            _movesLabel.text = "Moves left: " + _session.MovesLeft;
            _starsLabel.text = TimeFormat.Stars(_session.Stars);
            if (_ended)
            {
                _statusLabel.text = "Submitting...";
            }
            else if (_hammerArmed)
            {
                _statusLabel.text = "Hammer armed: tap any block to remove it";
            }
            else if (_session.ObjectiveReached)
            {
                _statusLabel.text = "Target reached! Finish now or keep scoring";
            }
            else
            {
                _statusLabel.text = "Tap groups of 2+ same-color blocks";
            }
            RefreshBoosters();
            _finishButton.interactable = _session.ObjectiveReached && !_ended;
        }

        private void RefreshBoosters()
        {
            int hammers = Available(BoosterTypes.Hammer);
            int shuffles = Available(BoosterTypes.Shuffle);
            int extra = Available(BoosterTypes.ExtraMoves);
            UiFactory.SetButtonLabel(_hammerButton, (_hammerArmed ? "Cancel hammer" : "Hammer") + " (" + hammers + ")");
            _hammerButton.image.color = _hammerArmed ? UiTheme.Warning : UiTheme.Secondary;
            _hammerButton.interactable = !_ended && (hammers > 0 || _hammerArmed);
            UiFactory.SetButtonLabel(_shuffleButton, "Shuffle (" + shuffles + ")");
            _shuffleButton.interactable = !_ended && shuffles > 0;
            UiFactory.SetButtonLabel(_extraButton, _session.ExtraMovesUsed ? "+5 Moves (used)" : "+5 Moves (" + extra + ")");
            _extraButton.interactable = !_ended && !_session.ExtraMovesUsed && extra > 0;
        }

        /// <summary>Owned boosters minus the ones already used in this attempt (charged by the server at the end).</summary>
        private int Available(string boosterType)
        {
            int owned = App.State.BoosterCount(boosterType);
            int used;
            switch (boosterType)
            {
                case BoosterTypes.Hammer: used = _session.Board.HammersUsed; break;
                case BoosterTypes.Shuffle: used = _session.Board.ShufflesUsed; break;
                default: used = _session.ExtraMovesUsed ? 1 : 0; break;
            }
            return Math.Max(0, owned - used);
        }

        // ------------------------------------------------------------------ input

        private void OnCellTapped(int row, int col)
        {
            if (_ended || App.Modal.IsOpen)
            {
                return;
            }
            string problem;
            if (_hammerArmed)
            {
                _hammerArmed = false;
                if (Available(BoosterTypes.Hammer) <= 0)
                {
                    App.Toast.Show("No hammers left", true);
                    Refresh();
                    return;
                }
                problem = _session.Apply(Move.Hammer(row, col));
            }
            else
            {
                if (_session.OutOfMoves)
                {
                    HandleOutOfMoves();
                    return;
                }
                List<CellPos> group = _session.Board.GroupAt(row, col);
                if (group.Count < 2)
                {
                    App.Toast.Show("Tap a group of 2 or more blocks of the same color", false, 1.5f);
                    return;
                }
                problem = _session.Apply(Move.Tap(row, col));
            }
            if (problem != null)
            {
                App.Toast.Show("Illegal move: " + problem, true);
                Refresh();
                return;
            }
            Refresh();
            CheckEndConditions();
        }

        private void OnHammer()
        {
            if (_ended) return;
            if (!_hammerArmed && Available(BoosterTypes.Hammer) <= 0)
            {
                App.Toast.Show("No hammers left. Buy some in the shop.", true);
                return;
            }
            _hammerArmed = !_hammerArmed;
            Refresh();
        }

        private void OnShuffle()
        {
            if (_ended) return;
            if (Available(BoosterTypes.Shuffle) <= 0)
            {
                App.Toast.Show("No shuffles left. Buy some in the shop.", true);
                return;
            }
            _hammerArmed = false;
            string problem = _session.Apply(Move.Shuffle());
            if (problem != null)
            {
                App.Toast.Show("Illegal move: " + problem, true);
            }
            Refresh();
        }

        private void OnExtraMoves()
        {
            if (_ended) return;
            if (_session.ExtraMovesUsed)
            {
                App.Toast.Show("+5 Moves can only be used once per level", true);
                return;
            }
            if (Available(BoosterTypes.ExtraMoves) <= 0)
            {
                App.Toast.Show("No +5 Moves boosters left. Buy some in the shop.", true);
                return;
            }
            _session.ActivateExtraMoves();
            App.Toast.Show("+5 moves added");
            Refresh();
        }

        private void OnFinish()
        {
            if (_ended) return;
            if (!_session.ObjectiveReached)
            {
                App.Toast.Show("Reach the target score first", true);
                return;
            }
            Run(SubmitWin());
        }

        private void OnQuit()
        {
            if (_ended) return;
            App.Modal.Show("Give up?", "The life spent on this attempt is not refunded.",
                ModalButton.Danger("Give up", () => Run(SubmitLoss())),
                ModalButton.Secondary("Keep playing", null));
        }

        // ------------------------------------------------------------------ end of level

        /// <summary>After each move: auto-finish when the moves are gone.</summary>
        private void CheckEndConditions()
        {
            if (!_session.OutOfMoves)
            {
                return;
            }
            if (_session.ObjectiveReached)
            {
                Run(SubmitWin());
                return;
            }
            HandleOutOfMoves();
        }

        /// <summary>Offers the EXTRA_MOVES booster when the player owns one, otherwise the level is lost.</summary>
        private void HandleOutOfMoves()
        {
            if (_ended || App.Modal.IsOpen)
            {
                return;
            }
            if (!_session.ExtraMovesUsed && Available(BoosterTypes.ExtraMoves) > 0)
            {
                App.Modal.Show("Out of moves", "Use a +5 Moves booster to keep going?",
                    ModalButton.Primary("Use +5 Moves", () =>
                    {
                        _session.ActivateExtraMoves();
                        Refresh();
                    }),
                    ModalButton.Secondary("Give up", () => Run(SubmitLoss())));
                return;
            }
            Run(SubmitLoss());
        }

        private IEnumerator SubmitWin()
        {
            if (_ended) yield break;
            _ended = true;
            _hammerArmed = false;
            Refresh();
            var result = new ApiResult<LevelCompleteResponse>();
            yield return App.Flow.SubmitCompletion(_session, result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Screens.Show(new ResultScreen(_session, result.Value));
                yield break;
            }
            _ended = false;
            Refresh();
            ShowSubmitError(result.Error, () => Run(SubmitWin()));
        }

        private IEnumerator SubmitLoss()
        {
            if (_ended) yield break;
            _ended = true;
            _hammerArmed = false;
            Refresh();
            var result = new ApiResult<LevelFailResponse>();
            yield return App.Flow.SubmitFailure(_session, result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Screens.Show(new ResultScreen(_session, result.Value));
                yield break;
            }
            _ended = false;
            Refresh();
            ShowSubmitError(result.Error, () => Run(SubmitLoss()));
        }

        /// <summary>Transient failures offer a retry (same Idempotency-Key); rejections only lead home.</summary>
        private void ShowSubmitError(ApiException error, Action retry)
        {
            bool transient = error.IsNetworkError || error.HttpStatus >= 500 ||
                             error.Code == "IDEMPOTENT_REQUEST_IN_PROGRESS" || error.Code == "CONCURRENT_MODIFICATION";
            if (transient)
            {
                App.Modal.Show("Could not submit the level", error.Message,
                    ModalButton.Primary("Retry", retry),
                    ModalButton.Secondary("Home", () => App.Flow.GoHome()));
            }
            else
            {
                App.Modal.Show("Level rejected by the server", error.Message + "\n(" + error.Code + ")",
                    ModalButton.Primary("Home", () => App.Flow.GoHome()));
            }
        }
    }
}
