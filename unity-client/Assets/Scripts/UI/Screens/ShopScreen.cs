using System.Collections;
using System.Collections.Generic;
using BlastScale.Client.Audio;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// Booster and life purchases. Prices come from remote config (so an experiment can change
    /// them per player); every purchase carries an Idempotency-Key so a retried request cannot
    /// charge twice. Successful purchases pop the item card and count the wallet down.
    /// </summary>
    public sealed class ShopScreen : UiScreen
    {
        private RectTransform _coinsPill;
        private Text _coinsLabel;
        private Text _livesLabel;
        private Button _refillButton;
        private RectTransform _livesCard;
        private readonly Dictionary<string, Text> _ownedLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Text> _priceLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Button> _buyButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, RectTransform> _cards = new Dictionary<string, RectTransform>();
        private long _shownCoins = -1;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 16f, 36, 20, 28);
            RectTransform header = CreateHeader(column, "Shop", () => App.Flow.GoHome());
            _coinsPill = UiFactory.CreatePill(header, "CoinsPill", IconFactory.Coin(), Color.white, "-", out _coinsLabel, 84f, null, UiTheme.BodySize, 190f);

            UiFactory.CreateLabel(column, "BOOSTERS", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold, 34f);
            foreach (string type in BoosterTypes.All)
            {
                BuildBoosterCard(column, type);
            }

            UiFactory.CreateLabel(column, "LIVES", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold, 34f);
            _livesCard = UiFactory.CreateCard(column, "Lives", UiTheme.CardRadius, 22, 18, TextAnchor.MiddleLeft, null, true);
            UiFactory.CreateIcon(_livesCard, IconFactory.Heart(), Color.white, 96f);
            RectTransform texts = UiFactory.CreateRect(_livesCard, "Texts");
            UiFactory.AddVerticalLayout(texts, 4f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(texts.gameObject, flexibleWidth: 1f);
            UiFactory.CreateLabel(texts, "Refill lives", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
            _livesLabel = UiFactory.CreateLabel(texts, "", UiTheme.SmallSize - 2, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.Body);
            _refillButton = UiFactory.CreateButton(_livesCard, "Refill", OnRefill, ButtonStyle.Gold, UiTheme.SmallSize, 110f, 250f, IconFactory.Coin());

            UiFactory.CreateSpacer(column);
            UiFactory.CreateLabel(column, "Prices are served by remote config and may differ per experiment variant.",
                UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);
        }

        private void BuildBoosterCard(RectTransform column, string type)
        {
            RectTransform card = UiFactory.CreateCard(column, "Booster " + type, UiTheme.CardRadius, 22, 18, TextAnchor.MiddleLeft, null, true);
            _cards[type] = card;
            Sprite icon = type == BoosterTypes.Hammer ? IconFactory.Hammer() : type == BoosterTypes.Shuffle ? IconFactory.Shuffle() : IconFactory.Bolt();
            Color iconColor = type == BoosterTypes.Shuffle ? UiTheme.Sky : type == BoosterTypes.ExtraMoves ? UiTheme.Gold : Color.white;
            UiFactory.CreateIcon(card, icon, iconColor, 96f);

            RectTransform texts = UiFactory.CreateRect(card, "Texts");
            UiFactory.AddVerticalLayout(texts, 4f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(texts.gameObject, flexibleWidth: 1f);
            UiFactory.CreateLabel(texts, BoosterTypes.Label(type), UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
            UiFactory.CreateLabel(texts, Describe(type), UiTheme.TinySize, UiTheme.TextSoft, TextAnchor.MiddleLeft, UiFont.Body);
            _ownedLabels[type] = UiFactory.CreateLabel(texts, "", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold);

            string captured = type;
            Button buy = UiFactory.CreateButton(card, "Buy", () => Run(Buy(captured)), ButtonStyle.Blue, UiTheme.SmallSize, 110f, 250f, IconFactory.Coin());
            _buyButtons[type] = buy;
            _priceLabels[type] = buy.GetComponentInChildren<Text>();
        }

        private static string Describe(string type)
        {
            switch (type)
            {
                case BoosterTypes.Hammer: return "Smash any single block";
                case BoosterTypes.Shuffle: return "Regenerate the whole board";
                case BoosterTypes.ExtraMoves: return "Five extra moves, once per level";
                default: return "";
            }
        }

        protected override void OnShown()
        {
            Refresh(false);
            if (App.State.Config == null)
            {
                Run(LoadConfig());
            }
        }

        private IEnumerator LoadConfig()
        {
            var config = new ApiResult<ClientConfigResponse>();
            yield return App.Api.GetJson(ApiRoutes.Config, config);
            if (!IsAlive) yield break;
            if (config.Ok)
            {
                App.State.SetConfig(config.Value);
                Refresh(false);
            }
            else
            {
                App.Flow.ShowError(config.Error);
            }
        }

        private Dictionary<string, int> Prices()
        {
            ClientConfigResponse config = App.State.Config;
            return config != null
                ? config.GetIntMap(ConfigKeys.BoosterPrices, ConfigKeys.DefaultBoosterPrices)
                : ConfigKeys.DefaultBoosterPrices;
        }

        private int RefillPrice()
        {
            ClientConfigResponse config = App.State.Config;
            return config != null ? config.GetInt(ConfigKeys.LifeRefillPrice, ConfigKeys.DefaultLifeRefillPrice) : ConfigKeys.DefaultLifeRefillPrice;
        }

        private void Refresh(bool animate)
        {
            GameState state = App.State;
            WalletSnapshot wallet = state.Wallet;
            Dictionary<string, int> prices = Prices();
            long coins = wallet != null ? wallet.coins : 0;
            if (animate && _shownCoins >= 0 && coins != _shownCoins)
            {
                Tween.Float(_shownCoins, coins, 0.5f, v => _coinsLabel.text = TimeFormat.Number((long)v), Ease.OutCubic, 0f, null, _coinsLabel);
                Tween.Punch(_coinsPill, 0.15f, 0.35f);
            }
            else
            {
                _coinsLabel.text = wallet != null ? TimeFormat.Number(coins) : "-";
            }
            _shownCoins = coins;
            foreach (string type in BoosterTypes.All)
            {
                int price = prices.TryGetValue(type, out int p) ? p : 0;
                _ownedLabels[type].text = "Owned: " + state.BoosterCount(type);
                _priceLabels[type].text = price.ToString();
                UiFactory.SetButtonEnabled(_buyButtons[type], wallet == null || wallet.coins >= price);
            }
            if (wallet != null)
            {
                bool full = wallet.lives >= wallet.maxLives;
                _livesLabel.text = wallet.lives + "/" + wallet.maxLives + (full ? "  ·  full" : "  ·  next in " + TimeFormat.Countdown(state.NextLifeInSecondsNow));
                UiFactory.SetButtonEnabled(_refillButton, !full && wallet.coins >= RefillPrice());
            }
            else
            {
                _livesLabel.text = "-";
            }
            UiFactory.SetButtonLabel(_refillButton, RefillPrice().ToString());
        }

        private IEnumerator Buy(string type)
        {
            var result = new ApiResult<PurchaseResult>();
            yield return App.Flow.BuyBooster(type, 1, result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                AudioManager.Play(Sfx.CoinBurst, 1.1f);
                Tween.Punch(_cards[type], 0.06f, 0.4f);
                if (App.Fx != null) App.Fx.Sparkle(_cards[type].position, UiTheme.Sky, 12, 120f);
                App.Toast.Show("Bought " + result.Value.quantity + " x " + BoosterTypes.Label(type) + " for " + result.Value.coinsSpent + " coins");
                Refresh(true);
            }
            else
            {
                App.Flow.ShowError(result.Error);
            }
        }

        private void OnRefill()
        {
            WalletSnapshot wallet = App.State.Wallet;
            if (wallet != null && wallet.lives >= wallet.maxLives)
            {
                App.Toast.Show("Your lives are already full");
                return;
            }
            Run(Refill());
        }

        private IEnumerator Refill()
        {
            var result = new ApiResult<PurchaseResult>();
            yield return App.Flow.BuyLives(result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                AudioManager.Play(Sfx.CoinBurst, 1.1f);
                Tween.Punch(_livesCard, 0.06f, 0.4f);
                if (App.Fx != null) App.Fx.Sparkle(_livesCard.position, UiTheme.Heart, 14, 120f);
                App.Toast.Show("Lives refilled for " + result.Value.coinsSpent + " coins");
                Refresh(true);
            }
            else
            {
                App.Flow.ShowError(result.Error);
            }
        }
    }
}
