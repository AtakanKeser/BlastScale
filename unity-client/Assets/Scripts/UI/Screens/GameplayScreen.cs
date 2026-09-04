using System;
using System.Collections;
using System.Collections.Generic;
using BlastScale.Client.Audio;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI.Board;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using BlastScale.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// The board. Every tap is validated and applied by the local engine copy and recorded in the
    /// <see cref="LevelSession"/>; the server later replays the same moves to compute the score.
    /// The screen owns the "juice": animated pops with particles, score count-up, star pops,
    /// combo banners, booster feedback and the end-of-level hand-over. Rules enforced here for a
    /// good experience (the server enforces them again):
    /// <list type="bullet">
    ///   <item>a TAP needs a group of 2+ and a remaining move;</item>
    ///   <item>boosters can only be used while the player owns enough of them;</item>
    ///   <item>EXTRA_MOVES works once per attempt and adds 5 moves;</item>
    ///   <item>"Finish" appears once the target is reached; when moves run out the level is
    ///         submitted as won (target reached) or lost (after offering EXTRA_MOVES).</item>
    /// </list>
    /// </summary>
    public sealed class GameplayScreen : UiScreen
    {
        private LevelSession _session;
        private BoardView _board;
        private Text _scoreLabel;
        private Text _targetLabel;
        private Text _movesLabel;
        private RectTransform _movesPill;
        private Image _movesPillBody;
        private Image _progressFill;
        private Image _progressGlow;
        private readonly Image[] _stars = new Image[3];
        private Text _hintLabel;
        private Button _finishButton;
        private CanvasGroup _finishGroup;
        private Button _hammerButton;
        private Button _shuffleButton;
        private Button _extraButton;
        private Text _hammerBadge;
        private Text _shuffleBadge;
        private Text _extraBadge;
        private Text _banner;
        private CanvasGroup _bannerGroup;
        private RectTransform _boardCard;

        private bool _hammerArmed;
        private bool _ended;
        private bool _busy;
        private int _displayedScore;
        private int _shownStars;
        private bool _finishShown;
        private bool _lowMovesPulsing;
        private float _shownProgress;
        private TweenHandle _scoreTween;

        /// <summary>The session being played (tests read the board through it).</summary>
        public LevelSession Session => _session;

        /// <summary>True while an animation or a submission blocks input.</summary>
        public bool IsBusy => _busy || _ended || (_board != null && _board.IsAnimating);

        // ------------------------------------------------------------------ building

        protected override void Build(RectTransform root)
        {
            _session = App.State.Session;
            RectTransform column = CreateContentColumn(root, 14f, 28, 20, 28);
            if (_session == null)
            {
                UiFactory.CreateSpacer(column);
                UiFactory.CreateTitle(column, "No active level", UiTheme.HeadingSize, UiTheme.Text);
                UiFactory.CreateButton(column, "Home", () => App.Flow.GoHome(), ButtonStyle.Primary);
                UiFactory.CreateSpacer(column);
                return;
            }
            BuildTopRow(column);
            BuildScoreCard(column);
            BuildBoard(column);
            // The finish button sits between the board and the controls; it keeps its space while
            // hidden (scale 0) so nothing jumps when it bounces in.
            BuildFinishButton(column);
            _hintLabel = UiFactory.CreateLabel(column, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body, 36f);
            BuildBoosters(column);
            BuildBanner(root);
            _displayedScore = _session.Score;
            RefreshHud(false);
        }

        private void BuildTopRow(RectTransform column)
        {
            RectTransform row = UiFactory.CreateRow(column, "TopRow", UiTheme.IconButtonSize, 14f, TextAnchor.MiddleLeft);
            UiFactory.CreatePill(row, "LevelPill", null, Color.white, "Level " + _session.Level, out Text levelLabel, 84f,
                UiTheme.WithAlpha(UiTheme.Violet, 0.55f), UiTheme.BodySize + 2);
            _ = levelLabel;
            UiFactory.CreateSpacer(row);
            _movesPill = UiFactory.CreatePill(row, "MovesPill", IconFactory.Bolt(), UiTheme.Gold, "", out _movesLabel, 84f, null, UiTheme.BodySize + 8, 190f);
            _movesPillBody = _movesPill.Find("Body").GetComponent<Image>();
            UiFactory.CreateIconButton(row, "Quit", IconFactory.Close(), OnQuit, ButtonStyle.Ghost, 84f, null, 0.42f);
        }

        private void BuildScoreCard(RectTransform column)
        {
            RectTransform card = UiFactory.CreateCard(column, "ScoreCard", UiTheme.CardRadius, 24, 6);
            RectTransform top = UiFactory.CreateRow(card, "ScoreRow", 78f, 12f, TextAnchor.MiddleLeft);
            _scoreLabel = UiFactory.CreateTitle(top, "0", UiTheme.ScoreSize - 8, UiTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(_scoreLabel.gameObject, flexibleWidth: 1f);
            RectTransform starsRow = UiFactory.CreateRect(top, "Stars");
            UiFactory.AddHorizontalLayout(starsRow, 6f, 0, TextAnchor.MiddleRight);
            UiFactory.SetLayout(starsRow.gameObject, preferredWidth: 200f, flexibleWidth: 0f);
            Sprite star = IconFactory.Star();
            for (int i = 0; i < 3; i++)
            {
                _stars[i] = UiFactory.CreateIcon(starsRow, star, UiTheme.StarOff, i == 1 ? 64f : 54f);
            }
            RectTransform bottom = UiFactory.CreateRow(card, "TargetRow", 40f, 12f, TextAnchor.MiddleLeft);
            _targetLabel = UiFactory.CreateLabel(bottom, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold);
            UiFactory.SetLayout(_targetLabel.gameObject, flexibleWidth: 1f);
            UiFactory.CreateProgressBar(card, "Progress", 26f, UiTheme.Primary, out _progressFill, out _progressGlow);
        }

        /// <summary>
        /// The board sits in its own square card with an inner shadow; the view lays the blocks out
        /// itself. Spacers above and below take the column's leftover height (a little more below,
        /// so the board sits slightly above the centre like in most puzzle games).
        /// </summary>
        private void BuildBoard(RectTransform column)
        {
            UiFactory.CreateSpacer(column, 0.4f);
            _boardCard = UiFactory.CreateCard(column, "BoardCard", UiTheme.CardRadius, 16, 0, TextAnchor.MiddleCenter, UiTheme.BoardFill);
            UiFactory.SetLayout(_boardCard.gameObject, flexibleWidth: 1f);
            _boardCard.gameObject.AddComponent<BoardCardFitter>();
            Image inner = UiFactory.CreateImage(_boardCard, "InnerShadow", SpriteFactory.InnerShadow(UiTheme.CardRadius, 18f), new Color(0f, 0f, 0f, 0.45f));
            UiFactory.Stretch(inner.rectTransform);
            UiFactory.IgnoreLayout(inner.rectTransform);
            inner.transform.SetSiblingIndex(3);

            RectTransform area = UiFactory.CreateRect(_boardCard, "BoardArea");
            UiFactory.SetLayout(area.gameObject, flexibleHeight: 1f, flexibleWidth: 1f);
            _board = BoardView.Create(area, _session.Config.Rows, _session.Config.Cols, OnCellTapped);
            _board.SetSnapshot(_session.Board.Snapshot(), true);
            UiFactory.CreateSpacer(column, 0.6f);
        }

        private void BuildBoosters(RectTransform column)
        {
            RectTransform row = UiFactory.CreateRow(column, "Boosters", UiTheme.ButtonHeight, 14f);
            _hammerButton = UiFactory.CreateButton(row, "Hammer", OnHammer, ButtonStyle.Secondary, UiTheme.SmallSize, UiTheme.ButtonHeight, -1f, IconFactory.Hammer());
            _shuffleButton = UiFactory.CreateButton(row, "Shuffle", OnShuffle, ButtonStyle.Secondary, UiTheme.SmallSize, UiTheme.ButtonHeight, -1f, IconFactory.Shuffle());
            _extraButton = UiFactory.CreateButton(row, "+5 Moves", OnExtraMoves, ButtonStyle.Secondary, UiTheme.SmallSize, UiTheme.ButtonHeight, -1f, IconFactory.Bolt(), UiTheme.Gold);
            UiFactory.CreateBadge((RectTransform)_hammerButton.transform, "0", UiTheme.Blue, out _hammerBadge);
            UiFactory.CreateBadge((RectTransform)_shuffleButton.transform, "0", UiTheme.Blue, out _shuffleBadge);
            UiFactory.CreateBadge((RectTransform)_extraButton.transform, "0", UiTheme.Blue, out _extraBadge);
        }

        private void BuildFinishButton(RectTransform column)
        {
            _finishButton = UiFactory.CreateButton(column, "Finish level", OnFinish, ButtonStyle.Primary, UiTheme.HeadingSize - 6, UiTheme.ButtonHeight, -1f, IconFactory.Check());
            _finishGroup = _finishButton.GetComponent<CanvasGroup>();
            _finishGroup.alpha = 0f;
            _finishGroup.blocksRaycasts = false;
            _finishButton.transform.localScale = Vector3.zero;
        }

        /// <summary>The combo banner lives above everything in the screen and is invisible until a big group pops.</summary>
        private void BuildBanner(RectTransform root)
        {
            _banner = UiFactory.CreateTitle(root, "", 96, UiTheme.Gold, TextAnchor.MiddleCenter);
            UiFactory.AddOutline(_banner, 4f, new Color(0.25f, 0.05f, 0.35f, 0.9f));
            _banner.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rt = _banner.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.55f);
            rt.sizeDelta = new Vector2(1000f, 140f);
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>Updates score, progress, stars, moves and boosters; animates what changed.</summary>
        private void RefreshHud(bool animate = true)
        {
            int score = _session.Score;
            int target = _session.TargetScore;
            _targetLabel.text = "Target " + TimeFormat.Number(target);
            if (animate && score != _displayedScore)
            {
                _scoreTween.Kill();
                int from = _displayedScore;
                _scoreTween = Tween.Float(from, score, 0.45f, v =>
                {
                    _displayedScore = Mathf.RoundToInt(v);
                    _scoreLabel.text = TimeFormat.Number(_displayedScore);
                }, Ease.OutCubic, 0f, null, _scoreLabel);
                Tween.Punch(_scoreLabel.transform, 0.12f, 0.35f);
            }
            else
            {
                _displayedScore = score;
                _scoreLabel.text = TimeFormat.Number(score);
            }

            float progress = target > 0 ? Mathf.Clamp01(score / (float)target) : 1f;
            if (animate && !Mathf.Approximately(progress, _shownProgress))
            {
                Tween.Kill(_progressFill);
                Tween.Float(_shownProgress, progress, 0.4f, p =>
                {
                    _shownProgress = p;
                    UiFactory.SetProgress(_progressFill, p);
                }, Ease.OutCubic, 0f, null, _progressFill);
            }
            else if (!animate)
            {
                _shownProgress = progress;
                UiFactory.SetProgress(_progressFill, progress);
            }
            bool reached = _session.ObjectiveReached;
            Color glow = UiTheme.WithAlpha(UiTheme.Primary, reached ? 0.9f : 0f);
            if (animate) Tween.Tint(_progressGlow, glow, 0.4f); else _progressGlow.color = glow;
            if (reached && animate && !_finishShown)
            {
                Tween.Pulse(_progressGlow.transform, 0.02f, 1.4f);
            }

            RefreshStars(animate);

            int movesLeft = _session.MovesLeft;
            string movesText = movesLeft.ToString();
            if (_movesLabel.text != movesText)
            {
                _movesLabel.text = movesText;
                if (animate) Tween.Punch(_movesPill, 0.15f, 0.35f);
            }
            bool low = movesLeft <= 3 && !_ended;
            _movesPillBody.color = low ? UiTheme.WithAlpha(UiTheme.Danger, 0.75f) : new Color(0f, 0f, 0f, 0.32f);
            if (low && !_lowMovesPulsing)
            {
                _lowMovesPulsing = true;
                Tween.Pulse(_movesPillBody.transform, 0.06f, 0.9f);
            }
            else if (!low && _lowMovesPulsing)
            {
                _lowMovesPulsing = false;
                Tween.Kill(_movesPillBody.transform);
            }

            _hintLabel.text = _ended ? "Submitting..."
                : _hammerArmed ? "Hammer armed: tap any block to smash it"
                : reached ? "Target reached! Finish now or keep scoring"
                : "Tap groups of 2+ blocks of the same colour";

            RefreshBoosters();
            RefreshFinishButton(animate);
        }

        private void RefreshStars(bool animate)
        {
            int stars = _session.Stars;
            for (int i = 0; i < 3; i++)
            {
                bool lit = i < stars;
                Color target = lit ? UiTheme.Gold : UiTheme.StarOff;
                if (lit && i >= _shownStars && animate)
                {
                    Image star = _stars[i];
                    star.color = target;
                    Tween.ScaleFrom(star.transform, 0.2f, 0.45f, Ease.OutBack, i * 0.1f);
                    int captured = i;
                    Tween.Delay(i * 0.1f, () =>
                    {
                        AudioManager.Play(Sfx.StarChime, 1f + captured * 0.12f);
                        if (UiParticles.Instance != null) UiParticles.Instance.Sparkle(star.transform.position, UiTheme.Gold, 10, 40f);
                    }, star);
                }
                else
                {
                    _stars[i].color = target;
                }
            }
            _shownStars = stars;
        }

        private void RefreshFinishButton(bool animate)
        {
            bool show = _session.ObjectiveReached && !_ended;
            if (show && !_finishShown)
            {
                _finishShown = true;
                _finishGroup.blocksRaycasts = true;
                if (animate)
                {
                    Tween.Fade(_finishGroup, 1f, 0.2f);
                    Tween.ScaleFrom(_finishButton.transform, 0f, 0.5f, Ease.OutBack, 0f, () => Tween.Pulse(_finishButton.transform, 0.04f, 1.1f));
                    AudioManager.Play(Sfx.ComboSwell, 1.15f, 0.7f);
                }
                else
                {
                    _finishGroup.alpha = 1f;
                    _finishButton.transform.localScale = Vector3.one;
                }
            }
            else if (!show && _finishShown)
            {
                _finishShown = false;
                _finishGroup.blocksRaycasts = false;
                Tween.Kill(_finishButton.transform);
                Tween.Fade(_finishGroup, 0f, 0.15f);
                Tween.Scale(_finishButton.transform, 0f, 0.2f, Ease.InCubic);
            }
            UiFactory.SetButtonEnabled(_finishButton, show);
        }

        private void RefreshBoosters()
        {
            int hammers = Available(BoosterTypes.Hammer);
            int shuffles = Available(BoosterTypes.Shuffle);
            int extra = Available(BoosterTypes.ExtraMoves);
            UiFactory.SetButtonLabel(_hammerButton, _hammerArmed ? "Cancel" : "Hammer");
            UiFactory.SetButtonStyle(_hammerButton, _hammerArmed ? ButtonStyle.Gold : ButtonStyle.Secondary);
            UiFactory.SetButtonEnabled(_hammerButton, !_ended && (hammers > 0 || _hammerArmed));
            _hammerBadge.text = hammers.ToString();
            UiFactory.SetButtonEnabled(_shuffleButton, !_ended && shuffles > 0);
            _shuffleBadge.text = shuffles.ToString();
            UiFactory.SetButtonLabel(_extraButton, _session.ExtraMovesUsed ? "Used" : "+5 Moves");
            UiFactory.SetButtonEnabled(_extraButton, !_ended && !_session.ExtraMovesUsed && extra > 0);
            _extraBadge.text = extra.ToString();
            _board.SetWiggle(_hammerArmed && !_ended);
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

        /// <summary>"Great!" / "Awesome!" / "Unstoppable!" for big groups, with a rising chord and a shake for the biggest.</summary>
        private void ShowBanner(int groupSize)
        {
            string text = groupSize >= 12 ? "Unstoppable!" : groupSize >= 8 ? "Awesome!" : "Great!";
            Color color = groupSize >= 12 ? UiTheme.Pink : groupSize >= 8 ? UiTheme.Amber : UiTheme.Lime;
            _banner.text = text;
            _banner.color = color;
            Tween.Kill(_bannerGroup);
            Tween.Kill(_banner.transform);
            _bannerGroup.alpha = 1f;
            Tween.ScaleFrom(_banner.transform, 0.3f, 0.4f, Ease.OutBack);
            Tween.Fade(_bannerGroup, 0f, 0.3f, Ease.InQuad, 0.75f);
            AudioManager.Play(Sfx.ComboSwell, groupSize >= 12 ? 1.25f : groupSize >= 8 ? 1.12f : 1f);
            if (groupSize >= 8)
            {
                Tween.Shake(App.ScreenLayer, groupSize >= 12 ? 16f : 10f, 0.32f);
            }
        }

        // ------------------------------------------------------------------ input

        /// <summary>Test seam: same as tapping the block at (row, col).</summary>
        public void TapCell(int row, int col)
        {
            OnCellTapped(row, col);
        }

        /// <summary>Test seam: same as pressing "Finish level".</summary>
        public void FinishLevel()
        {
            OnFinish();
        }

        private void OnCellTapped(int row, int col)
        {
            if (IsBusy || App.Modal.IsOpen || !IsAlive)
            {
                return;
            }
            if (_hammerArmed)
            {
                UseHammer(row, col);
                return;
            }
            if (_session.OutOfMoves)
            {
                HandleOutOfMoves();
                return;
            }
            List<CellPos> group = _session.Board.GroupAt(row, col);
            if (group.Count < 2)
            {
                _board.ShakeBlock(row, col);
                AudioManager.Play(Sfx.Invalid, 1f, 0.7f);
                return;
            }
            int colorIndex = _session.Board.Cell(row, col);
            int[][] before = _session.Board.Snapshot();
            int scoreBefore = _session.Score;
            string problem = _session.Apply(Move.Tap(row, col));
            if (problem != null)
            {
                App.Toast.Show("Illegal move: " + problem, true);
                RefreshHud();
                return;
            }
            int[][] after = _session.Board.Snapshot();
            int gained = _session.Score - scoreBefore;
            _busy = true;
            AudioManager.PlayPop(group.Count);
            Vector3 centre = _board.GroupCentre(group);
            if (App.Fx != null)
            {
                App.Fx.ScorePopup(centre, "+" + TimeFormat.Number(gained), UiTheme.Lighten(UiTheme.BlockColor(colorIndex), 0.35f), group.Count >= 8 ? 64 : 52);
            }
            if (group.Count >= 5)
            {
                ShowBanner(group.Count);
            }
            _board.AnimatePop(group, before, after, () =>
            {
                _busy = false;
                if (!IsAlive) return;
                RefreshHud();
                CheckEndConditions();
            });
            RefreshHud();
        }

        private void UseHammer(int row, int col)
        {
            _hammerArmed = false;
            if (Available(BoosterTypes.Hammer) <= 0)
            {
                App.Toast.Show("No hammers left", true);
                RefreshHud();
                return;
            }
            int[][] before = _session.Board.Snapshot();
            string problem = _session.Apply(Move.Hammer(row, col));
            if (problem != null)
            {
                App.Toast.Show("Illegal move: " + problem, true);
                RefreshHud();
                return;
            }
            int[][] after = _session.Board.Snapshot();
            _busy = true;
            AudioManager.Play(Sfx.BoosterUse);
            AudioManager.Play(Sfx.Pop, 0.8f);
            var group = new List<CellPos> { new CellPos(row, col) };
            _board.AnimatePop(group, before, after, () =>
            {
                _busy = false;
                if (!IsAlive) return;
                RefreshHud();
                CheckEndConditions();
            });
            RefreshHud();
        }

        private void OnHammer()
        {
            if (IsBusy) return;
            if (!_hammerArmed && Available(BoosterTypes.Hammer) <= 0)
            {
                App.Toast.Show("No hammers left. Buy some in the shop.", true);
                return;
            }
            _hammerArmed = !_hammerArmed;
            if (_hammerArmed) AudioManager.Play(Sfx.BoosterUse, 0.9f, 0.6f);
            RefreshHud();
        }

        private void OnShuffle()
        {
            if (IsBusy) return;
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
                RefreshHud();
                return;
            }
            _busy = true;
            AudioManager.Play(Sfx.BoosterUse);
            AudioManager.Play(Sfx.Whoosh, 1.2f, 0.6f);
            _board.AnimateShuffle(_session.Board.Snapshot(), () =>
            {
                _busy = false;
                if (!IsAlive) return;
                RefreshHud();
            });
            RefreshHud();
        }

        private void OnExtraMoves()
        {
            if (IsBusy) return;
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
            AudioManager.Play(Sfx.BoosterUse, 1.1f);
            App.Toast.Show("+5 moves added");
            RefreshHud();
            Tween.Punch(_movesPill, 0.3f, 0.5f);
        }

        private void OnFinish()
        {
            if (_ended || _busy) return;
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

        /// <summary>After each move settles: auto-finish when the moves are gone.</summary>
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
                        AudioManager.Play(Sfx.BoosterUse, 1.1f);
                        RefreshHud();
                        Tween.Punch(_movesPill, 0.3f, 0.5f);
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
            RefreshHud();
            var result = new ApiResult<LevelCompleteResponse>();
            yield return App.Flow.SubmitCompletion(_session, result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Screens.Show(new ResultScreen(_session, result.Value));
                yield break;
            }
            _ended = false;
            RefreshHud();
            ShowSubmitError(result.Error, () => Run(SubmitWin()));
        }

        private IEnumerator SubmitLoss()
        {
            if (_ended) yield break;
            _ended = true;
            _hammerArmed = false;
            RefreshHud();
            var result = new ApiResult<LevelFailResponse>();
            yield return App.Flow.SubmitFailure(_session, result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Screens.Show(new ResultScreen(_session, result.Value));
                yield break;
            }
            _ended = false;
            RefreshHud();
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

        protected override void OnDismissed()
        {
            if (_board != null) _board.SetWiggle(false);
        }
    }
}
